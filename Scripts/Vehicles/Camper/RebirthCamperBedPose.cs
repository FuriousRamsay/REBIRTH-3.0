using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Rebirth.Vehicles.Camper
{
    public static class RebirthCamperBedPoseService
    {
        public sealed class Evidence
        {
            public int occupantId, camperId, nativeSlot = 4, bodyAnimatorCount, activeGraphs;
            public bool nativeMembership, clipHumanMotion, clipGenericMotion, nativeRigCompatible, controllerPreserved, nativeBedIKTargetsAbsent;
            public string clip, status, error;
        }
        public static Evidence Inspect(EntityAlive occupant)
        {
            var pose = occupant == null ? null : occupant.gameObject.GetComponent<RebirthCamperBedVisual>();
            return pose == null ? new Evidence { occupantId = occupant == null ? -1 : occupant.entityId,
                camperId = -1, status = "NOT_APPLIED", error = "No camper bed visual component." } : pose.Inspect();
        }
        internal static EntityVehicle BedOwner(EntityAlive occupant)
        {
            var owner = AuthoredPoseOwner(occupant);
            return owner != null && NativeSlot(occupant, owner) == 4 ? owner : null;
        }
        internal static int NativeSlot(EntityAlive occupant, EntityVehicle owner)
        {
            if (occupant == null || owner == null) return -1;
            for (int slot = 2; slot <= 5; slot++)
                if (owner.GetAttachMaxCount() > slot && owner.GetAttached(slot) == occupant) return slot;
            return -1;
        }
        internal static EntityVehicle AuthoredPoseOwner(EntityAlive occupant)
        {
            var owner = occupant?.AttachedToEntity as EntityVehicle;
            if (owner == null || occupant.IsDead() || owner.vehicle == null || owner.vehicle.GetName() != "rebirthCamper80s") return null;
            int slot = NativeSlot(occupant, owner);
            if (slot < 0) return null;
            // New cabin overlays are optional until their paired assets arrive.
            // An older bundle retains native seating without repeated warnings.
            if (slot != 4 && owner.ModelTransform?.Find("M/CabinPoseAssets/Seat" + slot) == null) return null;
            return owner;
        }
        internal static void Refresh(EntityAlive occupant)
        {
            if (GameManager.IsDedicatedServer || occupant == null || !ThreadManager.IsMainThread()) return;
            var pose = occupant.gameObject.GetComponent<RebirthCamperBedVisual>();
            var owner = AuthoredPoseOwner(occupant);
            if (owner == null)
            {
                if (pose != null) { pose.Stop(); UnityEngine.Object.Destroy(pose); }
                return;
            }
            if (pose == null) pose = occupant.gameObject.AddComponent<RebirthCamperBedVisual>();
            pose.Bind(occupant, owner);
        }
    }

    internal sealed class RebirthCamperBedVisual : MonoBehaviour
    {
        private sealed class Body
        {
            internal Animator Animator;
            internal RuntimeAnimatorController Controller;
            internal PlayableGraph Graph;
            internal int State;
            internal float Time;
            internal bool RootMotion;
        }
        private EntityAlive occupant;
        private EntityVehicle owner;
        private Transform model;
        private AnimationClip clip;
        private readonly List<Body> bodies = new List<Body>();
        private float nextBind;
        private string failure;
        private bool warned;
        private bool layoutValidated;
        private int nativeSlot = -1;

        internal void Bind(EntityAlive actor, EntityVehicle camper)
        {
            int selectedSlot = RebirthCamperBedPoseService.NativeSlot(actor, camper);
            if (occupant != actor || owner != camper || model != camper.ModelTransform || nativeSlot != selectedSlot)
            { Stop(); occupant = actor; owner = camper; model = camper.ModelTransform; nativeSlot = selectedSlot; warned = false; nextBind = 0; }
            if (Time.unscaledTime < nextBind) return;
            nextBind = Time.unscaledTime + .5f;
            try
            {
                string holderPath = nativeSlot == 4 ? "M/BedPoseAsset" : "M/CabinPoseAssets/Seat" + nativeSlot;
                string expectedClip = nativeSlot == 4 ? "rebirthCamperBedLying" : "rebirthCamperCabinSeat" + nativeSlot;
                var holder = model == null ? null : model.Find(holderPath);
                var asset = holder == null ? null : holder.GetComponent<Animator>();
                var clips = asset?.runtimeAnimatorController?.animationClips.Where(c => c != null && c.name == expectedClip).Distinct().ToArray();
                if (asset == null || asset.enabled || clips == null || clips.Length != 1 || clips[0].humanMotion || clips[0].legacy)
                    throw new InvalidOperationException("Authored native generic camper clip dependency missing/invalid for slot " + nativeSlot + "; pair the candidate asset with this runtime.");
                clip = clips[0];
                if (camper.vehicle.GetIKTargets(nativeSlot) != null)
                    throw new InvalidOperationException("Authored camper slot " + nativeSlot + " has conflicting native IK targets.");
                var avatar = actor.emodel?.avatarController as AvatarMultiBodyController;
                if (avatar == null) throw new InvalidOperationException("Player MultiBody controller unavailable.");
                var current = avatar.BodyAnimators.Select(b => b.Animator).Where(a => a != null && a.isActiveAndEnabled).Distinct().ToArray();
                if (current.Length == 0 || current.Any(a => a.avatar == null || !a.avatar.isValid || a.isHuman || a.runtimeAnimatorController == null))
                    throw new InvalidOperationException("All active player body animators must retain the native valid generic BaseRig avatar.");
                var layout = holder.Find("NativeRigLayout");
                if (layout == null) throw new InvalidOperationException("Native camper skeletal binding layout missing; rebuild the candidate.");
                foreach (var animator in current) ValidateLayout(layout, animator.transform, "");
                layoutValidated = true;
                foreach (var previous in bodies.ToArray())
                    if (!current.Contains(previous.Animator) || previous.Animator.runtimeAnimatorController != previous.Controller)
                    { Restore(previous); bodies.Remove(previous); }
                foreach (var animator in current)
                {
                    if (bodies.Any(b => b.Animator == animator && b.Graph.IsValid())) continue;
                    var native = animator.GetCurrentAnimatorStateInfo(0);
                    var body = new Body { Animator = animator, Controller = animator.runtimeAnimatorController,
                        State = native.fullPathHash, Time = native.normalizedTime, RootMotion = animator.applyRootMotion };
                    bodies.Add(body);
                    animator.applyRootMotion = false;
                    body.Graph = PlayableGraph.Create("REBIRTHCamperAuthoredSlot" + nativeSlot);
                    body.Graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                    var playable = AnimationClipPlayable.Create(body.Graph, clip);
                    playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                    playable.SetTime(clip.length * .5); playable.SetSpeed(0);
                    var output = AnimationPlayableOutput.Create(body.Graph, "LyingBody", animator);
                    output.SetSourcePlayable(playable); body.Graph.Play();
                }
                failure = null;
            }
            catch (Exception error)
            {
                Stop(); failure = error.Message;
                if (!warned) { Log.Warning("[REBIRTH Camper Pose] " + failure); warned = true; }
            }
        }
        private static void ValidateLayout(Transform layout, Transform target, string prefix)
        {
            foreach (Transform bone in layout)
            {
                string path = prefix.Length == 0 ? bone.name : prefix + "/" + bone.name;
                if (target.Find(path) == null) throw new InvalidOperationException("Player body does not match authored native bed skeleton: " + path);
                ValidateLayout(bone, target, path);
            }
        }
        private void LateUpdate()
        {
            if (occupant == null || RebirthCamperBedPoseService.AuthoredPoseOwner(occupant) != owner)
            { Stop(); Destroy(this); return; }
            Bind(occupant, owner); // Attachment replication, avatar replacement and armor bodies all use native membership.
        }
        internal RebirthCamperBedPoseService.Evidence Inspect()
        {
            return new RebirthCamperBedPoseService.Evidence { occupantId = occupant == null ? -1 : occupant.entityId,
                camperId = owner == null ? -1 : owner.entityId,
                nativeSlot = nativeSlot,
                nativeMembership = occupant != null && RebirthCamperBedPoseService.AuthoredPoseOwner(occupant) == owner
                    && RebirthCamperBedPoseService.NativeSlot(occupant, owner) == nativeSlot,
                nativeBedIKTargetsAbsent = owner != null && owner.vehicle != null && nativeSlot >= 0 && owner.vehicle.GetIKTargets(nativeSlot) == null,
                clip = clip == null ? null : clip.name, clipHumanMotion = clip != null && clip.humanMotion,
                clipGenericMotion = clip != null && !clip.humanMotion && !clip.legacy,
                nativeRigCompatible = layoutValidated && failure == null && bodies.Count > 0
                    && bodies.All(b => b.Animator != null && b.Animator.isActiveAndEnabled
                        && b.Animator.avatar != null && b.Animator.avatar.isValid && !b.Animator.isHuman
                        && b.Graph.IsValid() && b.Graph.IsPlaying()),
                bodyAnimatorCount = bodies.Count, activeGraphs = bodies.Count(b => b.Graph.IsValid() && b.Graph.IsPlaying()),
                controllerPreserved = bodies.All(b => b.Animator != null && b.Animator.runtimeAnimatorController == b.Controller),
                status = failure != null ? "FAILED" : bodies.Count > 0 ? "APPLIED_UNQUALIFIED" : "WAITING_FOR_AVATAR", error = failure };
        }
        private void Restore(Body body)
        {
            if (body.Graph.IsValid()) body.Graph.Destroy();
            if (body.Animator == null) return;
            body.Animator.applyRootMotion = body.RootMotion;
            // Detached riders already had native VehiclePose=-1/TPVResetAnimPose.
            // Never replay the old vehicle state over that native detach/death path.
            if (occupant != null && !occupant.IsDead() && RebirthCamperBedPoseService.AuthoredPoseOwner(occupant) == owner
                && RebirthCamperBedPoseService.NativeSlot(occupant, owner) == nativeSlot
                && body.Animator.runtimeAnimatorController == body.Controller
                && body.Animator.HasState(0, body.State)) body.Animator.Play(body.State, 0, body.Time);
        }
        internal void Stop() { foreach (var body in bodies) Restore(body); bodies.Clear(); layoutValidated = false; }
        private void OnDisable() { Stop(); }
        private void OnDestroy() { Stop(); }
    }

    [HarmonyPatch]
    internal static class RebirthCamperBedPosePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(EntityAlive), "AttachToEntity", new[] { typeof(Entity), typeof(int) });
            yield return AccessTools.Method(typeof(EntityAlive), "SetVehicleAnimation", new[] { typeof(int), typeof(int) });
            yield return AccessTools.Method(typeof(EntityAlive), "Detach", Type.EmptyTypes);
        }
        private static void Postfix(EntityAlive __instance) { RebirthCamperBedPoseService.Refresh(__instance); }
    }
}



