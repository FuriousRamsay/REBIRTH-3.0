#!/usr/bin/env python3
from __future__ import annotations
import argparse, csv, os, re, sys, xml.etree.ElementTree as ET
from collections import Counter, defaultdict

parser=argparse.ArgumentParser(description='Validate REBIRTH crafting progression policy/coverage.')
parser.add_argument('--project-root', default=None)
parser.add_argument('--require-complete', action='store_true', help='Fail if any GATED recipe still lacks a live Capability.')
parser.add_argument('--require-runtime', action='store_true', help='Fail unless crafting policy runtime integration is present (Chunk B+).')
parser.add_argument('--require-chunk-c', action='store_true', help='Fail unless the Chunk C weapons/ammunition/tools closure is present and exact.')
parser.add_argument('--require-chunk-d', action='store_true', help='Fail unless the Chunk D structural/access/defense closure is present and exact.')
parser.add_argument('--require-chunk-e', action='store_true', help='Fail unless the Chunk E furniture/storage/fixtures closure is present and exact.')
parser.add_argument('--require-chunk-f', action='store_true', help='Fail unless the Chunk F remaining-recipe closure is present and exact.')
parser.add_argument('--require-chunk-g', action='store_true', help='Fail unless Chunk G grouped Knowledge/literature/migration/XP integration is present and exact.')
args=parser.parse_args()
ROOT=os.path.abspath(args.project_root or os.path.join(os.path.dirname(__file__),'..','..'))
CFG=os.path.join(ROOT,'Config','_Survivor')
DOC=os.path.join(ROOT,'_Documentation','ProjectChanges')
MAN=os.path.join(CFG,'crafting_progression.xml')
CAP=os.path.join(CFG,'capabilities.xml')
PROG=os.path.join(CFG,'progression.xml')
RK=os.path.join(CFG,'recipe_knowledge.xml')
SOURCE=os.path.join(ROOT,'_Documentation','FeatureDesign','SurvivorSkillSystem','Audit','2026-08-29','Pass3D','REBIRTH_3_0_PASS3D_NATIVE_RECIPE_AUTHORED_CLASSIFICATION_630.csv')
SOURCE_TXT=os.path.join(ROOT,'_Documentation','FeatureDesign','SurvivorSkillSystem','Audit','2026-08-29','Pass3D','rebirth_native_recipe_catalogue_20260829-200949.txt')
CRIT=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CRITICAL_GAPS_20260901.csv')
NORM=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_A_NORMALIZED_RECIPE_AUDIT_20260901.csv')
FURN=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_A_FURNITURE_HELPER_AUDIT_20260901.csv')
CHUNK_C=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_C_WEAPONS_AMMO_TOOLS_MATRIX_20260901.csv')
CHUNK_C_SOURCE=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_C_SOURCE_MATERIAL_AUDIT_20260901.csv')
CHUNK_D=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_D_BLOCKS_ACCESS_DEFENSES_MATRIX_20260901.csv')
CHUNK_D_SOURCE=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_D_SOURCE_MATERIAL_AUDIT_20260901.csv')
CHUNK_E=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_E_FURNITURE_STORAGE_FIXTURES_MATRIX_20260901.csv')
CHUNK_E_SOURCE=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_E_SOURCE_MATERIAL_AUDIT_20260901.csv')
CHUNK_E_HELPERS=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_E_HELPER_VARIANT_AUDIT_20260901.csv')
CHUNK_F_MATRIX=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_F_REMAINING_RECIPE_CLOSURE_MATRIX_20260901.csv')
CHUNK_F_SOURCE=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_F_SOURCE_MATERIAL_AUDIT_20260901.csv')
CHUNK_G_MATRIX=os.path.join(DOC,'REBIRTH_3_0_CRAFTING_PROGRESSION_CHUNK_G_KNOWLEDGE_LITERATURE_MATRIX_20260901.csv')
LITERATURE=os.path.join(CFG,'literature.xml')
LIT_DIST=os.path.join(CFG,'literature_distribution.xml')
ITEMS=os.path.join(CFG,'items.xml')
LOOT=os.path.join(CFG,'loot.xml')
TRADERS=os.path.join(CFG,'traders.xml')
LOCALIZATION=os.path.join(ROOT,'Config','Localization.csv')
SKILL_SOURCES=os.path.join(CFG,'skill_sources.xml')
MIGRATION=os.path.join(ROOT,'Scripts','Survivor','Persistence','RebirthWorldCharacterMigration.cs')
WORLD_MODEL=os.path.join(ROOT,'Scripts','Survivor','Domain','RebirthSurvivorRuntimeModels.cs')
RUNTIME=os.path.join(ROOT,'Scripts','Survivor','CraftingProgression','RebirthCraftingProgressionRegistry.cs')
RUNTIME_DEFS=os.path.join(ROOT,'Scripts','Survivor','CraftingProgression','RebirthCraftingProgressionDefinitions.cs')
RUNTIME_VECTORS=os.path.join(ROOT,'Scripts','Survivor','CraftingProgression','RebirthCraftingProgressionVectorHarness.cs')
CAP_SERVICE=os.path.join(ROOT,'Scripts','Survivor','Capability','RebirthCapabilityService.cs')
PERSONAL=os.path.join(ROOT,'Scripts','Survivor','Capability','RebirthPersonalCraftAuthorizationService.cs')
WORKSTATION=os.path.join(ROOT,'Scripts','Survivor','Capability','RebirthRecipeCapabilityIntegration.cs')
RUNTIME_CONFIG=os.path.join(ROOT,'Scripts','Survivor','Progression','RebirthProgressionRuntimeConfig.cs')
CRAFT_SKILL=os.path.join(ROOT,'Scripts','Survivor','Progression','RebirthServiceCraftSkillService.cs')
GRAPH=os.path.join(ROOT,'Scripts','Survivor','Progression','Explorer','RebirthProgressionGraphRegistry.cs')
DEBUG=os.path.join(ROOT,'Scripts','Survivor','Debug','ConsoleCmdRebirthSurvivor.cs')
INSTALLER=os.path.join(ROOT,'Scripts','Survivor','Progression','RebirthSurvivorProgressionInstaller.cs')

errors=[]; notes=[]; warnings=[]
def fail(x): errors.append(x)
def need(v,x):
    if not v: fail(x)
def parse(path):
    try:return ET.parse(path).getroot()
    except Exception as e: fail(f'XML parse failed {os.path.relpath(path,ROOT)}: {e}'); return ET.Element('invalid')
def rows(path):
    try:
        with open(path,newline='',encoding='utf-8-sig') as f:return list(csv.DictReader(f))
    except Exception as e: fail(f'CSV read failed {os.path.relpath(path,ROOT)}: {e}'); return []
def read(path):
    try:
        with open(path,encoding='utf-8-sig') as f:return f.read()
    except Exception as e: fail(f'read failed {os.path.relpath(path,ROOT)}: {e}'); return ''

source_rows=rows(SOURCE)
source_names=[(r.get('recipe') or '').strip() for r in source_rows if (r.get('recipe') or '').strip()]
source_unique=set(source_names)
need(len(source_rows)==630,f'Pass 3D target recipe source expected 630 rows, got {len(source_rows)}')
need(len(source_unique)==581,f'Pass 3D normalized target recipe IDs expected 581, got {len(source_unique)}')
notes.append(f'target recipe source rows: {len(source_rows)}; normalized IDs: {len(source_unique)}')
source_txt=read(SOURCE_TXT)
expected_sha='8c20183def9543429e6ed4dd91ab6dac269286624984f1a61cf7f83899ef28b3'
need(expected_sha in source_txt,'target native recipe SHA provenance missing from Pass 3D capture text')

