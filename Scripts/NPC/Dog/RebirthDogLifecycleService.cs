using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// P2 dog acquisition/ownership authority. This layer intentionally owns only
/// deployment, hiring, ownership, caps, pickup/redeploy, respawn and persistence projection.
/// Movement/combat/inventory are implemented by the dog-specific services without reflection.
/// </summary>
public static class RebirthDogLifecycleService
{
    public const string HireItemName = "foodRawMeat";
    public const int HireItemCount = 1;
    public const float MaximumHireDistance = 5f;
    public const float MaximumPickupDistance = 5f;

    private static bool initialized;

    private sealed class GhostRepairRetry
    {
        public int PlayerEntityId;
        public int AttemptsRemaining;
        public long NextAttemptUtcTicks;
        public bool OwnerProjectionReconciled;
    }

    private static readonly object GhostRepairSync = new object();
    private static readonly Dictionary<int, GhostRepairRetry> GhostRepairRetries =
        new Dictionary<int, GhostRepairRetry>();

    public static void EnsureInitialized()
    {
        if (initialized) return;
        RebirthDogDefinitions.EnsureInitialized();
        RebirthDogNavigationMarkerService.EnsureInitialized();
        RebirthDogStorageService.EnsureInitialized();
        initialized = true;
    }

    public static bool TryResolveOwnerId(EntityPlayer player, out string ownerId)
    {
        ownerId = string.Empty;
        if (player == null || GameManager.Instance == null) return false;

        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if (persistent != null && persistent.PrimaryId != null)
        {
            ownerId = persistent.PrimaryId.ToString();
            if (!string.IsNullOrEmpty(ownerId)) return true;
        }

        // A joining remote client can have its own persistent identity before the
        // entity-indexed player list arrives. Only use this identity for that exact
        // local player; never assign it to another streamed player.
        World localWorld = GameManager.Instance.World;
        if (localWorld != null && localWorld.IsRemote() &&
            ReferenceEquals(localWorld.GetPrimaryPlayer(), player))
        {
            PersistentPlayerData local = GameManager.Instance.GetPersistentLocalPlayer();
            if (local != null && local.PrimaryId != null)
            {
                ownerId = local.PrimaryId.ToString();
                if (!string.IsNullOrEmpty(ownerId)) return true;
            }
        }

        // Relog/first-spawn edge: the player can already exist in World while the normal
        // PersistentPlayerData entity index is not yet restored. Fall back to the stable
        // server-owned identity cache so ownership checks/projectors continue to recognize
        // the same durable owner during the reconnect transition.
        RebirthStablePlayerIdentity identity;
        if (RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) &&
            identity != null && !string.IsNullOrEmpty(identity.CanonicalId))
        {
            ownerId = identity.CanonicalId;
            return true;
        }
        return false;
    }

    public static bool CanAcquire(EntityPlayer player, out string reason)
    {
        reason = string.Empty;
        EnsureInitialized();
        if (player == null)
        {
            reason = Localization.Get("xuiRebirthDogAcquireNoPlayer");
            return false;
        }

        int dogs, total, globalCap;
        if (!TryGetOwnershipCounts(player, out dogs, out total, out globalCap))
        {
            World activeWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
            reason = activeWorld != null && activeWorld.IsRemote()
                ? Localization.Get("xuiRebirthDogCapacitySyncPending")
                : Localization.Get("xuiRebirthDogAcquireNoOwnerId");
            return false;
        }

        // Dog capacity is independent of other companion categories. In Rebirth progression
        // the base allowance is authored by the Advanced Discipline foundation; Base Game keeps
        // the existing legacy perk calculation. There is no separate hard dog ceiling here.
        if (dogs >= globalCap)
        {
            reason = Localization.Get(RebirthSurvivorMode.IsEnabledForCurrentWorld()
                ? "xuiRebirthDogCapReachedRebirth" : "xuiRebirthDogCapReached");
            return false;
        }
        if (RebirthSurvivorMode.IsEnabledForCurrentWorld() && total >= RebirthAdvancedDisciplineRegistry.GlobalCompanionSafetyLimit)
        {
            reason = Localization.Get("xuiRebirthCompanionSafetyLimitReached");
            return false;
        }
        return true;
    }

    public static bool TryGetOwnershipCounts(EntityPlayer player, out int dogCount, out int totalCount, out int globalCap)
    {
        dogCount = 0;
        totalCount = 0;
        globalCap = 1;
        if (player == null) return false;

        // Capacity is world-scoped. This guard catches save-to-save transitions even if
        // WorldShuttingDown was skipped/delayed, before any stale aggregate can be counted.
        RebirthNpcLifecycle.EnsureCurrentWorldScope();

        // Dedicated-client placement must use the server's persistent count, not a best-effort
        // local reconstruction. Otherwise an unloaded/AwaitingRespawn dog can be invisible to
        // the client, allowing native Block.PlaceBlock to consume/play success before the server
        // rejects it. The sync cache is populated on player spawn and every capacity mutation.
        World activeWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (activeWorld != null && activeWorld.IsRemote())
        {
            // Keep the owned-dog count server-authoritative. Capacity itself is derived from
            // the progression-mode authority: centralized Rebirth tunables in Rebirth mode,
            // or the existing legacy perk calculation in Base Game mode.
            int syncedCap;
            if (!RebirthDogCapacitySyncService.TryGetClientCounts(
                    player, out dogCount, out totalCount, out syncedCap))
            {
                // Fresh clients can reach placement before the initial server capacity
                // snapshot arrives. Do not turn that short synchronization window into
                // a false "no dog slots" denial. Predict from companions the local player
                // can actually see; TryDeploy repeats CanAcquire on the authoritative
                // server, so an unloaded durable dog can still never exceed the real cap.
                dogCount = CountVisibleOwnedDogsForClient(player);
                totalCount = dogCount;
                globalCap = CalculateDogCapacity(player);
                RebirthDogDeploymentDebug.TraceGate(
                    "CapacitySync pending: client placement prediction dogs=" + dogCount +
                    " cap=" + globalCap + " player=" + player.entityId + ".");
                return true;
            }
            int liveCap = CalculateDogCapacity(player);
            globalCap = liveCap > 0 ? liveCap : Math.Max(1, syncedCap);
            return true;
        }

        string ownerId;
        if (!TryResolveOwnerId(player, out ownerId))
            return false;

        CountOwnedCompanions(ownerId, out dogCount, out totalCount);
        globalCap = CalculateDogCapacity(player);
        return true;
    }

    private static int CalculateDogCapacity(EntityPlayer player)
    {
        // Rebirth 3.0 companion authority must not depend on the retired Charismatic Nature perk.
        // Chunk A centralizes the base allowance; later Animal Handling/discipline chunks can layer
        // authored capacity bonuses over this service without reviving the legacy class dependency.
        if (RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return RebirthAdvancedDisciplineRegistry.BaseDogCapacity;

        // Preserve the existing native/legacy calculation when Character Progression is Base Game.
        int charismaticNature = 0;
        if (player != null && player.Progression != null)
        {
            ProgressionValue value = player.Progression.GetProgressionValue("perkCharismaticNature");
            if (value != null) charismaticNature = Math.Max(0, value.CalculatedLevel(player));
        }
        return charismaticNature + 1;
    }

    private static int CountVisibleOwnedDogsForClient(EntityPlayer player)
    {
        if (player == null || player.Companions == null)
            return 0;

        int dogs = 0;
        HashSet<int> seenEntityIds = new HashSet<int>();
        for (int i = 0; i < player.Companions.Count; i++)
        {
            EntityRebirthDogCompanion dog = player.Companions[i] as EntityRebirthDogCompanion;
            if ((UnityEngine.Object)dog == (UnityEngine.Object)null || dog.IsDead())
                continue;
            if (!seenEntityIds.Add(dog.entityId))
                continue;

            RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
            if (state != null && state.OwnershipKind != RebirthNpcOwnershipKind.Player)
                continue;
            if (state != null && RebirthDogStateService.IsTamedWild(state.StableId))
                continue;

            dogs++;
        }
        return dogs;
    }

    public static bool TryHire(World world, EntityPlayer player, EntityRebirthDogCompanion dog, out string reason)
    {
        reason = string.Empty;
        EnsureInitialized();
        if (!IsAuthoritative(world))
        {
            reason = Localization.Get("xuiRebirthDogServerRequired");
            return false;
        }
        if (player == null || dog == null || dog.IsDead())
        {
            reason = Localization.Get("xuiRebirthDogHireUnavailable");
            return false;
        }
        if (Vector3.Distance(player.position, dog.position) > MaximumHireDistance)
        {
            reason = Localization.Get("xuiRebirthDogTooFar");
            return false;
        }

        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        if (state == null || state.ProfileId != RebirthDogDefinitions.ProfileId)
        {
            reason = Localization.Get("xuiRebirthDogHireUnavailable");
            return false;
        }
        if (state.OwnershipKind != RebirthNpcOwnershipKind.None)
        {
            reason = Localization.Get("xuiRebirthDogAlreadyOwned");
            return false;
        }
        if (!CanAcquire(player, out reason)) return false;

        ItemValue rawMeat = ItemClass.GetItem(HireItemName);
        int toolbeltMeat = player.inventory != null ? player.inventory.GetItemCount(rawMeat, false, -1, -1) : 0;
        int backpackMeat = player.bag != null ? player.bag.GetItemCount(rawMeat, -1, -1, false) : 0;
        if (rawMeat.IsEmpty() || toolbeltMeat + backpackMeat < HireItemCount)
        {
            reason = Localization.Get("xuiRebirthDogHireNeedsRawMeat");
            return false;
        }

        string ownerId;
        if (!TryResolveOwnerId(player, out ownerId))
        {
            reason = Localization.Get("xuiRebirthDogAcquireNoOwnerId");
            return false;
        }

        RebirthNpcOrderState previousOrder = state.Order;
        Vector3 previousGuardPosition = state.GuardPosition;
        bool previousHasGuardPosition = state.HasGuardPosition;
        int removedFromToolbelt = 0;
        int removedFromBackpack = 0;
        int remainingToRemove = HireItemCount;
        if (remainingToRemove > 0 && player.inventory != null && toolbeltMeat > 0)
        {
            int requested = Math.Min(remainingToRemove, toolbeltMeat);
            int before = player.inventory.GetItemCount(rawMeat, false, -1, -1);
            player.inventory.DecItem(rawMeat, requested, false);
            int after = player.inventory.GetItemCount(rawMeat, false, -1, -1);
            removedFromToolbelt = Math.Max(0, before - after);
            remainingToRemove -= removedFromToolbelt;
        }
        if (remainingToRemove > 0 && player.bag != null && backpackMeat > 0)
        {
            int requested = Math.Min(remainingToRemove, backpackMeat);
            int before = player.bag.GetItemCount(rawMeat, -1, -1, false);
            player.bag.DecItem(rawMeat, requested, false);
            int after = player.bag.GetItemCount(rawMeat, -1, -1, false);
            removedFromBackpack = Math.Max(0, before - after);
            remainingToRemove -= removedFromBackpack;
        }
        if (remainingToRemove != 0)
        {
            RestoreHireItems(player, rawMeat, removedFromToolbelt + removedFromBackpack);
            reason = Localization.Get("xuiRebirthDogHireNeedsRawMeat");
            return false;
        }
        bool committed = false;
        bool ownerApplied = false;
        bool orderApplied = false;
        try
        {
            RebirthNpcTransactionResult ownerResult = dog.SetRebirthOwner(RebirthNpcOwnershipKind.Player, ownerId);
            if (!ownerResult.Succeeded)
            {
                reason = ownerResult.Error;
                return false;
            }
            ownerApplied = true;
            RebirthNpcTransactionResult orderResult = dog.SetRebirthOrder(RebirthNpcOrderState.Follow, Vector3.zero, false);
            if (!orderResult.Succeeded)
            {
                dog.SetRebirthOwner(RebirthNpcOwnershipKind.None, string.Empty);
                ownerApplied = false;
                reason = orderResult.Error;
                return false;
            }
            orderApplied = true;
            // A successfully hired living NPC must be promoted out of Dynamic spawning
            // or base 3.1 may omit it from the entity save even though the REBIRTH
            // ownership record survives. This mirrors 2.6 companion persistence.
            dog.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);

            SynchronizeLegacyOwnerProjection(dog);

            // 2.6 hire parity: wake the animal and inherit the owner's current
            // Full Control/Hunting and Halt/Resume axes at the moment ownership is
            // established. The modern persistent dog component remains authoritative.
            try
            {
                if (dog.IsSleeping) dog.ConditionalTriggerSleeperWakeUp();
            }
            catch { }
            float playerMode = player.Buffs != null ? player.Buffs.GetCustomVar("varNPCModMode") : 0f;
            float playerHalt = player.Buffs != null ? player.Buffs.GetCustomVar("varNPCModStopAttacking") : 0f;
            RebirthDogStateService.SetCombatMode(state.StableId,
                playerMode == 0f ? RebirthCompanionBehaviorMode.FullControl : RebirthCompanionBehaviorMode.Hunting);
            RebirthDogStateService.SetAttackStopped(state.StableId, playerHalt == 1f);
            SynchronizeDisplayName(dog);

            RebirthNpcAggregatePersistenceStore.Save();
            RebirthNpcStableIdentityStore.Save();
            RebirthDogCapacitySyncService.NotifyOwner(player);
            committed = true;
            reason = Localization.Get("xuiRebirthDogHireSuccess");
            return true;
        }
        finally
        {
            if (!committed)
            {
                // Roll back runtime/aggregate ownership before refunding the authoritative
                // debit. Never refund an unknown committed ownership result: that can mint a
                // free owned dog plus the hire resource.
                bool ownershipRollbackComplete = true;
                try
                {
                    if (orderApplied)
                    {
                        RebirthNpcTransactionResult orderRollback =
                            dog.SetRebirthOrder(previousOrder, previousGuardPosition, previousHasGuardPosition);
                        ownershipRollbackComplete &= orderRollback.Succeeded;
                    }
                    if (ownerApplied)
                    {
                        RebirthNpcTransactionResult ownerRollback =
                            dog.SetRebirthOwner(RebirthNpcOwnershipKind.None, string.Empty);
                        ownershipRollbackComplete &= ownerRollback.Succeeded;
                    }
                    if (ownershipRollbackComplete) ReleaseLegacyOwnerProjection(dog);
                }
                catch (Exception rollbackEx)
                {
                    ownershipRollbackComplete = false;
                    Log.Warning("[REBIRTH Dog] hire ownership rollback failed stableId=" + state.StableId +
                        " error=" + rollbackEx.GetType().Name + ": " + rollbackEx.Message);
                }

                if (ownershipRollbackComplete)
                {
                    if (!RestoreHireItems(player, rawMeat, removedFromToolbelt + removedFromBackpack))
                        Log.Warning("[REBIRTH Dog] hire item compensation is indeterminate stableId=" + state.StableId + ".");
                }
                else
                {
                    try
                    {
                        RebirthNpcAggregatePersistenceStore.Mutate(state.StableId, delegate(RebirthNpcPersistentRecord r)
                        {
                            if (r.Audit == null) r.Audit = new RebirthNpcAuditRecord { RecoveryFlags = new string[0] };
                            List<string> flags = new List<string>(r.Audit.RecoveryFlags ?? new string[0]);
                            if (!flags.Contains("dog-hire-rollback-indeterminate"))
                                flags.Add("dog-hire-rollback-indeterminate");
                            r.Audit.RecoveryFlags = flags.ToArray();
                            r.Audit.LastMutationKind = "dog-hire-rollback-indeterminate";
                            r.Audit.LastMutationWorldTime = world != null ? (long)world.worldTime : 0L;
                        });
                        RebirthNpcAggregatePersistenceStore.Save();
                    }
                    catch { }
                    Log.Warning("[REBIRTH Dog] hire debit retained because ownership rollback is indeterminate stableId=" +
                        state.StableId + ".");
                }
            }
        }
    }

    public static bool TryDeploy(World world, EntityPlayer player, RebirthDogBreedDefinition breed,
        Vector3 position, Quaternion rotation, out EntityRebirthDogCompanion dog, out string reason,
        string capsuleStableId = null, string capsuleNonce = null, ItemValue capsuleItem = null)
    {
        dog = null;
        reason = string.Empty;
        EnsureInitialized();
        if (!IsAuthoritative(world) || player == null || breed == null)
        {
            reason = Localization.Get("xuiRebirthDogDeployFailed");
            return false;
        }
        // The client-side block placement gate is not authoritative on dedicated
        // servers. Repeat the world-height rule here before any dog record or runtime
        // entity can be created. Dog deployments intentionally have no time cooldown.
        if (position.y + 2f >= 254f)
        {
            reason = Localization.Get("xuiRebirthDogDeployInvalidPlacement");
            return false;
        }
        // 2.6 placement parity: ownership limits are a precondition for BOTH a new
        // deployment and a picked-up dog's redeployment. Picked-up dogs themselves are
        // excluded from CountOwnedCompanions, so a player can put the same dog back down
        // unless other companions filled the freed slot while it was in inventory.
        if (!CanAcquire(player, out reason)) return false;

        RebirthNpcStableId existingId;
        bool hasCapsule = !string.IsNullOrWhiteSpace(capsuleStableId) || !string.IsNullOrWhiteSpace(capsuleNonce);
        if (hasCapsule)
        {
            if (!RebirthNpcStableId.TryParse(capsuleStableId, out existingId) || string.IsNullOrWhiteSpace(capsuleNonce))
            { reason = Localization.Get("xuiRebirthDogCapsuleInvalid"); return false; }
            return TryRedeployExisting(world, player, breed, existingId, capsuleNonce, position, rotation, out dog, out reason, capsuleItem);
        }
        int entityClassId = EntityClass.FromString(breed.EntityClassName);
        EntityClass entityClassDefinition;
        if (EntityClass.list == null ||
            !EntityClass.list.TryGetValue(entityClassId, out entityClassDefinition) ||
            entityClassDefinition == null)
        {
            reason = Localization.Get("xuiRebirthDogDeployFailed");
            RebirthDogDeploymentDebug.TraceServer(false,
                "TryDeploy entity class missing name='" + breed.EntityClassName + "' hash=" + entityClassId + ".");
            return false;
        }
        Entity spawned = EntityFactory.CreateEntity(entityClassId, position, rotation.eulerAngles);
        dog = spawned as EntityRebirthDogCompanion;
        if (dog == null) { reason = Localization.Get("xuiRebirthDogDeployFailed"); return false; }

        // Base 3.1 EntityNPC only persists a living NPC when it is not a Dynamic
        // spawner entity. 2.6 explicitly promoted hired/respawned companions to
        // StaticSpawner. Without this, the dog can vanish from the entity save on
        // logout while its durable ownership record remains, producing the exact
        // "not my dog after relog, but I am still at the cap" split-brain failure.
        dog.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
        dog.PrepareNewDogDeployment();
        RebirthNpcStableId createdStableId = dog.RebirthRuntimeState != null
            ? dog.RebirthRuntimeState.StableId : default(RebirthNpcStableId);
        world.SpawnEntityInWorld(dog);
        bool committed = false;
        try
        {
            string ownerId;
            if (!TryResolveOwnerId(player, out ownerId)) { reason = Localization.Get("xuiRebirthDogAcquireNoOwnerId"); return false; }
            RebirthNpcTransactionResult ownerResult = dog.SetRebirthOwner(RebirthNpcOwnershipKind.Player, ownerId);
            if (!ownerResult.Succeeded) { reason = ownerResult.Error; return false; }
            RebirthNpcTransactionResult orderResult = dog.SetRebirthOrder(RebirthNpcOrderState.Follow, Vector3.zero, false);
            if (!orderResult.Succeeded) { reason = orderResult.Error; return false; }
            SynchronizeLegacyOwnerProjection(dog);
            if (RebirthDogStateService.EnsureView(dog) == null)
            { reason = Localization.Get("xuiRebirthDogDeployFailed"); return false; }
            // 'dog' is an out parameter; only the stable local ID enters this callback.
            if (!RebirthNpcAggregatePersistenceStore.Mutate(createdStableId, delegate(RebirthNpcPersistentRecord record)
            {
                if (record.Dog == null) throw new InvalidOperationException("dog state missing after initialization");
                record.Dog.BreedId = breed.BreedId;
                record.Dog.Lifecycle = RebirthDogLifecycleKind.Active;
                record.Dog.Level = Math.Max(1, record.Dog.Level);
                unchecked { record.Dog.Revision++; }
            })) { reason = Localization.Get("xuiRebirthDogDeployFailed"); return false; }
            RebirthDogRuntimeService.ApplyProgressionCvars(dog);
            RebirthDogStateService.CaptureRuntime(dog);
            RebirthNpcAggregatePersistenceStore.Save();
            RebirthNpcStableIdentityStore.Save();
            RebirthDogCapacitySyncService.NotifyOwner(player);
            committed = true;
            reason = Localization.Get("xuiRebirthDogDeploySuccess");
            return true;
        }
        finally
        {
            if (!committed && dog != null)
            {
                int id = dog.entityId;
                world.RemoveEntity(id, EnumRemoveEntityReason.Despawned);
                dog = null;
                if (!createdStableId.IsEmpty)
                {
                    try
                    {
                        RebirthNpcAggregatePersistenceStore.Remove(createdStableId);
                        RebirthNpcAggregatePersistenceStore.Save();
                    }
                    catch (Exception cleanupEx)
                    {
                        Log.Warning("[REBIRTH Dog] failed deployment cleanup remains retryable stableId=" +
                            createdStableId + " error=" + cleanupEx.GetType().Name + ": " + cleanupEx.Message);
                    }
                }
            }
        }
    }

    private static bool TryRedeployExisting(World world, EntityPlayer player, RebirthDogBreedDefinition breed,
        RebirthNpcStableId stableId, string nonce, Vector3 position, Quaternion rotation,
        out EntityRebirthDogCompanion dog, out string reason, ItemValue capsuleItem = null)
    {
        dog = null; reason = string.Empty;
        RebirthNpcPersistentRecordView record; RebirthDogPersistentRecordView dogRecord;
        string ownerId;
        if (!TryResolveOwnerId(player, out ownerId) || !RebirthDogStateService.TryGetView(stableId, out record, out dogRecord) ||
            record.Ownership == null || !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId, ownerId, StringComparison.OrdinalIgnoreCase) ||
            dogRecord.Lifecycle != RebirthDogLifecycleKind.PickedUp || dogRecord.PickupNonceConsumed ||
            dogRecord.PickupRecoveryPending ||
            !string.Equals(dogRecord.PickupNonce, nonce, StringComparison.Ordinal) ||
            !string.Equals(dogRecord.BreedId, breed.BreedId, StringComparison.OrdinalIgnoreCase))
        {
            reason = Localization.Get("xuiRebirthDogCapsuleInvalid");
            return false;
        }
        int existingEntityId;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out existingEntityId) && world.GetEntity(existingEntityId) != null)
        { reason = Localization.Get("xuiRebirthDogAlreadyActive"); return false; }

        int entityClassId = EntityClass.FromString(breed.EntityClassName);
        EntityClass entityClassDefinition;
        if (EntityClass.list == null ||
            !EntityClass.list.TryGetValue(entityClassId, out entityClassDefinition) ||
            entityClassDefinition == null)
        {
            reason = Localization.Get("xuiRebirthDogDeployFailed");
            return false;
        }
        EntityRebirthDogCompanion created = EntityFactory.CreateEntity(entityClassId, position, rotation.eulerAngles) as EntityRebirthDogCompanion;
        if (created == null) { reason = Localization.Get("xuiRebirthDogDeployFailed"); return false; }

        bool hadOrder = record.Order != null;
        string previousOrderState = hadOrder ? record.Order.OrderState : string.Empty;
        string previousOrderTarget = hadOrder ? record.Order.OrderTarget : string.Empty;
        string previousOrderPrevious = hadOrder ? record.Order.PreviousOrderState : string.Empty;
        bool hadTransform = record.Transform != null;
        Vector3? previousAnchorPosition = hadTransform ? record.Transform.AnchorPosition : null;
        float? previousAnchorRotation = hadTransform ? record.Transform.AnchorRotation : null;
        bool hadPresence = record.Presence != null;
        string previousPresenceState = hadPresence ? record.Presence.PresenceState : string.Empty;
        string previousPresenceReason = hadPresence ? record.Presence.TransitionReason : string.Empty;
        Vector3? previousStayPosition = dogRecord.StayPosition;
        bool previousGuardStay = dogRecord.GuardIsStationaryStay;
        RebirthDogLifecycleKind previousLifecycle = dogRecord.Lifecycle;
        bool previousNonceConsumed = dogRecord.PickupNonceConsumed;
        string previousNonce = dogRecord.PickupNonce ?? string.Empty;

        bool spawned = false;
        try
        {
            created.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
            created.PreparePersistedDogDeployment(stableId);
            // OnAdded and native entity distribution both observe this identity.
            // Rebinding after spawn can publish the temporary factory identity to clients.
            if (!RebirthNpcRuntimeRegistry.TryRebindStableId(created.entityId, stableId))
                throw new InvalidOperationException("Stable dog identity could not be rebound before spawn.");
            dog = created;

            // A picked-up dog is being placed at a NEW owner-selected position. Its old Stay/Guard
            // anchor must not survive the capsule round-trip or it will immediately run back to the
            // previous hold point. Reset the durable order BEFORE restoring it onto the new entity.
            RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
            {
                if (r.Order == null) r.Order = new RebirthNpcOrderRecord();
                r.Order.PreviousOrderState = r.Order.OrderState;
                r.Order.OrderState = RebirthNpcOrderState.Follow.ToString();
                r.Order.OrderTarget = string.Empty;
                unchecked { r.Order.OrderRevision++; }

                if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
                r.Dog.StayPosition = null;
                r.Dog.GuardIsStationaryStay = true;
                unchecked { r.Dog.Revision++; }

                if (r.Transform == null) r.Transform = new RebirthNpcTransformRecord();
                r.Transform.AnchorPosition = null;
                r.Transform.AnchorRotation = null;
                unchecked { r.Transform.TransformRevision++; }
            });

            // The general restore guard deliberately excludes PickedUp records.
            // This path already authenticated the capsule nonce and durable owner.
            // Establish both fields before native spawn data or OnAdded broadcasts them.
            RebirthNpcTransactionResult ownerResult = created.SetRebirthOwner(RebirthNpcOwnershipKind.Player, ownerId);
            if (!ownerResult.Succeeded) throw new InvalidOperationException(ownerResult.Error);
            RebirthNpcTransactionResult orderResult = created.SetRebirthOrder(RebirthNpcOrderState.Follow, Vector3.zero, false);
            if (!orderResult.Succeeded) throw new InvalidOperationException(orderResult.Error);
            world.SpawnEntityInWorld(created);
            spawned = true;
            RestorePersistedOwnershipAndOrder(dog);
            RebirthDogRuntimeService.ClearCombat(dog);
            SynchronizeLegacyOwnerProjection(dog);
            ApplyPickupMetadataToPersistentState(stableId, capsuleItem);
            RebirthDogRuntimeService.ApplyProgressionCvars(dog);
            ApplyPickupMetadataToRuntime(dog, capsuleItem);
            if (record.Vitals != null && record.Vitals.CurrentHealth > 0 && (capsuleItem == null || !capsuleItem.HasMetadata("health")))
                dog.Health = Math.Min(dog.GetMaxHealth(), record.Vitals.CurrentHealth);
            RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
            {
                if (r.Dog == null) return;
                r.Dog.Lifecycle = RebirthDogLifecycleKind.Active; r.Dog.PickupNonceConsumed = true; r.Dog.PickupNonce = string.Empty;
                if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
                r.Presence.PresenceState = RebirthNpcPresenceState.Active.ToString();
                unchecked { r.Dog.Revision++; r.Dog.PickupRevision++; }
            });
            RebirthDogStateService.CaptureRuntime(dog);
            RebirthNpcAggregatePersistenceStore.Save();
            RebirthDogChunkObserverService.Synchronize(dog);
            RebirthDogCapacitySyncService.NotifyOwner(player);
            reason = Localization.Get("xuiRebirthDogDeploySuccess");
            return true;
        }
        catch (Exception ex)
        {
            if (spawned || world.GetEntity(created.entityId) == created)
            {
                try { world.RemoveEntity(created.entityId, EnumRemoveEntityReason.Despawned); }
                catch (Exception removeEx)
                {
                    Log.Warning("[REBIRTH Dog] redeploy entity rollback failed stableId=" + stableId +
                        " error=" + removeEx.GetType().Name + ": " + removeEx.Message);
                }
            }
            else
            {
                // Factory initialization registers a runtime projection even if spawn fails.
                RebirthNpcRuntimeRegistry.Unregister(created.entityId, false);
            }
            dog = null;
            try
            {
                RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
                {
                    if (!hadOrder) r.Order = null;
                    else
                    {
                        if (r.Order == null) r.Order = new RebirthNpcOrderRecord();
                        r.Order.OrderState = previousOrderState;
                        r.Order.OrderTarget = previousOrderTarget;
                        r.Order.PreviousOrderState = previousOrderPrevious;
                        unchecked { r.Order.OrderRevision++; }
                    }
                    if (!hadTransform) r.Transform = null;
                    else
                    {
                        if (r.Transform == null) r.Transform = new RebirthNpcTransformRecord();
                        r.Transform.AnchorPosition = previousAnchorPosition;
                        r.Transform.AnchorRotation = previousAnchorRotation;
                        unchecked { r.Transform.TransformRevision++; }
                    }
                    if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
                    r.Dog.StayPosition = previousStayPosition;
                    r.Dog.GuardIsStationaryStay = previousGuardStay;
                    r.Dog.Lifecycle = previousLifecycle;
                    r.Dog.PickupNonceConsumed = previousNonceConsumed;
                    r.Dog.PickupNonce = previousNonce;
                    unchecked { r.Dog.Revision++; r.Dog.PickupRevision++; }
                    if (!hadPresence) r.Presence = null;
                    else
                    {
                        if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
                        r.Presence.PresenceState = previousPresenceState;
                        r.Presence.TransitionReason = previousPresenceReason;
                        unchecked { r.Presence.PresenceRevision++; }
                    }
                });
                RebirthNpcAggregatePersistenceStore.Save();
            }
            catch (Exception rollbackEx)
            {
                Log.Warning("[REBIRTH Dog] redeploy persistence rollback failed stableId=" + stableId +
                    " error=" + rollbackEx.GetType().Name + ": " + rollbackEx.Message);
            }
            reason = Localization.Get("xuiRebirthDogDeployFailed");
            Log.Warning("[REBIRTH Dog] redeploy aborted stableId=" + stableId +
                " error=" + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    public static bool TryPickup(World world, EntityPlayer player, EntityRebirthDogCompanion dog, out string reason)
    {
        reason = string.Empty;
        if (!IsAuthoritative(world) || player == null || dog == null || dog.IsDead() || !IsOwnedBy(dog, player) || dog.RebirthRuntimeState == null)
        { reason = Localization.Get("xuiRebirthDogPickupUnavailable"); return false; }
        if ((dog.position - player.position).sqrMagnitude > MaximumPickupDistance * MaximumPickupDistance)
        { reason = Localization.Get("xuiRebirthDogTooFar"); return false; }
        if (dog.Buffs != null && (dog.Buffs.HasBuff("delayPickup") || dog.Buffs.GetCustomVar("$delayedPickup") > 0f))
        { reason = Localization.Get("xuiRebirthDogPickupUnavailable"); return false; }
        RebirthNpcStableId id = dog.RebirthRuntimeState.StableId;
        RebirthNpcPersistentRecordView pickupAggregate; RebirthDogPersistentRecordView pickupState;
        if (RebirthDogStateService.TryGetView(id, out pickupAggregate, out pickupState) && pickupState != null && pickupState.IsTamedWild)
        { reason = Localization.Get("xuiRebirthWildPickupUnavailable"); return false; }
        if (pickupState != null && pickupState.PickupRecoveryPending &&
            pickupState.Lifecycle == RebirthDogLifecycleKind.PickedUp &&
            !string.IsNullOrEmpty(pickupState.PickupRecoveryNonce))
        {
            return TryCompletePendingPickupRemoval(world, player, dog, id, pickupState.PickupRecoveryNonce, out reason);
        }
        RebirthDogBreedDefinition breed;
        if (!RebirthDogStateService.TryGetBreed(id, out breed))
        { reason = Localization.Get("xuiRebirthDogPickupUnavailable"); return false; }

        // The canonical inventory remains RebirthDogInventoryService. If a native bag
        // edit surface somehow remains open, flush it before the capsule is created.
        if (RebirthDogStorageService.IsOpenServer(dog.entityId))
            RebirthDogStorageService.CaptureNow(dog);
        RebirthDogStateService.CaptureRuntime(dog);
        string nonce = Guid.NewGuid().ToString("N");
        ItemValue capsule = ItemClass.GetItem(breed.PickedUpDeployId, false);
        if (capsule.IsEmpty()) capsule = ItemClass.GetItem(breed.SpawnDeployId, false);
        if (capsule.IsEmpty()) { reason = Localization.Get("xuiRebirthDogPickupUnavailable"); return false; }
        RebirthNpcPersistentRecordView pickupRecord; RebirthDogPersistentRecordView pickupDog;
        RebirthDogStateService.TryGetView(id, out pickupRecord, out pickupDog);
        int pickupKills = pickupDog != null ? pickupDog.KillCount : Math.Max(0, Mathf.RoundToInt(dog.Buffs.GetCustomVar("$varNumKills")));
        int pickupLevel = pickupDog != null ? Math.Max(1, pickupDog.Level) : Math.Max(1, Mathf.RoundToInt(dog.Buffs.GetCustomVar("$FR_NPC_Level")));
        int pickupMiningLevel = pickupDog != null ? Math.Max(0, pickupDog.MiningLevel) : Math.Max(0, Mathf.RoundToInt(dog.Buffs.GetCustomVar("$FR_NPC_MiningLevel")));
        capsule.SetMetadata("RebirthDogStableId", id.ToString());
        capsule.SetMetadata("RebirthDogNonce", nonce);
        capsule.SetMetadata("RebirthDogBreedId", breed.BreedId);
        // Exact 2.6 companion pickup metadata contract.
        capsule.SetMetadata("NPCName", dog.EntityName ?? string.Empty);
        capsule.SetMetadata("health", Math.Max(0, dog.Health));
        capsule.SetMetadata("numKills", pickupKills);
        capsule.SetMetadata("NPCLevel", pickupLevel);
        capsule.SetMetadata("NPCMiningLevel", pickupMiningLevel);
        // 3.1 additive audit metadata. Inventory contents remain canonical in the stable aggregate.
        capsule.SetMetadata("RebirthDogInventorySlots", RebirthDogStateService.InventorySlots);
        capsule.SetMetadata("RebirthDogInventoryRevision", pickupRecord != null && pickupRecord.Inventory != null ? (int)pickupRecord.Inventory.InventoryRevision : 0);
        RebirthDogLifecycleKind previousLifecycle = pickupDog != null ? pickupDog.Lifecycle : RebirthDogLifecycleKind.Active;
        string previousNonce = pickupDog != null ? (pickupDog.PickupNonce ?? string.Empty) : string.Empty;
        bool previousNonceConsumed = pickupDog != null && pickupDog.PickupNonceConsumed;
        string previousPresence = pickupRecord != null && pickupRecord.Presence != null
            ? (pickupRecord.Presence.PresenceState ?? RebirthNpcPresenceState.Active.ToString())
            : RebirthNpcPresenceState.Active.ToString();
        string previousTransition = pickupRecord != null && pickupRecord.Presence != null
            ? (pickupRecord.Presence.TransitionReason ?? string.Empty) : string.Empty;

        // Persist a write-ahead intent before publishing the capsule. If the process exits
        // between any later steps, the canonical dog record advertises an unresolved pickup
        // instead of silently presenting two authoritative representations.
        try
        {
            SetPickupRecoveryIntent(id, breed.BreedId, dog, nonce, "prepared");
            RebirthNpcAggregatePersistenceStore.Save();
        }
        catch (Exception intentEx)
        {
            reason = Localization.Get("xuiRebirthDogPickupUnavailable");
            Log.Warning("[REBIRTH Dog] pickup refused because recovery intent could not be persisted stableId=" +
                id + " error=" + intentEx.GetType().Name + ": " + intentEx.Message);
            return false;
        }

        ItemStack item = new ItemStack(capsule, 1);
        bool added = false;
        try
        {
            added = player.inventory != null && player.inventory.AddItem(item);
            if (!added && player.bag != null)
                added = player.bag.AddItem(item);
        }
        catch (Exception addEx)
        {
            MarkPickupRecoveryStage(id, nonce, "capsule-credit-threw");
            TrySavePickupRecovery("capsule credit exception", id);
            reason = Localization.Get("xuiRebirthDogPickupUnavailable");
            Log.Warning("[REBIRTH Dog] pickup capsule credit threw stableId=" + id +
                " error=" + addEx.GetType().Name + ": " + addEx.Message);
            return false;
        }
        if (!added)
        {
            ClearPickupRecoveryIntent(id, nonce);
            TrySavePickupRecovery("capsule no-room cleanup", id);
            reason = Localization.Get("xuiRebirthDogPickupNoRoom");
            return false;
        }

        try
        {
            RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
            {
                RebirthDogStateService.EnsureCommonRecord(r, breed.BreedId, dog);
                r.Dog.Lifecycle = RebirthDogLifecycleKind.PickedUp;
                r.Dog.PickupNonce = nonce;
                r.Dog.PickupNonceConsumed = false;
                r.Dog.PickupRecoveryPending = true;
                r.Dog.PickupRecoveryNonce = nonce;
                r.Dog.PickupRecoveryStage = "capsule-published";
                r.Dog.PickupRecoveryUtcTicks = DateTime.UtcNow.Ticks;
                unchecked { r.Dog.PickupRevision++; r.Dog.Revision++; }
                if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
                r.Presence.PresenceState = "PickedUp";
                r.Presence.TransitionReason = "dog-pickup";
            });
            dog.SetRebirthPresence(RebirthNpcPresenceState.Suspended);
            RebirthNpcAggregatePersistenceStore.Save();
        }
        catch (Exception ex)
        {
            bool capsuleRemoved = TryRemovePickupCapsule(player, id.ToString(), nonce);
            bool rollbackSaved = false;
            try
            {
                RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
                {
                    if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
                    r.Dog.Lifecycle = previousLifecycle;
                    r.Dog.PickupNonce = previousNonce;
                    r.Dog.PickupNonceConsumed = previousNonceConsumed;
                    r.Dog.PickupRecoveryPending = !capsuleRemoved;
                    r.Dog.PickupRecoveryNonce = capsuleRemoved ? string.Empty : nonce;
                    r.Dog.PickupRecoveryStage = capsuleRemoved ? string.Empty : "rollback-required";
                    r.Dog.PickupRecoveryUtcTicks = capsuleRemoved ? 0L : DateTime.UtcNow.Ticks;
                    if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
                    r.Presence.PresenceState = previousPresence;
                    r.Presence.TransitionReason = previousTransition;
                    unchecked { r.Dog.PickupRevision++; r.Dog.Revision++; }
                });
                RebirthNpcAggregatePersistenceStore.Save();
                rollbackSaved = true;
            }
            catch (Exception rollbackEx)
            {
                // Keep/restore a recovery marker in memory even when the rollback save itself
                // failed. The persistence coordinator retains dirty state for its next retry.
                MarkPickupRecoveryStage(id, nonce, capsuleRemoved ? "rollback-save-required" : "rollback-required");
                Log.Warning("[REBIRTH Dog] pickup persistence rollback failed stableId=" + id +
                    " capsuleRemoved=" + capsuleRemoved + " error=" + rollbackEx.GetType().Name + ": " + rollbackEx.Message);
            }
            try { dog.SetRebirthPresence(RebirthNpcPresenceState.Active); } catch { }
            reason = Localization.Get("xuiRebirthDogPickupUnavailable");
            Log.Warning("[REBIRTH Dog] pickup aborted after persistence failure stableId=" + id +
                " capsuleRemoved=" + capsuleRemoved + " rollbackSaved=" + rollbackSaved +
                " error=" + ex.GetType().Name + ": " + ex.Message);
            return false;
        }

        // The aggregate and capsule now agree on PickedUp. Keep the recovery marker until
        // the live entity is actually removed; a native despawn exception must not erase the
        // evidence needed to reconcile a duplicate live entity after restart/retry.
        int pickedUpEntityId = dog.entityId;
        try
        {
            MarkPickupRecoveryStage(id, nonce, "remove-entity");
            RebirthNpcAggregatePersistenceStore.Save();

            RebirthDogChunkObserverService.Release(id);
            ReleaseLegacyOwnerProjection(dog);
            RebirthDogRuntimeService.Remove(pickedUpEntityId);
            RebirthDogStorageService.Remove(pickedUpEntityId);
            RebirthNpcLifecycle.OnEntityDeactivated(pickedUpEntityId, false);
            world.RemoveEntity(pickedUpEntityId, EnumRemoveEntityReason.Despawned);

            ClearPickupRecoveryIntent(id, nonce);
            RebirthNpcAggregatePersistenceStore.Save();
        }
        catch (Exception removeEx)
        {
            MarkPickupRecoveryStage(id, nonce, "remove-entity-required");
            TrySavePickupRecovery("entity removal failure", id);
            reason = Localization.Get("xuiRebirthDogPickupUnavailable");
            Log.Warning("[REBIRTH Dog] pickup left recoverable entity-removal intent stableId=" + id +
                " entityId=" + pickedUpEntityId + " error=" + removeEx.GetType().Name + ": " + removeEx.Message);
            return false;
        }

        NotifyPickupCompleted(world, player, pickedUpEntityId, id);
        RebirthDogCapacitySyncService.NotifyOwner(player);
        reason = Localization.Get("xuiRebirthDogPickupSuccess");
        return true;
    }

    private static bool TryCompletePendingPickupRemoval(World world, EntityPlayer player,
        EntityRebirthDogCompanion dog, RebirthNpcStableId id, string nonce, out string reason)
    {
        reason = Localization.Get("xuiRebirthDogPickupUnavailable");
        if (world == null || player == null || dog == null) return false;
        int entityId = dog.entityId;
        try
        {
            MarkPickupRecoveryStage(id, nonce, "remove-entity");
            RebirthNpcAggregatePersistenceStore.Save();
            RebirthDogChunkObserverService.Release(id);
            ReleaseLegacyOwnerProjection(dog);
            RebirthDogRuntimeService.Remove(entityId);
            RebirthDogStorageService.Remove(entityId);
            RebirthNpcLifecycle.OnEntityDeactivated(entityId, false);
            world.RemoveEntity(entityId, EnumRemoveEntityReason.Despawned);
            ClearPickupRecoveryIntent(id, nonce);
            RebirthNpcAggregatePersistenceStore.Save();
            NotifyPickupCompleted(world, player, entityId, id);
            RebirthDogCapacitySyncService.NotifyOwner(player);
            reason = Localization.Get("xuiRebirthDogPickupSuccess");
            return true;
        }
        catch (Exception ex)
        {
            MarkPickupRecoveryStage(id, nonce, "remove-entity-required");
            TrySavePickupRecovery("pending entity removal retry", id);
            Log.Warning("[REBIRTH Dog] pending pickup removal retry failed stableId=" + id +
                " entityId=" + entityId + " error=" + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static void SetPickupRecoveryIntent(RebirthNpcStableId id, string breedId,
        EntityRebirthDogCompanion dog, string nonce, string stage)
    {
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            RebirthDogStateService.EnsureCommonRecord(r, breedId, dog);
            r.Dog.PickupRecoveryPending = true;
            r.Dog.PickupRecoveryNonce = nonce ?? string.Empty;
            r.Dog.PickupRecoveryStage = stage ?? string.Empty;
            r.Dog.PickupRecoveryUtcTicks = DateTime.UtcNow.Ticks;
            unchecked { r.Dog.Revision++; }
        });
    }

    private static void MarkPickupRecoveryStage(RebirthNpcStableId id, string nonce, string stage)
    {
        try
        {
            RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
            {
                if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
                if (!r.Dog.PickupRecoveryPending ||
                    !string.Equals(r.Dog.PickupRecoveryNonce ?? string.Empty, nonce ?? string.Empty, StringComparison.Ordinal))
                {
                    r.Dog.PickupRecoveryPending = true;
                    r.Dog.PickupRecoveryNonce = nonce ?? string.Empty;
                }
                r.Dog.PickupRecoveryStage = stage ?? string.Empty;
                r.Dog.PickupRecoveryUtcTicks = DateTime.UtcNow.Ticks;
                unchecked { r.Dog.Revision++; }
            });
        }
        catch { }
    }

    private static void ClearPickupRecoveryIntent(RebirthNpcStableId id, string nonce)
    {
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (r.Dog == null) return;
            if (!string.IsNullOrEmpty(nonce) &&
                !string.Equals(r.Dog.PickupRecoveryNonce ?? string.Empty, nonce, StringComparison.Ordinal))
                return;
            r.Dog.PickupRecoveryPending = false;
            r.Dog.PickupRecoveryNonce = string.Empty;
            r.Dog.PickupRecoveryStage = string.Empty;
            r.Dog.PickupRecoveryUtcTicks = 0L;
            unchecked { r.Dog.Revision++; }
        });
    }

    private static void TrySavePickupRecovery(string context, RebirthNpcStableId id)
    {
        try { RebirthNpcAggregatePersistenceStore.Save(); }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Dog] pickup recovery save deferred stableId=" + id +
                " context=" + context + " error=" + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TryRemovePickupCapsule(EntityPlayer player, string stableId, string nonce)
    {
        if (player == null || string.IsNullOrEmpty(stableId) || string.IsNullOrEmpty(nonce)) return false;
        if (player.inventory != null)
        {
            int count = player.inventory.Length;
            for (int i = 0; i < count; i++)
            {
                ItemStack stack = player.inventory.GetItemStack(i);
                if (!MatchesPickupCapsule(stack, stableId, nonce)) continue;
                ItemStack after = stack.Clone(); after.count--;
                player.inventory.SetItem(i, after.count > 0 ? after : ItemStack.Empty.Clone());
                return true;
            }
        }
        if (player.bag != null)
        {
            ItemStack[] slots = player.bag.ItemGrid.items;
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                ItemStack stack = slots[i];
                if (!MatchesPickupCapsule(stack, stableId, nonce)) continue;
                ItemStack after = stack.Clone(); after.count--;
                player.bag.SetSlot(i, after.count > 0 ? after : ItemStack.Empty.Clone());
                return true;
            }
        }
        return false;
    }

    private static bool MatchesPickupCapsule(ItemStack stack, string stableId, string nonce)
    {
        if (stack == null || stack.IsEmpty() || stack.itemValue == null) return false;
        string candidateStableId = string.Empty;
        string candidateNonce = string.Empty;
        stack.itemValue.TryGetMetadata("RebirthDogStableId", out candidateStableId);
        stack.itemValue.TryGetMetadata("RebirthDogNonce", out candidateNonce);
        return string.Equals(candidateStableId, stableId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidateNonce, nonce, StringComparison.Ordinal);
    }

    private static void NotifyPickupCompleted(
        World world, EntityPlayer player, int dogEntityId, RebirthNpcStableId stableId)
    {
        if (world == null || player == null) return;
        EntityPlayerLocal local = player as EntityPlayerLocal;
        if (local != null && world.GetPrimaryPlayerId() == player.entityId)
        {
            try { Audio.Manager.PlayInsidePlayerHead("item_pickup"); } catch { }
            RebirthDogNavigationMarkerService.Remove(stableId.ToString());
            return;
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer) return;
        connection.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthDogPickupFeedback>()
                .Setup(player.entityId, dogEntityId, stableId.ToString()),
            _attachedToEntityId: player.entityId);
    }

    private static void ApplyPickupMetadataToPersistentState(RebirthNpcStableId stableId, ItemValue capsule)
    {
        if (capsule == null) return;
        int kills, level, miningLevel, health; string name;
        bool hasKills = capsule.TryGetMetadata("numKills", out kills);
        bool hasLevel = capsule.TryGetMetadata("NPCLevel", out level);
        bool hasMining = capsule.TryGetMetadata("NPCMiningLevel", out miningLevel);
        bool hasHealth = capsule.TryGetMetadata("health", out health);
        bool hasName = capsule.TryGetMetadata("NPCName", out name);
        RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
        {
            if (!RebirthDogStateService.IsDog(r)) return;
            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            if (hasKills) r.Dog.KillCount = Math.Max(0, kills);
            if (hasLevel) r.Dog.Level = Mathf.Clamp(level, 1, 10);
            if (hasMining) r.Dog.MiningLevel = Math.Max(0, miningLevel);
            if (hasHealth)
            {
                if (r.Vitals == null) r.Vitals = new RebirthNpcVitalStateRecord { ActivePersistentBuffSet = new string[0] };
                r.Vitals.CurrentHealth = Math.Max(0, health);
            }
            if (hasName && !string.IsNullOrWhiteSpace(name))
            {
                if (r.Identity == null) r.Identity = new RebirthNpcIdentityRecord { StableNpcId = stableId };
                r.Identity.GeneratedOrAssignedDisplayName = name.Trim();
                unchecked { r.Identity.NameRevision++; }
            }
            unchecked { r.Dog.Revision++; }
        });
    }

    private static void ApplyPickupMetadataToRuntime(EntityRebirthDogCompanion dog, ItemValue capsule)
    {
        if (dog == null || capsule == null) return;
        string name; int health;
        if (capsule.TryGetMetadata("NPCName", out name) && !string.IsNullOrWhiteSpace(name)) dog.SetEntityName(name.Trim());
        if (capsule.TryGetMetadata("health", out health))
        {
            dog.Buffs.SetCustomVar("$tempHealth", Math.Max(1, health));
            // 2.6 deliberately delayed the final Health write until after the
            // level/difficulty MaxHealth effects had recomputed. Preserve that
            // ordering so a high-level injured dog cannot be clamped to base health
            // during the placement frame.
            if (BuffManager.GetBuff("AdjustNPCStats") != null)
                dog.Buffs.AddBuff("AdjustNPCStats");
            else
                dog.Health = Math.Min(dog.GetMaxHealth(), Math.Max(1, health));
        }
    }

    /// <summary>
    /// Intercepts lethal damage for a hired dog before EntityAlive applies the fatal health
    /// subtraction. This is the 3.1 equivalent of the 2.6 non-terminal companion death path.
    /// </summary>
    public static bool TryInterceptDamageResponse(EntityRebirthDogCompanion dog, DamageResponse damageResponse)
    {
        if (dog == null || dog.RebirthRuntimeState == null) return false;

        RebirthNpcPersistentRecordView record;
        RebirthDogPersistentRecordView dogRecord;
        if (!RebirthDogStateService.TryGetView(dog.RebirthRuntimeState.StableId, out record, out dogRecord) || dogRecord == null)
            return false;
        // A dog can receive damage before its one-second owner maintenance after load.
        // Rehydrate only its own non-terminal durable owner before deciding it is wild.
        ReconcileRuntimeOwnerFromPersistentRecord(dog);
        if (dog.RebirthRuntimeState.OwnershipKind != RebirthNpcOwnershipKind.Player ||
            string.IsNullOrWhiteSpace(dog.RebirthRuntimeState.OwnerId)) return false;

        if (dogRecord.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn)
        {
            MaintainAwaitingRespawnProjection(dog);
            return true;
        }
        if (dogRecord.Lifecycle != RebirthDogLifecycleKind.Active) return false;

        bool lethal = damageResponse.Fatal || damageResponse.Strength >= Math.Max(1, dog.Health);
        if (!lethal) return false;
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return false;

        return EnterAwaitingRespawn(dog, "lethal-damage");
    }

    /// <summary>
    /// Defensive parity path for any caller that invokes Kill directly instead of DamageEntity.
    /// </summary>
    public static bool TryInterceptDirectKill(EntityRebirthDogCompanion dog, DamageResponse damageResponse)
    {
        if (dog == null || dog.RebirthRuntimeState == null) return false;
        RebirthNpcPersistentRecordView record;
        RebirthDogPersistentRecordView dogRecord;
        if (!RebirthDogStateService.TryGetView(dog.RebirthRuntimeState.StableId, out record, out dogRecord) || dogRecord == null)
            return false;
        // A dog can receive damage before its one-second owner maintenance after load.
        // Rehydrate only its own non-terminal durable owner before deciding it is wild.
        ReconcileRuntimeOwnerFromPersistentRecord(dog);
        if (dog.RebirthRuntimeState.OwnershipKind != RebirthNpcOwnershipKind.Player ||
            string.IsNullOrWhiteSpace(dog.RebirthRuntimeState.OwnerId)) return false;

        if (dogRecord.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn)
        {
            MaintainAwaitingRespawnProjection(dog);
            return true;
        }
        if (dogRecord.Lifecycle != RebirthDogLifecycleKind.Active ||
            !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return false;

        return EnterAwaitingRespawn(dog, "direct-kill");
    }

    private static bool EnterAwaitingRespawn(EntityRebirthDogCompanion dog, string reason)
    {
        if (dog == null || dog.RebirthRuntimeState == null ||
            !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return false;

        RebirthNpcStableId id = dog.RebirthRuntimeState.StableId;
        RebirthNpcPersistentRecordView existing;
        RebirthDogPersistentRecordView existingDog;
        if (!RebirthDogStateService.TryGetView(id, out existing, out existingDog) || existingDog == null ||
            existingDog.Lifecycle == RebirthDogLifecycleKind.PickedUp ||
            existingDog.Lifecycle == RebirthDogLifecycleKind.Removed) return false;
        if (existingDog.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn)
        {
            MaintainAwaitingRespawnProjection(dog);
            return true;
        }

        EntityPlayer capacityOwner = RebirthDogRuntimeService.ResolveOwnerPublic(dog.world, dog.RebirthRuntimeState.OwnerId);
        Vector3 deathPosition = dog.position;
        float deathYaw = dog.rotation.y;
        long now = DateTime.UtcNow.Ticks;

        RebirthDogStateService.CaptureRuntime(dog);
        try { dog.SetRebirthOrder(RebirthNpcOrderState.Stay, Vector3.zero, false); } catch { }
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            RebirthDogStateService.EnsureCommonRecord(r, RebirthDogStateService.ResolveBreedId(dog), dog);
            r.Dog.Lifecycle = RebirthDogLifecycleKind.AwaitingRespawn;
            r.Dog.RespawnFallback = deathPosition;
            r.Dog.StayPosition = deathPosition;
            unchecked { r.Dog.Revision++; }

            if (r.Order == null) r.Order = new RebirthNpcOrderRecord();
            r.Order.PreviousOrderState = r.Order.OrderState;
            r.Order.OrderState = RebirthNpcOrderState.Stay.ToString();
            r.Order.OrderTarget = string.Empty;
            unchecked { r.Order.OrderRevision++; }

            if (r.Respawn == null) r.Respawn = new RebirthNpcRespawnRecord();
            r.Respawn.RespawnPolicyId = r.Respawn.PreferredAnchorPosition.HasValue
                ? "explicit-companion-point"
                : "owner-bedroll-death-fallback";
            r.Respawn.RespawnState = "AwaitingRespawn";
            r.Respawn.DeathWorldTime = now;
            r.Respawn.FallbackPosition = deathPosition;
            r.Respawn.RetainedInventoryPolicy = "retain-all";
            unchecked { r.Respawn.RespawnRevision++; }

            if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
            r.Presence.PresenceState = RebirthNpcPresenceState.AwaitingRespawn.ToString();
            r.Presence.TransitionReason = "dog-death-pending-park";
            r.Presence.TransitionStartedWorldTime = now;
            unchecked { r.Presence.PresenceRevision++; }

            if (r.Transform == null) r.Transform = new RebirthNpcTransformRecord();
            r.Transform.WorldPosition = deathPosition;
            r.Transform.RotationYaw = deathYaw;
            r.Transform.LastSafePosition = deathPosition;
            r.Transform.LastSafePositionWorldTime = now;
            unchecked { r.Transform.TransformRevision++; }

            if (r.Vitals == null) r.Vitals = new RebirthNpcVitalStateRecord();
            r.Vitals.CurrentHealth = 1;
            r.Vitals.MaximumHealthAtSave = dog.GetMaxHealth();
            r.Vitals.DeathOrIncapacitationState = "awaiting-respawn";
            unchecked { r.Vitals.VitalsRevision++; }
        });

        // Keep the SAME live dog projection and durable owner mapping. 2.6 did not remove
        // the hired NPC on death; it parked it off-duty so Report for Duty could reactivate it.
        dog.Health = 1;
        dog.SetRebirthPresence(RebirthNpcPresenceState.AwaitingRespawn);
        try { dog.Buffs.SetCustomVar("$FR_NPC_Respawn", 1f); } catch { }
        dog.SetIgnoredByAI(true);
        RebirthDogRuntimeService.ClearCombat(dog);
        try { dog.motion = Vector3.zero; } catch { }
        try { if (!dog.IsSleeping) dog.TriggerSleeperPose(0); } catch { }
        SynchronizeLegacyOwnerProjection(dog);
        EnsureNavigationMarker(dog);
        RebirthNpcAggregatePersistenceStore.Save();
        RebirthDogChunkObserverService.Release(id);
        RebirthDogCapacitySyncService.NotifyOwner(capacityOwner);
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Dog] non-terminal death entity=" + dog.entityId + " stableId=" + id +
                " reason=" + (reason ?? string.Empty) + " awaitingRespawn=true"); }
        return true;
    }

    /// <summary>
    /// Maintains the 2.6 off-duty projection and performs its delayed park move. Priority:
    /// explicit dog respawn point, owner bed/bedroll (+1 Y), otherwise exact death location.
    /// </summary>
    public static void MaintainAwaitingRespawnProjection(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null ||
            !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

        RebirthNpcPersistentRecordView record;
        RebirthDogPersistentRecordView dogRecord;
        if (!RebirthDogStateService.TryGetView(dog.RebirthRuntimeState.StableId, out record, out dogRecord) ||
            record == null || dogRecord == null || dogRecord.Lifecycle != RebirthDogLifecycleKind.AwaitingRespawn) return;

        // Do not reset Health here. The 2.6 off-duty companion can recover gradually
        // while parked on the owner's active bed/bedroll spawn point.
        dog.SetRebirthPresence(RebirthNpcPresenceState.AwaitingRespawn);
        try { dog.Buffs.SetCustomVar("$FR_NPC_Respawn", 1f); } catch { }
        dog.SetIgnoredByAI(true);
        RebirthDogRuntimeService.ClearCombat(dog);
        try { dog.motion = Vector3.zero; } catch { }
        try { if (!dog.IsSleeping) dog.TriggerSleeperPose(0); } catch { }
        SynchronizeLegacyOwnerProjection(dog);
        EnsureNavigationMarker(dog);

        bool alreadyParked = record.Presence != null &&
            string.Equals(record.Presence.TransitionReason, "dog-death-parked", StringComparison.OrdinalIgnoreCase);
        long deathTicks = record.Respawn != null ? (record.Respawn.DeathWorldTime ?? 0L) : 0L;
        if (deathTicks > 0L && DateTime.UtcNow.Ticks - deathTicks < TimeSpan.FromSeconds(4).Ticks) return;

        Vector3 parkPosition;
        float parkYaw;
        string parkSource;
        ResolveAwaitingRespawnPosition(dog.world, dog, record, dogRecord, out parkPosition, out parkYaw, out parkSource);

        // RebirthNpcAggregatePersistenceStore.Save() captures runtime envelopes and may
        // replace Presence.TransitionReason with "runtime-capture". v284 therefore lost
        // the persisted "dog-death-parked" sentinel immediately after saving, causing
        // this method to SetPosition + Save + Log again every live update. Physical
        // proximity is the authoritative idempotence check and also handles a moved
        // respawn anchor correctly.
        bool physicallyParked = (dog.position - parkPosition).sqrMagnitude <= 0.5625f; // 0.75 m
        if (alreadyParked || physicallyParked) return;
        try
        {
            dog.SetPosition(parkPosition, true);
            dog.position = parkPosition;
            dog.SetRotation(new Vector3(0f, parkYaw, 0f));
            if (dog.getNavigator() != null) dog.getNavigator().clearPath();
            if (dog.moveHelper != null) dog.moveHelper.Stop();
            dog.motion = Vector3.zero;
        }
        catch { }

        RebirthNpcStableId stableId = dog.RebirthRuntimeState.StableId;
        RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
        {
            if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
            r.Presence.PresenceState = RebirthNpcPresenceState.AwaitingRespawn.ToString();
            r.Presence.TransitionReason = "dog-death-parked";
            unchecked { r.Presence.PresenceRevision++; }
            if (r.Transform == null) r.Transform = new RebirthNpcTransformRecord();
            r.Transform.WorldPosition = parkPosition;
            r.Transform.RotationYaw = parkYaw;
            r.Transform.LastSafePosition = parkPosition;
            r.Transform.LastSafePositionWorldTime = DateTime.UtcNow.Ticks;
            unchecked { r.Transform.TransformRevision++; }
            if (r.Dog != null) r.Dog.RespawnFallback = parkPosition;
        });
        RebirthNpcAggregatePersistenceStore.Save();
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Dog] parked awaiting-respawn entity=" + dog.entityId + " stableId=" + stableId +
                " source=" + parkSource + " pos=" + parkPosition); }
    }

    private static void ResolveAwaitingRespawnPosition(
        World world,
        EntityRebirthDogCompanion dog,
        RebirthNpcPersistentRecordView record,
        RebirthDogPersistentRecordView dogRecord,
        out Vector3 position,
        out float yaw,
        out string source)
    {
        position = dog != null ? dog.position : Vector3.zero;
        yaw = dog != null ? dog.rotation.y : 0f;
        source = "current-location";

        if (record != null && record.Respawn != null && record.Respawn.PreferredAnchorPosition.HasValue)
        {
            position = record.Respawn.PreferredAnchorPosition.Value;
            if (record.Respawn.PreferredAnchorRotation.HasValue) yaw = record.Respawn.PreferredAnchorRotation.Value;
            source = "explicit-companion-point";
            return;
        }

        EntityPlayer owner = dog != null && dog.RebirthRuntimeState != null
            ? RebirthDogRuntimeService.ResolveOwnerPublic(world, dog.RebirthRuntimeState.OwnerId)
            : null;
        if (owner != null && owner.SpawnPoints != null && owner.SpawnPoints.Count > 0)
        {
            // Exact 2.6 behavior: +1 Y keeps the dog on top of a bed that acts as a bedroll
            // instead of embedding the companion inside the bed block/model.
            position = (Vector3)owner.SpawnPoints[0] + Vector3.up;
            yaw = owner.rotation.y;
            source = "owner-bedroll-plus-one";
            return;
        }

        if (record != null && record.Respawn != null && record.Respawn.FallbackPosition.HasValue)
        {
            position = record.Respawn.FallbackPosition.Value;
            source = "death-location";
            return;
        }
        if (dogRecord != null && dogRecord.RespawnFallback.HasValue)
        {
            position = dogRecord.RespawnFallback.Value;
            source = "dog-death-fallback";
        }
    }

    private static void ReactivateAwaitingRespawnProjection(EntityRebirthDogCompanion dog, EntityPlayer owner)
    {
        if (dog == null || dog.RebirthRuntimeState == null) return;
        RebirthNpcStableId stableId = dog.RebirthRuntimeState.StableId;
        dog.SetIgnoredByAI(false);
        try { dog.Buffs.SetCustomVar("$FR_NPC_Respawn", 0f); } catch { }
        try
        {
            if (dog.IsSleeping) dog.ConditionalTriggerSleeperWakeUp();
        }
        catch { }
        // Report for Duty changes state; it is not a heal. Keep the health the companion
        // earned while off-duty (or the initial 1 HP if it was not resting on the spawn bed).
        dog.Health = Mathf.Clamp(Math.Max(1, dog.Health), 1, dog.GetMaxHealth());
        dog.SetRebirthPresence(RebirthNpcPresenceState.Active);
        RebirthDogRuntimeService.ClearCombat(dog);
        SynchronizeLegacyOwnerProjection(dog);
        EnsureNavigationMarker(dog);

        int health = dog.Health;
        int maxHealth = dog.GetMaxHealth();
        RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
        {
            if (r.Dog != null)
            {
                r.Dog.Lifecycle = RebirthDogLifecycleKind.Active;
                r.Dog.RespawnFallback = dog.position;
                unchecked { r.Dog.Revision++; }
            }
            if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
            r.Presence.PresenceState = RebirthNpcPresenceState.Active.ToString();
            r.Presence.TransitionReason = "report-for-duty";
            unchecked { r.Presence.PresenceRevision++; }
            if (r.Respawn != null)
            {
                r.Respawn.RespawnState = "Active";
                r.Respawn.AttemptCount++;
                unchecked { r.Respawn.RespawnRevision++; }
            }
            if (r.Vitals == null) r.Vitals = new RebirthNpcVitalStateRecord();
            r.Vitals.CurrentHealth = health;
            r.Vitals.MaximumHealthAtSave = maxHealth;
            r.Vitals.DeathOrIncapacitationState = "alive";
            unchecked { r.Vitals.VitalsRevision++; }
        });
        RebirthNpcAggregatePersistenceStore.Save();
        RebirthDogCapacitySyncService.NotifyOwner(owner);
    }

    public static bool HandleDeath(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return false;
        ReconcileRuntimeOwnerFromPersistentRecord(dog);
        if (dog.RebirthRuntimeState.OwnershipKind != RebirthNpcOwnershipKind.Player ||
            string.IsNullOrWhiteSpace(dog.RebirthRuntimeState.OwnerId)) return false;
        return EnterAwaitingRespawn(dog, "entity-death-fallback");
    }

    /// <summary>
    /// One-shot repair for dog records terminally removed by the incorrect v230-v234
    /// administrative killall handler. This intentionally accepts ONLY records carrying the
    /// dog-admin-killall removal marker; legitimate PickedUp/AdminRemove records are refused.
    /// The recovered dog returns to AwaitingRespawn and must still be explicitly Reported for Duty.
    /// </summary>
    public static bool TryRecoverAdministrativeKillAllRecord(EntityPlayer player, RebirthNpcStableId stableId, out string reason)
    {
        reason = string.Empty;
        if (player == null || stableId.IsEmpty || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        { reason = "player/server unavailable"; return false; }

        string ownerId;
        if (!TryResolveOwnerId(player, out ownerId) || string.IsNullOrWhiteSpace(ownerId))
        { reason = "persistent owner id unavailable"; return false; }

        int currentDogs, currentTotal, currentCap;
        if (TryGetOwnershipCounts(player, out currentDogs, out currentTotal, out currentCap) && currentTotal >= currentCap)
        { reason = "companion cap is full; pick up/remove the replacement companion first"; return false; }

        RebirthNpcPersistentRecordView record;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record) || record == null ||
            record.Identity == null || record.Profile == null ||
            !string.Equals(record.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase) ||
            record.Lifecycle == null ||
            !string.Equals(record.Lifecycle.RemovalReason ?? string.Empty, "dog-admin-killall", StringComparison.OrdinalIgnoreCase))
        { reason = "record is not a recoverable v230-v234 killall dog"; return false; }

        int liveEntityId;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out liveEntityId) && player.world != null && player.world.GetEntity(liveEntityId) != null)
        { reason = "a live projection already exists"; return false; }

        Vector3 fallback = record.Transform != null ? record.Transform.WorldPosition : Vector3.zero;
        long now = DateTime.UtcNow.Ticks;
        RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
        {
            if (r == null) return;
            if (r.Lifecycle == null) r.Lifecycle = new RebirthNpcLifecycleRecord();
            r.Lifecycle.DismissalState = string.Empty;
            r.Lifecycle.RemovalReason = string.Empty;
            r.Lifecycle.TombstoneState = false;
            r.Lifecycle.TombstoneWorldTime = null;
            unchecked { r.Lifecycle.LifecycleRevision++; }

            if (r.Ownership == null) r.Ownership = new RebirthNpcOwnershipRecord();
            r.Ownership.OwnershipState = RebirthNpcOwnershipKind.Player.ToString();
            r.Ownership.OwnerPlatformIdOrPersistentPlayerId = ownerId;
            r.Ownership.DismissedWorldTime = null;
            if (string.IsNullOrWhiteSpace(r.Ownership.PermissionPolicyId)) r.Ownership.PermissionPolicyId = "rebirth.default";
            if (string.IsNullOrWhiteSpace(r.Ownership.PartyAccessMode)) r.Ownership.PartyAccessMode = "owner-and-party";
            unchecked { r.Ownership.OwnerRevision++; }

            if (r.Order == null) r.Order = new RebirthNpcOrderRecord();
            r.Order.PreviousOrderState = r.Order.OrderState;
            r.Order.OrderState = RebirthNpcOrderState.Stay.ToString();
            r.Order.OrderTarget = string.Empty;
            unchecked { r.Order.OrderRevision++; }

            if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
            r.Presence.PresenceState = RebirthNpcPresenceState.AwaitingRespawn.ToString();
            r.Presence.TransitionReason = "recover-v230-v234-killall";
            r.Presence.TransitionStartedWorldTime = now;
            unchecked { r.Presence.PresenceRevision++; }

            if (r.Respawn == null) r.Respawn = new RebirthNpcRespawnRecord();
            r.Respawn.RespawnPolicyId = r.Respawn.PreferredAnchorPosition.HasValue
                ? "explicit-companion-point"
                : "owner-bedroll-death-fallback";
            r.Respawn.RespawnState = "AwaitingRespawn";
            r.Respawn.DeathWorldTime = now;
            if (!r.Respawn.FallbackPosition.HasValue) r.Respawn.FallbackPosition = fallback;
            r.Respawn.RetainedInventoryPolicy = "retain-all";
            unchecked { r.Respawn.RespawnRevision++; }

            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            r.Dog.Lifecycle = RebirthDogLifecycleKind.AwaitingRespawn;
            if (!r.Dog.RespawnFallback.HasValue) r.Dog.RespawnFallback = fallback;
            unchecked { r.Dog.Revision++; }

            if (r.Vitals == null) r.Vitals = new RebirthNpcVitalStateRecord { ActivePersistentBuffSet = new string[0] };
            r.Vitals.CurrentHealth = 1;
            r.Vitals.DeathOrIncapacitationState = "awaiting-respawn";
            unchecked { r.Vitals.VitalsRevision++; r.AggregateRevision++; }
        });

        RebirthNpcAggregatePersistenceStore.Save();
        RebirthDogChunkObserverService.Release(stableId);
        RebirthDogCapacitySyncService.NotifyOwner(player);
        reason = "restored to AwaitingRespawn; use Report for Duty from the Companions window";
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Dog] recovered legacy killall record stableId=" + stableId + " owner=" + ownerId); }
        return true;
    }


    public static bool TryDismissMissingRecord(EntityPlayer player, RebirthNpcStableId stableId, out string reason)
    {
        reason = string.Empty;
        if (player == null || stableId.IsEmpty || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        {
            reason = Localization.Get("xuiRebirthDogDismissUnavailable");
            return false;
        }

        string ownerId;
        if (!TryResolveOwnerId(player, out ownerId) || string.IsNullOrWhiteSpace(ownerId))
        {
            reason = Localization.Get("xuiRebirthDogDismissUnavailable");
            return false;
        }

        RebirthNpcPersistentRecordView record;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record) || record == null ||
            !RebirthDogStateService.IsDog(record) || record.Dog == null || record.Ownership == null ||
            !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty,
                ownerId, StringComparison.OrdinalIgnoreCase) ||
            record.Dog.Lifecycle != RebirthDogLifecycleKind.Active)
        {
            reason = Localization.Get("xuiRebirthDogDismissUnavailable");
            return false;
        }

        // "Missing" is a recovery state, so do not trust only the StableId runtime map
        // before relinquishing ownership. A live dog can exist in the loaded world while
        // that recovery index is stale (the same condition that previously produced a false
        // Missing status in the Companions window).
        if (player.world != null && player.world.Entities != null && player.world.Entities.list != null)
        {
            List<Entity> loaded = player.world.Entities.list;
            for (int i = 0; i < loaded.Count; i++)
            {
                EntityRebirthDogCompanion liveDog = loaded[i] as EntityRebirthDogCompanion;
                if (liveDog != null && !liveDog.IsDead() && liveDog.RebirthRuntimeState != null &&
                    liveDog.RebirthRuntimeState.StableId == stableId)
                {
                    reason = Localization.Get("xuiRebirthDogDismissUnavailable");
                    return false;
                }
            }
        }

        int liveEntityId;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out liveEntityId) &&
            player.world != null && player.world.GetEntity(liveEntityId) != null)
        {
            reason = Localization.Get("xuiRebirthDogDismissUnavailable");
            return false;
        }

        long now = player.world != null ? unchecked((long)player.world.worldTime) : 0L;
        bool changed = RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
        {
            if (r == null || r.Ownership == null || r.Dog == null) return;

            // Match normal Dismiss semantics as closely as possible without a live entity:
            // relinquish ownership, clear the positional order and owner-scoped combat state,
            // but DO NOT tombstone/delete the stable identity. If an unloaded dog later
            // materializes from its chunk, it comes back unowned rather than resurrecting
            // the previous ownership.
            r.Ownership.OwnershipState = RebirthNpcOwnershipKind.None.ToString();
            r.Ownership.OwnerPlatformIdOrPersistentPlayerId = string.Empty;
            r.Ownership.DismissedWorldTime = now;
            unchecked { r.Ownership.OwnerRevision++; }

            if (r.Order == null) r.Order = new RebirthNpcOrderRecord();
            r.Order.PreviousOrderState = r.Order.OrderState;
            r.Order.OrderState = RebirthNpcOrderState.None.ToString();
            r.Order.OrderTarget = string.Empty;
            unchecked { r.Order.OrderRevision++; }

            if (r.Transform != null)
            {
                r.Transform.AnchorPosition = null;
                r.Transform.AnchorRotation = null;
                unchecked { r.Transform.TransformRevision++; }
            }

            if (r.Lifecycle == null) r.Lifecycle = new RebirthNpcLifecycleRecord();
            r.Lifecycle.DismissalState = "dismissed";
            r.Lifecycle.RemovalReason = "dog-missing-dismiss";
            r.Lifecycle.TombstoneState = false;
            r.Lifecycle.TombstoneWorldTime = null;
            unchecked { r.Lifecycle.LifecycleRevision++; }

            r.Dog.CombatMode = RebirthCompanionBehaviorMode.FullControl;
            r.Dog.AttackStopped = false;
            unchecked { r.Dog.Revision++; r.AggregateRevision++; }
        });

        if (!changed)
        {
            reason = Localization.Get("xuiRebirthDogDismissUnavailable");
            return false;
        }

        RebirthNpcAggregatePersistenceStore.Save();
        RebirthNpcStableIdentityStore.Save();
        RebirthDogChunkObserverService.Release(stableId);
        RebirthDogCapacitySyncService.NotifyOwner(player);
        reason = Localization.Get("xuiRebirthDogDismissSuccess");
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Dog] dismissed missing dog stableId=" + stableId + " formerOwner=" + ownerId); }
        return true;
    }


    public static bool TryForgetMissingRecord(EntityPlayer player, RebirthNpcStableId stableId, out string reason)
    {
        reason = string.Empty;
        if (player == null || stableId.IsEmpty || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        { reason = "player/server unavailable"; return false; }

        string ownerId;
        if (!TryResolveOwnerId(player, out ownerId) || string.IsNullOrWhiteSpace(ownerId))
        { reason = "persistent owner id unavailable"; return false; }

        RebirthNpcPersistentRecordView record;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record) || record == null ||
            !RebirthDogStateService.IsDog(record) || record.Dog == null || record.Ownership == null)
        { reason = "dog record was not found"; return false; }

        if (!string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
        { reason = "dog record is not owned by this player"; return false; }

        if (record.Dog.Lifecycle != RebirthDogLifecycleKind.Active)
        { reason = "only an Active record with no live projection can be forgotten; current lifecycle=" + record.Dog.Lifecycle; return false; }

        int entityId;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out entityId) &&
            player.world != null && player.world.GetEntity(entityId) != null)
        { reason = "a live projection still exists; use the normal companion actions instead"; return false; }

        if (!RebirthNpcAggregatePersistenceStore.Remove(stableId))
        { reason = "aggregate record could not be removed"; return false; }

        RebirthDogChunkObserverService.Release(stableId);
        string identityReason;
        bool identityRemoved = RebirthNpcStableIdentityStore.TryForgetPending(stableId, out identityReason);
        RebirthNpcAggregatePersistenceStore.Save();
        RebirthDogCapacitySyncService.NotifyOwner(player);

        reason = "forgot missing Active record stableId=" + stableId
            + (identityRemoved ? " and removed its pending stable-identity projection" : "; identity store: " + identityReason);
        Log.Warning("[REBIRTH Dog] ADMIN " + reason);
        return true;
    }

    // v230-v234 carried a special administrative killall path that terminally removed
    // owned dogs. That behavior is intentionally retired: killall now follows the same
    // non-terminal 2.6 hired-dog death contract as every other lethal damage source.

    public static bool TryDismiss(World world, EntityPlayer player, EntityRebirthDogCompanion dog, out string reason)
    {
        reason = string.Empty;
        if (!IsAuthoritative(world) || player == null || dog == null || dog.IsDead() || dog.RebirthRuntimeState == null)
        {
            reason = Localization.Get("xuiRebirthDogDismissUnavailable");
            return false;
        }
        if (Vector3.Distance(player.position, dog.position) > MaximumHireDistance || !IsOwnedBy(dog, player))
        {
            reason = Localization.Get("xuiRebirthDogDismissUnavailable");
            return false;
        }

        RebirthNpcStableId id = dog.RebirthRuntimeState.StableId;
        RebirthNpcTransactionResult orderResult = dog.SetRebirthOrder(RebirthNpcOrderState.None, Vector3.zero, false);
        if (!orderResult.Succeeded)
        {
            reason = orderResult.Error;
            return false;
        }
        RebirthNpcTransactionResult ownerResult = dog.SetRebirthOwner(RebirthNpcOwnershipKind.None, string.Empty);
        if (!ownerResult.Succeeded)
        {
            reason = ownerResult.Error;
            return false;
        }
        // Publish deliberate unownership before any projection reconciliation. SetRebirthOwner
        // updates runtime state only; the previous ordering silently re-hired this dog.
        bool cleared = RebirthNpcAggregatePersistenceStore.Mutate(id, r =>
        {
            r.Ownership = null;
            if (r.Order != null) r.Order.OrderState = RebirthNpcOrderState.None.ToString();
        });
        if (!cleared)
        {
            RestorePersistedOwnershipAndOrder(dog);
            reason = "Unable to commit companion dismissal; ownership retained.";
            return false;
        }
        ReleaseLegacyOwnerProjection(dog);
        RebirthDogChunkObserverService.Release(id);

        // A dismissed dog remains a valid living dog in the world and can be hired again.
        // Clear owner-scoped behavior axes so the former owner's combat preferences do not leak
        // into a future ownership session.
        RebirthDogStateService.SetCombatMode(id, RebirthCompanionBehaviorMode.FullControl);
        RebirthDogStateService.SetAttackStopped(id, false);
        RebirthDogStateService.CaptureRuntime(dog);
        RebirthNpcAggregatePersistenceStore.Save();
        RebirthNpcStableIdentityStore.Save();
        RebirthDogCapacitySyncService.NotifyOwner(player);
        reason = Localization.Get("xuiRebirthDogDismissSuccess");
        return true;
    }

    public static bool TryAdminRemove(World world, EntityPlayer player, EntityRebirthDogCompanion dog)
    {
        if (!IsAuthoritative(world) || player == null || dog == null || dog.RebirthRuntimeState == null) return false;
        if (!(player.IsAdmin || player.IsGodMode.Value)) return false;
        if (Vector3.Distance(player.position, dog.position) > MaximumHireDistance) return false;

        RebirthNpcStableId id = dog.RebirthRuntimeState.StableId;
        RebirthDogChunkObserverService.Release(id);
        EntityPlayer capacityOwner = RebirthDogRuntimeService.ResolveOwnerPublic(dog.world, dog.RebirthRuntimeState.OwnerId);
        int entityId = dog.entityId;
        long now = DateTime.UtcNow.Ticks;

        // Clear every live ownership projection before the terminal record mutation so
        // companion counts and legacy $Leader/belongsPlayerId consumers agree immediately.
        try { dog.SetRebirthOrder(RebirthNpcOrderState.None, Vector3.zero, false); } catch { }
        try { dog.SetRebirthOwner(RebirthNpcOwnershipKind.None, string.Empty); } catch { }
        SynchronizeLegacyOwnerProjection(dog);

        // 2.6 admin Remove was terminal despawn, not Dismiss. Preserve that distinction
        // by tombstoning the persistent NPC and releasing its live StableId mapping.
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (r.Lifecycle == null) r.Lifecycle = new RebirthNpcLifecycleRecord();
            r.Lifecycle.DismissalState = "removed";
            r.Lifecycle.RemovalReason = "dog-admin-remove";
            r.Lifecycle.TombstoneState = true;
            r.Lifecycle.TombstoneWorldTime = now;
            unchecked { r.Lifecycle.LifecycleRevision++; }

            if (r.Ownership == null) r.Ownership = new RebirthNpcOwnershipRecord();
            r.Ownership.OwnershipState = RebirthNpcOwnershipKind.None.ToString();
            r.Ownership.OwnerPlatformIdOrPersistentPlayerId = string.Empty;
            r.Ownership.DismissedWorldTime = now;
            unchecked { r.Ownership.OwnerRevision++; }

            if (r.Order == null) r.Order = new RebirthNpcOrderRecord();
            r.Order.PreviousOrderState = r.Order.OrderState;
            r.Order.OrderState = RebirthNpcOrderState.None.ToString();
            r.Order.OrderTarget = string.Empty;
            unchecked { r.Order.OrderRevision++; }

            if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
            r.Presence.PresenceState = RebirthNpcPresenceState.Removed.ToString();
            r.Presence.TransitionReason = "dog-admin-remove";
            r.Presence.TransitionStartedWorldTime = now;
            unchecked { r.Presence.PresenceRevision++; }

            if (r.Dog != null)
            {
                r.Dog.AttackStopped = false;
                r.Dog.OfflineParked = false;
                r.Dog.OfflineParkPosition = null;
                unchecked { r.Dog.Revision++; }
            }
            unchecked { r.AggregateRevision++; }
        });

        try { dog.SetRebirthPresence(RebirthNpcPresenceState.Removed); } catch { }
        RebirthDogRuntimeService.Remove(entityId);
        RebirthNpcLifecycle.OnEntityDeactivated(entityId, true);
        world.RemoveEntity(entityId, EnumRemoveEntityReason.Despawned);
        RebirthNpcAggregatePersistenceStore.Save();
        RebirthNpcStableIdentityStore.Save();
        RebirthDogCapacitySyncService.NotifyOwner(capacityOwner);
        return true;
    }

    public static void SynchronizeDisplayName(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null || dog.RebirthRuntimeState.StableId.IsEmpty) return;
        string displayName = RebirthNpcWorldIntegrationService.GetDisplayName(dog.RebirthRuntimeState.StableId);
        if (string.IsNullOrWhiteSpace(displayName)) return;
        string clean = displayName.Trim();
        try
        {
            if (!string.Equals(dog.EntityName ?? string.Empty, clean, StringComparison.Ordinal))
                dog.SetEntityName(clean);
        }
        catch
        {
            // Presentation sync must never invalidate the persistent dog identity.
        }
    }

    /// <summary>
    /// Keeps the owner-local map/compass marker synchronized without tying its lifetime to
    /// the live Entity. The marker service uses a position NavObject so chunk unload cannot
    /// erase the last authoritative dog location.
    /// </summary>
    public static void EnsureNavigationMarker(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null || dog.RebirthRuntimeState.StableId.IsEmpty) return;
        SynchronizeDisplayName(dog);
        RebirthDogNavigationMarkerService.SynchronizeLoadedDog(dog);
    }

    public static bool TryRename(EntityPlayer player, RebirthNpcStableId stableId, string requestedName, out string reason)
    {
        reason = string.Empty;
        if (player == null || stableId.IsEmpty || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        { reason = Localization.Get("xuiRebirthDogRenameUnavailable"); return false; }
        RebirthNpcPersistentRecordView record; RebirthDogPersistentRecordView dogRecord; string ownerId;
        if (!TryResolveOwnerId(player, out ownerId) || !RebirthDogStateService.TryGetView(stableId, out record, out dogRecord) ||
            record.Ownership == null || !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId, ownerId, StringComparison.OrdinalIgnoreCase))
        { reason = Localization.Get("xuiRebirthDogRenameUnavailable"); return false; }
        string clean = (requestedName ?? string.Empty).Trim();
        if (clean.Length < 1 || clean.Length > 32)
        { reason = Localization.Get("xuiRebirthDogRenameInvalid"); return false; }
        for (int i = 0; i < clean.Length; i++) if (char.IsControl(clean[i]))
        { reason = Localization.Get("xuiRebirthDogRenameInvalid"); return false; }
        string detail;
        if (!RebirthNpcWorldIntegrationService.TrySetDisplayName(stableId, clean, out detail))
        { reason = string.IsNullOrEmpty(detail) ? Localization.Get("xuiRebirthDogRenameUnavailable") : detail; return false; }
        RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
        {
            if (r.Identity == null) r.Identity = new RebirthNpcIdentityRecord { StableNpcId = stableId };
            r.Identity.GeneratedOrAssignedDisplayName = clean;
            unchecked { r.Identity.NameRevision++; }
        });
        int liveEntityId;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out liveEntityId))
        {
            EntityRebirthDogCompanion liveDog = GameManager.Instance?.World?.GetEntity(liveEntityId) as EntityRebirthDogCompanion;
            SynchronizeDisplayName(liveDog);
        }
        RebirthNpcAggregatePersistenceStore.Save();
        reason = Localization.Get("xuiRebirthDogRenameSuccess");
        return true;
    }

    public static bool TrySetRespawnPoint(World world, EntityPlayer player, EntityRebirthDogCompanion dog, out string reason)
    {
        reason = string.Empty;
        if (!IsAuthoritative(world) || player == null || dog == null || dog.IsDead() ||
            dog.RebirthRuntimeState == null || !IsOwnedBy(dog, player))
        {
            reason = Localization.Get("xuiRebirthDogRespawnPointUnavailable");
            return false;
        }

        RebirthNpcStableId stableId = dog.RebirthRuntimeState.StableId;
        Vector3 anchor = dog.position;
        float yaw = dog.rotation.y;
        RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
        {
            if (r == null) return;
            if (r.Respawn == null) r.Respawn = new RebirthNpcRespawnRecord();
            // This is the modern equivalent of 2.6 RebirthManager.UpdateHireInfo(
            // ..., "reSpawnPosition", ...): a durable per-companion explicit anchor.
            r.Respawn.PreferredAnchorType = "companion-set-point";
            r.Respawn.PreferredAnchorIdentity = stableId.ToString();
            r.Respawn.PreferredAnchorPosition = anchor;
            r.Respawn.PreferredAnchorRotation = yaw;
            r.Respawn.RespawnPolicyId = "explicit-companion-point";
            unchecked { r.Respawn.RespawnRevision++; r.AggregateRevision++; }
        });
        RebirthNpcAggregatePersistenceStore.Save();
        reason = Localization.Get("xuiRebirthDogRespawnPointSet");
        return true;
    }

    public static bool TryReportForDuty(World world, EntityPlayer player, RebirthNpcStableId stableId, out EntityRebirthDogCompanion dog, out string reason)
    {
        dog = null; reason = string.Empty;
        if (!IsAuthoritative(world) || player == null)
        { reason = Localization.Get("xuiRebirthDogRespawnUnavailable"); return false; }

        RebirthNpcPersistentRecordView record; RebirthDogPersistentRecordView dogRecord; string ownerId; RebirthDogBreedDefinition breed=null;
        if (!TryResolveOwnerId(player, out ownerId) ||
            !RebirthDogStateService.TryGetView(stableId, out record, out dogRecord) ||
            dogRecord == null || dogRecord.Lifecycle != RebirthDogLifecycleKind.AwaitingRespawn ||
            record == null || record.Ownership == null ||
            !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId, ownerId, StringComparison.OrdinalIgnoreCase))
        { reason = Localization.Get("xuiRebirthDogRespawnUnavailable"); return false; }
        string respawnEntityClass = dogRecord.IsTamedWild ? (dogRecord.TamedEntityClass ?? string.Empty) : string.Empty;
        if(!dogRecord.IsTamedWild && !RebirthDogDefinitions.TryGetByBreedId(dogRecord.BreedId,out breed))
        { reason=Localization.Get("xuiRebirthDogRespawnUnavailable");return false; }
        if(!dogRecord.IsTamedWild)respawnEntityClass=breed.EntityClassName;
        if(string.IsNullOrWhiteSpace(respawnEntityClass))
        { reason=Localization.Get("xuiRebirthDogRespawnUnavailable");return false; }

        // 2.6 retained the same live NPC in an off-duty/sitting state. Report for Duty
        // therefore reactivates that projection in place instead of spawning a duplicate.
        int liveEntityId;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out liveEntityId))
        {
            EntityRebirthDogCompanion liveDog = world.GetEntity(liveEntityId) as EntityRebirthDogCompanion;
            if (liveDog != null && !liveDog.IsDead())
            {
                dog = liveDog;
                ReactivateAwaitingRespawnProjection(dog, player);
                reason = Localization.Get("xuiRebirthDogRespawnSuccess");
                return true;
            }
            RebirthNpcRuntimeRegistry.Unregister(liveEntityId, false);
        }

        // Recovery-only path: if the live projection was genuinely lost (crash/entity
        // disappearance), recreate the SAME StableId at the already-resolved 2.6 park
        // location. Priority remains explicit dog point -> owner bedroll/bed +1 Y ->
        // recorded death location. No owner-current-position substitution is used.
        Vector3 spawnPosition = Vector3.zero;
        float spawnYaw = record.Transform != null ? record.Transform.RotationYaw : player.rotation.y;
        bool hasPosition = false;
        if (record.Respawn != null && record.Respawn.PreferredAnchorPosition.HasValue)
        {
            spawnPosition = record.Respawn.PreferredAnchorPosition.Value;
            if (record.Respawn.PreferredAnchorRotation.HasValue) spawnYaw = record.Respawn.PreferredAnchorRotation.Value;
            hasPosition = true;
        }
        else if (player.SpawnPoints != null && player.SpawnPoints.Count > 0)
        {
            spawnPosition = (Vector3)player.SpawnPoints[0] + Vector3.up;
            spawnYaw = player.rotation.y;
            hasPosition = true;
        }
        else if (record.Respawn != null && record.Respawn.FallbackPosition.HasValue)
        {
            spawnPosition = record.Respawn.FallbackPosition.Value;
            hasPosition = true;
        }
        else if (dogRecord.RespawnFallback.HasValue)
        {
            spawnPosition = dogRecord.RespawnFallback.Value;
            hasPosition = true;
        }
        else if (record.Transform != null)
        {
            spawnPosition = record.Transform.WorldPosition;
            hasPosition = true;
        }
        if (!hasPosition)
        { reason = Localization.Get("xuiRebirthDogRespawnNoSafeLocation"); return false; }

        int entityClassId = EntityClass.FromString(respawnEntityClass);
        EntityClass entityClassDefinition;
        if (EntityClass.list == null || !EntityClass.list.TryGetValue(entityClassId, out entityClassDefinition) || entityClassDefinition == null)
        { reason = Localization.Get("xuiRebirthDogRespawnUnavailable"); return false; }

        dog = EntityFactory.CreateEntity(entityClassId, spawnPosition, new Vector3(0f, spawnYaw, 0f)) as EntityRebirthDogCompanion;
        if (dog == null)
        { reason = Localization.Get("xuiRebirthDogRespawnUnavailable"); return false; }
        dog.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
        dog.PreparePersistedDogDeployment(stableId);
        world.SpawnEntityInWorld(dog);
        if (!RebirthNpcRuntimeRegistry.TryRebindStableId(dog.entityId, stableId))
        {
            world.RemoveEntity(dog.entityId, EnumRemoveEntityReason.Despawned);
            dog = null;
            reason = Localization.Get("xuiRebirthDogRespawnUnavailable");
            return false;
        }
        RestorePersistedOwnershipAndOrder(dog);
        ReactivateAwaitingRespawnProjection(dog, player);
        reason = Localization.Get("xuiRebirthDogRespawnSuccess");
        return true;
    }

    public static void RestorePersistedOwnershipAndOrder(EntityRebirthDogCompanion dog)
    {
        if (dog == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
        RebirthNpcPersistentRecordView record;
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        if (state == null || !RebirthNpcAggregatePersistenceStore.TryGetView(state.StableId, out record) || record == null) return;
        if (!CanRestoreOwnedDogRecord(record)) return;

        if (record.Ownership != null && !string.IsNullOrWhiteSpace(record.Ownership.OwnerPlatformIdOrPersistentPlayerId))
        {
            RebirthNpcOwnershipKind ownership;
            if (Enum.TryParse(record.Ownership.OwnershipState ?? string.Empty, true, out ownership) && ownership != RebirthNpcOwnershipKind.None)
            {
                dog.SetRebirthOwner(ownership, record.Ownership.OwnerPlatformIdOrPersistentPlayerId);
                dog.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
            }
        }

        if (record.Order != null)
        {
            RebirthNpcOrderState order;
            if (Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order) && order != RebirthNpcOrderState.None)
            {
                bool hasGuard = order == RebirthNpcOrderState.Guard && record.Transform != null && record.Transform.AnchorPosition.HasValue;
                Vector3 guard = hasGuard ? record.Transform.AnchorPosition.Value : Vector3.zero;
                dog.SetRebirthOrder(order, guard, hasGuard);
            }
        }
        RebirthDogStateService.EnsureView(dog);
        RebirthDogRuntimeService.ApplyProgressionCvars(dog);

        RebirthDogPersistentRecordView dogRecord = record.Dog;
        if (dogRecord != null && dogRecord.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn)
        {
            // Save/load parity: an off-duty dog remains the same owned, live projection,
            // untargetable and sitting until its owner explicitly chooses Report for Duty.
            // Preserve any bed/bedroll recovery accumulated before the save.
            int persistedHealth = record.Vitals != null ? record.Vitals.CurrentHealth : dog.Health;
            dog.Health = Mathf.Clamp(Math.Max(1, persistedHealth), 1, dog.GetMaxHealth());
            dog.SetRebirthPresence(RebirthNpcPresenceState.AwaitingRespawn);
            try { dog.Buffs.SetCustomVar("$FR_NPC_Respawn", 1f); } catch { }
            dog.SetIgnoredByAI(true);
            RebirthDogRuntimeService.ClearCombat(dog);
            try { dog.motion = Vector3.zero; } catch { }
            try { if (!dog.IsSleeping) dog.TriggerSleeperPose(0); } catch { }
            SynchronizeLegacyOwnerProjection(dog);
            EnsureNavigationMarker(dog);
            MaintainAwaitingRespawnProjection(dog);
            return;
        }

        dog.SetIgnoredByAI(false);
        try { dog.Buffs.SetCustomVar("$FR_NPC_Respawn", 0f); } catch { }
        SynchronizeLegacyOwnerProjection(dog);
        EnsureNavigationMarker(dog);
    }

    /// <summary>
    /// Re-projects the durable 3.1 owner onto the base-game live ownership surfaces used
    /// by legacy 2.6 AI/dialog code. This is intentionally repeatable because an NPC can
    /// finish loading before its owning player exists in the World after a relog.
    /// </summary>
    // Called only after an identity/revision-checked server snapshot was accepted.
    // Never read a client sidecar or emit ownership/CVar mutations to the server.
    public static void SynchronizeClientOwnerProjection(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.world == null || !dog.world.IsRemote() || dog.RebirthRuntimeState == null)
            return;
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        bool owned = state.OwnershipKind == RebirthNpcOwnershipKind.Player && !string.IsNullOrWhiteSpace(state.OwnerId);
        EntityPlayer owner = owned ? RebirthDogRuntimeService.ResolveOwnerPublic(dog.world, state.OwnerId) : null;
        // Owner may stream in after the dog. Keep existing projection until it resolves.
        if (owned && owner == null) return;
        int ownerEntityId = owner != null ? owner.entityId : 0;
        int previousOwnerEntityId = ResolveLegacyOwnerEntityId(dog);
        if (previousOwnerEntityId > 0 && previousOwnerEntityId != ownerEntityId)
        {
            EntityAlive previous = dog.world.GetEntity(previousOwnerEntityId) as EntityAlive;
            if (previous != null && previous.HasOwnedEntity(dog.entityId)) previous.RemoveOwnedEntity(dog.entityId);
        }
        dog.belongsPlayerId = ownerEntityId;
        if (dog.Buffs != null) dog.Buffs.SetCustomVar("$Leader", ownerEntityId, false);
        if (owner != null && !owner.HasOwnedEntity(dog.entityId)) owner.AddOwnedEntity((Entity)dog);
    }

    public static void SynchronizeLegacyOwnerProjection(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null || dog.Buffs == null) return;

        // v287 ownership repair: the aggregate dog record is the durable authority. A reconstructed
        // or reloaded live projection must never become hireable merely because its transient runtime
        // owner was not projected yet. Re-apply durable ownership before touching any of the legacy
        // 2.6 surfaces ($Leader / belongsPlayerId / ownedEntities). This is deliberately one-way:
        // a deliberate Dismiss clears the aggregate ownership first, so it cannot be re-owned here.
        ReconcileRuntimeOwnerFromPersistentRecord(dog);
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        if (state.OwnershipKind != RebirthNpcOwnershipKind.Player || string.IsNullOrWhiteSpace(state.OwnerId))
        {
            dog.ReleaseRebirthOwnerTeleportHandler();
            ReleaseLegacyOwnerProjection(dog);
            return;
        }

        EntityPlayer owner = RebirthDogRuntimeService.ResolveOwnerPublic(dog.world, state.OwnerId);
        if (owner == null)
        {
            dog.ReleaseRebirthOwnerTeleportHandler();
            return;
        }
        dog.SynchronizeRebirthOwnerTeleportHandler(owner);

        // If an entity-id was replaced across reload/redeploy, remove any live projection
        // from a previous owner entity before binding the durable owner. 2.6
        // SetLeaderAndOwner projected ownership onto ALL three live surfaces: belongsPlayerId,
        // $Leader and the player's owned-entity collection. Keep that contract in 3.1 while
        // the stable aggregate remains the source of truth.
        int previousOwnerEntityId = ResolveLegacyOwnerEntityId(dog);
        if (previousOwnerEntityId > 0 && previousOwnerEntityId != owner.entityId)
        {
            EntityAlive previousOwner = dog.world != null ? dog.world.GetEntity(previousOwnerEntityId) as EntityAlive : null;
            if (previousOwner != null && previousOwner.HasOwnedEntity(dog.entityId))
                previousOwner.RemoveOwnedEntity(dog.entityId);
        }

        dog.belongsPlayerId = owner.entityId;
        dog.Buffs.SetCustomVar("$Leader", owner.entityId);
        if (!owner.HasOwnedEntity(dog.entityId))
            owner.AddOwnedEntity((Entity)dog);
    }


    private static bool CanRestoreOwnedDogRecord(RebirthNpcPersistentRecordView record)
    {
        return record?.Identity != null && record.Dog != null && RebirthDogStateService.IsDog(record) &&
            record.Ownership != null && !string.IsNullOrWhiteSpace(record.Ownership.OwnerPlatformIdOrPersistentPlayerId) &&
            string.Equals(record.Ownership.OwnershipState, RebirthNpcOwnershipKind.Player.ToString(), StringComparison.OrdinalIgnoreCase) &&
            record.Lifecycle?.TombstoneState != true &&
            !string.Equals(record.Presence?.PresenceState, RebirthNpcPresenceState.Removed.ToString(), StringComparison.OrdinalIgnoreCase) &&
            (record.Dog.Lifecycle == RebirthDogLifecycleKind.Active || record.Dog.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn);
    }

    private static void ReconcileRuntimeOwnerFromPersistentRecord(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null || dog.RebirthRuntimeState.StableId.IsEmpty) return;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer) return;

        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        RebirthNpcPersistentRecordView record;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(state.StableId, out record) ||
            !CanRestoreOwnedDogRecord(record))
            return;

        RebirthNpcOwnershipKind durableKind;
        if (!Enum.TryParse(record.Ownership.OwnershipState ?? string.Empty, true, out durableKind) ||
            durableKind == RebirthNpcOwnershipKind.None)
            return;

        string durableOwner = record.Ownership.OwnerPlatformIdOrPersistentPlayerId.Trim();
        if (state.OwnershipKind == durableKind &&
            string.Equals(state.OwnerId ?? string.Empty, durableOwner, StringComparison.OrdinalIgnoreCase))
            return;

        RebirthNpcTransactionResult repaired = dog.SetRebirthOwner(durableKind, durableOwner);
        if (repaired.Succeeded)
        {
            dog.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Dog] restored live owner projection from persistent record entity=" + dog.entityId +
                " stableId=" + state.StableId + " owner=" + durableOwner); }
        }
        else
        {
            Log.Warning("[REBIRTH Dog] failed to restore live owner projection entity=" + dog.entityId +
                " stableId=" + state.StableId + " owner=" + durableOwner + " reason=" +
                (repaired.Error ?? string.Empty));
        }
    }

    /// <summary>
    /// Removes only the live/base-game owner projection. Durable REBIRTH ownership is untouched
    /// unless the caller separately changes SetRebirthOwner. This is required for pickup/removal;
    /// AwaitingRespawn deliberately retains its live owner projection because the same dog entity
    /// remains parked off-duty, matching the 2.6 hire roster contract.
    /// </summary>
    public static void ReleaseLegacyOwnerProjection(EntityRebirthDogCompanion dog)
    {
        if (dog == null) return;
        dog.ReleaseRebirthOwnerTeleportHandler();
        int ownerEntityId = ResolveLegacyOwnerEntityId(dog);
        EntityAlive owner = ownerEntityId > 0 && dog.world != null
            ? dog.world.GetEntity(ownerEntityId) as EntityAlive
            : null;
        if (owner != null && owner.HasOwnedEntity(dog.entityId))
            owner.RemoveOwnedEntity(dog.entityId);
        dog.belongsPlayerId = 0;
        if (dog.Buffs != null) dog.Buffs.SetCustomVar("$Leader", 0f);
    }

    private static int ResolveLegacyOwnerEntityId(EntityRebirthDogCompanion dog)
    {
        if (dog == null) return 0;
        if (dog.belongsPlayerId > 0) return dog.belongsPlayerId;
        if (dog.Buffs != null)
        {
            int leader = Mathf.RoundToInt(dog.Buffs.GetCustomVar("$Leader"));
            if (leader > 0) return leader;
        }
        return 0;
    }

    public static bool IsOwnedBy(EntityRebirthDogCompanion dog, EntityPlayer player)
    {
        if (dog == null || player == null || dog.RebirthRuntimeState == null) return false;
        string ownerId;
        return TryResolveOwnerId(player, out ownerId) &&
               dog.RebirthRuntimeState.OwnershipKind == RebirthNpcOwnershipKind.Player &&
               string.Equals(dog.RebirthRuntimeState.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RestoreHireItems(EntityPlayer player, ItemValue item, int count)
    {
        if (count <= 0) return true;
        if (player == null || item == null || item.IsEmpty()) return false;
        ItemStack stack = new ItemStack(item.Clone(), count);
        if (player.inventory != null && player.inventory.AddItem(stack)) return true;
        if (stack.count <= 0) return true;
        if (player.bag != null && player.bag.AddItem(stack)) return true;
        if (stack.count <= 0) return true;
        try
        {
            GameManager.Instance.ItemDropServer(stack, player.position + Vector3.up, Vector3.zero,
                player.entityId, 60f, false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Dog] could not restore " + stack.count + " x " + HireItemName +
                " after failed hire transaction: " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    public static void ScheduleGhostRepair(EntityPlayer player)
    {
        if (player == null || SingletonMonoBehaviour<ConnectionManager>.Instance == null ||
            !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return;

        lock (GhostRepairSync)
        {
            GhostRepairRetries[player.entityId] = new GhostRepairRetry
            {
                PlayerEntityId = player.entityId,
                AttemptsRemaining = 30,
                NextAttemptUtcTicks = DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(750).Ticks
            };
        }
    }

    public static void TickGhostRepairRetries()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance?.World;
        if (connection == null || !connection.IsServer || world == null || world.IsRemote()) return;

        long now = DateTime.UtcNow.Ticks;
        List<GhostRepairRetry> due = new List<GhostRepairRetry>();
        lock (GhostRepairSync)
            foreach (GhostRepairRetry retry in GhostRepairRetries.Values)
                if (retry != null && retry.NextAttemptUtcTicks <= now) due.Add(retry);

        foreach (GhostRepairRetry retry in due)
        {
            EntityPlayer player = world.GetEntity(retry.PlayerEntityId) as EntityPlayer;
            bool ready = false, pending = true;
            try
            {
                string ownerId;
                ready = player != null && TryResolveOwnerId(player, out ownerId);
                if (ready)
                {
                    // The immediate spawn call can precede owner indexing. Do the complete
                    // projection once when the persistent owner is actually resolvable.
                    if (!retry.OwnerProjectionReconciled)
                    {
                        RebirthDogChunkObserverService.RefreshForPlayer(player);
                        ReconcileOwnerProjectionForPlayer(player);
                        retry.OwnerProjectionReconciled = true;
                    }
                    else RepairMissingActiveDogsForPlayer(world, player, "spawn-retry");
                    string resolvedOwner;
                    pending = !TryResolveOwnerId(player, out resolvedOwner) || HasMissingActiveFollowDog(world, resolvedOwner);
                }
            }
            catch (Exception ex)
            {
                if (retry.AttemptsRemaining == 30)
                    Log.Warning("[REBIRTH Dog] reconciliation waiting player=" + retry.PlayerEntityId + " reason=" + ex.Message);
            }
            retry.AttemptsRemaining--;
            bool remove = (ready && !pending) || retry.AttemptsRemaining <= 0;
            if (remove)
            {
                lock (GhostRepairSync) GhostRepairRetries.Remove(retry.PlayerEntityId);
                if (pending) Log.Warning("[REBIRTH Dog] reconciliation still pending player=" + retry.PlayerEntityId +
                    "; use rbdog ownership then rbdog reconcile. No ownership or inventory was discarded.");
            }
            else retry.NextAttemptUtcTicks = now + TimeSpan.TicksPerSecond;
        }
    }

    public static void ResetGhostRepairRetries()
    {
        lock (GhostRepairSync) GhostRepairRetries.Clear();
    }

    private static bool HasMissingActiveFollowDog(World world, string ownerId)
    {
        if (world == null || string.IsNullOrWhiteSpace(ownerId)) return false;
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (!IsRepairableOwnedActiveRecord(record, ownerId) || record.Order == null) continue;
            RebirthNpcOrderState order;
            if (!Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order) ||
                order != RebirthNpcOrderState.Follow)
                continue;
            int matches;
            if (FindLoadedDogByStableId(world, record.Identity.StableNpcId, out matches) == null)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Reconstructs an Active durable dog record only when no physical world entity exists.
    /// The SAME StableId is rebound before the entity enters the World so OnAddedToWorld
    /// restores ownership/order/name against the authoritative record instead of creating a
    /// second logical companion. Follow dogs recover near the owner; Stay/Guard dogs recover
    /// only at their own loaded anchor.
    /// </summary>
    public static bool TryRepairActiveGhostDog(
        World world,
        EntityPlayer player,
        RebirthNpcStableId stableId,
        out EntityRebirthDogCompanion dog,
        out string reason)
    {
        return TryRepairActiveGhostDog(world, player, stableId, 0, "explicit", out dog, out reason);
    }

    public static int RepairMissingActiveDogsForPlayer(World world, EntityPlayer player, string trigger)
    {
        if (!IsAuthoritative(world) || player == null) return 0;

        string ownerId;
        if (!TryResolveOwnerId(player, out ownerId) || string.IsNullOrWhiteSpace(ownerId))
            return 0;

        int repaired = 0;
        int ordinal = 0;
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (!IsRepairableOwnedActiveRecord(record, ownerId)) continue;

            RebirthNpcStableId stableId = record.Identity.StableNpcId;
            EntityRebirthDogCompanion existing;
            int matches;
            existing = FindLoadedDogByStableId(world, stableId, out matches);
            if (matches > 1)
            {
                Log.Warning("[REBIRTH Dog] ghost repair refused duplicate live StableId=" + stableId +
                    " matches=" + matches + " trigger=" + (trigger ?? string.Empty));
                continue;
            }
            if (existing != null)
            {
                EnsureRuntimeBinding(existing, stableId);
                RestorePersistedOwnershipAndOrder(existing);
                SynchronizeLegacyOwnerProjection(existing);
                continue;
            }

            EntityRebirthDogCompanion created;
            string detail;
            if (TryRepairActiveGhostDog(world, player, stableId, ordinal++, trigger, out created, out detail))
                repaired++;
            else if (!string.IsNullOrEmpty(detail) &&
                     !string.Equals(detail, "anchor-chunk-not-loaded", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(detail, "owner-safe-position-unavailable", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(detail, "anchor-safe-position-unavailable", StringComparison.OrdinalIgnoreCase))
                Log.Warning("[REBIRTH Dog] ghost repair skipped stableId=" + stableId +
                    " trigger=" + (trigger ?? string.Empty) + " reason=" + detail);
        }

        if (repaired > 0)
        {
            RebirthNpcAggregatePersistenceStore.Save();
            RebirthNpcStableIdentityStore.Save();
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Dog] ghost repair completed owner=" + ownerId +
                " repaired=" + repaired + " trigger=" + (trigger ?? string.Empty)); }
        }
        return repaired;
    }

    private static bool TryRepairActiveGhostDog(
        World world,
        EntityPlayer player,
        RebirthNpcStableId stableId,
        int ordinal,
        string trigger,
        out EntityRebirthDogCompanion dog,
        out string reason)
    {
        dog = null;
        reason = string.Empty;
        if (!IsAuthoritative(world) || player == null || stableId.IsEmpty)
        {
            reason = "not-authoritative";
            return false;
        }

        string ownerId;
        RebirthNpcPersistentRecordView record;
        RebirthDogPersistentRecordView dogRecord;
        if (!TryResolveOwnerId(player, out ownerId) ||
            !RebirthDogStateService.TryGetView(stableId, out record, out dogRecord) ||
            !IsRepairableOwnedActiveRecord(record, ownerId))
        {
            reason = "record-not-repairable";
            return false;
        }

        int matches;
        EntityRebirthDogCompanion existing = FindLoadedDogByStableId(world, stableId, out matches);
        if (matches > 1)
        {
            reason = "duplicate-live-stable-id";
            return false;
        }
        if (existing != null)
        {
            EnsureRuntimeBinding(existing, stableId);
            RestorePersistedOwnershipAndOrder(existing);
            SynchronizeLegacyOwnerProjection(existing);
            dog = existing;
            return true;
        }

        int staleEntityId;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out staleEntityId))
        {
            Entity mapped = world.GetEntity(staleEntityId);
            if (mapped != null)
            {
                reason = "stable-id-bound-to-nonmatching-live-entity";
                return false;
            }
            RebirthNpcRuntimeRegistry.Unregister(staleEntityId, false);
        }

        RebirthNpcOrderState order = RebirthNpcOrderState.Follow;
        if (record.Order != null)
            Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order);

        Vector3 spawnPosition;
        if (order == RebirthNpcOrderState.Follow)
        {
            // A missing Follow dog reconstructed at the owner is the recovery form of recall.
            // Preserve the same exact-position contract instead of scattering by StableId.
            spawnPosition = player.position;
            if (!world.IsChunkAreaLoaded(spawnPosition))
            {
                reason = "owner-safe-position-unavailable";
                return false;
            }
        }
        else if (order == RebirthNpcOrderState.Stay || order == RebirthNpcOrderState.Guard)
        {
            Vector3 anchor = record.Transform != null ? record.Transform.WorldPosition : player.position;
            if (order == RebirthNpcOrderState.Stay && dogRecord.StayPosition.HasValue)
                anchor = dogRecord.StayPosition.Value;
            else if (order == RebirthNpcOrderState.Guard && record.Transform != null && record.Transform.AnchorPosition.HasValue)
                anchor = record.Transform.AnchorPosition.Value;

            if (!world.IsChunkAreaLoaded(anchor))
            {
                reason = "anchor-chunk-not-loaded";
                return false;
            }
            if (!RebirthDogRuntimeService.TryFindSafeAtExactXZ(world, anchor, out spawnPosition) || spawnPosition.y <= 1f)
            {
                reason = "anchor-safe-position-unavailable";
                return false;
            }
        }
        else
        {
            reason = "active-order-not-repairable:" + order;
            return false;
        }

        RebirthDogBreedDefinition breed=null;
        string reconstructionEntityClass=dogRecord.IsTamedWild?(dogRecord.TamedEntityClass??string.Empty):string.Empty;
        if(!dogRecord.IsTamedWild)
        {
            if(!RebirthDogDefinitions.TryGetByBreedId(dogRecord.BreedId,out breed)||breed==null){reason="unknown-breed:"+(dogRecord.BreedId??string.Empty);return false;}
            reconstructionEntityClass=breed.EntityClassName;
        }
        if(string.IsNullOrWhiteSpace(reconstructionEntityClass)){reason="missing-tamed-entity-class";return false;}

        int entityClassId = EntityClass.FromString(reconstructionEntityClass);
        EntityClass entityClassDefinition;
        if (EntityClass.list == null ||
            !EntityClass.list.TryGetValue(entityClassId, out entityClassDefinition) ||
            entityClassDefinition == null)
        {
            reason = "missing-entity-class:" + reconstructionEntityClass;
            return false;
        }

        int persistedHealth = record.Vitals != null ? record.Vitals.CurrentHealth : 1;
        int persistedMaximumHealth = record.Vitals != null ? record.Vitals.MaximumHealthAtSave : 1;
        uint previousEmbodimentGeneration = record.Presence != null ? record.Presence.EmbodimentGeneration : 0U;
        float yaw = record.Transform != null ? record.Transform.RotationYaw : player.rotation.y;

        EntityRebirthDogCompanion created = null;
        bool spawned = false;
        try
        {
            created = EntityFactory.CreateEntity(entityClassId, spawnPosition, new Vector3(0f, yaw, 0f))
                as EntityRebirthDogCompanion;
            if (created == null)
            {
                reason = "entity-factory-failed";
                return false;
            }

            created.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
            created.PreparePersistedDogDeployment(stableId);

            // Rebind BEFORE SpawnEntityInWorld. The dog-specific OnAddedToWorld calls
            // RebirthDogStateService.Ensure, so entering the World with a temporary StableId
            // would create an orphan aggregate record before the authoritative ID is restored.
            if (!RebirthNpcRuntimeRegistry.TryRebindStableId(created.entityId, stableId))
            {
                RebirthNpcRuntimeRegistry.Unregister(created.entityId, false);
                reason = "stable-id-rebind-failed";
                return false;
            }

            world.SpawnEntityInWorld(created);
            spawned = true;
            if (!RebirthDogStateService.IsPersistableLiveProjection(created))
            {
                reason = "spawned-projection-invalid";
                return false;
            }

            RestorePersistedOwnershipAndOrder(created);
            RebirthDogRuntimeService.ApplyProgressionCvars(created);
            // AdjustNPCStats is a three-second delayed Health restore. It MUST have
            // $tempHealth populated before the buff starts; otherwise the buff finishes
            // by setting Health to zero. v284 omitted this during ghost reconstruction,
            // which made every repaired dog enter AwaitingRespawn exactly three seconds
            // after it was recreated. Mirror the proven 2.6 placement ordering here.
            int restoredHealth = Mathf.Clamp(Math.Max(1, persistedHealth), 1, created.GetMaxHealth());
            try { created.Buffs.SetCustomVar("$tempHealth", restoredHealth); } catch { }
            try
            {
                if (BuffManager.GetBuff("AdjustNPCStats") != null)
                    created.Buffs.AddBuff("AdjustNPCStats");
            }
            catch { }

            created.Health = restoredHealth;
            // A reconstructed body can be exposed to the same collider edge case as a
            // long-range recall. Keep the recovery point's exact X/Z for a short fall guard.
            RebirthDogRuntimeService.ArmRecallGroundGuardForRecovery(created, spawnPosition);
            SynchronizeLegacyOwnerProjection(created);
            SynchronizeDisplayName(created);
            EnsureNavigationMarker(created);
            RebirthDogStateService.CaptureRuntime(created);
            RebirthDogChunkObserverService.Synchronize(created);

            long now = DateTime.UtcNow.Ticks;
            RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
            {
                if (r == null) return;
                if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
                r.Dog.Lifecycle = RebirthDogLifecycleKind.Active;
                unchecked { r.Dog.Revision++; }

                if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
                r.Presence.PresenceState = RebirthNpcPresenceState.Active.ToString();
                r.Presence.TransitionReason = "ghost-reconstructed-v284:" + (trigger ?? string.Empty);
                r.Presence.TransitionStartedWorldTime = now;
                r.Presence.SuspendedRuntimeEntityId = null;
                unchecked { r.Presence.PresenceRevision++; r.Presence.EmbodimentGeneration++; }

                if (r.Vitals == null) r.Vitals = new RebirthNpcVitalStateRecord();
                r.Vitals.CurrentHealth = created.Health;
                r.Vitals.MaximumHealthAtSave = Math.Max(created.GetMaxHealth(), persistedMaximumHealth);
                r.Vitals.DeathOrIncapacitationState = "alive";
                unchecked { r.Vitals.VitalsRevision++; }

                if (r.Audit == null) r.Audit = new RebirthNpcAuditRecord();
                r.Audit.LastMutationKind = "dog-ghost-reconstruction-v284";
                r.Audit.LastMutationWorldTime = now;
                List<string> flags = new List<string>(r.Audit.RecoveryFlags ?? new string[0]);
                if (!flags.Contains("dog-ghost-reconstructed-v284"))
                    flags.Add("dog-ghost-reconstructed-v284");
                r.Audit.RecoveryFlags = flags.ToArray();
            });

            dog = created;
            reason = "repaired";
            RebirthCompanionRecallDebug.TraceGhostReconstruction(stableId, previousEmbodimentGeneration,
                unchecked(previousEmbodimentGeneration + 1U), persistedHealth, created, trigger);
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Dog] reconstructed missing Active dog stableId=" + stableId +
                " entity=" + created.entityId + " breed=" + dogRecord.BreedId +
                " order=" + order + " pos=" + created.position +
                " trigger=" + (trigger ?? string.Empty)); }
            return true;
        }
        finally
        {
            if (dog == null && created != null)
            {
                try
                {
                    if (spawned && world.GetEntity(created.entityId) != null)
                        world.RemoveEntity(created.entityId, EnumRemoveEntityReason.Despawned);
                    else
                        RebirthNpcRuntimeRegistry.Unregister(created.entityId, false);
                }
                catch { }
            }
        }
    }

    private static bool IsRepairableOwnedActiveRecord(RebirthNpcPersistentRecordView record, string ownerId)
    {
        if (record == null || record.Identity == null || record.Profile == null || record.Ownership == null ||
            record.Dog == null || record.Identity.StableNpcId.IsEmpty)
            return false;
        if (!string.Equals(record.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            return false;
        if (record.Dog.Lifecycle != RebirthDogLifecycleKind.Active)
            return false;
        if (record.Lifecycle != null && record.Lifecycle.TombstoneState)
            return false;
        if (record.Presence != null &&
            string.Equals(record.Presence.PresenceState ?? string.Empty, RebirthNpcPresenceState.Removed.ToString(), StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static EntityRebirthDogCompanion FindLoadedDogByStableId(
        World world,
        RebirthNpcStableId stableId,
        out int matches)
    {
        matches = 0;
        EntityRebirthDogCompanion first = null;
        if (world == null || world.Entities == null || world.Entities.list == null || stableId.IsEmpty)
            return null;

        for (int i = 0; i < world.Entities.list.Count; i++)
        {
            EntityRebirthDogCompanion candidate = world.Entities.list[i] as EntityRebirthDogCompanion;
            if (candidate == null || candidate.IsDead() || candidate.IsMarkedForUnload()) continue;

            RebirthNpcStableId runtimeId = candidate.RebirthRuntimeState != null
                ? candidate.RebirthRuntimeState.StableId
                : default(RebirthNpcStableId);
            RebirthNpcStableId embeddedId = candidate.RebirthSavedStableIdForDebug;
            if (runtimeId != stableId && embeddedId != stableId) continue;

            matches++;
            if (first == null) first = candidate;
        }
        return first;
    }

    private static void EnsureRuntimeBinding(EntityRebirthDogCompanion dog, RebirthNpcStableId stableId)
    {
        if (dog == null || dog.RebirthRuntimeState == null || stableId.IsEmpty) return;
        RebirthNpcStableId previousStableId = dog.RebirthRuntimeState.StableId;
        if (previousStableId == stableId) return;
        int existingId;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out existingId) && existingId != dog.entityId)
            return;
        if (RebirthNpcRuntimeRegistry.TryRebindStableId(dog.entityId, stableId) &&
            !previousStableId.IsEmpty && previousStableId != stableId &&
            dog.RebirthSavedStableIdForDebug == stableId)
            RebirthNpcAggregatePersistenceStore.Remove(previousStableId);
    }

    private const string V284GhostReconstructedFlag = "dog-ghost-reconstructed-v284";
    private const string V285FalseDeathRecoveredFlag = "dog-v284-false-death-recovered-v285";

    /// <summary>
    /// One-time repair for the v284 reconstruction health-CVar defect. A dog carrying the
    /// v284 reconstruction flag could be rebuilt correctly, then AdjustNPCStats expired
    /// three seconds later with an unset $tempHealth and forced Health to zero. Those dogs
    /// entered AwaitingRespawn even though no legitimate death occurred. Recover each such
    /// record once, restore its pre-death order and full saved MaxHealth, and stamp a v285
    /// flag so a later legitimate death is never auto-revived by this migration.
    /// </summary>
    private static int RecoverV284FalseReconstructionDeaths(World world, EntityPlayer player)
    {
        if (!IsAuthoritative(world) || player == null) return 0;

        string ownerId;
        if (!TryResolveOwnerId(player, out ownerId) || string.IsNullOrWhiteSpace(ownerId)) return 0;

        int recovered = 0;
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (record == null || record.Identity == null || record.Profile == null ||
                record.Ownership == null || record.Dog == null || record.Audit == null ||
                record.Dog.Lifecycle != RebirthDogLifecycleKind.AwaitingRespawn ||
                !string.Equals(record.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
                continue;

            RebirthNpcReadOnlyArray<string> flags = record.Audit.RecoveryFlags;
            bool wasV284Reconstruction = false;
            bool alreadyRecovered = false;
            for (int f = 0; flags != null && f < flags.Length; f++)
            {
                if (string.Equals(flags[f], V284GhostReconstructedFlag, StringComparison.OrdinalIgnoreCase))
                    wasV284Reconstruction = true;
                if (string.Equals(flags[f], V285FalseDeathRecoveredFlag, StringComparison.OrdinalIgnoreCase))
                    alreadyRecovered = true;
            }
            if (!wasV284Reconstruction || alreadyRecovered) continue;

            RebirthNpcOrderState restoredOrder = RebirthNpcOrderState.Follow;
            if (record.Order != null)
            {
                RebirthNpcOrderState parsed;
                if (Enum.TryParse(record.Order.PreviousOrderState ?? string.Empty, true, out parsed) &&
                    parsed != RebirthNpcOrderState.None)
                    restoredOrder = parsed;
            }

            int restoredHealth = record.Vitals != null
                ? Math.Max(1, record.Vitals.MaximumHealthAtSave)
                : 1;
            RebirthNpcStableId stableId = record.Identity.StableNpcId;
            RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
            {
                if (r == null) return;
                if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
                r.Dog.Lifecycle = RebirthDogLifecycleKind.Active;
                unchecked { r.Dog.Revision++; }

                if (r.Order == null) r.Order = new RebirthNpcOrderRecord();
                r.Order.OrderState = restoredOrder.ToString();
                r.Order.OrderTarget = string.Empty;
                unchecked { r.Order.OrderRevision++; }

                if (r.Presence == null) r.Presence = new RebirthNpcPresenceRecord();
                r.Presence.PresenceState = RebirthNpcPresenceState.Active.ToString();
                r.Presence.TransitionReason = "v285-recover-v284-health-cvar-bug";
                r.Presence.TransitionStartedWorldTime = DateTime.UtcNow.Ticks;
                unchecked { r.Presence.PresenceRevision++; }

                if (r.Respawn != null)
                {
                    r.Respawn.RespawnState = "Active";
                    r.Respawn.DeathWorldTime = null;
                    r.Respawn.EligibleRespawnWorldTime = null;
                    r.Respawn.LastFailureReason = string.Empty;
                    unchecked { r.Respawn.RespawnRevision++; }
                }

                if (r.Vitals == null) r.Vitals = new RebirthNpcVitalStateRecord();
                r.Vitals.CurrentHealth = restoredHealth;
                r.Vitals.MaximumHealthAtSave = Math.Max(restoredHealth, r.Vitals.MaximumHealthAtSave);
                r.Vitals.DeathOrIncapacitationState = "alive";
                unchecked { r.Vitals.VitalsRevision++; }

                if (r.Audit == null) r.Audit = new RebirthNpcAuditRecord { RecoveryFlags = new string[0] };
                List<string> nextFlags = new List<string>(r.Audit.RecoveryFlags ?? new string[0]);
                bool found = false;
                for (int j = nextFlags.Count - 1; j >= 0; j--)
                {
                    if (string.Equals(nextFlags[j], V284GhostReconstructedFlag, StringComparison.OrdinalIgnoreCase))
                        nextFlags.RemoveAt(j);
                    else if (string.Equals(nextFlags[j], V285FalseDeathRecoveredFlag, StringComparison.OrdinalIgnoreCase))
                        found = true;
                }
                if (!found) nextFlags.Add(V285FalseDeathRecoveredFlag);
                r.Audit.RecoveryFlags = nextFlags.ToArray();
                r.Audit.LastMutationKind = "v285-recover-v284-reconstruction-health-cvar-bug";
                r.Audit.LastMutationWorldTime = DateTime.UtcNow.Ticks;
            });

            int matches;
            EntityRebirthDogCompanion liveDog = FindLoadedDogByStableId(world, stableId, out matches);
            if (liveDog != null && matches == 1)
            {
                liveDog.Health = Mathf.Clamp(restoredHealth, 1, liveDog.GetMaxHealth());
                try { liveDog.Buffs.SetCustomVar("$tempHealth", liveDog.Health); } catch { }
                try { liveDog.Buffs.SetCustomVar("$FR_NPC_Respawn", 0f); } catch { }
                liveDog.SetIgnoredByAI(false);
                try { if (liveDog.IsSleeping) liveDog.ConditionalTriggerSleeperWakeUp(); } catch { }

                bool hasGuard = restoredOrder == RebirthNpcOrderState.Guard &&
                    record.Transform != null && record.Transform.AnchorPosition.HasValue;
                Vector3 guard = hasGuard ? record.Transform.AnchorPosition.Value : Vector3.zero;
                try { liveDog.SetRebirthPresence(RebirthNpcPresenceState.Active); } catch { }
                try { liveDog.SetRebirthOrder(restoredOrder, guard, hasGuard); } catch { }
                RebirthDogRuntimeService.ClearCombat(liveDog);
                SynchronizeLegacyOwnerProjection(liveDog);
                EnsureNavigationMarker(liveDog);
                RebirthDogStateService.CaptureRuntime(liveDog);
            }
            recovered++;
        }

        if (recovered > 0)
        {
            RebirthNpcAggregatePersistenceStore.Save();
            RebirthDogCapacitySyncService.NotifyOwner(player);
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Dog] v285 repaired v284 false reconstruction deaths owner=" + ownerId +
                " recovered=" + recovered); }
        }
        return recovered;
    }

    /// <summary>
    /// Called after a player is present in the World. Durable aggregate ownership is
    /// projected back to loaded dogs so $Leader, belongsPlayerId and ownedEntities all
    /// agree with the same owner after a relog.
    /// </summary>
    public static void ReconcileOwnerProjectionForPlayer(EntityPlayer player)
    {
        if (player == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
        string ownerId;
        if (!TryResolveOwnerId(player, out ownerId)) return;

        World world = player.world;

        // First undo the one-time v284 reconstruction health-CVar defect. This converts
        // only records explicitly stamped by v284 and stamps them again so a later real
        // death remains a real AwaitingRespawn state.
        RecoverV284FalseReconstructionDeaths(world, player);

        // Repair durable Active records that no longer have a physical dog projection.
        // This is intentionally performed after the owner is in the World so Follow dogs
        // can be reconstructed on verified terrain near the owner.
        RepairMissingActiveDogsForPlayer(world, player, "owner-spawn");

        // A dog entity can be loaded before its owning player exists. On pre-v231 saves
        // that means the initial stable-identity pass may not have enough durable owner
        // context to choose the old owned record. Retry once now that the player is live.
        // This is migration/recovery only; v231+ saves normally bind by their embedded ID.
        RebirthNpcRuntimeState[] liveStates = RebirthNpcRuntimeRegistry.GetSnapshot();
        for (int i = 0; i < liveStates.Length; i++)
        {
            RebirthNpcRuntimeState liveState = liveStates[i];
            if (liveState == null || !string.Equals(liveState.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase)) continue;
            int liveEntityId;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(liveState.StableId, out liveEntityId)) continue;
            EntityRebirthDogCompanion liveDog = world != null ? world.GetEntity(liveEntityId) as EntityRebirthDogCompanion : null;
            if (liveDog == null || liveDog.IsDead() || !liveDog.RebirthSavedStableIdForDebug.IsEmpty) continue;
            RebirthNpcStableIdentityStore.TryRestore(liveDog);
            RestorePersistedOwnershipAndOrder(liveDog);
        }

        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (record?.Identity == null || record.Profile == null || record.Ownership == null ||
                !string.Equals(record.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase)) continue;

            if ((record.Lifecycle != null && record.Lifecycle.TombstoneState) ||
                (record.Presence != null && string.Equals(record.Presence.PresenceState, RebirthNpcPresenceState.Removed.ToString(), StringComparison.OrdinalIgnoreCase)) ||
                (record.Dog != null && record.Dog.Lifecycle == RebirthDogLifecycleKind.PickedUp)) continue;

            int entityId;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(record.Identity.StableNpcId, out entityId)) continue;
            EntityRebirthDogCompanion dog = world != null ? world.GetEntity(entityId) as EntityRebirthDogCompanion : null;
            if (dog == null || dog.IsDead()) continue;

            RestorePersistedOwnershipAndOrder(dog);
            SynchronizeLegacyOwnerProjection(dog);
        }

        RebirthDogCapacitySyncService.NotifyOwner(player);
    }

    public static string GetOwnershipDebugReport(EntityPlayer player)
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Dog Ownership] v237 authoritative ownership audit");
        if (player == null)
        {
            b.Append("  player=<none>");
            return b.ToString().TrimEnd();
        }

        string ownerId;
        bool hasOwnerId = TryResolveOwnerId(player, out ownerId);
        int dogs = 0, total = 0, cap = 1;
        bool countsOk = TryGetOwnershipCounts(player, out dogs, out total, out cap);
        ItemValue meat = ItemClass.GetItem(HireItemName);
        int toolbeltMeat = !meat.IsEmpty() && player.inventory != null ? player.inventory.GetItemCount(meat, false, -1, -1) : 0;
        int backpackMeat = !meat.IsEmpty() && player.bag != null ? player.bag.GetItemCount(meat, -1, -1, false) : 0;

        b.Append("  playerEntity=").Append(player.entityId)
            .Append(" persistentOwner=").Append(hasOwnerId ? ownerId : "<unresolved>")
            .Append(" counts=").Append(countsOk ? (dogs + " dogs / " + total + " total / cap " + cap) : "<unavailable>")
            .Append(" rawMeat(toolbelt/backpack)=").Append(toolbeltMeat).Append('/').Append(backpackMeat)
            .AppendLine();

        b.AppendLine("  persistent records:");
        int persistentWritten = 0;
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView r = records[i];
            if (r?.Identity == null || r.Profile == null ||
                !string.Equals(r.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase)) continue;
            string recordOwner = r.Ownership != null ? (r.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty) : string.Empty;
            if (hasOwnerId && !string.Equals(recordOwner, ownerId, StringComparison.OrdinalIgnoreCase)) continue;

            bool removed = (r.Lifecycle != null && r.Lifecycle.TombstoneState) ||
                           (r.Presence != null && string.Equals(r.Presence.PresenceState, RebirthNpcPresenceState.Removed.ToString(), StringComparison.OrdinalIgnoreCase));
            bool picked = r.Dog != null && r.Dog.Lifecycle == RebirthDogLifecycleKind.PickedUp;
            bool contributes = !removed && !picked && !string.IsNullOrEmpty(recordOwner);
            int liveId;
            bool hasLive = RebirthNpcRuntimeRegistry.TryGetEntityId(r.Identity.StableNpcId, out liveId);
            b.Append("    stable=").Append(r.Identity.StableNpcId)
                .Append(" name='").Append(r.Identity.GeneratedOrAssignedDisplayName ?? string.Empty).Append("\'")
                .Append(" owner='").Append(recordOwner).Append("\'")
                .Append(" lifecycle=").Append(r.Dog != null ? r.Dog.Lifecycle.ToString() : "<none>")
                .Append(" presence=").Append(r.Presence != null ? r.Presence.PresenceState : "<none>")
                .Append(" contributes=").Append(contributes)
                .Append(" liveEntity=").Append(hasLive ? liveId.ToString() : "<none>");
            if (!hasLive && r.Dog != null && r.Dog.Lifecycle == RebirthDogLifecycleKind.Active)
                b.Append(" rollbackCleanup='rbdog forgetmissing ").Append(r.Identity.StableNpcId).Append("'");
            b.AppendLine();
            persistentWritten++;
        }
        if (persistentWritten == 0) b.AppendLine("    <none>");

        // v230-v234 incorrectly terminally removed hired dogs when `killall all/alive` was
        // used. List those records separately even though their owner field was cleared, so
        // the affected player can explicitly recover the exact StableId without save editing.
        b.AppendLine("  recoverable legacy killall records:");
        int recoverableWritten = 0;
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView r = records[i];
            if (r?.Identity == null || r.Profile == null || r.Lifecycle == null ||
                !string.Equals(r.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(r.Lifecycle.RemovalReason ?? string.Empty, "dog-admin-killall", StringComparison.OrdinalIgnoreCase)) continue;
            b.Append("    stable=").Append(r.Identity.StableNpcId)
                .Append(" name='").Append(r.Identity.GeneratedOrAssignedDisplayName ?? string.Empty).Append("'")
                .Append(" use: rbdog recoverkillall ").Append(r.Identity.StableNpcId)
                .AppendLine();
            recoverableWritten++;
        }
        if (recoverableWritten == 0) b.AppendLine("    <none>");

        b.AppendLine("  live dog projections:");
        int liveWritten = 0;
        RebirthNpcRuntimeState[] runtime = RebirthNpcRuntimeRegistry.GetSnapshot();
        for (int i = 0; i < runtime.Length; i++)
        {
            RebirthNpcRuntimeState state = runtime[i];
            if (state == null || !string.Equals(state.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase)) continue;
            if (hasOwnerId && state.OwnershipKind == RebirthNpcOwnershipKind.Player &&
                !string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase)) continue;
            int entityId;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId)) entityId = -1;
            EntityRebirthDogCompanion dog = player.world != null && entityId > 0 ? player.world.GetEntity(entityId) as EntityRebirthDogCompanion : null;
            int leader = dog != null && dog.Buffs != null ? Mathf.RoundToInt(dog.Buffs.GetCustomVar("$Leader")) : 0;
            bool ownerCollection = dog != null && player.HasOwnedEntity(dog.entityId);
            b.Append("    entity=").Append(entityId)
                .Append(" stable=").Append(state.StableId)
                .Append(" runtimeOwner=").Append(state.OwnershipKind).Append(':').Append(state.OwnerId ?? string.Empty)
                .Append(" presence=").Append(state.Presence)
                .Append(" belongsPlayerId=").Append(dog != null ? dog.belongsPlayerId : 0)
                .Append(" $Leader=").Append(leader)
                .Append(" playerOwnedSet=").Append(ownerCollection);
            if (dog != null)
                b.Append(" embeddedStable=").Append(dog.RebirthSavedStableIdForDebug)
                 .Append(" embeddedOwner='").Append(dog.RebirthSavedOwnerIdForDebug).Append("\'");
            b.AppendLine();
            liveWritten++;
        }
        if (liveWritten == 0) b.AppendLine("    <none>");

        b.Append("  identityStore: ").Append(RebirthNpcStableIdentityStore.GetReport()).AppendLine();
        b.Append("  timeline: ").Append(RebirthNpcPersistenceTimelineService.GetReport());
        return b.ToString().TrimEnd();
    }

    private static bool IsAuthoritative(World world)
    {
        return world != null && !world.IsRemote() && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;
    }

    private static void CountOwnedCompanions(string ownerId, out int dogCount, out int totalCount)
    {
        dogCount = 0;
        totalCount = 0;

        // v231 ownership rule: the persistent aggregate is authoritative for any
        // companion that has a durable record. Runtime state is only a fallback for
        // newly-created ownership-capable entities that have not been persisted yet.
        // This prevents a suspended live projection from keeping a picked-up dog in
        // the cap and prevents transient relog identity failures from inventing counts.
        HashSet<RebirthNpcStableId> seen = new HashSet<RebirthNpcStableId>();
        HashSet<RebirthNpcStableId> excluded = new HashSet<RebirthNpcStableId>();

        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (record?.Identity == null || record.Ownership == null) continue;
            RebirthNpcStableId stableId = record.Identity.StableNpcId;
            if (stableId.IsEmpty) continue;
            if (!string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase)) continue;

            // PC009: bound undead use the canonical NPC aggregate but deliberately do not
            // masquerade as a dog/NPC profile. Count an already-bound durable record toward
            // the global companion safety ceiling before profile resolution. Conditioning-only
            // records are not companions and do not consume this global slot.
            if (record.BoundUndead != null && record.BoundUndead.IsBound)
            {
                bool boundRemoved = (record.Lifecycle != null && record.Lifecycle.TombstoneState) ||
                    (record.Presence != null && string.Equals(record.Presence.PresenceState,
                        RebirthNpcPresenceState.Removed.ToString(), StringComparison.OrdinalIgnoreCase));
                if (boundRemoved) { excluded.Add(stableId); continue; }
                if (seen.Add(stableId)) totalCount++;
                continue;
            }

            if (record.Profile == null) continue;
            RebirthNpcProfile profile;
            if (!RebirthNpcProfileRegistry.TryResolve(record.Profile.ProfileId, out profile) || !profile.Has(RebirthNpcCapabilities.Ownership)) continue;

            bool removed = (record.Lifecycle != null && record.Lifecycle.TombstoneState) ||
                           (record.Presence != null && string.Equals(record.Presence.PresenceState,
                               RebirthNpcPresenceState.Removed.ToString(), StringComparison.OrdinalIgnoreCase));
            bool pickedUpDog = profile.Category == RebirthNpcCategory.DogCompanion &&
                               record.Dog != null && record.Dog.Lifecycle == RebirthDogLifecycleKind.PickedUp;

            if (removed || pickedUpDog)
            {
                excluded.Add(stableId);
                continue;
            }

            if (!seen.Add(stableId)) continue;
            totalCount++;
            if (profile.Category == RebirthNpcCategory.DogCompanion && !(record.Dog != null && record.Dog.IsTamedWild)) dogCount++;
        }

        RebirthNpcRuntimeState[] runtime = RebirthNpcRuntimeRegistry.GetSnapshot();
        for (int i = 0; i < runtime.Length; i++)
        {
            RebirthNpcRuntimeState state = runtime[i];
            if (state == null || state.Presence == RebirthNpcPresenceState.Removed ||
                state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                !string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase) ||
                excluded.Contains(state.StableId) || seen.Contains(state.StableId)) continue;

            RebirthNpcProfile profile;
            if (!RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) || !profile.Has(RebirthNpcCapabilities.Ownership)) continue;
            if (!seen.Add(state.StableId)) continue;
            totalCount++;
            RebirthNpcPersistentRecordView runtimeRecord;
            bool runtimeWild = RebirthNpcAggregatePersistenceStore.TryGetView(state.StableId,out runtimeRecord) && runtimeRecord != null && runtimeRecord.Dog != null && runtimeRecord.Dog.IsTamedWild;
            if (profile.Category == RebirthNpcCategory.DogCompanion && !runtimeWild) dogCount++;
        }
    }

}

