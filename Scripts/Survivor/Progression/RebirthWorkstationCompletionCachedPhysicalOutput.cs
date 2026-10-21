using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Internal full-transition validation mode, never a network phase. A
    // loaded final owner source can apply its remaining output+queue image in
    // one real packet without inventing an earlier accepted physical read.
    private const int LoadedCachedFinalValidation=7;
    // The original primary comes exclusively from the saved cache. Neither a
    // client primary, an Add argument, nor a new ItemValue can replace it.
    private static bool ValidateCachedPhysicalDelivery(CachedPhysicalHostAction action,XElement active,XElement delivery,
        byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,int phase)
    {
        try
        {
            var cache=action?.Issue?.Cache;
            bool inventory=(string)delivery?.Attribute("sink")=="Inventory";
            if(cache==null||!action.Current()||phase!=2&&phase!=3&&phase!=LoadedCachedFinalValidation||active?.Name!="nativeOwningBefore"||delivery?.Name!="nativeOwningDelivery"||
                !inventory&&(string)delivery.Attribute("sink")!="Output"||(bool?)active.Attribute("retry")!=true||
                (bool?)delivery.Attribute("retry")!=true||!cache.ReserveRetirementWorkspace(checked(16L*(prior.Length+proposed.Length+
                    RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(active)+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(delivery))+65536)))return false;
            var initial=cache.Original.Element("before").Elements().Single();var full=cache.CurrentFull;
            foreach(string name in new[]{"operation","world","viewer","creation","deaths","x","y","z","block","slot","beforeCount","nativeQueueCountBefore","startingOwner",
                "startingOwnerKnown","startingOwnerKey","startingOwnerCreation","startingOwnerDeaths","extrasDefinition"})
                if((string)active.Attribute(name)!=(string)initial.Attribute(name)||(string)delivery.Attribute(name)!=(string)active.Attribute(name))return false;
            if((string)active.Attribute("stationBirth")!=(string)delivery.Attribute("stationBirth"))return false;
            if((int?)delivery.Attribute("afterCount")!=(int)initial.Attribute("beforeCount")-1||
                !((bool?)delivery.Attribute("nativeOriginalAccepted")==true||(bool?)delivery.Attribute("nativeChildrenAccepted")==true)||
                (string)active.Element("recipeFull")!=(string)initial.Element("recipeFull")||
                (string)delivery.Element("recipeFull")!=(string)active.Element("recipeFull")||
                (string)delivery.Element("recipeNative")!=(string)active.Element("recipeNative")||
                !XNode.DeepEquals(active.Element("cachedPredecessor")?.Elements().SingleOrDefault(),full)||
                !XNode.DeepEquals(active.Element("cachedPredecessor"),delivery.Element("cachedPredecessor")))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryRead(prior,priorOffset,out var old,out _)||
                    !RebirthProductiveEarnedNativeBody.TryRead(proposed,proposedOffset,out var next,out ulong elapsed)||elapsed>GameTimer.Instance.ticks||
                    !RebirthRemoteResourcePlacedStationQueue.TryDecode((string)active.Element("uiQueueFull"),out var ui)||
                    !RebirthRemoteResourcePlacedStationQueue.TryDecode((string)initial.Element("recipeFull"),out var frozen)||frozen.Length!=1)return false;
                int index=(int)initial.Attribute("slot");
                if(index<0||index>=old.Queue.Length||old.Queue.Length!=next.Queue.Length||ui.Length!=old.Queue.Length||
                    old.Queue[index]?.Recipe==null||ui[index]?.Recipe==null||old.Queue[index].Multiplier!=(int)initial.Attribute("beforeCount")||
                    ui[index].Multiplier!=old.Queue[index].Multiplier||RecipeBinding(old.Queue[index])!=RecipeBinding(frozen[0])||
                    RebirthProductiveEarnedUiQueueOwner.Binding(ui[index])!=RebirthProductiveEarnedUiQueueOwner.Binding(frozen[0]))return false;
                var primary=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"primaryNative");
                var originalPrimary=RebirthProductiveEarnedUiRemote.ReadGrid(full,"primaryNative");
                var extras=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"extrasNative");
                var before=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"outputBefore");
                var after=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"outputAfter");
                var recipe=old.Queue[index].Recipe;
                if(primary.Length!=1||!CachedPhysicalSame(primary,originalPrimary)||primary[0].count!=recipe.count||
                    !RebirthProductiveEarnedUiRemote.ValidateExtraOutputs(recipe,delivery,extras)||
                    !RebirthWorkstationCompletionWitnessValidator.Matches(active,delivery,recipe.count))return false;
                if(inventory){if(!CachedPhysicalSame(before,after))return false;}
                else if(!RebirthRecipeOutputIntegration.PredictOwningOutput(before,primary[0],extras,true,out var merged)||!CachedPhysicalSame(merged,after))return false;
                foreach(string name in new[]{"outputBefore","bagBefore","beltBefore"})
                    if((string)delivery.Element(name)!=(string)active.Element(name))return false;
                foreach(string name in new[]{"bag","belt"})
                {
                    RebirthProductiveEarnedUiRemote.ReadGrid(delivery,name+"Before");RebirthProductiveEarnedUiRemote.ReadGrid(delivery,name+"After");
                    if(!inventory&&(string)delivery.Element(name+"Before")!=(string)delivery.Element(name+"After"))return false;
                }
                if(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.ValidateCachedPhysicalActor(action,active,delivery,primary[0],extras,inventory))return false;
                if(phase==2||phase==LoadedCachedFinalValidation)
                {if(!CachedPhysicalSame(old.Output,before)||!CachedPhysicalSame(next.Output,after))return false;old.Output=after;}
                if(phase==3||phase==LoadedCachedFinalValidation)
                {
                    if(!CachedPhysicalSame(old.Output,after)||!CachedPhysicalSame(next.Output,after))return false;
                    var row=old.Queue[index];row.Multiplier--;
                    if(row.Multiplier==0){row.Recipe=null;row.CraftingTimeLeft=0;row.IsCrafting=false;row.Quality=1;row.StartingEntityId=action.Issue.Viewer.entityId;row.OneItemCraftTime=0;}
                    else{row.CraftingTimeLeft=ui[index].OneItemCraftTime;row.IsCrafting=ui[index].IsCrafting;}
                }
                var expected=RebirthProductiveEarnedNativeBody.Encode(old,elapsed,out int offset);
                return offset==proposedOffset&&expected.SequenceEqual(proposed);
            }
        }
        catch{return false;}
    }
    private static bool CachedPhysicalSame(ItemStack[] left,ItemStack[] right)=>
        RebirthCamperInventorySnapshot.Capture(left).SequenceEqual(RebirthCamperInventorySnapshot.Capture(right));
}
