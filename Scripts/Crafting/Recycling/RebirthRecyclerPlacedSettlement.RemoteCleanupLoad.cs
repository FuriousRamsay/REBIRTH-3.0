using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static void PumpRemoteLoadedRecyclerCleanup(LoadedCleanup native)
    {
        if(native.World.IsRemote())return;
        foreach(var owner in remoteLoadedRecyclerOwners.ToArray())
        {
            if(!owner.Current()||!ReferenceEquals(owner.File.world,native.World))continue;
            foreach(var receipt in ReadRecyclerReceipts(owner.File.buffs))
                if(receipt.Value==3&&!remoteRecyclerOriginals.ContainsKey(receipt.Key))TryRetireRemoteLoadedRecyclerCleanup(owner,native,receipt.Key);
        }
    }
    private static bool TryRetireRemoteLoadedRecyclerCleanup(RemoteLoadedOwner owner,LoadedCleanup native,Guid operation)
    {
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),path=Path.Combine(root,operation.ToString("N")+".remote-completed.xml");
        if(new[]{path,path+".tmp",path+".bak"}.Any(ArtifactNodeExists))return false;
        string terminalPath=Path.Combine(root,operation.ToString("N")+".terminal.xml"),cleanupPath=Path.Combine(root,operation.ToString("N")+".cleanup.xml");
        long length=RebirthWorkstationCompletionPublication.PublishedLength(terminalPath)+RebirthWorkstationCompletionPublication.PublishedLength(cleanupPath);
        if(length<2||length>48L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*length+65536),out var memory))return false;
        using(memory)
        {
            using var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(decoder==null)return false;
            var peer=owner.CurrentPeer();if(peer==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity))return false;
            var terminal=RebirthWorkstationCompletionPublication.ReadPrimary(terminalPath);var cleanup=RebirthWorkstationCompletionPublication.ReadPrimary(cleanupPath);
            var original=terminal?.Element("before")?.Elements().SingleOrDefault();var retirement=cleanup?.Element("recyclerTerminalMarkerRetirement");
            if(terminal?.Name!="workstationCompletionIntent"||cleanup?.Name!="workstationCompletionIntent"||original?.Name!="placedRecyclerOriginal"||
                (string)terminal.Attribute("operation")!=operation.ToString("N")||(string)cleanup.Attribute("operation")!=operation.ToString("N")||
                !XNode.DeepEquals(original,cleanup.Element("before")?.Elements().SingleOrDefault())||terminal.Element("recyclerPlacedTerminal")==null||retirement==null||
                (string)original.Attribute("owner")!=identity.StorageKey||(string)original.Attribute("account")!=identity.CanonicalId||(string)original.Attribute("creation")!=owner.Creation||
                (int?)original.Attribute("deaths")!=owner.Deaths||(string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(native.World)||
                new Vector3i((int)original.Attribute("x"),(int)original.Attribute("y"),(int)original.Attribute("z"))!=native.Station.ToWorldPos()||
                !PhaseBytes(owner.File.buffs,"rbRecyclerPlaced_"+operation.ToString("N"),out float phase)||phase!=3f)return false;
            var bag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBag"),4*1024*1024));
            var belt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBelt"),4*1024*1024));
            var beforeBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBag"),4*1024*1024));
            var beforeBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBelt"),4*1024*1024));
            if(!SameOwnerGrid(bag,owner.File.bag)||!SameOwnerGrid(belt,owner.File.belt)||beforeBag?.Length!=bag.Length||beforeBelt?.Length!=belt.Length)return false;
            int slots=beforeBag.Zip(bag,(a,b)=>Same(a,b)?0:1).Sum()+beforeBelt.Zip(belt,(a,b)=>Same(a,b)?0:1).Sum();
            if(!PhaseBytes(owner.File.buffs,"rbRecyclerSlot_"+operation.ToString("N"),out float completed)||completed!=slots)return false;
            var descriptors=terminal.Element("recyclerPlacedTerminal").Elements("actualRemoteOwnerSaved").ToArray();
            if(descriptors.Length!=2||!ValidateHistoricalRemoteOwnerDescriptor(original,operation,(string)original.Attribute("world"),identity.CanonicalId,descriptors[0],1,0)||
                !ValidateHistoricalRemoteOwnerDescriptor(original,operation,(string)original.Attribute("world"),identity.CanonicalId,descriptors[1],3,slots))return false;
            var station=terminal.Element("recyclerPlacedTerminal").Element("actualStationSaved")?.Elements().SingleOrDefault();
            var stationSaved=station?.Element("recyclerPlacedStationSaved");
            byte[] after=ColdBytes(original.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
            int afterOffset=(int)original.Attribute("afterOffset");
            if(station?.Name!="workstationCompletionIntent"||(string)station.Attribute("operation")!=operation.ToString("N")||
                (string)station.Attribute("world")!=(string)original.Attribute("world")||
                !XNode.DeepEquals(station.Element("before")?.Elements().SingleOrDefault(),original)||(string)stationSaved?.Attribute("stage")!="after"||
                !ColdBytes(stationSaved.Element("native"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes).SequenceEqual(after)||
                afterOffset<13||afterOffset>after.Length-8)return false;
            byte[] savedTile=ColdBytes(stationSaved.Element("nativePersistency"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes+32);
            if(savedTile==null||savedTile.Length!=(int?)stationSaved.Attribute("tileLength")||
                RebirthStationPublicationRecord.Digest(savedTile)!=(string)stationSaved.Attribute("tileDigest")||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(savedTile,out var savedImage)||
                !RebirthProductiveEarnedNativeBody.Encode(savedImage,BitConverter.ToUInt64(after,afterOffset),out int savedOffset).SequenceEqual(after)||savedOffset!=afterOffset)return false;
            byte[] terminalBytes=ColdBytes(retirement.Element("terminal"),32*1024*1024);
            if(terminalBytes==null||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(terminalPath,terminalBytes))return false;
            string marker="RebirthRecyclerPlacedV1:"+native.Station.localChunkPos.x+":"+native.Station.localChunkPos.y+":"+native.Station.localChunkPos.z+":"+operation.ToString("N");
            if(!native.ObservedAbsent(marker)||!native.Current()||!owner.Current())return false;
            // Actual primary owner Load/ToPlayer and actual native chunk
            // Load/Instantiate/read qualify settled metadata only. No Original,
            // owner setter, raw native resend, or absent-row rollback is issued.
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",(string)original.Attribute("world")),
                new XElement("before",new XElement(original)),new XElement("recyclerRemoteCompleted",new XAttribute("actualLoadedCompletion",true),
                    new XAttribute("completed",slots),new XAttribute("terminal",RebirthStationPublicationRecord.Digest(terminalBytes)),new XAttribute("terminalLength",terminalBytes.Length),
                    new XElement("recyclerCleanupSaved",new XAttribute("actualNativeLoad",true),new XAttribute("actualOwnerToPlayer",true),
                        new XAttribute("tileReadFence",native.Fence),new XAttribute("regionDirectory",native.Directory))));
            return RebirthWorkstationCompletionPublication.Write(path,record)&&XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))&&
                native.Current()&&owner.Current()&&native.ObservedAbsent(marker)&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(terminalPath,terminalBytes);
        }
    }
}
