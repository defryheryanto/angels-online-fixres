# How Angels Online FixRes Works

## TL;DR (the honest one-liner)

It flips a handful of bytes in the game client so it renders at your real
resolution instead of a fixed 1280x720. That's it. Everything below is the
grown-up, over-engineered description of those few bytes.

---

## 1. Problem statement: the sub-native fractional-rescale pathology

The `Angels Online Global` client is a 32-bit, GDI-backed, sprite-compositing
engine whose scene rasterizer is pinned to a **fixed 1280x720 intermediate
framebuffer** established at its original design-time art authorship resolution.
At runtime the compositor performs a terminal-stage **`StretchBlt` upsample** of
that device-independent bitmap (DIB) into the host window's client rectangle.

When the target window is 1920x1080, the transfer function degenerates into a
non-integer 1.5x magnification. Because the source and destination raster grids
are mutually irrational under integer pixel quantization, every source texel is
distributed across a fractional destination footprint, and GDI's interpolation
kernel (nearest-neighbour or halftone, depending on the active stretch-blit mode)
introduces the characteristic low-pass "soft/zoomed" artifact. In plain terms:
1280 pixels cannot cleanly become 1920 pixels, so it looks blurry.

## 2. The rendering subsystem: a DIB-section double-buffered blit pipeline

The engine's presentation path is a classical retained off-screen compositor:

1. `CreateDIBSection` allocates the backing raster surface (the render target),
   dimensioned from the client's internally-arbitrated **render resolution**.
2. `CreateCompatibleBitmap` / `CreateCompatibleDC` establish the device-context
   scaffolding for the memory blit.
3. `SetStretchBltMode` configures the resampling policy.
4. `StretchBlt` performs the source-to-window raster transfer with scaling.

Critically, the render-target dimensions are **not** the window dimensions. They
are governed independently by a dedicated resolution-arbitration state machine.

## 3. The resolution-governance finite state machine

The client stores its render dimensionality in a pair of adjacent 32-bit global
lattice cells (`renderW`, `renderH`). These are populated by a three-way
arbitration protocol:

- **INI-derived hydration.** On boot, `ScreenWidth` / `ScreenHeight` are parsed
  from `midage.ini` and proposed to the arbiter.
- **Capability validation.** A validator enumerates the host's supported display
  modes via `EnumDisplaySettings` and gates the proposed resolution against them,
  reverting non-conforming proposals.
- **Best-fit candidate election.** On a cold/first launch, the arbiter iterates a
  descending-ordered **candidate resolution table** and elects the first
  admissible entry.

The pathology is enforced by a **`SetRenderSize` boundary clamp**: a conditional
guard (`cmp / jg`) that rejects any proposed dimension exceeding the design-time
ceiling of 1280x720, discarding the write entirely. This is the single most
consequential control-flow edge in the entire subsystem, and it is why editing
`midage.ini` alone accomplishes nothing.

## 4. Signature-directed idempotent binary morphology (the actual patch)

FixRes performs **surgical in-place instruction rewriting** against the client
image. To remain resilient across client revisions (which shift absolute file
offsets), it does not hard-code addresses. Instead it employs **wildcard-masked
byte-pattern signatures** anchored on displacement-invariant opcode topology,
with all absolute operands (globals, RVA-relative pointers) masked out. Three
transformations are applied:

1. **Clamp ceiling elevation.** The two comparison immediates in the
   `SetRenderSize` guard are raised from `0x500`/`0x2D0` to a permissive `0x4000`
   upper bound, neutralizing the sub-native rejection edge. A structural
   post-condition (renderW/renderH globals must be 4-byte-adjacent) is asserted to
   guarantee the signature bound to the correct call site.
2. **Validator short-circuit.** The capability-validation routine's prologue is
   overwritten with an unconditional affirmative return (`mov al,1 ; ret`), so the
   arbiter accepts the operator-elected resolution without display-mode veto.
3. **Candidate-apex substitution.** The zeroth entry of the candidate resolution
   table is rewritten to the operator's native geometry, so even the cold-launch
   election path converges on the native mode.

Coupled with a corresponding `midage.ini` hydration, the render target is now
allocated at native geometry, the terminal `StretchBlt` becomes an identity
(1:1) transfer, and the fractional-rescale pathology is eliminated.

All rewrites are **idempotent**: signatures anchor exclusively on bytes the patch
does not mutate, so re-application is a no-op-equivalent convergent operation.

## 5. Safety invariants and rollback semantics

- **Fingerprint-gated refusal.** If any of the three signatures fails to resolve,
  the tool aborts without a single write. It will never mutate an image it does
  not structurally recognize.
- **Timestamped snapshot backups.** Prior to mutation, `angel.dat` and
  `midage.ini` are copied to timestamped `.fixres.bak` restore points. The
  in-app **Revert** action restores the most recent snapshot.
- **Zero residual coupling.** The tool touches only render geometry. Account
  state, network protocol, and gameplay logic are wholly untouched.

## 6. In fewer words

It raises a size limit, tells one check to say "yes", sets the default
resolution, and updates the config. Then the game draws at your resolution.

## Credits

Reverse-engineered and built by **nosorry**. Reach out on Discord: **no.sorry**.
