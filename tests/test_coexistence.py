from pathlib import Path
import re
import hashlib
import unittest
import xml.etree.ElementTree as E

ROOT = Path(__file__).resolve().parents[1]
ORIGINAL_PLUGIN_ID = '3881c10b-30a8-4fe5-bb48-9e53a6fea1f4'
ORIGINAL_RUI_ID = '9b748537-c94f-50a8-81c7-a3bc2003ad84'


class CoexistenceTests(unittest.TestCase):
    def test_assembly_runtime_and_setup_have_independent_identity(self):
        assembly = (ROOT / 'plugin/BKID.AutoCropSixView/AssemblyInfo.cs').read_text()
        plugin = (ROOT / 'plugin/BKID.AutoCropSixView/SixViewPlugin.cs').read_text()
        setup = (ROOT / 'plugin/BKID.AutoCropSixView.Setup/InstallationIdentity.cs').read_text()
        guid = re.search(r'Guid\("([^"]+)"\)', assembly).group(1)
        self.assertNotEqual(guid.lower(), ORIGINAL_PLUGIN_ID)
        self.assertIn(guid, plugin)
        self.assertIn(guid, setup)
        self.assertNotIn('RemoveLegacyRegistration', plugin)
        self.assertNotIn('RunAutoCrop', plugin)
        self.assertIn('"BKID", "BKID_AutoCrop_SixView", "runtime"', plugin)
        self.assertIn('BKIDAutoCropSixViewCapture', plugin)
        self.assertNotIn('return \"BKIDAutoCrop\";', plugin)

    def test_toolbar_guids_commands_and_group_do_not_claim_autocrop(self):
        root = E.parse(ROOT / 'plugin/BKID.AutoCropSixView/Toolbar/BKID.AutoCropSixView.rui').getroot()
        self.assertNotEqual(root.get('guid'), ORIGINAL_RUI_ID)
        commands = [m.findtext('script') for m in root.findall('macros/macro_item')]
        self.assertEqual(set(commands), {'! _BKIDAutoCropSixViewCapture', '! _BKIDAutoCropSixViewResolution'})
        self.assertEqual(root.findtext('tool_bar_groups/tool_bar_group/text/locale_1033'), 'BKID_AutoCrop_SixView')

    def test_wizard_artwork_matches_original_autocrop(self):
        artwork = ROOT / 'BKID_AutoCrop_SixView/assets/WizardImage.png'
        self.assertEqual(hashlib.sha256(artwork.read_bytes()).hexdigest(),
                         'de7d90e80ff418fc02d8cfec794faadedd992d558fa79c9c2db9a8cd4fc4cfc8')

    def test_frame_uses_original_autocrop_blue_in_vector_and_raster(self):
        from collections import Counter
        from PIL import Image
        source = E.parse(ROOT / 'BKID_AutoCrop_SixView/assets/BKID_AutoCrop_SixView_Icon.svg').getroot()
        ns = '{http://www.w3.org/2000/svg}'
        self.assertEqual(source.find(ns+'g').get('fill'), '#0645F5')
        for name in ('icon_master.png', 'icon_64.png'):
            with Image.open(ROOT / 'BKID_AutoCrop_SixView/assets' / name) as im:
                colors = Counter(im.convert('RGBA').getdata())
                blue = {c: n for c,n in colors.items() if c[2] > 200 and c[0] < 40 and c[1] < 110 and c[3] == 255}
                self.assertTrue(blue, 'Icon must have opaque blue frame pixels')
                self.assertEqual(max(blue, key=blue.get), (6, 69, 245, 255))


if __name__ == '__main__':
    unittest.main()
