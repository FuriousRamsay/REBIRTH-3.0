from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
ERR=[]; WARN=[]
def req(v,m):
    if not v: ERR.append(m)
cs=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCharacter.cs').read_text(encoding='utf-8')
expl=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs').read_text(encoding='utf-8')
nav=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerNavigation.cs').read_text(encoding='utf-8')
root=ET.parse(ROOT/'Config/XUi_InGame/windows.xml').getroot()
names={e.attrib.get('name') for e in root.iter() if e.attrib.get('name')}
for prefix,count in [('survivorSkillRow',42),('survivorSkillName',42),('survivorSkillValue',42),('btnSurvivorSkillExplore',42),('survivorKnowledgeRow',27),('survivorKnowledgeName',27),('btnSurvivorKnowledgeExplore',27)]:
    for i in range(count): req(prefix+str(i) in names,'XUi_InGame missing '+prefix+str(i))
for token in ['OpenLiveProgressionExplorer','RebirthProgressionExplorerMode.LiveCharacter','RebirthSurvivorClientState.GetOwnerStateSnapshot','progressionExplorerReturnTab','progressionExplorerReturnFocus','progressionExplorerSkillsContentPosition','progressionExplorerKnowledgeContentPosition','resumeFromProgressionExplorerPending','suspendedForProgressionExplorer','SetNavigationTargetLater']:
    req(token in cs,'live Character integration missing '+token)
for token in ['RebirthProgressionExplorerReturnRegistry','RebirthProgressionExplorerReturnContext']:
    req(token in cs+nav+expl,'launch/return contract missing '+token)
req('skillsContent.ViewComponent.Position = progressionExplorerSkillsContentPosition' in cs,'Skills scroll position is not restored')
req('knowledgeContent.ViewComponent.Position = progressionExplorerKnowledgeContentPosition' in cs,'Knowledge scroll position is not restored')
req('OpenLiveProgressionExplorer(skillExploreIds[index], "Live Skill"' in cs,'Skill click does not deep-link LiveCharacter')
req('OpenLiveProgressionExplorer(knowledgeExploreIds[index], "Live Knowledge"' in cs,'Knowledge click does not deep-link LiveCharacter')
# Explorer is informational only.
section=cs[cs.find('private void OpenLiveProgressionExplorer'):cs.find('private void RenderTraits()',cs.find('private void OpenLiveProgressionExplorer'))]
for bad in ['AwardSkill','GrantKnowledge(','SaveRecord(','SaveCharacter(','SendToServer(']: req(bad not in section,'PE-08 live explorer launch mutates progression via '+bad)
loc=(ROOT/'Config/Localization.csv').read_text(encoding='utf-8-sig')
for key in ['xuiRebirthProgressionExplorerReturnCharacter','xuiRebirthProgressionExplorerLiveSkillHint','xuiRebirthProgressionExplorerLiveKnowledgeHint']:
    req(key in loc,'missing localization '+key)
WARN.append('PE-08 is statically validated only; live Character row clicks, scroll restoration, controller focus and authoritative overlay behavior require in-game acceptance')
print(f'PE-08 progression explorer Live Character integration static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
raise SystemExit(1 if ERR else 0)
