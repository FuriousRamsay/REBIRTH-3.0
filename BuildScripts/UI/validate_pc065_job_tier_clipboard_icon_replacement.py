from pathlib import Path
import re
root = Path(__file__).resolve().parents[2]
xml_path = root / 'Config' / 'XUi_InGame' / 'windows.xml'
icon_path = root / 'UIAtlases' / 'RebirthHud' / 'rb_hud_job_clipboard.png'
errors = []
if not xml_path.exists():
    errors.append(f'Missing XML: {xml_path}')
if not icon_path.exists():
    errors.append(f'Missing icon asset: {icon_path}')
if xml_path.exists():
    text = xml_path.read_text(encoding='utf-8', errors='ignore')
    if 'name="rebirthJobTierIcon"' not in text or 'sprite="rb_hud_job_clipboard"' not in text:
        errors.append('Job tier icon binding to rb_hud_job_clipboard not found in windows.xml')
if errors:
    print('FAIL')
    for e in errors:
        print('-', e)
    raise SystemExit(1)
print('PASS - PC065 job tier clipboard icon replacement validated.')
