using System;
using System.IO;
using AngelsFixRes;

public static class Tests
{
    static int failures = 0;
    static void Check(bool c, string n) { Console.WriteLine((c ? "PASS " : "FAIL ") + n); if (!c) failures++; }

    // A pristine client binary is needed for the binary-patch tests. Tests that
    // need it are skipped (not failed) when it is not present.
    const string PristineDat = @"C:\UserJoy\Angels Online Global\angel.dat.pristine-4774912.bak";

    const string Sample =
        "[OPTION]\r\nMusicVolume = 0\r\nGameWndSizeWidth = 1920\r\nGameWndSizeHeight = 1080\r\n" +
        "GameWndFullScreen = 1\r\nScreenLeft = 0\r\nScreenTop = 0\r\nScreenWidth = 1280\r\nScreenHeight = 720\r\n" +
        "[SYSTEM]\r\nSwitchscreen = 1\r\n";

    static void TestIni()
    {
        string o = FixCore.ApplyResolution(Sample, 2560, 1440);
        Check(FixCore.ReadIntKey(o, "ScreenWidth") == 2560, "ini ScreenWidth set");
        Check(FixCore.ReadIntKey(o, "ScreenHeight") == 1440, "ini ScreenHeight set");
        Check(FixCore.ReadIntKey(o, "GameWndSizeWidth") == 2560, "ini GameWndSizeWidth set");
        Check(o.Contains("MusicVolume = 0") && o.Contains("[SYSTEM]"), "ini preserves other lines");
        Check(FixCore.ReadIntKey(o, "GameWndFullScreen") == 1, "ini leaves GameWndFullScreen alone by default");
        string w = FixCore.ApplyResolution(Sample, 1920, 1080, 0);
        Check(FixCore.ReadIntKey(w, "GameWndFullScreen") == 0, "ini forces windowed when asked");
        string fs = FixCore.ApplyResolution(Sample, 3840, 2160, 1);
        Check(FixCore.ReadIntKey(fs, "GameWndFullScreen") == 1, "ini forces fullscreen when asked");
        string secondScreen = FixCore.ApplyResolution(Sample, 2560, 1440, 1, 1920, -120);
        Check(FixCore.ReadIntKey(secondScreen, "ScreenLeft") == 1920, "ini targets selected screen left");
        Check(FixCore.ReadIntKey(secondScreen, "ScreenTop") == -120, "ini targets selected screen top");
    }

