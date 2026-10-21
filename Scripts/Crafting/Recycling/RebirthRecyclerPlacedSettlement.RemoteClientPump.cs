using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static void PumpRecyclerClients(World world)
    {
        foreach(var client in recyclerClients.ToArray())
        {
            if(!ReferenceEquals(client.Source.World,world)){recyclerClients.Remove(client);client.Dispose();continue;}
            try
            {
                if(!client.MetadataCurrent())
                {
                    if(client.SavedTerminalMetadataCurrent()&&UnityEngine.Time.realtimeSinceStartup>=client.NextQuery)
                    {client.NextQuery=UnityEngine.Time.realtimeSinceStartup+1f;SendRecyclerAcknowledgement(client);}
                    continue;
                }
                if(client.Reentry?.Returned==true)TryFinishRecyclerHydrationOpen(client,client.Reentry);
                if(client.UnusedCloseObserved)
                {
                    if(UnityEngine.Time.realtimeSinceStartup>=client.NextQuery)
                    {client.NextQuery=UnityEngine.Time.realtimeSinceStartup+1f;SendPlacedText(UnusedClientCommand(client).ToString(SaveOptions.DisableFormatting));}
                    continue;
                }
                PumpAutomaticRecyclerOwner(client);
                bool query=UnityEngine.Time.realtimeSinceStartup>=client.NextQuery;
                if(query)client.NextQuery=UnityEngine.Time.realtimeSinceStartup+1f;
                if(client.Nonce==Guid.Empty){if(query)SendPrepare(client);continue;}
                if(query)
                {
                    if(client.ReceiptSaved&&client.AutomaticPacket==null&&client.AutomaticSent<=client.AutomaticAcknowledged)SendRecyclerAcknowledgement(client);
                    else SendPlacedText(ClientCommand(client,"query").ToString(SaveOptions.DisableFormatting));
                }
                TryPublishRecyclerClientTerminal(client);
                if(client.Unknown||!client.Live())continue;
                if(client.Owner==null)
                {
                    if(client.Stage<1)continue;
                    startingRecyclerOwner=client;
                    try{client.Owner=client.Source.ClosedOwner!=null&&client.ColdJoined!=null?
                        OwnerPublication.FromActualClosedJoin(client):OwnerPublication.Begin(client.Source);}finally{startingRecyclerOwner=null;}
                    continue;
                }
                var checkpoint=client.Owner.PendingCheckpoint;
                if(checkpoint==null){client.Owner.TrySaveInitialCheckpoint();continue;}
                if(checkpoint!=null&&checkpoint.LocalObservedSaved()&&!checkpoint.ObservedSaved())
                {
                    if(!checkpoint.TryFreezeOwnerPacket())continue;
                    if(!ReferenceEquals(client.SentOwner,checkpoint))
                    {client.SentOwner=checkpoint;client.OwnerPacketSent=false;client.HostSaved=false;SendOwnerHeader(client,checkpoint);continue;}
                    if(client.HostPhase!=checkpoint.Phase||client.HostCompleted!=checkpoint.Completed)
                    {if(query)SendOwnerHeader(client,checkpoint);continue;}
                    if(!client.OwnerPacketSent)
                    {
                        var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",client.Source.Operation.ToString("N")),new XAttribute("world",client.Source.WorldKey),
                            new XElement("before",XElement.Parse(Encoding.UTF8.GetString(client.Source.Image))),new XElement("recyclerOwnerUploadAttempted",checkpoint.CopyLocalSavedDescriptor(),new XElement(client.LastReply)));
                        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),client.Source.Operation.ToString("N")+(client.Generation==Guid.Empty?"":".reentry."+client.Generation.ToString("N"))+".owner-upload."+checkpoint.Phase+"."+checkpoint.Completed+".xml");
                        if(!RebirthWorkstationCompletionPublication.Write(path,attempt)||!XNode.DeepEquals(attempt,RebirthWorkstationCompletionPublication.ReadPrimary(path)))continue;
                        client.OwnerPacketSent=true;SendPlacedOwnerEnvelope(client,checkpoint);
                    }
                    continue;
                }
                if(client.AutomaticPacket!=null||client.AutomaticSent>client.AutomaticAcknowledged)continue;
                if(client.Source.ClosedOwner!=null)
                {
                    if(client.Owner.StationPaid)client.Owner.AdvanceOwner();
                    TryPublishRecyclerClientTerminal(client);continue;
                }
                if(!client.Applied)
                {
                    if(client.Stage<2||!client.Owner.BeforeCheckpointSaved)continue;
                    ApplyRemoteRecyclerPrincipal(client);continue;
                }
                if(!client.NativeSent)
                {
                    if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(client.Source.Station,client.Source.AfterNative,client.Source.AfterOffset))continue;
                    var packet=NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(client.Source.Station,StreamModeWrite.ToServer);
                    client.NativeSent=true;sendingRecyclerStation=packet;
                    try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(packet);}finally{sendingRecyclerStation=null;}
                    continue;
                }
                if(!client.Owner.StationPaid)continue;
                client.Owner.AdvanceOwner();
                TryPublishRecyclerClientTerminal(client);
            }
            catch{/* One-attempt native stages remain retained; only metadata may be retried. */}
        }
    }
    private static void TryPublishRecyclerClientTerminal(RecyclerClient client)
    {
                if(client.Stage>=5&&client.Owner?.OwnerSaved==true&&!client.ReceiptSaved)
                {
                    var terminal=new XElement("workstationCompletionIntent",new XAttribute("operation",client.Source.Operation.ToString("N")),new XAttribute("world",client.Source.WorldKey),
                        new XElement("before",XElement.Parse(Encoding.UTF8.GetString(client.Source.Image))),new XElement("recyclerRemoteClientTerminal",
                            client.Owner.PendingCheckpoint.CopyLocalSavedDescriptor(),new XElement(client.LastReply)));
                    string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),client.Source.Operation.ToString("N")+(client.Generation==Guid.Empty?"":".reentry."+client.Generation.ToString("N"))+".remote-client-terminal.xml");
                    if(RebirthWorkstationCompletionPublication.Write(path,terminal)&&XNode.DeepEquals(terminal,RebirthWorkstationCompletionPublication.ReadPrimary(path)))
                    {
                        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
                        if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var terminalMemory))return;
                        try
                        {
                            byte[] bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                            if(bytes?.LongLength!=length||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,bytes))return;
                            client.TerminalMemory?.Dispose();client.TerminalMemory=terminalMemory;terminalMemory=null;
                            client.Terminal=bytes;client.TerminalPath=path;client.ReceiptSaved=true;SendRecyclerAcknowledgement(client);
                        }
                        finally{terminalMemory?.Dispose();}
                    }
                }
    }
    private static void SendOwnerHeader(RecyclerClient client,SavedOwnerCheckpoint checkpoint)
    {
        var header=ClientCommand(client,"owner");header.Add(new XElement("recyclerOwnerCheckpoint",new XAttribute("operation",client.Source.Operation.ToString("N")),
            new XAttribute("nonce",client.Nonce.ToString("N")),new XAttribute("phase",checkpoint.Phase),new XAttribute("completed",checkpoint.Completed),
            new XAttribute("wireLength",checkpoint.FrozenWireLength),new XAttribute("wireDigest",checkpoint.FrozenWireDigest)));
        SendPlacedText(header.ToString(SaveOptions.DisableFormatting));
    }
    private static void ApplyRemoteRecyclerPrincipal(RecyclerClient client)
    {
        var source=client.Source;
        if(!client.Live()||client.Unknown||client.Applied||client.PrincipalEntered||projectingRecyclerClient!=null||!client.Owner.BeforeCheckpointSaved||
            !source.MatchesOwnerCheckpoint(source.Actor.bag.ItemGrid.items,source.Actor.inventory.ItemGrid.items,0)||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.BeforeNative,source.BeforeOffset))return;
        var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
            new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerRemotePrincipalAttempted",client.Owner.PendingCheckpoint.CopyLocalSavedDescriptor(),new XElement(client.LastReply)));
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+(client.Generation==Guid.Empty?"":".reentry."+client.Generation.ToString("N"))+".remote-principal-attempted.xml");
        if(!client.PrincipalWritingStarted&&(ArtifactNodeExists(path)||ArtifactNodeExists(path+".tmp")||ArtifactNodeExists(path+".bak")))return;
        client.PrincipalWritingStarted=true;
        if(!RebirthWorkstationCompletionPublication.Write(path,attempt)||
            !XNode.DeepEquals(attempt,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
        projectingRecyclerClient=client;
        client.PrincipalEntered=true;
        try
        {
            var station=source.Station;station.SetDisableModifiedCheck(true);
            station.Input=source.Input;station.Output=source.Output;station.Tools=source.Tools;station.Queue=source.Queue;
            if(source.Cycle)station.cycleRecipeQueue();station.ResetTickTime();
            if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.AfterNative,source.AfterOffset)){client.Unknown=true;return;}
            client.Applied=true;station.SetDisableModifiedCheck(false);station.setModified();
            RebirthRecyclerRuntime.Refresh(source.Actor,station);
        }
        catch
        {
            // A complete actual postimage may be uploaded and saved, without
            // repeating any setter. A partial tuple remains unknown.
            if(client.Live()&&source.MatchesOwnerCheckpoint(source.Actor.bag.ItemGrid.items,source.Actor.inventory.ItemGrid.items,0)&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.AfterNative,source.AfterOffset))client.Applied=true;
            else client.Unknown=true;
        }
        finally
        {
            if(source.Current()&&(RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.BeforeNative,source.BeforeOffset)||
                RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.AfterNative,source.AfterOffset)))source.Station.SetDisableModifiedCheck(false);
            projectingRecyclerClient=null;
        }
    }
}
