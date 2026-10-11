using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

// Native direction/authentication supplies authority. Request nonce binds every reply
// to this client's current world session; projection bytes never authorize gameplay.
[Preserve]
public sealed class NetPackageRebirthPoiMapRequest : NetPackage
{
    private int player;
    private Guid request;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    internal NetPackageRebirthPoiMapRequest Setup(int playerId, Guid nonce) { player = playerId; request = nonce; return this; }
    public override void read(PooledBinaryReader reader) { player = reader.ReadInt32(); request = new Guid(reader.ReadBytes(16)); }
    public override void write(PooledBinaryWriter writer) { base.write(writer); writer.Write(player); writer.Write(request.ToByteArray()); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled || world == null || world.IsRemote()
            || Sender == null || !Sender.loginDone || request == Guid.Empty || !ValidEntityIdForSender(player)) return;
        RebirthPoiMapSync.Request(Sender, request);
    }
    public int GetLength() => 24;
}
[Preserve]
public sealed class NetPackageRebirthPoiMapFrame : NetPackage
{
    private byte[] bytes;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    internal NetPackageRebirthPoiMapFrame Setup(RebirthPoiMapFrame frame) { bytes = RebirthPoiMapFrame.Encode(frame); return this; }
    public override void read(PooledBinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 101 || length > RebirthPoiMapFrame.MaxBytes) throw new InvalidDataException("Invalid POI map package.");
        bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException();
    }
    public override void write(PooledBinaryWriter writer) { base.write(writer); writer.Write(bytes.Length); writer.Write(bytes); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled || connection == null || connection.IsServer
            || world == null || !world.IsRemote() || !ReferenceEquals(GameManager.Instance.World, world)) return;
        RebirthPoiMapFrame frame;
        if (RebirthPoiMapFrame.TryDecode(bytes, out frame)) RebirthPoiMapSync.Receive(world, frame);
    }
    public int GetLength() => 8 + (bytes == null ? 0 : bytes.Length);
}
internal static class RebirthPoiMapSync
{
    private sealed class Peer
    {
        internal ClientInfo Client;
        internal Guid Request;
        internal double NextRequest;
        internal long Sent,ObjectivesSent;
        internal bool ObjectiveTurn=true;
        internal Transfer Sending;
    }
    private sealed class Transfer
    {
        internal readonly RebirthPoiMapRecord[] Records;
        internal readonly long Sequence, Previous;
        internal readonly bool Full;
        internal readonly Guid Id = Guid.NewGuid();
        internal int Page;
        internal int Pages => Math.Max(1, (Records.Length + RebirthPoiMapFrame.PageRecords - 1) / RebirthPoiMapFrame.PageRecords);
        internal Transfer(RebirthPoiMapRecord[] records, long sequence, long previous, bool full)
        { Records = records; Sequence = sequence; Previous = previous; Full = full; }
    }
    private static readonly Dictionary<ClientInfo, Peer> peers = new Dictionary<ClientInfo, Peer>();
    private static readonly Queue<Peer> roundRobin = new Queue<Peer>();
    private static readonly Queue<Tuple<long, RebirthPoiMapRecord[]>> deltas = new Queue<Tuple<long, RebirthPoiMapRecord[]>>();
    private static Dictionary<string, RebirthPoiMapRecord> map = new Dictionary<string, RebirthPoiMapRecord>(StringComparer.Ordinal);
    private static Dictionary<string, RebirthPoiMapRecord> building;
    private static List<RebirthPoiMapRecord> changedRecords;
    private static RebirthPoiMapRecord[] fullRecords = new RebirthPoiMapRecord[0];
    private static IEnumerator<RebirthPoiClearanceRecord> scan;
    private static RebirthPoiWorldSnapshot original, observed;
    private static Guid worldId, session;
    private static long sequence;
    private static bool overflow;
    private static object objectiveSource;
    private static long objectiveSequence,objectiveRevision,objectiveGeneration;
    private static bool objectiveKnown;
    private static int objectiveTarget=75;
    private static RebirthPurgeObjectiveFrame.Biome[] objectiveBiomes=new RebirthPurgeObjectiveFrame.Biome[0];
    private static RebirthPurgeObjectiveClient objectiveClient;
    internal static RebirthPurgeObjectiveFrame LocalObjectives => objectiveClient?.Published;
    private static World clientWorld;
    private static Guid clientRequest;
    private static RebirthPoiMapReceiver receiver;
    private static double nextClientRequest, nextPoll, nextSend;
    private static bool clientNeedsSnapshot;
    internal static IReadOnlyDictionary<string, RebirthPoiMapRecord> LocalMap =>
        clientWorld != null ? receiver?.Published : map;
    internal static long LocalRevision => clientWorld != null ? receiver?.Sequence ?? 0 : sequence;
    private static double Now => Time.realtimeSinceStartup;
    internal static void Reset()
    {
        scan?.Dispose(); scan = null; building = null; original = null; observed = null;
        changedRecords = null; fullRecords = new RebirthPoiMapRecord[0];
        peers.Clear(); roundRobin.Clear(); deltas.Clear(); map = new Dictionary<string, RebirthPoiMapRecord>(StringComparer.Ordinal); sequence = 0;
        worldId = Guid.Empty; session = Guid.NewGuid(); overflow = false;
        clientWorld = null; clientRequest = Guid.Empty; receiver = null; nextClientRequest = 0; nextPoll = 0; nextSend = 0; clientNeedsSnapshot = true;
        objectiveSource=null;objectiveSequence=0;objectiveRevision=0;objectiveGeneration=0;objectiveKnown=false;objectiveBiomes=new RebirthPurgeObjectiveFrame.Biome[0];objectiveClient=null;
        RebirthPoiMapPresentation.Reset();
    }
    internal static void Request(ClientInfo client, Guid request)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || !connection.Clients.List.Contains(client)) return;
        Peer peer;
        if (peers.TryGetValue(client, out peer))
        {
            if (Now < peer.NextRequest) return;
            peer.Request = request; peer.Sent = 0; peer.ObjectivesSent=0; peer.ObjectiveTurn=true; peer.Sending = null; peer.NextRequest = Now + 5;
        }
        else
        {
            if (peers.Count >= 256) return;
            peer = new Peer { Client = client, Request = request, NextRequest = Now + 5 };
            peers.Add(client, peer); roundRobin.Enqueue(peer);
        }
    }
    internal static void Receive(World world, RebirthPoiMapFrame frame)
    {
        if (!ReferenceEquals(world, clientWorld) || receiver == null) return;
        var result = receiver.Accept(world, frame);
        // Only new staged progress extends the transfer timeout. Replayed pages must
        // neither start a needless resync after publication nor postpone a stalled transfer.
        if (result == RebirthPoiMapReceive.Accepted) { clientNeedsSnapshot = true; nextClientRequest = Now + 30; }
        if (result == RebirthPoiMapReceive.Published) { clientNeedsSnapshot = false; objectiveClient?.Publish(receiver.PublishedWorld,receiver.PublishedSession); }
        if (result == RebirthPoiMapReceive.Resync) { clientNeedsSnapshot = true; nextClientRequest = Math.Min(nextClientRequest, Now + 1); }
    }
    internal static void ReceiveObjectives(World world,RebirthPurgeObjectiveFrame frame)
    {
        if(!ReferenceEquals(world,clientWorld) || receiver==null || objectiveClient==null)return;
        objectiveClient.Accept(frame,receiver.PublishedWorld,receiver.PublishedSession);
    }
    private static void ObserveObjectives(RebirthPoiWorldSnapshot snapshot)
    {
        if(!RebirthSandboxOptionManager.Current.IsPurge)return;
        var progress=RebirthPurgeObjectiveProgress.Instance;
        bool known=progress.Published!=null && progress.WorldId==snapshot.WorldId && progress.Revision==snapshot.Revision;
        object source=known?(object)progress.Published:null;
        long generation=RebirthPurgePoiCensus.Instance.Generation;
        if(objectiveSequence>0 && ReferenceEquals(source,objectiveSource) && objectiveRevision==snapshot.Revision && objectiveGeneration==generation && objectiveTarget==progress.TargetPercentage)return;
        objectiveSequence++;objectiveSource=source;objectiveKnown=known;objectiveRevision=snapshot.Revision;objectiveGeneration=generation;objectiveTarget=progress.TargetPercentage;
        try { objectiveBiomes=known?progress.Published.Values.Select(b=>new RebirthPurgeObjectiveFrame.Biome(b.Biome,b.Eligible,b.Discovered,b.Cleared,new Dictionary<int,int>(b.EligibleByTier),new Dictionary<int,int>(b.ClearedByTier))).ToArray():new RebirthPurgeObjectiveFrame.Biome[0];
        new RebirthPurgeObjectiveFrame(Guid.NewGuid(),worldId,session,objectiveSequence,objectiveRevision,objectiveGeneration,objectiveKnown,objectiveBiomes,objectiveTarget); }
        catch(ArgumentException) { objectiveBiomes=new RebirthPurgeObjectiveFrame.Biome[0];objectiveKnown=false;Log.Warning("[RebirthPurge] Objective summary exceeds validated bounds; displaying unknown."); }
    }
    internal static void Pulse()
    {
        if (!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled) return;
        var game = GameManager.Instance; var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (game == null || game.World == null || connection == null || game.IsStartingGame) return;
        if (!connection.IsServer)
        {
            var player = game.World.GetPrimaryPlayer(); if (player == null) return;
            if (!ReferenceEquals(clientWorld, game.World))
            {
                clientWorld = game.World; clientRequest = Guid.NewGuid();
                receiver = new RebirthPoiMapReceiver(clientWorld, clientRequest); objectiveClient=new RebirthPurgeObjectiveClient(clientRequest); nextClientRequest = 0; clientNeedsSnapshot = true;
            }
            if (clientNeedsSnapshot && Now >= nextClientRequest)
            {
                // A fresh nonce makes late responses from earlier resyncs harmless.
                clientRequest = Guid.NewGuid(); receiver = new RebirthPoiMapReceiver(clientWorld, clientRequest); objectiveClient=new RebirthPurgeObjectiveClient(clientRequest);
                connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPoiMapRequest>().Setup(player.entityId, clientRequest));
                nextClientRequest = Now + 30;
            }
            return;
        }
        RebirthPoiWorldStore store;
        if (!RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store)) return;
        var snapshot = store.Published; if (snapshot == null) return;
        if (worldId != snapshot.WorldId)
        {
            // Initial binding retains authenticated join requests received while the ledger opened.
            // Switching an already bound world still discards every old peer/session.
            if (worldId != Guid.Empty) Reset();
            worldId = snapshot.WorldId;
            if (session == Guid.Empty) session = Guid.NewGuid();
        }
        ObserveObjectives(snapshot);
        if (scan == null && !overflow && !ReferenceEquals(snapshot, observed) && Now >= nextPoll)
        {
            nextPoll = Now + .5; original = snapshot;
            building = new Dictionary<string, RebirthPoiMapRecord>(StringComparer.Ordinal);
            changedRecords = new List<RebirthPoiMapRecord>();
            scan = snapshot.Shards.Values.SelectMany(s => s.Records.Values).GetEnumerator();
        }
        if (scan != null)
        {
            for (int budget = 0; budget < 128; budget++)
            {
                if (!original.Binding.IsCurrent) { scan.Dispose(); scan = null; building = null; break; }
                if (!scan.MoveNext()) { CompleteProjection(); break; }
                var record = scan.Current;
                if (building.Count >= RebirthPoiMapFrame.MaxRecords)
                {
                    overflow = true; scan.Dispose(); scan = null; building = null;
                    Log.Warning("[RebirthPurge] Map projection exceeds its explicit capacity; projection withheld.");
                    break;
                }
                var projected = new RebirthPoiMapRecord(record.Identity, record.Epoch, record.ResetOnly?RebirthPoiClearanceState.ResetPending:record.State);
                building.Add(record.Identity.Key, projected);
                RebirthPoiMapRecord previous; if (!map.TryGetValue(record.Identity.Key, out previous) || !projected.Same(previous)) changedRecords.Add(projected);
            }
        }
        if (Now >= nextSend) { nextSend = Now + .1; SendPages(connection); }
    }
    private static void CompleteProjection()
    {
        scan.Dispose(); scan = null; observed = original;
        var changed = changedRecords.ToArray(); changedRecords = null;
        if (sequence == 0 || changed.Length > 0)
        {
            sequence++;
            map = building; fullRecords = map.Values.ToArray();
            deltas.Enqueue(Tuple.Create(sequence, changed));
            while (deltas.Count > 64) deltas.Dequeue();
        }
        building = null; original = null;
    }
    private static Transfer Plan(Peer peer)
    {
        if (sequence == 0 || peer.Sent == sequence) return null;
        if (peer.Sent == 0) return new Transfer(fullRecords, sequence, 0, true);
        var needed = deltas.Where(d => d.Item1 > peer.Sent).ToArray();
        if (needed.Length == 0 || needed[0].Item1 != peer.Sent + 1)
            return new Transfer(fullRecords, sequence, peer.Sent, true);
        var changes = new Dictionary<string, RebirthPoiMapRecord>(StringComparer.Ordinal);
        foreach (var delta in needed) foreach (var record in delta.Item2) changes[record.Identity.Key] = record;
        return new Transfer(changes.Values.ToArray(), sequence, peer.Sent, false);
    }
    private static void SendPages(ConnectionManager connection)
    {
        // Global budget, rather than four frames per player.
        for (int budget = 0; budget < 4 && roundRobin.Count > 0; budget++)
        {
            var peer = roundRobin.Dequeue();
            if (!connection.Clients.List.Contains(peer.Client) || !peer.Client.loginDone) { peers.Remove(peer.Client); continue; }
            roundRobin.Enqueue(peer);
            var transfer = peer.Sending ?? (peer.Sending = Plan(peer));
            bool objectivesPending=RebirthSandboxOptionManager.Current.IsPurge && objectiveSequence>peer.ObjectivesSent;
            if(objectivesPending && (peer.ObjectiveTurn || transfer==null))
            {
                peer.Client.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeObjectives>().Setup(new RebirthPurgeObjectiveFrame(peer.Request,worldId,session,objectiveSequence,objectiveRevision,objectiveGeneration,objectiveKnown,objectiveBiomes,objectiveTarget)));
                peer.ObjectivesSent=objectiveSequence;peer.ObjectiveTurn=false;continue;
            }
            if(transfer==null)continue;
            peer.ObjectiveTurn=true;
            var records = transfer.Records.Skip(transfer.Page * RebirthPoiMapFrame.PageRecords).Take(RebirthPoiMapFrame.PageRecords).ToArray();
            peer.Client.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPoiMapFrame>().Setup(
                new RebirthPoiMapFrame(peer.Request, worldId, session, transfer.Id, transfer.Sequence, transfer.Previous,
                    transfer.Full, transfer.Page, transfer.Pages, transfer.Records.Length, records)));
            if (++transfer.Page == transfer.Pages) { peer.Sent = transfer.Sequence; peer.Sending = null; }
        }
    }
}

