#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
xml_path = ROOT / 'Config/XUi_InGame/windows.xml'
cs_path = ROOT / 'Scripts/UI/XUiC_RebirthCompassWindow.cs'
hud = ROOT / 'UIAtlases/RebirthHud'

checks = []
def check(name, cond):
    checks.append((name, bool(cond)))
    print(('PASS' if cond else 'FAIL') + ' - ' + name)

ET.parse(xml_path)
check('windows.xml parses', True)
xml = xml_path.read_text(encoding='utf-8')
cs = cs_path.read_text(encoding='utf-8')

check('shell left cap is 8 px in XML', 'name="rebirthCompassShellLeft"' in xml and 'width="8" height="38"' in xml.split('name="rebirthCompassShellLeft"',1)[1].split('/>',1)[0])
check('shell right cap is 8 px in XML', 'name="rebirthCompassShellRight"' in xml and 'width="8" height="38"' in xml.split('name="rebirthCompassShellRight"',1)[1].split('/>',1)[0])
check('temperature module has 72 px safe minimum geometry', 'name="rebirthCompassTemperature" pos="464,0" width="72" height="38"' in xml)
check('temperature label uses clampcontent', 'name="rebirthCompassTemperatureText"' in xml and 'overflow="clampcontent"' in xml.split('name="rebirthCompassTemperatureText"',1)[1].split('/>',1)[0])
check('runtime shell cap constant is 8', 'private const int ShellCapWidth = 8;' in cs)
check('runtime temperature minimum is 72', 'private const int TemperatureMinWidth = 72;' in cs)
check('temperature uses native-binding width helper', 'MeasureTemperatureLabelWidth(temperatureLabelView, 34)' in cs)
check('native temperature binding is read directly', 'base.GetBindingValueInternal(ref nativeText, "outsidetemp")' in cs)
check('temperature text is explicitly refreshed when binding exists', 'labelView.Text = nativeText;' in cs)
check('player XP remains radial', 'ApplyXpFill(levelRingProgressView, GetXpFill(player), true);' in cs)
check('job tier satchel remains authored', 'sprite="rb_hud_job_satchel"' in xml)
check('level/job divider remains present', 'name="rebirthCompassLevelSeparator"' in xml)
check('responsive job/day/time layout remains present', 'MeasureLabelWidth(jobLabelView' in cs and 'MeasureLabelWidth(dayLabelView' in cs and 'MeasureLabelWidth(timeLabelView' in cs)

for fn, expected in [('rb_hud_shell_cap_left.png',(8,38)),('rb_hud_shell_stretch.png',(16,38)),('rb_hud_shell_cap_right.png',(8,38))]:
    p = hud / fn
    check(fn + ' exists', p.exists())
    if p.exists():
        im = Image.open(p).convert('RGBA')
        check(fn + ' dimensions', im.size == expected)

center = np.array(Image.open(hud/'rb_hud_shell_stretch.png').convert('RGBA'))
# Every column must use exactly the same vertical profile; this prevents horizontal gradient/banding.
check('shell center has no horizontal color/alpha gradient', np.array_equal(center, np.repeat(center[:,0:1,:], center.shape[1], axis=1)))
left = np.array(Image.open(hud/'rb_hud_shell_cap_left.png').convert('RGBA'))
right = np.array(Image.open(hud/'rb_hud_shell_cap_right.png').convert('RGBA'))
# Inner edges of both caps must exactly meet the center strip profile.
check('left cap inner edge matches shell center profile', np.max(np.abs(left[:,-1,:].astype(int)-center[:,0,:].astype(int))) <= 2)
check('right cap inner edge matches shell center profile', np.max(np.abs(right[:,0,:].astype(int)-center[:,0,:].astype(int))) <= 2)

passed = sum(1 for _, ok in checks if ok)
print(f'\nSUMMARY: {passed} / {len(checks)} checks passed.')
raise SystemExit(0 if passed == len(checks) else 1)
