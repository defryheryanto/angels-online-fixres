using System;

namespace AngelsFixRes
{
    // Result of inspecting a client binary.
    public class InspectResult
    {
        public bool Recognized;   // all three patch sites were found
        public bool Applied;      // the render fix is already in place
        public int Candidate0W;   // current top resolution candidate
        public int Candidate0H;
        public string Detail = "";
    }

    // Result of applying the fix.
    public class PatchResult
    {
        public bool Success;
        public string Message = "";
    }

    // Locates and applies the Angels Online render-resolution fix inside the
    // client binary (angel.dat) by byte-pattern signature, so it works across
    // builds and refuses safely on a client it does not recognise.
    //
    // The fix, for a target resolution WxH:
    //   1. Raise the SetRenderSize clamp (it rejects anything above 1280x720).
    //   2. Force the resolution validator to accept (mov al,1; ret).
    //   3. Set the top resolution candidate to WxH (first-launch lands native).
    // Signatures anchor on bytes the patch does NOT change, so re-applying is
    // safe (idempotent) and absolute addresses are wildcarded for build safety.
    public static class RenderPatch
    {
        // Fill mode raises the render clamp to 1920x1080 - a safe render size the engine then
        // stretches to fill the monitor. Native mode (the 4-arg Apply) raises it to the monitor's
        // OWN resolution instead. The render buffers are dynamic (they realloc from the render
        // globals), so a higher clamp is safe up to the GPU's max texture size - the old "engine
        // crashes above 1080p" was actually just the GPU texture limit, not a baked-in ceiling.
        const int ClampLimitW = 1920;
        const int ClampLimitH = 1080;

        // -1 = wildcard.
        // cmp eax,IMM ; jg .. ; cmp ecx,IMM ; jg .. ; mov [renderW],eax ; mov [renderH],ecx
        static readonly int[] Clamp = {
            0x3D,-1,-1,-1,-1, 0x7F,-1, 0x81,0xF9,-1,-1,-1,-1, 0x7F,-1, 0xA3,-1,-1,-1,-1, 0x89,0x0D
        };
        // (body of validator, starting 3 bytes past the entry) sub esp,0xA4 ; mov eax,[cookie] ; xor eax,ebp ; mov [ebp-4],eax ; mov edx,[g] ; push esi ; push edi ; cmp [g],edx
        static readonly int[] ValidateBody = {
            0x81,0xEC,0xA4,0x00,0x00,0x00, 0xA1,-1,-1,-1,-1, 0x33,0xC5, 0x89,0x45,0xFC,
            0x8B,0x15,-1,-1,-1,-1, 0x56,0x57, 0x39,0x15,-1,-1,-1,-1
        };
        // stable 6-entry tail of the candidate table: 1366x768, 1280x720, 800x600.
        // The list-count dword that FOLLOWS (at cand+24) is intentionally NOT part of the
        // signature, so the tool can shrink the in-game list to a single entry (see
        // SetResolutionList) without breaking client recognition. entry0 sits 40 bytes before.
        static readonly int[] CandidateTail = {
            0x56,0x05,0x00,0x00, 0x00,0x03,0x00,0x00,
            0x00,0x05,0x00,0x00, 0xD0,0x02,0x00,0x00,
            0x20,0x03,0x00,0x00, 0x58,0x02,0x00,0x00
        };

        public static int Find(byte[] data, int[] pat, int start = 0)
        {
            int last = data.Length - pat.Length;
            for (int i = start; i <= last; i++)
            {
                bool ok = true;
                for (int j = 0; j < pat.Length; j++)
                {
                    if (pat[j] != -1 && data[i + j] != pat[j]) { ok = false; break; }
                }
                if (ok) return i;
            }
            return -1;
        }

        static int RI32(byte[] d, int o) { return BitConverter.ToInt32(d, o); }
        static void WI32(byte[] d, int o, int v)
        {
            byte[] b = BitConverter.GetBytes(v);
            d[o] = b[0]; d[o + 1] = b[1]; d[o + 2] = b[2]; d[o + 3] = b[3];
        }

        // Confirm the clamp match really is the render-size setter: the two
        // globals it writes (renderW, renderH) must be adjacent (4 bytes apart).
        static bool ClampIsRenderSetter(byte[] d, int m)
        {
            uint wAddr = (uint)RI32(d, m + 16);   // A3 <renderW addr>
            uint hAddr = (uint)RI32(d, m + 22);   // 89 0D <renderH addr>
            return hAddr == wAddr + 4;
        }

