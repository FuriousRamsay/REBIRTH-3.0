using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed class CachedSpace:IDisposable
    {
        internal CachedCustody Source;internal XUiC_WorkstationOutputGrid Grid;internal Guid Event;
        internal byte[] Before,After;internal int BeforeOffset,AfterOffset;internal long Version;internal IDisposable Workspace;
        public void Dispose(){Source=null;Grid=null;Before=After=null;Workspace?.Dispose();Workspace=null;}
    }
    private static object BeginCachedPrimarySpace(XUiC_WorkstationOutputGrid grid,ItemStack[] argument,bool run)
    {
        if(!run||current!=null||grid?.windowGroup?.Controller is not XUiC_WorkstationWindowGroup group||argument==null)return null;
        CachedSpace scope=null;
        try
        {
            var matches=cachedCustodies.Values.Where(value=>value.Active&&!value.Physical&&!value.Closed&&value.Frame?.Observation.FullBoundary==true&&
                !value.Frame.Observation.Continued&&ReferenceEquals(value.Frame.Observation.Window,group)&&value.SourceCurrent()).ToArray();
            if(matches.Length!=1||!matches[0].NativeCurrent())return null;
            var source=matches[0];var observed=source.Frame.Observation;
            if(!RebirthRecipeOutputIntegration.IsCurrentOwningNativeVersion(observed.Card,observed.NativeVersionAfter)||!observed.Current()||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var workspace))return null;
            scope=new CachedSpace{Source=source,Grid=grid,Event=Guid.NewGuid(),Version=observed.NativeVersionAfter,Workspace=workspace};
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(source.Station,out scope.Before,out scope.BeforeOffset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(scope.Before,scope.BeforeOffset,out var body,out ulong elapsed)||
                    !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(argument,262144,349528,out _))return null;
                body.Output=ItemStack.Clone(argument);scope.After=RebirthProductiveEarnedNativeBody.Encode(body,elapsed,out scope.AfterOffset);
            }
            var attempt=CachedSpaceRecord(scope,"cachedSpaceAttempt");
            long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(attempt);
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scope.Workspace,checked(64L*1024*1024+8*measured))||
                !RebirthWorkstationCompletionPublication.Write(source.Path+".space."+scope.Event.ToString("N")+".attempt",attempt))return null;
            var result=scope;scope=null;return result;
        }
        catch{return null;}
        finally{scope?.Dispose();}
    }
    private static XElement CachedSpaceRecord(CachedSpace scope,string stage)=>new XElement("workstationCompletionIntent",
        new XAttribute("operation",scope.Source.Operation.ToString("N")),new XAttribute("world",scope.Source.WorldGuid),new XElement(scope.Source.Original.Element("before")),
        new XElement(stage,new XAttribute("event",scope.Event.ToString("N")),new XAttribute("nativeVersion",scope.Version),
            new XAttribute("beforeOffset",scope.BeforeOffset),new XAttribute("afterOffset",scope.AfterOffset),
            new XElement("nativeBefore",Convert.ToBase64String(scope.Before)),new XElement("nativeAfter",Convert.ToBase64String(scope.After))));
    private static void ObserveCachedPrimarySpace(XUiC_WorkstationOutputGrid grid,bool run,object state)
    {
        if(state is not CachedSpace scope||!run||!ReferenceEquals(scope.Grid,grid)||!scope.Source.SourceCurrent()||
            !RebirthRecipeOutputIntegration.IsCurrentOwningNativeVersion(scope.Source.Frame.Observation.Card,scope.Version))return;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(scope.Source.Station,scope.After,scope.AfterOffset))return;
            var observed=CachedSpaceRecord(scope,"actualCachedSpace");long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(observed);
            if(!scope.Source.Grow(checked(64L*1024*1024+8*(measured+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(scope.Source.Original)))))return;
            string path=scope.Source.Path+".space."+scope.Event.ToString("N")+".observed";
            if(!RebirthWorkstationCompletionPublication.Write(path,observed)||!XNode.DeepEquals(observed,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
            scope.Source.SpacePath=path;scope.Source.SpaceImage=RebirthWorkstationCompletionPublication.ReadEvidence(path);
            scope.Source.Native=scope.After;scope.After=null;scope.Source.Offset=scope.AfterOffset;scope.Source.Saved=false;
            scope.Source.Watch?.Dispose();scope.Source.Watch=null;
        }
        catch{}
    }
}
