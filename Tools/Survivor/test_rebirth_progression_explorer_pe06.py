#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
import csv,sys,re
ROOT=Path(__file__).resolve().parents[2]
ERR=[];WARN=[]
def req(c,m):
    if not c: ERR.append(m)

menu=ROOT/'Config/XUi_Menu/windows.xml'
installer=ROOT/'Scripts/Survivor/UI/RebirthSurvivorUiInstaller.cs'
ui=ROOT/'Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs'
loc=ROOT/'Config/Localization.csv'

try: ET.parse(menu)
except Exception as e: ERR.append(f'XML parse failed Config/XUi_Menu/windows.xml: {e}')
mt=menu.read_text(encoding='utf-8')
req('name="btnRebirthProgressionExplorer"' in mt,'PE-06 Main Menu button is missing')
req('caption_key="xuiRebirthProgressionExplorerMenu"' in mt,'PE-06 Main Menu button caption key is missing')
req('name="rows">15</setattribute>' in mt,'PE-06 Main Menu grid row capacity was not increased to 15')
req(mt.find('name="btnRebirthSurvivorProfiles"') < mt.find('name="btnRebirthProgressionExplorer"'),'PE-06 Progression Explorer button is not ordered after REBIRTH Survivor Profiles')

t=installer.read_text(encoding='utf-8')
for token in [
    'RebirthProgressionExplorerMainMenuIntegration.TryWire(__instance)',
    'public static class RebirthProgressionExplorerMainMenuIntegration',
    'btnRebirthProgressionExplorer',
    'RebirthProgressionExplorerMode.Neutral',
    'new RebirthProgressionExplorerReturnContext(',
    '"mainMenu"',
    'RebirthProgressionExplorerUiService.Open(xui, request, out error)',
    'manager.Open((GUIWindow)mainMenu.windowGroup, true)',
]: req(token in t,f'PE-06 Main Menu integration missing {token}')
# New integration must not subscribe to native Play Game.
section=t[t.find('public static class RebirthProgressionExplorerMainMenuIntegration'):]
req('btnPlayGame' not in section,'PE-06 Explorer integration touches vanilla btnPlayGame')
req('AwardSkill' not in section and 'GrantKnowledge(' not in section and 'SaveRecord(' not in section,'PE-06 Main Menu integration mutates progression')

ut=ui.read_text(encoding='utf-8')
for token in ['ReturnOrClose()','returnContext!=null&&returnContext.HasCaller','manager.Close(WindowGroupId)','manager.Open((GUIWindow)caller.windowGroup,true)']:
    req(token in ut,f'PE-06 return path missing existing PE-04 contract token {token}')

with loc.open(encoding='utf-8-sig',newline='') as fh: rows=list(csv.reader(fh))
keys={r[0]:r[1] if len(r)>1 else '' for r in rows if r}
for key in ['xuiRebirthProgressionExplorerMenu','xuiRebirthProgressionExplorerReturnMainMenu','xuiRebirthProgressionExplorerMainMenuLaunchReason']:
    req(key in keys and bool(keys[key].strip()),f'missing PE-06 localization {key}')

for f in [installer,ui]:
    tx=f.read_text(encoding='utf-8'); req(tx.count('{')==tx.count('}'),f'brace imbalance {f.relative_to(ROOT)}')

WARN.append('PE-06 is statically validated only; live Main Menu layout, cursor/focus and return behavior still require in-game acceptance')
WARN.append('Creator and live-character permanent deep-link entry points remain PE-07 and PE-08')
print(f'PE-06 progression explorer Main Menu integration static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
sys.exit(1 if ERR else 0)