    static void TestPlanFill()
    {
        // Above 1080p: always render the shipped 1920x1080 layout and let the engine stretch it
        // to fill the whole screen (the scene is art-baked <=1080p, so a bigger render can't help).
        FixCore.FillPlan p4k = FixCore.PlanFill(3840, 2160);
        Check(p4k.UseFill && p4k.RenderW == 1920 && p4k.RenderH == 1080 && p4k.NativeW == 3840, "4K -> fill, render 1920x1080");
        FixCore.FillPlan p1440 = FixCore.PlanFill(2560, 1440);
        Check(p1440.UseFill && p1440.RenderW == 1920 && p1440.RenderH == 1080, "1440p -> fill, render 1920x1080");
        FixCore.FillPlan p1800 = FixCore.PlanFill(3200, 1800);
        Check(p1800.UseFill && p1800.RenderW == 1920 && p1800.RenderH == 1080, "1800p -> fill, render 1920x1080");
        FixCore.FillPlan p1080 = FixCore.PlanFill(1920, 1080);
        Check(p1080.UseFill && p1080.RenderW == 1920 && p1080.RenderH == 1080, "1080p -> render 1:1 (shipped layout)");
        // Non-16:9 above 1080p now FILLS too (fullscreen with a mild stretch) instead of a small window.
        FixCore.FillPlan p5k2k = FixCore.PlanFill(5120, 2160);
        Check(p5k2k.UseFill && p5k2k.RenderW == 1920 && p5k2k.NativeW == 5120 && p5k2k.NativeH == 2160, "5K2K 21:9 5120x2160 -> fill (render 1920x1080, stretched)");
        Check(FixCore.PlanFill(5120, 2880).UseFill, "5K 16:9 5120x2880 -> fill (was a small window)");
        Check(FixCore.PlanFill(3440, 1440).UseFill, "ultrawide 3440x1440 -> fill (was a small window)");
        Check(FixCore.PlanFill(1920, 1200).UseFill, "16:10 1920x1200 -> fill (was a small window)");
        // At/below 1080p: a shipped layout renders 1:1; a non-layout size still falls back to windowed.
        FixCore.FillPlan p900 = FixCore.PlanFill(1600, 900);
        Check(p900.UseFill && p900.RenderW == 1600 && p900.RenderH == 900, "1600x900 -> render 1:1 (shipped layout)");
        Check(!FixCore.PlanFill(1366, 768).UseFill, "1366x768 -> no fill (not a shipped layout, <=1080p)");
        Check(!FixCore.PlanFill(1280, 1024).UseFill, "1280x1024 5:4 -> no fill (not a shipped layout, <=1080p)");

        int rw, rh;
        FixCore.RenderSize(3840, 2160, out rw, out rh); Check(rw == 1920 && rh == 1080, "RenderSize 4K -> 1920x1080");
        FixCore.RenderSize(2560, 1440, out rw, out rh); Check(rw == 1920 && rh == 1080, "RenderSize 1440p -> 1920x1080");
        FixCore.RenderSize(1920, 1080, out rw, out rh); Check(rw == 1920 && rh == 1080, "RenderSize 1080p -> 1920x1080 (1:1)");
        FixCore.RenderSize(1920, 1200, out rw, out rh); Check(rw == 1728 && rh == 1080, "RenderSize 16:10 1920x1200 -> aspect-fit 1728x1080");
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
        // critical: the >box aspect-fit TARGET must be raised too (else fill renders 1280x720, not 1920x1080)
        Check(d[0x30236F] == 0x80 && d[0x302370] == 0x07 && d[0x3023AC] == 0x38 && d[0x3023AD] == 0x04, "aspect-fit target raised to 1920x1080 (fill is 1920, not 720p)");
        Check(RenderPatch.Apply(d, 1920, 1080).Success, "re-apply is idempotent");
        byte[] d3 = File.ReadAllBytes(PristineDat);
        RenderPatch.Apply(d3, 1600, 900);
        InspectResult i3 = RenderPatch.Inspect(d3);
        Check(i3.Candidate0W == 1600 && i3.Candidate0H == 900, "per-resolution: 1600x900");
        byte[] d4 = File.ReadAllBytes(PristineDat);
        RenderPatch.Apply(d4, 2560, 1440);
        InspectResult i4 = RenderPatch.Inspect(d4);
        Check(i4.Candidate0W == 1920 && i4.Candidate0H == 1080, "above 1080p is capped to 1920x1080");
        // SetResolutionList collapses the in-game Display list to a single entry = the render size
        byte[] d5 = File.ReadAllBytes(PristineDat);
        RenderPatch.Apply(d5, 1920, 1080);
        RenderPatch.SetResolutionList(d5, 1600, 900);
        InspectResult i5 = RenderPatch.Inspect(d5);
        Check(i5.Candidate0W == 1600 && i5.Candidate0H == 900, "SetResolutionList sets entry0 = render size");
        Check(d5[0x3D1F88] == 0x01 && d5[0x3D1F89] == 0x00 && d5[0x3D1F8A] == 0x00 && d5[0x3D1F8B] == 0x00, "SetResolutionList sets in-game list count = 1");

        // native mode: Apply with an explicit clamp ceiling raises the render clamp ABOVE 1080p
        byte[] dn = File.ReadAllBytes(PristineDat);
        Check(RenderPatch.Apply(dn, 3840, 2160, 3840, 2160).Success, "native apply 3840x2160 success");
        Check(dn[0x30232F] == 0x00 && dn[0x302330] == 0x0F && dn[0x302331] == 0x00 && dn[0x302332] == 0x00, "native: render clamp W raised to 3840");
        Check(dn[0x302337] == 0x70 && dn[0x302338] == 0x08 && dn[0x302339] == 0x00 && dn[0x30233A] == 0x00, "native: render clamp H raised to 2160");
        InspectResult iN = RenderPatch.Inspect(dn);
        Check(iN.Applied && iN.Candidate0W == 3840 && iN.Candidate0H == 2160, "native: entry0 = 3840x2160 (uncapped)");
        Check(dn[0x30236F] == 0x00 && dn[0x302370] == 0x0F && dn[0x3023AC] == 0x70 && dn[0x3023AD] == 0x08, "native: aspect-fit target raised to 3840x2160");

        // 5K / ultrawide native: the same path handles any resolution up to the GPU texture cap
        byte[] d5k = File.ReadAllBytes(PristineDat);
        Check(RenderPatch.Apply(d5k, 5120, 2160, 5120, 2160).Success, "native apply 5120x2160 (5K2K 21:9) success");
        Check(d5k[0x30232F] == 0x00 && d5k[0x302330] == 0x14 && d5k[0x302337] == 0x70 && d5k[0x302338] == 0x08, "native: 5K2K render clamp raised to 5120x2160");
        Check(d5k[0x30236F] == 0x00 && d5k[0x302370] == 0x14 && d5k[0x3023AC] == 0x70 && d5k[0x3023AD] == 0x08, "native: 5K2K aspect-fit target raised to 5120x2160");
        InspectResult i5k = RenderPatch.Inspect(d5k);
        Check(i5k.Applied && i5k.Candidate0W == 5120 && i5k.Candidate0H == 2160, "native: 5K2K entry0 = 5120x2160");
    }

