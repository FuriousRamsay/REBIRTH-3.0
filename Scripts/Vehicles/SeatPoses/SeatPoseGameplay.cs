using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rebirth.Vehicles.Authoring
{
    internal static class SeatPoseGameplay
    {
        internal static readonly string PublishedDirectory = Path.Combine(Application.persistentDataPath, "REBIRTH", "SeatPoseProfiles", "Published");
        static readonly Dictionary<string, SeatPoseProfile> Cache = new Dictionary<string, SeatPoseProfile>(StringComparer.Ordinal);
        internal static int Revision { get; private set; }
        internal static Func<EntityAlive, bool> IsAuthoring;
        internal static Action<EntityAlive> BeforeAttachmentChange;
        internal static void Reload() { Cache.Clear(); Revision++; }
        internal static SeatPoseProfile Find(EntityAlive actor, EntityVehicle owner, int slot, string rig)
        {
            // Local experiments are single-player only. A listen host's native
            // base pose can replicate, so it must not use an unpublished-to-peers
            // local override. Multiplayer uses identical shipped configuration.
            bool local = actor is EntityPlayerLocal && SingletonMonoBehaviour<ConnectionManager>.Instance != null &&
                SingletonMonoBehaviour<ConnectionManager>.Instance.IsSinglePlayer;
            string key = SeatPosePublishedStore.Key(owner.vehicle.GetName(), slot, rig);
            string cacheKey = (local ? "local:" : "shipped:") + key;
            if (Cache.TryGetValue(cacheKey, out var cached)) return cached;
            SeatPoseProfile profile = null;
            try
            {
                if (local) profile = SeatPosePublishedStore.Load(PublishedDirectory, owner.vehicle.GetName(), slot, rig);
                if (profile == null)
                {
                    string root = Path.GetDirectoryName(typeof(SeatPoseGameplay).Assembly.Location);
                    profile = SeatPosePublishedStore.Load(Path.Combine(root, "Config", "VehicleSeatPoses"), owner.vehicle.GetName(), slot, rig);
                }
            }
            catch (Exception error) { UnityEngine.Debug.LogWarning("[REBIRTH Seat Profile] Rejected " + key + ": " + error.Message); }
            Cache[cacheKey] = profile; return profile;
        }
        internal static void Refresh(EntityAlive actor)
        {
            if (GameManager.IsDedicatedServer || !actor || !ThreadManager.IsMainThread()) return;
            var current = actor.GetComponent<SeatPoseGameplayBody>();
            var owner = actor.AttachedToEntity as EntityVehicle;
            if (!owner || actor.IsDead()) { if (current) current.Release(); return; }
            if (!current || current.Released) current = actor.gameObject.AddComponent<SeatPoseGameplayBody>();
            current.Actor = actor;
        }
        internal static bool Owns(EntityAlive actor) => actor && actor.GetComponent<SeatPoseGameplayBody>()?.Applied == true;
    }

    [DefaultExecutionOrder(19000)]
    internal sealed class SeatPoseGameplayBody : MonoBehaviour
    {
        internal EntityAlive Actor;
        internal bool Released { get; private set; }
        internal bool Applied => profiles.Count > 0;
        internal string Describe() => Applied
            ? "APPLIED vehicle=" + owner.vehicle.GetName() + " seat=" + slot + " basePose=" + profiles[0].basePose + " actualPose=" + Actor.GetVehicleAnimation() + " bodies=" + profiles.Count
            : "NATIVE_FALLBACK";
        readonly List<SeatPoseRig> rigs = new List<SeatPoseRig>();
        readonly List<SeatPoseBones> bones = new List<SeatPoseBones>();
        readonly List<SeatPoseProfile> profiles = new List<SeatPoseProfile>();
        Animator[] animators = new Animator[0];
        EntityVehicle owner;
        Transform model;
        Vector3 position;
        Quaternion rotation;
        int slot = -1, pose, revision = -1;
        float nextBind;
        bool applyingPose;
        readonly List<Behaviour> overlays = new List<Behaviour>();
        readonly List<bool> overlayStates = new List<bool>();

        void Update()
        {
            if (!Actor || Actor.IsDead() || GameManager.Instance?.World != Actor.world || !(Actor.AttachedToEntity is EntityVehicle vehicle)) { Release(); return; }
            if (SeatPoseGameplay.IsAuthoring?.Invoke(Actor) == true) { Stop(); nextBind = 0; return; }
            if (owner != vehicle || slot != vehicle.FindAttachSlot(Actor) || model != Actor.ModelTransform ||
                revision != SeatPoseGameplay.Revision || animators.Any(a => !a || !a.isActiveAndEnabled))
            { Stop(); nextBind = 0; }
            foreach (var b in bones) b.RemoveOffsets();
            if (Applied && Actor.GetVehicleAnimation() != profiles[0].basePose)
                Actor.SetVehiclePoseMode(profiles[0].basePose);
            if (Applied) ApplyAnchor(); // Before native animation and rig evaluation.
            if (Time.unscaledTime < nextBind) return;
            nextBind = Time.unscaledTime + .5f;
            var avatar = Actor.emodel?.avatarController as AvatarMultiBodyController;
            var active = avatar?.BodyAnimators.Select(b => b.Animator).Where(a => a && a.isActiveAndEnabled).Distinct().ToArray() ?? new Animator[0];
            if (active.Length == animators.Length && active.SequenceEqual(animators) && revision == SeatPoseGameplay.Revision) return;
            Stop();
            owner = vehicle; slot = vehicle.FindAttachSlot(Actor); model = Actor.ModelTransform; revision = SeatPoseGameplay.Revision; animators = active;
            if (slot < 0 || !model || !vehicle.ModelTransform || active.Length == 0) return;
            try
            {
                var selected = active.Select(a => a.avatar ? SeatPoseGameplay.Find(Actor, vehicle, slot, a.avatar.name) : null).ToArray();
                if (selected.Any(p => p == null)) return; // Never partially pose a layered player body.
                if (selected.Any(p => p.basePose != selected[0].basePose || p.anchorPosition != selected[0].anchorPosition || p.anchorRotation != selected[0].anchorRotation))
                    throw new InvalidDataException("Body layers disagree on seat anchor or native base pose.");
                position = model.localPosition; rotation = model.localRotation; pose = Actor.GetVehicleAnimation();
                // Stop competing legacy camper graphs before the native base pose.
                foreach (var overlay in Actor.GetComponents<Behaviour>().Where(b => b && b.GetType().FullName == "Rebirth.Vehicles.Camper.RebirthCamperBedVisual"))
                { overlays.Add(overlay); overlayStates.Add(overlay.enabled); overlay.enabled = false; }
                for (int i = 0; i < active.Length; i++)
                { rigs.Add(new SeatPoseRig(active[i], vehicle.ModelTransform)); bones.Add(new SeatPoseBones(active[i].transform)); profiles.Add(selected[i]); }
                applyingPose = true;
                try { Actor.SetVehiclePoseMode(selected[0].basePose); }
                finally { applyingPose = false; }
                ApplyAnchor();
                for (int i = 0; i < rigs.Count; i++) rigs[i].Apply(profiles[i], true);
            }
            catch (Exception error)
            {
                Stop(); animators = active; revision = SeatPoseGameplay.Revision;
                // Retry on a real body/seat change or explicit publication, not a
                // repeating half-second failure loop for an incompatible profile.
                UnityEngine.Debug.LogWarning("[REBIRTH Seat Profile] " + error.Message);
            }
        }
        void LateUpdate()
        {
            if (!Applied || !Actor || Actor.IsDead() || !owner || !owner.ModelTransform || Actor.AttachedToEntity != owner || owner.FindAttachSlot(Actor) != slot || !model) return;
            try
            {
                for (int i = 0; i < bones.Count; i++) bones[i].ApplyOffsets(profiles[i], false); // immutable, validated at bind
            }
            catch (Exception error)
            {
                var failedBodies = animators;
                Stop(); animators = failedBodies; revision = SeatPoseGameplay.Revision;
                UnityEngine.Debug.LogWarning("[REBIRTH Seat Profile] " + error.Message);
            }
        }
        void ApplyAnchor()
        {
            var p = profiles[0];
            model.SetPositionAndRotation(owner.ModelTransform.TransformPoint(p.anchorPosition), owner.ModelTransform.rotation * Quaternion.Euler(p.anchorRotation));
        }
        internal void Stop()
        {
            bool hadPose = Applied;
            foreach (var b in bones) Cleanup(b.Dispose);
            foreach (var r in rigs) Cleanup(r.Dispose);
            bones.Clear(); rigs.Clear(); profiles.Clear();
            if (hadPose && Actor && !Actor.IsDead() && GameManager.Instance?.World == Actor.world && Actor.AttachedToEntity == owner && owner && owner.FindAttachSlot(Actor) == slot && model == Actor.ModelTransform)
            {
                model.localPosition = position; model.localRotation = rotation;
                if (!applyingPose) Cleanup(() => Actor.SetVehiclePoseMode(pose));
            }
            for (int i = 0; i < overlays.Count; i++) if (overlays[i]) overlays[i].enabled = overlayStates[i];
            overlays.Clear(); overlayStates.Clear();
            animators = new Animator[0]; revision = -1;
        }
        internal void Release() { if (Released) return; Released = true; Stop(); enabled = false; Destroy(this); }
        static void Cleanup(Action action) { try { action(); } catch (Exception e) { UnityEngine.Debug.LogWarning("[REBIRTH Seat Profile cleanup] " + e.Message); } }
        void OnDisable() => Stop();
        void OnDestroy() => Stop();
    }

    [HarmonyPatch]
    internal static class SeatPoseGameplayPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(EntityAlive), "AttachToEntity", new[] { typeof(Entity), typeof(int) });
            yield return AccessTools.Method(typeof(EntityAlive), "SetVehicleAnimation", new[] { typeof(int), typeof(int) });
            yield return AccessTools.Method(typeof(EntityAlive), "Detach", Type.EmptyTypes);
        }
        static void Postfix(EntityAlive __instance) => SeatPoseGameplay.Refresh(__instance);
        static void Prefix(EntityAlive __instance, MethodBase __originalMethod)
        {
            if (GameManager.IsDedicatedServer || !__instance || !ThreadManager.IsMainThread()) return;
            if (__originalMethod.Name != "Detach" && __originalMethod.Name != "AttachToEntity") return;
            // Release owned offsets before native detach resets animation/IK, not
            // a frame later after inverse deltas would affect the unseated pose.
            try { SeatPoseGameplay.BeforeAttachmentChange?.Invoke(__instance); }
            catch (Exception error) { UnityEngine.Debug.LogWarning("[REBIRTH Seat Profile authoring cleanup] " + error.Message); }
            var runtime = __instance.GetComponent<SeatPoseGameplayBody>();
            if (runtime)
                try { runtime.Release(); }
                catch (Exception error) { UnityEngine.Debug.LogWarning("[REBIRTH Seat Profile detach cleanup] " + error.Message); }
        }
    }
}
