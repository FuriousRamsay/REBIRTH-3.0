// Actual conductor, immutable intent/WAL, native receipt parser/checkpoint,
// owner hold, native apply/permit, saved source qualification and terminal.
// Native inventory/PDS IO, physiology schema, identity and initial warm/cold
// metadata-only shortcut selection remain explicit adapters.
internal sealed class ItemValue {internal string Data="";internal bool IsEmpty()=>string.IsNullOrEmpty(Data);internal object ItemClass=>this;}
internal sealed class RebirthMetabolismState {internal string SurvivorCreationId;internal int Revision;internal ItemStack HydrationSlotItem=new ItemStack();internal RebirthHydrationTransferOriginal HydrationTransferReceipt;internal void Touch(){Revision++;}}
internal static class RebirthNativeItemCodec {internal static string Encode(ItemValue value)=>value.Data;internal static bool TryDecode(string data,out ItemValue value){value=new ItemValue{Data=data};return !string.IsNullOrEmpty(data);}}
internal sealed class Definition {internal bool IsDrink=true,HydrationEquippable=true;}
internal static class RebirthConsumableResolver {internal static bool TryResolve(ItemValue value,out Definition definition){definition=new Definition();return !value.IsEmpty();}}
internal sealed class Support {internal Dictionary<string,string> EquippedGearBySlot=new Dictionary<string,string>();}
internal sealed class Character {internal Support Support=new Support();}
internal static class RebirthWorldCharacterService {internal static bool TryGet(EntityPlayerLocal player,out Character character){character=new Character();return true;}}
internal static class RebirthSurvivorGearService {internal const string SupportSlotId="support";}
internal sealed class ClientInfo {internal bool loginDone=true,disconnecting;}
internal sealed class ClientList {internal ClientInfo ForEntityId(int id)=>null;}
internal static class RebirthMetabolismService {internal static void SendSnapshotToOwner(EntityPlayerLocal player,bool force){}}
internal static class RebirthHydrationClientOriginals {internal static bool TryRead(EntityPlayerLocal player,out RebirthHydrationTransferOriginal original){original=null;return false;}}
internal static class RebirthHydrationRemoteGrant {internal static bool HasIssued(Guid operation)=>false;}
internal static class RebirthHydrationRemoteClientGrant {internal static bool HasIssued(Guid operation)=>false;internal static bool RetireHistorical(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>false;}
internal static class RebirthHydrationRemoteClientTerminal {internal static bool TryGetAcknowledgment(EntityPlayerLocal player,Guid operation,string digest,out RebirthHydrationTransferOriginal original){original=null;return false;}}
internal static class RebirthHydrationTerminalSeenCheckpoint {internal static bool HasSavedHistorical(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>false;}
internal static class RebirthHydrationServerSourceHold {internal static bool HoldsPlayer(EntityPlayerLocal player)=>false;}
internal static class RebirthHydrationLocalRefusedCheckpoint {internal static bool TryPersist(RebirthHydrationTransferJournal.LocalPreparedRejection refusal)=>false;internal static bool HasSaved(RebirthHydrationTransferJournal.LocalPreparedRejection refusal)=>false;}
internal sealed class RebirthHydrationColdRecoveryLease {internal static bool TryGetRetained(EntityPlayerLocal player,out RebirthHydrationColdRecoveryLease lease){lease=null;return false;}internal static bool TryAcquire(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out RebirthHydrationColdRecoveryLease lease){lease=null;return false;}internal bool TryComplete()=>false;}
internal static partial class RebirthHydrationTransferJournal {internal static bool TryCompleteSavedLocal(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>false;}
internal sealed class IndexAdapter {internal RebirthCamperTransferJournal.Record FindPending(Guid group)=>null;internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid group,string creation)=>new RebirthCamperTransferJournal.Record[0];internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid group,string creation)=>new RebirthCamperTransferJournal.Record[0];}
internal static class RebirthCamperTransferHistoryScope {internal static bool TryGet(RebirthCamperTransferJournal journal,out IndexAdapter index){index=null;return false;}}
internal static class NativeWrites {
 internal static int Calls;internal static void Write(object grid,bool bag,int index,object payload,int count,Action setter){Calls++;if(!RebirthGearNativeWritePermit.TryConsume(bag?RebirthGearNativeWritePermit.Kind.BagSlot:RebirthGearNativeWritePermit.Kind.BeltSlot,grid,index,payload,count))throw new InvalidOperationException("native permit absent");setter();}
}
internal sealed partial class Bag {internal void SetSlot(int index,ItemStack stack)=>NativeWrites.Write(this,true,index,stack,stack.count,()=>ItemGrid.items[index]=stack);}
internal sealed partial class Inventory {internal void SetItem(int index,ItemValue value,int count)=>NativeWrites.Write(this,false,index,value,count,()=>ItemGrid.items[index]=new ItemStack(value,count));}
internal static class Program {
 private static EntityPlayerLocal player;private static RebirthHydrationTransferOriginal original;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static ItemStack[] Cells(int count)=>Enumerable.Range(0,count).Select(_=>new ItemStack()).ToArray();
 private static void Seed(bool after){var world=new World();player=new EntityPlayerLocal{world=world,bag=new Bag{ItemGrid=new Grid{items=Cells(44)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=Cells(20)}}};
  world.Actor=world.Primary=player;GameManager.Instance.World=world;ThreadManager.Main=true;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true,connectionToServer=null};RebirthStablePlayerIdentity.Owner="account";GameManager.Instance.PersistentOwner="account";RebirthRemoteResourceOwnerAction.Allowed=true;
  player.bag.ItemGrid.items[3]=new ItemStack{count=3,Data=Convert.ToBase64String(new byte[]{7,9,3,25})};
  RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=player.Creation,Revision=8};NativeStore.Suppress=NativeStore.ThrowAfterSave=false;
  Need(RebirthGearInventorySnapshot.TryCapture(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,4,out var snapshot)&&RebirthHydrationTransferPlan.TryCreate(snapshot.Bag,snapshot.Belt,player.bag.LockedSlots,4,new Stack(),true,3,out var plan)&&RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),Guid.Parse(world.Guid),"account",player.Creation,player.Died,8,4,player.bag.LockedSlots,plan,out original),"seed original failed");
  Need(RebirthHydrationOwnerHold.TryAcquire(player,original)&&RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"seed original custody failed");
  RebirthMetabolismStateRepository.Seed();Need(RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying)&&RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _),"seed saved applying/native attempt failed");
  if(after){player.bag.ItemGrid.items[3].count=2;NativeStore.Save(player);} // Scenario setup: saved original setter outcome before receipt1.
  var old=player;var native=NativeStore.File;world=new World{Guid=original.World.ToString("N")};
  player=new EntityPlayerLocal{world=world,Creation=original.Creation,Died=original.Deaths,bag=new Bag{ItemGrid=new Grid{items=NativeStore.Copy(native.Bag)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=NativeStore.Copy(native.Belt)}}};
  player.Buffs.Values["rbGear_"+original.Operation.ToString("N")]=2;player.Buffs.Values[RebirthHydrationOwnerCheckpoint.Marker(original)]=2;
  world.Actor=world.Primary=player;GameManager.Instance.World=world;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true,connectionToServer=null};
  RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=original.Creation,Revision=0};NativeWrites.Calls=0;
  Need(!ReferenceEquals(old,player)&&RebirthHydrationOwnerHold.BlocksInventory(old.bag,true),"old native original evidence lost before reentry");
 }
 public static void Main(){
  Seed(false);Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==1&&player.bag.ItemGrid.items[3].count==2&&RebirthMetabolismStateRepository.State.HydrationSlotItem.count==1&&!RebirthHydrationOwnerHold.IsHeld(player),"local saved Before did not settle one original under server connectionToServer=null");
  Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Refused&&NativeWrites.Calls==1,"retired local reentry replayed native debit");
  Seed(true);Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==0&&player.bag.ItemGrid.items[3].count==2&&RebirthMetabolismStateRepository.State.HydrationSlotItem.count==1,"saved Applying After replayed native debit instead of checkpoint/source completion");
  Seed(false);RebirthMetabolismStateRepository.SaveAllowed=false;
  Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Pending&&NativeWrites.Calls==1&&RebirthHydrationOwnerHold.IsHeld(player),"failed source save released reentry custody");
  RebirthMetabolismStateRepository.SaveAllowed=true;Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==1,"source save retry repeated native debit");
  Seed(false);RebirthMetabolismStateRepository.SaveAllowed=false;
  Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Pending&&NativeWrites.Calls==1,"same-object manager reentry seed failed");
  SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true,connectionToServer=null};RebirthMetabolismStateRepository.SaveAllowed=true;
  Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==1&&!RebirthHydrationOwnerHold.IsHeld(player),"same native player retained pending prevented exact original manager reentry or repeated debit");
  Seed(false);RebirthMetabolismStateRepository.SaveAllowed=false;
  Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Pending&&NativeWrites.Calls==1,"same-object bag reentry seed failed");
  var replaced=player.bag;player.bag=new Bag{ItemGrid=new Grid{items=NativeStore.Copy(replaced.ItemGrid.items)},LockedSlots=new bool[44]};RebirthMetabolismStateRepository.SaveAllowed=true;
  Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==1&&!RebirthHydrationOwnerHold.BlocksInventory(replaced,true),"same native player retained pending prevented exact original bag reentry or lost old custody retirement");
  Seed(false);player.Died++;Need(RebirthHydrationLocalTransfer.TryResume(player)!=RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==0&&player.bag.ItemGrid.items[3].count==3,"ended life inherited old original permission");
  Seed(false);player.bag.ItemGrid.items[3].Data=Convert.ToBase64String(new byte[]{99});Need(RebirthHydrationLocalTransfer.TryResume(player)!=RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==0,"changed rich owner source was overwritten");
  Seed(false);RebirthMetabolismStateRepository.AfterDecode=()=>GameManager.Instance.World=new World();Need(RebirthHydrationLocalTransfer.TryResume(player)!=RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==0,"source-read World replacement authorized original native debit");
  Console.WriteLine("PASS actual local native outcome reentry/owner/source/WAL/terminal; native engine/network endpoints NOT RUN");
 }
}
