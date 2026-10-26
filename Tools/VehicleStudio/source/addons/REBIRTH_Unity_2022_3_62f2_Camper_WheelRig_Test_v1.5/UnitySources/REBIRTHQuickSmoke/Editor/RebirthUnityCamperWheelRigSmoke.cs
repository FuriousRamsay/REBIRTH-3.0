// REBIRTH Vehicle Studio v1.5: geometry-evidence-based wheel rig preview.
// UNITY 2022.3.62f2 EDITOR ONLY. Does not edit the Asset Store prefab or touch 7DTD/REBIRTH.
// The discovered centres are NOT calibrated 7DTD suspension/steering/animation pivots.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RebirthVehicleInspector.UnityTools;

public static class RebirthUnityCamperWheelRigSmoke
{
    private const string UnityVersion = "2022.3.62f2";
    private const string OutputRoot = "Assets/REBIRTHCamperWheelRigGenerated";
    private const string ExpectedSource = "Assets/Perfectvertex/80sCamperTruck/Prefabs/assembled_vehicle.prefab";

    [Serializable]
    private sealed class WheelEvidence
    {
        public string corner;
        public string wheelClass;
        public string originalName;
        public string previewName;
        public Vector3 expectedCenter;
        public Vector3 expectedBoundsSize;
        public string tirePivotPath;
        public string steerPivotPath;
    }

    [Serializable]
    private sealed class Evidence
    {
        public string kind;
        public string sourceAsset;
        public string sourceSha256;
        public string sourceImporterMetaSha256;
        public string unityVersion;
        public int geometryObjects;
        public int hierarchyNodes;
        public float wheelCenterTolerance;
        public float wheelBoundsTolerance;
        public WheelEvidence[] wheels;
    }

    [Serializable]
    private sealed class WheelMeasurement
    {
        public string corner;
        public string wheelClass;
        public string sourceName;
        public string previewName;
        public string pivotPath;
        public string steeringPath;
        public Vector3 expectedCenter;
        public Vector3 measuredCenter;
        public Vector3 measuredSize;
        public float centerError;
        public bool attached;
    }

    [Serializable]
    private sealed class Result
    {
        public string kind = "REBIRTH_CAMPER_WHEEL_RIG_PREVIEW_V1_5";
        public string status = "FAIL";
        public string runId;
        public string unityVersion;
        public string sourceAsset;
        public string sourceSha256;
        public string sourceMetaSha256;
        public string profileAsset;
        public string previewPrefab;
        public string reportDirectory;
        public int wheelMeshesFound;
        public int wheelsAttached;
        public int duplicateNamesRepaired;
        public bool sourceUnchanged;
        public bool pivotsGameVerified = false;
        public bool seatPoseVerified = false;
        public bool assetBundleBuilt = false;
        public WheelMeasurement[] wheels = new WheelMeasurement[0];
        public string[] steps = new string[0];
        public string[] warnings = new string[0];
        public string error = "";
    }

    private sealed class MatchedWheel
    {
        public WheelEvidence evidence;
        public Transform wheel;
        public Renderer renderer;
        public Vector3 originalWorldBoundsCenter;
        public Vector3 originalWorldBoundsSize;
        public float centerError;
    }

    // Command: -executeMethod RebirthUnityCamperWheelRigSmoke.RunBatch
    // -rebirthCamperRunId <GUID N> -rebirthCamperEvidence <absolute contract JSON>
    public static void RunBatch()
    {
        bool pass = Run();
        EditorApplication.Exit(pass ? 0 : 1);
    }

