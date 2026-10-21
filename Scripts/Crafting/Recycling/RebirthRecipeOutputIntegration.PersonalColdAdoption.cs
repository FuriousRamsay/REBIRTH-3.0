using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private static bool PersonalColdIdentity(XElement row,string name,string attributes,string elements,Guid operation,string hash)
        =>ExactPersonalOutputFields(row,name,attributes,elements)&&(string)row.Attribute("operation")==operation.ToString("N")&&
            (string)row.Attribute("original")==hash;
    private static byte[] PersonalColdImage(XElement row,string name)
    {
        string text=row.Element(name).Value;byte[] bytes=Convert.FromBase64String(text);
        if(Convert.ToBase64String(bytes)!=text)throw new InvalidDataException();return bytes;
    }
    private static bool PersonalColdAbsent(string path)
        =>!File.Exists(path)&&!File.Exists(path+".tmp")&&!File.Exists(path+".bak");
    private static bool PublishPersonalColdBinding(string path,byte[] image)
        =>PublishUiOriginal(path,image)||ReadPersonalOutputSidecar(path,out var existing,out _)&&existing.SequenceEqual(image);
    private static bool TryPersonalColdNextAttempt(string path,Guid operation,string hash,int index,ItemStack incoming,byte[] bag,byte[] belt,byte[] equipment,out int attempt)
    {
        attempt=0;
        while(true)
        {
            string entryPath=path+".output-"+index+(attempt==0?"":".retry-"+attempt)+".entered";
            if(PersonalColdAbsent(entryPath))return true;
            if(incoming==null||!ReadPersonalOutputSidecar(entryPath,out _,out var entered)||
                !PersonalColdIdentity(entered,"recipeOutputEntered","operation,index,attempt,equipmentCallbacks,original","incoming,bag,belt,equipment,expectedBag,expectedBelt,expectedEquipmentItems",operation,hash)||
                (int)entered.Attribute("index")!=index||(int)entered.Attribute("attempt")!=attempt||(bool)entered.Attribute("equipmentCallbacks")||
                !PersonalColdImage(entered,"bag").SequenceEqual(bag)||!PersonalColdImage(entered,"belt").SequenceEqual(belt)||!PersonalColdImage(entered,"equipment").SequenceEqual(equipment)||
                !ReadPersonalOutputSidecar(entryPath+".refused",out _,out var refused)||
                !PersonalColdIdentity(refused,"recipeOutputRefused","operation,index,attempt,original","bag,belt,equipment",operation,hash)||
                (int)refused.Attribute("index")!=index||(int)refused.Attribute("attempt")!=attempt||
                !PersonalColdImage(refused,"bag").SequenceEqual(bag)||!PersonalColdImage(refused,"belt").SequenceEqual(belt)||!PersonalColdImage(refused,"equipment").SequenceEqual(equipment))return false;
            var actual=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(entered,"incoming"));
            if(actual.Length!=1||!RebirthStationGridIngredients.IsSameStackSnapshot(actual[0],incoming)||attempt==int.MaxValue)return false;
            attempt++;
        }
    }

    private static bool TryAdoptPersonalOutputCold(UiOutputLoadedOwner loaded,Guid operation)
    {
        IDisposable memory=null;
        try
        {
            if(ui!=null||loaded==null||!loaded.Current()||!loaded.Operations.Contains(operation)||
                loaded.Queue!=RebirthRemoteResourceClientOriginal.QueueImage(loaded.Actor.PlayerUI?.xui?.GetCraftingData())||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(loaded.Buffs,PersonalOutputReceipt(operation),out float marker,out bool present)||
                !present||marker<1f||marker>65f||marker!=(int)marker||loaded.Actor.Buffs.GetCustomVar(PersonalOutputReceipt(operation))!=marker)return false;
            int prefix=(int)marker;
            string path=Path.Combine(loaded.Save,"rebirth","recipe-outputs","ui",operation.ToString("N")+".xml");
            if(!PersonalColdAbsent(path+".queue-owner-saved")||!PersonalColdAbsent(path+".queue-observed")||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*UiOriginalMaximum+65536,out memory))return false;
            if(!ReadPersonalOutputSidecar(path,out var bytes,out var image)||
                !ExactPersonalOutputOriginal(image)||
                (string)image.Attribute("version")!="1"||(string)image.Attribute("operation")!=operation.ToString("N")||
                (string)image.Attribute("world")!=loaded.World.Guid||(string)image.Attribute("owner")!=loaded.Owner||
                (string)image.Attribute("creation")!=loaded.Creation||(int)image.Attribute("deaths")!=loaded.Deaths||
                (string)image.Attribute("sink")!="Inventory"||image.Element("personalQueueBefore").Value!=loaded.Queue)return false;
            int slot=(int)image.Attribute("personalQueueIndex"),count=(int)image.Attribute("beforeCount");
            var cards=loaded.Actor.PlayerUI.xui.FindWindowGroupByName("crafting")?.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
            if(slot<0||slot>=255||cards==null||slot>=cards.Length)return false;
            var card=cards[slot];var recipe=card?.GetRecipe();
            if(card==null||recipe==null||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty()||count<1||card.GetRecipeCount()!=count||card.AmountToRepair!=0||
                card.GetParentByType<XUiC_WorkstationWindowGroup>()!=null||!ReferenceEquals(card.xui,loaded.Actor.PlayerUI.xui)||
                card.StartingEntityId!=(int)image.Attribute("startingOwner")||HasPendingUiOutput(card)||
                RebirthRemoteResourcePlacedStationQueue.Encode(new[]{ActualCardRow(card)})!=image.Element("recipe").Value)return false;
            string hash=UiOriginalHash(bytes);
            if(!ReadPersonalOutputSidecar(path+".effect-entered",out _,out var entered)||
                !PersonalColdIdentity(entered,"recipeOutputEffectEntered","operation,effect,original","",operation,hash)||
                ((string)entered.Attribute("effect")!="giveExp"&&(string)entered.Attribute("effect")!="cached-native-delivery")||
                !ReadPersonalOutputSidecar(path+".effect-observed",out _,out var observed)||
                !PersonalColdIdentity(observed,"recipeOutputEffectObserved","operation,nativeRan,original","primaryAfterEffects",operation,hash)||
                (bool)observed.Attribute("nativeRan")!=((string)entered.Attribute("effect")=="giveExp")||
                !RebirthNativeItemCodec.TryDecode(observed.Element("primaryAfterEffects").Value,out var primary)||
                RebirthNativeItemCodec.Encode(primary)!=observed.Element("primaryAfterEffects").Value||primary.type!=recipe.itemValueType)return false;
            var outputs=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputs"));
            if(outputs.Length<1||outputs.Length>65||prefix>outputs.Length||outputs[0].count!=recipe.count||
                outputs[0].itemValue.type!=recipe.itemValueType||RebirthNativeItemCodec.Encode(outputs[0].itemValue)!=image.Element("primaryBeforeEffects").Value||
                outputs.Any(value=>value==null||value.IsEmpty())||(long)image.Attribute("nativeVersion")<0||
                !PersonalColdAbsent(path+".output-"+prefix+".accepted"))return false;
            bool callbackReturned=false;
            if(!PersonalColdAbsent(path+".native-continuation-entered")||!PersonalColdAbsent(path+".native-continuation-returned"))
            {
                if(prefix!=outputs.Length||!ReadPersonalOutputSidecar(path+".native-continuation-entered",out _,out var callbackEntry)||
                    !PersonalColdIdentity(callbackEntry,"recipeOutputNativeContinuationEntered","operation,original,outputPrefix","",operation,hash)||
                    (int)callbackEntry.Attribute("outputPrefix")!=prefix||
                    !ReadPersonalOutputSidecar(path+".native-continuation-returned",out _,out var callbackReturn)||
                    !PersonalColdIdentity(callbackReturn,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",operation,hash)||
                    (int)callbackReturn.Attribute("outputPrefix")!=prefix)return false;
                callbackReturned=true;
            }
            if(prefix==outputs.Length&&!callbackReturned&&(string)image.Attribute("callbackFence")!="1")return false;
            byte[] bag=PersonalColdImage(image,"bagBefore"),belt=PersonalColdImage(image,"beltBefore"),equipment=PersonalColdImage(image,"equipmentBefore");
            for(int index=0;index<prefix;index++)
            {
                if(!ReadPersonalOutputSidecar(path+".output-"+index+".accepted",out var acceptedBytes,out var accepted)||
                    !PersonalColdIdentity(accepted,"recipeOutputAccepted","operation,index,attempt,original","bag,belt,equipment",operation,hash)||
                    (int)accepted.Attribute("index")!=index||(int)accepted.Attribute("attempt")<0)return false;
                int attempt=(int)accepted.Attribute("attempt");
                string entryPath=path+".output-"+index+(attempt==0?"":".retry-"+attempt)+".entered";
                if(!ReadPersonalOutputSidecar(entryPath,out _,out var entry)||
                    !PersonalColdIdentity(entry,"recipeOutputEntered","operation,index,attempt,equipmentCallbacks,original","incoming,bag,belt,equipment,expectedBag,expectedBelt,expectedEquipmentItems",operation,hash)||
                    (int)entry.Attribute("index")!=index||(int)entry.Attribute("attempt")!=attempt||
                    !PersonalColdImage(entry,"bag").SequenceEqual(bag)||!PersonalColdImage(entry,"belt").SequenceEqual(belt)||
                    !PersonalColdImage(entry,"equipment").SequenceEqual(equipment))return false;
                var incoming=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(entry,"incoming"));
                var expected=index==0?new ItemStack(primary,recipe.count):outputs[index];
                if(incoming.Length!=1||!RebirthStationGridIngredients.IsSameStackSnapshot(incoming[0],expected))return false;
                bag=PersonalColdImage(accepted,"bag");belt=PersonalColdImage(accepted,"belt");equipment=PersonalColdImage(accepted,"equipment");
                if(!PersonalColdImage(entry,"expectedBag").SequenceEqual(bag)||!PersonalColdImage(entry,"expectedBelt").SequenceEqual(belt))return false;
                // The actual loaded full native prefix proves the complete
                // ordered paid set. Earlier save observers may be superseded
                // by that later full save; do not require every historical PDS
                // to remain the final file or manufacture an absent save fact.
                if(index!=prefix-1)continue;
                if(!ReadPersonalOutputSidecar(path+".output-"+index+".owner-saved",out _,out var saved)||
                    !PersonalColdIdentity(saved,"recipeOutputOwnerPrefixSaved","operation,index,original,accepted,owner,creation,deaths,entity,playerFile,nativeDigest,nativeLength,personalPrefix","",operation,hash)||
                    (int)saved.Attribute("index")!=index||(int)saved.Attribute("personalPrefix")!=index+1||
                    (string)saved.Attribute("accepted")!=UiOriginalHash(acceptedBytes)||(string)saved.Attribute("owner")!=loaded.Owner||
                    (string)saved.Attribute("creation")!=loaded.Creation||(int)saved.Attribute("deaths")!=loaded.Deaths||
                    (int)saved.Attribute("entity")<0||(string)saved.Attribute("playerFile")!=loaded.Path||
                    Convert.FromBase64String((string)saved.Attribute("nativeDigest")).Length!=32||
                    (int)saved.Attribute("nativeLength")<6||(int)saved.Attribute("nativeLength")>16*1024*1024)return false;
            }
            if(callbackReturned&&!TryReadScrapCompletedEquipment(path,operation,hash,recipe,prefix,equipment,out equipment))return false;
            if(!bag.SequenceEqual(loaded.Bag)||!belt.SequenceEqual(loaded.Belt)||!equipment.SequenceEqual(loaded.Equipment)||!loaded.Current())return false;
            if(!TryPersonalColdNextAttempt(path,operation,hash,prefix,prefix<outputs.Length?outputs[prefix]:null,bag,belt,equipment,out int nextAttempt))return false;
            // Narrow only this positively bound slot. No unrelated restored
            // card or marker gains permission from another original's load.
            if(!BindPersonalOutputRecoverySlot(loaded.Actor,operation,slot))return false;
            var scope=new UiScope{Card=card,Outputs=outputs.Skip(1).Select(value=>value.Clone()).ToArray(),Cached=(bool)image.Attribute("cached")};
            var original=new UiOutputOriginal(scope,loaded,image,bytes,primary,operation,prefix,bag,belt,equipment);scope.Original=original;
            original.OutputAttempt=nextAttempt;
            var binding=new XElement("recipeOutputPersonalColdBinding",new XAttribute("operation",operation.ToString("N")),new XAttribute("original",hash),
                new XAttribute("native",UiOriginalHash(loaded.Native)),new XAttribute("slot",slot),new XAttribute("prefix",prefix),new XAttribute("attempt",nextAttempt),
                new XAttribute("callbackFence",1),new XElement("recipe",image.Element("recipe").Value));
            string digest=BitConverter.ToString(Convert.FromBase64String(UiOriginalHash(loaded.Native))).Replace("-","").ToLowerInvariant();
            if(!PublishPersonalColdBinding(path+".personal-cold-"+digest,UiOriginalBytes(binding))||!loaded.Current()||HasPendingUiOutput(card))return false;
            // The restored card did not persist the generated decorated value.
            // Bind its exact saved original value; outputStack is intercepted
            // by the retained original before it can generate/award anything.
            card.outputItemValue=primary;card.isInventoryFull=false;original.Memory=memory;RetainUiOutput(original);memory=null;
            if(callbackReturned&&pendingUiOutputs.TryGetValue(card,out var pending))pending.NativeContinuationEntered=pending.Returned=true;
            return true;
        }
        catch{return false;}
        finally{memory?.Dispose();}
    }
    private static bool CompleteUiOutputReturnedNative(XUiC_RecipeStack card)
    {
        if(ui!=null||!pendingUiOutputs.TryGetValue(card,out var pending)||!pending.NativeContinuationEntered||!pending.Returned||pending.Source==null)return false;
        var original=pending.Source;var previous=ui;ui=original.Scope;
        try{return (original.Scope.Grid==null||original.HasStationGrid)&&!original.OutputEntered&&original.CallbackPhysicalComplete&&
            original.Current()&&original.CallbackOwnerCurrent()&&CanInsertOutputs(original.Actor);}
        finally{ui=previous;}
    }
    private static bool CanAdvancePendingPersonalOutput(XUiC_RecipeStack card)
    {
        if(!pendingUiOutputs.TryGetValue(card,out var pending)||pending.Source==null||!IsUiOutputPersonalCard(pending.Source))return true;
        var original=pending.Source;if(ui!=null)return false;var previous=ui;ui=original.Scope;
        try
        {
            if(card.OriginalItem!=null&&!card.OriginalItem.IsEmpty())return false;
            if(!original.Current()||original.OutputEntered||!CanInsertOutputs(original.Actor))return false;
            if(original.OutputPrefix==0)
            {
                if(original.OutputAttempt<1||!original.OwnerBeforeBag.SequenceEqual(OwningGrid(original.Actor.bag.ItemGrid.items))||
                    !original.OwnerBeforeBelt.SequenceEqual(OwningGrid(original.Actor.inventory.ItemGrid.items))||
                    !original.OwnerBeforeEquipment.SequenceEqual(OwningEquipmentNative(original.Actor)))return false;
            }
            else if(!original.AcceptedOwnerCurrent())return false;
            if(pending.NativeContinuationEntered)return pending.Returned&&original.OutputPrefix==original.Scope.Outputs.Count+1;
            if(pending.Returned||original.OutputPrefix<0||original.OutputPrefix>original.Scope.Outputs.Count+1)return false;
            var remaining=(original.OutputPrefix==0?new[]{new ItemStack(original.Primary,original.Recipe.count)}.Concat(original.Scope.Outputs):
                original.Scope.Outputs.Skip(original.OutputPrefix-1)).Select(value=>value.Clone()).ToArray();
            return remaining.Length==0||RebirthRecipeOutputInventoryPrediction.TryPlan(original.Actor,remaining,out _,out _,out _);
        }
        finally{ui=previous;}
    }
    private sealed class PersonalCraftedObservation
    {internal UiOutputOriginal Original;internal UiOutputPending Pending;internal bool Scrap;}
    internal static object EnterPersonalOutputCraftedItem(ItemStack item,bool ran)
        =>EnterPersonalOutputCallback(item,ran,false);
    internal static object EnterPersonalOutputScrappedItem(ItemStack item,bool ran)
        =>EnterPersonalOutputCallback(item,ran,true);
    // Only the private continuation borrows AddItemToInventory. That helper
    // always calls CraftedItem; the original fresh scrap branch calls
    // ScrappedItem with its frozen recipe ingredient instead.
    internal static bool RedirectPersonalOutputScrap(QuestEventManager events,ItemStack item,bool ran)
    {
        var original=ui?.Original;
        if(!ran||original==null||!original.Recipe.IsScrap||!ui.CachedContinuation||!ui.CachedPrimaryConsumed||
            !(IsUiOutputPersonalCard(original)||original.GridCallbackFence||original.RemoteCallbackFence)||item==null||
            !RebirthStationGridIngredients.IsSameStackSnapshot(item,new ItemStack(original.Primary,original.Recipe.count)))return false;
        if(original.Recipe.ingredients==null||original.Recipe.ingredients.Count==0||original.Recipe.ingredients[0]==null)
            throw new InvalidOperationException("Saved scrap continuation has no original ingredient.");
        events.ScrappedItem(original.Recipe.ingredients[0]);
        original.Actor.equipment.UnlockCosmeticItem(original.Recipe.ingredients[0].itemValue.ItemClass);
        return true;
    }
    private static object EnterPersonalOutputCallback(ItemStack item,bool ran,bool scrap)
    {
        var original=ui?.Original;
        if(!ran||original==null||!(IsUiOutputPersonalCard(original)||original.GridCallbackFence||original.RemoteCallbackFence)||item==null)return null;
        var expected=scrap&&original.Recipe.IsScrap&&original.Recipe.ingredients!=null&&original.Recipe.ingredients.Count>0
            ?original.Recipe.ingredients[0]:!scrap?new ItemStack(original.Primary,original.Recipe.count):null;
        if(expected==null||!RebirthStationGridIngredients.IsSameStackSnapshot(item,expected))return null;
        if(!original.Current()||!original.CallbackOwnerCurrent()||original.OutputEntered||!original.CallbackPhysicalComplete||
            !pendingUiOutputs.TryGetValue(original.Scope.Card,out var pending)||!ReferenceEquals(pending.Source,original)||
            pending.NativeCallbackEntered||
            pending.NativeContinuationEntered&&!(original.Scope.CachedContinuation&&original.Scope.CachedPrimaryConsumed))
            throw new InvalidOperationException("Personal output callback has no unentered exact original.");
        var entry=new XElement("recipeOutputNativeContinuationEntered",new XAttribute("operation",original.Operation.ToString("N")),
            new XAttribute("original",UiOriginalHash(original.Original)),new XAttribute("outputPrefix",original.CallbackPrefix));
        if(!PublishPersonalColdBinding(original.Path+".native-continuation-entered",UiOriginalBytes(entry)))
            throw new InvalidOperationException("Personal output callback entry could not be saved.");
        pending.NativeContinuationEntered=pending.NativeCallbackEntered=true;
        return new PersonalCraftedObservation{Original=original,Pending=pending,Scrap=scrap};
    }
    internal static void ObservePersonalOutputCraftedItem(object state,bool ran,Exception error)
    {
        if(state is not PersonalCraftedObservation observation||!ran||error!=null)return;
        var original=observation.Original;var pending=observation.Pending;
        if(!original.Current()||!original.CallbackOwnerCurrent()||!pending.NativeContinuationEntered||
            !pendingUiOutputs.TryGetValue(original.Scope.Card,out var retained)||!ReferenceEquals(retained,pending))return;
        if(observation.Scrap)
        {
            var quest=new XElement("recipeOutputScrapQuestReturned",new XAttribute("operation",original.Operation.ToString("N")),
                new XAttribute("original",UiOriginalHash(original.Original)),new XAttribute("outputPrefix",original.CallbackPrefix));
            PublishPersonalColdBinding(original.Path+".scrap-quest-returned",UiOriginalBytes(quest));return;
        }
        var returned=new XElement("recipeOutputNativeContinuationReturned",new XAttribute("operation",original.Operation.ToString("N")),
            new XAttribute("original",UiOriginalHash(original.Original)),new XAttribute("outputPrefix",original.CallbackPrefix));
        if(PublishPersonalColdBinding(original.Path+".native-continuation-returned",UiOriginalBytes(returned)))
        {pending.Returned=true;SaveCompletedOutputCallbackOwner(original,UiOriginalBytes(returned));}
    }
    private static bool TryCompletePersonalOutputLoadedQueue(UiOutputLoadedOwner loaded,Guid operation)
    {
        if(loaded==null||!loaded.Current()||!loaded.Operations.Contains(operation)||
            loaded.Queue!=RebirthRemoteResourceClientOriginal.QueueImage(loaded.Actor.PlayerUI?.xui?.GetCraftingData()))return false;
        string path=Path.Combine(loaded.Save,"rebirth","recipe-outputs","ui",operation.ToString("N")+".xml");
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!PersonalColdAbsent(path+".queue-owner-saved")||
                !ReadPersonalOutputSidecar(path,out var bytes,out var image)||
                !ExactPersonalOutputOriginal(image)||
                (string)image.Attribute("version")!="1"||(string)image.Attribute("operation")!=operation.ToString("N")||
                (string)image.Attribute("world")!=loaded.World.Guid||(string)image.Attribute("owner")!=loaded.Owner||
                (string)image.Attribute("creation")!=loaded.Creation||(int)image.Attribute("deaths")!=loaded.Deaths||
                (string)image.Attribute("sink")!="Inventory"||(int)image.Attribute("personalQueueIndex")<0||(int)image.Attribute("personalQueueIndex")>=255)return false;
            string hash=UiOriginalHash(bytes);int count=(int)image.Attribute("beforeCount");
            if(!RebirthRemoteResourcePlacedStationQueue.TryDecode((string)image.Element("recipe"),out var originalRows)||originalRows.Length!=1||originalRows[0]?.Recipe==null)return false;
            var outputs=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputs"));int prefix=outputs.Length;
            if(count<1||prefix<1||prefix>65||outputs.Any(value=>value==null||value.IsEmpty())||!ReadPersonalOutputSidecar(path+".queue-observed",out _,out var queue)||
                !PersonalColdIdentity(queue,"recipeOutputQueueObserved","operation,original,beforeCount,afterCount","personalQueueAfter",operation,hash)||
                (int)queue.Attribute("beforeCount")!=count||(int)queue.Attribute("afterCount")!=count-1||
                queue.Element("personalQueueAfter").Value!=loaded.Queue||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(loaded.Buffs,PersonalOutputReceipt(operation),out float phase,out bool present)||!present||phase!=prefix||
                loaded.Actor.Buffs.GetCustomVar(PersonalOutputReceipt(operation))!=prefix||
                !ReadPersonalOutputSidecar(path+".output-"+(prefix-1)+".accepted",out var acceptedBytes,out var accepted)||
                !PersonalColdIdentity(accepted,"recipeOutputAccepted","operation,index,attempt,original","bag,belt,equipment",operation,hash)||
                (int)accepted.Attribute("index")!=prefix-1||(int)accepted.Attribute("attempt")<0||
                !PersonalColdImage(accepted,"bag").SequenceEqual(loaded.Bag)||!PersonalColdImage(accepted,"belt").SequenceEqual(loaded.Belt)||
                !TryReadScrapCompletedEquipment(path,operation,hash,originalRows[0].Recipe,prefix,PersonalColdImage(accepted,"equipment"),out var completedEquipment)||
                !completedEquipment.SequenceEqual(loaded.Equipment)||
                !ReadPersonalOutputSidecar(path+".output-"+(prefix-1)+".owner-saved",out _,out var saved)||
                !PersonalColdIdentity(saved,"recipeOutputOwnerPrefixSaved","operation,index,original,accepted,owner,creation,deaths,entity,playerFile,nativeDigest,nativeLength,personalPrefix","",operation,hash)||
                (int)saved.Attribute("index")!=prefix-1||(int)saved.Attribute("personalPrefix")!=prefix||
                (string)saved.Attribute("accepted")!=UiOriginalHash(acceptedBytes)||(string)saved.Attribute("owner")!=loaded.Owner||
                (string)saved.Attribute("creation")!=loaded.Creation||(int)saved.Attribute("deaths")!=loaded.Deaths||
                (int)saved.Attribute("entity")<0||(string)saved.Attribute("playerFile")!=loaded.Path||
                Convert.FromBase64String((string)saved.Attribute("nativeDigest")).Length!=32||
                (int)saved.Attribute("nativeLength")<6||(int)saved.Attribute("nativeLength")>16*1024*1024||!loaded.Current())return false;
            var terminal=new XElement("recipeOutputPersonalQueueSaved",new XAttribute("operation",operation.ToString("N")),new XAttribute("original",hash),
                new XAttribute("owner",loaded.Owner),new XAttribute("creation",loaded.Creation),new XAttribute("deaths",loaded.Deaths),
                new XAttribute("beforeCount",count),new XAttribute("afterCount",count-1),new XAttribute("outputPrefix",prefix),
                new XAttribute("playerFile",loaded.Path),new XAttribute("nativeDigest",UiOriginalHash(loaded.Native)),new XAttribute("nativeLength",loaded.Native.Length),
                new XAttribute("queueDigest",UiOriginalHash(Encoding.UTF8.GetBytes(loaded.Queue))),new XAttribute("personalPrefix",prefix));
            return loaded.Current()&&PublishPersonalColdBinding(path+".queue-owner-saved",UiOriginalBytes(terminal));
        }
    }
}
