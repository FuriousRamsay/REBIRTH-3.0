using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Direct 3.1 implementation of Rebirth's legacy 2.6 "Lock Drone to Player" feature.
///
/// This state is intentionally independent from EntityDrone.IsLocked(), which is the
/// vanilla multiplayer access/security lock. No runtime reflection is used here.
/// The project-level hard exclusion against globally patching Entity.OnUpdatePosition is
/// also preserved: the tether is applied only from EntityDrone.OnUpdateEntity.
/// </summary>
public static class RebirthDroneLockToPlayerService
{
    public const int RadialCommandIndex = 10010;

    private sealed class LockState
    {
        public int OwnerEntityId;
        public bool InPosition;
    }

    private const float FollowDistance = 0.5f;
    private const float BeginFollowDistance = 0.1f;
    private const float CatchupSpeed = 30f;
    private const float HoldSpeed = 100f;

    // Unity entity/network callbacks are handled on the game thread. Keeping this map
    // lock-free avoids adding synchronization overhead to the per-entity update path.
    private static readonly Dictionary<int, LockState> States = new Dictionary<int, LockState>();

    public static bool IsLockedToPlayer(int droneEntityId)
    {
        return droneEntityId >= 0 && States.ContainsKey(droneEntityId);
    }

    // Typed overload used by the drone speed/trace systems. Keep the entity-to-id
    // conversion here so callers do not need to guess or duplicate the lock key.
    public static bool IsLockedToPlayer(EntityDrone drone)
    {
        return drone != null && IsLockedToPlayer(drone.entityId);
    }

    public static bool SetState(EntityDrone drone, EntityPlayer owner, bool enabled)
    {
        if (drone == null || owner == null || drone.IsDead() || drone.belongsPlayerId != owner.entityId)
            return false;

        if (!enabled)
        {
            States.Remove(drone.entityId);
            return true;
        }

        LockState state;
        if (!States.TryGetValue(drone.entityId, out state))
        {
            state = new LockState();
            States[drone.entityId] = state;
        }

        state.OwnerEntityId = owner.entityId;
        state.InPosition = false;
        return true;
    }

    public static void RemoveDrone(int droneEntityId)
    {
        if (droneEntityId >= 0) States.Remove(droneEntityId);
    }

    public static void RemoveOwner(int ownerEntityId)
    {
        if (ownerEntityId < 0 || States.Count == 0) return;
        List<int> remove = null;
        foreach (KeyValuePair<int, LockState> pair in States)
        {
            if (pair.Value != null && pair.Value.OwnerEntityId == ownerEntityId)
            {
                if (remove == null) remove = new List<int>();
                remove.Add(pair.Key);
            }
        }
        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++) States.Remove(remove[i]);
    }

    public static void ResetForWorldChange()
    {
        States.Clear();
    }

    public static EntityDrone FindOwnedLoadedDrone(EntityPlayer player)
    {
        if (player == null || GameManager.Instance?.World == null) return null;
        // 3.1 EntityAlive exposes GetOwnedEntities(int _classId). Filter directly to
        // the native junk-drone entity class, matching DroneManager/ConsoleCmdJunkDrone.
        List<OwnedEntityData> owned = player.GetOwnedEntities(EntityClass.junkDroneClass);
        if (owned == null) return null;
        for (int i = 0; i < owned.Count; i++)
        {
            OwnedEntityData data = owned[i];
            if (data == null) continue;

            EntityDrone drone = GameManager.Instance.World.GetEntity(data.Id) as EntityDrone;
            if (drone != null && !drone.IsDead() && drone.belongsPlayerId == player.entityId)
                return drone;
        }
        return null;
    }


    internal static void ApplyAutomaticRecall(EntityDrone drone)
    {
        if (drone == null || drone.IsDead() ||
            !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer ||
            drone.OrderState != EntityDrone.Orders.Follow) return;

        EntityPlayer owner = drone.Owner as EntityPlayer;
        if (owner == null || owner.IsDead() || owner.AttachedToEntity != null || drone.belongsPlayerId != owner.entityId) return;

        float max = RebirthCompanionService.AutomaticFollowRecallDistance;
        if ((drone.position - owner.position).sqrMagnitude <= max * max) return;

        Vector3 target = owner.position;
        drone.SetPosition(target, true);
        drone.position = target;
        try
        {
            if (drone.getNavigator() != null) drone.getNavigator().clearPath();
            if (drone.moveHelper != null) drone.moveHelper.Stop();
        }
        catch { }
    }

    internal static void ApplyLockedPosition(EntityDrone drone)
    {
        if (drone == null) return;
        LockState state;
        if (!States.TryGetValue(drone.entityId, out state) || state == null) return;

        EntityPlayer owner = drone.Owner as EntityPlayer;
        if (owner == null || owner.IsDead() || owner.entityId != state.OwnerEntityId)
        {
            States.Remove(drone.entityId);
            return;
        }

        Vector3 forward = owner.GetForwardVector();
        if (forward.sqrMagnitude < 0.0001f) forward = owner.transform.forward;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 target = owner.getChestPosition() - forward * FollowDistance;
        float distance = Vector3.Distance(target, drone.position);
        float speed = distance < BeginFollowDistance || state.InPosition ? HoldSpeed : CatchupSpeed;
        Vector3 next = Vector3.Lerp(drone.position, target, Mathf.Clamp01(Time.deltaTime * speed));

        drone.transform.position = next - Origin.position;
        drone.SetPosition(next, true);
        drone.SetRotation(Quaternion.LookRotation(forward, Vector3.up).eulerAngles);
        state.InPosition = distance < BeginFollowDistance;
    }
}

