// Review-only by default. Any reparenting requires an explicit dialog for each
// individual wheel and uses the existing Undo-capable authoring operation.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public sealed class RebirthVehicleWheelMapperWindow : EditorWindow
    {
        private RebirthVehicleRigWorkspace workspace;
        private List<RebirthVehicleWheelMapper.Proposal> proposals = new List<RebirthVehicleWheelMapper.Proposal>();
        private Vector2 scroll;

        [MenuItem("Tools/REBIRTH/Conservative FBX Wheel Mapper")]
        public static void Open() { GetWindow<RebirthVehicleWheelMapperWindow>("FBX Wheel Mapper"); }
        private void ExportUnapprovedPlan()
        {
            if (workspace == null || workspace.profile == null || workspace.importedVisuals == null ||
                workspace.origin == null)
                throw new InvalidOperationException("Choose a source FBX scene workspace and profile.");
            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(workspace.importedVisuals);
            string sourceFbx = AssetDatabase.GetAssetPath(prefabSource);
            string profilePath = AssetDatabase.GetAssetPath(workspace.profile);
            if (string.IsNullOrEmpty(sourceFbx) || string.IsNullOrEmpty(profilePath))
                throw new InvalidOperationException("Imported FBX/profile must both be saved Unity assets.");
            var bindings = new List<RebirthVehicleAssemblyPlan.Attachment>();
            foreach (var p in proposals)
            {
                if (p == null || p.outcome != "candidate" || p.selectedMesh == null) continue;
                string relativePath = RelativePath(workspace.importedVisuals.transform, p.selectedMesh);
                if (string.IsNullOrEmpty(relativePath)) continue;
                Renderer[] renderers = p.selectedMesh.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                bindings.Add(new RebirthVehicleAssemblyPlan.Attachment {
                    visualPath = relativePath,
                    targetPivotPath = p.tirePivotPath,
                    role = "Wheel",
                    approved = false,
                    pivotReviewed = false,
                    allowUnreferencedTarget = false,
                    pivotVehiclePosition = workspace.origin.InverseTransformPoint(bounds.center),
                    pivotVehicleRotationEuler = Vector3.zero,
                    reviewerNotes = "UNREVIEWED: mesh-bounds center is only a candidate; inspect true axle pivot and spin axis."
                });
            }
            if (bindings.Count == 0)
                throw new InvalidOperationException("No unambiguous proposals to export. Review FBX wheel grouping manually.");
            var plan = new RebirthVehicleAssemblyPlan.Plan {
                sourceFbxAsset = sourceFbx,
                profileAsset = profilePath,
                targetVehicleName = workspace.profile.targetVehicleName,
                attachments = bindings.ToArray()
            };
            string output = EditorUtility.SaveFilePanel("Save unapproved vehicle assembly JSON", "", "assembly_REVIEW_REQUIRED.json", "json");
            if (string.IsNullOrEmpty(output)) return;
            if (File.Exists(output)) throw new IOException("Refusing to overwrite an existing plan: " + output);
            File.WriteAllText(output, JsonUtility.ToJson(plan, true), new System.Text.UTF8Encoding(false));
            EditorUtility.DisplayDialog("Draft plan exported",
                "All attachment approvals and pivotReviewed flags are FALSE. Inspect each source mesh, axle center, " +
                "pivot axis and target reference in Unity before setting them TRUE. The plan does not validate vehicle behavior.", "OK");
        }
        private static string RelativePath(Transform root, Transform item)
        {
            var parts = new List<string>();
            for (var p = item; p != null && p != root; p = p.parent) parts.Add(p.name);
            if (parts.Count == 0 || !item.IsChildOf(root)) return null;
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("FBX Wheel Mapper — manual approval", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Only UNIQUE explicit wheel-corner names in both FBX and 7DTD XML paths can become proposals. " +
                "This does NOT assume wheel0=front-left, classify by scene x/z, separate fused meshes, " +
                "or verify physics. The source FBX asset is never edited.", MessageType.Warning);
            workspace = (RebirthVehicleRigWorkspace)EditorGUILayout.ObjectField(
                "Scene workspace", workspace, typeof(RebirthVehicleRigWorkspace), true);
            if (GUILayout.Button("Scan source FBX (no changes)"))
            {
                try { proposals = RebirthVehicleWheelMapper.Discover(workspace); }
                catch (Exception ex) { EditorUtility.DisplayDialog("Cannot scan", ex.Message, "OK"); }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var p in proposals)
            {
                if (p == null) continue;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(p.xmlWheelClass + " / " + (p.wheelCorner ?? "unknown"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Pivot", p.tirePivotPath ?? "missing");
                if (!string.IsNullOrEmpty(p.sourceMeshPath)) EditorGUILayout.LabelField("Visual", p.sourceMeshPath);
                EditorGUILayout.HelpBox(p.explanation, p.outcome == "candidate" ? MessageType.Info : MessageType.Warning);
                using (new EditorGUI.DisabledScope(p.outcome != "candidate" || p.selectedMesh == null))
                {
                    if (GUILayout.Button("Approve and attach THIS wheel mesh"))
                    {
                        bool accepted = EditorUtility.DisplayDialog("Apply ONE wheel mapping?",
                            "This will unpack the SCENE FBX instance as needed and reparent " +
                            p.sourceMeshPath + " to " + p.tirePivotPath +
                            ". Verify the joint's axis/center and actual in-game rotation afterwards. " +
                            "Original imported FBX asset is unchanged.", "Attach", "Cancel");
                        if (!accepted) continue;
                        try
                        {
                            RebirthVehicleRigBuilder.AttachVisualToJoint(workspace, p.selectedMesh, p.tirePivotPath);
                            proposals = new List<RebirthVehicleWheelMapper.Proposal>();
                            SceneView.RepaintAll();
                            break;
                        }
                        catch (Exception ex) { EditorUtility.DisplayDialog("Wheel attach failed", ex.Message, "OK"); }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            if (proposals.Count > 0 && GUILayout.Button("Export UNAPPROVED batch assembly plan (JSON)"))
            {
                try { ExportUnapprovedPlan(); }
                catch (Exception ex) { EditorUtility.DisplayDialog("Assembly plan export failed", ex.Message, "OK"); }
            }
            if (proposals.Count > 0)
                EditorGUILayout.HelpBox("After approval, RE-SCAN to review remaining wheels. " +
                    "Do not infer correct colliders, axes, suspension or steering behavior from name matches.", MessageType.Warning);
        }
    }
}
