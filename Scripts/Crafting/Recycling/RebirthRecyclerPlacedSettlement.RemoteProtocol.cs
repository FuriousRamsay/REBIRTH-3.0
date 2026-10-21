using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private sealed class RecyclerAssembly:IDisposable
    {
        internal World World;internal ClientInfo Peer;internal INetConnection Connection;internal Guid Group;internal byte[][] Parts;internal int Total,Received;internal IDisposable Memory;
        public void Dispose(){Parts=null;Memory?.Dispose();Memory=null;}
    }
    private static readonly List<RecyclerAssembly> recyclerAssemblies=new List<RecyclerAssembly>();
    private static bool RemoteRecyclerStationHeld(TileEntityWorkstation station)=>remoteRecyclerOriginals.Values.Any(value=>!value.FinancialReleased&&
        ReferenceEquals(value.World,GameManager.Instance?.World)&&ReferenceEquals(value.Station,station)&&ReferenceEquals(value.World.GetTileEntity(station.ToWorldPos()),station));
    internal static bool IsRecyclerPurposePackage(NetPackage package)=>authorityInstalled&&package is NetPackagePlayerId||
        package is NetPackageRebirthRecyclerClosedGrantV1||package is NetPackageRebirthRecyclerPlacedRequestV1||package is NetPackageRebirthRecyclerPlacedReplyV1||
        authorityInstalled&&remoteRecyclerOriginals.Count>0&&(package is NetPackageTileEntity||package is NetPackagePlayerData);
    internal static void ReceivePlacedRequest(NetPackageRebirthRecyclerPlacedRequestV1 packet,World world)
    {
        if(!authorityInstalled||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||packet.Sender==null||processingRecyclerRequest!=null||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(packet.Sender.netConnection?[0],connection)||connection.IsDisconnected())return;
        RecyclerAssembly assembly=null;
        try
        {
            var peer=packet.Sender;
            if(!peer.loginDone||peer.disconnecting||packet.Total<1||packet.Total>25*1024*1024||packet.Count!=(packet.Total+8999)/9000||packet.Index<0||packet.Index>=packet.Count||
                packet.Payload?.Length!=Math.Min(9000,packet.Total-packet.Index*9000))return;
            assembly=recyclerAssemblies.SingleOrDefault(value=>ReferenceEquals(value.World,world)&&ReferenceEquals(value.Peer,peer)&&ReferenceEquals(value.Connection,connection)&&value.Group==packet.Group);
            if(assembly==null)
            {
                if(recyclerAssemblies.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*packet.Total+65536),out var memory))return;
                assembly=new RecyclerAssembly{World=world,Peer=peer,Connection=connection,Group=packet.Group,Total=packet.Total,Parts=new byte[packet.Count][],Memory=memory};recyclerAssemblies.Add(assembly);
            }
            if(assembly.Total!=packet.Total||assembly.Parts.Length!=packet.Count)return;
            if(assembly.Parts[packet.Index]!=null)
            {if(!assembly.Parts[packet.Index].SequenceEqual(packet.Payload)){recyclerAssemblies.Remove(assembly);assembly.Dispose();}return;}
            assembly.Parts[packet.Index]=(byte[])packet.Payload.Clone();assembly.Received++;
            if(assembly.Received!=assembly.Parts.Length)return;
            var bytes=new byte[assembly.Total];int at=0;foreach(byte[] part in assembly.Parts){Buffer.BlockCopy(part,0,bytes,at,part.Length);at+=part.Length;}
            processingRecyclerRequest=packet;
            try
            {
                if(bytes[0]==1)ReceivePlacedOwnerEnvelope(packet,world,connection,bytes);
                else{string text=new UTF8Encoding(false,true).GetString(bytes);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,12*1024*1024);ProcessPlacedRequest(packet,world,body);}
            }
            finally{processingRecyclerRequest=null;}
            recyclerAssemblies.Remove(assembly);assembly.Dispose();
        }
        catch{if(assembly!=null){recyclerAssemblies.Remove(assembly);assembly.Dispose();}}
    }
    private static void ProcessPlacedRequest(NetPackageRebirthRecyclerPlacedRequestV1 packet,World world,XElement body)
    {
        if(body.Name=="recyclerViewedCompletion"){ReceiveViewedCompletion(packet,world,body);return;}
        if(body.Name=="recyclerRemotePrepare")
        {
            var node=body.Element("original")?.Elements().SingleOrDefault();
            if(!Guid.TryParseExact((string)node?.Attribute("operation"),"N",out var operation)||operation==Guid.Empty)return;
            if(remoteRecyclerOriginals.TryGetValue(operation,out var retained))
            {
                if(ReferenceEquals(retained.Peer,packet.Sender)&&ReferenceEquals(retained.Connection,packet.Sender.netConnection?[0])&&retained.World==world&&
                    retained.Image!=null&&Encoding.UTF8.GetString(retained.Image)==node.ToString(SaveOptions.DisableFormatting))ReplyPlaced(retained);
                return;
            }
            if(remoteRecyclerOriginals.Count>=4)return;
            var source=RemoteHostOriginal.Capture(packet,world,body);if(source==null)return;
            bool transferred=false;
            try
            {
                string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),path=Path.Combine(root,operation.ToString("N")+".plan.xml");
                bool priorPlan=ArtifactNodeExists(path)||ArtifactNodeExists(path+".tmp")||ArtifactNodeExists(path+".bak");
                if(ArtifactNodeExists(Path.Combine(root,operation.ToString("N")+".terminal.xml"))||
                    priorPlan&&source.ClosedAuthority?.Pending?.ReuseActualLoadedPlan(source,path)!=true)return;
                source.Access=source.ClosedAuthority!=null?source.ClosedAuthority.TransferAccess(source):AccessHold.CapturePending(source);if(source.Access==null)return;
                remoteRecyclerOriginals.Add(operation,source);transferred=true;
                TryPrepareRemoteAdmission(source);ReplyPlaced(source);
            }
            finally{if(!transferred)source.Dispose();}
            return;
        }
        if(body.Name!="recyclerRemoteCommand"||!Guid.TryParseExact((string)body.Attribute("operation"),"N",out var op))return;
        if(!remoteRecyclerOriginals.TryGetValue(op,out var current)){if(!ReplyRemoteRecyclerUnused(packet,world,body,op))ReplyPlacedRetired(packet,world,body,op);return;}
        if(current.FinancialReleased&&ReplyPlacedRetired(packet,world,body,op))
        {
            if((string)body.Attribute("command")=="ack")
            {remoteRecyclerOriginals.Remove(op);current.Dispose();}
            return;
        }
        bool earlyUnused=(string)body.Attribute("command")=="unused"&&(bool?)body.Attribute("beforeReply")==true&&
            (string)body.Attribute("nonce")==Guid.Empty.ToString("N")&&current.OwnerCheckpoint==null&&current.OwnerBeforeCheckpoint==null&&
            !current.AcceptedStation&&!current.AdmissionUnknown;
        if(!ReferenceEquals(current.World,world)||!ReferenceEquals(current.Peer,packet.Sender)||
            !ReferenceEquals(current.Connection,packet.Sender.netConnection?[0])||
            (string)body.Attribute("nonce")!=current.Nonce.ToString("N")&&!earlyUnused||
            (string)body.Attribute("original")!=RebirthStationPublicationRecord.Digest(current.Image))return;
        string command=(string)body.Attribute("command");
        if(command=="unused")
        {if(RequestRemoteRecyclerUnused(current)&&PumpRemoteRecyclerUnused(current))ReplyRemoteRecyclerUnused(packet,world,body,op);return;}
        if(command=="owner")RemoteOwnerCheckpoint.Begin(current,body.Element("recyclerOwnerCheckpoint"));
        else if(command=="ack")
        {
            if(current.FinancialReleased&&current.TerminalPath!=null&&current.Terminal!=null&&current.Access?.Ready()==true&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(current.TerminalPath,current.Terminal))
            {
                var acknowledgement=new XElement("workstationCompletionIntent",new XAttribute("operation",op.ToString("N")),new XAttribute("world",current.WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(current.Image))),new XElement("recyclerRemoteReceived",new XAttribute("nonce",current.Nonce.ToString("N")),
                        new XAttribute("completed",current.Slots.Length),new XAttribute("terminal",RebirthStationPublicationRecord.Digest(current.Terminal))));
                string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),op.ToString("N")+".remote-received.xml");
                if(RebirthWorkstationCompletionPublication.Write(path,acknowledgement)&&XNode.DeepEquals(acknowledgement,RebirthWorkstationCompletionPublication.ReadPrimary(path)))
                {ReplyPlaced(current,true);remoteRecyclerOriginals.Remove(op);current.Dispose();return;}
            }
        }
        else if(command!="query")return;
        ReplyPlaced(current);
    }
    private static void ReplyPlaced(RemoteHostOriginal source,bool retired=false)
    {
        if(!source.OwnerCurrent()||source.Image==null)return;
        int stage=retired?6:source.FinancialReleased?5:source.OwnerCheckpoint?.Phase==3&&source.OwnerCheckpoint.CurrentSaved()?4:
            source.AcceptedStation&&source.Publication?.SavedAfter()==true?3:source.OwnerBeforeCheckpoint?.CurrentSaved()==true?2:source.Publication?.SavedBefore()==true?1:0;
        var response=new XElement("recyclerRemoteStatus",new XAttribute("version",1),new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("nonce",source.Nonce.ToString("N")),
            new XAttribute("original",RebirthStationPublicationRecord.Digest(source.Image)),new XAttribute("stage",stage),new XAttribute("phase",source.OwnerCheckpoint?.Phase??0),
            new XAttribute("completed",source.OwnerCheckpoint?.Completed??0),new XAttribute("ownerSaved",source.OwnerCheckpoint?.CurrentSaved()==true),
            new XAttribute("automaticSequence",source.AutomaticOwnerSequence),
            new XAttribute("world",source.WorldKey),new XAttribute("owner",source.Owner),new XAttribute("creation",source.Creation),new XAttribute("deaths",source.Deaths));
        source.Peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthRecyclerPlacedReplyV1>().Setup(response.ToString(SaveOptions.DisableFormatting)));
    }
    private static void TryPrepareRemoteAdmission(RemoteHostOriginal source)
    {
        if(source.Publication!=null||source.AdmissionUnknown||source.AdmissionEntered||source.Access==null||source.Current()!=true||
            !source.HasOwnerAdmission())return;
        if(!source.PlanSaved)
        {
            // Retry only this retained actual introduction. No archived document
            // can create a source, and no native mutation has begun here.
            var plan=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))));
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".plan.xml");
            if(!RebirthWorkstationCompletionPublication.Write(path,plan)||!XNode.DeepEquals(plan,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
            source.PlanSaved=true;
        }
        try
        {
            source.AdmissionEntered=true;
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(source.World,source.Station,out _))
                using(var birth=RebirthLogisticsEndpointIdentity.Begin(source.World,source.Station,source.Current))
                    if(birth==null){source.AdmissionEntered=false;return;}
            var publication=source.ClosedAuthority?.Pending!=null?StationPublication.FromClosedPending(source):StationPublication.BeginBefore(source);
            // Every null return precedes the native marker setter. Exceptions
            // after entering remain unknown and never repeat that setter.
            if(publication==null){source.AdmissionEntered=false;return;}
            source.Publication=publication;
            if(!source.Access.Bind(publication))source.AdmissionUnknown=true;
        }
        catch{source.AdmissionUnknown=true;}
    }
    private static void PumpPlacedProtocol(World world)
    {
        foreach(var assembly in recyclerAssemblies.ToArray())
            if(!ReferenceEquals(assembly.World,world)||assembly.Peer.disconnecting||!assembly.Peer.loginDone||!ReferenceEquals(assembly.Peer.netConnection?[0],assembly.Connection)||assembly.Connection.IsDisconnected())
            {recyclerAssemblies.Remove(assembly);assembly.Dispose();}
        foreach(var source in remoteRecyclerOriginals.Values.ToArray())
        {
            try
            {
                if(source.PendingReentry!=null)continue;
                if(source.UnusedRequested){PumpRemoteRecyclerUnused(source);continue;}
                FinishAcceptedRecyclerStation(source);
                if(source.Publication==null)TryPrepareRemoteAdmission(source);
                if(source.Access==null||source.Publication==null)continue;
                if(source.ClosedAuthority!=null&&!source.AcceptedStation)
                {source.Publication.ApplyClosedAuthorityAfter(source);continue;}
                if(source.Terminal==null)
                {
                    if(!source.AcceptedStation||!source.Publication.SavedAfter()||source.OwnerCheckpoint?.Phase!=3||source.OwnerCheckpoint.Completed!=source.Slots.Length||
                        !source.OwnerCheckpoint.CurrentSaved()||source.OwnerBeforeCheckpoint?.CurrentSaved()!=true)continue;
                    var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
                        new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerPlacedTerminal",
                            source.OwnerBeforeCheckpoint.CopySavedDescriptor(),source.OwnerCheckpoint.CopySavedDescriptor(),new XElement("actualStationSaved",source.Publication.CopySavedAfter())));
                    string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".terminal.xml");
                    if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))continue;
                    long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
                    if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var allocation))continue;
                    byte[] bytes=null;
                    try{bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null||bytes.LongLength!=length){allocation.Dispose();continue;}}
                    catch{allocation.Dispose();throw;}
                    source.Terminal=bytes;source.TerminalPath=path;source.TerminalMemory=allocation;
                    source.Access.ObserveRemoteTerminal(source.OwnerCheckpoint,source.Publication);
                }
                if(!source.Publication.MarkerRemoved)
                {
                    if(source.OwnerCheckpoint?.Phase!=3||source.OwnerCheckpoint.Completed!=source.Slots.Length||!source.OwnerCheckpoint.CurrentSaved()||
                        source.OwnerBeforeCheckpoint?.CurrentSaved()!=true||!source.Publication.SavedAfter())continue;
                    source.Access.ObserveRemoteTerminal(source.OwnerCheckpoint,source.Publication);
                }
                bool saved=source.Publication.RetireTerminalMarker(source.TerminalPath,source.Terminal);
                if(source.Publication.MarkerRemoved)source.FinancialReleased=true;
                if(saved)
                {
                    source.Access.ObserveCleanup(source.Publication);
                    if(source.Access.Ready()&&PublishRemoteRecyclerCompleted(source))
                    {
                        ReplyPlaced(source);
                        if(source.Connection.IsDisconnected()||source.Peer.disconnecting)
                        {remoteRecyclerOriginals.Remove(source.Operation);source.Dispose();}
                    }
                }
            }
            catch{/* Metadata retry only. Owner/native packets are never resent. */}
        }
    }
}