[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.OnUpdateEntity))]
internal static class RebirthDroneLockToPlayerUpdatePatch
{
    private static void Postfix(EntityDrone __instance)
    {
        RebirthDroneLockToPlayerService.ApplyLockedPosition(__instance);
        RebirthDroneLockToPlayerService.ApplyAutomaticRecall(__instance);
    }
}

[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.OnEntityUnload))]
internal static class RebirthDroneLockToPlayerUnloadPatch
{
    private static void Prefix(EntityDrone __instance)
    {
        if (__instance != null) RebirthDroneLockToPlayerService.RemoveDrone(__instance.entityId);
    }
}

/// <summary>
/// Adds the Lock Drone to Player action to the normal TAB radial immediately after
/// the base chat entry, while SetupMenuData is still building the menu.  This matches
/// the legacy 2.6 placement pattern and lets base SetCommonData position all entries.
/// </summary>
[HarmonyPatch(typeof(XUiC_Radial), nameof(XUiC_Radial.CreateRadialEntry),
    new Type[] { typeof(int), typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool) })]
internal static class RebirthDroneLockToPlayerRadialEntryPatch
{
    private static void Postfix(XUiC_Radial __instance, int _commandIdx, string _icon, string _selectionText)
    {
        if (__instance?.xui?.playerUI == null || _commandIdx != 9 || _icon != "ui_game_symbol_chat") return;
        if (!string.Equals(_selectionText, Localization.Get("inpActChatName"), StringComparison.Ordinal)) return;

        EntityPlayerLocal player = __instance.xui.playerUI.entityPlayer;
        EntityDrone drone = RebirthDroneLockToPlayerService.FindOwnedLoadedDrone(player);
        if (drone == null) return;

        bool locked = RebirthDroneLockToPlayerService.IsLockedToPlayer(drone.entityId);
        __instance.CreateRadialEntry(
            RebirthDroneLockToPlayerService.RadialCommandIndex,
            "rb_container_lock",
            "RebirthUiIcons",
            string.Empty,
            Localization.Get(locked ? "xuiRebirthUnlockDroneFromPlayer" : "xuiRebirthLockDroneToPlayer"),
            false);
    }
}

/// <summary>
/// Wraps the public SetCommonData handler delegate for the main TAB radial. The base
/// private menu handler is already provided to SetCommonData as a delegate, so we can
/// intercept only our command and invoke that supplied delegate for every base command.
/// No private method lookup or reflective invocation is needed.
/// </summary>
[HarmonyPatch(typeof(XUiC_Radial), nameof(XUiC_Radial.SetCommonData))]
internal static class RebirthDroneLockToPlayerRadialHandlerPatch
{
    private static void Prefix(
        XUiC_Radial __instance,
        ref XUiC_Radial.CommandHandlerDelegate _commandHandlerFunc,
        XUiC_Radial.RadialContextAbs _context)
    {
        // SetupMenuData is the context-free radial. Entity/block/etc. radials carry a
        // typed context and must retain their own handlers untouched.
        if (__instance == null || _context != null || _commandHandlerFunc == null) return;

        XUiC_Radial.CommandHandlerDelegate baseHandler = _commandHandlerFunc;
        _commandHandlerFunc = delegate(
            XUiC_Radial sender,
            int commandIndex,
            XUiC_Radial.RadialContextAbs context)
        {
            if (commandIndex != RebirthDroneLockToPlayerService.RadialCommandIndex)
            {
                baseHandler(sender, commandIndex, context);
                return;
            }

            EntityPlayerLocal player = sender?.xui?.playerUI?.entityPlayer;
            EntityDrone drone = RebirthDroneLockToPlayerService.FindOwnedLoadedDrone(player);
            if (player == null || drone == null) return;

            bool locked = RebirthDroneLockToPlayerService.IsLockedToPlayer(drone.entityId);
            RebirthCompanionService.Request(
                "D:" + drone.entityId,
                locked ? RebirthCompanionCommand.UnlockFromPlayer : RebirthCompanionCommand.LockToPlayer);
        };
    }
}
