#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
import sys,re

ROOT=Path(__file__).resolve().parents[2]
ERR=[]; WARN=[]
def req(cond,msg):
    if not cond: ERR.append(msg)

base=ROOT/'Scripts/Survivor/Progression/Explorer'
for name in [
    'RebirthProgressionGraphDefinition.cs','RebirthProgressionGraphRegistry.cs','RebirthProgressionGraphQueryService.cs',
    'RebirthProgressionExplorerOverlay.cs','RebirthProgressionExplorerDebug.cs','RebirthProgressionGraphValidator.cs']:
    req((base/name).exists(),f'missing PE-02 source {name}')

q=(base/'RebirthProgressionGraphQueryService.cs').read_text(encoding='utf-8')
for token in ['GetFocusedNeighborhood','FindShortestPath','GetPrerequisites','GetDependents','GetTrainingSources','WasTruncated']:
    req(token in q,f'query service missing {token}')
req('maxNodes' in q and 'incomingDepth' in q and 'outgoingDepth' in q,'focused query lacks bounded local-neighborhood controls')

o=(base/'RebirthProgressionExplorerOverlay.cs').read_text(encoding='utf-8')
for token in ['Neutral','CreatorPreview','LiveCharacter','CreateNeutral','CreateCreatorPreview','CreateLiveCharacter','RebirthProgressionNodeAccessState','RebirthProgressionRequirementState']:
    req(token in o,f'overlay layer missing {token}')
req('RebirthCapabilityRegistry.TryGetRecipe' in o,'creator/live overlay does not evaluate current capability authority for recipes')
req('RebirthCapabilityKinds.Any' in o and 'RebirthCapabilityKinds.All' in o,'overlay does not preserve ALL/ANY requirement semantics')
req('StartingKnowledgeIds' in o and 'StartingSkills' in o,'creator preview does not consume resolved draft starting progression')
req('snapshot.KnowledgeIds' in o and 'snapshot.Skills' in o,'live overlay does not consume authoritative owner snapshot progression')

# PE-02 explicitly carries requirement group metadata forward for future visual grouping.
d=(base/'RebirthProgressionGraphDefinition.cs').read_text(encoding='utf-8')
r=(base/'RebirthProgressionGraphRegistry.cs').read_text(encoding='utf-8')
req('RequirementGroupPath' in d and 'RequirementGroupMode' in d,'graph edges do not retain requirement-group metadata')
req('parentMode' in r and 'path+"/"+mode+"["+i+"]"' in r,'registry does not retain deterministic ALL/ANY group paths')
req("Append(e.RequirementGroupPath)" in r and "Append(e.RequirementGroupMode)" in r,'semantic hash omits requirement grouping semantics')

# Known vertical slice must remain ALL(Knowledge, Metalworking >=20, Electrical >=20).
cap=ET.parse(ROOT/'Config/_Survivor/capabilities.xml').getroot()
fence=None
for x in cap.findall('./capability'):
    if x.attrib.get('id')=='capability.recipe.electric_fence_post': fence=x;break
req(fence is not None,'electric fence capability missing')
if fence is not None:
    allg=fence.find('./requires_all')
    req(allg is not None,'electric fence capability no longer requires_all')
    if allg is not None:
        ks=[x.attrib.get('id') for x in allg.findall('./knowledge')]
        ss={x.attrib.get('id'):x.attrib.get('minimum') for x in allg.findall('./skill')}
        req('knowledge.electrical.fundamentals' in ks,'electric fence missing Electrical Fundamentals prerequisite')
        req(ss.get('skill.metalworking')=='20','electric fence Metalworking minimum is not 20')
        req(ss.get('skill.electrical')=='20','electric fence Electrical minimum is not 20')

cmd=(ROOT/'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs').read_text(encoding='utf-8')
for token in ['neighborhood','path <from> <to>','overlay <neutral|creator|live>']:
    req(token in cmd,f'consolidated debug command missing PE-02 diagnostic {token}')

h=(ROOT/'Scripts/Survivor/Debug/RebirthSurvivorTestHarness.cs').read_text(encoding='utf-8')
for token in ['GetFocusedNeighborhood','GetPrerequisites','FindShortestPath','CreateNeutral','CreateCreatorPreview']:
    req(token in h,f'read-only test-all harness missing PE-02 assertion {token}')

# Guardrails: no direct progression mutation in overlay/query layer.
for f in [base/'RebirthProgressionGraphQueryService.cs',base/'RebirthProgressionExplorerOverlay.cs']:
    txt=f.read_text(encoding='utf-8')
    for bad in ['GrantKnowledge(', 'AwardSkill', 'SaveRecord(', 'CommitCharacter', 'SendPackage(']:
        req(bad not in txt,f'{f.name} contains prohibited mutation surface {bad}')

WARN.append('vehicle-service Knowledge/action gating remains the deliberate PE-01 audit gap; PE-02 overlays current authority rather than inventing the missing gate')
print(f'PE-02 progression explorer static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
sys.exit(1 if ERR else 0)