    private static bool Run()
    {
        string runId = ReadArg("-rebirthCamperRunId");
        if (string.IsNullOrEmpty(runId) ||
            !System.Text.RegularExpressions.Regex.IsMatch(runId, "^[0-9a-f]{32}$"))
            runId = Guid.NewGuid().ToString("N");
        string project = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(project, "REBIRTH_CamperWheelRig_Reports", runId);
        Directory.CreateDirectory(output);
        var result = new Result {
            runId = runId, unityVersion = Application.unityVersion,
            sourceAsset = ExpectedSource, reportDirectory = output
        };
        var steps = new List<string>();
        var warnings = new List<string>();
        GameObject workspaceGameObject = null;
        try
        {
            if (Application.unityVersion != UnityVersion)
                throw new InvalidDataException("Expected exactly Unity " + UnityVersion);
            steps.Add("PASS: correct Unity version");
            string evidencePath = ReadArg("-rebirthCamperEvidence");
            if (string.IsNullOrEmpty(evidencePath) || !File.Exists(evidencePath) ||
                new FileInfo(evidencePath).Length > 65536)
                throw new InvalidDataException("Missing or oversized camper evidence JSON.");
            var evidence = JsonUtility.FromJson<Evidence>(File.ReadAllText(evidencePath));
            if (evidence == null || evidence.kind != "REBIRTH_CAMPER_WHEEL_EVIDENCE_V1" ||
                evidence.sourceAsset != ExpectedSource || evidence.unityVersion != UnityVersion ||
                evidence.wheels == null || evidence.wheels.Length != 4 ||
                evidence.geometryObjects != 16 || evidence.hierarchyNodes != 18 ||
                evidence.wheelCenterTolerance <= 0f || evidence.wheelCenterTolerance > 0.05f ||
                evidence.wheelBoundsTolerance <= 0f || evidence.wheelBoundsTolerance > 0.05f)
                throw new InvalidDataException("Camper evidence contract is not recognized.");
            string sourceDisk = Path.Combine(project, ExpectedSource.Replace('/', Path.DirectorySeparatorChar));
            string originalSha = Sha256(sourceDisk);
            string metaSha = Sha256(sourceDisk + ".meta");
            if (originalSha != evidence.sourceSha256 || metaSha != evidence.sourceImporterMetaSha256)
                throw new InvalidDataException("Imported prefab or .meta changed since the capture. " +
                    "Do not apply stale wheel coordinates; re-audit this asset first.");
            result.sourceSha256 = originalSha;
            result.sourceMetaSha256 = metaSha;
            steps.Add("PASS: imported prefab and importer metadata match captured SHA-256 hashes");

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ExpectedSource);
            if (source == null || !PrefabUtility.IsPartOfPrefabAsset(source))
                throw new InvalidDataException("The captured camper prefab is not loadable as a prefab asset.");
            var audit = RebirthVehicleFbxAudit.Inspect(source, ExpectedSource);
            if (audit.geometry == null || audit.geometry.Length != evidence.geometryObjects ||
                audit.nodes == null || audit.nodes.Length != evidence.hierarchyNodes)
                throw new InvalidDataException("Prefab's live hierarchy/geometry count differs from evidence.");
            foreach (var component in source.GetComponentsInChildren<Component>(true))
                if (component == null || component is MonoBehaviour)
                    throw new InvalidDataException("Missing/managed script on camper prefab; cannot make preview safely.");
            steps.Add("PASS: source hierarchy, geometry and component inventory match prior Unity audit");

            string assetFolder = OutputRoot + "/" + runId;
            EnsureFolder(assetFolder);
            result.profileAsset = assetFolder + "/UNAPPROVED_Camper_RigProfile.asset";
            result.previewPrefab = assetFolder + "/UNAPPROVED_Camper_FourWheel_Preview.prefab";
            var profile = ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            profile.targetVehicleName = "camper_geom_preview_" + runId;
            profile.sourceVehicleName = "NOT_CAPTURED";
            profile.sourceGameVersion = "NOT_VERIFIED";
            profile.sourceXmlStatus = "NO_7DTD_XML_CAPTURE";
            profile.family = VehicleFamily.CarOrTruck;
            profile.importWarnings.Add("Wheels use geometry-derived centres, NOT game-validated steering/suspension pivots.");
            profile.importWarnings.Add("Seat0 remains uncalibrated. No pose, hand IK or exit point supplied.");
            profile.seats.Add(new VehicleSeatAnchor {index = 0, poseId = ""});
            foreach (var wheel in evidence.wheels)
            {
                if (!SafeWheel(wheel)) throw new InvalidDataException("Unsupported wheel binding in evidence.");
                profile.wheels.Add(new VehicleWheelLink {
                    partClass = wheel.wheelClass,
                    steerTransformPath = wheel.steerPivotPath,
                    tireTransformPath = wheel.tirePivotPath
                });
                if (!string.IsNullOrEmpty(wheel.steerPivotPath))
                    profile.links.Add(new VehicleTransformLink {
                        partClass = wheel.wheelClass,
                        propertyName = "steerTransform", transformPath = wheel.steerPivotPath,
                        authoredPlaceholder = true
                    });
                profile.links.Add(new VehicleTransformLink {
                    partClass = wheel.wheelClass,
                    propertyName = "tireTransform", transformPath = wheel.tirePivotPath,
                    authoredPlaceholder = true
                });
            }
            AssetDatabase.CreateAsset(profile, result.profileAsset);
            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            source = AssetDatabase.LoadAssetAtPath<GameObject>(ExpectedSource);
            profile = AssetDatabase.LoadAssetAtPath<RebirthVehicleRigProfile>(result.profileAsset);
            if (source == null || profile == null)
                throw new InvalidDataException("Source or profile unavailable after Unity scene change.");
            var workspace = RebirthVehicleRigBuilder.CreateWorkspace(source, profile);
            workspaceGameObject = workspace.gameObject;
            steps.Add("PASS: workspace created using the previously tested Vehicle Studio builder");
            var found = FindFourWheels(workspace, evidence);
            result.wheelMeshesFound = found.Count;
            steps.Add("PASS: four unique physical wheel meshes matched by position and bounds, not labels");

            // All preflight checks must succeed before we unpack the independent scene copy.
            var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(workspace.importedVisuals);
            if (instance != null)
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            var measured = new List<WheelMeasurement>();
            foreach (var entry in found)
            {
                var e = entry.evidence;
                if (entry.wheel.name != e.originalName)
                    throw new InvalidDataException("Wheel source name changed: " + e.corner);
                if (entry.wheel.name != e.previewName)
                {
                    entry.wheel.name = e.previewName; // SCENE COPY ONLY; never source prefab
                    result.duplicateNamesRepaired++;
                }
                // Align steering parent and tire child independently. Both are
                // geometry-derived start points; physics axes are NOT certified.
                if (!string.IsNullOrEmpty(e.steerPivotPath))
                {
                    Transform steer = RebirthVehicleRigBuilder.FindPath(workspace.transform, e.steerPivotPath);
                    if (steer == null) throw new InvalidDataException("Missing steering parent: " + e.steerPivotPath);
                    steer.position = workspace.origin.TransformPoint(e.expectedCenter);
                    steer.rotation = workspace.origin.rotation;
                }
                Transform tire = RebirthVehicleRigBuilder.FindPath(workspace.transform, e.tirePivotPath);
                if (tire == null || !tire.IsChildOf(workspace.origin))
                    throw new InvalidDataException("Missing/invalid tire joint: " + e.tirePivotPath);
                tire.position = workspace.origin.TransformPoint(e.expectedCenter);
                tire.rotation = workspace.origin.rotation;
                entry.wheel.SetParent(tire, true);
                if (Vector3.Distance(entry.renderer.bounds.center, entry.originalWorldBoundsCenter) > 0.001f ||
                    Vector3.Distance(entry.renderer.bounds.size, entry.originalWorldBoundsSize) > 0.001f)
                    throw new InvalidDataException("Wheel geometry moved during rig creation: " + e.corner);
                if (tire.GetComponentsInChildren<Renderer>(true).Length != 1)
                    throw new InvalidDataException("Tire joint has an unexpected number of renderers: " + e.corner);
                measured.Add(new WheelMeasurement {
                    corner = e.corner, wheelClass = e.wheelClass, sourceName = e.originalName,
                    previewName = entry.wheel.name, pivotPath = e.tirePivotPath,
                    steeringPath = e.steerPivotPath, expectedCenter = e.expectedCenter,
                    measuredCenter = workspace.origin.InverseTransformPoint(entry.originalWorldBoundsCenter),
                    measuredSize = entry.originalWorldBoundsSize,
                    centerError = entry.centerError, attached = true
                });
                result.wheelsAttached++;
            }
            result.wheels = measured.ToArray();
            if (result.wheelsAttached != 4 || result.duplicateNamesRepaired != 1)
                throw new InvalidDataException("Expected four attached wheels and exactly one corrected duplicate name.");
            steps.Add("PASS: all four wheel meshes reparented to independently positioned joints in scene copy");
            steps.Add("PASS: misnamed rear-left wheel corrected only in the preview copy");

            foreach (var finding in RebirthVehicleRigValidator.Inspect(workspace.transform, profile))
                if (finding.severity == "ERROR")
                    throw new InvalidDataException("Workspace structural validation failed: " + finding);
            steps.Add("PASS: Vehicle Studio workspace structural validator reports no ERRORs");
            RebirthVehicleRigBuilder.SaveCleanPrefab(workspace, result.previewPrefab);
            var preview = AssetDatabase.LoadAssetAtPath<GameObject>(result.previewPrefab);
            if (preview == null ||
                preview.GetComponentInChildren<RebirthVehicleRigWorkspace>(true) != null)
                throw new InvalidDataException("Preview prefab missing or editor-only authoring script leaked.");
            int rendererCount = preview.GetComponentsInChildren<Renderer>(true).Length;
            if (rendererCount != evidence.geometryObjects)
                throw new InvalidDataException("Preview prefab lost/added visual meshes: " + rendererCount);
            foreach (var wheel in evidence.wheels)
            {
                Transform pivot = RebirthVehicleRigBuilder.FindPath(preview.transform, wheel.tirePivotPath);
                if (pivot == null || pivot.GetComponentsInChildren<Renderer>(true).Length != 1)
                    throw new InvalidDataException("Saved preview wheel joint is empty: " + wheel.tirePivotPath);
            }
            foreach (var finding in RebirthVehicleRigValidator.Inspect(preview.transform, profile, true))
                if (finding.severity == "ERROR")
                    throw new InvalidDataException("Saved preview validation failed: " + finding);
            steps.Add("PASS: clean preview prefab saved with all four wheel visuals and geometry intact");
            result.sourceUnchanged = Sha256(sourceDisk) == originalSha &&
                Sha256(sourceDisk + ".meta") == metaSha;
            if (!result.sourceUnchanged)
                throw new InvalidDataException("Source prefab or metadata changed; unexpected source modification.");
            steps.Add("PASS: original imported Asset Store prefab and importer metadata unchanged");
            warnings.Add("All wheel pivots are based on geometry, not a 7DTD runtime capture. Review steering axes and suspension before game export.");
            warnings.Add("No native 7DTD seat pose, seat position, hands, feet, exit attempts, lighting or collision configured.");
            warnings.Add("The source includes a shadow_plane mesh. Review it before creating a game-ready prefab.");
            warnings.Add("No AssetBundle, XML, or REBIRTH deployment is performed by this test.");
            result.status = "PASS_GEOMETRY_PREVIEW_NEEDS_REVIEW";
        }
        catch (Exception ex)
        {
            result.error = ex.ToString();
            result.status = "FAIL";
            Debug.LogError("REBIRTH CAMPER WHEEL-RIG TEST FAILED: " + ex);
        }
        finally
        {
            result.steps = steps.ToArray();
            result.warnings = warnings.ToArray();
            try
            {
                if (workspaceGameObject != null)
                    UnityEngine.Object.DestroyImmediate(workspaceGameObject);
                AssetDatabase.SaveAssets();
                File.WriteAllText(Path.Combine(output, "result.json"),
                    JsonUtility.ToJson(result, true), new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to write camper wheel-rig report: " + ex);
                result.status = "FAIL";
            }
        }
        return result.status == "PASS_GEOMETRY_PREVIEW_NEEDS_REVIEW";
    }

