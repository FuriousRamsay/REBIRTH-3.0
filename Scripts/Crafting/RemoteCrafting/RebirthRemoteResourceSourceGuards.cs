using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Exact source holds and separately issued owning-action player input holds.
// Voluntary collection is fenced; source death/despawn retain native behavior.
internal static class RebirthRemoteResourceSourceGuards
{
    internal static void Install(Harmony harmony)
    {
        foreach(var type in new[]{typeof(GridSlot),typeof(GridBulk),typeof(GridAdd),typeof(GridCount),typeof(GridRead),typeof(BagVoid),typeof(BagBool),typeof(BagPartial),typeof(BagCount),typeof(BagResize),
            typeof(StorageRemove),typeof(StorageRead),typeof(StationOutput),typeof(StationTick),typeof(StationRead),typeof(Access),typeof(VehicleSync),typeof(DroneSync),typeof(MobileCollect),typeof(VehiclePickupUnlock),
            typeof(RebirthPlacedOriginalPickupRequestPatch),typeof(RebirthPlacedOriginalNativePickupPatch),typeof(RebirthPlacedOriginalDamagePatch)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        RebirthRemoteResourcePlacedStationOriginal.InstallActionPaymentPickupObserver(harmony);
    }
    private static bool Blocks(object target)=>RebirthRemoteResourceSourceCustody.Blocks(target)||RemoteResourceTransactions.ActionBlocksNative(target);
    private static bool BlocksStation(TileEntityWorkstation station)=>RebirthRemoteResourceSourceCustody.BlocksStation(station)||RemoteResourceTransactions.ActionBlocksNative(station);
    private static MethodBase Required(Type type,string name,params Type[] arguments)=>AccessTools.Method(type,name,arguments)??throw new MissingMethodException(type.FullName,name);
    [HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.SetItem))]
    private static class GridSlot
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ItemStackGrid __instance,int _index,ItemStack _stack)=> (!RebirthRemoteResourceSourceCustody.Blocks(__instance)||RebirthRemoteResourceSourceCustody.Consume(__instance,_index,_stack))&&RemoteResourceTransactions.ActionAllowsGrid(__instance,_index,_stack);
        private static void Postfix(ItemStackGrid __instance,int _index,ItemStack _stack,bool __runOriginal)
        {
            try{RebirthNpcStationQueueOriginal.ObserveGrid(__instance,_index,__runOriginal,null);}catch{}
            try{RebirthRemoteResourceOwnerAction.ObserveCancellationOwnerSlot(__instance,_index,_stack,__runOriginal);}finally{RemoteResourceTransactions.ObserveActionGrid(__instance,_index,_stack,__runOriginal);}
        }
        private static Exception Finalizer(ItemStackGrid __instance,int _index,bool __runOriginal,Exception __exception)
        {if(__exception!=null)try{RebirthNpcStationQueueOriginal.ObserveGrid(__instance,_index,false,__exception);}catch{}return __exception;}
    }
    [HarmonyPatch]
    private static class GridBulk
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(string name in new[]{"SetItems","ClearItems","Compact","Sort","CompactSlots","CopyItemsFrom","CopyFrom","Reset","Clear","Resize","SetSlotLocks","SetLocked","SetPreferences"})
                yield return AccessTools.Method(typeof(ItemStackGrid),name)??throw new MissingMethodException(typeof(ItemStackGrid).Name,name);
        }
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ItemStackGrid __instance)=>!Blocks(__instance);
    }
    [HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.AddItem))]
    private static class GridAdd
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!Blocks(__instance))return true;__result=-1;return false;}
    }
    [HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.ChangeCount))]
    private static class GridCount
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!Blocks(__instance))return true;__result=0;return false;}
    }
    [HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.ReadInto))]
    private static class GridRead
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ItemStackGrid __instance,PooledBinaryReader _br,StreamModeRead _eStreamMode)
        {
            if(!Blocks(__instance))return true;
            // Exact native decoder on a detached holderless grid. All original
            // bytes are consumed; no held item/lock/preference object is reused.
            ItemStackGrid.Read(_br,_eStreamMode);return false;
        }
    }
    [HarmonyPatch]
    private static class BagVoid
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {foreach(string name in new[]{"SetSlots","PerformActionOnSlots"})yield return AccessTools.Method(typeof(Bag),name)??throw new MissingMethodException(typeof(Bag).Name,name);}
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Bag __instance)=>!Blocks(__instance);
    }
    [HarmonyPatch(typeof(Bag),nameof(Bag.AddItem))]
    private static class BagBool
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Bag __instance,ref bool __result){if(!Blocks(__instance))return true;__result=false;return false;}
    }
    [HarmonyPatch(typeof(Bag),nameof(Bag.TryStackItem))]
    private static class BagPartial
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Bag __instance,ref (bool anyMoved,bool allMoved) __result){if(!Blocks(__instance))return true;__result=(false,false);return false;}
    }
    [HarmonyPatch(typeof(Bag),nameof(Bag.DecItem))]
    private static class BagCount
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Bag __instance,ref int __result){if(!Blocks(__instance))return true;__result=0;return false;}
    }
    [HarmonyPatch(typeof(Bag),nameof(Bag.Resize))]
    private static class BagResize
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Bag __instance,ref List<ItemStack> __result){if(!Blocks(__instance))return true;__result=new List<ItemStack>();return false;}
    }
    [HarmonyPatch(typeof(TEFeatureStorage),nameof(TEFeatureStorage.RemoveItems))]
    private static class StorageRemove
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(TEFeatureStorage __instance,ref int __result){if(!Blocks(__instance))return true;__result=0;return false;}
    }
    // Old storage formats can replace itemGrid rather than call ReadInto.
    // Decode completely into a detached native grid and restore in a finalizer.
    [HarmonyPatch(typeof(TEFeatureStorage),nameof(TEFeatureStorage.Read))]
    private static class StorageRead
    {
        private static void Prefix(TEFeatureStorage __instance,out ItemStackGrid __state)
        {
            __state=null;if(!Blocks(__instance))return;
            __state=__instance.itemGrid;
            __instance.itemGrid=ItemStackGrid.Create(__state.ContainerSize,__state.StackLocation,__state.SlotLocks!=null,__state.Preferences!=null,null);
        }
        private static Exception Finalizer(TEFeatureStorage __instance,ItemStackGrid __state,Exception __exception)
        {if(__state!=null)__instance.itemGrid=__state;return __exception;}
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),"set_Output")]
    private static class StationOutput
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(TileEntityWorkstation __instance,ItemStack[] value)=>(!RebirthRemoteResourceSourceCustody.BlocksStation(__instance)||RebirthRemoteResourceSourceCustody.Consume(__instance,value)||RebirthNpcStationQueueOriginal.AllowsOutputSetter(__instance,value))&&RemoteResourceTransactions.ActionAllowsStation(__instance,value);
        private static void Postfix(TileEntityWorkstation __instance,ItemStack[] value,bool __runOriginal)=>RemoteResourceTransactions.ObserveActionStation(__instance,value,__runOriginal);
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.UpdateTick))]
    private static class StationTick
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(TileEntityWorkstation __instance)=>!BlocksStation(__instance);
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.read))]
    private static class StationRead
    {
        private static void Prefix(TileEntityWorkstation __instance,out ItemStack[] __state)
        {__state=null;if(!BlocksStation(__instance))return;__state=__instance.output;__instance.output=ItemStack.Clone(__state);}
        private static Exception Finalizer(TileEntityWorkstation __instance,ItemStack[] __state,Exception __exception)
        {if(__state!=null)__instance.output=__state;return __exception;}
    }
    [HarmonyPatch]
    private static class Access
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var seen=new HashSet<MethodBase>();
            foreach(var type in new[]{typeof(TEFeatureStorage),typeof(TileEntityWorkstation),typeof(EntityVehicle),typeof(EntityDrone)})
            {var method=Required(type,"OnLockRequestServer",typeof(int),typeof(PooledBinaryReader),typeof(ushort));if(seen.Add(method))yield return method;}
        }
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(object __instance,ref bool __result)
        {if(!RemoteResourceTransactions.AllowActionSourceEscape(__instance,"native-lock-request")){__result=false;return false;}if(!Blocks(__instance))return true;__result=false;return false;}
    }
    // Collection is voluntary extraction. Death/despawn do not call this
    // permission and continue through their real native lifecycle unchanged.
    [HarmonyPatch]
    private static class MobileCollect
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {foreach(var type in new[]{typeof(EntityVehicle),typeof(EntityDrone)})yield return Required(type,"OnCollectServer",typeof(int));}
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(object __instance)=>RemoteResourceTransactions.AllowActionSourceEscape(__instance,"native-voluntary-collect");
    }
    [HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnUnlockedServer))]
    private static class VehiclePickupUnlock
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(EntityVehicle __instance,ushort __1)=>__1!=1||RemoteResourceTransactions.AllowActionSourceEscape(__instance,"native-pickup-unlock");
    }
    [HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.ReadSyncData))]
    private static class VehicleSync
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(EntityVehicle __instance,ushort __1)=>(!RebirthRemoteResourceSourceCustody.BlocksVehicle(__instance)&&!RemoteResourceTransactions.ActionBlocksNative(__instance))||(__1&12)==0;
    }
    [HarmonyPatch(typeof(EntityDrone),nameof(EntityDrone.ReadSyncData))]
    private static class DroneSync
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(EntityDrone __instance,ushort __1)=>!Blocks(__instance)||(__1&136)==0;
    }
}
