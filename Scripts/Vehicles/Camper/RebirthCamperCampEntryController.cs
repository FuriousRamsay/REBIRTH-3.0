using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Native FixedMove rewrites stepOffset every physics tick. Limit its source only
// during that call, without changing the player's normal controller settings.
internal static class RebirthCamperCampEntryController
{
    internal const float CampStepOffset = .1f;
    private static bool installed;
    private static readonly HashSet<EntityVehicle> vehicles = new HashSet<EntityVehicle>();
    private static readonly ConditionalWeakTable<EntityVehicle, Binding> bindings = new ConditionalWeakTable<EntityVehicle, Binding>();
    private sealed class Zone { internal Collider Collider; internal Bounds Local; }
    private sealed class Binding
    {
        internal Transform Root, Model, Shell;
        internal readonly List<Zone> Zones = new List<Zone>();
    }
    internal struct Movement
    {
        internal bool Active;
        internal CharacterController Controller;
        internal float StepHeight, StepOffset;
    }
    internal static void Install()
    {
        if (installed) return;
        var harmony = new Harmony("rebirth.camper.camp.entry.v1");
        harmony.CreateClassProcessor(typeof(RebirthCamperCampEntryVehicleTick)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperCampEntryVehicleUnload)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperCampEntryMovement)).Patch();
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
        installed = true;
    }
    internal static void Track(EntityVehicle vehicle)
    {
        if (ThreadManager.IsMainThread() && RebirthCamperRadial.IsCamper(vehicle)) vehicles.Add(vehicle);
    }
    internal static void Forget(EntityVehicle vehicle)
    { vehicles.Remove(vehicle); bindings.Remove(vehicle); }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data) => vehicles.Clear();
    private static Binding Resolve(EntityVehicle vehicle)
    {
        if (vehicle.RootTransform == null || vehicle.ModelTransform == null) return null;
        if (bindings.TryGetValue(vehicle, out var found)
            && (found.Root != vehicle.RootTransform || found.Model != vehicle.ModelTransform))
        { bindings.Remove(vehicle); found = null; }
        if (found != null) return found;
        found = new Binding { Root = vehicle.RootTransform, Model = vehicle.ModelTransform };
        foreach (var transform in found.Root.GetComponentsInChildren<Transform>(true))
            if (transform.name == "REBIRTHCampWalkInColliders") { found.Shell = transform; break; }
        if (found.Shell == null) return null;
        foreach (var collider in found.Shell.GetComponentsInChildren<Collider>(true))
        {
            if (collider.name != "Camp floor" && collider.name != "Rear entry floor"
                && collider.name != "Camp floor ramp" && collider.name != "Camp stair continuous collision") continue;
            Bounds local;
            if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
            else continue;
            found.Zones.Add(new Zone { Collider = collider, Local = local });
        }
        // Retry partial model loads. Legacy models without the authored geometry
        // get no player change; do not substitute a broad vehicle-distance test.
        if (found.Zones.Count != 4) return null;
        bindings.Add(vehicle, found);
        return found;
    }
    private static bool NearFloor(Binding binding, Vector3 feet, float radius)
    {
        if (binding.Shell == null || !binding.Shell.gameObject.activeInHierarchy) return false;
        foreach (var zone in binding.Zones)
        {
            var collider = zone.Collider;
            if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
            // Transform the actual controller feet into each authored collider's
            // space, so rotated campers do not expand the zone into nearby roads.
            var point = collider.transform.InverseTransformPoint(feet);
            var scale = collider.transform.lossyScale;
            float x = (radius + .2f) / Mathf.Max(.001f, Mathf.Abs(scale.x));
            float z = (radius + .2f) / Mathf.Max(.001f, Mathf.Abs(scale.z));
            float y = Mathf.Max(.001f, Mathf.Abs(scale.y));
            var min = zone.Local.min; var max = zone.Local.max;
            if (point.x >= min.x - x && point.x <= max.x + x
                && point.z >= min.z - z && point.z <= max.z + z
                && point.y >= min.y - .4f / y && point.y <= max.y + 1.25f / y) return true;
        }
        return false;
    }
    internal static Movement Begin(vp_FPController controller)
    {
        var player = controller.localPlayer;
        if (!ThreadManager.IsMainThread() || player == null || player.IsDead() || controller.MotorFreeFly
            || player.world == null || !ReferenceEquals(GameManager.Instance?.World, player.world)
            || !ReferenceEquals(player.world.GetEntity(player.entityId), player)) return default;
        var native = controller.CharacterController;
        if (native == null || !native.enabled || controller.m_StepHeight <= CampStepOffset) return default;
        var feet = native.transform.TransformPoint(native.center - Vector3.up * (native.height * .5f));
        foreach (var vehicle in vehicles)
        {
            if (vehicle == null || vehicle.IsDead() || !ReferenceEquals(vehicle.world, player.world)
                || !ReferenceEquals(player.world.GetEntity(vehicle.entityId), vehicle)
                || RebirthCamperRadial.IsDriver(vehicle, player) || (vehicle.position - player.position).sqrMagnitude > 225f
                || !RebirthCamperEnergyRuntime.TryObservePresentation(vehicle, out var mode, out _, out _, out _)
                || mode != RebirthCamperEnergyMode.Camp) continue;
            var binding = Resolve(vehicle);
            if (binding == null || !NearFloor(binding, feet, native.radius)) continue;
            var movement = new Movement { Active = true, Controller = native, StepHeight = controller.m_StepHeight, StepOffset = native.stepOffset };
            controller.m_StepHeight = CampStepOffset;
            return movement;
        }
        return default;
    }
    internal static void End(vp_FPController controller, Movement movement)
    {
        if (!movement.Active) return;
        controller.m_StepHeight = movement.StepHeight;
        if (movement.Controller != null) movement.Controller.stepOffset = movement.StepOffset;
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.Update))]
internal static class RebirthCamperCampEntryVehicleTick
{ private static void Postfix(EntityVehicle __instance) => RebirthCamperCampEntryController.Track(__instance); }

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.OnEntityUnload))]
internal static class RebirthCamperCampEntryVehicleUnload
{ private static void Prefix(EntityVehicle __instance) => RebirthCamperCampEntryController.Forget(__instance); }

[HarmonyPatch(typeof(vp_FPController), nameof(vp_FPController.FixedMove))]
internal static class RebirthCamperCampEntryMovement
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(vp_FPController __instance, out RebirthCamperCampEntryController.Movement __state)
        => __state = RebirthCamperCampEntryController.Begin(__instance);
    private static void Finalizer(vp_FPController __instance, RebirthCamperCampEntryController.Movement __state)
        => RebirthCamperCampEntryController.End(__instance, __state);
}
