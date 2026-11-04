import base64
import io
from pathlib import Path
import unittest
import xml.etree.ElementTree as E
import zipfile
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
RUI = ROOT / 'plugin/BKID.AutoCropSixView/Toolbar/BKID.AutoCropSixView.rui'


class NativeToolbarTests(unittest.TestCase):
    def test_native_group_and_logo_references(self):
        root = E.parse(RUI).getroot()
        self.assertEqual(root.get('major_ver'), '2')
        group = root.find('tool_bar_groups/tool_bar_group')
        bar = root.find('tool_bars/tool_bar')
        macros = {item.get('guid'): item for item in root.findall('macros/macro_item')}
        button = bar.find('tool_bar_item')
        macro = macros[button.findtext('left_macro_id')]
        right = macros[button.findtext('right_macro_id')]
        self.assertEqual(group.findtext('text/locale_1033'), 'BKID_AutoCrop_SixView')
        self.assertEqual(group.get('active_tool_bar_group'), bar.get('guid'))
        self.assertEqual(group.find('tool_bar_group_item').get('guid'), bar.get('guid'))
        self.assertEqual(group.findtext('tool_bar_group_item/tool_bar_id'), bar.get('guid'))
        self.assertEqual(group.find('dock_bar_info').get('floating'), 'True')
        self.assertEqual(bar.findtext('tool_bar_item/left_macro_id'), macro.get('guid'))
        self.assertEqual(macro.findtext('script'), '! _BKIDAutoCropSixViewCapture')
        self.assertEqual(right.findtext('script'), '! _BKIDAutoCropSixViewResolution')
        self.assertNotEqual(macro.get('guid'), right.get('guid'))
        self.assertIn('1배', macro.findtext('tooltip/locale_1033'))
        self.assertIn('×2', right.findtext('tooltip/locale_1033'))
        self.assertNotIn('×2', macro.findtext('tooltip/locale_1033'))
        self.assertNotIn('1배', right.findtext('tooltip/locale_1033'))
        self.assertNotEqual(macro.findtext('tooltip/locale_1033'), right.findtext('tooltip/locale_1033'))
        self.assertEqual(right.get('bitmap_id'), macro.get('bitmap_id'))
        # Native RUI stores bitmap_id as an attribute, not a child element.
        self.assertIsNone(macro.find('bitmap_id'))
        for size, name in [(16, 'small'), (24, 'normal'), (32, 'large')]:
            bitmap = root.find('bitmaps/' + name + '_bitmap')
            self.assertEqual(bitmap.find('bitmap_item').get('guid'), macro.get('bitmap_id'))
            with Image.open(io.BytesIO(base64.b64decode(bitmap.findtext('bitmap')))) as png:
                self.assertEqual(png.size, (size, size))
                self.assertEqual(png.getpixel((0, 0))[3], 0)
                # Validate the image actually embedded in the button's RUI.
                pixels = list(png.convert('RGBA').getdata())
                self.assertTrue(any(r > 240 and g > 240 and b > 240 and a == 255
                                    for r,g,b,a in pixels), 'Button must have opaque white fill')
                self.assertTrue(any(r < 220 and abs(r-g) < 5 and abs(g-b) < 5 and a > 40
                                    for r,g,b,a in pixels), 'Button must retain antialiased dark outline pixels')

    def test_rhi_contains_matching_named_plugin_and_toolbar(self):
        with zipfile.ZipFile(ROOT / 'installers/BKID_AutoCrop_SixView_3.1.5.rhi') as archive:
            self.assertIsNone(archive.testzip())
            self.assertEqual(set(archive.namelist()), {'BKID.AutoCropSixView.rhp', 'BKID.AutoCropSixView.rui'})
            self.assertTrue(archive.read('BKID.AutoCropSixView.rhp').startswith(b'MZ'))
            self.assertEqual(archive.read('BKID.AutoCropSixView.rui'), RUI.read_bytes())
