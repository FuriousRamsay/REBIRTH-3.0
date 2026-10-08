from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]; ERR=[]; WARN=[]
def req(v,m):
    if not v: ERR.append(m)
prog=ET.parse(ROOT/'Config/_Survivor/progression.xml').getroot()
skills={e.attrib['id'] for e in prog.find('skills')}
knowledge=list(prog.find('knowledge'))
req(len(knowledge)==27,f'expected 27 current Knowledge entries, got {len(knowledge)}')
for e in knowledge:
    kid=e.attrib.get('id',''); assoc=[x.strip() for x in e.attrib.get('associated_skills','').split(',') if x.strip()]
    req(bool(assoc),f'{kid} has no PE-10 associated_skills')
    req(bool(e.attrib.get('explorer_status','')),f'{kid} has no explorer_status')
    for sid in assoc: req(sid in skills,f'{kid} references missing associated Skill {sid}')
models=(ROOT/'Scripts/Survivor/Domain/RebirthSurvivorDefinitionModels.cs').read_text(encoding='utf-8')
loader=(ROOT/'Scripts/Survivor/Definitions/RebirthSurvivorDefinitionLoader.cs').read_text(encoding='utf-8')
registry=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphRegistry.cs').read_text(encoding='utf-8')
validator=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphValidator.cs').read_text(encoding='utf-8')
for token in ['AssociatedSkillIds','ExplorerStatus']: req(token in models,'Knowledge model missing '+token)
for token in ['associated_skills','explorer_status']: req(token in loader,'loader missing '+token)
for token in ['AddKnowledgeAssociationEdges','RebirthProgressionGraphEdgeType.RelatedTo','informational relationship, not itself a gate']: req(token in registry,'registry missing '+token)
req('Knowledge has an Explorer domain association but no current gameplay gate/unlock' in validator,'validator does not distinguish informational-only Knowledge')
req('Skill-to-Skill prerequisite is prohibited' in validator,'Skill-to-Skill prerequisite guard missing')
WARN.append('PE-10 associated Skill links are explicitly informational unless a separate current capability/recipe/action gate exists; this chunk does not manufacture gameplay locks.')
WARN.append('Vehicle Service action gating remains an explicit unresolved gameplay decision; the Explorer can now show Mechanics association without pretending that association is a gate.')
print(f'PE-10 progression content audit/expansion static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
raise SystemExit(1 if ERR else 0)
