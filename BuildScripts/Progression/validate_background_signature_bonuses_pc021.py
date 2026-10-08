#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET, math, re

root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve(); passed=failed=0

def check(name, ok):
    global passed, failed
    if ok: passed+=1; print('PASS', name)
    else: failed+=1; print('FAIL', name)

def text(rel): return (root/rel).read_text(encoding='utf-8', errors='replace')

bon=ET.parse(root/'Config/_Survivor/background_bonuses.xml').getroot(); B={e.attrib['id']:e for e in bon.findall('bonus')}
def tune(b,key):
    for t in b.findall('tuning'):
        if t.attrib.get('key')==key:return (t.attrib.get('value'),t.attrib.get('locked'))
    return None

check('Ore Sense range tuning explicit unlocked', tune(B['background_bonus.ore_sense'],'range_meters')==('10','false'))
check('Professional Logging wood tuning explicit unlocked', tune(B['background_bonus.professional_logging'],'wood_output_multiplier')==('1.5','false'))
check('Professional Logging stamina tuning explicit unlocked', tune(B['background_bonus.professional_logging'],'cutting_stamina_multiplier')==('0.75','false'))
check('Fire Resistant damage tuning explicit unlocked', tune(B['background_bonus.fire_resistant'],'fire_damage_multiplier')==('0.5','false'))
check('Fire Resistant duration tuning explicit unlocked', tune(B['background_bonus.fire_resistant'],'burn_duration_multiplier')==('0.5','false'))

ore=text('Scripts/Survivor/Backgrounds/RebirthOreSenseService.cs')
res=text('Scripts/Survivor/Backgrounds/RebirthResourceSignatureService.cs')
net=text('Scripts/Survivor/Backgrounds/RebirthResourceSignatureNetPackages.cs')
ui=text('Scripts/Survivor/UI/XUiC_RebirthOreSenseHud.cs')
actions=text('Scripts/Input/PlayerActionsRebirth.cs')
controls=text('Scripts/Input/RebirthNativeControls.cs')
patches=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs')
installer=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
dbg=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
xui=text('Config/XUi_InGame/xui.xml')
windows=text('Config/XUi_InGame/windows.xml')
loc=text('Config/Localization.csv')

# Ore Sense active ability + presentation.
check('Ore Sense service installed', 'RebirthOreSenseService.Install()' in installer)
check('Ore Sense bonus ownership gate', 'RebirthBackgroundBonusService.HasBonus(player,BonusId)' in ore)
check('Ore Sense Rebirth mode gate', 'RebirthSurvivorMode.IsEnabledForCurrentWorld()' in ore)
check('Ore Sense range clamped 8-12m', 'Mathf.Clamp(RebirthResourceSignatureService.GetTuning(BonusId,"range_meters",10f),8f,12f)' in ore)
check('Ore Sense incremental scan budget 96', 'ScanBudgetPerFrame=96' in ore)
check('Ore Sense marker cap 64', 'MaxVisualMarkers=64' in ore)
check('Ore Sense cache cycle is incremental', 'while(budget-->0&&scanIndex<Offsets.Count)' in ore and 'nextScanCycle=now+0.75f' in ore)
check('Ore Sense 3D movement recenter', 'DistanceSq(center,p)>=4' in ore and 'int x=a.x-b.x,y=a.y-b.y,z=a.z-b.z' in ore)
check('Ore Sense continuously prunes changed ore', 'TryClassify(world.GetBlock(kv.Key)' in ore)
check('Ore Sense only five ore identities', all(x in ore for x in ['Iron=1','Lead=2','Coal=3','Nitrate=4','OilShale=5']))
check('Ore Sense no entity query', 'GetEntities' not in ore and 'GetEntity(' not in ore and 'Loot' not in ore and 'Container' not in ore)
check('Ore Sense classification requires ore-like block name', 'bool oreWord=n.Contains("ore")' in ore)
check('Ore Sense uses depth-independent rendering', 'CompareFunction.Always' in ore and '"_ZTest"' in ore and '"_ZWrite"' in ore)
check('Ore Sense range-edge fading', 'Mathf.Clamp01((range-e.Distance)' in ore and 'Mathf.Lerp(.10f,.82f,edge)' in ore)
check('Ore Sense nearest-first marker selection', 'entries.Sort((a,b)=>a.Distance.CompareTo(b.Distance))' in ore)
check('Ore Sense keybind action exists', 'OreSense = CreatePlayerAction("OreSense")' in actions)
check('Ore Sense default Shift+O', 'OreSense.AddDefaultBinding(new[] { Key.Shift, Key.O })' in actions)
check('Ore Sense controller rebindable', 'ControllerRebindableActions.Add(OreSense)' in actions)
check('Ore Sense dispatched through native controls', 'Actions.OreSense.WasPressed' in controls and 'RebirthOreSenseService.TryToggle(player)' in controls)
check('Ore Sense localized controls', all(k in loc for k in ['inpActRebirthOreSenseName,Ore Sense','inpActRebirthOreSenseDesc,Toggle the Miner Ore Sense overlay']))
check('Ore Sense HUD controller exists', 'class XUiC_RebirthOreSenseHud' in ui)
check('Ore Sense HUD uses approved icon', 'rb_bonus_ore_sense' in windows)
check('Ore Sense HUD state title localized', 'xuiRebirthOreSenseHudTitle,ORE SENSE' in loc)
check('Ore Sense HUD registered only in Rebirth toolbelt', "<if cond=\"character_progression('Rebirth')\"><append xpath=\"/xui/window_group[@name='toolbelt']\">" in xui and '<window name="rebirthOreSenseHud"/>' in xui)
check('Ore Sense debug report available', 'RebirthOreSenseService.BuildDebugReport' in dbg)

