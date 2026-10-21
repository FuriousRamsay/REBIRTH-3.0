using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private sealed class InitialBudget:IRebirthStationSnapshotByteBudget
    {public int MaximumSnapshotBytes=>RebirthStationSnapshotEvidence.MaximumSnapshotBytes;public bool TryReserveAllocation(long bytes,out IDisposable value)=>RebirthProductiveEarnedObservationBudget.TryRetainDetached(bytes,out value);}
    private sealed class InitialPublication:IDisposable
    {
        internal Witness Source;internal RebirthRemoteResourceNativePublication.Watch Watch;internal string Path;internal bool Saved;internal byte[] Accepted;internal int Offset;
        internal bool Current()=>Source?.SourceCurrent()==true&&Source.Current()&&Source.Source4Image==null&&Source.InitialLoaded==null&&
            (Saved||RebirthProductiveEarnedNativeBody.MatchesCurrent(Source.Station,Accepted,Offset));
        public void Dispose(){Watch?.Dispose();Watch=null;Source=null;Accepted=null;}
    }
    private static readonly List<InitialPublication> initialPublications=new List<InitialPublication>();
    private static bool OrdinaryInitial(RecipeQueueItem row)=>row?.Recipe!=null&&!row.Recipe.IsScrap&&!RebirthCookingHeat.Managed(row.Recipe)&&
        !RebirthRecyclerRuntime.IsMarked(row.Recipe)&&!RebirthStationGridQueue.IsMarked(row.Recipe);
    private static void BeginInitialPublication(Witness source,byte[] accepted,int offset)
    {
        if(source?.Source4Image!=null||source?.InitialLoaded!=null||source?.Station is RebirthCamperStationState||!source.SourcePublished()||
            source.Station.Queue.Count(row=>source.Matches(row)&&OrdinaryInitial(row))!=1)return;
        if(initialPublications.Any(value=>ReferenceEquals(value.Source,source)))return;
        initialPublications.Add(new InitialPublication{Source=source,Path=source.Path+".initial",Accepted=(byte[])accepted.Clone(),Offset=offset});PumpInitialPublications();
    }
    internal static bool HoldsInitialPublication(TileEntityWorkstation station)
    {try{return initialPublications.Any(value=>!value.Saved&&ReferenceEquals(value.Source?.Station,station)&&value.Current());}catch{return initialPublications.Any(value=>!value.Saved&&ReferenceEquals(value.Source?.Station,station));}}
    private static void UpdateInitialPublications(ref ModEvents.SGameUpdateData data)
    {try{PumpInitialPublications();}catch{/* Observation cannot fail native GameUpdate or mint a row source. */}}
    private static void PumpInitialPublications()
    {
        if(!ThreadManager.IsMainThread())return;
        // Fresh native queues have no audit publication to poll. Retain the
        // update path only while an old load or publication is actually held.
        if(initialPublications.Count==0)
        {lock(initialLoadGate)if(initialNativeOriginals.Count==0)return;}
        ObserveWorld();
        lock(initialLoadGate)foreach(var old in initialNativeOriginals.Where(value=>!ReferenceEquals(value.World,GameManager.Instance?.World)||value.Save!=Path.GetFullPath(GameIO.GetSaveGameDir())).ToArray())
        {initialNativeOriginals.Remove(old);old.Dispose();}
        foreach(var value in initialPublications.ToArray())
        {
            try
            {
                if(!value.Current()){initialPublications.Remove(value);value.Dispose();continue;}
                var source=value.Source;var station=source.Station;
                if(value.Saved)continue;
                if(value.Watch?.Current()!=true){value.Watch?.Dispose();value.Watch=null;}
                if(value.Watch==null)
                {
                    if(!RebirthRemoteResourceNativeBinding.TryResolve(source.World,RemoteResourceIdentity.Workstation(station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,
                        source.Birth.ToString("N"),out var binding))continue;
                    var p=station.localChunkPos;
                    value.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,value.Current,new InitialBudget(),"RebirthLogisticsStationV1:"+p.x+":"+p.y+":"+p.z);
                }
                if(value.Watch?.TryReadPublishedTileDescriptor(out var publication,out long tileOffset,out int tileLength,out string tileDigest)!=true||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(8L*RebirthStationSnapshotEvidence.MaximumSnapshotBytes,out var workspace))continue;
                using var copiedWorkspace=workspace;
                if(!RebirthStationSnapshotEvidence.TryCopy(value.Watch.ChunkProof,out var snapshot,out var allocation))continue;
                using var copiedSnapshot=allocation;
                if(tileOffset<8||tileLength<1||tileOffset>snapshot.LongLength-tileLength)continue;
                var tile=new byte[tileLength];Buffer.BlockCopy(snapshot,(int)tileOffset,tile,0,tileLength);
                if(RebirthStationPublicationRecord.Digest(tile)!=tileDigest)continue;
                var marker=value.Watch.Markers.Single(item=>item.Key=="RebirthLogisticsStationV1:"+station.localChunkPos.x+":"+station.localChunkPos.y+":"+station.localChunkPos.z);
                if(!Capture(station,out var live,out int elapsed)||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(tile,out var native))continue;
                byte[] projected=RebirthProductiveEarnedNativeBody.Encode(native,0,out int offset);
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(live,elapsed,projected,offset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(live,elapsed,out var current,out _)||!source.Current())continue;
                var rows=station.Queue;var matches=Enumerable.Range(0,rows.Length).Where(index=>source.Matches(rows[index])).ToArray();
                if(matches.Length!=1||!OrdinaryInitial(rows[matches[0]])||rows[matches[0]].Multiplier!=source.OriginalCount)continue;
                string raw=value.Path+".snapshot";
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(raw,snapshot,RebirthStationSnapshotEvidence.MaximumSnapshotBytes))continue;
                var position=station.ToWorldPos();
                var node=new XElement("initialNativeQueueOwner",new XAttribute("version",1),new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("birth",source.Birth.ToString("N")),
                    new XAttribute("owner",source.OriginalOwnerKey),new XAttribute("creation",source.Creation),new XAttribute("entity",source.StartingEntityId),new XAttribute("deaths",source.Deaths),
                    new XAttribute("world",source.WorldKey),new XAttribute("save",source.Save),new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),
                    new XAttribute("slot",matches[0]),new XAttribute("count",source.OriginalCount),new XAttribute("offset",tileOffset),new XAttribute("tileLength",tile.Length),
                    new XAttribute("snapshotLength",snapshot.Length),new XAttribute("snapshotDigest",InitialHash(snapshot,8,snapshot.Length-8)),new XAttribute("tileDigest",InitialHash(tile)),
                    new XAttribute("originalDigest",InitialHash(source.Image)),new XElement("nativeRowDigest",InitialHash(Encoding.UTF8.GetBytes(NativeBinding(rows[matches[0]])))),new XElement("marker",new XAttribute("key",marker.Key),Convert.ToBase64String(marker.Bytes)));
                byte[] receipt=Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));
                if(receipt.Length>8192||!value.Current()||!value.Watch.TryReadPublishedTileDescriptor(out var confirmed,out long confirmedOffset,out int confirmedLength,out string confirmedDigest)||
                    !ReferenceEquals(publication,confirmed)||tileOffset!=confirmedOffset||tileLength!=confirmedLength||tileDigest!=confirmedDigest||
                    !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(value.Path,receipt,8192))continue;
                value.Saved=true;value.Watch.Dispose();value.Watch=null;value.Accepted=null;
            }
            catch{/* Actual earlier insertion remains retained; no native work or source is invented. */}
        }
    }
    private static string InitialHash(byte[] bytes,int offset=0,int count=-1)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(bytes,offset,count<0?bytes.Length:count));}
}
