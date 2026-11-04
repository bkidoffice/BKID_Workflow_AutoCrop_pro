import base64
import importlib.util
import io
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch, Mock
import xml.etree.ElementTree as ET
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    spec = importlib.util.spec_from_file_location('bkid_installer', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ToolbarHost:
    def __init__(self):
        self.aliases = {}
        self.open = {}
        self.visible = set()
        self.fail_show = False
        self.close_calls = 0
        self.refuse_close = False
        self.close_result = None

    def FindByPath(self, path):
        if path not in self.open:
            return None
        def close(prompt):
            self.close_calls += 1
            if not self.refuse_close:
                self.CloseToolbarCollection(self.open[path], prompt)
            return self.close_result
        host = self
        class Group:
            Name = 'BKID AutoCrop'
            @property
            def Visible(self): return not host.fail_show and host.open.get(path) in host.visible
            @Visible.setter
            def Visible(self, value):
                if value and not host.fail_show: host.visible.add(host.open[path])
        return SimpleNamespace(Close=close, GetGroup=lambda key: Group(), GroupCount=1)

    def Open(self, path):
        self.OpenToolbarCollection(path)
        return self.FindByPath(path)

    def IsAlias(self, name): return name in self.aliases
    def AliasMacro(self, name, value=None):
        if value is not None: self.aliases[name] = value
        return self.aliases.get(name)
    def AddAlias(self, name, value): self.aliases[name] = value
    def DeleteAlias(self, name): del self.aliases[name]
    def IsToolbarCollection(self, path): return self.open.get(path)
    def CloseToolbarCollection(self, name, prompt):
        self.open = {p: n for p, n in self.open.items() if n != name}
        self.visible.discard(name)
        return True
    def OpenToolbarCollection(self, path):
        ET.parse(path)
        self.open[path] = 'BKID_AutoCrop'
        return 'BKID_AutoCrop'
    def ShowToolbar(self, name, group):
        if self.fail_show: return False
        self.visible.add(name)
        return True
    def IsToolbarVisible(self, name, group): return name in self.visible


class InstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='BKID 한글 ')
        self.addCleanup(self.temp.cleanup)
        self.host = ToolbarHost()
        self.log = []
        system = SimpleNamespace(Environment=SimpleNamespace(
            OSVersion=SimpleNamespace(Platform=1), SpecialFolder=SimpleNamespace(ApplicationData=1),
            GetFolderPath=lambda unused: self.temp.name), PlatformID=SimpleNamespace(Win32NT=1))
        rhino = SimpleNamespace(RhinoApp=SimpleNamespace(Version=SimpleNamespace(Major=8), WriteLine=self.log.append, ToolbarFiles=self.host))
        self.mods = {'System': system, 'Rhino': rhino, 'rhinoscriptsyntax': self.host}
        self.module = load(ROOT / 'dist/Install_BKID_AutoCrop.py')
        self.module.show_logo_window = Mock(side_effect=RuntimeError('UI unavailable'))

    def install(self):
        with patch.dict(sys.modules, self.mods), patch.object(sys, 'platform', 'cli'), patch.object(sys, 'version_info', (2, 7)):
            return Path(self.module.install())

    def test_standalone_repeat_install_and_move(self):
        folder = self.install()
        for name in self.module.MANAGED_FILES:
            self.assertEqual((folder / name).read_bytes(), (ROOT / 'BKID_AutoCrop' / name).read_bytes())
        self.assertIn(str(folder / 'BKID_AutoCrop.py'), self.host.aliases['RunAutoCrop'])
        self.assertEqual(len(self.host.open), 1)
        (folder / 'user_notes.txt').write_text('keep')
        self.assertEqual(self.install(), folder)
        self.assertEqual(len(self.host.open), 1)
        self.assertEqual((folder / 'user_notes.txt').read_text(), 'keep')
        # The standalone installer does not read files beside itself.
        self.module.__file__ = '/nonexistent/moved/Install_BKID_AutoCrop.py'
        self.assertEqual(self.install(), folder)

    def test_failed_update_restores_previous_files_and_alias(self):
        folder = self.install()
        target = folder / 'BKID_AutoCrop.py'
        target.write_bytes(b'previous installation')
        self.host.aliases['RunAutoCrop'] = 'previous macro'
        self.host.fail_show = True
        with self.assertRaises(RuntimeError): self.install()
        self.assertEqual(target.read_bytes(), b'previous installation')
        self.assertEqual(self.host.aliases['RunAutoCrop'], 'previous macro')
        self.assertEqual(len(self.host.open), 1)
        self.assertFalse(any('설치 완료' in line for line in self.log[3:]))

    def test_first_install_failure_removes_new_alias_and_files(self):
        self.host.fail_show = True
        with self.assertRaises(RuntimeError): self.install()
        self.assertFalse(self.host.aliases)
        self.assertFalse(self.host.open)
        self.assertFalse(list(Path(self.temp.name).rglob('*.py')))

    def test_missing_payload_leaves_old_install_untouched(self):
        folder = self.install()
        before = (folder / 'BKID_AutoCrop.py').read_bytes()
        del self.module.EMBEDDED_FILES['BKID_AutoCrop.rui']
        with self.assertRaises(KeyError): self.install()
        self.assertEqual((folder / 'BKID_AutoCrop.py').read_bytes(), before)
        self.assertEqual(len(self.host.open), 1)

    def test_rhino6_uses_separate_folder(self):
        folder8 = self.install()
        self.mods['Rhino'].RhinoApp.Version.Major = 6
        folder6 = self.install()
        self.assertNotEqual(folder8, folder6)
        self.assertEqual(folder6.name, 'Rhino6')

    def test_identical_toolbar_is_reused_even_when_close_is_refused(self):
        folder = self.install()
        self.host.refuse_close = True
        # Reproduce the original name-based API failure.
        self.host.CloseToolbarCollection = lambda *args: False
        self.assertEqual(self.install(), folder)
        self.assertEqual(self.host.close_calls, 0)

    def test_changed_toolbar_closes_by_path_with_void_return(self):
        folder = self.install()
        (folder / 'BKID_AutoCrop.rui').write_bytes(b'<RhinoUI />')
        self.host.close_result = None
        self.install()
        self.assertEqual(self.host.close_calls, 1)
        self.assertEqual((folder / 'BKID_AutoCrop.rui').read_bytes(),
                         (ROOT / 'BKID_AutoCrop/BKID_AutoCrop.rui').read_bytes())

    def test_close_refusal_preserves_previous_install(self):
        folder = self.install()
        old = b'<RhinoUI />'
        (folder / 'BKID_AutoCrop.rui').write_bytes(old)
        self.host.refuse_close = True
        with self.assertRaisesRegex(RuntimeError, 'Rhino'):
            self.install()
        self.assertEqual((folder / 'BKID_AutoCrop.rui').read_bytes(), old)
        self.assertEqual(len(self.host.open), 1)

    def test_native_show_does_not_use_name_based_lookup(self):
        self.host.ShowToolbar = lambda *args: False
        self.install()
        self.module.show_logo_window.assert_not_called()

    def test_native_failure_opens_logo_window_and_keeps_commands(self):
        self.host.fail_show = True
        self.module.show_logo_window.side_effect = None
        folder = self.install()
        self.module.show_logo_window.assert_called_once_with(str(folder))
        self.assertIn('RunAutoCrop', self.host.aliases)
        self.assertIn('ShowAutoCrop', self.host.aliases)
        self.assertTrue((folder / 'assets/icon_32.png').is_file())

    def test_logo_window_lifetime_click_and_reopen(self):
        class Event:
            def __init__(self): self.handlers = []
            def __iadd__(self, handler): self.handlers.append(handler); return self
            def fire(self):
                for handler in self.handlers: handler(None, None)
        class Form:
            def __init__(self):
                self.Closed = Event()
                self.front = 0
            def Show(self): self.shown = True
            def BringToFront(self): self.front += 1
        class Button:
            def __init__(self): self.Click = Event()
        class Bitmap:
            def __init__(self, path): self.path = path; self.disposed = False
            def Dispose(self): self.disposed = True
        forms = SimpleNamespace(Form=Form, Button=Button)
        drawing = SimpleNamespace(Bitmap=Bitmap, Padding=lambda n: n, Size=lambda *n: n)
        rhino = self.mods['Rhino']
        rhino.UI = SimpleNamespace(RhinoEtoApp=SimpleNamespace(MainWindow=object()))
        rhino.RhinoApp.RunScript = Mock(return_value=True)
        sticky = {}
        modules = dict(self.mods, Eto=SimpleNamespace(Forms=forms, Drawing=drawing),
                       scriptcontext=SimpleNamespace(sticky=sticky))
        modules.update({'Eto.Forms': forms, 'Eto.Drawing': drawing})
        with patch.dict(sys.modules, modules):
            launcher = load(ROOT / 'BKID_AutoCrop/Show_AutoCrop.py')
            form = launcher.show()
            self.assertTrue(form.shown)
            self.assertIs(launcher.show(), form)
            self.assertEqual(form.front, 1)
            form.Content.Click.fire()
            rhino.RhinoApp.RunScript.assert_called_once_with('! RunAutoCrop', False)
            form.Closed.fire()
            self.assertFalse(sticky)
            self.assertTrue(form.Content.Image.disposed)
            self.assertIsNot(launcher.show(), form)

    def test_toolbar_icon_and_macro_references(self):
        rui = ET.parse(ROOT / 'BKID_AutoCrop/BKID_AutoCrop.rui').getroot()
        macro = rui.find('macros/macro_item')
        self.assertEqual(macro.findtext('script'), '! RunAutoCrop')
        self.assertEqual(rui.findtext('tool_bars/tool_bar/tool_bar_item/left_macro_id'), macro.get('guid'))
        self.assertEqual(rui.findtext('tool_bar_groups/tool_bar_group/tool_bar_group_item/tool_bar_id'), rui.find('tool_bars/tool_bar').get('guid'))
        for label, size in [('small', 16), ('normal', 24), ('large', 32)]:
            node = rui.find('bitmaps/' + label + '_bitmap')
            self.assertEqual(node.find('bitmap_item').get('guid'), macro.findtext('bitmap_id'))
            png = Image.open(io.BytesIO(base64.b64decode(node.findtext('bitmap'))))
            self.assertEqual(png.size, (size, size))
            self.assertEqual(png.mode, 'RGBA')
            self.assertEqual(png.getpixel((0, 0))[3], 0)
            self.assertGreater(png.getchannel('A').getextrema()[1], 0)


if __name__ == '__main__':
    unittest.main()
