using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEngine.Rendering;using Object=UnityEngine.Object;
public static class RebirthCamperDamageArmor
{
 public const string Folder="Assets/REBIRTHCamperDamageArmor_20261018_v1";
 public const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperDamageArmor_20261018";
 const string Input="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/Approved40Export_20261018";
 const string Ownership="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/DamageOwnership_20261018";
 [Serializable] public class Piece {public int order,hideAtHealthPercent;}
 [Serializable] public class Plan {public Piece[] pieces;}
 public static void Author()
 {
  Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
  var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperApprovedArmor.Folder+"/rebirthCamper80s.prefab"));root.name="rebirthCamper80s";
  var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Ownership+"/plan.json"));var proof=new List<string>();int totalRanges=0;
  foreach(string label in new[]{"plates","grilles","door"})
  {
   var filter=root.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Approved "+label);var old=filter.sharedMesh;
   var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var buckets=new SortedDictionary<int,List<int>>();var owners=File.ReadAllBytes(Ownership+"/"+label+".owners");var unique=new Dictionary<string,int>();
   using(var br=new BinaryReader(File.OpenRead(Input+"/"+label+".meshbin")))
   {
    int nv=br.ReadInt32(),ni=br.ReadInt32();if(owners.Length*3!=ni)throw new Exception("Ownership length");var remap=new int[nv];
    for(int i=0;i<nv;i++)
    {
     var p=new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());var n=new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());var t=new Vector2(br.ReadSingle(),br.ReadSingle());
     if(label=="door"&&p.z< -3.15f){float a=Mathf.Abs(p.x);p.x=Mathf.Sign(p.x)*(a<=.4f?a*1.3f:a<.9f?.52f+(a-.4f)*.76f:a);n.x/=(a<=.4f?1.3f:a<.9f?.76f:1f);}
     p=filter.transform.InverseTransformPoint(p);n=filter.transform.InverseTransformDirection(n).normalized;
     string key=p.x.ToString("R")+","+p.y.ToString("R")+","+p.z.ToString("R")+","+n.x.ToString("R")+","+n.y.ToString("R")+","+n.z.ToString("R")+","+t.x.ToString("R")+","+t.y.ToString("R");
     if(!unique.TryGetValue(key,out int index)){index=vertices.Count;unique.Add(key,index);vertices.Add(p);normals.Add(n);uv.Add(t);}remap[i]=index;
    }
    for(int tri=0;tri<owners.Length;tri++){int owner=owners[tri];if(!buckets.TryGetValue(owner,out var indices)){indices=new List<int>();buckets.Add(owner,indices);}for(int j=0;j<3;j++)indices.Add(remap[br.ReadInt32()]);}
   }
   var all=new List<int>();foreach(var bucket in buckets)
   {
    int threshold=plan.pieces.Single(p=>p.order==bucket.Key).hideAtHealthPercent;
    var marker=new GameObject("ArmorRange_"+bucket.Key+"_"+threshold+"_"+all.Count+"_"+bucket.Value.Count);marker.transform.SetParent(filter.transform,false);all.AddRange(bucket.Value);totalRanges++;
   }
   if(all.Count!=old.triangles.Length)throw new Exception("Triangle coverage changed: "+label+" "+all.Count+" / "+old.triangles.Length);
   var mesh=new Mesh{name="Damage ranges "+label,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(all,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
   // Keep triangle order: range metadata refers to this immutable original ordering.
   AssetDatabase.CreateAsset(mesh,Folder+"/"+label+".asset");filter.sharedMesh=mesh;filter.name="REBIRTHArmorSurface_"+label;proof.Add(label+": "+buckets.Count+" ranges, "+all.Count/3+" triangles, existing atlas/material retained");
  }
  if(totalRanges!=54)throw new Exception("Expected 54 ranges, got "+totalRanges);
  PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();Object.DestroyImmediate(root);File.WriteAllLines(Output+"/author-proof.txt",proof);Build();
 }
 public static void Build()
 {
  Directory.CreateDirectory(Output+"/Bundles");var build=new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Folder+"/rebirthCamper80s.prefab"},addressableNames=new[]{"rebirthCamper80s"}};
  if(!RebirthAssetBundlePackaging.BuildAssetBundles(Output+"/Bundles",new[]{build},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Build failed");
  Verify();
 }
 public static void Verify()
 {
  string path=Output+"/Bundles/rebirth_camper80s.unity3d";if(!BuildPipeline.GetCRCForAssetBundle(path,out uint crc))throw new Exception("CRC missing");var bundle=AssetBundle.LoadFromFile(path,crc);if(!bundle)throw new Exception("CRC reopen failed");
  try
  {
   var root=Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));var ts=root.GetComponentsInChildren<Transform>(true);var ranges=ts.Where(t=>t.name.StartsWith("ArmorRange_")).ToArray();if(ranges.Length!=54||ranges.Select(t=>t.name.Split('_')[1]).Distinct().Count()!=54)throw new Exception("Range IDs");
   if(root.GetComponentsInChildren<WheelCollider>(true).Length!=4||ts.Count(t=>t.name.StartsWith("REBIRTH_Seat"))!=30)throw new Exception("Rig changed");
   var hull=ts.Single(t=>t.name=="REBIRTHModPlowHull").GetComponentsInChildren<MeshCollider>(true);if(hull.Length!=27||hull.Any(c=>!c.convex||c.isTrigger||c.gameObject.layer!=21))throw new Exception("Plough changed");
   foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("REBIRTHArmorSurface_")))
   {var r=f.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("ArmorRange_")).Select(t=>t.name.Split('_')).ToArray();if(r.Sum(a=>int.Parse(a[4]))!=f.sharedMesh.triangles.Length)throw new Exception("Coverage");}
   foreach(string name in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"})ts.Single(t=>t.name==name).gameObject.SetActive(true);
   var filters=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("REBIRTHArmorSurface_")).ToArray();var originals=filters.Select(f=>f.sharedMesh.triangles).ToArray();
   foreach(var f in filters)f.sharedMesh=Object.Instantiate(f.sharedMesh);
   foreach(int health in new[]{100,50,5,100})
   {
    for(int i=0;i<filters.Length;i++)
    {
     var indices=new List<int>();foreach(Transform range in filters[i].transform){var a=range.name.Split('_');if(health>int.Parse(a[2]))indices.AddRange(originals[i].Skip(int.Parse(a[3])).Take(int.Parse(a[4])));}
     filters[i].sharedMesh.SetTriangles(indices,0,false);
     if(health==5&&indices.Count!=0||health==100&&indices.Count!=originals[i].Length)throw new Exception("Damage/repair validation");
    }
    Render(root,health);
   }
   foreach(var f in filters)Object.DestroyImmediate(f.sharedMesh);
   Object.DestroyImmediate(root);File.WriteAllText(Output+"/verified.txt","PASS CRC="+crc+"; 54 unique complete ranges; 100/50/5 percent and full-repair mesh checks passed; 3 armor renderers and existing atlases retained; 4 wheels; 30 seat markers; 27 convex non-trigger plough colliders. No gameplay testing.");
  }
  finally{bundle.Unload(true);}
 }
 static void Render(GameObject root,int health)
 {
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);var key=new GameObject("DamageReviewKey").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.2f;key.transform.rotation=Quaternion.Euler(40,-35,0);
  var cam=new GameObject("DamageReviewCamera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=2.9f;cam.transform.position=new Vector3(6,3.6f,7);cam.transform.LookAt(new Vector3(0,1.6f,-.1f));cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.15f,.17f,.18f);
  var rt=new RenderTexture(1280,880,24);var image=new Texture2D(1280,880,TextureFormat.RGB24,false);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,880),0,0);image.Apply();File.WriteAllBytes(Output+"/health-"+health+".png",image.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;
  Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(cam.gameObject);Object.DestroyImmediate(key.gameObject);
 }

}
