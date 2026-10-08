from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]; ERR=[]; WARN=[]
def req(v,m):
    if not v: ERR.append(m)
cs=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionLockCrossLink.cs').read_text(encoding='utf-8')
xml=(ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
cap=(ROOT/'Scripts/Survivor/Capability/RebirthCapabilityService.cs').read_text(encoding='utf-8')
evalcs=(ROOT/'Scripts/Survivor/Capability/RebirthCapabilityEvaluation.cs').read_text(encoding='utf-8')
for t in ['TryGetRecipeLock','EvaluateRecipe','MissingHardRequirements','RequirementFocusId','RebirthProgressionExplorerMode.LiveCharacter','RebirthProgressionExplorerReturnContext','XUiC_RebirthRecipeProgressionCrossLink','XUiC_CraftingInfoWindow','AccessTools.Field','btnRebirthRecipeProgressionExplore']:
    req(t in cs,'PE-09 cross-link missing '+t)
req("/windows/window[@name='craftingInfoPanel']" in xml,'craftingInfoPanel append missing')
req('RebirthRecipeProgressionCrossLink, RebirthUtils' in xml,'recipe cross-link controller not authored')
req('rebirthRecipeProgressionLockReason' in xml,'lock reason label missing')
req('btnRebirthRecipeProgressionExplore' in xml,'Explore button missing')
loc=(ROOT/'Config/Localization.csv').read_text(encoding='utf-8-sig')
for key in ['xuiRebirthProgressionExplore','xuiRebirthProgressionRecipeLockedBy','xuiRebirthProgressionRecipeLaunchReason','xuiRebirthProgressionExplorerReturnCrafting']:
    req(key in loc,'missing localization '+key)
# Cross-link must not mutate progression or alter authoritative capability logic.
for bad in ['GrantKnowledge(','AwardSkill(','SaveRecord(','SaveCharacter(','SendToServer(']: req(bad not in cs,'PE-09 cross-link mutates progression via '+bad)
req('EvaluateRequirement' in cap and 'MissingHardRequirements' in evalcs,'authoritative capability evaluation unavailable')
WARN.append('PE-09 recipe cross-link is statically validated only; exact native craftingInfoPanel placement and selected-recipe reflection require in-game 7DTD acceptance')
WARN.append('No existing player-facing REBIRTH blocked-action panel was found in the audited project; RebirthProgressionLockCrossLinkService is the shared PE-09 API for those action surfaces when/where they present a lock.')
print(f'PE-09 progression lock/recipe cross-link static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
raise SystemExit(1 if ERR else 0)
