[Flags]enum RebirthNpcCapabilities {Inventory=1}
[Flags]enum RebirthNpcInventoryAuthorityOperations {Mutate=1,Transfer=2}
class RebirthNpcProfile {public bool Has(RebirthNpcCapabilities capability){return true;}}
class RebirthNpcRuntimeState {public string ProfileId="survivor.ambient";}
class RebirthNpcProfileRegistry {public static bool TryResolve(string id,out RebirthNpcProfile profile){profile=new RebirthNpcProfile();return true;}}
class RebirthNpcRuntimeRegistry {
 public static Dictionary<RebirthNpcStableId,int> Entities=new Dictionary<RebirthNpcStableId,int>();
 public static bool TryGetEntityId(RebirthNpcStableId id,out int entityId){return Entities.TryGetValue(id,out entityId);}
 public static bool TryGet(int id,out RebirthNpcRuntimeState state){state=new RebirthNpcRuntimeState();return true;}
}
class RebirthNpcInventoryAuthorityService {public static bool Allowed=true;public static bool Validate(string key,RebirthNpcInventoryAuthorityOperations op,RebirthNpcStableId a,RebirthNpcStableId b=default(RebirthNpcStableId)){return Allowed;}}
class RebirthNpcInventoryPersistenceStore {public static void EnsureLoaded(){}public static void MarkDirty(){}}
public class TypedTransactionFixture {
 public static string Run(){int checks=0;var owner=RebirthNpcStableId.NewId();var destination=RebirthNpcStableId.NewId();RebirthNpcRuntimeRegistry.Entities.Add(owner,1);RebirthNpcRuntimeRegistry.Entities.Add(destination,2);
 RebirthNpcNativeStackRecord item;RebirthNpcNativeStackRecord.TryCapture(Guid.NewGuid(),owner,new ItemStack(new ItemValue{ItemClass=new ItemClass{Name="weapon"},Metadata="mods",Quality=5,Wear=7},3),out item);
 RebirthNpcNativeStackSet set;RebirthNpcNativeStackSet.TryCreate(owner,7,new[]{item},out set);string error;
 var initial=new RebirthNpcInventoryPersistentRecord{NpcId=owner,Revision=7,Quantities=new Dictionary<string,int>{{"weapon",5}},Reservations=new Dictionary<string,int>{{"weapon",1}},ReplayJournal=new Guid[0],NativeStacks=set};
 Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;};
 check(RebirthNpcInventoryTransactionService.TryRestorePersistentRecord(initial,out error),"restore exact set");uint revision;
 Func<Guid,int,RebirthNpcInventoryTransaction> transaction=(id,delta)=>new RebirthNpcInventoryTransaction(id,owner,7,"test",new[]{new RebirthNpcInventoryMutation("weapon",delta)});
 check(RebirthNpcInventoryTransactionService.Apply(transaction(Guid.NewGuid(),-2),out revision)==RebirthNpcInventoryTransactionResult.ReservationConflict,"cannot overlap typed and reserved stock");
 var before=RebirthNpcInventoryTransactionService.GetSnapshot(owner);check(before.Revision==7&&before.Quantities["weapon"]==5&&before.NativeStacks.Revision==7,"refusal preserves state");
 var accepted=transaction(Guid.NewGuid(),-1);check(RebirthNpcInventoryTransactionService.Apply(accepted,out revision)==RebirthNpcInventoryTransactionResult.Applied&&revision==8,"untyped debit succeeds");
 var after=RebirthNpcInventoryTransactionService.GetSnapshot(owner);check(after.NativeStacks.Revision==8&&after.NativeStacks.GetQuantity("weapon")==3&&after.Quantities["weapon"]==4,"typed set retained with new revision");
 check(RebirthNpcInventoryTransactionService.Apply(accepted,out revision)==RebirthNpcInventoryTransactionResult.Replayed&&RebirthNpcInventoryTransactionService.GetSnapshot(owner).Quantities["weapon"]==4,"accepted debit replay has no effect");
 check(!RebirthNpcInventoryTransactionService.TryReserve(owner,"weapon",1),"reserve cannot include typed stock");
 uint sourceRevision,destinationRevision;var transfer=new RebirthNpcInventoryTransfer(Guid.NewGuid(),owner,destination,8,0,"weapon",1,"test");
 check(RebirthNpcInventoryTransactionService.ApplyTransfer(transfer,out sourceRevision,out destinationRevision)==RebirthNpcInventoryTransferResult.ReservationConflict,"transfer protects typed plus reserved stock");
 RebirthNpcInventoryTransactionService.ReleaseReservation(owner,"weapon",1);
 check(RebirthNpcInventoryTransactionService.ApplyTransfer(transfer,out sourceRevision,out destinationRevision)==RebirthNpcInventoryTransferResult.Applied&&sourceRevision==9&&destinationRevision==1,"untyped transfer succeeds after reservation release");
 check(RebirthNpcInventoryTransactionService.GetSnapshot(owner).NativeStacks.GetQuantity("weapon")==3&&RebirthNpcInventoryTransactionService.GetSnapshot(destination).NativeStacks==null,"typed ownership not silently moved");
 check(RebirthNpcInventoryTransactionService.ApplyTransfer(transfer,out sourceRevision,out destinationRevision)==RebirthNpcInventoryTransferResult.Replayed,"transfer replay refused mutation");
 var typedTransfer=new RebirthNpcInventoryTransfer(Guid.NewGuid(),owner,destination,9,1,"weapon",1,"test");check(RebirthNpcInventoryTransactionService.ApplyTransfer(typedTransfer,out sourceRevision,out destinationRevision)==RebirthNpcInventoryTransferResult.ReservationConflict,"name transfer cannot spend exact instance");
 var records=RebirthNpcInventoryTransactionService.CapturePersistentRecords();check(Array.Exists(records,r=>r.NpcId==owner&&r.NativeStacks!=null&&r.NativeStacks.Revision==r.Revision),"recapture preserves exact revision");
 RebirthNpcInventoryAuthorityService.Allowed=false;
 var denied=new RebirthNpcInventoryTransaction(Guid.NewGuid(),owner,9,"test",new[]{new RebirthNpcInventoryMutation("weapon",1)});
 check(RebirthNpcInventoryTransactionService.Apply(denied,out revision)==RebirthNpcInventoryTransactionResult.AuthorityDenied,"authority denial prevents credit");
 check(RebirthNpcInventoryTransactionService.ApplyTransfer(new RebirthNpcInventoryTransfer(Guid.NewGuid(),owner,destination,9,1,"weapon",1,"test"),out sourceRevision,out destinationRevision)==RebirthNpcInventoryTransferResult.AuthorityDenied,"authority denial prevents transfer");
 RebirthNpcInventoryAuthorityService.Allowed=true;
 check(RebirthNpcInventoryTransactionService.Apply(new RebirthNpcInventoryTransaction(Guid.NewGuid(),owner,8,"test",new[]{new RebirthNpcInventoryMutation("weapon",1)}),out revision)==RebirthNpcInventoryTransactionResult.RevisionConflict,"stale mutation rejected");
 check(RebirthNpcInventoryTransactionService.ApplyTransfer(new RebirthNpcInventoryTransfer(Guid.NewGuid(),owner,destination,8,1,"weapon",1,"test"),out sourceRevision,out destinationRevision)==RebirthNpcInventoryTransferResult.RevisionConflict,"stale source transfer rejected");
 check(RebirthNpcInventoryTransactionService.ApplyTransfer(new RebirthNpcInventoryTransfer(Guid.NewGuid(),owner,destination,9,0,"weapon",1,"test"),out sourceRevision,out destinationRevision)==RebirthNpcInventoryTransferResult.RevisionConflict,"stale destination transfer rejected");
 var finalSource=RebirthNpcInventoryTransactionService.GetSnapshot(owner);var finalDestination=RebirthNpcInventoryTransactionService.GetSnapshot(destination);
 check(finalSource.Revision==9&&finalSource.Quantities["weapon"]==3&&finalSource.NativeStacks.Revision==9&&finalDestination.Revision==1&&finalDestination.Quantities["weapon"]==1,"all denied and stale operations preserve both owners");
 var exhausted=RebirthNpcStableId.NewId();RebirthNpcRuntimeRegistry.Entities.Add(exhausted,3);var knownReplay=Guid.NewGuid();
 var exhaustedRecord=new RebirthNpcInventoryPersistentRecord{NpcId=exhausted,Revision=uint.MaxValue,Quantities=new Dictionary<string,int>{{"weapon",2}},Reservations=new Dictionary<string,int>(),ReplayJournal=new[]{knownReplay}};
 check(RebirthNpcInventoryTransactionService.TryRestorePersistentRecord(exhaustedRecord,out error),"legacy maximum revision still loads");
 check(RebirthNpcInventoryTransactionService.Apply(new RebirthNpcInventoryTransaction(Guid.NewGuid(),exhausted,uint.MaxValue,"test",new[]{new RebirthNpcInventoryMutation("weapon",1)}),out revision)==RebirthNpcInventoryTransactionResult.RevisionConflict&&revision==uint.MaxValue,"mutation cannot wrap revision");
 check(RebirthNpcInventoryTransactionService.ApplyTransfer(new RebirthNpcInventoryTransfer(Guid.NewGuid(),exhausted,destination,uint.MaxValue,1,"weapon",1,"test"),out sourceRevision,out destinationRevision)==RebirthNpcInventoryTransferResult.RevisionConflict,"source revision cannot wrap");
 check(RebirthNpcInventoryTransactionService.ApplyTransfer(new RebirthNpcInventoryTransfer(Guid.NewGuid(),destination,exhausted,1,uint.MaxValue,"weapon",1,"test"),out sourceRevision,out destinationRevision)==RebirthNpcInventoryTransferResult.RevisionConflict,"destination revision cannot wrap");
 check(RebirthNpcInventoryTransactionService.Apply(new RebirthNpcInventoryTransaction(knownReplay,exhausted,uint.MaxValue,"test",new[]{new RebirthNpcInventoryMutation("weapon",1)}),out revision)==RebirthNpcInventoryTransactionResult.Replayed,"known replay still acknowledged at exhausted revision");
 check(RebirthNpcInventoryTransactionService.GetSnapshot(exhausted).Quantities["weapon"]==2&&RebirthNpcInventoryTransactionService.GetSnapshot(exhausted).Revision==uint.MaxValue&&RebirthNpcInventoryTransactionService.GetSnapshot(destination).Quantities["weapon"]==1,"exhausted operations preserve both stocks");
 return "PASS "+checks+" whole production inventory transaction/stack/set checks; native registry, capabilities, authority and persistence adapters doubled";
 }
}