using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static bool LoadedUnusedStationHeld(TileEntityWorkstation station)=>loadedRecyclerCleanups.Any(value=>value.UnusedPending&&
        ReferenceEquals(value.Station,station)&&value.Current()&&ReferenceEquals(value.World.GetTileEntity(station.ToWorldPos()),station));
    private static void PumpRemoteLoadedRecyclerUnused(LoadedCleanup native)
    {
        if(native.World.IsRemote()||!native.Current()||native.Native==null)return;
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced");
        foreach(string path in Directory.EnumerateFiles(root,"*.unused-marker-attempted.xml"))
        {
            string name=Path.GetFileName(path);if(!Guid.TryParseExact(name.Substring(0,name.Length-".unused-marker-attempted.xml".Length),"N",out var operation)||
                remoteRecyclerOriginals.ContainsKey(operation)||ArtifactNodeExists(Path.Combine(root,operation.ToString("N")+".remote-unused-host.xml")))continue;
            foreach(var owner in remoteLoadedRecyclerOwners.ToArray())
                if(owner.Current()&&ReferenceEquals(owner.File.world,native.World))TryPublishLoadedRecyclerUnused(native,owner,operation,path);
        }
    }
    private static bool TryPublishLoadedRecyclerUnused(LoadedCleanup native,RemoteLoadedOwner owner,Guid operation,string attemptedPath)
    {
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),enteredPath=Path.Combine(root,operation.ToString("N")+".remote-unused-entered.xml");
        long attemptedLength=RebirthWorkstationCompletionPublication.PublishedLength(attemptedPath),enteredLength=RebirthWorkstationCompletionPublication.PublishedLength(enteredPath);
        if(attemptedLength<1||enteredLength<1||attemptedLength+enteredLength>48L*1024*1024||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*(attemptedLength+enteredLength)+65536),out var memory))return false;
        using(memory)
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return false;
            var attempted=RebirthWorkstationCompletionPublication.ReadPrimary(attemptedPath);var entered=RebirthWorkstationCompletionPublication.ReadPrimary(enteredPath);
            var original=attempted?.Element("before")?.Elements().SingleOrDefault();var unused=entered?.Element("recyclerRemoteUnusedEntered");
            var saved=attempted?.Element("recyclerUnusedMarkerAttempted")?.Element("actualRetainedStationSaved");
            var peer=owner.CurrentPeer();if(peer==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity))return false;
            if(attempted?.Name!="workstationCompletionIntent"||entered?.Name!="workstationCompletionIntent"||original?.Name!="placedRecyclerOriginal"||
                (string)attempted.Attribute("operation")!=operation.ToString("N")||(string)entered.Attribute("operation")!=operation.ToString("N")||
                !XNode.DeepEquals(original,entered.Element("before")?.Elements().SingleOrDefault())||
                (string)original.Attribute("owner")!=identity.StorageKey||(string)original.Attribute("account")!=identity.CanonicalId||
                (string)original.Attribute("creation")!=owner.Creation||(int?)original.Attribute("deaths")!=owner.Deaths||
                (string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(native.World)||
                native.Station.ToWorldPos()!=new Vector3i((int)original.Attribute("x"),(int)original.Attribute("y"),(int)original.Attribute("z"))||
                native.Station.block.GetBlockName()!=(string)original.Attribute("block")||
                (bool?)unused?.Attribute("principalAccepted")!=false||(bool?)unused.Attribute("ownerSlotsAccepted")!=false||
                !Guid.TryParseExact((string)unused.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty||
                (string)saved?.Attribute("operation")!=operation.ToString("N")||(string)saved.Attribute("stage")!="before"||
                !XNode.DeepEquals(saved,unused.Element("actualRetainedStationSaved")))return false;
            string savedPath=Path.GetFullPath((string)saved.Attribute("path"));long savedLength=(long?)saved.Attribute("length")??-1;
            if(!savedPath.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
                savedLength<1||savedLength>24L*1024*1024||RebirthWorkstationCompletionPublication.PublishedLength(savedPath)!=savedLength||
                RecyclerFileDigest(savedPath)!=(string)saved.Attribute("digest"))return false;
            if(!PhaseBytes(owner.File.buffs,"rbRecyclerPlaced_"+operation.ToString("N"),out float phase)||(phase!=0&&phase!=1)||
                !PhaseBytes(owner.File.buffs,"rbRecyclerSlot_"+operation.ToString("N"),out float completed)||completed!=0||
                !SameOwnerGrid(owner.File.bag,RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBag"),4*1024*1024)))||
                !SameOwnerGrid(owner.File.belt,RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBelt"),4*1024*1024))))return false;
            byte[] before=ColdBytes(original.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);int offset=(int)original.Attribute("beforeOffset");
            if(offset<13||offset>before.Length-8||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(native.Native,out var loaded)||
                !RebirthProductiveEarnedNativeBody.Encode(loaded,BitConverter.ToUInt64(before,offset),out int actualOffset).SequenceEqual(before)||actualOffset!=offset)return false;
            string marker="RebirthRecyclerPlacedV1:"+native.Station.localChunkPos.x+":"+native.Station.localChunkPos.y+":"+native.Station.localChunkPos.z+":"+operation.ToString("N");
            if(!native.ObservedAbsent(marker)||!native.Current()||!owner.Current())return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",(string)original.Attribute("world")),
                new XElement("before",new XElement(original)),new XElement("recyclerRemoteUnusedSaved",new XAttribute("nonce",nonce.ToString("N")),
                    new XAttribute("principalAccepted",false),new XAttribute("ownerSlotsAccepted",false),new XAttribute("metadataMarkerEntered",true),
                    new XElement("recyclerCleanupSaved",new XAttribute("actualNativeLoad",true),new XAttribute("actualOwnerToPlayer",true),
                        new XAttribute("tileReadFence",native.Fence),new XAttribute("nativeBefore",RebirthStationPublicationRecord.Digest(native.Native)),new XElement(saved))));
            string result=Path.Combine(root,operation.ToString("N")+".remote-unused-host.xml");
            bool published=RebirthWorkstationCompletionPublication.Write(result,record)&&XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(result))&&native.Current()&&owner.Current();
            if(published)native.UnusedPending=false;
            return published;
        }
    }
}
