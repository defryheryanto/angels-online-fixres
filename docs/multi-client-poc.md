# Multi-client, multi-resolution POC

## Existing GUI workflow

The main application now supports the dynamic workflow directly: select Screen 1, click **Fix UI**,
then **Play**; select Screen 2, click **Fix UI**, then **Play** again. Each Fix UI click creates and
prepares a unique sandbox under the directory configured in `fixres-client-path.txt` beside the
application. The shipped default is `D:\Games\Angels Online Fixres`. Environment variables are
expanded, and relative paths are resolved from the application directory. Window movement tracks
only the newly started process, so an existing client is never moved when another one launches.

The game stores resolution in installation-level `angel.dat` and `midage.ini` files. Therefore,
simultaneous clients cannot safely have different resolutions while sharing one game folder.
This POC makes a full, isolated sandbox per client, patches only that sandbox, launches its
`START.EXE`, and moves the new game window to the requested screen.

Build it:

```powershell
.\build-multiclient-poc.ps1
```

Launch profiles (up to the game's five-client limit):

```powershell
$game = 'C:\UserJoy\Angels Online Global'
$sandboxes = "$env:LOCALAPPDATA\AngelsOnlineFixRes\clients"
.\dist\MultiClientPoc.exe $game $sandboxes client1 1 borderless
.\dist\MultiClientPoc.exe $game $sandboxes client2 1 windowed
.\dist\MultiClientPoc.exe $game $sandboxes client3 2 borderless
```

Screen numbers follow Windows' current `Screen.AllScreens` order. The first launch copies the
entire installation, so it can take time and use substantial disk space. Later launches reuse the
profile sandbox and only add a new backup when needed.

Safety properties:

- The sandbox root must be outside the source installation.
- The source installation is only read; patching and configuration writes target the sandbox.
- SHA-256 hashes of the source `angel.dat` and `midage.ini` are checked after preparation and the
  client is not launched if either changed.
- No hard links or junctions are used, preventing sandbox writes from affecting installed files.
- Each sandbox retains the existing FixRes clean-backup/revert protection.

Delete a profile sandbox while its client is closed to recreate it from an updated installation.
