from pathlib import Path
import re, sys, xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthAnimalHandlingService.cs'
text=SRC.read_text(encoding='utf-8')
checks=[]
def check(name, ok): checks.append((name,bool(ok)))
check('Animal Handling service exists', SRC.is_file())
check('runtime threshold vector introduced', 'float[] commandThresholds' in text)
check('threshold vector preserves Guard constant', 'GuardSkillRequired, GuardAreaSkillRequired, HuntingSkillRequired' in text)
check('threshold ordering evaluated through runtime container', 'commandThresholds[0] < commandThresholds[1]' in text and 'commandThresholds[1] < commandThresholds[2]' in text)
check('direct constant threshold comparison removed', 'GuardSkillRequired < GuardAreaSkillRequired && GuardAreaSkillRequired < HuntingSkillRequired' not in text)
check('runtime schema vector introduced', 'int[] persistenceSchema' in text)
check('schema vector preserves CurrentSchemaVersion', 'RebirthDogPersistentRecord.CurrentSchemaVersion' in text)
check('schema validation evaluated through runtime container', 'persistenceSchema[0] < 3' in text)
check('direct constant schema comparison removed', 'RebirthDogPersistentRecord.CurrentSchemaVersion < 3' not in text)
check('threshold failure diagnostic preserved', 'command Skill thresholds are not ordered' in text)
check('schema failure diagnostic preserved', 'dog persistence schema is not Chunk-B current' in text)
check('fresh dog vector preserved', 'RebirthDogPersistentRecord fresh = new RebirthDogPersistentRecord();' in text)
check('basic command vector preserved', 'fresh dog is missing basic commands' in text)
check('Hunting exclusion vector preserved', 'fresh dog must not start with Hunting learned' in text)
check('RunVectors PASS/FAIL contract preserved', '[REBIRTH AnimalHandling] vectors=' in text)
# Structural delimiters after stripping comments/strings approximately.
check('brace counts balanced', text.count('{') == text.count('}'))
# XML parse remains a cheap whole-project guard.
xmls=list(ROOT.rglob('*.xml'))
xml_ok=True
for p in xmls:
    try: ET.parse(p)
    except Exception: xml_ok=False; break
check(f'all XML parse | XML={len(xmls)}', xml_ok)
for name,ok in checks: print(('PASS' if ok else 'FAIL')+' | '+name)
failed=sum(1 for _,ok in checks if not ok)
print(f'PC032_STATIC_CHECKS={len(checks)} PASSED={len(checks)-failed} FAILED={failed} XML={len(xmls)}')
sys.exit(1 if failed else 0)