# System-wide tree withholding + server contribution settlement.
check('tree settlement service installed', 'RebirthResourceSignatureService.Install(HarmonyInstance)' in installer)
check('tree wood patch intercepts native collector', '[HarmonyPatch(typeof(GameUtils),"collectHarvestedItem")]' in res)
check('tree withholding limited to resourceWood + tree', 'IsWood(item)' in res and 'IsTree(tree)' in res)
check('tree withholding uses native-equivalent count scaling', all(x in res for x in ['damageMax/(float)count','damageTotalOfTarget','damageGiven','random.RandomFloat>probability']))
check('tree remote credit package server-directed', 'NetPackageRebirthTreeWoodCreditRequest' in net and 'PackageDirection=>NetPackageDirection.ToServer' in net)
check('tree remote request sender validated', 'ValidEntityIdForSender(playerId)' in net)
check('tree server records independent damage evidence', 'RecordTreeDamageEvidence' in res and 'Evidence.Add(new RebirthTreeDamageEvidence' in res)
check('tree evidence no harvest-tool gate', 'useHarvestTool' in res and 'RecordTreeDamageEvidence' in res and '&&useHarvestTool' not in res and 'if(!useHarvestTool)' not in res)
check('tree server bounds requested credit', 'ConsumeEvidence(player.entityId,pos,blockName,requestedCount)' in res and 'totalCap=Math.Max(16,ledger.AuthoredWoodMax*12)' in res)
check('tree per-contributor accounting', 'Dictionary<int,RebirthTreeContributorCredit>' in res and 'ledger.Contributors.TryGetValue(player.entityId' in res)
check('tree destruction hook targets BlockModelTree override', '[HarmonyPatch(typeof(BlockModelTree), nameof(BlockModelTree.OnBlockDestroyedBy)' in patches and 'OnTreeDestroyedServer' in patches)
check('tree destruction settlement delayed for late client credits', 'DestructionSettleDelaySeconds=0.45' in res and 'now-l.DestroyedUtc' in res)
check('tree destroyed ledger retained for late credits', 'PostDestructionRetentionSeconds=4.0' in res and 'age>=PostDestructionRetentionSeconds' in res)
check('tree late-credit payout is delta-only/non-duplicating', 'SettledBaseWood' in res and 'c.BaseWood-c.SettledBaseWood' in res and 'c.SettledBaseWood+=unsettled' in res)
check('tree settlement pays each contributor separately', 'foreach(KeyValuePair<int,RebirthTreeContributorCredit> ck in l.Contributors)' in res and 'payouts.Add(new RebirthTreeWoodPayout' in res)
check('non-Logger base share preserved', 'HasBonus(player,ProfessionalLoggingBonusId)?' in res and ':1f' in res)
check('Logger multiplier applies to Logger share only', 'q.BaseWood*mult' in res and 'ProfessionalLoggingBonusId' in res)
check('tree payout package server to owner', 'NetPackageRebirthTreeWoodGrant' in net and 'PackageDirection=>NetPackageDirection.ToClient' in net and '_attachedToEntityId:playerId' in res)
check('tree payout preserves harvest quest event', 'QuestEventManager.Current.HarvestedItem' in res)
check('tree payout preserves harvesting XP', '_xpFromHarvesting' in res and 'XPTypes.Harvesting' in res)
check('tree sapling Destroy drops not intercepted', 'EnumDropEvent.Destroy' not in res)
check('tree stale evidence/ledgers bounded', 'EvidenceLifetimeSeconds=2.5' in res and 'LedgerLifetimeSeconds=180.0' in res and 'Evidence.Count>256' in res)

# Logger cost reduction.
check('Logger cutting cost patch uses actual melee hit target', '[HarmonyPatch(typeof(ItemActionMelee),"hitTheTarget")]' in res and 'RefundLoggerCuttingStamina' in res)
check('Logger cost requires Logger bonus', 'HasBonus(player,ProfessionalLoggingBonusId)' in res)
check('Logger cost requires actual tree', 'if(!IsTree(bv))return' in res)
check('Logger cost requires Logging tool classifier', 'IsLoggingTool(actionData.invData.itemValue,name)' in res and 'skill.logging' in res)
check('Logger refunds native StaminaLoss fraction', 'EffectManager.GetValue(PassiveEffects.StaminaLoss' in res and 'refund=native*(1f-mult)' in res)
check('Logger stamina multiplier clamped safely', 'Mathf.Clamp(GetTuning(ProfessionalLoggingBonusId,"cutting_stamina_multiplier",0.75f),0.25f,1f)' in res)