prog=parse(PROG)
skills={n.get('id') for n in prog.findall('.//skill') if n.get('id')}
knowledge={n.get('id') for n in prog.findall('.//knowledge') if n.get('id')}
need(len(skills)>=48,f'expected at least the current 48-Skill release catalogue, got {len(skills)}')

caproot=parse(CAP)
caps=[c for c in caproot.findall('./capability') if c.get('target_type')=='recipe']
cap_by_target={c.get('target_id'):c for c in caps}
cap_by_id={c.get('id'):c for c in caps}
need(len(caps)>=204,f'expected at least the preserved 204 recipe Capabilities, got {len(caps)}')
need(len(cap_by_target)==len(caps),'duplicate current Capability recipe targets')
need(len(cap_by_id)==len(caps),'duplicate current Capability IDs')

rkroot=parse(RK)
rk=[r for r in rkroot.findall('./recipe') if r.get('name')]
rk_by={r.get('name'):r for r in rk}
need(len(rk)>=199,f'expected at least the preserved 199 recipe Knowledge mappings, got {len(rk)}')

man=parse(MAN)
need(man.tag=='crafting_progression','crafting_progression.xml root must be <crafting_progression>')
need(man.get('schema_version')=='1','crafting progression schema_version must be 1')
need(man.get('native_source_sha256')==expected_sha,'manifest native source SHA does not match target capture')
need(man.get('native_source_rows')=='630','manifest native_source_rows must be 630')
need(man.get('native_unique_recipes')=='581','manifest native_unique_recipes must be 581')
entries=man.findall('./recipes/recipe')
ids=[e.get('id') for e in entries]
need(len(ids)==len(set(ids)),'duplicate recipe IDs in crafting progression manifest')
by_id={e.get('id'):e for e in entries}
need(len(entries)==656,f'Chunk A manifest expected 656 recipes (581 native + 75 current REBIRTH-only Capability targets), got {len(entries)}')
need(man.get('manifest_recipe_count')==str(len(entries)),'manifest_recipe_count metadata mismatch')

native_manifest={e.get('id') for e in entries if e.get('source')=='native_b259_capture'}
rebirth_manifest={e.get('id') for e in entries if e.get('source')=='rebirth_current'}
need(native_manifest==source_unique,f'native manifest set mismatch: missing={sorted(source_unique-native_manifest)[:20]} extra={sorted(native_manifest-source_unique)[:20]}')
expected_rebirth=set(cap_by_target)-source_unique
need(rebirth_manifest==expected_rebirth,f'REBIRTH-only manifest set mismatch: missing={sorted(expected_rebirth-rebirth_manifest)[:20]} extra={sorted(rebirth_manifest-expected_rebirth)[:20]}')
notes.append(f'manifest entries: {len(entries)} = native {len(native_manifest)} + REBIRTH/current {len(rebirth_manifest)}')

allowed={'gated','universal','disabled'}
policy_count=Counter()
impl_count=Counter()
planned=[]
for e in entries:
    rid=e.get('id') or '<missing>'
    pol=(e.get('policy') or '').strip()
    impl=(e.get('implementation') or '').strip()
    policy_count[pol]+=1; impl_count[impl]+=1
    need(pol in allowed,f'{rid}: invalid/unfinished policy {pol!r}; owned manifest entries must be gated/universal/disabled')
    need(bool((e.get('family') or '').strip()),f'{rid}: missing family')
    ps=(e.get('primary_skill') or '').strip()
    if ps: need(ps in skills,f'{rid}: unknown primary Skill {ps}')
    know=[x.strip() for x in (e.get('knowledge') or '').split(',') if x.strip()]
    for kid in know: need(kid in knowledge,f'{rid}: unknown Knowledge {kid}')
    cid=(e.get('capability') or '').strip()
    if pol=='gated':
        need(bool(ps),f'{rid}: GATED recipe missing explicit primary_skill')
        need(bool(cid),f'{rid}: GATED recipe missing capability ID/reservation')
        if impl=='existing_capability':
            c=cap_by_id.get(cid)
            need(c is not None,f'{rid}: existing_capability references missing {cid}')
            if c is not None: need(c.get('target_id')==rid,f'{rid}: Capability {cid} targets {c.get("target_id")}')
        elif impl=='planned_capability':
            planned.append(rid)
            if args.require_complete: fail(f'{rid}: planned Capability not implemented')
        else:
            fail(f'{rid}: GATED recipe has invalid implementation={impl!r}')
    elif pol=='universal':
        need(impl=='policy_declared',f'{rid}: UNIVERSAL recipe must use implementation=policy_declared, got {impl}')
        need(not cid,f'{rid}: UNIVERSAL recipe must not reference a Capability')
    elif pol=='disabled':
        if args.require_runtime:
            need(impl=='runtime_disabled',f'{rid}: DISABLED recipe must be runtime_disabled once Chunk B runtime authority is required, got {impl}')
        else:
            need(impl in ('runtime_disable_pending','runtime_disabled'),f'{rid}: DISABLED recipe implementation state invalid: {impl}')
        need(not cid,f'{rid}: DISABLED recipe must not reference a Capability')

# All existing Capability recipes must be protected by the manifest and must retain exact Capability IDs.
for target,c in cap_by_target.items():
    e=by_id.get(target)
    need(e is not None,f'current Capability target {target} missing from crafting progression manifest')
    if e is not None:
        need(e.get('policy')=='gated',f'current Capability target {target} not GATED in manifest')
        need(e.get('implementation')=='existing_capability',f'current Capability target {target} not marked existing_capability')
        need(e.get('capability')==c.get('id'),f'{target}: manifest Capability {e.get("capability")} != current {c.get("id")}')

# Every current recipe Knowledge mapping must remain represented as a gated recipe.
for target,r in rk_by.items():
    e=by_id.get(target)
    need(e is not None,f'recipe Knowledge target {target} missing from manifest')
    if e is not None:
        need(e.get('policy')=='gated',f'recipe Knowledge target {target} must remain GATED')
        mk={x.strip() for x in (e.get('knowledge') or '').split(',') if x.strip()}
        need((r.get('knowledge') or '') in mk,f'{target}: manifest does not preserve mapped Knowledge {r.get("knowledge")}')

crit_rows=rows(CRIT)
crit=defaultdict(set)
for r in crit_rows: crit[(r.get('group') or '').strip()].add((r.get('recipe') or '').strip())
expected_counts={
 'melee_weapons':17,'archery_weapons_ammo':13,'structural_shapes':5,
 'nonpowered_doors_hatches_shutters_drawbridge':20,'powered_doors_hatches_drawbridge':11,
 'furniture_decor':21,'fixtures_storage':4,'secure_storage':7,'defensive_building':7,
 'construction_tools':2,'modern_firearms_deferred':18,
}
for group,n in expected_counts.items():
    need(len(crit[group])==n,f'critical ledger {group} expected {n}, got {len(crit[group])}')
    for rid in crit[group]: need(rid in by_id,f'critical recipe {rid} ({group}) missing from manifest')

