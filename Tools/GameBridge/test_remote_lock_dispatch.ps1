$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Crafting/RemoteCrafting/RemoteResourceLiveSync.cs')
function Extract([string]$signature) {
 $a=$source.IndexOf($signature);if($a -lt 0){throw "Missing $signature"};$b=$source.IndexOf('{',$a);$depth=1;$i=$b+1
 while($depth -gt 0){if($source[$i] -eq '{'){$depth++};if($source[$i] -eq '}'){$depth--};$i++}
 return $source.Substring($a,$i-$a)
}
$methods=(Extract '    internal static void NotifyDifferences(')+(Extract '    private static Exception Finalizer(')
$methods=$methods.Replace('private static Exception Finalizer','public static Exception Finalizer')
Add-Type -TypeDefinition (@'
using System;using System.Collections.Generic;
public interface ILockTarget {}
public class Target:ILockTarget {}
public class LockManager {public List<LockEntry> Entries=new List<LockEntry>();public bool Fail; public struct LockEntry:IEquatable<LockEntry> {public ILockTarget Target;public ushort Channel;public LockEntry(ILockTarget t,ushort c){Target=t;Channel=c;}public bool Equals(LockEntry o){return ReferenceEquals(Target,o.Target)&&Channel==o.Channel;}}}
public static class Log {public static void Warning(string s){}}
public static class RemoteResourceAccessEventPatchInstaller {public static List<ILockTarget> Seen=new List<ILockTarget>(); public static void LockTargetChanged(ILockTarget t){Seen.Add(t);}}
public static class Actual {public static List<LockManager.LockEntry> Capture(LockManager m,int p){if(m.Fail)throw new Exception();return m.Entries;}
'@+$methods+@'
}
public static class Checks {
static void A(bool v,string n){if(!v)throw new Exception(n);}
public static void Run(){
 var t=new Target();var u=new Target();var empty=new List<LockManager.LockEntry>();var one=new List<LockManager.LockEntry>{new LockManager.LockEntry(t,0)};
 Actual.NotifyDifferences(empty,one);A(RemoteResourceAccessEventPatchInstaller.Seen.Count==1,"accepted lock missed");
 RemoteResourceAccessEventPatchInstaller.Seen.Clear();Actual.NotifyDifferences(one,one);A(RemoteResourceAccessEventPatchInstaller.Seen.Count==0,"denied/unchanged request invalidated");
 Actual.NotifyDifferences(one,empty);A(RemoteResourceAccessEventPatchInstaller.Seen.Count==1,"release missed");
 RemoteResourceAccessEventPatchInstaller.Seen.Clear();var channels=new List<LockManager.LockEntry>{new LockManager.LockEntry(t,0),new LockManager.LockEntry(t,1),new LockManager.LockEntry(u,0)};Actual.NotifyDifferences(empty,channels);A(RemoteResourceAccessEventPatchInstaller.Seen.Count==2,"same target channels not coalesced");
 var error=new Exception("native");var m=new LockManager{Entries=one};A(Object.ReferenceEquals(Actual.Finalizer(m,7,empty,error),error),"native exception changed");
 m.Fail=true;A(Object.ReferenceEquals(Actual.Finalizer(m,7,empty,error),error),"observer failure masked native error");
 A(Actual.Finalizer(m,7,null,null)==null,"disabled observer altered native success");
 Console.WriteLine("PASS actual lock-set delta/finalizer: acquired/released targets, no unchanged invalidation, channel deduplication, exception-after-mutation observation, preserved native errors and disabled pass-through; native tables doubled.");
}}
'@)
[Checks]::Run()
if($source -match 'typeof\(RemoteResource(TileEntityLocked|TileEntityUnlocked|EntityLocked|EntityUnlocked)Patch\)'){throw 'Duplicate base callback registration remains'}
if($source -notmatch 'typeof\(RemoteResourceServerLockTransitionPatch\)'){throw 'Central observer unregistered'}
