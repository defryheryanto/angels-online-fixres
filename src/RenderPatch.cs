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
        // High ceiling so any monitor (up to 8K) is accepted by the clamp.
        const int ClampLimit = 16384;

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
        // stable tail of the candidate table: 1366x768, 1280x720, 800x600, count=8
        static readonly int[] CandidateTail = {
            0x56,0x05,0x00,0x00, 0x00,0x03,0x00,0x00,
            0x00,0x05,0x00,0x00, 0xD0,0x02,0x00,0x00,
            0x20,0x03,0x00,0x00, 0x58,0x02,0x00,0x00,
            0x08,0x00,0x00,0x00
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

        // Apply the fix in-place for the target resolution. Returns Success=false
        // (and changes nothing) if the client is not recognised.
        public static PatchResult Apply(byte[] dat, int width, int height)
        {
            var res = new PatchResult();
            int clamp = Find(dat, Clamp);
            if (clamp < 0 || !ClampIsRenderSetter(dat, clamp)) { res.Message = "Could not find the render-size clamp - this client build is not recognised. Nothing was changed."; return res; }
            int val = Find(dat, ValidateBody);
            if (val < 0) { res.Message = "Could not find the resolution validator - this client build is not recognised. Nothing was changed."; return res; }
            int cand = Find(dat, CandidateTail);
            if (cand < 0) { res.Message = "Could not find the resolution table - this client build is not recognised. Nothing was changed."; return res; }

            // 1. raise the clamp
            WI32(dat, clamp + 1, ClampLimit);
            WI32(dat, clamp + 9, ClampLimit);
            // 2. validator always accepts (mov al,1 ; ret) at the function entry
            dat[val - 3] = 0xB0; dat[val - 2] = 0x01; dat[val - 1] = 0xC3;
            // 3. top candidate = target resolution
            int table = cand - 40;
            WI32(dat, table, width);
            WI32(dat, table + 4, height);

            res.Success = true;
            res.Message = "Client patched for " + width + "x" + height + ".";
            return res;
        }
    }
}