# Critical policy invariants. Chunk C explicitly preserves primitive early-game recipes as UNIVERSAL rather than inventing hard locks.
primitive_melee={'meleeWpnBladeT0BoneKnife','meleeWpnClubT0WoodenClub','meleeWpnKnucklesT0LeatherKnuckles','meleeWpnSledgeT0StoneSledgehammer','meleeWpnSpearT0StoneSpear'}
for rid in crit['melee_weapons']:
    e=by_id.get(rid)
    if e is None: continue
    if rid in primitive_melee:
        need(e.get('policy')=='universal',f'{rid}: primitive melee must remain explicit UNIVERSAL')
    else:
        need(e.get('policy')=='gated' and e.get('primary_skill')=='skill.metalworking',f'{rid}: non-primitive melee fabrication must be GATED / Metalworking-owned')
primitive_archery={'ammoArrowStone','ammoCrossbowBoltStone','gunBowT0PrimitiveBow'}
for rid in crit['archery_weapons_ammo']:
    e=by_id.get(rid)
    if e is None: continue
    if rid in primitive_archery:
        need(e.get('policy')=='universal' and e.get('primary_skill')=='skill.archery',f'{rid}: primitive Archery recipe must be explicit UNIVERSAL / Archery-owned')
    else:
        need(e.get('policy')=='gated' and e.get('primary_skill')=='skill.archery',f'{rid}: advanced Archery recipe must be GATED / skill.archery primary')
for rid in crit['modern_firearms_deferred']:
    e=by_id.get(rid)
    if e is not None: need(e.get('policy')=='disabled' and e.get('primary_skill')=='skill.gunsmithing',f'{rid}: complete modern firearm must be explicitly DISABLED / Gunsmithing owned')
for rid in crit['structural_shapes']:
    e=by_id.get(rid)
    if e is None: continue
    need(e.get('primary_skill')=='skill.construction',f'{rid}: structural shape must be Construction-owned')
    if rid in ('frameShapes:VariantHelper','woodShapes:VariantHelper'): need(e.get('policy')=='universal',f'{rid}: early structural tier must remain UNIVERSAL')
    else: need(e.get('policy')=='gated',f'{rid}: advanced structural tier must be GATED')
for group in ('nonpowered_doors_hatches_shutters_drawbridge','powered_doors_hatches_drawbridge','furniture_decor','fixtures_storage','secure_storage','defensive_building','construction_tools'):
    for rid in crit[group]:
        e=by_id.get(rid)
        if e is not None:
            need(e.get('policy')=='gated',f'{rid}: {group} must be GATED')
            need(e.get('primary_skill')=='skill.construction',f'{rid}: {group} must use Construction as primary fabrication Skill')

# Misclassification regression: furniture/storage must never be assigned to Farming.
for e in entries:
    fam=e.get('family') or ''
    if fam.startswith('construction.furniture') or fam.startswith('construction.fixtures') or fam.startswith('construction.storage'):
        need(e.get('primary_skill')!='skill.farming',f'{e.get("id")}: furniture/storage regressed to Farming')

# Furniture/helper audit must enumerate all approved helper/storage entry points and explicitly disclose missing exact block-member capture.
furn_rows=rows(FURN)
furn_ids={r.get('recipe_helper_or_recipe') for r in furn_rows}
required_furn=crit['furniture_decor']|crit['fixtures_storage']|crit['secure_storage']
need(required_furn <= furn_ids,f'furniture helper audit missing approved entries: {sorted(required_furn-furn_ids)}')
for r in furn_rows:
    need(r.get('target_recipe_capture')=='VERIFIED',f'{r.get("recipe_helper_or_recipe")}: furniture audit recipe source not verified')
    need(r.get('exact_variant_member_enumeration')=='BLOCKED_TARGET_BLOCKS_XML_NOT_RETAINED',f'{r.get("recipe_helper_or_recipe")}: exact helper member status must be explicit, not guessed')

# Normalized ledger must have one row per target recipe name and agree on policy/primary skill.
norm=rows(NORM)
need(len(norm)==581,f'normalized Chunk A audit expected 581 rows, got {len(norm)}')
norm_by={r.get('recipe'):r for r in norm}
need(set(norm_by)==source_unique,'normalized audit recipe set mismatch')
chunk_c_rows=rows(CHUNK_C) if os.path.exists(CHUNK_C) else []
chunk_c_ids={r.get('recipe') for r in chunk_c_rows if r.get('recipe')}
for rid,r in norm_by.items():
    e=by_id.get(rid)
    if e is None or rid in chunk_c_ids or (e.get('decision_status') or '').startswith('approved_chunk_'): continue # later approved chunks supersede the Chunk A planning ledger
    if rid not in {'armorPrimitiveBoots','armorPrimitiveGloves','armorPrimitiveHelmet','armorPrimitiveOutfit'}:
        need((r.get('corrected_policy') or '').lower()==e.get('policy'),f'{rid}: normalized audit policy mismatch')
        # PC024 intentionally supersedes the historical Cooking assignment for prepared drinks.
        # Preserve the old audit row as evidence, but do not treat an approved Drink Preparation
        # reassignment as a regression in this historical validator.
        if not (rid.startswith('drink') and (e.get('primary_skill') or '')=='skill.drink_preparation'):
            need((r.get('primary_skill') or '')==(e.get('primary_skill') or ''),f'{rid}: normalized audit primary Skill mismatch')
    else:
        need(e.get('policy')=='universal' and not (e.get('primary_skill') or '') and not (e.get('capability') or '') and not (e.get('knowledge') or ''),f'{rid}: PC017 primitive armor universal policy mismatch')

# Chunk C exact decision matrix.
def cap_skills(c):
    out=[]
    req=c.find('./requires_all') if c is not None else None
    if req is None:return out
    for n in req.findall('./skill'):
        out.append((n.get('id'),float(n.get('minimum')),float(n.get('recommended'))))
    return out
def cap_knowledge(c):
    req=c.find('./requires_all') if c is not None else None
    return [n.get('id') for n in req.findall('./knowledge')] if req is not None else []
def fnum(x):
    return None if x is None or str(x).strip()=='' else float(x)
