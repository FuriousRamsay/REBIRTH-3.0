from pathlib import Path
import re
root = Path(__file__).resolve().parents[2]
p = root / 'Config' / '_Metabolism' / 'windows.xml'
text = p.read_text(encoding='utf-8')
checks = {
    '70px Energy module': 'name="rebirthMetabolismHud" pos="251,0" width="70" height="38"' in text,
    '18px Energy bolt': 'name="rebirthMetabolismEnergyLightningIcon"' in text and 'pos="9,-19" width="18" height="18"' in text,
    '15px Energy font': 'name="rebirthMetabolismEnergyText"' in text and 'font_size="15"' in text,
    '50px Energy text field': 'pos="45,-21" width="50" height="18"' in text,
    'non-shrinking overflow': 'overflow="clampcontent"' in text,
    'single line': 'max_line_count="1"' in text,
    'separator preserved': 'name="rebirthCompassEnergySeparator" pos="321,-8"' in text,
}
failed=[k for k,v in checks.items() if not v]
if failed:
    print('FAIL')
    for k in failed: print('-', k)
    raise SystemExit(1)
print('PASS - PC069 Energy font is locked to authored 15px without shrinkcontent.')