        public static InspectResult Inspect(byte[] dat)
        {
            var r = new InspectResult();
            int clamp = Find(dat, Clamp);
            int val = Find(dat, ValidateBody);
            int cand = Find(dat, CandidateTail);
            bool clampOk = clamp >= 0 && ClampIsRenderSetter(dat, clamp);
            r.Recognized = clampOk && val >= 0 && cand >= 0;
            if (!r.Recognized)
            {
                r.Detail = "client not recognised (clamp=" + (clampOk) + " validator=" + (val >= 0) + " table=" + (cand >= 0) + ")";
                return r;
            }
            int table = cand - 40;               // entry0 sits 5 entries (40 bytes) before the tail
            r.Candidate0W = RI32(dat, table);
            r.Candidate0H = RI32(dat, table + 4);
            bool clampRaised = RI32(dat, clamp + 1) > 1280;
            bool validatePatched = dat[val - 3] == 0xB0 && dat[val - 2] == 0x01 && dat[val - 1] == 0xC3;
            r.Applied = clampRaised && validatePatched;
            return r;
        }

        // Apply the fix in-place, lifting the render clamp to the engine's safe 1080p ceiling.
        public static PatchResult Apply(byte[] dat, int width, int height)
        {
            return Apply(dat, width, height, ClampLimitW, ClampLimitH);
        }

        // Apply the fix in-place with an explicit render-clamp ceiling. The render buffers are all
        // dynamic (they realloc from the render globals), so the clamp can be raised to the monitor's
        // native resolution for a true native render - the only real limit is the GPU's max texture
        // size, which the tool checks before calling this with a native clampW/clampH. When clampW/H is
        // 1920x1080 this is the standard fill patch. Returns Success=false (unchanged) if unrecognised.
        public static PatchResult Apply(byte[] dat, int width, int height, int clampW, int clampH)
        {
            var res = new PatchResult();
            if (width > clampW) width = clampW;
            if (height > clampH) height = clampH;
            int clamp = Find(dat, Clamp);
            if (clamp < 0 || !ClampIsRenderSetter(dat, clamp)) { res.Message = "Could not find the render-size clamp - this client build is not recognised. Nothing was changed."; return res; }
            int val = Find(dat, ValidateBody);
            if (val < 0) { res.Message = "Could not find the resolution validator - this client build is not recognised. Nothing was changed."; return res; }
            int cand = Find(dat, CandidateTail);
            if (cand < 0) { res.Message = "Could not find the resolution table - this client build is not recognised. Nothing was changed."; return res; }

            // 1. set the render clamp box (imm32 at clamp+1 = W, clamp+9 = H). 1920x1080 for fill,
            //    or the monitor's native resolution for native mode.
            WI32(dat, clamp + 1, clampW);
            WI32(dat, clamp + 9, clampH);
            // 1b. CRITICAL: the "> box" branch aspect-fits into a SEPARATE hardcoded 1280x720 target
            //     (mov ecx,1280 @clamp+0x40 ; mov eax,720 @clamp+0x7D), NOT the box above. Without
            //     raising it too, any fill request larger than the box (e.g. a 4K/1440p window) renders
            //     1280x720 stretched to the panel - the exact blur the tool exists to remove. Raise it
            //     to the box, guarded on the two mov opcodes so an unrecognised layout is left alone.
            if (dat[clamp + 0x40] == 0xB9 && dat[clamp + 0x7D] == 0xB8)
            {
                WI32(dat, clamp + 0x41, clampW);
                WI32(dat, clamp + 0x7E, clampH);
            }
            // 2. validator always accepts (mov al,1 ; ret) at the function entry
            dat[val - 3] = 0xB0; dat[val - 2] = 0x01; dat[val - 1] = 0xC3;
            // 3. top candidate = target resolution
            int table = cand - 40;
            WI32(dat, table, width);
            WI32(dat, table + 4, height);

            res.Success = true;
            res.Message = "Client patched for " + width + "x" + height + " (render clamp " + clampW + "x" + clampH + ").";
            return res;
        }

