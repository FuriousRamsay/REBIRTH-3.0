using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Dedicated disposable batch scene only: never saves prefab, material or scene.
public static class RebirthCamperLightingReview
{
    [Serializable] public class Lamp { public string path,mode; public Vector3 position,direction; public float range,angle,intensity; public bool enabled; }
    [Serializable] public class Report { public string status="FAIL",prefab,error,scope="Unity authored lights only; native game is not launched or simulated."; public string[] images; public Lamp[] lamps; public bool headlightsDirectional,rearDirectional,markersIndependent; }
    public static void RunBatch()
    {
        string output=Arg("-rebirthLightingReport","E:/_Haven/shared/REBIRTHTemp/CamperLightingReview");Directory.CreateDirectory(output);
        var report=new Report{prefab=Arg("-rebirthLightingPrefab","")};var owned=new List<UnityEngine.Object>();
        int previousPixelLights=QualitySettings.pixelLightCount;
        try
        {
            if(!Application.isBatchMode || Application.isPlaying)throw new Exception("Dedicated batch editor required.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(report.prefab);if(source==null)throw new Exception("Candidate prefab missing.");
            var root=UnityEngine.Object.Instantiate(source);owned.Add(root);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var model=root.transform.Find("GameObject")??root.transform;
            foreach(var rb in root.GetComponentsInChildren<Rigidbody>(true))rb.isKinematic=true;
            QualitySettings.pixelLightCount=64;
            foreach(var light in root.GetComponentsInChildren<Light>(true)){light.enabled=false;light.renderMode=LightRenderMode.ForcePixel;}
            var heads=model.Find("M/HeadLights/Lights");var rear=model.Find("M/RearLights");var markers=model.Find("M/BodyMarkerLights");
            if(heads==null||rear==null||markers==null)throw new Exception("Authored lighting groups required.");
            var h=heads.GetComponentsInChildren<Light>(true);var r=rear.GetComponentsInChildren<Light>(true);var m=markers.GetComponentsInChildren<Light>(true);
            if(h.Length!=2 || r.Length!=2 || m.Length<2)throw new Exception("Lamp group counts invalid.");
            report.headlightsDirectional=h.All(l=>l.type==LightType.Spot && Vector3.Dot(model.InverseTransformDirection(l.transform.forward),Vector3.forward)>.99f && l.range<=32.001f && l.spotAngle<=48.001f);
            report.rearDirectional=r.All(l=>l.type==LightType.Spot && Vector3.Dot(model.InverseTransformDirection(l.transform.forward),Vector3.back)>.999f && l.range<=4.001f && l.spotAngle<=48.001f);
            if(!report.headlightsDirectional||!report.rearDirectional||m.Any(l=>l.type!=LightType.Spot||l.range>.651f))throw new Exception("Directional/restrained lamp gate failed.");
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.035f,.035f,.045f);RenderSettings.fog=false;
            var fill=new GameObject("NightFill");owned.Add(fill);var sun=fill.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.35f;fill.transform.rotation=Quaternion.Euler(35,140,0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);owned.Add(floor);floor.name="ReviewGround";floor.transform.position=new Vector3(0,-.02f,0);floor.transform.localScale=new Vector3(9,1,9);
            var ground=new Material(Shader.Find("Standard"));owned.Add(ground);ground.color=new Color(.22f,.22f,.22f);ground.SetFloat("_Glossiness",0);floor.GetComponent<Renderer>().sharedMaterial=ground;
            var cameraObject=new GameObject("ReviewCamera");owned.Add(cameraObject);var camera=cameraObject.AddComponent<Camera>();camera.backgroundColor=new Color(.012f,.012f,.02f);camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.05f;camera.farClipPlane=120;camera.allowHDR=true;
            var records=new List<Lamp>();var images=new List<string>();
            Set(h,1.1f,true);Set(r,0,false);Set(m,0,false);heads.gameObject.SetActive(true);
            Emission(model,"M/VisualBody/80sCamperTruck/headlights_glass",Color.white);Record(model,"headlights_only",h.Concat(r).Concat(m),records);
            Capture(camera,new Vector3(0,17,13),new Vector3(0,0,9),"headlights_top",output,images);
            Capture(camera,new Vector3(-10,5,7),new Vector3(0,1,0),"headlights_front_side",output,images);
            Set(h,0,false);heads.gameObject.SetActive(false);Emission(model,"M/VisualBody/80sCamperTruck/headlights_glass",Color.black);Set(r,.65f,true);Set(m,0,false);Emission(model,"M/VisualBody/80sCamperTruck/lights/RearRedLenses",Color.red*.65f);
            Record(model,"braking_only",h.Concat(r).Concat(m),records);Capture(camera,new Vector3(0,11,-9),new Vector3(0,0,-3),"brake_top",output,images);
            Capture(camera,new Vector3(-6,3,-8),new Vector3(0,1,-2.5f),"brake_rear_side",output,images);
            Set(r,0,false);Emission(model,"M/VisualBody/80sCamperTruck/lights/RearRedLenses",Color.black);Set(m,.08f,true);
            Emission(model,"M/VisualBody/80sCamperTruck/lights/BodyMarkerLenses",Color.red*.08f);
            report.markersIndependent=h.All(l=>!l.enabled)&&r.All(l=>!l.enabled)&&m.All(l=>l.enabled&&Mathf.Abs(l.intensity-.08f)<.00001f);
            if(!report.markersIndependent)throw new Exception("Marker-only independence gate failed.");
            Record(model,"engine_markers_only_headlights_off_not_reversing",h.Concat(r).Concat(m),records);
            Capture(camera,new Vector3(-8,3.8f,0),new Vector3(0,1.5f,-.8f),"engine_markers_side",output,images);
            Capture(camera,new Vector3(5,4,-7),new Vector3(0,1.8f,-1.8f),"engine_markers_rear",output,images);
            Set(m,0,false);Emission(model,"M/VisualBody/80sCamperTruck/lights/BodyMarkerLenses",Color.black);
            Record(model,"engine_off_all_lamps_off",h.Concat(r).Concat(m),records);
            Capture(camera,new Vector3(-8,3.8f,0),new Vector3(0,1.5f,-.8f),"engine_off_side",output,images);
            report.lamps=records.ToArray();report.images=images.ToArray();report.status="PASS_UNITY_DIRECTION_STATE_GATES_IMAGES_REQUIRE_REVIEW";
        }
        catch(Exception e){report.error=e.ToString();throw;}
        finally{QualitySettings.pixelLightCount=previousPixelLights;File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));foreach(var obj in owned.AsEnumerable().Reverse())if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);}
    }
    static void Set(IEnumerable<Light> lamps,float intensity,bool enabled){foreach(var l in lamps){l.intensity=intensity;l.enabled=enabled;}}
    static void Emission(Transform model,string path,Color color){var t=model.Find(path);if(t==null)return;var renderer=t.GetComponent<Renderer>();if(renderer==null)return;var b=new MaterialPropertyBlock();renderer.GetPropertyBlock(b);b.SetColor("_EmissionColor",color);renderer.SetPropertyBlock(b);}
    static void Record(Transform model,string mode,IEnumerable<Light> lights,List<Lamp> target){foreach(var l in lights)target.Add(new Lamp{path=AnimationUtility.CalculateTransformPath(l.transform,model),mode=mode,position=model.InverseTransformPoint(l.transform.position),direction=model.InverseTransformDirection(l.transform.forward),range=l.range,angle=l.spotAngle,intensity=l.intensity,enabled=l.enabled});}
    static void Capture(Camera camera,Vector3 position,Vector3 target,string name,string folder,List<string> images)
    {
        camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=48;
        var rt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();string path=Path.Combine(folder,name+".png");File.WriteAllBytes(path,tex.EncodeToPNG());images.Add(path);}
        finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
    static string Arg(string name,string fallback){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
}

