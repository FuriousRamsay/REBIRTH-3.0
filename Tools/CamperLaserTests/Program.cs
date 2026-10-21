using UnityEngine;
class Program{
 static int checks;static void Check(bool yes,string label){if(!yes)throw new Exception(label);checks++;}
 static void Main(){foreach(float length in new[]{0f,1f,10f,1000f})foreach(var basis in new[]{new Vector3(1,1,1),new Vector3(.2f,3,7),new Vector3(8,.01f,.4f)}){
 var native=new Vector3(.01f,.02f,.03f);Check(RebirthCamperLaserScale.Try(native,basis,.7874f,length,out var scale),"Valid nested basis");Check(Math.Abs(scale.x*basis.x-native.x)<.000001f&&Math.Abs(scale.y*basis.y-native.y)<.000001f,"Native effective width independent of length/parent scale");Check(Math.Abs(scale.z*basis.z*.7874f-length)<.001f,"Actual surface distance retained");}
 Check(!RebirthCamperLaserScale.Try(new Vector3(1,1,1),new Vector3(0,1,1),1,10,out _),"Degenerate parent refused");Check(!RebirthCamperLaserScale.Try(new Vector3(1,1,1),new Vector3(1,1,1),1,float.NaN,out _),"Invalid distance refused");Check(!RebirthCamperLaserScale.Try(new Vector3(1,1,1),new Vector3(1,1,1),0,10,out _),"Missing native mesh length refused");Console.WriteLine("PASS "+checks+" production native-width laser scale checks; actual native prefab/GPU CCTV remains separate qualification.");}
}
namespace UnityEngine{internal struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new();}}
