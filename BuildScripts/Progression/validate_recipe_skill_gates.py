#!/usr/bin/env python3
from __future__ import annotations
import csv, os, sys, xml.etree.ElementTree as ET
from collections import Counter

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
DOC=os.path.join(ROOT,'_Documentation','ProjectChanges')
MATRIX=os.path.join(DOC,'REBIRTH_3_0_RECIPE_SKILL_GATE_MATRIX_20260831.csv')
GAPS=os.path.join(DOC,'REBIRTH_3_0_RECIPE_SKILL_GATE_COVERAGE_GAPS_20260831.csv')
CHUNK_C=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_C_WEAPONS_AMMO_TOOLS_MATRIX_20260901.csv')
CHUNK_F=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_F_REMAINING_RECIPE_CLOSURE_MATRIX_20260901.csv')
CAP=os.path.join(ROOT,'Config','_Survivor','capabilities.xml')
RK=os.path.join(ROOT,'Config','_Survivor','recipe_knowledge.xml')
PROG=os.path.join(ROOT,'Config','_Survivor','progression.xml')
CRAFTING_PROGRESS=os.path.join(ROOT,'Config','_Survivor','crafting_progression.xml')
LIT=os.path.join(ROOT,'Config','_Survivor','literature.xml')
LOC=os.path.join(ROOT,'Config','Localization.csv')
WIN=os.path.join(ROOT,'Config','XUi_InGame','windows.xml')
REG=os.path.join(ROOT,'Scripts','Survivor','Capability','RebirthCapabilityRegistry.cs')
INT=os.path.join(ROOT,'Scripts','Survivor','Capability','RebirthRecipeCapabilityIntegration.cs')
PATCH=os.path.join(ROOT,'Scripts','Survivor','Progression','RebirthSurvivorProgressionPatches.cs')
INSTALLER=os.path.join(ROOT,'Scripts','Survivor','Progression','RebirthSurvivorProgressionInstaller.cs')
PERSONAL_AUTH=os.path.join(ROOT,'Scripts','Survivor','Capability','RebirthPersonalCraftAuthorizationService.cs')
OUTCOME=os.path.join(ROOT,'Scripts','Crafting','UI','RebirthCraftOutcomeService.cs')

errors=[]; notes=[]
def fail(s): errors.append(s)
def need(cond,s):
    if not cond: fail(s)
def parse(path):
    try: return ET.parse(path).getroot()
    except Exception as e: fail(f'XML parse failed {os.path.relpath(path,ROOT)}: {e}'); return ET.Element('invalid')
def read(path):
    try:
        with open(path,encoding='utf-8-sig') as f:return f.read()
    except Exception as e: fail(f'read failed {os.path.relpath(path,ROOT)}: {e}'); return ''
def rows(path):
    try:
        with open(path,newline='',encoding='utf-8-sig') as f:return list(csv.DictReader(f))
    except Exception as e: fail(f'CSV read failed {os.path.relpath(path,ROOT)}: {e}'); return []
def fnum(s):
    try:return float(str(s).strip())
    except:return None
def fmt(v):
    if v is None:return ''
    if float(v).is_integer():return str(int(v))
    return ('%.6f'%v).rstrip('0').rstrip('.')

def cap_knowledge(node):
    out=[]
    for n in node.iter('knowledge'):
        x=(n.get('id') or '').strip()
        if x:out.append(x)
    return out
def cap_skills(node):
    out=[]
    for n in node.iter('skill'):
        sid=(n.get('id') or '').strip(); mn=fnum(n.get('minimum')); rec=fnum(n.get('recommended'))
        out.append((sid,mn,rec))
    return out

def expected_matrix_skills(r):
    out=[]
    for prefix in ('primary','secondary','tertiary'):
        sid=(r.get(prefix+'_skill') or '').strip()
        if sid:
            out.append((sid,fnum(r.get(prefix+'_min')),fnum(r.get(prefix+'_recommended'))))
    return out

