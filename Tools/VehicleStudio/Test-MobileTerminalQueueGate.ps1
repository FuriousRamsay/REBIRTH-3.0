#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationQueuedAuthority.cs'))
$camperStart=$camperSource.IndexOf('        internal bool IsCurrent()')
$camperEnd=$camperSource.IndexOf('        // Owner/installation/publication scope only.',$camperStart)
if($camperStart -lt 0 -or $camperEnd -le $camperStart){throw 'Terminal queue gate missing.'}
$camperFixture=@'
using System;using System.Collections.Generic;
public class Admission { public string JobId="job"; }
public class Progression { public Dictionary<string,object> StationCancellationAttempts=new Dictionary<string,object>(),StationCancellationRefunds=new Dictionary<string,object>(); }
public class TerminalGateFixture {
 private Admission Admission=new Admission();private Progression Progression=new Progression();private bool Owner=true,Choice=true,ThrowChoice;
 private bool IsOwnerCurrent()=>Owner;
 private bool MatchesCompletionIntent(){if(ThrowChoice)throw new Exception("invalid choice adapter");return Choice;}
METHOD_BODY
 private static void Check(bool b,string n){if(!b)throw new Exception(n);}
 public static void Run(){var f=new TerminalGateFixture();Check(f.IsCurrent(),"original owner and choice accepted");
  f.Progression.StationCancellationAttempts.Add("job",new object());Check(!f.IsCurrent(),"attempted cancellation holds processing");f.Progression.StationCancellationAttempts.Clear();
  f.Progression.StationCancellationRefunds.Add("job",new object());Check(!f.IsCurrent(),"refund custody holds processing");f.Progression.StationCancellationRefunds.Clear();
  f.Choice=false;Check(!f.IsCurrent(),"foreign terminal choice denied");f.Choice=true;f.ThrowChoice=true;Check(!f.IsCurrent(),"choice exception fails closed");f.ThrowChoice=false;
  f.Owner=false;Check(!f.IsCurrent(),"stale owner denied");
  Console.WriteLine("PASS actual terminal queue gate: owner/choice required, cancellation attempt/refund hold and malformed choice fails closed. Owner/custody adapters doubled; no gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('METHOD_BODY',$camperSource.Substring($camperStart,$camperEnd-$camperStart))
[TerminalGateFixture]::Run()
