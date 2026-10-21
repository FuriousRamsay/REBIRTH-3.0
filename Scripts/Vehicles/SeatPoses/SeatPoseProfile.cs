using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Rebirth.Vehicles.Authoring
{
    [Serializable] public sealed class SeatPoseTransform
    {
        public string name;
        public Vector3 position, rotation;
        public float weight = 1;
        // Optional schema-1 extension: preserves an authored offset relative to
        // a moving native target parent even when entering while steering.
        public string nativeParentPath;
        public Vector3 nativeParentPosition, nativeParentRotation;
    }
    [Serializable] public sealed class SeatPoseBoneOffset
    {
        public string path;
        public Vector3 rotation;
    }
    [Serializable] public sealed class SeatPoseHand
    {
        public float close, spread;
        public float[] curl = new float[5]; // thumb, index, middle, ring, little
    }
    [Serializable] public sealed class SeatPoseProfile
    {
        static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            MaxDepth = 64,
            Converters = new JsonConverter[] { new SeatPoseVectorConverter() }
        };
        public int schemaVersion = 1;
        public string vehicle, rig, name;
        public int seat, basePose;
        public string coordinateFrame = "vehicle-model-local";
        public Vector3 anchorPosition, anchorRotation;
        public float torsoLean;
        public SeatPoseTransform[] targets = new SeatPoseTransform[0];
        public SeatPoseBoneOffset[] bones = new SeatPoseBoneOffset[0];
        public SeatPoseHand leftHand = new SeatPoseHand(), rightHand = new SeatPoseHand();

        public void Validate()
        {
            if (schemaVersion != 1 || coordinateFrame != "vehicle-model-local") throw new InvalidDataException("Unsupported seat profile schema or frame.");
            if (string.IsNullOrWhiteSpace(vehicle) || string.IsNullOrWhiteSpace(rig) || seat < 0 || basePose < 0) throw new InvalidDataException("Missing vehicle, rig or seat identity.");
            Check(anchorPosition); Check(anchorRotation);
            Finite(torsoLean); if (torsoLean < -80 || torsoLean > 80) throw new InvalidDataException("Torso lean outside -80..80 degrees.");
            if (targets == null || bones == null) throw new InvalidDataException("Missing profile arrays.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in targets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.name) || !names.Add(target.name)) throw new InvalidDataException("Missing or duplicate target.");
                if(Array.IndexOf(new[]{"IKHandL","IKHandR","IKFootL","IKFootR","IKElbowL","IKElbowR","IKKneeL","IKKneeR"},target.name)<0)throw new InvalidDataException("Unsupported IK target: "+target.name);
                Check(target.position); Check(target.rotation); Unit(target.weight);
                Check(target.nativeParentPosition); Check(target.nativeParentRotation);
                if (target.nativeParentPath != null && (target.nativeParentPath.StartsWith("/") || target.nativeParentPath.Contains("..") || target.nativeParentPath.Contains("\\")))
                    throw new InvalidDataException("Invalid native target parent path.");
            }
            names.Clear();
            foreach (var bone in bones)
            {
                if (bone == null || string.IsNullOrWhiteSpace(bone.path) || !names.Add(bone.path)) throw new InvalidDataException("Missing or duplicate bone path.");
                Check(bone.rotation);
            }
            foreach (var hand in new[] { leftHand, rightHand })
            {
                if (hand == null || hand.curl == null || hand.curl.Length != 5) throw new InvalidDataException("Five finger controls required.");
                Signed(hand.close); Signed(hand.spread); foreach (float value in hand.curl) Signed(value);
            }
        }
        static void Check(Vector3 v) { Finite(v.x); Finite(v.y); Finite(v.z); }
        static void Finite(float v) { if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("Non-finite profile value."); }
        static void Unit(float v) { Finite(v); if (v < 0 || v > 1) throw new InvalidDataException("Control weight outside 0..1."); }
        static void Signed(float v) { Finite(v); if (v < -1 || v > 1) throw new InvalidDataException("Finger control outside -1..1."); }
        public string Serialize() { Validate(); return JsonConvert.SerializeObject(this, Formatting.Indented, JsonSettings); }
        public static SeatPoseProfile Parse(string json)
        {
            // Unity JsonUtility silently omits nested mod-assembly types in the player.
            // Reject those incomplete old documents instead of accepting data loss.
            var document = JObject.Parse(json);
            foreach (string field in new[] { "targets", "bones", "leftHand", "rightHand" })
                if (document[field] == null) throw new InvalidDataException("Incomplete seat profile: " + field);
            var profile = JsonConvert.DeserializeObject<SeatPoseProfile>(json, JsonSettings);
            if (profile == null) throw new InvalidDataException("Empty seat profile.");
            profile.Validate(); return profile;
        }
        public void MirrorTarget(string sourceName)
        {
            var source=Array.Find(targets,t=>t.name==sourceName);
            if(source==null||(!sourceName.EndsWith("L")&&!sourceName.EndsWith("R")))throw new InvalidDataException("Select a left/right limb target.");
            string destinationName=sourceName.Substring(0,sourceName.Length-1)+(sourceName.EndsWith("L")?"R":"L");
            var destination=Array.Find(targets,t=>t.name==destinationName);
            if(destination==null)throw new InvalidDataException("Opposite limb target missing.");
            var frame=Quaternion.Euler(anchorRotation);
            var local=Quaternion.Inverse(frame)*(source.position-anchorPosition);local.x=-local.x;
            var rotation=Quaternion.Inverse(frame)*Quaternion.Euler(source.rotation);
            rotation=new Quaternion(rotation.x,-rotation.y,-rotation.z,rotation.w);
            destination.position=anchorPosition+frame*local;destination.rotation=(frame*rotation).eulerAngles;destination.weight=source.weight;
        }
    }

    internal sealed class SeatPoseVectorConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type == typeof(Vector3);
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var v = (Vector3)value;
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(v.x);
            writer.WritePropertyName("y"); writer.WriteValue(v.y);
            writer.WritePropertyName("z"); writer.WriteValue(v.z);
            writer.WriteEndObject();
        }
        public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
        {
            var value = JObject.Load(reader);
            if (value.Count != 3 || value["x"] == null || value["y"] == null || value["z"] == null) throw new InvalidDataException("Invalid vector.");
            return new Vector3(value["x"].Value<float>(), value["y"].Value<float>(), value["z"].Value<float>());
        }
    }

    internal sealed class SeatPoseHistory
    {
        readonly List<string> snapshots = new List<string>();
        int cursor = -1;
        public void Commit(SeatPoseProfile profile)
        {
            string json = profile.Serialize();
            if (cursor >= 0 && snapshots[cursor] == json) return;
            if (cursor + 1 < snapshots.Count) snapshots.RemoveRange(cursor + 1, snapshots.Count - cursor - 1);
            snapshots.Add(json);
            if (snapshots.Count > 100) snapshots.RemoveAt(0);
            cursor = snapshots.Count - 1;
        }
        public SeatPoseProfile Undo() => cursor > 0 ? SeatPoseProfile.Parse(snapshots[--cursor]) : null;
        public SeatPoseProfile Redo() => cursor + 1 < snapshots.Count ? SeatPoseProfile.Parse(snapshots[++cursor]) : null;
    }

    internal static class SeatPoseProfileStore
    {
        public static void Save(string directory, string name, SeatPoseProfile profile)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..") throw new InvalidDataException("Invalid profile filename.");
            string json = profile.Serialize(); // Validate before touching an existing file.
            Directory.CreateDirectory(directory);
            string path = Path.Combine(Path.GetFullPath(directory), name + ".json");
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json);
                SeatPoseProfile.Parse(File.ReadAllText(temporary));
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static SeatPoseProfile Load(string path) => SeatPoseProfile.Parse(File.ReadAllText(path));
    }
}
