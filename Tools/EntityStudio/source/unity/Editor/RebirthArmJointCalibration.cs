using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable]public sealed class ArmJointReference {public string name;public Vector3 position;}
 [Serializable]public sealed class ArmJointCorrection {public string name;public Vector3 offset;}
 [Serializable]public sealed class ArmJointCalibration {public int version;public string templateHash,foundationHash,method;public float referenceHeight;public ArmJointReference[] referenceJoints;public ArmJointCorrection[] guides;}
 // Optional authoring calibration. Existing canonical and tibia evaluators are unchanged.
 public static class RebirthArmJointCalibration {
  static bool Finite(Vector3 p)=>!(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude));
  static bool Supported(string name){if(string.IsNullOrEmpty(name)||!(name.StartsWith("joint-l-",StringComparison.Ordinal)||name.StartsWith("joint-r-",StringComparison.Ordinal)))return false;string suffix=name.Substring(8);return suffix=="shoulder"||suffix=="elbow"||suffix=="hand"||suffix=="hand-2"||suffix=="hand-3"||suffix.StartsWith("finger-",StringComparison.Ordinal);}
  struct Frame {public Vector3 x,y,z;public float length;}
  static Frame Segment(Dictionary<string,Vector3> joints,char side,bool upper){string prefix="joint-"+side+"-";Vector3 a=joints[prefix+(upper?"shoulder":"elbow")],b=joints[prefix+(upper?"elbow":"hand")],axis=b-a;float length=axis.magnitude;if(length<.01f)throw new InvalidDataException("Degenerate arm calibration segment");axis/=length;var forward=Vector3.ProjectOnPlane(Vector3.forward,axis);if(forward.sqrMagnitude<1e-6f)throw new InvalidDataException("Unsupported arm calibration orientation");forward.Normalize();return new Frame{x=Vector3.Cross(axis,forward),y=axis,z=forward,length=length};}
  public static Dictionary<string,Vector3> Evaluate(BodyTemplate template,BodyRecipe recipe,ArmJointCalibration data){
   if(data==null||data.version!=1||data.templateHash!=template.contentHash||string.IsNullOrEmpty(recipe.authoredBodyGuid)||data.foundationHash!=recipe.authoredBodyHash||data.referenceJoints==null||data.guides==null||data.guides.Length==0||(float.IsNaN(data.referenceHeight)||float.IsInfinity(data.referenceHeight))||data.referenceHeight<.5f||data.referenceHeight>3)throw new InvalidDataException("Arm calibration requires its reviewed authored foundation and template");
   if(data.referenceJoints.Any(j=>j==null||string.IsNullOrEmpty(j.name)||!Finite(j.position))||data.guides.Any(j=>j==null||!Supported(j.name)||!Finite(j.offset))||data.referenceJoints.Select(j=>j.name).Distinct().Count()!=data.referenceJoints.Length||data.guides.Select(j=>j.name).Distinct().Count()!=data.guides.Length)throw new InvalidDataException("Invalid arm calibration guides");
   var current=RebirthBodyAnatomicalJoints.Evaluate(template,recipe);float scale=recipe.height/data.referenceHeight;var reference=data.referenceJoints.ToDictionary(j=>j.name,j=>j.position*scale);var result=new Dictionary<string,Vector3>(current);
   foreach(var correction in data.guides){if(!current.ContainsKey(correction.name)||!reference.ContainsKey(correction.name))throw new InvalidDataException("Unknown arm calibration guide");char side=correction.name[6];bool upper=correction.name.EndsWith("-shoulder",StringComparison.Ordinal)||correction.name.EndsWith("-elbow",StringComparison.Ordinal);var a=Segment(reference,side,upper);var b=Segment(current,side,upper);Vector3 offset=correction.offset*scale;var transported=b.x*Vector3.Dot(offset,a.x)+b.y*(Vector3.Dot(offset,a.y)*b.length/a.length)+b.z*Vector3.Dot(offset,a.z);result[correction.name]=current[correction.name]+transported;}
   return result;
  }
 }
}