        // Collapse the in-game System->Display->Resolution list to a SINGLE entry = the render
        // resolution the tool applies (WxH). Changing resolution in-game re-centres the fixed
        // HUD (it floats to the middle of the screen); with only one option, equal to the current
        // render, any in-game "Apply" is a no-op and the HUD can't float. Pure data edit: entry0
        // (cand-40) = WxH, and the list count (cand+24) = 1. entry0 does NOT drive the render
        // (that comes from midage.ini GameWndSize through the clamp); this is display-only.
        public static bool SetResolutionList(byte[] dat, int w, int h)
        {
            int cand = Find(dat, CandidateTail);
            if (cand < 40 || cand + 28 > dat.Length) return false;   // bounds: table=cand-40 .. count=cand+24
            int table = cand - 40;
            WI32(dat, table, w);
            WI32(dat, table + 4, h);
            WI32(dat, cand + 24, 1);   // in-game list shows only this one resolution
            return true;
        }

        // --- True borderless windowed-fullscreen (v1.2) -------------------------
        // The fullscreen branch already builds a WS_POPUP window covering the monitor.
        // These edits make it behave like a real borderless-windowed mode:
        //   CDS    neutralise the exclusive ChangeDisplaySettingsA  -> no desktop mode
        //          switch / no flicker/blackout on Alt-Tab.
        //   MIN    NOP the focus-loss ShowWindow(SW_MINIMIZE)       -> never minimises.
        //   A1     drop the creation-time WS_EX_TOPMOST             -> other apps can come
        //          to the front (load-bearing: HWND_TOP can't clear an existing band).
        //   A2/A3  HWND_TOPMOST -> HWND_NOTOPMOST on the two re-asserting SetWindowPos.
        //   B      NOP the main-loop focus gate                     -> keeps ticking,
        //          rendering and pumping the network while unfocused (no freeze).
        // Located by unique wildcard-anchored signature (verified in the current build).
        // The single-byte flips wildcard the flipped byte, so they are idempotent AND
        // land the correct value whether the client is pristine or already part-patched.

