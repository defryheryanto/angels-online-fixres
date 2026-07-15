using System;
using System.Collections.Generic;
using System.Globalization;

namespace AngelsFixRes
{
    // Pure logic: choose a resolution and edit midage.ini keys. No file or UI
    // dependencies, so this is fully unit-testable.
    public static class FixCore
    {
        public struct Resolution
        {
            public int Width;
            public int Height;
            public Resolution(int w, int h) { Width = w; Height = h; }
            public override string ToString() { return Width + "x" + Height; }
        }

        // The game ships native UI layouts (save/cfg/wob_WxH.xml) for exactly
        // these three resolutions, so these are the safe native targets.
        public static readonly Resolution[] SupportedResolutions = new Resolution[]
        {
            new Resolution(1280, 720),
            new Resolution(1600, 900),
            new Resolution(1920, 1080),
        };

        // Largest supported resolution that fits within the monitor.
        // Falls back to the smallest supported resolution if none fit.
        public static Resolution ChooseDefaultResolution(int monitorW, int monitorH)
        {
            Resolution best = SupportedResolutions[0];
            bool found = false;
            foreach (Resolution r in SupportedResolutions)
            {
                if (r.Width <= monitorW && r.Height <= monitorH)
                {
                    if (!found || r.Width > best.Width) { best = r; found = true; }
                }
            }
            return best;
        }

        // How to drive a given monitor in v1.2 fill mode.
        public struct FillPlan
        {
            public int RenderW, RenderH;   // the DIB render size (a shipped layout the engine stretches up)
            public int NativeW, NativeH;   // the window / fullscreen stretch target (the monitor)
            public bool UseFill;           // true = fullscreen fill; false = fall back to windowed
            public FillPlan(int rw, int rh, int nw, int nh, bool fill)
            { RenderW = rw; RenderH = rh; NativeW = nw; NativeH = nh; UseFill = fill; }
        }

        static bool IsSupported(int w, int h)
        {
            foreach (Resolution r in SupportedResolutions)
                if (r.Width == w && r.Height == h) return true;
            return false;
        }

        // Plan the fix for a monitor. The engine composites its world into a fixed, art-baked
        // surface (<=1080p) and its Direct2D present STRETCHES that render up to fill the window -
        // so the way to fill any big screen is to render at a shipped layout and let the engine
        // scale it, NOT to enlarge the render (that only grows the buffer, the scene stays small
        // and lands in the top-left = the black-bars bug). So:
        //   - above 1080p: render 1920x1080 (a shipped HUD layout) and fill the whole screen. 16:9
        //     panels fill with no distortion; wider/taller panels fill with a mild stretch (still
        //     fullscreen, still a real HUD layout - which is what the player actually wants).
        //   - at/below 1080p: render the monitor 1:1 if it is itself a shipped layout (crisp, no
        //     scaling); otherwise there is nothing to fill cleanly, so fall back to windowed.
        public static FillPlan PlanFill(int nativeW, int nativeH)
        {
            if (nativeW <= 1920 && nativeH <= 1080)
            {
                if (IsSupported(nativeW, nativeH)) return new FillPlan(nativeW, nativeH, nativeW, nativeH, true);
                return new FillPlan(0, 0, nativeW, nativeH, false);
            }
            return new FillPlan(1920, 1080, nativeW, nativeH, true);
        }

        // The clamped render size the engine actually produces for a given window size: the
        // largest WxH that fits inside the 1920x1080 render box at the window's aspect ratio,
        // never upscaled above the window, floored to even dimensions (matching the engine's
        // internal aspect-fit). The in-game resolution list is pinned to exactly this
        // (SetResolutionList) so its single entry equals the real render and cannot float the HUD.
        public static void RenderSize(int windowW, int windowH, out int renderW, out int renderH)
        {
            const int boxW = 1920, boxH = 1080;
            if (windowW <= 0 || windowH <= 0) { renderW = boxW; renderH = boxH; return; }
            double s = Math.Min((double)boxW / windowW, (double)boxH / windowH);
            if (s > 1.0) s = 1.0;   // never render larger than the window (engine caps at <=1080p)
            renderW = ((int)(windowW * s)) & ~1;
            renderH = ((int)(windowH * s)) & ~1;
        }

