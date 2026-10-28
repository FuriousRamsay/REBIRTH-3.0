// Unity authoring data only. DO NOT attach this ScriptableObject or the workspace component
// to exported 7DTD vehicle prefabs. The prefab builder strips workspace components.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public enum VehicleFamily
    {
        CarOrTruck, Bicycle, Minibike, Motorcycle, ATV, Gyrocopter,
        Helicopter, Boat, HeavyMachinery, Custom
    }

    [Serializable]
    public sealed class VehicleSeatAnchor
    {
        public int index;
        public string poseId = "";
        public Vector3 position;
        public Vector3 rotationEuler;
        public List<Vector3> exits = new List<Vector3>();
        public string requiredMod = "";
        public string modTransformPath = "";
    }

    [Serializable]
    public sealed class VehicleIkAnchor
    {
        public string partClass = "handlebars";
        public string partType = "Steering";
        public string pivotPath = "Origin";
        public bool hasLeftHand, hasRightHand, hasLeftFoot, hasRightFoot;
        public Vector3 leftHandPosition, leftHandRotationEuler;
        public Vector3 rightHandPosition, rightHandRotationEuler;
        public Vector3 leftFootPosition, leftFootRotationEuler;
        public Vector3 rightFootPosition, rightFootRotationEuler;
    }

    [Serializable]
    public sealed class VehicleTransformLink
    {
        public string partClass, propertyName, transformPath;
        // A generated empty transform is a placeholder, NOT a calibrated pivot.
        public bool authoredPlaceholder;
    }

    [Serializable]
    public sealed class VehicleWheelLink
    {
        public string partClass, steerTransformPath, tireTransformPath;
    }

    // This is a Unity Editor authoring profile, not a 7DTD runtime component.
    [CreateAssetMenu(menuName = "REBIRTH/7DTD Vehicle Rig Profile", fileName = "VehicleRigProfile")]
    public sealed class RebirthVehicleRigProfile : ScriptableObject
    {
        public int profileVersion = 2;
        public string sourceCaptureFilename = "";
        public string sourceGameVersion = "unknown";
        public string sourceXmlStatus = "unverified";
        public string sourceVehicleName = "";
        public string targetVehicleName = "";
        public string sourcePrefabReference = "";
        public VehicleFamily family = VehicleFamily.Custom;
        public List<VehicleSeatAnchor> seats = new List<VehicleSeatAnchor>();
        public List<VehicleIkAnchor> ik = new List<VehicleIkAnchor>();
        public List<VehicleTransformLink> links = new List<VehicleTransformLink>();
        public List<VehicleWheelLink> wheels = new List<VehicleWheelLink>();
        public List<string> importWarnings = new List<string>();
    }

    // Only for editing inside a Unity SCENE, not for shipping to the game.
    [DisallowMultipleComponent]
    public sealed class RebirthVehicleRigWorkspace : MonoBehaviour
    {
        public RebirthVehicleRigProfile profile;
        public Transform origin;
        public GameObject importedVisuals;
    }
}
