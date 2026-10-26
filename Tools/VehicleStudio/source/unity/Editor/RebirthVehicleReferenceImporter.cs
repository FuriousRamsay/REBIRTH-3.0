using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    // Mirrors only the schema-v1 fields needed by Unity. No game DLL references.
    public static class RebirthVehicleReferenceImporter
    {
        [Serializable] private sealed class CaptureDto
        {
            public int schemaVersion;
            public string capturedUtc, gameVersion;
            public ConfigDto xmlConfig;
        }
        [Serializable] private sealed class ConfigDto
        {
            public string name, prefab, status;
            public SeatDto[] seats;
            public PartDto[] parts;
        }
        [Serializable] private sealed class SeatDto
        {
            public int index;
            public string pose, position, rotation, exit, mod, modT;
        }
        [Serializable] private sealed class PartDto
        {
            public string className;
            public PropertyDto[] properties;
        }
        [Serializable] private sealed class PropertyDto { public string key, value; }

        public static RebirthVehicleRigProfile Import(string capturePath, string assetPath)
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Profile must be saved as Assets/.../*.asset", "assetPath");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                throw new IOException("Refusing to overwrite existing Unity asset: " + assetPath);

            string json = File.ReadAllText(capturePath);
            var profile = FromJson(json);
            profile.sourceCaptureFilename = Path.GetFileName(capturePath);
            // Project paths may specify nested folders not yet created by a batch job.
            // Never write outside Assets/ and never overwrite existing assets.
            string folder = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureAssetFolder(folder);
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (folder == "Assets") return;
            if (string.IsNullOrWhiteSpace(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Rig profile must be under Assets/", "folder");
            var tokens = folder.Split('/');
            string current = "Assets";
            for (int i = 1; i < tokens.Length; i++)
            {
                if (tokens[i] == "" || tokens[i] == "." || tokens[i] == "..")
                    throw new ArgumentException("Unsafe profile folder path", "folder");
                string next = current + "/" + tokens[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, tokens[i]);
                current = next;
            }
        }

        // Available for Unity EditMode tests and batch scripts. Source is NOT trusted as a
        // validated 7DTD rig merely because its JSON parses successfully.
        public static RebirthVehicleRigProfile FromJson(string json)
        {
            CaptureDto capture;
            try { capture = JsonUtility.FromJson<CaptureDto>(json); }
            catch (Exception ex) { throw new InvalidDataException("Capture JSON parse failed", ex); }
            if (capture == null || capture.schemaVersion != 1 || capture.xmlConfig == null)
                throw new InvalidDataException("Capture must have schemaVersion=1 and xmlConfig.");
            var cfg = capture.xmlConfig;
            var result = ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            result.sourceGameVersion = capture.gameVersion ?? "unknown";
            result.sourceXmlStatus = cfg.status ?? "unknown";
            result.sourceVehicleName = cfg.name ?? "";
            result.targetVehicleName = cfg.name ?? "";
            result.sourcePrefabReference = cfg.prefab ?? "";
            result.family = GuessFamily(cfg.name);
            if (cfg.status != "ok")
                result.importWarnings.Add("XML config was not verified as bound to the running vehicle.");
            if ((capture.gameVersion ?? "").IndexOf("SYNTHETIC", StringComparison.OrdinalIgnoreCase) >= 0)
                result.importWarnings.Add("SYNTHETIC fixture. Not actual in-game vehicle evidence.");

            var usedSeatIds = new HashSet<int>();
            foreach (var seat in cfg.seats ?? new SeatDto[0])
            {
                if (seat.index < 0 || seat.index > 5)
                {
                    result.importWarnings.Add("seat" + seat.index + " outside documented seat0-seat5 range; skipped.");
                    continue;
                }
                if (!usedSeatIds.Add(seat.index))
                {
                    result.importWarnings.Add("Duplicate seat" + seat.index + " skipped.");
                    continue;
                }
                Vector3 position;
                if (!TryVector(seat.position, out position))
                {
                    result.importWarnings.Add("seat" + seat.index + " missing/invalid position; skipped.");
                    continue;
                }
                var target = new VehicleSeatAnchor {
                    index = seat.index, poseId = seat.pose ?? "",
                    position = position, requiredMod = seat.mod ?? "",
                    modTransformPath = seat.modT ?? ""
                };
                Vector3 rotation;
                if (TryVector(seat.rotation, out rotation)) target.rotationEuler = rotation;
                else if (!string.IsNullOrEmpty(seat.rotation))
                    result.importWarnings.Add("seat" + seat.index + " rotation malformed; using zero.");
                foreach (string exit in (seat.exit ?? "").Split('~'))
                {
                    Vector3 p;
                    if (TryVector(exit, out p)) target.exits.Add(p);
                    else if (!string.IsNullOrWhiteSpace(exit))
                        result.importWarnings.Add("seat" + seat.index + " contains invalid exit coordinate: " + exit);
                }
                result.seats.Add(target);
            }
            result.seats.Sort((a, b) => a.index.CompareTo(b.index));

            foreach (var part in cfg.parts ?? new PartDto[0])
            {
                string cls = part.className ?? "";
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var prop in part.properties ?? new PropertyDto[0])
                    if (prop != null && !string.IsNullOrEmpty(prop.key)) dict[prop.key] = prop.value;

                if (cls == "handlebars" || cls == "pedals")
                {
                    var ik = new VehicleIkAnchor { partClass = cls,
                        partType = Get(dict, "class") ?? (cls == "pedals" ? "Pedals" : "Steering"),
                        pivotPath = Get(dict, "transform") ?? "" };
                    ik.hasLeftHand = ReadVector(dict, "IKHandLPosition", out ik.leftHandPosition);
                    ik.hasRightHand = ReadVector(dict, "IKHandRPosition", out ik.rightHandPosition);
                    ik.hasLeftFoot = ReadVector(dict, "IKFootLPosition", out ik.leftFootPosition);
                    ik.hasRightFoot = ReadVector(dict, "IKFootRPosition", out ik.rightFootPosition);
                    ReadVector(dict, "IKHandLRotation", out ik.leftHandRotationEuler);
                    ReadVector(dict, "IKHandRRotation", out ik.rightHandRotationEuler);
                    ReadVector(dict, "IKFootLRotation", out ik.leftFootRotationEuler);
                    ReadVector(dict, "IKFootRRotation", out ik.rightFootRotationEuler);
                    if (string.IsNullOrEmpty(ik.pivotPath))
                        result.importWarnings.Add(cls + ".transform missing; cannot place IK targets accurately.");
                    result.ik.Add(ik);
                }

                if (cls.StartsWith("wheel", StringComparison.Ordinal) && cls.Length > 5)
                    result.wheels.Add(new VehicleWheelLink {partClass = cls,
                        steerTransformPath = Get(dict, "steerTransform") ?? "",
                        tireTransformPath = Get(dict, "tireTransform") ?? ""});
                foreach (string key in new [] {"transform", "steerTransform", "tireTransform", "modT", "modHideT"})
                {
                    string path = Get(dict, key);
                    if (!string.IsNullOrWhiteSpace(path))
                        result.links.Add(new VehicleTransformLink {
                            partClass = cls, propertyName = key, transformPath = path});
                }
            }
            // A seat mod's path is referenced in a different XML block.
            foreach (var s in result.seats)
                if (!string.IsNullOrWhiteSpace(s.modTransformPath))
                    result.links.Add(new VehicleTransformLink {
                        partClass = "seat" + s.index, propertyName = "modT",
                        transformPath = s.modTransformPath });
            return result;
        }

        private static string Get(Dictionary<string, string> data, string key)
        { string v; return data.TryGetValue(key, out v) ? v : null; }
        private static bool ReadVector(Dictionary<string, string> data, string key, out Vector3 value)
        { return TryVector(Get(data, key), out value); }
        public static bool TryVector(string text, out Vector3 value)
        {
            value = Vector3.zero;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var v = text.Split(',');
            if (v.Length != 3) return false;
            float x, y, z;
            if (!float.TryParse(v[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !float.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z) ||
                float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
                float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z)) return false;
            value = new Vector3(x, y, z);
            return true;
        }
        private static VehicleFamily GuessFamily(string name)
        {
            string s = (name ?? "").ToLowerInvariant();
            if (s.Contains("bicycle")) return VehicleFamily.Bicycle;
            if (s.Contains("minibike")) return VehicleFamily.Minibike;
            if (s.Contains("motorcycle")) return VehicleFamily.Motorcycle;
            if (s.Contains("gyrocopter")) return VehicleFamily.Gyrocopter;
            if (s.Contains("helicopter")) return VehicleFamily.Helicopter;
            if (s.Contains("boat")) return VehicleFamily.Boat;
            if (s.Contains("truck") || s.Contains("car") || s.Contains("4x4")) return VehicleFamily.CarOrTruck;
            return VehicleFamily.Custom; // A suggestion; not a game-supported family claim.
        }
    }
}
