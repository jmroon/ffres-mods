"""Build script-only and extract-and-play TSR packages without touching the game."""

import argparse
import configparser
import hashlib
from pathlib import Path
import re
import subprocess
import sys
import zipfile

MOD = Path(__file__).resolve().parents[1]
REPO = MOD.parents[1]
BINARIES = "FFRS/Binaries/Win64"
SCRIPT = f"{BINARIES}/ue4ss/Mods/TSRAlwaysOn/Scripts/main.lua"
ENABLED = f"{BINARIES}/ue4ss/Mods/TSRAlwaysOn/enabled.txt"
UE4SS_VERSION = "v3.0.1-1181-g83330635"
# Accept only the original loader archive used in our game tests.
UE4SS_SHA256 = "b9a166a756605b21058a64cde93bcaab1a855dba3553e5c7e6ce0fa8fdfad7b9"
LOADER_FILES = (
    "dwmapi.dll",
    "ue4ss/UE4SS.dll",
    "ue4ss/Mods/shared/UEHelpers/UEHelpers.lua",
    "ue4ss/LICENSE",
)


def loader_files(path):
    if hashlib.sha256(path.read_bytes()).hexdigest() != UE4SS_SHA256:
        raise ValueError(f"Use the original UE4SS_{UE4SS_VERSION}.zip (non-dev)")
    with zipfile.ZipFile(path) as archive:
        files = {f"{BINARIES}/{name}": archive.read(name) for name in LOADER_FILES}
        settings = archive.read("ue4ss/UE4SS-settings.ini").decode("utf-8").replace("\r", "")

    # Preserve the non-dev defaults; only supply the engine override tested with FFRS.
    for key, value in (("MajorVersion", 5), ("MinorVersion", 6)):
        settings, count = re.subn(
            rf"(?m)^{key}[ \t]*=[ \t]*$", f"{key} = {value}", settings
        )
        if count != 1:
            raise ValueError(f"Unexpected UE4SS setting: {key}")
    config = configparser.ConfigParser()
    config.read_string(settings)
    if dict(config["EngineVersionOverride"]).get("minorversion") != "6":
        raise ValueError("Engine override was not applied")
    for name in ("ConsoleEnabled", "GuiConsoleEnabled", "GuiConsoleVisible"):
        if config.getint("Debug", name) != 0:
            raise ValueError("Expected non-dev loader settings")
    files[f"{BINARIES}/ue4ss/UE4SS-settings.ini"] = settings.encode("utf-8")
    files[f"{BINARIES}/ue4ss/Mods/mods.txt"] = b"TSRAlwaysOn : 1\n"
    return files


def write_verified_zip(path, files):
    # Validate the candidate before replacing a previous successful build.
    candidate = path.with_suffix(".tmp")
    try:
        with zipfile.ZipFile(candidate, "w") as archive:
            for name, content in sorted(files.items()):
                info = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.create_system = 3
                info.external_attr = 0o100644 << 16
                archive.writestr(info, content, compresslevel=9)
        with zipfile.ZipFile(candidate) as archive:
            if set(archive.namelist()) != set(files) or len(archive.namelist()) != len(files):
                raise ValueError("Unexpected archive contents")
            if archive.testzip() is not None:
                raise ValueError("Corrupt archive entry")
            for name, content in files.items():
                if archive.read(name) != content:
                    raise ValueError(f"Packaged content differs: {name}")
        candidate.replace(path)
    finally:
        candidate.unlink(missing_ok=True)
    print(f"Built and verified: {path} ({len(files)} files)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ue4ss", type=Path, required=True, help="Original tested non-dev UE4SS ZIP")
    args = parser.parse_args()
    loader = loader_files(args.ue4ss)
    for mod in ("anti-aliasing-options", "tsr-always-on"):
        subprocess.run([sys.executable, str(REPO / "mods" / mod / "tests/test_runtime.py")], check=True)

    readme = (MOD / "INSTALL.txt").read_text(encoding="utf-8")
    license_text = (REPO / "LICENSE").read_text(encoding="utf-8")
    script_files = {
        SCRIPT: (MOD / "src/main.lua").read_bytes(),
        ENABLED: b"",
        "README.txt": (readme + "\n" + license_text).encode("utf-8"),
    }
    version = (MOD / "VERSION").read_text().strip()
    dist = REPO / "dist"
    dist.mkdir(exist_ok=True)
    write_verified_zip(dist / f"TSR-Always-On-{version}-Script-only.zip", script_files)
    write_verified_zip(dist / f"TSR-Always-On-{version}-Standalone.zip", {**loader, **script_files})


if __name__ == "__main__":
    main()
