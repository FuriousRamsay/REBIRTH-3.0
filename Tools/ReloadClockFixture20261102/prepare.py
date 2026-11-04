from pathlib import Path
import hashlib, json, re
T=Path(__file__).resolve().parent; R=T.parents[1]
source=R/'Scripts/Survivor/Progression/RebirthWeaponSustainedDpsService.cs'
new=source.read_text(encoding='utf-8-sig')
old=(R/'_Documentation/ReloadReceipt_20261029/RebirthWeaponSustainedDpsService.cs.baseline').read_text(encoding='utf-8-sig')
def member(s,key):
    a=s.index(key); i=s.index('{',a)+1; depth=1
    while depth:
        if s[i]=='{': depth+=1
        if s[i]=='}': depth-=1
        i+=1
    return s[a:i]
common='''
static object Gate=new object();static Dictionary<int,RuntimeReloadStart> ActiveReloads=new();static Dictionary<string,float> MeasuredBaseReloadSeconds=new();static Dictionary<int,int> Cache=new(),RecentHeld=new();static bool ReloadTimingDebug=false;
static float ResolveEffectiveReloadMultiplier(EntityPlayer p,ItemValue v,string s,bool held)=>p.Multiplier;
static float ReadReloadSeconds(ItemClass c,object a)=>5;static void LogReloadAnimationClips(EntityPlayer p){}static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
public static int Learned=>MeasuredBaseReloadSeconds.Count;public static int Pending=>ActiveReloads.Count;public static float LearnedValue{get{foreach(var v in MeasuredBaseReloadSeconds.Values)return v;return -1;}}
public static void Reset(){ActiveReloads.Clear();MeasuredBaseReloadSeconds.Clear();}
'''
parts=['using System;using System.Collections.Generic;using System.Globalization;using System.Runtime.CompilerServices;']
for label,s in [('Baseline',old),('Proposed',new)]:
    keys=['private sealed class RuntimeReloadStart','public static void OnRuntimeReloadStarted(','public static void OnRuntimeReloadCompleted(','private static string BuildReloadMeasurementKey(']
    extra=''
    if label=='Proposed':
        keys+=['private static bool TryResolveRuntimeReload(','public static void OnRuntimeReloadCancelled(','private sealed class ReloadTimingObservation','internal static bool TryNormalizeReloadSample(']
        extra='static ConditionalWeakTable<EntityPlayer,ReloadTimingObservation> ReloadTimingObservations=new();'
    parts.append('public static class '+label+'{'+''.join(member(s,k) for k in keys)+common+extra+'}')
    for suffix,cb in [('Start','private static void Prefix('),('Complete','private static void Postfix(')]:
        patch=member(s,'public static class RebirthRangedReload'+suffix+'TimingPatch')
        body=member(patch,cb).replace('private static void','public static void').replace('RebirthWeaponSustainedDpsService.',label+'.')
        parts.append('public static class '+label+suffix+'Hook{'+body+'}')
fixture=(R/'Tools/ReloadReceiptFixture/Fixture.cs.txt').read_text()
fixture=fixture.replace('public static float realtimeSinceStartup;','public static float realtimeSinceStartup;public static double timeAsDouble;')
fixture=re.sub(r'Time.realtimeSinceStartup=([^;]+);',r'Time.realtimeSinceStartup=\1;Time.timeAsDouble=Time.realtimeSinceStartup;',fixture)
extra_checks='''
 Reset();var clockOwner=new EntityPlayerLocal();var clockData=Setup(clockOwner);Start(clockData);End(clockData);
 A(Proposed.LearnedValue==3,"initial valid receipt");
 Time.realtimeSinceStartup=20;Time.timeAsDouble=20;Start(clockData);
 Time.realtimeSinceStartup=46;Time.timeAsDouble=23;Proposed.OnRuntimeReloadCompleted(Act(clockData),clockData);
 A(Proposed.LearnedValue==3&&Proposed.Pending==0,"26 second hitch cannot replace valid calibration");
 Reset();clockData=Setup(new EntityPlayerLocal());Start(clockData);Time.realtimeSinceStartup=36;Time.timeAsDouble=13;
 Proposed.OnRuntimeReloadCompleted(Act(clockData),clockData);A(Proposed.Learned==0,"cold stalled reload keeps fallback");
 foreach(var sample in new[]{(3f,3d,1f,true),(3.2f,3d,2f,true),(26f,3d,1f,false),(3f,0d,1f,false),(3f,6d,1f,false),(3f,double.NaN,1f,false),(3f,double.PositiveInfinity,1f,false),(3f,3d,float.NaN,false),(3f,3d,0f,false),(31f,31d,1f,false)}){
  bool ok=Proposed.TryNormalizeReloadSample(sample.Item1,sample.Item2,sample.Item3,out float value);
  A(ok==sample.Item4,"paired-clock validity "+sample);
  if(ok)A(value==sample.Item1*sample.Item3,"preserves measured normalization");
 }
'''
fixture=fixture.replace(' Console.WriteLine($"PASS',extra_checks+' Console.WriteLine($"PASS')
(T/'Actual.cs').write_text('\n'.join(parts)+fixture+'\npublic static class Program{public static void Main()=>Checks.Run();}')
(T/'source-binding.json').write_text(json.dumps({'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'scope':'Actual receipt methods/hooks + actual clock validator; Unity objects/clocks are doubles. Native acceptance pending.'},indent=2))
print('Extracted current production receipt methods and clock validator; historical receipt negatives retained')
