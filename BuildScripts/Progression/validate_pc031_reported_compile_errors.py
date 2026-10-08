#!/usr/bin/env python3
from pathlib import Path
import sys, re, xml.etree.ElementTree as ET

root=Path(sys.argv[1] if len(sys.argv)>1 else Path(__file__).resolve().parents[2])
checks=[]
def text(rel): return (root/rel).read_text(encoding='utf-8-sig')
def ck(name, ok, detail=''):
    checks.append(bool(ok)); print(('PASS' if ok else 'FAIL')+' | '+name+((' | '+str(detail)) if detail else ''))

resource=text('Scripts/Survivor/Backgrounds/RebirthResourceSignatureService.cs')
combat=text('Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureNetPackages.cs')
scav=text('Scripts/Survivor/Backgrounds/RebirthScavengerSalvageProfileService.cs')
work=text('Scripts/Survivor/Backgrounds/RebirthWorkmanshipSignatureService.cs')
craft=text('Scripts/Survivor/Capability/RebirthPersonalCraftAuthorizationService.cs')
console=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
ids=text('Scripts/Survivor/Domain/RebirthSurvivorIds.cs')
harness=text('Scripts/Survivor/Debug/RebirthSurvivorMigrationVectorHarness.cs')
beast=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs')
black=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicTargetClassifier.cs')
explorer=text('Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerOverlay.cs')
infra=text('Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs')
stats=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorStatisticsPanel.cs')
players=text('Scripts/UI/Players/XUiC_RebirthPlayers.cs')

# User-reported CS1061 AttackHitInfo.hitPosition: compile against lowest-common target member.
for name,s in [('resource signature',resource),('scavenger salvage',scav),('workmanship',work)]:
    ck(name+' no AttackHitInfo.hitPosition', '.attackDetails.hitPosition' not in s)
    ck(name+' uses AttackHitInfo.raycastHitPosition', '.attackDetails.raycastHitPosition' in s)

# CS0121 overload ambiguity.
ck('tracking packet UInt16 explicitly widened before Math.Min', 'Math.Min(256,(int)b.ReadUInt16())' in combat)
ck('tracking packet old ambiguous Math.Min absent', 'Math.Min(256,b.ReadUInt16())' not in combat)

# CS0103 Manager namespace.
ck('personal craft imports Audio namespace', re.search(r'^using Audio;\s*$', craft, re.M) is not None)
ck('personal craft denied sound retained', 'Manager.PlayInsidePlayerHead("ui_denied")' in craft)

# CS0165 console pos.
ck('console provenance position initialized', 'Vector3i pos=Vector3i.zero;' in console)
ck('console block/crop/electrical parse gate retained', 'TryParseProvenancePos(p,2,out pos)' in console)

# CS0117 stable ID.
ck('SkillDrinkPreparation stable ID exists', 'public const string SkillDrinkPreparation = "skill.drink_preparation";' in ids)
ck('migration harness uses stable Drink Preparation ID', harness.count('RebirthSurvivorIds.SkillDrinkPreparation') >= 4)

# CS0177 / CS0269 out parameter definite assignment.
can_interact=re.search(r'public static bool CanInteract\([^\)]*out string reason\)\s*\{(?P<body>.*?)\n\s*\}', beast, re.S)
ck('Beastmaster CanInteract initializes reason', bool(can_interact and 'reason=string.Empty;' in can_interact.group('body')[:120]))
classify=re.search(r'public static bool TryClassifyZombieAnimal\([^\)]*out string reason\)\s*\{(?P<body>.*?)\n\s*\}', black, re.S)
ck('Black Magic classifier initializes reason', bool(classify and 'reason=string.Empty;' in classify.group('body')[:140]))
ck('Black Magic protected-tag short-circuit retained', 'definition.Protected||HasProtectedTag(target.EntityClass,out reason)' in black)

# CS0136 local shadowing on older compiler.
ck('discipline branch uses disciplineBlocked', 'bool disciplineBlocked=false,disciplineUnknown=false;' in explorer)
ck('discipline branch no reused blocked declaration', 'bool blocked=false,unknown=false;for(int ri=' not in explorer)
ck('normal graph blocked variables retained', 'bool blocked=false,unknown=false,recommended=false;' in explorer)

# CS1061 WorldBase.worldTime.
capture=re.search(r'public static bool CaptureBuiltConfiguration\(WorldBase world.*?\n\s*\}', infra, re.S)
ck('CaptureBuiltConfiguration no WorldBase.worldTime', bool(capture and 'world.worldTime' not in capture.group(0)))
ck('CaptureBuiltConfiguration uses live World worldTime', 'GameManager.Instance.World.worldTime' in (capture.group(0) if capture else ''))

# CS1061 XUiController.IsVisible.
ck('statistics visibility uses ViewComponent', 'c.ViewComponent.IsVisible=v;' in stats)
ck('statistics direct XUiController IsVisible absent', 'c.IsVisible=v;' not in stats)

# CS0165 groupView.
ck('players groupView initialized', 'RebirthPartyIdentityView groupView = null;' in players)
ck('players group identity lookup retained', 'TryGet(selected.entityId, out groupView)' in players)

# Global regression-focused scans.
script_root=root/'Scripts'
all_cs='\n'.join(p.read_text(encoding='utf-8-sig',errors='ignore') for p in script_root.rglob('*.cs'))
ck('no remaining AttackHitInfo .hitPosition references', '.attackDetails.hitPosition' not in all_cs)
ck('no ambiguous Math.Min UInt16 pattern', 'Math.Min(256,b.ReadUInt16())' not in all_cs)

# Structural delimiter sanity for every changed C# source.
changed=[
'Scripts/Survivor/Backgrounds/RebirthResourceSignatureService.cs',
'Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureNetPackages.cs',
'Scripts/Survivor/Backgrounds/RebirthScavengerSalvageProfileService.cs',
'Scripts/Survivor/Backgrounds/RebirthWorkmanshipSignatureService.cs',
'Scripts/Survivor/Capability/RebirthPersonalCraftAuthorizationService.cs',
'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs',
'Scripts/Survivor/Debug/RebirthSurvivorMigrationVectorHarness.cs',
'Scripts/Survivor/Domain/RebirthSurvivorIds.cs',
'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs',
'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicTargetClassifier.cs',
'Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerOverlay.cs',
'Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs',
'Scripts/Survivor/UI/XUiC_RebirthSurvivorStatisticsPanel.cs',
'Scripts/UI/Players/XUiC_RebirthPlayers.cs',
]
# Harness is not modified by PC031 but is included above as a consumer check; only structural-check actual modified files.
actual_changed=[x for x in changed if x!='Scripts/Survivor/Debug/RebirthSurvivorMigrationVectorHarness.cs']
for rel in actual_changed:
    s=text(rel)
    ck('delimiter '+Path(rel).name, s.count('{')==s.count('}') and s.count('(')==s.count(')') and s.count('[')==s.count(']'))

xmls=list(root.rglob('*.xml')); bad=[]
for p in xmls:
    try: ET.parse(p)
    except Exception as e: bad.append((str(p.relative_to(root)),str(e)))
ck('all XML parse', not bad, 'XML='+str(len(xmls)))

passed=sum(checks); failed=len(checks)-passed
print(f'PC031_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={len(xmls)}')
if failed: sys.exit(1)
