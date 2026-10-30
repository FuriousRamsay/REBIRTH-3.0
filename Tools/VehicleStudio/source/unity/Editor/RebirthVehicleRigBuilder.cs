using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public static class RebirthVehicleRigBuilder
    {
        // Prefab/FBX assets only. Keep the imported FBX intact: all generated joints
        // belong to a NEW scene wrapper, never to the FBX source file.
        public static RebirthVehicleRigWorkspace CreateWorkspace(GameObject sourceFbx,
            RebirthVehicleRigProfile profile)
        {
            if (sourceFbx == null || profile == null)
                throw new ArgumentException("An imported FBX/prefab asset and rig profile are required.");
            string sourcePath = AssetDatabase.GetAssetPath(sourceFbx);
            if (string.IsNullOrEmpty(sourcePath) || !sourcePath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Choose a model/prefab asset in the Project view, not a scene object.");
            GameObject root = new GameObject("REBIRTH_" + sourceFbx.name);
            Undo.RegisterCreatedObjectUndo(root, "Create 7DTD vehicle workspace");
            var origin = new GameObject("Origin");
            Undo.RegisterCreatedObjectUndo(origin, "Create vehicle Origin");
            Undo.SetTransformParent(origin.transform, root.transform, "Add Origin");
            origin.transform.localPosition = Vector3.zero;
            origin.transform.localRotation = Quaternion.identity;
            origin.transform.localScale = Vector3.one;

            var modelInstance = PrefabUtility.InstantiatePrefab(sourceFbx) as GameObject;
            if (modelInstance == null)
                throw new InvalidOperationException("The selected model could not be instantiated as a prefab/FBX.");
            Undo.RegisterCreatedObjectUndo(modelInstance, "Instantiate FBX visuals");
            Undo.SetTransformParent(modelInstance.transform, origin.transform, "Parent visuals under Origin");
            // Preserve the importer's mesh hierarchy. Ensure wrapper is at identity.
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            var workspace = Undo.AddComponent<RebirthVehicleRigWorkspace>(root);
            workspace.profile = profile;
            workspace.origin = origin.transform;
            workspace.importedVisuals = modelInstance;
            EnsureReferenceHierarchy(workspace);
            Selection.activeGameObject = root;
            return workspace;
        }

        public static int EnsureReferenceHierarchy(RebirthVehicleRigWorkspace workspace)
        {
            if (workspace == null || workspace.profile == null)
                throw new ArgumentException("Select a workspace with a rig profile.");
            int count = 0;
            foreach (var link in workspace.profile.links)
            {
                if (link == null || string.IsNullOrWhiteSpace(link.transformPath)) continue;
                bool created;
                EnsurePath(workspace.transform, link.transformPath, out created);
                if (created)
                {
                    link.authoredPlaceholder = true;
                    count++;
                }
            }
            if (count > 0) EditorUtility.SetDirty(workspace.profile);
            return count;
        }

        // XML path is relative to the actual prefab root (e.g. Origin/steering_joint).
        // Never silently fall back to the model root on an unresolved IK path.
        public static Transform FindPath(Transform root, string xmlPath)
        {
            string[] parts = SplitPath(root, xmlPath);
            if (parts == null) return null;
            Transform current = root;
            foreach (string segment in parts)
            {
                current = FindImmediateChild(current, segment);
                if (current == null) return null;
            }
            return current;
        }
        public static Transform EnsurePath(Transform root, string xmlPath, out bool created)
        {
            created = false;
            string[] parts = SplitPath(root, xmlPath);
            if (parts == null) throw new ArgumentException("Unsafe/invalid Unity reference path: " + xmlPath);
            Transform current = root;
            foreach (string segment in parts)
            {
                Transform child = FindImmediateChild(current, segment);
                if (child == null)
                {
                    GameObject joint = new GameObject(segment);
                    Undo.RegisterCreatedObjectUndo(joint, "Create reference joint " + segment);
                    Undo.SetTransformParent(joint.transform, current, "Parent reference joint");
                    joint.transform.localPosition = Vector3.zero;
                    joint.transform.localRotation = Quaternion.identity;
                    joint.transform.localScale = Vector3.one;
                    child = joint.transform;
                    created = true;
                }
                current = child;
            }
            return current;
        }
        private static Transform FindImmediateChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }
        private static string[] SplitPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path)) return null;
            string clean = path.Trim().Trim('/').Replace('\\', '/');
            if (clean.StartsWith("$vehicle/", StringComparison.Ordinal))
                clean = clean.Substring("$vehicle/".Length);
            if (clean.StartsWith(root.name + "/", StringComparison.Ordinal))
                clean = clean.Substring(root.name.Length + 1);
            if (clean == root.name || clean == "$vehicle") return new string[0];
            string[] parts = clean.Split('/');
            foreach (string s in parts)
                if (string.IsNullOrEmpty(s) || s == "." || s == ".." ||
                    s.IndexOfAny(new [] {'<','>','[',']',':'}) >= 0) return null;
            return parts;
        }

        // This is a destructive SCENE-WORKSPACE operation, prompted by the caller:
        // the original FBX asset remains untouched, but its scene prefab instance is unpacked.
        public static void AttachVisualToJoint(RebirthVehicleRigWorkspace workspace,
            Transform meshTransform, string xmlPath)
        {
            if (workspace == null || meshTransform == null)
                throw new ArgumentException("Workspace and mesh transform required.");
            if (!meshTransform.IsChildOf(workspace.transform))
                throw new ArgumentException("Select geometry inside this workspace, not from another object.");
            if (meshTransform == workspace.origin || meshTransform == workspace.transform)
                throw new ArgumentException("Cannot attach the Origin or entire workspace.");
            Transform joint = FindPath(workspace.transform, xmlPath);
            if (joint == null) throw new InvalidOperationException("Joint missing: " + xmlPath);
            if (joint == meshTransform || joint.IsChildOf(meshTransform))
                throw new InvalidOperationException("Reparenting would cause a cycle.");

            Renderer[] renderers = meshTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("Selected object contains no renderer/geometry.");
            // Align at the current visual geometry center for a starting point. An axle/hinge
            // still needs explicit human verification (especially steering, rotors and boats).
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Undo.RecordObject(joint, "Align joint pivot to mesh bounds");
            joint.position = bounds.center;

            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(meshTransform.gameObject);
            if (prefabRoot != null)
                PrefabUtility.UnpackPrefabInstance(prefabRoot,
                    PrefabUnpackMode.Completely, InteractionMode.UserAction);
            Undo.SetTransformParent(meshTransform, joint, "Attach selected visual to joint");
        }

        public static string SaveCleanPrefab(RebirthVehicleRigWorkspace workspace, string assetPath)
        {
            if (workspace == null || workspace.profile == null)
                throw new ArgumentException("Rig workspace/profile required.");
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Prefab output must be Assets/.../*.prefab");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                throw new IOException("Refusing to overwrite an existing prefab asset: " + assetPath);
            string absoluteParent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureFolder(absoluteParent);
            // Clone workspace; strip authoring-only MonoBehaviour before exporting to game.
            var clone = UnityEngine.Object.Instantiate(workspace.gameObject);
            clone.name = workspace.gameObject.name.Replace("REBIRTH_", "");
            try
            {
                var markers = clone.GetComponentsInChildren<RebirthVehicleRigWorkspace>(true);
                foreach (var marker in markers) UnityEngine.Object.DestroyImmediate(marker);
                // No code-defined custom components should remain on the prefab.
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(clone, assetPath);
                if (prefab == null) throw new IOException("SaveAsPrefabAsset returned null.");
                AssetDatabase.SaveAssets();
                return assetPath;
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }
        private static void EnsureFolder(string unityFolder)
        {
            if (unityFolder == "Assets") return;
            string[] parts = unityFolder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") throw new ArgumentException("Folder must be under Assets");
            string current = "Assets";
            for (int i=1; i<parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        [Serializable]
        private sealed class BundleReceipt
        {
            public int schemaVersion = 1;
            public string unityEditorVersion;
            public string buildTarget;
            public string bundleName, bundlePath, bundleSha256;
            public long bundleBytes;
            public string prefabAssetPath, prefabName;
            public bool prefabVerified;
            public string[] assetNames;
        }

        // Builds ONLY the selected prefab, and writes a verifiable offline receipt.
        // The AssetBundle name has an explicit .unity3d suffix to match 7DTD Resources refs.
        // Loading the built bundle in Unity verifies the prefab was actually included;
        // this is NOT equivalent to testing it in 7DTD.
        public static string BuildOneAssetBundle(string prefabAssetPath, string outputFolder,
            string bundleName, BuildTarget target)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            if (prefab == null)
                throw new FileNotFoundException("Prefab asset not found", prefabAssetPath);
            if (string.IsNullOrWhiteSpace(bundleName))
                throw new ArgumentException("AssetBundle name must not be empty.");
            string normalized = bundleName.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase)
                ? bundleName : bundleName + ".unity3d";
            if (!System.Text.RegularExpressions.Regex.IsMatch(normalized,
                @"^[a-zA-Z0-9_\-]+\.unity3d$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new ArgumentException("AssetBundle name must contain letters, digits, - or _ and .unity3d.");
            normalized = normalized.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("AssetBundle output folder required.");
            Directory.CreateDirectory(outputFolder);
            string futureBundle = Path.GetFullPath(Path.Combine(outputFolder, normalized));
            if (File.Exists(futureBundle) || File.Exists(futureBundle + ".rebirth-receipt.json"))
                throw new IOException("Refusing to overwrite an existing bundle or receipt: " + futureBundle);
            var builds = new [] { new AssetBundleBuild {
                assetBundleName = normalized,
                assetNames = new [] { prefabAssetPath } } };
            AssetBundleManifest manifest = RebirthAssetBundlePackaging.BuildAssetBundles(outputFolder, builds,
                BuildAssetBundleOptions.None, target);
            if (manifest == null) throw new IOException("Unity AssetBundle build failed; inspect Console.");
            string built = Path.GetFullPath(Path.Combine(outputFolder, normalized));
            if (!File.Exists(built) || new FileInfo(built).Length < 128)
                throw new IOException("Unity claimed success but built bundle is absent/empty: " + built);

            // Evidence for the deployment tool; do not assume that a BuildPipeline return
            // implies the expected prefab was actually included in the bundle.
            string[] assetNames;
            AssetBundle loaded = AssetBundle.LoadFromFile(built);
            if (loaded == null) throw new IOException("Built bundle could not be reopened by this Unity editor.");
            try
            {
                assetNames = loaded.GetAllAssetNames();
                string matched = null;
                foreach (string name in assetNames)
                    if (string.Equals(name, prefabAssetPath.Replace('\\','/'), StringComparison.OrdinalIgnoreCase))
                        matched = name;
                if (matched == null || loaded.LoadAsset<GameObject>(matched) == null)
                    throw new IOException("Built bundle lacks loadable prefab: " + prefabAssetPath);
            }
            finally { loaded.Unload(false); }

            string sha;
            using (SHA256 hasher = SHA256.Create())
            using (FileStream stream = File.OpenRead(built))
                sha = BitConverter.ToString(hasher.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            var receipt = new BundleReceipt {
                schemaVersion = 1,
                unityEditorVersion = Application.unityVersion,
                buildTarget = target.ToString(),
                bundleName = normalized,
                bundlePath = built,
                bundleSha256 = sha,
                bundleBytes = new FileInfo(built).Length,
                prefabAssetPath = prefabAssetPath,
                prefabName = prefab.name,
                prefabVerified = true,
                assetNames = assetNames
            };
            string receiptFile = built + ".rebirth-receipt.json";
            File.WriteAllText(receiptFile, JsonUtility.ToJson(receipt, true),
                new System.Text.UTF8Encoding(false));
            return built;
        }
    }
}