        // call ChangeDisplaySettingsA ; test eax,eax ; jne +7 ; mov byte[cdsFlag],1
        static readonly int[] CdsCall = {
            0xFF,0x15,0x08,0xF4,0x7C,0x00, 0x85,0xC0, 0x75,0x07, 0xC6,0x05,0x2F,0x07,0x9D,0x00, 0x01 };
        // add esp,8 (discard the two stdcall args) then NOP -> mode switch skipped
        static readonly byte[] CdsPatch = {
            0x83,0xC4,0x08, 0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90 };
        // push 6(SW_MINIMIZE) ; push [esi+4] ; call ShowWindow
        static readonly int[] MinimizeCall = {
            0x6A,0x06, 0xFF,0x76,0x04, 0xFF,0x15,0x08,0xF5,0x7C,0x00 };
        static readonly byte[] MinimizePatch = {
            0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90 };
        // Windowed-mode fit: force the "window fits the work area" branch so toggling the
        // in-game windowed-mode checkbox never shrinks the window (and the StretchBlt dest)
        // below the render size. Result: a clean 1920x1080 window the render fills 1:1,
        // instead of a squished / black-barred mismatch. Only the windowed branch is touched
        // (fullscreen is a separate branch). cmp edx,eax; jg; cmp esi,ecx; jle <fits> -> jmp <fits>.
        static readonly int[] WindowedFitCheck = { 0x3B,0xD0, 0x7F,0x04, 0x3B,0xF1, 0x7E,0x49 };
        static readonly byte[] WindowedFitPatch = { 0xEB,0x4F, 0x90,0x90,0x90,0x90,0x90,0x90 };
        // Disable the in-game System->Display->Resolution "OK" AND tell the player why.
        // Picking a resolution and applying it re-sizes the render surface and corrupts /
        // UI-scale-mismatches the display (it fights our fixed render/window/UI-layout setup).
        // We repoint the res-apply `call <core>` (sig+10) to a small stub in the file-backed
        // all-zero slack at the tail of .text: the stub prints an on-screen notice and then does
        // the exact same no-op the plain patch did (add esp,8 ; xor al,al) - balances the 2
        // stdcall args + forces the "not applied" branch, so picking a resolution + OK stays a
        // no-op and no stale cfg XML is written. If that slack is not clear (a build we don't
        // fully match) we fall back to the plain no-op with no notice, so the client is never
        // corrupted. The windowed-mode ("Wnd Mode") toggle is a separate handler and is untouched.
        // Sig: mov ecx,[chat]; mov edi,eax; push edi; push esi; CALL <res-apply>; test al,al; je.
        static readonly int[] DisableResCall = {
            0x8B,0x0D,0x6C,0x35,0x9B,0x00, 0x8B,0xF8, 0x57, 0x56, 0xE8,-1,-1,-1,-1, 0x84,0xC0, 0x74 };
        // Fallback when the notice slack is occupied (shifted build): plain no-op, no notice.
        static readonly byte[] DisableResPatch = { 0x83,0xC4,0x08, 0x32,0xC0 };
        // On-screen notice string + stub, placed in the file-backed all-zero .text tail slack
        // (VA 0x7CE0C2.., mapped R+X by the loader). Byte-exact for build 4774912: the VAs are
        // baked into the stub, so this only fires when the slack is still clear (see WriteNotice).
        // String at VA 0x7CE0C2 (file 0x3CD4C2); stub at VA 0x7CE118 (file 0x3CD518, right after).
        const int NoticeStrOff  = 0x3CD4C2;
        const int NoticeStubOff = 0x3CD518;
        const string NoticeText = "Resolution is managed by the FixRes tool. Re-run the tool to change it.";
        // pop eax ; add esp,8 ; push eax                    restore the stdcall ret-8 stack + retaddr
        // cmp esi,[renderW] ; jne show ; cmp edi,[renderH] ; je done   show only on an actual change
        // show: push 0 ; push 0 ; push 7 ; push 0x7CE0C2 ; call 0x5E00E0   ecx=[chat] singleton (live)
        // done: xor al,al ; ret                             force the "not applied" branch -> res no-op
        static readonly byte[] NoticeStub = {
            0x58, 0x83,0xC4,0x08, 0x50,
            0x3B,0x35,0xC4,0x62,0x87,0x00, 0x75,0x08,
            0x3B,0x3D,0xC8,0x62,0x87,0x00, 0x74,0x10,
            0x6A,0x00, 0x6A,0x00, 0x6A,0x07, 0x68,0xC2,0xE0,0x7C,0x00,
            0xE8,0xA3,0x1F,0xE1,0xFF, 0x32,0xC0, 0xC3 };
        // `call 0x7CE118` (rel32 to the stub) - replaces the original `call 0x5A92EA`.
        static readonly byte[] DisableResHook = { 0xE8,0xD6,0x2A,0x29,0x00 };
        // A1: push 0x80000000 ; push [ebp+0x10] ; push ebx ; push 8(WS_EX_TOPMOST) ; call CreateWindowExA
        static readonly int[] CreateExTopmost = {
            0x68,0x00,0x00,0x00,0x80, 0xFF,0x75,0x10, 0x53, 0x6A,-1, 0xFF,0x15,0x90,0xF4,0x7C,0x00 };
        // A2: push 3 ; push edi x4 ; push -1(HWND_TOPMOST) ; push [esi+0x10] ; call SetWindowPos
        static readonly int[] ActivateSwpTopmost = {
            0x6A,0x03, 0x57,0x57,0x57,0x57, 0x6A,-1, 0xFF,0x76,0x10, 0xFF,0x15,0x04,0xF5,0x7C,0x00 };
        // A3: push 0 ; push 0 ; push -1(HWND_TOPMOST) ; push ebx ; call SetWindowPos
        static readonly int[] DisplaySwpTopmost = {
            0x6A,0x00, 0x6A,0x00, 0x6A,-1, 0x53, 0xFF,0x15,0x04,0xF5,0x7C,0x00 };
        // B: cmp byte[esi+0xc],0 ; je (idle-when-unfocused) ; <focused idle: call tick ; call stub>
        static readonly int[] FreezeGate = {
            0x80,0x7E,0x0C,0x00, -1,-1, 0x8B,0x06, 0x8B,0xCE, 0xFF,0x50,0x78, 0x8B,0x06, 0x8B,0xCE, 0xFF,0x50,0x7C };

