"""Load source or packaged Lua with the same per-mod module lookup as UE4SS."""

import os
from pathlib import Path

from lupa.lua54 import LuaRuntime

REPO = Path(__file__).resolve().parents[1]


def load_mod(mod, setup=""):
    scripts = Path(os.environ.get("FFRES_TEST_SCRIPTS", mod / "src"))
    # Packaged tests must resolve the core from the ZIP, never from the repository.
    modules = scripts if "FFRES_TEST_SCRIPTS" in os.environ else REPO / "shared"
    lua = LuaRuntime()
    lua.execute((REPO / "tests/unreal_fixture.lua").read_text(encoding="utf-8"))
    lua.globals().package.path = (modules / "?.lua").as_posix()
    lua.execute(setup)
    lua.execute((scripts / "main.lua").read_text(encoding="utf-8"))
    return lua
