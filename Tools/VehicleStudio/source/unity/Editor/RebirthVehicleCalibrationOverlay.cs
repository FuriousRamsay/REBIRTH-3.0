// REBIRTH Vehicle Studio v0.4: read-only aggregated occupant calibration overlay.
// Consumes reports emitted by tools/vehicle_calibration.py, NOT arbitrary captures.
// Draws the measured game skeleton on a selected target scene workspace as a
// comparison aid. Does not edit rig profiles, seats, IK or the source FBX.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public sealed class RebirthVehicleCalibrationOverlay : EditorWindow
    {
        [Serializable] private class V3 { public float x, y, z;
            public Vector3 ToVector() { return new Vector3(x, y, z); } }
        [Serializable] private class Bone
        {
            public string name, source, note;
            public int sampleCount;
            public V3 median, p10, p90;
            public float maxDistanceFromMedianMetres;
        }
        [Serializable] private class Seat
        {
            public int seatIndex, entityId, seatFrames, captureFrames;
            public string seatPoseIdFromXml, associationLevel;
            public string[] associationSources;
            public Bone[] bones;
        }
        [Serializable] private class Session
        {
            public string label, sourceCapture, sourceKind, gameVersion;
            public string vehicleXmlName, xmlStatus, captureType;
            public int captureFrames;
            public string[] warnings;
            public Seat[] seats;
        }
        [Serializable] private class CalibrationReport
        {
            public int schemaVersion;
            public string reportType, coordinateFrame, units, disclaimer;
            public bool autoApplySafe;
            public Session[] sessions;
        }

        private RebirthVehicleRigWorkspace workspace;
        private CalibrationReport report;
        private string fileName, warning = "";
        private int sessionChoice, seatChoice;
        private bool showBones = true;
        private bool showLabels = true;
        private bool showReferenceSeat = true;
        private bool showGripMarkers = true;
        private bool includeAllBones = false;
        private Vector2 scroll;
        private static readonly string[] KeyBones = {
            "Hips", "Spine", "Head", "LeftShoulder", "RightShoulder",
            "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm",
            "LeftHand", "RightHand", "LeftUpperLeg", "RightUpperLeg",
            "LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot", "LeftToes", "RightToes"
        };

        [MenuItem("Tools/REBIRTH/Occupant Calibration Overlay")]
        public static void Open() { GetWindow<RebirthVehicleCalibrationOverlay>("7DTD Seat Calibration"); }

        private void OnEnable() { SceneView.duringSceneGui += OnSceneView; }
        private void OnDisable() { SceneView.duringSceneGui -= OnSceneView; }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Runtime Occupant Calibration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bone markers are measured in a captured vanilla/custom vehicle's LOCAL root frame. " +
                "A different FBX may have another origin, scale and geometry. " +
                "This is a visual COMPARISON, not proof that an IK/seat target is correct. " +
                "Nothing is applied to XML or prefabs automatically.", MessageType.Warning);
            workspace = (RebirthVehicleRigWorkspace)EditorGUILayout.ObjectField(
                "Scene rig workspace", workspace, typeof(RebirthVehicleRigWorkspace), true);
            if (GUILayout.Button("Load calibration report JSON")) Load();
            EditorGUILayout.LabelField("Report", string.IsNullOrEmpty(fileName) ? "None" : Path.GetFileName(fileName));
            if (!string.IsNullOrEmpty(warning)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            if (report == null) return;

            Session session = SelectSession();
            if (session == null) return;
            EditorGUILayout.LabelField("Game version", session.gameVersion ?? "unknown");
            EditorGUILayout.LabelField("Reference vehicle", session.vehicleXmlName ?? "unbound");
            EditorGUILayout.LabelField("Source", session.sourceKind ?? "unknown");
            EditorGUILayout.LabelField("Recorded frames", session.captureFrames.ToString());
            if (session.sourceKind == "synthetic")
                EditorGUILayout.HelpBox("SYNTHETIC fixture — this is not a real game capture.", MessageType.Error);
            if (session.xmlStatus != "ok")
                EditorGUILayout.HelpBox("Reference XML was not bound; pose IDs may be unavailable.", MessageType.Warning);

            Seat selected = SelectSeat(session);
            showBones = EditorGUILayout.ToggleLeft("Draw measured bones", showBones);
            showLabels = EditorGUILayout.ToggleLeft("Show labels", showLabels);
            showReferenceSeat = EditorGUILayout.ToggleLeft("Show authored target seat and pelvis link", showReferenceSeat);
            showGripMarkers = EditorGUILayout.ToggleLeft("Show authored hand and supported pedal IK vs measured limbs", showGripMarkers);
            if (workspace != null && workspace.profile != null &&
                session.vehicleXmlName != workspace.profile.targetVehicleName)
                EditorGUILayout.HelpBox("The captured vehicle does not match this authoring target. " +
                    "Grip-to-bone distance will NOT be calculated across different vehicle origins.", MessageType.Warning);
            includeAllBones = EditorGUILayout.ToggleLeft("Include all observed bones (can clutter scene)", includeAllBones);
            if (selected != null)
            {
                EditorGUILayout.LabelField("Evidence", selected.associationLevel ?? "unknown");
                EditorGUILayout.LabelField("Attached frames", selected.seatFrames + " / " + selected.captureFrames);
                EditorGUILayout.LabelField("XML pose ID", selected.seatPoseIdFromXml ?? "");
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(75));
                foreach (Bone bone in selected.bones ?? new Bone[0])
                {
                    if (bone == null || bone.median == null) continue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(bone.name + " (" + bone.sampleCount + " frames)", GUILayout.MinWidth(165));
                    if (GUILayout.Button("Copy x,y,z", GUILayout.Width(85)))
                    {
                        Vector3 p = bone.median.ToVector();
                        EditorGUIUtility.systemCopyBuffer =
                            p.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                            p.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                            p.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            foreach (string message in session.warnings ?? new string[0])
                EditorGUILayout.HelpBox(message, MessageType.Info);
            if (GUI.changed) SceneView.RepaintAll();
        }

        private Session SelectSession()
        {
            if (report.sessions == null || report.sessions.Length == 0)
            {
                EditorGUILayout.HelpBox("No capture sessions in report.", MessageType.Warning);
                return null;
            }
            sessionChoice = Mathf.Clamp(sessionChoice, 0, report.sessions.Length - 1);
            string[] names = new string[report.sessions.Length];
            for (int i = 0; i < names.Length; i++) names[i] = report.sessions[i].label ?? "Capture " + i;
            sessionChoice = EditorGUILayout.Popup("Capture", sessionChoice, names);
            return report.sessions[sessionChoice];
        }

        private Seat SelectSeat(Session session)
        {
            if (session.seats == null || session.seats.Length == 0)
            {
                EditorGUILayout.HelpBox("No verified vehicle reference WITH reported seat ID. " +
                    "Nearby people cannot be used for rig calibration.", MessageType.Warning);
                return null;
            }
            seatChoice = Mathf.Clamp(seatChoice, 0, session.seats.Length - 1);
            string[] names = new string[session.seats.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = "seat" + session.seats[i].seatIndex + " / entity #" + session.seats[i].entityId;
            seatChoice = EditorGUILayout.Popup("Occupant", seatChoice, names);
            return session.seats[seatChoice];
        }

        private void Load()
        {
            string path = EditorUtility.OpenFilePanel("Select occupant calibration report", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var parsed = JsonUtility.FromJson<CalibrationReport>(File.ReadAllText(path));
                if (parsed == null || parsed.schemaVersion != 1 ||
                    parsed.reportType != "rebirthVehicleOccupantCalibration" ||
                    parsed.coordinateFrame != "captured_vehicle_root_local" ||
                    parsed.autoApplySafe || parsed.sessions == null || parsed.sessions.Length == 0)
                    throw new InvalidDataException("Expected v0.4 read-only occupant calibration schema.");
                report = parsed;
                fileName = path;
                sessionChoice = 0;
                seatChoice = 0;
                warning = "The measurements are observed bones, not verified target-FBX IK positions.";
                SceneView.RepaintAll();
            }
            catch (Exception e)
            {
                report = null;
                warning = "Could not load report: " + e.Message;
            }
        }

        private void OnSceneView(SceneView scene)
        {
            if (workspace == null || report == null || report.sessions == null ||
                sessionChoice < 0 || sessionChoice >= report.sessions.Length) return;
            Session session = report.sessions[sessionChoice];
            if (session == null || session.seats == null || seatChoice < 0 || seatChoice >= session.seats.Length) return;
            Seat seat = session.seats[seatChoice];
            if (seat == null) return;
            Transform root = workspace.transform;
            Vector3? pelvis = null;
            foreach (Bone bone in seat.bones ?? new Bone[0])
            {
                if (bone == null || bone.median == null || !Valid(bone.median)) continue;
                if (!includeAllBones && Array.IndexOf(KeyBones, bone.name) < 0) continue;
                Vector3 at = root.TransformPoint(bone.median.ToVector());
                bool isHand = bone.name.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isFoot = bone.name.IndexOf("Foot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              bone.name.IndexOf("Toes", StringComparison.OrdinalIgnoreCase) >= 0;
                Handles.color = isHand ? Color.green : isFoot ? Color.magenta : Color.white;
                if (showBones)
                {
                    float size = HandleUtility.GetHandleSize(at) * 0.055f;
                    Handles.SphereHandleCap(0, at, Quaternion.identity, size, EventType.Repaint);
                    if (showLabels) Handles.Label(at + Vector3.up * size, bone.name + " [" + bone.sampleCount + " samples]");
                }
                if (bone.name == "Hips" || bone.name == "Pelvis") pelvis = at;
            }
            if (showGripMarkers && workspace.profile != null &&
                string.Equals(session.vehicleXmlName, workspace.profile.targetVehicleName,
                    StringComparison.Ordinal) && session.xmlStatus == "ok")
                DrawGripComparison(root, workspace.profile, seat);
            if (showReferenceSeat && workspace.profile != null)
            {
                VehicleSeatAnchor targetSeat = workspace.profile.seats.Find(s => s != null && s.index == seat.seatIndex);
                if (targetSeat == null) return;
                Vector3 seatAt = root.TransformPoint(targetSeat.position);
                Handles.color = Color.cyan;
                Handles.DrawWireDisc(seatAt, root.up, 0.19f);
                if (showLabels) Handles.Label(seatAt, "Authored seat" + seat.seatIndex + " (pose " + targetSeat.poseId + ")");
                // Seat origin and pelvis bone are NOT expected to coincide. The line
                // is a visualization of two different anchors, NOT an error vector.
                if (pelvis.HasValue && showBones)
                {
                    Handles.color = Color.gray;
                    Handles.DrawDottedLine(seatAt, pelvis.Value, 4f);
                }
            }
        }

        // Measured skeletal hand/foot bone positions are not identical to the
        // Animator IK targets. Draw distances as observations only and NEVER
        // apply these numbers automatically to Unity/7DTD XML.
        private static void DrawGripComparison(Transform root, RebirthVehicleRigProfile profile, Seat seat)
        {
            foreach (VehicleIkAnchor ik in profile.ik)
            {
                if (ik == null) continue;
                Transform pivot = RebirthVehicleRigBuilder.FindPath(root, ik.pivotPath);
                if (pivot == null) continue;
                if (ik.hasLeftHand) DrawObservedPair(root, pivot, ik.leftHandPosition, seat, "LeftHand", "Left grip");
                if (ik.hasRightHand) DrawObservedPair(root, pivot, ik.rightHandPosition, seat, "RightHand", "Right grip");
                if (ik.partClass == "pedals")
                {
                    if (ik.hasLeftFoot) DrawObservedPair(root, pivot, ik.leftFootPosition, seat, "LeftFoot", "Left pedal");
                    if (ik.hasRightFoot) DrawObservedPair(root, pivot, ik.rightFootPosition, seat, "RightFoot", "Right pedal");
                }
            }
        }
        private static void DrawObservedPair(Transform root, Transform pivot, Vector3 localTarget,
            Seat seat, string boneName, string gripName)
        {
            Vector3 target = pivot.TransformPoint(localTarget);
            if (!Finite(target.x) || !Finite(target.y) || !Finite(target.z)) return;
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(target, Vector3.up, 0.08f);
            Handles.Label(target, gripName + " XML IK marker");
            if (seat.bones == null) return;
            foreach (Bone bone in seat.bones)
            {
                if (bone == null || bone.name != boneName || bone.source != "humanoid" ||
                    bone.median == null || !Valid(bone.median)) continue;
                Vector3 observed = root.TransformPoint(bone.median.ToVector());
                float distance = Vector3.Distance(observed, target);
                Handles.color = Color.yellow;
                Handles.DrawDottedLine(target, observed, 3f);
                Handles.Label(Vector3.Lerp(target, observed, 0.5f),
                    gripName + " observed bone-to-marker " +
                    distance.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                    "m (NOT a calibrated IK error)");
                break;
            }
        }

        private static bool Valid(V3 p)
        {
            return Finite(p.x) && Finite(p.y) && Finite(p.z) &&
                   Mathf.Abs(p.x) <= 10000f && Mathf.Abs(p.y) <= 10000f && Mathf.Abs(p.z) <= 10000f;
        }
        private static bool Finite(float n) { return !float.IsInfinity(n) && !float.IsNaN(n); }
    }
}
