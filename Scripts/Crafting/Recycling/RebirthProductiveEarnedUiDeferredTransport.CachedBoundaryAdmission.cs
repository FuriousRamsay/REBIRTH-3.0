using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed class CachedBoundaryAdmission:IDisposable
    {
        internal readonly RebirthWorkstationCompletionPipeline.NativeHostOriginal Source;
        internal XElement Full,Before,Ledger;
        internal byte[] Body;internal readonly int Offset;
        internal readonly NativeViewerPds.SavedCachedBoundaryOwner Owner;
        private IDisposable allocation;
        private CachedBoundaryAdmission(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement full,XElement before,XElement ledger,
            byte[] body,int offset,NativeViewerPds.SavedCachedBoundaryOwner owner,IDisposable memory)
        {Source=source;Full=new XElement(full);Before=new XElement(before);Ledger=ledger==null?null:new XElement(ledger);Body=(byte[])body.Clone();Offset=offset;Owner=owner;allocation=memory;}
        internal bool Current()=>allocation!=null&&Source.CurrentViewer()&&Source.AdmissionCurrent()&&(Owner==null||Owner.Current());
        public void Dispose(){Full=Before=Ledger=null;Body=null;Owner?.Dispose();allocation?.Dispose();allocation=null;}
        internal static bool TryCreate(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,byte[] body,int offset,out CachedBoundaryAdmission token)
        {
            token=null;IDisposable allocation=null;NativeViewerPds.SavedCachedBoundaryOwner owner=null;
            try
            {
                var full=contribution.Element("fullBoundary")?.Elements().SingleOrDefault();var before=contribution.Element("activeBefore")?.Elements().SingleOrDefault();
                if(source?.CurrentViewer()!=true||!source.AdmissionCurrent()||!source.BeforeSaved||(int?)contribution.Attribute("phase")!=5||
                    full?.Name!="nativeOwningFullBoundary"||(string)full.Attribute("operation")!=source.Operation.ToString("N")||
                    (long?)full.Attribute("nativeVersion") is not long version||version<1||!source.IsSavedActiveBefore(before)||!source.IsSavedFullBoundaryBefore(full)||
                    (string)full.Element("recipeFull")!=(string)before.Element("recipeFull")||
                    (bool?)full.Attribute("freshCallback")!=true&&(bool?)full.Attribute("cachedCallback")!=true)return false;
                long measured=MeasurePublicationElement(contribution)+body.Length;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*measured+65536),out allocation))return false;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    var native=Convert.FromBase64String((string)full.Element("nativeAfter"));int fullOffset=(int)full.Attribute("nativeElapsedOffset");
                    if(decoder==null||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,fullOffset,body,offset)||
                        !RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,body,offset))return false;
                }
                XElement ledger=null;
                if(contribution.Name=="sameOwnerEffect")
                {if(!NativeViewerPds.TryValidateCachedBoundaryOwner(source,contribution,out owner))return false;}
                else
                {
                    var membership=new XElement(contribution);membership.Elements("fullBoundary").Remove();
                    if(contribution.Name!="deferredNativeContribution"||!host.TryGetValue(source.Operation,out var actual)||
                        !ReferenceEquals(actual.NativeOriginal,source)||actual.NativePhase!=1||!actual.SourceCurrentWithoutViewer()||
                        !XNode.DeepEquals(actual.NativeMembership,NativeMembership(membership))||
                        !RebirthProductiveEarnedNativeBody.SameExceptElapsed(actual.NativeAcceptedBody,actual.NativeAcceptedOffset,body,offset)||
                        !RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(contribution,"nativeAddArgument"),out var argument)||
                        Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new[]{argument.CraftedItemStack}))!=(string)full.Element("primaryNative"))return false;
                    ledger=contribution;
                }
                token=new CachedBoundaryAdmission(source,full,before,ledger,body,offset,owner,allocation);owner=null;allocation=null;return true;
            }
            catch{return false;}
            finally{owner?.Dispose();allocation?.Dispose();}
        }
    }
    internal static void ObserveCachedBoundaryReceipt(RebirthWorkstationCompletionPipeline.RemoteNativeEvent actual)
    {
        if(actual?.CachedBoundary==null||!actual.IsActualAcceptedRead()||actual.Contribution.Name!="deferredNativeContribution"||
            !host.TryGetValue(actual.Source.Operation,out var entry)||!ReferenceEquals(entry.NativeOriginal,actual.Source)||entry.NativePhase!=1||
            !RebirthProductiveEarnedNativeBody.SameExceptElapsed(entry.NativeAcceptedBody,entry.NativeAcceptedOffset,actual.Body,actual.Offset))return;
        // The nochange native read reconstructs native receipt handles. Renew
        // only this exact admitted aggregate; never infer a new contribution.
        var rows=actual.Source.Station.CraftCompleteList?.Where(row=>Row(row).SequenceEqual(entry.Aggregate)).ToArray();
        if(rows?.Length!=1)return;entry.AcceptedReceipt=rows[0];entry.NativeDispatch=actual.Dispatch;
    }
}
