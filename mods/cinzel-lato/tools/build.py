"""Build and verify the font package without changing the game installation."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile
import zipfile

from fontTools.ttLib import TTFont
from fontTools.otlLib.builder import buildStatTable
from fontTools.varLib.instancer import instantiateVariableFont

MOD = Path(__file__).resolve().parents[1]

CONTAINER = "zz_CinzelSemiboldLato_P"
VERSION = (MOD / "VERSION").read_text().strip()
FONT_PATH = Path("FFRS/Content/UI/Font/FontFace/SE")
FACE_FILES = {
    "SE-GARRITHR": "Cinzel-Semibold.ttf",
    "SE-MLSDS": "Cinzel-Semibold.ttf",
    "SE-MLSDT": "Cinzel-Semibold.ttf",
    "SE-VOGEN": "Lato-Regular.ttf",
    "SE-VOGEB___": "Lato-Bold.ttf",
    "SE-VOGEI___": "Lato-Italic.ttf",
    "SE-ALPSN": "Lato-Regular.ttf",
    "SE-ALPSB": "Lato-Bold.ttf",
    "SE-ALPSI": "Lato-Italic.ttf",
    "SE-ALPSCB": "Lato-Regular.ttf",
}


def run(*arguments):
    subprocess.run([str(arg) for arg in arguments], check=True)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def check_inputs(source):
    expected = json.loads((MOD / "supported-inputs.json").read_text())
    for relative, digest in expected["assets"].items():
        data = (source / relative).read_bytes()
        require(hashlib.sha256(data).hexdigest() == digest,
                f"Unsupported source asset: {relative}; recheck compatibility before building.")
    require((source / "scriptobjects.bin").is_file(), "Missing scriptobjects.bin")


def static_fonts(output):
    output.mkdir()
    variable = TTFont(MOD / "fonts/Cinzel.ttf")
    # Cinzel supports 600 but its STAT table names only 400, 700 and 900.
    # Supply that missing label before creating the static Semibold face.
    buildStatTable(variable, [{"tag": "wght", "name": "Weight", "values": [
        {"value": 400, "name": "Regular", "flags": 2, "linkedValue": 700},
        {"value": 600, "name": "SemiBold"},
        {"value": 700, "name": "Bold"},
        {"value": 900, "name": "Black"},
    ]}])
    font = instantiateVariableFont(variable, {"wght": 600}, inplace=False, updateFontNames=True)
    font.save(output / "Cinzel-Semibold.ttf")
    weights = {"Cinzel-Semibold.ttf": 600, "Lato-Regular.ttf": 400,
               "Lato-Bold.ttf": 700, "Lato-Italic.ttf": 400}
    for name in weights:
        if name.startswith("Lato"):
            shutil.copy2(MOD / "fonts" / name, output / name)
    for name, weight in weights.items():
        font = TTFont(output / name)
        require("fvar" not in font, f"Variable font remains: {name}")
        require(font["OS/2"].usWeightClass == weight, f"Wrong weight: {name}")
        sample = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 éèêëàâäçñöüß¿¡‘’“”–—%&+/"
        require(all(ord(c) in font.getBestCmap() for c in sample), f"Missing common glyph: {name}")


def pack_fonts(repak, pak, work, fonts):
    contents = work / "pak-contents"
    run(repak, "unpack", pak, "--output", contents)
    target = contents / FONT_PATH
    target.mkdir(parents=True, exist_ok=True)
    for asset, name in FACE_FILES.items():
        data = (fonts / name).read_bytes()
        # Unreal's cooked .ufont payload is a little-endian byte count followed by the TTF.
        (target / (asset + ".ufont")).write_bytes(struct.pack("<I", len(data)) + data)
    rebuilt = work / "fonts.pak"
    run(repak, "pack", contents, rebuilt, "--version", "V11")
    shutil.copy2(rebuilt, pak)
    extracted = work / "verified-pak"
    run(repak, "unpack", pak, "--output", extracted)
    wanted = {p.relative_to(contents) for p in contents.rglob("*") if p.is_file()}
    actual = {p.relative_to(extracted) for p in extracted.rglob("*") if p.is_file()}
    require(actual == wanted, "Unexpected pak contents")
    for relative in wanted:
        require((contents / relative).read_bytes() == (extracted / relative).read_bytes(), f"Pak changed {relative}")
    for path in (extracted / FONT_PATH).glob("*.ufont"):
        data = path.read_bytes()
        require(struct.unpack_from("<I", data)[0] == len(data) - 4, "Bad .ufont length")
        TTFont(io.BytesIO(data[4:]))
    print("Verified all ten packaged font payloads.")


def verify_containers(args, tool, legacy, containers, work):
    # Imports refer to original packages. Read-only hardlinks let retoc resolve them
    # without copying gigabytes or including other installed mods in the check.
    game_root = args.game_paks.resolve().parents[2]
    stage = Path(tempfile.mkdtemp(prefix=".font-validation-", dir=game_root))
    created = []
    try:
        for root in (args.game_paks, containers):
            for path in root.iterdir():
                if path.suffix not in (".utoc", ".ucas"):
                    continue
                target = stage / path.name
                if path.drive.lower() == stage.drive.lower():
                    os.link(path, target)
                else:
                    shutil.copy2(path, target)
                created.append(target)
        command = [args.retoc, "to-legacy", stage, work / "roundtrip", "--version", "UE5_6", "--no-shaders", "--no-parallel"]
        for name in ("WBP_SystemSubhead", "SystemTextNUM"):
            command.extend(["--filter", name])
        run(*command)
        run(*tool, "verify", args.mappings, legacy, work / "roundtrip")
    finally:
        # Only unlink files created above; never recursively delete game paths.
        require(stage.resolve().parent == game_root, "Invalid validation cleanup path")
        for path in created:
            require(path.parent == stage, "Invalid validation file")
            path.unlink()
        stage.rmdir()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "mappings", "retoc", "repak", "dotnet", "game-paks", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    check_inputs(args.source)
    args.output.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="build-", dir=args.output))
    tool_dir = work / "tool"
    run(args.dotnet, "build", MOD / "tools/FontAssets/FontAssets.csproj", "-c", "Release", "-p:RestoreLockedMode=true", "-o", tool_dir)
    tool = (args.dotnet, tool_dir / "FontAssets.dll")
    legacy = work / "legacy"
    run(*tool, "build", args.mappings, args.source, legacy)
    containers = work / "containers"
    containers.mkdir()
    run(args.retoc, "to-zen", "--version", "UE5_6", legacy, containers / (CONTAINER + ".utoc"))
    fonts = work / "static-fonts"
    static_fonts(fonts)
    pack_fonts(args.repak, containers / (CONTAINER + ".pak"), work, fonts)
    verify_containers(args, tool, legacy, containers, work)
    files = {"FFRS/Content/Paks/~mods/" + p.name: p for p in containers.iterdir()}
    require(len(files) == 3, "Expected exactly three container files")
    files["README.txt"] = MOD / "INSTALL.txt"
    for name in ("Cinzel-OFL.txt", "Lato-OFL.txt"):
        files["Font Licenses/" + name] = MOD / "fonts" / name
    archive = args.output / f"Cinzel-Lato-{VERSION}.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as package:
        for name, path in sorted(files.items()):
            package.writestr(name, path.read_bytes())
    with zipfile.ZipFile(archive) as package:
        require(set(package.namelist()) == set(files), "ZIP file list mismatch")
        for name, path in files.items():
            require(package.read(name) == path.read_bytes(), f"ZIP changed {name}")
    print(f"Verified ZIP: {archive}\nContainers: {containers}\nPackage checks passed; consult BUILD.md for in-game testing coverage.")


if __name__ == "__main__":
    main()
