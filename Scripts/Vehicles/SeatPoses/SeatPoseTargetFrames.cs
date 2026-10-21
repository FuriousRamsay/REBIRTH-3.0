using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rebirth.Vehicles.Authoring
{
    internal static class SeatPoseTargetFrames
    {
        internal static string Path(Transform model, Transform parent)
        {
            if (!model || !parent || (parent != model && !parent.IsChildOf(model))) return null;
            var segments = new List<string>();
            for (var node = parent; node != model; node = node.parent) segments.Add(node.name);
            segments.Reverse();
            string path = string.Join("/", segments);
            if ((path.Length == 0 ? model : model.Find(path)) != parent)
                throw new InvalidOperationException("Ambiguous native IK parent path: " + path);
            return path;
        }
        internal static Transform Parent(Transform model, string path)
        {
            if (path == null) return null;
            var parent = path.Length == 0 ? model : model.Find(path);
            if (!parent) throw new InvalidOperationException("Saved native IK parent missing: " + path);
            return parent;
        }
        internal static void Capture(Transform model, Transform parent, Vector3 position, Vector3 rotation, out Vector3 localPosition, out Vector3 localRotation)
        {
            localPosition = parent.InverseTransformPoint(model.TransformPoint(position));
            localRotation = (Quaternion.Inverse(parent.rotation) * model.rotation * Quaternion.Euler(rotation)).eulerAngles;
        }
        internal static void Resolve(Transform model, Transform parent, Vector3 localPosition, Vector3 localRotation, out Vector3 position, out Vector3 rotation)
        {
            position = model.InverseTransformPoint(parent.TransformPoint(localPosition));
            rotation = (Quaternion.Inverse(model.rotation) * parent.rotation * Quaternion.Euler(localRotation)).eulerAngles;
        }
        internal static void Apply(Transform target, Transform parent, Vector3 localPosition, Vector3 localRotation)
        {
            target.SetParent(parent, false); target.localPosition = localPosition; target.localRotation = Quaternion.Euler(localRotation);
        }
    }
}
