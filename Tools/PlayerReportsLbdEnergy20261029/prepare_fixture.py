"""Compile exact production method bodies with explicit native/world/storage test adapters."""
from pathlib import Path
import re,sys
root=Path(__file__).resolve().parents[2]
here=Path(__file__).resolve().parent
baseline='--baseline' in sys.argv
src=here/'baseline' if baseline else root
out=here/('negative' if baseline else 'Fixture')
out.mkdir(exist_ok=True)
def read(rel): return (src/rel).read_text(encoding='utf-8-sig')
def body(text,signature):
    start=text.index(signature); at=text.index('{',start); depth=1; i=at+1
    # These selected methods have no brace-bearing verbatim strings; ignore ordinary strings/comments.
    mode=None
    while depth:
        c=text[i];n=text[i:i+2]
        if mode=='line':
            if c=='\n':mode=None
        elif mode=='comment':
            if n=='*/':mode=None;i+=1
        elif mode=='string':
            if c=='\\':i+=1
            elif c=='"':mode=None
        elif n=='//':mode='line';i+=1
        elif n=='/*':mode='comment';i+=1
        elif c=='"':mode='string'
        elif c=='{':depth+=1
        elif c=='}':depth-=1
        i+=1
    return text[start:i]
patch=read('Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs')
local=body(patch,'internal static class RebirthSurvivorCombatSkillPatch')
received=(body(patch,'internal static class RebirthSurvivorReceivedCombatSkillPatch') if not baseline else '''internal static class RebirthSurvivorReceivedCombatSkillPatch {
internal struct DamageState{} private static void Prefix(EntityAlive __instance,DamageResponse _dmResponse,out DamageState __state){__state=default;}
private static void Postfix(EntityAlive __instance,DamageResponse _dmResponse,DamageState __state,bool __runOriginal){} }''')
router=read('Scripts/Survivor/Progression/RebirthSkillEventRouter.cs')
router=router[:router.index('    public static void OnFarmingHarvestCompleted')]+'}\n'
dps=read('Scripts/Survivor/Progression/RebirthWeaponSustainedDpsService.cs')
batch=dps[dps.index('    private sealed class CombatAward'):dps.index('    private static readonly FastTags<TagGroup.Global> PrimaryTags')]
methods=['public static void QueueCombatProgress','public static void FlushQueuedCombatProgress','public static float CombatSkillMultiplier','public static float CalculateCombatProgress']
service='public static class RebirthWeaponSustainedDpsService { private static readonly object Gate=new object();\n'+batch+'\n'+'\n'.join(body(dps,m) for m in methods)+'''
private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
public sealed class Profile{public bool Valid=true;public float SustainedDps=24;}
public static bool ProfileAvailable=true; public static Profile ActiveProfile=new Profile();
public static bool TryGetCombatProfile(EntityPlayer p,ItemValue i,out Profile profile){profile=ActiveProfile;return ProfileAvailable;}
}'''
met=read('Scripts/Metabolism/RebirthMetabolismService.cs')
names=['private static bool IsGodModeActive','public static float GetEnergyReserveCeiling(EntityPlayer','public static void ClampEnergyToReserveCeiling','private static float ProcessEnergy','private static float GetPreparedMealEnergyEfficiencyMultiplier','private static float GetEnergyRecoveryFactor','private static float EstimateEnergyRecoveryPerRealMinute','public static float GetEnergyRecoveryHydrationMultiplier','private static void ResolveActivity(','public static RebirthMetabolismSnapshot BuildSnapshot(EntityPlayer','private static void ResolveActivityNoMutation']
sample=met[met.index('        state.LastEnergyUsePerRealMinute = IsGodModeActive'):met.index('        state.HasEnergyRateSample = true;',met.index('        state.LastEnergyUsePerRealMinute = IsGodModeActive'))+len('        state.HasEnergyRateSample = true;')] if not baseline else ''
energy='public static partial class RebirthMetabolismService {\n'+'\n'.join(body(met,n) for n in names)+'''
public static void Step(EntityPlayer player,RebirthMetabolismState state,RebirthMetabolismModifiers mods,float seconds){
ResolveActivity(player,state,seconds/60f,out _,out float energyUsePerRealMinute);
float energyRecoveryPerRealMinute=ProcessEnergy(player,state,mods,energyUsePerRealMinute,seconds/60f);
'''+sample+'\n}\n}'
config=read('Scripts/Metabolism/RebirthMetabolismConfig.cs')
fields=re.findall(r'public static float \w+\s*=\s*[0-9.]+f;',config)
config='public static class RebirthMetabolismConfig{\n'+'\n'.join(fields)+'\n'+body(config,'public static float GetEnergyHydrationEfficiency')+'\n'+body(config,'public static float GetEnergyReserveCeiling')+'\n}'
# Classifier/weapon-family methods are unchanged current-production sources, copied as fixture inputs.
classifier=(here/'inputs/RebirthProgressionRuntimeConfig.cs').read_text(encoding='utf-8-sig')
classify='public static partial class RebirthProgressionRuntimeConfig { private static readonly Dictionary<int,string> CombatClassCache=new(); public static List<RebirthSkillSourceMap> CombatMaps=new();\n'+'\n'.join(body(classifier,n) for n in ['public static string ClassifyCombat(ItemValue','private static string ClassifyCombatUncached','private static bool MatchesExact','private static bool MatchesAny','private static RebirthSkillSourceMap ParseMap','private static string[] ExactTokens','private static string[] Tokens'])+'\npublic static void LoadMap(XElement e)=>CombatMaps.Add(ParseMap(e));\n}'
weapon=(here/'inputs/RebirthWeaponFamilySkillService.cs').read_text(encoding='utf-8-sig')
families='public static class RebirthWeaponFamilySkillService{'+weapon[weapon.index('    private static readonly HashSet<string> MeleeSkills'):weapon.index('    private static bool installed;')]+'\n'+'\n'.join(body(weapon,n) for n in ['public static bool IsChunk7Skill','public static bool IsMeleeSkill','public static bool IsRangedSkill'])+'}'
for file,text in [('Combat.cs','using System;using System.Collections.Generic;using System.Runtime.CompilerServices;using System.Xml.Linq;using UnityEngine;\n'+local+'\n'+received+'\n'+service+'\n'+classify+'\n'+families),('Router.cs',router),('Energy.cs','using System;using System.Collections.Generic;using UnityEngine;\n'+energy+'\n'+config)]: (out/file).write_text(text,encoding='utf-8')
for name in ['Program.cs','Adapters.cs','Fixture.csproj']:(out/name).write_bytes((here/'FixtureSource'/name).read_bytes())
if not baseline:
    abi=here/'NativeAbi';abi.mkdir(exist_ok=True)
    (abi/'Boundaries.cs').write_text('using System;using HarmonyLib;\n[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.ProcessDamageResponse))]\n'+received,encoding='utf-8')
print(out)
