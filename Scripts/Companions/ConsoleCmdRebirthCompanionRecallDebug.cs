using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Consolidated server-side diagnostic for the manual companion recall shortcut.
/// It is deliberately read-only apart from enabling/disabling trace collection.
/// </summary>
public static class RebirthCompanionRecallDebug
{
    private sealed class DogSample
    {
        public int EntityId;
        public bool HasPrevious;
        public Vector3 PreviousPosition;
        public float PreviousYaw;
        public int PreviousHealth;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<string, DogSample> Samples = new Dictionary<string, DogSample>(StringComparer.OrdinalIgnoreCase);
    private static float traceUntil = -1f;
    private static float recallSampleUntil = -1f;
    private static float nextRecallSample = -1f;
    private static int ownerEntityId = -1;
    private static string ownerId = string.Empty;
    private static int recallBatch;

    public static bool TraceEnabled => Time.realtimeSinceStartup <= traceUntil;

    public static void BeginTrace(EntityPlayer player, float seconds)
    {
        ownerEntityId = player != null ? player.entityId : -1;
        ownerId = string.Empty;
        if (player != null) RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId);
        traceUntil = Time.realtimeSinceStartup + Mathf.Clamp(seconds, 5f, 600f);
        recallSampleUntil = -1f;
        nextRecallSample = -1f;
        recallBatch = 0;
        lock (Sync) Samples.Clear();
        RebirthDogExistenceDebug.BeginTrace(player, seconds);
        Log.Out("[REBIRTH CompanionRecall] TRACE_ON ownerEntity=" + ownerEntityId +
            " ownerId='" + (ownerId ?? string.Empty) + "' seconds=" +
            Mathf.Clamp(seconds, 5f, 600f).ToString("0", CultureInfo.InvariantCulture));
    }

    public static void StopTrace()
    {
        traceUntil = -1f;
        recallSampleUntil = -1f;
        nextRecallSample = -1f;
        ownerEntityId = -1;
        ownerId = string.Empty;
        lock (Sync) Samples.Clear();
        RebirthDogExistenceDebug.StopTrace();
        Log.Out("[REBIRTH CompanionRecall] TRACE_OFF");
    }

    public static void BeginRecallBatch(EntityPlayer player)
    {
        if (!IsTraceOwner(player)) return;
        recallBatch++;
        recallSampleUntil = Time.realtimeSinceStartup + 15f;
        nextRecallSample = Time.realtimeSinceStartup;
        lock (Sync) Samples.Clear();
        Log.Out("[REBIRTH CompanionRecall] RECALL_BEGIN batch=" + recallBatch +
            " playerEntity=" + player.entityId + " playerPos=" + F(player.position) +
            " playerYaw=" + N(player.rotation.y));
    }

    public static void EndRecallBatch(EntityPlayer player)
    {
        if (!IsTraceOwner(player)) return;
        Log.Out("[REBIRTH CompanionRecall] RECALL_DISPATCH_COMPLETE batch=" + recallBatch +
            " playerPos=" + F(player.position) +
            " sampleWindowSeconds=15");
    }

    public static void TraceTeleport(string stage, EntityAlive entity, EntityPlayer player)
    {
        if (!TraceEnabled || entity == null || !IsTraceOwner(player)) return;
        string stable = ResolveStable(entity);
        RebirthNpcPersistentRecordView record = ResolveRecord(stable);
        Log.Out("[REBIRTH CompanionRecall] " + (stage ?? "TELEPORT") +
            " batch=" + recallBatch +
            " type=" + entity.GetType().Name +
            " entity=" + entity.entityId +
            " stable=" + stable +
            " generation=" + Generation(record) +
            " hp=" + entity.Health + "/" + entity.GetMaxHealth() +
            " pos=" + F(entity.position) +
            " ownerPos=" + (player != null ? F(player.position) : "<none>") +
            " delta=" + (player != null ? F(entity.position - player.position) : "<none>") +
            " yaw=" + N(entity.rotation.y) +
            " motion=" + F(entity.motion) +
            " chunk=" + Chunk(entity));
    }

