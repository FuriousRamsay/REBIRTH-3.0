using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// The authored trigger is a hit target, never permission. Callers separately
// validate ownership, character, revisions and station/energy custody.
internal static class RebirthCamperPhysicalTargets
{
    private const string Prefix = "REBIRTHInteraction_";
    private static readonly HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal)
    {
        "forge", "campfire", "workbench", "cement_mixer", "iron_oven", "circular_saw", "outfit_designer",
        "recycling_station", "shredder", "ammo_recycler", "gas_stovetop", "mortar_pestle", "toolbox",
        "chemistry_station", "distiller", "research_table", "battery_bank", "generator", "solar_bank", "rear_door", "sink"
    };
    private sealed class Bindings
    {
        internal Transform Model, Root;
        internal readonly Dictionary<string, BoxCollider> Targets = new Dictionary<string, BoxCollider>(StringComparer.Ordinal);
        internal bool Invalid;
    }
    private static readonly ConditionalWeakTable<EntityVehicle, Bindings> bindings = new ConditionalWeakTable<EntityVehicle, Bindings>();
    private static bool PlayerCurrent(EntityPlayer p) => ThreadManager.IsMainThread() && p?.world != null && !p.IsDead()
        && ReferenceEquals(GameManager.Instance?.World, p.world) && ReferenceEquals(p.world.GetEntity(p.entityId), p);
    private static bool Current(EntityVehicle v, EntityPlayer p) => PlayerCurrent(p) && v?.vehicle != null && !v.IsDead()
        && ReferenceEquals(v.world, p.world) && ReferenceEquals(v.world.GetEntity(v.entityId), v)
        && RebirthCamperRadial.IsCamper(v) && !RebirthCamperRadial.IsDriver(v, p)
        && RebirthCamperEnergyRuntime.TryObserveMode(v, out var mode, out _, out _) && mode == RebirthCamperEnergyMode.Camp;
    private static bool TryTarget(Collider c, out EntityVehicle v, out string id)
    {
        v = null; id = null;
        if (!(c is BoxCollider) || !c.enabled || !c.isTrigger || !c.gameObject.activeInHierarchy
            || !c.name.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        id = c.name.Substring(Prefix.Length);
        if (!ids.Contains(id)) { id = null; return false; }
        v = c.GetComponentInParent<EntityVehicle>();
        return v != null && RebirthCamperRadial.IsCamper(v);
    }
    private static BoxCollider Resolve(EntityVehicle v, string id)
    {
        // Fail closed when the dedicated server has no authored model/colliders.
        // Missing geometry must not become permission through a proximity fallback.
        if (v.RootTransform == null || v.ModelTransform == null) return null;
        if (bindings.TryGetValue(v, out var found) && (found.Root != v.RootTransform || found.Model != v.ModelTransform))
        { bindings.Remove(v); found = null; }
        if (found == null)
        {
            found = new Bindings { Root = v.RootTransform, Model = v.ModelTransform };
            foreach (var c in v.RootTransform.GetComponentsInChildren<BoxCollider>(true))
            {
                // Bind even before Camp enables targets; check activation at use.
                if (!c.isTrigger || !c.name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                string candidate = c.name.Substring(Prefix.Length);
                if (!ids.Contains(candidate) || !ReferenceEquals(c.GetComponentInParent<EntityVehicle>(), v)) continue;
                if (found.Targets.ContainsKey(candidate)) { found.Invalid = true; break; }
                found.Targets.Add(candidate, c);
            }
            // Retry incomplete model loading rather than caching missing targets.
            if (found.Targets.Count == ids.Count || found.Invalid) bindings.Add(v, found);
        }
        return !found.Invalid && found.Targets.TryGetValue(id, out var target) ? target : null;
    }
    // Native EntityFactory puts the actor component on GameObject while its
    // Physics collider may be a sibling under RootTransform. Parent-component
    // lookup alone therefore does not exclude all of the actor's own geometry.
    private static bool OwnPlayer(Collider c, EntityPlayer p) => p.RootTransform != null
        && (c.transform == p.RootTransform || c.transform.IsChildOf(p.RootTransform));
    private static bool Clear(Vector3 eye, Vector3 point, EntityPlayer p)
    {
        var direction = point - eye; float distance = direction.magnitude;
        if (distance < .001f) return true;
        foreach (var hit in Physics.RaycastAll(new Ray(eye, direction / distance), distance, Physics.AllLayers, QueryTriggerInteraction.Collide))
        {
            var c = hit.collider;
            // All solid geometry participates, including the camper's own hull,
            // closed door and world walls. Unrelated triggers cannot occlude.
            if (c == null || c.isTrigger || OwnPlayer(c, p)) continue;
            if (hit.distance < distance - .005f) return false;
        }
        return true;
    }
    internal static bool Validate(EntityVehicle v, EntityPlayer p, string targetId)
    {
        if (targetId == null || !ids.Contains(targetId) || !Current(v, p)) return false;
        var target = Resolve(v, targetId);
        if (target == null || !target.enabled || !target.isTrigger || !target.gameObject.activeInHierarchy) return false;
        // Use the actor's native eye, not gaze/camera, so an open station UI can
        // remain valid while the player looks elsewhere. Unity uses shifted space.
        Vector3 eye = p.position + Vector3.up * p.GetEyeHeight() - Origin.position;
        Vector3 point = target.ClosestPoint(eye);
        float reach = Constants.cCollectItemDistance;
        return reach > 0 && (point - eye).sqrMagnitude < reach * reach && Clear(eye, point, p);
    }
    internal static bool TryGetFocused(EntityPlayerLocal p, out EntityVehicle vehicle, out string targetId)
    {
        vehicle = null; targetId = null;
        if (!PlayerCurrent(p) || Constants.cCollectItemDistance <= 0) return false;
        Ray native = p.GetLookRay();
        // Installed EntityPlayerLocal.GetLookRay adds Origin.position to its
        // camera ray. Subtract exactly once, as native HandleInteraction does.
        var ray = new Ray(native.origin - Origin.position, native.direction.normalized);
        var hits = Physics.RaycastAll(ray, Constants.cCollectItemDistance, Physics.AllLayers, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        float solidDistance = float.PositiveInfinity;
        foreach (var hit in hits)
            if (hit.collider != null && !hit.collider.isTrigger && !OwnPlayer(hit.collider, p))
            { solidDistance = hit.distance; break; }
        foreach (var hit in hits)
        {
            var c = hit.collider;
            if (c == null || OwnPlayer(c, p)) continue;
            // Trigger faces fitted exactly to a door/model can coincide with its
            // solid face; do not let unspecified equal-hit ordering hide them.
            if (hit.distance > solidDistance + .005f) return false;
            if (TryTarget(c, out var candidate, out var id))
            {
                if (!Validate(candidate, p, id)) continue;
                vehicle = candidate; targetId = id; return true;
            }
        }
        return false;
    }
}
