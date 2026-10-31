"""Rebuild the parked prototype or its font-only rollback. Never install implicitly."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile

MOD = Path(__file__).resolve().parents[1]
NAME = 'zz_CinzelSemiboldLato_P'


def run(*arguments):
    subprocess.run([str(value) for value in arguments], check=True)


def verify_package(args, tool, legacy, containers, work, mode):
    game = args.game_paks.resolve().parents[2]
    stage = Path(tempfile.mkdtemp(prefix='.battle-dock-check-', dir=game))
    created = []
    try:
        for folder in (args.game_paks, containers):
            for path in folder.iterdir():
                if path.suffix not in ('.utoc', '.ucas'):
                    continue
                destination = stage / path.name
                if path.drive.lower() == destination.drive.lower():
                    os.link(path, destination)
                else:
                    shutil.copy2(path, destination)
                created.append(destination)
        roundtrip = work / 'roundtrip'
        command = [args.retoc, 'to-legacy', stage, roundtrip, '--version', 'UE5_6', '--no-shaders', '--no-parallel']
        for path in sorted(legacy.rglob('*.uasset')):
            command.extend(['--filter', path.stem])
        run(*command)
        if mode == 'dock':
            run(*tool, 'verify', args.mappings, args.source, work / 'dock-legacy', roundtrip)
            # The dock intentionally replaces the font baseline's party widget.
            fonts = work / 'separate-fonts'
            dock_assets = {p.stem for p in (work / 'dock-legacy').rglob('*.uasset')}
            for path in (work / 'default-size-fonts').rglob('*'):
                if path.is_file() and path.stem not in dock_assets:
                    target = fonts / path.relative_to(work / 'default-size-fonts')
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(path, target)
        else:
            fonts = legacy
        run(*tool, 'verify-fonts', args.mappings, args.source, fonts, roundtrip)
    finally:
        assert stage.resolve().parent == game
        for path in created:
            path.unlink()
        stage.rmdir()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'mappings', 'font-baseline', 'font-pak', 'game-paks', 'dotnet', 'retoc', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--target', choices=('dock', 'font-only'), default='dock')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='build-', dir=args.output))
    run(args.dotnet, 'build', MOD / 'tools/BattleDock.csproj', '--nologo', '-p:RestoreLockedMode=true', '-o', work / 'tool')
    tool = (args.dotnet, work / 'tool/BattleDock.dll')
    fonts = work / 'default-size-fonts'
    run(*tool, 'font-baseline', args.mappings, args.source, args.font_baseline, fonts)
    run(sys.executable, '-X', 'utf8', MOD / 'tools/check_font_defaults.py', work)
    legacy = work / 'legacy'
    shutil.copytree(fonts, legacy)
    if args.target == 'dock':
        dock = work / 'dock-legacy'
        run(*tool, 'build', args.mappings, args.source, fonts, dock)
        run(sys.executable, '-X', 'utf8', MOD / 'tools/check_bindings.py', work)
        run(sys.executable, '-X', 'utf8', MOD / 'tools/check_layout.py', work)
        run(sys.executable, '-X', 'utf8', MOD / 'tools/check_help_strip.py')
        shutil.copytree(dock, legacy, dirs_exist_ok=True)
    assert len(list(legacy.rglob('*.uasset'))) == (32 if args.target == 'dock' else 11)
    containers = work / 'containers'
    containers.mkdir()
    run(args.retoc, 'to-zen', '--version', 'UE5_6', legacy, containers / (NAME + '.utoc'))
    shutil.copy2(args.font_pak, containers / (NAME + '.pak'))
    verify_package(args, tool, legacy, containers, work, args.target)

    files = {'FFRS/Content/Paks/~mods/' + p.name: p.read_bytes() for p in containers.iterdir()}
    files['README.txt'] = (MOD / 'INSTALL.txt').read_bytes()
    for path in (MOD.parent / 'cinzel-lato/fonts').glob('*OFL.txt'):
        files['Font Licenses/' + path.name] = path.read_bytes()
    if args.target == 'dock':
        prefix = 'FFRS/Binaries/Win64/ue4ss/Mods/BattleDockHelp/'
        files[prefix + 'Scripts/main.lua'] = (MOD / 'runtime/main.lua').read_bytes()
        files[prefix + 'enabled.txt'] = b''
    archive = args.output / ('Battle-Dock-WIP.zip' if args.target == 'dock' else 'Cinzel-Lato-Default-Sizes.zip')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
        for relative, data in sorted(files.items()):
            package.writestr(relative, data)
    with zipfile.ZipFile(archive) as package:
        assert set(package.namelist()) == set(files)
        for relative, data in files.items():
            assert package.read(relative) == data
    print(f'Verified {args.target} package: {archive}\nBuild artifacts: {work}')


if __name__ == '__main__':
    main()
