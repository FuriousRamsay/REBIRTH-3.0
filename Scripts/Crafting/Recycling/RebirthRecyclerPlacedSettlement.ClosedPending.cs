using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed class ClosedPendingPublication:IDisposable
    {
        internal readonly ClosedAuthorityCompletion Source;internal readonly RebirthRemoteResourceNativeBinding Binding;
        internal readonly Chunk Chunk;internal readonly string Key;internal readonly ChunkCustomData Marker;internal readonly byte[] MarkerImage;
        private RebirthRemoteResourceNativePublication.Watch watch;private IDisposable memory;private LoadedStation loaded;
        private bool closed,promoted;private byte[] receipt;private string path;
        private ClosedPendingPublication(ClosedAuthorityCompletion source,RebirthRemoteResourceNativeBinding binding,Chunk chunk,string key,ChunkCustomData marker)
        {Source=source;Binding=binding;Chunk=chunk;Key=key;Marker=marker;MarkerImage=(byte[])marker.data.Clone();}
        internal bool Current()=>!closed&&!promoted&&Source.Current()&&Source.Attached?.AcceptedStation!=true&&Binding.Current()&&
            ReferenceEquals(Source.Station.GetChunk(),Chunk)&&Chunk.ChunkCustomData.dict.TryGetValue(Key,out var marker)&&ReferenceEquals(marker,Marker)&&
            marker.TriggerWriteDataDelegate==null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.data.SequenceEqual(MarkerImage)&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(Source.Station,Source.BeforeNative,Source.BeforeOffset);
        internal bool CanReturnUnenteredAttachment(RemoteHostOriginal source)=>!promoted&&ReferenceEquals(Source.Attached,source)&&
            !source.AcceptedStation&&!source.AdmissionEntered&&!source.AdmissionUnknown&&source.Publication==null&&source.OwnerCheckpoint==null&&Current();
        internal bool HistoricalSaved()=>!closed&&!promoted&&Source.NativeOriginCurrent()&&receipt!=null&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,receipt);
        internal bool IsLoaded(LoadedStation value)=>ReferenceEquals(loaded,value)&&value?.Current()==true&&Current();
        internal void PreserveUnadoptedLoad(LoadedStation value){if(ReferenceEquals(loaded,value)&&!value.Adopted)loaded=null;}
        internal static ClosedPendingPublication Begin(ClosedAuthorityCompletion source)
        {
            if(!source.Current()||source.Attached!=null)return null;
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(source.World,source.Station,out _))
                using(var birth=RebirthLogisticsEndpointIdentity.Begin(source.World,source.Station,source.Current))if(birth==null)return null;
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(source.World,source.Station,out string incarnation)||
                !RebirthRemoteResourceNativeBinding.TryResolve(source.World,RemoteResourceIdentity.Workstation(source.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,incarnation,out var binding))return null;
            var chunk=source.Station.GetChunk();if(chunk==null||chunk.InProgressUnloading)return null;
            string key="RebirthRecyclerPlacedV1:"+source.Station.localChunkPos.x+":"+source.Station.localChunkPos.y+":"+source.Station.localChunkPos.z+":"+source.Operation.ToString("N");
            if(chunk.ChunkCustomData.dict.ContainsKey(key))return null;
            var header=new XElement("recyclerClosedAuthorityPending",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("ticket",source.Ticket.ToString("N")),
                new XAttribute("owner",source.Owner),new XAttribute("creation",source.Creation),new XAttribute("deaths",source.Deaths),
                new XAttribute("original",RebirthWorkstationCompletionOriginal.Hash(Encoding.UTF8.GetString(source.Image))));
            var marker=new ChunkCustomData(key,ulong.MaxValue,false){data=Encoding.UTF8.GetBytes(header.ToString(SaveOptions.DisableFormatting))};
            // Source retains this exact privately issued marker even if the
            // native snapshot worker is busy. It is never equality-adopted.
            var result=new ClosedPendingPublication(source,binding,chunk,key,marker);
            source.Pending=result;
            chunk.ChunkCustomData.Set(key,marker);chunk.isModified=true;
            result.watch=RebirthRemoteResourceNativePublication.Begin(binding,result.Current,key);return result;
        }
        internal bool Saved()
        {
            if(!Current())return false;
            if(receipt!=null)return RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,receipt)&&(loaded==null||loaded.Current());
            if(watch==null||!watch.Current()){watch?.Dispose();watch=RebirthRemoteResourceNativePublication.Begin(Binding,Current,Key);}
            if(watch==null||!watch.TryReadPublishedTileDescriptor(out var native,out long offset,out int length,out string digest))return false;
            var marker=watch.Markers?.SingleOrDefault(value=>value.Key==Key);
            if(watch.TileSpan?.Length!=length||marker?.Span==null||!marker.Bytes.SequenceEqual(MarkerImage)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(24L*(length+marker.Span.Length)+16L*Source.Image.Length+65536),out var charge))return false;
            try
            {
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Source.Operation.ToString("N")),new XAttribute("world",Source.WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(Source.Image))),new XElement("recyclerPlacedStationSaved",new XAttribute("stage","before"),
                        new XAttribute("path",native.Path),new XAttribute("payload",native.PayloadDigest),new XAttribute("tileOffset",offset),new XAttribute("tileLength",length),new XAttribute("tileDigest",digest),
                        Bytes("marker",MarkerImage),Bytes("native",Source.BeforeNative),Bytes("nativePersistency",watch.TileSpan),Bytes("nativeMarkerSpan",marker.Span)));
                path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Source.Operation.ToString("N")+".closed-pending."+Source.Ticket.ToString("N")+".xml");
                if(!Current()||!watch.TryReadPublishedTileDescriptor(out var confirmed,out long confirmedOffset,out int confirmedLength,out string confirmedDigest)||
                    !ReferenceEquals(native,confirmed)||offset!=confirmedOffset||length!=confirmedLength||digest!=confirmedDigest||
                    !RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
                receipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(receipt==null)return false;
                memory=charge;charge=null;return Current();
            }
            finally{charge?.Dispose();}
        }
        internal static ClosedPendingPublication FromActualPendingLoad(ClosedAuthorityCompletion source,LoadedStation loaded)
        {
            if(source?.Current()!=true||loaded?.Current()!=true||loaded.Stage!="before"||loaded.Operation!=source.Operation||!loaded.MatchesOriginal(source.Image)||
                !ReferenceEquals(loaded.Station,source.Station)||!RebirthLogisticsEndpointIdentity.TryGetIncarnation(source.World,source.Station,out string birth)||
                !RebirthRemoteResourceNativeBinding.TryResolve(source.World,RemoteResourceIdentity.Workstation(source.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,birth,out var binding)||
                !loaded.Chunk.ChunkCustomData.dict.TryGetValue(loaded.MarkerKey,out var marker)||!marker.data.SequenceEqual(loaded.MarkerImage))return null;
            long length=RebirthWorkstationCompletionPublication.PublishedLength(loaded.ReceiptPath);
            if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var charge))return null;
            var result=new ClosedPendingPublication(source,binding,loaded.Chunk,loaded.MarkerKey,marker){path=loaded.ReceiptPath,memory=charge};
            result.receipt=loaded.CopyReceipt();if(result.receipt==null||!result.Current()||!loaded.Current()){result.Dispose();return null;}
            result.loaded=loaded;return result;
        }
        internal bool ReuseActualLoadedPlan(RemoteHostOriginal source,string planPath)
        {
            if(loaded?.Current()!=true||!Current()||!ReferenceEquals(Source.Attached,source)||!ReferenceEquals(source.ClosedAuthority,Source)||
                Source.OwnerGeneration==Guid.Empty||!source.Current()||ArtifactNodeExists(planPath+".tmp")||ArtifactNodeExists(planPath+".bak"))return false;
            long length=RebirthWorkstationCompletionPublication.PublishedLength(planPath);
            if(length<1||length>12L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*length+65536),out var charge))return false;
            using(charge)
            {
                var record=RebirthWorkstationCompletionPublication.ReadPrimary(planPath);
                if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=source.Operation.ToString("N")||
                    (string)record.Attribute("world")!=source.WorldKey||record.Elements().Count()!=1||
                    !XNode.DeepEquals(record.Element("before")?.Elements().SingleOrDefault(),XElement.Parse(Encoding.UTF8.GetString(source.Image)))||
                    !Current()||!loaded.Current())return false;
                source.PlanSaved=true;return true;
            }
        }
        internal void MarkPromoted(){promoted=true;watch?.Dispose();watch=null;receipt=null;loaded?.Dispose();loaded=null;memory?.Dispose();memory=null;}
        public void Dispose(){if(closed)return;closed=true;watch?.Dispose();watch=null;receipt=null;loaded?.Dispose();loaded=null;memory?.Dispose();memory=null;}
    }
    internal sealed partial class ClosedAuthorityCompletion
    {internal ClosedPendingPublication Pending;}
    internal sealed partial class StationPublication
    {
        internal static StationPublication FromClosedPending(RemoteHostOriginal source)
        {
            var pending=source.ClosedAuthority?.Pending;
            if(pending==null||!ReferenceEquals(pending.Source.Attached,source)||!pending.Saved()||!source.Current())return null;
            var result=new StationPublication(source,pending.Binding,pending.Chunk,pending.Key,pending.Marker);
            if(!result.Current()){result.Dispose();return null;}
            result.watch=RebirthRemoteResourceNativePublication.Begin(pending.Binding,result.Current,pending.Key);
            pending.MarkPromoted();return result;
        }
    }
}
