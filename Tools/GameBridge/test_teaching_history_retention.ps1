$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$s=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Progression/RebirthTeachingService.cs'))
$a=$s.IndexOf('    private static void PruneHistory(');$b=$s.IndexOf('    private static bool HasTeacherBonus',$a)
if($a-lt0-or$b-lt0){throw 'Production method not found'}
$method=$s.Substring($a,$b-$a)
$code=@'
using System;using System.Collections.Generic;
public class RebirthTeachingHistoryRuntimeState {public string Key,StudentStorageKey;public long LastCompletedUtcTicks;public float StudentTheoryValue;public int CompletionCount;}
public class RebirthWorldProgressionState {public Dictionary<string,RebirthTeachingHistoryRuntimeState> TeachingHistory=new Dictionary<string,RebirthTeachingHistoryRuntimeState>();}
public static class TeachingHistoryFixture {
__METHOD__
static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
static void Add(RebirthWorldProgressionState p,string key,string student,long ticks){p.TeachingHistory[key]=new RebirthTeachingHistoryRuntimeState{Key="not-the-dictionary-key",StudentStorageKey=student,LastCompletedUtcTicks=ticks,StudentTheoryValue=42,CompletionCount=7};}
public static void Run(){
 PruneHistory(null);
 var p=new RebirthWorldProgressionState();Add(p,"companion","npc:stable",1);var original=p.TeachingHistory["companion"];
 for(int i=0;i<300;i++)Add(p,"human"+i.ToString("D3"),"player"+i,100+i);
 PruneHistory(p);Check(p.TeachingHistory.Count==129,"128 ordinary plus companion");
 Check(object.ReferenceEquals(original,p.TeachingHistory["companion"])&&original.StudentTheoryValue==42&&original.CompletionCount==7&&original.LastCompletedUtcTicks==1,"companion theory/count/cooldown preserved");
 Check(!p.TeachingHistory.ContainsKey("human171")&&p.TeachingHistory.ContainsKey("human172")&&p.TeachingHistory.ContainsKey("human299"),"oldest ordinary discarded by actual dictionary key");
 PruneHistory(p);Check(p.TeachingHistory.Count==129,"idempotent");
 var only=new RebirthWorldProgressionState();for(int i=0;i<160;i++)Add(only,"npc"+i,"NPC:"+i,i+1);PruneHistory(only);Check(only.TeachingHistory.Count==160,"no arbitrary companion cap");
 var ties=new RebirthWorldProgressionState();for(int i=129;i>=0;i--)Add(ties,"key"+i.ToString("D3"),"player",10);ties.TeachingHistory["null"]=null;PruneHistory(ties);
 Check(ties.TeachingHistory.Count==128&&!ties.TeachingHistory.ContainsKey("null")&&!ties.TeachingHistory.ContainsKey("key000")&&!ties.TeachingHistory.ContainsKey("key001"),"null cleanup and deterministic ties");
 Console.WriteLine("PASS teaching history: companion Theory/count/cooldown survive pressure; newest128 ordinary retained; idempotence; NPC-only histories; null and tie handling.");
}
}
'@
Add-Type -TypeDefinition $code.Replace('__METHOD__',$method) -Language CSharp
[TeachingHistoryFixture]::Run()
if(!$s.Contains('PublishSavedOutcome(instructor, "teaching-npc:" + chosen.Id);')){throw 'NPC publication retry wiring missing'}
'PASS NPC publication uses existing durable owner retry helper.'