    public static void TraceUnloadedDogRecovery(RebirthNpcPersistentRecordView record, bool queued, Vector3 recoveryPosition)
    {
        if (!TraceEnabled || record == null || record.Identity == null) return;
        Log.Out("[REBIRTH CompanionRecall] UNLOADED_DOG_RECOVERY batch=" + recallBatch +
            " stable=" + record.Identity.StableNpcId +
            " generation=" + Generation(record) +
            " queued=" + queued +
            " persistedHp=" + (record.Vitals != null ? record.Vitals.CurrentHealth.ToString(CultureInfo.InvariantCulture) : "<none>") +
            " persistedPos=" + (record.Transform != null ? F(record.Transform.WorldPosition) : "<none>") +
            " physicalRecoveryPos=" + F(recoveryPosition));
    }

    public static void TraceGroundRecovery(EntityRebirthDogCompanion dog, Vector3 before, Vector3 safe)
    {
        if (!OwnsTraceDog(dog)) return;
        Log.Warning("[REBIRTH CompanionRecall] GROUND_RECOVERY entity=" + dog.entityId +
            " stable=" + ResolveStable(dog) +
            " before=" + F(before) + " restored=" + F(safe) +
            " rise=" + N(safe.y - before.y) +
            " chunk=" + Chunk(dog));
    }

    public static void TraceDogLifecycle(string stage, EntityRebirthDogCompanion dog)
    {
        if (!TraceEnabled || dog == null) return;
        string stable = ResolveStable(dog);
        RebirthNpcPersistentRecordView record = ResolveRecord(stable);
        bool ownerMatch = OwnsTraceDog(dog) || RecordOwnedByTrace(record);
        if (!ownerMatch) return;
        Log.Out("[REBIRTH CompanionRecall] LIFECYCLE_" + (stage ?? "UNKNOWN") +
            " entity=" + dog.entityId +
            " stable=" + stable +
            " generation=" + Generation(record) +
            " hp=" + dog.Health + "/" + dog.GetMaxHealth() +
            " pos=" + F(dog.position) +
            " yaw=" + N(dog.rotation.y) +
            " chunk=" + Chunk(dog));
    }

