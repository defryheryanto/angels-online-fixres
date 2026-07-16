using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace AngelsFixRes
{
    // POC launcher: every profile owns a complete client sandbox.  Nothing in the
    // source installation is opened for writing, so five clients may use five INIs
    // and five differently-patched angel.dat files concurrently.
    public static class MultiClientPoc
    {
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after,
            int x, int y, int cx, int cy, uint flags);
        const uint SWP_NOSIZE = 1, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10;

        static void Usage()
        {
            Console.WriteLine("Usage: MultiClientPoc.exe <game-folder> <sandbox-root> <profile> <screen-number> [windowed|borderless]");
            Console.WriteLine("Example: MultiClientPoc.exe \"C:\\UserJoy\\Angels Online Global\" .\\clients client1 1 borderless");
        }

        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }

        static void CopyTree(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(target, dir.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar)));
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".fixres.bak", StringComparison.OrdinalIgnoreCase)) continue;
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                string destination = Path.Combine(target, relative);
                if (!File.Exists(destination)) File.Copy(file, destination, false);
            }
        }

        static HashSet<int> ExistingClients()
        {
            var ids = new HashSet<int>();
            foreach (Process p in Process.GetProcessesByName("angel")) { ids.Add(p.Id); p.Dispose(); }
            return ids;
        }

        static void MoveNewClient(HashSet<int> oldIds, Screen screen)
        {
            for (int attempt = 0; attempt < 240; attempt++)
            {
                foreach (Process p in Process.GetProcessesByName("angel"))
                {
                    try
                    {
                        if (oldIds.Contains(p.Id) || p.MainWindowHandle == IntPtr.Zero) continue;
                        for (int i = 0; i < 20; i++)
                        {
                            p.Refresh();
                            if (p.MainWindowHandle != IntPtr.Zero)
                                SetWindowPos(p.MainWindowHandle, IntPtr.Zero, screen.Bounds.Left, screen.Bounds.Top,
                                    0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                            Thread.Sleep(250);
                        }
                        Console.WriteLine("Client process {0} moved to screen {1}.", p.Id, Array.IndexOf(Screen.AllScreens, screen) + 1);
                        return;
                    }
                    finally { p.Dispose(); }
                }
                Thread.Sleep(250);
            }
            Console.WriteLine("Launcher started, but no new angel window appeared within 60 seconds.");
        }

        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length < 4 || args.Length > 5) { Usage(); return 2; }
            string source = Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar);
            string root = Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);
            string profile = args[2];
            int screenNumber;
            if (!int.TryParse(args[3], out screenNumber) || screenNumber < 1 || screenNumber > Screen.AllScreens.Length)
            { Console.Error.WriteLine("Screen number must be between 1 and " + Screen.AllScreens.Length + "."); return 2; }
            bool windowed = args.Length == 5 && args[4].Equals("windowed", StringComparison.OrdinalIgnoreCase);
            if (profile.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || profile == "." || profile == "..")
            { Console.Error.WriteLine("Invalid profile name."); return 2; }
            if (!GameFiles.IsGameFolder(source)) { Console.Error.WriteLine("Source is not an Angels Online game folder."); return 2; }
            string sandbox = Path.Combine(root, profile);
            if (source.Equals(sandbox, StringComparison.OrdinalIgnoreCase) || sandbox.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            { Console.Error.WriteLine("Sandbox root must be outside the game installation."); return 2; }

            string sourceDatHash = Hash(Path.Combine(source, GameFiles.DatName));
            string sourceIniHash = Hash(Path.Combine(source, GameFiles.IniName));
            Console.WriteLine(Directory.Exists(sandbox) ? "Reusing sandbox: " + sandbox : "Creating isolated sandbox: " + sandbox);
            CopyTree(source, sandbox);

            Screen screen = Screen.AllScreens[screenNumber - 1];
            int nativeW = screen.Bounds.Width, nativeH = screen.Bounds.Height;
            FixCore.FillPlan plan = FixCore.PlanFill(nativeW, nativeH);
            int renderW = plan.UseFill ? plan.RenderW : Math.Min(nativeW, 1920);
            int renderH = plan.UseFill ? plan.RenderH : Math.Min(nativeH, 1080);
            ApplyOutcome outcome = windowed
                ? GameFiles.ApplyFillFix(sandbox, renderW, renderH, nativeW, nativeH, DateTime.Now, true, nativeW, nativeH)
                : (plan.UseFill ? GameFiles.ApplyFillFix(sandbox, plan.RenderW, plan.RenderH, nativeW, nativeH, DateTime.Now)
                    : GameFiles.ApplyRenderFix(sandbox, renderW, renderH, DateTime.Now, true));
            if (!outcome.Success) { Console.Error.WriteLine(outcome.Message); return 1; }

            // A hard safety assertion: fail before launch if anything changed in the installation.
            if (sourceDatHash != Hash(Path.Combine(source, GameFiles.DatName)) || sourceIniHash != Hash(Path.Combine(source, GameFiles.IniName)))
            { Console.Error.WriteLine("SAFETY CHECK FAILED: source installation changed; refusing to launch."); return 1; }

            string launcher = Path.Combine(sandbox, GameFiles.LauncherName);
            if (!File.Exists(launcher)) { Console.Error.WriteLine("START.EXE is missing from the sandbox."); return 1; }
            HashSet<int> oldIds = ExistingClients();
            Process.Start(new ProcessStartInfo(launcher) { WorkingDirectory = sandbox, UseShellExecute = true });
            Console.WriteLine("Profile {0}: {1}x{2}, screen {3}, {4}.", profile, nativeW, nativeH, screenNumber, windowed ? "windowed" : "borderless");
            MoveNewClient(oldIds, screen);
            return 0;
        }
    }
}
