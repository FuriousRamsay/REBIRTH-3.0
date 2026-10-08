"""Wrap Explorer prose in native scroll views without reserializing huge XML files."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
NAMES = ('progressionExplorerFocusDescription', 'progressionExplorerSummary', 'progressionExplorerRequirements')

def search_row(match):
    raw = match.group(0)
    node = ET.fromstring(raw)
    name = node.attrib['name']
    if name.startswith('progressionSearchName'):
        attributes = {'pos': '12,-5', 'width': '460'}
    elif name.startswith('progressionSearchType'):
        attributes = {'pos': '12,-34', 'width': '150',
                      'font_size': str(max(16, int(node.attrib['font_size'])))}
    else:
        attributes = {'pos': '170,-34', 'width': '302'}
    for key, value in attributes.items():
        raw = re.sub(r'\b' + key + r'="[^"]+"', f'{key}="{value}"', raw)
    return raw

def wrap(match):
    raw = match.group(0)
    node = ET.fromstring(raw)
    name, pos = node.attrib['name'], node.attrib['pos']
    width, height = int(node.attrib['width']), node.attrib['height']
    if name == 'progressionExplorerRequirements':
        height = '116'
    body = re.sub(r'pos="[^"]+"', 'pos="0,0"', raw)
    body = re.sub(r'width="[^"]+"', f'width="{width - 28}"', body)
    body = re.sub(r'height="[^"]+"', f'height="{height}"', body)
    body = re.sub(r'font_size="[^"]+"', 'font_size="20"', body)
    body = re.sub(r'overflow="[^"]+"', 'overflow="resizeheight"', body)
    return (f'<rect name="{name}Reader" controller="RebirthReadableText, RebirthUtils" '
            f'pos="{pos}" width="{width}" height="{height}" on_scroll="true" '
            'gamepad_selectable="true" snap="false" use_selection_box="true">'
            '<defaultscrollbar/>'
            f'<scrollview name="readableTextViewport" depth="10" width="{width - 22}" '
            f'height="{height}" clippingsoftness="0,4">{body}</scrollview></rect>')

if __name__ == '__main__':
    for folder in ('XUi_InGame', 'XUi_Menu'):
        path = ROOT / 'Config' / folder / 'windows.xml'
        content = path.read_text(encoding='utf-8-sig')
        original = content
        content = content.replace('controller="RebirthReadableText"', 'controller="RebirthReadableText, RebirthUtils"')
        content = content.replace('<scrollview name="readableTextViewport" width=', '<scrollview name="readableTextViewport" depth="10" width=')
        # Idempotent: wrapped bodies have position 0,0.
        pattern = r'<label\b(?=[^>]*name="(?:' + '|'.join(NAMES) + r')")(?=[^>]*pos="20,-)[^>]*/>'
        content, count = re.subn(pattern, wrap, content)
        # Preserve readable secondary captions when regenerating either layout.
        # Breadcrumbs retain their separate layout policy.
        secondary = r'(<label\b[^>]*name="progression(?:SearchMeta|NodeType)\d+"[^>]*font_size=")([0-9]+)(")'
        content = re.sub(secondary, lambda m: m[1] + str(max(16, int(m[2]))) + m[3], content)
        content = re.sub(r'<label\b[^>]*name="progressionSearch(?:Name|Type|Meta)\d+"[^>]*/>', search_row, content)
        ET.fromstring(content)
        if content != original:
            path.write_text(content, encoding='utf-8')
        print(folder, count, 'readable panels updated')
