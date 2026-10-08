using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthHornDoorService
{
    private const float Radius = 20f;
    private const float OpenSeconds = 8f;
    private const float PruneSeconds = 30f;

    private sealed class DoorRegistration
    {
        public Vector3i Position;
        public TEFeatureDoor Feature;
    }

    private sealed class DoorLease
    {
        public DoorRegistration Registration;
        public float CloseAt;
    }

    private static readonly Dictionary<Vector3i, DoorRegistration> Doors =
        new Dictionary<Vector3i, DoorRegistration>();
    private static readonly Dictionary<Vector3i, DoorLease> Active =
        new Dictionary<Vector3i, DoorLease>();
    private static readonly List<Vector3i> Scratch = new List<Vector3i>(32);

    private static World activeWorld;
    private static float nextPrune;
    private static float nextActiveDeadline = float.PositiveInfinity;

    public static void Install(Harmony harmony)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthHornDoorAddedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthHornDoorManualActivationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthHornDoorPlayerUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthHornDoorServerUpdatePatch));
        TryInstallRemovalHook(harmony);
    }

    private static void TryInstallRemovalHook(Harmony harmony)
    {
        try
        {
            // Quiet lookups: AccessTools.* logs a warning on every miss.
            const System.Reflection.BindingFlags own = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            var target = typeof(TEFeatureDoor).GetMethod("OnRemoved", own, null, Type.EmptyTypes, null)
                ?? typeof(TEFeatureDoor).GetMethod("OnBlockRemoved", own, null, Type.EmptyTypes, null);
            if (target != null)
            {
                harmony.Patch(target, postfix: new HarmonyMethod(typeof(RebirthHornDoorRemovalPatch), nameof(RebirthHornDoorRemovalPatch.Postfix)));
                return;
            }

            // Game 3.2: TEFeatureDoor no longer has OnRemoved/OnBlockRemoved; removal goes through the inherited
            // TEFeatureAbs.OnRemove(World). Patch that and filter for doors.
            var removal = typeof(TEFeatureDoor).GetMethod("OnRemove", own, null, new[] { typeof(World) }, null)
                ?? typeof(TEFeatureAbs).GetMethod("OnRemove", own, null, new[] { typeof(World) }, null);
            if (removal == null)
                return;

            harmony.Patch(removal, postfix: new HarmonyMethod(typeof(RebirthHornDoorRemovalPatch), nameof(RebirthHornDoorRemovalPatch.PostfixAny)));
        }
        catch (Exception ex)
        {
#if DEBUG
            Log.Warning("[REBIRTH HornDoors] optional removal hook unavailable: " + ex.Message);
#endif
        }
    }

    private static bool EnsureWorld(World world)
    {
        if (world == null || world.IsRemote())
            return false;

        if (ReferenceEquals(activeWorld, world))
            return true;

        activeWorld = world;
        Doors.Clear();
        Active.Clear();
        Scratch.Clear();
        nextPrune = 0f;
        nextActiveDeadline = float.PositiveInfinity;
        return true;
    }

    internal static void OnDoorAdded(TEFeatureDoor feature, Vector3i blockPosition, BlockValue blockValue)
    {
        if (feature == null || blockValue.ischild)
            return;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (!EnsureWorld(world))
            return;

        Doors[blockPosition] = new DoorRegistration
        {
            Position = blockPosition,
            Feature = feature
        };

        // A replacement at the same coordinate is a new generation. Never let a
        // lease acquired by the old tile entity close the replacement door.
        DoorLease lease;
        if (Active.TryGetValue(blockPosition, out lease)
            && (lease == null || !ReferenceEquals(lease.Registration.Feature, feature)))
        {
            Active.Remove(blockPosition);
            RecomputeNextDeadline();
        }
    }

    internal static void OnDoorRemoved(TEFeatureDoor feature)
    {
        if (feature == null || activeWorld == null)
            return;

        Vector3i position = ResolvePosition(feature);
        DoorRegistration registration;
        if (Doors.TryGetValue(position, out registration)
            && registration != null
            && ReferenceEquals(registration.Feature, feature))
        {
            Doors.Remove(position);
        }

        DoorLease lease;
        if (Active.TryGetValue(position, out lease)
            && lease != null
            && ReferenceEquals(lease.Registration.Feature, feature))
        {
            Active.Remove(position);
            RecomputeNextDeadline();
        }
    }

    internal static void OnManualDoorStateChanged(
        TEFeatureDoor feature,
        Vector3i blockPosition,
        bool wasOpen)
    {
        if (feature == null || feature.IsOpen() == wasOpen)
            return;

        // Only a successful non-horn state change transfers ownership back to the
        // player. A rejected/failed activation leaves the horn lease intact.
        DoorLease lease;
        if (Active.TryGetValue(blockPosition, out lease)
            && lease != null
            && ReferenceEquals(lease.Registration.Feature, feature))
        {
            Active.Remove(blockPosition);
            RecomputeNextDeadline();
        }
    }

    internal static void PlayerUpdate(PlayerMoveController controller)
    {
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;
        EntityPlayerLocal player = controller == null ? null : controller.entityPlayerLocal;
        if (player == null || !(player.AttachedToEntity is EntityVehicle) || !HornPressed(player)) return;
        RebirthHornActivatedDoorsMode mode = RebirthSandboxOptionManager.Current.HornActivatedDoors;
        if (mode == RebirthHornActivatedDoorsMode.None) return;
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        if (persistent?.PrimaryId == null) return;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null) return;
        if (connection.IsServer) ProcessRequest(GameManager.Instance.World, player.entityId, persistent.PrimaryId);
        else connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthHornDoorRequest>().Setup(player.entityId, persistent.PrimaryId));
    }

    private static bool HornPressed(EntityPlayerLocal player)
    {
        return player.playerInput != null &&
               player.playerInput.VehicleActions != null &&
               player.playerInput.VehicleActions.HonkHorn != null &&
               player.playerInput.VehicleActions.HonkHorn.WasPressed;
    }

    public static void ProcessRequest(World world, int playerId, PlatformUserIdentifierAbs userId)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || !EnsureWorld(world)) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId);
        if (player == null || !(player.AttachedToEntity is EntityVehicle) || persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(userId)) return;
        RebirthHornActivatedDoorsMode mode = RebirthSandboxOptionManager.Current.HornActivatedDoors;
        if (mode == RebirthHornActivatedDoorsMode.None) return;

        DoorRegistration best = null;
        float bestSq = Radius * Radius;
        Scratch.Clear();

        foreach (KeyValuePair<Vector3i, DoorRegistration> pair in new List<KeyValuePair<Vector3i, DoorRegistration>>(Doors))
        {
            Vector3i position = pair.Key;

            // The registry can be large. Reject distant positions before any tile
            // entity/block lookup; only nearby candidates pay native lookup cost.
            float sq = (position.ToVector3() - player.position).sqrMagnitude;
            if (sq > bestSq)
                continue;

            DoorRegistration registration = pair.Value;
            if (!IsCurrentGeneration(world, registration))
            {
                Scratch.Add(position);
                continue;
            }

            // An already-open door needs no horn action and must not hide a closed candidate.
            if (registration.Feature.IsOpen())
                continue;

            if (!IsEligible(world, player, position, mode))
                continue;

            best = registration;
            bestSq = sq;
        }

        RemoveStaleRegistrations();
        if (best == null)
            return;

        // Do not claim/auto-close a door that was already open for another reason.
        if (!SetOpenIfChanged(best, true))
            return;

        float deadline = Time.time + OpenSeconds;
        Active[best.Position] = new DoorLease
        {
            Registration = best,
            CloseAt = deadline
        };
        if (deadline < nextActiveDeadline)
            nextActiveDeadline = deadline;
    }

    private static bool IsEligible(World world, EntityPlayer player, Vector3i position, RebirthHornActivatedDoorsMode mode)
    {
        if (mode == RebirthHornActivatedDoorsMode.Trader) return IsTraderPosition(world, position);
        TileEntityComposite tile = world.GetTileEntity(position) as TileEntityComposite;
        if (tile == null) return false;
        TEFeatureLockable lockable = tile.GetFeature<TEFeatureLockable>();
        if (lockable == null || !lockable.IsLocked()) return true;
        string reason;
        return RebirthSecureAccessPolicy.CanAccess(player, tile, RebirthSecureAccessPurpose.HornDoorActivation, out reason);
    }

    private static bool IsTraderPosition(World world, Vector3i position)
    {
        if (world == null || world.TraderAreas == null) return false;
        for (int i = 0; i < world.TraderAreas.Count; i++)
        {
            TraderArea area = world.TraderAreas[i];
            if (area == null) continue;
            Vector3i min = area.Position;
            Vector3i max = area.Position + area.PrefabSize;
            if (position.x >= min.x && position.x < max.x &&
                position.y >= min.y && position.y < max.y &&
                position.z >= min.z && position.z < max.z)
                return true;
        }
        return false;
    }

    internal static void ServerUpdate()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (connection == null || !connection.IsServer || !EnsureWorld(world)) return;

        float now = Time.time;
        if (Active.Count > 0 && now >= nextActiveDeadline)
        {
            Scratch.Clear();
            foreach (KeyValuePair<Vector3i, DoorLease> pair in new List<KeyValuePair<Vector3i, DoorLease>>(Active))
            {
                DoorLease lease = pair.Value;
                if (lease == null || !IsCurrentGeneration(world, lease.Registration))
                {
                    Scratch.Add(pair.Key);
                    continue;
                }
                if (now < lease.CloseAt)
                    continue;

                SetOpenIfChanged(lease.Registration, false);
                Scratch.Add(pair.Key);
            }

            for (int i = 0; i < Scratch.Count; i++)
                Active.Remove(Scratch[i]);
            RecomputeNextDeadline();
        }

        if (now < nextPrune)
            return;

        nextPrune = now + PruneSeconds;
        Scratch.Clear();
        foreach (KeyValuePair<Vector3i, DoorRegistration> pair in new List<KeyValuePair<Vector3i, DoorRegistration>>(Doors))
            if (!IsCurrentGeneration(world, pair.Value))
                Scratch.Add(pair.Key);
        RemoveStaleRegistrations();
    }

    private static void RemoveStaleRegistrations()
    {
        if (Scratch.Count == 0)
            return;

        bool deadlineChanged = false;
        for (int i = 0; i < Scratch.Count; i++)
        {
            Vector3i position = Scratch[i];
            Doors.Remove(position);
            if (Active.Remove(position))
                deadlineChanged = true;
        }
        Scratch.Clear();
        if (deadlineChanged)
            RecomputeNextDeadline();
    }

    private static bool IsCurrentGeneration(World world, DoorRegistration registration)
    {
        if (registration == null || registration.Feature == null || !ReferenceEquals(world, activeWorld))
            return false;

        TEFeatureDoor current;
        return TryGetDoor(world, registration.Position, out current)
            && ReferenceEquals(current, registration.Feature);
    }

    private static bool TryGetDoor(World world, Vector3i position, out TEFeatureDoor door)
    {
        door = null;
        TileEntityComposite composite = world != null ? world.GetTileEntity(position) as TileEntityComposite : null;
        return composite != null && composite.TryGetSelfOrFeature<TEFeatureDoor>(out door) && door != null;
    }

    private static bool SetOpenIfChanged(DoorRegistration registration, bool open)
    {
        if (registration == null || registration.Feature == null || registration.Feature.IsOpen() == open)
            return false;
        registration.Feature.SetOpen(open, true);
        return registration.Feature.IsOpen() == open;
    }

    private static void RecomputeNextDeadline()
    {
        float next = float.PositiveInfinity;
        foreach (DoorLease lease in Active.Values)
            if (lease != null && lease.CloseAt < next)
                next = lease.CloseAt;
        nextActiveDeadline = next;
    }

    private static Vector3i ResolvePosition(TEFeatureDoor feature)
    {
        if (feature == null)
            return Vector3i.zero;
        TileEntityComposite parent = feature.Parent;
        return parent != null ? parent.ToWorldPos() : feature.ToWorldPos();
    }
}

