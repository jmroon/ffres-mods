"""Build and round-trip the menu tables; all caller-supplied game files are read-only."""

import hashlib
import json
from pathlib import Path
import shutil
import subprocess

REPO = Path(__file__).resolve().parents[1]
MOD = REPO / "mods/anti-aliasing"
CONTAINER = "zz_AntiAliasingOptions_P"
CONTAINER_EXTENSIONS = ("pak", "ucas", "utoc")
PAKS = "FFRS/Content/Paks/~mods"


def run(*arguments):
    subprocess.run([str(argument) for argument in arguments], check=True)


def check_inputs(source):
    """Refuse unknown assets: our binary edits target one verified game build."""
    expected = json.loads((MOD / "supported-inputs.json").read_text())
    for name, checksum in expected["files"].items():
        matches = list(source.rglob(name))
        if len(matches) != 1:
            raise ValueError(f"Expected one source asset named {name}")
        actual = hashlib.sha256(matches[0].read_bytes()).hexdigest()
        if actual != checksum:
            raise ValueError(
                f"Unsupported or modified source asset: {name}. "
                "Use vanilla assets from the documented game/retoc build."
            )
    if not (source / "scriptobjects.bin").is_file():
        raise ValueError("Missing scriptobjects.bin in the extracted source folder.")


def convert_and_verify(legacy, containers, roundtrip, retoc, game_paks, menu_tool, mappings):
    """Package the two tables, extract them again, then compare decoded contents."""
    containers.mkdir()
    run(retoc, "to-zen", "--version", "UE5_6", legacy, containers / f"{CONTAINER}.utoc")
    # Global script objects are required for decoding, but are never shipped.
    for extension in ("utoc", "ucas"):
        name = f"global.{extension}"
        shutil.copy2(game_paks / name, containers / name)
    # Serial extraction avoids retoc intermittently omitting one of the tables.
    run(
        retoc, "to-legacy", "--version", "UE5_6", "--no-shaders",
        "--no-parallel", containers, roundtrip,
    )
    run(*menu_tool, "verify", mappings, legacy, roundtrip)


def build_menu_assets(args, work):
    check_inputs(args.source)
    project = MOD / "tools/MenuAssets/MenuAssets.csproj"
    tool_output = REPO / "build/anti-aliasing/menu-tool"
    run(args.dotnet, "build", project, "-c", "Release", "-p:RestoreLockedMode=true", "-o", tool_output)
    menu_tool = (args.dotnet, tool_output / "MenuAssets.dll")

    legacy = work / "legacy"
    containers = work / "containers"
    run(*menu_tool, "build", args.mappings, args.source.resolve(), legacy)
    convert_and_verify(
        legacy, containers, work / "roundtrip", args.retoc, args.game_paks,
        menu_tool, args.mappings,
    )
    return {
        f"{PAKS}/{CONTAINER}.{extension}": (containers / f"{CONTAINER}.{extension}").read_bytes()
        for extension in CONTAINER_EXTENSIONS
    }
