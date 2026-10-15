using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Local rider position is owner-written by the native game. Attachment admission
// and detach replication remain native; this adds no remote-player warp packet.
internal static class RebirthCamperCampSeatReturn
{
    internal sealed class Point
    {
        internal EntityVehicle Vehicle;
        internal World World;
        internal Transform Root, Model;
        internal Vector3 LocalPosition;
        internal Quaternion LocalRotation;
    }
    private sealed class Notice { internal EntityVehicle Vehicle; internal int Slot = -1; }
    private static ConditionalWeakTable<EntityPlayerLocal, Point> points = new ConditionalWeakTable<EntityPlayerLocal, Point>();
    private static ConditionalWeakTable<EntityPlayerLocal, Notice> notices = new ConditionalWeakTable<EntityPlayerLocal, Notice>();
    private static bool installed;
    internal static void Install()
    {
        if (installed) return;
        var harmony = new Harmony("rebirth.camper.camp.seat.return.v1");
        harmony.CreateClassProcessor(typeof(RebirthCamperCampSeatCapture)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperCampSeatExit)).Patch();
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
        installed = true;
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {
        points = new ConditionalWeakTable<EntityPlayerLocal, Point>();
        notices = new ConditionalWeakTable<EntityPlayerLocal, Notice>();
    }
    private static bool Current(EntityVehicle vehicle, EntityPlayerLocal player)
        => ThreadManager.IsMainThread() && player != null && !player.IsDead() && !player.isEntityRemote
            && player.world != null && ReferenceEquals(GameManager.Instance?.World, player.world)
            && ReferenceEquals(player.world.GetEntity(player.entityId), player)
            && RebirthCamperRadial.IsCamper(vehicle) && !vehicle.IsDead()
            && ReferenceEquals(vehicle.world, player.world)
            && ReferenceEquals(player.world.GetEntity(vehicle.entityId), vehicle)
            && vehicle.RootTransform != null && vehicle.ModelTransform != null
            && RebirthCamperEnergyRuntime.TryObserveMode(vehicle, out var mode, out _, out _)
            && mode == RebirthCamperEnergyMode.Camp;
    private static bool OwnPlayer(Collider collider, EntityPlayerLocal player)
        => ReferenceEquals(collider.GetComponentInParent<EntityPlayer>(), player)
            || player.RootTransform != null && collider.transform.IsChildOf(player.RootTransform);
    private static void Trace(EntityPlayerLocal player, string stage, string detail)
    {
        if (player?.Buffs != null && player.Buffs.GetCustomVar("$rebirthCamperSeatReturnDebug") > 0)
            Log.Out("[REBIRTH Camper Seat Return] player=" + player.entityId + " stage=" + stage + " " + detail);
    }
    private static int CollisionMask(CharacterController controller)
    {
        int mask = 0, layer = controller.gameObject.layer;
        for (int other = 0; other < 32; other++)
            if (!Physics.GetIgnoreLayerCollision(layer, other)) mask |= 1 << other;
        return mask;
    }
    private static bool InteriorFloor(EntityVehicle vehicle, EntityPlayerLocal player, Vector3 feet)
    {
        // Both cabin floor levels and their connecting ramp are inside the door.
        // The external stair ramp/landing does not qualify.
        var controller = player.vp_FPController?.CharacterController;
        if (controller == null) return false;
        var hits = Physics.RaycastAll(feet + Vector3.up * .12f, Vector3.down, .35f,
            CollisionMask(controller), QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Collider collider = null;
        foreach (var hit in hits)
            if (hit.collider != null && !OwnPlayer(hit.collider, player)
                && !Physics.GetIgnoreCollision(controller, hit.collider)) { collider = hit.collider; break; }
        if (collider == null || (collider.name != "Camp floor" && collider.name != "Rear entry floor"
            && collider.name != "Camp floor ramp")
            || !collider.transform.IsChildOf(vehicle.RootTransform))
        { Trace(player, "floor-rejected", "feet=" + feet.ToString("F3") + " hit=" + (collider == null ? "none" : collider.name)); return false; }
        for (var parent = collider.transform.parent; parent != null; parent = parent.parent)
            if (parent.name == "REBIRTHCampWalkInColliders") return true;
        return false;
    }
    internal static Point Capture(EntityPlayerLocal player, Entity other)
    {
        var vehicle = other as EntityVehicle;
        if (player.AttachedToEntity != null || !Current(vehicle, player))
        { if (RebirthCamperRadial.IsCamper(vehicle)) Trace(player, "capture-rejected", "attached=" + (player.AttachedToEntity != null) + " current=" + Current(vehicle, player)); return null; }
        // A previous boarding session must never confer an interior return on a
        // subsequent exterior boarding (including a failed attach).
        points.Remove(player);
        var controller = player.vp_FPController?.CharacterController;
        if (controller == null || !controller.enabled)
        { Trace(player, "capture-rejected", "controller unavailable/disabled"); return null; }
        var feet = controller.transform.TransformPoint(controller.center - Vector3.up * (controller.height * .5f));
        if (!InteriorFloor(vehicle, player, feet)) return null;
        var point = new Point { Vehicle = vehicle, World = player.world, Root = vehicle.RootTransform,
            Model = vehicle.ModelTransform,
            // The FP controller is the native local movement authority; Entity.position
            // is refreshed later by updateTransform and can lag the current physics step.
            LocalPosition = vehicle.RootTransform.InverseTransformPoint(player.vp_FPController.Transform.position),
            LocalRotation = Quaternion.Inverse(vehicle.RootTransform.rotation) * Quaternion.Euler(player.rotation) };
        Trace(player, "captured", "camper=" + vehicle.entityId + " local=" + point.LocalPosition.ToString("F3"));
        return point;
    }
    internal static void Attached(EntityPlayerLocal player, Entity other, int result, Point candidate)
    {
        if (result < 0 || candidate == null || !ReferenceEquals(player.AttachedToEntity, other)
            || !Current(candidate.Vehicle, player)) return;
        points.Remove(player); points.Add(player, candidate);
        Trace(player, "committed", "slot=" + candidate.Vehicle.FindAttachSlot(player));
    }
    internal static void Exit(EntityPlayerLocal player)
    {
        notices.Remove(player);
        if (!points.TryGetValue(player, out var point)) { Trace(player, "exit-native", "no committed interior point"); return; }
        points.Remove(player); // consume once even when native safety fallback is needed
        var vehicle = point.Vehicle;
        if (player.AttachedToEntity != null || !Current(vehicle, player)
            || !ReferenceEquals(point.World, player.world) || point.Root != vehicle.RootTransform
            || point.Model != vehicle.ModelTransform)
        { Trace(player, "exit-native", "world/camper/model/Camp/membership validation failed"); return; }
        var controller = player.vp_FPController?.CharacterController;
        if (controller == null || !controller.enabled)
        { Trace(player, "exit-native", "controller unavailable/disabled"); return; }
        var shifted = point.Root.TransformPoint(point.LocalPosition);
        var rotation = point.Root.rotation * point.LocalRotation;
        var offset = controller.transform.position - player.vp_FPController.Transform.position
            + controller.transform.TransformVector(controller.center);
        var center = shifted + offset;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(controller.transform.lossyScale.x), Mathf.Abs(controller.transform.lossyScale.z));
        float height = controller.height * Mathf.Abs(controller.transform.lossyScale.y);
        var feet = center - Vector3.up * (height * .5f);
        if (!InteriorFloor(vehicle, player, feet) || radius <= 0 || height < radius * 2) return;
        var lower = feet + Vector3.up * (radius + .035f);
        var upper = feet + Vector3.up * (height - radius);
        foreach (var collider in Physics.OverlapCapsule(lower, upper, Mathf.Max(.01f, radius - .025f),
            CollisionMask(controller), QueryTriggerInteraction.Ignore))
            if (collider != null && !OwnPlayer(collider, player) && !Physics.GetIgnoreCollision(controller, collider))
            { Trace(player, "exit-native", "capsule blocked=" + collider.name + " layer=" + collider.gameObject.layer + " feet=" + feet.ToString("F3")); return; }
        // Native Detach already restored parent/controller/camera/driving activity.
        player.SetPosition(shifted + Origin.position);
        player.SetRotation(rotation.eulerAngles);
        player.ResetLastTickPos(player.position);
        player.vp_FPController.Stop();
        Trace(player, "returned", "position=" + player.position.ToString("F3"));
    }
    internal static void NotifySeatChanged(EntityVehicle vehicle, EntityPlayer player, int slot)
    {
        var local = player as EntityPlayerLocal;
        if (local == null || !RebirthCamperRadial.IsCamper(vehicle) || !RebirthVehicleSeatSwitch.Member(vehicle, local)
            || slot != vehicle.FindAttachSlot(local)) return;
        var notice = notices.GetValue(local, ignored => new Notice());
        if (ReferenceEquals(notice.Vehicle, vehicle) && notice.Slot == slot) return;
        notice.Vehicle = vehicle; notice.Slot = slot;
        string key = "xuiRebirthCamperSeat" + slot;
        GameManager.ShowTooltip(local, string.Format(Localization.Get("xuiRebirthCamperSeatSelected"), Localization.Get(key)));
    }
}

[HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.AttachToEntity), new[] { typeof(Entity), typeof(int) })]
internal static class RebirthCamperCampSeatCapture
{
    private static void Prefix(EntityPlayerLocal __instance, Entity __0, out RebirthCamperCampSeatReturn.Point __state)
        => __state = RebirthCamperCampSeatReturn.Capture(__instance, __0);
    private static void Postfix(EntityPlayerLocal __instance, Entity __0, int __result, RebirthCamperCampSeatReturn.Point __state)
        => RebirthCamperCampSeatReturn.Attached(__instance, __0, __result, __state);
}

[HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Detach))]
internal static class RebirthCamperCampSeatExit
{
    private static void Postfix(EntityPlayerLocal __instance) => RebirthCamperCampSeatReturn.Exit(__instance);
}
