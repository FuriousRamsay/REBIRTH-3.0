using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Observation of a genuine native FromPlayer snapshot. This is deliberately
    // separate from the one-use financial checkpoint source.
    internal sealed class RecyclerAutomaticOwner
    {
        internal RecyclerClient Client;internal SavedOwnerCheckpoint Checkpoint;internal long Sequence;
    }
    private static readonly ConditionalWeakTable<PlayerDataFile,RecyclerAutomaticOwner> recyclerAutomaticOwners=new ConditionalWeakTable<PlayerDataFile,RecyclerAutomaticOwner>();
    private static void ObserveAutomaticRecyclerOwner(PlayerDataFile data,EntityPlayer actor,bool run)
    {
        if(!run||data==null||!ThreadManager.IsMainThread())return;
        recyclerAutomaticOwners.Remove(data);
        var client=recyclerClients.SingleOrDefault(value=>!value.Acknowledged&&ReferenceEquals(value.Source?.Actor,actor));
        if(client?.Source?.OwnerLineageCurrent()!=true)return;
        long sequence=checked(++client.AutomaticSequence);
        if(savingOwner!=null)
        {if(ReferenceEquals(savingOwner.Original,client.Source)&&MatchesCheckpoint(savingOwner,data))savingOwner.NativeSnapshotSequence=sequence;return;}
        if(client.Unknown||client.Owner==null||!client.Owner.TryObserveStableOwner(data,out var checkpoint))return;
        for(int index=client.AutomaticCaptures.Count-1;index>=0;index--)if(!client.AutomaticCaptures[index].TryGetTarget(out _))client.AutomaticCaptures.RemoveAt(index);
        while(client.AutomaticCaptures.Count>=32)
        {if(client.AutomaticCaptures[0].TryGetTarget(out var old))recyclerAutomaticOwners.Remove(old);client.AutomaticCaptures.RemoveAt(0);}
        recyclerAutomaticOwners.Add(data,new RecyclerAutomaticOwner{Client=client,Checkpoint=checkpoint,Sequence=sequence});
        client.AutomaticCaptures.Add(new WeakReference<PlayerDataFile>(data));
    }
    private static bool CaptureAutomaticRecyclerOwner(NetPackagePlayerData packet)
    {
        if(packet?.playerDataFile==null||!recyclerAutomaticOwners.TryGetValue(packet.playerDataFile,out var observed))return false;
        var client=observed.Client;
        if(client.Source?.OwnerLineageCurrent()!=true||client.Unknown||!ReferenceEquals(client.Owner?.PendingCheckpoint,observed.Checkpoint)||
            client.Owner.TryObserveStableOwner(packet.playerDataFile,out var current)!=true||!ReferenceEquals(current,observed.Checkpoint))return false;
        if(observed.Sequence<=client.AutomaticCaptured)return true;
        // The native packet has not entered any send queue yet. Retain that
        // exact object, not a reconstructed PlayerDataFile or new Setup.
        if(packet.inSendQueuesCount!=0)return false;
        packet.RegisterSendQueue();
        var previous=client.AutomaticPacket;
        client.AutomaticPacket=packet;client.AutomaticObservation=observed;client.AutomaticCaptured=observed.Sequence;
        previous?.SendQueueHandled();return true;
    }
    private static bool CanSendFinalAutomaticRecyclerOwner(NetPackagePlayerData packet)
    {
        if(packet?.playerDataFile==null||!recyclerAutomaticOwners.TryGetValue(packet.playerDataFile,out var observed))return false;
        var client=observed.Client;
        return client.ReceiptSaved&&client.AcknowledgementSent&&client.MetadataCurrent()&&client.AutomaticPacket==null&&
            client.AutomaticSent<=client.AutomaticAcknowledged&&observed.Checkpoint.Phase==3&&
            client.Owner.TryObserveStableOwner(packet.playerDataFile,out var current)&&ReferenceEquals(current,observed.Checkpoint);
    }
    private static void SendRecyclerAcknowledgement(RecyclerClient client)
    {
        if(!client.ReceiptSaved||client.AutomaticPacket!=null||client.AutomaticSent>client.AutomaticAcknowledged)return;
        client.AcknowledgementSent=true;
        SendPlacedText(ClientCommand(client,"ack").ToString(SaveOptions.DisableFormatting));
    }
    private static void PumpAutomaticRecyclerOwner(RecyclerClient client)
    {
        if(client.AutomaticSent>client.AutomaticAcknowledged||client.AutomaticPacket==null||client.MetadataCurrent()!=true||client.Unknown||client.Nonce==Guid.Empty)return;
        var observed=client.AutomaticObservation;var current=client.Owner?.PendingCheckpoint;
        if(observed==null||current==null)return;
        if(!ReferenceEquals(current,observed.Checkpoint))
        {
            // The later actual manual FromPlayer/full native saved upload
            // includes this earlier progression snapshot. It is not absence.
            if(current.ObservedSaved()&&current.NativeSnapshotSequence>=observed.Sequence)ReleaseAutomaticRecyclerOwner(client);
            return;
        }
        if(!current.ObservedSaved()||client.Owner.TryObserveStableOwner(client.AutomaticPacket.playerDataFile,out var stable)!=true||!ReferenceEquals(stable,current))return;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var memory))return;
        using(memory)
        {
            byte[] nativeBefore=NativeOwnerImage(client.AutomaticPacket.playerDataFile),nativePacket;
            using(var stream=new RecyclerPdsPacketStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(stream);client.AutomaticPacket.write(writer);writer.Flush();nativePacket=stream.ToArray();}
            if(!nativeBefore.SequenceEqual(NativeOwnerImage(client.AutomaticPacket.playerDataFile)))return;
            var header=ClientCommand(client,"ownerAutosave");header.Add(new XAttribute("phase",current.Phase),new XAttribute("completed",current.Completed),
                new XAttribute("basis",current.FrozenWireDigest),new XAttribute("sequence",observed.Sequence));
            byte[] text=Encoding.UTF8.GetBytes(header.ToString(SaveOptions.DisableFormatting));if(text.Length>8192)return;
            using(var stream=new RecyclerPdsPacketStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write((byte)1);writer.Write(text.Length);writer.Write(text);writer.Write(nativePacket.Length);writer.Write(nativePacket);writer.Flush();
                // The exact captured native snapshot is sent once. Metadata
                // retries query this sequence; they never re-upload old data.
                client.AutomaticSent=observed.Sequence;SendPlacedBytes(stream.ToArray());
            }
        }
    }
    private static void ReleaseAutomaticRecyclerOwner(RecyclerClient client)
    {
        var packet=client.AutomaticPacket;client.AutomaticPacket=null;client.AutomaticObservation=null;
        packet?.SendQueueHandled();
    }
}
