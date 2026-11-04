"""Build a self-contained IronPython installer and a source ZIP.
Build dependency: CairoSVG==2.8.2. End users need no extra packages.
"""
import base64
import json
from pathlib import Path
import uuid
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / 'AutoCrop_SixView'
NS = uuid.UUID('92d7f1d6-06b4-514a-a84d-a03bb68767bc')


def guid(name):
    return str(uuid.uuid5(NS, name))


def text(parent, tag, value):
    node = ET.SubElement(parent, tag)
    ET.SubElement(node, 'locale_1033').text = value
    return node


def build_rui():
    import cairosvg
    root = ET.Element('RhinoUI', major_ver='2', minor_ver='0',
                      guid=guid('file'), localize='False', default_language_id='1033')
    extend = ET.SubElement(root, 'extend_rhino_menus')
    text(ET.SubElement(extend, 'menu', guid=guid('extend')), 'text', 'Extend Rhino Menus')
    ET.SubElement(root, 'menus')
    groups = ET.SubElement(root, 'tool_bar_groups')
    group = ET.SubElement(groups, 'tool_bar_group', guid=guid('group'),
                          dock_bar_guid32='', dock_bar_guid64='', active_tool_bar_group='',
                          single_file='False', hide_single_tab='True', point_floating='100,100')
    text(group, 'text', 'AutoCrop_SixView')
    item = ET.SubElement(group, 'tool_bar_group_item', guid=guid('group-item'),
                         major_version='1', minor_version='1')
    text(item, 'text', 'AutoCrop_SixView')
    ET.SubElement(item, 'tool_bar_id').text = guid('toolbar')
    bars = ET.SubElement(root, 'tool_bars')
    bar = ET.SubElement(bars, 'tool_bar', guid=guid('toolbar'), item_display_style='bitmap_only')
    text(bar, 'text', 'AutoCrop_SixView')
    button = ET.SubElement(bar, 'tool_bar_item', guid=guid('button'),
                           button_display_mode='control_only', button_style='normal')
    ET.SubElement(button, 'left_macro_id').text = guid('macro')
    ET.SubElement(button, 'right_macro_id').text = guid('macro')
    macros = ET.SubElement(root, 'macros')
    macro = ET.SubElement(macros, 'macro_item', guid=guid('macro'))
    for tag in ('text', 'button_text', 'menu_text'):
        text(macro, tag, 'Run SixView')
    text(macro, 'tooltip', 'Capture transparent PNG with 1px margin')
    text(macro, 'help_text', 'AutoCrop_SixView for Windows Rhino 6 / 8')
    ET.SubElement(macro, 'script').text = '! RunAutoCropSixView'
    ET.SubElement(macro, 'bitmap_id').text = guid('icon')
    bitmaps = ET.SubElement(root, 'bitmaps')
    svg = PACKAGE / 'assets' / 'AutoCrop_SixView_Icon.svg'
    for label, size in (('small', 16), ('normal', 24), ('large', 32)):
        png = cairosvg.svg2png(url=str(svg), output_width=size, output_height=size)
        (PACKAGE / 'assets' / ('icon_{0}.png'.format(size))).write_bytes(png)
        bitmap = ET.SubElement(bitmaps, label + '_bitmap', item_width=str(size), item_height=str(size))
        ET.SubElement(bitmap, 'bitmap').text = base64.b64encode(png).decode('ascii')
        ET.SubElement(bitmap, 'bitmap_item', guid=guid('icon'), index='0')
    ET.SubElement(root, 'scripts')
    ET.indent(root)
    (PACKAGE / 'AutoCrop_SixView.rui').write_bytes(ET.tostring(root, encoding='utf-8', xml_declaration=True))


def main():
    build_rui()
    dist = ROOT / 'dist'
    dist.mkdir(exist_ok=True)
    names = ('AutoCrop_SixView.py', 'AutoCrop_SixView.rui', 'assets/AutoCrop_SixView_Icon.svg', 'assets/icon_32.png', 'Show_AutoCropSixView.py')
    payload = {name: base64.b64encode((PACKAGE / name).read_bytes()).decode('ascii') for name in names}
    source = (PACKAGE / 'Install_RunAutoCropSixView.py').read_text(encoding='utf-8')
    assert source.count('EMBEDDED_FILES = None') == 1
    standalone = source.replace('EMBEDDED_FILES = None', 'EMBEDDED_FILES = ' + json.dumps(payload, sort_keys=True))
    (dist / 'Install_AutoCrop_SixView.py').write_text(standalone, encoding='utf-8')
    (ROOT / 'Install_AutoCrop_SixView.py').write_text(standalone, encoding='utf-8')
    with zipfile.ZipFile(dist / 'AutoCrop_SixView_1.2.2.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(PACKAGE.rglob('*')):
            if path.is_file() and '__pycache__' not in path.parts:
                archive.write(path, path.relative_to(ROOT))
    print('Built dist/Install_AutoCrop_SixView.py and dist/AutoCrop_SixView_1.2.2.zip')


if __name__ == '__main__':
    main()
