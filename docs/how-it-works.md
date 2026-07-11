# How Angels Online FixRes works

This is the honest, technical account of what the *Angels Online Global* client
does with resolution, what actually goes wrong on modern monitors, and exactly
what the tool changes. Everything here was reverse-engineered from the shipping
`angel.dat` (build 4774912) and cross-checked against the running client.

If you just want the short version: **the game draws its world at a small, fixed
size and then stretches that picture onto your screen. The tool makes that stretch
fill your whole monitor cleanly, in a borderless-fullscreen window.** The longer
version below explains why it is done that way and not another, because the "not
another" part is where the interesting discoveries are.

---

## 1. What the client actually does

The client is a 32-bit Windows game. Its renderer is a hybrid that surprised us:

1. **The world is composited with GDI.** The scene (map, sprites, characters, UI)
   is drawn into an in-memory bitmap using plain GDI. Crucially, the size of that
   composite bitmap is **not** your window size and **not** a resolution setting -
   it comes from the game's own art/scene descriptor. It is a fixed, authored size.
2. **The frame is presented with Direct2D.** That composited bitmap is uploaded to
   a Direct2D render target (`ID2D1HwndRenderTarget`) and drawn to the window with
   `DrawBitmap`, which **stretches** it to the render target's size.

So there are two completely separate size systems in play, and telling them apart
is the whole story:

| System | What sets it | What it controls |
|---|---|---|
| **Render / present size** | the client's resolution globals (fed from `midage.ini` and an internal clamp) | the window size, the Direct2D target size, the stretch destination |
| **Scene composite size** | the baked art/scene descriptor | how large the world is actually drawn before it is stretched |

The important, non-obvious fact: **these two systems never talk to each other in
the code.** The resolution globals are referenced only in the window/present code;
the scene composite size is set only in the scene/asset code. Nothing copies one
into the other.

---

## 2. The original problem: a blurry fractional stretch

Out of the box, the client's render/present size is clamped to a small fixed ceiling
(historically 1280x720), and the scene composites into its own separate, art-baked
surface. On a 1920x1080 monitor the picture is stretched about 1.5x. A 1.5x stretch
can't land on whole pixels, so every edge is smeared - the classic "soft, zoomed-in"
look. On a 4K or ultrawide monitor it's worse, or the game simply runs in a small
window.

That is the problem the tool exists to fix.

---

## 3. The tempting wrong answer: "just render at native resolution"

The obvious idea is: lift the clamp so the client renders at your monitor's real
resolution, and you get a crisp 1:1 image. We tried exactly that, and it produces a
very specific, very telling bug on high-resolution monitors:

> The game window correctly covers the whole screen, but the picture is drawn small
> in the **top-left corner**, with the rest of the screen black.

Here is why, and it is the key discovery. When you raise the render clamp to, say,
5120x2160:

- The resolution globals, the window, and the Direct2D target **all correctly
  become 5120x2160.** That part works.
- But the world still composites into its **baked scene surface** - a small, fixed
  authored size that no resolution setting can change, because (as section 1 showed)
  the scene size system doesn't read the resolution globals at all. They are walled
  off from each other in the binary.

So the small scene is composited into the top-left of the correctly-native render
buffer, and Direct2D then presents that whole buffer - only the top-left corner has
picture, the rest is black. We confirmed this on a real 5120x2160 monitor with a live
diagnostic that read the client's own size globals: every one was correct and
full-screen (render and display both reported 5120x2160), yet the picture stayed
small in the top-left. That ruled out "the buffer is too small" and pointed straight
at the second, hidden size system - the baked scene.

**Conclusion: this engine cannot be made to render its world at more than its baked
resolution by patching the client.** The scene is authored at a fixed size. Asking
for "true native" only grows the buffer, not the art.

---

## 4. The right answer: make the stretch fill the screen

Since the world is drawn at a fixed size and Direct2D already **stretches** it to
the present target, the correct fix is to keep the render small and make the target
- and therefore the stretch - fill your whole screen. That is what the tool does:

1. **Render at a shipped 1920x1080 layout.** 1080p is the largest resolution the
   game ships a matching HUD layout for. (This is also an upgrade over the stock
   720p: the HUD and interface layer are drawn at a higher resolution than stock.)
