from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
ERR=[]; WARN=[]
def req(v,m):
    if not v: ERR.append(m)
cs=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8')
nav=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerNavigation.cs').read_text(encoding='utf-8')
expl=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs').read_text(encoding='utf-8')
proj=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerUiProjection.cs').read_text(encoding='utf-8')
for f in ['Config/XUi_Menu/windows.xml','Config/XUi_InGame/windows.xml']:
    root=ET.parse(ROOT/f).getroot(); names={e.attrib.get('name') for e in root.iter() if e.attrib.get('name')}
    for prefix,count in [('btnBackgroundSkillExplore',12),('btnBackgroundKnowledgeExplore',5),('btnBackgroundWeaknessExplore',2),('btnReviewSkillExplore',42),('btnReviewKnowledgeExplore',27)]:
        for i in range(count): req(prefix+str(i) in names,f'{f}: missing {prefix}{i}')
for token in ['OpenCreatorProgressionExplorer','RebirthProgressionExplorerMode.CreatorPreview','model.Validation','suspendedForProgressionExplorer','resumeFromProgressionExplorerPending','progression-explorer-return','progressionExplorerDetailContentPosition','progressionExplorerReturnFocus','SetNavigationTargetLater']:
    req(token in cs,'creator integration missing '+token)
for token in ['CreatorPreviewResult','RebirthProgressionExplorerReturnRegistry','TryHandle']:
    req(token in nav+expl,'launch/return contract missing '+token)
req('Build(preparedFocusId,preparedMode,creatorPreviewResult)' in expl,'Explorer does not pass draft preview into projection')
req('if(creatorPreviewResult!=null)return RebirthProgressionExplorerOverlayService.CreateCreatorPreview' in proj,'projection does not prefer current draft preview')
req('bool preserveCreatorSession = suspendedForNativePicker || suspendedForNativeChildEditor || suspendedForProgressionExplorer;' in cs,'creator close does not preserve draft session')
req('model = null;' in cs,'baseline creator cleanup unexpectedly absent')
# must not mutate progression
section=cs[cs.find('private void OpenCreatorProgressionExplorer'):cs.find('private sealed class BackgroundSkillDisplay')]
for bad in ['AwardSkill','GrantKnowledge(','SaveRecord(','SaveCharacter(']: req(bad not in section,'PE-07 explorer launch mutates progression via '+bad)
loc=(ROOT/'Config/Localization.csv').read_text(encoding='utf-8-sig')
req('xuiRebirthProgressionExplorerReturnCreator' in loc,'creator return localization missing')
WARN.append('PE-07 is statically validated only; live creator scroll/focus restoration and XUi click behavior require in-game acceptance')
print(f'PE-07 progression explorer Survivor Creator integration static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
raise SystemExit(1 if ERR else 0)
