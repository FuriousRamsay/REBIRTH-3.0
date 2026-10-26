using System;
using UnityEngine;

namespace RebirthVehicleInspector.Model
{
    // Public fields and arrays are deliberate: Unity JsonUtility serializes them without
    // requiring external JSON libraries. Floats are in Unity world/local units (metres).
    [Serializable] public sealed class V3 { public float x,y,z; public static V3 Of(Vector3 p) => new V3 { x=p.x,y=p.y,z=p.z }; }
    [Serializable] public sealed class Q4 { public float x,y,z,w; public static Q4 Of(Quaternion q) => new Q4 { x=q.x,y=q.y,z=q.z,w=q.w }; }
    [Serializable] public sealed class Pair { public string key, value; }
    [Serializable] public sealed class Pose
    {
        public V3 worldPosition, localPosition, vehiclePosition;
        public Q4 worldRotation, localRotation, vehicleRotation;
        public V3 localScale;
        public static Pose From(Transform t, Transform vehicle)
        {
            return new Pose {
                worldPosition=V3.Of(t.position), localPosition=V3.Of(t.localPosition),
                vehiclePosition=V3.Of(vehicle.InverseTransformPoint(t.position)),
                worldRotation=Q4.Of(t.rotation), localRotation=Q4.Of(t.localRotation),
                vehicleRotation=Q4.Of(Quaternion.Inverse(vehicle.rotation)*t.rotation),
                localScale=V3.Of(t.localScale)
            };
        }
    }
    [Serializable] public sealed class ComponentRecord
    {
        public string type;
        public Pair[] metadata = new Pair[0];
    }
    [Serializable] public sealed class NodeRecord
    {
        public string path, parentPath, name;
        public bool activeSelf, activeInHierarchy;
        public int layer;
        public Pose pose;
        public ComponentRecord[] components = new ComponentRecord[0];
    }
    [Serializable] public sealed class EntitySummary
    {
        public int entityId;
        public string type, source, referenceStatus;
        public V3 worldPosition;
        public float distanceMetres;
    }
    [Serializable] public sealed class BoneRecord
    {
        public string bone;
        public string source; // humanoid or skinnedMesh (never assert IK support)
        public Pose pose;
    }
    [Serializable] public sealed class OccupantRecord
    {
        public int entityId;
        public string entityType, association, associationSource; // no guessing of seat index
        public int reportedSeatIndex = -1;
        public float distanceMetres;
        public string animatorPath, animationState;
        public BoneRecord[] bones = new BoneRecord[0];
        public string[] warnings = new string[0];
    }
    [Serializable] public sealed class DynamicNode
    {
        public string path; public Pose pose;
    }
    [Serializable] public sealed class FrameRecord
    {
        public float secondsFromStart, realtimeSinceStartup;
        public int unityFrame;
        public Pose vehiclePose;
        public DynamicNode[] trackedNodes = new DynamicNode[0];
        public OccupantRecord[] occupantCandidates = new OccupantRecord[0];
    }
    [Serializable] public sealed class PartConfig
    {
        public string className;
        public Pair[] properties = new Pair[0];
    }
    [Serializable] public sealed class SeatConfig
    {
        public int index;
        public string pose, position, rotation, exit, mod, modT;
        public Pair[] properties = new Pair[0];
    }
    [Serializable] public sealed class VehicleConfig
    {
        public string name, prefab, filePath, status;
        public SeatConfig[] seats = new SeatConfig[0];
        public PartConfig[] parts = new PartConfig[0];
        public Pair[] rootProperties = new Pair[0];
    }
    [Serializable] public sealed class TransformCheck
    {
        public string className, property, expectedPath, resolvedPath, note;
        public bool found;
    }
    [Serializable] public sealed class CaptureFile
    {
        public int schemaVersion = 1;
        public string inspectorVersion = "0.6.0", capturedUtc, gameVersion, captureType, scenarioLabel;
        // Serializer and counts allow downstream tools to reject old metadata-only captures.
        public string serializer;
        public string gameVersionSource = "UnityEngine.Application.version (not verified 7DTD release)";
        public int hierarchyNodeCount, frameCount, transformCheckCount;
        public int occupantCandidateSamples, occupantBoneSamples, vehicleAttachmentSamples, knownSeatSamples;
        public string selectionMethod, notes;
        public EntitySummary vehicle;
        public VehicleConfig xmlConfig;
        public NodeRecord[] hierarchy = new NodeRecord[0];
        public TransformCheck[] transformChecks = new TransformCheck[0];
        public FrameRecord[] frames = new FrameRecord[0];
        public string[] warnings = new string[0];
        public string[] diagnostics = new string[0];
        public bool truncated;
    }
}