2. **Set the window to your monitor's full size and go borderless fullscreen.** The
   client already has a fullscreen code path that builds a borderless window
   covering the monitor; the tool steers into it.
3. **Let Direct2D stretch 1080p up to your screen.** `DrawBitmap` scales the render
   to the full target size, so the picture fills the whole monitor. On a 16:9 screen
   this is a clean, aspect-correct (undistorted) upscale.

The net result is a full-screen picture at a higher render resolution than stock,
instead of a blurry fractional stretch or a small window - achieved by fixing the
*stretch*, which is the part of
the pipeline that was actually misconfigured, rather than fighting the render size,
which was never the real lever.

---

## 5. The exact changes

All patches are applied to a **copy** in memory and written back only after they all
succeed; the original files are backed up first. The binary edits are located by
**byte-pattern signature** (with the volatile bytes wildcarded), never by hardcoded
file offset, so a future game update that shifts code around does not silently land a
patch in the wrong place - if a signature no longer matches, the tool refuses to
write anything at all.

**In `angel.dat`:**

- **Render clamp raised to 1920x1080.** The client's render-size guard rejects any
  request above its baked ceiling; the tool raises that ceiling to 1920x1080 so the
  1080p render is accepted. It is deliberately **not** raised to your native size -
  section 3 explains why that would break.
- **Resolution validator forced to accept.** A routine that vetoes non-enumerated
  modes is made to always approve, so the chosen size isn't reverted at boot.
- **In-game resolution list collapsed to one entry**, and the in-game Display
  "Apply" is turned into a safe no-op (with an on-screen notice on recognized
  builds). Changing resolution from inside the game fights the tool's fixed setup and
  can misplace the interface, so the tool owns resolution and the in-game menu is
  disabled on purpose.
- **Borderless-fullscreen behavior.** A handful of small edits make the fullscreen
  window behave like a proper borderless-windowed mode: no exclusive display-mode
  switch (no black-screen flicker on Alt-Tab), it doesn't minimize or freeze when you
  tab out, and it doesn't force itself on top of other windows. (A scaling-mode edit
  also sharpens the client's *internal* composite step; the final on-screen upscale
  to your monitor is Direct2D's own stretch, which the tool doesn't change.)

**In `midage.ini`:** the window size is set to your monitor's resolution and
fullscreen is enabled, so the client drives the window and stretch target to your
full screen.

**A Windows per-app "high-DPI aware" flag** is set on the client, so the picture is
sized in real pixels (not soft-upscaled by Windows) at any display scaling.

The only new code is a tiny stub that shows the on-screen "resolution is managed by
the tool" notice; everything else - the fullscreen path, the stretch, the render
buffers - is the client's own, and the tool only changes which sizes they use and how
the window behaves.

---

## 6. Ultrawide and non-16:9 screens

The world is authored at 16:9. On a 16:9 monitor (1080p, 1440p, 4K, 5K) the 1080p
render stretches to your screen with no distortion. On an ultrawide or other non-16:9
screen (for example 5120x2160, 21:9), filling the whole screen means stretching a
16:9 picture wider than it was drawn, so the image is a little horizontally stretched
in exchange for using the full width. That is a deliberate trade in favor of a
genuinely full-screen picture. A pillarboxed (no-stretch, slim black side-bars)
option may be added later; today the tool always fills.

---

## 7. Safety and reverting

- **Signature-gated.** If any patch site fails to match, the tool aborts without a
  single byte written. It will not touch a client it does not recognize.
- **Backups.** `angel.dat` and `midage.ini` are copied to timestamped
  `.fixres.bak` restore points before any change. **Revert** restores the most
  recent clean backup, and you can also revert by hand (rename the newest backup
  back over the original).
- **No account or gameplay changes.** The tool only touches render geometry and
  window behavior. It does not read, send, or modify account data, and it does
  nothing to network traffic or game logic.

---

## 8. Honest limitations

- It cannot make the world render in more detail than the art is authored at. It
  fills your screen cleanly and at a higher internal resolution than stock, but this
  is a well-scaled classic 2D game, not a re-render.
- Non-16:9 screens fill with a mild horizontal stretch (see section 6).
- Monitors at or below 1080p that aren't a shipped layout size fall back to a clean
  windowed mode rather than a distorted fullscreen.

---

*Reverse-engineered and built by nosorry. Questions or bug reports: Discord
**no.sorry**.*