        // Apply the borderless edits. Returns how many sites were changed this call.
        public static int ApplyBorderless(byte[] dat)
        {
            int n = 0;
            int cds = Find(dat, CdsCall);
            if (cds >= 0) { for (int i = 0; i < CdsPatch.Length; i++) dat[cds + i] = CdsPatch[i]; n++; }
            int mini = Find(dat, MinimizeCall);
            if (mini >= 0) { for (int i = 0; i < MinimizePatch.Length; i++) dat[mini + i] = MinimizePatch[i]; n++; }
            int wf = Find(dat, WindowedFitCheck);   // windowed toggle -> clean 1920x1080 window (no shrink)
            if (wf >= 0) { for (int i = 0; i < WindowedFitPatch.Length; i++) dat[wf + i] = WindowedFitPatch[i]; n++; }
            // Disable in-game resolution change + show an on-screen notice (windowed toggle unaffected).
            int dr = Find(dat, DisableResCall);
            if (dr >= 0)
            {
                bool hooked = dat[dr + 10] == DisableResHook[0] && dat[dr + 11] == DisableResHook[1]
                           && dat[dr + 12] == DisableResHook[2] && dat[dr + 13] == DisableResHook[3] && dat[dr + 14] == DisableResHook[4];
                if (!hooked)
                {
                    byte[] apply = WriteNotice(dat) ? DisableResHook : DisableResPatch;
                    for (int i = 0; i < apply.Length; i++) dat[dr + 10 + i] = apply[i];
                    n++;
                }
            }
            n += SetByte(dat, CreateExTopmost, 10, 0x00);    // A1 drop creation WS_EX_TOPMOST
            n += SetByte(dat, ActivateSwpTopmost, 7, 0xFE);  // A2 HWND_TOPMOST -> HWND_NOTOPMOST
            n += SetByte(dat, DisplaySwpTopmost, 5, 0xFE);   // A3 HWND_TOPMOST -> HWND_NOTOPMOST
            // NOP the focus gate so the unfocused idle path runs the same tick+render as
            // focused - the game keeps running at its normal full FPS while tabbed out.
            int fg = Find(dat, FreezeGate);
            if (fg >= 0 && !(dat[fg + 4] == 0x90 && dat[fg + 5] == 0x90)) { dat[fg + 4] = 0x90; dat[fg + 5] = 0x90; n++; }
            // Crisp upscaling: the render DIB is StretchBlt'd to the window with SetStretchBltMode
            // (HALFTONE=4, soft averaging). Flip the mode operand of `push 4 ; call SetStretchBltMode`
            // to `push 3` (COLORONCOLOR = nearest-neighbour) so an integer upscale (e.g. a 1920 render
            // filling a 4K window = clean 2x) stays sharp. Signature-anchored + value-guarded (like the
            // other edits) so a shifted build is never corrupted; at 1:1 (native) it is a no-op.
            n += SetByte(dat, StretchMode, 1, 0x03);
            return n;
        }
        // push 4 ; push [esi+4] ; call [SetStretchBltMode] - unique in the build; flip operand (idx 1).
        static readonly int[] StretchMode = { 0x6A,0x04, 0xFF,0x76,0x04, 0xFF,0x15,0x54,0xF0,0x7C,0x00 };

        // Set dat[Find(sig)+idx] = val once. Returns 1 if it changed a byte, else 0.
        static int SetByte(byte[] dat, int[] sig, int idx, byte val)
        {
            int at = Find(dat, sig);
            if (at < 0 || dat[at + idx] == val) return 0;
            dat[at + idx] = val;
            return 1;
        }

        // Write the notice string + stub into the .text tail slack, but only if that region
        // is still all-zero - so a build where the slack holds real code is never corrupted
        // (the caller then falls back to the plain no-op). Returns true if it wrote the notice.
        static bool WriteNotice(byte[] dat)
        {
            byte[] text = System.Text.Encoding.ASCII.GetBytes(NoticeText);
            if (NoticeStrOff + text.Length + 1 > NoticeStubOff) return false;   // text too long for its slot
            int end = NoticeStubOff + NoticeStub.Length;
            if (end > dat.Length) return false;
            for (int i = NoticeStrOff; i < end; i++) if (dat[i] != 0x00) return false;   // slack not clear
            for (int i = 0; i < text.Length; i++) dat[NoticeStrOff + i] = text[i];        // text (NUL stays 0)
            for (int i = 0; i < NoticeStub.Length; i++) dat[NoticeStubOff + i] = NoticeStub[i];
            return true;
        }

        // True once the exclusive ChangeDisplaySettings neutralisation is present.
        public static bool BorderlessApplied(byte[] dat) { return Find(dat, CdsCall) < 0; }
    }
}
