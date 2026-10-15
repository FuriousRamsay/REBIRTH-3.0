using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Rebirth.Vehicles.Authoring
{
    [DefaultExecutionOrder(20000)]
    internal sealed class SeatPoseSession : MonoBehaviour
    {
        EntityPlayerLocal player;
        EntityVehicle vehicle;
        SeatPosePanel panel;
        SeatPoseCamera camera;
        readonly List<SeatPoseRig> rigs = new List<SeatPoseRig>();
        readonly List<SeatPoseBones> bones = new List<SeatPoseBones>();
        readonly List<Behaviour> overlays = new List<Behaviour>();
        readonly List<bool> overlayStates = new List<bool>();
        readonly Dictionary<Animator,float> animationSpeeds = new Dictionary<Animator,float>();
        PlayerMoveController input;
        bool inputAllowed, firstPerson, cursorVisible, closing, captured;
        CursorLockMode cursorLock;
        int originalPose, slot;
        Vector3 originalPosition;
        Quaternion originalRotation;
        internal Action<int> RequestSeat;
        internal bool IsReady => !closing && panel && panel.Profile != null && Valid();

        internal void Open(EntityPlayerLocal actor, EntityVehicle owner, string directory)
        {
            player = actor; vehicle = owner;
            if (GameManager.IsDedicatedServer || !actor || !owner || actor.AttachedToEntity != owner || actor.IsDead()) throw new InvalidOperationException("A live local seated player is required.");
            slot = owner.FindAttachSlot(actor); if (slot < 0) throw new InvalidOperationException("Native seat membership missing.");
            input = actor.GetComponent<PlayerMoveController>(); if (!input) throw new InvalidOperationException("Native player input controller missing.");
            var avatar = actor.emodel?.avatarController as AvatarMultiBodyController;
            if (avatar == null || !owner.ModelTransform || !actor.ModelTransform) throw new InvalidOperationException("Native model unavailable.");
            Animator[] animators;
            var nativeCamera = actor.vp_FPCamera ? actor.vp_FPCamera.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.enabled) : null;
            if (!nativeCamera) throw new InvalidOperationException("Native player camera unavailable.");
            originalPosition = actor.ModelTransform.localPosition; originalRotation = actor.ModelTransform.localRotation;
            originalPose = actor.GetVehicleAnimation(); firstPerson = actor.bFirstPersonView;
            inputAllowed = input.bAllowPlayerInput; cursorVisible = Cursor.visible; cursorLock = Cursor.lockState;
            captured = true;
            try
            {
                // Existing camper custom overlay must not compete with authoring. Restore exact enabled states on close.
                foreach (var behavior in actor.GetComponents<Behaviour>().Where(b => b && b.GetType().FullName == "Rebirth.Vehicles.Camper.RebirthCamperBedVisual"))
                { overlays.Add(behavior); overlayStates.Add(behavior.enabled); behavior.enabled = false; }
                actor.SetFirstPersonView(false, true); input.bAllowPlayerInput = false;
                // Select the visible third-person bodies after the native view transition.
                animators=avatar.BodyAnimators.Select(b=>b.Animator).Where(a=>a&&a.isActiveAndEnabled).Distinct().ToArray();
                if(animators.Length==0)throw new InvalidOperationException("Native third-person body unavailable.");
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                foreach (var animator in animators) { rigs.Add(new SeatPoseRig(animator, owner.ModelTransform)); bones.Add(new SeatPoseBones(animator.transform)); }
                camera = new SeatPoseCamera(nativeCamera, owner.ModelTransform, actor.ModelTransform);
                var profile = new SeatPoseProfile { vehicle = owner.vehicle.GetName(), rig = animators[0].avatar.name, seat = slot, basePose = originalPose,
                    anchorPosition = owner.ModelTransform.InverseTransformPoint(actor.ModelTransform.position),
                    anchorRotation = (Quaternion.Inverse(owner.ModelTransform.rotation) * actor.ModelTransform.rotation).eulerAngles,
                    bones = bones[0].EditableBones() };
                var targetRecords = animators[0].GetComponentsInChildren<TwoBoneIKConstraint>(true).Where(c => c.data.target && new[]{"IKHandL","IKHandR","IKFootL","IKFootR"}.Contains(c.data.target.name))
                    .Select(c => new SeatPoseTransform { name = c.data.target.name, position = owner.ModelTransform.InverseTransformPoint(c.data.target.position),
                        rotation = (Quaternion.Inverse(owner.ModelTransform.rotation) * c.data.target.rotation).eulerAngles, weight = c.weight }).ToList();
                var bodyBones=animators[0].GetComponentsInChildren<Transform>(true);
                foreach(string side in new[]{"Left","Right"})foreach(bool arm in new[]{true,false})
                {
                    string boneName=side+(arm?"ForeArm":"Leg");var joint=bodyBones.SingleOrDefault(t=>t.name==boneName);
                    if(!joint)throw new InvalidOperationException("Native hint joint missing: "+boneName);
                    targetRecords.Add(new SeatPoseTransform{name=(arm?"IKElbow":"IKKnee")+(side=="Left"?"L":"R"),position=owner.ModelTransform.InverseTransformPoint(joint.position),weight=1});
                }
                profile.targets=targetRecords.ToArray();
                panel = gameObject.AddComponent<SeatPosePanel>(); panel.DirectoryPath = directory;
                panel.MappingWarnings=string.Join(", ",bones.SelectMany(b=>b.Missing).Distinct());
                panel.SeatNames = Enumerable.Range(0, owner.GetAttachMaxCount()).Select(i => "Seat " + i + (owner.GetAttached(i) != null ? " (occupied)" : "")).ToArray();
                panel.Apply = Apply; panel.CameraPreset = camera.Preset; panel.SelectSeat = i => RequestSeat?.Invoke(i); panel.Closed = Close;
                foreach(var animator in animators)animationSpeeds.Add(animator,animator.speed);
                panel.SetFrozen=frozen=>{foreach(var pair in animationSpeeds)if(pair.Key)pair.Key.speed=frozen?0:pair.Value;};
                panel.Bind(profile); Apply(profile);
            }
            catch { Close(); throw; }
        }
        void Apply(SeatPoseProfile profile)
        {
            if (!Valid()) throw new InvalidOperationException("Authoring seat is no longer available.");
            foreach (var b in bones) b.RemoveOffsets();
            player.SetVehiclePoseMode(profile.basePose);
            player.ModelTransform.SetPositionAndRotation(vehicle.ModelTransform.TransformPoint(profile.anchorPosition), vehicle.ModelTransform.rotation * Quaternion.Euler(profile.anchorRotation));
            foreach (var rig in rigs) rig.Apply(profile);
        }
        bool Valid() => player && vehicle && !player.IsDead() && !vehicle.IsDead() && player.AttachedToEntity == vehicle
            && vehicle.vehicleRB && vehicle.vehicleRB.velocity.sqrMagnitude <= .01f
            && vehicle.FindAttachSlot(player) == slot && GameManager.Instance?.World == player.world;
        void Update()
        {
            if (!Valid()) { Close(); return; }
            if(animationSpeeds.Keys.Any(a=>!a||!a.isActiveAndEnabled)){Close();return;}
            foreach (var b in bones) b.RemoveOffsets();
            if (input) input.bAllowPlayerInput = false;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        void LateUpdate()
        {
            if (!Valid() || !panel) { Close(); return; }
            try
            {
                var p = panel.Profile;
                player.ModelTransform.SetPositionAndRotation(vehicle.ModelTransform.TransformPoint(p.anchorPosition), vehicle.ModelTransform.rotation * Quaternion.Euler(p.anchorRotation));
                foreach (var b in bones) b.ApplyOffsets(p);
                camera.Tick();
            }
            catch (Exception error) { UnityEngine.Debug.LogWarning("[REBIRTH Seat Editor] " + error.Message); Close(); }
        }
        void OnGUI()
        {
            if(closing||!panel||!panel.ShowTargets||camera==null||!vehicle||!vehicle.ModelTransform)return;
            int previous=GUI.depth;GUI.depth=1;
            try{foreach(var target in panel.Profile.targets)
            {var p=camera.Project(vehicle.ModelTransform.TransformPoint(target.position));if(p.z>0)GUI.Label(new Rect(p.x-4,Screen.height-p.y-10,130,24),"+ "+target.name);}}
            finally{GUI.depth=previous;}
        }
        internal void Close()
        {
            if (closing) return; closing = true;
            try
            {
                foreach (var b in bones) Cleanup(b.Dispose);
                foreach (var rig in rigs) Cleanup(rig.Dispose);
                if (camera != null) Cleanup(camera.Dispose);
                foreach(var pair in animationSpeeds)if(pair.Key)Cleanup(()=>pair.Key.speed=pair.Value);
                if (player && player.ModelTransform && player.AttachedToEntity == vehicle && vehicle && vehicle.FindAttachSlot(player) == slot)
                { player.ModelTransform.localPosition = originalPosition; player.ModelTransform.localRotation = originalRotation; Cleanup(() => player.SetVehiclePoseMode(originalPose)); }
                if (captured && player) Cleanup(() => player.SetFirstPersonView(firstPerson, true));
                for (int i = 0; i < overlays.Count; i++) if (overlays[i]) { int index=i; Cleanup(() => overlays[index].enabled = overlayStates[index]); }
                if (captured && input) input.bAllowPlayerInput = inputAllowed;
                if (captured) { Cursor.lockState = cursorLock; Cursor.visible = cursorVisible; }
            }
            finally { if (panel) { panel.Closed = null; Destroy(panel); } Destroy(this); }
        }
        static void Cleanup(Action action) { try { action(); } catch (Exception error) { UnityEngine.Debug.LogWarning("[REBIRTH Seat Editor cleanup] " + error.Message); } }
        void OnDestroy() { if (!closing) Close(); }
    }
}