    public static void TraceGhostReconstruction(RebirthNpcStableId stableId, uint oldGeneration,
        uint newGeneration, int persistedHealth, EntityRebirthDogCompanion created, string trigger)
    {
        if (!TraceEnabled || created == null) return;
        RebirthNpcPersistentRecordView record;
        RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record);
        if (!OwnsTraceDog(created) && !RecordOwnedByTrace(record)) return;
        Log.Warning("[REBIRTH CompanionRecall] GHOST_RECONSTRUCTED stable=" + stableId +
            " oldGeneration=" + oldGeneration + " newGeneration=" + newGeneration +
            " newEntity=" + created.entityId +
            " persistedHp=" + persistedHealth + " restoredHp=" + created.Health +
            " pos=" + F(created.position) + " trigger='" + (trigger ?? string.Empty) + "'");
    }

    public static void TraceDogDamage(EntityRebirthDogCompanion dog, DamageSource source,
        int requestedStrength, int healthBefore, int healthAfter, int result)
    {
        if (!OwnsTraceDog(dog)) return;
        Log.Warning("[REBIRTH CompanionRecall] DAMAGE entity=" + dog.entityId +
            " stable=" + ResolveStable(dog) +
            " requested=" + requestedStrength +
            " result=" + result +
            " hpBefore=" + healthBefore + " hpAfter=" + healthAfter +
            " source='" + source.ToString() + "'" +
            " pos=" + F(dog.position) + " motion=" + F(dog.motion));
    }

    /// <summary>Called from the dog live update; samples all owned dogs only for 15s after a recall.</summary>
    public static void TickDog(EntityRebirthDogCompanion dog)
    {
        if (!OwnsTraceDog(dog) || Time.realtimeSinceStartup > recallSampleUntil) return;
        if (Time.realtimeSinceStartup < nextRecallSample) return;

        // One dog reaches this first each frame; open the next global 0.5s sampling slot.
        nextRecallSample = Time.realtimeSinceStartup + 0.5f;
        SampleAllOwnedDogs(dog.world);
    }

    private static void SampleAllOwnedDogs(World world)
    {
        if (world == null || world.Entities == null || world.Entities.list == null) return;
        List<Entity> entities = world.Entities.list;
        for (int i = 0; i < entities.Count; i++)
        {
            EntityRebirthDogCompanion dog = entities[i] as EntityRebirthDogCompanion;
            if (!OwnsTraceDog(dog)) continue;

            string stable = ResolveStable(dog);
            DogSample previous;
            lock (Sync)
            {
                if (!Samples.TryGetValue(stable, out previous))
                {
                    previous = new DogSample();
                    Samples[stable] = previous;
                }
            }

            float yawStep = previous.HasPrevious ? Mathf.DeltaAngle(previous.PreviousYaw, dog.rotation.y) : 0f;
            float moved = previous.HasPrevious ? Vector3.Distance(previous.PreviousPosition, dog.position) : 0f;
            int hpDelta = previous.HasPrevious ? dog.Health - previous.PreviousHealth : 0;
            bool entityChanged = previous.HasPrevious && previous.EntityId != dog.entityId;
            RebirthNpcPersistentRecordView record = ResolveRecord(stable);
            EntityPlayer owner = world.GetEntity(ownerEntityId) as EntityPlayer;

            Log.Out("[REBIRTH CompanionRecall] SAMPLE batch=" + recallBatch +
                " entity=" + dog.entityId +
                " stable=" + stable +
                " generation=" + Generation(record) +
                " entityChanged=" + entityChanged +
                " hp=" + dog.Health + "/" + dog.GetMaxHealth() +
                " hpDelta=" + hpDelta +
                " pos=" + F(dog.position) +
                " ownerDelta=" + (owner != null ? F(dog.position - owner.position) : "<none>") +
                " moved=" + N(moved) +
                " yaw=" + N(dog.rotation.y) +
                " yawStep=" + N(yawStep) +
                " motion=" + F(dog.motion) +
                " order=" + (dog.RebirthRuntimeState != null ? dog.RebirthRuntimeState.Order.ToString() : "<none>") +
                " chunk=" + Chunk(dog));

            previous.EntityId = dog.entityId;
            previous.PreviousPosition = dog.position;
            previous.PreviousYaw = dog.rotation.y;
            previous.PreviousHealth = dog.Health;
            previous.HasPrevious = true;
        }
    }

    public static string BuildReport(World world, EntityPlayer player)
    {
        StringBuilder b = new StringBuilder(16384);
        b.AppendLine("[REBIRTH CompanionRecall] =====================================================");
        b.AppendLine("[REBIRTH CompanionRecall] CONSOLIDATED COMPANION RECALL AUDIT");
        if (world == null || player == null)
        {
            b.AppendLine("[REBIRTH CompanionRecall] RESULT=NO_WORLD_OR_PLAYER");
            return b.ToString().TrimEnd();
        }

        string resolvedOwner;
        RebirthDogLifecycleService.TryResolveOwnerId(player, out resolvedOwner);
        b.Append("[REBIRTH CompanionRecall] playerEntity=").Append(player.entityId)
            .Append(" ownerId='").Append(resolvedOwner ?? string.Empty).Append("'")
            .Append(" playerPos=").Append(F(player.position))
            .Append(" playerYaw=").Append(N(player.rotation.y)).AppendLine();

        List<Entity> entities = world.Entities != null ? world.Entities.list : null;
        int drones = 0, npcs = 0, dogs = 0;
        if (entities != null)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                EntityDrone drone = entities[i] as EntityDrone;
                if (drone != null && !drone.IsDead() && drone.belongsPlayerId == player.entityId)
                {
                    drones++;
                    b.Append("[REBIRTH CompanionRecall] LIVE_DRONE entity=").Append(drone.entityId)
                        .Append(" order=").Append(drone.OrderState)
                        .Append(" hp=").Append(drone.Health).Append('/').Append(drone.GetMaxHealth())
                        .Append(" pos=").Append(F(drone.position))
                        .Append(" delta=").Append(F(drone.position - player.position))
                        .Append(" yaw=").Append(N(drone.rotation.y))
                        .Append(" chunk=").Append(Chunk(drone)).AppendLine();
                }
            }
        }

        RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
        for (int i = 0; i < states.Length; i++)
        {
            RebirthNpcRuntimeState state = states[i];
            if (state == null || state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                !string.Equals(state.OwnerId ?? string.Empty, resolvedOwner ?? string.Empty, StringComparison.OrdinalIgnoreCase)) continue;
            int entityId;
            bool mapped = RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId);
            Entity entity = mapped ? world.GetEntity(entityId) : null;
            EntityRebirthDogCompanion dog = entity as EntityRebirthDogCompanion;
            if (dog != null) dogs++; else npcs++;
            RebirthNpcPersistentRecordView record;
            RebirthNpcAggregatePersistenceStore.TryGetView(state.StableId, out record);
            EntityAlive alive = entity as EntityAlive;
            b.Append("[REBIRTH CompanionRecall] RUNTIME_NPC stable=").Append(state.StableId)
                .Append(" generation=").Append(Generation(record))
                .Append(" entity=").Append(mapped ? entityId.ToString(CultureInfo.InvariantCulture) : "<none>")
                .Append(" worldEntity=").Append(entity != null ? entity.GetType().Name : "<none>")
                .Append(" presence=").Append(state.Presence)
                .Append(" order=").Append(state.Order)
                .Append(" hp=").Append(alive != null ? (alive.Health + "/" + alive.GetMaxHealth()) : "<none>")
                .Append(" pos=").Append(entity != null ? F(entity.position) : (record != null && record.Transform != null ? F(record.Transform.WorldPosition) : "<none>"))
                .Append(" chunk=").Append(alive != null ? Chunk(alive) : "<none>").AppendLine();
        }
        b.Append("[REBIRTH CompanionRecall] COUNTS drones=").Append(drones)
            .Append(" dogRuntimeStates=").Append(dogs)
            .Append(" otherNpcRuntimeStates=").Append(npcs).AppendLine();
        b.AppendLine("[REBIRTH CompanionRecall] NOTE stable= is the logical companion identity; entity= is only the current runtime body. A ghost reconstruction keeps stable= and increments generation while entity= changes.");
        b.AppendLine("[REBIRTH CompanionRecall] =====================================================");
        b.AppendLine(RebirthDogExistenceDebug.BuildReport(world, player));
        return b.ToString().TrimEnd();
    }

    private static bool IsTraceOwner(EntityPlayer player)
    {
        return TraceEnabled && player != null && (ownerEntityId <= 0 || player.entityId == ownerEntityId);
    }

    private static bool OwnsTraceDog(EntityRebirthDogCompanion dog)
    {
        if (!TraceEnabled || dog == null || dog.RebirthRuntimeState == null) return false;
        return !string.IsNullOrEmpty(ownerId) && string.Equals(dog.RebirthRuntimeState.OwnerId ?? string.Empty,
            ownerId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RecordOwnedByTrace(RebirthNpcPersistentRecordView record)
    {
        return TraceEnabled && record != null && record.Ownership != null && !string.IsNullOrEmpty(ownerId) &&
            string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty,
                ownerId, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveStable(EntityAlive entity)
    {
        EntityRebirthNPC npc = entity as EntityRebirthNPC;
        return npc != null && npc.RebirthRuntimeState != null ? npc.RebirthRuntimeState.StableId.ToString() : "<native>";
    }

    private static RebirthNpcPersistentRecordView ResolveRecord(string stable)
    {
        if (string.IsNullOrEmpty(stable) || stable == "<native>") return null;
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
            if (records[i] != null && records[i].Identity != null &&
                string.Equals(records[i].Identity.StableNpcId.ToString(), stable, StringComparison.OrdinalIgnoreCase)) return records[i];
        return null;
    }

    private static string Generation(RebirthNpcPersistentRecordView record)
    {
        return record != null && record.Presence != null
            ? record.Presence.EmbodimentGeneration.ToString(CultureInfo.InvariantCulture)
            : "<none>";
    }

    private static string Chunk(Entity entity)
    {
        if (entity == null) return "<none>";
        int px = World.toChunkXZ(Utils.Fastfloor(entity.position.x));
        int pz = World.toChunkXZ(Utils.Fastfloor(entity.position.z));
        return "pos=" + px + "," + pz +
            " added=" + entity.addedToChunk +
            " native=" + (entity.addedToChunk ? entity.chunkPosAddedEntityTo.x + "," + entity.chunkPosAddedEntityTo.z : "<none>");
    }

    private static string F(Vector3 v)
    {
        return "(" + N(v.x) + "," + N(v.y) + "," + N(v.z) + ")";
    }

    private static string N(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
}

[Preserve]
public sealed class ConsoleCmdRebirthCompanionRecallDebug : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => false;

    public override string[] getCommands()
    {
        return new[] { "rbcompaniondebug", "rbcompdebug", "rbcrecalldebug" };
    }

    public override string getDescription()
    {
        return "Traces companion recall position, runtime identity/generation, health, chunk residency, fall-through recovery and dog rotation.";
    }

    public override string getHelp()
    {
        return "Usage: rbcompaniondebug [playerEntityId] [traceSeconds]\n"
             + "       rbcompaniondebug dump [playerEntityId]\n"
             + "       rbcompaniondebug off\n"
             + "Default trace is 120 seconds. Start it, press the companion recall shortcut, reproduce the disappearance/spin, then run 'rbcompaniondebug dump' and send the output_log_client/server log.";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string first = parameters != null && parameters.Count > 0 ? parameters[0] ?? string.Empty : string.Empty;
        if (string.Equals(first, "off", StringComparison.OrdinalIgnoreCase))
        {
            RebirthCompanionRecallDebug.StopTrace();
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[REBIRTH CompanionRecall] world is not available.");
            return;
        }

        bool dumpOnly = string.Equals(first, "dump", StringComparison.OrdinalIgnoreCase);
        int parameterOffset = dumpOnly ? 1 : 0;
        EntityPlayer player = ResolvePlayer(world, parameters, parameterOffset, senderInfo);
        if (player == null)
        {
            Log.Out("[REBIRTH CompanionRecall] player could not be resolved.");
            return;
        }

        if (dumpOnly)
        {
            Log.Out(RebirthCompanionRecallDebug.BuildReport(world, player));
            return;
        }

        float seconds = 120f;
        float parsed;
        int secondsIndex = parameterOffset + 1;
        if (parameters != null && parameters.Count > secondsIndex &&
            float.TryParse(parameters[secondsIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            seconds = Mathf.Clamp(parsed, 5f, 600f);
        else if (parameters != null && parameters.Count == 1 &&
                 float.TryParse(parameters[0], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
                 world.GetEntity((int)parsed) == null)
            seconds = Mathf.Clamp(parsed, 5f, 600f);

        RebirthCompanionRecallDebug.BeginTrace(player, seconds);
        Log.Out(RebirthCompanionRecallDebug.BuildReport(world, player));
        Log.Out("[REBIRTH CompanionRecall] READY: press the recall shortcut now. After the problem reproduces, run 'rbcompaniondebug dump' before stopping the trace.");
    }

    private static EntityPlayer ResolvePlayer(World world, List<string> parameters, int index, CommandSenderInfo senderInfo)
    {
        int requestedId;
        if (parameters != null && parameters.Count > index && int.TryParse(parameters[index], out requestedId))
        {
            EntityPlayer requested = world.GetEntity(requestedId) as EntityPlayer;
            if (requested != null) return requested;
        }
        if (senderInfo.RemoteClientInfo != null)
        {
            EntityPlayer sender = world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;
            if (sender != null) return sender;
        }
        return world.GetPrimaryPlayer() as EntityPlayer;
    }
}
