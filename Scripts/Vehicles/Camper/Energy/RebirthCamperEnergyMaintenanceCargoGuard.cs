using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Exact held cargo only. Service validates its durable receipt/pre/post images.
public static class RebirthCamperEnergyMaintenanceCargoGuard
{
    private sealed class Hold {internal EntityVehicle Vehicle;internal World World;internal Bag Bag;internal ItemStackGrid Grid;}
    private sealed class Permit {internal Hold Hold;internal int Slot;internal ItemStack Desired;}
    private static readonly ConditionalWeakTable<object,Hold> held=new ConditionalWeakTable<object,Hold>();
    private static readonly ConditionalWeakTable<EntityVehicle,Hold> vehicles=new ConditionalWeakTable<EntityVehicle,Hold>();
    [ThreadStatic] private static Permit permit;
    private static bool installed;
    internal static readonly string[] MutationNames={"SetSlot","SetSlots","SetItem","SetItems","DecItem","AddItem","AddItemAt","ChangeSlotCount","clearSlotByIndex",
        "ChangeCount","ClearItems","Compact","Sort","CompactSlots","Reset","Clear","CopyFrom","CopyItemsFrom","SetSlotLocks","SetLocked","SetPreferences","TryStackItem","PerformActionOnSlots"};
    internal static void Install(Harmony harmony)
    {
        if(installed)return;if(harmony==null)throw new ArgumentNullException(nameof(harmony));
        Require(typeof(Bag),"SetSlot",typeof(void),typeof(int),typeof(ItemStack));
        Require(typeof(ItemStackGrid),"SetItem",typeof(void),typeof(int),typeof(ItemStack));
        Require(typeof(Bag),"Resize",typeof(List<ItemStack>),typeof(Vector2i));
        Require(typeof(ItemStackGrid),"Resize",typeof(List<ItemStack>),typeof(Vector2i));
        Require(typeof(EntityVehicle),nameof(EntityVehicle.OnLockRequestServer),typeof(bool),typeof(int),typeof(PooledBinaryReader),typeof(ushort));
        Require(typeof(EntityVehicle),nameof(EntityVehicle.ReadSyncData),typeof(void),typeof(PooledBinaryReader),typeof(ushort),typeof(int));
        foreach(var type in new[]{typeof(Bag),typeof(ItemStackGrid)})
            foreach(string name in type==typeof(Bag)?new[]{"SetSlot","SetSlots","Resize","DecItem","AddItem","TryStackItem","PerformActionOnSlots"}:
                new[]{"SetItem","SetItems","Resize","ChangeCount","Clear","Reset","CopyFrom","CopyItemsFrom","SetSlotLocks","SetLocked","SetPreferences"})
                if(!type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Any(m=>m.DeclaringType==type&&m.Name==name))throw new MissingMethodException(type.FullName,name);
        // Resolve ALL mandatory anchors before installing the first detour.
        foreach(var type in new[]{typeof(RebirthCamperMaintenanceCargoMutation),typeof(RebirthCamperMaintenanceCargoResize),typeof(RebirthCamperMaintenanceCargoLock),
            typeof(RebirthCamperMaintenanceCargoSync),typeof(RebirthCamperMaintenanceCargoCursor)})
            foreach(var method in Targets(type))if(method==null)throw new MissingMethodException("Maintenance cargo anchor unavailable: "+type.Name);
        foreach(var type in new[]{typeof(RebirthCamperMaintenanceCargoMutation),typeof(RebirthCamperMaintenanceCargoResize),typeof(RebirthCamperMaintenanceCargoLock),
            typeof(RebirthCamperMaintenanceCargoSync),typeof(RebirthCamperMaintenanceCargoCursor)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        installed=true;
    }
    private static void Require(Type type,string name,Type result,params Type[] parameters)
    {var method=AccessTools.DeclaredMethod(type,name,parameters);if(method==null||method.ReturnType!=result)throw new MissingMethodException(type.FullName,name);}
    private static IEnumerable<MethodBase> Targets(Type type)
    {
        if(type==typeof(RebirthCamperMaintenanceCargoLock)){yield return AccessTools.DeclaredMethod(typeof(EntityVehicle),nameof(EntityVehicle.OnLockRequestServer));yield break;}
        if(type==typeof(RebirthCamperMaintenanceCargoSync)){yield return AccessTools.DeclaredMethod(typeof(EntityVehicle),nameof(EntityVehicle.ReadSyncData));yield break;}
        if(type==typeof(RebirthCamperMaintenanceCargoCursor))
        {foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})yield return AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes);yield break;}
        foreach(var native in new[]{typeof(Bag),typeof(ItemStackGrid)})foreach(var method in native.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            if(method.DeclaringType==native&&(type==typeof(RebirthCamperMaintenanceCargoResize)?method.Name=="Resize":MutationNames.Contains(method.Name)))yield return method;
    }
    internal static IEnumerable<MethodBase> MutationTargets()=>Targets(typeof(RebirthCamperMaintenanceCargoMutation));
    internal static IEnumerable<MethodBase> ResizeTargets()=>Targets(typeof(RebirthCamperMaintenanceCargoResize));
    internal static IEnumerable<MethodBase> CursorTargets()=>Targets(typeof(RebirthCamperMaintenanceCargoCursor));
    private static bool Current(Hold hold)=>ThreadManager.IsMainThread()&&hold?.Vehicle?.world!=null&&!hold.World.IsRemote()&&
        ReferenceEquals(GameManager.Instance?.World,hold.World)&&ReferenceEquals(hold.Vehicle.world,hold.World)&&
        ReferenceEquals(hold.World.GetEntity(hold.Vehicle.entityId),hold.Vehicle)&&ReferenceEquals(hold.Vehicle.bag,hold.Bag)&&ReferenceEquals(hold.Bag.ItemGrid,hold.Grid);
    internal static void HoldCargo(EntityVehicle vehicle)
    {
        if(!installed||vehicle?.bag?.ItemGrid==null||!ThreadManager.IsMainThread()||vehicle.world==null||vehicle.world.IsRemote()||
            !ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle))throw new InvalidOperationException("Current installed maintenance cargo guard required.");
        if(vehicles.TryGetValue(vehicle,out var prior))
        {if(Current(prior))return;throw new InvalidOperationException("Captured maintenance cargo changed; retain receipt custody.");}
        var hold=new Hold{Vehicle=vehicle,World=vehicle.world,Bag=vehicle.bag,Grid=vehicle.bag.ItemGrid};
        if(held.TryGetValue(hold.Bag,out _)||held.TryGetValue(hold.Grid,out _))throw new InvalidOperationException("Cargo already owned by maintenance.");
        vehicles.Add(vehicle,hold);held.Add(hold.Bag,hold);held.Add(hold.Grid,hold);
    }
    internal static void ReleaseCargo(EntityVehicle vehicle)
    {
        if(vehicle==null||!vehicles.TryGetValue(vehicle,out var hold))return;
        if(held.TryGetValue(hold.Bag,out var bag)&&ReferenceEquals(bag,hold))held.Remove(hold.Bag);
        if(held.TryGetValue(hold.Grid,out var grid)&&ReferenceEquals(grid,hold))held.Remove(hold.Grid);
        vehicles.Remove(vehicle);
    }
    internal static bool Blocks(object target)=>target!=null&&held.TryGetValue(target,out var hold)&&Current(hold);
    internal static bool BlocksVehicle(EntityVehicle vehicle)=>vehicle!=null&&vehicles.TryGetValue(vehicle,out var hold)&&Current(hold);
    internal static void SetSlot(Bag bag,int index,ItemStack value)
    {
        if(permit!=null||bag==null||!held.TryGetValue(bag,out var hold)||!Current(hold)||index<0||index>=hold.Grid.Length||value==null)
            throw new InvalidOperationException("Exact held maintenance slot write required.");
        var desired=RebirthCamperEnergyItems.Copy(new[]{value})[0];permit=new Permit{Hold=hold,Slot=index,Desired=desired};
        try{bag.SetSlot(index,desired.Clone());}finally{permit=null;}
    }
    internal static bool AllowsMutation(object target,MethodBase method,object[] args)
    {
        if(!Blocks(target))return true;var p=permit;
        if(p==null||!Current(p.Hold)||!ReferenceEquals(target,p.Hold.Bag)&&!ReferenceEquals(target,p.Hold.Grid)||args==null||args.Length<2||
            !(args[0] is int index)||index!=p.Slot||!(args[1] is ItemStack stack))return false;
        return (ReferenceEquals(target,p.Hold.Bag)?method.Name=="SetSlot":method.Name=="SetItem")&&RebirthCamperEnergyItems.Same(stack,p.Desired);
    }
    internal static bool BlocksGesture(XUiC_ItemStack slot,MethodBase method)
    {
        // Native SwapItem clears/swaps cursor BEFORE the lower cargo setter.
        // Match the actual parent container's captured Bag, not its window name
        // or another open container. Owner bag/belt and unrelated slots stay usable.
        var container=slot?.GetParentByType<XUiC_BagContainer>();
        return container!=null&&Blocks(container.Bag);
    }
}

