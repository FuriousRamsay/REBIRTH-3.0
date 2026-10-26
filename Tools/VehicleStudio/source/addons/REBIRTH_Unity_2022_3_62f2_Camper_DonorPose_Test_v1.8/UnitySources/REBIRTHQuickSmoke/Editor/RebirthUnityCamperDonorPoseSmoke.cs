// REBIRTH Vehicle Studio v1.8 — vanilla 4x4 XML donor pose import into a COPY
// of the v1.6 camper cockpit calibration scene/profile. Unity 2022.3.62f2.
// DOES NOT copy donor seat positions/exits or enable hand/foot IK, build bundles,
// export game XML, install game DLLs, or change REBIRTH.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RebirthVehicleInspector.UnityTools;

public static class RebirthUnityCamperDonorPoseSmoke
{
    private const string RequiredUnity = "2022.3.62f2";
    private const string Kind = "REBIRTH_CAMPER_DONOR_POSE_CALIBRATION_V1_8";
    private const string OutputRoot = "Assets/REBIRTHCamperDonorPoseGenerated";
    private const string SourceAsset = "Assets/Perfectvertex/80sCamperTruck/Prefabs/assembled_vehicle.prefab";

    [Serializable]
    private sealed class DonorSeat
    {
        public int index;
        public string poseId, donorExitCandidates, dependsOnSeatMod;
        public Vector3 donorPosition, donorRotationEuler;
    }
    [Serializable]
    private sealed class DonorWheel
    {
        public int index;
        public string steerTransform, tireTransform;
    }
    [Serializable]
    private sealed class CalibrationPlan
    {
        public string kind, status, requiredUnityVersion, donorVehicleName;
        public string donorXmlSha256, donorCaptureSha256, camperSourceAsset;
        public string camperSourceSha256, camperSourceImporterMetaSha256;
        public string donorSteeringTransformPath, steeringPartClass, steeringClass;
        public string camperSteeringPivotPath, driverPoseId, passengerPoseId;
        public int selectedSeatCountForPreview, approvedSeatCount, approvedExitCount, referenceWheelCount;
        public bool enablesHandIk, enablesFootIk;
        public Vector3 donorSteeringAngle;
        public Vector3 donorHandLeftPosition, donorHandRightPosition;
        public Vector3 donorHandLeftRotationEuler, donorHandRightRotationEuler;
        public DonorSeat[] donorSeats;
        public DonorWheel[] donorWheels;
    }
    [Serializable]
    private sealed class PreviousResult
    {
        public string kind, runId, status, sourceSha256, sourceMetaSha256;
        public string cockpitPreviewPrefab, calibrationProfile, calibrationScene;
        public int preservedMeshes, preservedWheelJoints;
        public bool originalSourceUnchanged, originalWheelPreviewUnchanged;
    }
    [Serializable]
    private sealed class RunResult
    {
        public string kind = "REBIRTH_CAMPER_DONOR_POSE_IMPORT_V1_8";
        public string status = "FAIL", runId, cockpitRunId;
        public string unityVersion, donorVehicleName, donorXmlSha256, profileAsset, sceneAsset;
        public string sourceCamperProfile, sourceCamperScene, sourceCamperPrefab;
        public int savedSeatCount;
        public string driverPoseId, passengerPoseId;
        public Vector3 unchangedDriverCandidate, unchangedPassengerCandidate;
        public bool allPriorAssetsUnchanged, sourceGameXmlUnchanged;
        public bool seatsGameVerified = false, handIkGameVerified = false;
        public bool footIkGameVerified = false, exitsGameVerified = false;
        public bool bundleBuilt = false, gameXmlGenerated = false, installedIntoRebirth = false;
        public string[] steps = new string[0], warnings = new string[0];
        public string error = "";
    }

