using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Server-authoritative dog existence audit. This intentionally compares every layer that can
/// disagree: XML/entity-class authoring, durable aggregate records, runtime registry mappings,
/// actual World.Entities objects, native chunk residency, owned-entity projection and the
/// persistent position that feeds the Companions roster/navigation marker fallback.
///
/// It is diagnostic only: it never creates, moves, removes, repairs or mutates a dog record.
/// </summary>
public static class RebirthDogExistenceDebug
{
    private static float traceUntil = -1f;
    private static int traceOwnerEntityId = -1;

    public static bool TraceEnabled
    {
        get { return Time.realtimeSinceStartup <= traceUntil; }
    }

    public static void BeginTrace(EntityPlayer player, float seconds)
    {
        traceOwnerEntityId = player != null ? player.entityId : -1;
        traceUntil = Time.realtimeSinceStartup + Mathf.Clamp(seconds, 5f, 600f);
    }

    public static void StopTrace()
    {
        traceUntil = -1f;
        traceOwnerEntityId = -1;
    }

    public static void TraceCommand(string message, int playerEntityId = -1)
    {
        if (!TraceEnabled) return;
        if (traceOwnerEntityId > 0 && playerEntityId > 0 && traceOwnerEntityId != playerEntityId) return;
        Log.Out("[REBIRTH DogState] COMMAND " + (message ?? string.Empty));
    }

    public static string BuildReport(World world, EntityPlayer player)
    {
        StringBuilder b = new StringBuilder(8192);
        b.AppendLine("[REBIRTH DogState] ============================================================");
        b.AppendLine("[REBIRTH DogState] SERVER-AUTHORITATIVE DOG EXISTENCE AUDIT");
        b.AppendLine("[REBIRTH DogState] This report is READ-ONLY. No dog or persistence state is changed.");

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        b.Append("[REBIRTH DogState] world=").Append(world != null)
            .Append(" remote=").Append(world != null && world.IsRemote())
            .Append(" server=").Append(connection != null && connection.IsServer)
            .Append(" singlePlayer=").Append(connection != null && connection.IsSinglePlayer)
            .Append(" player=").Append(player != null ? player.entityId.ToString(CultureInfo.InvariantCulture) : "<none>")
            .AppendLine();

        if (world == null || player == null)
        {
            b.AppendLine("[REBIRTH DogState] RESULT=NO_WORLD_OR_PLAYER");
            return b.ToString().TrimEnd();
        }

        string ownerId;
        bool hasOwnerId = RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId);
        b.Append("[REBIRTH DogState] ownerId=").Append(hasOwnerId ? ownerId : "<unresolved>")
            .Append(" playerPos=").Append(FormatVector(player.position))
            .AppendLine();

        string saveDirectory = string.Empty;
        try { saveDirectory = GameIO.GetSaveGameDir() ?? string.Empty; } catch { }
        RebirthNpcLifecycleSnapshot lifecycle = RebirthNpcLifecycle.GetSnapshot();
        b.Append("[REBIRTH DogState] WORLD_SCOPE saveDir='").Append(saveDirectory)
            .Append("' lifecycleWorldGeneration=").Append(lifecycle.WorldGeneration)
            .Append(" forcedBoundaryResets=").Append(lifecycle.ForcedWorldBoundaryResets)
            .Append(" lifecycleState=").Append(lifecycle.State)
            .Append(" lastTransition=").Append(lifecycle.LastTransition ?? string.Empty)
            .Append(" aggregate=").Append(RebirthNpcAggregatePersistenceStore.GetSaveScopeReport())
            .AppendLine();

