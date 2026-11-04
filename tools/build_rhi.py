"""Build the real .NET plug-in and Windows Rhino Installer package.
Requires .NET SDK 8; Windows Rhino is needed for host execution validation.
"""
import argparse
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = '3.0.0'
PROJECT = ROOT / 'plugin/BKID.AutoCrop/BKID.AutoCrop.csproj'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', default=shutil.which('dotnet'))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error('Install .NET SDK 8 or pass --dotnet /path/to/dotnet')
    from build_native_toolbar import build as build_toolbar
    toolbar = build_toolbar()
    subprocess.run([args.dotnet, 'restore', str(PROJECT), '--locked-mode'], check=True)
    subprocess.run([args.dotnet, 'build', str(PROJECT), '-c', 'Release', '--no-restore'], check=True)
    probe = ROOT / 'tools/CheckRhino6/CheckRhino6.csproj'
    subprocess.run([args.dotnet, 'restore', str(probe), '--locked-mode'], check=True)
    subprocess.run([args.dotnet, 'build', str(probe), '-c', 'Release', '--no-restore'], check=True)
    binary = PROJECT.parent / 'bin/Release/net45/BKID.AutoCrop.rhp'
    subprocess.run([args.dotnet, 'run', '--project', str(ROOT / 'tools/VerifyPlugin'),
                    '-c', 'Release', '--', str(binary),
                    str(ROOT / 'BKID_AutoCrop/BKID_AutoCrop.py'),
                    str(ROOT / 'BKID_AutoCrop/assets/icon_64.png')], check=True)
    destination = ROOT / 'installers'
    destination.mkdir(exist_ok=True)
    installer = destination / ('BKID_AutoCrop_' + VERSION + '.rhi')
    with zipfile.ZipFile(installer, 'w', zipfile.ZIP_DEFLATED) as archive:
        info = zipfile.ZipInfo('BKID.AutoCrop.rhp', date_time=(2026, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        archive.writestr(info, binary.read_bytes())
        info = zipfile.ZipInfo('BKID.AutoCrop.rui', date_time=(2026, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        archive.writestr(info, toolbar.read_bytes())
    with zipfile.ZipFile(installer) as archive:
        if archive.testzip() is not None:
            raise RuntimeError('RHI integrity check failed')
        assert archive.namelist() == ['BKID.AutoCrop.rhp', 'BKID.AutoCrop.rui']
        assert archive.read('BKID.AutoCrop.rui') == toolbar.read_bytes()
        assert archive.read('BKID.AutoCrop.rhp') == binary.read_bytes()
    setup_project = ROOT / 'plugin/BKID.AutoCrop.Setup/BKID.AutoCrop.Setup.csproj'
    subprocess.run([args.dotnet, 'restore', str(setup_project), '--locked-mode'], check=True)
    subprocess.run([args.dotnet, 'build', str(setup_project), '-c', 'Release', '--no-restore'], check=True)
    setup_binary = setup_project.parent / 'bin/Release/net45' / ('BKID_AutoCrop_Setup_' + VERSION + '.exe')
    subprocess.run([args.dotnet, 'run', '--project', str(ROOT / 'tools/VerifyPlugin'), '-c', 'Release', '--', '--setup', str(setup_binary), str(installer), str(ROOT / 'BKID_AutoCrop/assets/WizardImage.png')], check=True)
    subprocess.run([args.dotnet, 'run', '--project', str(ROOT / 'tools/TestSetup'), '-c', 'Release'], check=True)
    subprocess.run([args.dotnet, 'run', '--project', str(ROOT / 'tools/TestCapture'), '-c', 'Release', '--', sys.executable], check=True)
    subprocess.run([args.dotnet, 'run', '--project', str(ROOT / 'tools/TestToolbar'), '-c', 'Release', '--', str(toolbar)], check=True)
    setup = destination / setup_binary.name
    shutil.copyfile(setup_binary, setup)
    print('Built and verified', installer.relative_to(ROOT), 'and', setup.relative_to(ROOT))


if __name__ == '__main__':
    main()
