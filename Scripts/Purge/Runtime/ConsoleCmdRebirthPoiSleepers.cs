using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class ConsoleCmdRebirthPoiSleepers : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => false;
    public override int DefaultPermissionLevel => 0;
    public override string[] getCommands() => new[] { "poisleepers" };
    public override string getDescription() => RebirthPurgeSleeperCommand.Message("Description");
    public override string getHelp() => RebirthPurgeSleeperCommand.Message("Help");
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        try
        {
            EntityPlayer player; ClientInfo client; string error;
            if (!RebirthPurgeSleeperCommand.ResolveRequester(sender, out player, out client, out error))
            { SdtdConsole.Instance.Output("poisleepers: " + error); return; }
            if (args == null || args.Count == 0) { SdtdConsole.Instance.Output(getHelp()); return; }
            string action = args[0].ToLowerInvariant();
            if (action == "status" && args.Count == 1) error = RebirthPurgeSleeperCommand.Status(player);
            else if (action == "stop" && args.Count == 1) error = RebirthPurgeSleeperCommand.Stop(player);
            else if (action == "start" && (args.Count == 1 || args.Count == 2 && args[1].Equals("force", StringComparison.OrdinalIgnoreCase)))
                error = RebirthPurgeSleeperCommand.Start(player, client, args.Count == 2);
            else error = getHelp();
            SdtdConsole.Instance.Output("poisleepers: " + error);
        }
        catch (Exception error) { SdtdConsole.Instance.Output("poisleepers: " + RebirthPurgeSleeperCommand.Message("Refused", error.Message)); }
    }
}

