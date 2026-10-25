Anti-Aliasing Options
====================

Adds Off / FXAA / TAA / TSR to the game's Graphics Settings as a separate
``AA Method`` row. The existing Anti-Aliasing quality row remains unchanged.

The game stores the selection in its native OptionSaveData. The Lua script
reads this setting and maps menu indices 0, 1, 2, 3 to engine methods 0, 1, 2, 4.
Index 3 in the engine is reserved for MSAA, so TSR needs this mapping.
The initial menu default is FXAA. Players explicitly select and apply TSR.

Why a script is needed
----------------------

The restored native menu can save its selection, but the game resets the
active method during loading and some event paths. A menu callback and five
load/cutscene hooks reapply the chosen method. A 250 ms poll handles missed
events and temporarily unavailable objects, with at most one queued check.
Console writes happen only when the active method differs. Persistent errors
are logged at most once a minute.

The script only writes ``r.AntiAliasingMethod``. It does not write resolution,
TSR history quality, exposure, bloom, DOF, scalability, configuration files or
save files. The game handles saving menu choices. No Engine.ini is required.

Source layout
-------------

* ``src/main.lua``: the complete runtime mod.
* ``tools/build_menu.py``: adds one menu row and creates its four-label table.
* ``tools/build_release.py``: validates inputs, rebuilds, decodes the packaged
  assets again, and assembles a verified ZIP.
* ``tests/test_runtime.py``: simulated engine/save/menu events and scope checks.

The asset builder preserves all ten original menu rows and adds two asset paths:

* Override: ``/Game/Datatable/UI/Option/Root/GraphicSetting/DT_OP_GraphicSetting``
* New table: ``/Game/Datatable/UI/Option/Root/GraphicSetting/DT_OP_GraphicSetting_Type``

The table's internal identifiers retain the tested names. Release filenames
and the UE4SS mod folder use ``AntiAliasingOptions``. The runtime differs from
the tested AA-only script only in its log prefix, now ``[Anti-Aliasing Options]``.

Compatibility and verification
------------------------------

Tested development installation: Steam Windows build 24250053, Unreal Engine
5.6, UE4SS v3.0.1-1181-g83330635. Live checks covered TAA across loading and TSR
across a conversation/cutscene and restart. This does not establish compatibility
with every scene or future game update.

The renamed release package still needs its own clean-install live check before
publication. See ``RELEASE-CHECKLIST.txt``. The local build verifies the menu
assets and archive without installing or launching anything.

TSR costs more GPU time than FXAA and may introduce temporal artifacts. This
mod selects the engine's AA method; it does not add DLSS, DLAA, frame generation,
sharpening or resolution presets. Mods overriding the same menu table or forcing
the AA method may conflict.
