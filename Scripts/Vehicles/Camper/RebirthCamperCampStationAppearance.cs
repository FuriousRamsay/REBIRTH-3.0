using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Cosmetic presentation follows the existing authoritative/synced energy mode.
// Guardrails are a separate root and are never changed by this controller.
internal static class RebirthCamperCampStationAppearance
{
    private sealed class View
    {
        internal Transform Model, Door;
        internal GameObject Roof, Interior, WalkIn, Power, Stairs;
        internal Renderer[] FadeRenderers;
        internal GameObject[] FadeGroups;
        internal Collider[] Targets;
        internal readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        internal RebirthCamperEnergyMode Mode;
        internal float Opacity, TargetOpacity, LastUpdate;
        internal bool Initialized, DoorOpen;
        internal Quaternion ClosedDoor, OpenDoor;
        internal BoxCollider DoorCollider;
        internal readonly System.Collections.Generic.List<BoxCollider> TravelHulls = new System.Collections.Generic.List<BoxCollider>();
        internal readonly System.Collections.Generic.List<bool> HullDefaults = new System.Collections.Generic.List<bool>();
        internal bool Camp, AppliedCamp;
        internal float NextObservation;
    }
    private static readonly ConditionalWeakTable<EntityVehicle, View> views = new ConditionalWeakTable<EntityVehicle, View>();
    internal static void Update(EntityVehicle vehicle)
    {
        if (!ThreadManager.IsMainThread() || !RebirthCamperRadial.IsCamper(vehicle) || vehicle.ModelTransform == null || vehicle.RootTransform == null) return;
        View view;
        if (views.TryGetValue(vehicle, out view) && view.Model != vehicle.ModelTransform) { views.Remove(vehicle); view = null; }
        if (view == null)
        {
            view = new View { Model = vehicle.ModelTransform };
            foreach (var t in vehicle.RootTransform.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "REBIRTHCampRoofStations") view.Roof = t.gameObject;
                if (t.name == "REBIRTHCampInteriorStations") view.Interior = t.gameObject;
                if (t.name == "REBIRTHCampPowerEquipment") view.Power = t.gameObject;
                if (t.name == "REBIRTHCampStairVisuals") view.Stairs = t.gameObject;
                if (t.name == "REBIRTHCampWalkInColliders") view.WalkIn = t.gameObject;
                if (t.name == "REBIRTHRearDoorPivot")
                {
                    view.Door = t; view.ClosedDoor = t.localRotation;
                    view.DoorCollider = t.GetComponent<BoxCollider>();
                }
                if (t.name == "REBIRTHRearDoorOpenPose") view.OpenDoor = t.localRotation;
                if (t.name == "CabHull" || t.name == "CamperHull" || t == vehicle.ModelTransform)
                {
                    var hull = t.GetComponent<BoxCollider>();
                    if (hull != null) { view.TravelHulls.Add(hull); view.HullDefaults.Add(hull.enabled); }
                }
            }
            // The model may still be loading. Do not cache a missing binding.
            if (view.Roof == null && view.Interior == null) return;
            var renderers = new System.Collections.Generic.List<Renderer>();
            view.FadeGroups = new[] { view.Roof, view.Interior, view.Power, view.Stairs };
            foreach (var group in view.FadeGroups)
                if (group != null) renderers.AddRange(group.GetComponentsInChildren<Renderer>(true));
            view.FadeRenderers = renderers.ToArray();
            var targets = new System.Collections.Generic.List<Collider>();
            foreach (var c in vehicle.RootTransform.GetComponentsInChildren<Collider>(true))
                if (c.name.StartsWith("REBIRTHInteraction_", System.StringComparison.Ordinal)) targets.Add(c);
            view.Targets = targets.ToArray();
            view.LastUpdate = Time.realtimeSinceStartup;
            views.Add(vehicle, view);
        }
        if (Time.realtimeSinceStartup >= view.NextObservation)
        {
            bool observed = RebirthCamperEnergyRuntime.TryObservePresentation(vehicle, out var mode, out _, out _, out var remaining);
            if (!observed) mode = RebirthCamperEnergyMode.Travel;
            if (!view.Initialized || mode != view.Mode) view.DoorOpen = mode == RebirthCamperEnergyMode.Camp;
            view.Mode = mode;
            view.Camp = mode == RebirthCamperEnergyMode.Camp;
            var config = RebirthCamperEnergyConfiguration.Current;
            view.TargetOpacity = Visibility(mode, remaining, config.DeploySeconds, config.PackSeconds);
            if (!view.Initialized) { view.Opacity = view.TargetOpacity; view.Initialized = true; }
            view.NextObservation = Time.realtimeSinceStartup + .15f;
        }
        bool show = view.Camp;
        float now = Time.realtimeSinceStartup;
        // Smooth replicated one-second timer steps without extrapolating a paused timer.
        float duration = view.Mode == RebirthCamperEnergyMode.Deploying ?
            RebirthCamperEnergyConfiguration.Current.DeploySeconds : RebirthCamperEnergyConfiguration.Current.PackSeconds;
        view.Opacity = show ? 1f : view.Mode == RebirthCamperEnergyMode.Travel ? 0f :
            Mathf.MoveTowards(view.Opacity, view.TargetOpacity, Mathf.Max(0, now - view.LastUpdate) / Mathf.Max(.001f, duration));
        view.LastUpdate = now;
        bool visible = view.Mode != RebirthCamperEnergyMode.Travel;
        foreach (var group in view.FadeGroups)
            if (group != null && group.activeSelf != visible) group.SetActive(visible);
        foreach (var renderer in view.FadeRenderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(view.Block);
            view.Block.SetFloat("_CampVisibility", view.Opacity);
            renderer.SetPropertyBlock(view.Block);
        }
        foreach (var target in view.Targets) if (target != null) target.enabled = show;
        if (view.WalkIn != null && view.WalkIn.activeSelf != show) view.WalkIn.SetActive(show);
        if (view.Door != null)
        {
            var rotation = show && view.DoorOpen ? view.OpenDoor : view.ClosedDoor;
            if (Quaternion.Angle(view.Door.localRotation, rotation) > .01f) view.Door.localRotation = rotation;
            if (view.DoorCollider != null && view.DoorCollider.enabled != show) view.DoorCollider.enabled = show;
        }
        // Replace the solid cabin boxes only when the authored Camp shell exists.
        // Wheels and native Rigidbody simulation are never changed here.
        if (view.WalkIn != null && (show || view.AppliedCamp))
            for (int i = 0; i < view.TravelHulls.Count; i++)
            {
                var hull = view.TravelHulls[i];
                bool enabled = !show && view.HullDefaults[i];
                if (hull != null && hull.enabled != enabled) hull.enabled = enabled;
            }
        view.AppliedCamp = show;
    }

    internal static float Visibility(RebirthCamperEnergyMode mode, float remaining, float deploy, float pack)
    {
        if (mode == RebirthCamperEnergyMode.Camp) return 1;
        if (mode == RebirthCamperEnergyMode.Deploying) return Mathf.Clamp01(1 - remaining / Mathf.Max(.001f, deploy));
        if (mode == RebirthCamperEnergyMode.Packing) return Mathf.Clamp01(remaining / Mathf.Max(.001f, pack));
        return 0;
    }

    // Presentation only. Caller must validate authority, distance and Camp state and replicate the choice.
    // Reapply on remote clients and after load; manual door state is deliberately not persisted here.
    internal static bool SetDoorOpen(EntityVehicle vehicle, bool open)
    {
        Update(vehicle);
        if (!views.TryGetValue(vehicle, out var view) || !view.Camp || view.Door == null) return false;
        view.DoorOpen = open;
        Update(vehicle);
        return true;
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.Update))]
internal static class RebirthCamperCampStationAppearanceTick
{
    private static void Postfix(EntityVehicle __instance) => RebirthCamperCampStationAppearance.Update(__instance);
}

// Native vehicle physics can refresh collider states; enforce Camp shell after it.
[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.PhysicsFixedUpdate))]
internal static class RebirthCamperCampStationAppearancePhysics
{
    private static void Postfix(EntityVehicle __instance) => RebirthCamperCampStationAppearance.Update(__instance);
}
