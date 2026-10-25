"""Build and round-trip verify a release without writing to the game directory."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile

from build_menu import build_menu
from decode_menu import decode
from unversioned import load_mappings

MOD = Path(__file__).resolve().parents[1]
REPO = MOD.parents[1]
CONTAINER = 'zz_AntiAliasingOptions_P'
RUNTIME = 'FFRS/Binaries/Win64/ue4ss/Mods/AntiAliasingOptions'
PAKS = 'FFRS/Content/Paks/~mods'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(*args):
    subprocess.run([str(arg) for arg in args], check=True)


def check_inputs(source):
    expected = json.loads((MOD / 'supported-inputs.json').read_text())
    for name, checksum in expected['files'].items():
        matches = list(source.rglob(name))
        if len(matches) != 1 or digest(matches[0]) != checksum:
            raise ValueError(f'Unsupported or modified source asset: {name}. '
                             'Use vanilla assets from the documented game/retoc build.')
    if not (source / 'scriptobjects.bin').is_file():
        raise ValueError('Missing scriptobjects.bin in the extracted source folder.')


def write_zip(stage, destination):
    """Stable ordering, timestamps and permissions make repeated builds comparable."""
    with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(stage.rglob('*')):
            if path.is_file():
                info = zipfile.ZipInfo(path.relative_to(stage).as_posix(), (2026, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.create_system = 3
                info.external_attr = 0o100644 << 16
                archive.writestr(info, path.read_bytes())


def verify_zip(archive_path):
    with zipfile.ZipFile(archive_path) as archive:
        expected = {
            f'{RUNTIME}/Scripts/main.lua', f'{RUNTIME}/enabled.txt',
            *(f'{PAKS}/{CONTAINER}.{ext}' for ext in ('pak', 'ucas', 'utoc')),
            'README.txt',
        }
        if set(archive.namelist()) != expected or len(archive.namelist()) != len(expected):
            raise ValueError('Unexpected archive contents')
        if archive.testzip() is not None:
            raise ValueError('Archive CRC verification failed')
        if archive.read(f'{RUNTIME}/Scripts/main.lua') != (MOD / 'src/main.lua').read_bytes():
            raise ValueError('Packaged script differs from source')
        if archive.read(f'{RUNTIME}/enabled.txt') != b'':
            raise ValueError('Activation marker must be empty')


def main():
    if not __debug__:
        raise RuntimeError('Do not use python -O: asset assertions are required.')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True, help='Vanilla retoc legacy extraction')
    parser.add_argument('--mappings', type=Path, required=True, help='Compatible uncompressed v4 .usmap')
    parser.add_argument('--retoc', type=Path, required=True, help='retoc 0.1.5 executable')
    parser.add_argument('--game-paks', type=Path, required=True, help='Original Paks folder; read-only')
    args = parser.parse_args()
    run(sys.executable, MOD / 'tests/test_runtime.py')
    check_inputs(args.source)
    load_mappings(args.mappings)
    version = (MOD / 'VERSION').read_text().strip()
    build_root = REPO / 'build/anti-aliasing-options'
    dist = REPO / 'dist'
    build_root.mkdir(parents=True, exist_ok=True)
    dist.mkdir(exist_ok=True)
    archive_path = dist / f'Anti-Aliasing-Options-{version}.zip'
    # Every invocation gets a fresh workspace. Never delete a caller-supplied directory.
    with tempfile.TemporaryDirectory(prefix='release-', dir=build_root) as work:
        work = Path(work)
        legacy, containers = work / 'legacy', work / 'containers'
        build_menu(args.source, legacy)
        containers.mkdir()
        run(args.retoc, 'to-zen', '--version', 'UE5_6', legacy, containers / f'{CONTAINER}.utoc')
        # Global script objects are needed to decode the new container, but never shipped.
        for ext in ('utoc', 'ucas'):
            shutil.copy2(args.game_paks / f'global.{ext}', containers / f'global.{ext}')
        roundtrip = work / 'roundtrip'
        run(args.retoc, 'to-legacy', '--version', 'UE5_6', '--no-shaders', '--no-parallel', containers, roundtrip)
        assets = list(legacy.rglob('*.uasset'))
        if len(assets) != 2 or len(list(roundtrip.rglob('*.uasset'))) != 2:
            raise ValueError('Expected exactly two menu assets')
        for asset in assets:
            if decode(asset) != decode(roundtrip / asset.relative_to(legacy)):
                raise ValueError(f'Round-trip mismatch: {asset.name}')
        stage = work / 'stage'
        script_dir = stage / RUNTIME / 'Scripts'
        script_dir.mkdir(parents=True)
        shutil.copyfile(MOD / 'src/main.lua', script_dir / 'main.lua')
        (script_dir.parent / 'enabled.txt').write_bytes(b'')
        (stage / PAKS).mkdir(parents=True)
        for ext in ('pak', 'ucas', 'utoc'):
            shutil.copyfile(containers / f'{CONTAINER}.{ext}', stage / PAKS / f'{CONTAINER}.{ext}')
        readme = (MOD / 'INSTALL.txt').read_text(encoding='utf-8')
        license_text = (REPO / 'LICENSE').read_text(encoding='utf-8')
        (stage / 'README.txt').write_text(readme + '\n' + license_text, encoding='utf-8', newline='\n')
        candidate = work / 'candidate.zip'
        write_zip(stage, candidate)
        verify_zip(candidate)
        shutil.copyfile(candidate, archive_path)
    print(f'Built and verified: {archive_path}')


if __name__ == '__main__':
    main()
