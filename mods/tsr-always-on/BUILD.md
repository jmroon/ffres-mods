# Building TSR Always On

Requires Python 3.11+ and the repository's `requirements-dev.txt` dependencies.
No game assets, mappings, retoc or .NET are needed.

From the repository root:

```powershell
python mods/tsr-always-on/tools/build_release.py --ue4ss "D:\ModTools\UE4SS_v3.0.1-1181-g83330635.zip"
```

Use the original non-dev UE4SS archive from its
[upstream release](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/experimental-latest).
The builder checks the tested archive, runs both mods' Lua tests, and creates:

- `dist/TSR-Always-On-1.0.0-Standalone.zip`: loader, required helper, license,
  the tested engine-version override, and the enabled script.
- `dist/TSR-Always-On-1.0.0-Script-only.zip`: enabled script and README only.

The standalone bundle omits UE4SS's example mods and debug tools. The DLLs,
helper and license are copied unchanged. No game files or settings are edited.
The shared Unreal fixture lives at `tests/unreal_fixture.lua` in the repository.

Version 1.0.0 passed 16 automated runtime tests and archive checks. TSR was
confirmed visibly active in game with the standalone package installed.
Dedicated load, cutscene and uninstall checks of this package remain unconfirmed.
