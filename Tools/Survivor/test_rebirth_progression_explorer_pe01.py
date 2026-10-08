#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
import re, sys

ROOT=Path(__file__).resolve().parents[2]
CFG=ROOT/'Config/_Survivor'
ERR=[]; WARN=[]

def req(cond,msg):
    if not cond: ERR.append(msg)

def ids(path, tag):
    root=ET.parse(path).getroot()
    return {e.attrib['id'] for e in root.findall('.//'+tag) if e.attrib.get('id')}

prog=ET.parse(CFG/'progression.xml').getroot()
skills={e.attrib['id']:e for e in prog.findall('./skills/skill')}
knowledge={e.attrib['id']:e for e in prog.findall('./knowledge/knowledge')}
bgroot=ET.parse(CFG/'backgrounds.xml').getroot()
backgrounds={e.attrib['id']:e for e in bgroot.findall('./background')}
traits={e.attrib['id']:e for e in ET.parse(CFG/'traits.xml').getroot().findall('./trait')}
rules=ET.parse(CFG/'recipe_knowledge.xml').getroot().findall('./recipe')
caproot=ET.parse(CFG/'capabilities.xml').getroot()

req(len(skills)==42, f'expected current 42 Skills, found {len(skills)}')
req(len(knowledge)==27, f'expected current 27 Knowledge entries, found {len(knowledge)}')
req(len(backgrounds)==28, f'expected current 28 Backgrounds, found {len(backgrounds)}')

for bid,b in backgrounds.items():
    for s in b.findall('./starting_skills/skill'):
        req(s.attrib.get('id') in skills, f'{bid} references unknown Skill {s.attrib.get("id")}')
    for k in b.findall('./starting_knowledge/knowledge'):
        req(k.attrib.get('id') in knowledge, f'{bid} references unknown Knowledge {k.attrib.get("id")}')

for r in rules:
    req(r.attrib.get('knowledge') in knowledge, f'recipe {r.attrib.get("name")} references unknown Knowledge {r.attrib.get("knowledge")}')

skill_req_seen=False
for cap in caproot.findall('./capability'):
    for k in cap.findall('.//knowledge'):
        req(k.attrib.get('id') in knowledge, f'capability {cap.attrib.get("id")} references unknown Knowledge {k.attrib.get("id")}')
    for s in cap.findall('.//skill'):
        skill_req_seen=True
        req(s.attrib.get('id') in skills, f'capability {cap.attrib.get("id")} references unknown Skill {s.attrib.get("id")}')
req(skill_req_seen,'expected at least one real capability Skill threshold (electric fence post vertical slice)')

files=[
ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphDefinition.cs',
ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphRegistry.cs',
ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphValidator.cs',
ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphDebug.cs',
]
for f in files: req(f.exists(), f'missing PE-01 source {f.relative_to(ROOT)}')

text=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphDefinition.cs').read_text(encoding='utf-8')
for token in ['RequiresKnowledge','RequiresSkill','UnlocksAction','UnlocksRecipe','TrainsSkill','GrantedByBackground']:
    req(token in text, f'graph definition missing {token}')
reg=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphRegistry.cs').read_text(encoding='utf-8')
req('AddTrainingActionNodes' in reg,'registry does not create player-facing Skill training routes')
req('GetRecipeRulesSnapshot' in reg,'registry does not derive current recipe Knowledge gates')
req('GetCapabilitiesSnapshot' in reg,'registry does not derive current capability gates')
req('StartingKnowledgeIds' in reg and 'StartingSkills' in reg,'registry does not derive Background grants')
req('RequiresSkill' in reg,'registry does not preserve real Skill thresholds')
validator=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphValidator.cs').read_text(encoding='utf-8')
req('Skill-to-Skill prerequisite is prohibited' in validator,'validator does not prohibit fake Skill-to-Skill prerequisite edges')
req('Vehicle Service Knowledge does not currently gate' in validator,'validator does not expose current vehicle-Knowledge/runtime gap')
installer=(ROOT/'Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs').read_text(encoding='utf-8')
req('RebirthProgressionGraphRegistry.BuildFromCurrentAuthority' in installer,'progression installer does not build graph after runtime config/capabilities')
cmd=(ROOT/'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs').read_text(encoding='utf-8')
req('progressiongraph' in cmd and 'ExecuteProgressionGraph' in cmd,'consolidated Survivor debug command lacks progressiongraph diagnostics')

# Audit design/runtime gap intentionally stays a warning: current vehicle service mechanics awards exist,
# but the project does not currently authorize those actions through Vehicle Service Knowledge.
WARN.append('expected audit gap: Vehicle Service Knowledge currently gates vehicle recipes but not general vehicle service actions; PE-01 reports rather than invents this gate')

print(f'PE-01 progression explorer static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
sys.exit(1 if ERR else 0)
