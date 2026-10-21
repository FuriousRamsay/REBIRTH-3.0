using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed class CachedClose:IDisposable
    {internal CachedCustody Source;internal XUiC_WorkstationWindowGroup Window;internal byte[] After;internal int Offset;internal IDisposable Workspace;
     internal bool Prepared,SyncObserved;public void Dispose(){After=null;Source=null;Window=null;Workspace?.Dispose();Workspace=null;}}
    [ThreadStatic]private static CachedClose cachedClose;
    private static object BeginCachedCompletionClose(XUiC_WorkstationWindowGroup group,bool run)
    {
        if(!run||cachedClose!=null||!ThreadManager.IsMainThread())return null;CachedClose close=null;
        try
        {
            var matches=cachedCustodies.Values.Where(value=>value.Active&&!value.Physical&&value.Frame!=null&&
                ReferenceEquals(value.Frame.Observation.Window,group)&&value.Frame.Observation.FullBoundary&&!value.Frame.Observation.Continued&&
                value.Frame.ConfirmedOriginal==null&&value.SourceCurrent()).ToArray();
            if(matches.Length!=1||!matches[0].NativeCurrent())return null;
            var source=matches[0];var observed=source.Frame.Observation;
            if(!observed.Current()||!observed.RecipeMatches()||!RebirthRecipeOutputIntegration.IsCurrentOwningNativeVersion(observed.Card,observed.NativeVersionAfter)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var workspace))return null;
            close=new CachedClose{Source=source,Window=group,Workspace=workspace};
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                var cards=group.craftingQueue.GetRecipesToCraft();
                if(decoder==null||cards.Any(card=>card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty()))return null;
                var rows=cards.Select(card=>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=(short)card.GetRecipeCount(),CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),
                    IsCrafting=card.IsCrafting,Quality=(byte)card.OutputQuality,StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()}).ToArray();
                if(!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(source.Station,rows,group.inputWindow?.GetSlots(),group.outputWindow?.GetSlots(),
                    group.fuelWindow?.GetSlots(),group.toolWindow?.GetSlots(),out _,out close.After,out _,out close.Offset))return null;
            }
            var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                new XElement(source.Original.Element("before")),new XElement("cachedCloseAttempt",new XAttribute("original",HistoricalHash(source.Image)),
                    new XAttribute("nativeVersion",observed.NativeVersionAfter),new XAttribute("elapsedOffset",close.Offset),new XElement("nativeAfter",Convert.ToBase64String(close.After))));
            if(!RebirthWorkstationCompletionPublication.Write(source.Path+".close-attempt",attempt))return null;
            close.Prepared=true;cachedClose=close;var result=close;close=null;return result;
        }
        catch{return null;}
        finally{close?.Dispose();}
    }
    private static void ObserveCachedCompletionClose(XUiC_WorkstationWindowGroup group,bool run,object state)
    {
        if(state is not CachedClose close||!ReferenceEquals(cachedClose,close)||!run||!close.Prepared||!ReferenceEquals(group,close.Window))return;
        var source=close.Source;
        if(!source.SourceCurrent())return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,close.SyncObserved?close.After:source.Native,
                close.SyncObserved?close.Offset:source.Offset))return;
        }
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
            new XElement(source.Original.Element("before")),new XElement("actualCachedClose",new XAttribute("syncObserved",close.SyncObserved),
                new XAttribute("original",HistoricalHash(source.Image))));
        if(!RebirthWorkstationCompletionPublication.Write(source.Path+".closed",record))return;
        if(close.SyncObserved){source.Native=close.After;close.After=null;source.Offset=close.Offset;source.Saved=false;source.Watch?.Dispose();source.Watch=null;}
        source.Closed=true;source.TrySave();
    }
    private static void EndCachedCompletionClose(object state)
    {if(state is CachedClose close){if(ReferenceEquals(cachedClose,close))cachedClose=null;close.Dispose();}}
    internal static bool CanSyncCachedPrimary(XUiC_WorkstationWindowGroup group)
    {
        var station=group?.WorkstationData?.TileEntity;
        var known=cachedCustodies.Values.Where(value=>value.Active&&!value.Physical&&IsKnownCachedStation(value,station)).ToArray();
        return known.Length==0||cachedClose?.Prepared==true&&ReferenceEquals(cachedClose.Window,group)||
            known.Length==1&&known[0].Frame?.ConfirmedOriginal!=null&&!known[0].Frame.NativeAccepted;
    }
    internal static bool CanAdvanceCachedPrimary(XUiC_RecipeStack card)
    {
        var station=card?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity;
        var known=cachedCustodies.Values.Where(value=>value.Active&&!value.Physical&&IsKnownCachedStation(value,station)).ToArray();
        return known.Length==0||known.Length==1&&!known[0].Closed&&ReferenceEquals(known[0].Frame?.Observation.Card,card);
    }
    internal static bool CanRotateCachedPrimary(XUiC_CraftingQueue queue)=>!cachedCustodies.Values.Any(value=>value.Active&&!value.Physical&&
        IsKnownCachedStation(value,queue?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity));
    private static bool IsKnownCachedStation(CachedCustody source,TileEntityWorkstation station)
    {
        if(!source.Active||station==null)return false;
        if(ReferenceEquals(source.Station,station)&&ReferenceEquals(source.World,GameManager.Instance?.World)||ReferenceEquals(source.Load?.Loaded?.Tile,station))return true;
        try
        {
            var before=source.Original.Element("before").Elements().Single();var world=GameManager.Instance?.World;
            // Hold classification only: no action source is issued without the
            // actual complete native Load/factory/read lineage.
            return world!=null&&RebirthWorkstationCompletionOutbox.CurrentGuid(world)==source.WorldGuid&&source.MarkerMatches(station.GetChunk())&&
                station.ToWorldPos()==new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z"));
        }
        catch{return false;}
    }
    private static bool CanTickCachedPrimary(TileEntityWorkstation station)=>!cachedCustodies.Values.Any(value=>value.Active&&!value.Physical&&IsKnownCachedStation(value,station));
    internal static bool CanUseCachedNativeRewards(TileEntityWorkstation station,EntityPlayerLocal actor)
    {
        foreach(var source in cachedCustodies.Values.Where(value=>value.Active&&IsKnownCachedStation(value,station)))
            if(source.Original.Element("cachedOriginal")?.Element("nativeOwnerOriginal")!=null&&source.OwnerSavedImage==null&&
                !RebirthProductiveEarnedUiOwnerFile.IsExecutingCachedOwnerConsumption(station,actor,source.Frame?.NativeOriginal))return false;
        return true;
    }
}
