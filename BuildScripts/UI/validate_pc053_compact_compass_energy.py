from pathlib import Path
from xml.etree import ElementTree as ET
import struct, sys

root=Path(__file__).resolve().parents[2]
checks=[]
def ck(ok,msg): checks.append((msg,bool(ok)))

xui=root/'Config/XUi_InGame/windows.xml'
met=root/'Config/_Metabolism/windows.xml'
comp=root/'Scripts/UI/XUiC_RebirthCompassWindow.cs'

for p in (xui,met):
    try:
        ET.parse(p); ck(True,f'XML parses: {p.relative_to(root)}')
    except Exception as e:
        ck(False,f'XML parses: {p.relative_to(root)} ({e})')

xs=xui.read_text(encoding='utf-8')
ms=met.read_text(encoding='utf-8')
cs=comp.read_text(encoding='utf-8')

ck('name="rebirthMetabolismHud" pos="251,0" width="70" height="38"' in ms,'Energy module reduced to 70px')
ck('name="rebirthMetabolismEnergyLightningIcon"' in ms,'Dedicated fixed-color lightning sprite present')
ck('width="18" height="18" color="255,255,255,255"' in ms,'Lightning displays at 18x18 without tinting authored colors')
ck('text="{rbmet_hud_energy_current_with_max}"' in ms,'Energy current/max value remains')
ck('rbmet_hud_energy_net' not in ms.split('<!-- Character-window Metabolism tab.')[0],'Net-rate text removed from compass Energy module')
ck('rbmet_hud_energy_state' not in ms.split('<!-- Character-window Metabolism tab.')[0],'State/fatigue text removed from compass Energy module')
ck('rebirthMetabolismEnergyModuleBackground' not in ms,'Energy module box background removed')
ck('rebirthMetabolismEnergyAccent' not in ms,'Purple Energy outline/accent removed')
ck('btnRebirthMetabolismEnergyInfo' not in ms,'Energy HUD color-picker/click overlay removed')
ck('rebirthCompassEnergySeparator" pos="321,-8"' in ms,'Energy-to-Day separator compacted')

ck('private const int EnergyModuleWidth = 70;' in cs,'Compass controller reserves 70px Energy width')
ck('rebirthCompassDay" pos="322,0"' in xs,'Day shifted left after compact Energy')
ck('rebirthCompassTime" pos="417,0"' in xs,'Time shifted left after compact Energy')
ck('rebirthCompassTemperature" pos="488,0"' in xs,'Temperature shifted left after compact Energy')
ck('width="250" height="38" controller="RebirthCompassWindow, RebirthUtils"' in xs,'Native compass root stays 250px')

expected={
 'rb_hud_shell_full.png':(780,38),
 'rb_hud_shell_day_only.png':(709,38),
 'rb_hud_shell_time_only.png':(685,38),
 'rb_hud_shell_compact.png':(614,38),
 'rb_hud_energy_bolt.png':(32,32),
}
for name,size in expected.items():
    p=root/'UIAtlases/RebirthHud'/name
    ok=False
    try:
        b=p.read_bytes()
        if b[:8]==b'\x89PNG\r\n\x1a\n' and b[12:16]==b'IHDR':
            w,h=struct.unpack('>II',b[16:24]); ok=(w,h)==size
    except Exception:
        pass
    ck(ok,f'{name} is {size[0]}x{size[1]}')

# Catch accidental reintroduction of the PC052 oversized purple module.
ck('width="170" height="38" controller="RebirthMetabolismHud' not in ms,'Old 170px Energy module absent')
ck('174,88,238,255' not in ms.split('<!-- Character-window Metabolism tab.')[0],'No purple Energy tint remains in compass module')

ck(cs.count('{')==cs.count('}'),'Compass controller braces balanced')
ck(cs.count('(')==cs.count(')'),'Compass controller parentheses balanced')

failed=[m for m,o in checks if not o]
for m,o in checks:
    print(('PASS' if o else 'FAIL')+' | '+m)
print(f'PC053_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)}')
if failed: sys.exit(1)
