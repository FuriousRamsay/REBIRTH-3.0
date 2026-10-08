#!/usr/bin/env python3
from pathlib import Path
import csv, re, sys, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]
checks=[]
def check(name,cond,detail=''):
    checks.append((name,bool(cond),detail))
def text(rel): return (ROOT/rel).read_text(encoding='utf-8')
def parse(rel): return ET.parse(ROOT/rel).getroot()

bonus=parse('Config/_Survivor/background_bonuses.xml')
node=next((x for x in bonus.findall('bonus') if x.get('id')=='background_bonus.nothing_is_junk'),None)
check('Nothing Is Junk bonus exists',node is not None)
if node is not None:
    check('Scavenger owner',node.get('background_id')=='background.scavenger')
    check('salvage profile handler',node.get('handler')=='salvage_profile')
    check('authored profile id',node.get('profile')=='authored_salvage_profiles')
    tune={x.get('key'):x for x in node.findall('tuning')}
    for k in ('ordinary_output_multiplier','valuable_chance_multiplier'):
        check('tuning '+k,k in tune)
        check(k+' unlocked',k in tune and tune[k].get('locked')=='false')

profiles=parse('Config/_Survivor/salvage_profiles.xml')
check('salvage profile root',profiles.tag=='rebirth_salvage_profiles')
check('schema version 1',profiles.get('schema_version')=='1')
for a in ('max_ordinary_extra_per_outcome','max_valuable_extra_per_damage','max_total_extra_per_damage','max_single_base_count','max_valuable_bonus_chance'):
    check('root cap '+a,profiles.get(a) is not None)
expected=['vehicle','industrial_machinery','appliance','electronics','plumbing','security_storage','furniture']
ps={p.get('id'):p for p in profiles.findall('profile')}
check('seven authored profiles',set(ps)==set(expected),str(sorted(ps)))
for pid in expected:
    p=ps.get(pid)
    check(pid+' exists',p is not None)
    if p is None: continue
    check(pid+' priority',p.get('priority') is not None)
    check(pid+' matchers',len(p.findall('match'))>0)
    check(pid+' ordinary outputs',len(p.findall('ordinary'))>0)
    check(pid+' valuable outputs',len(p.findall('valuable'))>0)
    for m in p.findall('match'):
        check(pid+' matcher non-catchall '+str(m.get('value')),bool((m.get('value') or '').strip()) and m.get('value')!='*')
    items=[(x.get('item') or '').lower() for x in list(p.findall('ordinary'))+list(p.findall('valuable'))]
    check(pid+' excludes currency',not any(any(k in x for k in ('casino','coin','cash','money','duke')) for x in items))

