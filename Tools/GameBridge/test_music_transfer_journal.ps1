$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '../../Scripts/Survivor'
$state = Get-Content -Raw (Join-Path $root 'Domain/RebirthMusicTransferState.cs')
$journal = Get-Content -Raw (Join-Path $root 'Support/RebirthMusicTransferJournal.cs')
$scope = Get-Content -Raw (Join-Path $root 'Network/RebirthSurvivorRequestScope.cs')
$source = $state + ($scope -replace 'using System;', '') + ($journal -replace 'using System;', '') + @'
public class RebirthMusicCassetteState { public string ItemId, ItemData; }
public class RebirthWorldSupportState {
 public object PendingGearTransfer;
 public object PendingLibraryTransfer;
 public long MusicRevision;
 public RebirthMusicTransferState PendingMusicTransfer;
 public System.Collections.Generic.List<RebirthMusicCassetteState> MusicCassettes=new System.Collections.Generic.List<RebirthMusicCassetteState>();
}
public static class RebirthMusicLibraryService {
 public const int Capacity=24;
 public static bool IsMusicCassette(string id){return id=="FuriousRamsayCassette01";}
}
public static class MusicJournalChecks {
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 public static void Run(){
  foreach(var creation in new[]{Guid.NewGuid().ToString("N"),"legacy-"+new string('a',64)}){
  var s=new RebirthWorldSupportState();
  var t=new RebirthMusicTransferState{TransactionId=Guid.NewGuid().ToString("N"),CreationId=creation,Operation=1,ItemId="FuriousRamsayCassette01",ItemData="AQID"};
  s.PendingLibraryTransfer=new object();Check(!RebirthMusicTransferJournal.Prepare(s,t,()=>{throw new Exception("library conflict attempted save");}),"library conflict accepted");s.PendingLibraryTransfer=null;
  s.PendingGearTransfer=new object();Check(!RebirthMusicTransferJournal.Prepare(s,t,()=>{throw new Exception("conflict attempted save");}),"gear conflict accepted");s.PendingGearTransfer=null;
  Check(!RebirthMusicTransferJournal.Prepare(s,t,()=>false)&&s.PendingMusicTransfer==null,"failed preparation rollback");
  Check(RebirthMusicTransferJournal.Prepare(s,t,()=>true)&&s.MusicCassettes.Count==0,"prepare changes no ownership");
  var changed=t.Clone();changed.ItemData="BAUG";
  Check(!RebirthMusicTransferJournal.Prepare(s,changed,()=>true),"same ID altered payload");
  Check(!RebirthMusicTransferJournal.Commit(s,"wrong",()=>true),"wrong ACK");
  Check(!RebirthMusicTransferJournal.Commit(s,t.TransactionId,()=>false)&&s.PendingMusicTransfer!=null&&s.MusicRevision==0&&s.MusicCassettes.Count==0,"failed commit rollback");
  Check(RebirthMusicTransferJournal.Commit(s,t.TransactionId,()=>true)&&s.MusicRevision==1&&s.MusicCassettes.Count==1,"insert commit");
  Check(!RebirthMusicTransferJournal.Commit(s,t.TransactionId,()=>true)&&s.MusicCassettes.Count==1,"duplicate ACK");
  var removal=t.Clone();removal.TransactionId=Guid.NewGuid().ToString("N");removal.Operation=2;removal.ExpectedRevision=1;
  Check(RebirthMusicTransferJournal.Prepare(s,removal,()=>true),"remove prepare");
  try{RebirthMusicTransferJournal.Commit(s,removal.TransactionId,()=>{throw new Exception("simulated save error");});throw new Exception("no save exception");}
  catch(Exception e){if(e.Message!="simulated save error")throw;}
  Check(s.PendingMusicTransfer!=null&&s.MusicCassettes.Count==1&&s.MusicRevision==1,"exception rollback");
  Check(RebirthMusicTransferJournal.Commit(s,removal.TransactionId,()=>true)&&s.MusicCassettes.Count==0&&s.MusicRevision==2,"remove retry");
  t.TransactionId=Guid.NewGuid().ToString("N");t.ExpectedRevision=2;
  Check(RebirthMusicTransferJournal.Prepare(s,t,()=>true),"cancel fixture");
  Check(!RebirthMusicTransferJournal.CancelRejected(s,"wrong",()=>true)&&s.PendingMusicTransfer!=null,"wrong cancellation");
  Check(!RebirthMusicTransferJournal.CancelRejected(s,t.TransactionId,()=>false)&&s.PendingMusicTransfer!=null&&s.MusicRevision==2,"cancel save rollback");
  Check(RebirthMusicTransferJournal.CancelRejected(s,t.TransactionId,()=>true)&&s.PendingMusicTransfer==null&&s.MusicRevision==3&&s.MusicCassettes.Count==0,"cancel preserved ownership");
  Check(!RebirthMusicTransferJournal.CancelRejected(s,t.TransactionId,()=>true)&&s.MusicRevision==3,"duplicate cancel");
  }
 }
}
'@
Add-Type -TypeDefinition $source
[MusicJournalChecks]::Run()
Write-Output 'PASS: production journal save failures, exception recovery, ownership transitions and duplicate ACK checks. Save/network adapters stubbed.'