/// <summary>
/// Server-authoritative companion capacity mirror used only by remote placement preflight.
/// It closes the prediction gap where a dedicated client cannot see an unloaded persistent dog
/// but the server still counts that dog. A denial can therefore happen before native placement,
/// preventing item loss and the success+denied double-audio path.
/// </summary>
public static class RebirthDogCapacitySyncService
{
    private static readonly object Sync = new object();
    private static int clientPlayerEntityId = -1;
    private static int clientDogCount;
    private static int clientTotalCount;
    private static int clientGlobalCap = 1;
    private static long clientRevision;
    private static World clientWorld;

    public static bool TryGetClientCounts(EntityPlayer player, out int dogs, out int total, out int globalCap)
    {
        dogs = 0; total = 0; globalCap = 1;
        if (player == null) return false;
        lock (Sync)
        {
            if (!object.ReferenceEquals(clientWorld, player.world)) return false;
            if (clientPlayerEntityId != player.entityId || clientRevision <= 0) return false;
            dogs = clientDogCount; total = clientTotalCount; globalCap = clientGlobalCap;
            return true;
        }
    }

    public static void Receive(int playerEntityId, int dogs, int total, int globalCap, long revision)
    {
        World currentWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
        lock (Sync)
        {
            if (!object.ReferenceEquals(clientWorld, currentWorld))
            {
                clientWorld = currentWorld;
                clientPlayerEntityId = -1;
                clientDogCount = 0;
                clientTotalCount = 0;
                clientGlobalCap = 1;
                clientRevision = 0;
            }
            if (revision < clientRevision && clientPlayerEntityId == playerEntityId) return;
            clientPlayerEntityId = playerEntityId;
            clientDogCount = Math.Max(0, dogs);
            clientTotalCount = Math.Max(0, total);
            clientGlobalCap = Math.Max(1, globalCap);
            clientRevision = Math.Max(1L, revision);
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            clientPlayerEntityId = -1;
            clientDogCount = 0;
            clientTotalCount = 0;
            clientGlobalCap = 1;
            clientRevision = 0;
            clientWorld = null;
        }
    }

