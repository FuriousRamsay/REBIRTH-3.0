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
        bool disposed;

        internal SeatPoseRig(Animator animator, Transform vehicleModel)
        {
            if (animator == null || animator.isHuman || animator.avatar == null || !animator.avatar.isValid) throw new InvalidOperationException("Valid native generic rig required.");
            model = vehicleModel ? vehicleModel : throw new ArgumentNullException(nameof(vehicleModel));
            builder = animator.GetComponent<RigBuilder>();
            if (!builder) throw new InvalidOperationException("Native RigBuilder unavailable.");
            foreach (var c in animator.GetComponentsInChildren<TwoBoneIKConstraint>(true))
            {
                string name = c.data.target ? c.data.target.name : "";
                if (name != "IKHandL" && name != "IKHandR" && name != "IKFootL" && name != "IKFootR") continue;
                if (saved.Any(s => s.target == name)) throw new InvalidOperationException("Ambiguous native constraint " + name);
                saved.Add(new SavedConstraint { constraint = c, data = c.data, weight = c.weight, target = name });
            }
            if (saved.Count != 4) throw new InvalidOperationException("All four native limb constraints required.");
            native = animator.GetComponent<IKController>(); nativeEnabled = native && native.enabled;
            holder = new GameObject("REBIRTH_SeatAuthoring_TemporaryTargets"); holder.transform.SetParent(model, false);
            if (native) native.enabled = false;
        }
        internal void Apply(SeatPoseProfile profile)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SeatPoseRig));
            profile.Validate();
            foreach (var record in profile.targets)
            {
                if (!targets.TryGetValue(record.name, out Transform t))
                { var go = new GameObject(record.name); t = go.transform; t.SetParent(holder.transform, false); targets.Add(record.name, t); }
                t.localPosition = record.position; t.localRotation = Quaternion.Euler(record.rotation);
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
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try
            {
                foreach (var s in saved) if (s.constraint) { s.constraint.data = s.data; s.constraint.weight = s.weight; }
                if (builder) { builder.Clear(); builder.Build(); }
            }
            finally { if (native) native.enabled = nativeEnabled; if (holder) UnityEngine.Object.Destroy(holder); }
        }
    }
}
