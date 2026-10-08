from pathlib import Path
from xml.etree import ElementTree as ET
import struct
import sys

root = Path(__file__).resolve().parents[2]
checks=[]
def ck(ok, msg): checks.append((msg, bool(ok)))

xui = root/'Config/XUi_InGame/windows.xml'
met = root/'Config/_Metabolism/windows.xml'
tool = root/'Scripts/UI/XUiC_RebirthToolbeltLayout.cs'
comp = root/'Scripts/UI/XUiC_RebirthCompassWindow.cs'
metcs = root/'Scripts/Metabolism/UI/XUiC_RebirthMetabolismUi.cs'
loc = root/'Config/Localization.csv'

for p in (xui, met):
    try:
        ET.parse(p); ck(True, f'XML parses: {p.relative_to(root)}')
    except Exception as e:
        ck(False, f'XML parses: {p.relative_to(root)} ({e})')

xs=xui.read_text(encoding='utf-8')
ms=met.read_text(encoding='utf-8')
ts=tool.read_text(encoding='utf-8')
cs=comp.read_text(encoding='utf-8')
hs=metcs.read_text(encoding='utf-8')
ls=loc.read_text(encoding='utf-8')

ck('rebirthVitalHealth" pos="0,0" width="155"' in xs, 'Authored five-slot Health is half width')
ck('rebirthVitalStamina" pos="155,0" width="155"' in xs, 'Authored five-slot Stamina is half width')
ck('btnRebirthVitalEnergyColor' not in xs, 'Toolbelt Energy color hit surface removed')
ck('Health | Stamina, each exactly one half' in ts, 'Toolbelt controller contract is Health/Stamina halves')
ck('int healthWidth = totalWidth / 2;' in ts and 'int staminaWidth = totalWidth - healthWidth;' in ts, 'Dynamic top row splits toolbelt 50/50')
ck('LayoutEnergy(' not in ts and 'energyHud' not in ts and 'energyColorButtonController' not in ts, 'Toolbelt controller no longer owns Energy')

ck("append xpath=\"/windows/window[@name='windowCompass']\"" in ms, 'Energy HUD is appended to compass window')
ck('name="rebirthMetabolismHud" pos="251,0" width="170" height="38"' in ms, 'Concept E Energy module occupies compass-header slot')
ck('text="{rbmet_hud_energy_current_with_max}"' in ms, 'Energy module shows current/max')
ck('text="{rbmet_hud_energy_net}"' in ms, 'Energy module shows live net rate')
ck('text="{rbmet_hud_energy_state}"' in ms, 'Energy module shows activity/recovery state')
ck('name="btnRebirthMetabolismEnergyInfo"' in ms and 'tooltip="{rbmet_hud_energy_tooltip}"' in ms, 'Energy module retains tooltip/click surface')
ck("append xpath=\"/windows/window[@name='windowToolbelt']\"" not in ms.split('<!-- Character-window Metabolism tab.')[0], 'Metabolism no longer appends Energy to toolbelt')

ck('private const int EnergyModuleWidth = 170;' in cs, 'Compass controller reserves Energy width')
ck('CompassWidth + SeparatorWidth + EnergyModuleWidth + SeparatorWidth' in cs, 'Optional Day/Time/Temperature layout starts after Energy module')
ck('rebirthCompassDay" pos="422,0"' in xs, 'Day module shifted after Energy')
ck('rebirthCompassTime" pos="517,0"' in xs, 'Time module shifted after Energy')
ck('rebirthCompassTemperature" pos="588,0"' in xs, 'Temperature module shifted after Energy')

ck('case "rbmet_hud_energy_net":' in hs and 'EnergyNet(snapshot)' in hs, 'Energy net binding implemented')
ck('case "rbmet_hud_energy_state":' in hs and 'BuildEnergyState(snapshot)' in hs, 'Energy state binding implemented')
ck('XUiC_RebirthVitalColorPicker.Open(xui, RebirthVitalHudKind.Energy);' in hs, 'Clicking compass Energy opens Energy color picker')
ck('RebirthVitalHudColors.Get(RebirthVitalHudKind.Energy)' in hs, 'Compass Energy honors saved Energy HUD color')
ck('xuiRebirthEnergyIdleRecovery' in hs and 'xuiRebirthEnergyCritical' in hs, 'Energy state text is localized')
for key in ('xuiRebirthEnergyIdleRecovery','xuiRebirthEnergyRecovering','xuiRebirthEnergyExertion','xuiRebirthEnergyStable','xuiRebirthEnergyFatigue','xuiRebirthEnergyCritical'):
    ck((key+',') in ls, f'Localization key present: {key}')

expected={
 'rb_hud_shell_full.png':(880,38),
 'rb_hud_shell_day_only.png':(809,38),
 'rb_hud_shell_time_only.png':(785,38),
 'rb_hud_shell_compact.png':(714,38),
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
    ck(ok, f'{name} is pre-rendered at {size[0]}x{size[1]}')

for p in (tool, comp, metcs):
    s=p.read_text(encoding='utf-8')
    ck(s.count('{')==s.count('}'), f'Balanced braces: {p.name}')
    ck(s.count('(')==s.count(')'), f'Balanced parentheses: {p.name}')

failed=[m for m,o in checks if not o]
for m,o in checks:
    print(('PASS' if o else 'FAIL')+' | '+m)
print(f'PC052_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)}')
if failed: sys.exit(1)
