# -*- coding: utf-8 -*-
"""Installer 1.2.2. Install with Windows Rhino 6/8 _RunPythonScript (IronPython 2.7)."""
import base64
import os
import shutil
import sys
import tempfile
import traceback

# The release builder replaces this marker with the complete embedded payload.
EMBEDDED_FILES = None
MANAGED_FILES = ("AutoCrop_SixView.py", "AutoCrop_SixView.rui",
                 "assets/AutoCrop_SixView_Icon.svg", "assets/icon_32.png", "Show_AutoCropSixView.py")


def payload():
    if EMBEDDED_FILES is not None:
        return dict((name, base64.b64decode(EMBEDDED_FILES[name]))
                    for name in MANAGED_FILES)
    folder = os.path.dirname(os.path.abspath(__file__))
    result = {}
    for name in MANAGED_FILES:
        with open(os.path.join(folder, *name.split('/')), "rb") as stream:
            result[name] = stream.read()
    return result


def same_file(path, content):
    if not os.path.isfile(path):
        return False
    with open(path, "rb") as stream:
        return stream.read() == content


def close_toolbar(path, toolbar_files):
    """Use the exact file handle; verify state instead of trusting Close's return."""
    toolbar = toolbar_files.FindByPath(path)
    if toolbar is None:
        return
    toolbar.Close(False)
    if toolbar_files.FindByPath(path) is not None:
        raise RuntimeError(u"BKID 툴바 파일이 아직 열려 있습니다. Rhino를 다시 시작한 뒤 설치해 주세요.")


def show_native_toolbar(path, toolbar_files):
    toolbar = toolbar_files.FindByPath(path)
    if toolbar is None:
        toolbar = toolbar_files.Open(path)
    if toolbar is None:
        raise RuntimeError("Toolbar file could not be opened: " + path)
    group = toolbar.GetGroup("AutoCrop_SixView")
    if group is None:
        names = [toolbar.GetGroup(i).Name for i in range(toolbar.GroupCount)]
        raise RuntimeError("AutoCrop_SixView group missing; loaded groups: " + repr(names))
    group.Visible = True
    if not group.Visible:
        raise RuntimeError("Rhino did not make the toolbar group visible")


def show_logo_window(folder):
    path = os.path.join(folder, "Show_AutoCropSixView.py")
    scope = {"__file__": path, "__name__": "__main__"}
    with open(path, "rb") as stream:
        code = compile(stream.read(), path, "exec")
    eval(code, scope)


