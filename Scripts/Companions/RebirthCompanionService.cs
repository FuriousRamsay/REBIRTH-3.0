using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthCompanionTargetKind : byte
{
    Drone = 0,
    Npc = 1
}

public enum RebirthCompanionCommand : byte
{
    LockToPlayer = 0,
    Unlock = 1,
    Follow = 2,
    Stay = 3,
    Guard = 4,
    Recall = 5,
    Hunting = 6,
    FullControl = 7,
    Stop = 8,
    Resume = 9,
    QuietToggle = 10,
    LightToggle = 11,
    HealMe = 12,
    ToggleHealAllies = 13,
    LockAccess = 14,
    UnlockFromPlayer = 15,
    PickUp = 16,
    ReportForDuty = 17,
    AdminRemove = 18,
    Rename = 19,
    Storage = 20,
    SetRespawnPoint = 21,
    CreateWaypoint = 22,
    Dismiss = 23,
    GuardArea = 24,
    TeleportFollowers = 25,
    DeployHorrorPanther = 26,
    DeployRagePanther = 27
}

public enum RebirthCompanionInventoryAction : byte
{
    ToggleLock = 0,
    UseOne = 1,
    TransferToPlayer = 2
}

public enum RebirthCompanionBehaviorMode : byte
{
    FullControl = 0,
    Hunting = 1
}

public sealed class RebirthCompanionListEntry
{
    public string Id = string.Empty;
    public RebirthCompanionTargetKind Kind;
    public int EntityId = -1;
    public string StableId = string.Empty;
    public string Name = string.Empty;
    public string Type = string.Empty;
    public string Status = string.Empty;
    public string Icon = string.Empty;
    public float Distance;
    public float DirectionAngle;
    public Vector3 WorldPosition;
    public int Health;
    public int MaxHealth;
    public int StorageUsed;
    public int StorageTotal;
    public string Profile = string.Empty;
    public string Order = string.Empty;
    // Two independent drone states: the custom Rebirth Lock-to-Player tether and
    // vanilla EntityDrone access/security locking. Never alias these states.
    public bool DroneLockedToPlayer;
    public bool DroneAccessLocked;
    public bool DroneQuiet;
    public bool DroneLightAttached;
    public bool DroneLightOn;
    public bool DroneHealAttached;
    public bool DroneHealingAllies;
    public int DroneQuality;
    public RebirthCompanionBehaviorMode BehaviorMode;
    public bool AttackStopped;
    public bool IsDog;
    public bool DogIsTamedWild;
    public string DogSpeciesCategory = string.Empty;
    public int DogAnimalCapacityCost;
    public string DogBreed = string.Empty;
    public int DogLevel;
    public int DogKills;
    public float DogTraining;
    public float DogBond;
    public int DogLearnedCommandCount;
    public bool DogLegacyCommandGrandfathered;
    public bool DogKnowsOwnerPositionStay;
    public bool DogKnowsGuardArea;
    public bool DogKnowsHunting;
    public RebirthDogLifecycleKind DogLifecycle;
    public bool IsBoundUndead;
    public string UndeadTier = string.Empty;
    public int UndeadCapacityCost;
    public float UndeadConditioning;
    public float UndeadTraining;
    public float UndeadBindingStability;
    public string UndeadLifecycle = string.Empty;
    public bool UndeadSummoned;
}

public sealed class RebirthCompanionCommandEntry
{
    public RebirthCompanionCommand Command;
    public string Text = string.Empty;
    public string Icon = string.Empty;
    public string IconAtlas = "UIAtlas";
    public bool Enabled;
}

public sealed class RebirthCompanionInventoryEntry
{
    public string RowKey = string.Empty;
    public int SlotIndex = -1;
    public int ItemType;
    public string ItemKey = string.Empty;
    public int Count;
    public bool Locked;
    public bool CanUse;
}

public sealed class RebirthCompanionSnapshot
{
    // Local transport failure only; not part of the server snapshot wire format.
    public bool RequestFailed;
    public string SelectedId = string.Empty;
    public string Feedback = string.Empty;
    public readonly List<RebirthCompanionListEntry> Companions = new List<RebirthCompanionListEntry>();
    public readonly List<RebirthCompanionCommandEntry> Commands = new List<RebirthCompanionCommandEntry>();
    public readonly List<RebirthCompanionInventoryEntry> Inventory = new List<RebirthCompanionInventoryEntry>();
}

public static class RebirthCompanionUiService
{
    public const string Group = "rebirthCompanions";

    public static void Open(XUi xui)
    {
        Open(xui, string.Empty, "Overview");
    }

    public static void Open(XUi xui, string selectedTargetId, string tabName)
    {
        XUiController group = xui?.FindWindowGroupByName(Group);
        XUiC_RebirthCompanions controller = group?.GetChildByType<XUiC_RebirthCompanions>();
        if (controller == null) return;
        controller.Prepare(selectedTargetId, tabName);
        xui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, true, true);
    }
}

public static class RebirthCompanionBehaviorService
{
    private sealed class State
    {
        public RebirthCompanionBehaviorMode Mode = RebirthCompanionBehaviorMode.FullControl;
        public bool StopAttacking;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcStableId, State> NpcStates =
        new Dictionary<RebirthNpcStableId, State>();

    private static State GetOrCreate(RebirthNpcStableId id)
    {
        lock (Sync)
        {
            State value;
            if (!NpcStates.TryGetValue(id, out value))
            {
                value = new State();
                NpcStates[id] = value;
            }
            return value;
        }
    }

    public static RebirthCompanionBehaviorMode GetMode(RebirthNpcStableId id)
    {
        if (id.IsEmpty) return RebirthCompanionBehaviorMode.FullControl;
        lock (Sync)
        {
            State value;
            return NpcStates.TryGetValue(id, out value)
                ? value.Mode
                : RebirthCompanionBehaviorMode.FullControl;
        }
    }

    public static bool IsAttackStopped(RebirthNpcStableId id)
    {
        if (id.IsEmpty) return false;
        lock (Sync)
        {
            State value;
            return NpcStates.TryGetValue(id, out value) && value.StopAttacking;
        }
    }

    public static void SetMode(RebirthNpcStableId id, RebirthCompanionBehaviorMode mode)
    {
        if (id.IsEmpty) return;
        lock (Sync) GetOrCreate(id).Mode = mode;
        RebirthNpcTargetingService.Invalidate(id, default(RebirthNpcStableId), "companion-mode-change");
    }

    public static void SetAttackStopped(RebirthNpcStableId id, bool stopped)
    {
        if (id.IsEmpty) return;
        lock (Sync) GetOrCreate(id).StopAttacking = stopped;
        if (stopped)
            RebirthNpcTargetingService.Invalidate(id, default(RebirthNpcStableId), "companion-stop");
    }

    public static float AdjustTargetScore(RebirthNpcStableId id, float score, RebirthNpcTargetCandidate candidate)
    {
        if (id.IsEmpty || candidate == null) return score;
        if (IsAttackStopped(id)) return float.MinValue;
        if (GetMode(id) == RebirthCompanionBehaviorMode.Hunting)
        {
            // 2.6 exposed Hunting as a distinct aggressive control mode. In the
            // 3.1 targeting model it deliberately promotes general hostile targets,
            // while Full Control keeps the normal owner-protection weighting.
            score += 250f;
            if (!candidate.ThreatensOwner) score += 125f;
        }
        return score;
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) NpcStates.Clear();
    }
}

public static class RebirthCompanionInventoryLockService
{
    private static readonly object Sync = new object();
    private static readonly HashSet<string> NpcLocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static string Key(RebirthNpcStableId id, string itemKey)
    {
        return id.ToString() + "|" + (itemKey ?? string.Empty).Trim();
    }

    public static bool IsNpcItemLocked(RebirthNpcStableId id, string itemKey)
    {
        if (id.IsEmpty || string.IsNullOrWhiteSpace(itemKey)) return false;
        lock (Sync) return NpcLocks.Contains(Key(id, itemKey));
    }

    public static bool ToggleNpcItemLock(RebirthNpcStableId id, string itemKey)
    {
        if (id.IsEmpty || string.IsNullOrWhiteSpace(itemKey)) return false;
        string key = Key(id, itemKey);
        lock (Sync)
        {
            if (NpcLocks.Remove(key)) return false;
            NpcLocks.Add(key);
            return true;
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) NpcLocks.Clear();
    }
}

public static class RebirthCompanionSnapshotService
{
    private const float RequestTimeoutSeconds = 5f;
    private const int MaximumPendingRequests = 4;
    private sealed class PendingRequest
    {
        internal Action<RebirthCompanionSnapshot> Callback;
        internal World World;
        internal float Deadline;
    }
    private static readonly Dictionary<ulong, PendingRequest> Pending = new Dictionary<ulong, PendingRequest>();
    private static ulong requestEpoch = 1;
    private static ulong nextRequestId;

    public static void Request(string selectedId, Action<RebirthCompanionSnapshot> cb)
    {
        if (cb == null) return;
        Update();
        if (Pending.Count >= MaximumPendingRequests)
        {
            cb(new RebirthCompanionSnapshot { RequestFailed = true, Feedback = Localization.Get("xuiRebirthCompanionRequestBusy") });
            return;
        }
        World world = GameManager.Instance?.World;
        ulong id;
        unchecked { id = ++nextRequestId; }
        if (id == 0) id = ++nextRequestId;
        Pending.Add(id, new PendingRequest { Callback = cb, World = world,
            Deadline = Time.unscaledTime + RequestTimeoutSeconds });
        EntityPlayerLocal player = world?.GetPrimaryPlayer();
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (world == null || player == null || persistent?.PrimaryId == null || connection == null)
        {
            Pending.Remove(id);
            cb(new RebirthCompanionSnapshot { RequestFailed = true, Feedback = Localization.Get("xuiRebirthCompanionRequestUnavailable") });
            return;
        }
        if (connection.IsServer)
            Receive(requestEpoch, id, Build(world, player, persistent.PrimaryId, selectedId ?? string.Empty));
        else
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCompanionSnapshotRequest>()
                .Setup(player.entityId, persistent, requestEpoch, id, selectedId ?? string.Empty));
    }

    internal static void Cancel(Action<RebirthCompanionSnapshot> owner)
    {
        if (owner == null) return;
        // Each surface owns its callback; closing it must preserve other in-flight requests.
        var ids = new List<ulong>();
        foreach (var pair in Pending)
            if (pair.Value.Callback == owner) ids.Add(pair.Key);
        foreach (ulong id in ids) Pending.Remove(id);
    }

    public static void Clear()
    {
        Pending.Clear();
        unchecked { requestEpoch++; }
        if (requestEpoch == 0) requestEpoch = 1;
    }

    public static void Update()
    {
        List<ulong> expired = null;
        foreach (var pair in Pending)
            if (!object.ReferenceEquals(pair.Value.World, GameManager.Instance?.World) ||
                Time.unscaledTime >= pair.Value.Deadline)
            {
                if (expired == null) expired = new List<ulong>();
                expired.Add(pair.Key);
            }
        if (expired == null) return;
        foreach (ulong id in expired)
        {
            PendingRequest request;
            if (!Pending.TryGetValue(id, out request)) continue;
            Pending.Remove(id); // Remove before invoking callbacks that may request again.
            // A world change retires the old surface; never deliver its timeout into a new session.
            if (request.World == null || !object.ReferenceEquals(request.World, GameManager.Instance?.World)) continue;
            request.Callback(new RebirthCompanionSnapshot { RequestFailed = true, Feedback = Localization.Get("xuiRebirthCompanionRequestTimeout") });
        }
    }

    public static void Receive(ulong epoch, ulong requestId, RebirthCompanionSnapshot snapshot)
    {
        PendingRequest request;
        if (epoch != requestEpoch || requestId == 0 || !Pending.TryGetValue(requestId, out request)) return;
        Pending.Remove(requestId);
        if (request.World == null || !object.ReferenceEquals(request.World, GameManager.Instance?.World)) return;
        RebirthCompanionSnapshot value = snapshot ?? new RebirthCompanionSnapshot();
        RebirthCompanionService.AugmentLocalSnapshot(value);
        request.Callback(value);
    }

    public static RebirthCompanionSnapshot Build(
        World world,
        EntityPlayer player,
        PlatformUserIdentifierAbs userId,
        string requestedSelection)
    {
        RebirthCompanionSnapshot result = new RebirthCompanionSnapshot();
        if (world == null || player == null || userId == null) return result;

        // The management window is observation only. It must never instantiate a replacement
        // runtime body merely because an Active durable StableId is temporarily unloaded.
        // Missing dogs remain visible through the durable row below; explicit Recall uses the
        // physical-chunk recovery path first, which preserves the original serialized body.
        var dogInventories = new Dictionary<RebirthNpcStableId, RebirthDogInventorySnapshot>();
        List<RebirthCompanionListEntry> companions = RebirthCompanionService.GetNearbyOwned(world, player, userId, RebirthCompanionService.ManagementRadius, dogInventories);
        if (companions.Count > RebirthNpcNetworkFraming.MaxCompanions)
        {
            RebirthCompanionListEntry requested = null;
            for (int i = 0; i < companions.Count; i++)
                if (string.Equals(companions[i].Id, requestedSelection, StringComparison.Ordinal)) { requested = companions[i]; break; }
            List<RebirthCompanionListEntry> bounded = companions.GetRange(0, RebirthNpcNetworkFraming.MaxCompanions);
            if (requested != null && !bounded.Contains(requested)) bounded[bounded.Count - 1] = requested;
            companions = bounded;
        }
        result.Companions.AddRange(companions);
        if (companions.Count == 0)
        {
            result.Feedback = Localization.Get("xuiRebirthNoCompanions");
            return result;
        }

        RebirthCompanionListEntry selected = null;
        for (int i = 0; i < companions.Count; i++)
        {
            if (string.Equals(companions[i].Id, requestedSelection, StringComparison.Ordinal))
            {
                selected = companions[i];
                break;
            }
        }
        if (selected == null) selected = companions[0];
        result.SelectedId = selected.Id;
        RebirthCompanionService.BuildCommands(world, player, selected, result.Commands);
        RebirthCompanionService.BuildInventory(world, player, selected, result.Inventory, dogInventories);
        return result;
    }
}


public static class RebirthCompanionService
{
    // Management visibility extends far enough to support the furthest manual command.
    // Individual actions are gated by the narrower authoritative distances below.
    public const float ManagementRadius = 150f;
    public const float StorageDistance = 20f;
    public const float PositionalCommandDistance = 80f;
    public const float BehaviorCommandDistance = 150f;
    public const float RecallDistance = 150f;
    public const float AutomaticFollowRecallDistance = 35f;

    private static readonly string[] DroneVoiceAndIdleSounds =
    {
        "drone_heal",
        "drone_wakeup",
        "drone_greeting",
        "drone_takefail",
        "drone_command",
        "drone_shutdown",
        "drone_enemy_sense",
        "drone_idle_hover"
    };

    internal static void StopKnownDroneAudio(EntityDrone drone)
    {
        if (drone == null) return;
        for (int i = 0; i < DroneVoiceAndIdleSounds.Length; i++)
            drone.StopOneShot(DroneVoiceAndIdleSounds[i]);
    }

    private static bool TryGetLocalDroneCommands(EntityDrone drone, EntityPlayerLocal player, out EntityActivationCommand[] commands)
    {
        commands = null;
        if (drone == null || player == null || drone.IsDead() || drone.belongsPlayerId != player.entityId) return false;
        drone.UpdateActivationCommands(player);
        commands = drone.GetActivationCommands();
        return commands != null;
    }