def expected_gap_skills(r):
    out=[]
    for chunk in (r.get('proposed_skills') or '').split(';'):
        chunk=chunk.strip()
        if not chunk:continue
        try:
            sid, vals=chunk.split(':',1); mn,rec=vals.split('/',1)
            out.append((sid.strip(),float(mn),float(rec)))
        except Exception:
            fail(f"bad proposed_skills syntax for gap {r.get('recipe')}: {chunk}")
    return out

matrix=rows(MATRIX); gaps=rows(GAPS)
need(len(matrix)==187,f'audit matrix expected 187 rows, got {len(matrix)}')
need(len(gaps)==17,f'coverage gaps expected 17 rows, got {len(gaps)}')
all_recipe_names=[(r.get('recipe') or '').strip() for r in matrix]+[(r.get('recipe') or '').strip() for r in gaps]
need(len(all_recipe_names)==len(set(all_recipe_names)), 'duplicate recipe names across matrix/gaps')

crafting_root=parse(CRAFTING_PROGRESS)
runtime_phase=(crafting_root.get('runtime_enforcement') or '').strip()
chunk_g=runtime_phase=='active_chunk_g'

prog=parse(PROG)
skill_nodes=[n for n in prog.findall('.//skill') if n.get('id')]
knowledge_nodes=[n for n in prog.findall('.//knowledge') if n.get('id')]
skill_ids={n.get('id') for n in skill_nodes}; knowledge_ids={n.get('id') for n in knowledge_nodes}
need(len(skill_ids)>=48,f'expected at least the current 48-Skill release catalogue, got {len(skill_ids)}')

caproot=parse(CAP); caps=caproot.findall('./capability')
recipe_caps=[c for c in caps if c.get('target_type')=='recipe']
need(len(recipe_caps)>=204,f'expected at least preserved 204 direct recipe Capabilities, got {len(recipe_caps)}')
ids=[c.get('id') for c in recipe_caps]; targets=[c.get('target_id') for c in recipe_caps]
need(len(ids)==len(set(ids)), 'duplicate direct Capability IDs')
need(len(targets)==len(set(targets)), 'duplicate direct recipe Capability targets')
need(set(all_recipe_names)<=set(targets),f'preserved direct Capability target set missing={sorted(set(all_recipe_names)-set(targets))}')
by_target={c.get('target_id'):c for c in recipe_caps}

# Validate all capability references and ranges.
for c in recipe_caps:
    tgt=c.get('target_id') or '<missing>'
    for k in cap_knowledge(c): need(k in knowledge_ids,f'{tgt}: unknown Knowledge {k}')
    for sid,mn,rec in cap_skills(c):
        need(sid in skill_ids,f'{tgt}: unknown Skill {sid}')
        need(mn is not None and -50 <= mn <= 100,f'{tgt}: invalid minimum for {sid}: {mn}')
        need(rec is not None and -50 <= rec <= 100,f'{tgt}: invalid recommended for {sid}: {rec}')
        if mn is not None and rec is not None: need(rec>=mn,f'{tgt}: recommended below minimum for {sid}')

# Exact audited matrix preservation.
for r in matrix:
    recipe=(r.get('recipe') or '').strip(); c=by_target.get(recipe)
    if c is None: continue
    exp=expected_matrix_skills(r); got=cap_skills(c)
    need(got==exp,f'{recipe}: Skill thresholds differ from audit. expected={exp} got={got}')
    knowledge=(r.get('knowledge') or '').strip()
    expk=[x.strip() for x in knowledge.split(' OR ') if x.strip()]
    gotk=cap_knowledge(c)
    if not chunk_g: need(set(gotk)==set(expk),f'{recipe}: Knowledge differs from audit. expected={expk} got={gotk}')

# Exact gap Skill preservation; Knowledge is validated through recipe_knowledge below.
for r in gaps:
    recipe=(r.get('recipe') or '').strip(); c=by_target.get(recipe)
    if c is None: continue
    exp=expected_gap_skills(r); got=cap_skills(c)
    need(got==exp,f'{recipe}: gap Skill thresholds differ from audit. expected={exp} got={got}')

