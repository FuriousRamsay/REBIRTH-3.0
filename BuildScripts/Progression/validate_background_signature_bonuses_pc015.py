from pathlib import Path
import csv, hashlib, re, sys, xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[2]
checks=[]
def ck(name,cond,detail=''): checks.append((name,bool(cond),detail))
def text(rel): return (R/rel).read_text(encoding='utf-8')
# Core data authority.
bp=R/'Config/_Survivor/background_bonuses.xml'; root=ET.parse(bp).getroot(); entries=root.findall('bonus')
ck('bonus schema version 1',root.get('schema_version')=='1',root.get('schema_version',''))
ck('clean slate authority frozen',root.get('clean_slate_background_id')=='background.clean_slate',root.get('clean_slate_background_id',''))
ck('Chunk C icon asset contract explicit',root.get('icon_asset_contract')=='pending_chunk_c',root.get('icon_asset_contract',''))
ck('27 bonus definitions',len(entries)==27,str(len(entries)))
attrs=['id','background_id','name_key','description_key','icon_key','icon_state','category','handler','profile']
for a in attrs: ck('all bonus '+a+' populated',all((e.get(a) or '').strip() for e in entries))
ids=[e.get('id') for e in entries]; bgs=[e.get('background_id') for e in entries]; icons=[e.get('icon_key') for e in entries]
ck('bonus IDs unique',len(ids)==len(set(x.lower() for x in ids)))
ck('bonus IDs stable prefix',all(x.startswith('background_bonus.') for x in ids))
ck('background bindings unique',len(bgs)==len(set(x.lower() for x in bgs)))
ck('bonus icon keys unique',len(icons)==len(set(x.lower() for x in icons)))
ck('bonus icon keys stable prefix',all(x.startswith('rb_bonus_') for x in icons))
ck('all icon assets intentionally pending Chunk C',all(e.get('icon_state')=='pending_chunk_c' for e in entries))
# Current Background authority and exact coverage.
bgroot=ET.parse(R/'Config/_Survivor/backgrounds.xml').getroot(); current=[e.get('id') for e in bgroot.findall('background')]
ck('28 current backgrounds',len(current)==28,str(len(current)))
ck('Clean Slate has no bonus','background.clean_slate' not in bgs)
ck('every non-Clean-Slate background bound exactly once',set(x.lower() for x in bgs)==set(x.lower() for x in current if x!='background.clean_slate'),str(sorted(set(current)-set(bgs)-{'background.clean_slate'})))
ck('Metal Worker stable ID retained','background.welder_fabricator' in bgs)
ck('Park Ranger stable ID retained','background.park_ranger_outdoor_guide' in bgs)
for old in ['background.doctor','background.nurse','background.security_guard','background.truck_driver','background.warehouse_worker']:
    ck('removed Background not rebound '+old,old not in bgs)
# Locked design values that are explicit in approved player-facing/design contracts.
byid={e.get('id'):e for e in entries}
def tune(bid,key):
    e=byid.get(bid)
    if e is None:return None
    for t in e.findall('tuning'):
        if t.get('key')==key:return (t.get('value'),t.get('locked'))
    return None