    public static void NotifyOwner(EntityPlayer player)
    {
        if (player == null || GameManager.Instance == null) return;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance.World;
        if (connection == null || !connection.IsServer || world == null || world.IsRemote()) return;

        int dogs, total, cap;
        if (!RebirthDogLifecycleService.TryGetOwnershipCounts(player, out dogs, out total, out cap)) return;
        RebirthDogNavigationMarkerService.NotifyOwner(player);
        long revision = DateTime.UtcNow.Ticks;

        // A local listen/single-player owner already reads the authoritative aggregate
        // directly. Remote players still need the server mirror.
        if (!GameManager.IsDedicatedServer && player is EntityPlayerLocal) return;
        try
        {
            connection.SendPackage(
                NetPackageManager.GetPackage<NetPackageRebirthDogCapacitySync>()
                    .Setup(player.entityId, dogs, total, cap, revision),
                _attachedToEntityId: player.entityId);
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Dog] capacity sync failed player=" + player.entityId +
                " error=" + ex.GetType().Name + ": " + ex.Message);
        }
    }

    public static void NotifyClient(ClientInfo clientInfo)
    {
        if (clientInfo == null || GameManager.Instance == null) return;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer) return;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(clientInfo.InternalId);
        EntityPlayer player = persistent != null ? GameManager.Instance.World?.GetEntity(persistent.EntityId) as EntityPlayer : null;
        NotifyOwner(player);
    }

    public static void NotifyAllPlayers()
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null) return;
        List<EntityPlayer> players = GameManager.Instance.World.GetPlayers();
        if (players == null) return;
        for (int i = 0; i < players.Count; i++) NotifyOwner(players[i]);
    }
}

