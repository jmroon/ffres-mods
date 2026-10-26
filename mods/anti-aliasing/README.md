# Anti-Aliasing Options

Choose one variant:

- **Menu:** adds Off / FXAA / TAA / TSR under Graphics Settings > AA Method
  and maintains your saved choice through loads and cutscenes.
- **TSR-only:** always enables TSR, without menu changes or save access.

Both use the same core and leave other graphics settings unchanged.
TSR improves stability in motion at a higher GPU cost than FXAA.

Use a **Standalone** ZIP if you need UE4SS, or the package without the loader
if UE4SS is already installed. Install only one variant at a time.
Existing installed folder and ZIP names are preserved.

[Installation and removal](INSTALL.txt) · [Building](BUILD.md) · [Credits](THIRD-PARTY.txt)

Source: [core](src/aa_core.lua), [menu entry](src/menu.lua), [TSR entry](src/tsr.lua).
The builder packages the selected entry as `Scripts/main.lua`, alongside `aa_core.lua`.

[Published downloads](https://github.com/jmroon/ffres-mods/releases).
