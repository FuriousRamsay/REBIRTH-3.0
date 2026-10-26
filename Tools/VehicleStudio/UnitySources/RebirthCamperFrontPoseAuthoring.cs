using System;using System.IO;using System.Linq;using System.Xml;using System.Globalization;using UnityEngine;using UnityEditor;using UnityEditor.Animations;
// Separate experimental front clips; never installs or changes runtime seat support.
public static class RebirthCamperFrontPoseAuthoring
{
 [Serializable] public class CushionPlan {public int vertexCount;public int[] vertexIds;public Vector3[] before,after;}
 public static AnimationClip[] Apply(GameObject root,string folder)
 {
  const string asset="Assets/AssetBundles/Player/Common/BaseRigs/baseRigPrefab.prefab";
  var bundle=AssetBundle.GetAllLoadedAssetBundles().First(b=>b.GetAllAssetNames().Any(n=>n.Equals(asset,StringComparison.OrdinalIgnoreCase)));
  var source=bundle.LoadAsset<GameObject>(asset);var clips=new System.Collections.Generic.List<AnimationClip>();
  bool benchTrial=Environment.GetCommandLineArgs().Contains("-rebirthFrontBenchExperiment");
  if(benchTrial){
  var model=root.transform.Find("GameObject");var filter=root.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="interior");
  var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;
  var plan=JsonUtility.FromJson<CushionPlan>(File.ReadAllText("E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/front-cushion-plan.json"));
  if(plan.vertexCount!=vertices.Length||plan.vertexIds.Length!=76)throw new Exception("Cushion source layout changed");
  for(int j=0;j<plan.vertexIds.Length;j++){int i=plan.vertexIds[j];var current=model.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));if(Vector3.Distance(current,plan.before[j])>.00001f)throw new Exception("Cushion source vertex changed "+i);vertices[i]=filter.transform.InverseTransformPoint(model.TransformPoint(plan.after[j]));}
  File.Copy("E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/front-cushion-plan.json",folder+"/CushionPlan.json");
  var normals=mesh.normals;var tangents=mesh.tangents;var changed=new System.Collections.Generic.HashSet<int>(plan.vertexIds);var affected=new System.Collections.Generic.HashSet<int>();var triangles=mesh.triangles;
  for(int i=0;i<triangles.Length;i+=3)if(changed.Contains(triangles[i])||changed.Contains(triangles[i+1])||changed.Contains(triangles[i+2]))for(int j=0;j<3;j++)affected.Add(triangles[i+j]);
  mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();var updated=mesh.normals;for(int i=0;i<updated.Length;i++)if(!affected.Contains(i))updated[i]=normals[i];mesh.normals=updated;
  mesh.RecalculateTangents();var updatedTangents=mesh.tangents;if(tangents.Length==updatedTangents.Length){for(int i=0;i<tangents.Length;i++)if(!affected.Contains(i))updatedTangents[i]=tangents[i];mesh.tangents=updatedTangents;}
  AssetDatabase.CreateAsset(mesh,folder+"/FrontBenchTrial.asset");filter.sharedMesh=mesh;
  model.Find("M/SteeringWheel").localPosition+=new Vector3(0,0,.06f);

  }
  var parent=root.transform.Find("GameObject/M/CabinPoseAssets");if(!parent)throw new Exception("Cumulative cabin dependencies required");
  foreach(int seat in new[]{0,1})
  {
   var rig=UnityEngine.Object.Instantiate(source);rig.SetActive(false);
   try{
    var ts=rig.GetComponentsInChildren<Transform>(true);Func<string,Transform> bone=n=>ts.Single(t=>t.name==n);var hips=bone("Hips");
    var arguments=Environment.GetCommandLineArgs();var seatDoc=new XmlDocument();seatDoc.Load(arguments[Array.IndexOf(arguments,"-rebirthAvatarCamperXml")+1]);
    var seatPosition=Vec(seatDoc.SelectSingleNode("/configs/append/vehicle[@name='rebirthCamper80s']/property[@class='seat"+seat+"']/property[@name='position']").Attributes["value"].Value);
    var native=UnityEngine.Object.Instantiate(source);native.SetActive(true);
    try{
     native.GetComponent<Animator>().runtimeAnimatorController=bundle.LoadAsset<RuntimeAnimatorController>("Assets/AssetBundles/Player/Common/AnimControllers/3PPlayerMaleController.controller");
     var probe=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperAvatarProbe",false)).First(t=>t!=null);
     probe.GetMethod("EvaluateNativePoseSnapshot").Invoke(null,new object[]{native,seat==0?40:41,null});
     foreach(var t in native.GetComponentsInChildren<Transform>(true).Where(t=>(t.name.StartsWith("LeftHand")||t.name.StartsWith("RightHand"))&&t.name!="LeftHand"&&t.name!="RightHand"))bone(t.name).localRotation=t.localRotation;
    }finally{UnityEngine.Object.DestroyImmediate(native);}
    var feet=new[]{bone("LeftFoot").rotation,bone("RightFoot").rotation};
    hips.position=rig.transform.TransformPoint(new Vector3(0,.9963558f,-.1427224f));
    foreach(string n in new[]{"Spine","Spine1","Spine2","Spine3"}){var t=bone(n);t.rotation=Quaternion.AngleAxis(benchTrial?(seat==0?5f:6f):8.75f,rig.transform.right)*t.rotation;}
    if(benchTrial)bone("Head").rotation=Quaternion.AngleAxis(seat==0?-15f:-19f,rig.transform.right)*bone("Head").rotation;
    foreach(string side in new[]{"Left","Right"})
    {
     float sign=side=="Left"?-1:1;var thigh=bone(side+"UpLeg");var knee=bone(side+"Leg");var foot=bone(side+"Foot");
     float a=Vector3.Distance(thigh.position,knee.position),b=Vector3.Distance(knee.position,foot.position);
     // At candidate anchor Y=-.0963558, hipsY=.9; kneeY=1.07, ankleY=.65.
     bool pedalLeg=benchTrial&&seat==0&&side=="Right";
     float kneeY=pedalLeg?.95f:1.0f;
     float rise=((benchTrial?kneeY-seatPosition.y:1.1663558f)-rig.transform.InverseTransformPoint(thigh.position).y)/a;
     float lateral=sign*.07f/a;
     if(pedalLeg)lateral=(-.10f-seatPosition.x-rig.transform.InverseTransformPoint(thigh.position).x)/a;
     Aim(thigh,knee,rig.transform.TransformDirection(new Vector3(lateral,rise,Mathf.Sqrt(1-rise*rise-lateral*lateral))));
     float down=((benchTrial?kneeY:1.07f)-.65f)/b;
     Aim(knee,foot,rig.transform.TransformDirection(new Vector3(0,-down,Mathf.Sqrt(1-down*down))));
     if(pedalLeg)Aim(knee,foot,rig.transform.TransformPoint(new Vector3(-.31f-seatPosition.x,.65f-seatPosition.y,.48f-seatPosition.z))-knee.position);
     foot.rotation=Quaternion.AngleAxis(benchTrial?-40f:0f,rig.transform.right)*feet[side=="Left"?0:1];
     if(benchTrial&&((seat==0&&side=="Right")||(seat==1&&side=="Left")))foot.rotation=Quaternion.AngleAxis(seat==0?-30f:30f,rig.transform.up)*foot.rotation;
     var arm=bone(side+"Arm");var fore=bone(side+"ForeArm");var hand=bone(side+"Hand");
     var wrist=hips.position+rig.transform.TransformDirection(new Vector3(sign*.16f,.18f,.31f));
     if(seat==0&&benchTrial){
      var args=Environment.GetCommandLineArgs();var doc=new XmlDocument();doc.Load(args[Array.IndexOf(args,"-rebirthAvatarCamperXml")+1]);
      var vehicle=doc.SelectSingleNode("/configs/append/vehicle[@name='rebirthCamper80s']");
      var anchor=Vec(vehicle.SelectSingleNode("property[@class='seat0']/property[@name='position']").Attributes["value"].Value);
      var target=Vec(vehicle.SelectSingleNode("property[@class='handlebars']/property[@name='IKHand"+(side=="Left"?"L":"R")+"Position']").Attributes["value"].Value);
      var model=root.transform.Find("GameObject");wrist=rig.transform.TransformPoint(model.InverseTransformPoint(model.Find("M/SteeringWheel").TransformPoint(target))-anchor);
     }
     TwoBone(arm,fore,hand,wrist,rig.transform.TransformDirection(new Vector3(sign*.08f,-.7f,.2f)));
     Aim(hand,bone(side+"HandMiddle1"),rig.transform.forward);
    }
    var clip=new AnimationClip{name="rebirthCamperCabinSeat"+seat,frameRate=30,legacy=false};
    foreach(var t in ts.Where(t=>t!=rig.transform&&t.IsChildOf(hips)))
    {
     string path=AnimationUtility.CalculateTransformPath(t,rig.transform);var p=t.localPosition;var q=t.localRotation;
     Set(clip,path,"m_LocalPosition.x",p.x);Set(clip,path,"m_LocalPosition.y",p.y);Set(clip,path,"m_LocalPosition.z",p.z);
     Set(clip,path,"m_LocalRotation.x",q.x);Set(clip,path,"m_LocalRotation.y",q.y);Set(clip,path,"m_LocalRotation.z",q.z);Set(clip,path,"m_LocalRotation.w",q.w);
    }
    clip.EnsureQuaternionContinuity();AssetDatabase.CreateAsset(clip,folder+"/"+clip.name+".anim");
    var paths=AnimationUtility.GetCurveBindings(clip).Select(c=>c.path).Distinct().OrderBy(p=>p).ToArray();
    if(paths.Any(p=>p.Length==0||p.Contains("IKRig")||p.Contains("IKHand")))throw new Exception("Forbidden root/IK curves");
    File.WriteAllLines(folder+"/Seat"+seat+".animated-paths.txt",paths);
    var holder=new GameObject("Seat"+seat);holder.transform.SetParent(parent,false);var controller=AnimatorController.CreateAnimatorControllerAtPath(folder+"/Seat"+seat+".controller");controller.AddMotion(clip);var animator=holder.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.enabled=false;
    var layout=new GameObject("NativeRigLayout");layout.transform.SetParent(holder.transform,false);foreach(string path in paths){var cursor=layout.transform;foreach(string n in path.Split('/')){var t=cursor.Find(n);if(!t){t=new GameObject(n).transform;t.SetParent(cursor,false);}cursor=t;}}
    clips.Add(clip);
   }finally{UnityEngine.Object.DestroyImmediate(rig);}
  }
  AssetDatabase.SaveAssets();return clips.ToArray();
 }
 static void Aim(Transform joint,Transform child,Vector3 d){joint.rotation=Quaternion.FromToRotation(child.position-joint.position,d.normalized)*joint.rotation;}
 static void TwoBone(Transform a,Transform b,Transform c,Vector3 target,Vector3 pole){float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);var d=target-a.position;float length=d.magnitude;var axis=d.normalized;float along=(l1*l1+length*length-l2*l2)/(2*length);var elbow=a.position+axis*along+Vector3.ProjectOnPlane(pole,axis).normalized*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));Aim(a,b,elbow-a.position);Aim(b,c,target-b.position);}
 static void Set(AnimationClip c,string p,string property,float v){AnimationUtility.SetEditorCurve(c,EditorCurveBinding.FloatCurve(p,typeof(Transform),property),AnimationCurve.Constant(0,1,v));}
 static Vector3 Vec(string s){var p=s.Split(',');return new Vector3(float.Parse(p[0],CultureInfo.InvariantCulture),float.Parse(p[1],CultureInfo.InvariantCulture),float.Parse(p[2],CultureInfo.InvariantCulture));}
}


