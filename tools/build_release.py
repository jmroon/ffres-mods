"""Build either AA mod, optionally with UE4SS, and test the packaged Lua."""

import argparse
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile

from menu_assets import build_menu_assets
from ue4ss import loader_files

REPO = Path(__file__).resolve().parents[1]
BINARIES = "FFRS/Binaries/Win64"
# Source directory, installed folder, release filename.
VARIANTS = {
    "menu": ("anti-aliasing-options", "AntiAliasingOptions", "Anti-Aliasing-Options"),
    "tsr": ("tsr-always-on", "TSRAlwaysOn", "TSR-Always-On"),
}


def run_tests(mod, scripts=None):
    env = os.environ.copy()
    env.pop("FFRES_TEST_SCRIPTS", None)
    if scripts is not None:
        env["FFRES_TEST_SCRIPTS"] = str(scripts)
    subprocess.run([sys.executable, str(mod / "tests/test_runtime.py")], env=env, check=True)


def runtime_files(mod, mod_folder):
    runtime = f"{BINARIES}/ue4ss/Mods/{mod_folder}"
    readme = (mod / "INSTALL.txt").read_text(encoding="utf-8")
    license_text = (REPO / "LICENSE").read_text(encoding="utf-8")
    return {
        f"{runtime}/Scripts/main.lua": (mod / "src/main.lua").read_bytes(),
        f"{runtime}/Scripts/aa_core.lua": (REPO / "shared/aa_core.lua").read_bytes(),
        f"{runtime}/enabled.txt": b"",
        "README.txt": (readme + "\n" + license_text).encode("utf-8"),
    }


def write_zip(path, files):
    # Stable ordering, timestamps and permissions keep builds reproducible.
    with zipfile.ZipFile(path, "w") as archive:
        for name, content in sorted(files.items()):
            info = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, content, compresslevel=9)


def verify_zip(path, files, mod, mod_folder, extracted):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if set(names) != set(files) or len(names) != len(files):
            raise ValueError("Unexpected archive contents")
        if archive.testzip() is not None:
            raise ValueError("Corrupt archive entry")
        for name, content in files.items():
            if archive.read(name) != content:
                raise ValueError(f"Packaged content differs: {name}")
        archive.extractall(extracted)
    scripts = extracted / BINARIES / "ue4ss/Mods" / mod_folder / "Scripts"
    run_tests(mod, scripts)


def parse_arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--variant", choices=VARIANTS, required=True)
    parser.add_argument("--bundle-loader", action="store_true", help="Include the tested UE4SS loader")
    parser.add_argument("--ue4ss", type=Path, help="Original tested non-dev UE4SS ZIP")
    parser.add_argument("--source", type=Path, help="Menu only: vanilla retoc legacy extraction")
    parser.add_argument("--mappings", type=Path, help="Menu only: uncompressed v4 .usmap")
    parser.add_argument("--retoc", type=Path, help="Menu only: retoc 0.1.5 executable")
    parser.add_argument("--game-paks", type=Path, help="Menu only: original Paks folder; read-only")
    parser.add_argument("--dotnet", default="dotnet", help="Menu only: .NET 10 SDK executable")
    args = parser.parse_args()
    if args.bundle_loader and args.ue4ss is None:
        parser.error("--bundle-loader requires --ue4ss")
    if args.ue4ss is not None and not args.bundle_loader:
        parser.error("Use --bundle-loader to include --ue4ss")
    if args.variant == "menu":
        for name in ("source", "mappings", "retoc", "game_paks"):
            if getattr(args, name) is None:
                parser.error(f"--variant menu requires --{name.replace('_', '-')}")
    return args


def main():
    args = parse_arguments()
    directory, mod_folder, release_name = VARIANTS[args.variant]
    mod = REPO / "mods" / directory
    files = runtime_files(mod, mod_folder)
    if args.bundle_loader:
        files.update(loader_files(args.ue4ss, mod_folder))
    # Both entry scripts depend on the core, so check both on every build.
    for source, _, _ in VARIANTS.values():
        run_tests(REPO / "mods" / source)

    version = (mod / "VERSION").read_text().strip()
    suffix = ""
    if args.bundle_loader:
        suffix = "-Standalone"
    elif args.variant == "tsr":
        suffix = "-Script-only"
    destination = REPO / "dist" / f"{release_name}-{version}{suffix}.zip"
    destination.parent.mkdir(exist_ok=True)
    build_root = REPO / "build" / directory
    build_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="release-", dir=build_root) as directory:
        work = Path(directory)
        if args.variant == "menu":
            files.update(build_menu_assets(args, work))
        candidate = work / "candidate.zip"
        write_zip(candidate, files)
        verify_zip(candidate, files, mod, mod_folder, work / "extracted")
        candidate.replace(destination)
    print(f"Built and verified: {destination} ({len(files)} files)")


if __name__ == "__main__":
    main()
