using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static bool IsKnownLocalCachedUiCard(RebirthProductiveEarnedUiCoordinator.Original original,XUiC_RecipeStack card)
    {
        if(original==null||card==null||original.Binding.World.IsRemote()||!original.Current())return false;
        var matches=frames.Values.Where(value=>!value.Closed&&value.Saved&&value.Attempted&&
            ReferenceEquals(value.Observation.Card,card)&&value.Observation.Operation==original.Binding.Operation&&
            ReferenceEquals(value.Observation.World,original.Binding.World)&&ReferenceEquals(value.Observation.Station,original.Binding.Station)&&
            value.Observation.FullBoundary&&!value.Observation.Continued&&!value.Observation.Accepted&&!value.NativeAccepted&&
            value.ConfirmedOriginal==null&&value.SavedFullBoundaryVersion==value.Observation.NativeVersionAfter&&
            value.Observation.Current()&&value.Observation.RecipeMatches()).ToArray();
        return matches.Length==1;
    }
    private static void SaveActualCachedFullBoundary(Frame frame)
    {
        var observed=frame.Observation;
        if(!observed.FullBoundary||!observed.RanUpdate||observed.FullBoundaryEvidence==null||observed.NativeVersionAfter<1||
            frame.SavedFullBoundaryVersion==observed.NativeVersionAfter||!observed.Current()||!observed.RecipeMatches())return;
        try
        {
            long bytes=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(observed.FullBoundaryEvidence)+
                RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(ActiveBefore(frame.Intent))+65536;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*bytes),out var workspace))return;
            using(workspace)
            {
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",observed.Operation.ToString("N")),
                    new XAttribute("world",observed.WorldGuid),new XElement("before",new XElement(ActiveBefore(frame.Intent))),
                    new XElement("actualCachedFull",new XAttribute("nativeVersion",observed.NativeVersionAfter),
                        new XElement(observed.FullBoundaryEvidence)));
                if(RebirthWorkstationCompletionPublication.Write(frame.Path+".full."+observed.NativeVersionAfter+".xml",record))
                {frame.SavedFullBoundaryVersion=observed.NativeVersionAfter;ObserveCachedCustody(frame);}
            }
        }
        catch{/* Original actual cached value remains retained; no reconstruction or re-award permission. */}
    }
    internal static bool TryQualifyLocalCachedSync(NativeAddOriginal source,XUiC_WorkstationWindowGroup group,byte[] previous,int previousOffset,
        out byte[] actual,out byte[] planned,out int actualOffset,out int plannedOffset)
    {
        actual=planned=null;actualOffset=plannedOffset=-1;
        var frame=frames.Values.SingleOrDefault(value=>ReferenceEquals(value.NativeOriginal,source)&&value.ConfirmedOriginal!=null&&!value.NativeAccepted&&
            value.Saved&&ReferenceEquals(value.Observation.Window,group)&&value.Observation.Retry&&
            ActiveBefore(value.Intent)?.Element("cachedPredecessor")!=null);
        if(frame==null||!source.SourceCurrentWithoutRow()||frame.ConfirmedOriginal.Operation!=source.Operation.ToString("N"))return false;
        var delivery=frame.ConfirmedOriginal.Write().Element("delivery").Elements().Single();
        if(delivery.Element("equipmentAfter")!=null&&Convert.ToBase64String(RebirthRecipeOutputIntegration.OwningEquipmentNative(frame.Observation.Viewer))!=(string)delivery.Element("equipmentAfter"))return false;
        var cards=group.craftingQueue?.GetRecipesToCraft();
        if(cards==null||cards.Any(card=>card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty())||
            Grid(source.Station.Output)!=(string)delivery.Element("outputAfter")||
            Grid(frame.Observation.Viewer.bag.ItemGrid.items)!=(string)delivery.Element("bagAfter")||
            Grid(frame.Observation.Viewer.inventory.ItemGrid.items)!=(string)delivery.Element("beltAfter"))return false;
        var rows=cards.Select(card=>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=(short)card.GetRecipeCount(),
            CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),IsCrafting=card.IsCrafting,Quality=(byte)card.OutputQuality,
            StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()}).ToArray();
        if(!QueueDecrement((string)ActiveBefore(frame.Intent).Element("nativeQueueFull"),RebirthRemoteResourcePlacedStationQueue.Encode(rows),
            (string)ActiveBefore(frame.Intent).Element("recipeFull")))return false;
        // Caller owns the shared decoder. Current body may differ from the
        // saved owner-removal image only in physical output: the positive
        // cached successor independently froze its actual changed preimage.
        if(!RebirthProductiveEarnedNativeBody.TryRead(previous,previousOffset,out var old,out ulong elapsed)||
            !RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(source.Station,rows,group.inputWindow?.GetSlots(),group.outputWindow?.GetSlots(),
                group.fuelWindow?.GetSlots(),group.toolWindow?.GetSlots(),out actual,out planned,out actualOffset,out plannedOffset)||
            !RebirthProductiveEarnedNativeBody.TryRead(actual,actualOffset,out var current,out _))return false;
        int index=frame.Observation.Index;
        if(index<0||index>=old.Queue.Length||rows.Length!=old.Queue.Length||old.Queue[index].Multiplier!=frame.Observation.BeforeCount||
            rows[index].Multiplier!=frame.Observation.BeforeCount-1||
            (rows[index].Multiplier>0?RecipeBinding(rows[index])!=RecipeBinding(old.Queue[index]):rows[index].Recipe!=null))return false;
        for(int i=0;i<rows.Length;i++)if(i!=index&&
            !RebirthProductiveEarnedRowProof.CaptureRow(rows[i]).SequenceEqual(RebirthProductiveEarnedRowProof.CaptureRow(old.Queue[i])))return false;
        old.Output=current.Output;
        var expectedBefore=RebirthProductiveEarnedNativeBody.Encode(old,elapsed,out int offset);
        if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(actual,actualOffset,expectedBefore,offset))return false;
        current.Queue=rows;
        var expectedAfter=RebirthProductiveEarnedNativeBody.Encode(current,elapsed,out offset);
        return RebirthProductiveEarnedNativeBody.SameExceptElapsed(planned,plannedOffset,expectedAfter,offset);
    }
}
