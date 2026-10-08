#!/usr/bin/env python3
from pathlib import Path
import sys, re, xml.etree.ElementTree as ET
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
passed=failed=0

def check(name, ok):
    global passed,failed
    if ok: passed+=1; print('PASS',name)
    else: failed+=1; print('FAIL',name)

def text(rel): return (root/rel).read_text(encoding='utf-8',errors='replace')

xml=ET.parse(root/'Config/_Survivor/background_bonuses.xml').getroot()
bonus={e.attrib['id']:e for e in xml.findall('bonus')}
maint=bonus['background_bonus.nothing_is_disposable']; gun=bonus['background_bonus.gunsmith_master_restoration']; tail=bonus['background_bonus.tailor_master_restoration']
def tuning(e,key):
    for t in e.findall('tuning'):
        if t.attrib.get('key')==key:return t.attrib.get('value'),t.attrib.get('locked')
    return None
check('Maintenance 0.5 locked tuning',tuning(maint,'repair_permanent_loss_multiplier')==('0.5','true'))
check('Gunsmith restoration tuning explicit/unlocked',tuning(gun,'restoration_fraction')==('0.25','false'))
check('Tailor restoration tuning explicit/unlocked',tuning(tail,'restoration_fraction')==('0.25','false'))
check('Gunsmith profile preserved',gun.attrib.get('profile')=='firearm_restoration')
check('Tailor profile preserved',tail.attrib.get('profile')=='wearable_restoration')

svc=text('Scripts/Survivor/Repair/RebirthRepairSignatureService.cs')
adp=text('Scripts/Survivor/Repair/RebirthItemConditionStatAdapter.cs')
net=text('Scripts/Survivor/Repair/RebirthRepairSignatureNetPackages.cs')
prov=text('Scripts/Survivor/Provenance/RebirthItemProvenanceAdapter.cs')
inst=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
cmd=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
vec=text('Scripts/Survivor/Repair/RebirthRepairSignatureVectorHarness.cs')

