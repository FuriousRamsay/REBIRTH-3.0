using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Rebirth.Vehicles.Authoring
{
    // Owns temporary targets for one actual native generic body. Session owns pose and attachment.
    internal sealed class SeatPoseRig : IDisposable
    {
        sealed class SavedConstraint
        {
            internal TwoBoneIKConstraint constraint;
            internal TwoBoneIKConstraintData data;
            internal float weight;
            internal string target;
        }
        readonly Transform model;
        readonly RigBuilder builder;
        readonly List<SavedConstraint> saved = new List<SavedConstraint>();
        readonly Dictionary<string, Transform> targets = new Dictionary<string, Transform>();
        readonly GameObject holder;
        readonly IKController native;
        readonly bool nativeEnabled;
        readonly List<IKController.Target> nativeTargets;
        bool disposed;
        readonly SeatPoseTorso torso;

        internal void CaptureNativeParents(SeatPoseProfile profile)
        {
            foreach (var record in profile.targets)
            {
                var original = saved.SingleOrDefault(s => s.target == record.name);
                var parent = original?.data.target ? original.data.target.parent : null;
                record.nativeParentPath = SeatPoseTargetFrames.Path(model, parent);
                if (record.nativeParentPath == null) continue;
                SeatPoseTargetFrames.Capture(model, parent, record.position, record.rotation, out record.nativeParentPosition, out record.nativeParentRotation);
            }
        }

        internal SeatPoseRig(Animator animator, Transform vehicleModel)
        {
            if (animator == null || animator.isHuman || animator.avatar == null || !animator.avatar.isValid) throw new InvalidOperationException("Valid native generic rig required.");
            model = vehicleModel ? vehicleModel : throw new ArgumentNullException(nameof(vehicleModel));
            torso = new SeatPoseTorso(animator);
            builder = animator.GetComponent<RigBuilder>();
            if (!builder) throw new InvalidOperationException("Native RigBuilder unavailable.");
            native = animator.GetComponent<IKController>(); nativeEnabled = native && native.enabled;
            nativeTargets = native ? native.targets : null;
            foreach (var c in animator.GetComponentsInChildren<TwoBoneIKConstraint>(true))
            {
                // ModifyRig replaces data.target with a vehicle target (or the
                // constraint itself). Its name no longer identifies the limb.
                int goal = native && native.rigConstraints != null
                    ? Array.FindIndex(native.rigConstraints, entry => entry.tbConstraint == c) : -1;
                string name = goal >= 0 && goal < IKController.IKNames.Length
                    ? IKController.IKNames[goal] : c.data.target ? c.data.target.name : "";
                if (name != "IKHandL" && name != "IKHandR" && name != "IKFootL" && name != "IKFootR") continue;
                if (saved.Any(s => s.target == name)) throw new InvalidOperationException("Ambiguous native constraint " + name);
                saved.Add(new SavedConstraint { constraint = c, data = c.data, weight = c.weight, target = name });
            }
            if (saved.Count != 4) throw new InvalidOperationException("All four native limb constraints required.");
            holder = new GameObject("REBIRTH_SeatAuthoring_TemporaryTargets"); holder.transform.SetParent(model, false);
            if (native) native.enabled = false;
        }
        internal SeatPoseTransform[] CaptureTargets()
        {
            return saved.Where(s => s.data.target).Select(s => new SeatPoseTransform {
                name = s.target,
                position = model.InverseTransformPoint(s.data.target.position),
                rotation = (Quaternion.Inverse(model.rotation) * s.data.target.rotation).eulerAngles,
                weight = s.weight
            }).ToArray();
        }
        internal void SuspendIK()
        {
            torso.Clear();
            foreach (var entry in saved) entry.constraint.weight = 0;
        }
        internal SeatPoseTransform[] CaptureAnimatedPoseTargets()
        {
            var result = new List<SeatPoseTransform>();
            foreach (var entry in saved)
            {
                var data = entry.constraint.data;
                if (!data.tip || !data.mid) throw new InvalidOperationException("Native limb bones unavailable.");
                result.Add(new SeatPoseTransform {
                    name = entry.target, position = model.InverseTransformPoint(data.tip.position),
                    rotation = (Quaternion.Inverse(model.rotation) * data.tip.rotation).eulerAngles, weight = 1
                });
                string hint = (entry.target.StartsWith("IKFoot") ? "IKKnee" : "IKElbow")
                    + (entry.target.EndsWith("L") ? "L" : "R");
                result.Add(new SeatPoseTransform {
                    name = hint, position = model.InverseTransformPoint(data.mid.position),
                    rotation = (Quaternion.Inverse(model.rotation) * data.mid.rotation).eulerAngles, weight = 1
                });
            }
            return result.ToArray();
        }
        internal void Apply(SeatPoseProfile profile, bool followNativeParents = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SeatPoseRig));
            profile.Validate();
            foreach (var record in profile.targets)
            {
                if (!targets.TryGetValue(record.name, out Transform t))
                { var go = new GameObject(record.name); t = go.transform; t.SetParent(holder.transform, false); targets.Add(record.name, t); }
                t.SetParent(holder.transform, false);
                t.localPosition = record.position; t.localRotation = Quaternion.Euler(record.rotation);
                if (followNativeParents)
                {
                    if (record.nativeParentPath != null)
                    {
                        var authoredParent = SeatPoseTargetFrames.Parent(model, record.nativeParentPath);
                        SeatPoseTargetFrames.Apply(t, authoredParent, record.nativeParentPosition, record.nativeParentRotation);
                        continue;
                    }
                    var original = saved.SingleOrDefault(s => s.target == record.name);
                    // Retain the authored neutral offset while following the native
                    // moving steering hierarchy. Hints remain in vehicle space.
                    var parent = original?.data.target ? original.data.target.parent : null;
                    if (parent && (parent == model || parent.IsChildOf(model))) t.SetParent(parent, true);
                }
            }
            bool rebuild = false;
            foreach (var original in saved)
            {
                var c = original.constraint; var data = c.data;
                var record = profile.targets.SingleOrDefault(r => r.name == original.target);
                Transform target = record == null ? original.data.target : targets[record.name];
                string hintName = original.target.StartsWith("IKFoot") ? "IKKnee" : "IKElbow";
                hintName += original.target.EndsWith("L") ? "L" : "R";
                var hintRecord = profile.targets.SingleOrDefault(r => r.name == hintName);
                Transform hint = hintRecord == null ? original.data.hint : targets[hintName];
                rebuild |= data.target != target || data.hint != hint;
                data.target = target; data.hint = hint;
                data.hintWeight = hintRecord == null ? original.data.hintWeight : hintRecord.weight;
                c.data = data; c.weight = record == null ? original.weight : record.weight;
            }
            if (rebuild) { builder.Clear(); if (!builder.Build()) throw new InvalidOperationException("Native rig rebuild failed."); }
            torso.Apply(profile.torsoLean, builder.graph);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            torso.Clear();
            try
            {
                foreach (var s in saved) if (s.constraint) { s.constraint.data = s.data; s.constraint.weight = s.weight; }
                if (builder)
                {
                    builder.Clear();
                    // Native detach/Cleanup or a new seat may have installed a
                    // different target list while this component owned the rig.
                    // Honour that latest native state instead of resurrecting the
                    // saved vehicle targets after the player has left it.
                    if (native && !ReferenceEquals(native.targets, nativeTargets)) native.ModifyRig();
                    else builder.Build();
                }
            }
            finally
            {
                if (native) native.enabled = nativeEnabled;
                // Gameplay targets may be parented to steering transforms rather
                // than the holder, so destroying the holder alone would leak them.
                foreach (var t in targets.Values) if (t) UnityEngine.Object.Destroy(t.gameObject);
                targets.Clear();
                if (holder) UnityEngine.Object.Destroy(holder);
            }
        }
    }
}