        // Integer value of a key, or null if the key is absent or non-numeric.
        public static int? ReadIntKey(string iniText, string key)
        {
            foreach (string line in SplitLines(iniText))
            {
                string k, v;
                if (TryParseKeyValue(line, out k, out v) &&
                    string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    int parsed;
                    if (int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                        return parsed;
                    return null;
                }
            }
            return null;
        }

        private static readonly string[] WidthKeys = { "GameWndSizeWidth", "ScreenWidth" };
        private static readonly string[] HeightKeys = { "GameWndSizeHeight", "ScreenHeight" };
        // Set the resolution keys to the target, preserving every other line.
        // Missing keys are inserted immediately after the [OPTION] header. When
        // gameWndFullScreen is non-null the GameWndFullScreen key is set to it
        // (0 = windowed, 1 = fullscreen).
        public static string ApplyResolution(string iniText, int width, int height, int? gameWndFullScreen = null,
            int screenLeft = 0, int screenTop = 0)
        {
            var targets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string k in WidthKeys) targets[k] = width;
            foreach (string k in HeightKeys) targets[k] = height;
            targets["ScreenLeft"] = screenLeft;
            targets["ScreenTop"] = screenTop;
            // Optionally pin the window mode. Windowed (0) keeps the window at the
            // 1920x1080 draw region (v1.1 fallback). Fullscreen (1) is used by fill
            // mode: the window is the native monitor size while the render underneath
            // is smaller, so the engine stretches the render across the whole screen.
            if (gameWndFullScreen.HasValue) targets["GameWndFullScreen"] = gameWndFullScreen.Value;

            string newline = iniText.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = iniText.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int optionHeaderIndex = -1;
            var result = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (optionHeaderIndex < 0 &&
                    line.Trim().Equals("[OPTION]", StringComparison.OrdinalIgnoreCase))
                {
                    optionHeaderIndex = result.Count;
                }

                string k, v;
                if (TryParseKeyValue(line, out k, out v) && targets.ContainsKey(k))
                {
                    result.Add(ReplaceValuePreservingFormat(line, targets[k]));
                    seen.Add(k);
                }
                else
                {
                    result.Add(line);
                }
            }

            var missing = new List<string>();
            foreach (var kv in targets)
            {
                if (!seen.Contains(kv.Key))
                    missing.Add(kv.Key + " = " + kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (missing.Count > 0)
            {
                int insertAt = optionHeaderIndex >= 0 ? optionHeaderIndex + 1 : result.Count;
                result.InsertRange(insertAt, missing);
            }

            return string.Join(newline, result.ToArray());
        }

        // Replace only the value in "Key = Value", keeping the key and spacing.
        private static string ReplaceValuePreservingFormat(string line, int value)
        {
            int eq = line.IndexOf('=');
            if (eq < 0) return line;
            string left = line.Substring(0, eq + 1); // includes '='
            string right = line.Substring(eq + 1);
            string leading = "";
            int p = 0;
            while (p < right.Length && (right[p] == ' ' || right[p] == '\t')) { leading += right[p]; p++; }
            if (leading.Length == 0) leading = " ";
            return left + leading + value.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryParseKeyValue(string line, out string key, out string value)
        {
            key = null; value = null;
            if (line == null) return false;
            string t = line.TrimStart();
            if (t.StartsWith("[") || t.StartsWith(";") || t.StartsWith("#")) return false;
            int eq = line.IndexOf('=');
            if (eq <= 0) return false;
            key = line.Substring(0, eq).Trim();
            value = line.Substring(eq + 1).Trim();
            return key.Length != 0;
        }

        private static string[] SplitLines(string text)
        {
            return text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
    }
}
