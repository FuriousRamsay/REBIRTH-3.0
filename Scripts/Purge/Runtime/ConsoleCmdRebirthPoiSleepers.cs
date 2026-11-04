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
    public override string getHelp() => RebirthPurgeSleeperCommand.Message("Help")+"\npoisleepers audit - write a read-only room/actor diagnostic snapshot";
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        try
        {
            EntityPlayer player; ClientInfo client; string error;
            if (!RebirthPurgeSleeperCommand.ResolveRequester(sender, out player, out client, out error))
            { SdtdConsole.Instance.Output("poisleepers: " + error); return; }
            if (args == null || args.Count == 0) { SdtdConsole.Instance.Output(getHelp()); return; }
            string action = args[0].ToLowerInvariant();
            if (action == "audit" && args.Count == 1)
            { RebirthPurgeSleeperAudit.Capture(player,"manual-audit");error="room/actor diagnostic snapshot written to log"; }
            else if (action == "status" && args.Count == 1)
            { RebirthPurgeSleeperAudit.Capture(player,"command-status");error=RebirthPurgeSleeperCommand.Status(player); }
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
        internal int NativeId, DefinitionIndex, Flags;
        internal bool Done, Requested;
        internal readonly HashSet<int> Activated=new HashSet<int>();
        internal Vector3i Minimum, Maximum;
        internal short SpawnMin, SpawnMax;
    }
    private sealed class Job
    {
        internal World World;
        internal EntityPlayer Player;
        internal ClientInfo Client;
        internal RebirthPoiNativeManifest.RoomCertificate Certificate;
        internal PrefabInstance Prefab=>Certificate.Prefab;
        internal Room[] Rooms;
        internal Prefab Author;
        internal Vector3i Origin;
        internal int Cursor, Requested, Scan;
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
    private static bool CaptureRooms(RebirthPoiNativeManifest.RoomCertificate certificate, out Room[] rooms)
    {
        rooms=null;
        if(!certificate.AuthoredCurrent())return false;
        var prefab=certificate.Prefab;var definitions=prefab.prefab.SleeperVolumeList;
        if(definitions==null||definitions.Count==0||definitions.Count>4096)return false;
        rooms=new Room[definitions.Count];
        for(int i=0;i<definitions.Count;i++)
        {
            var definition=definitions[i];var min=prefab.boundingBoxPosition+definition.startPos;
            rooms[i]=new Room{Definition=definition,DefinitionIndex=i,Minimum=min,Maximum=min+definition.size,
                Flags=definition.flags,SpawnMin=definition.spawnCountMin,SpawnMax=definition.spawnCountMax};
        }
        return true;
    }
    // Only examined rooms incur native lookups after admission; never rescan the whole POI per slice.
    private static bool RoomCurrent(Job job, Room room)
    {
        // Each room can register independently. Never require every authored room,
        // a native loaded-list count match, or a published clearance record.
        var definitions=job.Author.SleeperVolumeList;
        if(room.DefinitionIndex>=definitions.Count)return false;
        var definition=definitions[room.DefinitionIndex];
        int id=job.World.FindSleeperVolume(room.Minimum,room.Maximum);
        if(id<0)return false;
        var volume=job.World.GetSleeperVolume(id);
        if(volume==null||job.Prefab.sleeperVolumes==null||!job.Prefab.sleeperVolumes.Contains(volume))return false;
        if(!ReferenceEquals(definition,room.Definition)||!ReferenceEquals(volume.prefabInstance,job.Prefab)||
            !(job.Origin+definition.startPos).Equals(room.Minimum)||!(job.Origin+definition.startPos+definition.size).Equals(room.Maximum)||
            !volume.BoxMin.Equals(room.Minimum)||!volume.BoxMax.Equals(room.Maximum)||definition.flags!=room.Flags||!RebirthPoiSleeperShapePolicy.FlagsMatch(volume.flags,room.Flags)||
            definition.spawnCountMin!=room.SpawnMin||definition.spawnCountMax!=room.SpawnMax||
            volume.spawnCountMin!=room.SpawnMin||volume.spawnCountMax!=room.SpawnMax)return false;
        if(room.Volume!=null&&!ReferenceEquals(room.Volume,volume))return false;
        room.Volume=volume;room.NativeId=id;return true;
    }
    private static bool Current(Job job)
    {
        return RebirthPurgeReleasePolicy.Enabled&&RebirthSandboxOptionManager.Current.IsPurge&&
            PlayerCurrent(job.World,job.Player,job.Client)&&Eligible(job.Player)&&
            ReferenceEquals(RebirthPurgeScannerPoi.Resolve(job.World,job.Player.position),job.Prefab)&&
            job.Certificate.AuthoredCurrent();
    }
    internal static bool IsTracking(World world, EntityPlayer player, PrefabInstance prefab)
        => player != null && jobs.TryGetValue(player.entityId, out var job) &&
            ReferenceEquals(job.World, world) && ReferenceEquals(job.Player, player) &&
            ReferenceEquals(job.Prefab, prefab) && Current(job);
    private static bool Purged(Job job)
    {
        RebirthPoiWorldStore store;RebirthPoiClearanceRecord record;
        return RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store)&&store.Published!=null&&
            store.Published.TryGet(job.Certificate.Identity,out record)&&record.State==RebirthPoiClearanceState.Cleared;
    }
    internal static string Start(EntityPlayer player, ClientInfo client, bool force)
    {
        RebirthPurgeSleeperAudit.Capture(player,"command-start");
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge)return Message("Mode");
        var world=player.world as World;
        if(!PlayerCurrent(world,player,client)||!Eligible(player))return Message("Player");
        if(jobs.TryGetValue(player.entityId,out var existing)&&ReferenceEquals(existing.Player,player))return Message("Already");
        if(jobs.Count>=16)return Message("Limit");
        if(!RebirthPoiNativeManifest.RoomCertificate.TryCapture(world,RebirthPurgeScannerPoi.Resolve(world,player.position),out var certificate)||
            !certificate.BindIdentity()||!CaptureRooms(certificate,out var rooms))return Message("NotReady");
        var job=new Job{World=world,Player=player,Client=client,Certificate=certificate,Rooms=rooms,
            Next=Time.realtimeSinceStartup,Deadline=Time.realtimeSinceStartup+120f,Author=certificate.Prefab.prefab,
            Origin=certificate.Prefab.boundingBoxPosition};
        if(!Current(job)||RebirthPoiNativeRestorationConnection.HasPending(certificate.Identity))return Message("Unavailable");
        int progressed=0,cleared=0,spawned=0,spawning=0,alive=0;
        foreach(var room in rooms)if(RoomCurrent(job,room))
        {
            if(room.Volume.wasCleared)cleared++;
            if(room.Volume.isSpawned)spawned++;
            if(room.Volume.isSpawning)spawning++;
            int living=room.Volume.GetAliveCount();alive+=living;
            if(room.Volume.wasCleared||living>0)progressed++;
        }
        if(!force&&(long)progressed*100<(long)rooms.Length*85)return Message("Progress",progressed,rooms.Length);
        jobs[player.entityId]=job;
        RebirthPurgeScanner.SetCommandMarkers(player,certificate.Identity,true);
        Log.Out("[poisleepers] start player="+player.entityId+" poi="+certificate.Identity.Prefab+
            " rooms="+rooms.Length+" cleared="+cleared+" spawned="+spawned+" spawning="+spawning+" nativePlannedPlusRespawn="+alive+" force="+force);
        return Message(force?"StartedForce":"Started",certificate.Identity.Prefab,progressed,rooms.Length);
    }
    internal static string Status(EntityPlayer player) => jobs.TryGetValue(player.entityId, out var job) && ReferenceEquals(job.Player, player)
        ? Message("Running", job.Certificate.Identity.Prefab, job.Cursor, job.Rooms.Length, job.Requested) : Message("NoRequest");
    internal static string Stop(EntityPlayer player)
    {
        bool markers=RebirthPurgeScanner.SetCommandMarkers(player,null,false);
        if (!jobs.TryGetValue(player.entityId, out var job) || !ReferenceEquals(job.Player, player)) return Message(markers?"Stopped":"NoRequest");
        jobs.Remove(player.entityId); return Message("Stopped");
    }
    private static void Finish(Job job, string result)
    {
        if (!jobs.TryGetValue(job.Player.entityId, out var current) || !ReferenceEquals(current, job)) return;
        jobs.Remove(job.Player.entityId);
        RebirthPurgeSleeperAudit.Capture(job.Player,"command-finish:"+result);
        string message = "poisleepers: " + Message("Finished", result, job.Requested);
        Log.Out(message);
        if (!PlayerCurrent(job.World, job.Player, job.Client)) return;
        if (job.Client != null) job.Client.SendPackage(NetPackageManager.GetPackage<NetPackageConsoleCmdClient>().Setup(message, false));
        else SdtdConsole.Instance.Output(message);
    }
    private static bool ActivateLoadedSleepers(Job job, Room selected)
    {
        var room=selected.Volume;
        if(room.isSpawning||room.pendingSpawnOps.Any(h=>h==null||!h.IsCompleted||h.onComplete!=null))return false;
        bool ready=true;
        foreach(var member in room.respawnMap)
        {
            if(!RebirthPoiCombatantPolicy.IsRequired(member.Value.className))continue;
            var actor=job.World.GetEntity(member.Key) as EntityAlive;
            if(actor==null){ready=false;continue;}
            if(!actor.IsAlive())continue;
            if(!actor.IsSleeper){ready=false;continue;}
            // UpdatePlayerTouched is a spawn request and returns immediately for
            // already-spawned rooms. Wake their actual participants explicitly.
            if(!selected.Activated.Add(actor.entityId))continue;
            actor.Buffs.SetCustomVar("$sleeperActivated",2,true);
            actor.SetSleeperActive();
            actor.ConditionalTriggerSleeperWakeUp();
            actor.SetAttackTarget(job.Player,60000);
            Log.Out("[poisleepers] activated actor="+actor.entityId+" class="+member.Value.className+
                " player="+job.Player.entityId+" position="+actor.position+" room="+room.BoxMin+
                " sleeping="+actor.IsSleeping+" passive="+actor.IsSleeperPassive+
                " target="+(actor.GetAttackTarget()?.entityId??-1)+" targetTicks="+actor.attackTargetTime);
        }
        return ready;
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
                if(Purged(job)){Finish(job,Message("Purged"));continue;}
                if (now >= job.Deadline) { Finish(job, Message("Budget")); continue; }
                if(RebirthPoiNativeRestorationConnection.HasPending(job.Certificate.Identity))continue;
                int requested=0,examined=0;
                while(job.Cursor<rooms.Length&&requested<2&&examined<Math.Min(64,rooms.Length))
                {
                    if(RebirthPoiNativeRestorationConnection.HasPending(job.Certificate.Identity))break;
                    var selected=rooms[job.Scan++%rooms.Length];examined++;
                    if(selected.Done)continue;
                    // Missing streamed registration waits while other available rooms proceed.
                    if(!RoomCurrent(job,selected))continue;
                    var room=selected.Volume;
                    if(room.wasCleared)
                    {selected.Done=true;job.Cursor++;continue;}
                    if(!Current(job)){Finish(job,Message("BatchChanged"));break;}
                    requested++;
                    if(!selected.Requested)
                    {
                        room.UpdatePlayerTouched(job.World,job.Player);
                        selected.Requested=true;job.Requested++;
                    }
                    room.Tick(job.World);
                    if(!ActivateLoadedSleepers(job,selected))continue;
                    selected.Done=true;job.Cursor++;
                }
                if (jobs.ContainsKey(job.Player.entityId) && job.Cursor >= rooms.Length)
                    Finish(job,Message("Complete"));
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