svc=text('Scripts/Survivor/Backgrounds/RebirthScavengerSalvageProfileService.cs')
net=text('Scripts/Survivor/Backgrounds/RebirthScavengerSalvageProfileNetPackages.cs')
installer=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
debug=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
prog=text('Config/_Survivor/progression.xml')
for token in [
    'RebirthSurvivorDefinitionLoader.ResolveConfigRoot()', 'salvage_profiles.xml', 'GameUtils', 'collectHarvestedItem',
    'HarvestPrefix', 'HarvestPostfix', 'delta<=0', 'MatchProfile(requestedBlockName)', 'IsAuthoredNativeSalvageDrop',
    'ClassifyHarvestTool', 'skill.salvage', 'Constants.cDigAndBuildDistance', 'OrdinaryReplayDamage', 'ValuableReplayDamage',
    'TotalExtraAtDamage', 'maxOrdinaryExtraPerOutcome', 'maxValuableExtraPerDamage', 'maxTotalExtraPerDamage',
    'profile.Ordinary.Contains(baseItemName)', 'profile.FindValuable', 'GetAuthoredSalvageDrops', 'd.tag', 'd.toolCategory',
    'salvageHarvest', 'Disassemble', 'maxValuableBonusChance', 'ordinary_output_multiplier', 'valuable_chance_multiplier',
    'UNPROFILED' if False else 'native-fallback', 'fullInventoryNativeOutcome=conservative'
]: check('service token '+token,token in svc)
check('no player need inference symbol','Needs' not in svc and 'needScore' not in svc and 'wantedItem' not in svc)
check('ordinary only after native positive delta',svc.find('delta<=0') < svc.find('ProcessSalvageRequest'))
check('server bonus ownership check','RebirthBackgroundBonusService.HasBonus(player,BonusId)' in svc)
check('same live block server check','string.Equals(currentName,requestedBlockName' in svc)
check('remote request uses ToServer','PackageDirection=>NetPackageDirection.ToServer' in net)
check('request validates sender','ValidEntityIdForSender(playerId)' in net)
check('grant is ToClient','PackageDirection=>NetPackageDirection.ToClient' in net)
check('grant has inventory-full drop fallback','ItemDropServer' in svc)
check('profile loader sorts priority','Profiles.Sort' in svc and 'Priority' in svc)
check('unknown match returns null','return null;' in svc[svc.find('public static RebirthSalvageProfileDefinition MatchProfile'):svc.find('public static List<RebirthAuthoredSalvageDrop>')])
check('valuable requires profile and authored', 'profile.FindValuable(authored[i].ItemName)' in svc)
check('valuable once per damage', 'ClaimDamage(ValuableReplayDamage' in svc)
check('ordinary once per item damage','ClaimDamage(OrdinaryReplayDamage' in svc)
check('world shutdown clears runtime','WorldShuttingDown.RegisterHandler' in svc and 'OrdinaryReplayDamage.Clear()' in svc)
check('installer installs Chunk N','RebirthScavengerSalvageProfileService.Install(HarmonyInstance)' in installer)
check('installer report includes Chunk N','chunkNSalvage' in installer)
check('debug help','rbsurvivor chunknbonus' in debug)
check('debug alias Nothing Is Junk','nothingisjunk' in debug)
check('debug alias scavenger salvage','scavengersalvage' in debug)
check('debug vectors','RebirthScavengerSalvageProfileService.RunVectors()' in debug)
check('debug block profile','BuildDebugReport(blockPlayer,pos)' in debug)
check('progression salvage updated','Chunk N:' in prog and 'authored Scavenger salvage-profile bonus' in prog)
check('tool wear remains source audit','tool-wear remains source-audit' in prog)

cat=ROOT/'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC027_CHUNK_N_CATALOGUE_20260902.csv'
check('catalogue exists',cat.exists())
if cat.exists():
    with cat.open(encoding='utf-8-sig',newline='') as f: rows=list(csv.DictReader(f))
    check('catalogue has audited salvage rows',len(rows)>=400,str(len(rows)))
    check('catalogue contains fallback rows',any(r['classification_status']=='NATIVE_FALLBACK' for r in rows))
    check('catalogue contains all profiles',set(expected).issubset({r['matched_profile'] for r in rows}))
    check('catalogue no currency profile output',not any(any(k in (r['ordinary_profile_outputs']+';'+r['valuable_profile_candidates']).lower() for k in ('casino','coin','cash','money','duke')) for r in rows))

for rel in [
'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC027_CHUNK_N_SOURCE_AUDIT_20260902.md',
'_Documentation/ProjectChanges/REBIRTH_3_0_PROJECT_CHANGE_BACKGROUND_SIGNATURE_BONUSES_PC027_CHUNK_N_SCAVENGER_SALVAGE_PROFILES_20260902.md']:
    check('doc '+Path(rel).name,(ROOT/rel).exists())

xmls=list(ROOT.rglob('*.xml')); bad=[]
for x in xmls:
    try: ET.parse(x)
    except Exception as e: bad.append((str(x),str(e)))
check('XML count 129',len(xmls)==129,str(len(xmls)))
check('all XML parse',not bad,str(bad[:3]))

passed=sum(1 for _,ok,_ in checks if ok); failed=len(checks)-passed
for name,ok,detail in checks:
    print(('PASS' if ok else 'FAIL')+' | '+name+((' | '+detail) if detail else ''))
print(f'PC027_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={len(xmls)}')
sys.exit(1 if failed else 0)