rkroot=parse(RK); rks=rkroot.findall('./recipe')
need(len(rks)>=199,f'expected at least preserved 199 recipe_knowledge rows, got {len(rks)}')
rkn=[r.get('name') for r in rks]; need(len(rkn)==len(set(rkn)),'duplicate recipe_knowledge recipe names')
rk_by={r.get('name'):r for r in rks}
chunk_c=rows(CHUNK_C) if os.path.exists(CHUNK_C) else []
chunk_f=rows(CHUNK_F) if os.path.exists(CHUNK_F) else []
if chunk_c:
    need(len(chunk_c)==91,f'Chunk C matrix expected 91 rows, got {len(chunk_c)}')
    if chunk_f:
        need(len(chunk_f)==175,f'Chunk F matrix expected 175 rows, got {len(chunk_f)}')
        need(len(recipe_caps)==500,f'Chunk F expected 500 gated recipe Capabilities after PC017 primitive-armor supersession, got {len(recipe_caps)}')
        need(len(rks)==(500 if chunk_g else 439),f'Chunk F/G recipe_knowledge row count unexpected: {len(rks)}')
    else:
        need(len(recipe_caps)>=265,f'Chunk C+ expected at least 265 total recipe Capabilities, got {len(recipe_caps)}')
        need(len(rks)>=222,f'Chunk C+ expected at least 222 recipe_knowledge rows, got {len(rks)}')

if chunk_g:
    need(len(recipe_caps)==500,f'Chunk G expected 500 gated recipe Capabilities after PC017 primitive-armor supersession, got {len(recipe_caps)}')
    need(len(rks)==500,f'Chunk G expected 500 recipe_knowledge rows after PC017 primitive-armor supersession, got {len(rks)}')
    coarse={'knowledge.tailoring.basic','knowledge.farming.practical','knowledge.maintenance.general','knowledge.electrical.fundamentals','knowledge.construction.basic'}
    need(not [r.get('name') for r in rks if r.get('knowledge') in coarse],'Chunk G temporary coarse Knowledge bridge remains in recipe_knowledge')
    notes.append('Chunk G exact recipe Knowledge mapping contract active')

# Every legacy recipe Knowledge mapping must be explicitly preserved by direct Capability.
for rule in rks:
    recipe=rule.get('name') or ''; kid=rule.get('knowledge') or ''
    need(kid in knowledge_ids,f'{recipe}: recipe_knowledge references unknown Knowledge {kid}')
    c=by_target.get(recipe)
    need(c is not None,f'{recipe}: recipe_knowledge has no direct Capability')
    if c is not None: need(kid in cap_knowledge(c),f'{recipe}: direct Capability does not preserve exact mapped Knowledge {kid}')

# Literature integrity: if a recipe rule names literature, it must exist and unlock that exact Knowledge.
litroot=parse(LIT); lit_items=[i for i in litroot.findall('./item') if i.get('id')]
lit_by={i.get('id'):i for i in lit_items}
for rule in rks:
    lid=(rule.get('literature_item') or '').strip(); kid=(rule.get('knowledge') or '').strip(); recipe=rule.get('name') or ''
    if not lid: continue
    item=lit_by.get(lid); need(item is not None,f'{recipe}: missing literature item {lid}')
    if item is not None:
        ik=(item.get('knowledge') or '').strip()
        need(ik==kid,f'{recipe}: literature {lid} unlocks {ik!r}, expected {kid!r}')

# Explicit gap mappings must exist.
gap_names={(r.get('recipe') or '').strip() for r in gaps}
for recipe in sorted(gap_names): need(recipe in rk_by,f'coverage gap recipe {recipe} not added to recipe_knowledge')

