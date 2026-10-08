#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$codec=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcSpawnReplayCodec.cs'))
$gate=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcSpawnAttemptGate.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','')
$fixture=@"
public static class SpawnAttemptFixture{
 public static string Run(){int checks=0;var gate=new RebirthNpcSpawnAttemptGate();System.Guid lease,other;
 if(!gate.TryBegin("spawn:a",out lease))throw new System.Exception("initial refused");checks++;
 if(gate.TryBegin("spawn:a",out other))throw new System.Exception("duplicate admitted");checks++;
 if(gate.ReleaseKnown("spawn:a",System.Guid.NewGuid()))throw new System.Exception("foreign lease released");checks++;
 if(!gate.ReleaseKnown("spawn:a",lease))throw new System.Exception("known release failed");checks++;
 if(!gate.TryBegin("spawn:a",out other))throw new System.Exception("known retry refused");checks++;
 if(gate.ReleaseKnown("spawn:a",lease))throw new System.Exception("stale lease released new attempt");checks++;
 for(int i=1;i<1024;i++)if(!gate.TryBegin("spawn:key"+i,out lease))throw new System.Exception("capacity early refusal");
 if(gate.TryBegin("spawn:overflow",out lease))throw new System.Exception("capacity overflow admitted");checks++;
 gate.Reset();if(gate.TryBegin("bad",out lease))throw new System.Exception("malformed admitted");checks++;
 return "PASS "+checks+" actual in-process spawn gate checks; native delegate/durable recovery not exercised";
 }
}
"@
Add-Type -TypeDefinition ($codec+$gate+$fixture)
[SpawnAttemptFixture]::Run()