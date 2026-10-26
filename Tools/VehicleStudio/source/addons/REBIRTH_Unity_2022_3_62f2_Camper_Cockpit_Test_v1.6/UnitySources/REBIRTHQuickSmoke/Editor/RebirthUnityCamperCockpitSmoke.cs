// REBIRTH Vehicle Studio v1.6: cockpit rig + editable OCCUPANT REVIEW SCENE.
// Unity 2022.3.62f2 Editor only. Intentionally does NOT build bundles or 7DTD XML.
// Steering-wheel pivot is mesh-derived; all seat/hand/foot positions are UNVERIFIED.
// The source Asset Store prefab and previously proven v1.5 wheel preview are read-only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RebirthVehicleInspector.UnityTools;

public static class RebirthUnityCamperCockpitSmoke
{
    private const string RequiredUnity = "2022.3.62f2";
    private const string SourceAsset = "Assets/Perfectvertex/80sCamperTruck/Prefabs/assembled_vehicle.prefab";
    private const string WheelRoot = "Assets/REBIRTHCamperWheelRigGenerated/";
    private const string OutputRoot = "Assets/REBIRTHCamperCockpitGenerated";
    private const string SteeringPivotName = "CamperSteeringWheelPivot_UNVERIFIED";

    [Serializable]
    private sealed class Evidence
    {
        public string kind, sourceAsset, sourceSha256, sourceImporterMetaSha256, unityVersion;
        public string steeringVisualName;
        public int geometryObjects;
        public Vector3 steeringExpectedCenter, steeringExpectedPivot, steeringBoundsSize;
        public float steeringTolerance;
    }
    [Serializable]
    private sealed class PriorWheelResult
    {
        public string kind, runId, status, previewPrefab;
        public int wheelsAttached;
        public bool sourceUnchanged;
    }
    [Serializable]
    private sealed class Result
    {
        public string kind = "REBIRTH_CAMPER_COCKPIT_PREVIEW_V1_6";
        public string status = "FAIL", runId, sourceAsset = SourceAsset, wheelRunId;
        public string wheelPreviewPrefab, sourceSha256, sourceMetaSha256, wheelPreviewSha256;
        public string cockpitPreviewPrefab, calibrationProfile, calibrationScene;
        public string unityVersion, reportDirectory;
        public int preservedMeshes, preservedWheelJoints;
        public bool steeringMeshReparented, noMeshMovement, originalSourceUnchanged;
        public bool originalWheelPreviewUnchanged, driverPoseVerified = false;
        public bool seatPositionVerified = false, steeringAxisGameVerified = false;
        public bool handIkGameVerified = false, footIkGameVerified = false;
        public bool assetBundleBuilt = false, modXmlExported = false;
        public Vector3 steeringPivotVehicleLocal, steeringCenterVehicleLocal;
        public Vector3 candidateDriverPelvis, candidatePassengerPelvis;
        public Vector3 suggestedLeftHandLocal, suggestedRightHandLocal;
        public string[] steps = new string[0], warnings = new string[0];
        public string error = "";
    }
    private sealed class RendererSnapshot
    {
        public Renderer renderer;
        public Bounds bounds;
    }

