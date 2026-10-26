---
name: ffres-compatibility
description: Verify ffres-mods after a Steam game update, investigate compatibility failures, guide in-game testing, and reconcile the update-monitor issue and tested-build baseline.
---

# Verify a game update

Drive the checks that can be automated and guide the user through the game checks.
A Steam build change calls for verification; it does not prove a mod is broken.

## Establish the target

- Read the reported issue in `jmroon/ffres-mods` and the latest run in the private
  `jmroon/ffres-mods-monitor` repo. Distinguish a build mismatch from a network or
  credential failure; the latter is not grounds to change the compatibility baseline.
- Compare the current Steam public build for app **3259780**, the installed
  `buildid` in Steam's `appmanifest_3259780.acf`, and
  [supported-inputs.json](../../../mods/anti-aliasing/supported-inputs.json).
  Confirm the installed game has finished updating to the build being tested.
  Use the monitor's current implementation for the remote lookup.
- Inspect the working tree and identify the published packages and source revision
  under test. A successful test of unreleased code does not validate existing downloads.
- Locate local tools and game paths; do not assume a particular drive.
  Read [BUILD.md](../../../mods/anti-aliasing/BUILD.md) and
  [INSTALL.txt](../../../mods/anti-aliasing/INSTALL.txt) for current requirements.
  BUILD.md also covers mapping generation, pinned downloads, runtime diagnostics,
  and private monitor access/credential renewal.
  Preserve saves, unrelated mods (including the font mod), and graphics settings.
  Close the game before replacing mod files.

## Run the local checks

From `mods/anti-aliasing`:

```powershell
uv run --locked python -m unittest discover -s tests -v
uv run --locked tools/build_release.py --variant tsr
```

For the menu variant, extract **fresh vanilla assets from the updated game** into
a new ignored build directory. Exclude the installed menu override from the
extraction. Reusing an old extraction cannot verify the updated assets.

Follow BUILD.md to build the menu target with that extraction, compatible mappings,
retoc, and the updated game's original Paks. The builder checks source hashes,
UAssetAPI round-trips, preservation of original menu rows, IoStore conversion,
and packaged Lua behavior. For each distributed Standalone variant, also build
with the pinned bundled loader and verify that package in game.

If hashes differ, compare the decoded vanilla tables and inspect the builder's
assumptions before accepting new hashes. Keep the original baseline available for
comparison. Stage new hashes locally only after understanding the changes, then
rebuild and test. Never remove checks or blindly refresh hashes to obtain a pass.
An engine update may also require revisiting mappings, retoc support, the UE4SS
version override, and hook/property names in `src/`.

## Guide in-game verification

Test one variant at a time using the installation/switching instructions.
First check every published package configuration, including Standalone with
the exact loader and settings shipped in its ZIP. A newer local UE4SS does not
validate those downloads. If changes are needed, repeat with rebuilt candidates;
record the package, loader version, and settings each result covers.

Give the user short, concrete actions and collect results as they go:

- **Menu:** verify Off / FXAA / TAA / TSR display and work. Check Apply and
  persistence across a full restart, plus discarding an unapplied preview.
  Verify TAA and TSR survive returning to title/Continue, a level change, and
  a conversation or cutscene.
- **TSR-only:** verify TSR activates after loading and survives the same transitions.
- Inspect the deployed UE4SS log for errors. Check actual
  `r.AntiAliasingMethod` when live diagnostics are available:
  Off = 0, FXAA = 1, TAA = 2, TSR = 4. A highlighted menu choice alone is insufficient;
  the game previously restored the saved highlight while resetting rendering to FXAA.
  Distinguish live readings, log evidence, and the user's visual observations.
- Confirm other graphics settings remain unchanged.

If the game or user is unavailable, finish independent checks and report exactly
which runtime checks remain pending. Do not claim compatibility from mocked Lua
tests or a successful package build alone.

## Record the result and clear the monitor

Only advance `steam_build` once both variants and their distributed loader
configurations are verified on the target build. If Standalone testing is
missing or fails, leave the baseline unchanged and the maintenance issue open.
Update asset hashes only for reviewed changes. Keep extracted game files,
mappings, saves, and credentials out of Git.

If existing downloads work, a compatibility acknowledgement is sufficient.
If code, menu assets, or the bundled loader changed, prepare a versioned release
using the repository's current conventions. Identify downloads that remain
incompatible; a green build monitor does not mean fixed packages were published.

Follow the session's authorization for commits, pushes, releases, and issue
comments/closure; invoking this skill alone does not authorize those actions.
When pushing the verified baseline to `ffres-mods/main` is authorized, rerun:

```powershell
gh workflow run monitor.yml --repo jmroon/ffres-mods-monitor --ref main
```

Inspect that run's result. If Steam has advanced again, test the newer build
instead of acknowledging it automatically. If access to the private monitor is
unavailable, report that limitation rather than claiming it passed.

When authorized, record the tested build, packages, automated and runtime results,
and any release link in the issue, then close it after the normal check passes.
Closing the issue alone does not acknowledge compatibility: the monitor reopens
it while the build differs. Leave unresolved compatibility failures open.

Finish with the evidence, changes made, monitor/issue status, and any remaining
user action. Keep the report concise.