[Preserve]
public sealed class NetPackageRebirthDogCapacitySync : NetPackage
{
    private int playerEntityId;
    private int dogCount;
    private int totalCount;
    private int globalCap;
    private long revision;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthDogCapacitySync Setup(int playerId, int dogs, int total, int cap, long rev)
    {
        playerEntityId = playerId; dogCount = dogs; totalCount = total; globalCap = cap; revision = rev;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        playerEntityId = binary.ReadInt32();
        dogCount = binary.ReadInt32();
        totalCount = binary.ReadInt32();
        globalCap = binary.ReadInt32();
        revision = binary.ReadInt64();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        PooledBinaryWriter binary = writer;
        binary.Write(playerEntityId);
        binary.Write(dogCount);
        binary.Write(totalCount);
        binary.Write(globalCap);
        binary.Write(revision);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthDogCapacitySyncService.Receive(playerEntityId, dogCount, totalCount, globalCap, revision);
    }

    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthDogHireRequest : NetPackage
{
    private int playerEntityId;
    private PlatformUserIdentifierAbs userId;
    private int dogEntityId;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthDogHireRequest Setup(int playerId, PlatformUserIdentifierAbs persistentUserId, int targetDogEntityId)
    {
        playerEntityId = playerId;
        userId = persistentUserId;
        dogEntityId = targetDogEntityId;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        playerEntityId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        dogEntityId = binary.ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        PooledBinaryWriter binary = writer;
        binary.Write(playerEntityId);
        userId.ToStream(binary);
        binary.Write(dogEntityId);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || userId == null ||
            !ValidEntityIdForSender(playerEntityId) || !ValidUserIdForSender(userId)) return;
        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        EntityRebirthDogCompanion dog = world.GetEntity(dogEntityId) as EntityRebirthDogCompanion;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerEntityId);
        if (player == null || dog == null || persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(userId)) return;
        string reason;
        bool success = RebirthDogLifecycleService.TryHire(world, player, dog, out reason);
        if (!string.IsNullOrEmpty(reason)) GameManager.ShowTooltipMP(player, reason, success ? "ui_success" : "ui_denied");
    }

    public int GetLength() => 0;
}

