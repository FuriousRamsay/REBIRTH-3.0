using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Reuse the native exclusive vehicle lock, including its server grant, cargo
// snapshot and keep-open channel. This is not a remote item-transfer transport.
public static class RebirthCamperModuleInteraction
{
    private sealed class Lease { internal EntityPlayerLocal Player; }
    private static readonly ConditionalWeakTable<EntityVehicle, Lease> leases = new ConditionalWeakTable<EntityVehicle, Lease>();
    private static readonly AccessTools.FieldRef<EntityVehicle, string> command
        = AccessTools.FieldRefAccess<EntityVehicle, string>("transientLockCommand");

    internal static void Request(EntityVehicle vehicle, EntityPlayerLocal player, string action)
    {
        RebirthCamperTrace.Write("lock.request",vehicle,action);
        if (!ThreadManager.IsMainThread() || !RebirthCamperRadial.Allowed(vehicle, player)
            || RebirthCamperRadial.IsDriver(vehicle,player)) return;
        if(vehicle.world.IsRemote() && action==RebirthCamperRadial.Craft
            && (!RebirthCamperRemoteStationClient.IsReady || !RebirthCamperRemoteStationTransport.IsRegistered))return;
        var ui = LocalPlayerUI.GetUIForPlayer(player);
        if (ui == null || player.inventory.IsHoldingItemActionRunning() || ui.xui.IsUsingItemActionEntryUse) return;
        // One native lock is owned by an interaction, never by this menu request
        // as well as a different native cargo or service window.
        if (leases.TryGetValue(vehicle, out _) || LockManager.Instance.IsLockedByLocalPlayer(vehicle, 0)) return;
        command(vehicle) = action;
        LockManager.Instance.LockRequestLocal(vehicle, 0);
    }

    internal static void Granted(EntityVehicle vehicle, EntityPlayerLocal player, string action)
    {
        RebirthCamperTrace.Write("lock.granted",vehicle,action);
        if (!ThreadManager.IsMainThread() || !LockManager.Instance.IsLockedByLocalPlayer(vehicle, 0))
            throw new InvalidOperationException("Native vehicle lock grant required.");
        if (leases.TryGetValue(vehicle, out _)) throw new InvalidOperationException("Camper interaction already owns a lease.");
        leases.Add(vehicle, new Lease { Player = player });
        try
        {
            if (!Current(vehicle, player) || !RebirthCamperRadial.Allowed(vehicle, player))
                throw new InvalidOperationException("Camper access expired before the native grant.");
            LocalPlayerUI.GetUIForPlayer(player).xui.Vehicle.CurrentVehicle = vehicle;
            if (!RebirthCamperPhysicalInteraction.TryOpenGranted(vehicle, player, action))
                RebirthCamperRadial.OpenSession(vehicle, player, action);
        }
        catch { Release(vehicle, player); throw; }
    }

    internal static bool Current(EntityVehicle vehicle, EntityPlayerLocal player)
        => vehicle != null && player != null && leases.TryGetValue(vehicle, out var lease)
            && ReferenceEquals(lease.Player, player) && !RebirthCamperRadial.IsDriver(vehicle,player)
            && LockManager.Instance.IsLockedByLocalPlayer(vehicle, 0);

    internal static Func<bool> CaptureCurrent(EntityVehicle vehicle, EntityPlayerLocal player)
    {
        if (!Current(vehicle, player) || !leases.TryGetValue(vehicle, out var expected))
            throw new InvalidOperationException("Current native camper lease required.");
        return () => leases.TryGetValue(vehicle, out var live) && ReferenceEquals(live, expected) && Current(vehicle, player);
    }

    internal static Action CaptureRelease(EntityVehicle vehicle, EntityPlayerLocal player)
    {
        if (!Current(vehicle, player) || !leases.TryGetValue(vehicle, out var expected))
            throw new InvalidOperationException("Current native camper lease required.");
        // Delayed OnClose from an old window cannot release a new session on
        // this same vehicle, even when it belongs to the same local player.
        return () => { if (leases.TryGetValue(vehicle, out var live) && ReferenceEquals(live, expected)) Release(vehicle, player); };
    }

    internal static void Release(EntityVehicle vehicle, EntityPlayerLocal player)
    {
        RebirthCamperTrace.Write("lock.release",vehicle);
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Native interaction must close on the game thread.");
        if (!leases.TryGetValue(vehicle, out var lease) || !ReferenceEquals(lease.Player, player)) return;
        leases.Remove(vehicle); // callback reentry cannot release twice
        // A native disconnect/close may already have revoked this exact lock.
        // Never send UnlockRequestLocal against a different current interaction.
        if (LockManager.Instance.IsLockedByLocalPlayer(vehicle, 0)) vehicle.StopUIInteraction();
    }
}

[HarmonyPatch(typeof(EntityVehicle), "StartInteraction")]
public static class RebirthCamperModuleInteractionGrant
{
    private static bool Prefix(EntityVehicle __instance, ReadOnlySpan<char> __0)
    {
        if (!RebirthCamperRadial.IsCommand(__0)) return true;
        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        RebirthCamperModuleInteraction.Granted(__instance, player, __0.ToString());
        return false;
    }
}
