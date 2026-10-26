"""Bundle the tested UE4SS loader, preserving its binaries, helper and license."""

import configparser
import hashlib
import re
import zipfile

BINARIES = "FFRS/Binaries/Win64"
UE4SS_VERSION = "v3.0.1-1181-g83330635"
# Accept only the original loader archive used in our game tests.
UE4SS_SHA256 = "b9a166a756605b21058a64cde93bcaab1a855dba3553e5c7e6ce0fa8fdfad7b9"
LOADER_FILES = (
    "dwmapi.dll",
    "ue4ss/UE4SS.dll",
    "ue4ss/Mods/shared/UEHelpers/UEHelpers.lua",
    "ue4ss/LICENSE",
)


def loader_files(path, mod_folder):
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
    files[f"{BINARIES}/ue4ss/Mods/mods.txt"] = f"{mod_folder} : 1\n".encode("utf-8")
    return files