    static void TestBorderless()
    {
        if (!File.Exists(PristineDat)) { Console.WriteLine("SKIP Borderless (no pristine client binary)"); return; }
        byte[] d = File.ReadAllBytes(PristineDat);
        Check(!RenderPatch.BorderlessApplied(d), "pristine: borderless not applied");
        Check(RenderPatch.ApplyBorderless(d) == 9, "borderless: 9 sites patched");
        Check(d[0x303234] == 0x83 && d[0x303235] == 0xC4 && d[0x303236] == 0x08 && d[0x303237] == 0x90, "borderless: ChangeDisplaySettings neutralised (add esp,8)");
        Check(d[0x30A4F2] == 0x90 && d[0x30A4FC] == 0x90, "borderless: focus-loss minimize NOPed");
        Check(d[0x302F16] == 0xEB && d[0x302F17] == 0x4F, "borderless: windowed-mode forces fits branch (clean 1920 window)");
        Check(d[0x13AA3D] == 0xE8 && d[0x13AA3E] == 0xD6 && d[0x13AA3F] == 0x2A && d[0x13AA40] == 0x29 && d[0x13AA41] == 0x00, "borderless: res-apply call repointed to notice stub (call 0x7CE118)");
        // in-game notice: text at file 0x3CD4C2 (VA 0x7CE0C2), NUL-terminated, then the 40-byte stub at 0x3CD518
        Check(d[0x3CD4C2] == 0x52 && d[0x3CD4C3] == 0x65 && d[0x3CD509] == 0x00 && d[0x3CD517] == 0x00, "borderless: notice text written + NUL-terminated in .text slack");
        Check(d[0x3CD518] == 0x58 && d[0x3CD519] == 0x83 && d[0x3CD51C] == 0x50 && d[0x3CD53F] == 0xC3, "borderless: notice stub written (pop eax .. ret) at VA 0x7CE118");
        Check(d[0x3CD533] == 0x68 && d[0x3CD534] == 0xC2 && d[0x3CD535] == 0xE0 && d[0x3CD536] == 0x7C && d[0x3CD537] == 0x00, "borderless: stub pushes notice string VA 0x7CE0C2");
        Check(d[0x3CD538] == 0xE8 && d[0x3CD539] == 0xA3 && d[0x3CD53A] == 0x1F && d[0x3CD53B] == 0xE1 && d[0x3CD53C] == 0xFF, "borderless: stub calls message sink 0x5E00E0");
        Check(d[0x3CD53D] == 0x32 && d[0x3CD53E] == 0xC0, "borderless: stub ends xor al,al (resolution stays a no-op)");
        Check(d[0x30A076] == 0x00, "borderless: creation WS_EX_TOPMOST dropped (A1)");
        Check(d[0x12688A] == 0xFE, "borderless: activate SWP -> HWND_NOTOPMOST (A2)");
        Check(d[0x303273] == 0xFE, "borderless: display SWP -> HWND_NOTOPMOST (A3)");
        Check(d[0x30A86C] == 0x90 && d[0x30A86D] == 0x90, "borderless: freeze gate NOPed - full FPS when tabbed out (B)");
        Check(d[0x256C62] == 0x03, "borderless: StretchBlt mode -> COLORONCOLOR (crisp nearest-neighbour upscale)");
        Check(RenderPatch.BorderlessApplied(d), "borderless: inspect reports applied");
        Check(RenderPatch.ApplyBorderless(d) == 0, "borderless: re-apply is idempotent (0 sites)");
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
            Check(RenderPatch.BorderlessApplied(File.ReadAllBytes(Path.Combine(tmp, "angel.dat"))), "ApplyRenderFix now applies borderless (keep-rendering + res-disable on windowed fallback)");
            Check(File.Exists(o.DatBackup), "angel.dat backup created");
            Check(File.Exists(o.IniBackup), "midage.ini backup created");
            GameStatus st = GameFiles.Inspect(tmp);
            Check(st.Applied && st.CurrentW == 1920, "inspect reports applied at 1920");
            Check(st.HasBackup, "inspect reports a backup exists");
            Check(FixCore.ReadIntKey(File.ReadAllText(Path.Combine(tmp, "midage.ini")), "ScreenWidth") == 1920, "midage.ini updated on disk");
            Check(FixCore.ReadIntKey(File.ReadAllText(Path.Combine(tmp, "midage.ini")), "GameWndFullScreen") == 1, "1080p or lower stays fullscreen");

            ApplyOutcome rev = GameFiles.RevertLast(tmp);
            Check(rev.Success, "RevertLast success");
            GameStatus st2 = GameFiles.Inspect(tmp);
            Check(!st2.Applied && st2.CurrentW == 3840, "after revert -> unpatched (candidate 3840 restored)");
            Check(!RenderPatch.BorderlessApplied(File.ReadAllBytes(Path.Combine(tmp, "angel.dat"))), "after revert -> borderless patches gone (in-game resolution menu works again)");

            // a 4K request is hard-capped to 1080p in BOTH the client and the INI,
            // and an above-ceiling monitor switches the game to windowed
            GameFiles.ApplyRenderFix(tmp, 3840, 2160, new DateTime(2026, 7, 2, 2, 0, 0), true);
            GameStatus st3 = GameFiles.Inspect(tmp);
            Check(st3.CurrentW == 1920 && st3.CurrentH == 1080, "4K request capped to 1920 in the client");
            Check(FixCore.ReadIntKey(File.ReadAllText(Path.Combine(tmp, "midage.ini")), "ScreenWidth") == 1920, "4K request capped to 1920 in midage.ini");
            Check(FixCore.ReadIntKey(File.ReadAllText(Path.Combine(tmp, "midage.ini")), "GameWndFullScreen") == 0, "above 1080p switches to windowed");

            // fill mode: render 1920x1080 but window/INI = native 3840x2160 + fullscreen
            GameFiles.ApplyFillFix(tmp, 1920, 1080, 3840, 2160, new DateTime(2026, 7, 2, 3, 0, 0));
            GameStatus st4 = GameFiles.Inspect(tmp);
            Check(st4.CurrentW == 1920 && st4.CurrentH == 1080, "fill: in-game list pinned to the render size 1920x1080 (4K -> aspect-fit)");
            string iniFill = File.ReadAllText(Path.Combine(tmp, "midage.ini"));
            Check(FixCore.ReadIntKey(iniFill, "GameWndSizeWidth") == 3840, "fill: window native 3840");
            Check(FixCore.ReadIntKey(iniFill, "GameWndFullScreen") == 1, "fill: fullscreen on");
            byte[] filled = File.ReadAllBytes(Path.Combine(tmp, "angel.dat"));
            Check(RenderPatch.BorderlessApplied(filled), "fill: borderless applied end-to-end");
            // regression guard: the render-size setter call must stay PRISTINE (the render-freeze
            // latch that crashed the client at boot must never be re-introduced here).
            Check(filled[0x30337D] == 0xE8 && filled[0x30337E] == 0x8E && filled[0x30337F] == 0xEF, "fill: render-size setter call left pristine (no crashing latch)");

            // windowed mode keeps the exact same render patch/UI layout, changing only the INI
            // window target and fullscreen flag.
            GameFiles.ApplyFillFix(tmp, 1920, 1080, 3840, 2160, new DateTime(2026, 7, 2, 4, 0, 0), true, 3808, 2018);
            string iniWindowed = File.ReadAllText(Path.Combine(tmp, "midage.ini"));
            Check(FixCore.ReadIntKey(iniWindowed, "GameWndSizeWidth") == 3808, "windowed: client width fits the work area");
            Check(FixCore.ReadIntKey(iniWindowed, "GameWndSizeHeight") == 2018, "windowed: client height leaves room for taskbar and chrome");
            Check(FixCore.ReadIntKey(iniWindowed, "GameWndFullScreen") == 0, "windowed: fullscreen off");
            Check(RenderPatch.BorderlessApplied(File.ReadAllBytes(Path.Combine(tmp, "angel.dat"))), "windowed: render/UI patches unchanged");
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
            // ApplyFillFix set the high-DPI AppCompat flag for the temp angel.dat path; clean it up.
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true))
                { if (k != null) k.DeleteValue(Path.Combine(tmp, "angel.dat"), false); }
            }
            catch { }
        }
    }

    static void TestWobScaling()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "aofixwob_" + Guid.NewGuid().ToString("N"));
        string cfg = Path.Combine(tmp, "save", "cfg");
        Directory.CreateDirectory(cfg);
        try
        {
            File.WriteAllText(Path.Combine(cfg, "wob_1920_1080.xml"),
                "<root>\r\n\t<window name=\"WND_MINIMAP\" x=\"1756\" y=\"0\"/>\r\n" +
                "\t<window name=\"WND_QUICK\" x=\"262\" y=\"94\">\r\n\t\t<custom name=\"QUICK_X1\" value=\"13\" x=\"500\" y=\"500\"/>\r\n\t</window>\r\n</root>");
            GameFiles.GenerateScaledWob(tmp, 3840, 2160);
            string outp = Path.Combine(cfg, "wob_3840_2160.xml");
            Check(File.Exists(outp), "wob: scaled file wob_3840_2160.xml created");
            string t = File.ReadAllText(outp);
            Check(t.Contains("x=\"3512\""), "wob: WND_MINIMAP x 1756 -> 3512 (x2)");
            Check(t.Contains("x=\"524\"") && t.Contains("y=\"188\""), "wob: WND_QUICK 262,94 -> 524,188");
            Check(t.Contains("value=\"13\""), "wob: <custom> value preserved (not scaled)");
            Check(t.Contains("x=\"500\"") && t.Contains("y=\"500\""), "wob: <custom> x/y NOT scaled (only <window> tags)");
            // ultrawide (21:9): NON-uniform scale (x by width-ratio 2.667, y by height-ratio 2.0) so the
            // HUD tracks the wider canvas - icons follow the screen edges instead of floating mid-screen.
            GameFiles.GenerateScaledWob(tmp, 5120, 2160);
            string uw = File.ReadAllText(Path.Combine(cfg, "wob_5120_2160.xml"));
            Check(uw.Contains("x=\"4683\""), "wob 21:9: WND_MINIMAP x 1756 -> 4683 (x2.667)");
            Check(uw.Contains("x=\"699\"") && uw.Contains("y=\"188\""), "wob 21:9: WND_QUICK 262,94 -> 699,188 (x2.667, y2.0)");
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    public static int Main()
    {
        TestIni();
        TestPlanFill();
        TestRenderPatch();
        TestBorderless();
        TestGameFiles();
        TestWobScaling();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : (failures + " TEST(S) FAILED"));
        return failures == 0 ? 0 : 1;
    }
}