[HarmonyPatch(typeof(TEFeatureDoor), nameof(TEFeatureDoor.OnAdded))]
internal static class RebirthHornDoorAddedPatch
{
    private static void Postfix(TEFeatureDoor __instance, Vector3i _blockPos, BlockValue _blockValue)
    {
        RebirthHornDoorService.OnDoorAdded(__instance, _blockPos, _blockValue);
    }
}

[HarmonyPatch(typeof(TEFeatureDoor), nameof(TEFeatureDoor.OnBlockActivated))]
internal static class RebirthHornDoorManualActivationPatch
{
    private static void Prefix(TEFeatureDoor __instance, ref bool __state)
    {
        __state = __instance != null && __instance.IsOpen();
    }

    private static void Postfix(TEFeatureDoor __instance, Vector3i __2, bool __state)
    {
        RebirthHornDoorService.OnManualDoorStateChanged(__instance, __2, __state);
    }
}

internal static class RebirthHornDoorRemovalPatch
{
    public static void Postfix(TEFeatureDoor __instance)
    {
        RebirthHornDoorService.OnDoorRemoved(__instance);
    }

    public static void PostfixAny(TEFeatureAbs __instance)
    {
        TEFeatureDoor door = __instance as TEFeatureDoor;
        if (door != null)
            RebirthHornDoorService.OnDoorRemoved(door);
    }
}

[HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
internal static class RebirthHornDoorPlayerUpdatePatch
{
    private static void Postfix(PlayerMoveController __instance)
    {
        RebirthHornDoorService.PlayerUpdate(__instance);
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
internal static class RebirthHornDoorServerUpdatePatch
{
    private static void Postfix()
    {
        RebirthHornDoorService.ServerUpdate();
    }
}

[Preserve]
public sealed class NetPackageRebirthHornDoorRequest : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthHornDoorRequest Setup(int entityId, PlatformUserIdentifierAbs id) { playerId = entityId; userId = id; return this; }
    public override void read(PooledBinaryReader reader) { BinaryReader binary = (BinaryReader)reader; playerId = binary.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(binary); }
    public override void write(PooledBinaryWriter writer) { base.write(writer); BinaryWriter binary = (BinaryWriter)writer; binary.Write(playerId); userId.ToStream(binary); }
    public override void ProcessPackage(World world, GameManager callbacks) { if (world != null && !world.IsRemote() && ValidEntityIdForSender(playerId) && ValidUserIdForSender(userId)) RebirthHornDoorService.ProcessRequest(world, playerId, userId); }
    public int GetLength() => 32;
}