internal static class RebirthDogDeploymentRefunds
{
    public static bool TryBuild(EntityPlayer player, string placedItemName, string stableIdText, string nonce,
        RebirthDogBreedDefinition breed, out ItemValue refund)
    {
        refund = null;
        if (breed == null || string.IsNullOrWhiteSpace(placedItemName)) return false;
        if (string.IsNullOrWhiteSpace(stableIdText) && string.IsNullOrWhiteSpace(nonce))
        {
            ItemValue fresh = ItemClass.GetItem(placedItemName);
            if (fresh == null || fresh.IsEmpty()) return false;
            refund = fresh;
            return true;
        }

        RebirthNpcStableId stableId;
        RebirthNpcPersistentRecordView record; RebirthDogPersistentRecordView dogRecord; string ownerId;
        if (!RebirthNpcStableId.TryParse(stableIdText, out stableId) ||
            !RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId) ||
            !RebirthDogStateService.TryGetView(stableId, out record, out dogRecord) || record == null || dogRecord == null ||
            record.Ownership == null || !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId, ownerId, StringComparison.OrdinalIgnoreCase) ||
            dogRecord.Lifecycle != RebirthDogLifecycleKind.PickedUp || dogRecord.PickupNonceConsumed ||
            !string.Equals(dogRecord.PickupNonce, nonce, StringComparison.Ordinal) ||
            !string.Equals(dogRecord.BreedId, breed.BreedId, StringComparison.OrdinalIgnoreCase)) return false;

        ItemValue rebuilt = ItemClass.GetItem(placedItemName);
        if (rebuilt == null || rebuilt.IsEmpty()) return false;
        rebuilt.SetMetadata("RebirthDogStableId", stableId.ToString());
        rebuilt.SetMetadata("RebirthDogNonce", dogRecord.PickupNonce ?? string.Empty);
        rebuilt.SetMetadata("RebirthDogBreedId", dogRecord.BreedId ?? breed.BreedId);
        if (record.Identity != null) rebuilt.SetMetadata("NPCName", record.Identity.GeneratedOrAssignedDisplayName ?? string.Empty);
        if (record.Vitals != null) rebuilt.SetMetadata("health", Math.Max(0, record.Vitals.CurrentHealth));
        rebuilt.SetMetadata("RebirthDogInventorySlots", RebirthDogStateService.InventorySlots);
        rebuilt.SetMetadata("RebirthDogInventoryRevision", record.Inventory != null ? (int)record.Inventory.InventoryRevision : 0);
        refund = rebuilt;
        return true;
    }
}

