// Optional interactive editor action: export the CURRENT edited camper profile to JSON
// for review without publishing unverified seat/IK values as game-ready XML.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RebirthVehicleInspector.UnityTools;

public static class RebirthCamperProfileExporter
{
    [Serializable]
    private sealed class ReviewedProfileExport
    {
        public string kind = "REBIRTH_CAMPER_PROFILE_EDITOR_EXPORT_V1";
        public string status = "NOT_GAME_VERIFIED";
        public string sourceProfile, runId, exportUtc, targetVehicleName;
        public string sourceVehicleName, sourceGameVersion, sourceXmlStatus;
        public VehicleSeatAnchor[] seats;
        public VehicleIkAnchor[] ik;
        public string[] importWarnings;
        public bool real7dtdPoseVerified = false;
        public bool liveHandIkVerified = false;
        public bool runtimeDriverTestPassed = false;
    }
    [MenuItem("REBIRTH/Vehicle Studio/Export Selected Camper Profile JSON")]
    public static void Export()
    {
        RebirthVehicleRigProfile profile = Selection.activeObject as RebirthVehicleRigProfile;
        if (profile == null && Selection.activeGameObject != null)
        {
            var workspace = Selection.activeGameObject.GetComponent<RebirthVehicleRigWorkspace>();
            if (workspace != null) profile = workspace.profile;
        }
        if (profile == null)
        {
            EditorUtility.DisplayDialog("REBIRTH Camper", "Select the calibration scene's REBIRTH vehicle root or its profile asset first.", "OK");
            return;
        }
        string path = AssetDatabase.GetAssetPath(profile);
        const string prefix = "Assets/REBIRTHCamperCockpitGenerated/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal) ||
            !path.EndsWith("/UNAPPROVED_Camper_OccupantProfile.asset", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("REBIRTH Camper", "This is not the camper v1.6 review profile. Refusing to export an unrelated asset.", "OK");
            return;
        }
        string remainder = path.Substring(prefix.Length);
        string runId = remainder.Substring(0, remainder.IndexOf('/'));
        if (!System.Text.RegularExpressions.Regex.IsMatch(runId, "^[0-9a-f]{32}$"))
            throw new InvalidDataException("Invalid calibration run ID in profile path.");
        AssetDatabase.SaveAssets();
        var payload = new ReviewedProfileExport {
            sourceProfile = path, runId = runId,
            exportUtc = DateTime.UtcNow.ToString("o"),
            targetVehicleName = profile.targetVehicleName,
            sourceVehicleName = profile.sourceVehicleName,
            sourceGameVersion = profile.sourceGameVersion,
            sourceXmlStatus = profile.sourceXmlStatus,
            seats = profile.seats.ToArray(), ik = profile.ik.ToArray(),
            importWarnings = profile.importWarnings.ToArray()
        };
        string project = Directory.GetParent(Application.dataPath).FullName;
        string dir = Path.Combine(project, "REBIRTH_CamperCockpit_Reports", runId);
        Directory.CreateDirectory(dir);
        string filename = Path.Combine(dir, "edited_profile_export_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(filename, JsonUtility.ToJson(payload, true),
            new System.Text.UTF8Encoding(false));
        Debug.Log("REBIRTH camper profile exported (UNVERIFIED): " + filename);
        EditorUtility.RevealInFinder(filename);
    }
}