    private static List<MatchedWheel> FindFourWheels(RebirthVehicleRigWorkspace workspace, Evidence evidence)
    {
        var instanceRoot = workspace.importedVisuals.transform;
        var candidates = new List<Transform>();
        foreach (Transform child in instanceRoot)
            if (child.name.StartsWith("80sCamperTruck_wheel_", StringComparison.Ordinal))
                candidates.Add(child);
        if (candidates.Count != 4)
            throw new InvalidDataException("Expected exactly four direct-child camper wheel mesh nodes, got " + candidates.Count);
        var assigned = new HashSet<Transform>();
        var found = new List<MatchedWheel>();
        foreach (var e in evidence.wheels)
        {
            MatchedWheel match = null;
            foreach (var candidate in candidates)
            {
                var rend = candidate.GetComponent<Renderer>();
                var mesh = candidate.GetComponent<MeshFilter>();
                if (rend == null || mesh == null || mesh.sharedMesh == null)
                    throw new InvalidDataException("Expected separate MeshFilter/Renderer on camper wheel: " + candidate.name);
                Vector3 center = workspace.origin.InverseTransformPoint(rend.bounds.center);
                Vector3 dims = rend.bounds.size;
                float distance = Vector3.Distance(center, e.expectedCenter);
                if (distance > evidence.wheelCenterTolerance) continue;
                if (Vector3.Distance(dims, e.expectedBoundsSize) > evidence.wheelBoundsTolerance)
                    throw new InvalidDataException("Wheel matched position but dimensions differ: " + e.corner);
                if (candidate.name != e.originalName)
                    throw new InvalidDataException("Wheel at " + e.corner + " has an unexpected label: " + candidate.name);
                if (match != null)
                    throw new InvalidDataException("Two wheel meshes occupy the same expected wheel corner: " + e.corner);
                match = new MatchedWheel {
                    evidence = e, wheel = candidate, renderer = rend,
                    originalWorldBoundsCenter = rend.bounds.center,
                    originalWorldBoundsSize = rend.bounds.size,
                    centerError = distance
                };
            }
            if (match == null || !assigned.Add(match.wheel))
                throw new InvalidDataException("Missing/repeated wheel geometry at " + e.corner);
            found.Add(match);
        }
        if (assigned.Count != 4)
            throw new InvalidDataException("Not every wheel mesh was independently identified.");
        return found;
    }