    internal static void AugmentLocalSnapshot(RebirthCompanionSnapshot snapshot)
    {
        if (snapshot == null || GameManager.Instance?.World == null) return;
        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
        if (player == null) return;

        // The server snapshot is the durable ownership/presence authority. Project dog
        // markers from it so an Active dog remains locatable even when its entity chunk is
        // not currently loaded on this client.
        RebirthDogNavigationMarkerService.SynchronizeSnapshot(snapshot.Companions);

        RebirthCompanionListEntry selected = null;
        for (int i = 0; i < snapshot.Companions.Count; i++)
        {
            RebirthCompanionListEntry entry = snapshot.Companions[i];
            if (entry == null) continue;
            if (entry.Kind == RebirthCompanionTargetKind.Drone)
            {
                EntityDrone drone = GameManager.Instance.World.GetEntity(entry.EntityId) as EntityDrone;
                if (drone != null && drone.belongsPlayerId == player.entityId)
                {
                    entry.DroneLockedToPlayer = RebirthDroneLockToPlayerService.IsLockedToPlayer(drone.entityId);
                    entry.DroneAccessLocked = drone.IsLocked();
                    entry.DroneLightAttached = drone.IsFlashlightAttached;
                    entry.DroneLightOn = drone.IsFlashlightOn;
                    entry.DroneHealAttached = drone.IsHealModAttached;
                    entry.DroneHealingAllies = drone.IsHealingAllies;

                    EntityActivationCommand[] commands;
                    if (TryGetLocalDroneCommands(drone, player, out commands))
                    {
                        for (int c = 0; c < commands.Length; c++)
                        {
                            EntityActivationCommand command = commands[c];
                            if (string.Equals(command.commandId, "drone_silent_off", StringComparison.Ordinal) && command.enabled)
                            {
                                entry.DroneQuiet = true;
                                break;
                            }
                            if (string.Equals(command.commandId, "drone_silent_on", StringComparison.Ordinal) && command.enabled)
                                entry.DroneQuiet = false;
                        }
                    }
                }
            }
            if (string.Equals(entry.Id, snapshot.SelectedId, StringComparison.Ordinal)) selected = entry;
        }

        // NPC commands carry authoritative lifecycle/training and entity-presence gates.
        // Rebuilding them on a remote client can replace those gates with absent local
        // persistence or an entity that has not entered the client observer range yet.
        // Only drones need augmentation from their native client activation state.
        if (selected != null && selected.Kind == RebirthCompanionTargetKind.Drone)
        {
            snapshot.Commands.Clear();
            BuildCommands(GameManager.Instance.World, player, selected, snapshot.Commands);
        }
    }

