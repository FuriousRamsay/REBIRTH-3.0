using System;
using System.Collections;
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
        MovementInput vehicleInput;
        bool vehicleInputCaptured, nativeCameraEnabled;
        bool samplingPose;
        bool inputAllowed, firstPerson, cursorVisible, closing, captured;
        CursorLockMode cursorLock;
        int originalPose, slot;
        Vector3 originalPosition;
        Quaternion originalRotation;
        internal Action<int> RequestSeat;
        internal bool IsReady => !closing && panel && panel.Profile != null && Valid();
        internal string Status => IsReady ? "READY seat=" + slot + " pose=" + panel.Profile.basePose + " bridgeClicks=" + panel.BridgeClicks : "NOT_READY";

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
                // Native vehicle steering reads the driving camera directly,
                // independently of PlayerMoveController.bAllowPlayerInput.
                nativeCameraEnabled = actor.vp_FPCamera.enabled;
                actor.vp_FPCamera.enabled = false;
                if (slot == 0)
                {
                    vehicleInput = owner.movementInput;
                    vehicleInputCaptured = true;
                    owner.movementInput = null;
                }
                // Existing camper custom overlay must not compete with authoring. Restore exact enabled states on close.
                foreach (var behavior in actor.GetComponents<Behaviour>().Where(b => b && b.GetType().FullName == "Rebirth.Vehicles.Camper.RebirthCamperBedVisual"))
                { overlays.Add(behavior); overlayStates.Add(behavior.enabled); behavior.enabled = false; }
                actor.SetFirstPersonView(false, true); input.bAllowPlayerInput = false;
                // Select the visible third-person bodies after the native view transition.
                animators=avatar.BodyAnimators.Select(b=>b.Animator).Where(a=>a&&a.isActiveAndEnabled).Distinct().ToArray();
                if(animators.Length==0)throw new InvalidOperationException("Native third-person body unavailable.");
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                foreach (var animator in animators) { rigs.Add(new SeatPoseRig(animator, owner.ModelTransform)); bones.Add(new SeatPoseBones(animator.transform)); }
                var hips = animators[0].GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == "Hips");
                camera = new SeatPoseCamera(nativeCamera, owner.ModelTransform, hips ? hips : actor.ModelTransform);
                var profile = new SeatPoseProfile { vehicle = owner.vehicle.GetName(), rig = animators[0].avatar.name, seat = slot, basePose = originalPose,
                    anchorPosition = owner.ModelTransform.InverseTransformPoint(actor.ModelTransform.position),
                    anchorRotation = (Quaternion.Inverse(owner.ModelTransform.rotation) * actor.ModelTransform.rotation).eulerAngles,
                    bones = bones[0].EditableBones() };
                var targetRecords = rigs[0].CaptureTargets().ToList();
                var bodyBones=animators[0].GetComponentsInChildren<Transform>(true);
                foreach(string side in new[]{"Left","Right"})foreach(bool arm in new[]{true,false})
                {
                    string boneName=side+(arm?"ForeArm":"Leg");var joint=bodyBones.SingleOrDefault(t=>t.name==boneName);
                    if(!joint)throw new InvalidOperationException("Native hint joint missing: "+boneName);
                    targetRecords.Add(new SeatPoseTransform{name=(arm?"IKElbow":"IKKnee")+(side=="Left"?"L":"R"),position=owner.ModelTransform.InverseTransformPoint(joint.position),weight=1});
                }
                profile.targets=targetRecords.ToArray();
                panel = gameObject.AddComponent<SeatPosePanel>(); panel.DirectoryPath = directory;
                // Capture native targets before loading a published experiment.
                panel.OriginalTargets = SeatPoseProfile.Parse(profile.Serialize()).targets;
                panel.ResetPoseDefaults = () => { if (!samplingPose) StartCoroutine(ReadPoseDefaults()); };
                panel.ReadVehicleTargets = () => SeatPoseVehicleTargets.Read(owner.ModelTransform, slot);
                panel.AlignVehicleTargets = () => { if (!samplingPose) StartCoroutine(ReadPoseDefaults(true)); };
                panel.MappingWarnings=string.Join(", ",bones.SelectMany(b=>b.Missing).Distinct());
                panel.SeatNames = Enumerable.Range(0, owner.GetAttachMaxCount()).Select(i => "Seat " + i + (owner.GetAttached(i) != null ? " (occupied)" : "")).ToArray();
                panel.Apply = Apply; panel.PrepareLoaded = ResolveSavedTargets; panel.CameraPreset = camera.Preset; panel.SelectSeat = i => RequestSeat?.Invoke(i); panel.Closed = Close;
                foreach(var animator in animators)animationSpeeds.Add(animator,animator.speed);
                panel.SetFrozen=frozen=>{foreach(var pair in animationSpeeds)if(pair.Key)pair.Key.speed=frozen?0:pair.Value;};
                var published = SeatPoseGameplay.Find(actor, owner, slot, profile.rig);
                if (published != null)
                {
                    profile = SeatPoseProfile.Parse(published.Serialize());
                    // Reconstruct the editor's model-space fields from the saved
                    // moving-parent offsets at the current steering angle. Do not
                    // reinterpret neutral model-space positions as turned offsets.
                    ResolveSavedTargets(profile);
                }
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
            // Use one canonical visible body's native vehicle target hierarchy.
            // Every body layer shares the same vehicle-space authored targets.
            if (rigs.Count > 0) rigs[0].CaptureNativeParents(profile);
            foreach (var rig in rigs) rig.Apply(profile);
        }
        void ResolveSavedTargets(SeatPoseProfile profile)
        {
            foreach (var target in profile.targets)
            {
                if (target.nativeParentPath == null) continue;
                var parent = SeatPoseTargetFrames.Parent(vehicle.ModelTransform, target.nativeParentPath);
                SeatPoseTargetFrames.Resolve(vehicle.ModelTransform, parent, target.nativeParentPosition, target.nativeParentRotation, out target.position, out target.rotation);
            }
        }
        IEnumerator ReadPoseDefaults(bool alignVehicle = false)
        {
            if (!Valid() || !panel) yield break;
            samplingPose = panel.SamplingPose = true;
            var before = SeatPoseProfile.Parse(panel.Profile.Serialize());
            var speeds = animationSpeeds.Keys.Where(a => a).ToDictionary(a => a, a => a.speed);
            bool completed = false;
            try
            {
                foreach (var b in bones) b.RemoveOffsets();
                foreach (var rig in rigs) rig.SuspendIK();
                player.SetVehiclePoseMode(before.basePose);
                foreach (var animator in speeds.Keys) animator.speed = 1;
                // Let the actual native generic controller evaluate the selected
                // pose with IK disabled, including any transition already in flight.
                yield return null;
                float deadline = Time.realtimeSinceStartup + 2;
                while (Valid() && speeds.Keys.Any(a => a && a.IsInTransition(0)) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return new WaitForEndOfFrame();
                if (closing || !Valid() || !panel) yield break;
                if (panel.Profile.Serialize() != before.Serialize()) yield break;
                if (speeds.Keys.Any(a => a && a.IsInTransition(0))) yield break;
                if (alignVehicle)
                {
                    var positions = panel.ReadVehicleTargets();
                    var hips = animationSpeeds.Keys.First().GetComponentsInChildren<Transform>(true).Single(t => t.name == "Hips");
                    // Match the pelvis contact, rather than assuming every native
                    // animation uses the same model-origin-to-pelvis offset.
                    Vector3 delta = positions["Seat"] - vehicle.ModelTransform.InverseTransformPoint(hips.position);
                    panel.ApplyVehicleTargets(positions, before.anchorPosition + delta);
                }
                else panel.ApplyPoseDefaults(rigs[0].CaptureAnimatedPoseTargets());
                completed = true;
            }
            finally
            {
                foreach (var pair in speeds) if (pair.Key) pair.Key.speed = pair.Value;
                samplingPose = false;
                if (panel) panel.SamplingPose = false;
                if (!closing && Valid() && panel && !completed) Apply(panel.Profile);
            }
        }
        bool Valid() => player && vehicle && !player.IsDead() && !vehicle.IsDead() && player.AttachedToEntity == vehicle
            && vehicle.vehicleRB && vehicle.vehicleRB.velocity.sqrMagnitude <= .01f
            && vehicle.FindAttachSlot(player) == slot && GameManager.Instance?.World == player.world;
        void Update()
        {
            if (closing) return;
            if (!Valid()) { Close(); return; }
            if(animationSpeeds.Keys.Any(a=>!a||!a.isActiveAndEnabled)){Close();return;}
            foreach (var b in bones) b.RemoveOffsets();
            if (samplingPose) foreach (var rig in rigs) rig.SuspendIK();
            if (panel && panel.Profile != null)
            {
                var profile = panel.Profile;
                player.ModelTransform.SetPositionAndRotation(vehicle.ModelTransform.TransformPoint(profile.anchorPosition), vehicle.ModelTransform.rotation * Quaternion.Euler(profile.anchorRotation));
            }
            if (input) input.bAllowPlayerInput = false;
            if (vehicleInputCaptured) vehicle.movementInput = null;
            if (camera != null && panel) camera.HandlePointer(panel.WindowRect);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        void LateUpdate()
        {
            if (closing) return;
            if (!Valid() || !panel) { Close(); return; }
            try
            {
                var p = panel.Profile;
                if (!samplingPose) foreach (var b in bones) b.ApplyOffsets(p);
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
            StopAllCoroutines();
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
                if (vehicleInputCaptured && vehicle && player && player.AttachedToEntity == vehicle
                    && vehicle.FindAttachSlot(player) == 0 && vehicle.movementInput == null)
                    vehicle.movementInput = vehicleInput;
                if (captured && player && player.vp_FPCamera) player.vp_FPCamera.enabled = nativeCameraEnabled;
                if (captured) { Cursor.lockState = cursorLock; Cursor.visible = cursorVisible; }
            }
            finally { if (panel) { panel.Closed = null; Destroy(panel); } Destroy(this); }
        }
        static void Cleanup(Action action) { try { action(); } catch (Exception error) { UnityEngine.Debug.LogWarning("[REBIRTH Seat Editor cleanup] " + error.Message); } }
        void OnDestroy() { if (!closing) Close(); }
    }
}
