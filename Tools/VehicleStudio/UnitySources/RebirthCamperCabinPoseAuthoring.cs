using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Candidate poses on the real generic BaseRig. Never modifies original rig/controllers.
public static class RebirthCamperCabinPoseAuthoring
{
    public const string HolderPath="M/CabinPoseAssets";
    const string RigAsset="Assets/AssetBundles/Player/Common/BaseRigs/baseRigPrefab.prefab";
    const string ControllerAsset="Assets/AssetBundles/Player/Common/AnimControllers/3PPlayerMaleController.controller";
    [Serializable] public class SeatEvidence {public int seat;public string clip;public Vector3 nativePelvisOffset,leftKnee,leftFoot,rightKnee,rightFoot;public float floorY,leftThighAngle,rightThighAngle;public bool nativeBoneLengthsPreserved=true,comfortAccepted=false;}
    [Serializable] public class Report {public string status="AUTHORED_CANDIDATES_NOT_COMFORT_ACCEPTED",xml;public SeatEvidence[] seats;}

    public static AnimationClip[] Apply(GameObject root,string folder,string seatXmlPath)
    {
        var model=root.transform.Find("GameObject")??root.transform;
        if(model.Find(HolderPath)!=null || model.Find("M")==null)throw new Exception("Cabin pose holder exists or model M is missing.");
        if(!File.Exists(seatXmlPath))throw new Exception("Exact candidate seat XML required for floor-fit authoring.");
        string game=Arg("-rebirthAvatarGameRoot","C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die");
        var bundle=AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(b=>b.GetAllAssetNames().Any(n=>n.Equals(RigAsset,StringComparison.OrdinalIgnoreCase)));
        bool owned=bundle==null;if(owned)bundle=AssetBundle.LoadFromFile(Path.Combine(game,"Data/Addressables/Standalone/player_assets_entities/player/common.bundle"));
        if(bundle==null)throw new Exception("Native BaseRig bundle unavailable.");
        try
        {
            var source=bundle.LoadAsset<GameObject>(RigAsset);var controller=bundle.LoadAsset<RuntimeAnimatorController>(ControllerAsset);
            if(source==null||controller==null)throw new Exception("Exact native generic rig/controller unavailable.");
            var document=new XmlDocument();document.Load(seatXmlPath);var vehicle=document.SelectSingleNode("/configs/append/vehicle[@name='rebirthCamper80s']");if(vehicle==null)throw new Exception("Candidate camper XML missing.");
            Vector3 pelvis=NativePelvis(source,controller);
            var parent=new GameObject("CabinPoseAssets");parent.transform.SetParent(model.Find("M"),false);
            var clips=new List<AnimationClip>();var evidence=new List<SeatEvidence>();
            foreach(int seat in new[]{2,3,5})
            {
                var node=vehicle.SelectSingleNode("property[@class='seat"+seat+"']");if(node==null)throw new Exception("Candidate slot missing: "+seat);
                var position=Vec(Value(node,"position"));var rotation=Quaternion.Euler(Vec(Value(node,"rotation")));
                var rig=UnityEngine.Object.Instantiate(source);rig.SetActive(false);
                try
                {
                    var animator=rig.GetComponent<Animator>();if(animator==null||animator.avatar==null||!animator.avatar.isValid||animator.avatar.isHuman)throw new Exception("Expected actual native generic rig.");
                    foreach(var behavior in rig.GetComponentsInChildren<MonoBehaviour>(true))if(behavior!=null)behavior.enabled=false;
                    var bones=rig.GetComponentsInChildren<Transform>(true);Func<string,Transform> bone=name=>{var matches=bones.Where(t=>t.name==name).ToArray();if(matches.Length!=1)throw new Exception("Native seated bone absent/ambiguous: "+name);return matches[0];};
                    var hips=bone("Hips");var feetRest=new[]{bone("LeftFoot").rotation,bone("RightFoot").rotation};
                    float sole=Mathf.Max(.02f,(rig.transform.InverseTransformPoint(bone("LeftFoot").position).y+rig.transform.InverseTransformPoint(bone("RightFoot").position).y)*.5f);
                    hips.position=rig.transform.TransformPoint(pelvis); // Retain actual native pose41 pelvis basis.
                    // The high sink cushion leaves insufficient upright head clearance.
                    // Distribute a forward lean through the spine without moving the
                    // supported pelvis or changing any native bone lengths.
                    if(seat==5)
                        foreach(string spine in new[]{"Spine","Spine1","Spine2","Spine3"})
                        {var joint=bone(spine);joint.rotation=Quaternion.AngleAxis(2f,rig.transform.right)*joint.rotation;}
                    // Keep the dinette pelvis on its narrow cushion while bringing
                    // shoulders inboard of the wall-mounted cabinets and curtains.
                    if(seat==2||seat==3)
                    {
                        var inboard=Quaternion.Inverse(rotation)*Vector3.left;
                        var leanAxis=rig.transform.TransformDirection(Vector3.Cross(Vector3.up,inboard));
                        foreach(string spine in new[]{"Spine","Spine1","Spine2","Spine3"})
                        {var joint=bone(spine);joint.rotation=Quaternion.AngleAxis(4f,leanAxis)*joint.rotation;}
                    }
                    var row=new SeatEvidence{seat=seat,clip="rebirthCamperCabinSeat"+seat,nativePelvisOffset=pelvis};
                    foreach(string side in new[]{"Left","Right"})
                    {
                        var upper=bone(side+"UpLeg");var knee=bone(side+"Leg");var foot=bone(side+"Foot");float upperLength=Vector3.Distance(upper.position,knee.position),lowerLength=Vector3.Distance(knee.position,foot.position);
                        // Dinette outer feet otherwise land inside the raised bench
                        // platform. Keep both feet in the measured aisle instead.
                        var aisleOffset=seat==2||seat==3?Quaternion.Inverse(rotation)*(Vector3.left*.28f):Vector3.zero;
                        float lateral=aisleOffset.x/upperLength;
                        float floor=FloorAt(model,position+rotation*(rig.transform.InverseTransformPoint(upper.position)+aisleOffset+Vector3.forward*upperLength));
                        float desiredKnee=floor-position.y+sole+lowerLength;
                        float rise=(desiredKnee-rig.transform.InverseTransformPoint(upper.position).y)/upperLength;
                        if(Mathf.Abs(rise)>.85f)throw new Exception("Measured seat/floor cannot support bounded natural leg geometry at slot "+seat);
                        if(lateral*lateral+rise*rise>=.95f)throw new Exception("Measured aisle foot target exceeds native thigh reach");
                        Aim(upper,knee,rig.transform.TransformDirection(new Vector3(lateral,rise,Mathf.Sqrt(1-rise*rise-lateral*lateral))));
                        Aim(knee,foot,-rig.transform.up);foot.rotation=feetRest[side=="Left"?0:1];
                        // Two-bone arms put genuine wrists over the lap, with elbows
                        // tucked downward to clear the dinette's wall furnishings.
                        float sign=side=="Left"?-1:1;var hand=bone(side+"Hand");
                        TwoBone(bone(side+"Arm"),bone(side+"ForeArm"),hand,
                            hips.position+rig.transform.TransformDirection(new Vector3(sign*.16f,.10f,.25f)),
                            rig.transform.TransformDirection(new Vector3(sign*.08f,-.7f,.2f)));
                        Aim(hand,bone(side+"HandMiddle1"),rig.transform.forward);
                        if(side=="Left"){row.leftThighAngle=Mathf.Asin(rise)*Mathf.Rad2Deg;row.leftKnee=rig.transform.InverseTransformPoint(knee.position);row.leftFoot=rig.transform.InverseTransformPoint(foot.position);}
                        else{row.rightThighAngle=Mathf.Asin(rise)*Mathf.Rad2Deg;row.rightKnee=rig.transform.InverseTransformPoint(knee.position);row.rightFoot=rig.transform.InverseTransformPoint(foot.position);}
                        row.floorY=floor;
                    }
                    var clip=WriteClip(rig.transform,hips,folder,row.clip);clips.Add(clip);evidence.Add(row);
                    var holder=new GameObject("Seat"+seat);holder.transform.SetParent(parent.transform,false);
                    var assetController=AnimatorController.CreateAnimatorControllerAtPath(folder+"/CabinSeat"+seat+"Asset.controller");assetController.AddMotion(clip);
                    var asset=holder.AddComponent<Animator>();asset.runtimeAnimatorController=assetController;asset.enabled=false;
                    var layout=new GameObject("NativeRigLayout");layout.transform.SetParent(holder.transform,false);
                    foreach(var target in bones.Where(t=>t!=rig.transform&&t.IsChildOf(hips)))
                    {var cursor=layout.transform;foreach(string name in AnimationUtility.CalculateTransformPath(target,rig.transform).Split('/')){var child=cursor.Find(name);if(child==null){child=new GameObject(name).transform;child.SetParent(cursor,false);}cursor=child;}}
                }
                finally{UnityEngine.Object.DestroyImmediate(rig);}
            }
            File.WriteAllText(folder+"/CabinPoseAuthoring.result.json",JsonUtility.ToJson(new Report{xml=seatXmlPath,seats=evidence.ToArray()},true));
            return clips.ToArray();
        }
        finally{if(owned)bundle.Unload(false);}
    }
    static Vector3 NativePelvis(GameObject source,RuntimeAnimatorController controller)
    {
        var rig=UnityEngine.Object.Instantiate(source);try{foreach(var behavior in rig.GetComponentsInChildren<MonoBehaviour>(true))if(behavior!=null)behavior.enabled=false;var animator=rig.GetComponent<Animator>();animator.runtimeAnimatorController=controller;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;animator.Rebind();animator.SetInteger("VehiclePose",41);for(int n=0;n<60;n++)animator.Update(1f/30f);var hips=rig.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Hips");return rig.transform.InverseTransformPoint(hips.position);}finally{UnityEngine.Object.DestroyImmediate(rig);}
    }
    static float FloorAt(Transform model,Vector3 point)
    {
        float height=float.NegativeInfinity;bool found=false;
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name=="camper_interior"||m.name=="camper_body"||m.name=="Rear entry floor"))
        {var mesh=filter.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;for(int n=0;n<indices.Length;n+=3){var a=model.InverseTransformPoint(filter.transform.TransformPoint(vertices[indices[n]]));var b=model.InverseTransformPoint(filter.transform.TransformPoint(vertices[indices[n+1]]));var c=model.InverseTransformPoint(filter.transform.TransformPoint(vertices[indices[n+2]]));var center=(a+b+c)/3;if(center.y<.7f||center.y>1.3f||Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y)<.85f)continue;
            float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(denominator)<.000001f)continue;
            float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/denominator;
            float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/denominator;
            if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
            float y=u*a.y+v*b.y+(1-u-v)*c.y;if(y>height){height=y;found=true;}
        }}
        if(!found)throw new Exception("No actual cabin floor triangle beneath the authored foot target "+point+"");return height;
    }
    static void TwoBone(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
    {
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,end.position);var delta=target-upper.position;float distance=delta.magnitude;if(distance<a*.05f||distance>=a+b)throw new Exception("Natural lap wrist target unreachable without bone stretching");var axis=delta.normalized;var bend=Vector3.ProjectOnPlane(pole,axis).normalized;if(bend.sqrMagnitude<.5f)throw new Exception("Arm bend plane degenerate");float along=(a*a+distance*distance-b*b)/(2*distance);var elbow=upper.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));Aim(upper,lower,elbow-upper.position);Aim(lower,end,target-lower.position);
    }
    static void Aim(Transform joint,Transform child,Vector3 direction){var current=child.position-joint.position;if(current.sqrMagnitude<.000001f)throw new Exception("Zero native bone length");joint.rotation=Quaternion.FromToRotation(current,direction.normalized)*joint.rotation;}
    static AnimationClip WriteClip(Transform rig,Transform hips,string folder,string name)
    {
        var clip=new AnimationClip{name=name,frameRate=30,legacy=false};foreach(var transform in rig.GetComponentsInChildren<Transform>(true).Where(t=>t!=rig&&t.IsChildOf(hips)))
        {string path=AnimationUtility.CalculateTransformPath(transform,rig);var p=transform.localPosition;var q=transform.localRotation;Curve(clip,path,"m_LocalPosition.x",p.x);Curve(clip,path,"m_LocalPosition.y",p.y);Curve(clip,path,"m_LocalPosition.z",p.z);Curve(clip,path,"m_LocalRotation.x",q.x);Curve(clip,path,"m_LocalRotation.y",q.y);Curve(clip,path,"m_LocalRotation.z",q.z);Curve(clip,path,"m_LocalRotation.w",q.w);}
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);string asset=folder+"/"+name+".anim";AssetDatabase.CreateAsset(clip,asset);AssetDatabase.SaveAssets();var authored=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);if(!Valid(authored))throw new Exception("Generic cabin skeletal clip invalid");return authored;
    }
    public static bool Valid(AnimationClip clip){if(clip==null||clip.humanMotion||clip.legacy)return false;var curves=AnimationUtility.GetCurveBindings(clip);return curves.Length>100&&curves.All(c=>c.type==typeof(Transform)&&!string.IsNullOrEmpty(c.path))&&new[]{"Hips","LeftUpLeg","LeftLeg","LeftFoot","RightUpLeg","RightLeg","RightFoot","LeftArm","RightArm"}.All(n=>curves.Any(c=>c.path.EndsWith("/"+n)&&c.propertyName=="m_LocalRotation.w"));}
    static void Curve(AnimationClip clip,string path,string property,float value){AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),AnimationCurve.Constant(0,1,value));}
    static string Value(XmlNode seat,string name){var node=seat.SelectSingleNode("property[@name='"+name+"']");if(node==null)throw new Exception("Seat property absent: "+name);return node.Attributes["value"].Value;}
    static Vector3 Vec(string text){var parts=text.Split(',');return new Vector3(float.Parse(parts[0],CultureInfo.InvariantCulture),float.Parse(parts[1],CultureInfo.InvariantCulture),float.Parse(parts[2],CultureInfo.InvariantCulture));}
    static string Arg(string key,string fallback){var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,key);return n>=0&&n+1<args.Length?args[n+1]:fallback;}
}



