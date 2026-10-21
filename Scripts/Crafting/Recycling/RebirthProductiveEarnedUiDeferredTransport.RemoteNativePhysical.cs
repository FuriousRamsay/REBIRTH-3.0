using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Read-only admission under the original's charged callback workspace.
    // Parsed delivery is corroboration of the already saved original; it
    // cannot mint a source, a native effect, or a queued-owner incarnation.
    internal static bool ValidateRemoteNativePhysical(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,
        byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,int phase,out XElement physical)
        =>ValidateNativePhysical(source,contribution,prior,priorOffset,proposed,proposedOffset,phase,false,out physical);
    internal static bool ValidateSameOwnerNativePhysical(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,
        byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,int phase,out XElement physical)
        =>ValidateNativePhysical(source,contribution,prior,priorOffset,proposed,proposedOffset,phase,true,out physical);
    private static bool ValidateNativePhysical(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,
        byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,int phase,bool direct,out XElement physical)
    {
        physical=null;
        try
        {
            var identity=direct?contribution?.Element("nativeViewerEffect"):contribution;
            if(source?.CurrentViewer()!=true||!source.AdmissionCurrent()||!source.BeforeSaved||(phase!=2&&phase!=3)||
                contribution?.Name!=(direct?"sameOwnerEffect":"deferredNativeContribution")||
                (int?)identity?.Attribute("version")!=1||(int?)contribution.Attribute("phase")!=phase||
                (string)identity.Attribute("operation")!=source.Operation.ToString("N")||
                (string)identity.Attribute("nonce")!=source.Nonce.ToString("N")||
                (string)identity.Attribute(direct?"source":"original")!=source.SourceHash||
                !direct&&(string)contribution.Attribute("birth")!=source.Birth.ToString("N"))return false;
            var active=contribution.Element("activeBefore")?.Elements().SingleOrDefault();
            var delivery=contribution.Element("physicalDelivery")?.Elements().SingleOrDefault();
            bool inventory= (string)delivery?.Attribute("sink")=="Inventory";
            if(active?.Name!="nativeOwningBefore"||delivery?.Name!="nativeOwningDelivery"||!source.IsSavedActiveBefore(active)||
                (int?)active.Attribute("version")!=1||(int?)delivery.Attribute("version")!=1||
                (string)active.Attribute("operation")!=source.Operation.ToString("N")||
                (string)active.Attribute("world")!=source.WorldGuid||(string)active.Attribute("viewer")!=source.ViewerKey||
                (string)active.Attribute("creation")!=source.ViewerCreation||(int?)active.Attribute("deaths")!=source.ViewerDeaths||
                (int?)active.Attribute("slot")!=source.Index||(int?)active.Attribute("beforeCount")!=source.BeforeCount||
                (int?)active.Attribute("startingOwner")!=source.EarlierOwner.StartingEntityId||
                (bool?)active.Attribute("startingOwnerKnown")!=true||(string)active.Attribute("startingOwnerKey")!=source.EarlierOwner.OriginalOwnerKey||
                (string)active.Attribute("startingOwnerCreation")!=source.EarlierOwner.Creation||(int?)active.Attribute("startingOwnerDeaths")!=source.EarlierOwner.Deaths)return false;
            foreach(string name in new[]{"operation","world","save","viewer","creation","deaths","stationBirth","extrasDefinition","startingOwner","startingOwnerKnown",
                "startingOwnerKey","startingOwnerCreation","startingOwnerDeaths","x","y","z","block","slot","beforeCount","nativeQueueCountBefore","retry"})
                if((string)active.Attribute(name)!=(string)delivery.Attribute(name))return false;
            var position=source.Station.ToWorldPos();
            if((int?)active.Attribute("x")!=position.x||(int?)active.Attribute("y")!=position.y||(int?)active.Attribute("z")!=position.z||
                (int?)active.Attribute("block")!=source.World.GetBlock(position).type||
                !string.IsNullOrEmpty((string)active.Attribute("stationBirth"))&&(string)active.Attribute("stationBirth")!=source.Birth.ToString("N")||
                (int?)delivery.Attribute("afterCount")!=source.BeforeCount-1||!inventory&&(string)delivery.Attribute("sink")!="Output"||
                !((bool?)delivery.Attribute("nativeOriginalAccepted")==true||(bool?)delivery.Attribute("nativeChildrenAccepted")==true)||
                (string)delivery.Element("recipeFull")!=(string)active.Element("recipeFull")||
                (string)delivery.Element("recipeNative")!=(string)active.Element("recipeNative"))return false;
            if(!RebirthRemoteResourcePlacedStationQueue.TryDecode((string)active.Element("recipeFull"),out var frozen)||frozen.Length!=1||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode((string)active.Element("uiQueueFull"),out var ui)||
                !RebirthProductiveEarnedNativeBody.TryRead(prior,priorOffset,out var old,out _)||
                !RebirthProductiveEarnedNativeBody.TryRead(proposed,proposedOffset,out var next,out ulong elapsed)||
                elapsed>GameTimer.Instance.ticks||source.Index<0||source.Index>=old.Queue.Length||old.Queue.Length!=next.Queue.Length||ui.Length!=old.Queue.Length)return false;
            int index=source.Index;var row=old.Queue[index];var uiRow=ui[index];
            if(row?.Recipe==null||uiRow?.Recipe==null||frozen[0]?.Recipe==null||row.Multiplier!=source.BeforeCount||uiRow.Multiplier!=source.BeforeCount||
                row.StartingEntityId!=source.EarlierOwner.StartingEntityId||uiRow.StartingEntityId!=source.EarlierOwner.StartingEntityId||
                RebirthProductiveEarnedUiQueueOwner.Binding(uiRow)!=RebirthProductiveEarnedUiQueueOwner.Binding(frozen[0])||
                RebirthWorkstationCompletionPipeline.RecipeBinding(row)!=RebirthWorkstationCompletionPipeline.RecipeBinding(frozen[0]))return false;
            var liveRecipe=new RecipeQueueItem{Recipe=source.Recipe,Multiplier=1,Quality=row.Quality,StartingEntityId=row.StartingEntityId,OneItemCraftTime=row.OneItemCraftTime};
            if(RebirthProductiveEarnedUiQueueOwner.Binding(liveRecipe)!=RebirthProductiveEarnedUiQueueOwner.Binding(frozen[0])||
                (string)active.Attribute("extrasDefinition")!=RebirthRecipeOutputs.DefinitionIdentity(source.Recipe))return false;
            foreach(string name in new[]{"outputBefore","bagBefore","beltBefore"})
                if((string)delivery.Element(name)!=(string)active.Element(name))return false;
            if(!inventory&&((string)delivery.Element("bagAfter")!=(string)active.Element("bagBefore")||
                (string)delivery.Element("beltAfter")!=(string)active.Element("beltBefore")))return false;
            var outputBefore=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"outputBefore");
            var outputAfter=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"outputAfter");
            var primary=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"primaryNative");
            var extras=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"extrasNative");
            // Decode both unchanged actor grids too: equality of malformed text
            // is not a canonical full actor inventory observation.
            RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"bagBefore");RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"beltBefore");
            bool retry=(bool?)delivery.Attribute("retry")==true;
            if(primary.Length!=1||primary[0]?.itemValue==null||primary[0].count!=source.Recipe.count||
                (int?)active.Attribute("primaryType")!=source.Recipe.itemValueType||(int?)active.Attribute("primaryCount")!=source.Recipe.count||
                (int?)active.Attribute("quality")!=uiRow.Quality||
                primary[0].itemValue.type!=source.Recipe.itemValueType||primary[0].itemValue.ItemClass==null||
                primary[0].itemValue.Quality!=(primary[0].itemValue.ItemClass.HasQuality?uiRow.Quality:0)||
                !RebirthProductiveEarnedUiRemote.ValidateExtraOutputs(source.Recipe,delivery,extras)||
                !RebirthWorkstationCompletionWitnessValidator.Matches(active,delivery,source.Recipe.count))return false;
            if(direct)
            {if(!NativePhysicalSame(primary,RebirthProductiveEarnedUiRemote.ReadGrid(identity,"primaryNative")))return false;}
            else if(!RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(contribution,"nativeAddArgument"),out var argument)||
                argument.CrafterEntityID!=source.EarlierOwner.StartingEntityId||argument.RecipeName!=source.Recipe.GetName()||
                !RebirthRecipeOutputIntegration.MatchesNativeOutputScrappedName(source.Recipe,argument.ItemScrapped)||
                argument.CraftExpGain!=source.Recipe.craftExpGain||argument.RecipeUsedCount!=1||!NativePhysicalSame(primary,new[]{argument.CraftedItemStack}))return false;
            if(inventory)
            {if(!NativePhysicalSame(outputBefore,outputAfter)||!NativeViewerPds.ValidateInventory(source,active,delivery,primary[0],extras))return false;}
            else if(!RebirthRecipeOutputIntegration.PredictOwningOutput(outputBefore,primary[0],extras,retry,out var merged)||!NativePhysicalSame(merged,outputAfter))return false;
            if(retry)
            {
                var predecessor=delivery.Element("cachedPredecessor")?.Elements().SingleOrDefault();
                if(predecessor?.Name!="nativeOwningFullBoundary"||(string)predecessor.Attribute("operation")!=source.Operation.ToString("N")||
                    !XNode.DeepEquals(delivery.Element("cachedPredecessor"),active.Element("cachedPredecessor")))return false;
            }
            else if(delivery.Element("cachedPredecessor")!=null||active.Element("cachedPredecessor")!=null)return false;
            if(phase==2)
            {
                if(!NativePhysicalSame(old.Output,outputBefore)||!NativePhysicalSame(next.Output,outputAfter))return false;
                old.Output=outputAfter;
            }
            else
            {
                if(!NativePhysicalSame(old.Output,outputAfter)||!NativePhysicalSame(next.Output,outputAfter))return false;
                // Native UI completion changes one selected row. The fresh
                // branch adds its interval to its actual UI timer; cached retry
                // initializes exactly one interval and never calls outputStack.
                row.Multiplier--;
                if(row.Multiplier==0)
                {row.Recipe=null;row.CraftingTimeLeft=0;row.IsCrafting=false;row.Quality=1;row.StartingEntityId=source.Viewer.entityId;row.OneItemCraftTime=0;}
                else{row.CraftingTimeLeft=retry?uiRow.OneItemCraftTime:uiRow.CraftingTimeLeft+uiRow.OneItemCraftTime;row.IsCrafting=uiRow.IsCrafting;}
            }
            var expected=RebirthProductiveEarnedNativeBody.Encode(old,elapsed,out int offset);
            if(offset!=proposedOffset||!expected.SequenceEqual(proposed))return false;
            physical=new XElement(delivery);return true;
        }
        catch{return false;}
    }
    private static bool NativePhysicalSame(ItemStack[] left,ItemStack[] right)=>
        RebirthCamperInventorySnapshot.Capture(left).SequenceEqual(RebirthCamperInventorySnapshot.Capture(right));
}
