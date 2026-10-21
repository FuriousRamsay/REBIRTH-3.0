using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class ScrapUnlockObservation
    {internal UiOutputOriginal Original;internal UiOutputPending Pending;internal byte[] Before,After;}
    private static byte[] ScrapEquipmentImage(Equipment equipment)
    {
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);equipment.Write(writer,StreamModeWrite.Persistency);writer.Flush();if(stream.Length>4*1024*1024)throw new InvalidDataException();return stream.ToArray();}
    }
    private static bool TryPlanScrapEquipment(byte[] before,ItemClass item,out byte[] after)
    {
        after=null;if(before==null||before.Length<1||before.Length>4*1024*1024||item==null)return false;
        try
        {
            Equipment detached;
            using(var stream=new MemoryStream(before,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
            {reader.SetBaseStream(stream);detached=Equipment.Read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)return false;}
            if(!ScrapEquipmentImage(detached).SequenceEqual(before))return false;
            // Exact native UnlockCosmeticItem rule on a detached codec image;
            // no native callback, entity binding or live equipment setter.
            if(Equipment.CosmeticMappingStringID.TryGetValue(item.GetItemName(),out int id)&&!detached.m_unlockedCosmetics.Contains(id))detached.m_unlockedCosmetics.Add(id);
            after=ScrapEquipmentImage(detached);return true;
        }
        catch{return false;}
    }
    internal static object EnterOutputScrapUnlock(Equipment equipment,ItemClass item,bool ran)
    {
        var original=ui?.Original;
        if(!ran||original==null||!original.Recipe.IsScrap||!(IsUiOutputPersonalCard(original)||original.GridCallbackFence)||
            !ReferenceEquals(original.Actor.equipment,equipment)||original.Recipe.ingredients==null||original.Recipe.ingredients.Count<1||
            !ReferenceEquals(original.Recipe.ingredients[0].itemValue.ItemClass,item))return null;
        if(!original.Current()||!original.CallbackOwnerCurrent()||!original.CallbackPhysicalComplete||
            !pendingUiOutputs.TryGetValue(original.Scope.Card,out var pending)||!ReferenceEquals(pending.Source,original)||!pending.NativeCallbackEntered||pending.NativeCosmeticEntered||
            !ReadPersonalOutputSidecar(original.Path+".scrap-quest-returned",out _,out var quest)||
            !PersonalColdIdentity(quest,"recipeOutputScrapQuestReturned","operation,original,outputPrefix","",original.Operation,UiOriginalHash(original.Original))||
            (int?)quest.Attribute("outputPrefix")!=original.CallbackPrefix)
            throw new InvalidOperationException("Scrap unlock has no exact returned quest original.");
        var before=OwningEquipmentNative(original.Actor);
        if(!TryPlanScrapEquipment(before,item,out var after))throw new InvalidOperationException("Scrap unlock native equipment cannot be predicted.");
        var entry=ScrapUnlockRecord(original,"recipeOutputScrapUnlockEntered",before,after);
        if(!PublishPersonalColdBinding(original.Path+".scrap-unlock-entered",UiOriginalBytes(entry)))throw new InvalidOperationException("Scrap unlock entry could not be saved.");
        pending.NativeCosmeticEntered=true;return new ScrapUnlockObservation{Original=original,Pending=pending,Before=before,After=after};
    }
    private static XElement ScrapUnlockRecord(UiOutputOriginal original,string name,byte[] before,byte[] after)
        =>new XElement(name,new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("original",UiOriginalHash(original.Original)),
            new XAttribute("outputPrefix",original.CallbackPrefix),new XElement("equipmentBefore",Convert.ToBase64String(before)),new XElement("equipmentAfter",Convert.ToBase64String(after)));
    private static bool TryReadScrapCompletedEquipment(string path,Guid operation,string hash,Recipe recipe,int prefix,byte[] before,out byte[] after)
    {
        after=before;if(!recipe.IsScrap)return true;
        if(recipe.ingredients==null||recipe.ingredients.Count<1||recipe.ingredients[0]?.itemValue?.ItemClass==null||
            !ReadPersonalOutputSidecar(path+".scrap-quest-returned",out _,out var quest)||
            !PersonalColdIdentity(quest,"recipeOutputScrapQuestReturned","operation,original,outputPrefix","",operation,hash)||(int?)quest.Attribute("outputPrefix")!=prefix||
            !ReadPersonalOutputSidecar(path+".scrap-unlock-entered",out _,out var entered)||
            !PersonalColdIdentity(entered,"recipeOutputScrapUnlockEntered","operation,original,outputPrefix","equipmentBefore,equipmentAfter",operation,hash)||(int?)entered.Attribute("outputPrefix")!=prefix||
            !ReadPersonalOutputSidecar(path+".scrap-unlock-returned",out _,out var returned)||
            !PersonalColdIdentity(returned,"recipeOutputScrapUnlockReturned","operation,original,outputPrefix","equipmentBefore,equipmentAfter",operation,hash)||(int?)returned.Attribute("outputPrefix")!=prefix||
            !PersonalColdImage(entered,"equipmentBefore").SequenceEqual(before)||!PersonalColdImage(returned,"equipmentBefore").SequenceEqual(before)||
            !TryPlanScrapEquipment(before,recipe.ingredients[0].itemValue.ItemClass,out var expected)||
            !PersonalColdImage(entered,"equipmentAfter").SequenceEqual(expected)||!PersonalColdImage(returned,"equipmentAfter").SequenceEqual(expected))return false;
        after=expected;return true;
    }
    internal static void ObserveOutputScrapUnlock(object state,bool ran,Exception error)
    {
        if(state is not ScrapUnlockObservation observation||!ran||error!=null)return;
        var original=observation.Original;var pending=observation.Pending;
        if(!original.Current()||!observation.After.SequenceEqual(OwningEquipmentNative(original.Actor))||
            !pendingUiOutputs.TryGetValue(original.Scope.Card,out var retained)||!ReferenceEquals(retained,pending))return;
        // Only the exact predicted cosmetic delta may advance the full native
        // owner image; every equipped item and unrelated serialized field stays
        // fixed by the detached native codec comparison above.
        var old=original.CallbackEquipmentAfter;original.CallbackEquipmentAfter=observation.After;
        var previous=original.AcceptedEquipment;if(!original.GridCallbackFence)original.AcceptedEquipment=observation.After;
        if(!original.CallbackOwnerCurrent()){original.CallbackEquipmentAfter=old;original.AcceptedEquipment=previous;return;}
        if(!PublishPersonalColdBinding(original.Path+".scrap-unlock-returned",UiOriginalBytes(ScrapUnlockRecord(original,"recipeOutputScrapUnlockReturned",observation.Before,observation.After))))return;
        var returned=new XElement("recipeOutputNativeContinuationReturned",new XAttribute("operation",original.Operation.ToString("N")),
            new XAttribute("original",UiOriginalHash(original.Original)),new XAttribute("outputPrefix",original.CallbackPrefix));
        if(PublishPersonalColdBinding(original.Path+".native-continuation-returned",UiOriginalBytes(returned)))
        {pending.Returned=true;SaveCompletedOutputCallbackOwner(original,UiOriginalBytes(returned));}
    }
    private static void SaveCompletedOutputCallbackOwner(UiOutputOriginal original,byte[] returned)
    {
        try
        {
            if(original.World.IsRemote()){RegisterRemoteCallbackOwner(original,returned);return;}
            if(!original.Current()||!original.CallbackOwnerCurrent())return;
            int index=original.CallbackPrefix-1;if(index<0)return;string accepted=UiOriginalHash(returned);
            if(!original.GridCallbackFence)
            {
                if(!original.Recipe.IsScrap||!ReadPersonalOutputSidecar(original.Path+".output-"+index+".accepted",out var image,out _))return;
                accepted=UiOriginalHash(image);
            }
            RegisterUiOutputOwnerPrefix(original,index,OwningGrid(original.Actor.bag.ItemGrid.items),OwningGrid(original.Actor.inventory.ItemGrid.items),OwningEquipmentNative(original.Actor),accepted);
            if(IsUiOutputPersonalCard(original))SaveUiOutputPersonalPrefix(original,index);
            else GameManager.Instance.SaveLocalPlayerData();
        }
        catch{/* Native callback remains returned; owning save grants no replay. */}
    }
}

[HarmonyPatch(typeof(Equipment),nameof(Equipment.UnlockCosmeticItem),new[]{typeof(ItemClass)})]
internal static class RebirthRecipeOutputScrapUnlock
{
    private static void Prefix(Equipment __instance,ItemClass __0,bool __runOriginal,out object __state)
        =>__state=RebirthRecipeOutputIntegration.EnterOutputScrapUnlock(__instance,__0,__runOriginal);
    private static Exception Finalizer(Exception __exception,bool __runOriginal,object __state)
    {try{RebirthRecipeOutputIntegration.ObserveOutputScrapUnlock(__state,__runOriginal,__exception);}catch{}return __exception;}
}
