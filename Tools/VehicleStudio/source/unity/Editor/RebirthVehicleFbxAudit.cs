// REBIRTH Vehicle Studio: read-only FBX / prefab-geometry audit.
// Only inspects a temporary Editor instance; never writes into the FBX asset.
// Not a substitute for a live 7DTD reference vehicle or a seat-pose test.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public static class RebirthVehicleFbxAudit
    {
        [Serializable] public sealed class Node
        {
            public string path;
            public string name;
            public Vector3 rootPosition, localPosition, localScale;
            public Vector3 rootEuler;
            public int rendererCount, colliderCount;
            public bool ambiguousPath;
        }
        [Serializable] public sealed class Geometry
        {
            public string path, meshName, type;
            public int vertices, submeshes;
            public long indices;
            public bool isReadable;
            public Vector3 boundsCenter, boundsSize;
            public string[] materialNames, shaders;
            public string rootBonePath;
            public string[] bonePaths;
        }
        [Serializable] public sealed class Report
        {
            public int schemaVersion = 1;
            public string kind = "rebirthVehicleFbxAudit";
            public string sourceAsset, sourceSha256, sourceImporterMetaSha256, editorVersion;
            public string coordinateSpace = "FBX instance root local, metres ONLY if source import scale is calibrated";
            public Vector3 visibleBoundsCenter, visibleBoundsSize;
            public Node[] nodes;
            public Geometry[] geometry;
            public string[] warnings;
        }

        [MenuItem("Assets/REBIRTH/Audit Selected Vehicle FBX (Read Only)", true)]
        private static bool AuditSelectionAvailable()
        {
            GameObject selected = Selection.activeObject as GameObject;
            if (selected == null) return false;
            string path = AssetDatabase.GetAssetPath(selected);
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
        }
        [MenuItem("Assets/REBIRTH/Audit Selected Vehicle FBX (Read Only)", false, 1600)]
        private static void AuditSelected()
        {
            if (!AuditSelectionAvailable()) return;
            GameObject selected = (GameObject)Selection.activeObject;
            string selectedPath = AssetDatabase.GetAssetPath(selected);
            string output = EditorUtility.SaveFilePanel("Save read-only vehicle FBX audit",
                Application.dataPath, selected.name + "_fbx_audit", "json");
            if (string.IsNullOrEmpty(output)) return;
            try
            {
                Report report = Inspect(selected, selectedPath);
                WriteNew(report, output);
                EditorUtility.DisplayDialog("REBIRTH Vehicle FBX Audit",
                    "Saved audit with " + report.nodes.Length + " transforms and " +
                    report.geometry.Length + " geometry objects. Review warnings before rigging.", "OK");
            }
            catch(Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("REBIRTH Audit Failed", e.Message, "OK");
            }
        }

        public static Report Inspect(GameObject source, string assetPath)
        {
            if (source == null || string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                 !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) ||
                AssetDatabase.GetAssetPath(source) != assetPath)
                throw new ArgumentException("Audit an imported Assets/... FBX or prefab, not a scene object.");
            string diskFile = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(diskFile)) throw new FileNotFoundException("Source asset missing", diskFile);
            string sha = HashFile(diskFile);
            string importerMeta = diskFile + ".meta";
            string metaSha = File.Exists(importerMeta) ? HashFile(importerMeta) : "";

            GameObject copy = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (copy == null) throw new InvalidDataException("Failed to instantiate source FBX for read-only inspection.");
            copy.transform.position = Vector3.zero;
            copy.transform.rotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            try
            {
                var nodes = new List<Node>();
                var geometry = new List<Geometry>();
                var warnings = new List<string>();
                var paths = new HashSet<string>(StringComparer.Ordinal);
                bool haveBounds = false;
                Bounds total = new Bounds();
                Transform root = copy.transform;
                var all = root.GetComponentsInChildren<Transform>(true);
                if (all.Length > 20000)
                    throw new InvalidDataException("FBX contains more than 20,000 transforms; split the asset before audit.");
                foreach (Transform tr in all)
                {
                    // An imported prefab may itself have a MeshFilter at root;
                    // include it in the audit, but never permit it as an
                    // automatically detachable wheel visual.
                    string path = tr == root ? "$FBX_ROOT" : RelativePath(root, tr);
                    var item = new Node {
                        path = path, name = tr.name,
                        localPosition = tr.localPosition, localScale = tr.localScale,
                        rootPosition = root.InverseTransformPoint(tr.position),
                        rootEuler = (Quaternion.Inverse(root.rotation) * tr.rotation).eulerAngles,
                        rendererCount = tr.GetComponents<Renderer>().Length,
                        colliderCount = tr.GetComponents<Collider>().Length,
                        ambiguousPath = !paths.Add(path)
                    };
                    if (item.ambiguousPath) warnings.Add("Ambiguous duplicate transform path: " + path);
                    nodes.Add(item);
                    var filter = tr.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                        geometry.Add(Describe(path, filter.sharedMesh, "MeshFilter", tr,
                            tr.GetComponent<MeshRenderer>(), root, ref haveBounds, ref total));
                    var skinned = tr.GetComponent<SkinnedMeshRenderer>();
                    if (skinned != null && skinned.sharedMesh != null)
                        geometry.Add(Describe(path, skinned.sharedMesh, "SkinnedMeshRenderer", tr,
                            skinned, root, ref haveBounds, ref total));
                }
                if (geometry.Exists(m => m.path == "$FBX_ROOT"))
                    warnings.Add("ROOT_GEOMETRY: model root holds renderable geometry; it cannot be independently attached as a wheel or moving part without separation.");
                if (geometry.Count == 0) warnings.Add("NO_MESHES: no MeshFilter or SkinnedMeshRenderer meshes available.");
                if (geometry.Count == 1) warnings.Add("SINGLE_MESH: movable wheels/rotors/doors may be fused; audit geometry before rigging.");
                if (geometry.Count > 15000) throw new InvalidDataException("FBX exceeds 15,000 geometry entries.");
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null)
                {
                    warnings.Add("MODEL_IMPORT_SCALE_REVIEW: verify Unity importer scale, mesh orientation, and 7DTD game units before approving pivot coordinates.");
                }
                return new Report {
                    sourceAsset=assetPath, sourceSha256=sha,
                    sourceImporterMetaSha256=metaSha, editorVersion=Application.unityVersion,
                    visibleBoundsCenter=haveBounds ? total.center : Vector3.zero,
                    visibleBoundsSize=haveBounds ? total.size : Vector3.zero,
                    nodes=nodes.ToArray(), geometry=geometry.ToArray(), warnings=warnings.ToArray()
                };
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        private static string HashFile(string path)
        {
            using (var hasher = SHA256.Create())
            using (var input = File.OpenRead(path))
                return BitConverter.ToString(hasher.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
        private static Geometry Describe(string path, Mesh mesh, string type, Transform tr,
            Renderer renderer, Transform root, ref bool haveTotal, ref Bounds total)
        {
            long indices = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) indices += (long)mesh.GetIndexCount(s);
            Matrix4x4 matrix = root.worldToLocalMatrix * tr.localToWorldMatrix;
            Bounds bounds = TransformBounds(mesh.bounds, matrix);
            if (!haveTotal) { total = bounds; haveTotal = true; }
            else total.Encapsulate(bounds);
            var names = new List<string>();
            var shaders = new List<string>();
            if (renderer != null)
                foreach (var material in renderer.sharedMaterials)
                {
                    names.Add(material != null ? material.name : "<MISSING_MATERIAL>");
                    shaders.Add(material != null && material.shader != null ? material.shader.name : "<MISSING_SHADER>");
                }
            var bonePaths = new List<string>();
            var skin = renderer as SkinnedMeshRenderer;
            if (skin != null)
                foreach (var bone in skin.bones)
                    bonePaths.Add(bone == null ? "<MISSING_BONE>" :
                        bone == root ? "$FBX_ROOT" : RelativePath(root, bone));
            return new Geometry {
                path=path, meshName=mesh.name, type=type,
                vertices=mesh.vertexCount, submeshes=mesh.subMeshCount, indices=indices,
                isReadable=mesh.isReadable, boundsCenter=bounds.center, boundsSize=bounds.size,
                materialNames=names.ToArray(), shaders=shaders.ToArray(),
                rootBonePath=skin == null || skin.rootBone == null ? "" :
                    skin.rootBone == root ? "$FBX_ROOT" : RelativePath(root, skin.rootBone),
                bonePaths=bonePaths.ToArray()
            };
        }
        private static Bounds TransformBounds(Bounds input, Matrix4x4 transform)
        {
            Vector3 p = transform.MultiplyPoint3x4(input.center);
            Vector3 ext = input.extents;
            Vector3 axisX = transform.MultiplyVector(new Vector3(ext.x, 0f, 0f));
            Vector3 axisY = transform.MultiplyVector(new Vector3(0f, ext.y, 0f));
            Vector3 axisZ = transform.MultiplyVector(new Vector3(0f, 0f, ext.z));
            var dst = new Vector3(Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(p, 2f * dst);
        }
        private static string RelativePath(Transform root, Transform child)
        {
            var tokens = new List<string>();
            Transform p = child;
            while (p != root && p != null) { tokens.Add(p.name); p = p.parent; }
            if (p == null) throw new InvalidOperationException("FBX mesh is not a child of audited model root.");
            tokens.Reverse();
            return string.Join("/", tokens.ToArray());
        }
        public static string WriteNew(Report audit, string path)
        {
            if (audit == null || string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Audit result and new output file required.");
            string dest = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            using (var stream = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
                writer.Write(JsonUtility.ToJson(audit, true));
            return dest;
        }
    }
}
