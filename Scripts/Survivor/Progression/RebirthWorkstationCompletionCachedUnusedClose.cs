using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class CachedUnusedClose:IDisposable
    {
        internal CachedPhysicalSource Source;
        internal readonly Guid Close=Guid.NewGuid();
        internal Guid Dispatch;
        internal long Session;
        internal bool Primary,SyncEntered,SyncRan,Enqueued,Observed,Ended;
        internal int Setups;
        internal byte[] Before,After;
        internal int BeforeOffset,AfterOffset;
        internal XElement Owner,Header,Report;
        internal string Path,Digest;
        internal IDisposable Memory,Lease;
        internal float Next;
        internal bool Current()=>!Ended&&Source.CurrentUnusedClose(Session,out var primary)&&primary==Primary;
        public void Dispose(){if(Ended)return;Ended=true;Before=After=null;Owner=Header=Report=null;Lease?.Dispose();Lease=null;Memory?.Dispose();Memory=null;}
    }
    [ThreadStatic]private static CachedUnusedClose closingCachedUnused;
    private static readonly Dictionary<Guid,CachedUnusedClose> cachedUnusedCloses=new Dictionary<Guid,CachedUnusedClose>();
    private static object BeginCachedUnusedClose(XUiC_WorkstationWindowGroup window,bool run)
    {
        if(!run||closingCachedUnused!=null||!ThreadManager.IsMainThread()||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeUiSession(window,out var session))return null;
        var values=cachedPhysicalSources.Values.Where(value=>ReferenceEquals(value.Window,window)&&value.Frame?.Observation.RanUpdate!=true).ToArray();
        if(values.Length!=1||cachedUnusedCloses.ContainsKey(values[0].Operation)||!values[0].CurrentUnusedClose(session,out var primary))return null;
        CachedUnusedClose close=null;
        try
        {
            var source=values[0];
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024+65536,out var memory))return null;
            close=new CachedUnusedClose{Source=source,Session=session,Primary=primary,Memory=memory,Lease=source.Grant.Retain(),Path=source.Path+".unused-close"};
            if(close.Lease==null||!source.NativeUnusedCloseCurrent())return null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                var cards=window.craftingQueue.GetRecipesToCraft();
                if(decoder==null||cards==null||!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(source.Station,cards.Select(CachedUnusedRow).ToArray(),
                    window.inputWindow.GetSlots(),window.outputWindow.GetSlots(),window.fuelWindow.GetSlots(),window.toolWindow.GetSlots(),
                    out close.Before,out close.After,out close.BeforeOffset,out close.AfterOffset))return null;
            }
            if(!RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(source.Viewer,new[]{source.Card.GetRecipe().GetName()},out int size)||size>8*1024*1024||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(close.Memory,checked(64L*1024*1024+16L*size+16L*(close.Before.Length+close.After.Length)+65536)))return null;
            close.Owner=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(source.Viewer,new[]{source.Card.GetRecipe().GetName()});
            close.Header=new XElement("cachedUnusedClose",new XAttribute("phase","prepare"),new XAttribute("ticket",source.Grant.Ticket.ToString("N")),
                new XAttribute("group",source.Grant.Group.ToString("N")),new XAttribute("action",source.Action.ToString("N")),
                new XAttribute("close",close.Close.ToString("N")),new XAttribute("original",source.Grant.Original),new XAttribute("session",session),
                new XAttribute("nativeVersion",source.NativeVersion),new XAttribute("primary",primary),
                new XElement("before",new XAttribute("offset",close.BeforeOffset),Convert.ToBase64String(close.Before)),
                new XElement("after",new XAttribute("offset",close.AfterOffset),Convert.ToBase64String(close.After)),new XElement(close.Owner));
            close.Digest=RebirthWorkstationCompletionOriginal.Hash(close.Header.ToString(SaveOptions.DisableFormatting));
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(source.World)),new XElement("before",new XElement(source.Before)),new XElement(close.Header));
            if(!RebirthWorkstationCompletionPublication.Write(close.Path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(close.Path))||!close.Current())return null;
            cachedUnusedCloses.Add(source.Operation,close);closingCachedUnused=close;var result=close;close=null;return result;
        }
        catch{return null;}
        finally{close?.Dispose();}
    }
    private static RecipeQueueItem CachedUnusedRow(XUiC_RecipeStack card)=>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=checked((short)card.GetRecipeCount()),
        CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),IsCrafting=card.IsCrafting,Quality=checked((byte)card.OutputQuality),StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()};
    internal static bool CanSyncCachedUnused(XUiC_WorkstationWindowGroup group)=>closingCachedUnused?.Current()==true&&
        ReferenceEquals(closingCachedUnused.Source.Window,group)&&!closingCachedUnused.SyncEntered;
    private static void ObserveCachedUnusedClose(XUiC_WorkstationWindowGroup window,bool run,CachedUnusedClose close)
    {
        if(!run||!ReferenceEquals(closingCachedUnused,close)||!ReferenceEquals(close.Source.Window,window)||!close.Current()||!close.SyncRan||!close.Enqueued||close.Setups!=1)return;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(close.Source.Station,close.After,close.AfterOffset))return;
            if(!XNode.DeepEquals(close.Owner,RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(close.Source.Viewer,new[]{close.Source.Card.GetRecipe().GetName()})))return;
            close.Report=new XElement("cachedUnusedClose",new XAttribute("phase","observed"),new XAttribute("close",close.Close.ToString("N")),
                new XAttribute("action",close.Source.Action.ToString("N")),new XAttribute("ticket",close.Source.Grant.Ticket.ToString("N")),
                new XAttribute("original",close.Source.Grant.Original),new XAttribute("session",close.Session),
                new XAttribute("dispatch",close.Dispatch.ToString("N")),new XAttribute("header",close.Digest));
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",close.Source.Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(close.Source.World)),new XElement("before",new XElement(close.Source.Before)),new XElement(close.Report));
            if(!RebirthWorkstationCompletionPublication.Write(close.Path+".observed",record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(close.Path+".observed")))return;
            close.Observed=true;Send(true,new XElement(close.Report),close.Source.Operation,close.Source.Viewer.entityId);
        }
        catch{}
    }
    private static void EndCachedUnusedClose(CachedUnusedClose close){if(ReferenceEquals(closingCachedUnused,close))closingCachedUnused=null;}
    private static void PumpCachedUnusedCloses(World world)
    {
        foreach(var close in cachedUnusedCloses.Values.ToArray())
        {
            if(!ReferenceEquals(close.Source.World,world)){cachedUnusedCloses.Remove(close.Source.Operation);close.Dispose();continue;}
            if(close.Observed&&UnityEngine.Time.realtimeSinceStartup>=close.Next)
            {close.Next=UnityEngine.Time.realtimeSinceStartup+1;Send(true,new XElement(close.Report),close.Source.Operation,close.Source.Viewer.entityId);}
        }
        foreach(var host in cachedUnusedHosts.Values.ToArray())if(!ReferenceEquals(host.Cache?.World,world)||host.SavedImage!=null&&host.Cache?.Active!=true)
        {cachedUnusedHosts.Remove(host.Operation);host.Dispose();}
    }
    private static void AcceptCachedUnusedClose(EntityPlayer viewer,Guid operation,XElement ack)
    {
        if(!cachedUnusedCloses.TryGetValue(operation,out var close)||!close.Observed||!ReferenceEquals(close.Source.Viewer,viewer)||
            (string)ack.Attribute("close")!=close.Close.ToString("N")||(string)ack.Attribute("header")!=close.Digest||
            (string)ack.Attribute("digest")!=close.Source.Grant.Original)return;
        var source=close.Source;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),
            new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(source.World)),new XElement("before",new XElement(source.Before)),new XElement("cachedUnusedCloseSaved",new XElement(ack)));
        if(!RebirthWorkstationCompletionPublication.Write(close.Path+".saved",record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(close.Path+".saved")))return;
        if(source.Frame is Frame frame)
        {
            if(cards.TryGetValue(frame.Observation.Card,out var attached)&&ReferenceEquals(attached,frame))cards.Remove(frame.Observation.Card);
            if(frames.TryGetValue(operation,out attached)&&ReferenceEquals(attached,frame))frames.Remove(operation);
            if(nativeViewerClients.TryGetValue(frame,out var saved)){nativeViewerClients.Remove(frame);saved.Dispose();}
            frame.Closed=true;frame.Observation.Dispose();
        }
        cachedPhysicalSources.Remove(operation);cachedUnusedCloses.Remove(operation);source.Dispose();close.Dispose();
    }
}
