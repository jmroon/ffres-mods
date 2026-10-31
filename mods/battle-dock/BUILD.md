# Rebuilding the WIP

Requires Python 3.11+, Lupa 2.6 for the mocked runtime checks, .NET 10,
UAssetAPI 1.1.0 (locked NuGet dependency), and retoc 0.1.5.
Use a Python environment with `lupa==2.6` installed.

First build the [Cinzel/Lato source on this branch](../cinzel-lato/BUILD.md).
Keep its eleven-asset `legacy` directory and completed font `.pak`.
Also retain a full original UI extraction and the matching Unreal 5.6 mappings.
Do not supply the older 79-asset smaller-UI experiment as the font baseline.
Extracted assets, mappings, compiled tools and local paths are not committed.

From this directory:

```powershell
python tools/build.py --target dock --source <vanilla-legacy> --mappings <FFRS.usmap> --font-baseline <font-build/legacy> --font-pak <font-build/containers/zz_CinzelSemiboldLato_P.pak> --game-paks <game/FFRS/Content/Paks> --dotnet <dotnet.exe> --retoc <retoc.exe> --output <build-output>
```

Use the same command with `--target font-only` for the default-size font rollback.
Every run uses a fresh build directory. Neither target installs or launches the game.
Verification makes temporary read-only hardlinks to original containers for
reference resolution; those links are removed afterward. Administrator access
may be needed for hardlinks on some Windows installations.

Checks cover lossless serialization, default font sizes, portrait bindings,
fixed bounds, and semantic IoStore readback (32 dock/font assets or 11 font-only
assets). ZIP entries are checked byte for byte. The Lua tests cover parent
restoration, visibility, failed attachment and invalid-widget recovery using
mocked engine objects; they do not verify the real engine API or controller input.

The retained font `.pak` supplies Cinzel/Lato payloads and must come from the
matching font builder. The dock builder does not regenerate those payloads.
The existing font licenses are included in both output ZIPs.
