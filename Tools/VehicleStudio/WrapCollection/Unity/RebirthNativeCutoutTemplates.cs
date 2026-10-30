using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Native geometry produces the masks. No artwork, rectangle-shaped decal,
// generated vehicle silhouette or source UV assumption is used as a cutout.
public static class RebirthNativeCutoutTemplates
{
 [Serializable] public sealed class Panel { public string name; public Vector3 center,normal,up,right; public float width,height; public float cameraDistance=8; public float familyOverride=-1; public bool truck; public int pixelsWide,pixelsHigh; public int paintablePixels,visiblePixels; }
 [Serializable] public sealed class Manifest { public string status="Native geometry masks; artwork still requires native fit verification"; public int pixelsPerMetre=640; public Panel[] panels; }
 static string Arg(string k){var a=Environment.GetCommandLineArgs();return a[Array.IndexOf(a,k)+1];}
 static Panel P(string name,Vector3 center,Vector3 normal,Vector3 up,float width,float height,bool truck=false)=>new Panel{name=name,center=center,normal=normal,up=up,width=width,height=height,truck=truck,right=Vector3.Cross(up,-normal).normalized};
 static void WriteBedNet(string output){
  string source=Path.Combine(Path.GetDirectoryName(output),"Templates/NativeCutouts-v3");
  foreach(string suffix in new[]{"paintable","visible-safe"}){
   var ids=new[]{"pickup-bed-floor","pickup-inner-bed-left","pickup-inner-bed-right","pickup-inner-bed-front","pickup-inner-tailgate"};
   var images=ids.Select(id=>{var t=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(t,File.ReadAllBytes(Path.Combine(source,id+"-"+suffix+".png")));return t;}).ToArray();
   int middle=Math.Max(images[0].width,Math.Max(images[3].width,images[4].width)),side=images[1].height,bottom=images[3].height,top=images[4].height;
   int w=middle+side*2,h=bottom+images[0].height+top;var dest=new Texture2D(w,h,TextureFormat.RGBA32,false);var pixels=new Color32[w*h];
   for(int part=0;part<images.Length;part++){
    var t=images[part];var src=t.GetPixels32();
    for(int y=0;y<t.height;y++)for(int x=0;x<t.width;x++){
     int dx=0,dy=0;
     if(part==0){dx=side+(middle-t.width)/2+x;dy=bottom+y;}
     if(part==1){dx=side+middle+y;dy=bottom+t.width-1-x;}
     if(part==2){dx=t.height-1-y;dy=bottom+x;}
     if(part==3){dx=side+t.width-1-x;dy=t.height-1-y;}
     if(part==4){dx=side+(middle-t.width)/2+x;dy=bottom+images[0].height+y;}
     pixels[dy*w+dx]=src[y*t.width+x];
    }
   }
   dest.SetPixels32(pixels);dest.Apply();File.WriteAllBytes(Path.Combine(output,"pickup-bed-interior-unfolded-"+suffix+".png"),dest.EncodeToPNG());
   foreach(var t in images)Object.DestroyImmediate(t);Object.DestroyImmediate(dest);
  }
 }
 public static void Run(){
  string output=Arg("-wrapReviewOutput");Directory.CreateDirectory(output);
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  const string shaderPath="Assets/Editor/NativePaintCutout.shader";
  File.WriteAllText(shaderPath,@"Shader ""Hidden/REBIRTH/NativePaintCutout"" { Properties { _Paint(""Paint"",Float)=0 _PaintFamily(""Paint family"",Float)=0 _MainTex(""Native"",2D)=""white""{} _NeutralTex(""Neutral"",2D)=""white""{} _RearDoor(""Door"",Float)=0 _NativeBody(""Outer body mesh"",Float)=0 } SubShader { Tags { ""RenderType""=""Opaque"" } Pass { Cull Off ZWrite On CGPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #include ""UnityCG.cginc""
  sampler2D _MainTex,_NeutralTex; float4 _MainTex_ST; float _SurfaceOnly,_RearDoor,_ExteriorSide,_NativeBody,_CutoutExteriorBody,_ExcludeRearDoor,_CutoutDepth; float _Paint,_PaintFamily,_CutoutFamily; float4 _CutoutCenter,_CutoutNormal,_CutoutUp,_CutoutRight,_CutoutSize;
  struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
  v2f vert(appdata_base v){v2f o;o.uv=v.texcoord.xy*_MainTex_ST.xy+_MainTex_ST.zw;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
  fixed4 frag(v2f i):SV_Target { if(_SurfaceOnly>.5)clip(_Paint-.5);
   float3 a=tex2D(_MainTex,i.uv).rgb,b=tex2D(_NeutralTex,i.uv).rgb;
   #ifndef UNITY_COLORSPACE_GAMMA
   a=LinearToGammaSpace(a);b=LinearToGammaSpace(b);
   #endif
   float hi=max(a.r,max(a.g,a.b)),lo=min(a.r,min(a.g,a.b));
   float gray=smoothstep(.30,.46,lo)*(1-smoothstep(.08,.18,hi-lo));
   float red=step(a.g*1.7,a.r)*step(a.b*1.7,a.r)*step(.12,a.r);
   float nativeMask=lerp(saturate((length(a-b)-.015)/.045),max(gray,red),_PaintFamily);
   float hatch=step(.45714,i.uv.x)*step(i.uv.x,.54030)*step(.64743,i.uv.y)*step(i.uv.y,.75143);
   float vent=step(.52699,i.uv.x)*step(i.uv.x,.63214)*step(.37387,i.uv.y)*step(i.uv.y,.45700);
   nativeMask*=1-_PaintFamily*max(hatch,vent);
   if(_RearDoor>.5)nativeMask=max(nativeMask,step(.74,i.world.y)*step(i.world.y,1.90)*step(abs(i.world.x),.49));
   float3 d=i.world-_CutoutCenter.xyz;float mask=step(abs(dot(d,_CutoutRight.xyz)),_CutoutSize.x*.5)*step(abs(dot(d,_CutoutUp.xyz)),_CutoutSize.y*.5)*step(.55,dot(normalize(i.normal),_CutoutNormal.xyz))*_Paint*lerp(1,_NativeBody,_CutoutExteriorBody*(1-_PaintFamily))*(1-_RearDoor*_ExcludeRearDoor)*lerp(1,step(0,dot(i.world,float3(_CutoutNormal.x,0,0))),_ExteriorSide)*step(.5,nativeMask)*lerp(1-step(.5,abs(_PaintFamily-_CutoutFamily)),1,step(1.5,_CutoutFamily));mask*=lerp(1,step(abs(dot(d,_CutoutNormal.xyz)),_CutoutDepth),step(.001,_CutoutDepth));return float4(mask,mask,mask,1);}
  ENDCG } } }");
  AssetDatabase.ImportAsset(shaderPath,ImportAssetOptions.ForceSynchronousImport);
  var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Cutout shader failed");
  var bundle=AssetBundle.LoadFromFile(Arg("-wrapVehicleBundle"));if(!bundle)throw new Exception("Native bundle missing");
  var root=Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;
  var materials=new List<Material>();var original=root.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r,r=>r.sharedMaterials);
  var paint=original.ToDictionary(p=>p.Key,p=>p.Value.Any(m=>m&&m.HasProperty("_PatternEnabled")));
  foreach(var group in root.GetComponentsInChildren<LODGroup>(true))group.ForceLOD(0);
  foreach(var pair in original){
   pair.Key.sharedMaterials=pair.Value.Select(m=>{var n=new Material(shader);n.SetFloat("_Paint",m&&m.HasProperty("_PatternEnabled")?1:0);n.SetFloat("_PaintFamily",m&&m.HasProperty("_IsCamper")?m.GetFloat("_IsCamper"):0);n.SetFloat("_NativeBody",pair.Key.name=="body"||pair.Key.name=="Camper shared body body"?1:0);n.SetFloat("_RearDoor",pair.Key.name=="RearDoor_camper_body"?1:0);if(m&&m.HasProperty("_MainTex")){n.SetTexture("_MainTex",m.GetTexture("_MainTex"));n.SetTextureScale("_MainTex",m.GetTextureScale("_MainTex"));n.SetTextureOffset("_MainTex",m.GetTextureOffset("_MainTex"));}if(m&&m.HasProperty("_NeutralTex"))n.SetTexture("_NeutralTex",m.GetTexture("_NeutralTex"));materials.Add(n);return n;}).ToArray();
   pair.Key.SetPropertyBlock(null);for(int slot=0;slot<pair.Value.Length;slot++)pair.Key.SetPropertyBlock(null,slot);
   if(pair.Key.name.Contains("REBIRTHDistanceLOD"))pair.Key.enabled=false;
  }
  var panels=new List<Panel>();
  foreach(int side in new[]{-1,1}){
   string prefix=side<0?"left-":"right-";var n=new Vector3(side,0,0);
   // These domains partition the projected surfaces; the mesh/depth pass
   // supplies their actual outlines, window apertures and wheel openings.
   panels.Add(P(prefix+"camper-top-fascia",new Vector3(0,2.94f,-1.245f),n,Vector3.up,4.41f,.22f));
   panels.Add(P(prefix+"camper-banner",new Vector3(0,2.54f,-1.245f),n,Vector3.up,4.41f,.60f));
   panels.Add(P(prefix+"camper-main-wall",new Vector3(0,1.805f,-1.245f),n,Vector3.up,4.41f,.87f));
   panels.Add(P(prefix+"camper-front-slope",new Vector3(0,1.98f,.62f),n,Vector3.up,.68f,1.58f));
   panels.Add(P(prefix+"truck-front-fender",new Vector3(0,.94f,1.22f),n,Vector3.up,1.48f,1.02f,true));
   panels.Add(P(prefix+"truck-cab-door",new Vector3(0,.97f,-.085f),n,Vector3.up,1.13f,1.08f,true));
   panels.Add(P(prefix+"truck-bed",new Vector3(0,.90f,-1.65f),n,Vector3.up,2f,.94f,true));
   panels.Add(P(prefix+"camper-rear-service",new Vector3(0,.90f,-3.05f),n,Vector3.up,.80f,.94f));
  }
  panels.Add(P("truck-front-face",new Vector3(0,.90f,1.96f),Vector3.forward,Vector3.up,1.80f,.80f,true));
  // Native hood curl reaches z > 2.01 while still upward-dominant. The old
  // z max 1.96 clipped those real paintable faces and left a transverse band.
  // Keep the rear edge at .48; extend the canvas to 2.06 before creating art.
  panels.Add(P("hood",new Vector3(0,1.25f,1.27f),Vector3.up,Vector3.back,1.60f,1.58f,true));
  panels.Add(P("cab-roof",new Vector3(0,1.55f,.05f),Vector3.up,Vector3.back,1.70f,.80f,true));
  panels.Add(P("camper-roof",new Vector3(0,3.02f,-1.245f),Vector3.up,Vector3.back,2.20f,4.41f));
  panels.Add(P("camper-front",new Vector3(0,2.23f,1.1f),Vector3.forward,Vector3.up,2.20f,1.16f));
  panels.Add(P("rear-door",new Vector3(0,1.73f,-3.31f),Vector3.back,Vector3.up,1.06f,2.06f));
  panels.Add(P("rear-fixed-left",new Vector3(-.83f,1.775f,-3.31f),Vector3.back,Vector3.up,.54f,2.65f));
  panels.Add(P("rear-fixed-right",new Vector3(.83f,1.775f,-3.31f),Vector3.back,Vector3.up,.54f,2.65f));
  panels.Add(P("cab-back",new Vector3(0,1.375f,-.65f),Vector3.back,Vector3.up,1.7f,.65f,true));
  // Native extent diagnostic128: actual upper tailgate face exceeds old Ymax
  // 1.10. Keep lower bound .45, include top through1.25 before authoring art.
  panels.Add(P("pickup-tailgate",new Vector3(0,.85f,-2.7f),Vector3.back,Vector3.up,1.7f,.80f,true));
  panels.Add(P("camper-front-fascia",new Vector3(0,2.94f,1.1f),Vector3.forward,Vector3.up,2.20f,.26f));
  panels.Add(P("rear-upper-header",new Vector3(0,2.93f,-3.31f),Vector3.back,Vector3.up,1.12f,.34f));
  panels.Add(P("pickup-bed-floor",new Vector3(0,.70f,-1.57f),Vector3.up,Vector3.back,1.52f,2.20f,true));
  foreach(int side in new[]{-1,1}){var inner=P(side<0?"pickup-inner-bed-left":"pickup-inner-bed-right",new Vector3(side*.78f,.91f,-1.57f),new Vector3(-side,0,0),Vector3.up,2.20f,.44f,true);inner.cameraDistance=.35f;panels.Add(inner);}
  var frontInner=P("pickup-inner-bed-front",new Vector3(0,.90f,-.43f),Vector3.back,Vector3.up,1.54f,.44f,true);frontInner.cameraDistance=.35f;panels.Add(frontInner);
  var tailInner=P("pickup-inner-tailgate",new Vector3(0,.92f,-2.70f),Vector3.forward,Vector3.up,1.42f,.42f,true);tailInner.cameraDistance=.35f;panels.Add(tailInner);
  if(Environment.GetCommandLineArgs().Contains("-wrapCombinedCutouts")){
   var details=panels;
   panels=new List<Panel>();
   foreach(int side in new[]{-1,1}){var whole=P(side<0?"left-continuous":"right-continuous",new Vector3(0,1.73f,-.745f),new Vector3(side,0,0),Vector3.up,5.60f,2.74f);whole.familyOverride=2;panels.Add(whole);}
   foreach(string name in new[]{"hood","cab-roof","camper-roof","rear-door","cab-back","pickup-tailgate","truck-front-face"})panels.Add(details.Single(p=>p.name==name));
   panels.Add(P("camper-front-continuous",new Vector3(0,2.34f,1.1f),Vector3.forward,Vector3.up,2.20f,1.48f));
   panels.Add(P("camper-rear-fixed-continuous",new Vector3(0,1.775f,-3.31f),Vector3.back,Vector3.up,2.20f,2.65f));
  }
  var camera=new GameObject("Native cutout camera").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.allowHDR=false;camera.allowMSAA=false;
  foreach(var panel in panels){
   foreach(var t in root.GetComponentsInChildren<Transform>(true)){
    if(t.name=="REBIRTHCamperBodyModule")t.gameObject.SetActive(!panel.truck);
    if(t.name=="REBIRTHPickupOnlyPaint"||t.name=="REBIRTHPickupOnlyBodywork")t.gameObject.SetActive(panel.truck);
    if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment","REBIRTHCampStairVisuals","REBIRTHCampWalkInColliders"}.Contains(t.name))t.gameObject.SetActive(false);
   }
   camera.transform.position=panel.center+panel.normal*panel.cameraDistance;camera.transform.LookAt(panel.center,panel.up);camera.orthographicSize=panel.height*.5f;camera.aspect=panel.width/panel.height;
   Shader.SetGlobalFloat("_ExcludeRearDoor",panel.name=="camper-rear-fixed-continuous"?1:0);Shader.SetGlobalFloat("_CutoutExteriorBody",panel.name=="left-continuous"||panel.name=="right-continuous"?1:panel.truck&&!panel.name.Contains("inner")&&panel.name!="pickup-tailgate"&&panel.name!="pickup-bed-floor"?1:0);Shader.SetGlobalFloat("_ExteriorSide",Mathf.Abs(panel.normal.x)>.5f&&!panel.name.Contains("inner")?1:0);Shader.SetGlobalFloat("_CutoutFamily",panel.familyOverride>=0?panel.familyOverride:panel.truck?0:1);Shader.SetGlobalVector("_CutoutCenter",panel.center);Shader.SetGlobalVector("_CutoutNormal",panel.normal);Shader.SetGlobalVector("_CutoutUp",panel.up);Shader.SetGlobalVector("_CutoutRight",camera.transform.right);Shader.SetGlobalVector("_CutoutSize",new Vector4(panel.width,panel.height,0,0));
   // An end projection must not turn distant cab-back paint into tailgate
   // coverage through genuine exclusions or above the actual tailgate outline.
   Shader.SetGlobalFloat("_CutoutDepth",panel.name=="pickup-tailgate"?.20f:0);
   panel.pixelsWide=Mathf.CeilToInt(panel.width*640);panel.pixelsHigh=Mathf.CeilToInt(panel.height*640);
   // Physical surface pass removes ladders/wheels/exterior accessories as
   // occluders, but keeps glass: the actual window aperture stays excluded.
   // A second safe-visibility mask includes all hardware occlusion.
   foreach(bool visible in new[]{false,true}){
    var saved=original.Keys.ToDictionary(r=>r,r=>r.enabled);
    Shader.SetGlobalFloat("_SurfaceOnly",visible?0:1);
    if(!visible)foreach(var r in original.Keys)if(!paint[r])r.enabled=false;
    var rt=new RenderTexture(panel.pixelsWide,panel.pixelsHigh,24,RenderTextureFormat.ARGB32){antiAliasing=1};camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
    var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);var pixels=texture.GetPixels32();int count=0;
    for(int i=0;i<pixels.Length;i++){bool allowed=pixels[i].r>127;pixels[i]=allowed?new Color32(255,255,255,255):new Color32(0,0,0,0);if(allowed)count++;}
    texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(Path.Combine(output,panel.name+(visible?"-visible-safe.png":"-paintable.png")),texture.EncodeToPNG());if(visible)panel.visiblePixels=count;else panel.paintablePixels=count;
    camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(texture);rt.Release();Object.DestroyImmediate(rt);foreach(var pair in saved)pair.Key.enabled=pair.Value;
   }
  }
  if(Environment.GetCommandLineArgs().Contains("-wrapCombinedCutouts"))WriteBedNet(output);
  File.WriteAllText(Path.Combine(output,"native-cutout-layout.json"),JsonUtility.ToJson(new Manifest{panels=panels.ToArray()},true));
  Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(root);foreach(var m in materials)Object.DestroyImmediate(m);bundle.Unload(true);
  Debug.Log("NATIVE_CUTOUT_TEMPLATES_WRITTEN panels="+panels.Count+" masks="+panels.Count*2);
 }
}

