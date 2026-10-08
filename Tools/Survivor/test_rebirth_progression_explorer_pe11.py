from pathlib import Path
import xml.etree.ElementTree as ET
import csv
ROOT=Path(__file__).resolve().parents[2]
ERR=[]; WARN=[]
def req(v,m):
    if not v: ERR.append(m)
# Both XUi contexts must contain the legend and navigation hint.
for rel in ['Config/XUi_Menu/windows.xml','Config/XUi_InGame/windows.xml']:
    root=ET.parse(ROOT/rel).getroot()
    win=root.find(".//window[@name='rebirthProgressionExplorerWindow']")
    req(win is not None, rel+' missing Progression Explorer window')
    if win is not None:
        req(win.find(".//rect[@name='progressionExplorerLegend']") is not None, rel+' missing PE-11 legend')
        labels=[e.attrib.get('text','') for e in win.iter('label')]
        req('NODE TYPES' in labels, rel+' missing graphical node-type legend heading')
        req('STATE' in labels, rel+' missing graphical state legend heading')
        for value in ['Background','Skill','Knowledge','Action','Recipe','AVAILABLE','LOCKED','RECOMMENDED','UNKNOWN','INFORMATIONAL']:
            req(value in labels, rel+' missing legend entry '+value)
# Accessibility semantics must not rely on color alone.
controller=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs').read_text(encoding='utf-8')
projection=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerUiProjection.cs').read_text(encoding='utf-8')
for token in ['AccessText','StateBadgeColor','SetNodeIcon','xuiRebirthProgressionExplorerType']:
    req(token in controller,'controller missing '+token)
req('BuildRequirementRows' in projection,'projection missing requirement-row projection')
req('Set(stateLabels[s],AccessText(displayAccess))' in controller,'node cards do not expose explicit textual access state badges')
for rel in ['Config/XUi_Menu/windows.xml','Config/XUi_InGame/windows.xml']:
    xml=(ROOT/rel).read_text(encoding='utf-8')
    req('progressionNodeState0' in xml, rel+' missing explicit node state label')
    req('progressionNodeIconSprite0' in xml, rel+' missing graphical node icon sprite')
    req('progressionExplorerIncomingScrollCapture' in xml and 'progressionExplorerOutgoingScrollCapture' in xml, rel+' missing controller-owned relationship scroll captures')
    req('progressionExplorerIncomingScrollThumb' in xml and 'progressionExplorerOutgoingScrollThumb' in xml, rel+' missing controller-owned relationship scroll thumbs')
# Explorer localization keys must match this project's two-column CSV format.
with (ROOT/'Config/Localization.csv').open(encoding='utf-8-sig',newline='') as f:
    rows=list(csv.reader(f))
by={r[0]:r for r in rows if r}
keys=['xuiRebirthProgressionExplorerLegend','xuiRebirthProgressionExplorerStateLegend','xuiRebirthProgressionExplorerNavHint','xuiRebirthProgressionExplorerModeCreator','xuiRebirthProgressionExplorerModeLive','xuiRebirthProgressionExplorerModeNeutral','xuiRebirthProgressionExplorerStateLocked','xuiRebirthProgressionExplorerStateAvailable','xuiRebirthProgressionExplorerStateRecommended','xuiRebirthProgressionExplorerStateUnknown','xuiRebirthProgressionExplorerStateInfo']
for k in keys:
    req(k in by,k+' localization missing')
    if k in by: req(len(by[k])==2,k+' must use two-column Localization.csv format')
WARN.append('PE-11 static checks cannot prove controller directional focus quality; verify with mouse and controller in Menu and In-Game contexts.')
WARN.append('The fixed 1760x900 virtual XUi canvas is retained; live testing must verify supported UI scale/aspect settings before changing the project-wide coordinate model.')
print(f'PE-11 polish/accessibility static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
raise SystemExit(1 if ERR else 0)

# Localization/centering regression checks added 2026-08-27.
search_src = (root / "Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerSearch.cs").read_text(encoding="utf-8")
projection_src = (root / "Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerUiProjection.cs").read_text(encoding="utf-8")
check("shared display resolver localizes raw DisplayName keys", "string localizedDisplay=Localization.Get(display);" in search_src)
check("projection uses shared display resolver", "return RebirthProgressionExplorerSearchService.ResolveDisplayName(node);" in projection_src)
check("search result buttons are sliced", 'name="btnProgressionSearch0"' in menu_xml and 'type="sliced"' in menu_xml)
