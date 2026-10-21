using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed class LocalUnusedClose
    {internal Frame Frame;internal LocalNativeBefore Before;internal long Session;internal IDisposable Workspace;internal bool Finished;}
    [ThreadStatic]private static LocalUnusedClose localUnusedClose;
    internal static object BeginLocalCompletionClose(XUiC_WorkstationWindowGroup group,bool run)
    {
        if(!run||!ThreadManager.IsMainThread()||localUnusedClose!=null)return null;
        var remoteCached=BeginCachedUnusedClose(group,run);if(remoteCached!=null)return remoteCached;
        var cached=BeginCachedCompletionClose(group,run);if(cached!=null)return cached;
        var matches=frames.Values.Where(value=>!value.Closed&&value.LocalBefore!=null&&ReferenceEquals(value.Observation.Window,group)&&
            !value.Observation.World.IsRemote()&&!value.Observation.RanUpdate&&value.NativeOriginal==null&&value.ConfirmedOriginal==null&&!value.NativeAccepted).ToArray();
        if(matches.Length!=1)return null;var frame=matches[0];
        if(!frame.LocalBefore.Current()||!RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(frame.Observation.Card,frame.Observation.NativeVersionBefore))return null;
        long measure=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(ActiveBefore(frame.Intent))+65536;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*measure),out var workspace))return null;
        return localUnusedClose=new LocalUnusedClose{Frame=frame,Before=frame.LocalBefore,Session=frame.LocalBefore.Session,Workspace=workspace};
    }
    internal static void ObserveLocalCompletionClose(XUiC_WorkstationWindowGroup group,bool run,object state)
    {
        if(state is CachedUnusedClose remoteCached){ObserveCachedUnusedClose(group,run,remoteCached);return;}
        if(state is CachedClose){ObserveCachedCompletionClose(group,run,state);return;}
        if(state is not LocalUnusedClose close||!ReferenceEquals(localUnusedClose,close)||close.Finished||!run)return;
        var frame=close.Frame;var observed=frame.Observation;
        try
        {
            if(!ReferenceEquals(observed.Window,group)||!ReferenceEquals(frame.LocalBefore,close.Before)||frame.Closed||observed.RanUpdate||
                frame.NativeOriginal!=null||frame.ConfirmedOriginal!=null||frame.NativeAccepted||
                !RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(observed.Card,observed.NativeVersionBefore)||
                !ReferenceEquals(GameManager.Instance?.World,observed.World)||!ReferenceEquals(observed.World.GetPrimaryPlayer(),observed.Viewer)||
                observed.Viewer.IsDead()||observed.Viewer.Died!=observed.ViewerDeaths||
                RebirthCamperCharacterAccess.CurrentCreation(observed.Viewer)!=observed.ViewerCreation||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=observed.ViewerOwnerKey||
                !ReferenceEquals(observed.World.GetTileEntity(observed.Position),observed.Station)||!frame.EarlierOwner.SourceCurrent()||
                !close.Before.MatchesClosedNative()||!XNode.DeepEquals(frame.Intent,RebirthWorkstationCompletionPublication.ReadPrimary(frame.Path)))return;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",observed.Operation.ToString("N")),
                new XAttribute("world",observed.WorldGuid),new XElement("before",new XElement(ActiveBefore(frame.Intent))),
                new XElement("actualUnusedClose",new XAttribute("session",close.Session),new XAttribute("nativeVersion",observed.NativeVersionBefore),
                    new XAttribute("viewer",observed.ViewerOwnerKey),new XAttribute("creation",observed.ViewerCreation),new XAttribute("deaths",observed.ViewerDeaths)));
            string path=frame.Path+".unused-close";
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!close.Before.MatchesClosedNative())return;
            // Positive native close with no original Update invocation releases
            // observation custody only. It neither refunds nor retries an effect.
            close.Finished=true;frame.Closed=true;
            if(cards.TryGetValue(observed.Card,out var actual)&&ReferenceEquals(actual,frame))cards.Remove(observed.Card);
            if(frames.TryGetValue(observed.Operation,out actual)&&ReferenceEquals(actual,frame))frames.Remove(observed.Operation);
            frame.LocalBefore.Dispose();frame.LocalBefore=null;frame.NativeBefore=null;observed.Dispose();
        }
        catch{}
    }
    internal static void EndLocalCompletionClose(object state)
    {
        if(state is CachedUnusedClose remoteCached){EndCachedUnusedClose(remoteCached);return;}
        if(state is CachedClose){EndCachedCompletionClose(state);return;}
        if(state is not LocalUnusedClose close)return;
        if(ReferenceEquals(localUnusedClose,close))localUnusedClose=null;
        close.Frame=null;close.Before=null;close.Workspace?.Dispose();close.Workspace=null;
    }
}
