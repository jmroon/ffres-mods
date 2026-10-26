"""Load source or packaged Lua with the same per-mod module lookup as UE4SS."""

import os
from pathlib import Path

from lupa.lua54 import LuaRuntime

MOD = Path(__file__).resolve().parents[1]


def load_mod(variant, setup=""):
    scripts = Path(os.environ.get("FFRES_TEST_SCRIPTS", MOD / "src"))
    # Packaged tests must resolve the core from the ZIP, never from the repository.
    entry = "main.lua" if "FFRES_TEST_SCRIPTS" in os.environ else f"{variant}.lua"
    lua = LuaRuntime()
    lua.execute((MOD / "tests/unreal_fixture.lua").read_text(encoding="utf-8"))
    lua.globals().package.path = (scripts / "?.lua").as_posix()
    lua.execute(setup)
    lua.execute((scripts / entry).read_text(encoding="utf-8"))
    return lua
