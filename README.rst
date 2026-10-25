ffres-mods
==========

Small, independently installable mods for FINAL FANTASY RESONANCE.
Each mod has its own source, build instructions and version under ``mods/``.

Anti-Aliasing Options
--------------------

Adds an AA Method row to Graphics Settings: Off / FXAA / TAA / TSR.
Maintains the saved selection through loading and cutscenes. Rendering
resolution, TSR history quality and other graphics effects remain game-controlled.

* Player instructions: ``mods/anti-aliasing-options/INSTALL.txt``
* Source and design: ``mods/anti-aliasing-options/README.rst``
* Build instructions: ``mods/anti-aliasing-options/BUILD.rst``
* Local release ZIPs: ``dist/`` (excluded from Git)

Development
-----------

Python 3.11 or newer. In a virtual environment::

    python -m pip install -r requirements-dev.txt
    python mods/anti-aliasing-options/tests/test_runtime.py

Asset builds additionally require retoc and compatible mappings supplied locally.
Game assets, mappings, UE4SS binaries, saves and generated packages are excluded
from this repository. Build outputs never modify the installed game.

Suggested future release tag: ``anti-aliasing-options-v1.0.0``. Each future mod
can use its own version and release tag without versioning the whole collection.
No automatic release publication or upload workflow is configured.

License
-------

Original code and documentation are MIT licensed; see ``LICENSE``.
The license does not grant rights to game content or third-party components.
See the mod's ``THIRD-PARTY.txt`` for dependency credits and asset scope.
