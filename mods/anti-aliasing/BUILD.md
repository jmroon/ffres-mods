# Building Anti-Aliasing Options

Run from the repository root. Both targets use Python 3.11+ and Lupa 2.6:

```powershell
python -m venv .venv
.venv\Scripts\python -m pip install -r mods/anti-aliasing/requirements-dev.txt
.venv\Scripts\python -m unittest discover -s mods/anti-aliasing/tests -v
```

The source lives in this folder: `src/aa_core.lua` handles hooks and polling;
`src/menu.lua` reads the saved choice and previews menu edits; `src/tsr.lua`
always requests TSR. The selected entry is shipped as `Scripts/main.lua` with
its own copy of `aa_core.lua`. The single `VERSION` applies to both targets.

## TSR-only

```powershell
.venv\Scripts\python mods/anti-aliasing/tools/build_release.py --variant tsr
```

No game assets, mappings, retoc or .NET are needed.

## Menu

Requires the .NET 10 SDK, retoc 0.1.5 and these read-only inputs:

- A vanilla retoc legacy extraction with `DT_OP_GraphicSetting.uasset`,
  `DT_OP_GraphicSetting_Rank.uasset`, their `.uexp` files and original directory
  structure, plus `scriptobjects.bin` at the extraction root.
- A compatible uncompressed version-4 `.usmap`, obtained separately.
- The original game Paks folder, for `global.utoc` and `global.ucas` verification.

Example extraction, with no menu override installed:

```powershell
retoc to-legacy "D:\Games\FINAL FANTASY RESONANCE\FFRS\Content\Paks" build/vanilla --filter DT_OP_GraphicSetting --no-shaders --version UE5_6
```

Build with your local paths:

```powershell
.venv\Scripts\python mods/anti-aliasing/tools/build_release.py --variant menu --source build/vanilla --mappings "D:\ModTools\FFRS.usmap" --retoc "D:\ModTools\retoc.exe" --game-paks "D:\Games\FINAL FANTASY RESONANCE\FFRS\Content\Paks"
```

If needed, pass `--dotnet "D:\ModTools\dotnet\dotnet.exe"`.
The build restores pinned UAssetAPI 1.1.0 and compiles this mod's `tools/MenuAssets` project.
Inputs must match `supported-inputs.json`; investigate mismatches instead of
removing the check. Extracted game assets and mappings are never committed.

## Optional loader

Add these flags to either target for an extract-and-play Standalone ZIP:

```powershell
--bundle-loader --ue4ss "D:\ModTools\UE4SS_v3.0.1-1181-g83330635.zip"
```

Use the original non-dev archive from the
[UE4SS release](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/experimental-latest).
Its hash is checked against the tested version. Bundling preserves its DLLs,
helper and license, supplies the tested Unreal 5.6 override, and omits example mods.

## Output and verification

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