    public static List<RebirthCompanionListEntry> GetNearbyOwned(
        World world,
        EntityPlayer player,
        PlatformUserIdentifierAbs userId,
        float radius,
        Dictionary<RebirthNpcStableId, RebirthDogInventorySnapshot> dogInventories = null)
    {
        List<RebirthCompanionListEntry> result = new List<RebirthCompanionListEntry>();
        if (world == null || player == null || userId == null) return result;

        // Companion enumeration is world-scoped. Force the same save-boundary guard used by
        // dog capacity before consulting any static registry or durable companion record.
        RebirthNpcLifecycle.EnsureCurrentWorldScope();

        List<IRemoteResourceSource> sources = RemoteResourceRegistry.ResolveNearby(world, player, radius, false, false, true);
        for (int i = 0; i < sources.Count; i++)
        {
            IRemoteResourceSource source = sources[i];
            string reason;
            if (source == null || source.Kind != RemoteResourceSourceKind.DroneStorage || !source.IsAuthorized(player, true, out reason)) continue;
            string[] bits = source.StableId.Split(':'); int id;
            if (bits.Length != 2 || !int.TryParse(bits[1], out id)) continue;
            EntityDrone drone = world.GetEntity(id) as EntityDrone;
            if (drone == null || drone.IsDead()) continue;
            result.Add(DescribeDrone(player, drone));
        }

        string owner = userId.ToString();
        HashSet<RebirthNpcStableId> seen = new HashSet<RebirthNpcStableId>();

        // A loaded world entity is the strongest source of truth for whether a dog is
        // actually present. The runtime StableId->entity projection can legitimately lag
        // after load/recovery; trusting it first made a live dog appear as
        // "Missing - Last Known Position" and also stripped its live command set.
        List<Entity> loadedEntities = world.Entities != null ? world.Entities.list : null;
        if (loadedEntities != null)
        {
            for (int i = 0; i < loadedEntities.Count; i++)
            {
                EntityRebirthDogCompanion liveDog = loadedEntities[i] as EntityRebirthDogCompanion;
                RebirthNpcRuntimeState liveState = liveDog != null ? liveDog.RebirthRuntimeState : null;
                if (liveDog == null || liveDog.IsDead() || liveState == null || liveState.StableId.IsEmpty ||
                    !RebirthDogLifecycleService.IsOwnedBy(liveDog, player))
                    continue;

                RebirthNpcPersistentRecordView liveRecord;
                RebirthDogPersistentRecordView liveDogRecord;
                if (RebirthDogStateService.TryGetView(liveState.StableId, out liveRecord, out liveDogRecord) &&
                    liveDogRecord != null && liveDogRecord.Lifecycle != RebirthDogLifecycleKind.Active)
                    continue;

                RebirthNpcProfile liveProfile;
                if (!RebirthNpcProfileRegistry.TryResolve(RebirthDogDefinitions.ProfileId, out liveProfile))
                    continue;

                result.Add(DescribeNpc(player, liveDog, liveState, liveProfile, dogInventories));
                seen.Add(liveState.StableId);
            }
        }

        RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
        for (int i = 0; i < states.Length; i++)
        {
            RebirthNpcRuntimeState state = states[i];
            if (state == null || seen.Contains(state.StableId) ||
                state.Presence != RebirthNpcPresenceState.Active || state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                !string.Equals(state.OwnerId ?? string.Empty, owner, StringComparison.OrdinalIgnoreCase)) continue;
            RebirthNpcProfile profile;
            if (!RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) || !profile.Has(RebirthNpcCapabilities.Ownership)) continue;
            int entityId; if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId)) continue;
            EntityRebirthNPC npc = world.GetEntity(entityId) as EntityRebirthNPC;
            if (npc == null || npc.IsDead()) continue;
            float distance = Vector3.Distance(player.position, npc.position);
            // Dogs are durable owned companions and must remain visible in the management
            // roster regardless of distance. The radius remains appropriate for generic NPC
            // companions; applying it to dogs would make a healthy far-away dog look missing.
            if (distance > radius && profile.Category != RebirthNpcCategory.DogCompanion) continue;
            result.Add(DescribeNpc(player, npc, state, profile, dogInventories)); seen.Add(state.StableId);
        }

        // Durable dog records remain manageable while awaiting respawn or while their
        // live projection is unloaded. PickedUp is intentionally excluded: once embodied as
        // an inventory item there is no companion entity to interact with in this window.
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (!RebirthDogStateService.IsDog(record) || record.Identity == null || record.Dog == null || record.Ownership == null ||
                record.Lifecycle?.TombstoneState == true || record.Dog.Lifecycle == RebirthDogLifecycleKind.Removed ||
                string.Equals(record.Presence?.PresenceState, RebirthNpcPresenceState.Removed.ToString(), StringComparison.OrdinalIgnoreCase) ||
                record.Dog.Lifecycle == RebirthDogLifecycleKind.PickedUp ||
                !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, owner, StringComparison.OrdinalIgnoreCase) ||
                seen.Contains(record.Identity.StableNpcId)) continue;
            // A durable Active record with no live projection is still an owned dog. Keep it
            // visible in the Companions roster at its last persisted position instead of making
            // it disappear from management and tempting the player to deploy a replacement.
            result.Add(DescribePersistentDog(player, record, dogInventories));
        }

        // PC009 bound undead are durable aggregate companions even though their runtime body
        // remains an EntityZombie-family class. Keep them visible while unloaded or AwaitingReturn.
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (record == null || record.Identity == null || record.BoundUndead == null || !record.BoundUndead.IsBound ||
                record.Ownership == null || (record.Lifecycle != null && record.Lifecycle.TombstoneState) ||
                !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, owner, StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(DescribeBoundUndead(player, world, record));
        }

        result.Sort(delegate(RebirthCompanionListEntry a, RebirthCompanionListEntry b)
        {
            bool ai = (a.IsDog && (a.DogLifecycle != RebirthDogLifecycleKind.Active || a.EntityId < 0)) || (a.IsBoundUndead && (!string.Equals(a.UndeadLifecycle,"Active",StringComparison.OrdinalIgnoreCase) || a.EntityId < 0));
            bool bi = (b.IsDog && (b.DogLifecycle != RebirthDogLifecycleKind.Active || b.EntityId < 0)) || (b.IsBoundUndead && (!string.Equals(b.UndeadLifecycle,"Active",StringComparison.OrdinalIgnoreCase) || b.EntityId < 0));
            if (ai != bi) return ai ? 1 : -1;
            int byDistance = a.Distance.CompareTo(b.Distance);
            if (byDistance != 0) return byDistance;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    public static string GetDogStatus(RebirthNpcOrderState order, bool guardIsStationaryStay, bool attackStopped)
    {
        if (attackStopped) return Localization.Get("xuiRebirthStatusStopped");
        switch (order)
        {
            case RebirthNpcOrderState.Follow:
                return Localization.Get("xuiRebirthStatusFollowing");
            case RebirthNpcOrderState.Stay:
                return Localization.Get("xuiRebirthDogStatusStayingHere");
            case RebirthNpcOrderState.Guard:
                return guardIsStationaryStay
                    ? Localization.Get("xuiRebirthDogStatusStayingWithOwnerPosition")
                    : Localization.Get("xuiRebirthStatusGuarding");
            default:
                return Localization.Get("xuiRebirthActive");
        }
    }

    private static bool TryParseOrder(string value, out RebirthNpcOrderState order)
    {
        order = RebirthNpcOrderState.Follow;
        return !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out order);
    }

    private static bool IsFollowingOrder(string order)
    {
        return string.Equals(order ?? string.Empty, "Follow", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWithinStorageDistance(float distance)
    {
        return distance <= StorageDistance;
    }

    public static bool IsWithinPositionalCommandDistance(float distance)
    {
        return distance <= PositionalCommandDistance;
    }

    public static bool IsWithinBehaviorCommandDistance(float distance)
    {
        return distance <= BehaviorCommandDistance;
    }

    public static bool CanRecall(RebirthCompanionListEntry target)
    {
        return target != null && target.Distance <= RecallDistance && IsFollowingOrder(target.Order);
    }

    private static bool IsCommandAllowedForTarget(RebirthCompanionListEntry target, RebirthCompanionCommand command)
    {
        if (target == null) return false;
        switch (command)
        {
            case RebirthCompanionCommand.Storage:
                return target.Distance <= StorageDistance;
            case RebirthCompanionCommand.Follow:
            case RebirthCompanionCommand.Stay:
            case RebirthCompanionCommand.Guard:
            case RebirthCompanionCommand.GuardArea:
                return target.Distance <= PositionalCommandDistance;
            case RebirthCompanionCommand.Recall:
                return CanRecall(target);
            case RebirthCompanionCommand.Hunting:
            case RebirthCompanionCommand.FullControl:
            case RebirthCompanionCommand.Stop:
            case RebirthCompanionCommand.Resume:
                return target.Distance <= BehaviorCommandDistance;
            default:
                return true;
        }
    }

    private static bool TryGetLocalOwnedEntry(string targetId, out RebirthCompanionListEntry target)
    {
        target = null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (world == null || player == null || persistent == null || persistent.PrimaryId == null || string.IsNullOrEmpty(targetId))
            return false;

        List<RebirthCompanionListEntry> owned = GetNearbyOwned(world, player, persistent.PrimaryId, ManagementRadius);
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != null && string.Equals(owned[i].Id, targetId, StringComparison.Ordinal))
            {
                target = owned[i];
                return true;
            }
        }
        return false;
    }

    private static string GetTamedWildSpeciesDisplay(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return Localization.Get("xuiRebirthTamedWildType");
        string text = category.Replace('_', ' ').Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
    }

    private static RebirthCompanionListEntry DescribePersistentDog(EntityPlayer player, RebirthNpcPersistentRecordView record,
        Dictionary<RebirthNpcStableId, RebirthDogInventorySnapshot> dogInventories)
    {
        bool isTamedWild = record.Dog != null && record.Dog.IsTamedWild;
        RebirthDogBreedDefinition breed = null;
        if (!isTamedWild) RebirthDogDefinitions.TryGetByBreedId(record.Dog.BreedId, out breed);
        RebirthDogInventorySnapshot inventory = isTamedWild ? null : GetDogInventoryForRefresh(record.Identity.StableNpcId, dogInventories);
        int used = 0;
        if (inventory != null && inventory.Slots != null)
            for (int i = 0; i < inventory.Slots.Length; i++) if (inventory.Slots[i] != null && !inventory.Slots[i].IsEmpty()) used++;
        string wildSpecies = isTamedWild ? GetTamedWildSpeciesDisplay(record.Dog.SpeciesCategory) : string.Empty;
        string name = record.Identity != null ? record.Identity.GeneratedOrAssignedDisplayName : string.Empty;
        if (string.IsNullOrWhiteSpace(name)) name = isTamedWild ? wildSpecies : (breed != null ? Localization.Get(breed.LocalizationKey) : Localization.Get("xuiRebirthAttackDogType"));
        RebirthNpcOrderState persistedOrder;
        if (!TryParseOrder(record.Order != null ? record.Order.OrderState : string.Empty, out persistedOrder))
            persistedOrder = RebirthNpcOrderState.Follow;
        string status = record.Dog.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn
            ? Localization.Get("xuiRebirthStatusRespawned")
            : record.Dog.Lifecycle == RebirthDogLifecycleKind.PickedUp
                ? Localization.Get("xuiRebirthStatusPickedUp")
                : "Not loaded - last known state";
        Vector3 pos = record.Transform != null ? record.Transform.WorldPosition : player.position;
        return new RebirthCompanionListEntry
        {
            Id = "N:" + record.Identity.StableNpcId, Kind = RebirthCompanionTargetKind.Npc, EntityId = -1, StableId = record.Identity.StableNpcId.ToString(),
            Name = name, Type = isTamedWild ? Localization.Get("xuiRebirthTamedWildType") + " - " + wildSpecies : Localization.Get("xuiRebirthAttackDogType") + (breed != null ? " - " + Localization.Get(breed.LocalizationKey) : string.Empty),
            Status = status, Icon = breed != null ? breed.IconName : "ui_game_symbol_tracking_wolf", Distance = Vector3.Distance(player.position, pos),
            DirectionAngle = DirectionAngle(player, pos), WorldPosition = pos,
            Health = record.Vitals != null ? record.Vitals.CurrentHealth : 0, MaxHealth = record.Vitals != null ? record.Vitals.MaximumHealthAtSave : 500,
            StorageUsed = isTamedWild ? 0 : used, StorageTotal = isTamedWild ? 0 : RebirthDogInventoryService.Capacity, Profile = RebirthDogDefinitions.ProfileId,
            Order = record.Order != null ? record.Order.OrderState : RebirthNpcOrderState.Follow.ToString(), BehaviorMode = record.Dog.CombatMode,
            AttackStopped = record.Dog.AttackStopped, IsDog = true, DogIsTamedWild = isTamedWild, DogSpeciesCategory = record.Dog.SpeciesCategory ?? string.Empty, DogAnimalCapacityCost = Math.Max(0, record.Dog.AnimalCapacityCost), DogBreed = isTamedWild ? wildSpecies : (breed != null ? Localization.Get(breed.LocalizationKey) : record.Dog.BreedId),
            DogLevel = record.Dog.Level, DogKills = record.Dog.KillCount, DogTraining = Mathf.Clamp(record.Dog.Training, 0f, 100f), DogBond = Mathf.Clamp(record.Dog.Bond, 0f, 100f),
            DogLearnedCommandCount = record.Dog.LearnedCommands != null ? record.Dog.LearnedCommands.Count : 0,
            DogLegacyCommandGrandfathered = record.Dog.LegacyCommandGrandfathered,
            DogKnowsOwnerPositionStay = record.Dog.LearnedCommands != null && record.Dog.LearnedCommands.Contains(RebirthDogTrainingCommandIds.OwnerPositionStay),
            DogKnowsGuardArea = record.Dog.LearnedCommands != null && record.Dog.LearnedCommands.Contains(RebirthDogTrainingCommandIds.GuardArea),
            DogKnowsHunting = record.Dog.LearnedCommands != null && record.Dog.LearnedCommands.Contains(RebirthDogTrainingCommandIds.Hunting),
            DogLifecycle = record.Dog.Lifecycle
        };
    }

    private static RebirthCompanionListEntry DescribeBoundUndead(EntityPlayer player, World world, RebirthNpcPersistentRecordView record)
    {
        EntityAlive live; bool hasLive = RebirthBoundUndeadService.TryGetLiveEntity(world, record.Identity.StableNpcId, out live);
        Vector3 pos = hasLive ? live.position : (record.Transform != null ? record.Transform.WorldPosition : player.position);
        string lifecycle = record.BoundUndead.Lifecycle ?? string.Empty;
        string tier = string.IsNullOrEmpty(record.BoundUndead.TierId) ? "normal" : record.BoundUndead.TierId;
        string name = record.Identity.GeneratedOrAssignedDisplayName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)) name = Localization.Get("xuiRebirthBoundUndead");
        string status = string.Equals(lifecycle,"AwaitingReturn",StringComparison.OrdinalIgnoreCase)
            ? Localization.Get("xuiRebirthBoundUndeadAwaitingReturn")
            : (record.BoundUndead.AttackStopped ? Localization.Get("xuiRebirthStatusStopped") : (record.Order != null && string.Equals(record.Order.OrderState,"Stay",StringComparison.OrdinalIgnoreCase) ? Localization.Get("xuiRebirthStatusWaiting") : Localization.Get("xuiRebirthStatusFollowing")));
        return new RebirthCompanionListEntry
        {
            Id="N:"+record.Identity.StableNpcId, Kind=RebirthCompanionTargetKind.Npc, EntityId=hasLive?live.entityId:-1, StableId=record.Identity.StableNpcId.ToString(),
            Name=name, Type=Localization.Get("xuiRebirthBoundUndead")+" - "+CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier.ToLowerInvariant()), Status=status, Icon="ui_game_symbol_skull",
            Distance=Vector3.Distance(player.position,pos), DirectionAngle=DirectionAngle(player,pos), WorldPosition=pos,
            Health=hasLive?Math.Max(0,live.Health):(record.Vitals!=null?record.Vitals.CurrentHealth:0), MaxHealth=hasLive?live.GetMaxHealth():(record.Vitals!=null?record.Vitals.MaximumHealthAtSave:0),
            StorageUsed=0, StorageTotal=0, Profile="companion.bound_undead", Order=record.Order!=null?record.Order.OrderState:RebirthNpcOrderState.Follow.ToString(),
            AttackStopped=record.BoundUndead.AttackStopped, IsBoundUndead=true, UndeadTier=tier, UndeadCapacityCost=Math.Max(1,record.BoundUndead.CapacityCost),
            UndeadConditioning=Mathf.Clamp(record.BoundUndead.ConditioningProgress,0f,100f), UndeadTraining=Mathf.Clamp(record.BoundUndead.Training,0f,100f),
            UndeadBindingStability=Mathf.Clamp(record.BoundUndead.BindingStability,0f,100f), UndeadLifecycle=lifecycle, UndeadSummoned=record.BoundUndead.IsSummoned
        };
    }

    private static RebirthCompanionListEntry DescribeDrone(EntityPlayer player, EntityDrone drone)
    {
        ItemStack[] slots = drone.bag?.ItemGrid.items;
        int used = 0;
        if (slots != null)
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && !slots[i].IsEmpty()) used++;

        string status;
        if (drone.GetState() == EntityDrone.State.Shutdown)
            status = Localization.Get("xuiRebirthStatusStopped");
        else if (drone.OrderState == EntityDrone.Orders.Stay)
            // A vanilla drone's Stay/SentryMode is a stationary waiting order, not
            // the Rebirth NPC/dog Guard order. Keep the UI semantics distinct.
            status = Localization.Get("xuiRebirthStatusWaiting");
        else
            status = Localization.Get("xuiRebirthStatusFollowing");

        return new RebirthCompanionListEntry
        {
            Id = "D:" + drone.entityId,
            Kind = RebirthCompanionTargetKind.Drone,
            EntityId = drone.entityId,
            Name = RebirthDroneRenameService.GetDisplayName(drone),
            Type = Localization.Get("xuiRebirthRoboticDroneType"),
            Status = status,
            Icon = "gunBotT3JunkDrone",
            Distance = Vector3.Distance(player.position, drone.position),
            DirectionAngle = DirectionAngle(player, drone.position),
            WorldPosition = drone.position,
            Health = Mathf.RoundToInt(drone.Health),
            MaxHealth = Mathf.RoundToInt(drone.Stats.Health.Max),
            StorageUsed = used,
            StorageTotal = slots != null ? slots.Length : 0,
            Profile = "vanilla.drone",
            Order = drone.OrderState.ToString(),
            DroneLockedToPlayer = RebirthDroneLockToPlayerService.IsLockedToPlayer(drone.entityId),
            DroneAccessLocked = drone.IsLocked(),
            DroneQuiet = false,
            DroneLightAttached = drone.IsFlashlightAttached,
            DroneLightOn = drone.IsFlashlightOn,
            DroneHealAttached = drone.IsHealModAttached,
            DroneHealingAllies = drone.IsHealingAllies,
            DroneQuality = drone.OriginalItemValue != null ? Mathf.Clamp((int)drone.OriginalItemValue.Quality, 1, 6) : 1
        };
    }

    private static RebirthCompanionListEntry DescribeNpc(
        EntityPlayer player, EntityRebirthNPC npc, RebirthNpcRuntimeState state, RebirthNpcProfile profile,
        Dictionary<RebirthNpcStableId, RebirthDogInventorySnapshot> dogInventories)
    {
        bool isDog = profile.Category == RebirthNpcCategory.DogCompanion;
        // Reuse one immutable dog view for row statistics rather than re-reading it per field.
        RebirthNpcPersistentRecordView persistentDogRecord = null;
        RebirthDogPersistentRecordView dogRecord = null;
        if (isDog) RebirthDogStateService.TryGetView(state.StableId, out persistentDogRecord, out dogRecord);
        bool isTamedWild = dogRecord != null && dogRecord.IsTamedWild;
        int used = 0, total = 0;
        RebirthDogInventorySnapshot dogInventory = null;
        if (isDog && !isTamedWild)
        {
            dogInventory = GetDogInventoryForRefresh(state.StableId, dogInventories);
            total = RebirthDogInventoryService.Capacity;
            for (int i = 0; i < dogInventory.Slots.Length; i++) if (dogInventory.Slots[i] != null && !dogInventory.Slots[i].IsEmpty()) used++;
        }
        else
        {
            RebirthNpcInventorySnapshot inventory = profile.Has(RebirthNpcCapabilities.Inventory) ? RebirthNpcInventoryTransactionService.GetSnapshot(state.StableId) : null;
            used = inventory?.Quantities?.Count ?? 0;
            if (inventory?.Quantities != null) foreach (KeyValuePair<string, int> pair in inventory.Quantities) total += Math.Max(0, pair.Value);
        }
        string name = RebirthNpcWorldIntegrationService.GetDisplayName(state.StableId);
        if (string.IsNullOrWhiteSpace(name)) name = profile.Category.ToString();
        string companionType = CompanionType(profile.Category);
        string companionIcon = profile.Category == RebirthNpcCategory.Survivor ? "ui_game_symbol_players" : "ui_game_symbol_allies";
        string breedName = string.Empty;
        string wildSpecies = isTamedWild ? GetTamedWildSpeciesDisplay(dogRecord.SpeciesCategory) : string.Empty;
        if (isDog)
        {
            RebirthDogBreedDefinition breed;
            if (!isTamedWild && dogRecord != null && RebirthDogDefinitions.TryGetByBreedId(dogRecord.BreedId, out breed))
            { companionIcon = breed.IconName; breedName = Localization.Get(breed.LocalizationKey); companionType = Localization.Get("xuiRebirthAttackDogType") + " - " + breedName; }
            else if (isTamedWild)
            { companionIcon = "ui_game_symbol_tracking_wolf"; breedName = wildSpecies; companionType = Localization.Get("xuiRebirthTamedWildType") + " - " + wildSpecies; }
        }
        bool attackStopped = isDog ? (dogRecord != null && dogRecord.AttackStopped) : RebirthCompanionBehaviorService.IsAttackStopped(state.StableId);
        bool stationaryDogGuard = isDog && state.Order == RebirthNpcOrderState.Guard && (dogRecord != null && dogRecord.GuardIsStationaryStay);
        RebirthDogLifecycleKind dogLifecycle = RebirthDogLifecycleKind.Active;
        if (isDog && dogRecord != null)
            dogLifecycle = dogRecord.Lifecycle;
        string status = isDog
            ? (dogLifecycle == RebirthDogLifecycleKind.AwaitingRespawn
                ? Localization.Get("xuiRebirthStatusRespawned")
                : GetDogStatus(state.Order, stationaryDogGuard, attackStopped))
            : attackStopped ? Localization.Get("xuiRebirthStatusStopped") :
              state.Order == RebirthNpcOrderState.Guard ? Localization.Get("xuiRebirthStatusGuarding") :
              state.Order == RebirthNpcOrderState.Stay ? Localization.Get("xuiRebirthStatusWaiting") :
              state.Order == RebirthNpcOrderState.Follow ? Localization.Get("xuiRebirthStatusFollowing") : Localization.Get("xuiRebirthActive");
        return new RebirthCompanionListEntry
        {
            Id = "N:" + state.StableId, Kind = RebirthCompanionTargetKind.Npc, EntityId = npc.entityId, StableId = state.StableId.ToString(),
            Name = name, Type = companionType, Status = status, Icon = companionIcon, Distance = Vector3.Distance(player.position, npc.position),
            DirectionAngle = DirectionAngle(player, npc.position), WorldPosition = npc.position,
            Health = npc.Health, MaxHealth = npc.GetMaxHealth(), StorageUsed = used, StorageTotal = total, Profile = profile.Id, Order = state.Order.ToString(),
            BehaviorMode = isDog ? (dogRecord != null ? dogRecord.CombatMode : RebirthCompanionBehaviorMode.FullControl) : RebirthCompanionBehaviorService.GetMode(state.StableId),
            AttackStopped = attackStopped, IsDog = isDog, DogIsTamedWild = isTamedWild, DogSpeciesCategory = isTamedWild ? (dogRecord.SpeciesCategory ?? string.Empty) : string.Empty, DogAnimalCapacityCost = isTamedWild ? Math.Max(0, dogRecord.AnimalCapacityCost) : 0, DogBreed = breedName, DogLevel = isDog ? (dogRecord != null ? Mathf.Clamp(dogRecord.Level, 1, 10) : 1) : 0,
            DogKills = isDog ? (dogRecord != null ? Math.Max(0, dogRecord.KillCount) : 0) : 0, DogTraining = isDog ? (dogRecord != null ? Mathf.Clamp(dogRecord.Training, 0f, 100f) : 0f) : 0f,
            DogBond = isDog ? (dogRecord != null ? Mathf.Clamp(dogRecord.Bond, 0f, 100f) : 0f) : 0f, DogLearnedCommandCount = isDog ? (dogRecord != null ? dogRecord.LearnedCommands.Count : 0) : 0,
            DogLegacyCommandGrandfathered = isDog && (dogRecord != null && dogRecord.LegacyCommandGrandfathered),
            DogKnowsOwnerPositionStay = isDog && (dogRecord != null && dogRecord.LearnedCommands.Contains(RebirthDogTrainingCommandIds.OwnerPositionStay)),
            DogKnowsGuardArea = isDog && (dogRecord != null && dogRecord.LearnedCommands.Contains(RebirthDogTrainingCommandIds.GuardArea)),
            DogKnowsHunting = isDog && (dogRecord != null && dogRecord.LearnedCommands.Contains(RebirthDogTrainingCommandIds.Hunting)), DogLifecycle = dogLifecycle
        };
    }

    public static float DirectionAngle(EntityPlayer player, Vector3 target)
    {
        if (player == null) return 0f;
        Vector2 delta = new Vector2(target.x - player.position.x, target.z - player.position.z);
        if (delta.sqrMagnitude <= 0.001f) return 0f;

        // The trader job-card arrow is authored pointing straight up. Convert the
        // target's world bearing into a signed turn from the player's CURRENT yaw:
        //   0 = straight ahead, -90 UI rotation = 90 degrees right,
        //   +90 = 90 degrees left, +/-180 = directly behind.
        float targetBearing = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
        float relativeTurn = Mathf.DeltaAngle(player.rotation.y, targetBearing);
        return -relativeTurn;
    }

    private static string CompanionType(RebirthNpcCategory category)
    {
        switch (category)
        {
            case RebirthNpcCategory.DogCompanion: return Localization.Get("xuiRebirthAttackDogType");
            case RebirthNpcCategory.PantherCompanion: return Localization.Get("xuiRebirthPantherType");
            case RebirthNpcCategory.Survivor: return Localization.Get("xuiRebirthSurvivorFollowerType");
            default: return category.ToString();
        }
    }

    public static void BuildCommands(
        World world,
        EntityPlayer player,
        RebirthCompanionListEntry selected,
        List<RebirthCompanionCommandEntry> output)
    {
        if (output == null || selected == null) return;
        output.Clear();
        if (selected.Kind == RebirthCompanionTargetKind.Drone)
        {
            EntityDrone drone = world?.GetEntity(selected.EntityId) as EntityDrone;
            if (drone == null) return;
            // Custom Rebirth Lock-to-Player and vanilla access/security Lock/Unlock are
            // independent mechanics. Keep both rows and never let one mutate the other.
            AddCommand(output,
                selected.DroneLockedToPlayer ? RebirthCompanionCommand.UnlockFromPlayer : RebirthCompanionCommand.LockToPlayer,
                selected.DroneLockedToPlayer ? "xuiRebirthUnlockDroneFromPlayer" : "xuiRebirthLockDroneToPlayer",
                "ui_game_symbol_dronelocktoplayer", true, "RebirthUiIcons");

            AddCommand(output,
                selected.DroneAccessLocked ? RebirthCompanionCommand.Unlock : RebirthCompanionCommand.LockAccess,
                selected.DroneAccessLocked ? "xuiRebirthUnlockDrone" : "xuiRebirthLockDrone",
                selected.DroneAccessLocked ? "ui_game_symbol_unlock" : "ui_game_symbol_lock", true);

            AddCommand(output, RebirthCompanionCommand.Rename, "xuiRebirthRename", "ui_game_symbol_pen", true);
            // Use the exact stock drone storage interaction/icon. This opens the native
            // BagStorage window and takes the same LockManager storage lock as the drone
            // dialog/activation path instead of switching to the management Inventory tab.
            AddCommand(output, RebirthCompanionCommand.Storage, "xuiStorage", "ui_game_symbol_loot_sack",
                drone.bag != null && selected.Distance <= StorageDistance);

            bool positionalInRange = selected.Distance <= PositionalCommandDistance;
            AddCommand(output, RebirthCompanionCommand.Follow, "xuiRebirthFollow", "ui_game_symbol_run",
                positionalInRange && !selected.DroneLockedToPlayer && drone.OrderState != EntityDrone.Orders.Follow);
            AddCommand(output, RebirthCompanionCommand.Stay, "xuiRebirthStay", "ui_game_symbol_run_and_gun",
                positionalInRange && !selected.DroneLockedToPlayer && drone.OrderState != EntityDrone.Orders.Stay);
            AddCommand(output, RebirthCompanionCommand.Recall, "xuiRebirthRecall", "ui_game_symbol_twitch_blur",
                selected.Distance <= RecallDistance && drone.OrderState == EntityDrone.Orders.Follow);
            AddCommand(output, RebirthCompanionCommand.QuietToggle,
                selected.DroneQuiet ? "xuiRebirthQuietOff" : "xuiRebirthQuietOn",
                selected.DroneQuiet ? "ui_game_symbol_sight" : "ui_game_symbol_stealth", true);
            if (selected.DroneLightAttached)
                AddCommand(output, RebirthCompanionCommand.LightToggle,
                    selected.DroneLightOn ? "xuiRebirthLightOff" : "xuiRebirthLightOn",
                    selected.DroneLightOn ? "ui_game_symbol_electric_switch" : "ui_game_symbol_lightbulb", true);
            if (selected.DroneHealAttached)
            {
                AddCommand(output, RebirthCompanionCommand.HealMe, "xuiRebirthHealMe", "ui_game_symbol_cardio", true);
                AddCommand(output, RebirthCompanionCommand.ToggleHealAllies,
                    selected.DroneHealingAllies ? "xuiRebirthHealOnlyMe" : "xuiRebirthHealAllies",
                    selected.DroneHealingAllies ? "ui_game_symbol_player" : "ui_game_symbol_allies", true);
            }
            return;
        }

        RebirthNpcStableId npcId;
        if (!RebirthNpcStableId.TryParse(selected.StableId, out npcId)) return;
        if (selected.IsBoundUndead)
        {
            if (string.Equals(selected.UndeadLifecycle,"AwaitingReturn",StringComparison.OrdinalIgnoreCase))
            {
                AddCommand(output,RebirthCompanionCommand.ReportForDuty,"xuiRebirthBoundUndeadReportForDuty","ui_game_symbol_check",true);
                return;
            }
            bool pos=selected.Distance<=PositionalCommandDistance, behavior=selected.Distance<=BehaviorCommandDistance;
            AddCommand(output,RebirthCompanionCommand.Follow,"xuiRebirthFollow","ui_game_symbol_run",pos&&!string.Equals(selected.Order,"Follow",StringComparison.OrdinalIgnoreCase));
            AddCommand(output,RebirthCompanionCommand.Stay,"xuiRebirthStay","ui_game_symbol_run_and_gun",pos&&!string.Equals(selected.Order,"Stay",StringComparison.OrdinalIgnoreCase));
            AddCommand(output,selected.AttackStopped?RebirthCompanionCommand.Resume:RebirthCompanionCommand.Stop,selected.AttackStopped?"xuiRebirthResume":"xuiRebirthStop",selected.AttackStopped?"ui_game_symbol_twitch_play":"ui_game_symbol_twitch_pause",behavior);
            AddCommand(output,RebirthCompanionCommand.Recall,"xuiRebirthRecall","ui_game_symbol_twitch_blur",selected.Distance<=RecallDistance&&string.Equals(selected.Order,"Follow",StringComparison.OrdinalIgnoreCase));
            return;
        }
        if (selected.IsDog)
        {
            if (selected.DogLifecycle == RebirthDogLifecycleKind.PickedUp) return;

            if (selected.DogLifecycle == RebirthDogLifecycleKind.AwaitingRespawn)
            {
                AddCommand(output, RebirthCompanionCommand.ReportForDuty, "xuiRebirthDogReportForDuty", "ui_game_symbol_check", true);
                return;
            }

            // A durable Active dog keeps its complete command surface even during the short
            // window between observer creation and entity projection. Runtime-only commands
            // are disabled rather than deleting every row. This is also what lets a remote
            // Stay/Guard dog remain manageable while its 2.6-style chunk observer loads.
            EntityRebirthDogCompanion liveDog = world != null && selected.EntityId >= 0
                ? world.GetEntity(selected.EntityId) as EntityRebirthDogCompanion
                : null;
            RebirthNpcRuntimeState dogState = liveDog != null ? liveDog.RebirthRuntimeState : null;
            bool hasLiveDog = liveDog != null && dogState != null && dogState.StableId == npcId && !liveDog.IsDead();

            RebirthNpcOrderState effectiveOrder;
            if (!TryParseOrder(selected.Order, out effectiveOrder))
                effectiveOrder = RebirthNpcOrderState.Follow;
            if (hasLiveDog) effectiveOrder = dogState.Order;

            // 2.6 dog Storage/"Show Me your inventory" was an inventory-open action.
            // In 3.1 the dog inventory already lives in the Companions Inventory tab; do not
            // manufacture or lock a second native loot container for this command.
            bool dogStorageInRange = selected.Distance <= StorageDistance;
            bool dogPositionalInRange = selected.Distance <= PositionalCommandDistance;
            bool dogBehaviorInRange = selected.Distance <= BehaviorCommandDistance;
            // Dog action order for the Companions management window. Recall is intentionally
            // available here because this is the remote-management surface.
            AddCommand(output, RebirthCompanionCommand.Storage, "xuiStorage", "ui_game_symbol_loot_sack",
                !selected.DogIsTamedWild && RebirthDogInventoryPolicy.InventoryEnabled && hasLiveDog && dogStorageInRange);
            AddCommand(output, RebirthCompanionCommand.PickUp, "xuiRebirthDogPickUpCompanion", "ui_game_symbol_hand",
                !selected.DogIsTamedWild && hasLiveDog && selected.Distance <= RebirthDogLifecycleService.MaximumPickupDistance);
            AddCommand(output, RebirthCompanionCommand.Follow, "xuiRebirthDogFollow", "ui_game_symbol_run",
                dogPositionalInRange && effectiveOrder != RebirthNpcOrderState.Follow);
            AddCommand(output, RebirthCompanionCommand.Stay, "xuiRebirthDogStayWhereDogIs", "ui_game_symbol_run_and_gun",
                dogPositionalInRange && effectiveOrder != RebirthNpcOrderState.Stay);
            AddCommand(output, RebirthCompanionCommand.Guard, "xuiRebirthDogStayWhereOwnerIs", "ui_game_symbol_run_and_gun",
                dogPositionalInRange && RebirthAnimalHandlingService.CanUseDogCommandForUi(player, selected, RebirthCompanionCommand.Guard));
            AddCommand(output, RebirthCompanionCommand.GuardArea, "xuiRebirthDogGuardArea", "ui_game_symbol_twitch_shield",
                dogPositionalInRange && RebirthAnimalHandlingService.CanUseDogCommandForUi(player, selected, RebirthCompanionCommand.GuardArea));
            AddCommand(output, selected.AttackStopped ? RebirthCompanionCommand.Resume : RebirthCompanionCommand.Stop,
                selected.AttackStopped ? "xuiRebirthResume" : "xuiRebirthStop", selected.AttackStopped ? "ui_game_symbol_twitch_play" : "ui_game_symbol_twitch_pause", dogBehaviorInRange);
            RebirthCompanionCommand behaviorCommand = selected.BehaviorMode == RebirthCompanionBehaviorMode.Hunting ? RebirthCompanionCommand.FullControl : RebirthCompanionCommand.Hunting;
            AddCommand(output, behaviorCommand,
                selected.BehaviorMode == RebirthCompanionBehaviorMode.Hunting ? "xuiRebirthFullControl" : "xuiRebirthHunting",
                selected.BehaviorMode == RebirthCompanionBehaviorMode.Hunting ? "ui_game_symbol_intellect" : "ui_game_symbol_spear",
                dogBehaviorInRange && RebirthAnimalHandlingService.CanUseDogCommandForUi(player, selected, behaviorCommand));
            AddCommand(output, RebirthCompanionCommand.SetRespawnPoint, "xuiRebirthDogSetRespawnPoint", "ui_game_symbol_drop_item", hasLiveDog);
            AddCommand(output, RebirthCompanionCommand.Recall, "xuiRebirthRecall", "ui_game_symbol_twitch_blur",
                selected.Distance <= RecallDistance && effectiveOrder == RebirthNpcOrderState.Follow);
            AddCommand(output, RebirthCompanionCommand.Dismiss, "xuiRebirthDogDismissCompanion", "ui_game_symbol_x", hasLiveDog);
            if (RebirthSurvivorMode.IsEnabledForCurrentWorld())
            {
                string horrorReason, rageReason;
                AddCommand(output, RebirthCompanionCommand.DeployHorrorPanther, "xuiRebirthDeployHorrorPanther", "ui_game_symbol_allies", RebirthSpecialPantherService.CanDeploy(player,RebirthSpecialPantherService.WitchDoctorRoute,out horrorReason));
                AddCommand(output, RebirthCompanionCommand.DeployRagePanther, "xuiRebirthDeployRagePanther", "ui_game_symbol_allies", RebirthSpecialPantherService.CanDeploy(player,RebirthSpecialPantherService.BerserkerRoute,out rageReason));
            }
            return;
        }
        RebirthNpcRuntimeState state; int entityId;
        if (!RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId) || !RebirthNpcRuntimeRegistry.TryGet(entityId, out state)) return;
        bool npcPositionalInRange = selected.Distance <= PositionalCommandDistance;
        bool npcBehaviorInRange = selected.Distance <= BehaviorCommandDistance;
        AddCommand(output, RebirthCompanionCommand.Follow, "xuiRebirthFollow", "ui_game_symbol_run",
            npcPositionalInRange && state.Order != RebirthNpcOrderState.Follow);
        AddCommand(output, RebirthCompanionCommand.Stay, "xuiRebirthStayWhereCompanionIs", "ui_game_symbol_run_and_gun",
            npcPositionalInRange && state.Order != RebirthNpcOrderState.Stay);
        AddCommand(output, RebirthCompanionCommand.Guard, "xuiRebirthStayWhereOwnerIs", "ui_game_symbol_frames", npcPositionalInRange);
        AddCommand(output, RebirthCompanionCommand.Recall, "xuiRebirthRecall", "ui_game_symbol_twitch_blur",
            selected.Distance <= RecallDistance && state.Order == RebirthNpcOrderState.Follow);
        AddCommand(output, RebirthCompanionCommand.Hunting, "xuiRebirthHunting", "ui_game_symbol_spear", npcBehaviorInRange && selected.BehaviorMode != RebirthCompanionBehaviorMode.Hunting);
        AddCommand(output, RebirthCompanionCommand.FullControl, "xuiRebirthFullControl", "ui_game_symbol_intellect", npcBehaviorInRange && selected.BehaviorMode != RebirthCompanionBehaviorMode.FullControl);
        AddCommand(output, RebirthCompanionCommand.Stop, "xuiRebirthStop", "ui_game_symbol_twitch_pause", npcBehaviorInRange && !selected.AttackStopped);
        AddCommand(output, RebirthCompanionCommand.Resume, "xuiRebirthResume", "ui_game_symbol_twitch_play", npcBehaviorInRange && selected.AttackStopped);
    }

    private static void AddCommand(List<RebirthCompanionCommandEntry> output,
        RebirthCompanionCommand command, string localizationKey, string icon, bool enabled, string iconAtlas = "UIAtlas")
    {
        output.Add(new RebirthCompanionCommandEntry
        {
            Command = command,
            Text = Localization.Get(localizationKey),
            Icon = icon,
            IconAtlas = string.IsNullOrEmpty(iconAtlas) ? "UIAtlas" : iconAtlas,
            Enabled = enabled
        });
    }

    private static void AddCommandText(List<RebirthCompanionCommandEntry> output,
        RebirthCompanionCommand command, string text, string icon, bool enabled, string iconAtlas = "UIAtlas")
    {
        output.Add(new RebirthCompanionCommandEntry
        {
            Command = command,
            Text = text ?? string.Empty,
            Icon = icon,
            IconAtlas = string.IsNullOrEmpty(iconAtlas) ? "UIAtlas" : iconAtlas,
            Enabled = enabled
        });
    }

    // The caller owns this cache for one synchronous, observation-only refresh.
    // Never retain it for actions, later requests or another world.
    internal static RebirthDogInventorySnapshot GetDogInventoryForRefresh(
        RebirthNpcStableId id, Dictionary<RebirthNpcStableId, RebirthDogInventorySnapshot> cache)
    {
        RebirthDogInventorySnapshot snapshot;
        if (cache != null && cache.TryGetValue(id, out snapshot)) return snapshot;
        snapshot = RebirthDogInventoryService.GetSnapshot(id);
        if (cache != null) cache[id] = snapshot;
        return snapshot;
    }

    public static void BuildInventory(
        World world,
        EntityPlayer player,
        RebirthCompanionListEntry selected,
        List<RebirthCompanionInventoryEntry> output,
        Dictionary<RebirthNpcStableId, RebirthDogInventorySnapshot> dogInventories = null)
    {
        if (output == null || selected == null) return;
        output.Clear();
        if(selected.IsBoundUndead)return; // no undead inventory is authored in PC009
        // Inventory/Storage is a local companion interaction. Keep the tab present, but do
        // not expose its contents beyond the same 20 m rule used by the Storage action.
        if (selected.Distance > StorageDistance) return;
        if (selected.Kind == RebirthCompanionTargetKind.Drone)
        {
            EntityDrone drone = world?.GetEntity(selected.EntityId) as EntityDrone;
            ItemStack[] slots = drone?.bag?.ItemGrid.items;
            PackedBoolArray locks = drone?.bag?.LockedSlots;
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack stack = slots[i];
                if (stack == null || stack.IsEmpty()) continue;
                output.Add(new RebirthCompanionInventoryEntry
                {
                    RowKey = "D:" + selected.EntityId + ":" + i,
                    SlotIndex = i,
                    ItemType = stack.itemValue.type,
                    ItemKey = stack.itemValue.ItemClass?.Name ?? string.Empty,
                    Count = stack.count,
                    Locked = locks != null && i >= 0 && i < locks.Length && locks[i],
                    CanUse = CanUseFromManagement(stack.itemValue.type)
                });
            }
            return;
        }

        RebirthNpcStableId npcId;
        if (!RebirthNpcStableId.TryParse(selected.StableId, out npcId)) return;
        if (selected.IsDog)
        {
            if (selected.DogIsTamedWild) return;
            // Persisted inventory remains intact while the dog is unloaded, but there is no
            // live entity interaction/lock surface, so mutation waits for the chunk projection.
            if (selected.DogLifecycle == RebirthDogLifecycleKind.Active && selected.EntityId < 0) return;
            if (!RebirthDogInventoryPolicy.InventoryEnabled) return;
            RebirthDogInventorySnapshot dogSnapshot = GetDogInventoryForRefresh(npcId, dogInventories);
            for (int i = 0; i < dogSnapshot.Slots.Length; i++)
            {
                ItemStack stack = dogSnapshot.Slots[i]; if (stack == null || stack.IsEmpty()) continue;
                output.Add(new RebirthCompanionInventoryEntry
                {
                    RowKey = "N:" + npcId + ":" + i, SlotIndex = i, ItemType = stack.itemValue.type,
                    ItemKey = stack.itemValue.ItemClass != null ? stack.itemValue.ItemClass.Name : string.Empty, Count = stack.count,
                    Locked = dogSnapshot.Locks != null && dogSnapshot.Locks[i], CanUse = CanUseFromManagement(stack.itemValue.type)
                });
            }
            return;
        }
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
        if (snapshot?.Quantities == null) return;
        List<string> keys = new List<string>(snapshot.Quantities.Keys);
        // Resolve display names once per refresh, not twice per sort comparison.
        // Keep this cache local so language/item-definition changes need no invalidation.
        Dictionary<string, string> displayNames = new Dictionary<string, string>(keys.Count, StringComparer.Ordinal);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            ItemClass item = ItemClass.GetItemClass(key);
            displayNames[key] = item != null ? item.GetLocalizedItemName() : key;
        }
        keys.Sort(delegate(string a, string b)
        {
            return string.Compare(displayNames[a], displayNames[b], StringComparison.OrdinalIgnoreCase);
        });
        for (int i = 0; i < keys.Count; i++)
        {
            string itemKey = keys[i];
            int count = snapshot.Quantities[itemKey];
            if (count <= 0) continue;
            ItemValue value = ItemClass.GetItem(itemKey);
            if (value.IsEmpty() || value.ItemClass == null) continue;
            output.Add(new RebirthCompanionInventoryEntry
            {
                RowKey = "N:" + npcId + ":" + itemKey,
                SlotIndex = -1,
                ItemType = value.type,
                ItemKey = itemKey,
                Count = count,
                Locked = RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId, itemKey),
                CanUse = CanUseFromManagement(value.type)
            });
        }
    }

    public static bool CanUseFromManagement(int itemType)
    {
        ItemClass item = ItemClass.GetForId(itemType);
        return item != null && item.Actions != null && item.Actions.Length > 0 && item.Actions[0] is ItemActionEat;
    }

    public static void RequestTeleportFollowers()
    {
        EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        if (player == null || persistent?.PrimaryId == null || player.IsDead()) return;
        if (player.AttachedToEntity is EntityVehicle) return;

        string owner = persistent.PrimaryId.ToString();
        bool hasFollower = HasTeleportTarget(GameManager.Instance.World, player, owner);

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null) return;
        if (connection.IsServer)
        {
            // The authoritative host knows about unloaded persistent dogs. If there are no
            // eligible followers at all, preserve the old shortcut's no-op behavior.
            if (!hasFollower) return;
            Process(GameManager.Instance.World, player.entityId, persistent.PrimaryId, "*", RebirthCompanionCommand.TeleportFollowers);
        }
        else
        {
            // A remote client's local projection can legitimately know nothing about a Follow
            // dog whose old chunk is already unloaded. Do not let that incomplete projection
            // suppress the authenticated server request; the server will validate/no-op it.
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCompanionCommand>()
                .Setup(player.entityId, persistent, "*", RebirthCompanionCommand.TeleportFollowers));
        }

        // Keep the whistle local only when this client actually knows an eligible follower.
        // A server-only unloaded dog can still be recovered without inventing client state.
        if (hasFollower)
        {
            try { Audio.Manager.PlayInsidePlayerHead("FuriousRamsayWhistle"); } catch { }
        }
    }

    public static void Request(string targetId, RebirthCompanionCommand command)
    {
        EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        if (player == null || persistent?.PrimaryId == null || string.IsNullOrEmpty(targetId)) return;

        RebirthDogExistenceDebug.TraceCommand(
            "LOCAL_REQUEST player=" + player.entityId + " target='" + targetId + "' command=" + command, player.entityId);

        // Apply the same distance/order policy on direct local invocation as the command
        // list. This prevents radial/direct-window shortcuts from bypassing disabled rows.
        if (command == RebirthCompanionCommand.Recall || command == RebirthCompanionCommand.Storage ||
            command == RebirthCompanionCommand.Follow || command == RebirthCompanionCommand.Stay ||
            command == RebirthCompanionCommand.Guard || command == RebirthCompanionCommand.GuardArea ||
            command == RebirthCompanionCommand.Hunting || command == RebirthCompanionCommand.FullControl ||
            command == RebirthCompanionCommand.Stop || command == RebirthCompanionCommand.Resume)
        {
            RebirthCompanionListEntry localTarget;
            if (!TryGetLocalOwnedEntry(targetId, out localTarget))
            {
                RebirthDogExistenceDebug.TraceCommand(
                    "LOCAL_SUPPRESSED target not found in local owned snapshot target='" + targetId + "' command=" + command, player.entityId);
                return;
            }
            if (!IsCommandAllowedForTarget(localTarget, command))
            {
                RebirthDogExistenceDebug.TraceCommand(
                    "LOCAL_SUPPRESSED policy target='" + targetId + "' command=" + command +
                    " distance=" + localTarget.Distance.ToString("0.0", CultureInfo.InvariantCulture) +
                    " order='" + (localTarget.Order ?? string.Empty) + "' entityId=" + localTarget.EntityId, player.entityId);
                return;
            }
        }

        if (command == RebirthCompanionCommand.Recall)
        {
            try { Audio.Manager.PlayInsidePlayerHead("FuriousRamsayWhistle"); } catch { }
        }

        // Storage is a native loot/bag surface for both drones and dogs. The Companions
        // Inventory tab remains available for management, but the Storage action itself
        // must behave like the 2.6 "show me your inventory" action.
        if (command == RebirthCompanionCommand.Storage)
        {
            OpenNativeStorageFromManagement(player, targetId);
            return;
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        RebirthDogExistenceDebug.TraceCommand(
            "LOCAL_DISPATCH target='" + targetId + "' command=" + command +
            " via=" + (connection.IsServer ? "direct-server" : "network-to-server"), player.entityId);
        if (connection.IsServer)
            Process(GameManager.Instance.World, player.entityId, persistent.PrimaryId, targetId, command);
        else
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCompanionCommand>()
                .Setup(player.entityId, persistent, targetId, command));
    }


    public static void Process(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        string targetId,
        RebirthCompanionCommand command)
    {
        RebirthDogExistenceDebug.TraceCommand(
            "RECEIVED player=" + playerId + " target='" + (targetId ?? string.Empty) + "' command=" + command, playerId);
        if (command == RebirthCompanionCommand.AdminRemove)
        {
            EntityPlayer adminPlayer = world?.GetEntity(playerId) as EntityPlayer;
            PersistentPlayerData adminPersistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId);
            if (adminPlayer == null || adminPersistent?.PrimaryId == null || userId == null || !adminPersistent.PrimaryId.Equals(userId)) return;
            if (!(adminPlayer.IsAdmin || adminPlayer.IsGodMode.Value)) return;
            if (string.IsNullOrEmpty(targetId) || !targetId.StartsWith("N:", StringComparison.Ordinal)) return;
            RebirthNpcStableId adminTargetId;
            if (!RebirthNpcStableId.TryParse(targetId.Substring(2), out adminTargetId)) return;
            int adminEntityId;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(adminTargetId, out adminEntityId)) return;
            EntityRebirthDogCompanion adminDog = world.GetEntity(adminEntityId) as EntityRebirthDogCompanion;
            RebirthDogLifecycleService.TryAdminRemove(world, adminPlayer, adminDog);
            return;
        }

        if (command == RebirthCompanionCommand.TeleportFollowers)
        {
            ProcessTeleportFollowers(world, playerId, userId);
            return;
        }

        if (command == RebirthCompanionCommand.DeployHorrorPanther || command == RebirthCompanionCommand.DeployRagePanther)
        {
            EntityPlayer pantherOwner=world?.GetEntity(playerId) as EntityPlayer;
            PersistentPlayerData pantherPersistent=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId);
            if(pantherOwner==null||pantherPersistent?.PrimaryId==null||userId==null||!pantherPersistent.PrimaryId.Equals(userId))return;
            string pantherReason;string route=command==RebirthCompanionCommand.DeployHorrorPanther?RebirthSpecialPantherService.WitchDoctorRoute:RebirthSpecialPantherService.BerserkerRoute;
            bool deployed=RebirthSpecialPantherService.TryDeploy(world,pantherOwner,route,out pantherReason);
            if(!string.IsNullOrEmpty(pantherReason))GameManager.ShowTooltipMP(pantherOwner,pantherReason,deployed?"ui_success":"ui_denied");
            return;
        }

        // Lock Drone to Player is a separate custom Rebirth tether. The TAB radial can
        // invoke it outside the ordinary management command gate, so validate direct
        // ownership here rather than folding it into vanilla access Lock/Unlock.
        if (command == RebirthCompanionCommand.LockToPlayer ||
            command == RebirthCompanionCommand.UnlockFromPlayer)
        {
            EntityPlayer lockOwner;
            EntityDrone lockDrone;
            if (!TryResolveOwnedDrone(world, playerId, userId, targetId, out lockOwner, out lockDrone)) return;
            RebirthDroneLockToPlayerService.SetState(
                lockDrone, lockOwner, command == RebirthCompanionCommand.LockToPlayer);
            return;
        }

        EntityPlayer player;
        RebirthCompanionListEntry target;
        if (!TryResolveAuthorizedTarget(world, playerId, userId, targetId, out player, out target))
        {
            RebirthDogExistenceDebug.TraceCommand(
                "DENIED authorization/target-resolution target='" + (targetId ?? string.Empty) + "' command=" + command, playerId);
            return;
        }
        if (!IsCommandAllowedForTarget(target, command))
        {
            RebirthDogExistenceDebug.TraceCommand(
                "DENIED command-policy target='" + target.Id + "' command=" + command +
                " distance=" + target.Distance.ToString("0.0", CultureInfo.InvariantCulture) +
                " order='" + (target.Order ?? string.Empty) + "'", playerId);
            return;
        }
        RebirthDogExistenceDebug.TraceCommand(
            "AUTHORIZED target='" + target.Id + "' stable='" + (target.StableId ?? string.Empty) +
            "' entityId=" + target.EntityId + " isDog=" + target.IsDog + " command=" + command +
            " distance=" + target.Distance.ToString("0.0", CultureInfo.InvariantCulture), playerId);

        if (target.Kind == RebirthCompanionTargetKind.Drone)
        {
            EntityDrone drone = world.GetEntity(target.EntityId) as EntityDrone;
            if (drone == null) return;
            switch (command)
            {
                case RebirthCompanionCommand.LockAccess:
                    drone.playSound("locking");
                    drone.SetLocked(true);
                    drone.SendSyncData((ushort)2);
                    break;
                case RebirthCompanionCommand.Unlock:
                    drone.playSound("unlocking");
                    drone.SetLocked(false);
                    drone.SendSyncData((ushort)2);
                    break;
                case RebirthCompanionCommand.Follow:
                    drone.FollowMode();
                    break;
                case RebirthCompanionCommand.Stay:
                    // Lock-to-Player owns the drone's positional order and always means Follow.
                    // Reject Stay server-side as well as disabling it in the UI.
                    if (!RebirthDroneLockToPlayerService.IsLockedToPlayer(drone.entityId))
                        drone.SentryMode();
                    break;
                case RebirthCompanionCommand.Recall:
                    // Recall is transient and only valid while the drone is already Following.
                    // The shared policy above also enforces the 150 m maximum.
                    if (drone.OrderState == EntityDrone.Orders.Follow)
                    {
                        float recoveryDistance = Vector3.Distance(drone.position, player.position);
                        RebirthComplexSkillSystemService.OnMeaningfulStockDroneRecovery(player, drone, recoveryDistance);
                        TeleportToPlayerPosition(drone, player);
                    }
                    break;
                case RebirthCompanionCommand.QuietToggle:
                    // Do not call EntityDrone.ToggleQuietMode() from the management window path:
                    // vanilla ToggleQuietMode ends by calling stopInteraction(), which unlocks the
                    // current LockManager interaction and closes the Companions modal. Reproduce
                    // the state/audio/sync portion directly instead.
                    drone.isQuietMode = !drone.isQuietMode;
                    drone.playVO("drone_command", true);
                    drone.idleLoop?.Stop(drone.entityId);
                    drone.idleLoop = null;
                    if (drone.isQuietMode) StopKnownDroneAudio(drone);
                    drone.SendSyncData((ushort)32);
                    break;
                case RebirthCompanionCommand.LightToggle:
                    if (drone.IsFlashlightAttached) drone.ToggleLightAction();
                    break;
                case RebirthCompanionCommand.HealMe:
                    if (drone.IsHealModAttached) drone.HealRequest();
                    break;
                case RebirthCompanionCommand.ToggleHealAllies:
                    if (drone.IsHealModAttached) drone.ToggleHealAllies();
                    break;
            }
            return;
        }

        RebirthNpcStableId npcId;
        if (!RebirthNpcStableId.TryParse(target.StableId, out npcId))
        {
            RebirthDogExistenceDebug.TraceCommand(
                "NOOP invalid StableId target='" + target.Id + "' stable='" + (target.StableId ?? string.Empty) + "'", playerId);
            return;
        }
        if (target.IsBoundUndead)
        {
            string boundReason; bool ok=RebirthBoundUndeadService.TryProcessCompanionCommand(world,player,npcId,command,out boundReason);
            if(!string.IsNullOrEmpty(boundReason))GameManager.ShowTooltipMP(player,boundReason,ok?"ui_success":"ui_denied");
            return;
        }
        if (target.IsDog && command == RebirthCompanionCommand.ReportForDuty)
        {
            EntityRebirthDogCompanion respawned; string reason;
            RebirthDogLifecycleService.TryReportForDuty(world, player, npcId, out respawned, out reason);
            return;
        }
        EntityRebirthNPC npc = world.GetEntity(target.EntityId) as EntityRebirthNPC;
        RebirthNpcRuntimeState runtime = npc?.RebirthRuntimeState;

        // A durable dog row can outlive its physical projection. Attempt one authoritative
        // same-StableId reconstruction before treating the command as a no-op. This means a
        // Stay/Follow click can recover a ghost rather than silently succeeding against entity -1.
        if ((npc == null || runtime == null) && target.IsDog)
        {
            EntityRebirthDogCompanion repairedDog;
            string repairReason;
            if (RebirthDogLifecycleService.TryRepairActiveGhostDog(
                    world, player, npcId, out repairedDog, out repairReason) && repairedDog != null)
            {
                npc = repairedDog;
                runtime = repairedDog.RebirthRuntimeState;
                RebirthDogExistenceDebug.TraceCommand(
                    "GHOST_REPAIRED stable='" + target.StableId + "' entityId=" + repairedDog.entityId +
                    " command=" + command, playerId);
            }
            else
            {
                RebirthDogExistenceDebug.TraceCommand(
                    "GHOST_REPAIR_FAILED stable='" + target.StableId + "' command=" + command +
                    " reason='" + (repairReason ?? string.Empty) + "'", playerId);
            }
        }

        if (npc == null || runtime == null)
        {
            RebirthDogExistenceDebug.TraceCommand(
                "NOOP missing live NPC entity target='" + target.Id + "' stable='" + target.StableId +
                "' entityId=" + target.EntityId + " worldEntity=" +
                (world.GetEntity(target.EntityId) != null ? world.GetEntity(target.EntityId).GetType().Name : "<none>") +
                " runtime=" + (runtime != null), playerId);
            return;
        }
        string issuer = userId.ToString();
        if (target.IsDog)
        {
            EntityRebirthDogCompanion dog = npc as EntityRebirthDogCompanion;
            if (dog == null)
            {
                RebirthDogExistenceDebug.TraceCommand(
                    "NOOP target is marked dog but world entity is " + npc.GetType().Name + " entityId=" + target.EntityId, playerId);
                return;
            }
            RebirthNpcPersistentRecordView authoritativeDogRecord; RebirthDogPersistentRecordView dogProgression;
            string trainingGateReason = string.Empty;
            bool hasDogProgression = RebirthDogStateService.TryGetView(npcId, out authoritativeDogRecord, out dogProgression);
            bool advancedTrainingCommand = command == RebirthCompanionCommand.Guard || command == RebirthCompanionCommand.GuardArea || command == RebirthCompanionCommand.Hunting;
            bool rebirthProgression = RebirthSurvivorMode.IsEnabledForCurrentWorld();
            if ((!hasDogProgression && advancedTrainingCommand && rebirthProgression) ||
                (hasDogProgression && !RebirthAnimalHandlingService.CanUseDogCommand(player, dogProgression, command, out trainingGateReason)))
            {
                if (!hasDogProgression) trainingGateReason = Localization.Get("xuiRebirthDogCommandUnavailable");
                if (!string.IsNullOrEmpty(trainingGateReason)) GameManager.ShowTooltipMP(player, trainingGateReason, "ui_denied");
                RebirthDogExistenceDebug.TraceCommand("DENIED animal-handling stable='" + target.StableId + "' command=" + command + " reason='" + (trainingGateReason ?? string.Empty) + "'", playerId);
                return;
            }
            RebirthDogExistenceDebug.TraceCommand(
                "LIVE_DOG entity=" + dog.entityId + " pos=" + dog.position + " addedToChunk=" + dog.addedToChunk +
                " chunk=" + dog.chunkPosAddedEntityTo.x + "," + dog.chunkPosAddedEntityTo.z +
                " runtimeOrder=" + runtime.Order + " applying=" + command, playerId);
            switch (command)
            {
                case RebirthCompanionCommand.Follow:
                    ApplyDogPositionalOrder(dog, npcId, RebirthNpcOrderState.Follow, Vector3.zero, false, false, dog.rotation.y);
                    return;
                case RebirthCompanionCommand.Stay:
                    // 2.6 "Stay Where You're Standing": snapshot the DOG'S current position
                    // and make the hold strictly stationary.
                    RebirthDogStateService.SetGuardStationaryStay(npcId, true);
                    ApplyDogPositionalOrder(dog, npcId, RebirthNpcOrderState.Stay, dog.position, false, true, dog.rotation.y);
                    RebirthAnimalHandlingService.BeginStayExercise(player, dog);
                    return;
                case RebirthCompanionCommand.Guard:
                    // Player-facing second Stay command: snapshot the OWNER'S block-centered
                    // current position and keep this Guard-backed order strictly stationary.
                    RebirthDogStateService.SetGuardStationaryStay(npcId, true);
                    ApplyDogPositionalOrder(dog, npcId, RebirthNpcOrderState.Guard,
                        RebirthDogRuntimeService.CenterBlockPosition(player.position), true, false, player.rotation.y);
                    return;
                case RebirthCompanionCommand.GuardArea:
                    // True bounded Guard is separate from both 2.6 Stay commands. It may
                    // pursue valid targets inside its leash and returns to this anchor afterward.
                    RebirthDogStateService.SetGuardStationaryStay(npcId, false);
                    ApplyDogPositionalOrder(dog, npcId, RebirthNpcOrderState.Guard,
                        RebirthDogRuntimeService.CenterBlockPosition(player.position), true, false, player.rotation.y);
                    return;
                case RebirthCompanionCommand.Recall:
                    if (runtime.Order == RebirthNpcOrderState.Follow) RebirthDogRuntimeService.Recall(dog, player);
                    return;
                case RebirthCompanionCommand.Hunting:
                    RebirthDogStateService.SetOwnerCombatMode(player, RebirthCompanionBehaviorMode.Hunting);
                    TryPlayDogCommandSound(player, "NPCHuntingMode"); return;
                case RebirthCompanionCommand.FullControl:
                    RebirthDogStateService.SetOwnerCombatMode(player, RebirthCompanionBehaviorMode.FullControl);
                    TryPlayDogCommandSound(player, "NPCFullControlMode"); return;
                case RebirthCompanionCommand.Stop:
                    RebirthDogStateService.SetOwnerAttackStopped(player, true);
                    TryPlayDogCommandSound(player, "NPCHalt"); return;
                case RebirthCompanionCommand.Resume:
                    RebirthDogStateService.SetOwnerAttackStopped(player, false);
                    TryPlayDogCommandSound(player, "NPCResume"); return;
                case RebirthCompanionCommand.SetRespawnPoint:
                {
                    string respawnPointReason;
                    bool set = RebirthDogLifecycleService.TrySetRespawnPoint(world, player, dog, out respawnPointReason);
                    if (!string.IsNullOrEmpty(respawnPointReason))
                        GameManager.ShowTooltipMP(player, respawnPointReason, set ? "ui_success" : "ui_denied");
                    return;
                }
                case RebirthCompanionCommand.PickUp:
                {
                    string pickupReason;
                    bool pickedUp = RebirthDogLifecycleService.TryPickup(world, player, dog, out pickupReason);
                    if (!string.IsNullOrEmpty(pickupReason))
                        GameManager.ShowTooltipMP(player, pickupReason, pickedUp ? "ui_success" : "ui_denied");
                    return;
                }
                case RebirthCompanionCommand.Dismiss:
                {
                    string dismissReason;
                    bool dismissed = RebirthDogLifecycleService.TryDismiss(world, player, dog, out dismissReason);
                    if (!string.IsNullOrEmpty(dismissReason))
                        GameManager.ShowTooltipMP(player, dismissReason, dismissed ? "ui_success" : "ui_denied");
                    return;
                }
            }
        }
        switch (command)
        {
            case RebirthCompanionCommand.Follow:
                RebirthNpcCommandGateway.SubmitAuthenticated(npc, issuer, playerId,
                    RebirthNpcCommandKind.Follow, Vector3.zero, false, runtime.Revision);
                break;
            case RebirthCompanionCommand.Stay:
                RebirthNpcCommandGateway.SubmitAuthenticated(npc, issuer, playerId,
                    RebirthNpcCommandKind.Stay, Vector3.zero, false, runtime.Revision);
                break;
            case RebirthCompanionCommand.Guard:
                RebirthNpcCommandGateway.SubmitAuthenticated(npc, issuer, playerId,
                    RebirthNpcCommandKind.Guard, player.position, true, runtime.Revision);
                break;
            case RebirthCompanionCommand.Recall:
                if (runtime.Order == RebirthNpcOrderState.Follow)
                    TeleportToPlayerPosition(npc, player);
                break;
            case RebirthCompanionCommand.Hunting:
                RebirthCompanionBehaviorService.SetMode(npcId, RebirthCompanionBehaviorMode.Hunting);
                break;
            case RebirthCompanionCommand.FullControl:
                RebirthCompanionBehaviorService.SetMode(npcId, RebirthCompanionBehaviorMode.FullControl);
                break;
            case RebirthCompanionCommand.Stop:
                RebirthCompanionBehaviorService.SetAttackStopped(npcId, true);
                break;
            case RebirthCompanionCommand.Resume:
                RebirthCompanionBehaviorService.SetAttackStopped(npcId, false);
                break;
        }
    }

    private static bool HasTeleportTarget(World world, EntityPlayer player, string ownerId)
    {
        if (world == null || player == null || string.IsNullOrWhiteSpace(ownerId)) return false;

        // The manual teleport shortcut is the 2.6 Q-style "bring followers here" action.
        // It has no distance ceiling: a stranded follower is exactly what the shortcut
        // must recover.
        List<Entity> entities = world.Entities != null ? world.Entities.list : null;
        if (entities != null)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                EntityDrone drone = entities[i] as EntityDrone;
                if (drone != null && !drone.IsDead() && drone.belongsPlayerId == player.entityId &&
                    drone.OrderState == EntityDrone.Orders.Follow)
                    return true;
            }
        }

        RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
        for (int i = 0; i < states.Length; i++)
        {
            RebirthNpcRuntimeState state = states[i];
            if (state != null && state.Presence == RebirthNpcPresenceState.Active &&
                state.OwnershipKind == RebirthNpcOwnershipKind.Player &&
                state.Order == RebirthNpcOrderState.Follow &&
                string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // A Follow dog may already have lost its live projection because its old chunk
        // unloaded. Durable ownership/order is still authoritative, so keep the shortcut
        // available and let the server materialize the original saved entity for recovery.
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            RebirthNpcOrderState order;
            if (record == null || record.Identity == null || record.Ownership == null ||
                record.Dog == null || record.Dog.Lifecycle != RebirthDogLifecycleKind.Active ||
                !RebirthDogStateService.IsDog(record) || record.Order == null ||
                !Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order) ||
                order != RebirthNpcOrderState.Follow ||
                !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
                continue;
            return true;
        }
        return false;
    }

    private static void ProcessTeleportFollowers(World world, int playerId, PlatformUserIdentifierAbs userId)
    {
        EntityPlayer player = world?.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId);
        if (player == null || persistent?.PrimaryId == null || userId == null || !persistent.PrimaryId.Equals(userId)) return;
        if (player.IsDead() || player.AttachedToEntity is EntityVehicle || player.IsInElevator()) return;

        RebirthCompanionRecallDebug.BeginRecallBatch(player);

        // Manual mass recall means ALL active owned companions, not only companions whose
        // current positional order happens to be Follow. Preserve each order after moving.
        List<Entity> entities = world.Entities != null ? world.Entities.list : null;
        if (entities != null)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                EntityDrone drone = entities[i] as EntityDrone;
                if (drone == null || drone.IsDead() || drone.belongsPlayerId != player.entityId) continue;
                RebirthCompanionRecallDebug.TraceTeleport("DRONE_BEFORE", drone, player);
                TeleportToPlayerPosition(drone, player);
                RebirthCompanionRecallDebug.TraceTeleport("DRONE_AFTER", drone, player);
            }
        }

        string ownerId = userId.ToString();
        HashSet<RebirthNpcStableId> liveDogs = new HashSet<RebirthNpcStableId>();
        RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
        for (int i = 0; i < states.Length; i++)
        {
            RebirthNpcRuntimeState state = states[i];
            if (state == null || state.Presence != RebirthNpcPresenceState.Active ||
                state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                !string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
                continue;

            int entityId;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId)) continue;
            EntityRebirthNPC npc = world.GetEntity(entityId) as EntityRebirthNPC;
            if (npc == null || npc.IsDead()) continue;

            EntityRebirthDogCompanion dog = npc as EntityRebirthDogCompanion;
            if (dog != null)
            {
                liveDogs.Add(state.StableId);
                RebirthCompanionRecallDebug.TraceTeleport("DOG_BEFORE", dog, player);
                RebirthDogRuntimeService.RecallFromShortcut(dog, player);
                RebirthCompanionRecallDebug.TraceTeleport("DOG_AFTER_REQUEST", dog, player);
                continue;
            }

            RebirthCompanionRecallDebug.TraceTeleport("NPC_BEFORE", npc, player);
            TeleportToPlayerPosition(npc, player);
            // A stationary activity keeps a local anchor. Re-submit the same order after
            // relocation so Stay adopts the new current position and Guard uses owner.pos.
            try
            {
                if (state.Order == RebirthNpcOrderState.Stay)
                    npc.SetRebirthOrder(RebirthNpcOrderState.Stay, Vector3.zero, false);
                else if (state.Order == RebirthNpcOrderState.Guard)
                    npc.SetRebirthOrder(RebirthNpcOrderState.Guard, player.position, true);
            }
            catch { }
            RebirthCompanionRecallDebug.TraceTeleport("NPC_AFTER", npc, player);
        }

        // A durable dog may already be unloaded when the shortcut is pressed. Observe its
        // last PHYSICAL chunk and materialize the original embedded StableId; do not spawn a
        // replacement. Manual recall accepts Follow/Stay/Guard and re-anchors after arrival.
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (record == null || record.Identity == null || record.Transform == null ||
                record.Ownership == null || record.Dog == null ||
                record.Dog.Lifecycle != RebirthDogLifecycleKind.Active ||
                !RebirthDogStateService.IsDog(record) ||
                !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase) ||
                liveDogs.Contains(record.Identity.StableNpcId))
                continue;

            Vector3 recoveryPosition = record.Transform.LastSafePosition.HasValue
                ? record.Transform.LastSafePosition.Value
                : record.Transform.WorldPosition;
            bool queued = RebirthDogChunkObserverService.RequestRecallRecovery(
                record.Identity.StableNpcId, ownerId, recoveryPosition);
            RebirthCompanionRecallDebug.TraceUnloadedDogRecovery(record, queued, recoveryPosition);
        }

        // Persist any Stay/Guard anchor changes made by this explicit player action.
        RebirthNpcAggregatePersistenceStore.Save();
        RebirthCompanionRecallDebug.EndRecallBatch(player);
    }

    /// <summary>
    /// Server-authoritative automatic recovery after the owner completes a teleport. Unlike
    /// the explicit shortcut, this preserves intentional Stay/Guard anchors and recalls only
    /// companions whose current order is Follow. It also includes unloaded persistent dogs.
    /// </summary>
    internal static void RecallFollowingAfterOwnerTeleport(EntityPlayer player)
    {
        World world = player != null ? player.world : null;
        if (world == null || world.IsRemote() || player.IsDead() || player.IsInElevator()) return;

        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if (persistent?.PrimaryId == null) return;
        string ownerId = persistent.PrimaryId.ToString();

        RebirthCompanionRecallDebug.BeginRecallBatch(player);

        List<Entity> entities = world.Entities != null ? world.Entities.list : null;
        if (entities != null)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                EntityDrone drone = entities[i] as EntityDrone;
                if (drone == null || drone.IsDead() || drone.belongsPlayerId != player.entityId ||
                    drone.OrderState != EntityDrone.Orders.Follow) continue;
                RebirthCompanionRecallDebug.TraceTeleport("AUTO_DRONE_BEFORE", drone, player);
                TeleportToPlayerPosition(drone, player);
                RebirthCompanionRecallDebug.TraceTeleport("AUTO_DRONE_AFTER", drone, player);
            }
        }

        HashSet<RebirthNpcStableId> liveDogs = new HashSet<RebirthNpcStableId>();
        RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
        for (int i = 0; i < states.Length; i++)
        {
            RebirthNpcRuntimeState state = states[i];
            if (state == null || state.Presence != RebirthNpcPresenceState.Active ||
                state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                state.Order != RebirthNpcOrderState.Follow ||
                !string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
                continue;

            int entityId;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId)) continue;
            EntityRebirthNPC npc = world.GetEntity(entityId) as EntityRebirthNPC;
            if (npc == null || npc.IsDead()) continue;

            EntityRebirthDogCompanion dog = npc as EntityRebirthDogCompanion;
            if (dog != null)
            {
                liveDogs.Add(state.StableId);
                RebirthCompanionRecallDebug.TraceTeleport("AUTO_DOG_BEFORE", dog, player);
                RebirthDogRuntimeService.Recall(dog, player);
                RebirthCompanionRecallDebug.TraceTeleport("AUTO_DOG_AFTER_REQUEST", dog, player);
                continue;
            }

            RebirthCompanionRecallDebug.TraceTeleport("AUTO_NPC_BEFORE", npc, player);
            TeleportToPlayerPosition(npc, player);
            RebirthCompanionRecallDebug.TraceTeleport("AUTO_NPC_AFTER", npc, player);
        }

        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            RebirthNpcOrderState order;
            if (record == null || record.Identity == null || record.Transform == null ||
                record.Ownership == null || record.Dog == null ||
                record.Dog.Lifecycle != RebirthDogLifecycleKind.Active ||
                !RebirthDogStateService.IsDog(record) || record.Order == null ||
                !Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order) ||
                order != RebirthNpcOrderState.Follow ||
                !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase) ||
                liveDogs.Contains(record.Identity.StableNpcId))
                continue;

            Vector3 recoveryPosition = record.Transform.LastSafePosition.HasValue
                ? record.Transform.LastSafePosition.Value
                : record.Transform.WorldPosition;
            bool queued = RebirthDogChunkObserverService.RequestFollowRecovery(
                record.Identity.StableNpcId, ownerId, recoveryPosition);
            RebirthCompanionRecallDebug.TraceUnloadedDogRecovery(record, queued, recoveryPosition);
        }

        RebirthCompanionRecallDebug.EndRecallBatch(player);
    }

    private static void TryPlayDogCommandSound(EntityPlayer player, string sound)
    {
        if (player == null || string.IsNullOrEmpty(sound)) return;
        try { player.PlayOneShot(sound, false); } catch { }
    }

    public static void RequestInventoryAction(
        string targetId,
        RebirthCompanionInventoryAction action,
        int slotIndex,
        int itemType,
        string itemKey,
        int quantity = 0)
    {
        EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        if (player == null || persistent?.PrimaryId == null || string.IsNullOrEmpty(targetId)) return;

        RebirthCompanionListEntry localTarget;
        if (!TryGetLocalOwnedEntry(targetId, out localTarget) || localTarget.Distance > StorageDistance) return;

        if (action == RebirthCompanionInventoryAction.TransferToPlayer)
        {
            if (quantity <= 0 || itemType <= 0) return;
            string selection = slotIndex.ToString(CultureInfo.InvariantCulture) + "|" +
                itemType.ToString(CultureInfo.InvariantCulture) + "|" +
                quantity.ToString(CultureInfo.InvariantCulture) + "|" + (itemKey ?? string.Empty);
            LogisticsTransferService.Request(QuickStackRadialAction.CompanionInventoryPull, targetId, selection);
            return;
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null) return;
        if (connection.IsServer)
            ProcessInventoryAction(GameManager.Instance.World, player.entityId, persistent.PrimaryId,
                targetId, action, slotIndex, itemType, itemKey, quantity);
        else
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCompanionInventoryAction>()
                .Setup(player.entityId, persistent, targetId, action, slotIndex, itemType, itemKey, quantity));
    }

    internal static bool TryReadInventoryTransferSelection(string selection,
        out int slot, out int itemType, out int quantity, out string itemKey)
    {
        slot = -1; itemType = 0; quantity = 0; itemKey = string.Empty;
        string[] fields = (selection ?? string.Empty).Split(new[] { '|' }, 4);
        if (fields.Length != 4 ||
            !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out slot) ||
            !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out itemType) ||
            !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) ||
            slot < -1 || itemType <= 0 || quantity <= 0) return false;
        itemKey = fields[3];
        return true;
    }

    private static void ApplyDogPositionalOrder(
        EntityRebirthDogCompanion dog,
        RebirthNpcStableId stableId,
        RebirthNpcOrderState order,
        Vector3 anchor,
        bool hasGuardAnchor,
        bool captureStayAnchor,
        float anchorYaw)
    {
        if (dog == null || dog.RebirthRuntimeState == null)
        {
            RebirthDogExistenceDebug.TraceCommand(
                "ORDER NOOP dog/runtime missing stable=" + stableId + " order=" + order);
            return;
        }

        RebirthDogExistenceDebug.TraceCommand(
            "ORDER BEGIN entity=" + dog.entityId + " stable=" + stableId + " order=" + order +
            " anchor=" + anchor + " pos=" + dog.position);
        RebirthDogRuntimeService.ClearCombat(dog);
        if (captureStayAnchor)
            RebirthDogStateService.SetStayPosition(stableId, anchor);
        else
            RebirthDogStateService.ClearStayPosition(stableId);

        RebirthNpcTransactionResult result = dog.SetRebirthOrder(order, anchor, hasGuardAnchor);
        if (!result.Succeeded)
        {
            RebirthDogExistenceDebug.TraceCommand(
                "ORDER FAILED entity=" + dog.entityId + " stable=" + stableId + " order=" + order +
                " error='" + (result.Error ?? string.Empty) + "'");
            return;
        }

        // 2.6 persisted both the hold position and the issuing orientation.
        // Stay captures the dog's current facing; "Stay Where I'm Standing"
        // captures the owner's facing at the moment the command is issued.
        RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
        {
            if (r == null) return;
            if (r.Transform == null) r.Transform = new RebirthNpcTransformRecord();
            if (order == RebirthNpcOrderState.Stay || order == RebirthNpcOrderState.Guard)
            {
                r.Transform.AnchorPosition = anchor;
                r.Transform.AnchorRotation = Mathf.Repeat(anchorYaw, 360f);
            }
            else
            {
                r.Transform.AnchorPosition = null;
                r.Transform.AnchorRotation = null;
            }
            unchecked { r.Transform.TransformRevision++; }
        });

        RebirthDogStateService.CaptureRuntime(dog);
        RebirthNpcAggregatePersistenceStore.Save();
        RebirthDogChunkObserverService.Synchronize(dog);
        RebirthDogExistenceDebug.TraceCommand(
            "ORDER APPLIED entity=" + dog.entityId + " stable=" + stableId + " order=" + order +
            " persistedPos=" + dog.position + " addedToChunk=" + dog.addedToChunk +
            " chunk=" + dog.chunkPosAddedEntityTo.x + "," + dog.chunkPosAddedEntityTo.z);
    }

    public static void ProcessInventoryAction(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        string targetId,
        RebirthCompanionInventoryAction action,
        int slotIndex,
        int itemType,
        string itemKey,
        int quantity)
    {
        EntityPlayer player;
        RebirthCompanionListEntry target;
        if (!TryResolveAuthorizedTarget(world, playerId, userId, targetId, out player, out target)) return;
        if (target.IsBoundUndead) return; // Chunk G authors no bound-undead inventory; forged inventory requests fail closed server-side.
        if (target.Distance > StorageDistance) return;
        if (target.Kind == RebirthCompanionTargetKind.Drone)
        {
            EntityDrone drone = world.GetEntity(target.EntityId) as EntityDrone;
            ItemStack[] slots = drone?.bag?.ItemGrid.items;
            if (drone == null || slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
            if (action == RebirthCompanionInventoryAction.ToggleLock)
            {
                PackedBoolArray locks = drone.bag.LockedSlots ?? new PackedBoolArray(slots.Length);
                if (locks.Length != slots.Length) locks.Length = slots.Length;
                locks[slotIndex] = !locks[slotIndex];
                drone.bag.ItemGrid.SetSlotLocks(locks);
                RemoteResourceLiveSync.NotifySourceChanged(
                    RemoteResourceIdentity.Drone(drone.entityId), drone.position,
                    "companion inventory slot lock toggled");
                return;
            }
            if (action == RebirthCompanionInventoryAction.UseOne)
                UseDroneItem(player, drone, slotIndex, itemType);
            else if (action == RebirthCompanionInventoryAction.TransferToPlayer)
            {
                PackedBoolArray currentLocks = drone.bag.LockedSlots;
                if (currentLocks != null && slotIndex < currentLocks.Length && currentLocks[slotIndex]) return;
                TransferDroneItemToPlayer(player, drone, slotIndex, itemType, quantity);
            }
            return;
        }

        RebirthNpcStableId npcId;
        if (!RebirthNpcStableId.TryParse(target.StableId, out npcId)) return;
        if (target.IsDog)
        {
            RebirthNpcPersistentRecordView inventoryRecord; RebirthDogPersistentRecordView inventoryDog;
            if(!RebirthDogStateService.TryGetView(npcId,out inventoryRecord,out inventoryDog)||inventoryDog==null)return;
            // Chunk D does not author species inventories. Tamed wild animals fail closed instead of inheriting dog storage by accident.
            if(inventoryDog.IsTamedWild)return;
            if (!RebirthDogInventoryPolicy.InventoryEnabled) return;
            if (action == RebirthCompanionInventoryAction.ToggleLock) RebirthDogInventoryService.ToggleLock(npcId, slotIndex);
            else if (action == RebirthCompanionInventoryAction.UseOne) RebirthDogInventoryService.UseOne(player, npcId, slotIndex, itemType);
            else if (action == RebirthCompanionInventoryAction.TransferToPlayer) RebirthDogInventoryService.TransferToPlayer(player, npcId, slotIndex, quantity, itemType);
            return;
        }
        if (string.IsNullOrWhiteSpace(itemKey)) return;
        if (action == RebirthCompanionInventoryAction.ToggleLock)
        {
            RebirthNpcInventorySnapshot current = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
            int availableQuantity;
            if (current?.Quantities == null || !current.Quantities.TryGetValue(itemKey, out availableQuantity) || availableQuantity <= 0) return;
            RebirthCompanionInventoryLockService.ToggleNpcItemLock(npcId, itemKey);
            Entity npcEntity = world.GetEntity(target.EntityId);
            if (npcEntity != null)
                RemoteResourceLiveSync.NotifySourceChanged(
                    "N:" + npcId.ToString(), npcEntity.position,
                    "companion inventory item lock toggled");
            return;
        }
        if (action == RebirthCompanionInventoryAction.UseOne)
            UseNpcItem(player, npcId, itemType, itemKey);
        else if (action == RebirthCompanionInventoryAction.TransferToPlayer)
        {
            if (RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId, itemKey)) return;
            TransferNpcItemToPlayer(player, npcId, itemType, itemKey, quantity);
        }
    }

    internal static void SendConsumptionResult(EntityPlayer player, RebirthConsumeResult result)
    {
        if (player is EntityPlayerLocal) RebirthMetabolismUiFeedback.Receive(result);
        else if (player != null)
        {
            var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (connection != null && connection.IsServer)
                connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>().Setup(result),
                    _attachedToEntityId: player.entityId);
        }
    }

    private static void UseDroneItem(EntityPlayer player, EntityDrone drone, int slotIndex, int itemType)
    {
        if (player == null || drone?.bag == null || !CanUseFromManagement(itemType)) return;
        ItemStack[] slots = drone.bag.ItemGrid.items;
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
        ItemStack source = slots[slotIndex];
        if (source == null || source.IsEmpty() || source.itemValue.type != itemType || source.count <= 0) return;
        ItemAction action = source.itemValue.ItemClass.Actions[0];
        RebirthConsumableDefinition consumption;
        if (action is ItemActionConsumeMetabolismRebirth &&
            RebirthConsumableResolver.TryResolve(source.itemValue, out consumption) && consumption != null)
        {
            RebirthConsumeResult result = RebirthMetabolismService.ConsumeExternalSource(player, source,
                delegate(ItemStack expected, ItemStack replacement, ItemStack siblings)
                {
                    if (drone.IsDead() || drone.belongsPlayerId != player.entityId || drone.bag == null) return false;
                    ItemStack[] planned;
                    if (!RebirthCompanionConsumptionSlots.TryPlan(drone.bag.ItemGrid.items, drone.bag.LockedSlots,
                        slotIndex, expected, replacement, siblings, out planned)) return false;
                    drone.bag.SetSlots(planned);
                    drone.SendSyncData((ushort)8);
                    return true;
                });
            SendConsumptionResult(player, result);
            return;
        }
        var treatment = action as ItemActionUseMedRebirth;
        if (treatment != null && !treatment.CanUseTreatment(player)) return;
        ItemStack use = source.Clone();
        use.count = 1;
        ItemStack after = source.Clone();
        after.count--;
        drone.bag.SetSlot(slotIndex, after.count > 0 ? after : ItemStack.Empty);
        bool success = false;
        try { success = action.ExecuteInstantAction(player, use, false, null); }
        catch (Exception ex) { Log.Warning("[Companions] item use failed: " + ex.Message); }
        if (!success) drone.bag.SetSlot(slotIndex, source);
        else drone.SendSyncData((ushort)8);
    }

    private static void UseNpcItem(EntityPlayer player, RebirthNpcStableId npcId, int itemType, string itemKey)
    {
        if (player == null || npcId.IsEmpty || !CanUseFromManagement(itemType)) return;
        ItemValue value = ItemClass.GetItem(itemKey);
        if (value.IsEmpty() || value.ItemClass == null || value.type != itemType) return;
        var treatment = value.ItemClass.Actions[0] as ItemActionUseMedRebirth;
        if (treatment != null && !treatment.CanUseTreatment(player)) return;
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
        int available;
        if (snapshot?.Quantities == null || !snapshot.Quantities.TryGetValue(itemKey, out available) || available <= 0) return;
        using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
            "companions.use-item", RebirthNpcInventoryAuthorityOperations.Mutate, TimeSpan.FromSeconds(30), npcId))
        {
            uint next;
            RebirthNpcInventoryTransactionResult removed = RebirthNpcInventoryTransactionService.Apply(
                new RebirthNpcInventoryTransaction(Guid.NewGuid(), npcId, snapshot.Revision, lease.AuthorityKey,
                    new[] { new RebirthNpcInventoryMutation(itemKey, -1) }), out next);
            if (removed != RebirthNpcInventoryTransactionResult.Applied && removed != RebirthNpcInventoryTransactionResult.Replayed) return;

            ItemStack use = new ItemStack(value, 1);
            bool success = false;
            try { success = value.ItemClass.Actions[0].ExecuteInstantAction(player, use, false, null); }
            catch (Exception ex) { Log.Warning("[Companions] NPC item use failed: " + ex.Message); }
            if (!success)
            {
                uint rollbackRevision = RebirthNpcInventoryTransactionService.GetRevision(npcId);
                uint ignored;
                RebirthNpcInventoryTransactionService.Apply(
                    new RebirthNpcInventoryTransaction(Guid.NewGuid(), npcId, rollbackRevision, lease.AuthorityKey,
                        new[] { new RebirthNpcInventoryMutation(itemKey, 1) }), out ignored);
            }
        }
    }

    private static void TransferDroneItemToPlayer(
        EntityPlayer player, EntityDrone drone, int slotIndex, int itemType, int quantity)
    {
        if (player == null || drone?.bag == null || quantity <= 0) return;
        ItemStack[] slots = drone.bag.ItemGrid.items;
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
        ItemStack source = slots[slotIndex];
        if (source == null || source.IsEmpty() || source.itemValue.type != itemType || source.count <= 0) return;

        ItemStack transfer = source.Clone();
        transfer.count = Math.Min(source.count, quantity);
        int before = transfer.count;
        LogisticsTransferService.MoveIntoBag(player, transfer);
        int moved = before - transfer.count;
        if (moved <= 0) return;

        ItemStack after = source.Clone();
        after.count -= moved;
        drone.bag.SetSlot(slotIndex, after.count > 0 ? after : ItemStack.Empty);
        drone.SendSyncData((ushort)8);
    }

    private static void TransferNpcItemToPlayer(
        EntityPlayer player, RebirthNpcStableId npcId, int itemType, string itemKey, int quantity)
    {
        if (player == null || npcId.IsEmpty || quantity <= 0 || string.IsNullOrWhiteSpace(itemKey)) return;
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (value.IsEmpty() || value.ItemClass == null || value.type != itemType) return;
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
        int available;
        if (snapshot?.Quantities == null || !snapshot.Quantities.TryGetValue(itemKey, out available) || available <= 0) return;

        LogisticsBackpackEndpoint endpoint = new LogisticsBackpackEndpoint(player);
        RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);
        try
        {
            int requested = Math.Min(available, quantity);
            int negotiated = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                endpoint.EndpointId, itemKey, requested, false);
            if (negotiated <= 0) return;
            using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
                "companions.manual-transfer-to-player",
                RebirthNpcInventoryAuthorityOperations.Transfer,
                TimeSpan.FromSeconds(30), npcId))
            {
                uint nextRevision;
                string error;
                RebirthNpcExternalInventoryTransferCoordinator.Apply(
                    new RebirthNpcExternalTransferRequest(
                        Guid.NewGuid(), npcId, snapshot.Revision, endpoint.EndpointId,
                        itemKey, negotiated, RebirthNpcExternalTransferDirection.NpcToExternal,
                        lease.AuthorityKey), out nextRevision, out error);
            }
        }
        finally
        {
            RebirthNpcExternalInventoryEndpointRegistry.Unregister(endpoint.EndpointId);
        }
    }

    private static bool OpenNativeStorageFromManagement(EntityPlayerLocal player, string targetId)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || player == null || string.IsNullOrEmpty(targetId)) return false;

        if (targetId.StartsWith("D:", StringComparison.Ordinal))
        {
            int entityId;
            if (!int.TryParse(targetId.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId))
                return false;

            EntityDrone drone = world.GetEntity(entityId) as EntityDrone;
            if (drone == null || drone.IsDead() || drone.belongsPlayerId != player.entityId || drone.bag == null)
                return false;
            if ((drone.position - player.position).sqrMagnitude > StorageDistance * StorageDistance)
                return false;

            LocalPlayerUI playerUi = LocalPlayerUI.GetUIForPlayer(player);
            if (playerUi == null) return false;
            playerUi.windowManager.Close(RebirthCompanionUiService.Group);
            drone.OpenStorageFromDialog(player);
            return true;
        }

        if (targetId.StartsWith("N:", StringComparison.Ordinal))
        {
            RebirthNpcStableId stableId;
            if (!RebirthNpcStableId.TryParse(targetId.Substring(2), out stableId)) return false;
            int entityId;
            EntityRebirthDogCompanion dog = RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out entityId)
                ? world.GetEntity(entityId) as EntityRebirthDogCompanion
                : null;
            string reason;
            bool opened = RebirthDogStorageService.OpenLocal(player, dog, out reason);
            if (!opened && !string.IsNullOrEmpty(reason))
                GameManager.ShowTooltip(player, reason, string.Empty, "ui_denied");
            return opened;
        }

        return false;
    }

    private static bool TryResolveOwnedDrone(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        string targetId,
        out EntityPlayer player,
        out EntityDrone drone)
    {
        player = world?.GetEntity(playerId) as EntityPlayer;
        drone = null;
        if (player == null || userId == null || string.IsNullOrEmpty(targetId) ||
            !targetId.StartsWith("D:", StringComparison.Ordinal)) return false;

        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId);
        if (persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(userId)) return false;

        int droneEntityId;
        if (!int.TryParse(targetId.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out droneEntityId))
            return false;
        drone = world.GetEntity(droneEntityId) as EntityDrone;
        return drone != null && !drone.IsDead() && drone.belongsPlayerId == playerId;
    }

    private static bool TryResolveAuthorizedTarget(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        string targetId,
        out EntityPlayer player,
        out RebirthCompanionListEntry target)
    {
        player = world?.GetEntity(playerId) as EntityPlayer;
        target = null;
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId);
        if (player == null || persistent?.PrimaryId == null || userId == null || !persistent.PrimaryId.Equals(userId)) return false;
        List<RebirthCompanionListEntry> allowed = GetNearbyOwned(world, player, userId, ManagementRadius);
        for (int i = 0; i < allowed.Count; i++)
        {
            if (string.Equals(allowed[i].Id, targetId, StringComparison.Ordinal))
            {
                target = allowed[i];
                return true;
            }
        }
        return false;
    }

    private static void TeleportToPlayerPosition(EntityAlive entity, EntityPlayer player)
    {
        if (entity == null || player == null) return;
        entity.SetAttackTarget(null, 0);
        entity.SetRevengeTarget(null);

        // Drones are intentionally airborne. Grounded NPC companions, however, must resolve
        // a supported standing point BEFORE SetPosition so Recall never leaves them suspended
        // at the player's transform waiting for gravity to settle them. If no supported point
        // can be resolved, do not perform an unsafe raw-transform teleport.
        Vector3 target = player.position;
        if (!(entity is EntityDrone) && !TryResolveGroundedCompanionRecall(entity, player, out target))
            return;

        RebirthCompanionCollisionService.PrepareForRecall(entity, player);
        entity.SetPosition(target, true);
        entity.position = target;
        try
        {
            entity.motion = Vector3.zero;
            if (entity.getNavigator() != null) entity.getNavigator().clearPath();
            if (entity.moveHelper != null) entity.moveHelper.Stop();
        }
        catch { }
    }

    private static bool TryResolveGroundedCompanionRecall(EntityAlive entity, EntityPlayer player, out Vector3 target)
    {
        target = player != null ? player.position : Vector3.zero;
        World world = entity != null ? entity.world : null;
        if (world == null || player == null) return false;

        Vector3 forward = player.transform != null ? player.transform.forward : Vector3.forward;
        Vector3 right = player.transform != null ? player.transform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        if (right.sqrMagnitude < 0.01f) right = Vector3.right;
        forward.Normalize();
        right.Normalize();

        // Keep exact player X/Z whenever it is standable; only correct Y before relocation.
        if (RebirthDogRuntimeService.TryFindSafeAtExactXZ(world, player.position, out target)) return true;

        Vector3[] offsets =
        {
            forward * 2.5f, right * 2.5f, -right * 2.5f, -forward * 2.5f,
            forward * 2.5f + right * 2.5f, forward * 2.5f - right * 2.5f,
            -forward * 2.5f + right * 2.5f, -forward * 2.5f - right * 2.5f
        };
        int start = (entity.entityId & 0x7fffffff) % offsets.Length;
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 desired = player.position + offsets[(start + i) % offsets.Length];
            if (!world.IsChunkAreaLoaded(desired)) continue;
            if (RebirthDogRuntimeService.TryFindSafeNear(world, desired, out target)) return true;
        }

        return false;
    }


    public static void ResetForWorldChange()
    {
        RebirthCompanionSnapshotService.Clear();
        RebirthDogCapacitySyncService.Reset();
        RebirthDroneLockToPlayerService.ResetForWorldChange();
        RebirthDroneQualitySpeed.ResetForWorldChange();
        RebirthDroneMotionTrace.ResetForWorldChange();
        RebirthCompanionBehaviorService.ResetForWorldChange();
        RebirthCompanionInventoryLockService.ResetForWorldChange();
        RebirthOwnedCompanionAttackPassthrough.ResetForWorldChange();
        RebirthDogNavigationMarkerService.ResetForWorldChange();
        RebirthCompanionOwnerTeleportService.ResetForWorldChange();
        RebirthCompanionColorService.ResetForWorldChange();
        RebirthCompanionCollisionService.ResetForWorldChange();
        RebirthCompanionCardStyleRuntime.ResetForWorldChange();
    }

    public static void Install()
    {
        Harmony harmony = new Harmony("rebirth.companions.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthCompanionQuietSyncPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneLockToPlayerUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneLockToPlayerUnloadPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneLockToPlayerRadialEntryPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneLockToPlayerRadialHandlerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneRenameActivationCommandsPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneRenameActivationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneQualitySpeedOnUpdateEntityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneQualitySpeedMovePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneQualitySpeedMoveTargetPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDroneMotionTraceLateUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDronePersistedNameAddedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDronePersistedNameSyncPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthOwnedCompanionRangedRaycastPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthOwnedCompanionMeleeRaycastPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthOwnedCompanionProjectileRaycastPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthCompanionOwnerExplicitTeleportPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthCompanionOwnerTeleportPlayerUpdatePatch));
    }
}

