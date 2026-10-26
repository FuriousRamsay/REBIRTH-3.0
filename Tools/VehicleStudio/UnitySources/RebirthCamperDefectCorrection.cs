using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class RebirthCamperDefectCorrection {
 const string F="Assets/REBIRTHCamperCorrected_20261014";
 const string O=RebirthCamperDefectAudit.O;
 static Transform Find(GameObject o,string n)=>RebirthCamperDefectAudit.Find(o,n);
 static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float width,Material mat){var o=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(o.GetComponent<Collider>());o.name=name;o.transform.SetParent(parent,true);o.transform.position=(a+b)/2;o.transform.up=(b-a).normalized;o.transform.localScale=new Vector3(width,(b-a).magnitude,width);o.GetComponent<Renderer>().sharedMaterial=mat;}
 public static void Run(){Directory.CreateDirectory(O);Directory.CreateDirectory(O+"/Renders");AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperDefectAudit.P));root.name="rebirthCamper80s";
 var walk=Find(root,"REBIRTHCampWalkInColliders");foreach(var c in walk.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name.StartsWith("Camp stair")))c.enabled=false;
 var ramp=new GameObject("Camp stair continuous collision");ramp.transform.SetParent(walk,false);ramp.layer=walk.GetComponentsInChildren<Collider>(true)[0].gameObject.layer;
 var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.39f,0,-5.03f),new Vector3(.39f,0,-5.03f),new Vector3(-.39f,.72f,-4.0f),new Vector3(.39f,.72f,-4.0f),new Vector3(-.39f,.72f,-3.25f),new Vector3(.39f,.72f,-3.25f),new Vector3(-.39f,-.06f,-5.03f),new Vector3(.39f,-.06f,-5.03f),new Vector3(-.39f,.65f,-3.25f),new Vector3(.39f,.65f,-3.25f)};
 mesh.triangles=new[]{0,2,1,1,2,3,2,4,3,3,4,5,6,7,8,7,9,8,0,1,6,1,7,6,4,8,5,5,8,9,0,6,2,2,6,8,2,8,4,1,3,7,3,9,7,3,5,9};mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,F+"/StairCollisionRamp.asset");var mc=ramp.AddComponent<MeshCollider>();mc.sharedMesh=mesh;mc.convex=true;
 var stairVisuals=Find(root,"REBIRTHCampStairVisuals"); foreach(Transform t in stairVisuals)t.position+=new Vector3(0,0,-.60f); var stairMat=stairVisuals.GetComponentInChildren<Renderer>(true).sharedMaterial; for(int j=0;j<12;j++)Beam(stairVisuals,"Landing slat",new Vector3(-.39f,.702f,-3.88f+j*.05f),new Vector3(.39f,.702f,-3.88f+j*.05f),.036f,stairMat); foreach(float x in new[]{-.34f,.34f})Beam(stairVisuals,"Landing support",new Vector3(x,.65f,-4f),new Vector3(x,.65f,-3.28f),.04f,stairMat); var solar=Find(root,"solar panel");var rr=solar.GetComponentsInChildren<Renderer>(true);var sb=rr[0].bounds;foreach(var r in rr)sb.Encapsulate(r.bounds);
 var iron=Find(root,"REBIRTHCampStairVisuals").GetComponentInChildren<Renderer>(true).sharedMaterial;
 // Brackets connect the supplied solar model's base to the rear structural wall.
 var mount=new GameObject("Solar wall mount");mount.transform.SetParent(solar,true);
 foreach(float x in new[]{-.43f,.43f}) {
   Beam(mount.transform,"Solar base rail",new Vector3(x,sb.min.y-.015f,sb.min.z),new Vector3(x,sb.min.y-.015f,sb.max.z),.035f,iron);
   Beam(mount.transform,"Solar wall plate",new Vector3(x,2.62f,-3.345f),new Vector3(x,sb.min.y,-3.345f),.045f,iron);
   Beam(mount.transform,"Solar bracket arm",new Vector3(x,sb.min.y-.02f,-3.345f),new Vector3(x,sb.min.y-.02f,sb.min.z),.035f,iron);
   Beam(mount.transform,"Solar diagonal brace",new Vector3(x,2.62f,-3.345f),new Vector3(x,sb.min.y-.02f,sb.min.z),.025f,iron);
 }
 var ti=(TextureImporter)AssetImporter.GetAtPath(F+"/DiscoBandSharp.png");ti.maxTextureSize=2048;ti.mipmapEnabled=true;ti.anisoLevel=8;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
 var shader=Shader.Find("REBIRTH/CamperCorrectedWrap");var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(F+"/DiscoBandSharp.png");
 foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="camper_body")) {var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++){if(!mats[i].HasProperty("_BandArtwork"))continue;var m=new Material(mats[i]);m.shader=shader;m.SetTexture("_SharpBand",tex);AssetDatabase.CreateAsset(m,F+"/CamperWrap_"+i+".mat");mats[i]=m;}r.sharedMaterials=mats;}
 RebirthCamperCampIntegration.SetMode(root,true);var results=new List<string>{"Solar source bounds "+sb};foreach(var h in new[]{1.72f,1.8f})foreach(var step in new[]{.1f,.3f,.5f,.7f})results.Add(RebirthCamperDefectAudit.Walk(root,h,step));File.WriteAllLines(O+"/corrected-entry.txt",results);
 RebirthCamperCampIntegration.SetMode(root,false);foreach(var n in new[]{"REBIRTHCampPowerEquipment","REBIRTHCampStairVisuals"})Find(root,n).gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,F+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();
 RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);var sun=new GameObject().AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.transform.rotation=Quaternion.Euler(45,-35,0);
 var cam=new GameObject().AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.14f,.16f);cam.nearClipPlane=.01f;cam.orthographic=true;
 foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="body"||r.name=="camper_body")){var b=new MaterialPropertyBlock();b.SetFloat("_PatternEnabled",1);r.SetPropertyBlock(b);}
 Save(cam,new Vector3(-5,3.5f,-5),new Vector3(0,1.8f,-1.2f),3.4f,"corrected-wrap");
 Save(cam,new Vector3(-4,2.6f,-1),new Vector3(0,2.35f,-1.3f),1.15f,"corrected-siding-close");
 RebirthCamperCampIntegration.SetMode(root,true);foreach(var n in new[]{"REBIRTHCampPowerEquipment","REBIRTHCampStairVisuals"})Find(root,n).gameObject.SetActive(true);
 Save(cam,new Vector3(-2.2f,3,-5.5f),new Vector3(0,2.45f,-3.4f),1.1f,"corrected-solar-mount");
 }
 static void Save(Camera c,Vector3 eye,Vector3 target,float size,string n){c.transform.position=eye;c.transform.LookAt(target);c.orthographicSize=size;var rt=new RenderTexture(1600,1200,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;var im=new Texture2D(1600,1200,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,1600,1200),0,0);im.Apply();File.WriteAllBytes(O+"/Renders/"+n+".png",im.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(im);}
}

