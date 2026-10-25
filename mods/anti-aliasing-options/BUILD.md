# Building Anti-Aliasing Options

Run commands from the repository root. Python 3.11+, Lupa 2.6, the .NET 10 SDK,
and retoc 0.1.5 are required. No Unreal Editor installation is needed.

These are build/test tools; none are installed with the mod:

* `tools/build_release.py` runs tests, packages the mod, and checks the ZIP.
* `tools/MenuAssets/Program.cs` adds the AA row and choices using UAssetAPI
  1.1.0, which handles reading and writing the asset format.
* `tests/test_runtime.py` tests the Lua script using `tests/unreal_fixture.lua`.

Use a virtual environment:

```powershell
python -m venv .venv
.venv\Scripts\python -m pip install -r requirements-dev.txt
.venv\Scripts\python mods/anti-aliasing-options/tests/test_runtime.py
```

Supply these local inputs (not committed or shipped):

* A vanilla retoc legacy extraction containing `DT_OP_GraphicSetting.uasset`
  and `DT_OP_GraphicSetting_Rank.uasset`, their `.uexp` siblings, their game
  directory structure, and `scriptobjects.bin` at the extraction root.
* A compatible uncompressed version-4 `.usmap` mapping. The development build
  used mappings named `FFRS-DEMO.usmap`; the filename itself is not important.
  Obtain/dump mappings separately; they are not bundled or fetched by the build.
* The game's original `FFRS/Content/Paks` folder, for its `global.utoc` and
  `global.ucas`. These are used only for verification and never packaged.

Example extraction with retoc (replace the example paths):

```powershell
retoc to-legacy "D:\Games\FINAL FANTASY RESONANCE\FFRS\Content\Paks" build/vanilla --filter DT_OP_GraphicSetting --no-shaders --version UE5_6
```

Use vanilla inputs, without a menu override contaminating the extraction.
The builder checks the four source asset hashes in `supported-inputs.json`
before changing anything. A hash mismatch means the inputs need investigation;
do not simply remove the check to support a new game build.

Build:

```powershell
.venv\Scripts\python mods/anti-aliasing-options/tools/build_release.py --source build/vanilla --mappings "D:\ModTools\FFRS.usmap" --retoc "D:\ModTools\retoc.exe" --game-paks "D:\Games\FINAL FANTASY RESONANCE\FFRS\Content\Paks"
```

The build restores the pinned UAssetAPI dependency from NuGet and compiles the
menu tool automatically. Its dependency lock file is included in the source. If `dotnet` is
not on PATH, pass `--dotnet "D:\ModTools\dotnet\dotnet.exe"`.

UAssetAPI must read and write each input without changing its bytes. The builder
then clones the existing menu row, edits named properties, and verifies the saved
result. No custom binary parser or byte-offset patching is used.

The build runs the Lua tests, then verifies all ten original rows are unchanged,
the new method labels have the expected order, and both assets decode identically
after IoStore conversion. It then writes one file to the ignored `dist/` directory:

* `Anti-Aliasing-Options-1.0.0.zip`

The ZIP contains only the five mod files and README.txt, including credits and
the MIT license. The build checks its contents and requires the runtime script
to match `src/main.lua` byte-for-byte. Checks report to the console; no checksum
files, manifests or verification reports are generated.

Each build uses a fresh temporary folder under `build/`. Game files and
caller-supplied input folders are only read. The build restores .NET dependencies
but does not download retoc or mappings, install the mod, edit saves/configuration,
launch the game or publish a release.

Tool references:

* [UAssetAPI](https://github.com/atenfyr/UAssetAPI)
* [retoc](https://github.com/trumank/retoc)
* [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS)
