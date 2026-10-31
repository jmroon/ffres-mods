# Handoff — 2026-10-30

## Latest candidate

- Fixed panel: 742 × 472 UI units, inset 20 from the top-right.
- Commands: width 318; party: width 400, four reserved rows of height 82.
- Footer reserves 128 units; its inner ScrollBox is 726 × 120.
- Portrait crops use the native UnitId binding; active-row visibility uses the
  original `bIsInAction -> SetIsAction -> WBP_Subhead` path.
- Timelines retain a 110-unit gap and 52-unit marker spacing.
- Menu row/selection textures and small nine-slice portrait ornaments reuse
  the game's artwork. Shared equipment/menu buttons are not modified.
- All font sizes, including omitted/inherited defaults, match the vanilla source.
  Cinzel/Lato faces and Lato narration routing remain.

The previous flat candidate was inspected in game: main commands, Skills,
Items and item scrolling stayed in the fixed frame. Lasswell's outline covered
his portrait and stats. Stock tooltips overlapped content in that version.

The latest decorated candidate was installed and the maintainer reported that
it looked nice. This is aesthetic approval, **not** confirmation of tooltip
lifecycle or every battle state. The project was then shelved and uninstalled.

## Checks still required

- Real-engine help-strip attachment, wrapping, Show/Hide Tips and scrolling.
- Controller access to long help text, and normal tooltip restoration outside battle.
- Four-member parties, active-highlight transfer and crowded status icons.
- Limit availability/prompts, targeting, nested menus, battle exit and loading.
- Default-size clipping, long/localized names and numbers, 1080p/other resolutions.
- Full font coverage outside battle, including shops, dialogue and narration.
- Other languages retain their mappings but have not been visually tested.

The helper polls on the game thread every 250 ms. It reparents only the existing
tooltip's inner content and restores its original parent afterward. Its UObject
selection and API assumptions need live verification; mock tests are insufficient.
There is no claim of compatibility with other battle HUD or widget-editing mods.

## Resuming

Start with the font-only target and a known clean baseline. Back up installed
containers before trying the dock. Check the help adapter's `[Battle Dock]`
messages in `FFRS/Binaries/Win64/ue4ss/UE4SS.log`, then work through the outstanding
cases above. Runtime diagnostic probes from development are not included.
This branch also preserves the uncommitted narration and battle-font groundwork
needed to generate the eleven-asset baseline; it is not a new font release.