[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.ReadSyncData))]
internal static class RebirthCompanionQuietSyncPatch
{
    private static void Postfix(EntityDrone __instance, ushort __1)
    {
        // Stopping these groups on any quiet-mode sync is harmless when quiet is being
        // disabled and guarantees already-playing VO is cut when quiet is enabled.
        if (__instance != null && (__1 & 32) != 0)
            RebirthCompanionService.StopKnownDroneAudio(__instance);
    }
}

[Preserve]
public sealed class NetPackageRebirthCompanionSnapshotRequest : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private ulong requestEpoch;
    private ulong requestId;
    private string selectedId;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthCompanionSnapshotRequest Setup(int id, PersistentPlayerData persistent, ulong epoch, ulong request, string selection)
    {
        playerId = id; userId = persistent.PrimaryId; requestEpoch = epoch; requestId = request; selectedId = selection ?? string.Empty; return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader b = (BinaryReader)reader; playerId = b.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(b); requestEpoch = b.ReadUInt64(); requestId = b.ReadUInt64(); selectedId = RebirthSurvivorNetworkCodec.ReadBoundedString(b, 256);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); BinaryWriter b = (BinaryWriter)writer; b.Write(playerId); userId.ToStream(b); b.Write(requestEpoch); b.Write(requestId); RebirthSurvivorNetworkCodec.WriteString(b, selectedId, 256);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || requestEpoch == 0 || requestId == 0 || !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId);
        if (player == null || persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(userId)) return;
        RebirthCompanionSnapshot snapshot = RebirthCompanionSnapshotService.Build(world, player, userId, selectedId);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthCompanionSnapshotResponse>().Setup(requestEpoch, requestId, snapshot), _attachedToEntityId: playerId);
    }
    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthCompanionSnapshotResponse : NetPackage
{
    private ulong requestEpoch;
    private ulong requestId;
    private RebirthCompanionSnapshot snapshot;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthCompanionSnapshotResponse Setup(ulong epoch, ulong request, RebirthCompanionSnapshot value)
    { requestEpoch = epoch; requestId = request; snapshot = value ?? new RebirthCompanionSnapshot(); return this; }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader b = (BinaryReader)reader;
        requestEpoch = b.ReadUInt64(); requestId = b.ReadUInt64();
        snapshot = RebirthCompanionSnapshotCodec.Read(b);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); BinaryWriter b = (BinaryWriter)writer; b.Write(requestEpoch); b.Write(requestId);
        RebirthCompanionSnapshotCodec.Write(b, snapshot ?? new RebirthCompanionSnapshot());
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthCompanionSnapshotService.Receive(requestEpoch, requestId, snapshot);
    }
    public int GetLength() => 0;
}