[Preserve]
public sealed class NetPackageRebirthDogDeployRequest : NetPackage
{
    private int playerEntityId;
    private PlatformUserIdentifierAbs userId;
    private Vector3i blockPosition;
    private string breedId;
    private ItemValue capsuleItem;
    private bool traceRequested;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthDogDeployRequest Setup(
        int playerId,
        PlatformUserIdentifierAbs persistentUserId,
        Vector3i position,
        string requestedBreedId,
        ItemValue heldItem,
        bool requestTrace)
    {
        playerEntityId = playerId;
        userId = persistentUserId;
        blockPosition = position;
        breedId = requestedBreedId ?? string.Empty;
        capsuleItem = heldItem != null ? heldItem.Clone() : null;
        traceRequested = requestTrace;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        playerEntityId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        blockPosition = StreamUtils.ReadVector3i(binary);
        breedId = binary.ReadString();
        capsuleItem = ItemValue.ReadOrNull(binary);
        traceRequested = binary.ReadBoolean();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        PooledBinaryWriter binary = writer;
        binary.Write(playerEntityId);
        userId.ToStream(binary);
        StreamUtils.Write(binary, blockPosition);
        binary.Write(breedId ?? string.Empty);
        ItemValue.Write(capsuleItem, binary);
        binary.Write(traceRequested);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthDogDeploymentDebug.TraceServer(traceRequested,
            "DeployRequest RECEIVE player=" + playerEntityId +
            " breed=" + (breedId ?? "<null>") +
            " pos=" + blockPosition +
            " worldRemote=" + (world != null && world.IsRemote()));

        if (world == null || world.IsRemote() || userId == null ||
            !ValidEntityIdForSender(playerEntityId) || !ValidUserIdForSender(userId))
        {
            RebirthDogDeploymentDebug.TraceServer(traceRequested,
                "DeployRequest REJECT sender/world validation failed.");
            return;
        }

        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerEntityId);
        if (player == null || persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(userId))
        {
            RebirthDogDeploymentDebug.TraceServer(traceRequested,
                "DeployRequest REJECT player/persistent identity validation failed.");
            return;
        }

