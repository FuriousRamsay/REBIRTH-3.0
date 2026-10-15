using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rebirth.Vehicles.Authoring
{
    // Session calls RemoveOffsets before native evaluation, ApplyOffsets after native pose/IK.
    // Keeping those phases explicit prevents additive rotation accumulation on unwritten bones.
    internal sealed class SeatPoseBones : IDisposable
    {
        sealed class Finger
        {
            internal Transform bone;
            internal Vector3 bendAxis, spreadAxis;
            internal int hand, digit, joint;
        }
        readonly Dictionary<string, Transform> paths = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> applied = new Dictionary<Transform, Quaternion>();
        readonly List<Finger> fingers = new List<Finger>();
        internal readonly List<string> Missing = new List<string>();
        internal SeatPoseBones(Transform root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            foreach (var bone in root.GetComponentsInChildren<Transform>(true)) paths.Add(Path(root, bone), bone);
            string[] digits = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
            var all = paths.Values.ToArray();
            for (int hand = 0; hand < 2; hand++)
            for (int digit = 0; digit < digits.Length; digit++)
            {
                string prefix = (hand == 0 ? "Left" : "Right") + "Hand" + digits[digit];
                var chain = Enumerable.Range(1, 3).Select(i => all.SingleOrDefault(t => t.name == prefix + i)).ToArray();
                if (chain.Any(t => !t)) { Missing.Add(prefix); continue; }
                var a = chain[1].position - chain[0].position; var b = chain[2].position - chain[1].position;
                var normal = Vector3.Cross(a, b);
                // A straight chain has no measured bend plane; advanced joint offsets remain available.
                if (normal.sqrMagnitude < 1e-12f) { Missing.Add(prefix + " bend plane"); continue; }
                var palm = all.SingleOrDefault(t => t.name == (hand == 0 ? "LeftHand" : "RightHand"));
                if (!palm) { Missing.Add(prefix + " palm"); continue; }
                var spreadNormal = Vector3.Cross(chain[0].position - palm.position, a).normalized;
                if (spreadNormal.sqrMagnitude < .5f) spreadNormal = normal.normalized;
                for (int joint = 0; joint < chain.Length; joint++)
                    fingers.Add(new Finger { bone = chain[joint], bendAxis = chain[joint].InverseTransformDirection(normal.normalized),
                        spreadAxis = chain[joint].InverseTransformDirection(spreadNormal), hand = hand, digit = digit, joint = joint });
            }
        }
        internal SeatPoseBoneOffset[] EditableBones() => paths.Where(p => p.Key.Length > 0).Select(p => new SeatPoseBoneOffset { path = p.Key }).ToArray();
        internal void RemoveOffsets()
        {
            foreach (var pair in applied) if (pair.Key) pair.Key.localRotation *= Quaternion.Inverse(pair.Value);
            applied.Clear();
        }
        internal void ApplyOffsets(SeatPoseProfile profile)
        {
            if (applied.Count != 0) throw new InvalidOperationException("Remove previous offsets before evaluating the native pose.");
            profile.Validate();
            foreach (var finger in fingers)
            {
                var hand = finger.hand == 0 ? profile.leftHand : profile.rightHand;
                float curl = Mathf.Clamp(hand.close + hand.curl[finger.digit], -1, 1);
                Quaternion delta = Quaternion.AngleAxis(curl * (finger.joint == 0 ? 45 : 65), finger.bendAxis);
                if (finger.joint == 0) delta *= Quaternion.AngleAxis(hand.spread * (finger.digit - 2) * 8, finger.spreadAxis);
                Add(finger.bone, delta);
            }
            foreach (var offset in profile.bones)
            {
                if (!paths.TryGetValue(offset.path, out Transform bone)) throw new InvalidOperationException("Saved bone missing: " + offset.path);
                Add(bone, Quaternion.Euler(offset.rotation));
            }
        }
        void Add(Transform bone, Quaternion delta)
        {
            bone.localRotation *= delta;
            applied[bone] = applied.TryGetValue(bone, out Quaternion previous) ? previous * delta : delta;
        }
        static string Path(Transform root, Transform bone)
        {
            if (bone == root) return "";
            var names = new List<string>(); for (var t = bone; t != root; t = t.parent) names.Add(t.name);
            names.Reverse(); return string.Join("/", names);
        }
        public void Dispose() => RemoveOffsets();
    }
}
