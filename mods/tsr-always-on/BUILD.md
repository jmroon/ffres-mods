# Building TSR Always On

Run from the repository root with Python 3.11+ and `requirements-dev.txt`
installed. No game assets, mappings, retoc or .NET are needed.

```powershell
python tools/build_release.py --variant tsr
python tools/build_release.py --variant tsr --bundle-loader --ue4ss "D:\ModTools\UE4SS_v3.0.1-1181-g83330635.zip"
```

These create `dist/TSR-Always-On-1.0.0-Script-only.zip` and
`dist/TSR-Always-On-1.0.0-Standalone.zip`, respectively.
Use the original non-dev UE4SS archive from its
[upstream release](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/experimental-latest).
The builder checks that it matches the tested version.

The entry script always requests TSR. [The shared core](../../shared/aa_core.lua)
handles applying it, hooks and polling. Both Lua files are included in the mod's
own `Scripts` folder; no separate shared mod is required.

Every build tests both variants, verifies the ZIP contents against their sources,
and runs the selected variant's tests again using only the packaged Lua files.
The standalone package adds UE4SS, its helper and license, and the tested engine
version override. Example mods and debug tools are omitted.

Builds do not install the mod or change game files. The refactored packages have
not been retested in game; the previous standalone version was confirmed active.
