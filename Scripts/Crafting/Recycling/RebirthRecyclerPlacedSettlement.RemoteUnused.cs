using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal static void ObserveRecyclerWindowClosed(XUiC_WorkstationWindowGroup window,bool run)
    {
        if(!run||!ThreadManager.IsMainThread())return;
        foreach(var client in recyclerClients.Where(value=>ReferenceEquals(value.Window,window)&&ReferenceEquals(value.Source?.Station,window.WorkstationData?.TileEntity)).ToArray())
        {
            if(!client.MetadataCurrent()||client.Unknown||client.PrincipalEntered||client.Applied||client.NativeSent||
                client.Owner!=null&&!client.Owner.ObserveUnusedOwnerBoundary()||
                !client.Source.MatchesOwnerCheckpoint(client.Source.Actor.bag.ItemGrid.items,client.Source.Actor.inventory.ItemGrid.items,0)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(client.Source.Station,client.Source.BeforeNative,client.Source.BeforeOffset)||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeUiSession(window,out long session)||session!=client.Session)continue;
            var close=new XElement("recyclerRemoteUnusedClose",new XAttribute("nonce",client.Nonce.ToString("N")),new XAttribute("session",session),
                new XAttribute("actualFinalClose",true),new XAttribute("principalEntered",false),new XAttribute("ownerSlotsEntered",false),new XAttribute("beforeReply",client.Nonce==Guid.Empty),
                new XAttribute("nativeBefore",RebirthStationPublicationRecord.Digest(client.Source.BeforeNative)));
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",client.Source.Operation.ToString("N")),new XAttribute("world",client.Source.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(client.Source.Image))),close);
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),client.Source.Operation.ToString("N")+".remote-unused-request.xml");
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))continue;
            client.UnusedCloseObserved=true;SendPlacedText(UnusedClientCommand(client).ToString(SaveOptions.DisableFormatting));
        }
    }
    private static XElement UnusedClientCommand(RecyclerClient client)
    {var command=ClientCommand(client,"unused");if(client.UnusedCloseObserved&&client.Nonce==Guid.Empty)command.Add(new XAttribute("beforeReply",true));return command;}
    private static bool RequestRemoteRecyclerUnused(RemoteHostOriginal source)
    {
        if(source.UnusedRequested)return true;
        if(!source.Current()||source.AcceptedStation||source.AdmissionUnknown||source.PendingReentry!=null||
            source.OwnerCheckpoint!=null&&(source.OwnerCheckpoint.Phase!=1||source.OwnerCheckpoint.Completed!=0||source.OwnerCheckpoint.Workers!=0||!source.OwnerCheckpoint.CurrentSaved())||
            source.Publication==null&&source.AdmissionEntered||source.Publication!=null&&!source.Publication.RetainedSavedCustody(source))return false;
        // Actual authentic original close is only a request. The host's exact
        // retained before custody/no accepted principal decides unusedness.
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
            new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerRemoteUnusedEntered",
                new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("principalAccepted",false),new XAttribute("ownerSlotsAccepted",false),
                source.Publication?.CopyRetainedSavedCustody(source),source.OwnerCheckpoint?.CopySavedDescriptor()));
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".remote-unused-entered.xml");
        if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!source.Current())return false;
        source.UnusedRequested=true;return true;
    }
    private static bool PumpRemoteRecyclerUnused(RemoteHostOriginal source)
    {
        if(!source.UnusedRequested||source.AcceptedStation||!source.NativeLineageCurrent()||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.BeforeNative,source.BeforeOffset))return false;
        if(source.Publication!=null&&!source.Publication.RetireUnusedBefore(source))return false;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
            new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerRemoteUnusedSaved",
                new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("principalAccepted",false),new XAttribute("ownerSlotsAccepted",false),
                new XAttribute("metadataMarkerEntered",source.Publication!=null),source.Publication?.CopyCleanupSaved()));
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".remote-unused-host.xml");
        if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
            !source.Access.ObserveRemoteUnused(source))return false;
        if(source.ClosedAuthority!=null)
        {
            // The actual source's saved no-effect outcome and native cleanup
            // are positive here. Its retained grant must not keep a disposed
            // recipient source attached and hold the station indefinitely.
            if(!source.Access.Ready())return false;
            source.ClosedAuthority.Dispose();source.ClosedAuthority=null;
        }
        remoteRecyclerOriginals.Remove(source.Operation);source.Dispose();return true;
    }
    private static bool ReplyRemoteRecyclerUnused(NetPackageRebirthRecyclerPlacedRequestV1 packet,World world,XElement command,Guid operation)
    {
        string action=(string)command.Attribute("command");if(action!="unused"&&action!="query")return false;
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".remote-unused-host.xml");
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||length>24L*1024*1024||!RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out var identity)||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var memory))return false;
        using(memory)
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var original=record?.Element("before")?.Elements().SingleOrDefault();var unused=record?.Element("recyclerRemoteUnusedSaved");
            if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=operation.ToString("N")||original?.Name!="placedRecyclerOriginal"||unused==null||
                (string)original.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||
                (string)original.Attribute("owner")!=identity.StorageKey||(string)original.Attribute("account")!=identity.CanonicalId||
                ((string)unused.Attribute("nonce")!=(string)command.Attribute("nonce")&&
                    !((bool?)command.Attribute("beforeReply")==true&&(string)command.Attribute("nonce")==Guid.Empty.ToString("N")))||
                (bool?)unused.Attribute("principalAccepted")!=false||(bool?)unused.Attribute("ownerSlotsAccepted")!=false||
                (bool?)unused.Attribute("metadataMarkerEntered")==true&&unused.Element("recyclerCleanupSaved")==null||
                RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting)))!=(string)command.Attribute("original"))return false;
            var reply=new XElement("recyclerRemoteUnused",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("nonce",(string)unused.Attribute("nonce")),
                new XAttribute("original",(string)command.Attribute("original")),new XAttribute("owner",identity.StorageKey),new XAttribute("world",(string)original.Attribute("world")),
                new XAttribute("creation",(string)original.Attribute("creation")),new XAttribute("deaths",(int)original.Attribute("deaths")),new XAttribute("savedUnused",true));
            packet.Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthRecyclerPlacedReplyV1>().Setup(reply.ToString(SaveOptions.DisableFormatting)));return true;
        }
    }
    private static bool ObserveRemoteRecyclerUnusedReply(XElement reply,INetConnection connection)
    {
        if(reply.Name!="recyclerRemoteUnused")return false;
        if((int?)reply.Attribute("version")!=1||!Guid.TryParseExact((string)reply.Attribute("operation"),"N",out var operation))return true;
        if(ObserveJoinedRecyclerUnusedReply(reply,connection,operation))return true;
        var client=recyclerClients.SingleOrDefault(value=>value.Source?.Operation==operation&&ReferenceEquals(value.Connection,connection));
        if(client?.UnusedCloseObserved!=true||!client.Source.OwnerLineageCurrent()||client.PrincipalEntered||client.Applied||client.NativeSent||client.Unknown||
            (client.Nonce!=Guid.Empty&&(string)reply.Attribute("nonce")!=client.Nonce.ToString("N"))||
            !Guid.TryParseExact((string)reply.Attribute("nonce"),"N",out var savedNonce)||savedNonce==Guid.Empty||
            (string)reply.Attribute("original")!=RebirthStationPublicationRecord.Digest(client.Source.Image)||
            (string)reply.Attribute("owner")!=client.Source.Owner||(string)reply.Attribute("world")!=client.Source.WorldKey||(string)reply.Attribute("creation")!=client.Source.Creation||
            (int?)reply.Attribute("deaths")!=client.Source.Deaths||(bool?)reply.Attribute("savedUnused")!=true||
            !client.Source.MatchesOwnerCheckpoint(client.Source.Actor.bag.ItemGrid.items,client.Source.Actor.inventory.ItemGrid.items,0))return true;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",client.Source.WorldKey),
            new XElement("before",XElement.Parse(Encoding.UTF8.GetString(client.Source.Image))),
            new XElement("recyclerUnusedAdmission",new XAttribute("nativeStationEntered",false),new XAttribute("nativeOwnerEntered",false)),new XElement(reply));
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".unused-admission.xml");
        if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return true;
        client.Acknowledged=true;recyclerClients.Remove(client);
        var packet=client.AutomaticPacket;long sequence=client.AutomaticObservation?.Sequence??0;
        var manual=client.Owner?.PendingCheckpoint;
        if(manual?.FrozenPacket!=null&&manual.NativeSnapshotSequence>sequence)
        {var older=packet;packet=manual.FrozenPacket;packet.RegisterSendQueue();sequence=manual.NativeSnapshotSequence;older?.SendQueueHandled();}
        client.AutomaticPacket=null;client.AutomaticObservation=null;
        // Never upload an older held native PDS over a positively observed
        // newer snapshot. Ordinary native autosave remains free after unused;
        // no financial inventory effect needs replay or restoration.
        try{if(packet!=null&&sequence==client.AutomaticSequence)SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(packet);}
        finally{packet?.SendQueueHandled();client.Dispose();}
        return true;
    }
}
