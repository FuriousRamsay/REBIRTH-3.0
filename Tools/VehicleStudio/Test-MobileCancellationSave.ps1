#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationCancellationSave.cs')).Replace('using System;','')
$camperFixture=@'
using System;using System.Collections.Generic;
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public class Admission { public string JobId="job"; }
public class Progression { public Dictionary<string,object> StationTerminalIntents=new Dictionary<string,object>(),StationCancellationRefunds=new Dictionary<string,object>(),StationCancellationAttempts=new Dictionary<string,object>(); }
public class Binding { public Progression Progression=new Progression();public Admission Admission=new Admission();public object Owner=new object(),Player=new object(),Identity=new object(); }
public class RebirthCamperStationCancellationCheckpoint {
 public Binding Binding=new Binding();public bool Valid=true;private object choice=new object(),refund=new object(),attempt=new object();
 public bool MatchesBefore()=>Valid&&Binding.Progression.StationTerminalIntents.Count==0;
 public bool MatchesStagedBefore()=>Valid&&Binding.Progression.StationTerminalIntents.TryGetValue("job",out var c)&&ReferenceEquals(c,choice)&&Binding.Progression.StationCancellationRefunds.TryGetValue("job",out var r)&&ReferenceEquals(r,refund)&&Binding.Progression.StationCancellationAttempts.TryGetValue("job",out var a)&&ReferenceEquals(a,attempt);
 public object CopyIntent()=>choice;public object CopyRefund()=>refund;public object CopyAttempt()=>attempt;
 public bool MatchesStagedOwner()=>MatchesStagedBefore();
}
public static class RebirthWorldCharacterService { public static bool Flush=true,ThrowDirty;public static int Saves;public static Action OnFlush;public static void MarkDirty(object owner,string reason){if(ThrowDirty)throw new Exception("dirty notification failure");}public static bool FlushPlayer(object p,string reason){Saves++;OnFlush?.Invoke();return Flush;} }
public static class RebirthWorldCharacterRepository { public static bool Saved;public static bool HasSavedStationCancellationAttempt(object identity,Admission admission,object choice,object refund,object attempt)=>Saved; }
public static class CancellationSaveFixture {
 private static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){var c=new RebirthCamperStationCancellationCheckpoint();
  Check(RebirthCamperStationCancellationSave.TryBegin(c,out var op)&&op!=null&&RebirthWorldCharacterService.Saves==0&&c.Binding.Progression.StationCancellationAttempts.Count==1,"staging is not a save or physical effect");
  RebirthWorldCharacterService.Flush=false;Check(!op.TrySave(),"failed save remains pending");RebirthWorldCharacterService.Flush=true;Check(!op.TrySave(),"requested save without primary readback remains pending");RebirthWorldCharacterRepository.Saved=true;
  Check(op.TrySave()&&op.IsConfirmedFor(c),"exact retained proposal confirmed");int saves=RebirthWorldCharacterService.Saves;Check(op.TrySave()&&RebirthWorldCharacterService.Saves==saves,"confirmed observation does not save repeatedly");
  Check(!op.IsConfirmedFor(new RebirthCamperStationCancellationCheckpoint()),"foreign proposal denied");c.Binding.Progression.StationCancellationRefunds["job"]=new object();Check(!op.TrySave(),"changed cached refund denied");
  c=new RebirthCamperStationCancellationCheckpoint();RebirthWorldCharacterService.ThrowDirty=true;Check(!RebirthCamperStationCancellationSave.TryBegin(c,out op)&&op!=null&&c.Binding.Progression.StationCancellationAttempts.Count==1,"dirty failure retains original operation and records");RebirthWorldCharacterService.ThrowDirty=false;Check(op.TrySave(),"same operation retries notification/save");
  c=new RebirthCamperStationCancellationCheckpoint();Check(RebirthCamperStationCancellationSave.TryBegin(c,out op),"fresh proposal stage");RebirthWorldCharacterService.OnFlush=()=>c.Valid=false;Check(!op.TrySave(),"authority changed during save denied");RebirthWorldCharacterService.OnFlush=null;
  c=new RebirthCamperStationCancellationCheckpoint();for(int i=0;i<64;i++)c.Binding.Progression.StationCancellationRefunds.Add("other"+i,new object());Check(!RebirthCamperStationCancellationSave.TryBegin(c,out op)&&op==null&&c.Binding.Progression.StationTerminalIntents.Count==0,"capacity refused before any staging");
  ThreadManager.Main=false;c=new RebirthCamperStationCancellationCheckpoint();Check(!RebirthCamperStationCancellationSave.TryBegin(c,out op),"off-thread denied");
  Console.WriteLine("PASS actual retained cancellation save: staged records, failed save/readback, same-operation retry, exact proposal, changed custody/authority, notification failure, capacity and thread. Owner/checkpoint/file adapters doubled; no native cancellation/refund or gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperSource)
[CancellationSaveFixture]::Run()
