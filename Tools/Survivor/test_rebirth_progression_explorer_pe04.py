#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
import csv,re,sys
ROOT=Path(__file__).resolve().parents[2]
ERR=[]; WARN=[]
def req(c,m):
    if not c: ERR.append(m)

base=ROOT/'Scripts/Survivor/Progression/Explorer'
nav=base/'RebirthProgressionExplorerNavigation.cs'
ui=ROOT/'Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs'
req(nav.exists(),'missing PE-04 navigation source')
req(ui.exists(),'missing explorer controller')
if nav.exists():
    t=nav.read_text(encoding='utf-8')
    for token in ['RebirthProgressionExplorerLaunchRequest','RebirthProgressionExplorerReturnContext','RebirthProgressionExplorerNavigationState','CanBack','CanForward','BreadcrumbIds','DebugSummary']:
        req(token in t,f'PE-04 navigation source missing {token}')
    req('AwardSkill' not in t and 'GrantKnowledge(' not in t,'PE-04 navigation source mutates progression')
if ui.exists():
    t=ui.read_text(encoding='utf-8')
    for token in ['btnProgressionExplorerBack','btnProgressionExplorerForward','btnProgressionExplorerHome','btnProgressionExplorerReturn','progressionExplorerBreadcrumbs','ReturnOrClose','navigation.CanBack','RebirthProgressionExplorerLaunchRequest']:
        req(token in t,f'PE-04 controller missing {token}')
    req('PermanentActions.Cancel.WasReleased' in t and 'ESC action=return' in t and 'ReturnOrClose();' in t,'ESC must return directly to the caller')
    req('AwardSkill' not in t and 'GrantKnowledge(' not in t and 'SaveRecord(' not in t,'PE-04 controller contains prohibited progression mutation')

for f in [ROOT/'Config/XUi_Menu/windows.xml',ROOT/'Config/XUi_InGame/windows.xml',ROOT/'Config/XUi_Menu/xui.xml',ROOT/'Config/XUi_InGame/xui.xml']:
    try: ET.parse(f)
    except Exception as e: ERR.append(f'XML parse failed {f.relative_to(ROOT)}: {e}')
for f in [ROOT/'Config/XUi_Menu/windows.xml',ROOT/'Config/XUi_InGame/windows.xml']:
    tx=f.read_text(encoding='utf-8')
    for token in ['progressionExplorerBreadcrumbs','progressionExplorerNavigation','btnProgressionExplorerBack','btnProgressionExplorerForward','btnProgressionExplorerHome','btnProgressionExplorerReturn','progressionExplorerNavigationStatus']:
        req(token in tx,f'{f.relative_to(ROOT)} missing PE-04 UI element {token}')

loc=ROOT/'Config/Localization.csv'
with loc.open(encoding='utf-8-sig',newline='') as fh:
    rows=list(csv.reader(fh))
keys={r[0] for r in rows if r}
for key in ['xuiRebirthProgressionExplorerBack','xuiRebirthProgressionExplorerForward','xuiRebirthProgressionExplorerCenter','xuiRebirthProgressionExplorerFit','xuiRebirthProgressionExplorerHome','xuiRebirthProgressionExplorerReturn','xuiRebirthProgressionExplorerBreadcrumbRoot']:
    req(key in keys,f'missing localization {key}')

h=(ROOT/'Scripts/Survivor/Debug/RebirthSurvivorTestHarness.cs').read_text(encoding='utf-8')
for token in ['RebirthProgressionExplorerNavigationState','nav.CanBack','nav.CanForward','nav.Home()','BreadcrumbIds']:
    req(token in h,f'test-all harness missing PE-04 assertion {token}')

# crude brace balance on changed C# to catch accidental text edits
for f in [nav,ui,ROOT/'Scripts/Survivor/Debug/RebirthSurvivorTestHarness.cs']:
    tx=f.read_text(encoding='utf-8')
    req(tx.count('{')==tx.count('}'),f'brace imbalance {f.relative_to(ROOT)}')

WARN.append('PE-04 return context can reopen a caller window group and preserve an opaque return token; exact creator draft/scroll restoration remains PE-07 integration work')
WARN.append('PE-04 no longer exposes Center Selected / Fit Branch because the fixed focused layout made those controls redundant; Back/Forward/Home and clickable breadcrumbs are the meaningful navigation controls')
WARN.append('Universal search remains PE-05; permanent Main Menu/Creator/Live launch integrations remain PE-06/07/08')
print(f'PE-04 progression explorer navigation static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
sys.exit(1 if ERR else 0)
