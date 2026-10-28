// AUTHORED_NOT_RUN. Actual terminal/grant/hold/settlement/native apply/WAL.
// Native authentication, wrapper path selection and physiology IO are adapters.
internal static partial class RebirthHydrationTransferJournal {
 private static void RequireSafe(string path){for(string current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current)){if(Directory.Exists(current)&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("fixture completion reparse path");}}
 private static string DirectoryPath(Guid world,string owner,string creation)=>Path.Combine(GameIO.Root,"terminal",world.ToString("N"));
 private static Guid Group(Guid world,string owner,string creation)=>world;
 private static bool SamePath(string a,string b)=>a==b;
 private static RebirthCamperTransferJournal Open(RebirthHydrationTransferOriginal original,bool create){var path=DirectoryPath(original.World,original.Owner,original.Creation);return create||Directory.Exists(path)?new RebirthCamperTransferJournal(path):null;}
 private static byte[] Bytes(RebirthHydrationTransferOriginal original)=>System.Text.Encoding.UTF8.GetBytes(original.Write().ToString(SaveOptions.DisableFormatting));
 private static bool Matches(RebirthCamperTransferJournal.Record record,RebirthHydrationTransferOriginal original)=>record!=null&&record.Operation==original.Operation&&record.Vehicle==original.World&&record.OwnerCreation==original.Creation&&record.Revision==original.Revision&&record.Before.SequenceEqual(Bytes(original))&&record.After.SequenceEqual(Bytes(original));
 internal static bool Prepare=true,Attempt=true;internal static int Prepares,Attempts;internal static Action DuringAttempt;
 internal static bool TryPrepare(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out string error){error=null;Prepares++;if(!Prepare)return false;var journal=Open(original,true);var data=Bytes(original);return Matches(journal.Prepare(original.Operation,original.World,original.Creation,original.Revision,data,data),original);}
 internal static bool TryMarkAttempted(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out string error){error=null;Attempts++;if(!Attempt)return false;DuringAttempt?.Invoke();return Matches(Open(original,false).Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain),original);}
}
internal sealed class IndexAdapter {
 internal RebirthCamperTransferJournal.Record FindPending(Guid group)=>null;
 internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid group,string creation)=>new RebirthCamperTransferJournal.Record[0];
 internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid group,string creation)=>new RebirthCamperTransferJournal.Record[0];
}
internal static class RebirthCamperTransferHistoryScope {internal static bool TryGet(RebirthCamperTransferJournal journal,out IndexAdapter index){index=null;return false;}}
// PROGRAM_PREFIX
 private static RebirthHydrationTransferGrant AppliedTerminal(){var grant=Ready();Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied,"owner leg unavailable");RebirthHydrationOwnerCheckpoint.SavedApplied=true;return grant;}
 public static void Main(){
  var grant=AppliedTerminal();Need(grant.TryCompleteLocal()&&!grant.Current()&&!RebirthHydrationOwnerHold.IsHeld(player)&&!RebirthHydrationOwnerHold.BlocksInventory(player.bag,true),"joint saved terminal did not retire exact custody");
  Need(!grant.TryCompleteLocal()&&NativeWrites.Calls==1,"retired grant replayed financial work");
  grant=AppliedTerminal();RebirthMetabolismStateRepository.SaveAllowed=false;Need(!grant.TryCompleteLocal()&&grant.Current()&&RebirthHydrationOwnerHold.IsHeld(player),"failed metabolism save released hold");
  RebirthMetabolismStateRepository.SaveAllowed=true;Need(grant.TryCompleteLocal()&&NativeWrites.Calls==1,"save retry could not release or repeated debit");
  grant=AppliedTerminal();Action<string,RebirthCamperTransferJournal.Record,bool> fault=(path,record,published)=>{if(record.State==RebirthCamperTransferJournal.Phase.Applied&&published)throw new IOException("unknown terminal publication");};
  RebirthCamperTransferJournal.PublicationFinished+=fault;try{Need(!grant.TryCompleteLocal()&&grant.Current()&&RebirthHydrationOwnerHold.IsHeld(player),"unknown terminal publication released custody");}finally{RebirthCamperTransferJournal.PublicationFinished-=fault;}
  Need(grant.TryCompleteLocal()&&NativeWrites.Calls==1,"published terminal could not finish metadata-only retry");
  grant=AppliedTerminal();fault=(path,record,published)=>{if(path.Contains("HydrationLocalCompletions")&&record.State==RebirthCamperTransferJournal.Phase.Applied&&published)throw new IOException("unknown local completion publication");};
  RebirthCamperTransferJournal.PublicationFinished+=fault;try{Need(!grant.TryCompleteLocal()&&grant.Current()&&RebirthHydrationOwnerHold.IsHeld(player),"unknown saved local closure released custody");}finally{RebirthCamperTransferJournal.PublicationFinished-=fault;}
  Need(grant.TryCompleteLocal()&&NativeWrites.Calls==1,"saved local closure retry repeated native application or could not retire");
  grant=AppliedTerminal();Need(RebirthHydrationTransferJournal.LocalTerminal.TryPublish(grant,out var terminal),"qualified terminal capability unavailable");
  player.world.Primary=null;Need(!RebirthHydrationOwnerHold.TryReleaseLocal(terminal)&&RebirthHydrationOwnerHold.IsHeld(player),"stale terminal capability released original native hold");
  grant=AppliedTerminal();Need(RebirthHydrationTransferJournal.LocalTerminal.TryPublish(grant,out terminal),"second terminal unavailable");
  RebirthHydrationOwnerCheckpoint.SavedApplied=false;Need(!RebirthHydrationOwnerHold.TryReleaseLocal(terminal)&&RebirthHydrationOwnerHold.IsHeld(player),"lost saved owner witness released custody");
  Console.WriteLine("PASS actual warm local terminal and release with real WAL; production wrapper auth/PDS/physiology schema/remote recovery NOT RUN");
 }
}
