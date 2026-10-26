using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Saves only the temporary vehicle instance, before any native review actors exist.
public static class RebirthCamperSeatingCandidate
{
    public static string SaveNative(GameObject vehicle,string sourcePath)
    {
        if(vehicle.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length!=0)throw new Exception("Native candidate must be saved before review bodies exist");
        var copy=UnityEngine.Object.Instantiate(vehicle);copy.name="rebirthCamper80s";
        try
        {
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);if(!original)throw new Exception("Exact original source required");
            string folder="Assets/REBIRTHCamperNativeSeatingGenerated/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            foreach(string path in new[]{"GameObject/M/BedPoseAsset","GameObject/M/CabinPoseAssets"}){var old=copy.transform.Find(path);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);}
            var front=copy.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="interior");var mesh=UnityEngine.Object.Instantiate(front.sharedMesh);mesh.name="NativePoseCabUpholstery";AssetDatabase.CreateAsset(mesh,folder+"/CabUpholstery.asset");front.sharedMesh=mesh;
            foreach(var t in original.GetComponentsInChildren<Transform>(true))
            {
                string path=AnimationUtility.CalculateTransformPath(t,original.transform);var dst=path.Length==0?copy.transform:copy.transform.Find(path);if(!dst)continue;dst.gameObject.SetActive(t.gameObject.activeSelf);
                var rb=t.GetComponent<Rigidbody>();if(rb)EditorUtility.CopySerialized(rb,dst.GetComponent<Rigidbody>());var light=t.GetComponent<Light>();if(light)dst.GetComponent<Light>().enabled=light.enabled;
            }
            if(copy.GetComponentsInChildren<Animator>(true).Any(a=>a.runtimeAnimatorController&&a.runtimeAnimatorController.animationClips.Any(c=>c.name.StartsWith("rebirthCamperCabinSeat")||c.name=="rebirthCamperBedLying")))throw new Exception("Authored clip dependency leaked into native source");
            string result=folder+"/rebirthCamper80s.prefab";PrefabUtility.SaveAsPrefabAsset(copy,result);AssetDatabase.SaveAssets();return result;
        }
        finally{UnityEngine.Object.DestroyImmediate(copy);}
    }
    [Serializable] public class NativeTarget {public string name;public Vector3 modelPosition,modelRotation;}
    [Serializable] public class NativeSeat {public int seat,pose;public Vector3 position,rotation;public NativeTarget[] ikTargets;}
    [Serializable] public class NativeProfile {public string scope;public NativeSeat[] occupants;}
    public static void RefineNativeBedSupport()
    {
        const string source="Assets/REBIRTHCamperNativeSeatingGenerated/b5d062097d6b44af9e95600e5ddace9e/rebirthCamper80s.prefab";
        const string folder="Assets/REBIRTHCamperNativeSeatingGenerated/BedSupport_v1_20261015";
        if(Directory.Exists(folder))throw new Exception("Preserve existing native candidate; choose a new output");
        var profile=JsonUtility.FromJson<NativeProfile>(File.ReadAllText(Path.GetDirectoryName(source)+"/NativeSeatIKProfile.json"));var bed=profile.occupants.Single(s=>s.seat==4);var copy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));copy.name="rebirthCamper80s";
        try
        {
            var model=copy.transform.Find("GameObject");var anchor=model.Find("M/REBIRTHNativeSeatIKProfiles/Seat4_Pose0");if(!anchor)throw new Exception("Native bed marker absent");
            if(Vector3.Distance(model.InverseTransformPoint(anchor.position),bed.position)>.00001f)throw new Exception("Profile/source bed anchor mismatch");
            foreach(var target in bed.ikTargets){var marker=anchor.Find(target.name);if(!marker||Vector3.Distance(model.InverseTransformPoint(marker.position),target.modelPosition)>.00001f)throw new Exception("Profile/source bed target mismatch");}
            anchor.position-=model.up*.025f;bed.position.y-=.025f;foreach(var target in bed.ikTargets)target.modelPosition.y-=.025f;
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();PrefabUtility.SaveAsPrefabAsset(copy,folder+"/rebirthCamper80s.prefab");File.WriteAllText(folder+"/NativeSeatIKProfile.json",JsonUtility.ToJson(profile,true));File.WriteAllText(folder+"/SupportChange.txt","Bed attachment and all six targets lowered25mm in model Y. Other seat targets, visible mesh buffers, physics, stations and finish unchanged. Source preserved: "+source);AssetDatabase.Refresh();AssetDatabase.SaveAssets();
        }
        finally{UnityEngine.Object.DestroyImmediate(copy);}
    }
    public static void AlignNativeBedPillow()
    {
        const string source="Assets/REBIRTHCamperNativeSeatingGenerated/BedSupport_v1_20261015/rebirthCamper80s.prefab";
        const string folder="Assets/REBIRTHCamperNativeSeatingGenerated/BedSupport_v2_20261015";
        if(Directory.Exists(folder))throw new Exception("Preserve existing pillow candidate");
        var profile=JsonUtility.FromJson<NativeProfile>(File.ReadAllText(Path.GetDirectoryName(source)+"/NativeSeatIKProfile.json"));
        var bed=profile.occupants.Single(s=>s.seat==4);var copy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try
        {
            copy.name="rebirthCamper80s";var model=copy.transform.Find("GameObject");var anchor=model.Find("M/REBIRTHNativeSeatIKProfiles/Seat4_Pose0");
            if(!anchor||Vector3.Distance(model.InverseTransformPoint(anchor.position),bed.position)>.00001f)throw new Exception("Bed source/profile mismatch");
            // Pillow top spans model X [-.7681,-.5625]; previous head joint X=-.5682 was at its inner edge.
            var delta=new Vector3(-.06f,0,0);anchor.position+=model.TransformVector(delta);bed.position+=delta;
            foreach(var target in bed.ikTargets)target.modelPosition+=delta;
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();PrefabUtility.SaveAsPrefabAsset(copy,folder+"/rebirthCamper80s.prefab");
            File.WriteAllText(folder+"/NativeSeatIKProfile.json",JsonUtility.ToJson(profile,true));
            File.WriteAllText(folder+"/SupportChange.txt","Bed anchor and all targets translated60mm toward pillow in model X. No geometry/material/other-seat changes. Requires native-body support and collision review; not accepted or installed.");AssetDatabase.Refresh();AssetDatabase.SaveAssets();
        }
        finally{UnityEngine.Object.DestroyImmediate(copy);}
    }
    public static void AttachNativeProfile(string candidate,string report)
    {
        if(!candidate.StartsWith("Assets/REBIRTHCamperNativeSeatingGenerated/"))throw new Exception("Only this new native candidate may receive profile");
        var profile=JsonUtility.FromJson<NativeProfile>(report);if(profile.occupants.Length!=6||profile.occupants.Any(s=>s.ikTargets==null||s.ikTargets.Length<4))throw new Exception("Six complete native IK profiles required");
        var copy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(candidate));copy.name="rebirthCamper80s";
        try
        {
            var model=copy.transform.Find("GameObject");var holder=new GameObject("REBIRTHNativeSeatIKProfiles");holder.transform.SetParent(model.Find("M"),false);
            foreach(var seat in profile.occupants)
            {
                var anchor=new GameObject("Seat"+seat.seat+"_Pose"+seat.pose);anchor.transform.SetParent(holder.transform,false);anchor.transform.position=model.TransformPoint(seat.position);anchor.transform.rotation=model.rotation*Quaternion.Euler(seat.rotation);
                foreach(var target in seat.ikTargets){var marker=new GameObject(target.name);marker.transform.SetParent(anchor.transform,false);marker.transform.position=model.TransformPoint(target.modelPosition);marker.transform.rotation=model.rotation*Quaternion.Euler(target.modelRotation);}
            }
            PrefabUtility.SaveAsPrefabAsset(copy,candidate);File.WriteAllText(Path.GetDirectoryName(candidate)+"/NativeSeatIKProfile.json",JsonUtility.ToJson(profile,true));AssetDatabase.Refresh();AssetDatabase.SaveAssets();
        }
        finally{UnityEngine.Object.DestroyImmediate(copy);}
    }
    public static string Save(GameObject vehicle,AnimationClip bedClip)
    {
        if(vehicle.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length!=0)
            throw new Exception("Review player skins must never enter the vehicle asset.");
        vehicle=UnityEngine.Object.Instantiate(vehicle); vehicle.name="rebirthCamper80s";
        try {
        var model=vehicle.transform.Find("GameObject")??vehicle.transform;
        if(bedClip==null||bedClip.humanMotion||bedClip.legacy)throw new Exception("Correct Generic bed dependency required.");
        foreach(int slot in new[]{2,3,5})
        {
            var holder=model.Find("M/CabinPoseAssets/Seat"+slot);
            var animator=holder==null?null:holder.GetComponent<Animator>();
            if(animator==null||animator.enabled||holder.Find("NativeRigLayout")==null||animator.runtimeAnimatorController==null)
                throw new Exception("Candidate cabin dependency absent for slot "+slot);
        }
        string folder="Assets/REBIRTHCamperSeatingGenerated/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var interior=model.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="camper_interior");
        // Persist a separate mesh copy; no original mesh is edited or destroyed.
        var mesh=UnityEngine.Object.Instantiate(interior.sharedMesh);mesh.name="CamperInteriorSeatingCandidate";
        AssetDatabase.CreateAsset(mesh,folder+"/Interior.asset");interior.sharedMesh=mesh;
        var previous=model.Find("M/BedPoseAsset");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var bed=new GameObject("BedPoseAsset");bed.transform.SetParent(model.Find("M"),false);
        var controller=AnimatorController.CreateAnimatorControllerAtPath(folder+"/BedPose.controller");controller.AddMotion(bedClip);
        var animatorBed=bed.AddComponent<Animator>();animatorBed.runtimeAnimatorController=controller;animatorBed.enabled=false;
        var layout=new GameObject("NativeRigLayout");layout.transform.SetParent(bed.transform,false);
        foreach(string path in AnimationUtility.GetCurveBindings(bedClip).Select(b=>b.path).Distinct())
        {
            if(string.IsNullOrEmpty(path))throw new Exception("Bed clip must not move the vehicle root");
            var parent=layout.transform;foreach(string name in path.Split('/')){var child=parent.Find(name);if(child==null){child=new GameObject(name).transform;child.SetParent(parent,false);}parent=child;}
        }
        var arguments=Environment.GetCommandLineArgs();int at=Array.IndexOf(arguments,"-rebirthAvatarCamperPrefab");
        if(at<0)throw new Exception("Source prefab required to preserve production defaults");
        var original=AssetDatabase.LoadAssetAtPath<GameObject>(arguments[at+1]);
        foreach(var t in original.GetComponentsInChildren<Transform>(true))
        {
            string path=AnimationUtility.CalculateTransformPath(t,original.transform);
            var dst=path.Length==0?vehicle.transform:vehicle.transform.Find(path);
            if(dst==null)continue;
            dst.gameObject.SetActive(t.gameObject.activeSelf);
            var rb=t.GetComponent<Rigidbody>();if(rb!=null)EditorUtility.CopySerialized(rb,dst.GetComponent<Rigidbody>());
            var light=t.GetComponent<Light>();if(light!=null)dst.GetComponent<Light>().enabled=light.enabled;
        }
        string result=folder+"/rebirthCamper80s.prefab";PrefabUtility.SaveAsPrefabAsset(vehicle,result);AssetDatabase.SaveAssets();
        var reopened=AssetDatabase.LoadAssetAtPath<GameObject>(result);
        if(reopened==null||reopened.GetComponentsInChildren<WheelCollider>(true).Length!=4||reopened.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length!=0)
            throw new Exception("Saved candidate physics/no-player contract failed");
        return result;
        } finally {UnityEngine.Object.DestroyImmediate(vehicle);}
    }
}


