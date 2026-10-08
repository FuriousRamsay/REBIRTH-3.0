from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
xml_path = root / 'Config' / 'XUi_Menu' / 'windows.xml'
errors = []

try:
    ET.parse(xml_path)
except Exception as exc:
    errors.append(f'XML parse failed: {exc}')

text = xml_path.read_text(encoding='utf-8', errors='ignore') if xml_path.exists() else ''
expected = '<label name="selectedProfileSignatureDescription" depth="4" pos="86,-154" width="484" height="64" font_size="16" pivot="topleft" color="180,180,188,255" overflow="clampcontent"/>'
if expected not in text:
    errors.append('Expected 16px non-shrinking selectedProfileSignatureDescription definition not found.')
if 'name="selectedProfileSignatureDescription"' not in text:
    errors.append('Signature description control is missing.')
if 'name="selectedProfileSignatureIcon"' not in text or 'name="selectedProfileSignatureName"' not in text:
    errors.append('PC070 Signature Bonus icon/name controls are missing.')
if 'name="selectedProfileSignatureDescription" depth="4" pos="86,-154" width="484" height="52" font_size="14"' in text:
    errors.append('Old PC070 14px Signature Bonus description definition is still present.')

if errors:
    print('FAIL')
    for e in errors:
        print('-', e)
    raise SystemExit(1)
print('PASS - PC071 Signature Bonus description is 16px, 64px high, and no longer shrink-to-fit.')
