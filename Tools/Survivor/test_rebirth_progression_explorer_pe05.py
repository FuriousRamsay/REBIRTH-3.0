#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
import csv,sys
ROOT=Path(__file__).resolve().parents[2]
ERR=[];WARN=[]
def req(c,m):
    if not c: ERR.append(m)
base=ROOT/'Scripts/Survivor/Progression/Explorer'
search=base/'RebirthProgressionExplorerSearch.cs'
ui=ROOT/'Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs'
req(search.exists(),'missing PE-05 search service')
if search.exists():
    t=search.read_text(encoding='utf-8')
    for token in ['RebirthProgressionExplorerSearchResult','RebirthProgressionExplorerSearchService','Search(string query,int limit)','ResolveDisplayName','Score(']: req(token in t,f'PE-05 search source missing {token}')
    req('AwardSkill' not in t and 'GrantKnowledge(' not in t and 'SaveRecord(' not in t,'PE-05 search mutates progression')
if ui.exists():
    t=ui.read_text(encoding='utf-8')
    for token in ['progressionExplorerSearchInput','progressionExplorerSearchPanel','Search_OnChanged','SearchResult_OnPressed','RebirthProgressionExplorerSearchService.Search','navigation.Navigate(preparedFocusId)']: req(token in t,f'PE-05 controller missing {token}')
    req('AwardSkill' not in t and 'GrantKnowledge(' not in t and 'SaveRecord(' not in t,'PE-05 controller contains prohibited progression mutation')
for f in [ROOT/'Config/XUi_Menu/windows.xml',ROOT/'Config/XUi_InGame/windows.xml']:
    try: ET.parse(f)
    except Exception as e: ERR.append(f'XML parse failed {f.relative_to(ROOT)}: {e}')
    tx=f.read_text(encoding='utf-8')
    for token in ['progressionExplorerSearchInput','progressionExplorerSearchPanel','btnProgressionSearch0','btnProgressionSearch5','progressionSearchType0','progressionSearchName0','progressionSearchMeta0']: req(token in tx,f'{f.relative_to(ROOT)} missing PE-05 search UI {token}')
loc=ROOT/'Config/Localization.csv'
with loc.open(encoding='utf-8-sig',newline='') as fh: rows=list(csv.reader(fh))
keys={r[0] for r in rows if r}
for key in ['xuiRebirthProgressionExplorerSearchResults','xuiRebirthProgressionExplorerSearchFollowed']: req(key in keys,f'missing localization {key}')
console=(ROOT/'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs').read_text(encoding='utf-8')
for token in ['progressiongraph search <text>','RebirthProgressionExplorerSearchService.Search(query,12)']: req(token in console,f'console diagnostic missing PE-05 search token {token}')
h=(ROOT/'Scripts/Survivor/Debug/RebirthSurvivorTestHarness.cs').read_text(encoding='utf-8')
for token in ['RebirthProgressionExplorerSearchService.Search("electric fence",6)','skill.mechanics','PE-05 universal search']: req(token in h,f'test-all harness missing PE-05 assertion {token}')
for f in [search,ui,ROOT/'Scripts/Survivor/Debug/RebirthSurvivorTestHarness.cs']:
    tx=f.read_text(encoding='utf-8');req(tx.count('{')==tx.count('}'),f'brace imbalance {f.relative_to(ROOT)}')
WARN.append('PE-05 searches canonical graph nodes only; additional item/output discoverability grows as PE-10 content authoring adds those real nodes')
WARN.append('Permanent Main Menu / Creator / Live launch integrations remain PE-06 / PE-07 / PE-08')
print(f'PE-05 progression explorer universal-search static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
sys.exit(1 if ERR else 0)
