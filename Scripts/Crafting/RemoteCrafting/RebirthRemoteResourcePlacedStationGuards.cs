using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

// Authoritative target freeze only. Client holds, original station payment
// permits, cancellation and recovery remain separate qualification requirements
// before any producer may activate. Ordinary unrelated TE behavior is unchanged.
internal static class RebirthRemoteResourcePlacedStationGuards
{
    internal static bool IsInstalled{get;private set;}
    private static MethodInfo Required(Type type,string name,params Type[] arguments)
        =>AccessTools.Method(type,name,arguments)??throw new MissingMethodException(type.FullName,name);
    internal static void Install()
    {
        if(IsInstalled)return;
        RebirthRemoteResourceSourceCustody.Install();
        RebirthRemoteResourcePlacedStationCandidate.Install();
        if(!RebirthRemoteResourcePlacedStationCandidate.IsInstalled)throw new InvalidOperationException("Placed native layout not qualified.");
        var harmony=new Harmony("rebirth.remote.resources.placed.target.guards.v1");
        try
        {
            foreach(var type in new[]{typeof(StationWrite),typeof(Output),typeof(FuelTick),typeof(ModelWrite),typeof(WindowSync),typeof(WindowTick),typeof(Incoming),typeof(Access)})
                RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
            IsInstalled=true;
        }
        catch{harmony.UnpatchSelf();throw;}
    }
    [HarmonyPatch]
    private static class StationWrite
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(string name in new[]{"Input","Tools","Fuel","Queue","IsBurning"})
                yield return AccessTools.PropertySetter(typeof(TileEntityWorkstation),name)??throw new MissingMethodException(name);
            yield return Required(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.UpdateTick),typeof(World));
            yield return Required(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.HandleRecipeQueue),typeof(float));
            yield return Required(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.HandleMaterialInput),typeof(float));
            yield return Required(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.ResetTickTime));
        }
        [HarmonyPriority(Priority.First)]private static bool Prefix(TileEntityWorkstation __instance,MethodBase __originalMethod,object[] __args)
            =>!RebirthRemoteResourcePlacedStationCustody.Blocks(__instance)||
            RebirthRemoteResourcePlacedStationOriginal.ConsumeStationSetter(__instance,__originalMethod,__args)||
            RebirthRemoteResourcePlacedStationOriginal.ConsumeCancellationStationSetter(__instance,__originalMethod,__args)||
            RebirthNpcStationQueueOriginal.ConsumeStationSetter(__instance,__originalMethod,__args);
        private static void Postfix(TileEntityWorkstation __instance,MethodBase __originalMethod,bool __runOriginal)
        {try{RebirthNpcStationQueueOriginal.ObserveStationSetter(__instance,__originalMethod,__runOriginal,null);}catch{}}
        private static Exception Finalizer(TileEntityWorkstation __instance,MethodBase __originalMethod,bool __runOriginal,Exception __exception)
        {if(__exception!=null)try{RebirthNpcStationQueueOriginal.ObserveStationSetter(__instance,__originalMethod,false,__exception);}catch{}return __exception;}
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),"set_Output")]
    private static class Output
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(TileEntityWorkstation __instance,ItemStack[] value)
        {
            if(!RebirthRemoteResourcePlacedStationCustody.Blocks(__instance))return true;
            // Read-only check of the one operation-bound NPC output permit; every other writer is unchanged.
            if(RebirthNpcStationQueueOriginal.AllowsOutputSetter(__instance,value))return true;
            return RebirthRemoteResourcePlacedStationCustody.TryReadGuardClaim(__instance,out var claim)&&
                RebirthRemoteResourceSourceCustody.IsExecutingExactOutput(__instance,value,claim.Owner,claim.Source,claim.Binding);
        }
        private static void Postfix(TileEntityWorkstation __instance,ItemStack[] value,bool __runOriginal)
        {try{RebirthNpcStationQueueOriginal.ObserveOutputSetter(__instance,value,__runOriginal,null);}catch{}}
        private static Exception Finalizer(TileEntityWorkstation __instance,ItemStack[] value,Exception __exception)
        {if(__exception!=null)try{RebirthNpcStationQueueOriginal.ObserveOutputSetter(__instance,value,false,__exception);}catch{}return __exception;}
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.HandleFuel))]
    private static class FuelTick
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(TileEntityWorkstation __instance,ref bool __result)
        {if(!RebirthRemoteResourcePlacedStationCustody.Blocks(__instance))return true;__result=false;return false;}
    }
    [HarmonyPatch]
    private static class ModelWrite
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(string name in new[]{"Input","Output","Tool","Fuel"})
            {
                yield return Required(typeof(XUiM_Workstation),"Set"+name+"Stacks",typeof(ItemStack[]));
                yield return Required(typeof(XUiM_Workstation),"Set"+name+"InSlot",typeof(int),typeof(ItemStack));
            }
            yield return Required(typeof(XUiM_Workstation),"SetRecipeQueueItems",typeof(RecipeQueueItem[]));
            yield return Required(typeof(XUiM_Workstation),"SetQueueInSlot",typeof(int),typeof(RecipeQueueItem));
            yield return Required(typeof(XUiM_Workstation),"SetIsBurning",typeof(bool));
        }
        [HarmonyPriority(Priority.First)]private static bool Prefix(XUiM_Workstation __instance)=>!RebirthRemoteResourcePlacedStationCustody.Blocks(__instance?.TileEntity);
    }
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),"syncTEfromUI")]
    private static class WindowSync
    {
        [HarmonyPriority(Priority.First)]private static bool Prefix(XUiC_WorkstationWindowGroup __instance)=>!RebirthRemoteResourcePlacedStationCustody.Blocks(__instance?.WorkstationData?.TileEntity);
    }
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),nameof(XUiC_WorkstationWindowGroup.Update))]
    private static class WindowTick
    {
        [HarmonyPriority(Priority.First)]private static bool Prefix(XUiC_WorkstationWindowGroup __instance)=>!RebirthRemoteResourcePlacedStationCustody.Blocks(__instance?.WorkstationData?.TileEntity);
    }
    [HarmonyPatch(typeof(NetPackageTileEntity),nameof(NetPackageTileEntity.ProcessPackage))]
    private static class Incoming
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(NetPackageTileEntity __instance,World _world)
        {
            if(_world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,_world))return true;
            return !(_world.GetTileEntity(__instance.teWorldPos) is TileEntityWorkstation station)||!RebirthRemoteResourcePlacedStationCustody.Blocks(station);
        }
    }
    [HarmonyPatch]
    private static class Access
    {
        private static MethodBase TargetMethod()=>Required(typeof(TileEntityWorkstation),"OnLockRequestServer",typeof(int),typeof(PooledBinaryReader),typeof(ushort));
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(object __instance,int __0,ref bool __result)
        {
            if(__instance is TileEntityWorkstation cancellationStation&&RebirthRemoteResourcePlacedStationOriginal.TryCancellationLockActor(cancellationStation,__0,out bool allowed))
            {if(allowed)return true;__result=false;return false;}
            // An NPC station-queue hold is not a placed-station claim: deny every UI lock while it lasts, so no client or host UI can take
            // inputs that the held-station guards would then silently drop (item loss / desync).
            if(__instance is TileEntityWorkstation heldStation&&RebirthNpcStationQueueOriginal.BlocksStation(heldStation)){__result=false;return false;}
            if(__instance is not TileEntityWorkstation station||!RebirthRemoteResourcePlacedStationCustody.TryReadGuardClaim(station,out var claim))return true;
            if(claim.AllowsNativeLockActor(__0))return true;
            __result=false;return false;
        }
    }
}