if chunk_c_rows:
    need(len(chunk_c_rows)==91,f'Chunk C matrix expected 91 rows, got {len(chunk_c_rows)}')
    cc_count=Counter((r.get('policy') or '').strip().lower() for r in chunk_c_rows)
    need(cc_count==Counter({'gated':61,'universal':12,'disabled':18}),f'Chunk C matrix policy counts wrong: {dict(cc_count)}')
    for r in chunk_c_rows:
        rid=(r.get('recipe') or '').strip(); pol=(r.get('policy') or '').strip().lower(); e=by_id.get(rid)
        need(e is not None,f'Chunk C {rid}: missing manifest entry')
        if e is None: continue
        need(e.get('policy')==pol,f'Chunk C {rid}: manifest policy {e.get("policy")} != {pol}')
        need((e.get('family') or '')==(r.get('family') or ''),f'Chunk C {rid}: family mismatch')
        need((e.get('primary_skill') or '')==(r.get('primary_skill') or ''),f'Chunk C {rid}: primary Skill mismatch')
        if pol=='gated':
            need(e.get('implementation')=='existing_capability',f'Chunk C {rid}: GATED recipe not live')
            cid=(r.get('capability') or '').strip(); c=cap_by_id.get(cid)
            need(c is not None,f'Chunk C {rid}: missing Capability {cid}')
            if c is not None:
                need(c.get('target_id')==rid,f'Chunk C {rid}: Capability targets {c.get("target_id")}')
                need(c.get('category')==(r.get('category') or ''),f'Chunk C {rid}: category mismatch')
                exp=[]
                for prefix in ('primary','secondary','tertiary'):
                    sid=(r.get(prefix+'_skill') or '').strip() if prefix!='primary' else (r.get('primary_skill') or '').strip()
                    mn=fnum(r.get(prefix+'_min')); rec=fnum(r.get(prefix+'_recommended'))
                    if sid and mn is not None and rec is not None: exp.append((sid,mn,rec))
                need(cap_skills(c)==exp,f'Chunk C {rid}: Skill thresholds differ. expected={exp} got={cap_skills(c)}')
                kid=(r.get('knowledge') or '').strip(); gotk=cap_knowledge(c)
                if not args.require_chunk_g: need(gotk==([kid] if kid else []),f'Chunk C {rid}: Knowledge mismatch expected={kid!r} got={gotk}')
                if kid and not args.require_chunk_g:
                    rr=rk_by.get(rid); need(rr is not None and rr.get('knowledge')==kid,f'Chunk C {rid}: recipe_knowledge missing {kid}')
        elif pol=='universal':
            need(e.get('implementation')=='policy_declared' and not e.get('capability'),f'Chunk C {rid}: UNIVERSAL must have no Capability')
        elif pol=='disabled':
            need(e.get('implementation')=='runtime_disabled' and not e.get('capability'),f'Chunk C {rid}: DISABLED must be runtime_disabled')
    source_rows_c=rows(CHUNK_C_SOURCE)
    need(len(source_rows_c)==91,f'Chunk C source/material audit expected 91 rows, got {len(source_rows_c)}')
if args.require_chunk_c or args.require_chunk_d or args.require_chunk_e or args.require_chunk_f or args.require_chunk_g:
    need(bool(chunk_c_rows),'Chunk C matrix missing')
    need(man.get('runtime_enforcement') in ('active_chunk_c','active_chunk_d','active_chunk_e','active_chunk_f','active_chunk_g'),f'Chunk C+ manifest runtime_enforcement is {man.get("runtime_enforcement")!r}')
    if not args.require_chunk_d and not args.require_chunk_e and not args.require_chunk_f and not args.require_chunk_g:
        need(len(caps)==265,f'Chunk C expected 265 live recipe Capabilities, got {len(caps)}')
        need(len(rk)==222,f'Chunk C expected 222 recipe Knowledge mappings, got {len(rk)}')
        need(policy_count==Counter({'gated':512,'universal':126,'disabled':18}),f'Chunk C manifest policy counts wrong: {dict(policy_count)}')
        need(impl_count.get('existing_capability')==265 and impl_count.get('planned_capability')==247,f'Chunk C implementation counts wrong: {dict(impl_count)}')
    if os.path.isfile(RUNTIME_VECTORS):
        chunk_c_vectors=read(RUNTIME_VECTORS)
        need(('Chunk C vectors' in chunk_c_vectors) or ('Chunk D vectors' in chunk_c_vectors) or ('Chunk E vectors' in chunk_c_vectors) or ('Chunk F vectors' in chunk_c_vectors) or ('Chunk G vectors' in chunk_c_vectors),'Chunk C+ runtime vector harness header not updated')
        need('primitive melee universal' in chunk_c_vectors,'Chunk C runtime vectors missing primitive-universal case')
        need('stun baton live gated' in chunk_c_vectors and 'stun baton primary Metalworking' in chunk_c_vectors,'Chunk C runtime vectors missing Stun Baton fabrication case')
        need('exploding arrow primary Explosives' in chunk_c_vectors,'Chunk C runtime vectors missing explosive-Archery ownership case')
        need('gas can primary Chemistry' in chunk_c_vectors,'Chunk C runtime vectors missing source-corrected gas-can domain case')

# Chunk D exact decision matrix: structural shapes, access, powered access, defenses, and reviewed helpers.
chunk_d_rows=rows(CHUNK_D) if os.path.exists(CHUNK_D) else []
if chunk_d_rows:
    need(len(chunk_d_rows)==51,f'Chunk D matrix expected 51 rows, got {len(chunk_d_rows)}')
    cd_count=Counter((r.get('policy') or '').strip().lower() for r in chunk_d_rows)
    need(cd_count==Counter({'gated':47,'universal':4}),f'Chunk D matrix policy counts wrong: {dict(cd_count)}')
    cd_ids={r.get('recipe') for r in chunk_d_rows}
    need(cd_ids==(crit['structural_shapes']|crit['nonpowered_doors_hatches_shutters_drawbridge']|crit['powered_doors_hatches_drawbridge']|crit['defensive_building']|{'glassBlockVariantHelper','ironBars','ironBarsCentered','ironBarsCNR','ladderMetal','ladderSteel','ladderWood','woodLogPillar100'}),f'Chunk D matrix recipe set mismatch')
    for r in chunk_d_rows:
        rid=(r.get('recipe') or '').strip(); pol=(r.get('policy') or '').strip().lower(); e=by_id.get(rid)
        need(e is not None,f'Chunk D {rid}: missing manifest entry')
        if e is None: continue
        need(e.get('decision_status')=='approved_chunk_d',f'Chunk D {rid}: decision_status not approved_chunk_d')
        need(e.get('policy')==pol,f'Chunk D {rid}: manifest policy mismatch')
        need((e.get('family') or '')==(r.get('family') or ''),f'Chunk D {rid}: family mismatch')
        need((e.get('primary_skill') or '')=='skill.construction',f'Chunk D {rid}: primary crafting Skill must be Construction')
        if pol=='gated':
            need(e.get('implementation')=='existing_capability',f'Chunk D {rid}: GATED recipe not live')
            cid=(r.get('capability') or '').strip(); c=cap_by_id.get(cid)
            need(c is not None,f'Chunk D {rid}: missing Capability {cid}')
            if c is not None:
                need(c.get('target_id')==rid,f'Chunk D {rid}: Capability target mismatch')
                exp=[]
                for prefix in ('primary','secondary','tertiary'):
                    sid=(r.get(prefix+'_skill') or '').strip(); mn=fnum(r.get(prefix+'_min')); rec=fnum(r.get(prefix+'_recommended'))
                    if sid and mn is not None and rec is not None: exp.append((sid,mn,rec))
                need(cap_skills(c)==exp,f'Chunk D {rid}: Skill thresholds differ. expected={exp} got={cap_skills(c)}')
                kid=(r.get('knowledge') or '').strip(); gotk=cap_knowledge(c)
                if not args.require_chunk_g: need(gotk==([kid] if kid else []),f'Chunk D {rid}: Knowledge mismatch expected={kid!r} got={gotk}')
                if kid and not args.require_chunk_g:
                    rr=rk_by.get(rid); need(rr is not None and rr.get('knowledge')==kid,f'Chunk D {rid}: recipe_knowledge missing {kid}')
        else:
            need(e.get('implementation')=='policy_declared' and not e.get('capability'),f'Chunk D {rid}: UNIVERSAL must remain capability-free')
    source_rows_d=rows(CHUNK_D_SOURCE)
    need(len(source_rows_d)==51,f'Chunk D source/material audit expected 51 rows, got {len(source_rows_d)}')
    need(all((r.get('target_recipe_verified') or '')=='YES' for r in source_rows_d),'Chunk D source audit contains an unverified target recipe ID')