internal static class RebirthCompanionSnapshotCodec
{
    private const int MaxCompanions = 128;
    private const int MaxCommands = 32;
    private const int MaxInventoryRows = 128;
    internal const int MaxText = 256;
    private const int MaxFeedback = 1024;

    public static void Write(BinaryWriter b, RebirthCompanionSnapshot value)
    {
        b.Write((byte)2);
        RebirthSurvivorNetworkCodec.WriteString(b, value.SelectedId, MaxText); RebirthSurvivorNetworkCodec.WriteString(b, value.Feedback, MaxFeedback);
        int companions = value.Companions != null ? value.Companions.Count : 0;
        if (companions > MaxCompanions) throw new InvalidDataException("Companion snapshot exceeds companion bound.");
        b.Write((byte)companions);
        for (int i = 0; i < companions; i++) WriteCompanion(b, value.Companions[i]);
        int commands = value.Commands != null ? value.Commands.Count : 0;
        if (commands > MaxCommands) throw new InvalidDataException("Companion snapshot exceeds command bound.");
        b.Write((byte)commands);
        for (int i = 0; i < commands; i++)
        {
            RebirthCompanionCommandEntry c = value.Commands[i] ?? new RebirthCompanionCommandEntry();
            b.Write((byte)c.Command); RebirthSurvivorNetworkCodec.WriteString(b, c.Text, MaxText); RebirthSurvivorNetworkCodec.WriteString(b, c.Icon, MaxText); RebirthSurvivorNetworkCodec.WriteString(b, c.IconAtlas ?? "UIAtlas", MaxText); b.Write(c.Enabled);
        }
        int inventory = value.Inventory != null ? value.Inventory.Count : 0;
        if (inventory > MaxInventoryRows) throw new InvalidDataException("Companion snapshot exceeds inventory bound.");
        b.Write((byte)inventory);
        for (int i = 0; i < inventory; i++)
        {
            RebirthCompanionInventoryEntry row = value.Inventory[i];
            RebirthSurvivorNetworkCodec.WriteString(b, row.RowKey, MaxText); b.Write(row.SlotIndex); b.Write(row.ItemType); RebirthSurvivorNetworkCodec.WriteString(b, row.ItemKey, MaxText);
            b.Write(row.Count); b.Write(row.Locked); b.Write(row.CanUse);
        }
    }