// Session-only requests. Native UpdatePlayerTouched and existing evidence hooks own all actors.
internal static class RebirthPurgeSleeperCommand
{
    private sealed class Room
    {
        internal SleeperVolume Volume;
        internal object Definition;
        internal int NativeId, DefinitionIndex, LoadedIndex, Flags;
        internal Vector3i Minimum, Maximum;
        internal short SpawnMin, SpawnMax;
    }
    private sealed class Job
    {
        internal World World;
        internal EntityPlayer Player;
        internal ClientInfo Client;
        internal RebirthPoiNativeManifest Manifest;
        internal RebirthPoiWorldStore Store;
        internal RebirthPoiWorldBinding Binding;
        internal long Epoch;
        internal Room[] Rooms;
        internal Prefab Author;
        internal object Definitions;
        internal Vector3i Origin, Size;
        internal int Rotation;
        internal string Name;
        internal int Cursor, Requested;
        internal float Next, Deadline;
    }
    internal static string Message(string key, params object[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, Localization.Get("rbPoiSleepers" + key), args);
    private static readonly Dictionary<int, Job> jobs = new Dictionary<int, Job>();
    private static readonly List<Job> work = new List<Job>(16);
    private static int cursor;
    internal static void Reset() { jobs.Clear(); work.Clear(); cursor = 0; }
    private static bool PlayerCurrent(World world, EntityPlayer player, ClientInfo client)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || connection == null || !connection.IsServer ||
            !ReferenceEquals(GameManager.Instance?.World, world) || player == null || !ReferenceEquals(player.world, world) ||
            !world.Entities.dict.TryGetValue(player.entityId, out var actual) || !ReferenceEquals(actual, player)) return false;
        if (client != null) return client.loginDone && client.entityId == player.entityId && ReferenceEquals(connection.Clients.ForEntityId(player.entityId), client);
        return !GameManager.IsDedicatedServer && player is EntityPlayerLocal && world.GetLocalPlayers().Contains((EntityPlayerLocal)player);
    }
    internal static bool ResolveRequester(CommandSenderInfo sender, out EntityPlayer player, out ClientInfo client, out string error)
    {
        var world = GameManager.Instance?.World; client = sender.RemoteClientInfo; player = null;
        if (world != null)
        {
            if (client == null) player = world.GetPrimaryPlayer();
            else if (world.Entities.dict.TryGetValue(client.entityId, out var actual)) player = actual as EntityPlayer;
        }
        error = Message("Auth");
        return PlayerCurrent(world, player, client);
    }
    private static bool Eligible(EntityPlayer player) => player.IsAlive() && player.IsSpawned() && !player.IsSpectator && player.AttachedToEntity == null && !player.IsFlyMode.Value;
    // Include noncombat rooms in the legacy denominator, without a world-volume scan.
    private static bool CaptureRooms(RebirthPoiNativeManifest manifest, out Room[] rooms)
    {
        rooms = null;
        var prefab = manifest.Prefab; var definitions = prefab.prefab.SleeperVolumeList; var loaded = prefab.sleeperVolumes;
        if (!manifest.StillMatches() || definitions == null || definitions.Count == 0 || definitions.Count > 4096 || loaded == null || loaded.Count != definitions.Count) return false;
        var members = new Dictionary<SleeperVolume, int>();
        for (int i = 0; i < loaded.Count; i++)
        {
            if (loaded[i] == null || members.ContainsKey(loaded[i])) return false;
            members.Add(loaded[i], i);
        }
        var result = new Room[definitions.Count];
        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i]; var min = prefab.boundingBoxPosition + definition.startPos; var max = min + definition.size;
            int id = manifest.World.FindSleeperVolume(min, max); if (id < 0) return false;
            var volume = manifest.World.GetSleeperVolume(id);
            if (volume == null || !members.TryGetValue(volume, out var index) || !ReferenceEquals(volume.prefabInstance, prefab) ||
                !volume.BoxMin.Equals(min) || !volume.BoxMax.Equals(max) || volume.flags != definition.flags ||
                volume.spawnCountMin != definition.spawnCountMin || volume.spawnCountMax != definition.spawnCountMax) return false;
            members.Remove(volume);
            result[i] = new Room { Volume = volume, Definition = definition, NativeId = id, DefinitionIndex = i, LoadedIndex = index,
                Minimum = min, Maximum = max, Flags = definition.flags, SpawnMin = definition.spawnCountMin, SpawnMax = definition.spawnCountMax };
        }
        rooms = result; return members.Count == 0;
    }
    // Only examined rooms incur native lookups after admission; never rescan the whole POI per slice.
    private static bool RoomCurrent(Job job, Room room)
    {
        var loaded = job.Manifest.Prefab.sleeperVolumes;
        var definitions = job.Author.SleeperVolumeList;
        if (room.LoadedIndex >= loaded.Count || room.DefinitionIndex >= definitions.Count ||
            !ReferenceEquals(loaded[room.LoadedIndex], room.Volume) || !ReferenceEquals(job.World.GetSleeperVolume(room.NativeId), room.Volume)) return false;
        var definition = definitions[room.DefinitionIndex]; var volume = room.Volume;
        return ReferenceEquals(definition, room.Definition) && ReferenceEquals(volume.prefabInstance, job.Manifest.Prefab) &&
            (job.Origin + definition.startPos).Equals(room.Minimum) && (job.Origin + definition.startPos + definition.size).Equals(room.Maximum) &&
            volume.BoxMin.Equals(room.Minimum) && volume.BoxMax.Equals(room.Maximum) && definition.flags == room.Flags && volume.flags == room.Flags &&
            definition.spawnCountMin == room.SpawnMin && definition.spawnCountMax == room.SpawnMax &&
            volume.spawnCountMin == room.SpawnMin && volume.spawnCountMax == room.SpawnMax;
    }
    private static bool Current(Job job)
    {
        if (!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge || !PlayerCurrent(job.World, job.Player, job.Client) || !Eligible(job.Player) ||
            !ReferenceEquals(job.World.GetPOIAtPosition(job.Player.position), job.Manifest.Prefab) ||
            !ReferenceEquals(job.Manifest.Prefab.prefab, job.Author) || !ReferenceEquals(job.Author.SleeperVolumeList, job.Definitions) ||
            job.Author.SleeperVolumeList.Count != job.Rooms.Length || job.Manifest.Prefab.sleeperVolumes == null || job.Manifest.Prefab.sleeperVolumes.Count != job.Rooms.Length ||
            !job.Manifest.Prefab.boundingBoxPosition.Equals(job.Origin) || !job.Manifest.Prefab.boundingBoxSize.Equals(job.Size) ||
            (int)job.Manifest.Prefab.rotation != job.Rotation || job.Author.PrefabName != job.Name ||
            !job.Binding.IsCurrent || !ReferenceEquals(job.Binding.Scope, job.World) ||
            RebirthPoiNativeEvidence.Instance.IsWithheld(job.Manifest.Identity)) return false;
        RebirthPoiWorldStore store; RebirthPoiClearanceRecord record;
        return RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store) && ReferenceEquals(store, job.Store) &&
            store.Published != null && ReferenceEquals(store.Published.Binding, job.Binding) &&
            store.Published.TryGet(job.Manifest.Identity, out record) && !record.ResetOnly && record.State == RebirthPoiClearanceState.Discovered && record.Epoch == job.Epoch;
    }
    internal static string Start(EntityPlayer player, ClientInfo client, bool force)
    {
        if (!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge) return Message("Mode");
        var world = player.world as World;
        if (!PlayerCurrent(world, player, client) || !Eligible(player)) return Message("Player");
        if (jobs.TryGetValue(player.entityId, out var existing) && ReferenceEquals(existing.Player, player)) return Message("Already");
        if (jobs.Count >= 16) return Message("Limit");
        RebirthPoiNativeManifest manifest; Room[] rooms; RebirthPoiWorldStore store; RebirthPoiClearanceRecord record;
        if (!RebirthPoiNativeManifest.TryResolve(world, world.GetPOIAtPosition(player.position), out manifest) || !CaptureRooms(manifest, out rooms) ||
            !RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store) || store.Published == null ||
            !store.Published.TryGet(manifest.Identity, out record)) return Message("NotReady");
        var job = new Job { World = world, Player = player, Client = client, Manifest = manifest, Rooms = rooms, Store = store,
            Binding = store.Published.Binding, Epoch = record.Epoch, Next = Time.realtimeSinceStartup, Deadline = Time.realtimeSinceStartup + 120f,
            Author = manifest.Prefab.prefab, Definitions = manifest.Prefab.prefab.SleeperVolumeList, Origin = manifest.Prefab.boundingBoxPosition,
            Size = manifest.Prefab.boundingBoxSize, Rotation = (int)manifest.Prefab.rotation, Name = manifest.Prefab.prefab.PrefabName };
        if (!Current(job) || RebirthPoiNativeRestorationConnection.HasPending(manifest.Identity)) return Message("Unavailable");
        int progressed = 0;
        foreach (var room in rooms) if (room.Volume.wasCleared || room.Volume.GetAliveCount() > 0) progressed++;
        if (!force && (long)progressed * 100 < (long)rooms.Length * 85) return Message("Progress", progressed, rooms.Length);
        jobs[player.entityId] = job;
        return Message(force ? "StartedForce" : "Started", manifest.Identity.Prefab, progressed, rooms.Length);
    }
    internal static string Status(EntityPlayer player) => jobs.TryGetValue(player.entityId, out var job) && ReferenceEquals(job.Player, player)
        ? Message("Running", job.Manifest.Identity.Prefab, job.Cursor, job.Rooms.Length, job.Requested) : Message("NoRequest");
    internal static string Stop(EntityPlayer player)
    {
        if (!jobs.TryGetValue(player.entityId, out var job) || !ReferenceEquals(job.Player, player)) return Message("NoRequest");
        jobs.Remove(player.entityId); return Message("Stopped");
    }
    private static void Finish(Job job, string result)
    {
        if (!jobs.TryGetValue(job.Player.entityId, out var current) || !ReferenceEquals(current, job)) return;
        jobs.Remove(job.Player.entityId);
        string message = "poisleepers: " + Message("Finished", result, job.Requested);
        Log.Out(message);
        if (!PlayerCurrent(job.World, job.Player, job.Client)) return;
        if (job.Client != null) job.Client.SendPackage(NetPackageManager.GetPackage<NetPackageConsoleCmdClient>().Setup(message, false));
        else SdtdConsole.Instance.Output(message);
    }
    internal static void Pulse()
    {
        if (jobs.Count == 0) return;
        work.Clear(); work.AddRange(jobs.Values);
        int processed = 0; float now = Time.realtimeSinceStartup;
        for (int n = 0; n < work.Count && processed < 4; n++)
        {
            if (cursor >= work.Count) cursor = 0;
            var job = work[cursor++]; if (now < job.Next) continue;
            processed++; job.Next = now + .25f;
            try
            {
                var rooms = job.Rooms;
                if (!Current(job)) { Finish(job, Message("Changed")); continue; }
                if (now >= job.Deadline) { Finish(job, Message("Budget")); continue; }
                // Legitimate native replacement/publication is temporary work, not lost ownership.
                // Preserve the untouched cursor and deadline; fault/epoch/room guards still apply.
                if (RebirthPoiNativeRestorationConnection.HasPending(job.Manifest.Identity))
                {
                    if (job.Cursor < rooms.Length && !RoomCurrent(job, rooms[job.Cursor])) Finish(job, Message("Changed"));
                    continue;
                }
                int requested = 0, examined = 0;
                while (job.Cursor < rooms.Length && requested < 2 && examined++ < 64)
                {
                    // The previous native call can start restoration within this very batch.
                    if (RebirthPoiNativeRestorationConnection.HasPending(job.Manifest.Identity)) break;
                    var selected = rooms[job.Cursor];
                    if (!RoomCurrent(job, selected)) { Finish(job, Message("Changed")); break; }
                    var room = selected.Volume;
                    if (room.wasCleared || room.isSpawned || room.isSpawning) { job.Cursor++; continue; }
                    if (!Current(job)) { Finish(job, Message("BatchChanged")); break; }
                    room.UpdatePlayerTouched(job.World, job.Player);
                    job.Cursor++; requested++; job.Requested++;
                }
                if (jobs.ContainsKey(job.Player.entityId) && job.Cursor >= rooms.Length) Finish(job, Message("Complete"));
            }
            catch (Exception error) { Finish(job, Message("Cancelled", error.Message)); }
        }
        work.Clear();
    }
}

[Preserve]
public sealed class RebirthPurgeSleeperCommandModApi : IModApi
{
    private static bool update, starting, shutdown, gameShutdown;
    public void InitMod(Mod mod)
    {
        if (!RebirthPurgeReleasePolicy.Enabled) return;
        if (!update) { ModEvents.GameUpdate.RegisterHandler(OnUpdate); update = true; }
        if (!starting) { ModEvents.GameStarting.RegisterHandler(OnStarting); starting = true; }
        if (!shutdown) { ModEvents.WorldShuttingDown.RegisterHandler(OnShutdown); shutdown = true; }
        if (!gameShutdown) { ModEvents.GameShutdown.RegisterHandler(OnGameShutdown); gameShutdown = true; }
    }
    private static void OnUpdate(ref ModEvents.SGameUpdateData data) => RebirthPurgeSleeperCommand.Pulse();
    private static void OnStarting(ref ModEvents.SGameStartingData data) => RebirthPurgeSleeperCommand.Reset();
    private static void OnShutdown(ref ModEvents.SWorldShuttingDownData data) => RebirthPurgeSleeperCommand.Reset();
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) => RebirthPurgeSleeperCommand.Reset();
}
