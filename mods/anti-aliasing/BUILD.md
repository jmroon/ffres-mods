# Building Anti-Aliasing Options

Install [uv](https://docs.astral.sh/uv/getting-started/installation/), then enter
the mod folder from the repository root. Both targets use Python 3.11+ and Lupa 2.6:

```powershell
cd mods/anti-aliasing
uv run --locked python -m unittest discover -s tests -v
```

uv creates an ignored `.venv` here and installs the development dependencies from
`uv.lock`; no manual activation is needed. Keep `pyproject.toml` and `uv.lock`
together. The project is a development environment, not a Python package.

The source lives in this folder: `src/aa_core.lua` handles hooks and polling;
`src/menu.lua` reads the saved choice and previews menu edits; `src/tsr.lua`
always requests TSR. The selected entry is shipped as `Scripts/main.lua` with
its own copy of `aa_core.lua`. The single `VERSION` applies to both targets.

## TSR-only

```powershell
uv run --locked tools/build_release.py --variant tsr
```

No game assets, mappings, retoc or .NET are needed.

## Menu

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0),
[retoc 0.1.5](https://github.com/trumank/retoc/releases/tag/v0.1.5)
(Windows x64: `retoc_cli-x86_64-pc-windows-msvc.zip`), and these read-only inputs:

- A vanilla retoc legacy extraction with `DT_OP_GraphicSetting.uasset`,
  `DT_OP_GraphicSetting_Rank.uasset`, their `.uexp` files and original directory
  structure, plus `scriptobjects.bin` at the extraction root.
- A compatible uncompressed version-4 `.usmap`; see mapping generation below.
- The original game Paks folder, for `global.utoc` and `global.ucas` verification.

Example extraction, with no menu override installed:

```powershell
retoc to-legacy "D:\Games\FINAL FANTASY RESONANCE\FFRS\Content\Paks" ../../build/vanilla --filter DT_OP_GraphicSetting --no-shaders --version UE5_6
```

Build with your local paths:

```powershell
uv run --locked tools/build_release.py --variant menu --source ../../build/vanilla --mappings "D:\ModTools\FFRS.usmap" --retoc "D:\ModTools\retoc.exe" --game-paks "D:\Games\FINAL FANTASY RESONANCE\FFRS\Content\Paks"
```

If needed, pass `--dotnet "D:\ModTools\dotnet\dotnet.exe"`.
The build restores pinned UAssetAPI 1.1.0 and compiles this mod's `tools/MenuAssets` project.
Inputs must match `supported-inputs.json`; investigate mismatches instead of
removing the check. Extracted game assets and mappings are never committed.

## Mapping generation and inspection tools

FModel is optional for browsing assets; neither FModel nor Unreal Editor is needed
to build these mods. UAssetAPI is restored by .NET; Lupa is installed by uv.

Generate mappings from your own updated game with the pinned UE4SS build:
close the game, then temporarily add and enable its upstream `Keybinds` mod in
a local diagnostic setup. Our Standalone packages omit that helper mod.
Preserve the existing mods/configuration when enabling it.

Launch the game, load a save and open Graphics Settings, then press
**Ctrl + Numpad 6**. The pinned
[Keybinds script](https://github.com/UE4SS-RE/RE-UE4SS/blob/83330635/assets/Mods/Keybinds/Scripts/main.lua)
calls `DumpUSMAP()`. Its
[dumper](https://github.com/UE4SS-RE/RE-UE4SS/blob/83330635/UE4SS/src/USMapGenerator/Generator.cpp)
writes an uncompressed v4 `.usmap` in the UE4SS working directory, normally
`FFRS/Binaries/Win64/ue4ss/`, and logs the output filename.

Copy that file to your local tool/input directory and pass it with `--mappings`.
Close the game and undo the temporary helper/configuration changes.
Mappings are game-build inputs, not portable engine SDKs: a successful dump still
needs the menu build's round-trip checks. Do not commit mappings or extracted assets.

## Optional loader

Add these flags to either target for an extract-and-play Standalone ZIP:

```powershell
--bundle-loader --ue4ss "D:\ModTools\UE4SS_v3.0.1-1181-g83330635.zip"
```

Download the exact [non-dev UE4SS archive](https://github.com/UE4SS-RE/RE-UE4SS/releases/download/experimental/UE4SS_v3.0.1-1181-g83330635.zip)
from the upstream experimental archive, not the moving `experimental-latest` release.
Keep a local copy. The builder checks its SHA-256 against [tools/ue4ss.py](tools/ue4ss.py);
if the upstream asset disappears, recover the original archive or deliberately
revalidate a replacement instead of bypassing the hash check. Bundling preserves its DLLs,
helper and license, supplies the tested Unreal 5.6 override, and omits example mods.

## Output and verification

Output paths below are relative to the repository root.

| Target | ZIP in `dist/` | Installed folder under `ue4ss/Mods` |
| --- | --- | --- |
| Menu | `Anti-Aliasing-Options-1.0.0.zip` | `AntiAliasingOptions` |
| TSR-only | `TSR-Always-On-1.0.0-Script-only.zip` | `TSRAlwaysOn` |

With `--bundle-loader`, filenames end in `-1.0.0-Standalone.zip` for either target.
Only Menu includes `zz_AntiAliasingOptions_P.pak/.ucas/.utoc`.

Every build runs both runtime test suites, checks ZIP entries against their
sources, then reruns the selected suite using only the packaged Lua files.
Menu builds also verify lossless UAssetAPI input round-trips, unchanged original
menu rows, correct new choices, and unchanged table contents after IoStore conversion.

Packages go to the repository's ignored `dist/`; scratch files go to `build/`.
.NET also creates ignored intermediate files under `tools/MenuAssets/obj` and
uses its NuGet cache. No checksum manifests or reports are shipped. Builds do not
install the mod, alter game settings, or publish releases.
In-game retesting of the shared-core packages remains pending.

## Runtime diagnostics

The deployed log is `FFRS/Binaries/Win64/ue4ss/UE4SS.log`.
This source revision logs `[TSR Always On] Applied TSR` or
`[Anti-Aliasing Options] Applied r.AntiAliasingMethod 4` after changing the method
and reading it back successfully. It stays quiet when the method is already correct;
persistent errors are logged at most once per minute.

For a direct check, a temporary UE4SS diagnostic can read
`UEHelpers.GetKismetSystemLibrary():GetConsoleVariableFloatValue("r.AntiAliasingMethod")`
on the game thread, after requiring `UEHelpers` and confirming the object is valid.
Values are Off = 0, FXAA = 1, TAA = 2, TSR = 4. Check after loads and cutscenes;
a saved menu highlight does not prove the renderer is using that method.
Remove temporary diagnostics afterward and preserve the user's graphics settings.

## Game-update monitoring

The private [ffres-mods-monitor](https://github.com/jmroon/ffres-mods-monitor)
repo checks Steam app **3259780** daily at **13:23 UTC** using SteamCMD.net metadata.
A build differing from `steam_build` in [supported-inputs.json](supported-inputs.json)
fails the run and opens an issue here assigned to `jmroon`; repeated checks reuse
the issue. Closing it without updating the baseline causes it to reopen.

Follow the [compatibility skill](../../.agents/skills/ffres-compatibility/SKILL.md)
before acknowledging a build. Push the verified baseline to this repo's `main`,
rerun `monitor.yml` in the monitor repo, then record results and close the issue.
A release is needed only when the distributed mod needs changes.

The monitor's Actions secret `FFRES_ISSUES_TOKEN` is a fine-grained token restricted
to `jmroon/ffres-mods` with **Issues: Read and write** (plus automatic Metadata read).
Renew it before expiry and replace the secret in the monitor repo's Actions settings.
Credential/network failures need investigation, not a baseline bump.
Contributors may lack access to that private repo; local compatibility checks still
work, but the maintainer must verify the monitor result.