    private static bool SafeWheel(WheelEvidence w)
    {
        if (w == null || string.IsNullOrEmpty(w.corner) || string.IsNullOrEmpty(w.wheelClass) ||
            string.IsNullOrEmpty(w.originalName) || string.IsNullOrEmpty(w.previewName) ||
            string.IsNullOrEmpty(w.tirePivotPath) || !w.tirePivotPath.StartsWith("Origin/", StringComparison.Ordinal))
            return false;
        if (!Finite(w.expectedCenter) || !Finite(w.expectedBoundsSize)) return false;
        if (w.wheelClass == "wheel0") return w.corner == "front-left" &&
            w.tirePivotPath == "Origin/FrontLeftSteer/FrontLeftWheel" && w.steerPivotPath == "Origin/FrontLeftSteer";
        if (w.wheelClass == "wheel1") return w.corner == "front-right" &&
            w.tirePivotPath == "Origin/FrontRightSteer/FrontRightWheel" && w.steerPivotPath == "Origin/FrontRightSteer";
        if (w.wheelClass == "wheel2") return w.corner == "rear-left" &&
            w.tirePivotPath == "Origin/RearLeftWheel" && string.IsNullOrEmpty(w.steerPivotPath);
        if (w.wheelClass == "wheel3") return w.corner == "rear-right" &&
            w.tirePivotPath == "Origin/RearRightWheel" && string.IsNullOrEmpty(w.steerPivotPath);
        return false;
    }
    private static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
               !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
               !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
    private static string Sha256(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Expected asset file missing", path);
        using (var hasher = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(hasher.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static string ReadArg(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == key) return args[i + 1];
        return "";
    }
    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length < 2 || parts[0] != "Assets")
            throw new ArgumentException("Output folder must be under Assets.");
        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
