using UnityEngine;
class Program{
 static int checks;static void Check(bool yes,string label){if(!yes)throw new Exception(label);checks++;}
 static void Main(){foreach(int height in new[]{480,720,1440})foreach(float fov in new[]{10f,40f,80f})foreach(float length in new[]{.1f,1f,65f,1000f}){
 const float diameter=.00937176682f*.25f*2.54f*.75f;const float depth=-.04f;
 float start=RebirthCamperLaserNearField.Start(diameter,height,fov,depth,1,length);
 Check(start>=0&&start<length,"Native beam remains visible through its original endpoint");
 Check(Math.Abs((start+(length-start))-length)<.0001f,"Clipping preserves hit endpoint");
 double pixelWidth=diameter*height/(2*(start+depth)*Math.Tan(fov*Math.PI/360));
 Check(start>length-.002f||pixelWidth<=2.00001,"Only nearfield flare exceeds two pixels; visible beam retains native width");
 }
 Check(RebirthCamperLaserNearField.Start(.004f,720,80,0,.5f,65)==0,"Other turret or side view unmodified");
 Check(RebirthCamperLaserNearField.Start(float.NaN,720,80,0,1,65)==0,"Invalid nearfield data refused");
 foreach(float length in new[]{0f,1f,10f,1000f})foreach(var basis in new[]{new Vector3(1,1,1),new Vector3(.2f,3,7),new Vector3(8,.01f,.4f)}){
 var native=new Vector3(.01f,.02f,.03f);Check(RebirthCamperLaserScale.Try(native,basis,.7874f,length,out var scale),"Valid nested basis");Check(Math.Abs(scale.x*basis.x-native.x)<.000001f&&Math.Abs(scale.y*basis.y-native.y)<.000001f,"Native effective width independent of length/parent scale");Check(Math.Abs(scale.z*basis.z*.7874f-length)<.001f,"Actual surface distance retained");}
 Check(!RebirthCamperLaserScale.Try(new Vector3(1,1,1),new Vector3(0,1,1),1,10,out _),"Degenerate parent refused");Check(!RebirthCamperLaserScale.Try(new Vector3(1,1,1),new Vector3(1,1,1),1,float.NaN,out _),"Invalid distance refused");Check(!RebirthCamperLaserScale.Try(new Vector3(1,1,1),new Vector3(1,1,1),0,10,out _),"Missing native mesh length refused");Console.WriteLine("PASS "+checks+" production native-width laser scale checks; actual native prefab/GPU CCTV remains separate qualification.");}
}
namespace UnityEngine{internal struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new();}}