if args.require_chunk_d or args.require_chunk_e or args.require_chunk_f or args.require_chunk_g:
    need(bool(chunk_d_rows),'Chunk D matrix missing')
    need(man.get('runtime_enforcement') in ('active_chunk_d','active_chunk_e','active_chunk_f','active_chunk_g'),f'Chunk D+ manifest runtime_enforcement is {man.get("runtime_enforcement")!r}')
    if not args.require_chunk_e and not args.require_chunk_f and not args.require_chunk_g:
        need(len(caps)==305,f'Chunk D expected 305 live recipe Capabilities, got {len(caps)}')
        need(len(rk)==261,f'Chunk D expected 261 recipe Knowledge mappings, got {len(rk)}')
        need(policy_count==Counter({'gated':512,'universal':126,'disabled':18}),f'Chunk D manifest policy counts wrong: {dict(policy_count)}')
        need(impl_count.get('existing_capability')==305 and impl_count.get('planned_capability')==207,f'Chunk D implementation counts wrong: {dict(impl_count)}')
    if os.path.isfile(RUNTIME_VECTORS):
        dv=read(RUNTIME_VECTORS)
        need(('Chunk D vectors' in dv) or ('Chunk E vectors' in dv) or ('Chunk F vectors' in dv) or ('Chunk G vectors' in dv),'Chunk D+ runtime vector harness header not updated')
        need('wood shapes remain universal' in dv,'Chunk D vectors missing early-building universal case')
        need('steel structural gate live' in dv and 'concrete structural gate live' in dv,'Chunk D vectors missing structural-tier cases')
        need('powered vault gate live' in dv,'Chunk D vectors missing powered-access case')
        need('iron defensive gate live' in dv,'Chunk D vectors missing defense case')

# Chunk E exact decision matrix: furniture, storage, fixtures, and one additional source-safe appliance helper.
chunk_e_rows=rows(CHUNK_E) if os.path.exists(CHUNK_E) else []
if chunk_e_rows:
    need(len(chunk_e_rows)==33,f'Chunk E matrix expected 33 rows, got {len(chunk_e_rows)}')
    ce_count=Counter((r.get('policy') or '').strip().lower() for r in chunk_e_rows)
    need(ce_count==Counter({'gated':33}),f'Chunk E matrix policy counts wrong: {dict(ce_count)}')
    expected_e=crit['furniture_decor']|crit['fixtures_storage']|crit['secure_storage']|{'appliancesVariantHelper'}
    ce_ids={r.get('recipe') for r in chunk_e_rows}
    need(ce_ids==expected_e,f'Chunk E matrix recipe set mismatch: missing={sorted(expected_e-ce_ids)} extra={sorted(ce_ids-expected_e)}')
    for r in chunk_e_rows:
        rid=(r.get('recipe') or '').strip(); e=by_id.get(rid)
        need(e is not None,f'Chunk E {rid}: missing manifest entry')
        if e is None: continue
        need(e.get('decision_status')=='approved_chunk_e',f'Chunk E {rid}: decision_status not approved_chunk_e')
        need(e.get('policy')=='gated',f'Chunk E {rid}: policy must be gated')
        need(e.get('implementation')=='existing_capability',f'Chunk E {rid}: Capability not live')
        need((e.get('family') or '')==(r.get('family') or ''),f'Chunk E {rid}: family mismatch')
        need((e.get('primary_skill') or '')=='skill.construction',f'Chunk E {rid}: primary crafting Skill must be Construction')
        need(not (e.get('family') or '').startswith('farming'),f'Chunk E {rid}: furniture/storage/fixtures regressed to Farming')
        cid=(r.get('capability') or '').strip(); c=cap_by_id.get(cid)
        need(c is not None,f'Chunk E {rid}: missing Capability {cid}')
        if c is not None:
            need(c.get('target_id')==rid,f'Chunk E {rid}: Capability target mismatch')
            need(c.get('category')=='construction',f'Chunk E {rid}: Capability category must be construction')
            exp=[]
            for prefix in ('primary','secondary','tertiary'):
                sid=(r.get(prefix+'_skill') or '').strip(); mn=fnum(r.get(prefix+'_min')); rec=fnum(r.get(prefix+'_recommended'))
                if sid and mn is not None and rec is not None: exp.append((sid,mn,rec))
            need(cap_skills(c)==exp,f'Chunk E {rid}: Skill thresholds differ. expected={exp} got={cap_skills(c)}')
            kid=(r.get('knowledge') or '').strip(); gotk=cap_knowledge(c)
            if not args.require_chunk_g: need(gotk==([kid] if kid else []),f'Chunk E {rid}: Knowledge mismatch expected={kid!r} got={gotk}')
            rr=rk_by.get(rid); 
            if not args.require_chunk_g: need(rr is not None and rr.get('knowledge')==kid,f'Chunk E {rid}: recipe_knowledge missing {kid}')
            need(rr is not None and rr.get('category')=='construction',f'Chunk E {rid}: recipe_knowledge category must be construction')
    source_rows_e=rows(CHUNK_E_SOURCE)
    need(len(source_rows_e)==33,f'Chunk E source/material audit expected 33 rows, got {len(source_rows_e)}')
    need(all((r.get('target_recipe_verified') or '')=='YES' for r in source_rows_e),'Chunk E source audit contains an unverified target recipe ID')
    helper_rows_e=rows(CHUNK_E_HELPERS)
    need(len(helper_rows_e)==33,f'Chunk E helper audit expected 33 rows, got {len(helper_rows_e)}')
    need(all((r.get('missing_alternate_count') or '0')=='0' for r in helper_rows_e),'Chunk E retained helper audit has unresolved alternate members')
    need(all('No new block-specific recipe' in (r.get('source_boundary') or '') or 'gates the verified native helper recipe as a whole' in (r.get('source_boundary') or '') for r in helper_rows_e),'Chunk E helper audit source boundary is not explicit')
if args.require_chunk_e or args.require_chunk_f or args.require_chunk_g:
    need(bool(chunk_e_rows),'Chunk E matrix missing')
    need(man.get('runtime_enforcement') in ('active_chunk_e','active_chunk_f','active_chunk_g'),f'Chunk E+ manifest runtime_enforcement is {man.get("runtime_enforcement")!r}')
    if args.require_chunk_e and not args.require_chunk_f and not args.require_chunk_g:
        need(len(caps)==338,f'Chunk E expected 338 live recipe Capabilities, got {len(caps)}')
        need(len(rk)==294,f'Chunk E expected 294 recipe Knowledge mappings, got {len(rk)}')
        need(policy_count==Counter({'gated':513,'universal':125,'disabled':18}),f'Chunk E manifest policy counts wrong: {dict(policy_count)}')
        need(impl_count.get('existing_capability')==338 and impl_count.get('planned_capability')==175,f'Chunk E implementation counts wrong: {dict(impl_count)}')
    for rid in crit['furniture_decor']|crit['fixtures_storage']|crit['secure_storage']:
        e=by_id.get(rid); need(e is not None and e.get('implementation')=='existing_capability',f'Chunk E critical furniture/storage route not live: {rid}')
    app=by_id.get('appliancesVariantHelper')
    need(app is not None and app.get('policy')=='gated' and app.get('implementation')=='existing_capability', 'Chunk E appliance helper not promoted from universal to live gated Construction route')
    if os.path.isfile(RUNTIME_VECTORS):
        ev=read(RUNTIME_VECTORS)
        need(('Chunk E vectors' in ev) or ('Chunk F vectors' in ev) or ('Chunk G vectors' in ev),'Chunk E+ runtime vector harness header not updated')
        need('wood furniture gate live' in ev,'Chunk E vectors missing basic furniture case')
        need('upholstered furniture gate live' in ev,'Chunk E vectors missing upholstery case')
        need('secure storage gate live' in ev,'Chunk E vectors missing secure-storage case')
        need('powered decor gate live' in ev,'Chunk E vectors missing powered-decor case')
        need('appliance gate live' in ev,'Chunk E vectors missing appliance case')