        RebirthDogBreedDefinition breed;
        if (!RebirthDogDefinitions.TryGetByBreedId(breedId, out breed) || breed == null)
        {
            RebirthDogDeploymentDebug.TraceServer(traceRequested,
                "DeployRequest REJECT unknown breedId='" + (breedId ?? string.Empty) + "'.");
            GameManager.ShowTooltipMP(player, Localization.Get("xuiRebirthDogDeployFailed"), "ui_denied");
            return;
        }

        BlockValue placedValue = world.GetBlock(blockPosition);
        Block placedBlock = placedValue.Block;
        string placedName = placedBlock != null ? placedBlock.GetBlockName() : string.Empty;
        string placedBreedId = string.Empty;
        placedBlock?.Properties?.Values?.TryGetValue("RebirthDogBreedId", out placedBreedId);

        bool blockClassOk = !placedValue.isair && placedBlock is RebirthDogDeployBlock;
        bool blockNameOk = string.Equals(placedName, breed.SpawnDeployId, StringComparison.Ordinal) ||
                           string.Equals(placedName, breed.PickedUpDeployId, StringComparison.Ordinal);
        bool breedOk = string.Equals(placedBreedId, breed.BreedId, StringComparison.OrdinalIgnoreCase);

        RebirthDogDeploymentDebug.TraceServer(traceRequested,
            "DeployRequest blockCheck name='" + placedName +
            "' clr='" + (placedBlock != null ? placedBlock.GetType().FullName : "<null>") +
            "' configuredBreed='" + placedBreedId +
            "' classOk=" + blockClassOk + " nameOk=" + blockNameOk + " breedOk=" + breedOk +
            " rotation=" + placedValue.rotation);

