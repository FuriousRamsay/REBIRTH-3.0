using System;
using System.IO;
using UnityEngine;
using Rebirth.Vehicles.Authoring;

public static class RebirthSeatPoseEditorChecks
{
    static void Require(bool pass, string label) { if (!pass) throw new Exception(label); }
    public static void Run()
    {
        const string output="E:/_Haven/shared/REBIRTHTemp/SeatPoseEditorChecks_v2_20261015";
        Directory.CreateDirectory(output);
        var profile=new SeatPoseProfile {vehicle="test",rig="generic-test",seat=4,basePose=0};
        profile.leftHand.close=-.4f;profile.rightHand.curl[2]=.6f;
        Require(SeatPoseProfile.Parse(profile.Serialize()).leftHand.close==-.4f,"signed hand round-trip");
        var history=new SeatPoseHistory();history.Commit(profile);profile.basePose=41;history.Commit(profile);
        Require(history.Undo().basePose==0&&history.Redo().basePose==41,"undo redo");
        profile.anchorPosition=new Vector3(1,2,3);profile.anchorRotation=new Vector3(20,70,5);
        profile.targets=new[]{new SeatPoseTransform{name="IKHandL",position=new Vector3(.4f,2.2f,3.1f),rotation=new Vector3(10,40,80)},new SeatPoseTransform{name="IKHandR"}};
        Vector3 originalTarget=profile.targets[0].position;Quaternion originalTargetRotation=Quaternion.Euler(profile.targets[0].rotation);
        profile.MirrorTarget("IKHandL");profile.MirrorTarget("IKHandR");
        Require(Vector3.Distance(originalTarget,profile.targets[0].position)<.00001f&&Quaternion.Angle(originalTargetRotation,Quaternion.Euler(profile.targets[0].rotation))<.02f,"rotated-frame mirror round-trip");
        SeatPoseProfileStore.Save(output,"roundtrip",profile);string before=File.ReadAllText(output+"/roundtrip.json");
        profile.anchorPosition.x=float.NaN;bool rejected=false;
        try{SeatPoseProfileStore.Save(output,"roundtrip",profile);}catch(InvalidDataException){rejected=true;}
        Require(rejected&&File.ReadAllText(output+"/roundtrip.json")==before,"invalid save preserves previous");profile.anchorPosition=Vector3.zero;
        var root=new GameObject("Body");
        try
        {
            var joint=new GameObject("Joint");joint.transform.SetParent(root.transform,false);joint.transform.localRotation=Quaternion.Euler(12,23,34);
            Quaternion original=joint.transform.localRotation;profile.bones=new[]{new SeatPoseBoneOffset {path="Joint",rotation=new Vector3(20,-30,8)}};
            using(var bones=new SeatPoseBones(root.transform))
            {
                for(int i=0;i<1000;i++){bones.ApplyOffsets(profile);bones.RemoveOffsets();}
                Require(Quaternion.Angle(original,joint.transform.localRotation)<.02f,"1000 offset cycles drift");
                bones.ApplyOffsets(profile);
            }
            Require(Quaternion.Angle(original,joint.transform.localRotation)<.02f,"dispose restores offsets");
            File.WriteAllText(output+"/passed.txt","PASS: signed profile round-trip; undo/redo; rotated-frame target mirror round-trip; invalid save preserves previous; 1000 base-relative bone offset cycles and dispose. Synthetic Unity transform test only; no native body, camera, game or multiplayer validation.");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