def install():
    if sys.platform != "cli" or sys.version_info[0] != 2:
        raise RuntimeError(u"Windows Rhino의 _RunPythonScript로 실행해 주세요 (IronPython 2.7).")
    import Rhino
    import System
    import rhinoscriptsyntax as rs
    if System.Environment.OSVersion.Platform != System.PlatformID.Win32NT:
        raise RuntimeError(u"Windows Rhino 전용 설치 파일입니다.")
    version = Rhino.RhinoApp.Version.Major
    if version not in (6, 8):
        raise RuntimeError(u"지원 대상은 Rhino 6 및 8입니다.")
    files = payload()  # Check the entire package before changing an existing install.
    folder = os.path.join(System.Environment.GetFolderPath(
        System.Environment.SpecialFolder.ApplicationData),
        "BKID", "AutoCrop_SixView", "Rhino{0}".format(version))
    if not os.path.isdir(folder):
        os.makedirs(folder)
    rui = os.path.join(folder, "AutoCrop_SixView.rui")
    backup = tempfile.mkdtemp(prefix="AutoCrop_SixView_backup_")
    old_alias = rs.AliasMacro("RunAutoCropSixView") if rs.IsAlias("RunAutoCropSixView") else None
    old_launcher_alias = rs.AliasMacro("ShowAutoCropSixView") if rs.IsAlias("ShowAutoCropSixView") else None
    old_collection = rs.IsToolbarCollection(rui)
    toolbar_changed = not same_file(rui, files["AutoCrop_SixView.rui"])
    closed = False
    written = []
    try:
        for name in MANAGED_FILES:
            target = os.path.join(folder, *name.split('/'))
            if os.path.isfile(target):
                saved = os.path.join(backup, *name.split('/'))
                parent = os.path.dirname(saved)
                if not os.path.isdir(parent):
                    os.makedirs(parent)
                shutil.copy2(target, saved)
        if old_collection and toolbar_changed:
            close_toolbar(rui, Rhino.RhinoApp.ToolbarFiles)
            closed = True
        for name in MANAGED_FILES:
            target = os.path.join(folder, *name.split('/'))
            if same_file(target, files[name]):
                continue
            parent = os.path.dirname(target)
            if not os.path.isdir(parent):
                os.makedirs(parent)
            written.append(name)
            with open(target, "wb") as stream:
                stream.write(files[name])
        macro = u'! _-RunPythonScript "{0}"'.format(os.path.join(folder, "AutoCrop_SixView.py"))
        if rs.IsAlias("RunAutoCropSixView"):
            rs.AliasMacro("RunAutoCropSixView", macro)
        else:
            rs.AddAlias("RunAutoCropSixView", macro)
        if rs.AliasMacro("RunAutoCropSixView") != macro:
            raise RuntimeError(u"RunAutoCropSixView 명령 등록에 실패했습니다.")
        launcher_macro = u'! _-RunPythonScript "{0}"'.format(os.path.join(folder, "Show_AutoCropSixView.py"))
        if rs.IsAlias("ShowAutoCropSixView"):
            rs.AliasMacro("ShowAutoCropSixView", launcher_macro)
        else:
            rs.AddAlias("ShowAutoCropSixView", launcher_macro)
        if rs.AliasMacro("ShowAutoCropSixView") != launcher_macro:
            raise RuntimeError("ShowAutoCropSixView alias registration failed")
        try:
            show_native_toolbar(rui, Rhino.RhinoApp.ToolbarFiles)
        except Exception:
            Rhino.RhinoApp.WriteLine("Native toolbar unavailable; trying logo window. Details:")
            Rhino.RhinoApp.WriteLine(traceback.format_exc())
            show_logo_window(folder)
            Rhino.RhinoApp.WriteLine(u"기본 툴바 대신 별도 로고 창을 표시했습니다. 다시 열기: ShowAutoCropSixView")

    except Exception:
        # Restore only files and the alias this installer owns.
        try:
            current = rs.IsToolbarCollection(rui)
            if current and ("AutoCrop_SixView.rui" in written or closed):
                close_toolbar(rui, Rhino.RhinoApp.ToolbarFiles)
            for name in written:
                target = os.path.join(folder, *name.split('/'))
                saved = os.path.join(backup, *name.split('/'))
                if os.path.isfile(saved):
                    shutil.copy2(saved, target)
                elif os.path.isfile(target):
                    os.remove(target)
            if old_alias is None:
                if rs.IsAlias("RunAutoCropSixView"):
                    rs.DeleteAlias("RunAutoCropSixView")
            else:
                rs.AliasMacro("RunAutoCropSixView", old_alias)
            if old_launcher_alias is None:
                if rs.IsAlias("ShowAutoCropSixView"):
                    rs.DeleteAlias("ShowAutoCropSixView")
            else:
                rs.AliasMacro("ShowAutoCropSixView", old_launcher_alias)
            if old_collection and closed:
                restored = rs.OpenToolbarCollection(rui)
                if not restored:
                    raise RuntimeError("Cannot reopen original toolbar")
        except Exception:
            Rhino.RhinoApp.WriteLine(u"복구 오류. 이전 파일 백업: {0}".format(backup))
            Rhino.RhinoApp.WriteLine(traceback.format_exc())
            backup = None  # Retain backup for manual recovery.
        raise
    finally:
        if backup is not None:
            shutil.rmtree(backup)
    Rhino.RhinoApp.WriteLine(u"설치 완료 (1.2.2): 로고 버튼을 클릭하거나 RunAutoCropSixView을 입력하세요.")
    Rhino.RhinoApp.WriteLine(u"설치 위치: {0}".format(folder))
    Rhino.RhinoApp.WriteLine(u"다운로드한 설치 파일은 이제 이동하거나 삭제해도 됩니다.")
    return folder


if __name__ == "__main__":
    try:
        install()
    except Exception:
        import Rhino
        Rhino.RhinoApp.WriteLine(u"AutoCrop_SixView 설치 실패:\n" + traceback.format_exc())
