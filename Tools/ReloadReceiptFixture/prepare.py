from pathlib import Path
import json,hashlib,difflib
R=Path(__file__).resolve().parents[2];D=R/'_Documentation/ReloadReceipt_20261029';T=Path(__file__).parent;O=T/'out';O.mkdir(exist_ok=True)
p=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Scripts/Survivor/Progression/RebirthWeaponSustainedDpsService.cs')
assert hashlib.sha256(p.read_bytes()).hexdigest().upper()==json.loads((D/'baseline-hash.json').read_text(encoding='utf-8-sig'))['Hash']
old=(D/'RebirthWeaponSustainedDpsService.cs.baseline').read_text(encoding='utf-8-sig');new=(R/'Scripts/Survivor/Progression/RebirthWeaponSustainedDpsService.cs').read_text(encoding='utf-8-sig')
def member(s,key):
 a=s.index(key);i=s.index('{',a)+1;n=1
 while n:
  if s[i]=='{':n+=1
  if s[i]=='}':n-=1
  i+=1
 return s[a:i]
# Only receipt members and their Harmony hooks may differ. All profile math,
# classification, key calculation, animation fallback, caches and progression stay exact.
def stripped(s):
 for key in ['private sealed class RuntimeReloadStart','public static void OnRuntimeReloadStarted(','public static void OnRuntimeReloadCompleted(']:s=s.replace(member(s,key),'')
 for key in ['private static bool TryResolveRuntimeReload(','public static void OnRuntimeReloadCancelled(']:
  if key in s:s=s.replace(member(s,key),'')
 start=s.index('[HarmonyPatch]\npublic static class RebirthRangedReloadStartTimingPatch');end=s.index('[HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]',start)
 return ''.join((s[:start]+s[end:]).split())
assert stripped(old)==stripped(new),'Unrelated service change'
common='''
static object Gate=new object();static Dictionary<int,RuntimeReloadStart> ActiveReloads=new Dictionary<int,RuntimeReloadStart>();static Dictionary<string,float> MeasuredBaseReloadSeconds=new Dictionary<string,float>();static Dictionary<int,int> Cache=new Dictionary<int,int>(),RecentHeld=new Dictionary<int,int>();static bool ReloadTimingDebug=false;
static float ResolveEffectiveReloadMultiplier(EntityPlayer p,ItemValue v,string s,bool held)=>p.Multiplier;
static float ReadReloadSeconds(ItemClass c,object a)=>5;static void LogReloadAnimationClips(EntityPlayer p){}static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
public static int Learned=>MeasuredBaseReloadSeconds.Count;public static int Pending=>ActiveReloads.Count;public static float LearnedValue{get{foreach(var v in MeasuredBaseReloadSeconds.Values)return v;return -1;}}
public static void Reset(){ActiveReloads.Clear();MeasuredBaseReloadSeconds.Clear();}
'''
parts=['using System;using System.Collections.Generic;using System.Globalization;']
for label,s in [('Baseline',old),('Proposed',new)]:
 keys=['private sealed class RuntimeReloadStart','public static void OnRuntimeReloadStarted(','public static void OnRuntimeReloadCompleted(','private static string BuildReloadMeasurementKey(']
 if label=='Proposed':keys+=['private static bool TryResolveRuntimeReload(','public static void OnRuntimeReloadCancelled(']
 parts.append('public static class '+label+'{'+''.join(member(s,k) for k in keys)+common+'}')
 # Actual Harmony callback bodies, stripped only of attributes/target resolver.
 for suffix,cb in [('Start','private static void Prefix('),('Complete','private static void Postfix(')]:
  patch=member(s,'public static class RebirthRangedReload'+suffix+'TimingPatch');body=member(patch,cb).replace('private static void','public static void').replace('RebirthWeaponSustainedDpsService.',label+'.')
  parts.append('public static class '+label+suffix+'Hook{'+body+'}')
(O/'Actual.cs').write_text('\n'.join(parts)+(T/'Fixture.cs.txt').read_text())
(D/'runtime.diff').write_text(''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='production/ReloadService',tofile='proposal/ReloadService')))
print('PASS current production guard and exact non-receipt source equivalence')
