from pathlib import Path
from xml.etree import ElementTree as ET
from PIL import Image
import sys

root = Path(__file__).resolve().parents[2]
checks = []
def check(name, cond):
    checks.append((name, bool(cond)))

main = root / 'Config/XUi_InGame/windows.xml'
met = root / 'Config/_Metabolism/windows.xml'
icon = root / 'UIAtlases/RebirthHud/rb_hud_job_clipboard.png'

for p in (main, met):
    try:
        ET.parse(p)
        check(f'XML parses: {p.relative_to(root)}', True)
    except Exception:
        check(f'XML parses: {p.relative_to(root)}', False)

ms = main.read_text(encoding='utf-8')
met_s = met.read_text(encoding='utf-8')
check('Job Tier uses clipboard asset', 'sprite="rb_hud_job_clipboard"' in ms)
check('Old satchel no longer referenced by Job Tier', 'name="rebirthJobTierIcon" atlas="RebirthHud" sprite="rb_hud_job_satchel"' not in ms)
check('Job Tier display box remains 18x18', 'name="rebirthJobTierIcon" atlas="RebirthHud" sprite="rb_hud_job_clipboard" pivot="center" pos="53,-19" width="18" height="18"' in ms)
check('Energy font remains 15px', 'name="rebirthMetabolismEnergyText"' in met_s and 'font_size="15"' in met_s)
check('Energy initial text width increased to 64', 'name="rebirthMetabolismEnergyText" depth="7" pivot="center" pos="58,-21" width="64"' in met_s)
check('Energy module initial width increased to 92', 'name="rebirthMetabolismHud" pos="251,0" width="92"' in met_s)
check('Energy text color matches other compass values', 'color="240,240,240,255"' in met_s)
check('Approved shell asset reference remains unchanged', 'sprite="rb_hud_shell_full"' in ms)

try:
    im = Image.open(icon).convert('RGBA')
    check('Clipboard asset is 32x32', im.size == (32, 32))
    alpha = im.getchannel('A')
    bbox = alpha.getbbox()
    check('Clipboard asset has transparent background', alpha.getextrema()[0] == 0)
    check('Clipboard visual occupancy is compact', bbox is not None and bbox[2]-bbox[0] <= 30 and bbox[3]-bbox[1] <= 30)
    # Nontransparent RGB should be effectively white/neutral.
    pixels = [px for px in im.getdata() if px[3] > 100]
    check('Clipboard visible pixels are monochrome white', bool(pixels) and min(min(p[:3]) for p in pixels) >= 235)
except Exception:
    check('Clipboard asset is readable', False)

passed = sum(1 for _, ok in checks if ok)
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + name)
print(f'\nSUMMARY: {passed} / {len(checks)} checks passed.')
sys.exit(0 if passed == len(checks) else 1)