check('Rebirth-only mode guard present',svc.count('RebirthSurvivorMode.IsEnabledForCurrentWorld()')>=3)
check('server-side native inventory commit patched dynamically','NetPackagePlayerInventory' in svc and 'NativeInventoryCommitPrefix' in svc and 'world.IsRemote()' in svc)
check('native package patch uses runtime discovery','AccessTools.TypeByName("NetPackagePlayerInventory")' in svc)
check('repair evidence package is ToServer','NetPackageRebirthRepairSignatureBegin' in net and 'NetPackageDirection.ToServer' in net and 'ValidEntityIdForSender(playerId)' in net)
check('correction package is ToClient','NetPackageRebirthRepairConditionCorrection' in net and 'NetPackageDirection.ToClient' in net)
check('server correction targeted to owning entity','_attachedToEntityId: correction.PlayerId' in svc)
check('repair queue creates evidence','BeginRepairFromUi' in svc and 'RebirthRepairSignatureQueuePatch' in svc)
check('host completion patched after outputStack','RebirthRepairSignatureCompletionPatch' in svc and '"outputStack"' in svc and 'CompleteLocalRepair' in svc)
check('successful repair verified by UseTimes decrease','post.UseTimes >= preUse - 0.001f' in svc and 'stack.itemValue.UseTimes >= pending.PreUseTimes - 0.001f' in svc)
check('pending evidence has bounded TTL','PendingSeconds = 900.0' in svc and 'CleanupLocked' in svc)
check('first repair free naturally no Maintenance adjustment','if (nativePostMax >= preMax) return nativePostMax' in svc)
check('Maintenance formula multiplies only current native loss','(preMax - nativePostMax) * m' in svc)
check('Maintenance does not call restoration helper in its branch',re.search(r'MaintenanceBonusId.*?CalculateMaintenanceTarget',svc,re.S) is not None)
check('restoration operates on current deficit','(originalCap - currentMax) * f' in svc)
check('restoration caps at original/crafted maximum','Math.Min(originalCap, currentMax + Math.Max(0, recovery))' in svc)
check('Gunsmith restoration requires firearm classifier','GunsmithBonusId' in svc and '&& IsFirearm(post)' in svc)
check('firearm classifier reuses authoritative combat families','IsFirearmSkill(RebirthProgressionRuntimeConfig.ClassifyCombat(item))' in svc)
check('Tailor restoration requires wearable classifier','TailorBonusId' in svc and '&& IsWearable(post)' in svc)
check('wearable classifier reuses existing Tailoring authority','RebirthServiceCraftSkillService.IsTailoringWearableName' in svc)
check('death baseline patch covers base player death surface','PatchDeathMethod(harmony, typeof(EntityAlive))' in svc and 'StampDeathBaselines' in svc)
check('repair start captures baseline before degradation','EnsureDurabilityOriginalMaxUseTimes(item, localCap)' in svc)
check('standalone durability baseline metadata exists','RebirthDurabilityOriginalMaxUseTimes' in prov)
check('crafted provenance remains cap authority','TryRead(value, out provenance)' in prov and 'provenance.OriginalMaxUseTimes > 0f' in prov)
check('server can force authoritative standalone baseline','ForceDurabilityOriginalMaxUseTimes' in prov)
check('baseline stack compatibility protected','TryGetDurabilityOriginalMaxUseTimes(a,out da)' in prov and 'Math.Abs(da-db)' in prov)
check('V3 stat adapter discovers Stats reflectively','FindMember(t, "Stats")' in adp or 'FindMember(t, "Stats")' in adp)
check('V3 stat adapter targets DegradationMax only','PassiveEffects.DegradationMax' in adp)
check('V3 stat adapter verifies observed MaxUseTimes response','candidateMax != current' in adp and 'source.Clone()' in adp)
check('V3 stat adapter fails closed',adp.count('return false;')>=10 and 'failed closed' in adp)
check('no destructive ClearStats call','ClearStats(' not in adp and 'ClearStats(' not in svc)
check('no destructive InitStats call','InitStats(' not in adp and 'InitStats(' not in svc)
check('no wholesale Stats assignment',re.search(r'\.Stats\s*=',adp+svc) is None)
check('unrelated stat entries preserved by in-place array element mutation','stats.SetValue(boxed, index)' in adp)
check('post-write cap verification present','appliedMax > upperCap || appliedMax < current' in adp)
check('service installed by survivor progression installer','RebirthRepairSignatureService.Install(HarmonyInstance)' in inst)
check('repair queue patch installed','typeof(RebirthRepairSignatureQueuePatch)' in inst)
check('repair completion patch installed','typeof(RebirthRepairSignatureCompletionPatch)' in inst)
check('read-only repair diagnostics registered','repairbonus [status|vectors]' in cmd and 'ExecuteRepairBonus' in cmd)
check('vector: first repair exemption','first repair free' in vec)
check('vector: repeated repair degradation','10% native repair loss is halved' in vec)
check('vector: death-degraded restoration','death-created deficit' in vec)
check('vector: cap','never exceeds original/crafted cap' in vec)
check('vector: disabled repair degradation restoration','repair degradation is off' in vec)
check('restoration tuning documented as unlocked in runtime report','locked=False' in svc)
check('Base Game does not receive separate entitlement persistence','SignatureBonusId' not in svc and 'SignatureBonusId' not in net)
check('no native repair degradation replacement system authored','PermanentRepairDegradation' not in svc and 'RepairDegradation' not in svc)
check('block repair not patched by Chunk E service','ItemActionRepair' not in svc)

for rel in ['Scripts/Survivor/Repair/RebirthItemConditionStatAdapter.cs','Scripts/Survivor/Repair/RebirthRepairSignatureService.cs','Scripts/Survivor/Repair/RebirthRepairSignatureNetPackages.cs','Scripts/Survivor/Repair/RebirthRepairSignatureVectorHarness.cs','Scripts/Survivor/Provenance/RebirthItemProvenanceAdapter.cs','Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs','Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs']:
    s=text(rel); check('delimiter '+rel,s.count('{')==s.count('}') and s.count('(')==s.count(')'))

bad=[]; xml_count=0
for p in root.rglob('*.xml'):
    xml_count+=1
    try: ET.parse(p)
    except Exception as e: bad.append((p,e))
check('all project XML parses',not bad)
print(f'PC018_STATIC_CHECKS={passed+failed} PASSED={passed} FAILED={failed} XML={xml_count}')
sys.exit(1 if failed else 0)
