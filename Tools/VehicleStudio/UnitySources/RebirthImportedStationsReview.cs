using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RebirthImportedStationsReview {
 const string Root="Assets/REBIRTHStationAddons/ImportedStations_20261014";
 const string Output="E:/_Haven/shared/REBIRTHTemp/ImportedStationsReview_20261014_v15";
 static List<string> report=new List<string>();static List<Vector3> roof=new List<Vector3>();
 static Bounds BoundsOf(GameObject o){var rs=o.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
 static Texture2D Load(string p,bool color,bool normal=false){var i=(TextureImporter)AssetImporter.GetAtPath(p);i.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;i.sRGBTexture=color;i.maxTextureSize=2048;i.isReadable=true;i.textureCompression=TextureImporterCompression.Uncompressed;i.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(p);}
 public static GameObject Import(string name){
  var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+name+"/Station.prefab");if(existing!=null){var copy=UnityEngine.Object.Instantiate(existing);copy.name=name.Replace('+',' ');long count=0;foreach(var f in copy.GetComponentsInChildren<MeshFilter>())count+=f.sharedMesh.triangles.Length/3;report.Add(copy.name+": triangles="+count);return copy;}
  string folder=Root+"/"+name;string fbx=Directory.GetFiles(folder,"*.fbx")[0].Replace('\\','/');var i=(ModelImporter)AssetImporter.GetAtPath(fbx);i.materialImportMode=ModelImporterMaterialImportMode.None;i.importNormals=ModelImporterNormals.Import;i.importTangents=ModelImporterTangents.CalculateMikk;i.SaveAndReimport();
  var o=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));o.name=name.Replace('+',' ');
  var paths=Directory.GetFiles(folder,"*",SearchOption.AllDirectories);Func<string,string> find=s=>paths.Single(p=>Path.GetFileNameWithoutExtension(p).EndsWith("_"+s)).Replace('\\','/');
  var albedo=Load(find("basecolor"),true);var normal=Load(find("normal"),false,true);var rough=Load(find("roughness"),false);var metal=Load(find("metallic"),false);
  var rp=rough.GetPixels();var mp=metal.GetPixels();if(rp.Length!=mp.Length)throw new Exception("Map mismatch "+name);var pack=new Texture2D(metal.width,metal.height,TextureFormat.RGBA32,false,true);for(int n=0;n<mp.Length;n++)mp[n]=new Color(mp[n].r,0,0,1-rp[n].r);pack.SetPixels(mp);pack.Apply();string p=folder+"/metallic-smoothness.png";File.WriteAllBytes(p,pack.EncodeToPNG());AssetDatabase.ImportAsset(p);
  var mat=new Material(Shader.Find("Standard"));mat.name=o.name;mat.SetTexture("_MainTex",albedo);mat.SetTexture("_BumpMap",normal);mat.SetTexture("_MetallicGlossMap",Load(p,false));mat.SetFloat("_GlossMapScale",1);mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICGLOSSMAP");AssetDatabase.CreateAsset(mat,folder+"/Station.mat");
  foreach(var r in o.GetComponentsInChildren<Renderer>()){var slots=r.sharedMaterials;for(int s=0;s<slots.Length;s++)slots[s]=mat;r.sharedMaterials=slots;}
  long tris=0;foreach(var f in o.GetComponentsInChildren<MeshFilter>())tris+=f.sharedMesh.triangles.Length/3;
  report.Add(o.name+": triangles="+tris+"; supplied textures="+albedo.width+"x"+albedo.height+"; no supplied emission map");PrefabUtility.SaveAsPrefabAsset(o,folder+"/Station.prefab");return o;
 }
 static float RoofY(float x,float z){return roof.OrderBy(p=>(p.x-x)*(p.x-x)+(p.z-z)*(p.z-z)).First().y;}
 static float FurnitureY(Transform parent,float x,float z,float fallback){float best=-100;foreach(var f in parent.GetComponentsInChildren<MeshFilter>()){var v=f.sharedMesh.vertices;var t=f.sharedMesh.triangles;for(int i=0;i<t.Length;i+=3){var a=f.transform.TransformPoint(v[t[i]]);var b=f.transform.TransformPoint(v[t[i+1]]);var c=f.transform.TransformPoint(v[t[i+2]]);if(Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y)<.95f)continue;float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<.000001f)continue;float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/det;float w=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/det;float q=1-u-w;if(u<0||w<0||q<0)continue;float y=u*a.y+w*b.y+q*c.y;if(y>.5f&&y<1.65f)best=Mathf.Max(best,y);}}if(best<-10)throw new Exception("No measured furniture surface at "+x+","+z);return best+.002f;}
 static void Place(GameObject o,Transform parent,Vector3 center,float width,float depth,float yaw){
  if(parent.name=="REBIRTHCampInteriorStations")center.y=FurnitureY(parent.parent,center.x,center.z,center.y);o.transform.SetParent(parent,true);o.transform.rotation=Quaternion.Euler(0,yaw,0)*o.transform.rotation;var b=BoundsOf(o);float scale=Mathf.Min(width/b.size.x,depth/b.size.z);o.transform.localScale*=scale;b=BoundsOf(o);o.transform.position+=new Vector3(center.x-b.center.x,center.y-b.min.y,center.z-b.center.z);b=BoundsOf(o);report.Add(o.name+" placed bounds="+b+"; yaw="+yaw);
 }
 static void Tube(Transform parent,Vector3 a,Vector3 b,Material mat){var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder);o.name="Guardrail tube";o.transform.SetParent(parent);o.transform.position=(a+b)*.5f;o.transform.up=(b-a).normalized;o.transform.localScale=new Vector3(.028f,(b-a).magnitude*.5f,.028f);o.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());}
 static void Render(Camera cam,Vector3 eye,Vector3 target,string name,bool ortho,float size=1){cam.transform.position=eye;cam.transform.LookAt(target);cam.orthographic=ortho;cam.orthographicSize=size;cam.fieldOfView=65;var rt=new RenderTexture(1600,1200,24);var im=new Texture2D(1600,1200,TextureFormat.RGB24,false);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;im.ReadPixels(new Rect(0,0,1600,1200),0,0);im.Apply();File.WriteAllBytes(Output+"/"+name+".png",im.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(im);}
 public static void RunBatch(){Directory.CreateDirectory(Output);try{
  AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  foreach(var line in File.ReadAllLines("E:/_Haven/shared/REBIRTHTemp/CamperRoofPrototype_20261012/roof-surface.csv").Skip(1)){var v=line.Split(',');roof.Add(new Vector3(float.Parse(v[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(v[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(v[2],System.Globalization.CultureInfo.InvariantCulture)));}
  var camper=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperWrapGenerated/BundleCandidates/3a11c6dfad874047ad18f6ffddec5d7b/rebirthCamper80s.prefab"));camper.name="Camper with supplied station previews";camper.transform.position=Vector3.zero;camper.transform.rotation=Quaternion.identity;
  var roofGroup=new GameObject("REBIRTHCampRoofStations").transform;roofGroup.SetParent(camper.transform,false);var inner=new GameObject("REBIRTHCampInteriorStations").transform;inner.SetParent(camper.transform,false);
  string[] roofNames={"stone+forge","rusty+fire+pit","tool+bench","cement+mixer","industrial+oven","circular+saw","shredder+compact+roof+module","vintage+sewing+machine","recycling+station","ammo+recycler"};
  float[] rows={.12f,-.70f,-1.45f,-2.15f,-2.80f};var roofBounds=new List<Bounds>();
  for(int n=0;n<roofNames.Length;n++){float x=n%2==0?-.44f:.44f;float z=rows[n/2];float width=.73f,depth=.63f;if(n==0){x=-.37f;width=.9125f;depth=.95f;}if(n==1){x=.53f;width=.58765f;depth=.6118f;}if(n==6){width=.73f;depth=.60f;}if(n==7){width=.66f;depth=.57f;}if(n==8){width=.78f;depth=.54f;}if(n==9){width=.70f;depth=.52f;}var o=Import(roofNames[n]);Place(o,roofGroup,new Vector3(x,RoofY(x,z)+.015f,z),width,depth,0);var b=BoundsOf(o);if(b.min.x<-.85f||b.max.x>.85f||b.min.z< -3.075f||b.max.z>.525f)throw new Exception("Station crosses rail clearance: "+o.name);foreach(var old in roofBounds)if(Mathf.Min(old.max.x,b.max.x)-Mathf.Max(old.min.x,b.min.x)>.005f&&Mathf.Min(old.max.z,b.max.z)-Mathf.Max(old.min.z,b.min.z)>.005f)throw new Exception("Station footprints overlap: "+o.name);roofBounds.Add(b);}
  Place(Import("stone+mortar+pestle"),inner,new Vector3(-.59f,1.387f,-1.82f),.144f,.156f,0);
  Place(Import("steampunk+chemistry+kit"),inner,new Vector3(-.57f,1.538f,-.87f),.49f,.43f,90);
  string[] table={"rusted+camping+stove","wooden+toolbox","steampunk+notebook","steampunk+distillation"};
  for(int n=0;n<4;n++){float x=n%2==0?.38f:.72f;float z=n<2?-1.34f:-1.66f;float size=.28f;if(n==0){x=.46f;z=-1.39f;}if(n==2){size*=.85f;x=.44f;z=-1.63f;}if(n==3){size*=1.25f;x=.71f;z=-1.67f;}Place(Import(table[n]),inner,new Vector3(x,1.387f,z),size,size,90);}
  var rails=new GameObject("Fitted rooftop guardrails").transform;rails.SetParent(camper.transform,false);var iron=new Material(Shader.Find("Standard"));iron.color=new Color(.22f,.19f,.16f);iron.SetFloat("_Metallic",.65f);iron.SetFloat("_Glossiness",.25f);AssetDatabase.CreateAsset(iron,Root+"/Guardrail.mat");
  foreach(float x in new[]{-.88f,.88f}){Vector3? previous=null;for(int n=0;n<=8;n++){float z=-3.1f+n*3.65f/8;float y=RoofY(x,z);var top=new Vector3(x,y+.24f,z);Tube(rails,new Vector3(x,y,z),top,iron);if(previous.HasValue)Tube(rails,previous.Value,top,iron);previous=top;}}
  foreach(float z in new[]{-3.1f,.55f})Tube(rails,new Vector3(-.88f,RoofY(-.88f,z)+.24f,z),new Vector3(.88f,RoofY(.88f,z)+.24f,z),iron);
  PrefabUtility.SaveAsPrefabAsset(camper,Root+"/CamperStationsCampPreview_v15.prefab");
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);RenderSettings.fog=false;var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.1f;key.transform.rotation=Quaternion.Euler(45,-40,0);key.shadows=LightShadows.Soft;var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.5f;fill.transform.rotation=Quaternion.Euler(25,130,0);
  var cam=new GameObject("Review Camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.14f,.16f);cam.nearClipPlane=.01f;cam.farClipPlane=100;
  Render(cam,new Vector3(5,7,5),new Vector3(0,1.6f,-1),"roof-quarter",true,3.7f);Render(cam,new Vector3(-4,7,-6),new Vector3(0,1.6f,-1),"roof-rear",true,3.7f);Render(cam,new Vector3(0,8,-1.25f),new Vector3(0,2.4f,-1.25f),"roof-top",true,2.4f);
  Render(cam,new Vector3(4,6,3),new Vector3(0,2.65f,-1.3f),"roof-detail",true,2.3f);
  foreach(var r in camper.GetComponentsInChildren<Renderer>())if(r.name=="camper_body"||r.name=="windows")r.enabled=false;roofGroup.gameObject.SetActive(false);rails.gameObject.SetActive(false);
  Render(cam,new Vector3(3.1f,3.9f,-3.9f),new Vector3(0,1.45f,-1.45f),"interior-cutaway",true,1.6f);
  Render(cam,new Vector3(.1f,1.95f,-2.6f),new Vector3(.54f,1.48f,-1.5f),"table-stations",false);
  Render(cam,new Vector3(.45f,2,-1.65f),new Vector3(-.57f,1.48f,-1.4f),"sink-stations",false);
  foreach(var r in camper.GetComponentsInChildren<Renderer>(true))r.enabled=true;roofGroup.gameObject.SetActive(true);rails.gameObject.SetActive(true);
    int campStationRenderers=roofGroup.GetComponentsInChildren<Renderer>().Length+inner.GetComponentsInChildren<Renderer>().Length;
  roofGroup.gameObject.SetActive(false);inner.gameObject.SetActive(false);rails.gameObject.SetActive(true);
  if(roofGroup.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled&&r.gameObject.activeInHierarchy)||inner.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled&&r.gameObject.activeInHierarchy))throw new Exception("Travel stations still active");
  if(rails.GetComponentsInChildren<Renderer>().Length==0)throw new Exception("Travel guardrails missing");
  PrefabUtility.SaveAsPrefabAsset(camper,Root+"/CamperStationsPreview_v15.prefab");
  Render(cam,new Vector3(5,7,5),new Vector3(0,1.6f,-1),"travel-guardrails",true,3.7f);
  report.Add("Camp station renderers="+campStationRenderers+"; travel station renderers=0; travel guardrail renderers="+rails.GetComponentsInChildren<Renderer>().Length+". Separate station groups inactive in travel prefab; guardrails active. Runtime camp-menu integration pending.");
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/CamperStationsReview_v15.unity");AssetDatabase.SaveAssets();report.Add("Placement candidate only. Supplied originals preserved; no game installation; All16 supplied stations imported; all10 roof stations arranged in five rows. Existing interior and approved size adjustments retained. Roof footprint overlap and guardrail clearance assertions passed. Full performance and clearance approval pending.");File.WriteAllLines(Output+"/receipt.txt",report);
 }catch(Exception e){File.WriteAllText(Output+"/failure.txt",e.ToString());throw;}}
}