    // -executeMethod RebirthUnityCamperDonorPoseSmoke.RunBatch
    // -rebirthDonorRunId {guidN} -rebirthCockpitRunId {guidN}
    // -rebirthDonorPlan {absolute plan JSON} -rebirthGameVehiclesXml {absolute XML path}
    public static void RunBatch()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Run()
    {
        string runId = ReadArg("-rebirthDonorRunId");
        string cockpitRunId = ReadArg("-rebirthCockpitRunId");
        if (!IsId(runId)) runId = Guid.NewGuid().ToString("N");
        string project = Directory.GetParent(Application.dataPath).FullName;
        string reportDir = Path.Combine(project, "REBIRTH_CamperDonorPose_Reports", runId);
        Directory.CreateDirectory(reportDir);
        var result = new RunResult {
            runId = runId, cockpitRunId = cockpitRunId, unityVersion = Application.unityVersion
        };
        var steps = new List<string>();
        var warnings = new List<string>();
        try
        {
            if (Application.unityVersion != RequiredUnity || !IsId(cockpitRunId))
                throw new InvalidDataException("Unexpected Unity version or invalid earlier cockpit run ID.");
            steps.Add("PASS: Unity 2022.3.62f2 and specific v1.6 cockpit run selected");
            string planPath = ReadArg("-rebirthDonorPlan");
            if (!File.Exists(planPath) || new FileInfo(planPath).Length > 65536)
                throw new InvalidDataException("Donor reference plan missing or too large.");
            CalibrationPlan plan = JsonUtility.FromJson<CalibrationPlan>(File.ReadAllText(planPath));
            if (plan == null || plan.kind != Kind ||
                plan.status != "DONOR_XML_ONLY_NOT_GAME_VALIDATED" ||
                plan.requiredUnityVersion != RequiredUnity ||
                plan.donorVehicleName != "vehicleTruck4x4" ||
                plan.camperSourceAsset != SourceAsset ||
                plan.driverPoseId != "40" || plan.passengerPoseId != "41" ||
                plan.donorSteeringTransformPath != "M/SteeringWheel" ||
                plan.steeringPartClass != "handlebars" || plan.steeringClass != "Steering" ||
                plan.camperSteeringPivotPath != "Origin/CamperSteeringWheelPivot_UNVERIFIED" ||
                plan.approvedSeatCount != 0 || plan.selectedSeatCountForPreview != 2 ||
                plan.approvedExitCount != 0 || plan.enablesHandIk || plan.enablesFootIk ||
                plan.referenceWheelCount != 4 || plan.donorSeats == null ||
                plan.donorSeats.Length != 6 || plan.donorWheels == null ||
                plan.donorWheels.Length != 4 || !IsSha(plan.donorXmlSha256) ||
                !IsSha(plan.camperSourceSha256) ||
                !IsSha(plan.camperSourceImporterMetaSha256))
                throw new InvalidDataException("Donor plan has an unsupported scope or broken evidence.");
            for (int i = 0; i < 6; i++)
                if (plan.donorSeats[i] == null || plan.donorSeats[i].index != i ||
                    plan.donorSeats[i].poseId != (40+i).ToString() ||
                    !Finite(plan.donorSeats[i].donorPosition) ||
                    !Finite(plan.donorSeats[i].donorRotationEuler))
                    throw new InvalidDataException("Donor seat references are missing or reordered.");
            for (int i = 0; i < 4; i++)
                if (plan.donorWheels[i] == null || plan.donorWheels[i].index != i ||
                    string.IsNullOrWhiteSpace(plan.donorWheels[i].tireTransform))
                    throw new InvalidDataException("Donor wheel references are incomplete.");
            if (!Finite(plan.donorHandLeftPosition) || !Finite(plan.donorHandRightPosition) ||
                !Finite(plan.donorHandLeftRotationEuler) ||
                !Finite(plan.donorHandRightRotationEuler) ||
                !Finite(plan.donorSteeringAngle))
                throw new InvalidDataException("Donor steering/hand IK includes nonfinite coordinates.");
            steps.Add("PASS: real 4x4 XML donor has six valid pose references and hand IK values");

            string vehiclesXml = ReadArg("-rebirthGameVehiclesXml");
            string gameHash = HashFile(vehiclesXml);
            if (gameHash != plan.donorXmlSha256)
                throw new InvalidDataException("The installed 7DTD vehicles.xml has changed since the reference capture.");
            result.donorVehicleName = plan.donorVehicleName;
            result.donorXmlSha256 = gameHash;
            steps.Add("PASS: current installed-game vehicles.xml matches donor capture SHA-256");

            string camperFile = Path.Combine(project, SourceAsset.Replace('/', Path.DirectorySeparatorChar));
            string camperSha = HashFile(camperFile);
            string camperMetaSha = HashFile(camperFile + ".meta");
            if (camperSha != plan.camperSourceSha256 ||
                camperMetaSha != plan.camperSourceImporterMetaSha256)
                throw new InvalidDataException("Camper original prefab/importer differs from measured geometry.");
            steps.Add("PASS: original imported Perfectvertex asset has not changed");

            string prefix = "Assets/REBIRTHCamperCockpitGenerated/" + cockpitRunId + "/";
            string priorReport = Path.Combine(project, "REBIRTH_CamperCockpit_Reports", cockpitRunId, "result.json");
            if (!File.Exists(priorReport))
                throw new FileNotFoundException("Missing successful v1.6 report", priorReport);
            PreviousResult previous = JsonUtility.FromJson<PreviousResult>(File.ReadAllText(priorReport));
            if (previous == null || previous.kind != "REBIRTH_CAMPER_COCKPIT_PREVIEW_V1_6" ||
                previous.runId != cockpitRunId ||
                previous.status != "PASS_COCKPIT_PREVIEW_NEEDS_CALIBRATION" ||
                previous.preservedMeshes != 16 || previous.preservedWheelJoints != 4 ||
                !previous.originalSourceUnchanged || !previous.originalWheelPreviewUnchanged ||
                previous.sourceSha256 != camperSha || previous.sourceMetaSha256 != camperMetaSha ||
                previous.cockpitPreviewPrefab != prefix + "UNAPPROVED_Camper_WheelsAndSteering.prefab" ||
                previous.calibrationProfile != prefix + "UNAPPROVED_Camper_OccupantProfile.asset" ||
                previous.calibrationScene != prefix + "OPEN_ME_Camper_Occupant_Calibration.unity")
                throw new InvalidDataException("The selected v1.6 camper cockpit reference was not successful.");
            result.sourceCamperProfile = previous.calibrationProfile;
            result.sourceCamperScene = previous.calibrationScene;
            result.sourceCamperPrefab = previous.cockpitPreviewPrefab;
            string oldProfileDisk = DiskAsset(project, previous.calibrationProfile);
            string oldSceneDisk = DiskAsset(project, previous.calibrationScene);
            string oldPrefabDisk = DiskAsset(project, previous.cockpitPreviewPrefab);
            string oldProfileSha = HashFile(oldProfileDisk);
            string oldSceneSha = HashFile(oldSceneDisk);
            string oldPrefabSha = HashFile(oldPrefabDisk);
            RebirthVehicleRigProfile sourceProfile = AssetDatabase.LoadAssetAtPath<RebirthVehicleRigProfile>(previous.calibrationProfile);
            if (sourceProfile == null || sourceProfile.seats == null || sourceProfile.seats.Count != 2 ||
                sourceProfile.ik == null || sourceProfile.ik.Count != 1 ||
                sourceProfile.seats[0].index != 0 || sourceProfile.seats[1].index != 1 ||
                !string.IsNullOrEmpty(sourceProfile.seats[0].poseId) ||
                !string.IsNullOrEmpty(sourceProfile.seats[1].poseId) ||
                sourceProfile.ik[0].hasLeftHand || sourceProfile.ik[0].hasRightHand ||
                sourceProfile.ik[0].hasLeftFoot || sourceProfile.ik[0].hasRightFoot)
                throw new InvalidDataException("The original camper profile has edits; refusing to override it.");
            Vector3 expectedDriver = sourceProfile.seats[0].position;
            Vector3 expectedPassenger = sourceProfile.seats[1].position;
            steps.Add("PASS: uncalibrated two-seat v1.6 profile loaded without mutating it");

            string destFolder = OutputRoot + "/" + runId;
            EnsureFolder(destFolder);
            result.profileAsset = destFolder + "/UNAPPROVED_Camper_DonorPose_Profile.asset";
            result.sceneAsset = destFolder + "/OPEN_ME_Camper_DonorPose_Review.unity";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result.profileAsset) != null ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result.sceneAsset) != null)
                throw new IOException("Destination already contains generated pose review assets.");
            Scene scene = EditorSceneManager.OpenScene(previous.calibrationScene, OpenSceneMode.Single);
            RebirthVehicleRigWorkspace[] workspaces = UnityEngine.Object.FindObjectsOfType<RebirthVehicleRigWorkspace>(true);
            if (workspaces.Length != 1 || workspaces[0].origin == null || workspaces[0].profile == null)
                throw new InvalidDataException("The v1.6 editable scene must have exactly one intact workspace.");
            var ws = workspaces[0];
            Transform wheelPivot = RebirthVehicleRigBuilder.FindPath(ws.transform, plan.camperSteeringPivotPath);
            if (wheelPivot == null) throw new InvalidDataException("Missing camper steering-wheel preview pivot.");
            if (ws.GetComponentsInChildren<Renderer>(true).Length != 16)
                throw new InvalidDataException("Calibrated scene vehicle geometry changed from 16 renderers.");

            // IMPORTANT: duplicate the ScriptableObject; only assign DONOR POSE identifiers.
            // All world-space camper positions, exits, IK enable flags and IK guesses remain untouched.
            RebirthVehicleRigProfile clone = UnityEngine.Object.Instantiate(sourceProfile);
            clone.name = "UNAPPROVED_Camper_DonorPose_Profile";
            clone.seats[0].poseId = plan.driverPoseId;
            clone.seats[1].poseId = plan.passengerPoseId;
            clone.sourceVehicleName = plan.donorVehicleName;
            clone.sourceXmlStatus = "DONOR_XML_ONLY_POSES_SET_NO_RUNTIME_VALIDATION";
            clone.sourceGameVersion = "UNCONFIRMED_GAME_VERSION_vehicles_xml_sha256";
            clone.sourceCaptureFilename = "donor_xml_capture.json SHA256:" + plan.donorCaptureSha256;
            clone.sourcePrefabReference = previous.cockpitPreviewPrefab;
            clone.ik[0].partClass = "handlebars";
            clone.ik[0].partType = "Steering";
            clone.importWarnings.Add("Seat pose IDs 40/41 are from installed raw vehicleTruck4x4 vehicles.xml only.");
            clone.importWarnings.Add("Do not export this profile: seat positions/poses require live-game character validation.");
            clone.importWarnings.Add("Vanilla IK values have different steering pivot assumptions: untransformed camper IK stays disabled.");
            clone.importWarnings.Add("Donor exits are not copied; all camper exits remain unreviewed.");
            clone.importWarnings.Add("Do not infer six available camper seats from vanilla 4x4.");
            if (clone.seats[0].position != expectedDriver ||
                clone.seats[1].position != expectedPassenger ||
                clone.seats[0].exits.Count != 0 || clone.seats[1].exits.Count != 0 ||
                clone.ik[0].hasLeftHand || clone.ik[0].hasRightHand ||
                clone.ik[0].hasLeftFoot || clone.ik[0].hasRightFoot)
                throw new InvalidDataException("Generated preview changed physical seats, exits or enabled IK.");
            AssetDatabase.CreateAsset(clone, result.profileAsset);
            AssetDatabase.SaveAssets();
            var persisted = AssetDatabase.LoadAssetAtPath<RebirthVehicleRigProfile>(result.profileAsset);
            if (persisted == null || persisted.seats.Count != 2 ||
                persisted.seats[0].poseId != "40" || persisted.seats[1].poseId != "41")
                throw new InvalidDataException("Generated donor-pose profile failed to persist.");
            ws.profile = persisted;
            EditorUtility.SetDirty(ws);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, result.sceneAsset, true))
                throw new IOException("Unable to save separate donor-pose review scene.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            steps.Add("PASS: separate pose-40/41 profile and editable scene saved from the v1.6 camper");

            // Re-open the saved copy to prove the new reference is actually wired in.
            EditorSceneManager.OpenScene(result.sceneAsset, OpenSceneMode.Single);
            var newWorkspaces = UnityEngine.Object.FindObjectsOfType<RebirthVehicleRigWorkspace>(true);
            if (newWorkspaces.Length != 1 || newWorkspaces[0].profile == null ||
                AssetDatabase.GetAssetPath(newWorkspaces[0].profile) != result.profileAsset)
                throw new InvalidDataException("Saved review scene did not reference the NEW pose profile.");
            result.savedSeatCount = newWorkspaces[0].profile.seats.Count;
            result.driverPoseId = newWorkspaces[0].profile.seats[0].poseId;
            result.passengerPoseId = newWorkspaces[0].profile.seats[1].poseId;
            result.unchangedDriverCandidate = newWorkspaces[0].profile.seats[0].position;
            result.unchangedPassengerCandidate = newWorkspaces[0].profile.seats[1].position;
            steps.Add("PASS: saved review scene reloaded with donor poses while retaining camper seat centres");

            result.allPriorAssetsUnchanged =
                HashFile(oldProfileDisk) == oldProfileSha &&
                HashFile(oldSceneDisk) == oldSceneSha &&
                HashFile(oldPrefabDisk) == oldPrefabSha &&
                HashFile(camperFile) == camperSha &&
                HashFile(camperFile + ".meta") == camperMetaSha;
            result.sourceGameXmlUnchanged = HashFile(vehiclesXml) == gameHash;
            if (!result.allPriorAssetsUnchanged || !result.sourceGameXmlUnchanged)
                throw new InvalidDataException("A previous project asset or raw 7DTD XML changed during the test.");
            steps.Add("PASS: original camper, v1.6 scene/profile/prefab and 7DTD XML unchanged");
            warnings.Add("Pose 40/41 are REFERENCES ONLY. Neither a Unity avatar nor the actual 7DTD character was animated in this test.");
            warnings.Add("Hand IK rotations/positions captured in evidence but NOT applied or enabled on camper.");
            warnings.Add("Six 4x4 seat definitions are not copied; only two unverified camper seats remain.");
            warnings.Add("No game XML, AssetBundle, runtime inspector or REBIRTH files were changed.");
            result.status = "PASS_DONOR_XML_POSE_REVIEW_NOT_GAME_VERIFIED";
        }
        catch (Exception ex)
        {
            result.error = ex.ToString();
            result.status = "FAIL";
            Debug.LogError("REBIRTH camper 4x4 pose donor import failed: " + ex);
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
                Debug.LogError("Could not write v1.8 donor pose result: " + writeError);
                result.status = "FAIL";
            }
        }
        return result.status == "PASS_DONOR_XML_POSE_REVIEW_NOT_GAME_VERIFIED";
    }

    private static string ReadArg(string argName)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == argName) return args[i+1];
        return "";
    }
    private static bool IsId(string value) =>
        !string.IsNullOrEmpty(value) &&
        System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-9a-f]{32}$");
    private static bool IsSha(string value) =>
        !string.IsNullOrEmpty(value) &&
        System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-9a-f]{64}$");
    private static bool Finite(Vector3 v) =>
        !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
        !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    private static string HashFile(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            throw new FileNotFoundException("Required input file missing", file);
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static string DiskAsset(string project, string unityPath) =>
        Path.Combine(project, unityPath.Replace('/', Path.DirectorySeparatorChar));
    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length < 2 || parts[0] != "Assets")
            throw new InvalidDataException("Unity output must be under Assets.");
        string cur = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