        // Authoring definitions are deliberately reported separately. Their existence only
        // proves that the breed/entity classes are loaded; the owned roster below comes from
        // durable player-owned records and/or actual runtime entities.
        RebirthDogBreedDefinition[] breeds = RebirthDogDefinitions.GetSnapshot();
        int resolvedBreedClasses = 0;
        for (int i = 0; i < breeds.Length; i++)
        {
            RebirthDogBreedDefinition breed = breeds[i];
            if (breed == null) continue;
            int classId = EntityClass.FromString(breed.EntityClassName);
            EntityClass definition;
            if (EntityClass.list != null && EntityClass.list.TryGetValue(classId, out definition) && definition != null)
                resolvedBreedClasses++;
        }
        b.Append("[REBIRTH DogState] AUTHORING breedDefinitions=").Append(breeds.Length)
            .Append(" resolvedEntityClasses=").Append(resolvedBreedClasses)
            .Append(" note='definitions are listed separately from owned/persistent dog records'")
            .AppendLine();

        List<EntityRebirthDogCompanion> loadedDogs = GetLoadedDogs(world);
        RebirthNpcRuntimeState[] runtimeStates = RebirthNpcRuntimeRegistry.GetSnapshot();
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        RebirthNpcPersistentRecordView[] quarantinedDogs = RebirthNpcAggregatePersistenceStore.QuarantinedDogViews();

        int dogRuntimeCount = 0;
        for (int i = 0; i < runtimeStates.Length; i++)
            if (runtimeStates[i] != null && string.Equals(runtimeStates[i].ProfileId ?? string.Empty,
                    RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase)) dogRuntimeCount++;

