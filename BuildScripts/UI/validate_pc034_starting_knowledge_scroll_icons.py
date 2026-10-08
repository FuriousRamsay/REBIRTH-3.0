#!/usr/bin/env python3
from pathlib import Path
import sys, re, xml.etree.ElementTree as ET

root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
checks=[]
def ck(cond, msg):
    checks.append((bool(cond),msg)); print(('PASS' if cond else 'FAIL')+' | '+msg)

def text(rel): return (root/rel).read_text(encoding='utf-8')

bg_path=root/'Config/_Survivor/backgrounds.xml'
rk_path=root/'Config/_Survivor/recipe_knowledge.xml'
win_path=root/'Config/XUi_Menu/windows.xml'
creator_path=root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs'
manager_path=root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs'
uitext_path=root/'Scripts/Survivor/UI/RebirthSurvivorUiText.cs'
spawn_path=root/'Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs'

bg_root=ET.parse(bg_path).getroot()
chef=next((b for b in bg_root.iter('background') if b.get('id')=='background.chef'),None)
ck(chef is not None,'Chef background exists')
ids=[]
pos_skills=[]
if chef is not None:
    sk=chef.find('starting_skills')
    if sk is not None:
        for e in sk.findall('skill'):
            try: v=float(e.get('value','0'))
            except: v=0
            if v>0: pos_skills.append(e.get('id'))
    kn=chef.find('starting_knowledge')
    if kn is not None: ids=[e.get('id') for e in kn.findall('knowledge') if e.get('id')]
ck(len(ids)==14,f'Chef authored Starting Knowledge count is 14 | actual={len(ids)}')
direct=[x for x in ids if x.startswith('recipe.')]
books=[x for x in ids if x.startswith('recipebook.')]
procedures=[x for x in ids if x.startswith('procedure.')]
ck(len(direct)==10,f'Chef individual recipe discoveries = 10 | actual={len(direct)}')
ck(len(books)==2,f'Chef recipe books = 2 | actual={len(books)}')
ck(len(procedures)==2 and all(x.startswith('procedure.cooking.') for x in procedures),f'Chef cooking procedures = 2 | actual={procedures}')
ck(len(pos_skills)==3,f'Chef positive starting Skills = 3 | actual={len(pos_skills)}')
ck(len(pos_skills)+len(ids)==17,'Chef Profile Summary progression list requires 17 rows (3 Skills + 14 Knowledge)')

rk=ET.parse(rk_path).getroot()
rows=list(rk.iter('recipe'))
def recipes_for_knowledge(k): return [r.get('name') for r in rows if r.get('knowledge')==k and r.get('name')]
book_recipes=[]
for b in books: book_recipes += recipes_for_knowledge(b)
direct_recipes=[]
for d in direct: direct_recipes += recipes_for_knowledge(d)
ck(len(direct_recipes)==14,f'Chef 10 direct recipe discoveries map to 14 recipe rows (four include prepared variants) | actual={len(direct_recipes)}')
ck(len(book_recipes)==15,f'Chef recipe books map to 15 recipes | actual={len(book_recipes)}')
ck(len(set(direct_recipes+book_recipes))==29,f'Chef effective unique starting recipe mappings = 29 | actual={len(set(direct_recipes+book_recipes))}')

win=text('Config/XUi_Menu/windows.xml')
creator=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs')
manager=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs')
uitext=text('Scripts/Survivor/UI/RebirthSurvivorUiText.cs')
spawn=text('Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs')

for token in ['backgroundKnowledgeNativeScrollHost','backgroundKnowledgeNativeScrollView','backgroundKnowledgeNativeScrollProxy','backgroundKnowledgeScrollCapture']:
    ck(token in win and token in creator,f'Creator Starting Knowledge native-scroll binding {token}')
ck('<defaultscrollbar/>' in win,'XUi contains native default scrollbar authoring')
ck('backgroundKnowledgeOffset + i' in creator,'Creator renders Starting Knowledge from scroll offset')
ck('UpdateBackgroundKnowledgeNativeScroll(knowledgeCount)' in creator,'Creator synchronizes Starting Knowledge native scrollbar')
ck('PollBackgroundKnowledgeNativeScroll();' in creator,'Creator polls Starting Knowledge native scrollbar')
ck('backgroundKnowledgeOffset = 0;' in creator,'Creator resets Starting Knowledge scroll when Background changes')
ck('knowledgeCount > BackgroundKnowledgeRows' in creator,'Creator scrollbar is overflow-gated')

for token in ['profileProgressionNativeScrollHost','profileProgressionNativeScrollView','profileProgressionNativeScrollProxy','profileProgressionScrollCapture']:
    ck(token in win and token in manager,f'Profile Summary progression native-scroll binding {token}')
ck('RenderDetailSection(0, ProgressionVisibleRows, progression, progressionOffset, false);' in manager,'Profile Summary uses progression scroll offset')
ck('selectedProgressionDisplayCount = progression.Count;' in manager,'Profile Summary tracks full Skills + Knowledge count')
ck('UpdateProgressionNativeScroll();' in manager,'Profile Summary synchronizes progression native scrollbar')
ck('PollProgressionNativeScroll();' in manager,'Profile Summary polls progression native scrollbar')
ck('progressionOffset = 0;' in manager,'Profile selection resets Skills & Knowledge scroll to top')

ck('StartingKnowledgeIconKey' in uitext,'Central Starting Knowledge icon resolver exists')
ck('StartsWith("recipebook."' in uitext and 'return "rb_bonus_bookworm"' in uitext,'Recipe books use dedicated book icon')
ck('StartsWith("procedure.cooking."' in uitext and 'return "rb_skill_cooking"' in uitext,'Cooking procedures use dedicated cooking-procedure icon')
ck('return "rb_ui_knowledge";' in uitext,'Individual recipes retain recipe/knowledge icon fallback')
ck('StartingKnowledgeIconKey(id)' in creator,'Creator uses typed Starting Knowledge icons')
ck('StartingKnowledgeIconKey(id)' in manager,'Profile Summary uses typed Starting Knowledge icons')
ck('StartingKnowledgeIconKey(knowledgeId)' in creator,'Review summary uses typed Starting Knowledge icons')
ck('StartingKnowledgeIconKey(id)' in spawn,'Pre-spawn profile projection uses typed Starting Knowledge icons')

# Atlas registrations used by resolver.
atlas=text('UIAtlases/RebirthSurvivorIcons/settings.xml')
for sprite in ['rb_ui_knowledge','rb_bonus_bookworm','rb_skill_cooking']:
    ck(f'<sprite name="{sprite}"' in atlas,f'Icon sprite registered: {sprite}')

# Modified C# delimiter sanity.
for rel in [creator_path,manager_path,uitext_path,spawn_path]:
    src=rel.read_text(encoding='utf-8')
    ck(src.count('{')==src.count('}'),f'brace counts balanced: {rel.name}')

xml_count=0; xml_errors=[]
for p in root.rglob('*.xml'):
    xml_count+=1
    try: ET.parse(p)
    except Exception as e: xml_errors.append((p,e))
ck(not xml_errors,f'all project XML parses | XML={xml_count}')

passed=sum(1 for ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC034_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={xml_count}')
sys.exit(1 if failed else 0)
