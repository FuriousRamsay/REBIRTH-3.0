using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    [CustomEditor(typeof(RebirthVehicleRigWorkspace))]
    public sealed class RebirthVehicleRigWorkspaceEditor : Editor
    {
        private enum EditKind { Seats, Exits, HandIK, FootIK }
        private EditKind editKind;
        private int seatChoice, ikChoice;
        private bool showAllMarkers = true;

        private RebirthVehicleRigWorkspace Workspace { get { return (RebirthVehicleRigWorkspace)target; } }

        public override void OnInspectorGUI()
        {
            RebirthVehicleRigWorkspace w = Workspace;
            EditorGUILayout.LabelField("7DTD VEHICLE AUTHORING", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edits apply to a ScriptableObject profile, never to the FBX source. " +
                "Generated pivots start at origin and must be aligned. Unity pose markers are NOT " +
                "the real 7DTD animated character.", MessageType.Info);
            DrawDefaultInspector();
            if (w.profile == null) return;
            var p = w.profile;
            if (GUILayout.Button("Create missing referenced joint paths (scene only)"))
            {
                int n = RebirthVehicleRigBuilder.EnsureReferenceHierarchy(w);
                Debug.Log("REBIRTH rig: created paths for " + n + " references. Calibrate pivots manually.");
            }
            showAllMarkers = EditorGUILayout.ToggleLeft("Show all anchors in Scene", showAllMarkers);
            editKind = (EditKind)GUILayout.Toolbar((int)editKind, new [] {"Seats", "Exits", "Hands", "Feet"});
            if (editKind == EditKind.Seats || editKind == EditKind.Exits) SeatInspector(p);
            else IkInspector(p);
            if (GUILayout.Button("Select profile asset for wheel/reference path editing"))
                Selection.activeObject = p;
            if (GUI.changed) SceneView.RepaintAll();
        }
        private void SeatInspector(RebirthVehicleRigProfile p)
        {
            if (p.seats.Count == 0)
            {
                EditorGUILayout.HelpBox("No seats imported. Create seat0 before exporting.", MessageType.Warning);
            }
            else
            {
                seatChoice = Mathf.Clamp(seatChoice, 0, p.seats.Count - 1);
                string[] names = new string[p.seats.Count];
                for (int i=0; i<names.Length; i++) names[i] = "seat" + p.seats[i].index + " (pose " + p.seats[i].poseId + ")";
                seatChoice = EditorGUILayout.Popup("Editing", seatChoice, names);
                VehicleSeatAnchor s = p.seats[seatChoice];
                string pose = EditorGUILayout.TextField("Pose ID (verified game XML)", s.poseId);
                Vector3 pos = EditorGUILayout.Vector3Field("Position (vehicle local)", s.position);
                Vector3 rot = EditorGUILayout.Vector3Field("Rotation Euler", s.rotationEuler);
                if (pose != s.poseId || pos != s.position || rot != s.rotationEuler)
                {
                    Undo.RecordObject(p, "Edit 7DTD seat");
                    s.poseId = pose; s.position = pos; s.rotationEuler = rot;
                    EditorUtility.SetDirty(p);
                }
                if (editKind == EditKind.Exits)
                {
                    for (int i=0; i<s.exits.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        Vector3 next = EditorGUILayout.Vector3Field("Exit " + i, s.exits[i]);
                        if (next != s.exits[i])
                        { Undo.RecordObject(p, "Edit exit"); s.exits[i] = next; EditorUtility.SetDirty(p); }
                        bool remove = GUILayout.Button("-", GUILayout.Width(24));
                        EditorGUILayout.EndHorizontal();
                        if (remove)
                        { Undo.RecordObject(p, "Remove exit"); s.exits.RemoveAt(i); EditorUtility.SetDirty(p); break; }
                    }
                    if (GUILayout.Button("Add exit attempt"))
                    { Undo.RecordObject(p, "Add exit"); s.exits.Add(s.position + Vector3.right * 1.3f); EditorUtility.SetDirty(p); }
                }
            }
            if (GUILayout.Button("Add next available seat (seat0 through seat5)"))
            {
                int next=-1;
                for (int id=0; id<6; id++)
                    if (!p.seats.Exists(s => s.index==id)) { next=id; break; }
                if (next < 0) EditorUtility.DisplayDialog("Seat limit", "Documented seat0 through seat5 are already defined.", "OK");
                else
                {
                    Undo.RecordObject(p, "Add vehicle seat");
                    p.seats.Add(new VehicleSeatAnchor { index = next });
                    p.seats.Sort((a,b) => a.index.CompareTo(b.index));
                    seatChoice = p.seats.FindIndex(s=>s.index==next);
                    EditorUtility.SetDirty(p);
                }
            }
        }
        private void IkInspector(RebirthVehicleRigProfile p)
        {
            if (p.ik.Count == 0)
            {
                EditorGUILayout.HelpBox("No IK definitions in imported reference. Do not assume custom limb IK is supported.", MessageType.Warning);
                return;
            }
            ikChoice = Mathf.Clamp(ikChoice, 0, p.ik.Count - 1);
            string[] names = new string[p.ik.Count];
            for (int i=0; i<names.Length; i++) names[i] = p.ik[i].partClass;
            ikChoice = EditorGUILayout.Popup("Component", ikChoice, names);
            var ik = p.ik[ikChoice];
            string path = EditorGUILayout.TextField("Pivot XML path", ik.pivotPath);
            if (path != ik.pivotPath)
            { Undo.RecordObject(p, "Change IK pivot"); ik.pivotPath = path; EditorUtility.SetDirty(p); }
            if (editKind == EditKind.HandIK)
            {
                EditIKPoint(p,"Left Hand",ref ik.hasLeftHand,ref ik.leftHandPosition,ref ik.leftHandRotationEuler);
                EditIKPoint(p,"Right Hand",ref ik.hasRightHand,ref ik.rightHandPosition,ref ik.rightHandRotationEuler);
            }
            else
            {
                if (ik.partClass != "pedals")
                    EditorGUILayout.HelpBox("Foot IK is documented for pedal components, not arbitrary vehicle classes.", MessageType.Warning);
                else
                {
                    EditIKPoint(p,"Left Foot",ref ik.hasLeftFoot,ref ik.leftFootPosition,ref ik.leftFootRotationEuler);
                    EditIKPoint(p,"Right Foot",ref ik.hasRightFoot,ref ik.rightFootPosition,ref ik.rightFootRotationEuler);
                }
            }
        }
        private static void EditIKPoint(RebirthVehicleRigProfile p,string label,ref bool has,
            ref Vector3 position,ref Vector3 rotation)
        {
            bool next = EditorGUILayout.ToggleLeft("Enable " + label + " override",has);
            Vector3 pos = EditorGUILayout.Vector3Field(label + " position", position);
            Vector3 rot = EditorGUILayout.Vector3Field(label + " rotation", rotation);
            if (next != has || pos != position || rot != rotation)
            {
                Undo.RecordObject(p, "Edit vehicle IK " + label);
                has=next; position=pos;rotation=rot;EditorUtility.SetDirty(p);
            }
        }
        private void OnSceneGUI()
        {
            var w=Workspace;
            if(w==null || w.profile==null) return;
            var p=w.profile;
            var root=w.transform;
            if (showAllMarkers)
            {
                foreach(var seat in p.seats)
                {
                    Vector3 at=root.TransformPoint(seat.position);
                    Handles.color=seat.index==0 ? Color.cyan : Color.yellow;
                    Handles.DrawWireDisc(at,root.up,0.16f);
                    Handles.ArrowHandleCap(0,at,root.rotation*Quaternion.Euler(seat.rotationEuler),0.42f,EventType.Repaint);
                    Handles.Label(at+Vector3.up*0.15f,"seat"+seat.index+" · pose "+seat.poseId);
                    foreach(Vector3 exit in seat.exits)
                    {
                        Handles.color=Color.yellow;
                        Handles.DrawWireDisc(root.TransformPoint(exit),root.up,0.13f);
                    }
                }
            }
            if (editKind==EditKind.Seats && p.seats.Count>0)
            {
                var s=p.seats[Mathf.Clamp(seatChoice,0,p.seats.Count-1)];
                Quaternion rotation=root.rotation*Quaternion.Euler(s.rotationEuler);
                EditorGUI.BeginChangeCheck();
                Vector3 at=Handles.PositionHandle(root.TransformPoint(s.position),rotation);
                Quaternion newRot=Handles.RotationHandle(rotation,at);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(p,"Move/rotate vehicle seat");
                    s.position=root.InverseTransformPoint(at);
                    s.rotationEuler=(Quaternion.Inverse(root.rotation)*newRot).eulerAngles;
                    EditorUtility.SetDirty(p);
                }
            }
            else if (editKind==EditKind.Exits && p.seats.Count>0)
            {
                var s=p.seats[Mathf.Clamp(seatChoice,0,p.seats.Count-1)];
                for(int i=0; i<s.exits.Count; i++)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 at=Handles.PositionHandle(root.TransformPoint(s.exits[i]),root.rotation);
                    if(EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(p,"Move vehicle exit");
                        s.exits[i]=root.InverseTransformPoint(at);EditorUtility.SetDirty(p);
                    }
                    Handles.Label(at,"exit"+i);
                }
            }
            else if(p.ik.Count>0 && (editKind==EditKind.HandIK || editKind==EditKind.FootIK))
            {
                VehicleIkAnchor ik=p.ik[Mathf.Clamp(ikChoice,0,p.ik.Count-1)];
                Transform pivot=RebirthVehicleRigBuilder.FindPath(root,ik.pivotPath);
                if(pivot==null)
                {
                    Handles.Label(root.position+Vector3.up*1.5f,
                        "IK disabled: unresolved pivot '"+ik.pivotPath+"'");
                    return;
                }
                if(editKind==EditKind.HandIK)
                {
                    if(ik.hasLeftHand) EditSceneIK(p,pivot,ik.leftHandPosition,ik.leftHandRotationEuler,"Left hand",(a,b)=>{
                        ik.leftHandPosition=a;ik.leftHandRotationEuler=b;});
                    if(ik.hasRightHand) EditSceneIK(p,pivot,ik.rightHandPosition,ik.rightHandRotationEuler,"Right hand",(a,b)=>{
                        ik.rightHandPosition=a;ik.rightHandRotationEuler=b;});
                }
                else if(ik.partClass=="pedals")
                {
                    if(ik.hasLeftFoot) EditSceneIK(p,pivot,ik.leftFootPosition,ik.leftFootRotationEuler,"Left foot",(a,b)=>{
                        ik.leftFootPosition=a;ik.leftFootRotationEuler=b;});
                    if(ik.hasRightFoot) EditSceneIK(p,pivot,ik.rightFootPosition,ik.rightFootRotationEuler,"Right foot",(a,b)=>{
                        ik.rightFootPosition=a;ik.rightFootRotationEuler=b;});
                }
            }
        }
        private static void EditSceneIK(RebirthVehicleRigProfile profile,Transform pivot,Vector3 local,
            Vector3 localEuler,string name,Action<Vector3,Vector3> apply)
        {
            Quaternion rot=pivot.rotation*Quaternion.Euler(localEuler);
            EditorGUI.BeginChangeCheck();
            Vector3 at=Handles.PositionHandle(pivot.TransformPoint(local),rot);
            Quaternion newRot=Handles.RotationHandle(rot,at);
            Handles.Label(at,name+" (relative to "+pivot.name+")");
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile,"Move " + name + " IK");
                apply(pivot.InverseTransformPoint(at),(Quaternion.Inverse(pivot.rotation)*newRot).eulerAngles);
                EditorUtility.SetDirty(profile);
            }
        }
    }
}