        int ownedPersistentCount = 0;
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (!RebirthDogStateService.IsDog(record) || record.Identity == null) continue;
            string recordOwner = record.Ownership != null
                ? (record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty)
                : string.Empty;
            if (!hasOwnerId || string.Equals(recordOwner, ownerId, StringComparison.OrdinalIgnoreCase))
                ownedPersistentCount++;
        }

        int quarantinedOwnedCount = 0;
        for (int i = 0; i < quarantinedDogs.Length; i++)
        {
            RebirthNpcPersistentRecordView record = quarantinedDogs[i];
            string recordOwner = record != null && record.Ownership != null
                ? (record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty)
                : string.Empty;
            if (!hasOwnerId || string.Equals(recordOwner, ownerId, StringComparison.OrdinalIgnoreCase))
                quarantinedOwnedCount++;
        }

        b.Append("[REBIRTH DogState] COUNTS persistentOwnedDogRecords=").Append(ownedPersistentCount)
            .Append(" quarantinedUnverifiedDogRecords=").Append(quarantinedOwnedCount)
            .Append(" runtimeDogStates=").Append(dogRuntimeCount)
            .Append(" loadedWorldDogEntities=").Append(loadedDogs.Count)
            .Append(" worldEntities=").Append(world.Entities != null && world.Entities.list != null ? world.Entities.list.Count : -1)
            .AppendLine();

        b.AppendLine("[REBIRTH DogState] --- PERSISTENT OWNED DOG RECORDS ---");
        int written = 0;
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (!RebirthDogStateService.IsDog(record) || record.Identity == null) continue;

            string recordOwner = record.Ownership != null
                ? (record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty)
                : string.Empty;
            if (hasOwnerId && !string.Equals(recordOwner, ownerId, StringComparison.OrdinalIgnoreCase)) continue;

            written++;
            AppendPersistentDogAudit(b, world, player, record, loadedDogs);
        }
        if (written == 0) b.AppendLine("[REBIRTH DogState]   <none>");

        b.AppendLine("[REBIRTH DogState] --- QUARANTINED / UNVERIFIED DOG RECORDS ---");
        int quarantineWritten = 0;
        for (int i = 0; i < quarantinedDogs.Length; i++)
        {
            RebirthNpcPersistentRecordView record = quarantinedDogs[i];
            if (record == null || record.Identity == null) continue;
            string recordOwner = record.Ownership != null
                ? (record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty)
                : string.Empty;
            if (hasOwnerId && !string.Equals(recordOwner, ownerId, StringComparison.OrdinalIgnoreCase)) continue;
            quarantineWritten++;
            b.Append("[REBIRTH DogState]   stable=").Append(record.Identity.StableNpcId)
                .Append(" name='").Append(record.Identity.GeneratedOrAssignedDisplayName ?? string.Empty)
                .Append("' owner='").Append(recordOwner)
                .Append("' status=NOT_AUTHORITATIVE_UNTIL_CURRENT_SAVE_ENTITY_PROOF")
                .AppendLine();
        }
        if (quarantineWritten == 0) b.AppendLine("[REBIRTH DogState]   <none>");

        b.AppendLine("[REBIRTH DogState] --- LOADED WORLD DOG ENTITIES ---");
        if (loadedDogs.Count == 0)
        {
            b.AppendLine("[REBIRTH DogState]   <none>");
        }
        else
        {
            for (int i = 0; i < loadedDogs.Count; i++)
                AppendLoadedDogAudit(b, world, player, loadedDogs[i]);
        }

        b.AppendLine("[REBIRTH DogState] --- DOG RUNTIME REGISTRY ---");
        int runtimeWritten = 0;
        for (int i = 0; i < runtimeStates.Length; i++)
        {
            RebirthNpcRuntimeState state = runtimeStates[i];
            if (state == null || !string.Equals(state.ProfileId ?? string.Empty,
                    RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase)) continue;
            if (hasOwnerId && state.OwnershipKind == RebirthNpcOwnershipKind.Player &&
                !string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase)) continue;

            runtimeWritten++;
            int mappedId;
            bool hasMapping = RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out mappedId);
            Entity mapped = hasMapping ? world.GetEntity(mappedId) : null;
            b.Append("[REBIRTH DogState]   stable=").Append(state.StableId)
                .Append(" mappedId=").Append(hasMapping ? mappedId.ToString(CultureInfo.InvariantCulture) : "<none>")
                .Append(" worldEntity=").Append(mapped != null ? mapped.GetType().Name : "<none>")
                .Append(" runtimePresence=").Append(state.Presence)
                .Append(" runtimeOrder=").Append(state.Order)
                .Append(" runtimeOwner=").Append(state.OwnershipKind).Append(':').Append(state.OwnerId ?? string.Empty)
                .AppendLine();
        }
        if (runtimeWritten == 0) b.AppendLine("[REBIRTH DogState]   <none>");

        b.AppendLine("[REBIRTH DogState] --- INTERPRETATION ---");
        b.AppendLine("[REBIRTH DogState] QUARANTINED / UNVERIFIED = disk record is preserved for recovery but is NOT shown, counted, recalled or ghost-repaired until a current-save entity proves the same embedded StableId.");
        b.AppendLine("[REBIRTH DogState] GHOST_ACTIVE_RECORD_NO_WORLD_ENTITY = the Companions roster/marker can exist from persistence while no dog entity is loaded.");
        b.AppendLine("[REBIRTH DogState] STALE_RUNTIME_MAPPING = runtime registry still names an entity id that World.GetEntity cannot resolve.");
        b.AppendLine("[REBIRTH DogState] LIVE_*_CHUNK_* = an actual entity exists but native chunk residency disagrees with its position/declared chunk.");
        b.AppendLine("[REBIRTH DogState] While trace is active, click one Stay action. A COMMAND line proves whether the click reached the server and why it applied or no-op'd.");
        b.AppendLine("[REBIRTH DogState] ============================================================");
        return b.ToString().TrimEnd();
    }

    private static void AppendPersistentDogAudit(StringBuilder b, World world, EntityPlayer player,
        RebirthNpcPersistentRecordView record, List<EntityRebirthDogCompanion> loadedDogs)
    {
        RebirthNpcStableId stableId = record.Identity.StableNpcId;
        string name = record.Identity.GeneratedOrAssignedDisplayName ?? string.Empty;
        string lifecycle = record.Dog != null ? record.Dog.Lifecycle.ToString() : "<none>";
        string presence = record.Presence != null ? (record.Presence.PresenceState ?? string.Empty) : "<none>";
        string order = record.Order != null ? (record.Order.OrderState ?? string.Empty) : "<none>";
        Vector3 persistedPos = record.Transform != null ? record.Transform.WorldPosition : Vector3.zero;
        string chunkKey = record.Transform != null ? (record.Transform.ChunkKey ?? string.Empty) : string.Empty;
        string presenceChunkKey = record.Presence != null ? (record.Presence.LastKnownChunkKey ?? string.Empty) : string.Empty;

        int registryId;
        bool registryMapped = RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out registryId);
        Entity registryEntity = registryMapped ? world.GetEntity(registryId) : null;
        EntityRebirthDogCompanion registryDog = registryEntity as EntityRebirthDogCompanion;

        List<EntityRebirthDogCompanion> stableMatches = new List<EntityRebirthDogCompanion>();
        for (int i = 0; i < loadedDogs.Count; i++)
        {
            RebirthNpcStableId candidate = ResolveStableId(loadedDogs[i]);
            if (!candidate.IsEmpty && candidate == stableId) stableMatches.Add(loadedDogs[i]);
        }

        EntityRebirthDogCompanion liveDog = registryDog;
        if (liveDog == null && stableMatches.Count > 0) liveDog = stableMatches[0];

        int persistedChunkX = World.toChunkXZ(Utils.Fastfloor(persistedPos.x));
        int persistedChunkZ = World.toChunkXZ(Utils.Fastfloor(persistedPos.z));
        Chunk persistedChunk = world.GetChunkSync(persistedChunkX, persistedChunkZ) as Chunk;

        b.Append("[REBIRTH DogState] DOG stable=").Append(stableId)
            .Append(" name='").Append(name).Append("'")
            .Append(" breed=").Append(record.Dog != null ? record.Dog.BreedId : "<none>")
            .AppendLine();
        b.Append("[REBIRTH DogState]   PERSIST lifecycle=").Append(lifecycle)
            .Append(" presence=").Append(presence)
            .Append(" order=").Append(order)
            .Append(" owner='").Append(record.Ownership != null ? record.Ownership.OwnerPlatformIdOrPersistentPlayerId : string.Empty).Append("'")
            .Append(" worldPos=").Append(FormatVector(persistedPos))
            .Append(" posChunk=").Append(persistedChunkX).Append(',').Append(persistedChunkZ)
            .Append(" chunkKey='").Append(chunkKey).Append("'")
            .Append(" presenceChunkKey='").Append(presenceChunkKey).Append("'")
            .Append(" generation=").Append(record.Presence != null ? record.Presence.EmbodimentGeneration.ToString(CultureInfo.InvariantCulture) : "<none>")
            .Append(" hp=").Append(record.Vitals != null ? record.Vitals.CurrentHealth.ToString(CultureInfo.InvariantCulture) : "<none>")
            .Append('/').Append(record.Vitals != null ? record.Vitals.MaximumHealthAtSave.ToString(CultureInfo.InvariantCulture) : "<none>")
            .Append(" posChunkLoaded=").Append(persistedChunk != null)
            .AppendLine();
        b.Append("[REBIRTH DogState]   REGISTRY mapped=").Append(registryMapped)
            .Append(" entityId=").Append(registryMapped ? registryId.ToString(CultureInfo.InvariantCulture) : "<none>")
            .Append(" World.GetEntity=").Append(registryEntity != null ? registryEntity.GetType().Name : "<none>")
            .Append(" worldStableMatches=").Append(stableMatches.Count)
            .AppendLine();

        bool active = record.Dog != null && record.Dog.Lifecycle == RebirthDogLifecycleKind.Active;
        if (liveDog == null)
        {
            string classification = active
                ? (registryMapped ? "GHOST_ACTIVE_STALE_RUNTIME_MAPPING" : "GHOST_ACTIVE_RECORD_NO_WORLD_ENTITY")
                : "NON_ACTIVE_DURABLE_STATE_NO_WORLD_ENTITY";
            b.Append("[REBIRTH DogState]   UI_FALLBACK wouldShow=").Append(active)
                .Append(" entityId=-1 markerFromPersistedPosition=").Append(active)
                .Append(" positionalActionsUsePersistedDistance=").Append(active)
                .AppendLine();
            b.Append("[REBIRTH DogState]   CLASSIFICATION=").Append(classification).AppendLine();
            if (active)
                b.AppendLine("[REBIRTH DogState]   EXPECTED_CLICK_BEHAVIOR=Stay/Guard can be displayed from the durable row, but server execution has no live Entity to mutate.");
            return;
        }

        int posChunkX = World.toChunkXZ(Utils.Fastfloor(liveDog.position.x));
        int posChunkZ = World.toChunkXZ(Utils.Fastfloor(liveDog.position.z));
        Chunk declaredChunk = liveDog.addedToChunk
            ? world.GetChunkSync(liveDog.chunkPosAddedEntityTo.x, liveDog.chunkPosAddedEntityTo.z) as Chunk
            : null;
        bool declaredContains = ContainsEntity(declaredChunk, liveDog);
        bool persistedContains = ContainsEntity(persistedChunk, liveDog);
        bool chunkCoordsMatch = liveDog.addedToChunk && liveDog.chunkPosAddedEntityTo.x == posChunkX && liveDog.chunkPosAddedEntityTo.z == posChunkZ;
        bool worldListContains = ContainsWorldEntity(world, liveDog);
        bool aliveListContains = ContainsAlive(world, liveDog);
        bool distributorTracked = false;
        try { distributorTracked = world.entityDistributer != null && world.entityDistributer.FindEntry(liveDog) != null; }
        catch { }

        b.Append("[REBIRTH DogState]   LIVE entity=").Append(liveDog.entityId)
            .Append(" type=").Append(liveDog.GetType().Name)
            .Append(" dead=").Append(liveDog.IsDead())
            .Append(" spawned=").Append(liveDog.IsSpawned())
            .Append(" markedUnload=").Append(liveDog.IsMarkedForUnload())
            .Append(" worldList=").Append(worldListContains)
            .Append(" aliveList=").Append(aliveListContains)
            .Append(" distributor=").Append(distributorTracked)
            .Append(" hp=").Append(liveDog.Health).Append('/').Append(liveDog.GetMaxHealth())
            .Append(" yaw=").Append(liveDog.rotation.y.ToString("0.00", CultureInfo.InvariantCulture))
            .Append(" motion=").Append(FormatVector(liveDog.motion))
            .AppendLine();
        b.Append("[REBIRTH DogState]   LIVE_POS entityPos=").Append(FormatVector(liveDog.position))
            .Append(" transformPos=").Append(FormatVector(liveDog.transform.position))
            .Append(" posChunk=").Append(posChunkX).Append(',').Append(posChunkZ)
            .Append(" addedToChunk=").Append(liveDog.addedToChunk)
            .Append(" declaredChunk=").Append(liveDog.addedToChunk ? (liveDog.chunkPosAddedEntityTo.x + "," + liveDog.chunkPosAddedEntityTo.z) : "<none>")
            .Append(" declaredChunkLoaded=").Append(declaredChunk != null)
            .Append(" declaredContainsEntity=").Append(declaredContains)
            .Append(" persistedChunkContainsEntity=").Append(persistedContains)
            .AppendLine();
        b.Append("[REBIRTH DogState]   OWNER belongsPlayerId=").Append(liveDog.belongsPlayerId)
            .Append(" playerHasOwnedEntity=").Append(player.HasOwnedEntity(liveDog.entityId))
            .Append(" leaderCVar=").Append(liveDog.Buffs != null ? Mathf.RoundToInt(liveDog.Buffs.GetCustomVar("$Leader")) : 0)
            .Append(" runtimeStable=").Append(liveDog.RebirthRuntimeState != null ? liveDog.RebirthRuntimeState.StableId.ToString() : "<none>")
            .Append(" embeddedStable=").Append(liveDog.RebirthSavedStableIdForDebug)
            .AppendLine();

        List<string> flags = new List<string>();
        if (!worldListContains) flags.Add("LIVE_ENTITY_NOT_IN_WORLD_LIST");
        if (!aliveListContains) flags.Add("LIVE_ENTITY_NOT_IN_ALIVE_LIST");
        if (!liveDog.addedToChunk) flags.Add("LIVE_ENTITY_NOT_IN_CHUNK");
        if (liveDog.addedToChunk && !chunkCoordsMatch) flags.Add("LIVE_ENTITY_CHUNK_COORD_MISMATCH");
        if (liveDog.addedToChunk && !declaredContains) flags.Add("LIVE_ENTITY_NOT_LISTED_IN_DECLARED_CHUNK");
        if (registryMapped && registryId != liveDog.entityId) flags.Add("LIVE_WORLD_ENTITY_REGISTRY_ID_MISMATCH");
        if (!registryMapped) flags.Add("LIVE_WORLD_ENTITY_REGISTRY_MISSING");
        if (stableMatches.Count > 1) flags.Add("DUPLICATE_LIVE_STABLE_ID");
        if (flags.Count == 0) flags.Add("LIVE_OK");
        b.Append("[REBIRTH DogState]   CLASSIFICATION=").Append(string.Join("|", flags.ToArray())).AppendLine();
    }

    private static void AppendLoadedDogAudit(StringBuilder b, World world, EntityPlayer player, EntityRebirthDogCompanion dog)
    {
        if (dog == null) return;
        RebirthNpcStableId stableId = ResolveStableId(dog);
        int posChunkX = World.toChunkXZ(Utils.Fastfloor(dog.position.x));
        int posChunkZ = World.toChunkXZ(Utils.Fastfloor(dog.position.z));
        Chunk declaredChunk = dog.addedToChunk ? world.GetChunkSync(dog.chunkPosAddedEntityTo.x, dog.chunkPosAddedEntityTo.z) as Chunk : null;
        b.Append("[REBIRTH DogState]   entity=").Append(dog.entityId)
            .Append(" stable=").Append(stableId.IsEmpty ? "<none>" : stableId.ToString())
            .Append(" name='").Append(dog.EntityName ?? string.Empty).Append("'")
            .Append(" pos=").Append(FormatVector(dog.position))
            .Append(" posChunk=").Append(posChunkX).Append(',').Append(posChunkZ)
            .Append(" addedToChunk=").Append(dog.addedToChunk)
            .Append(" declaredChunk=").Append(dog.addedToChunk ? (dog.chunkPosAddedEntityTo.x + "," + dog.chunkPosAddedEntityTo.z) : "<none>")
            .Append(" declaredContains=").Append(ContainsEntity(declaredChunk, dog))
            .Append(" dead=").Append(dog.IsDead())
            .Append(" markedUnload=").Append(dog.IsMarkedForUnload())
            .Append(" hp=").Append(dog.Health).Append('/').Append(dog.GetMaxHealth())
            .Append(" yaw=").Append(dog.rotation.y.ToString("0.00", CultureInfo.InvariantCulture))
            .Append(" motion=").Append(FormatVector(dog.motion))
            .Append(" belongsPlayerId=").Append(dog.belongsPlayerId)
            .Append(" playerOwnedSet=").Append(player.HasOwnedEntity(dog.entityId))
            .AppendLine();
    }

    private static List<EntityRebirthDogCompanion> GetLoadedDogs(World world)
    {
        List<EntityRebirthDogCompanion> result = new List<EntityRebirthDogCompanion>();
        if (world == null || world.Entities == null || world.Entities.list == null) return result;
        for (int i = 0; i < world.Entities.list.Count; i++)
        {
            EntityRebirthDogCompanion dog = world.Entities.list[i] as EntityRebirthDogCompanion;
            if (dog != null) result.Add(dog);
        }
        return result;
    }

    private static RebirthNpcStableId ResolveStableId(EntityRebirthDogCompanion dog)
    {
        if (dog == null) return default(RebirthNpcStableId);
        if (dog.RebirthRuntimeState != null && !dog.RebirthRuntimeState.StableId.IsEmpty)
            return dog.RebirthRuntimeState.StableId;
        if (!dog.RebirthSavedStableIdForDebug.IsEmpty)
            return dog.RebirthSavedStableIdForDebug;
        return default(RebirthNpcStableId);
    }

    private static bool ContainsWorldEntity(World world, Entity entity)
    {
        if (world == null || entity == null || world.Entities == null || world.Entities.list == null) return false;
        for (int i = 0; i < world.Entities.list.Count; i++)
            if (ReferenceEquals(world.Entities.list[i], entity) || world.Entities.list[i]?.entityId == entity.entityId) return true;
        return false;
    }

    private static bool ContainsAlive(World world, EntityRebirthDogCompanion dog)
    {
        if (world == null || dog == null || world.EntityAlives == null) return false;
        for (int i = 0; i < world.EntityAlives.Count; i++)
            if (ReferenceEquals(world.EntityAlives[i], dog) || world.EntityAlives[i]?.entityId == dog.entityId) return true;
        return false;
    }

    private static bool ContainsEntity(Chunk chunk, Entity entity)
    {
        if (chunk == null || entity == null || chunk.entityLists == null) return false;
        for (int layer = 0; layer < chunk.entityLists.Length; layer++)
        {
            List<Entity> list = chunk.entityLists[layer];
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                Entity candidate = list[i];
                if (ReferenceEquals(candidate, entity) || candidate?.entityId == entity.entityId) return true;
            }
        }
        return false;
    }

    private static string FormatVector(Vector3 value)
    {
        return "(" + value.x.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            value.y.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            value.z.ToString("0.00", CultureInfo.InvariantCulture) + ")";
    }
}

