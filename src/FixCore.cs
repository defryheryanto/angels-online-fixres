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
        private static readonly string[] ZeroKeys = { "ScreenLeft", "ScreenTop" };

        // Set the six resolution keys to the target, preserving every other line.
        // Missing keys are inserted immediately after the [OPTION] header.
        public static string ApplyResolution(string iniText, int width, int height)
        {
            var targets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string k in WidthKeys) targets[k] = width;
            foreach (string k in HeightKeys) targets[k] = height;
            foreach (string k in ZeroKeys) targets[k] = 0;

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
