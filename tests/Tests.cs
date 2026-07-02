using System;
using System.IO;
using AngelsFixRes;

public static class Tests
{
    static int failures = 0;
    static void Check(bool c, string n) { Console.WriteLine((c ? "PASS " : "FAIL ") + n); if (!c) failures++; }

    // A pristine client binary is needed for the binary-patch tests. Tests that
    // need it are skipped (not failed) when it is not present.
    const string PristineDat = @"C:\UserJoy\Angels Online Global\angel.dat.20260702-012951.rendersize.bak";

    const string Sample =
        "[OPTION]\r\nMusicVolume = 0\r\nGameWndSizeWidth = 1920\r\nGameWndSizeHeight = 1080\r\n" +
        "GameWndFullScreen = 0\r\nScreenLeft = 0\r\nScreenTop = 0\r\nScreenWidth = 1280\r\nScreenHeight = 720\r\n" +
        "[SYSTEM]\r\nSwitchscreen = 1\r\n";

    static void TestIni()
    {
        string o = FixCore.ApplyResolution(Sample, 2560, 1440);
        Check(FixCore.ReadIntKey(o, "ScreenWidth") == 2560, "ini ScreenWidth set");
        Check(FixCore.ReadIntKey(o, "ScreenHeight") == 1440, "ini ScreenHeight set");
        Check(FixCore.ReadIntKey(o, "GameWndSizeWidth") == 2560, "ini GameWndSizeWidth set");
        Check(o.Contains("MusicVolume = 0") && o.Contains("[SYSTEM]"), "ini preserves other lines");
    }

    static void TestRenderPatch()
    {
        if (!File.Exists(PristineDat)) { Console.WriteLine("SKIP RenderPatch (no pristine client binary)"); return; }
        byte[] d = File.ReadAllBytes(PristineDat);
        InspectResult i0 = RenderPatch.Inspect(d);
        Check(i0.Recognized, "pristine recognised");
        Check(!i0.Applied, "pristine not yet applied");
        Check(i0.Candidate0W == 3840 && i0.Candidate0H == 2160, "pristine top candidate 3840x2160");
        Check(RenderPatch.Apply(d, 1920, 1080).Success, "apply 1920x1080 success");
        InspectResult i1 = RenderPatch.Inspect(d);
        Check(i1.Applied, "after apply -> applied");
        Check(i1.Candidate0W == 1920 && i1.Candidate0H == 1080, "after apply top candidate 1920x1080");
        Check(RenderPatch.Apply(d, 1920, 1080).Success, "re-apply is idempotent");
        byte[] d3 = File.ReadAllBytes(PristineDat);
        RenderPatch.Apply(d3, 2560, 1440);
        InspectResult i3 = RenderPatch.Inspect(d3);
        Check(i3.Candidate0W == 2560 && i3.Candidate0H == 1440, "per-resolution: 2560x1440");
    }

    static void TestGameFiles()
    {
        if (!File.Exists(PristineDat)) { Console.WriteLine("SKIP GameFiles (no pristine client binary)"); return; }
        string tmp = Path.Combine(Path.GetTempPath(), "aofix_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            File.Copy(PristineDat, Path.Combine(tmp, "angel.dat"));
            File.WriteAllText(Path.Combine(tmp, "midage.ini"), Sample);
            Check(GameFiles.IsGameFolder(tmp), "IsGameFolder true (dat + ini)");
            Check(GameFiles.AutoDetect(tmp) == tmp, "AutoDetect finds own folder");
            ApplyOutcome o = GameFiles.ApplyRenderFix(tmp, 1920, 1080, new DateTime(2026, 7, 2, 1, 0, 0));
            Check(o.Success, "ApplyRenderFix success");
            Check(File.Exists(o.DatBackup), "angel.dat backup created");
            Check(File.Exists(o.IniBackup), "midage.ini backup created");
            GameStatus st = GameFiles.Inspect(tmp);
            Check(st.Applied && st.CurrentW == 1920, "inspect reports applied at 1920");
            Check(st.HasBackup, "inspect reports a backup exists");
            Check(FixCore.ReadIntKey(File.ReadAllText(Path.Combine(tmp, "midage.ini")), "ScreenWidth") == 1920, "midage.ini updated on disk");

            ApplyOutcome rev = GameFiles.RevertLast(tmp);
            Check(rev.Success, "RevertLast success");
            GameStatus st2 = GameFiles.Inspect(tmp);
            Check(!st2.Applied && st2.CurrentW == 3840, "after revert -> unpatched (candidate 3840 restored)");
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    public static int Main()
    {
        TestIni();
        TestRenderPatch();
        TestGameFiles();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : (failures + " TEST(S) FAILED"));
        return failures == 0 ? 0 : 1;
    }
}
