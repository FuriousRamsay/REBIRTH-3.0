#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
win = ROOT / 'Config/XUi_InGame/windows.xml'
met = ROOT / 'Config/_Metabolism/windows.xml'
csf = ROOT / 'Scripts/UI/XUiC_RebirthCompassWindow.cs'
asset = ROOT / 'UIAtlases/RebirthHud/rb_hud_shell_sliced.png'

checks=[]
def ck(name, cond):
    checks.append((name,bool(cond)))
    print(('PASS' if cond else 'FAIL')+' - '+name)

ET.parse(win); ck('windows.xml parses', True)
ET.parse(met); ck('_Metabolism/windows.xml parses', True)
xml=win.read_text(encoding='utf-8')
mx=met.read_text(encoding='utf-8')
cs=csf.read_text(encoding='utf-8')

ck('single sliced compass shell exists', 'name="rebirthCompassShell"' in xml and 'sprite="rb_hud_shell_sliced"' in xml)
seg=xml.split('name="rebirthCompassShell"',1)[1].split('/>',1)[0]
ck('single shell uses sliced rendering', 'type="sliced"' in seg and 'bordersize="8"' in seg)
ck('old three-piece shell no longer rendered', all(x not in xml for x in ['rebirthCompassShellLeft','rebirthCompassShellCenter','rebirthCompassShellRight']))
ck('runtime resolves one shell only', 'shellView = FindView("rebirthCompassShell")' in cs)
ck('runtime lays out one renderable shell', 'SetViewGeometry(shellView, shellLeft, 0, totalWidth, HeaderHeight);' in cs)
ck('old shell runtime fields removed', all(x not in cs for x in ['shellLeftView','shellCenterView','shellRightView','ShellCapWidth']))
ck('energy value font matches 15px compass labels', 'name="rebirthMetabolismEnergyText"' in mx and 'font_size="15"' in mx.split('name="rebirthMetabolismEnergyText"',1)[1].split('/>',1)[0])
ck('day remains 15px', 'name="rebirthCompassDayText"' in xml and 'font_size="15"' in xml.split('name="rebirthCompassDayText"',1)[1].split('/>',1)[0])
ck('time remains 15px', 'name="rebirthCompassTimeText"' in xml and 'font_size="15"' in xml.split('name="rebirthCompassTimeText"',1)[1].split('/>',1)[0])
ck('temperature remains 15px', 'name="rebirthCompassTemperatureText"' in xml and 'font_size="15"' in xml.split('name="rebirthCompassTemperatureText"',1)[1].split('/>',1)[0])
ck('temperature binding retained', 'text="{outsidetemp}"' in xml)
ck('responsive layout retained', all(x in cs for x in ['MeasureLabelWidth(jobLabelView','MeasureLabelWidth(energyLabelView','MeasureLabelWidth(dayLabelView','MeasureTemperatureLabelWidth']))

ck('sliced shell asset exists', asset.exists())
if asset.exists():
    im=Image.open(asset).convert('RGBA')
    ck('sliced shell asset is 38x38', im.size==(38,38))
    a=np.array(im)
    ck('left border to center seam is exact', np.array_equal(a[:,7,:],a[:,8,:]))
    ck('center to right border seam is exact', np.array_equal(a[:,29,:],a[:,30,:]))
    center=a[:,8:30,:]
    ck('shell center has no horizontal gradient', np.array_equal(center, np.repeat(center[:,0:1,:],center.shape[1],axis=1)))

passed=sum(ok for _,ok in checks)
print(f'\nSUMMARY: {passed} / {len(checks)} checks passed.')
raise SystemExit(0 if passed==len(checks) else 1)