# Source contract gates.
reg=read(REG); integ=read(INT); patch=read(PATCH); installer=read(INSTALLER); personal=read(PERSONAL_AUTH); outcome=read(OUTCOME); win=read(WIN); loc=read(LOC)
need('ContainsKnowledge(direct.Requirement,rule.KnowledgeId)' in reg,'registry missing direct Capability Knowledge-preservation gate')
need('does not preserve mapped Knowledge' in reg,'registry missing fail-closed direct Capability error')
need('RebirthSurvivorMode.IsEnabledForCurrentWorld()' in integ,'Capability integration missing Rebirth-mode gate')
need('AuthorizeActiveQueue' in integ,'workstation authority helper missing')
need('[HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.HandleRecipeQueue))]' in patch,'server-authoritative workstation queue patch missing')
need('[HarmonyPatch(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.OnActivated))]' in patch,'personal craft authority must patch the native full craft transaction')
need('RebirthPersonalCraftAuthorizationService.AuthorizeActivation(__instance)' in patch,'personal craft transaction patch is not routed through server authorization service')
need('typeof(RebirthSurvivorPersonalCraftAuthorityPatch)' in installer,'personal craft authority Harmony patch is not installed')
need('RebirthPersonalCraftAuthorizationService.Install()' in installer,'personal craft authorization lifecycle is not installed')
need('NetPackageDirection.ToServer' in personal and 'NetPackageDirection.ToClient' in personal,'personal craft authorization request/response direction contract missing')
need('ValidEntityIdForSender(playerEntityId)' in personal,'personal craft server request missing sender entity validation')
need('ValidUserIdForSender(userId)' in personal,'personal craft server request missing sender user validation')
need('RebirthCapabilityService.EvaluateRecipe(player, recipeName)' in personal,'personal craft server request does not evaluate authoritative Capability state')
need('MaxPendingRequests = 32' in personal,'personal craft pending authorization bound missing')
need('RequestTimeoutSeconds = 10' in personal,'personal craft authorization timeout missing')
need('action.OnActivated();' in personal,'approved personal craft does not re-enter complete native craft transaction')
need('ReferenceEquals(replayAction, action)' in personal,'personal craft one-shot replay guard missing')
need('AddRecipeToCraftAtIndex' not in patch,'saved personal queue restoration path must not be Harmony-gated')
need('[HarmonyPatch(typeof(XUiC_CraftingQueue)' not in personal,'personal authorization service must not Harmony-intercept saved queue restoration')
need('Manager.PlayInsidePlayerHead("ui_denied")' in personal,'personal craft denial feedback missing')
need('skillValue' not in personal and 'CurrentValue' not in personal,'personal craft request must not trust client-supplied Skill values')
need('CurrentValue.ToString' in outcome and 'RequiredValue.ToString' in outcome and 'RecommendedValue.ToString' in outcome,'crafting UI does not show Current/Minimum/Recommended Skill values')
need('RequiredValue > 0f' not in outcome,'crafting UI still hides zero/negative Skill minimums')
need('xuiRebirthKnowledgeSkills' in win and 'xuiRebirthCurrentMinRecommended' in win,'crafting metadata layout missing Knowledge/Skills or Current/Min/Rec labels')
need('xuiRebirthKnowledgeSkills,Knowledge / Skills' in loc,'missing Knowledge / Skills localization')
need('xuiRebirthCurrentMinRecommended,Current / Min / Rec' in loc,'missing Current / Min / Rec localization')

# XML parse all project Config XMLs to catch collateral mistakes.
xml_total=0
for dirpath,_,files in os.walk(os.path.join(ROOT,'Config')):
    for fn in files:
        if fn.lower().endswith('.xml'):
            xml_total+=1
            p=os.path.join(dirpath,fn)
            try: ET.parse(p)
            except Exception as e: fail(f'Config XML parse failed {os.path.relpath(p,ROOT)}: {e}')
notes.append(f'Config XML parsed: {xml_total}')

# Summary composition after the 17 additions.
skill_counts=[len(cap_skills(c)) for c in recipe_caps]
notes.append(f'direct recipe Capabilities: {len(recipe_caps)}')
notes.append(f'recipe Knowledge mappings: {len(rks)}')
notes.append(f'single-skill: {sum(x==1 for x in skill_counts)}')
notes.append(f'multi-skill: {sum(x>=2 for x in skill_counts)}')
notes.append(f'three-skill: {sum(x==3 for x in skill_counts)}')
notes.append(f'known Skills: {len(skill_ids)}')
notes.append(f'known Knowledge definitions: {len(knowledge_ids)}')

print('REBIRTH 3.0 RECIPE SKILL GATING STATIC VALIDATION')
print('project_root:',ROOT)
for n in notes: print('-',n)
if errors:
    print('RESULT: FAIL')
    for e in errors: print('ERROR:',e)
    sys.exit(1)
print('RESULT: PASS')