# Chunk F exact decision matrix: closes every remaining planned Capability with either a live gate or an explicit UNIVERSAL decision.
chunk_f_rows=rows(CHUNK_F_MATRIX) if os.path.isfile(CHUNK_F_MATRIX) else []
if chunk_f_rows:
    need(len(chunk_f_rows)==175,f'Chunk F matrix expected 175 rows, got {len(chunk_f_rows)}')
    cf_count=Counter(r.get('policy') for r in chunk_f_rows)
    need(cf_count==Counter({'gated':166,'universal':9}),f'Chunk F matrix policy counts wrong: {dict(cf_count)}')
    basic_universal={'resourceCloth','medicalAloeCream','medicalBandage','medicalSplint','drinkJarBoiledWater','drinkJarEmpty','foodCharredMeat','foodCornMeal','foodEggBoiled'}
    got_universal={r.get('recipe') for r in chunk_f_rows if r.get('policy')=='universal'}
    need(got_universal==basic_universal,f'Chunk F UNIVERSAL set mismatch: missing={sorted(basic_universal-got_universal)} extra={sorted(got_universal-basic_universal)}')
    for r in chunk_f_rows:
        rid=r.get('recipe'); pol=r.get('policy'); e=by_id.get(rid)
        need(e is not None,f'Chunk F {rid}: missing manifest entry')
        if e is None: continue
        need(e.get('decision_status')=='approved_chunk_f',f'Chunk F {rid}: decision_status not approved_chunk_f')
        need(e.get('policy')==pol,f'Chunk F {rid}: manifest policy mismatch')
        # PC024 is a planned later Project Change that moves prepared drinks from Cooking
        # to Drink Preparation. The immutable Chunk F matrix remains the historical decision
        # record; current drink mechanics are validated by the PC024 validator instead.
        if rid.startswith('drink') and (e.get('primary_skill') or '')=='skill.drink_preparation':
            continue
        need((e.get('family') or '')==(r.get('family') or ''),f'Chunk F {rid}: family mismatch')
        need((e.get('primary_skill') or '')==(r.get('primary_skill') or ''),f'Chunk F {rid}: primary Skill mismatch')
        if pol=='gated':
            need(e.get('implementation')=='existing_capability',f'Chunk F {rid}: GATED recipe not live')
            cid=e.get('capability'); c=cap_by_id.get(cid)
            need(c is not None,f'Chunk F {rid}: missing Capability {cid}')
            if c is not None:
                need(c.get('target_id')==rid,f'Chunk F {rid}: Capability target mismatch')
                need(c.get('category')==(r.get('category') or ''),f'Chunk F {rid}: Capability category mismatch')
                exp=[]
                for prefix in ('primary','secondary','tertiary'):
                    sid=(r.get(prefix+'_skill') or '').strip(); mn=fnum(r.get(prefix+'_min')); rec=fnum(r.get(prefix+'_recommended'))
                    if sid and mn is not None and rec is not None: exp.append((sid,mn,rec))
                need(cap_skills(c)==exp,f'Chunk F {rid}: Skill thresholds differ. expected={exp} got={cap_skills(c)}')
                kid=(r.get('knowledge') or '').strip(); gotk=cap_knowledge(c)
                if not args.require_chunk_g: need(gotk==([kid] if kid else []),f'Chunk F {rid}: Knowledge mismatch expected={kid!r} got={gotk}')
                if kid and not args.require_chunk_g:
                    rr=rk_by.get(rid); need(rr is not None and rr.get('knowledge')==kid,f'Chunk F {rid}: recipe_knowledge missing {kid}')
        else:
            need(e.get('implementation')=='policy_declared' and not e.get('capability'),f'Chunk F {rid}: UNIVERSAL recipe must be capability-free')
    source_rows_f=rows(CHUNK_F_SOURCE)
    need(len(source_rows_f)==175,f'Chunk F source/material audit expected 175 rows, got {len(source_rows_f)}')
    need(all((r.get('target_recipe_verified') or '')=='YES' for r in source_rows_f),'Chunk F source audit contains an unverified target recipe ID')
    need(sum(1 for r in source_rows_f if (r.get('retained_recipe_node') or '')=='YES')==138,'Chunk F retained source count changed from audited 138')
    need(sum(1 for r in source_rows_f if (r.get('retained_recipe_node') or '')=='NO')==37,'Chunk F retained-source boundary count changed from audited 37')
if args.require_chunk_f or args.require_chunk_g:
    need(bool(chunk_f_rows),'Chunk F matrix missing')
    need(man.get('runtime_enforcement') in ('active_chunk_f','active_chunk_g'),f'Chunk F+ manifest runtime_enforcement is {man.get("runtime_enforcement")!r}')
    need(len(caps)==500,f'Chunk F expected 500 live gated recipe Capabilities after PC017 primitive-armor supersession, got {len(caps)}')
    need(len(rk)==(504 if args.require_chunk_g else 439),f'Chunk F/G recipe Knowledge mapping count unexpected: {len(rk)}')
    need(policy_count==Counter({'gated':504,'universal':134,'disabled':18}),f'Chunk F manifest policy counts wrong: {dict(policy_count)}')
    need(impl_count==Counter({'existing_capability':504,'policy_declared':134,'runtime_disabled':18}),f'Chunk F implementation counts wrong: {dict(impl_count)}')
    need(not planned,'Chunk F requires zero planned Capabilities')
    for rid in ('meleeToolSalvageT1Wrench','meleeToolSalvageT2Ratchet','meleeToolSalvageT3ImpactDriver','resourceRocketCasing','resourceRocketTip'):
        e=by_id.get(rid); need(e is not None and e.get('primary_skill')=='skill.metalworking',f'Chunk F fabrication-domain correction missing for {rid}')
    if os.path.isfile(RUNTIME_VECTORS):
        fv=read(RUNTIME_VECTORS)
        need(('Chunk F vectors' in fv) or ('Chunk G vectors' in fv),'Chunk F+ runtime vector harness header not updated')
        for token in ('all owned gated live','basic medicine universal','advanced armor live','salvage tool fabrication Metalworking','rocket casing fabrication Metalworking','vehicle component live','maintenance mod live','advanced explosive live'):
            need(token in fv,f'Chunk F runtime vectors missing {token!r}')


