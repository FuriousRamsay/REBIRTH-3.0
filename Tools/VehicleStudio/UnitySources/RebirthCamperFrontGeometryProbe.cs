using System;using System.IO;using System.Linq;using System.Xml;using System.Globalization;using UnityEngine;using UnityEditor;
public static class RebirthCamperFrontGeometryProbe
{
 [Serializable] public class MeshData {public string source,mesh;public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles;}
 public static void Run()
 {
  const string path="Assets/REBIRTHCamperSeatingGenerated/ae2f4ac591114f92bdbc2ad9654d9d12/rebirthCamper80s.prefab";
  var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
  try{var model=root.transform.Find("GameObject");var f=root.GetComponentsInChildren<MeshFilter>(true).Single(t=>t.name=="interior");var m=f.sharedMesh;
   var data=new MeshData{source=path,mesh=m.name,vertices=m.vertices.Select(v=>model.InverseTransformPoint(f.transform.TransformPoint(v))).ToArray(),normals=m.normals.Select(n=>model.InverseTransformDirection(f.transform.TransformDirection(n))).ToArray(),uv=m.uv,triangles=m.triangles};
   File.WriteAllText("E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/front-interior.json",JsonUtility.ToJson(data));
   var wheel=model.Find("M/SteeringWheel");var wf=wheel.GetComponentInChildren<MeshFilter>();
   File.WriteAllText("E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/front-wheel.json",JsonUtility.ToJson(new MeshData{source=path,mesh=wf.sharedMesh.name,vertices=wf.sharedMesh.vertices.Select(v=>model.InverseTransformPoint(wf.transform.TransformPoint(v))).ToArray(),triangles=wf.sharedMesh.triangles}));
   var doc=new XmlDocument();doc.Load("E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/front_cushion_v5.xml");
   foreach(string side in new[]{"L","R"}){var node=doc.SelectSingleNode("/configs/append/vehicle[@name='rebirthCamper80s']/property[@class='handlebars']/property[@name='IKHand"+side+"Position']");var parts=node.Attributes["value"].Value.Split(',');var p=new Vector3(float.Parse(parts[0],CultureInfo.InvariantCulture),float.Parse(parts[1],CultureInfo.InvariantCulture),float.Parse(parts[2],CultureInfo.InvariantCulture));var target=model.InverseTransformPoint(wheel.TransformPoint(p))-Vector3.forward*.04f;p=wheel.InverseTransformPoint(model.TransformPoint(target));node.Attributes["value"].Value=p.x.ToString("R",CultureInfo.InvariantCulture)+","+p.y.ToString("R",CultureInfo.InvariantCulture)+","+p.z.ToString("R",CultureInfo.InvariantCulture);}
   doc.Save("E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/front_cushion_v6.xml");
  }finally{UnityEngine.Object.DestroyImmediate(root);}
 }
}

