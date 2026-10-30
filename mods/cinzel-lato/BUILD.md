# Building Cinzel + Lato

Requires uv, .NET 10, retoc 0.1.5 and repak 0.2.2. From this folder:

```powershell
uv run --locked tools/build.py --source <vanilla-legacy-folder> --mappings <FFRS.usmap> --game-paks <game-Paks-folder> --dotnet <dotnet.exe> --retoc <retoc.exe> --repak <repak.exe> --output ../../build/cinzel-lato-release
```

The source extraction must retain the original folders for `WBP_SystemSubhead`
and `SystemTextNUM`, their `.uexp` files, and `scriptobjects.bin`. Obtain them
from an unmodified installation with retoc's `to-legacy --version UE5_6
--no-shaders --no-parallel`; use `--filter` once for each asset name.
Mapping generation and tool links are in the [AA build notes](../anti-aliasing/BUILD.md).
Do not commit extracted game assets, mappings or build outputs.

The build pins FontTools 4.60.1 and UAssetAPI 1.1.0. It checks the source hashes,
lossless asset serialization, exact edit scope, static font weights and common
glyph coverage. Both assets are converted to IoStore and back, and their resolved
contents compared. Every packed font payload and ZIP entry is checked byte for byte.
Verification uses temporary read-only hardlinks to the base containers beside
FFRS.exe and removes the links afterward. It does not change installed mods.

`VERSION` names the output ZIP. It contains three containers, instructions and
font licenses. The builder does not install, commit or publish anything.

`tools/build.py` maps ten font payloads. `tools/FontAssets` redirects the shared
section-heading widget and extends only the Western numeric font's ASCII coverage.
Font sizes, spacing, scaling and decoration remain unchanged. Cinzel is a static
600-weight instance; its missing Semibold STAT label is supplied before conversion.
Lato Regular, Bold and Italic remain unmodified. Fonts come from Google Fonts
`ofl/cinzel` and `ofl/lato`; original font licenses are retained beside them.

## Testing coverage

Source build: Steam 24250053, Unreal 5.6. The maintainer tested Lato and then
Cinzel Semibold in game and approved their appearance. This is not an exhaustive
validation of every screen or language. Long/localized names, dialogue wrapping,
shops, chapter titles, battle numbers, alternate resolutions and transitions
remain checks for broader testing. Japanese, Chinese and Korean mappings were
not edited, but their rendering has not been tested.

The original file/container names from the approved test are retained, so the
release replaces those three files directly. Earlier font variants use different
names and must be removed before installing this one.
