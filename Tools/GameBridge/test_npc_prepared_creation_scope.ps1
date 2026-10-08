#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$foundation=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'))
$start=$foundation.IndexOf('public readonly struct RebirthNpcStableId');$end=$foundation.IndexOf('public sealed class RebirthNpcProfile',$start)
$scope=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcPreparedCreationScope.cs')).Replace('using System;','')
$fixture=@"
public static class CreationScopeFixture{
 static int checks;static void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
 public static string Run(){IDisposable lease,other;RebirthNpcStableId read;var id=RebirthNpcStableId.NewId();
 Check(!RebirthNpcPreparedCreationScope.TryGet(-123,"survivor.ambient",out read),"unscoped identity exposed");
 Check(RebirthNpcPreparedCreationScope.TryEnter(-123,"survivor.ambient",id,out lease),"negative native class hash refused");
 Check(RebirthNpcPreparedCreationScope.TryGet(-123,"SURVIVOR.AMBIENT",out read)&&read==id,"correct identity not supplied");
 Check(!RebirthNpcPreparedCreationScope.TryGet(-124,"survivor.ambient",out read),"foreign class identity supplied");
 Check(!RebirthNpcPreparedCreationScope.TryGet(-123,"bandit.standard",out read),"foreign profile identity supplied");
 Check(!RebirthNpcPreparedCreationScope.TryEnter(-123,"survivor.ambient",id,out other),"nested context admitted");
 bool isolated=false;var thread=new System.Threading.Thread(()=>isolated=!RebirthNpcPreparedCreationScope.TryGet(-123,"survivor.ambient",out var unused));thread.Start();thread.Join();Check(isolated,"identity crossed threads");
 lease.Dispose();Check(!RebirthNpcPreparedCreationScope.TryGet(-123,"survivor.ambient",out read),"dispose leaked identity");
 Check(RebirthNpcPreparedCreationScope.TryEnter(-123,"survivor.ambient",id,out other),"new scope refused");lease.Dispose();Check(RebirthNpcPreparedCreationScope.TryGet(-123,"survivor.ambient",out read),"stale disposal cleared new scope");other.Dispose();
 Check(!RebirthNpcPreparedCreationScope.TryEnter(1,"survivor.ambient",default(RebirthNpcStableId),out lease),"empty identity accepted");
 int nativeId;
 Check(!RebirthNpcPreparedCreationScope.TryEnter(-123,"survivor.ambient",id,0,out lease),"zero native ID accepted");
 Check(RebirthNpcPreparedCreationScope.TryEnter(-123,"survivor.ambient",id,42,out lease),"reserved ID scope refused");
 Check(RebirthNpcPreparedCreationScope.TryGetNativeEntityId(-123,out nativeId)&&nativeId==42,"reserved native ID lost");
 Check(!RebirthNpcPreparedCreationScope.TryGetNativeEntityId(-124,out nativeId),"foreign native ID supplied");
 lease.Dispose();Check(!RebirthNpcPreparedCreationScope.TryGetNativeEntityId(-123,out nativeId),"native ID scope leaked");
 return "PASS "+checks+" actual creation scope/production stable-ID checks; native factory/model initialization not exercised";
 }
}
"@
Add-Type -TypeDefinition ('using System;'+$foundation.Substring($start,$end-$start)+$scope+$fixture)
[CreationScopeFixture]::Run()