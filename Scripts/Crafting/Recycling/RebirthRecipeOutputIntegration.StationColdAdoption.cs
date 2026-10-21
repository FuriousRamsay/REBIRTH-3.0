using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private static bool TryAdoptStationOutputCold(XUiC_RecipeStack card)
    {
        if(card==null||ui!=null||HasPendingUiOutput(card)||!ThreadManager.IsMainThread())return false;
        var window=card.GetParentByType<XUiC_WorkstationWindowGroup>();var actor=card.xui?.playerUI?.entityPlayer;
        var tile=window?.WorkstationData?.TileEntity as TileEntityWorkstation;var grid=window?.GetChildByType<XUiC_WorkstationOutputGrid>();
        if(actor==null||tile==null||grid==null||actor.world.IsRemote()||tile is RebirthCamperStationState)return false;
        StationOutputNativeRequest[] candidates;
        lock(stationNativeLoadGate)
        {
            if(stationNativeLoadRequests.Count==0)return false;
            candidates=stationNativeLoadRequests.Where(value=>!value.Closed&&!value.Adopted&&
                value.Loaded!=null&&ReferenceEquals(value.Loaded.Tile,tile)&&ReferenceEquals(value.Owner?.Actor,actor)).ToArray();
        }
        // Equal native bodies from different originals do not choose custody.
        if(candidates.Length!=1)return false;var source=candidates[0];var owner=source.Owner;var image=source.Image;
        try
        {
            if(!owner.Current()||!source.Loaded.Current()||!ReferenceEquals(card.xui,actor.PlayerUI?.xui)||
                owner.Creation!=((string)image.Attribute("creation"))||owner.Deaths!=((int)image.Attribute("deaths"))||
                card.OriginalItem!=null&&!card.OriginalItem.IsEmpty()||card.AmountToRepair!=0||
                RebirthRemoteResourcePlacedStationQueue.Encode(new[]{ActualCardRow(card)})!=image.Element("recipe").Value||
                !source.Grid.SequenceEqual(OwningGrid(grid.GetSlots()))||
                RebirthRemoteResourcePlacedStationQueue.Encode(window.craftingQueue.GetRecipesToCraft().Select(ActualCardRow).ToArray())!=RebirthRemoteResourcePlacedStationQueue.Encode(tile.Queue))return false;
            var cards=window.craftingQueue.GetRecipesToCraft();int index=(int)image.Attribute("stationQueueIndex");
            if(index<0||index>=cards.Length||!ReferenceEquals(cards[index],card)||
                !RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(actor,new[]{card.GetRecipe().GetName()},out int measured)||measured>8*1024*1024||
                !RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(owner.Data,
                    RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(actor,new[]{card.GetRecipe().GetName()})))return false;
            string path=Path.Combine(owner.Save,"rebirth","recipe-outputs","ui",source.Operation.ToString("N")+".xml");
            if(!ReadPersonalOutputSidecar(path+".effect-observed",out _,out var effect)||
                !RebirthNativeItemCodec.TryDecode(effect.Element("primaryAfterEffects").Value,out var primary)||
                card.outputItemValue!=null&&!card.outputItemValue.IsEmpty()&&RebirthNativeItemCodec.Encode(card.outputItemValue)!=RebirthNativeItemCodec.Encode(primary))return false;
            var outputs=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputs"));
            var binding=new XElement("recipeOutputStationColdBinding",new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("original",source.OriginalHash),new XAttribute("birth",source.Birth),new XAttribute("queueIndex",index),
                new XAttribute("ownerNativeDigest",UiOriginalHash(owner.Native)),new XAttribute("callbackReturned",source.CallbackReturned),
                new XAttribute("loadedBody",UiOriginalHash(source.Loaded.Request.ReturnedSpan)));
            string bindingHash=UiOriginalHash(UiOriginalBytes(binding));
            if(!PublishPersonalColdBinding(path+".station-cold-"+bindingHash,UiOriginalBytes(binding))||
                !owner.Current()||!source.Loaded.Current()||HasPendingUiOutput(card))return false;
            var scope=new UiScope{Card=card,Grid=grid,Outputs=outputs.Skip(1).Select(value=>value.Clone()).ToArray(),Inserted=true};
            var original=new UiOutputOriginal(scope,source,primary);scope.Original=original;
            // Bind the immutable already-generated primary to the actual
            // restored native row. No generation, reward, output setter or
            // queue decrement occurs during this adoption.
            RetainUiOutput(original);source.Adopted=true;source.ActiveOriginal=original;card.outputItemValue=primary;card.isInventoryFull=false;
            if(!pendingUiOutputs.TryGetValue(card,out var pending))throw new InvalidOperationException();
            pending.NativeContinuationEntered=source.CallbackReturned;pending.NativeCallbackEntered=source.CallbackReturned;pending.Returned=source.CallbackReturned;
            RetireSupersededStationOutputCold(source);
            return true;
        }
        catch{return false;}
    }
    private static void RetireSupersededStationOutputCold(StationOutputNativeRequest current)
    {
        lock(stationNativeLoadGate)foreach(var old in stationNativeLoadRequests.Where(value=>!ReferenceEquals(value,current)&&value.Adopted&&
            value.Operation==current.Operation&&value.OriginalHash==current.OriginalHash&&value.ActiveOriginal!=null).ToArray())
        {
            // Only the positively reconstructed same immutable operation may
            // retire an obsolete card binding. A still-current native tile
            // witness is never replaced by a second equal UI projection.
            if(old.Loaded?.Current()==true)continue;
            var original=old.ActiveOriginal;var oldCard=original.Scope.Card;
            if(pendingUiOutputs.TryGetValue(oldCard,out var pending)&&ReferenceEquals(pending.Source,original))
            {
                pendingUiOutputs.Remove(oldCard);pending.Source=null;
                foreach(var queued in stationOutputQueues.Where(value=>ReferenceEquals(value.Pending,pending)).ToArray())
                {queued.Dispose();stationOutputQueues.Remove(queued);}
                original.Dispose();
            }
        }
    }
}