[HarmonyPatch]
internal static class RebirthCamperMaintenanceCargoMutation
{
    private static IEnumerable<MethodBase> TargetMethods()=>RebirthCamperEnergyMaintenanceCargoGuard.MutationTargets();
    [HarmonyPriority(Priority.First)]private static bool Prefix(object __instance,MethodBase __originalMethod,object[] __args)
        =>RebirthCamperEnergyMaintenanceCargoGuard.AllowsMutation(__instance,__originalMethod,__args);
}
[HarmonyPatch]
internal static class RebirthCamperMaintenanceCargoResize
{
    private static IEnumerable<MethodBase> TargetMethods()=>RebirthCamperEnergyMaintenanceCargoGuard.ResizeTargets();
    [HarmonyPriority(Priority.First)]private static bool Prefix(object __instance,ref List<ItemStack> __result)
    {if(!RebirthCamperEnergyMaintenanceCargoGuard.Blocks(__instance))return true;__result=new List<ItemStack>();return false;}
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnLockRequestServer))]
internal static class RebirthCamperMaintenanceCargoLock
{
    [HarmonyPriority(Priority.First)]private static bool Prefix(EntityVehicle __instance,ushort __2,ref bool __result)
    {if(__2>1||!RebirthCamperEnergyMaintenanceCargoGuard.BlocksVehicle(__instance))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.ReadSyncData))]
internal static class RebirthCamperMaintenanceCargoSync
{
    [HarmonyPriority(Priority.First)]private static bool Prefix(EntityVehicle __instance,ushort __1)
        =>!RebirthCamperEnergyMaintenanceCargoGuard.BlocksVehicle(__instance)||(__1&12)==0;
}
[HarmonyPatch]
internal static class RebirthCamperMaintenanceCargoCursor
{
    private static IEnumerable<MethodBase> TargetMethods()=>RebirthCamperEnergyMaintenanceCargoGuard.CursorTargets();
    [HarmonyPriority(Priority.First)]private static bool Prefix(XUiC_ItemStack __instance,MethodBase __originalMethod)
        =>!RebirthCamperEnergyMaintenanceCargoGuard.BlocksGesture(__instance,__originalMethod);
}
