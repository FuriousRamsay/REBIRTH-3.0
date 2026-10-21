using System;
using System.IO;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    private static bool HasDetachedActionTarget(WorldBase world,Vector3i position)
    {lock(actionTargetLoadGate)return actionTargetNativeOriginals.Any(value=>value.Payment==null&&ReferenceEquals(value.World,world)&&value.Position==position&&value.Current());}
    private static bool RememberPublishedActionTarget(ActionPayment payment,RebirthRemoteResourceNativePublication.Watch watch,
        RebirthRemoteResourceNativePublication.SavedTile saved,bool after)
    {
        IDisposable memory=null;
        try
        {
            if(payment?.TargetCurrent()!=true||watch?.Saved()!=true||!ReferenceEquals(watch.ElectricalOriginal,payment)||!ReferenceEquals(watch.Tile,payment.ServiceTarget)||
                watch.Chunk==null||watch.Markers?.Length!=1||payment.World.ChunkCache?.ChunkProvider is not ChunkProviderGenerateWorld provider||provider.m_RegionFileManager is not RegionFileManager manager||
                actionTargetNativeOriginals.Count>=4&&payment.RestartTargetSource==null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out memory))return false;
            var marker=watch.Markers[0];string key=RebirthElectricalSourceIdentity.NativeKey(payment.ServiceCandidate.Position);
            if(marker.Key!=key||!watch.Chunk.ChunkCustomData.dict.TryGetValue(key,out var current)||!ReferenceEquals(current,marker.Value)||current.data==null||!current.data.SequenceEqual(marker.Bytes))return false;
            var retained=new ActionTargetNativeOriginal{Operation=payment.Operation,World=payment.World,Save=payment.Save,NativeWorld=payment.NativeWorld,Recovery=payment.Recovery,
                Directory=payment.Directory,Original=payment.Original,Position=payment.ServiceCandidate.Position,MarkerKey=key,Region=Path.GetFullPath(manager.saveDirectory),
                Util=manager.snapshotUtil,Access=manager.regionFileAccess,Chunk=watch.Chunk,Body=payment.ServiceTarget,NativeMarker=current,Marker=marker.Bytes.ToArray(),
                Snapshot=saved.CopySnapshot(),Tile=saved.CopyTile(),Payload=saved.CopyPayload(),Offset=saved.TileOffset,Head=ActionHeadBytes(Path.Combine(payment.Directory,"current.xml")),
                Payment=payment,Installed=true,BeforeFirstGrant=!after,LoadedAfterPublication=after,NativeSavedOrigin=true,Memory=memory};
            retained.Type=(TileEntityType)retained.Snapshot[(int)retained.Offset-1];
            long bytes=checked(retained.Snapshot.LongLength+retained.Tile.LongLength+retained.Payload.LongLength+retained.Marker.LongLength+retained.Head.LongLength+2L*retained.Original.Length+65536);
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,bytes)||!retained.Current())return false;
            lock(actionTargetLoadGate)
            {
                var old=payment.RestartTargetSource;
                if(old!=null){old.Active=false;old.Payment=null;actionTargetNativeOriginals.Remove(old);ReleaseActionTargetNativeGraph(old);}
                payment.RestartTargetSource=retained;actionTargetNativeOriginals.Add(retained);memory=null;
            }
            return true;
        }
        finally{memory?.Dispose();}
    }
    private static void DetachPublishedActionTarget(ActionPayment payment)
    {
        var source=payment.RestartTargetSource;if(source==null)return;
        lock(actionTargetLoadGate)
        {
            IDisposable scratch=null;
            try
            {
                if(!source.Current()||source.Payment!=payment||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(48L*1024*1024,out scratch))return;
                byte[] head=ActionHeadBytes(Path.Combine(payment.Directory,"current.xml"));var image=ReadActionBoundedXml(head,24*1024*1024);string stage=(string)image.Attribute("stage");
                if(image.Element("original")?.Value!=payment.Original)return;
                long retained=checked(source.Snapshot.LongLength+source.Tile.LongLength+source.Payload.LongLength+source.Marker.LongLength+(source.MarkerAfter?.LongLength??0)+head.LongLength+2L*source.Original.Length+65536);
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,retained))return;
                source.Head=head;source.BeforeFirstGrant=stage=="target-before-saved";source.Payment=null;
                payment.RestartTargetSource=null;
            }
            finally
            {
                scratch?.Dispose();
                if(ReferenceEquals(source.Payment,payment))
                {source.Active=false;source.Payment=null;payment.RestartTargetSource=null;actionTargetNativeOriginals.Remove(source);ReleaseActionTargetNativeGraph(source);}
            }
        }
    }
}