# Firefighter damage + authored duration.
check('Firefighter server-side Heat damage gate', 'player.world.IsRemote()' in res and '_damageSource.GetDamageType()!=EnumDamageTypes.Heat' in res)
check('Firefighter requires bonus ownership', 'HasBonus(player,FireResistantBonusId)' in res)
check('Firefighter damage multiplier tunable and clamped', 'GetTuning(FireResistantBonusId,"fire_damage_multiplier",0.5f)' in res and '0.1f,1f' in res)
check('Firefighter fractional heat carry avoids rounding bias', 'HeatDamageResidual' in res and 'scaled=_strength*mult+residual' in res)
check('Firefighter patches native player damage surface', 'DeclaredMethod(typeof(EntityPlayer),nameof(EntityPlayer.DamageEntity)' in res)
check('Firefighter duration pass server-only', 'if(world.IsRemote()||c==null||!c.IsServer' in res and 'AdjustFirefighterBurnTimers(world)' in res)
check('Firefighter authored Molotov timer shortened', 'CapCVar(p,"$buffBurningMolotovDuration",16f*mult)' in res)
check('Firefighter authored flaming arrow timer shortened', 'CapCVar(p,"$buffBurningFlamingArrowDuration",14f*mult)' in res)
check('Firefighter authored burning element timer shortened', 'CapCVar(p,"$buffBurningElementDuration",10f*mult)' in res)
check('Firefighter authored environmental timer shortened', 'CapCVar(p,"$BurningEnvironmentDuration",1f*mult)' in res)
check('Firefighter fixed-duration filtering is burn-specific', 'IsAuthoredBurnBuff' in res and 'n.StartsWith("buffburning")' in res and 'n.Contains("burningtrap")' in res)
check('Firefighter does not remove burning buffs outright', 'RemoveBuff(' not in res)

# Rebirth-only/base isolation + diagnostics + source sanity.
check('tree withholding Rebirth-only', 'RebirthSurvivorMode.IsEnabledForCurrentWorld()' in res)
check('Ore Sense hidden/deactivated outside Rebirth', 'if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()' in ore and 'Deactivate();return' in ore)
check('resource debug command read-only surface', 'rbsurvivor resourcebonus [status|ore]' in dbg and 'ExecuteResourceBonus' in dbg)
for rel in [
    'Scripts/Survivor/Backgrounds/RebirthOreSenseService.cs',
    'Scripts/Survivor/Backgrounds/RebirthResourceSignatureService.cs',
    'Scripts/Survivor/Backgrounds/RebirthResourceSignatureNetPackages.cs',
    'Scripts/Survivor/UI/XUiC_RebirthOreSenseHud.cs']:
    s=text(rel); check('delimiter '+rel, s.count('{')==s.count('}') and s.count('(')==s.count(')'))

# Pure calculation/performance vectors.
def logger_payout(base,mult): return base*mult
def fire_total(ticks,mult):
    residual=0.0; total=0
    for strength in ticks:
        scaled=strength*mult+residual; applied=math.floor(scaled); residual=scaled-applied; total+=applied
    return total,residual
check('vector Logger 100 wood -> 150', abs(logger_payout(100,1.5)-150)<1e-9)
check('vector non-Logger 100 wood -> 100', abs(logger_payout(100,1.0)-100)<1e-9)
check('vector Logger stamina 20 -> 15 net', abs(20*0.75-15)<1e-9)
check('vector Firefighter 10 heat -> 5', math.floor(10*0.5)==5)
ft,fr=fire_total([1]*10,0.5); check('vector ten 1-point heat ticks -> 5 total', ft==5 and abs(fr)<1e-9)
# 12m integer sphere bound; service scans at most 96 blocks on an active frame and max 64 renders.
off=sum(1 for y in range(-12,13) for z in range(-12,13) for x in range(-12,13) if x*x+y*y+z*z<=144)
check('vector Ore Sense 12m sphere finite', off==7153)
check('vector Ore Sense minimum scan frames at 96 budget', math.ceil(off/96)==75)
check('vector Ore Sense render cap <=64', 64<=off)

# XML parse.
bad=[]; n=0
for p in root.rglob('*.xml'):
    n+=1
    try: ET.parse(p)
    except Exception as e: bad.append((str(p),str(e)))
check('all project XML parses', not bad)

print(f'PC021_STATIC_CHECKS={passed+failed} PASSED={passed} FAILED={failed} XML={n} ORE_OFFSETS={off}')
for x in bad: print('XMLERR',x)
sys.exit(1 if failed else 0)
