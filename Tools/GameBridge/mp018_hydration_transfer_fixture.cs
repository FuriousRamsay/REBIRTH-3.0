using System;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Actual legacy server-replica entry refusal. Actual local
// financial/native/WAL composition is mp018_local_conductor.mjs.
internal sealed class World {internal bool Remote;internal bool IsRemote()=>Remote;}
internal sealed class ItemValue {internal int type=1;internal ushort Seed=7;internal string Metadata="full-native-metadata";internal float Liquid=325;internal ItemValue Clone()=>new ItemValue{type=type,Seed=Seed,Metadata=Metadata,Liquid=Liquid};}
internal sealed class ItemStack {
 internal ItemValue itemValue=new ItemValue();internal int count=1;
 internal static ItemStack Empty=>new ItemStack{count=0,itemValue=new ItemValue{type=0}};
 internal bool IsEmpty()=>count<=0||itemValue==null||itemValue.type==0;
 internal ItemStack Clone()=>new ItemStack{count=count,itemValue=itemValue?.Clone()};
}
internal sealed class Grid {internal ItemStack[] items=new[]{new ItemStack()};}
internal sealed class Bag {internal Grid ItemGrid=new Grid();internal bool Suppress;internal void SetSlot(int index,ItemStack stack){if(!Suppress)ItemGrid.items[index]=stack;}}
internal sealed class Inventory {internal Grid ItemGrid=new Grid();internal int holdingItemIdx;internal bool Suppress;internal void SetItem(int index,ItemStack stack){if(!Suppress)ItemGrid.items[index]=stack;}}
internal class EntityPlayer {internal World world=new World();internal Bag bag=new Bag();internal Inventory inventory=new Inventory();}
internal sealed class EntityPlayerLocal : EntityPlayer {}
// This historical driver exercises only the uncut-over server replica path.
// The actual saved local conductor runs in mp018_local_conductor.mjs instead.
internal static class RebirthHydrationLocalTransfer {
 internal enum Result {Refused,Pending,Applied}
 internal static Result TryBegin(EntityPlayerLocal player,bool bag,int slot,bool returning)=>throw new InvalidOperationException("Use the actual local conductor driver");
}
internal static class RebirthSurvivorMetabolismCreationGate {internal static bool ShouldDefer(EntityPlayer player)=>false;}
internal static class RebirthBackpackLibraryReservation {internal static bool Held;internal static bool BlocksResourceUse(EntityPlayer player)=>Held;}
internal static class Localization {internal static string Get(string key)=>key;}
internal static class RebirthToolbeltCapacity {internal static int GetOwnedSlotCount(EntityPlayer player,int length)=>length;}
internal sealed class RebirthConsumableDefinition {internal bool IsDrink=true,HydrationEquippable=true;}
internal static class RebirthConsumableResolver {internal static bool TryResolve(ItemValue value,out RebirthConsumableDefinition definition){definition=new RebirthConsumableDefinition();return value!=null&&value.type==1;}}
internal sealed class RebirthMetabolismState {internal ItemStack HydrationSlotItem=ItemStack.Empty;internal void Touch(){}}
internal static class RebirthMetabolismStateRepository {internal static RebirthMetabolismState State=new RebirthMetabolismState();internal static RebirthMetabolismState GetOrCreate(EntityPlayer player)=>State;}
internal static class RebirthSurvivorMode {internal static bool IsEnabledForCurrentWorld()=>true;}
internal sealed class Support {internal Dictionary<string,string> EquippedGearBySlot=new Dictionary<string,string>();}
internal sealed class RebirthWorldCharacterRecord {internal Support Support=new Support();}
internal static class RebirthWorldCharacterService {internal static bool TryGet(EntityPlayer player,out RebirthWorldCharacterRecord record){record=new RebirthWorldCharacterRecord();return true;}}
internal static class RebirthSurvivorGearService {internal const string SupportSlotId="support";}
internal static class RebirthLiquidContainerService {internal static void NormalizeState(ItemValue item,RebirthConsumableDefinition definition,out float normalized){normalized=item.Liquid;}}
internal static class RebirthMetabolismService {
 internal static bool IsServerAuthority=true;private static void SendSnapshotToOwner(EntityPlayer player,bool force){}
// ACTUAL_HYDRATION_TRANSFER
 internal static bool EquipAt(EntityPlayer player,bool bag,int index)=>TryEquipHydrationAt(player,bag,index,out _);
}
internal static class Program {
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static EntityPlayer Fresh(){RebirthMetabolismStateRepository.State=new RebirthMetabolismState();RebirthBackpackLibraryReservation.Held=false;return new EntityPlayer();}
 public static void Main(){
  var player=Fresh();Need(!RebirthMetabolismService.EquipAt(player,true,0)&&!player.bag.ItemGrid.items[0].IsEmpty()&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"legacy remote replica equip bypassed owning original");
  RebirthMetabolismStateRepository.State.HydrationSlotItem=new ItemStack();
  Need(!RebirthMetabolismService.UnequipHydrationContainer(player,out _)&&RebirthMetabolismStateRepository.State.HydrationSlotItem.count==1&&player.bag.ItemGrid.items[0].itemValue.Liquid==325,"legacy remote return changed owner custody");
  player=Fresh();player.bag.Suppress=true;Need(!RebirthMetabolismService.EquipAt(player,true,0)&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty()&&!player.bag.ItemGrid.items[0].IsEmpty(),"MP018: suppressed bag debit minted hydration copy");
  player=Fresh();player.inventory.Suppress=true;Need(!RebirthMetabolismService.EquipAt(player,false,0)&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"MP018: suppressed belt debit minted hydration copy");
  player=Fresh();player.bag.ItemGrid.items[0]=ItemStack.Empty;RebirthMetabolismStateRepository.State.HydrationSlotItem=new ItemStack();player.bag.Suppress=true;
  Need(!RebirthMetabolismService.UnequipHydrationContainer(player,out _)&&!RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty()&&player.bag.ItemGrid.items[0].IsEmpty(),"MP018: suppressed return erased hydration custody");
  player=Fresh();RebirthBackpackLibraryReservation.Held=true;Need(!RebirthMetabolismService.EquipAt(player,true,0)&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"library hold allowed hydration move");
  Console.WriteLine("PASS actual legacy remote replica refusal; actual local/remote original settlement has separate drivers, all native topology NOT RUN");
 }
}