locked={
('background_bonus.trauma_specialist','treated_healing_multiplier'):('2','true'),
('background_bonus.bookworm','literature_content_multiplier'):('2','true'),
('background_bonus.built_to_last','max_durability_multiplier'):('1.15','true'),
('background_bonus.heat_treatment','max_durability_multiplier'):('1.15','true'),
('background_bonus.nothing_is_disposable','repair_permanent_loss_multiplier'):('0.5','true'),
('background_bonus.engineered_reliability','max_durability_multiplier'):('1.5','true'),
('background_bonus.combat_momentum','max_stacks'):('3','true'),
('background_bonus.patrol_car_familiarity','ammunition_multiplier'):('3','true'),
('background_bonus.master_mixologist','special_effect_duration_multiplier'):('2','true'),
('background_bonus.more_options','extra_reward_choices'):('1','true'),
}
for (bid,key),expected in locked.items(): ck('locked tuning '+bid+'/'+key,tune(bid,key)==expected,str(tune(bid,key)))
# Localization authority: new keys must exist exactly once.
rows=list(csv.reader((R/'Config/Localization.csv').open(encoding='utf-8-sig',newline='')))
keys=[r[0].strip() for r in rows[1:] if r and r[0].strip()]; counts={k:keys.count(k) for k in set(keys)}
loc_needed=[]
for e in entries: loc_needed.extend([e.get('name_key'),e.get('description_key')])
ck('54 bonus localization keys declared',len(loc_needed)==54 and len(set(loc_needed))==54,str(len(set(loc_needed))))
ck('all bonus localization keys exist exactly once',all(counts.get(k,0)==1 for k in loc_needed),str([k for k in loc_needed if counts.get(k,0)!=1][:8]))
# Icon routing: keys are reserved now, real atlas entries intentionally arrive in Chunk C.
settings=ET.parse(R/'UIAtlases/RebirthSurvivorIcons/settings.xml').getroot(); registered={e.get('name') for e in settings.iter('sprite') if e.get('name')}
pngs={p.stem for p in (R/'UIAtlases/RebirthSurvivorIcons').glob('*.png')}
ck('pending bonus icon keys do not collide with registered sprites',not(set(icons)&registered),str(sorted(set(icons)&registered)))
ck('pending bonus icon keys do not collide with current PNGs',not(set(icons)&pngs),str(sorted(set(icons)&pngs)))
ck('all 27 real icon assets still pending Chunk C',len(set(icons)-registered-pngs)==27,str(len(set(icons)-registered-pngs)))
# Registry/service source contract.
reg=text('Scripts/Survivor/Backgrounds/RebirthBackgroundBonusRegistry.cs'); svc=text('Scripts/Survivor/Backgrounds/RebirthBackgroundBonusService.cs'); inst=text('Scripts/Survivor/RebirthSurvivorInstaller.cs')
ck('registry validates current Background authority','RebirthSurvivorDefinitionRegistry.TryGetBackground' in reg and 'ValidateCoverage' in reg)
ck('registry enforces Clean Slate no bonus','Clean Slate cannot bind a Signature Bonus' in reg and 'Clean Slate must not have a Signature Bonus' in reg)
ck('registry validates duplicate IDs and Background bindings','Duplicate Signature Bonus ID' in reg and 'more than one Signature Bonus' in reg)
ck('registry stores handler/profile as data','public string Handler' in text('Scripts/Survivor/Backgrounds/RebirthBackgroundBonusDefinition.cs') and 'public string Profile' in text('Scripts/Survivor/Backgrounds/RebirthBackgroundBonusDefinition.cs'))
ck('service API exposes EntityPlayer query','GetSignatureBonus(EntityPlayer player)' in svc)
ck('service API exposes reusable profile query','GetSignatureBonus(RebirthSurvivorProfile profile)' in svc)
ck('service derives world ownership from immutable origin','record.Origin.BackgroundId' in svc)
ck('service derives profile ownership from existing BackgroundId','profile.BackgroundId' in svc)
ck('service derives client ownership from owner snapshot BackgroundId','owner.BackgroundId' in svc and 'owner.RebirthModeEnabled' in svc)
ck('service Base Game world gate','!RebirthSurvivorMode.IsEnabledForCurrentWorld()' in svc)
ck('service Base Game profile gate','RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth' in svc)
ck('no separate bonus entitlement persisted','SignatureBonusId' not in text('Scripts/Survivor/Persistence/RebirthSurvivorProfileModels.cs') and 'SignatureBonusId' not in text('Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs'))
ck('no separate bonus entitlement network field','SignatureBonusId' not in text('Scripts/Survivor/Network/RebirthSurvivorNetworkModels.cs'))
ck('installer installs registry from authoritative config root','RebirthBackgroundBonusRegistry.Install(bundle.ConfigRoot)' in inst)
# Export is data-derived and exact.
cat=R/'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC015_CHUNK_B_CATALOGUE_20260902.csv'
cr=list(csv.DictReader(cat.open(encoding='utf-8',newline='')))
ck('catalogue export has 27 rows',len(cr)==27,str(len(cr)))
ck('catalogue export bonus IDs match XML',{r['bonus_id'] for r in cr}==set(ids))
ck('catalogue export background bindings match XML',{r['background_id'] for r in cr}==set(bgs))
ck('export script reads authoritative XML',"Config/_Survivor/background_bonuses.xml" in text('BuildScripts/Progression/export_background_signature_bonuses.py'))
# No bonus behavior is enabled by Chunk B. Handler/profile tokens are opaque and no effect services consume them yet.
consumer_hits=[]
for p in (R/'Scripts').rglob('*.cs'):
    if p.as_posix().endswith('RebirthBackgroundBonusRegistry.cs') or p.as_posix().endswith('RebirthBackgroundBonusService.cs') or p.as_posix().endswith('RebirthBackgroundBonusDefinition.cs'): continue
    s=p.read_text(encoding='utf-8',errors='ignore')
    if 'background_bonus.' in s or 'RebirthBackgroundBonusService.HasBonus' in s: consumer_hits.append(p.relative_to(R).as_posix())
ck('Chunk B introduces no gameplay bonus consumers',not consumer_hits,str(consumer_hits[:8]))
# XML parse/static integrity.
xmls=list(R.rglob('*.xml')); bad=[]
for p in xmls:
    try: ET.parse(p)
    except Exception as e: bad.append((p.relative_to(R).as_posix(),str(e)))
ck('all project XML parses',not bad,str(bad[:3]))
# New localization keys specifically duplicate-free even though inherited file may contain unrelated legacy formatting.
ck('new bonus localization keys have zero duplicates',all(counts.get(k,0)==1 for k in loc_needed))
failed=[x for x in checks if not x[1]]
for n,v,d in checks: print(('PASS ' if v else 'FAIL ')+n+((' :: '+d) if d and not v else ''))
print(f'PC015_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)} BONUSES={len(entries)} BACKGROUNDS={len(current)} XML={len(xmls)}')
sys.exit(1 if failed else 0)