    // Unity arguments: -executeMethod RebirthUnityCamperCockpitSmoke.RunBatch
    // -rebirthCockpitRunId {32-character guid N} -rebirthWheelRunId {guid N}
    // -rebirthCockpitEvidence {absolute JSON file}
    public static void RunBatch()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Run()
    {
        string runId = ReadArg("-rebirthCockpitRunId");
        string wheelRunId = ReadArg("-rebirthWheelRunId");
        if (!IsRunId(runId)) runId = Guid.NewGuid().ToString("N");
        string project = Directory.GetParent(Application.dataPath).FullName;
        string reportDir = Path.Combine(project, "REBIRTH_CamperCockpit_Reports", runId);
        Directory.CreateDirectory(reportDir);
        var result = new Result { runId = runId, wheelRunId = wheelRunId,
            unityVersion = Application.unityVersion, reportDirectory = reportDir };
        var steps = new List<string>();
        var warnings = new List<string>();
        try
        {
            if (Application.unityVersion != RequiredUnity)
                throw new InvalidDataException("Expected exactly " + RequiredUnity);
            if (!IsRunId(wheelRunId))
                throw new InvalidDataException("Missing/invalid v1.5 wheel-rig run ID.");
            steps.Add("PASS: exact required Unity Editor version");
            string evidencePath = ReadArg("-rebirthCockpitEvidence");
            if (!File.Exists(evidencePath) || new FileInfo(evidencePath).Length > 65536)
                throw new InvalidDataException("Camper cockpit evidence is absent or oversized.");
            Evidence ev = JsonUtility.FromJson<Evidence>(File.ReadAllText(evidencePath));
            if (ev == null || ev.kind != "REBIRTH_CAMPER_COCKPIT_EVIDENCE_V1" ||
                ev.sourceAsset != SourceAsset || ev.unityVersion != RequiredUnity ||
                ev.geometryObjects != 16 || ev.steeringVisualName != "80sCamperTruck_steeringwheel" ||
                !(ev.steeringTolerance > 0f && ev.steeringTolerance <= 0.05f) ||
                !Finite(ev.steeringExpectedCenter) || !Finite(ev.steeringExpectedPivot) ||
                !Finite(ev.steeringBoundsSize))
                throw new InvalidDataException("Unsupported or invalid cockpit evidence contract.");

            string sourceDisk = Path.Combine(project, SourceAsset.Replace('/', Path.DirectorySeparatorChar));
            string sourceHash = HashFile(sourceDisk);
            string sourceMetaHash = HashFile(sourceDisk + ".meta");
            if (sourceHash != ev.sourceSha256 || sourceMetaHash != ev.sourceImporterMetaSha256)
                throw new InvalidDataException("Original camper or importer .meta changed since v1.4 audit.");
            result.sourceSha256 = sourceHash;
            result.sourceMetaSha256 = sourceMetaHash;
            steps.Add("PASS: original camper prefab and importer metadata match the actual Unity audit");

            string expectedPreview = WheelRoot + wheelRunId + "/UNAPPROVED_Camper_FourWheel_Preview.prefab";
            string wheelReportPath = Path.Combine(project, "REBIRTH_CamperWheelRig_Reports",
                wheelRunId, "result.json");
            if (!File.Exists(wheelReportPath))
                throw new FileNotFoundException("Missing prior SUCCESSFUL v1.5 wheel report", wheelReportPath);
            PriorWheelResult prior = JsonUtility.FromJson<PriorWheelResult>(File.ReadAllText(wheelReportPath));
            if (prior == null || prior.runId != wheelRunId ||
                prior.kind != "REBIRTH_CAMPER_WHEEL_RIG_PREVIEW_V1_5" ||
                prior.status != "PASS_GEOMETRY_PREVIEW_NEEDS_REVIEW" ||
                prior.wheelsAttached != 4 || !prior.sourceUnchanged ||
                prior.previewPrefab != expectedPreview)
                throw new InvalidDataException("Prior wheel rig was not a matching, successful v1.5 run.");
            string wheelPreviewDisk = Path.Combine(project, expectedPreview.Replace('/', Path.DirectorySeparatorChar));
            string wheelSha = HashFile(wheelPreviewDisk);
            result.wheelPreviewPrefab = expectedPreview;
            result.wheelPreviewSha256 = wheelSha;
            steps.Add("PASS: linked to a specific, successful real-camper four-wheel test");

            GameObject wheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(expectedPreview);
            if (wheelPrefab == null || !PrefabUtility.IsPartOfPrefabAsset(wheelPrefab))
                throw new InvalidDataException("v1.5 preview was not imported as a prefab.");
            // Fail early if the previously confirmed four-wheel hierarchy regressed.
            string[] wheelPaths = {
                "Origin/FrontLeftSteer/FrontLeftWheel",
                "Origin/FrontRightSteer/FrontRightWheel",
                "Origin/RearLeftWheel", "Origin/RearRightWheel"
            };
            foreach (string path in wheelPaths)
            {
                Transform joint = RebirthVehicleRigBuilder.FindPath(wheelPrefab.transform, path);
                if (joint == null || joint.GetComponentsInChildren<Renderer>(true).Length != 1)
                    throw new InvalidDataException("Missing or changed wheel joint: " + path);
                result.preservedWheelJoints++;
            }
            if (wheelPrefab.GetComponentsInChildren<Renderer>(true).Length != ev.geometryObjects)
                throw new InvalidDataException("Geometry count changed since accepted wheel preview.");
            steps.Add("PASS: all four v1.5 wheel joints and sixteen visual meshes are intact");

            // v1.2 exposed a destroyed asset reference after scene changes.
            // Always reload both Unity assets after opening the isolated calibration scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            wheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(expectedPreview);
            if (wheelPrefab == null) throw new InvalidDataException("Wheel prefab failed to reload after scene change.");
            GameObject instance = PrefabUtility.InstantiatePrefab(wheelPrefab) as GameObject;
            if (instance == null) throw new InvalidDataException("Cannot instantiate the v1.5 prefab.");
            var linked = PrefabUtility.GetOutermostPrefabInstanceRoot(instance);
            if (linked != null)
                PrefabUtility.UnpackPrefabInstance(linked, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            Transform origin = instance.transform.Find("Origin");
            if (origin == null) throw new InvalidDataException("No Origin root found on accepted wheel prefab.");

            List<RendererSnapshot> snapshots = CaptureBounds(instance);
            if (snapshots.Count != ev.geometryObjects)
                throw new InvalidDataException("Unexpected number of renderers in scene copy.");
            Transform steeringVisual = UniqueVisual(instance, ev.steeringVisualName);
            Renderer steeringRenderer = steeringVisual.GetComponent<Renderer>();
            if (steeringRenderer == null)
                throw new InvalidDataException("Steering-wheel visual lacks a renderer.");
            Vector3 wheelCenter = origin.InverseTransformPoint(steeringRenderer.bounds.center);
            Vector3 originalPivot = origin.InverseTransformPoint(steeringVisual.position);
            if (Vector3.Distance(wheelCenter, ev.steeringExpectedCenter) > ev.steeringTolerance ||
                Vector3.Distance(originalPivot, ev.steeringExpectedPivot) > ev.steeringTolerance ||
                Vector3.Distance(steeringRenderer.bounds.size, ev.steeringBoundsSize) > ev.steeringTolerance)
                throw new InvalidDataException("Steering-wheel location/size disagrees with original real prefab audit.");
            result.steeringCenterVehicleLocal = wheelCenter;
            steps.Add("PASS: original steering-wheel geometry and source pivot match measured camper coordinates");

            // New joint uses the mesh's existing authored local pivot. This is only a
            // steering-wheel VISUAL rotation pivot, not a confirmed game steering value.
            GameObject steeringPivot = new GameObject(SteeringPivotName);
            steeringPivot.transform.SetParent(origin, false);
            steeringPivot.transform.position = steeringVisual.position;
            steeringPivot.transform.rotation = steeringVisual.rotation;
            steeringVisual.SetParent(steeringPivot.transform, true);
            result.steeringMeshReparented = steeringVisual.parent == steeringPivot.transform;
            result.steeringPivotVehicleLocal = origin.InverseTransformPoint(steeringPivot.transform.position);
            result.noMeshMovement = VerifyBounds(snapshots, 0.001f);
            if (!result.steeringMeshReparented || !result.noMeshMovement)
                throw new InvalidDataException("Steering-wheel reparenting changed the visuals.");
            steps.Add("PASS: independent steering-wheel visual pivot created without moving any of 16 meshes");

            string assetFolder = OutputRoot + "/" + runId;
            EnsureFolder(assetFolder);
            result.cockpitPreviewPrefab = assetFolder + "/UNAPPROVED_Camper_WheelsAndSteering.prefab";
            result.calibrationProfile = assetFolder + "/UNAPPROVED_Camper_OccupantProfile.asset";
            result.calibrationScene = assetFolder + "/OPEN_ME_Camper_Occupant_Calibration.unity";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result.cockpitPreviewPrefab) != null ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result.calibrationProfile) != null ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result.calibrationScene) != null)
                throw new IOException("Refusing to overwrite a previous calibration result.");
            GameObject newPrefab = PrefabUtility.SaveAsPrefabAsset(instance, result.cockpitPreviewPrefab);
            if (newPrefab == null) throw new IOException("Unable to save separate cockpit preview prefab.");
            AssetDatabase.SaveAssets();
            newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.cockpitPreviewPrefab);
            if (newPrefab == null || newPrefab.GetComponentsInChildren<Renderer>(true).Length != ev.geometryObjects ||
                RebirthVehicleRigBuilder.FindPath(newPrefab.transform, "Origin/" + SteeringPivotName) == null ||
                newPrefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidDataException("New preview lost geometry/pivot or contains non-game scripts.");
            steps.Add("PASS: clean new prefab saved with steering pivot and four earlier wheel joints");

            // Seat centres are visually-derived EDITOR STARTING POINTS, NOT game poses.
            // Do not assign a native seat pose ID, exit, or active hand/foot IK.
            Vector3 driver = new Vector3(wheelCenter.x, wheelCenter.y - 0.34f, wheelCenter.z - 0.47f);
            Vector3 passenger = new Vector3(-wheelCenter.x, driver.y, driver.z);
            result.candidateDriverPelvis = driver;
            result.candidatePassengerPelvis = passenger;
            var profile = ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            profile.targetVehicleName = "80s_camper_COCKPIT_NOT_VERIFIED";
            profile.sourceVehicleName = "NOT_CAPTURED";
            profile.sourceGameVersion = "NOT_VERIFIED";
            profile.sourceXmlStatus = "NO_VANILLA_SEAT_OR_POSE_CAPTURE";
            profile.sourcePrefabReference = expectedPreview;
            profile.family = VehicleFamily.CarOrTruck;
            profile.seats.Add(new VehicleSeatAnchor { index = 0, poseId = "", position = driver });
            profile.seats.Add(new VehicleSeatAnchor { index = 1, poseId = "", position = passenger });
            profile.importWarnings.Add("Candidate seat positions are inferred ONLY from the steering wheel; do not export them as validated 7DTD poses.");
            profile.importWarnings.Add("No vanilla seat/animation pose or real occupant skeleton has been captured.");
            profile.importWarnings.Add("Do not enable steering hand IK until a 7DTD donor/camper comparison is reviewed.");
            profile.importWarnings.Add("Footwell guides are NOT native foot IK and must never be exported as foot overrides.");
            // All four wheels remain in a preview state; do not claim 7DTD XML links are calibrated.
            foreach (string path in wheelPaths)
                profile.importWarnings.Add("Wheel hierarchy is geometrically present but not yet game-validated: " + path);
            float gripX = steeringRenderer.bounds.extents.x * 0.75f;
            Vector3 steeringWorldCenter = steeringRenderer.bounds.center;
            Vector3 leftWorld = steeringWorldCenter - steeringPivot.transform.right * gripX;
            Vector3 rightWorld = steeringWorldCenter + steeringPivot.transform.right * gripX;
            var ik = new VehicleIkAnchor {
                partClass = "steering", partType = "Steering",
                pivotPath = "Origin/" + SteeringPivotName,
                hasLeftHand = false, hasRightHand = false,
                hasLeftFoot = false, hasRightFoot = false,
                leftHandPosition = steeringPivot.transform.InverseTransformPoint(leftWorld),
                rightHandPosition = steeringPivot.transform.InverseTransformPoint(rightWorld)
            };
            profile.ik.Add(ik);
            result.suggestedLeftHandLocal = ik.leftHandPosition;
            result.suggestedRightHandLocal = ik.rightHandPosition;
            AssetDatabase.CreateAsset(profile, result.calibrationProfile);
            AssetDatabase.SaveAssets();
            profile = AssetDatabase.LoadAssetAtPath<RebirthVehicleRigProfile>(result.calibrationProfile);
            if (profile == null || profile.seats.Count != 2 || profile.ik.Count != 1 ||
                profile.seats[0].poseId != "" || profile.ik[0].hasLeftHand || profile.ik[0].hasRightHand)
                throw new InvalidDataException("Unverified cockpit profile was not persisted safely.");
            steps.Add("PASS: two editable seat candidates saved without inventing pose IDs or enabling hand/foot IK");

            // The existing Vehicle Studio custom Inspector supplies interactive
            // Scene handles for the seat anchors and hand positions. Scene-only
            // spheres are visibility hints, never part of the clean exported prefab.
            RebirthVehicleRigWorkspace editorWorkspace = instance.AddComponent<RebirthVehicleRigWorkspace>();
            editorWorkspace.profile = profile;
            editorWorkspace.origin = origin;
            Transform imported = origin.Find("assembled_vehicle");
            editorWorkspace.importedVisuals = imported != null ? imported.gameObject : instance;
            GameObject hints = new GameObject("EDITOR_GUIDES_ONLY__NOT_IN_PREFAB");
            CreateHint(hints.transform, "HAND_LEFT__UNVERIFIED_GRIP", leftWorld, 0.055f);
            CreateHint(hints.transform, "HAND_RIGHT__UNVERIFIED_GRIP", rightWorld, 0.055f);
            // Not foot IK: floor/leg clearance reminders only.
            CreateHint(hints.transform, "FOOTWELL_LEFT__NOT_FOOT_IK",
                origin.TransformPoint(driver + new Vector3(-0.12f, -0.45f, 0.40f)), 0.06f);
            CreateHint(hints.transform, "FOOTWELL_RIGHT__NOT_FOOT_IK",
                origin.TransformPoint(driver + new Vector3(0.12f, -0.45f, 0.40f)), 0.06f);
            Selection.activeGameObject = instance;
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), result.calibrationScene))
                throw new IOException("Unity failed to save cockpit calibration scene.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(result.calibrationScene) == null)
                throw new IOException("Saved calibration scene not available in AssetDatabase.");
            steps.Add("PASS: saved an editable calibration scene with Vehicle Studio seat handles and clearly labeled grip/footwell guides");

            result.originalSourceUnchanged = HashFile(sourceDisk) == sourceHash &&
                HashFile(sourceDisk + ".meta") == sourceMetaHash;
            result.originalWheelPreviewUnchanged = HashFile(wheelPreviewDisk) == wheelSha;
            if (!result.originalSourceUnchanged || !result.originalWheelPreviewUnchanged)
                throw new InvalidDataException("Unexpected change to imported camper or successful v1.5 wheel rig.");
            steps.Add("PASS: original Asset Store prefab, importer settings, and prior wheel preview remain unchanged");
            result.preservedMeshes = ev.geometryObjects;
            warnings.Add("Seat0/seat1 are initial geometric guesses. Their animation pose IDs are intentionally empty.");
            warnings.Add("Two steering-hand grips are visualization suggestions; IK override booleans remain FALSE.");
            warnings.Add("Footwell points are clearance reminders, NOT 7DTD foot IK targets.");
            warnings.Add("Steering-wheel visual pivot axis, driver pose, exits, collisions and gameplay remain unverified.");
            warnings.Add("No Unity AssetBundle, XML or REBIRTH mod installation is performed by this test.");
            result.status = "PASS_COCKPIT_PREVIEW_NEEDS_CALIBRATION";
        }
        catch (Exception ex)
        {
            result.error = ex.ToString();
            result.status = "FAIL";
            Debug.LogError("REBIRTH camper cockpit test failed: " + ex);
        }
        finally
        {
            result.steps = steps.ToArray();
            result.warnings = warnings.ToArray();
            try
            {
                AssetDatabase.SaveAssets();
                File.WriteAllText(Path.Combine(reportDir, "result.json"),
                    JsonUtility.ToJson(result, true), new System.Text.UTF8Encoding(false));
            }
            catch (Exception writeError)
            {
                result.status = "FAIL";
                Debug.LogError("Could not save camper cockpit result: " + writeError);
            }
        }
        return result.status == "PASS_COCKPIT_PREVIEW_NEEDS_CALIBRATION";
    }

    private static List<RendererSnapshot> CaptureBounds(GameObject root)
    {
        var snapshots = new List<RendererSnapshot>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            snapshots.Add(new RendererSnapshot { renderer = r, bounds = r.bounds });
        return snapshots;
    }
    private static bool VerifyBounds(List<RendererSnapshot> original, float tolerance)
    {
        foreach (var snapshot in original)
        {
            if (snapshot.renderer == null ||
                Vector3.Distance(snapshot.renderer.bounds.center, snapshot.bounds.center) > tolerance ||
                Vector3.Distance(snapshot.renderer.bounds.size, snapshot.bounds.size) > tolerance)
                return false;
        }
        return true;
    }
    private static Transform UniqueVisual(GameObject root, string name)
    {
        Transform result = null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != name) continue;
            if (result != null) throw new InvalidDataException("Ambiguous steering visual name: " + name);
            result = t;
        }
        if (result == null) throw new InvalidDataException("Missing named steering visual: " + name);
        return result;
    }
    private static void CreateHint(Transform parent, string name, Vector3 world, float diameter)
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = name;
        s.transform.SetParent(parent, false);
        s.transform.position = world;
        s.transform.localScale = Vector3.one * diameter;
        Collider collider = s.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }
    private static string ReadArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        return "";
    }
    private static bool IsRunId(string text)
    {
        return !string.IsNullOrEmpty(text) &&
            System.Text.RegularExpressions.Regex.IsMatch(text, "^[0-9a-f]{32}$");
    }
    private static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
    private static string HashFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Required evidence file missing", path);
        using (var h = SHA256.Create())
        using (var f = File.OpenRead(path))
            return BitConverter.ToString(h.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
    }
    private static void EnsureFolder(string assetFolder)
    {
        string[] parts = assetFolder.Split('/');
        if (parts.Length < 2 || parts[0] != "Assets")
            throw new InvalidDataException("Output must be inside Assets folder.");
        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
