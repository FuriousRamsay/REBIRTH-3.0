using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

// Must be installed before registering/constructing compact recovery item entities.
internal static class RebirthGearRecoveryEntityCapture
{
    internal static bool IsInstalled{get;private set;}
    internal static bool Install(Harmony harmony)
    {
        if(IsInstalled)return true;
        if(harmony==null)return false;
        var constructor=AccessTools.Constructor(typeof(EntityCreationData),new[]{typeof(Entity),typeof(StreamModeWrite)});
        if(constructor==null)return false;
        harmony.Patch(constructor,postfix:new HarmonyMethod(typeof(RebirthGearRecoveryEntityCapture),nameof(AfterCapture)));
        IsInstalled=true;return true;
    }
    private static void AfterCapture(EntityCreationData __instance,Entity _e,StreamModeWrite _eStreamMode)
    {
        if(!(_e is EntityRebirthGearRecoveryItem item))return;
        if(item.RecoveryIdentity==null||item.itemStack==null||item.itemStack.IsEmpty())throw new InvalidDataException("Missing recovery publication custody.");
        // Snapshot current stack directly; never reuse a potentially stale live bag.
        var detached=new Bag(new Vector2i(1,1),XUiC_ItemStack.StackLocationTypes.LootContainer,null);
        detached.SetSlots(new[]{item.itemStack.Clone()});
        var blob=StreamUtils.ToBlob(writer=>detached.Write(writer,_eStreamMode));
        if(blob==null||blob.Length==0||blob.Length>4*1024*1024)throw new InvalidDataException("Invalid recovery stack snapshot.");
        __instance.bagData=blob;
    }
}