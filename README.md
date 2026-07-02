# Angels Online FixRes

A small Windows tool that makes the MMO *Angels Online Global* render at your
monitor's **native resolution** instead of a blurry, zoomed-in stretch.

The game renders its 2D world onto a fixed **1280x720** surface and then stretches
that to fill your window. At 1920x1080 that is a 1.5x stretch, and a fractional
stretch cannot land on whole pixels, so everything looks soft and magnified. This
tool makes the game render at your real resolution, so the picture is **1:1 and
sharp** (and you see more of the world). It backs up your files first and can be
reverted at any time.

It ships as a single executable with **no prerequisites** - no .NET, Python, or
anything else to install. Download it and run.

![Angels Online FixRes](assets/ui-preview.png)

## Before and after

The same spot in-game: stretched to 1280x720 (before) vs. rendered at your native
resolution (after) - sharper, and you see more of the world.

| Before (stretched, magnified) | After (native, sharp) |
|:---:|:---:|
| ![Before](assets/before.png) | ![After](assets/after.png) |

---

## Features

- **Native for your monitor.** It detects *your* resolution and pins the game to
  it - 1920x1080, 2560x1440, 3840x2160, whatever you actually have. Nothing is
  hardcoded to one screen.
- **One click.** Press **Fix UI** and it is done. Press **Play** to fix and launch
  in one step.
- **Safe.** It backs up `angel.dat` and `midage.ini` first, and it will **refuse**
  to touch a client it does not recognise (so a future game update can never make
  it corrupt your install).
- **Survives updates.** If a game update overwrites the client, just run the tool
  again to re-apply.

---

## Download and run

1. Open the [Releases](../../releases) page and download the latest
   `AngelsOnlineFixRes.exe`.
2. **Close Angels Online** if it is open (the client file is locked while it runs;
   the fix takes effect on the next launch).
3. Run `AngelsOnlineFixRes.exe`. If it does not find the game automatically, click
   **Browse...** and select your Angels Online Global folder (the one with
   `angel.dat` and `START.EXE`).
4. Confirm the resolution (it defaults to your monitor's native), press **Fix UI**,
   then launch the game - or just press **Play**.

A standard install lives at:

```text
C:\UserJoy\Angels Online Global
```

---

## Is it safe? (Windows SmartScreen)

`AngelsOnlineFixRes.exe` is new software from an independent developer and is not
code-signed with a paid certificate, so Windows SmartScreen may show a blue
**"Windows protected your PC"** notice the first time you run it. That notice means
Windows has not seen this exact file many times yet. It is not a virus warning,
and the tool is not malware.

To run it:

1. Click **More info**.
2. Click **Run anyway**.

Prefer to verify first? You have options:

- **Scan it.** Upload the file to [VirusTotal](https://www.virustotal.com/) or
  paste the hash below.
- **Check the hash.** The SHA-256 of `AngelsOnlineFixRes.exe` for v1.1.0 is:

  ```text
  2B2A5BE5E166E2306F26397CEA38D42C1F44539A473AFD477B73A404D5E802B9
  ```

  Verify it on your machine with:

  ```powershell
  Get-FileHash .\AngelsOnlineFixRes.exe -Algorithm SHA256
  ```

- **Read what it does.** The full method is documented in
  [docs/how-it-works.md](docs/how-it-works.md) - nothing is hidden about how the
  fix works.

The SmartScreen notice fades as more people download and run the tool.

---

## How to use

1. Run the tool. Green status at the top means it found your game; red means click
   **Browse...** and pick your Angels Online Global folder.
2. The card shows the current state. "renders at 1280x720 and stretches it" is the
   problem this fixes; "Fix is active" means you are already sorted.
3. Leave **Render at** on your native resolution (or pick a lower one for
   performance), then press **Fix UI** and confirm.
4. Launch the game - or use **Play** to fix and launch together. The picture is now
   sharp.

> **The interface will look a little different at native resolution** - elements
> sit at their true pixel size instead of being magnified, and you see more of the
> game world. That is the correct, crisp result.

---

## Reverting

Don't like it? Click **Revert fix** in the app to restore the most recent backup.

Every fix also writes timestamped `.fixres.bak` backups next to the originals, so
you can revert by hand too: delete the current file and rename the latest
`angel.dat.*.fixres.bak` / `midage.ini.*.fixres.bak` back to `angel.dat` /
`midage.ini`.

---

## How it works

Short version: the game caps its render resolution inside the client. The tool
lifts that cap so the game can render at your monitor's resolution with no stretch.
The full technical write-up (root cause, exactly what is changed, and why it is
safe) is in [docs/how-it-works.md](docs/how-it-works.md).

---

## Contact

Found a bug or a problem? Reach out on Discord: **no.sorry**

You can also click the **Discord: no.sorry** button in the bottom-left of the app
to copy the username to your clipboard.

---

<p align="center">
  <img src="https://visitor-badge.laobi.icu/badge?page_id=nosorry.angels-online-fixres" alt="Page views" />
</p>
