using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class RebirthCamperBodyAttachment
{
    internal const string Modifier="modVehicleCamperBody";
    internal const string Policy="RebirthCamperBodyPolicyV1";
    internal static bool Camper(ItemValue item)=>item?.ItemClass?.GetItemName()=="rebirthCamper80sPlaceable";
    internal static bool HasBody(ItemValue item)
    {if(!Camper(item))return false;for(int i=0;i<item.ModificationCount;i++)if(item.GetModification(i)?.ItemClass?.GetItemName()==Modifier)return true;return false;}
    internal static bool HasBody(EntityVehicle vehicle)=>vehicle?.vehicle!=null&&HasBody(vehicle.vehicle.GetUpdatedItemValue());
    internal static bool Occupied(ItemValue item)=>RemovalReason(item)!=null;
    internal static string RemovalReason(ItemValue item)
    {
        if(!Camper(item))return null;
        try
        {
            if(item.TryGetMetadata("RebirthCamperTransferPending",out string pending)&&pending=="1")
                return "xuiRbCamperBodyRecovery";
            bool hasIdentity=item.TryGetMetadata("RebirthCamperIdentity",out string text);
            bool hasModules=item.TryGetMetadata("RebirthCamperModules",out string ignoredModules);
            bool hasEnergy=item.TryGetMetadata(RebirthCamperEnergyState.Key,out string energy);
            Guid identity=Guid.Empty;
            if(hasIdentity&&(!Guid.TryParse(text,out identity)||identity==Guid.Empty)||
                !hasIdentity&&(hasModules||hasEnergy))return "xuiRbCamperBodyUnverified";
            RebirthCamperEnergyMode mode=RebirthCamperEnergyMode.Travel;
            if(hasIdentity)
            {
                if(RebirthCamperModuleItemCandidate.TryRestore(identity,item,out var storage,out var stations))
                {
                    var modules=ModuleReason(storage,stations);if(modules!=null)return modules;
                }
                if(hasEnergy)
                {
                    var state=RebirthCamperEnergyState.Decode(Convert.FromBase64String(energy),identity);
                    if(state.Sources.Count>0)return "xuiRbCamperBodyPower";
                    mode=state.Mode;
                }
            }
            if(item.TryGetMetadata(RebirthCamperTurretState.Key,out string turrets)&&
                RebirthCamperTurretState.Decode(Convert.FromBase64String(turrets)).Slots.Any(slot=>slot.HasTurret))
                return "xuiRbCamperBodyTurrets";
            // Existing deployment/packing safeguard: explain it rather than silently refusing.
            return mode!=RebirthCamperEnergyMode.Travel?"xuiRbCamperBodyTravel":null;
        }
        catch{return "xuiRbCamperBodyUnverified";}
    }
    private static string ModuleReason(RebirthCamperStorageInstallations storage,RebirthCamperStationInstallations stations)
    {
        for(int i=0;i<RebirthCamperStorageCapacity.ModuleSlots;i++)
            if(storage.GetSlot(i)?.IsEmpty()==false)return "xuiRbCamperBodyStorage";
        for(int i=0;i<RebirthCamperCraftingStation.ModuleSlots;i++)
            if(stations.GetSlot(i)!=null)return "xuiRbCamperBodyStations";
        return null;
    }
    internal static string LiveReason(Vehicle native)
    {
        var entity=native?.entity as EntityVehicle;
        if(!ThreadManager.IsMainThread()||entity?.world==null||entity.world.IsRemote()||
            !ReferenceEquals(entity.vehicle,native)||!ReferenceEquals(entity.world.GetEntity(entity.entityId),entity))return null;
        // Observe only an already admitted owner. Never create one while native loading is in progress.
        if(!RebirthCamperModuleRuntime.TryGetExisting(entity,out var runtime))return null;
        if(runtime.TransferPending)return "xuiRbCamperBodyRecovery";
        try{return ModuleReason(runtime.Storage,runtime.Stations);}
        catch{return "xuiRbCamperBodyUnverified";}
    }
    internal static void AddLegacyBody(ItemValue item)
    {
        if(!Camper(item)||item.TryGetMetadata(Policy,out string ignoredPolicy))return;
        var body=ItemClass.GetItem(Modifier,false);if(body?.ItemClass==null)return;
        var mods=(item.modifications??Array.Empty<ItemValue>()).ToList();
        while(mods.Count<6)mods.Add(ItemValue.None);
        if(!HasBody(item)){int slot=mods.FindIndex(m=>m==null||m.IsEmpty());if(slot<0)mods.Add(body.Clone());else mods[slot]=body.Clone();}
        item.modifications=mods.ToArray();item.SetMetadata(Policy,"1");
    }
    internal static void Install()
    {
        var h=new Harmony("rebirth.camper.body-modifier");
        h.CreateClassProcessor(typeof(RebirthCamperBodyNewItem)).Patch();h.CreateClassProcessor(typeof(RebirthCamperBodyLegacyRead)).Patch();
        h.CreateClassProcessor(typeof(RebirthCamperBodyRemoval)).Patch();h.CreateClassProcessor(typeof(RebirthCamperBodySwap)).Patch();
        h.CreateClassProcessor(typeof(RebirthCamperBodyVehicleAdmission)).Patch();
        h.CreateClassProcessor(typeof(RebirthCamperBodyGridRefresh)).Patch();
        h.CreateClassProcessor(typeof(RebirthCamperBareTruckSeats)).Patch();
    }
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.AttachEntityToSelf))]
internal static class RebirthCamperBareTruckSeats
{
    private static bool Prefix(EntityVehicle __instance,ref int slot,ref int __result)
    {
        if(!RebirthCamperRadial.IsCamper(__instance)||RebirthCamperBodyAttachment.HasBody(__instance))return true;
        if(slot<0)slot=__instance.GetAttached(0)==null?0:__instance.GetAttached(1)==null?1:2;
        if(slot<2)return true;__result=-1;return false;
    }
}
[HarmonyPatch]
internal static class RebirthCamperBodyNewItem
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(ItemValue).GetConstructors().Where(c=>c.GetParameters().Length>0&&c.GetParameters()[0].ParameterType==typeof(int));
    private static void Postfix(ItemValue __instance){if(RebirthCamperBodyAttachment.Camper(__instance))__instance.SetMetadata(RebirthCamperBodyAttachment.Policy,"1");}
}
[HarmonyPatch(typeof(ItemValue),nameof(ItemValue.ReadData))]
internal static class RebirthCamperBodyLegacyRead
{
    private static void Postfix(ItemValue __instance)=>RebirthCamperBodyAttachment.AddLegacyBody(__instance);
}
[HarmonyPatch(typeof(XUiC_ItemPartStack),"CanRemove")]
internal static class RebirthCamperBodyRemoval
{
    private sealed class Feedback {internal string Key;internal float Next;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemPartStack,Feedback> feedback
        =new System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemPartStack,Feedback>();
    internal static bool Blocked(XUiC_ItemPartStack slot)
    {
        if(slot==null)return false;
        var grid=slot.GetParentByType<XUiC_VehiclePartStackGrid>();
        bool wasBody=slot.ItemValue?.ItemClass?.GetItemName()==RebirthCamperBodyAttachment.Modifier;
        if(grid!=null)
        {
            bool refreshed=Refresh(grid);
            // Never operate on a stale grid while an item move is in progress.
            if(Stale(grid)||refreshed&&wasBody&&slot.ItemValue?.ItemClass?.GetItemName()!=RebirthCamperBodyAttachment.Modifier)
            {Notify(slot,"xuiRbCamperBodyChanged");return true;}
        }
        if(slot.ItemValue?.ItemClass?.GetItemName()!=RebirthCamperBodyAttachment.Modifier)return false;
        var reason=RebirthCamperBodyAttachment.LiveReason(grid?.CurrentVehicle)??
            RebirthCamperBodyAttachment.RemovalReason(slot.xui?.AssembleItem?.CurrentItem?.itemValue);
        if(reason==null)return false;
        Notify(slot,reason);return true;
    }
    private static bool Current(XUiC_VehiclePartStackGrid grid,out EntityVehicle entity,out ItemValue live)
    {
        live=null;var native=grid?.CurrentVehicle;entity=native?.entity as EntityVehicle;
        var item=grid?.CurrentItem?.itemValue;
        if(!RebirthCamperBodyAttachment.Camper(item)||entity?.vehicle==null||!ReferenceEquals(entity.vehicle,native)||
            entity.world==null||!ReferenceEquals(GameManager.Instance?.World,entity.world)||
            !ReferenceEquals(entity.world.GetEntity(entity.entityId),entity)||grid.WindowGroup?.isShowing!=true)return false;
        live=native.itemValue;return RebirthCamperBodyAttachment.Camper(live);
    }
    private static bool Stale(XUiC_VehiclePartStackGrid grid)
        =>Current(grid,out _,out var live)&&!ReferenceEquals(grid.CurrentItem.itemValue,live);
    internal static bool Refresh(XUiC_VehiclePartStackGrid grid)
    {
        if(!Current(grid,out var entity,out var live)||ReferenceEquals(grid.CurrentItem.itemValue,live))return false;
        var restoration=grid.GetParentByType<XUiC_RebirthRestorationModifiers>();
        var window=grid.GetParentByType<XUiC_VehicleWindowGroup>();
        bool restoring=restoration!=null&&restoration.GetParentByType<XUiC_RebirthVehicleRestoration>()?.NativeVehicle==entity;
        if(!restoring&&window?.CurrentVehicleEntity!=entity)return false;
        XUiController panel=restoring?(XUiController)restoration:window;
        // Both bindings refresh modification AND cosmetic slots. Preserve a pending
        // cursor move or selected/pressed source in either grid until it settles.
        if(grid.xui?.DragAndDropWindow?.CurrentStack?.IsEmpty()!=true)return false;
        foreach(var slot in panel.GetChildrenByType<XUiC_BasePartStack>())
            if(slot.IsSelected||slot.lastClicked)return false;
        // Sync flags4 replaces ItemValue. Native grids otherwise retain the pre-sync item indefinitely.
        // Reuse the owning window's binding for its grids and assemble model together.
        if(restoring){restoration.Bind(entity);return true;}
        window.CurrentVehicleEntity=entity;return true;
    }
    private static void Notify(XUiC_ItemPartStack slot,string key)
    {
        // CanRemove also supplies hover callouts. Explain a hovered/selected blocker,
        // without a repeated tooltip every frame or global feedback state across players.
        var player=slot.xui?.playerUI?.entityPlayer;
        if(player==null||(!slot.isOver&&!slot.IsSelected))return;
        var state=feedback.GetValue(slot,_=>new Feedback());float now=Time.realtimeSinceStartup;
        if(state.Key==key&&now<state.Next)return;
        state.Key=key;state.Next=now+2f;GameManager.ShowTooltip(player,Localization.Get(key));
    }
    private static bool Prefix(XUiC_ItemPartStack __instance,ref bool __result)
    {if(!Blocked(__instance))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(XUiC_ItemPartStack),"CanSwap")]
internal static class RebirthCamperBodySwap
{
    private static bool Prefix(XUiC_ItemPartStack __instance,ref bool __result){if(!RebirthCamperBodyRemoval.Blocked(__instance))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(Vehicle),nameof(Vehicle.SetItemValue))]
internal static class RebirthCamperBodyVehicleAdmission
{
    private static bool Prefix(Vehicle __instance,ItemValue __0)
    {
        if(!RebirthCamperBodyAttachment.Camper(__0)||RebirthCamperBodyAttachment.HasBody(__0))return true;
        var old=__instance.itemValue;
        if(RebirthCamperBodyAttachment.LiveReason(__instance)!=null)return false;
        if(old!=null&&RebirthCamperBodyAttachment.HasBody(old)&&RebirthCamperBodyAttachment.Occupied(old))return false;
        return !RebirthCamperBodyAttachment.Occupied(__0);
    }
}

[HarmonyPatch(typeof(XUiC_ItemPartStackGrid),nameof(XUiC_ItemPartStackGrid.Update))]
internal static class RebirthCamperBodyGridRefresh
{
    private static void Prefix(XUiC_ItemPartStackGrid __instance)
    {if(__instance is XUiC_VehiclePartStackGrid grid)RebirthCamperBodyRemoval.Refresh(grid);}
}
