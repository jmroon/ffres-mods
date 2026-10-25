"""Build and verify a release without writing to the game directory."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile

MOD = Path(__file__).resolve().parents[1]
REPO = MOD.parents[1]
CONTAINER = "zz_AntiAliasingOptions_P"
CONTAINER_EXTENSIONS = ("pak", "ucas", "utoc")
RUNTIME = "FFRS/Binaries/Win64/ue4ss/Mods/AntiAliasingOptions"
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


def stage_release(stage, containers):
    """Collect only the runtime script, activation marker, menu assets and README."""
    script_dir = stage / RUNTIME / "Scripts"
    script_dir.mkdir(parents=True)
    shutil.copyfile(MOD / "src/main.lua", script_dir / "main.lua")
    (script_dir.parent / "enabled.txt").write_bytes(b"")
    (stage / PAKS).mkdir(parents=True)
    for extension in CONTAINER_EXTENSIONS:
        name = f"{CONTAINER}.{extension}"
        shutil.copyfile(containers / name, stage / PAKS / name)
    readme = (MOD / "INSTALL.txt").read_text(encoding="utf-8")
    license_text = (REPO / "LICENSE").read_text(encoding="utf-8")
    (stage / "README.txt").write_text(
        readme + "\n" + license_text, encoding="utf-8", newline="\n"
    )


def write_zip(stage, destination):
    """Stable ordering, timestamps and permissions make builds comparable."""
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(stage.rglob("*")):
            if not path.is_file():
                continue
            name = path.relative_to(stage).as_posix()
            info = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, path.read_bytes())


def verify_zip(archive_path):
    with zipfile.ZipFile(archive_path) as archive:
        expected = {
            f"{RUNTIME}/Scripts/main.lua",
            f"{RUNTIME}/enabled.txt",
            *(f"{PAKS}/{CONTAINER}.{extension}" for extension in CONTAINER_EXTENSIONS),
            "README.txt",
        }
        names = archive.namelist()
        if set(names) != expected or len(names) != len(expected):
            raise ValueError("Unexpected archive contents")
        if archive.testzip() is not None:
            raise ValueError("Archive CRC verification failed")
        if archive.read(f"{RUNTIME}/Scripts/main.lua") != (MOD / "src/main.lua").read_bytes():
            raise ValueError("Packaged script differs from source")
        if archive.read(f"{RUNTIME}/enabled.txt") != b"":
            raise ValueError("Activation marker must be empty")


def parse_arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="Vanilla retoc legacy extraction")
    parser.add_argument("--mappings", type=Path, required=True, help="Uncompressed v4 .usmap")
    parser.add_argument("--retoc", type=Path, required=True, help="retoc 0.1.5 executable")
    parser.add_argument("--game-paks", type=Path, required=True, help="Original Paks folder; read-only")
    parser.add_argument("--dotnet", default="dotnet", help=".NET 10 SDK executable")
    return parser.parse_args()


def main():
    args = parse_arguments()
    run(sys.executable, MOD / "tests/test_runtime.py")
    check_inputs(args.source)

    version = (MOD / "VERSION").read_text().strip()
    build_root = REPO / "build/anti-aliasing-options"
    dist = REPO / "dist"
    build_root.mkdir(parents=True, exist_ok=True)
    dist.mkdir(exist_ok=True)
    archive_path = dist / f"Anti-Aliasing-Options-{version}.zip"
    project = MOD / "tools/MenuAssets/MenuAssets.csproj"
    tool_output = build_root / "menu-tool"
    run(args.dotnet, "build", project, "-c", "Release", "-p:RestoreLockedMode=true", "-o", tool_output)
    menu_tool = (args.dotnet, tool_output / "MenuAssets.dll")

    # A fresh workspace per build; never delete a caller-supplied directory.
    with tempfile.TemporaryDirectory(prefix="release-", dir=build_root) as directory:
        work = Path(directory)
        legacy = work / "legacy"
        containers = work / "containers"
        stage = work / "stage"
        candidate = work / "candidate.zip"

        run(*menu_tool, "build", args.mappings, args.source.resolve(), legacy)
        convert_and_verify(
            legacy, containers, work / "roundtrip", args.retoc, args.game_paks,
            menu_tool, args.mappings,
        )
        stage_release(stage, containers)
        write_zip(stage, candidate)
        verify_zip(candidate)
        shutil.copyfile(candidate, archive_path)
    print(f"Built and verified: {archive_path}")


if __name__ == "__main__":
    main()
