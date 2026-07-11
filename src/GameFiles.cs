using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AngelsFixRes
{
    public class GameStatus
    {
        public bool HasGame;
        public bool Recognized;   // client build understood by the patcher
        public bool Applied;      // fix already in place
        public int CurrentW, CurrentH;   // current top candidate (render target)
        public bool HasBackup;    // a FixRes backup exists to revert to
        public string Note = "";
    }

    public class ApplyOutcome
    {
        public bool Success;
        public string Message = "";
        public string DatBackup = "";
        public string IniBackup = "";
    }

    // Locates the game folder and applies / reverts the render fix.
    public static class GameFiles
    {
        public const string IniName = "midage.ini";
        public const string DatName = "angel.dat";
        public const string RegName = "reg.ini";
        public const string LauncherName = "START.EXE";
        const string BakSuffix = ".fixres.bak";   // our own backups, distinct from other tools'

        public static bool IsGameFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;
            return File.Exists(Path.Combine(folder, DatName)) && File.Exists(Path.Combine(folder, IniName));
        }

        public static string ReadGameDirFromReg(string regIniPath)
        {
            if (!File.Exists(regIniPath)) return null;
            foreach (string line in File.ReadAllLines(regIniPath))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (string.Equals(line.Substring(0, eq).Trim(), "GameDir", StringComparison.OrdinalIgnoreCase))
                {
                    string val = line.Substring(eq + 1).Trim();
                    return val.Length > 0 ? val : null;
                }
            }
            return null;
        }

        public static string AutoDetect(string toolDir)
        {
            if (IsGameFolder(toolDir)) return toolDir;
            string gameDir = ReadGameDirFromReg(Path.Combine(toolDir, RegName));
            if (gameDir != null && IsGameFolder(gameDir)) return gameDir;
            return null;
        }

        public static bool DatLocked(string gameFolder)
        {
            string dat = Path.Combine(gameFolder, DatName);
            if (!File.Exists(dat)) return false;
            try { using (new FileStream(dat, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } return false; }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }

        // Newest FixRes backup for a given base file (by the timestamp in the name).
        public static string LatestBackup(string folder, string baseName)
        {
            var re = new Regex("^" + Regex.Escape(baseName) + @"\.(\d{8}-\d{6})" + Regex.Escape(BakSuffix) + "$", RegexOptions.IgnoreCase);
            string best = null, bestStamp = null;
            try
            {
                foreach (string f in Directory.GetFiles(folder, baseName + ".*" + BakSuffix))
                {
                    Match m = re.Match(Path.GetFileName(f));
                    if (m.Success && (bestStamp == null || string.CompareOrdinal(m.Groups[1].Value, bestStamp) > 0))
                    { bestStamp = m.Groups[1].Value; best = f; }
                }
            }
            catch { }
            return best;
        }

        // Back up path -> path.<stamp>.fixres.bak, but ONLY when the client was pristine
        // (wasPatched == false). Re-applying on an already-patched file must never capture the
        // patched state as a "backup" - otherwise Revert would restore a still-fixed client.
        // When already patched, keep (and return) the existing clean backup.
        static string BackupOnce(string path, bool wasPatched, string stamp)
        {
            if (!wasPatched)
            {
                string bak = path + "." + stamp + BakSuffix;
                File.Copy(path, bak, true);
                return bak;
            }
            return LatestBackup(Path.GetDirectoryName(path), Path.GetFileName(path));   // null if none (UI handles it)
        }

        // Newest FixRes backup of baseName that is the CURRENT build (matching size) AND is a
        // clean, unpatched client. Skips patched backups left by older tool builds.
        static string NewestCleanBackup(string folder, string baseName, long size)
        {
            var re = new Regex("^" + Regex.Escape(baseName) + @"\.(\d{8}-\d{6})" + Regex.Escape(BakSuffix) + "$", RegexOptions.IgnoreCase);
            var cands = new List<KeyValuePair<string, string>>();   // stamp -> path
            try
            {
                foreach (string f in Directory.GetFiles(folder, baseName + ".*" + BakSuffix))
                {
                    Match m = re.Match(Path.GetFileName(f));
                    if (m.Success) cands.Add(new KeyValuePair<string, string>(m.Groups[1].Value, f));
                }
            }
            catch { return null; }
            cands.Sort((a, b) => string.CompareOrdinal(b.Key, a.Key));   // newest first
            foreach (var c in cands)
            {
                try
                {
                    if (size > 0 && new FileInfo(c.Value).Length != size) continue;   // must be this client build
                    if (!RenderPatch.Inspect(File.ReadAllBytes(c.Value)).Applied) return c.Value;   // clean
                }
                catch { }
            }
            return null;
        }

        public static GameStatus Inspect(string gameFolder)
        {
            var s = new GameStatus();
            if (!IsGameFolder(gameFolder)) { s.Note = "no game folder"; return s; }
            s.HasGame = true;
            // HasBackup gates the Revert button, so it must mean "a CLEAN backup Revert can actually
            // use" - not merely "some .bak exists" (which could be all patched, making Revert fail).
            long datSize = 0;
            try { datSize = new FileInfo(Path.Combine(gameFolder, DatName)).Length; } catch { }
            s.HasBackup = NewestCleanBackup(gameFolder, DatName, datSize) != null;
            try
            {
                byte[] dat = File.ReadAllBytes(Path.Combine(gameFolder, DatName));
                InspectResult r = RenderPatch.Inspect(dat);
                s.Recognized = r.Recognized;
                s.Applied = r.Applied;
                s.CurrentW = r.Candidate0W;
                s.CurrentH = r.Candidate0H;
                s.Note = r.Detail;
            }
            catch (Exception ex) { s.Note = ex.Message; }
            return s;
        }

        // Back up both files, patch angel.dat for WxH, and set midage.ini.
        public static ApplyOutcome ApplyRenderFix(string gameFolder, int width, int height, DateTime now, bool forceWindowed = false)
        {
            var outc = new ApplyOutcome();
            // Windowed fallback (non-16:9 monitors, or a fill render that is not a supported layout):
            // render at <=1920x1080 windowed. forceWindowed makes the window a 1920x1080 region rather
            // than the whole monitor, so a mismatched fullscreen stretch never leaves black bars.
            if (width > 1920) width = 1920;
            if (height > 1080) height = 1080;
            string dat = Path.Combine(gameFolder, DatName);
            string ini = Path.Combine(gameFolder, IniName);
            if (!File.Exists(dat)) { outc.Message = "angel.dat not found in the selected folder."; return outc; }

            byte[] bytes = File.ReadAllBytes(dat);
            bool wasPatched = RenderPatch.Inspect(bytes).Applied;   // is the current client already patched?
            PatchResult pr = RenderPatch.Apply(bytes, width, height);
            if (!pr.Success) { outc.Message = pr.Message; return outc; }
            // Pin the in-game Display list to the render size so changing it can't float the HUD.
            int rlW, rlH; FixCore.RenderSize(width, height, out rlW, out rlH);
            RenderPatch.SetResolutionList(bytes, rlW, rlH);
            // Same borderless edits as the fill/native paths: keep rendering when tabbed out, no
            // minimize, no display-mode switch, crisp scaling, and neutralise the in-game res-apply.
            // These edits are mode-independent, so the windowed fallback benefits too - and it makes
            // the "resolution is managed by the tool" caution honest on this path as well.
            RenderPatch.ApplyBorderless(bytes);

            string stamp = now.ToString("yyyyMMdd-HHmmss");
            outc.DatBackup = BackupOnce(dat, wasPatched, stamp);   // clean backup only, never a patched one
            File.WriteAllBytes(dat, bytes);

            if (File.Exists(ini))
            {
                outc.IniBackup = BackupOnce(ini, wasPatched, stamp);
                string fixedIni = FixCore.ApplyResolution(File.ReadAllText(ini), width, height, forceWindowed ? (int?)0 : null);
                File.WriteAllText(ini, fixedIni);
            }
            SetDpiAware(dat, true);   // game renders at physical pixels (crisp) at any Windows scaling

            outc.Success = true;
            outc.Message = "Patched angel.dat and midage.ini for " + width + "x" + height + ".";
            return outc;
        }

        // Fill mode (v1.2, >1080p 16:9 monitors): render at renderWxrenderH (a supported
        // layout, native/2 for a clean 2x), but set the window to the NATIVE monitor size
        // and go fullscreen, so the engine stretches the small render across the whole
        // screen. Integer scaling = sharp AND no black bars, and the render stays <=1920 so
        // the client never crashes (the native size is rejected by the render clamp and only
        // takes effect as the window / stretch target).
        public static ApplyOutcome ApplyFillFix(string gameFolder, int renderW, int renderH, int nativeW, int nativeH, DateTime now)
        {
            var outc = new ApplyOutcome();
            string dat = Path.Combine(gameFolder, DatName);
            string ini = Path.Combine(gameFolder, IniName);
            if (!File.Exists(dat)) { outc.Message = "angel.dat not found in the selected folder."; return outc; }

            byte[] bytes = File.ReadAllBytes(dat);
            bool wasPatched = RenderPatch.Inspect(bytes).Applied;   // is the current client already patched?
            PatchResult pr = RenderPatch.Apply(bytes, renderW, renderH);
            if (!pr.Success) { outc.Message = pr.Message; return outc; }
            // Collapse the in-game Display list to a single entry = the ACTUAL render size (the
            // engine's aspect-fit of the native window into the 1920x1080 box). With no other
            // size to switch to, an in-game "Apply" is a no-op, so the fixed HUD can't float.
            int rlW, rlH; FixCore.RenderSize(nativeW, nativeH, out rlW, out rlH);
            RenderPatch.SetResolutionList(bytes, rlW, rlH);
            // Borderless windowed fullscreen + crisp scaling: fills the screen, keeps rendering
            // when tabbed out, and nearest-neighbour upscales sharply.
            RenderPatch.ApplyBorderless(bytes);

            string stamp = now.ToString("yyyyMMdd-HHmmss");
            outc.DatBackup = BackupOnce(dat, wasPatched, stamp);   // clean backup only, never a patched one
            File.WriteAllBytes(dat, bytes);

            if (File.Exists(ini))
            {
                outc.IniBackup = BackupOnce(ini, wasPatched, stamp);
                string fixedIni = FixCore.ApplyResolution(File.ReadAllText(ini), nativeW, nativeH, 1);
                File.WriteAllText(ini, fixedIni);
            }
            SetDpiAware(dat, true);   // game renders at physical pixels (crisp) at any Windows scaling

            outc.Success = true;
            outc.Message = "Fill mode (borderless): render " + renderW + "x" + renderH + " stretched to " + nativeW + "x" + nativeH + ".";
            return outc;
        }

        // Native mode (opt-in): render at the monitor's OWN resolution (aspect-matched, 1:1, crisp -
        // no upscale). The render buffers realloc from the render globals, so raising the clamp to
        // renderWxrenderH is safe up to the GPU's max texture size (the caller checks that first).
        // Also writes a scaled UI layout so the HUD repositions to the larger canvas instead of
        // clustering top-left. renderW/renderH are the monitor's native resolution (16:9 or 21:9).
        public static ApplyOutcome ApplyNativeFix(string gameFolder, int renderW, int renderH, DateTime now)
        {
            var outc = new ApplyOutcome();
            string dat = Path.Combine(gameFolder, DatName);
            string ini = Path.Combine(gameFolder, IniName);
            if (!File.Exists(dat)) { outc.Message = "angel.dat not found in the selected folder."; return outc; }

            byte[] bytes = File.ReadAllBytes(dat);
            bool wasPatched = RenderPatch.Inspect(bytes).Applied;
            // Raise the render clamp to the native resolution: render == monitor, aspect-matched, 1:1.
            PatchResult pr = RenderPatch.Apply(bytes, renderW, renderH, renderW, renderH);
            if (!pr.Success) { outc.Message = pr.Message; return outc; }
            RenderPatch.SetResolutionList(bytes, renderW, renderH);   // in-game list = the native render
            RenderPatch.ApplyBorderless(bytes);

            string stamp = now.ToString("yyyyMMdd-HHmmss");
            outc.DatBackup = BackupOnce(dat, wasPatched, stamp);
            File.WriteAllBytes(dat, bytes);

            if (File.Exists(ini))
            {
                outc.IniBackup = BackupOnce(ini, wasPatched, stamp);
                string fixedIni = FixCore.ApplyResolution(File.ReadAllText(ini), renderW, renderH, 1);
                File.WriteAllText(ini, fixedIni);
            }
            SetDpiAware(dat, true);
            GenerateScaledWob(gameFolder, renderW, renderH);

            outc.Success = true;
            outc.Message = "Native mode: rendering at " + renderW + "x" + renderH + ".";
            return outc;
        }

        // Write save\cfg\wob_<W>_<H>.xml (the HUD window layout the client loads for that render size)
        // by scaling the shipped 1920x1080 layout's window origins to the target canvas, so the UI
        // sits at proportional positions instead of clustering in the top-left. Only <window> x/y are
        // scaled; <custom> values are preserved verbatim. Best-effort (never blocks the fix).
        public static void GenerateScaledWob(string gameFolder, int w, int h)
        {
            try
            {
                string cfg = Path.Combine(gameFolder, "save", "cfg");
                string src = Path.Combine(cfg, "wob_1920_1080.xml");
                if (!File.Exists(src)) return;
                string xml = File.ReadAllText(src);
                double sx = w / 1920.0, sy = h / 1080.0;
                // Scale x/y ONLY inside <window ...> opening tags - never a <custom> value= or any
                // other attribute. Process each window tag, then scale its x/y within it.
                xml = Regex.Replace(xml, "<window\\b[^>]*>", wm =>
                {
                    string tag = wm.Value;
                    tag = Regex.Replace(tag, "x=\"(\\d+)\"", m => "x=\"" + (int)Math.Round(int.Parse(m.Groups[1].Value) * sx) + "\"");
                    tag = Regex.Replace(tag, "y=\"(\\d+)\"", m => "y=\"" + (int)Math.Round(int.Parse(m.Groups[1].Value) * sy) + "\"");
                    return tag;
                });
                File.WriteAllText(Path.Combine(cfg, "wob_" + w + "_" + h + ".xml"), xml, new System.Text.UTF8Encoding(false));
            }
            catch { }
        }

        // Restore the newest CLEAN (unpatched) FixRes backup of the current client build, and the
        // midage.ini backed up in the same apply. Skipping patched backups is what makes Revert
        // actually undo the fix (older tool builds left patched backups that Revert would restore).
        public static ApplyOutcome RevertLast(string gameFolder)
        {
            var outc = new ApplyOutcome();
            string dat = Path.Combine(gameFolder, DatName);
            if (!File.Exists(dat)) { outc.Message = "angel.dat not found in the selected folder."; return outc; }
            string datBak = NewestCleanBackup(gameFolder, DatName, new FileInfo(dat).Length);
            if (datBak == null) { outc.Message = "No clean (unpatched) backup of this client build was found to revert to."; return outc; }
            File.Copy(datBak, dat, true);
            outc.DatBackup = datBak;
            // Prefer the midage.ini backed up in the SAME apply (same timestamp) as the chosen client.
            Match m = Regex.Match(Path.GetFileName(datBak), @"\.(\d{8}-\d{6})" + Regex.Escape(BakSuffix) + "$");
            string iniBak = m.Success ? Path.Combine(gameFolder, IniName + "." + m.Groups[1].Value + BakSuffix) : null;
            if (iniBak == null || !File.Exists(iniBak)) iniBak = LatestBackup(gameFolder, IniName);
            if (iniBak != null && File.Exists(iniBak)) { File.Copy(iniBak, Path.Combine(gameFolder, IniName), true); outc.IniBackup = iniBak; }
            SetDpiAware(dat, false);   // remove the high-DPI compatibility flag
            outc.Success = true;
            outc.Message = "Reverted to a clean backup (" + Path.GetFileName(datBak) + ").";
            return outc;
        }

        // Make (or un-make) the game render at physical pixels on high-DPI / scaled displays via
        // the Windows per-user AppCompat "high DPI aware" flag on the angel.dat image path. This is
        // pure OS configuration (no binary patch, no injection, instantly reversible): with it, a
        // DPI-unaware GDI client stops being bitmap-upscaled by Windows (soft) and instead sizes its
        // window to the real screen and scales its own render (crisp). Best-effort; a registry
        // failure never blocks the fix.
        const string LayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        static void SetDpiAware(string datPath, bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(LayersKey))
                {
                    if (k == null) return;
                    if (on)
                    {
                        string existing = k.GetValue(datPath) as string ?? "";
                        if (existing.IndexOf("HIGHDPIAWARE", StringComparison.OrdinalIgnoreCase) < 0)
                            k.SetValue(datPath, "~ HIGHDPIAWARE", RegistryValueKind.String);
                    }
                    else if (k.GetValue(datPath) != null)
                    {
                        k.DeleteValue(datPath, false);
                    }
                }
            }
            catch { }
        }
    }
}
