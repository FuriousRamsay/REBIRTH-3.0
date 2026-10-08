#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
import sys,re

ROOT=Path(__file__).resolve().parents[2]
ERR=[];WARN=[]
def req(c,m):
    if not c: ERR.append(m)

base=ROOT/'Scripts/Survivor/Progression/Explorer'
for name in ['RebirthProgressionExplorerUiProjection.cs']:
    req((base/name).exists(),f'missing PE-03 source {name}')
ui=ROOT/'Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs'
req(ui.exists(),'missing PE-03 XUi controller')
if ui.exists():
    t=ui.read_text(encoding='utf-8')
    for token in ['XUiC_RebirthProgressionExplorer','RebirthProgressionExplorerUiService','Node_OnPressed','progressionNodeCard','progressionExplorerFocusDescription','progressionExplorerRequirements']:
        req(token in t,f'PE-03 controller missing {token}')
    req('AwardSkill' not in t and 'GrantKnowledge(' not in t and 'SaveRecord(' not in t,'PE-03 XUi contains prohibited progression mutation')

proj=(base/'RebirthProgressionExplorerUiProjection.cs').read_text(encoding='utf-8') if (base/'RebirthProgressionExplorerUiProjection.cs').exists() else ''
for token in ['SideSlotCount=24','BuildCandidates','RebirthProgressionExplorerSlotRole.Focus','BuildRequirements','CreateLiveCharacter','CreateCreatorPreview','CreateNeutral']:
    req(token in proj,f'PE-03 projection missing {token}')
req('RebirthProgressionGraphQueryService.GetFocusedNeighborhood' in proj,'PE-03 projection does not consume bounded PE-02 query layer')

for f in [ROOT/'Config/XUi_Menu/windows.xml',ROOT/'Config/XUi_InGame/windows.xml',ROOT/'Config/XUi_Menu/xui.xml',ROOT/'Config/XUi_InGame/xui.xml']:
    try: ET.parse(f)
    except Exception as e: ERR.append(f'XML parse failed {f.relative_to(ROOT)}: {e}')

for f in [ROOT/'Config/XUi_Menu/windows.xml',ROOT/'Config/XUi_InGame/windows.xml']:
    text=f.read_text(encoding='utf-8')
    req('name="rebirthProgressionExplorerWindow"' in text,f'{f.name} missing explorer window')
    req('controller="RebirthProgressionExplorer, RebirthUtils"' in text,f'{f.name} missing explorer controller registration')
    for i in range(49): req(f'name="progressionNodeCard{i}"' in text and f'name="btnProgressionNode{i}"' in text,f'{f.name} missing node slot {i}')
    for token in ['progressionExplorerIncomingScroll','progressionExplorerOutgoingScroll']:
        req(token in text,f'{f.name} missing scrollable relationship lane {token}')
    req('progressionIncomingTrunk' not in text and 'progressionOutgoingTrunk' not in text,f'{f.name} still contains fixed connector trunks that do not scroll with relationship lanes')

for f in [ROOT/'Config/XUi_Menu/xui.xml',ROOT/'Config/XUi_InGame/xui.xml']:
    text=f.read_text(encoding='utf-8');req('name="rebirthProgressionExplorer"' in text,f'{f.name} missing explorer window group')

loc=(ROOT/'Config/Localization.csv').read_text(encoding='utf-8-sig')
for key in ['xuiRebirthProgressionExplorer','xuiRebirthProgressionExplorerRequirements','xuiRebirthProgressionExplorerPrototypeHint']:
    req(re.search(r'(?m)^'+re.escape(key)+r',',loc) is not None,f'missing localization {key}')

cmd=(ROOT/'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs').read_text(encoding='utf-8')
req('open <neutral|creator|live>' in cmd,'debug command missing PE-03 shell open route')
req('RebirthSurvivorDebug.Enabled' in cmd and 'RebirthProgressionExplorerUiService.Open' in cmd,'PE-03 debug shell open is not gated/routed through UI service')

h=(ROOT/'Scripts/Survivor/Debug/RebirthSurvivorTestHarness.cs').read_text(encoding='utf-8')
for token in ['RebirthProgressionExplorerUiProjectionService.Build','RebirthProgressionExplorerSlotRole.Focus','shellSlots']:
    req(token in h,f'test-all harness missing PE-03 assertion {token}')

WARN.append('PE-03 shell has since been extended by later chunks; this audit now validates the current scrollable relationship-lane shell.')
WARN.append('Vehicle Service Knowledge/action gating remains the previously documented design/runtime gap; PE-03 visualizes current authority rather than changing it')
print(f'PE-03 progression explorer shell static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
sys.exit(1 if ERR else 0)
