# Battle Dock — parked WIP

An unfinished battle UI experiment: commands and portrait-based party status
share a fixed top-right panel, using artwork from the game's main menu.
Tooltips move into a scrollable help strip during battle.

**Shelved on 2026-10-30. Not a release or a supported download.**
The maintainer liked the latest appearance, then restored the font-only mod
at default font sizes. Existing AA and other unrelated mods were preserved.

- Asset edits retain the game's battle logic, portrait bindings and selection state.
- `runtime/main.lua` is a small UE4SS adapter for the help strip.
- The test package combines the dock with Cinzel/Lato to avoid overlapping
  widget overrides. All authored font sizes are restored to their game defaults.
- `font-only` builds that rollback separately, without dock assets or scripts.

See [build instructions](BUILD.md) and [handoff/testing notes](STATUS.md).
Do not treat successful asset checks as proof that every battle state works.