    public static RebirthCompanionSnapshot Read(BinaryReader b)
    {
        byte schema = b.ReadByte();
        if (schema != 2) throw new InvalidDataException("Unsupported companion snapshot schema.");
        RebirthCompanionSnapshot value = new RebirthCompanionSnapshot
        {
            SelectedId = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText),
            Feedback = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxFeedback)
        };
        int companions = b.ReadByte();
        if (companions > MaxCompanions) throw new InvalidDataException("Companion snapshot exceeds companion bound.");
        for (int i = 0; i < companions; i++) value.Companions.Add(ReadCompanion(b));
        int commands = b.ReadByte();
        if (commands > MaxCommands) throw new InvalidDataException("Companion snapshot exceeds command bound.");
        for (int i = 0; i < commands; i++) value.Commands.Add(new RebirthCompanionCommandEntry
        {
            Command = (RebirthCompanionCommand)b.ReadByte(), Text = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), Icon = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), IconAtlas = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), Enabled = b.ReadBoolean()
        });
        int inventory = b.ReadByte();
        if (inventory > MaxInventoryRows) throw new InvalidDataException("Companion snapshot exceeds inventory bound.");
        for (int i = 0; i < inventory; i++) value.Inventory.Add(new RebirthCompanionInventoryEntry
        {
            RowKey = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), SlotIndex = b.ReadInt32(), ItemType = b.ReadInt32(), ItemKey = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText),
            Count = b.ReadInt32(), Locked = b.ReadBoolean(), CanUse = b.ReadBoolean()
        });
        return value;
    }

    private static void WriteCompanion(BinaryWriter b, RebirthCompanionListEntry x)
    {
        x = x ?? new RebirthCompanionListEntry();
        RebirthSurvivorNetworkCodec.WriteString(b, x.Id, MaxText); b.Write((byte)x.Kind); b.Write(x.EntityId); RebirthSurvivorNetworkCodec.WriteString(b, x.StableId, MaxText);
        RebirthSurvivorNetworkCodec.WriteString(b, x.Name, MaxText); RebirthSurvivorNetworkCodec.WriteString(b, x.Type, MaxText); RebirthSurvivorNetworkCodec.WriteString(b, x.Status, MaxText); RebirthSurvivorNetworkCodec.WriteString(b, x.Icon, MaxText);
        b.Write(x.Distance); b.Write(x.DirectionAngle);
        b.Write(x.WorldPosition.x); b.Write(x.WorldPosition.y); b.Write(x.WorldPosition.z);
        b.Write(x.Health); b.Write(x.MaxHealth); b.Write(x.StorageUsed); b.Write(x.StorageTotal);
        RebirthSurvivorNetworkCodec.WriteString(b, x.Profile, MaxText); RebirthSurvivorNetworkCodec.WriteString(b, x.Order, MaxText); b.Write(x.DroneLockedToPlayer); b.Write(x.DroneAccessLocked); b.Write(x.DroneQuiet);
        b.Write(x.DroneLightAttached); b.Write(x.DroneLightOn); b.Write(x.DroneHealAttached); b.Write(x.DroneHealingAllies); b.Write(x.DroneQuality);
        b.Write((byte)x.BehaviorMode); b.Write(x.AttackStopped); b.Write(x.IsDog); b.Write(x.DogIsTamedWild); RebirthSurvivorNetworkCodec.WriteString(b, x.DogSpeciesCategory, MaxText); b.Write(x.DogAnimalCapacityCost); RebirthSurvivorNetworkCodec.WriteString(b, x.DogBreed, MaxText);
        b.Write(x.DogLevel); b.Write(x.DogKills); b.Write(x.DogTraining); b.Write(x.DogBond); b.Write(x.DogLearnedCommandCount); b.Write(x.DogLegacyCommandGrandfathered);
        b.Write(x.DogKnowsOwnerPositionStay); b.Write(x.DogKnowsGuardArea); b.Write(x.DogKnowsHunting); b.Write((byte)x.DogLifecycle);
        b.Write(x.IsBoundUndead); RebirthSurvivorNetworkCodec.WriteString(b, x.UndeadTier, MaxText); b.Write(x.UndeadCapacityCost); b.Write(x.UndeadConditioning); b.Write(x.UndeadTraining); b.Write(x.UndeadBindingStability); RebirthSurvivorNetworkCodec.WriteString(b, x.UndeadLifecycle, MaxText); b.Write(x.UndeadSummoned);
    }

    private static RebirthCompanionListEntry ReadCompanion(BinaryReader b)
    {
        return new RebirthCompanionListEntry
        {
            Id = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), Kind = (RebirthCompanionTargetKind)b.ReadByte(), EntityId = b.ReadInt32(), StableId = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText),
            Name = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), Type = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), Status = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), Icon = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), Distance = b.ReadSingle(),
            DirectionAngle = b.ReadSingle(), WorldPosition = new Vector3(b.ReadSingle(), b.ReadSingle(), b.ReadSingle()),
            Health = b.ReadInt32(), MaxHealth = b.ReadInt32(), StorageUsed = b.ReadInt32(), StorageTotal = b.ReadInt32(),
            Profile = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), Order = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), DroneLockedToPlayer = b.ReadBoolean(), DroneAccessLocked = b.ReadBoolean(), DroneQuiet = b.ReadBoolean(),
            DroneLightAttached = b.ReadBoolean(), DroneLightOn = b.ReadBoolean(), DroneHealAttached = b.ReadBoolean(),
            DroneHealingAllies = b.ReadBoolean(), DroneQuality = b.ReadInt32(), BehaviorMode = (RebirthCompanionBehaviorMode)b.ReadByte(), AttackStopped = b.ReadBoolean(),
            IsDog = b.ReadBoolean(), DogIsTamedWild = b.ReadBoolean(), DogSpeciesCategory = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), DogAnimalCapacityCost = b.ReadInt32(), DogBreed = RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), DogLevel = b.ReadInt32(), DogKills = b.ReadInt32(),
            DogTraining = b.ReadSingle(), DogBond = b.ReadSingle(), DogLearnedCommandCount = b.ReadInt32(), DogLegacyCommandGrandfathered = b.ReadBoolean(),
            DogKnowsOwnerPositionStay = b.ReadBoolean(), DogKnowsGuardArea = b.ReadBoolean(), DogKnowsHunting = b.ReadBoolean(),
            DogLifecycle = (RebirthDogLifecycleKind)b.ReadByte(), IsBoundUndead=b.ReadBoolean(), UndeadTier=RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), UndeadCapacityCost=b.ReadInt32(),
            UndeadConditioning=b.ReadSingle(), UndeadTraining=b.ReadSingle(), UndeadBindingStability=b.ReadSingle(), UndeadLifecycle=RebirthSurvivorNetworkCodec.ReadBoundedString(b, MaxText), UndeadSummoned=b.ReadBoolean()
        };
    }
}

