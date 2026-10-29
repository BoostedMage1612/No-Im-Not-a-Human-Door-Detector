# No, I'm Not a Human — Door Detector

A Windows Steam mod for **No, I'm not a Human** that shows **HUMAN**, **VISITOR**, or **UNKNOWN** for the guest at your door in a panel at the **top-right** of the screen.

**Current release: v0.1.3.** Open this repository's **Releases** section and download **`NINAH_Door_Detector_v0.1.3.zip`**. That ZIP includes the compiled mod, installer, uninstaller, diagnostics collector, source, and official MelonLoader package. GitHub's automatically generated source-code archives do not contain the complete installer package.

## Install

1. Close the game.
2. Download and extract the entire release ZIP to a normal folder.
3. Double-click **`Install.cmd`**. It finds your Steam installation; if necessary, select `NoImNotAHuman.exe` when prompted.
4. Launch the game normally through Steam. Look for **DOOR DETECTOR** in the top-right corner.

The first launch can take several minutes while MelonLoader prepares the game bindings. Installation and the initial launch require internet access. The installer includes MelonLoader 0.7.3 and downloads a checksum-verified Microsoft .NET 6.0.36 runtime into this game's folder. No separate compiler or system-wide runtime installation is needed to play.

Existing known Door Detector 0.1.0, 0.1.1 and 0.1.2 installations upgrade in place. Loader, runtime, save files and identity reservation history are preserved.

## Compatibility

| Requirement | Supported build |
| --- | --- |
| Platform | Windows x64, Steam |
| Steam app | 3180070 |
| Steam build | **23967901** |
| Game version | **1.3.19** |
| Unity version | 6000.3.10f1, IL2CPP |
| Mod loader | MelonLoader 0.7.3 |

The installer and mod check the exact game binaries. A different game update requires a matching mod update; incompatible hooks are refused. The installer also leaves conflicting loader installations untouched and explains the conflict.

## What the verdict means

- **HUMAN / VISITOR:** the identity for this supported encounter.
- **UNKNOWN:** a special/group encounter or an identity check the mod cannot establish.
- **Waiting for a knock:** no current door verdict is being shown.
- **Checking door state:** the mod is retrying a temporary display/context read.

Fixed characters use the game's actual identity flag.

**Randomized characters need an extra step:** in this game build, their identity roll happens during admission. This mod assigns that identity when they arrive at the door and preserves the assignment when they are admitted. It uses the game's population thresholds and a fair random draw between them. This changes when the choice is made; it does not predict the exact result an unmodified run would have generated.

After admission, the mod checks that its verdict matches both the live character flag and the game's saved identity. Successful checks are logged as `VERIFIED ADMISSION`.

The Couple, Twins and certain other special entities show **UNKNOWN**. Displayed names are the game's internal character labels. Appearance and character-name stereotypes are not used to classify randomized guests.

## Controls

| Key | Action |
| --- | --- |
| **F8** | Hide or show the panel; identity assignments remain active. |
| **F9** | Record the current detector status in its diagnostic log. |

The panel follows the right edge after window or resolution changes and scales down if the window becomes too narrow.

## Saves and uninstalling

Pending assignments are stored in `UserData\DoorDetector\reservations.json` inside the game folder and associated with save snapshots. Keep that file if you want pending assignments to survive restarts. It is local to the installation and is not synchronized automatically by Steam Cloud. Assignments before the first saved checkpoint cannot be restored after exiting without saving.

To uninstall, close the game and run **`Uninstall.cmd`** from the extracted release. It removes this mod, restores any original mod backup recorded at installation, and removes the loader bootstrap if this installer added it and no other mods require it. Game saves, logs and reservation history are kept. Admitted guests retain their normal saved identities.

## Troubleshooting

Close the game and run **`Collect_Diagnostics.cmd`**. It creates a ZIP beside the script and does not upload it automatically.

The ZIP includes game/loader logs, game-specific Windows crash events when available, and build hashes. No game binaries or separate save files are copied, but the game's own logs can contain local paths and save data. Review them before posting anything publicly. For a GitHub issue, start with the mod version, game build, steps to reproduce and the relevant error lines with personal paths removed.

Diagnostic files are not included in this repository or release.

## Validation and limits

- The original release passed 32 identity/save-logic checks, 9 native-handle boundary checks and 21 display lifecycle checks outside Unity. Regression fixtures reproduced the earlier startup/display faults and passed with their corrections.
- A logged v0.1.2 playtest recorded nine displayed arrivals and five matching admissions, including four randomized guests, with no detector errors. The user subsequently confirmed v0.1.3 works in-game.
- v0.1.3 was compiled without warnings/errors and checked for layout at multiple window widths. Its installer scripts passed syntax checks.
- Full save/reload paths, every story branch, unusual scripted/group encounters and interaction with other identity-changing mods have not all been validated end to end.

The release asset is the original working v0.1.3 package, preserved byte-for-byte. See `RELEASE_SHA256.txt` for its checksum and the included `TECHNICAL_NOTES.txt` for implementation details.

## Build from source

Install a .NET SDK capable of targeting .NET 6, extract the official [MelonLoader 0.7.3 release](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3), and run from this source directory:

```powershell
dotnet build DoorDetector.csproj -c Release -p:MelonLoaderPath="C:\path\MelonLoader\net6\MelonLoader.dll"
```

The resulting DLL is `bin\Release\net6.0\DoorDetector.dll`. Building does not require proprietary game binaries. The installer scripts in this source tree are the release scripts; installing from a clone additionally requires the compiled DLL and loader payload in their expected locations. Use the release ZIP for normal installation.

Run the identity tests with .NET 8:

```powershell
dotnet run --project CoreTests.csproj -c Release
```

The native-handle and display suites require a C compiler and are described in `TECHNICAL_NOTES.txt`. Their fake `GameAssembly.dll` libraries are test fixtures only and must never be copied into the game folder. `MelonTestShim.cs` is excluded from the production mod project.

## License and credits

Original mod and installer code: **MIT**, see `LICENSE`. Created by Jimmy / Codex. Third-party components retain their own licenses; see `THIRD_PARTY_NOTICES.md` and the notices bundled in the release ZIP.

Unofficial fan mod, not affiliated with Trioskaz or the game's publisher. No proprietary game binaries or assets are distributed.