# Chunk G grouped Knowledge/literature/acquisition/migration/XP integration.
if args.require_chunk_g:
    need(man.get('runtime_enforcement')=='active_chunk_g',f'Chunk G manifest runtime_enforcement is {man.get("runtime_enforcement")!r}')
    need(len(caps)==500,f'Chunk G expected 500 live gated recipe Capabilities after PC017 primitive-armor supersession, got {len(caps)}')
    need(len(rk)==504,f'Chunk G expected exactly one recipe Knowledge mapping per GATED recipe, got {len(rk)}')
    gated_ids={e.get('id') for e in entries if e.get('policy')=='gated'}
    need(set(rk_by)==gated_ids,f'Chunk G recipe Knowledge coverage mismatch: missing={sorted(gated_ids-set(rk_by))[:12]} extra={sorted(set(rk_by)-gated_ids)[:12]}')
    coarse={'knowledge.tailoring.basic','knowledge.farming.practical','knowledge.maintenance.general','knowledge.electrical.fundamentals','knowledge.construction.basic'}
    coarse_users=[rid for rid,r in rk_by.items() if r.get('knowledge') in coarse]
    need(not coarse_users,f'Chunk G temporary coarse Knowledge bridges remain on recipes: {coarse_users[:20]}')
    for rid in sorted(gated_ids):
        rr=rk_by.get(rid); c=cap_by_target.get(rid); e=by_id.get(rid)
        kid=rr.get('knowledge') if rr is not None else ''
        need(bool(kid),f'Chunk G {rid}: empty recipe Knowledge')
        need(kid in knowledge,f'Chunk G {rid}: unknown Knowledge {kid}')
        need(c is not None,f'Chunk G {rid}: missing Capability')
        if c is not None: need(cap_knowledge(c)==[kid],f'Chunk G {rid}: Capability Knowledge {cap_knowledge(c)} != recipe_knowledge {kid}')
        if e is not None: need((e.get('knowledge') or '')==kid,f'Chunk G {rid}: manifest Knowledge {(e.get("knowledge") or "")} != {kid}')

    litroot=parse(LITERATURE)
    discoveries=[x for x in litroot.findall('./item') if x.get('kind')=='discovery']
    lit_by_knowledge=defaultdict(list)
    for x in discoveries:
        if x.get('knowledge'): lit_by_knowledge[x.get('knowledge')].append(x.get('id'))
    need(len(discoveries)==153,f'Chunk G expected 153 discovery literature entries, got {len(discoveries)}')
    gmatrix=rows(CHUNK_G_MATRIX)
    need(len(gmatrix)==50,f'Chunk G literature matrix expected 50 rows, got {len(gmatrix)}')
    itemsroot=parse(ITEMS); item_ids={x.get('name') for x in itemsroot.findall('.//item') if x.get('name')}
    loottext=read(LOOT); tradertext=read(TRADERS)
    iconroot=os.path.join(ROOT,'UIAtlases','ItemIconAtlas')
    for row in gmatrix:
        kid=(row.get('knowledge') or '').strip(); iid=(row.get('literature_item') or '').strip(); icon=(row.get('icon') or '').strip(); tier=(row.get('tier') or '').strip()
        need(kid in knowledge,f'Chunk G matrix unknown Knowledge {kid}')
        need(lit_by_knowledge.get(kid)==[iid],f'Chunk G {kid}: expected exactly literature {iid}, got {lit_by_knowledge.get(kid)}')
        need(iid in item_ids,f'Chunk G literature item missing from items.xml: {iid}')
        need(iid in loottext,f'Chunk G literature item missing from loot distribution: {iid}')
        need(iid in tradertext,f'Chunk G literature item missing from trader distribution: {iid}')
        need(os.path.isfile(os.path.join(iconroot,icon+'.png')),f'Chunk G literature icon missing: {icon}.png')
        need(tier in ('standard','advanced'),f'Chunk G literature tier invalid for {kid}: {tier}')

    ldist=parse(LIT_DIST)
    tiers={x.get('id'):x for x in ldist.findall('./tier')}
    need(tiers.get('discovery.standard') is not None and tiers['discovery.standard'].get('count')=='103','Chunk G standard discovery count must be 103')
    need(tiers.get('discovery.advanced') is not None and tiers['discovery.advanced'].get('count')=='50','Chunk G advanced discovery count must be 50')

    # Exact grouped-family smoke checks.
    expected_groups={
        'bed02BlockVariantHelper':'procedure.construction.upholstery',
        'cntGunSafeVariantHelper':'procedure.construction.secure_storage',
        'armorAssassinBoots':'pattern.tailoring.assassin_armor',
        'cntApiary':'procedure.farming.apiary',
        'modMeleeClubBarbedWire':'schematic.maintenance.club_mods',
        'ammoArrowExploding':'formula.explosives.projectile_payloads',
        'gunBowT3CompoundBow':'schematic.archery.crossbows_compound',
        'meleeToolAxeT3Chainsaw':'schematic.metalworking.power_tools',
        'ammo9mmBulletBall':'schematic.gunsmithing.standard_ammo',
        'meleeToolWireTool':'procedure.electrical.service_tools',
        'ammoGasCan':'formula.chemistry.fuel_blending',
        'ammoJunkTurretRegular':'schematic.turrets.ammunition',
        'glassBlockVariantHelper':'procedure.construction.glazing',
        'electricfencepost':'recipe.electricfencepost',
        'batterybank':'procedure.electrical.battery_service',
    }
    for rid,kid in expected_groups.items():
        need(rk_by.get(rid) is not None and rk_by[rid].get('knowledge')==kid,f'Chunk G grouped mapping wrong: {rid} -> {rk_by.get(rid).get("knowledge") if rk_by.get(rid) is not None else None}, expected {kid}')

    mig=read(MIGRATION); model=read(WORLD_MODEL); craftg=read(CRAFT_SKILL); runtimeg=read(RUNTIME_CONFIG); skillcfg=read(SKILL_SOURCES); capg=read(CAP_SERVICE)
    need('CurrentSchemaVersion = 8' in model,'Chunk G world-character schema was not advanced to 8')
    need('Register(7, Migrate7To8);' in mig and 'world-character-v7-to-v8-crafting-knowledge-grandfather-v1' in mig,'Chunk G v7->v8 grandfather migration missing')
    need('craftKnowledgeGrandfather=' in mig,'Chunk G migration audit summary missing')
    for token in ('MaintenanceCraftPerOutput','MechanicsCraftPerOutput','FarmingCraftPerOutput','ExplosivesCraftPerOutput','TurretsCraftPerOutput','MedicineCraftPerOutput'):
        need(token in runtimeg and token in craftg,f'Chunk G craft-XP primary Skill contract missing {token}')
    for token in ('maintenance_craft_per_output','mechanics_craft_per_output','farming_craft_per_output','explosives_craft_per_output','turrets_craft_per_output','medicine_craft_per_output'):
        need(token in skillcfg,f'Chunk G skill_sources missing {token}')
    for key in ('xuiRebirthMissingKnowledgeFormat','xuiRebirthSkillRequiredFormat','xuiRebirthSkillRecommendedFormat','xuiRebirthCraftDisabledByProgression'):
        need(key in read(LOCALIZATION),f'Chunk G lock-text localization missing {key}')
        need(key in capg,f'Chunk G Capability service does not consume localized lock text {key}')
    gv=read(RUNTIME_VECTORS)
    need('Chunk G vectors' in gv,'Chunk G runtime vector harness header not advanced')
    for token in ('stun baton grouped Knowledge','upholstery grouped Knowledge','secure storage grouped Knowledge','armor grouped Knowledge','explosive projectile grouped Knowledge'):
        need(token in gv,f'Chunk G runtime vectors missing grouped-Knowledge case {token!r}')
    notes.append('Chunk G Knowledge/literature: 504/504 GATED mapped; 50 grouped discoveries; schema 8 grandfather migration; bounded primary-only craft XP')

