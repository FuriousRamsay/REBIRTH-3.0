using System;using System.IO;using System.Linq;using System.Xml;using System.Globalization;using System.Collections.Generic;using UnityEngine;

// Editor-only measurement: align native curled finger arcs with the actual rim.
public static class RebirthCamperHandGripCalibration
{
 [Serializable] public class Hand {public string side;public Vector3 before,desired,after,wristLocal,rotationLocal;public float gripError,wristError,axisErrorDegrees;}
 [Serializable] public class Report {public string status="FAIL",scope="Actual native finger-bone arc centers versus rendered rim centerline, Unity preview only";public Vector3 rimCenter,rimAxis;public float rimRadius;public Hand[] hands;}
 public static void Calibrate(GameObject actor,Dictionary<string,Transform> targets,Action evaluate,string output)
 {
  var report=new Report();var model=actor.transform.parent.parent;var joint=targets.Values.First().parent;
  var mesh=joint.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name.Contains("steeringwheel"));
  var points=mesh.sharedMesh.vertices.Select(v=>model.InverseTransformPoint(mesh.transform.TransformPoint(v))).ToArray();
  var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);var center=bounds.center;var axis=model.InverseTransformDirection(joint.up).normalized;
  var radii=points.Select(p=>Vector3.ProjectOnPlane(p-center,axis).magnitude).OrderBy(r=>r).ToArray();float max=radii.Last();var ring=radii.Where(r=>r>max*.85f).ToArray();
  float radius=(ring[(int)(ring.Length*.1f)]+ring[(int)(ring.Length*.9f)])*.5f;
  if(radius<.18f||radius>.25f)throw new Exception("Unexpected native wheel rim radius "+radius);
  report.rimCenter=center;report.rimAxis=axis;report.rimRadius=radius;var rows=new List<Hand>();
  foreach(string side in new[]{"Left","Right"})
  {
   Vector3 oldAxis;var oldCenter=Grip(actor,model,side,out oldAxis);var radial=Vector3.ProjectOnPlane(side=="Left"?Vector3.left:Vector3.right,axis).normalized;
   var desired=center+radial*radius;var tangent=Vector3.Cross(axis,radial).normalized;if(Vector3.Dot(oldAxis,tangent)<0)tangent=-tangent;
   var target=targets[side=="Left"?"IKHandL":"IKHandR"];
   target.rotation=model.rotation*Quaternion.FromToRotation(oldAxis,tangent)*Quaternion.Inverse(model.rotation)*target.rotation;
   evaluate();Vector3 interimAxis;var interim=Grip(actor,model,side,out interimAxis);target.position+=model.TransformVector(desired-interim);evaluate();
   Vector3 finalAxis;var actual=Grip(actor,model,side,out finalAxis);var wrist=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name==side+"Hand");
   var row=new Hand{side=side,before=oldCenter,desired=desired,after=actual,wristLocal=target.localPosition,rotationLocal=target.localEulerAngles,gripError=Vector3.Distance(actual,desired),wristError=Vector3.Distance(wrist.position,target.position),axisErrorDegrees=Mathf.Acos(Mathf.Clamp(Mathf.Abs(Vector3.Dot(finalAxis,tangent)),-1,1))*Mathf.Rad2Deg};rows.Add(row);
   if(row.gripError>.003f||row.wristError>.003f||row.axisErrorDegrees>3f)throw new Exception("Measured native grip calibration failed for "+side);
  }
  var args=Environment.GetCommandLineArgs();var doc=new XmlDocument();doc.Load(args[Array.IndexOf(args,"-rebirthAvatarCamperXml")+1]);
  foreach(var row in rows){string side=row.side=="Left"?"L":"R";foreach(bool rotation in new[]{false,true}){var node=doc.SelectSingleNode("/configs/append/vehicle[@name='rebirthCamper80s']/property[@class='handlebars']/property[@name='IKHand"+side+(rotation?"Rotation":"Position")+"']");node.Attributes["value"].Value=Vector(rotation?row.rotationLocal:row.wristLocal);}}
  doc.Save(Path.Combine(output,"front-calibrated.xml"));report.hands=rows.ToArray();report.status="PASS_MEASURED_NEUTRAL_GRIP_NOT_GAME_VALIDATED";File.WriteAllText(Path.Combine(output,"front-grip-calibration.json"),JsonUtility.ToJson(report,true));
 }
 public static Vector3 Grip(GameObject actor,Transform model,string side,out Vector3 axis)
 {
  var centers=new List<Vector3>();var axes=new List<Vector3>();var bones=actor.GetComponentsInChildren<Transform>(true);
  foreach(string digit in new[]{"Index","Middle","Ring","Pinky"})
  {
   var p=new[]{1,2,3}.Select(n=>model.InverseTransformPoint(bones.Single(t=>t.name==side+"Hand"+digit+n).position)).ToArray();var u=p[1]-p[0];var v=p[2]-p[0];var normal=Vector3.Cross(u,v);float denominator=2*normal.sqrMagnitude;if(denominator<1e-12f)continue;
   var center=p[0]+(v.sqrMagnitude*Vector3.Cross(normal,u)+u.sqrMagnitude*Vector3.Cross(v,normal))/denominator;float radius=Vector3.Distance(center,p[0]);if(radius<.005f||radius>.08f)continue;
   centers.Add(center);var a=normal.normalized;if(axes.Count>0&&Vector3.Dot(a,axes[0])<0)a=-a;axes.Add(a);
  }
  if(centers.Count<3)throw new Exception("Insufficient curved native finger arcs for "+side);axis=axes.Aggregate(Vector3.zero,(a,b)=>a+b).normalized;return centers.Aggregate(Vector3.zero,(a,b)=>a+b)/centers.Count;
 }
 static string Vector(Vector3 v){return v.x.ToString("R",CultureInfo.InvariantCulture)+","+v.y.ToString("R",CultureInfo.InvariantCulture)+","+v.z.ToString("R",CultureInfo.InvariantCulture);}
}
