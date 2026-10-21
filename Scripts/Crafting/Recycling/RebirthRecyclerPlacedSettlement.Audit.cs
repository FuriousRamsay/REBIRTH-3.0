using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Read-only native acceptance evidence. No admission, save, reentry or pump.
    internal static JObject CaptureAuditState(EntityPlayer actor,Guid operation=default)
    {
        var result=new JObject{["available"]=false,["installed"]=authorityInstalled};
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||actor==null||!ReferenceEquals(actor.world,world)||
            !ReferenceEquals(world.GetEntity(actor.entityId),actor))return result;
        RebirthStablePlayerIdentity identity;
        if(world.IsRemote())
        {if(!ReferenceEquals(world.GetPrimaryPlayer(),actor)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity))return result;}
        else if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out identity))return result;
        int entity=actor.entityId,deaths=actor.Died;string creation=RebirthCamperCharacterAccess.CurrentCreation(actor),worldKey=RebirthWorkstationCompletionOutbox.CurrentGuid(world);
        result["owner"]=identity.StorageKey;result["account"]=identity.CanonicalId;
        result["world"]=RebirthWorkstationCompletionOutbox.CurrentGuid(world);result["creation"]=RebirthCamperCharacterAccess.CurrentCreation(actor);
        result["deaths"]=actor.Died;result["entity"]=actor.entityId;result["remote"]=world.IsRemote();
        if(actor is EntityPlayerLocal owner)
        {
            if(owner.bag.ItemGrid.items.Length>169||owner.inventory.ItemGrid.items.Length>20)return result;
            using(var lease=AuditRecyclerAllocation(8L*1024*1024))
            {
                if(lease==null)return result;
                result["bag"]=Grid("bag",owner.bag.ItemGrid.items).Value;result["belt"]=Grid("belt",owner.inventory.ItemGrid.items).Value;
            }
        }
        var issued=authorityOriginals.Where(value=>ReferenceEquals(value.Source?.Actor,actor)).Select(value=>value.Source.Operation)
            .Concat(recyclerClients.Where(value=>ReferenceEquals(value.Source?.Actor,actor)).Select(value=>value.Source.Operation))
            .Concat(closedAuthorityCompletions.Where(value=>ReferenceEquals(value.Recipient,actor)).Select(value=>value.Operation))
            .Concat(remoteRecyclerOriginals.Values.Where(value=>ReferenceEquals(value.Actor,actor)).Select(value=>value.Operation))
            .Concat(viewedCompletions.Where(value=>ReferenceEquals(value.Viewer,actor)).Select(value=>value.Operation)).Distinct().ToArray();
        result["issued"]=new JArray(issued.Select(value=>value.ToString("N")));
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced");
        var discovered=new System.Collections.Generic.HashSet<Guid>(issued);
        // Discovery is bounded and explicit. A saturated directory is not an
        // empty ledger or permission to start a replacement operation.
        if(operation==Guid.Empty&&Directory.Exists(root))
        {
            var paths=Directory.EnumerateFiles(root,"*.plan.xml",SearchOption.TopDirectoryOnly).Take(65).ToArray();
            if(paths.Length>64){result["saturated"]=true;return result;}
            foreach(string path in paths)
            {
                var file=Path.GetFileName(path);if(!Guid.TryParseExact(file.Substring(0,file.Length-9),"N",out var candidate))continue;
                long size=RebirthWorkstationCompletionPublication.PublishedLength(path);
                if(size<1||size>24L*1024*1024)continue;
                using(var lease=AuditRecyclerAllocation(size))
                {
                    if(lease==null)return result;
                    var saved=RebirthWorkstationCompletionPublication.ReadPrimary(path);var original=saved?.Element("before")?.Elements().SingleOrDefault();
                    if((string)original?.Attribute("owner")==identity.StorageKey&&
                        (string)original.Attribute("world")==RebirthWorkstationCompletionOutbox.CurrentGuid(world))discovered.Add(candidate);
                }
            }
        }
        result["operations"]=new JArray(discovered.Select(value=>value.ToString("N")));
        var records=new JArray();result["records"]=records;
        XElement source=null;long total=0;
        if(operation!=Guid.Empty)
        {
            result["operation"]=operation.ToString("N");
            result["viewedHeld"]=viewedCompletions.Any(value=>value.Operation==operation&&ReferenceEquals(value.Viewer,actor)&&value.Current(false));
            foreach(string suffix in new[]{".plan.xml",".remote-client.xml",".terminal.xml",".cleanup-saved.xml",".remote-completed.xml",".remote-retired.xml",".unused-admission.xml",".remote-unused-host.xml"})
            {
                string path=Path.Combine(root,operation.ToString("N")+suffix);
                if(!ArtifactNodeExists(path)&&!ArtifactNodeExists(path+".tmp")&&!ArtifactNodeExists(path+".bak"))continue;
                long size=RebirthWorkstationCompletionPublication.PublishedLength(path);total=checked(total+Math.Max(0,size));
                if(size<1||size>24L*1024*1024||total>64L*1024*1024){result["incomplete"]=true;return result;}
                using(var lease=AuditRecyclerAllocation(size))
                {
                    if(lease==null)return result;
                    var saved=RebirthWorkstationCompletionPublication.ReadPrimary(path);var original=saved?.Element("before")?.Elements().SingleOrDefault();
                    // A separate native viewer observes its own nonfinancial
                    // hold; recipient originals belong to the owner endpoint.
                    if(original?.Name=="placedRecyclerOriginal"&&(string)original.Attribute("owner")!=identity.StorageKey)
                    {result["foreignOwner"]=true;continue;}
                    if(saved?.Name!="workstationCompletionIntent"||(string)saved.Attribute("operation")!=operation.ToString("N")||
                        original?.Name!="placedRecyclerOriginal"||(string)original.Attribute("owner")!=identity.StorageKey||
                        (string)original.Attribute("account")!=identity.CanonicalId||(string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world))
                    {result["incomplete"]=true;return result;}
                    if(source!=null&&!XNode.DeepEquals(source,original)){result["incomplete"]=true;return result;}
                    source=source??new XElement(original);
                    records.Add(new JObject{["suffix"]=suffix,["xml"]=saved.ToString(SaveOptions.DisableFormatting),["digest"]=RecyclerFileDigest(path)});
                    if(suffix==".terminal.xml")result["terminalArtifactsValid"]=AuditRecyclerTerminal(saved,original,operation,identity.CanonicalId);
                }
            }
            result["held"]=issued.Contains(operation);
            if(source!=null)
            {
                var position=new Vector3i((int)source.Attribute("x"),(int)source.Attribute("y"),(int)source.Attribute("z"));
                var station=world.GetTileEntity(position) as TileEntityWorkstation;
                result["stationHeld"]=station!=null&&HoldsStation(station);
                result["nativeMarker"]=station!=null&&HasNativeOriginalMarker(station);
                result["original"]=source.ToString(SaveOptions.DisableFormatting);
                var viewer=AuditRecyclerViewer(source,root,operation,64L*1024*1024-total);
                if(viewer!=null)result["nativeViewer"]=new JObject{["entity"]=(int)viewer.Attribute("entity"),
                    ["creation"]=(string)viewer.Attribute("creation"),["deaths"]=(int)viewer.Attribute("deaths")};
                if(station!=null)
                {
                    using(var memory=AuditRecyclerAllocation(RebirthProductiveEarnedNativeBody.MaximumBodyBytes))
                    using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    {
                        if(memory==null||decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out var native,out int offset))return result;
                        result["stationNative"]=Convert.ToBase64String(native);result["stationOffset"]=offset;
                    }
                }
            }
        }
        result["available"]=ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(actor.world,world)&&actor.entityId==entity&&
            ReferenceEquals(world.GetEntity(entity),actor)&&actor.Died==deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(world)==worldKey;return result;
    }
    private static IDisposable AuditRecyclerAllocation(long bytes)=>
        RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*bytes+65536),out var memory)?memory:null;
    private static XElement AuditRecyclerViewer(XElement original,string root,Guid operation,long remaining)
    {
        var local=original.Element("nativeViewer");if(local!=null)return new XElement(local);
        var closed=original.Element("closedAuthority");
        if(!Guid.TryParseExact((string)closed?.Attribute("ticket"),"N",out var ticket)||ticket==Guid.Empty)return null;
        string path=Path.Combine(root,operation.ToString("N")+".closed-authority."+ticket.ToString("N")+".xml");
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||length>24L*1024*1024||length>remaining)return null;
        using(var memory=AuditRecyclerAllocation(length))
        {
            if(memory==null)return null;
            var archive=RebirthWorkstationCompletionPublication.ReadPrimary(path);var source=archive?.Element("recyclerClosedAuthoritySource");
            var native=source?.Element("recyclerClosedAuthorityOriginal");
            if((string)archive?.Attribute("operation")!=operation.ToString("N")||(string)source?.Attribute("ticket")!=ticket.ToString("N")||
                (string)native?.Attribute("operation")!=operation.ToString("N")||!HasClosedSourceProvenance(source,native)||
                (bool?)source.Attribute("actualNativeUi")!=true||!XNode.DeepEquals(archive.Element("before")?.Elements().SingleOrDefault(),native)||
                RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(native.ToString(SaveOptions.DisableFormatting)))!=(string)closed.Attribute("nativeOriginal"))return null;
            return native.Element("nativeViewer") is XElement viewer?new XElement(viewer):null;
        }
    }
    private static bool AuditRecyclerTerminal(XElement record,XElement original,Guid operation,string account)
    {
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
        if(decoder==null)return false;
        var terminal=record.Element("recyclerPlacedTerminal");if(terminal==null)return false;
        var station=terminal.Element("actualStationSaved")?.Elements().SingleOrDefault();var nativeStation=station?.Element("recyclerPlacedStationSaved");
        if(station?.Name!="workstationCompletionIntent"||!XNode.DeepEquals(station.Element("before")?.Elements().SingleOrDefault(),original)||
            (string)nativeStation?.Attribute("stage")!="after"||!XNode.DeepEquals(nativeStation.Element("native"),
                new XElement("native",original.Element("afterNative")?.Value)))return false;
        var beforeBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBag"),4*1024*1024));
        var beforeBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBelt"),4*1024*1024));
        var afterBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBag"),4*1024*1024));
        var afterBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBelt"),4*1024*1024));
        if(beforeBag.Length!=afterBag.Length||beforeBelt.Length!=afterBelt.Length)return false;
        int changed=beforeBag.Where((value,index)=>!Same(value,afterBag[index])).Count()+beforeBelt.Where((value,index)=>!Same(value,afterBelt[index])).Count();
        var remote=terminal.Elements("actualRemoteOwnerSaved").ToArray();
        if(remote.Length!=0)return remote.Length==2&&
            ValidateHistoricalRemoteOwnerDescriptor(original,operation,(string)original.Attribute("world"),account,remote[0],1,0)&&
            ValidateHistoricalRemoteOwnerDescriptor(original,operation,(string)original.Attribute("world"),account,remote[1],3,changed);
        var owner=terminal.Element("actualOwnerSaved")?.Elements().SingleOrDefault();
        var nativeOwner=owner?.Element("recyclerNativeOwnerSaved")??owner?.Element("recyclerNativeOwnerLoaded");
        return owner?.Name=="workstationCompletionIntent"&&XNode.DeepEquals(owner.Element("before")?.Elements().SingleOrDefault(),original)&&
            (int?)nativeOwner?.Attribute("phase")==3&&(int?)nativeOwner.Attribute("completed")==changed&&
            ColdBytes(nativeOwner.Element("native"),16*1024*1024)?.Length>5;
        }
    }
}
