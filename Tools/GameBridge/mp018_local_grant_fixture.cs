// AUTHORED_NOT_RUN. Actual local grant/hold/original/planner; the saved
// checkpoint, journal and metabolism/native item services below are adapters.
internal sealed class ItemValue {internal string Data="";}
internal sealed class RebirthMetabolismState {internal string SurvivorCreationId;internal int Revision;internal ItemStack HydrationSlotItem=new ItemStack();internal RebirthHydrationTransferOriginal HydrationTransferReceipt;}
internal static class RebirthMetabolismStateRepository {
 internal static RebirthMetabolismState State;internal static bool TryGet(EntityPlayerLocal player,out RebirthMetabolismState state){state=State;return state!=null;}
}
internal static class RebirthNativeItemCodec {
 internal static string Encode(ItemValue value)=>value.Data;
 internal static bool TryDecode(string data,out ItemValue value){value=new ItemValue{Data=data};return !string.IsNullOrEmpty(data);}
}
internal sealed class Definition {internal bool IsDrink=true,HydrationEquippable=true;}
internal static class RebirthConsumableResolver {internal static Definition Value=new Definition();internal static bool TryResolve(ItemValue value,out Definition definition){definition=Value;return definition!=null;}}
internal sealed class Support {internal Dictionary<string,string> EquippedGearBySlot=new Dictionary<string,string>();}
internal sealed class Character {internal Support Support=new Support();}
internal static class RebirthWorldCharacterService {internal static Character Value=new Character();internal static bool TryGet(EntityPlayerLocal player,out Character character){character=Value;return character!=null;}}
internal static class RebirthSurvivorGearService {internal const string SupportSlotId="support";}
internal static class RebirthHydrationOwnerCheckpoint {internal static bool Saved=true;internal static bool HasSavedBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>Saved;}
internal static class RebirthHydrationTransferJournal {
 internal static bool Prepare=true,Attempt=true;internal static int Prepares,Attempts;internal static Action DuringAttempt;
 internal static bool TryPrepare(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out string error){Prepares++;error=null;return Prepare;}
 internal static bool TryMarkAttempted(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out string error){Attempts++;error=null;DuringAttempt?.Invoke();return Attempt;}
}
internal static class Program {
 private static EntityPlayerLocal player;private static RebirthHydrationTransferOriginal original;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static ItemStack[] Cells(int length){var cells=new ItemStack[length];for(int i=0;i<length;i++)cells[i]=new ItemStack();return cells;}
 private static void Fresh(){
  var world=new World();player=new EntityPlayerLocal{world=world,bag=new Bag{ItemGrid=new Grid{items=Cells(44)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=Cells(20)}}};world.Actor=world.Primary=player;GameManager.Instance.World=world;
  ThreadManager.Main=true;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();RebirthStablePlayerIdentity.Owner="account";RebirthRemoteResourceOwnerAction.Allowed=true;RebirthDogLifecycleService.Held=false;RemoteResourceClientTransactionCoordinator.Pending=false;RebirthToolbeltCapacity.Owned=4;RebirthGearInventorySnapshot.DuringCapture=null;
  player.bag.ItemGrid.items[3]=new ItemStack{count=3,Data=Convert.ToBase64String(new byte[]{7,9,3,25})};
  Need(RebirthGearInventorySnapshot.TryCapture(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,4,out var snapshot)&&RebirthHydrationTransferPlan.TryCreate(snapshot.Bag,snapshot.Belt,null,4,new Stack(),true,3,out var plan)&&RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),Guid.Parse(world.Guid),"account",player.Creation,2,8,4,null,plan,out original)&&RebirthHydrationOwnerHold.TryAcquire(player,original),"fixture original hold unavailable");
  RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=player.Creation,Revision=8};RebirthConsumableResolver.Value=new Definition();RebirthWorldCharacterService.Value=new Character();RebirthHydrationOwnerCheckpoint.Saved=true;
  RebirthHydrationTransferJournal.Prepare=RebirthHydrationTransferJournal.Attempt=true;RebirthHydrationTransferJournal.Prepares=RebirthHydrationTransferJournal.Attempts=0;RebirthHydrationTransferJournal.DuringAttempt=null;
 }
 public static void Main(){
  Fresh();Need(RebirthHydrationTransferGrant.TryIssueLocal(player,original,out var grant)&&grant.Current(),"qualified local grant refused");
  Need(player.bag.ItemGrid.items[3].count==3&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"grant mutated a financial leg");
  Need(RebirthHydrationTransferGrant.TryIssueLocal(player,original,out var retained)&&ReferenceEquals(grant,retained)&&RebirthHydrationTransferJournal.Attempts==1,"warm grant repeated original attempt or changed identity");
  RebirthMetabolismStateRepository.State.Revision++;Need(grant.Current(),"unrelated timer revision revoked unchanged warm source");
  RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=player.Creation,Revision=8};Need(!grant.Current()&&RebirthHydrationOwnerHold.IsHeld(player),"replacement source inherited grant or released custody");
  for(int variant=0;variant<9;variant++){
   Fresh();switch(variant){case 0:RebirthHydrationOwnerCheckpoint.Saved=false;break;case 1:RebirthHydrationTransferJournal.Prepare=false;break;case 2:RebirthHydrationTransferJournal.Attempt=false;break;case 3:RebirthMetabolismStateRepository.State.Revision++;break;case 4:RebirthMetabolismStateRepository.State.HydrationSlotItem=new ItemStack{count=1,Data=player.bag.ItemGrid.items[3].Data};break;case 5:RebirthConsumableResolver.Value.IsDrink=false;break;case 6:RebirthConsumableResolver.Value.HydrationEquippable=false;break;case 7:RebirthWorldCharacterService.Value.Support.EquippedGearBySlot["support"]="occupied";break;case 8:SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;break;}
   Need(!RebirthHydrationTransferGrant.TryIssueLocal(player,original,out _)&&player.bag.ItemGrid.items[3].count==3&&RebirthHydrationOwnerHold.IsHeld(player),"unqualified local grant or custody loss "+variant);
   if(variant==8)Need(RebirthHydrationTransferJournal.Prepares==0&&RebirthHydrationTransferJournal.Attempts==0,"nonserver local producer reached durable attempt publication");
  }
  Fresh();RebirthHydrationTransferJournal.DuringAttempt=()=>RebirthMetabolismStateRepository.State.Revision++;
  Need(!RebirthHydrationTransferGrant.TryIssueLocal(player,original,out _)&&RebirthHydrationTransferJournal.Attempts==1&&RebirthHydrationOwnerHold.IsHeld(player),"source change during publication minted grant or discarded custody");
  Fresh();RebirthHydrationTransferJournal.DuringAttempt=()=>RebirthHydrationOwnerCheckpoint.Saved=false;
  Need(!RebirthHydrationTransferGrant.TryIssueLocal(player,original,out _),"lost saved Before during publication minted grant");
  Console.WriteLine("PASS actual local hydration grant and hold; native persistence/remote topology remain separate NOT RUN evidence");
 }
}
