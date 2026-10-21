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
    internal static bool Occupied(ItemValue item)
    {
        if(!Camper(item))return false;
        try
        {
            if(item.TryGetMetadata("RebirthCamperTransferPending",out string pending)&&pending=="1")return true;
            if(item.TryGetMetadata("RebirthCamperIdentity",out string text)&&Guid.TryParse(text,out var identity))
            {
                if(RebirthCamperModuleItemCandidate.TryRestore(identity,item,out var storage,out var stations))
                    for(int i=0;i<20;i++)if(storage.GetSlot(i)?.IsEmpty()==false||stations.GetSlot(i)!=null)return true;
                if(item.TryGetMetadata(RebirthCamperEnergyState.Key,out string energy))
                {var state=RebirthCamperEnergyState.Decode(Convert.FromBase64String(energy),identity);if(state.Sources.Count>0||state.Mode!=RebirthCamperEnergyMode.Travel)return true;}
            }
            else if(item.TryGetMetadata("RebirthCamperModules",out string ignoredModules))return true;
            if(item.TryGetMetadata(RebirthCamperTurretState.Key,out string turrets)&&RebirthCamperTurretState.Decode(Convert.FromBase64String(turrets)).Slots.Any(s=>s.HasTurret))return true;
            return false;
        }
        catch{return true;}
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
    internal static bool Blocked(XUiC_ItemPartStack slot)=>slot.ItemValue?.ItemClass?.GetItemName()==RebirthCamperBodyAttachment.Modifier
        &&RebirthCamperBodyAttachment.Occupied(slot.xui?.AssembleItem?.CurrentItem?.itemValue);
    private static bool Prefix(XUiC_ItemPartStack __instance,ref bool __result){if(!Blocked(__instance))return true;__result=false;return false;}
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
        if(old!=null&&RebirthCamperBodyAttachment.HasBody(old)&&RebirthCamperBodyAttachment.Occupied(old))return false;
        return !RebirthCamperBodyAttachment.Occupied(__0);
    }
}
