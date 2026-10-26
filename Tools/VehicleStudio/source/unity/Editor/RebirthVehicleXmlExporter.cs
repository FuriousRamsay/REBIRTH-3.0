using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public static class RebirthVehicleXmlExporter
    {
        public sealed class ExportResult
        {
            public string xml;
            public List<string> warnings = new List<string>();
            public int setCount, appendCount;
        }

        // Output a patch, not a replacement vehicles.xml. The target vehicle MUST
        // already exist in the provided effective/baseline XML (vanilla or a modlet).
        public static ExportResult Generate(RebirthVehicleRigProfile profile, string sourceVehiclesXml,
            string targetVehicleName)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            if (string.IsNullOrWhiteSpace(sourceVehiclesXml))
                throw new ArgumentException("Source vehicles.xml text required.");
            if (string.IsNullOrEmpty(targetVehicleName) ||
                !Regex.IsMatch(targetVehicleName, @"^[A-Za-z0-9_.-]+$"))
                throw new ArgumentException("Target vehicle name must be an XML-safe ID.");
            XmlDocument original = LoadSafeXml(sourceVehiclesXml);
            var targetPath = "/vehicles/vehicle[@name='" + targetVehicleName + "']";
            XmlElement vehicle = original.SelectSingleNode(targetPath) as XmlElement;
            if (vehicle == null)
                throw new InvalidDataException("Target vehicle does not exist in supplied XML: " + targetVehicleName +
                    ". For a NEW vehicle, first provide a complete vehicle definition from your mod.");

            var patch = new XmlDocument();
            XmlElement configs = patch.CreateElement("configs");
            patch.AppendChild(configs);
            var result = new ExportResult();

            var seatIds = new HashSet<int>();
            foreach (var seat in profile.seats)
            {
                if (seat == null) continue;
                if (seat.index < 0 || seat.index > 5)
                    throw new InvalidDataException("Only documented seat0 through seat5 are supported.");
                if (!seatIds.Add(seat.index))
                    throw new InvalidDataException("Duplicate seat index: " + seat.index);
                if (string.IsNullOrWhiteSpace(seat.poseId))
                    result.warnings.Add("seat" + seat.index + " has no animation pose ID; game may use an unwanted pose.");
                var values = new Dictionary<string, string> {
                    {"position", Vector(seat.position)},
                    {"rotation", Vector(seat.rotationEuler)}
                };
                if (!string.IsNullOrWhiteSpace(seat.poseId)) values["pose"] = seat.poseId;
                if (seat.exits.Count > 0)
                {
                    var attempts = new List<string>();
                    foreach (Vector3 p in seat.exits) attempts.Add(Vector(p));
                    values["exit"] = string.Join(" ~ ", attempts.ToArray());
                }
                if (!string.IsNullOrWhiteSpace(seat.requiredMod)) values["mod"] = seat.requiredMod;
                if (!string.IsNullOrWhiteSpace(seat.modTransformPath)) values["modT"] = seat.modTransformPath;
                ChangePart(original, vehicle, patch, configs, targetPath,
                    "seat" + seat.index, "Seat", values, true, result);
            }
            if (!seatIds.Contains(0)) result.warnings.Add("Driver seat0 not present in profile.");

            foreach (var ik in profile.ik)
            {
                if (ik == null || (ik.partClass != "handlebars" && ik.partClass != "pedals"))
                    continue;
                var values = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(ik.pivotPath)) values["transform"] = ik.pivotPath;
                if (ik.hasLeftHand) { values["IKHandLPosition"] = Vector(ik.leftHandPosition);
                    values["IKHandLRotation"] = Vector(ik.leftHandRotationEuler); }
                if (ik.hasRightHand) { values["IKHandRPosition"] = Vector(ik.rightHandPosition);
                    values["IKHandRRotation"] = Vector(ik.rightHandRotationEuler); }
                if (ik.partClass == "pedals")
                {
                    if (ik.hasLeftFoot) { values["IKFootLPosition"] = Vector(ik.leftFootPosition);
                        values["IKFootLRotation"] = Vector(ik.leftFootRotationEuler); }
                    if (ik.hasRightFoot) { values["IKFootRPosition"] = Vector(ik.rightFootPosition);
                        values["IKFootRRotation"] = Vector(ik.rightFootRotationEuler); }
                }
                else if (ik.hasLeftFoot || ik.hasRightFoot)
                    result.warnings.Add("Foot IK on non-pedals part is not known to be supported; skipped.");
                ChangePart(original, vehicle, patch, configs, targetPath,
                    ik.partClass, ik.partType, values, false, result);
            }

            foreach (var wheel in profile.wheels)
            {
                if (wheel == null || string.IsNullOrWhiteSpace(wheel.partClass)) continue;
                var values = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(wheel.steerTransformPath))
                    values["steerTransform"] = wheel.steerTransformPath;
                if (!string.IsNullOrWhiteSpace(wheel.tireTransformPath))
                    values["tireTransform"] = wheel.tireTransformPath;
                ChangePart(original, vehicle, patch, configs, targetPath,
                    wheel.partClass, null, values, false, result);
            }
            if (profile.sourceXmlStatus != "ok")
                result.warnings.Add("Capture's XML binding was not verified at source; compare against game.");
            result.warnings.Add("XML patch is NOT a full custom vehicle definition or proof of in-game compatibility.");
            using (var sw = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = XmlWriter.Create(sw, new XmlWriterSettings {
                Indent=true, IndentChars="  ", NewLineChars="\n", OmitXmlDeclaration=true }))
            {
                patch.Save(writer); writer.Flush(); result.xml=sw.ToString();
            }
            return result;
        }

        public static ExportResult ExportToFile(RebirthVehicleRigProfile profile, string sourceXmlFile,
            string targetVehicleName, string outputFile)
        {
            if (Path.GetFullPath(sourceXmlFile).Equals(Path.GetFullPath(outputFile),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to overwrite source vehicles.xml.");
            if (File.Exists(outputFile))
                throw new IOException("Refusing to overwrite an existing XML patch: " + outputFile);
            ExportResult result = Generate(profile, File.ReadAllText(sourceXmlFile), targetVehicleName);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile)));
            File.WriteAllText(outputFile, result.xml, new UTF8Encoding(false));
            return result;
        }

        private static void ChangePart(XmlDocument original, XmlElement vehicle, XmlDocument patch,
            XmlElement configs, string vehiclePath, string className, string classValue,
            Dictionary<string, string> properties, bool allowMissingClass, ExportResult result)
        {
            if (!Regex.IsMatch(className, @"^[A-Za-z0-9_.-]+$"))
                throw new InvalidDataException("Unsafe component class name: " + className);
            if (properties.Count == 0) return;
            string path = vehiclePath + "/property[@class='" + className + "']";
            XmlElement originalPart = vehicle.SelectSingleNode("property[@class='" + className + "']") as XmlElement;
            if (originalPart == null)
            {
                if (!allowMissingClass)
                {
                    result.warnings.Add("Skipping missing component " + className + ". Do not invent an unverified wheel/IK component.");
                    return;
                }
                // A new seat can be added to an existing vehicle.
                var append = patch.CreateElement("append");
                append.SetAttribute("xpath", vehiclePath);
                XmlElement part = patch.CreateElement("property");
                part.SetAttribute("class", className);
                AppendXmlProperty(patch, part, "class", classValue);
                foreach (var kv in properties) AppendXmlProperty(patch, part, kv.Key, kv.Value);
                append.AppendChild(part); configs.AppendChild(append); result.appendCount++;
                return;
            }
            foreach (var kv in properties)
            {
                // Do not silently emit invalid XPath value properties.
                if (!Regex.IsMatch(kv.Key, @"^[A-Za-z0-9_]+$"))
                    throw new InvalidDataException("Unsafe property name: " + kv.Key);
                XmlNode srcProp = originalPart.SelectSingleNode("property[@name='" + kv.Key + "']");
                if (srcProp != null && srcProp.Attributes != null && srcProp.Attributes["value"] != null)
                {
                    var set = patch.CreateElement("set");
                    set.SetAttribute("xpath", path + "/property[@name='" + kv.Key + "']/@value");
                    set.InnerText = kv.Value; configs.AppendChild(set); result.setCount++;
                }
                else if (srcProp == null)
                {
                    var append = patch.CreateElement("append");
                    append.SetAttribute("xpath", path);
                    AppendXmlProperty(patch, append, kv.Key, kv.Value);
                    configs.AppendChild(append); result.appendCount++;
                }
                else
                {
                    result.warnings.Add("Existing " + className + "." + kv.Key +
                        " has no value attribute; manual inspection required.");
                }
            }
        }
        private static void AppendXmlProperty(XmlDocument doc, XmlElement parent, string key, string value)
        {
            var p = doc.CreateElement("property");
            p.SetAttribute("name", key);
            p.SetAttribute("value", value ?? "");
            parent.AppendChild(p);
        }
        private static XmlDocument LoadSafeXml(string xml)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            var doc = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(xml), settings)) doc.Load(reader);
            return doc;
        }
        public static string Vector(Vector3 v)
        {
            if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z))
                throw new InvalidDataException("Vector contains NaN or Infinity.");
            return Scalar(v.x) + "," + Scalar(v.y) + "," + Scalar(v.z);
        }
        private static bool Finite(float f) { return !float.IsInfinity(f) && !float.IsNaN(f); }
        private static string Scalar(float f) { return f.ToString("0.######", CultureInfo.InvariantCulture); }
    }
}