# Planned-capability IDs must be deterministic and noncolliding, even before later chunks implement them.
cap_refs=[e.get('capability') for e in entries if e.get('capability')]
need(len(cap_refs)==len(set(cap_refs)),'duplicate Capability IDs/reservations in crafting manifest')
for e in entries:
    if e.get('implementation')=='planned_capability':
        expected='capability.recipe.'+re.sub(r'[^a-z0-9_]+','_',e.get('id').lower()).strip('_')
        need(e.get('capability')==expected,f'{e.get("id")}: planned Capability reservation is not deterministic: {e.get("capability")} != {expected}')

# Chunk B runtime integration. Default validation remains phase-aware; --require-runtime enforces the live contract.
runtime_state=man.get('runtime_enforcement') or ''
need(runtime_state in ('pending_chunk_b','active_chunk_b','active_chunk_c','active_chunk_d','active_chunk_e','active_chunk_f','active_chunk_g'),f'unknown runtime_enforcement state: {runtime_state}')
if args.require_runtime:
    need(runtime_state in ('active_chunk_b','active_chunk_c','active_chunk_d','active_chunk_e','active_chunk_f','active_chunk_g'),'Chunk B+ requires an active runtime_enforcement state')
    for path,label in ((RUNTIME,'runtime registry'),(RUNTIME_DEFS,'runtime definitions'),(RUNTIME_VECTORS,'runtime vectors'),
                       (CAP_SERVICE,'Capability service'),(PERSONAL,'personal craft authority'),(WORKSTATION,'workstation craft authority'),(RUNTIME_CONFIG,'runtime config loader'),
                       (CRAFT_SKILL,'craft Skill service'),(GRAPH,'Explorer graph'),(DEBUG,'debug command'),(INSTALLER,'progression installer')):
        need(os.path.isfile(path),f'Chunk B {label} missing: {os.path.relpath(path,ROOT)}')

    reg=read(RUNTIME); defs=read(RUNTIME_DEFS); capsvc=read(CAP_SERVICE); personal=read(PERSONAL)
    workstation=read(WORKSTATION); runtimecfg=read(RUNTIME_CONFIG); craft=read(CRAFT_SKILL); graph=read(GRAPH); debug=read(DEBUG); vectors=read(RUNTIME_VECTORS); installer=read(INSTALLER)
    need('crafting_progression.xml' in reg,'runtime registry does not reference crafting_progression.xml')
    need('RebirthSurvivorMode.IsEnabledForCurrentWorld()' in reg,'runtime crafting policy missing Rebirth-only mode gate')
    need('RebirthCraftingProgressionPolicies.Gated' in reg and 'RebirthCraftingProgressionPolicies.Universal' in reg and 'RebirthCraftingProgressionPolicies.Disabled' in reg,'runtime registry does not validate all owned policy kinds')
    need('RequiresServerAuthorization' in defs and 'Gated' in defs and 'Disabled' in defs,'runtime policy definitions missing authorization contract')
    need('RebirthCraftingProgressionRegistry.TryGetRecipe' in capsvc,'Capability recipe evaluation is not policy-owned')
    need('policy.IsUniversal' in capsvc and 'policy.IsDisabled' in capsvc,'Capability recipe evaluation missing UNIVERSAL/DISABLED handling')
    need('RebirthCapabilityRegistry.TryGet(policy.CapabilityId' in capsvc,'GATED policy does not evaluate the declared Capability ID')
    need('Progression requirements pending implementation' in capsvc,'missing planned Capability does not fail closed with a controlled reason')
    need('RequiresServerAuthorization(recipeName)' in personal,'personal craft admission still depends on direct Capability existence instead of policy')
    need('RebirthCapabilityService.EvaluateRecipe(player, recipeName)' in personal,'personal server authority does not use unified policy evaluator')
    need('RebirthCapabilityService.EvaluateRecipe(player,recipeName)' in workstation and 'crafting-policy:' in workstation,'workstation queue authority does not use unified crafting policy evaluator')
    need('RebirthCraftingProgressionRegistry.Load(root)' in runtimecfg,'runtime config does not load crafting policy after Capability authority')
    need('TryGetPrimarySkill(recipeName,out explicitSkill)' in craft,'craft-time/LBD classifier does not prefer explicit primary crafting Skill')
    need('RebirthSurvivorMode.IsEnabledForCurrentWorld()' in craft,'craft-time adapter lacks explicit Rebirth-only guard')
    pos_explicit=craft.find('TryGetPrimarySkill(recipeName,out explicitSkill)'); pos_output=craft.find('ItemClass output=')
    need(pos_explicit>=0 and pos_output>=0 and pos_explicit<pos_output,'heuristic craft classification runs before explicit manifest ownership')
    need('AddCraftingProgressionPolicyNodesAndEdges' in graph and 'primary crafting Skill / fabrication owner' in graph,'Explorer graph does not expose explicit crafting ownership')
    need('GetCraftAward(policy.PrimarySkillId,1)>0f' in graph,'Explorer graph does not distinguish ownership from actual craft-LBD routing')
    need('craftpolicy' in debug and 'RebirthCraftingProgressionVectorHarness.RunAll()' in debug,'read-only crafting policy debug/vector command missing')
    need('manifest count' in vectors and 'unknown external not intercepted' in vectors,'Chunk B runtime vector coverage incomplete')
    need('RebirthProgressionRuntimeConfig.Load()' in installer,'progression installer does not invoke runtime config/policy loading')
    notes.append('Chunk B runtime policy authority: present')
else:
    warnings.append('Runtime checks not required for this invocation; use --require-runtime for the Chunk B+ gate.')

# Parse every Config XML to catch collateral damage.
xml_total=0
for dp,_,fs in os.walk(os.path.join(ROOT,'Config')):
    for fn in fs:
        if fn.lower().endswith('.xml'):
            xml_total+=1
            p=os.path.join(dp,fn)
            try: ET.parse(p)
            except Exception as e: fail(f'Config XML parse failed {os.path.relpath(p,ROOT)}: {e}')
notes.append(f'Config XML parsed: {xml_total}')
notes.append('policy counts: '+', '.join(f'{k}={v}' for k,v in sorted(policy_count.items())))
notes.append('implementation states: '+', '.join(f'{k}={v}' for k,v in sorted(impl_count.items())))
notes.append(f'planned Capabilities reserved for later chunks: {len(planned)}')
notes.append(f'critical recipe ledger entries: {sum(len(v) for v in crit.values())}')
notes.append(f'furniture/helper audit entries: {len(furn_rows)}')
if planned and not args.require_complete: warnings.append(f'{len(planned)} GATED recipes still reserve planned Capability IDs for a later implementation phase.')

print('REBIRTH 3.0 CRAFTING PROGRESSION COVERAGE STATIC VALIDATION')
print('project_root:',ROOT)
for n in notes: print('-',n)
for w in warnings: print('WARNING:',w)
if errors:
    print('RESULT: FAIL')
    for e in errors: print('ERROR:',e)
    sys.exit(1)
print('RESULT: PASS')
