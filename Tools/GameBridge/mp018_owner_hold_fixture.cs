// AUTHORED_NOT_RUN. Actual owner hold + actual immutable/shared models.
// ACTUAL_PLAN_SOURCES
internal sealed class ItemStack {internal int count;internal string Data="";internal bool IsEmpty()=>count==0;}
internal sealed class Grid {internal ItemStack[] items;}
internal sealed class Bag {internal Grid ItemGrid;internal bool[] LockedSlots;}
internal sealed class Inventory {internal Grid ItemGrid;internal bool Running;internal bool IsHoldingItemActionRunning()=>Running;}
internal sealed class Drag {internal ItemStack CurrentStack=new ItemStack();}
internal sealed class Xui {internal bool IsUsingItemActionEntryUse;internal Drag DragAndDropWindow=new Drag();}
internal sealed class PlayerUI {internal Xui xui=new Xui();}
internal sealed partial class EntityPlayerLocal {
 internal World world;internal int entityId=1,Died=2;internal bool Dead;internal bool Spawned=true;internal string Creation=Guid.NewGuid().ToString("N");
 internal Bag bag;internal Inventory inventory;internal PlayerUI PlayerUI=new PlayerUI();internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;
}
internal sealed class World {internal string Guid=System.Guid.NewGuid().ToString("N");internal bool Remote;internal EntityPlayerLocal Actor,Primary;internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetEntity(int id)=>Actor;internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;}
internal sealed class NativePlatformId {internal string CombinedString;}
internal sealed partial class GameManager {internal static GameManager Instance=new GameManager();internal World World;internal string PersistentOwner="account";internal NativePlatformId getPersistentPlayerID(object unused)=>new NativePlatformId{CombinedString=PersistentOwner};}
internal sealed class Peer {internal bool Disconnected;internal bool IsDisconnected()=>Disconnected;}
internal sealed class ConnectionManager {internal bool IsServer=true;internal Peer[] connectionToServer=new[]{new Peer()};}
internal static class SingletonMonoBehaviour<T> where T:new() {internal static T Instance=new T();}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal static class GameIO {internal static string Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native");internal static string GetSaveGameDir()=>Root;internal static string GetPlayerDataDir()=>Path.Combine(Root,"Player");}
internal enum EnumGamePrefs {GameGuidClient}
internal static class GamePrefs {internal static string GetString(EnumGamePrefs pref)=>GameManager.Instance.World.Guid;}
internal sealed class RebirthStablePlayerIdentity {internal string StorageKey="account";internal string CanonicalId=>StorageKey;internal static string Owner="account";internal static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{StorageKey=Owner};return true;}}
internal static class RebirthConsumptionOwnerPublication {internal static bool TryCaptureCreation(EntityPlayerLocal player,out string creation){creation=player.Creation;return true;}}
internal static class RebirthRemoteResourceOwnerAction {internal static bool Allowed=true;internal static bool CanPrepareNativeOriginal(EntityPlayerLocal player)=>Allowed;}
internal static class RebirthDogLifecycleService {internal static bool Held;internal static bool HasHireInventoryHold(EntityPlayerLocal player)=>Held;}
internal static class RemoteResourceClientTransactionCoordinator {internal static bool Pending;internal static bool HasPendingInventoryOperation(EntityPlayerLocal player)=>Pending;}
internal static class RebirthToolbeltCapacity {internal static int Owned=4;internal static int GetOwnedSlotCount(EntityPlayerLocal player,int length)=>Owned;}
internal sealed class RebirthGearInventorySnapshot {
 internal RebirthGearInventoryPlan.Stack[] Bag,Belt;internal static Action DuringCapture;
 internal static bool TryCapture(ItemStack[] bag,ItemStack[] belt,int owned,out RebirthGearInventorySnapshot snapshot){
  snapshot=null;DuringCapture?.Invoke();
  if(!RebirthGearEncodedSnapshot.TryCopy(bag.Select(cell=>new Stack{Count=cell.count,ItemData=cell.Data}).ToArray(),belt.Select(cell=>new Stack{Count=cell.count,ItemData=cell.Data}).ToArray(),owned,out var b,out var t))return false;
  snapshot=new RebirthGearInventorySnapshot{Bag=b,Belt=t};return true;
 }
}
internal static class Program {
 private static EntityPlayerLocal player;private static RebirthHydrationTransferOriginal original;
 private static ItemStack[] Cells(int count){var cells=new ItemStack[count];for(int i=0;i<count;i++)cells[i]=new ItemStack();return cells;}
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static void Fresh(bool remote=false){
  var world=new World{Remote=remote};player=new EntityPlayerLocal{world=world,bag=new Bag{ItemGrid=new Grid{items=Cells(44)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=Cells(20)}}};
  world.Actor=world.Primary=player;GameManager.Instance.World=world;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=!remote};
  ThreadManager.Main=true;RebirthStablePlayerIdentity.Owner="account";RebirthRemoteResourceOwnerAction.Allowed=true;RebirthDogLifecycleService.Held=false;RemoteResourceClientTransactionCoordinator.Pending=false;RebirthToolbeltCapacity.Owned=4;RebirthGearInventorySnapshot.DuringCapture=null;
  player.bag.ItemGrid.items[3]=new ItemStack{count=3,Data=Convert.ToBase64String(new byte[]{7,9,3,25})};
  Need(RebirthGearInventorySnapshot.TryCapture(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,4,out var snapshot)&&RebirthHydrationTransferPlan.TryCreate(snapshot.Bag,snapshot.Belt,null,4,new Stack(),true,3,out var plan)&&RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),Guid.Parse(world.Guid),"account",player.Creation,2,8,4,null,plan,out original),"fixture original unavailable");
 }
 public static void Main(){
  Fresh();Need(RebirthHydrationOwnerHold.TryAcquire(player,original)&&RebirthHydrationOwnerHold.TryAcquire(player,original)&&RebirthHydrationOwnerHold.Matches(player,original)&&RebirthHydrationOwnerHold.BlocksInventory(player.bag,true)&&player.bag.ItemGrid.items[3].count==3,"exact host hold/dedup mutated or lost custody");
  var heldBag=player.bag;player.bag=new Bag{ItemGrid=new Grid{items=Cells(44)}};Need(!RebirthHydrationOwnerHold.Matches(player,original)&&RebirthHydrationOwnerHold.BlocksInventory(heldBag,true)&&!RebirthHydrationOwnerHold.BlocksInventory(player.bag,true),"replacement native bag inherited or erased original custody");
  Fresh(true);Need(RebirthHydrationOwnerHold.TryAcquire(player,original),"exact native remote owner hold refused");SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0]=new Peer();Need(!RebirthHydrationOwnerHold.Matches(player,original)&&RebirthHydrationOwnerHold.BlocksInventory(player.bag,true),"replacement peer inherited/lost retained native custody");
  for(int variant=0;variant<16;variant++){
   Fresh();switch(variant){case 0:ThreadManager.Main=false;break;case 1:player.Dead=true;break;case 2:player.Spawned=false;break;case 3:GameManager.Instance.World=new World();break;case 4:player.world.Primary=null;break;case 5:player.world.Actor=null;break;case 6:RebirthStablePlayerIdentity.Owner="foreign";break;case 7:player.Creation=Guid.NewGuid().ToString("N");break;case 8:player.Died++;break;case 9:RebirthRemoteResourceOwnerAction.Allowed=false;break;case 10:RebirthDogLifecycleService.Held=true;break;case 11:RemoteResourceClientTransactionCoordinator.Pending=true;break;case 12:player.inventory.Running=true;break;case 13:player.PlayerUI.xui.DragAndDropWindow.CurrentStack.count=1;break;case 14:player.bag.LockedSlots[3]=true;break;case 15:RebirthToolbeltCapacity.Owned=5;break;}
   Need(!RebirthHydrationOwnerHold.TryAcquire(player,original),"unqualified native owner acquired hold "+variant);
  }
  Fresh();RebirthGearInventorySnapshot.DuringCapture=()=>GameManager.Instance.World=new World();Need(!RebirthHydrationOwnerHold.TryAcquire(player,original),"native scope change during capture admitted");
  Fresh();Need(RebirthHydrationOwnerHold.TryAcquire(player,original),"replacement-body original hold unavailable");
  var replacement=new EntityPlayerLocal{world=player.world,Creation=player.Creation,bag=new Bag{ItemGrid=new Grid{items=Cells(44)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=Cells(20)}}};
  replacement.bag.ItemGrid.items[3]=new ItemStack{count=3,Data=player.bag.ItemGrid.items[3].Data};replacement.world.Actor=replacement.world.Primary=replacement;
  Need(RebirthHydrationOwnerHold.HoldsMetabolismOwner(replacement)&&!RebirthHydrationOwnerHold.IsHeld(replacement)&&
   !RebirthHydrationOwnerHold.BlocksInventory(replacement.bag,true)&&!RebirthHydrationOwnerHold.TryAcquire(replacement,original),"replacement body adopted native handles or bypassed original account custody");
  replacement.Creation=Guid.NewGuid().ToString("N");Need(RebirthHydrationOwnerHold.HoldsMetabolismOwner(replacement),"new creation erased account metabolism custody");
  var otherWorld=new World();replacement.world=otherWorld;Need(!RebirthHydrationOwnerHold.HoldsMetabolismOwner(replacement),"different native World inherited metabolism hold");
  Console.WriteLine("PASS actual hydration owner hold; native snapshot/peer and joint terminal producer remain NOT RUN");
 }
}
