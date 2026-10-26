// REBIRTH Vehicle Studio — explicitly reviewed FBX visual-to-rig assembly.
// This is EDITOR ONLY. Nothing is attached to exported prefabs or the game.
// All coordinates are relative to the authoring workspace's Origin transform.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public static class RebirthVehicleAssemblyPlan
    {
        [Serializable]
        public sealed class Attachment
        {
            public string visualPath;                 // descendant of imported FBX scene root, excluding root
            public string targetPivotPath;            // exact Unity prefab path (usually Origin/...)
            public string role;                       // Wheel, Steering, Rotor, Propeller, Rudder, Pedal, Door, Custom
            public bool approved;                     // operator-reviewed mesh-to-joint mapping
            public bool pivotReviewed;                // operator-reviewed pivot position and rotation
            public bool allowUnreferencedTarget;      // required for joints not present in captured game XML
            public Vector3 pivotVehiclePosition;      // metres relative to Origin
            public Vector3 pivotVehicleRotationEuler; // degrees relative to Origin
            public string reviewerNotes;
        }
        [Serializable]
        public sealed class PivotAdjustment
        {
            // Steering parents, hinges and other non-mesh pivots must be reviewed too.
            public string targetPivotPath;
            public Vector3 pivotVehiclePosition, pivotVehicleRotationEuler;
            public bool approved, pivotReviewed, allowUnreferencedTarget;
            public string reviewerNotes;
        }
        [Serializable]
        public sealed class Plan
        {
            public int schemaVersion = 1;
            public string sourceFbxAsset;             // Assets/VehicleModels/...fbx
            public string profileAsset;               // Assets/.../Profile.asset
            public string targetVehicleName;
            public string sourceFbxSha256;             // Optional hash from v0.7 FBX audit.
            public string sourceImporterMetaSha256;    // Unity importer .meta may change scale/axis.
            public string sourceReferenceVehicleName; // Exact donor XML ID from capture.
            public Attachment[] attachments = new Attachment[0];
            public PivotAdjustment[] pivotAdjustments = new PivotAdjustment[0];
        }
        public sealed class Result
        {
            public readonly List<string> details = new List<string>();
            public int attached;
        }
        private sealed class Binding
        {
            public Attachment plan;
            public Transform visual;
            public Transform pivot;
            public string pivotPath;
        }
        private static readonly HashSet<string> Roles = new HashSet<string>(StringComparer.Ordinal) {
            "Wheel", "Steering", "Rotor", "Propeller", "Rudder", "Pedal", "Door", "Custom"
        };

        public static Plan Read(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                throw new FileNotFoundException("Reviewed assembly plan JSON is required.", absolutePath);
            if (new FileInfo(absolutePath).Length > 1024 * 1024)
                throw new InvalidDataException("Assembly plan exceeds 1 MB limit.");
            Plan plan;
            try {plan = JsonUtility.FromJson<Plan>(File.ReadAllText(absolutePath));}
            catch (Exception ex) {throw new InvalidDataException("Assembly plan JSON failed to parse.", ex);}
            if (plan == null || plan.schemaVersion != 1 || plan.attachments == null || plan.attachments.Length == 0 ||
                plan.attachments.Length > 256 ||
                (plan.pivotAdjustments != null && plan.pivotAdjustments.Length > 256))
                throw new InvalidDataException("Plan must have schemaVersion=1 and 1–256 reviewed attachments.");
            return plan;
        }

        // Preflight builds all bindings before ANY scene mutation. Allows Codex batchmode,
        // but never silently attaches arbitrary geometry or fabricates unreviewed pivots.
        public static Result Apply(RebirthVehicleRigWorkspace workspace, Plan plan,
            string sourceAsset, string profileAsset)
        {
            if (workspace == null || workspace.profile == null || workspace.importedVisuals == null ||
                workspace.origin == null || plan == null)
                throw new ArgumentException("Workspace, profile, FBX instance and reviewed plan are required.");
            if (plan.schemaVersion != 1 || plan.attachments == null || plan.attachments.Length == 0 ||
                plan.attachments.Length > 256 ||
                (plan.pivotAdjustments != null && plan.pivotAdjustments.Length > 256))
                throw new InvalidDataException("Invalid assembly plan schema or attachment count.");
            if (plan.sourceFbxAsset != sourceAsset || plan.profileAsset != profileAsset)
                throw new InvalidDataException("Assembly plan source FBX/profile must match this exact Unity batch job.");
            if (string.IsNullOrWhiteSpace(plan.targetVehicleName) ||
                plan.targetVehicleName != workspace.profile.targetVehicleName)
                throw new InvalidDataException("Assembly plan target vehicle identity differs from Unity profile.");
            if (!string.IsNullOrWhiteSpace(plan.sourceReferenceVehicleName) &&
                plan.sourceReferenceVehicleName != workspace.profile.sourceVehicleName)
                throw new InvalidDataException("Reviewed plan is bound to a different donor runtime vehicle.");
            if (!string.IsNullOrWhiteSpace(plan.sourceFbxSha256))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(plan.sourceFbxSha256,
                    "^[0-9a-f]{64}$") || !sourceAsset.StartsWith("Assets/", StringComparison.Ordinal))
                    throw new InvalidDataException("Unsafe or invalid pinned source FBX SHA/path.");
                string disk = Path.GetFullPath(Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    sourceAsset.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(disk)) throw new FileNotFoundException("Pinned source FBX not found",disk);
                string actual;
                using (var sha=SHA256.Create())
                using (var stream=File.OpenRead(disk))
                    actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != plan.sourceFbxSha256)
                    throw new InvalidDataException("Source FBX has changed since the reviewed geometry audit.");
            }

            if (!string.IsNullOrWhiteSpace(plan.sourceImporterMetaSha256))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(plan.sourceImporterMetaSha256,
                    "^[0-9a-f]{64}$") || !sourceAsset.StartsWith("Assets/", StringComparison.Ordinal))
                    throw new InvalidDataException("Invalid pinned importer .meta SHA/path.");
                string meta = Path.GetFullPath(Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    sourceAsset.Replace('/', Path.DirectorySeparatorChar))) + ".meta";
                if (!File.Exists(meta)) throw new FileNotFoundException("Pinned Unity importer .meta missing",meta);
                string actual;
                using (var sha=SHA256.Create())
                using (var stream=File.OpenRead(meta))
                    actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != plan.sourceImporterMetaSha256)
                    throw new InvalidDataException("Unity FBX importer metadata changed; mesh scale/axes may differ.");
            }
            var bindings = new List<Binding>();
            var seenVisuals = new HashSet<Transform>();
            var seenPivots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in plan.attachments)
            {
                if (step == null || !step.approved || !step.pivotReviewed ||
                    !Roles.Contains(step.role) || !IsFinite(step.pivotVehiclePosition) ||
                    !IsFinite(step.pivotVehicleRotationEuler))
                    throw new InvalidDataException("All attachment roles, mappings and pivots must be reviewed and have finite coordinates.");
                if (step.pivotVehiclePosition.magnitude > 10000f || step.pivotVehicleRotationEuler.magnitude > 10000f)
                    throw new InvalidDataException("Reviewed pivot magnitude out of bounds.");
                var visual = FindUniqueDescendant(workspace.importedVisuals.transform, step.visualPath);
                if (visual == null || visual == workspace.importedVisuals.transform)
                    throw new InvalidDataException("FBX source transform missing, ambiguous or root: " + step.visualPath);
                if (!IsSafePivotPath(step.targetPivotPath))
                    throw new InvalidDataException("Unsafe pivot path or not under Origin: " + step.targetPivotPath);
                var pivot = FindUniqueDescendant(workspace.transform, step.targetPivotPath);
                // Some vehicle families need custom rotor, propeller, rudder or door
                // joints not named in vanilla XML. An explicitly approved custom
                // path may be CREATED under this reserved authoring-only joint tree.
                // This does not imply the game will animate that joint.
                if (pivot == null && (!step.allowUnreferencedTarget || step.role == "Wheel" ||
                    !step.targetPivotPath.StartsWith("Origin/REBIRTH_CustomJoints/", StringComparison.Ordinal)))
                    throw new InvalidDataException("Missing XML-referenced pivot; custom pivots require reserved path and explicit override: " + step.targetPivotPath);
                if (pivot != null && !pivot.IsChildOf(workspace.origin))
                    throw new InvalidDataException("Pivot must resolve uniquely under Origin: " + step.targetPivotPath);
                if (pivot == visual || (pivot != null && pivot.IsChildOf(visual)))
                    throw new InvalidDataException("Visual attachment would create a cycle: " + step.visualPath);
                if (visual.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidDataException("Visual has no renderer: " + step.visualPath);
                if (visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                    throw new InvalidDataException("SkinnedMeshRenderer requires a separate character/skinned-mesh workflow: " + step.visualPath);
                if (!seenVisuals.Add(visual) || !seenPivots.Add(step.targetPivotPath))
                    throw new InvalidDataException("Duplicate visual or pivot target in assembly plan.");
                if (step.role == "Wheel" && !workspace.profile.wheels.Any(w => w != null &&
                    w.tireTransformPath == step.targetPivotPath))
                    throw new InvalidDataException("Wheel attachment must target an exact verified profile tireTransform: " + step.targetPivotPath);
                if (!step.allowUnreferencedTarget && !IsReferencedByProfile(workspace.profile, step.targetPivotPath))
                    throw new InvalidDataException("Pivot not referenced by installed XML/profile (use explicit allowUnreferencedTarget after review): " + step.targetPivotPath);
                // A fused FBX mesh cannot become an independently spinning wheel.
                if (step.role == "Wheel")
                {
                    int all = workspace.importedVisuals.GetComponentsInChildren<Renderer>(true).Length;
                    int selected = visual.GetComponentsInChildren<Renderer>(true).Length;
                    if (all > 0 && selected == all)
                        throw new InvalidDataException("Wheel visual contains the entire FBX render hierarchy; separate wheel meshes first.");
                }
                bindings.Add(new Binding {plan = step, visual = visual, pivot = pivot,
                    pivotPath = step.targetPivotPath});
            }
            for (int a=0;a<bindings.Count;a++) for (int b=a+1;b<bindings.Count;b++)
            {
                if (bindings[a].visual.IsChildOf(bindings[b].visual) ||
                    bindings[b].visual.IsChildOf(bindings[a].visual))
                    throw new InvalidDataException("Overlapping FBX visual subtrees cannot be assembled twice.");
            }
            // Explicitly reviewed steering/hinge pivots that have NO attached mesh.
            // Without this, wheel tire joints may spin correctly while steering around
            // (0,0,0) because the parent steering pivots were never positioned.
            var extraPivots = new List<PivotAdjustment>();
            var extraPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var adjustment in plan.pivotAdjustments ?? new PivotAdjustment[0])
            {
                if (adjustment == null || !adjustment.approved || !adjustment.pivotReviewed ||
                    !IsSafePivotPath(adjustment.targetPivotPath) ||
                    !IsFinite(adjustment.pivotVehiclePosition) || !IsFinite(adjustment.pivotVehicleRotationEuler) ||
                    adjustment.pivotVehiclePosition.magnitude > 10000f ||
                    adjustment.pivotVehicleRotationEuler.magnitude > 10000f ||
                    !extraPaths.Add(adjustment.targetPivotPath))
                    throw new InvalidDataException("Invalid, duplicate or unapproved extra steering/hinge pivot.");
                Transform node = FindUniqueDescendant(workspace.transform, adjustment.targetPivotPath);
                if (node == workspace.origin || (node != null && !node.IsChildOf(workspace.origin)))
                    throw new InvalidDataException("Pivot-only adjustment must be a child under Origin.");
                if (node == null && (!adjustment.allowUnreferencedTarget ||
                    !adjustment.targetPivotPath.StartsWith("Origin/REBIRTH_CustomJoints/", StringComparison.Ordinal)))
                    throw new InvalidDataException("Referenced parent pivot is missing from imported hierarchy.");
                if (!adjustment.allowUnreferencedTarget &&
                    !IsReferencedByProfile(workspace.profile, adjustment.targetPivotPath))
                    throw new InvalidDataException("Pivot-only adjustment is not a profile-referenced joint.");
                foreach (var binding in bindings)
                    if (binding.pivotPath == adjustment.targetPivotPath &&
                        (Vector3.Distance(binding.plan.pivotVehiclePosition, adjustment.pivotVehiclePosition) > 0.001f ||
                         Vector3.Distance(binding.plan.pivotVehicleRotationEuler, adjustment.pivotVehicleRotationEuler) > 0.01f))
                        throw new InvalidDataException("Attachment and pivot-only position disagree on: " +
                            adjustment.targetPivotPath);
                extraPivots.Add(adjustment);
            }
            foreach (var binding in bindings)
            {
                if (binding.plan.role != "Wheel") continue;
                foreach (var wheel in workspace.profile.wheels)
                {
                    if (wheel == null || wheel.tireTransformPath != binding.pivotPath ||
                        string.IsNullOrWhiteSpace(wheel.steerTransformPath) ||
                        !binding.pivotPath.StartsWith(wheel.steerTransformPath + "/", StringComparison.Ordinal))
                        continue;
                    bool placeholderParent = workspace.profile.links.Any(x => x != null &&
                        x.transformPath == wheel.steerTransformPath && x.authoredPlaceholder);
                    if (placeholderParent && !extraPaths.Contains(wheel.steerTransformPath))
                        throw new InvalidDataException("Wheel steering parent pivot was authored as an uncalibrated " +
                            "placeholder. Add a reviewed pivotAdjustment for " + wheel.steerTransformPath);
                }
            }
            // The ORIGINAL source FBX is untouched. Unpacking is confined to this new
            // scene instance; do it once after all preflight gates succeed.
            GameObject rootInstance = PrefabUtility.GetOutermostPrefabInstanceRoot(workspace.importedVisuals);
            if (rootInstance != null)
                PrefabUtility.UnpackPrefabInstance(rootInstance,
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var result = new Result();
            // Parent-first and world-coordinate placement: nested wheel/steering
            // joints are resolved in Origin coordinates after their ancestors move.
            extraPivots.Sort((a, b) => a.targetPivotPath.Split('/').Length.CompareTo(
                b.targetPivotPath.Split('/').Length));
            foreach (var step in extraPivots)
            {
                Transform pivot = FindUniqueDescendant(workspace.transform, step.targetPivotPath);
                if (pivot == null)
                {
                    bool created;
                    pivot = RebirthVehicleRigBuilder.EnsurePath(workspace.transform,
                        step.targetPivotPath, out created);
                    if (!created) throw new InvalidOperationException("Approved custom pivot not created.");
                }
                Undo.RecordObject(pivot, "Calibrate reviewed steering/hinge pivot");
                pivot.position = workspace.origin.TransformPoint(step.pivotVehiclePosition);
                pivot.rotation = workspace.origin.rotation * Quaternion.Euler(step.pivotVehicleRotationEuler);
                result.details.Add("Reviewed pivot: " + step.targetPivotPath);
            }
            foreach (var binding in bindings)
            {
                var step = binding.plan;
                Transform pivot = binding.pivot;
                if (pivot == null)
                {
                    bool created;
                    pivot = RebirthVehicleRigBuilder.EnsurePath(workspace.transform,binding.pivotPath,out created);
                    if (!created) throw new InvalidOperationException("Expected reviewed custom pivot to be created: " + binding.pivotPath);
                }
                Undo.RecordObject(pivot, "Set reviewed vehicle pivot");
                pivot.position = workspace.origin.TransformPoint(step.pivotVehiclePosition);
                pivot.rotation = workspace.origin.rotation * Quaternion.Euler(step.pivotVehicleRotationEuler);
                Undo.SetTransformParent(binding.visual, pivot, "Attach reviewed vehicle visual");
                result.attached++;
                result.details.Add(step.role + " " + step.visualPath + " -> " + step.targetPivotPath +
                    " (reviewed pivot; requires live 7DTD motion/physics verification)");
            }
            // Clear warning flags only on paths whose pivots were explicitly
            // reviewed. Empty unrelated donor joints remain uncalibrated.
            var calibrated = new HashSet<string>(extraPaths, StringComparer.Ordinal);
            foreach (var binding in bindings) calibrated.Add(binding.pivotPath);
            foreach (var link in workspace.profile.links)
                if (link != null && calibrated.Contains(link.transformPath))
                    link.authoredPlaceholder = false;
            EditorUtility.SetDirty(workspace.profile);
            return result;
        }

        private static bool IsSafePivotPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Origin/",StringComparison.Ordinal) ||
                path.Contains("\\") || path.Length > 512) return false;
            foreach(var token in path.Split('/'))
            {
                if (string.IsNullOrWhiteSpace(token) || token == "." || token == ".." ||
                    token.IndexOfAny(new [] {'<','>','[',']',':'}) >= 0) return false;
            }
            return true;
        }
        private static bool IsReferencedByProfile(RebirthVehicleRigProfile profile, string path)
        {
            if (profile.links.Any(x => x != null && x.transformPath == path)) return true;
            if (profile.wheels.Any(x => x != null && (x.tireTransformPath == path || x.steerTransformPath == path))) return true;
            if (profile.ik.Any(x => x != null && x.pivotPath == path)) return true;
            return false;
        }
        public static Transform FindUniqueDescendant(Transform root, string relativePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(relativePath) ||
                relativePath.StartsWith("/", StringComparison.Ordinal) || relativePath.Contains("\\")) return null;
            Transform current = root;
            foreach (string token in relativePath.Split('/'))
            {
                if (string.IsNullOrWhiteSpace(token) || token == "." || token == "..") return null;
                Transform found = null;
                for (int i=0;i<current.childCount;i++)
                {
                    Transform child = current.GetChild(i);
                    if (child.name != token) continue;
                    if (found != null) return null; // duplicate sibling names are ambiguous
                    found = child;
                }
                if (found == null) return null;
                current = found;
            }
            return current;
        }
        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
