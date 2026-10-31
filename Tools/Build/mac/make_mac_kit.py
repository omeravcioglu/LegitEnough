"""Builds Builds/LegitEnough-MacKit-<version>.zip: everything needed to notarize the Mac demo and upload
Windows + macOS to Steam from a Mac (run-on-mac.sh, notarize-mac.sh, the entitlements and both builds).

The zip entries carry Unix permissions (scripts and Mach-O binaries 755), so they stay executable when the
zip is opened on the Mac, and entry names use forward slashes, which macOS needs.
Usage:  python Tools/Build/mac/make_mac_kit.py
"""
import re
import sys
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
BUILDS = ROOT / "Builds"
APP = BUILDS / "LegitEnoughDemoMac" / "Legit Enough.app"
WIN = BUILDS / "LegitEnoughDemo"
MACHO = {b"\xca\xfe\xba\xbe", b"\xcf\xfa\xed\xfe", b"\xce\xfa\xed\xfe", b"\xfe\xed\xfa\xcf", b"\xfe\xed\xfa\xce"}
SKIP_WIN = re.compile(r"\.pdb$|_BurstDebugInformation_DoNotShip|_BackUpThisFolder_ButDontShipItWithYourGame")


def add(z, src, name, mode):
    info = zipfile.ZipInfo.from_file(src, name)
    info.create_system = 3  # Unix, so external_attr is read as permissions
    info.external_attr = (0o100000 | mode) << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    data = src.read_bytes()
    if name.endswith(".sh"):
        data = data.replace(b"\r\n", b"\n")
    z.writestr(info, data)


def add_tree(z, folder, prefix, skip=None):
    machos = 0
    for f in sorted(folder.rglob("*")):
        rel = f.relative_to(folder).as_posix()
        if not f.is_file() or (skip and skip.search(rel)):
            continue
        with f.open("rb") as h:
            macho = h.read(4) in MACHO
        machos += macho
        add(z, f, f"{prefix}/{rel}", 0o755 if macho else 0o644)
    return machos


def main():
    if not (APP / "Contents" / "MacOS" / "Legit Enough").exists():
        sys.exit(f"No Mac build at {APP}. Run Tools/Build/build-demo-mac.ps1 first.")
    if not (WIN / "Legit Enough.exe").exists():
        sys.exit(f"No Windows build at {WIN}. Run Tools/Build/build-demo.ps1 first.")
    plist = (APP / "Contents" / "Info.plist").read_text(encoding="utf-8")
    version = re.search(r"<key>CFBundleShortVersionString</key>\s*<string>([^<]+)</string>", plist).group(1)
    top = f"LegitEnough-MacKit-{version}"
    out = BUILDS / f"{top}.zip"
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        add(z, HERE / "README-mac-kit.txt", f"{top}/README.txt", 0o644)
        add(z, HERE / "run-on-mac.sh", f"{top}/run-on-mac.sh", 0o755)
        add(z, HERE / "notarize-mac.sh", f"{top}/notarize-mac.sh", 0o755)
        add(z, HERE / "LegitEnough.entitlements", f"{top}/LegitEnough.entitlements", 0o644)
        machos = add_tree(z, APP, f"{top}/LegitEnoughDemoMac/Legit Enough.app")
        add_tree(z, WIN, f"{top}/LegitEnoughDemo", SKIP_WIN)
    print(f"Built {out} ({out.stat().st_size / 1e6:.1f} MB, version {version}, {machos} Mach-O files marked executable)")


if __name__ == "__main__":
    main()
