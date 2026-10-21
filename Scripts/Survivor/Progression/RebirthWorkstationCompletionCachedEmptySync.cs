using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed class CachedEmptySync:IDisposable
    {
        internal CachedCustody[] Sources;internal XUiC_WorkstationWindowGroup Window;internal long Session;
        internal XElement[] Attempts;internal IDisposable[] Retentions;
        public void Dispose(){if(Retentions!=null)foreach(var lease in Retentions)lease?.Dispose();Retentions=null;Attempts=null;Sources=null;Window=null;}
    }
    private static object CaptureSavedCachedEmptySync(XUiC_WorkstationWindowGroup window)
    {
        CachedEmptySync captured=null;
        try
        {
            var station=window?.WorkstationData?.TileEntity;
            var sources=cachedCustodies.Values.Where(value=>value.Physical&&value.Saved&&ReferenceEquals(value.Station,station)&&
                value.Original.Element("cachedOriginal")?.Element("cachedOwnerEffect")!=null).ToArray();
            if(sources.Length==0||!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out var session))return null;
            var cards=window.craftingQueue.GetRecipesToCraft();
            if(cards==null||cards.Any(card=>card.GetRecipe()!=null||card.GetRecipeCount()!=0||card.isInventoryFull||card.AmountToRepair!=0||
                card.OriginalItem!=null&&!card.OriginalItem.IsEmpty())||station.Queue==null||station.Queue.Length!=cards.Length||
                station.Queue.Any(row=>row==null||row.Recipe!=null||row.Multiplier!=0||row.AmountToRepair!=0||row.RepairItem!=null&&!row.RepairItem.IsEmpty()))return null;
            captured=new CachedEmptySync{Sources=sources,Window=window,Session=session,Attempts=new XElement[sources.Length],Retentions=new IDisposable[sources.Length]};
            for(int i=0;i<sources.Length;i++)
            {
                var value=sources[i];var frame=value.Frame;
                long physicalLength=RebirthWorkstationCompletionPublication.PublishedLength(value.Path+".physical");
                if(value.World.IsRemote()||frame==null||value.WindowSession!=session||!ReferenceEquals(frame.Observation.Window,window)||
                    !ReferenceEquals(value.World.GetPrimaryPlayer(),window.xui?.playerUI?.entityPlayer)||
                    !MatchesSavedCachedOwnerEffect(value.Original.Element("cachedOriginal").Element("cachedOwnerEffect").Elements().SingleOrDefault())||
                    !value.NativeCurrent()||physicalLength<1||!value.ReserveRetirementWorkspace(checked(64*1024L+32L*(value.Native.Length+physicalLength))))return null;
                captured.Retentions[i]=value.RetainResource();if(captured.Retentions[i]==null)return null;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    var rows=cards.Select(card=>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=(short)card.GetRecipeCount(),CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),
                        IsCrafting=card.IsCrafting,Quality=(byte)card.OutputQuality,StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()}).ToArray();
                    if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(station,rows,
                        window.inputWindow?.GetSlots(),window.outputWindow?.GetSlots(),window.fuelWindow?.GetSlots(),window.toolWindow?.GetSlots(),
                        out var before,out var after,out int beforeOffset,out int afterOffset)||
                        !RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.Native,value.Offset,before,beforeOffset)||
                        !RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,after,afterOffset))return null;
                }
                var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("world",value.WorldGuid),
                    new XElement(value.Original.Element("before")),new XElement("actualCachedEmptySyncAttempt",new XAttribute("original",HistoricalHash(value.Image)),
                        new XAttribute("session",session),new XAttribute("nativeVersion",frame.Observation.NativeVersionAfter),
                        new XAttribute("elapsedOffset",value.Offset),new XElement("native",Convert.ToBase64String(value.Native))));
                if(!RebirthWorkstationCompletionPublication.Write(value.Path+".empty-sync-attempt",attempt)||!value.NativeCurrent())return null;
                captured.Attempts[i]=attempt;
            }
            var result=captured;captured=null;return result;
        }
        catch{return null;}
        finally{captured?.Dispose();}
    }
    private static void ObserveSavedCachedEmptySync(XUiC_WorkstationWindowGroup window,bool run,object state)
    {
        if(state is not CachedEmptySync captured||!run||!ReferenceEquals(window,captured.Window)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out var session)||session!=captured.Session)return;
        foreach(var value in captured.Sources)if(!value.NativeCurrent())return;
        for(int i=0;i<captured.Sources.Length;i++)
        {
            var value=captured.Sources[i];
            if(!TryPublishCachedSettledNotice(value))return;
            var accepted=new XElement("workstationCompletionIntent",new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("world",value.WorldGuid),
                new XElement(value.Original.Element("before")),new XElement("actualCachedEmptySyncAccepted",new XElement(captured.Attempts[i])));
            string acceptedPath=value.Path+".empty-sync-accepted";
            if(!RebirthWorkstationCompletionPublication.Write(acceptedPath,accepted))return;
            var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(acceptedPath);if(bytes==null)return;
            var physical=RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".physical");if(physical==null)return;
            var retired=new XElement("workstationCompletionIntent",new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("world",value.WorldGuid),
                new XElement(value.Original.Element("before")),new XElement("cachedEmptyRetired",new XAttribute("original",HistoricalHash(value.Image)),
                    new XAttribute("group",value.Operation.ToString("N")),new XAttribute("handoffPath",acceptedPath),
                    new XElement("handoff",Convert.ToBase64String(bytes)),new XElement("physical",physical)));
            if(!RebirthWorkstationCompletionPublication.Write(value.Path+".empty-retired",retired)||
                !XNode.DeepEquals(retired,RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".empty-retired"))||!value.NativeCurrent())return;
        }
        foreach(var value in captured.Sources)
        {cachedCustodies.Remove(value.Operation);var owner=value.Frame?.EarlierOwner;value.Frame?.CachedReentry?.Dispose();if(owner?.CachedSource!=null)owner.Dispose();value.Dispose();}
    }
    private static void EndSavedCachedEmptySync(object state){if(state is CachedEmptySync captured)captured.Dispose();}
}
