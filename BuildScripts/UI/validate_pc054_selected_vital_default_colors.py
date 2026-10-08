from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'Scripts/UI/XUiC_RebirthVitalColorPicker.cs'
text = SRC.read_text(encoding='utf-8')

expected = [
    ('Health',  'new Color32(186, 115, 115, 255)', '#BA7373'),
    ('Stamina', 'new Color32(211, 185, 113, 255)', '#D3B971'),
    ('Water',   'new Color32(115, 162, 194, 255)', '#73A2C2'),
    ('Food',    'new Color32(143, 186, 128, 255)', '#8FBA80'),
]

checks = []
for name, literal, hexc in expected:
    checks.append((name + ' default literal', literal in text))

checks += [
    ('Energy default unchanged', 'new Color32(174, 88, 238, 255)' in text),
    ('Color picker Set remains', 'RebirthVitalHudColors.Set(kind, selectedColor);' in text),
    ('Exact five default entries', len(re.findall(r'new Color32\([^\n]+\)', text.split('private static readonly Color32[] Colors =',1)[1].split('};',1)[0])) == 5),
]

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ' | ' + name)
print(f'RESULT {len(checks)-len(failed)}/{len(checks)}')
if failed:
    raise SystemExit(1)
