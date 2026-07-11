# Changelog

All notable changes to Angels Online FixRes. The newest release is at the top.

## v1.2.0

Focus of this release: make the game genuinely fill **every** big monitor, and stop
promising something the engine can't actually do (see note below).

- **Fullscreen fill for every big monitor.** Every monitor larger than 1080p - 4K,
  5K, ultrawide, and tall 16:10 panels like 1920x1200 and 2560x1600 - now runs
  borderless-fullscreen and fills the whole display, instead of some of them playing
  in a small window in the corner.
- **Ultrawide / 21:9 fills edge to edge.** The game's picture is 16:9, so on a wider
  screen it's stretched a little at the sides to reach the edges. On 16:9 monitors
  there's no stretch.
- **Sharper than stock.** The game renders a full 1920x1080 HUD layout (up from its
  default 720p) and the game's own Direct2D present stretches that to fill your
  screen, so the interface is sharper than the stock soft 1.5x stretch.
- **Borderless-fullscreen behaves properly.** No black-screen flicker when you
  Alt-Tab, and the game keeps running and rendering while you're tabbed out - no
  minimizing, no freezing.
- **Nothing to configure.** The tool detects your monitor and fills the screen
  automatically - no resolution to pick or tick.
- **Correct scaling at any Windows display scaling** (125%, 150%, 200%), so high-DPI
  monitors look right.
- **The in-game Display resolution menu is turned off** while the fix is active
  (changing it would fight the tool's setup). **Revert fix** restores it.
- **Play** launches the game straight away when the fix is applied; one-click
  **Revert** restores the original game and the normal in-game resolution menu.
- Stability and reliability fixes.

**Why the wording changed from earlier versions.** Digging into the client showed
that its world is composited at a fixed, art-baked size and then stretched onto the
screen by Direct2D - and that baked size can't be enlarged by any resolution setting.
So "render at your native resolution" was never actually achievable on this engine;
what genuinely helps is making the stretch fill your whole screen cleanly, which is
what this release does. The full technical account is in
[docs/how-it-works.md](docs/how-it-works.md).

## v1.1.0

- Render capped at 1920x1080 for stability (the client is unreliable above it).
- A clean window on monitors larger than 1080p, removing the black bars.
- Detects your real monitor resolution automatically.

## v1.0.0

- First release: makes Angels Online render sharper instead of blurry and stretched.
