using System;
using System.IO;
using System.Text.RegularExpressions;

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

        public static GameStatus Inspect(string gameFolder)
        {
            var s = new GameStatus();
            if (!IsGameFolder(gameFolder)) { s.Note = "no game folder"; return s; }
            s.HasGame = true;
            s.HasBackup = LatestBackup(gameFolder, DatName) != null;
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
        public static ApplyOutcome ApplyRenderFix(string gameFolder, int width, int height, DateTime now)
        {
            var outc = new ApplyOutcome();
            string dat = Path.Combine(gameFolder, DatName);
            string ini = Path.Combine(gameFolder, IniName);
            if (!File.Exists(dat)) { outc.Message = "angel.dat not found in the selected folder."; return outc; }

            byte[] bytes = File.ReadAllBytes(dat);
            PatchResult pr = RenderPatch.Apply(bytes, width, height);
            if (!pr.Success) { outc.Message = pr.Message; return outc; }

            string stamp = now.ToString("yyyyMMdd-HHmmss");
            outc.DatBackup = dat + "." + stamp + BakSuffix;
            File.Copy(dat, outc.DatBackup, true);
            File.WriteAllBytes(dat, bytes);

            if (File.Exists(ini))
            {
                outc.IniBackup = ini + "." + stamp + BakSuffix;
                File.Copy(ini, outc.IniBackup, true);
                string fixedIni = FixCore.ApplyResolution(File.ReadAllText(ini), width, height);
                File.WriteAllText(ini, fixedIni);
            }

            outc.Success = true;
            outc.Message = "Patched angel.dat and midage.ini for " + width + "x" + height + ".";
            return outc;
        }

        // Restore the most recent FixRes backup of angel.dat (and midage.ini).
        public static ApplyOutcome RevertLast(string gameFolder)
        {
            var outc = new ApplyOutcome();
            string datBak = LatestBackup(gameFolder, DatName);
            if (datBak == null) { outc.Message = "No FixRes backup found to revert to."; return outc; }
            File.Copy(datBak, Path.Combine(gameFolder, DatName), true);
            outc.DatBackup = datBak;
            string iniBak = LatestBackup(gameFolder, IniName);
            if (iniBak != null) { File.Copy(iniBak, Path.Combine(gameFolder, IniName), true); outc.IniBackup = iniBak; }
            outc.Success = true;
            outc.Message = "Reverted from " + Path.GetFileName(datBak) + ".";
            return outc;
        }
    }
}
