// Editor-only bridge from an authored rig profile to the local Python REBIRTH
// vehicle deployment/staging utility. Emits *instructions*, not a deployed mod.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public static class RebirthVehicleModSpecExporter
    {
        [Serializable]
        private sealed class Receipt
        {
            public int schemaVersion;
            public string buildTarget, bundleName, bundlePath, prefabName, prefabAssetPath;
            public string bundleSha256;
            public long bundleBytes;
            public bool prefabVerified;
        }

        // Every name is explicit: vanilla vehicle, entity class and placeable item
        // often do not have identical names. All must match the owner's installed XML.
        public static string ExportToFile(RebirthVehicleRigProfile profile,
            string donorVehicle, string donorEntityClass, string donorPlaceableItem,
            string targetVehicle, string targetEntityClass, string targetPlaceableItem,
            string unityReceiptPath, string outputJson, bool updateItemMeshfile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            ValidateName(donorVehicle); ValidateName(donorEntityClass);
            ValidateName(donorPlaceableItem); ValidateName(targetVehicle);
            ValidateName(targetEntityClass); ValidateName(targetPlaceableItem);
            if (string.IsNullOrWhiteSpace(unityReceiptPath) || !File.Exists(unityReceiptPath))
                throw new FileNotFoundException("The verified Unity build receipt is required.", unityReceiptPath);
            Receipt receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(unityReceiptPath));
            if (receipt == null || receipt.schemaVersion != 1 ||
                !receipt.prefabVerified || receipt.buildTarget != "StandaloneWindows64" ||
                string.IsNullOrWhiteSpace(receipt.bundlePath) || !File.Exists(receipt.bundlePath))
                throw new InvalidDataException("Unity receipt is incomplete or has no verified Windows64 prefab.");
            if (string.IsNullOrWhiteSpace(outputJson) || File.Exists(outputJson))
                throw new IOException("Choose a new mod spec path. Refusing to overwrite an existing file: " + outputJson);

            var parts = new SortedDictionary<string, object>(StringComparer.Ordinal);
            var seatsSeen = new HashSet<int>();
            foreach (VehicleSeatAnchor seat in profile.seats)
            {
                if (seat == null) continue;
                if (seat.index < 0 || seat.index > 5 || !seatsSeen.Add(seat.index))
                    throw new InvalidDataException("Invalid or duplicate seat index: " + seat.index);
                var properties = new SortedDictionary<string, object>(StringComparer.Ordinal);
                properties["position"] = RebirthVehicleXmlExporter.Vector(seat.position);
                properties["rotation"] = RebirthVehicleXmlExporter.Vector(seat.rotationEuler);
                if (!string.IsNullOrWhiteSpace(seat.poseId)) properties["pose"] = seat.poseId;
                if (seat.exits != null && seat.exits.Count > 0)
                {
                    var exits = new List<string>();
                    foreach (Vector3 exit in seat.exits) exits.Add(RebirthVehicleXmlExporter.Vector(exit));
                    properties["exit"] = string.Join(" ~ ", exits.ToArray());
                }
                if (!string.IsNullOrWhiteSpace(seat.requiredMod)) properties["mod"] = seat.requiredMod;
                if (!string.IsNullOrWhiteSpace(seat.modTransformPath)) properties["modT"] = seat.modTransformPath;
                parts.Add("seat" + seat.index.ToString(CultureInfo.InvariantCulture), properties);
            }
            if (!seatsSeen.Contains(0)) throw new InvalidDataException("Driver seat0 required.");
            // This explicitly tells the staging utility to remove donor seat2..seat5
            // when the custom vehicle has only seat0 and seat1.
            var supportedSeats = new List<int>(seatsSeen);
            supportedSeats.Sort();
            for (int i=0;i<supportedSeats.Count;i++)
                if (supportedSeats[i]!=i)
                    throw new InvalidDataException("Seat IDs must be contiguous from seat0; do not leave holes.");

            foreach (VehicleWheelLink wheel in profile.wheels)
            {
                if (wheel == null || string.IsNullOrWhiteSpace(wheel.partClass)) continue;
                var properties = new SortedDictionary<string, object>(StringComparer.Ordinal);
                if (!string.IsNullOrWhiteSpace(wheel.steerTransformPath))
                    properties["steerTransform"] = wheel.steerTransformPath;
                if (!string.IsNullOrWhiteSpace(wheel.tireTransformPath))
                    properties["tireTransform"] = wheel.tireTransformPath;
                if (properties.Count != 0) parts.Add(wheel.partClass, properties);
            }

            foreach (VehicleIkAnchor ik in profile.ik)
            {
                if (ik == null || (ik.partClass != "handlebars" && ik.partClass != "pedals")) continue;
                var properties = new SortedDictionary<string, object>(StringComparer.Ordinal);
                if (!string.IsNullOrWhiteSpace(ik.pivotPath)) properties["transform"] = ik.pivotPath;
                if (ik.partClass == "handlebars")
                {
                    if (ik.hasLeftHand)
                    {
                        properties["IKHandLPosition"] = RebirthVehicleXmlExporter.Vector(ik.leftHandPosition);
                        properties["IKHandLRotation"] = RebirthVehicleXmlExporter.Vector(ik.leftHandRotationEuler);
                    }
                    if (ik.hasRightHand)
                    {
                        properties["IKHandRPosition"] = RebirthVehicleXmlExporter.Vector(ik.rightHandPosition);
                        properties["IKHandRRotation"] = RebirthVehicleXmlExporter.Vector(ik.rightHandRotationEuler);
                    }
                }
                if (ik.partClass == "pedals")
                {
                    if (ik.hasLeftFoot)
                    {
                        properties["IKFootLPosition"] = RebirthVehicleXmlExporter.Vector(ik.leftFootPosition);
                        properties["IKFootLRotation"] = RebirthVehicleXmlExporter.Vector(ik.leftFootRotationEuler);
                    }
                    if (ik.hasRightFoot)
                    {
                        properties["IKFootRPosition"] = RebirthVehicleXmlExporter.Vector(ik.rightFootPosition);
                        properties["IKFootRRotation"] = RebirthVehicleXmlExporter.Vector(ik.rightFootRotationEuler);
                    }
                }
                if (properties.Count != 0) parts.Add(ik.partClass, properties);
            }

            var payload = new SortedDictionary<string, object>(StringComparer.Ordinal) {
                {"schemaVersion", 1},
                {"base", new SortedDictionary<string, object>(StringComparer.Ordinal) {
                    {"vehicle", donorVehicle}, {"entityClass", donorEntityClass}, {"placeableItem", donorPlaceableItem} }},
                {"target", new SortedDictionary<string, object>(StringComparer.Ordinal) {
                    {"vehicle", targetVehicle}, {"entityClass", targetEntityClass}, {"placeableItem", targetPlaceableItem} }},
                {"asset", new SortedDictionary<string, object>(StringComparer.Ordinal) {
                    {"bundlePath", Path.GetFullPath(receipt.bundlePath)},
                    {"prefabName", receipt.prefabName},
                    {"unityReceipt", Path.GetFullPath(unityReceiptPath)} }},
                {"updateItemMeshfile", updateItemMeshfile},
                {"supportedSeatIndices", supportedSeats},
                {"parts", parts}
            };
            string folder = Path.GetDirectoryName(Path.GetFullPath(outputJson));
            Directory.CreateDirectory(folder);
            File.WriteAllText(outputJson, Encode(payload) + "\n", new UTF8Encoding(false));
            return outputJson;
        }

        private static void ValidateName(string text)
        {
            if (string.IsNullOrWhiteSpace(text) ||
                !System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z][A-Za-z0-9_]{2,79}$"))
                throw new ArgumentException("Vehicle/item/entity name must contain letters, digits and underscores only: " + text);
        }

        // Supported only for the exact value types built above. This avoids adding
        // an external JSON library to older Unity 2022-based tooling.
        private static string Encode(object node)
        {
            if (node is bool) return (bool)node ? "true" : "false";
            if (node is int) return ((int)node).ToString(CultureInfo.InvariantCulture);
            if (node is List<int>)
            {
                var items = new List<string>();
                foreach (int i in (List<int>)node) items.Add(i.ToString(CultureInfo.InvariantCulture));
                return "["+string.Join(",",items.ToArray())+"]";
            }
            if (node is string) return Quote((string)node);
            var members = node as SortedDictionary<string, object>;
            if (members == null) throw new ArgumentException("Unexpected export JSON type");
            var entries = new List<string>();
            foreach (var pair in members) entries.Add(Quote(pair.Key) + ":" + Encode(pair.Value));
            return "{" + string.Join(",", entries.ToArray()) + "}";
        }

        private static string Quote(string value)
        {
            var s = new StringBuilder("\"");
            foreach (char ch in value ?? "")
            {
                if (ch == '\\' || ch == '"') { s.Append('\\'); s.Append(ch); }
                else if (ch == '\n') s.Append("\\n");
                else if (ch == '\r') s.Append("\\r");
                else if (ch == '\t') s.Append("\\t");
                else if (ch < 32) s.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                else s.Append(ch);
            }
            return s.Append('"').ToString();
        }
    }
}
