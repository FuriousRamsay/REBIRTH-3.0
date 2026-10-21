using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class CachedPhysicalSpace:IDisposable
    {
        internal CachedPhysicalSource Source;
        internal XUiC_WorkstationOutputGrid Grid;
        internal byte[] Before,After;
        internal int BeforeOffset,AfterOffset;
        internal int Sequence;
        internal string Bag,Belt;
        internal XElement Evidence;
        internal string SavedPath;
        internal byte[] SavedImage;
        internal IDisposable Allocation,SourceLease;
        internal bool Observed,Ended,Enqueued;
        private int references=1;
        internal bool Current()=>!Ended&&Source?.Current()==true&&Source.Frame?.Attempted!=true&&
            ReferenceEquals(Grid.windowGroup?.Controller,Source.Window)&&Source.Grant.CurrentWindow()&&
            RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(Source.Card,Source.NativeVersion);
        internal IDisposable Retain(){if(Ended)return null;references++;return new Lease(this);}
        private sealed class Lease:IDisposable{private CachedPhysicalSpace source;internal Lease(CachedPhysicalSpace value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
        private void Release(){if(--references!=0)return;Ended=true;Before=After=SavedImage=null;Evidence=null;Source=null;Grid=null;Bag=Belt=null;SourceLease?.Dispose();SourceLease=null;Allocation?.Dispose();Allocation=null;}
        public void Dispose()=>Release();
    }
    [ThreadStatic]private static CachedPhysicalSpace cachedPhysicalSpace;
    private static object BeginCachedPhysicalSpace(XUiC_WorkstationOutputGrid grid,ItemStack[] argument,bool run)
    {
        if(!run||current!=null||cachedPhysicalSpace!=null||grid?.windowGroup?.Controller is not XUiC_WorkstationWindowGroup window||argument==null)return null;
        var values=cachedPhysicalSources.Values.Where(value=>ReferenceEquals(value.Window,window)&&value.Frame?.Attempted!=true&&
            (value.Space==null||value.SpaceSaved&&value.Space.Enqueued&&value.Space.Sequence<64)&&value.Current()&&value.NativeCurrent()).ToArray();
        if(values.Length!=1)return null;CachedPhysicalSpace scope=null;
        try
        {
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024+65536,out var memory))return null;
            scope=new CachedPhysicalSpace{Source=values[0],Grid=grid,Sequence=(values[0].Space?.Sequence??0)+1,Allocation=memory,SourceLease=values[0].Retain()};if(scope.SourceLease==null)return null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(argument,262144,349528,out _)||
                    !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(scope.Source.Station,out scope.Before,out scope.BeforeOffset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(scope.Before,scope.BeforeOffset,out var body,out ulong elapsed)||
                    !TryCaptureCachedTransferGrid(scope.Source.Viewer.bag.ItemGrid.items,out scope.Bag)||
                    !TryCaptureCachedTransferGrid(scope.Source.Viewer.inventory.ItemGrid.items,out scope.Belt))return null;
                body.Output=ItemStack.Clone(argument);scope.After=RebirthProductiveEarnedNativeBody.Encode(body,elapsed,out scope.AfterOffset);
            }
            var prior=scope.Source.Space;cachedPhysicalSpace=scope;scope.Source.Space=scope;scope.Source.SpaceSaved=false;
            prior?.Dispose();var result=scope;scope=null;return result;
        }
        catch{return null;}
        finally{scope?.Dispose();}
    }
    private static void ObserveCachedPhysicalSpace(XUiC_WorkstationOutputGrid grid,bool run,CachedPhysicalSpace scope)
    {
        if(!run||!ReferenceEquals(cachedPhysicalSpace,scope)||!ReferenceEquals(scope.Grid,grid)||!scope.Current())return;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(scope.Source.Station,scope.After,scope.AfterOffset))return;
            if(!TryCaptureCachedTransferGrid(scope.Source.Viewer.bag.ItemGrid.items,out var bag)||
                !TryCaptureCachedTransferGrid(scope.Source.Viewer.inventory.ItemGrid.items,out var belt))return;
            var source=scope.Source;
            scope.Evidence=new XElement("actualCachedPhysicalSpace",new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("ticket",source.Grant.Ticket.ToString("N")),new XAttribute("action",source.Action.ToString("N")),
                new XAttribute("session",source.WindowSession),new XAttribute("nativeVersion",source.NativeVersion),
                new XAttribute("sequence",scope.Sequence),
                new XElement("cachedFull",new XElement(source.Full)),new XElement("before",new XAttribute("offset",scope.BeforeOffset),Convert.ToBase64String(scope.Before)),
                new XElement("after",new XAttribute("offset",scope.AfterOffset),Convert.ToBase64String(scope.After)),
                new XElement("bagBefore",scope.Bag),new XElement("beltBefore",scope.Belt),new XElement("bagAfter",bag),new XElement("beltAfter",belt));
            long size=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(scope.Evidence);
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scope.Allocation,checked(64L*1024*1024+16*size+65536)))return;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(source.World)),new XElement("before",new XElement(source.Before)),new XElement(scope.Evidence));
            string path=source.Path+".space."+scope.Sequence;
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
            scope.Observed=true;ResumeCachedPhysicalSpace(source);
        }
        catch{/* The actual user transfer is retained; it cannot authorize a primary insertion. */}
    }
    private static void EndCachedPhysicalSpace(CachedPhysicalSpace scope)
    {
        if(ReferenceEquals(cachedPhysicalSpace,scope))cachedPhysicalSpace=null;
        // The source owns the exact observed transfer until its saved ACK.
        // An uncertain callback is retained as unknown, never re-created.
    }
    private static void ResumeCachedPhysicalSpace(CachedPhysicalSource source)
    {
        if(!source.Prepared||source.Space?.Observed!=true||!source.Space.Current())return;
        foreach(var pending in pendingNativeUploads.ToArray())
            if(ReferenceEquals(pending.Setup?.CachedSpace,source.Space))ResumePendingNative(pending);
    }
    private static bool ValidateCachedPhysicalSpace(CachedPhysicalHostAction action,XElement evidence,byte[] proposed,int proposedOffset)
    {
        try
        {
            var cache=action.Issue.Cache;
            if(evidence?.Name!="actualCachedPhysicalSpace"||action.PhysicalAccepted||cache.PhysicalObserved||cache.Epoch>=64||
                (string)evidence.Attribute("operation")!=action.Operation.ToString("N")||(string)evidence.Attribute("ticket")!=action.Issue.Ticket.ToString("N")||
                (string)evidence.Attribute("action")!=action.Action.ToString("N")||(long?)evidence.Attribute("session")!=action.Session||
                (long?)evidence.Attribute("nativeVersion") is not long version||version<0||
                (int?)evidence.Attribute("sequence")!=action.SpaceSequence+1||
                !XNode.DeepEquals(evidence.Element("cachedFull")?.Elements().SingleOrDefault(),cache.CurrentFull))return false;
            var before=ReadCachedSpaceBody(evidence,"before");var after=ReadCachedSpaceBody(evidence,"after");
            int beforeOffset=(int)evidence.Element("before").Attribute("offset"),afterOffset=(int)evidence.Element("after").Attribute("offset");
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,cache.Native,cache.Offset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(after,afterOffset,proposed,proposedOffset)||
                !RebirthProductiveEarnedNativeBody.TryRead(proposed,proposedOffset,out _,out ulong actualElapsed)||actualElapsed>GameTimer.Instance.ticks||
                !RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var first,out ulong elapsed)||
                !RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var last,out _))return false;
            first.Output=last.Output;var expected=RebirthProductiveEarnedNativeBody.Encode(first,elapsed,out int offset);
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(expected,offset,after,afterOffset))return false;
            foreach(string name in new[]{"bagBefore","beltBefore","bagAfter","beltAfter"})RebirthProductiveEarnedUiRemote.ReadGrid(evidence,name);
            return true;
        }
        catch{return false;}
    }
    private static byte[] ReadCachedSpaceBody(XElement evidence,string name)
    {
        string encoded=(string)evidence.Element(name);int maximum=RebirthProductiveEarnedNativeBody.MaximumBodyBytes;
        if(encoded==null||encoded.Length<4||encoded.Length%4!=0||encoded.Length>4*((maximum+2)/3))throw new System.IO.InvalidDataException();
        int padding=encoded.EndsWith("==",StringComparison.Ordinal)?2:encoded.EndsWith("=",StringComparison.Ordinal)?1:0;
        long length=3L*(encoded.Length/4)-padding;if(length<8||length>maximum)throw new System.IO.InvalidDataException();
        var bytes=Convert.FromBase64String(encoded);if(bytes.Length!=length||Convert.ToBase64String(bytes)!=encoded)throw new System.IO.InvalidDataException();return bytes;
    }
}