[Preserve]
public sealed class ConsoleCmdRebirthDogState : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => false;

    public override string[] getCommands()
    {
        return new[] { "rbdogstate", "rbdogghost", "rbdogaudit" };
    }

    public override string getDescription()
    {
        return "Audits whether REBIRTH dog roster entries correspond to real world entities and traces companion action no-ops.";
    }

    public override string getHelp()
    {
        return "Usage: rbdogstate [playerEntityId] [traceSeconds]\n"
             + "       rbdogstate off\n"
             + "Default traceSeconds is 90. The command immediately dumps persistent/runtime/world/chunk state, then traces companion commands so you can click Stay once and see whether the server received it.";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        if (parameters != null && parameters.Count > 0 &&
            string.Equals(parameters[0] ?? string.Empty, "off", StringComparison.OrdinalIgnoreCase))
        {
            RebirthDogExistenceDebug.StopTrace();
            Log.Out("[REBIRTH DogState] trace=OFF");
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[REBIRTH DogState] world is not available.");
            return;
        }

        EntityPlayer player = null;
        int requestedPlayerId;
        if (parameters != null && parameters.Count > 0 && int.TryParse(parameters[0], out requestedPlayerId))
            player = world.GetEntity(requestedPlayerId) as EntityPlayer;
        if (player == null && senderInfo.RemoteClientInfo != null)
            player = world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;
        if (player == null)
            player = world.GetPrimaryPlayer() as EntityPlayer;

        float traceSeconds = 90f;
        float parsedSeconds;
        if (parameters != null && parameters.Count > 1 &&
            float.TryParse(parameters[1], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedSeconds))
            traceSeconds = Mathf.Clamp(parsedSeconds, 5f, 600f);

        RebirthDogExistenceDebug.BeginTrace(player, traceSeconds);
        Log.Out(RebirthDogExistenceDebug.BuildReport(world, player));
        Log.Out("[REBIRTH DogState] trace=ON for " + traceSeconds.ToString("0", CultureInfo.InvariantCulture) +
            "s. NOW click one Stay action in the Companions window, then run rbdogstate again.");
    }
}