        if (!blockClassOk || !blockNameOk || !breedOk)
        {
            RebirthDogDeploymentDebug.TraceServer(traceRequested,
                "DeployRequest REJECT live placed block did not match the requested dog deployment token.");
            GameManager.ShowTooltipMP(player, Localization.Get("xuiRebirthDogDeployFailed"), "ui_denied");
            return;
        }

        Vector3 blockCenter = blockPosition.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
        float distance = Vector3.Distance(player.position, blockCenter);
        if (distance > 8f)
        {
            RebirthDogDeploymentDebug.TraceServer(traceRequested,
                "DeployRequest REJECT distance=" + distance.ToString("0.00") + " > 8m.");
            GameManager.ShowTooltipMP(player, Localization.Get("xuiRebirthDogDeployInvalidPlacement"), "ui_denied");
            return;
        }

        string capsuleStableId = string.Empty;
        string capsuleNonce = string.Empty;
        if (capsuleItem != null)
        {
            capsuleItem.TryGetMetadata("RebirthDogStableId", out capsuleStableId);
            capsuleItem.TryGetMetadata("RebirthDogNonce", out capsuleNonce);
        }

        Vector3 spawnPosition = blockPosition.ToVector3() + new Vector3(0.5f, 0.25f, 0.5f);
        Quaternion spawnRotation = Quaternion.Euler(0f, 90f * (placedValue.rotation & 3), 0f);
        EntityRebirthDogCompanion dog;
        string reason;
        bool success = RebirthDogLifecycleService.TryDeploy(
            world, player, breed, spawnPosition, spawnRotation,
            out dog, out reason, capsuleStableId, capsuleNonce, capsuleItem);

        RebirthDogDeploymentDebug.TraceServer(traceRequested,
            "DeployRequest TryDeploy success=" + success +
            " reason='" + (reason ?? string.Empty) +
            "' spawnedEntity=" + (dog != null ? dog.entityId.ToString() : "<none>") +
            " spawn=" + spawnPosition + " yaw=" + (90f * (placedValue.rotation & 3)));

        // The placement block is only a deployment token. Remove it after the
        // authoritative attempt, matching the 2.6 ephemeral spawn-cube behavior.
        world.SetBlockRPC((BlockValueRef)blockPosition, BlockValue.Air, playerEntityId);

        if (success)
        {
            if (!string.IsNullOrEmpty(reason))
                GameManager.ShowTooltipMP(player, reason, "ui_success");
            return;
        }

        // Native block placement already consumed the token on the requesting
        // client. Restore one token on an authoritative rejection, preserving
        // the previous local/listen-server failure semantics.
        ItemValue refund;
        if (!RebirthDogDeploymentRefunds.TryBuild(player, placedName, capsuleStableId, capsuleNonce, breed, out refund))
            refund = null;
        bool refunded = false;
        if (refund != null && !refund.IsEmpty())
        {
            ItemStack refundStack = new ItemStack(refund, 1);
            refunded = player.inventory != null && player.inventory.AddItem(refundStack);
            if (!refunded && player.bag != null)
                refunded = player.bag.AddItem(new ItemStack(refund, 1));
            if (!refunded && GameManager.Instance != null)
            {
                GameManager.Instance.ItemDropServer(new ItemStack(refund, 1), player.position + Vector3.up,
                    Vector3.zero, player.entityId, 60f, false);
                refunded = true;
            }
        }
        RebirthDogDeploymentDebug.TraceServer(traceRequested,
            "DeployRequest failure refund=" + refunded +
            " item='" + (refund != null && !refund.IsEmpty() && refund.ItemClass != null ? refund.ItemClass.GetItemName() : "<none>") + "'.");

        GameManager.ShowTooltipMP(player,
            string.IsNullOrEmpty(reason) ? Localization.Get("xuiRebirthDogDeployFailed") : reason,
            "ui_denied");
    }

    public int GetLength() => 0;
}

[Preserve]
public class RebirthDogDeployBlock : Block
{
    private string breedId;

    public override void Init()
    {
        base.Init();
        if (Properties != null && Properties.Values != null)
            Properties.Values.TryGetValue("RebirthDogBreedId", out breedId);

        RebirthDogDeploymentDebug.Trace(
            "Block.Init name='" + GetBlockName() + "' breed='" + (breedId ?? string.Empty) + "'.");
    }

    public override bool CanPlaceBlockAt(WorldBase world, Vector3i blockPos, BlockValue blockValue, bool omitCollideCheck = false)
    {
        // 2.6 placement gate: the 1x1x2 companion block must fit below the
        // historical world ceiling and pass the normal multiblock/collision check.
        if (blockPos.y + 2 >= 254)
        {
            RebirthDogDeploymentDebug.TraceGate("CanPlace=false reason=world-ceiling block='" + GetBlockName() + "' pos=" + blockPos);
            return false;
        }
        if (!base.CanPlaceBlockAt(world, blockPos, blockValue, omitCollideCheck))
        {
            RebirthDogDeploymentDebug.TraceGate("CanPlace=false reason=base-collision block='" + GetBlockName() + "' pos=" + blockPos);
            return false;
        }

        // Check dog capacity before the native item action consumes the deploy item
        // or emits the normal placement sound. Returning false lets the
        // native placement path play only its denial feedback; no temporary dog block
        // exists and no refund is needed. Dog deployments intentionally have no cooldown.
        World liveWorld = world as World;
        EntityPlayer player = liveWorld != null ? liveWorld.GetPrimaryPlayer() as EntityPlayer : null;
        // Native ItemActionPlaceAsBlock consumes after the void PlaceBlock callback returns.
        // Reject here as well, before that action enters its placement/consumption branch.
        if (liveWorld != null && liveWorld.IsRemote())
        {
            PersistentPlayerData persistent = player != null
                ? GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId) : null;
            if (persistent?.PrimaryId == null || !RebirthMusicLibraryClient.CanSend(
                SingletonMonoBehaviour<ConnectionManager>.Instance,
                NetPackageManager.GetPackage<NetPackageRebirthDogDeployRequest>()))
                return false;
        }
        if (player != null)
        {
            string reason;
            if (!RebirthDogLifecycleService.CanAcquire(player, out reason))
            {
                RebirthDogDeploymentDebug.TraceGate("CanPlace=false reason=CanAcquire:'" + (reason ?? string.Empty) + "' block='" + GetBlockName() + "' player=" + player.entityId);
                return false;
            }
        }
        return true;
    }

    public override void PlaceBlock(WorldBase world, BlockPlacement.Result result, EntityAlive placingEntity)
    {
        World liveWorld = world as World;
        EntityPlayer player = placingEntity as EntityPlayer;
        RebirthDogBreedDefinition breed;
        if (liveWorld == null || player == null || !RebirthDogDefinitions.TryGetByBreedId(breedId, out breed) || breed == null)
        {
            RebirthDogDeploymentDebug.Trace(
                "PlaceBlock EXIT world/player/breed resolution failed world=" + (liveWorld != null) +
                " player=" + (player != null) + " breed='" + (breedId ?? string.Empty) + "'.");
            return;
        }

        // Defensive preflight in case a caller bypasses CanPlaceBlockAt. Crucially this is
        // still BEFORE base.PlaceBlock. Native item consumption is guarded by CanPlaceBlockAt;
        // returning from this void callback alone cannot cancel the caller consumption.
        string preflightReason = string.Empty;
        if (!RebirthDogLifecycleService.CanAcquire(player, out preflightReason))
        {
            RebirthDogDeploymentDebug.Trace("PlaceBlock DENIED before base placement reason='" + preflightReason + "'.");
            GameManager.ShowTooltipMP(player, preflightReason, "ui_denied");
            return;
        }

        string capsuleStableId = string.Empty;
        string capsuleNonce = string.Empty;
        ItemValue heldBeforePlacement = player.inventory?.holdingItemItemValue?.Clone();
        if (heldBeforePlacement != null)
        {
            heldBeforePlacement.TryGetMetadata("RebirthDogStableId", out capsuleStableId);
            heldBeforePlacement.TryGetMetadata("RebirthDogNonce", out capsuleNonce);
        }

        RebirthDogDeploymentDebug.Trace(
            "PlaceBlock ENTER block='" + GetBlockName() +
            "' breed='" + (breedId ?? string.Empty) +
            "' pos=" + result.blockPos +
            " rotation=" + result.blockValue.rotation +
            " player=" + player.entityId +
            " worldRemote=" + world.IsRemote() +
            " capsuleStable='" + capsuleStableId + "'.");

        // Recheck request prerequisites before issuing native placement.
        // Keep the server's sender/owner checks unchanged; never infer an owner.
        PersistentPlayerData persistent = null;
        ConnectionManager connection = null;
        NetPackageRebirthDogDeployRequest deployRequest = null;
        if (liveWorld.IsRemote())
        {
            persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
            connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            deployRequest = NetPackageManager.GetPackage<NetPackageRebirthDogDeployRequest>();
            if (persistent?.PrimaryId == null || !RebirthMusicLibraryClient.CanSend(connection, deployRequest))
            {
                RebirthDogDeploymentDebug.Trace(
                    "PlaceBlock REMOTE denied before placement: identity or request channel unavailable.");
                GameManager.ShowTooltipMP(player, Localization.Get("xuiRebirthDogCapacitySyncPending"), "ui_denied");
                return;
            }
        }

        base.PlaceBlock(world, result, placingEntity);

        if (liveWorld.IsRemote())
        {
            connection.SendToServer(
                deployRequest.Setup(player.entityId, persistent.PrimaryId, result.blockPos, breed.BreedId,
                    heldBeforePlacement, RebirthDogDeploymentDebug.Enabled));
            RebirthDogDeploymentDebug.Trace(
                "PlaceBlock REMOTE deploy request queued player=" + player.entityId +
                " breed='" + breed.BreedId + "' pos=" + result.blockPos + ".");
            return;
        }

        string reason = string.Empty;
        EntityRebirthDogCompanion dog = null;
        Vector3 spawnPosition = result.blockPos.ToVector3() + new Vector3(0.5f, 0.25f, 0.5f);
        Quaternion spawnRotation = Quaternion.Euler(0f, 90f * (result.blockValue.rotation & 3), 0f);
        bool success = RebirthDogLifecycleService.TryDeploy(liveWorld, player, breed, spawnPosition,
            spawnRotation, out dog, out reason, capsuleStableId, capsuleNonce, heldBeforePlacement);

        RebirthDogDeploymentDebug.Trace(
            "PlaceBlock LOCAL TryDeploy success=" + success +
            " reason='" + (reason ?? string.Empty) +
            "' spawnedEntity=" + (dog != null ? dog.entityId.ToString() : "<none>") +
            " spawn=" + spawnPosition + " yaw=" + (90f * (result.blockValue.rotation & 3)));

        world.SetBlockRPC((BlockValueRef)result.blockPos, BlockValue.Air, player.entityId);
        if (success)
            return;

        ItemValue refund;
        if (!RebirthDogDeploymentRefunds.TryBuild(player, GetBlockName(), capsuleStableId, capsuleNonce, breed, out refund))
            refund = null;
        if (refund != null && !refund.IsEmpty())
        {
            bool refunded = player.inventory != null && player.inventory.AddItem(new ItemStack(refund, 1));
            if (!refunded && player.bag != null) player.bag.AddItem(new ItemStack(refund, 1));
        }
        GameManager.ShowTooltipMP(player,
            string.IsNullOrEmpty(reason) ? Localization.Get("xuiRebirthDogDeployFailed") : reason,
            "ui_denied");
    }
}

public static class RebirthDogP2AuthoringValidator
{
    public static string Validate()
    {
        int resolved = 0;
        List<string> errors = new List<string>();
        RebirthDogBreedDefinition[] breeds = RebirthDogDefinitions.GetSnapshot();
        for (int i = 0; i < breeds.Length; i++)
        {
            RebirthDogBreedDefinition breed = breeds[i];
            BlockValue value = Block.GetBlockValue(breed.SpawnDeployId);
            if (value.isair || value.Block == null)
            {
                errors.Add("missing deploy block " + breed.SpawnDeployId);
                continue;
            }
            if (!(value.Block is RebirthDogDeployBlock))
            {
                errors.Add("deploy block " + breed.SpawnDeployId + " is not RebirthDogDeployBlock");
                continue;
            }
            string configuredBreed = string.Empty;
            value.Block.Properties?.Values?.TryGetValue("RebirthDogBreedId", out configuredBreed);
            if (!string.Equals(configuredBreed, breed.BreedId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("deploy block " + breed.SpawnDeployId + " breedId='" + configuredBreed + "' expected='" + breed.BreedId + "'");
                continue;
            }
            resolved++;
        }
        string report = "[REBIRTH Dog P2] acquisition authoring validation result=" +
            (errors.Count == 0 && resolved == RebirthDogDefinitions.ExpectedBreedCount ? "PASS" : "FAIL") +
            " deployBlocks=" + resolved + "/" + RebirthDogDefinitions.ExpectedBreedCount +
            " errors=" + errors.Count;
        for (int i = 0; i < errors.Count; i++) report += "\n  ERROR: " + errors[i];
        return report;
    }
}

public enum RebirthDogLifecycleRequestKind : byte
{
    Pickup = 0,
    ReportForDuty = 1,
    Dismiss = 2,
    ReservedLegacyPet = 3
}

[Preserve]
public sealed class NetPackageRebirthDogLifecycleRequest : NetPackage
{
    private int playerEntityId;
    private PlatformUserIdentifierAbs userId;
    private string stableId;
    private RebirthDogLifecycleRequestKind kind;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthDogLifecycleRequest Setup(int playerId, PlatformUserIdentifierAbs persistentUserId,
        string dogStableId, RebirthDogLifecycleRequestKind requestKind)
    {
        playerEntityId = playerId; userId = persistentUserId; stableId = dogStableId ?? string.Empty; kind = requestKind; return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        playerEntityId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        stableId = binary.ReadString();
        kind = (RebirthDogLifecycleRequestKind)binary.ReadByte();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        PooledBinaryWriter binary = writer;
        binary.Write(playerEntityId); userId.ToStream(binary); binary.Write(stableId ?? string.Empty); binary.Write((byte)kind);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || userId == null || !ValidEntityIdForSender(playerEntityId) || !ValidUserIdForSender(userId)) return;
        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerEntityId);
        RebirthNpcStableId id;
        if (player == null || persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(userId) || !RebirthNpcStableId.TryParse(stableId, out id)) return;
        string reason = string.Empty; bool success = false;
        if (kind == RebirthDogLifecycleRequestKind.Pickup)
        {
            int entityId;
            EntityRebirthDogCompanion dog = RebirthNpcRuntimeRegistry.TryGetEntityId(id, out entityId) ? world.GetEntity(entityId) as EntityRebirthDogCompanion : null;
            success = RebirthDogLifecycleService.TryPickup(world, player, dog, out reason);
        }
        else if (kind == RebirthDogLifecycleRequestKind.ReportForDuty)
        {
            EntityRebirthDogCompanion dog;
            success = RebirthDogLifecycleService.TryReportForDuty(world, player, id, out dog, out reason);
        }
        else if (kind == RebirthDogLifecycleRequestKind.Dismiss)
        {
            int entityId;
            EntityRebirthDogCompanion dog = RebirthNpcRuntimeRegistry.TryGetEntityId(id, out entityId)
                ? world.GetEntity(entityId) as EntityRebirthDogCompanion
                : null;
            success = dog != null
                ? RebirthDogLifecycleService.TryDismiss(world, player, dog, out reason)
                : RebirthDogLifecycleService.TryDismissMissingRecord(player, id, out reason);
        }
        if (!string.IsNullOrEmpty(reason)) GameManager.ShowTooltipMP(player, reason, success ? "ui_success" : "ui_denied");
    }

    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthDogPickupFeedback : NetPackage
{
    private int playerEntityId;
    private int dogEntityId;
    private string stableId;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthDogPickupFeedback Setup(int playerId, int entityId, string dogStableId)
    {
        playerEntityId = playerId;
        dogEntityId = entityId;
        stableId = dogStableId ?? string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        playerEntityId = reader.ReadInt32();
        dogEntityId = reader.ReadInt32();
        stableId = reader.ReadString();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        // PooledBinaryWriter exposes ReadOnlySpan overloads in 3.1. RebirthUtils targets
        // the legacy compiler/runtime surface, so bind through BinaryWriter to avoid CS7069.
        PooledBinaryWriter binary = writer;
        binary.Write(playerEntityId);
        binary.Write(dogEntityId);
        binary.Write(stableId ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.GetPrimaryPlayerId() != playerEntityId) return;
        // Retire the REBIRTH projection immediately instead of waiting for the base entity
        // removal packet. This makes the HUD/companion roster react like drone pickup.
        RebirthDogRuntimeService.Remove(dogEntityId);
        RebirthDogStorageService.Remove(dogEntityId);
        RebirthNpcLifecycle.OnEntityDeactivated(dogEntityId, false);
        if (!string.IsNullOrEmpty(stableId)) RebirthDogNavigationMarkerService.Remove(stableId);
        try { Audio.Manager.PlayInsidePlayerHead("item_pickup"); } catch { }
    }

    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthDogRenameRequest : NetPackage
{
    private int playerEntityId;
    private PlatformUserIdentifierAbs userId;
    private string stableId;
    private string requestedName;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthDogRenameRequest Setup(int playerId, PlatformUserIdentifierAbs persistentUserId, string dogStableId, string name)
    {
        playerEntityId = playerId; userId = persistentUserId; stableId = dogStableId ?? string.Empty; requestedName = name ?? string.Empty; return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        playerEntityId = binary.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(binary);
        stableId = binary.ReadString(); requestedName = binary.ReadString();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); PooledBinaryWriter binary = writer;
        binary.Write(playerEntityId); userId.ToStream(binary); binary.Write(stableId ?? string.Empty); binary.Write(requestedName ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || userId == null || !ValidEntityIdForSender(playerEntityId) || !ValidUserIdForSender(userId)) return;
        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerEntityId);
        RebirthNpcStableId id;
        if (player == null || persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(userId) || !RebirthNpcStableId.TryParse(stableId, out id)) return;
        string reason; bool success = RebirthDogLifecycleService.TryRename(player, id, requestedName, out reason);
        if (!string.IsNullOrEmpty(reason)) GameManager.ShowTooltipMP(player, reason, success ? "ui_success" : "ui_denied");
    }

    public int GetLength() => 0;
}