[Preserve]
public sealed class NetPackageRebirthCompanionCommand : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private string targetId;
    private RebirthCompanionCommand command;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthCompanionCommand Setup(int id, PersistentPlayerData persistent, string target, RebirthCompanionCommand value)
    {
        playerId = id; userId = persistent.PrimaryId; targetId = target ?? string.Empty; command = value; return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader b = (BinaryReader)reader; playerId = b.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(b); targetId = RebirthSurvivorNetworkCodec.ReadBoundedString(b, RebirthCompanionSnapshotCodec.MaxText); command = (RebirthCompanionCommand)b.ReadByte();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); BinaryWriter b = (BinaryWriter)writer; b.Write(playerId); userId.ToStream(b); RebirthSurvivorNetworkCodec.WriteString(b, targetId, RebirthCompanionSnapshotCodec.MaxText); b.Write((byte)command);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world != null && !world.IsRemote() && ValidEntityIdForSender(playerId) && ValidUserIdForSender(userId))
            RebirthCompanionService.Process(world, playerId, userId, targetId, command);
    }
    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthCompanionInventoryAction : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private string targetId;
    private RebirthCompanionInventoryAction action;
    private int slotIndex;
    private int itemType;
    private string itemKey;
    private int quantity;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthCompanionInventoryAction Setup(int id, PersistentPlayerData persistent, string target,
        RebirthCompanionInventoryAction value, int slot, int type, string key, int count)
    {
        playerId = id; userId = persistent.PrimaryId; targetId = target ?? string.Empty; action = value;
        slotIndex = slot; itemType = type; itemKey = key ?? string.Empty; quantity = count; return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader b = (BinaryReader)reader; playerId = b.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(b);
        targetId = RebirthSurvivorNetworkCodec.ReadBoundedString(b, RebirthCompanionSnapshotCodec.MaxText); action = (RebirthCompanionInventoryAction)b.ReadByte(); slotIndex = b.ReadInt32(); itemType = b.ReadInt32(); itemKey = RebirthSurvivorNetworkCodec.ReadBoundedString(b, RebirthCompanionSnapshotCodec.MaxText); quantity = b.ReadInt32();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); BinaryWriter b = (BinaryWriter)writer; b.Write(playerId); userId.ToStream(b); RebirthSurvivorNetworkCodec.WriteString(b, targetId, RebirthCompanionSnapshotCodec.MaxText);
        b.Write((byte)action); b.Write(slotIndex); b.Write(itemType); RebirthSurvivorNetworkCodec.WriteString(b, itemKey, RebirthCompanionSnapshotCodec.MaxText); b.Write(quantity);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        // Transfers require the logistics bag snapshot/receipt protocol, not this fire-and-forget packet.
        if (action == RebirthCompanionInventoryAction.TransferToPlayer) return;
        if (world != null && !world.IsRemote() && ValidEntityIdForSender(playerId) && ValidUserIdForSender(userId))
            RebirthCompanionService.ProcessInventoryAction(world, playerId, userId, targetId, action, slotIndex, itemType, itemKey, quantity);
    }
    public int GetLength() => 0;
}
