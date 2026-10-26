using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RebirthStandardVehicleMotionReview
{
    private static string reviewPrefab, reviewOutput, cosmetic;
    private static GameObject bundledPrefab;
    public static void ReviewBothBundlesBatch()
    {
        string version="v1", selected=null;
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++)
        {if(args[i]=="-vehicleCandidateVersion")version=args[i+1];if(args[i]=="-vehicleKind")selected=args[i+1];}
        if(!System.Text.RegularExpressions.Regex.IsMatch(version,@"^v[0-9]+$") || (selected!=null && selected!="Buggy" && selected!="Bike"))
            throw new InvalidDataException("Invalid candidate selection");
        try
        {
            foreach(string kind in selected==null?new[]{"Buggy","Bike"}:new[]{selected})
            {
                string path="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/"+kind+"Candidate_"+version+"/rebirth_"+kind.ToLowerInvariant()+".unity3d";
                var bundle=AssetBundle.LoadFromFile(path);
                if(bundle==null)throw new InvalidDataException("Bundle load failed: "+path);
                try
                {
                    bundledPrefab=bundle.LoadAsset<GameObject>("rebirth"+kind);
                    if(bundledPrefab==null)throw new InvalidDataException("Bundled prefab missing");
                    foreach(string state in new[]{"None","Dye","Disco"})
                    {
                        cosmetic=state;reviewPrefab=path;
                        reviewOutput="E:/_Haven/shared/REBIRTHTemp/BuggyBikeAudit_20261012/"+kind+"BundleProof_"+version+"/"+state;
                        if(Directory.Exists(reviewOutput))throw new InvalidOperationException("Proof exists; preserve evidence and choose a new version");
                        ReviewBatch();
                    }
                }
                finally{bundledPrefab=null;bundle.Unload(true);}
            }
        }
        finally{reviewPrefab=null;reviewOutput=null;cosmetic=null;bundledPrefab=null;}
    }
    public static void ReviewBothPaintBatch()
    {
        try
        {
            foreach(string kind in new[]{"Buggy","Bike"})
            foreach(string state in new[]{"None","Dye","Disco"})
            {
                cosmetic=state;
                reviewPrefab="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Paint_v2/rebirth"+kind+".prefab";
                reviewOutput="E:/_Haven/shared/REBIRTHTemp/BuggyBikeAudit_20261012/"+kind+"PaintProof_v2/"+state;
                ReviewBatch();
            }
        }
        finally {reviewPrefab=null;reviewOutput=null;cosmetic=null;}
    }
    public static void ReviewBothLightsBatch()
    {
        try
        {
            foreach(string kind in new[]{"Buggy","Bike"})
            {
                reviewPrefab="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Lights_v2/rebirth"+kind+".prefab";
                reviewOutput="E:/_Haven/shared/REBIRTHTemp/BuggyBikeAudit_20261012/"+kind+"LightsProof_v2";
                ReviewBatch();
            }
        }
        finally {reviewPrefab=null;reviewOutput=null;}
    }
    public static void ReviewBatch()
    {
        var args=Environment.GetCommandLineArgs(); string prefabPath="", output="";
        for(int i=0;i<args.Length-1;i++)
        {if(args[i]=="-vehiclePrefab") prefabPath=args[i+1];if(args[i]=="-vehicleProof") output=args[i+1];}
        if(reviewPrefab!=null){prefabPath=reviewPrefab;output=reviewOutput;}
        if(!Application.isBatchMode) throw new InvalidOperationException("Batch only");
        Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var prefab=bundledPrefab!=null?bundledPrefab:AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(prefab==null) throw new FileNotFoundException(prefabPath);
        var vehicle=UnityEngine.Object.Instantiate(prefab);
        try
        {
            var renderers=vehicle.GetComponentsInChildren<Renderer>();
            if(cosmetic!=null)
            foreach(var renderer in renderers)
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(materials[i]==null || materials[i].shader.name!="REBIRTH/StandardVehiclePaint")continue;
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
                    block.SetColor("_Color",cosmetic=="Dye"?new Color(32,140,32,1):new Color(255,255,255,1));
                    block.SetFloat("_PatternEnabled",cosmetic=="Disco"?1:0);
                    renderer.SetPropertyBlock(block,i);
                }
            }
            if(renderers.Length==0) throw new InvalidDataException("No geometry");
            Bounds bounds=renderers[0].bounds;
            foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            var sun=new GameObject("Proof light").AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=1.1f;
            sun.transform.rotation=Quaternion.Euler(35,140,0);
            var camera=new GameObject("Proof camera").AddComponent<Camera>();
            camera.backgroundColor=new Color(.12f,.14f,.16f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=bounds.center+new Vector3(-1,.45f,1).normalized*bounds.size.magnitude*1.15f;
            camera.transform.LookAt(bounds.center);camera.nearClipPlane=.05f;camera.farClipPlane=100;
            Capture(camera,output+"/rest.png");
            var rearLenses=vehicle.transform.Find("M/RearLenses");
            var rearLights=vehicle.transform.Find("M/RearLights");
            if(rearLenses!=null && rearLights!=null)
            {
                Vector3 originalPosition=camera.transform.position;
                camera.transform.position=bounds.center+new Vector3(-.5f,.25f,-1).normalized*bounds.size.magnitude*.85f;
                camera.transform.LookAt(bounds.center);
                var lensRenderers=rearLenses.GetComponentsInChildren<Renderer>();
                var lamps=rearLights.GetComponentsInChildren<Light>();
                for(int state=0;state<3;state++)
                {
                    float intensity=state==0?0:state==1?.12f:.65f;
                    foreach(var renderer in lensRenderers)
                    {var block=new MaterialPropertyBlock();block.SetColor("_EmissionColor",Color.red*intensity);renderer.SetPropertyBlock(block);}
                    foreach(var lamp in lamps){lamp.intensity=intensity;lamp.enabled=intensity>0;}
                    Capture(camera,output+"/rear-"+new[]{"off","running","brake"}[state]+".png");
                }
                camera.transform.position=originalPosition;camera.transform.LookAt(bounds.center);
            }
            var fork=vehicle.transform.Find("M/Forks");
            if(fork!=null) fork.localRotation=Quaternion.Euler(0,25,0);
            int count=0;
            foreach(string path in new[]{"Wheel0","Wheel1","Wheel2","Wheel3","M/Forks/Wheel0"})
            {
                var wheel=vehicle.transform.Find(path);
                if(wheel==null) continue;
                wheel.localRotation=Quaternion.Euler(85,fork==null&&count<2?25:0,0);count++;
                if(wheel.GetComponentsInChildren<MeshFilter>().Length==0)
                    throw new InvalidDataException("Empty wheel pivot: "+path);
            }
            if(count!=(fork==null?4:2)) throw new InvalidDataException("Wheel count mismatch");
            Capture(camera,output+"/steer-and-spin.png");
            var steering=vehicle.transform.Find("M/Steering");
            if(steering!=null)
            {
                Vector3 center=steering.position;
                camera.transform.position=center+vehicle.transform.TransformVector(new Vector3(.7f,.4f,-.5f));
                camera.transform.LookAt(center);
                Capture(camera,output+"/steering-rest.png");
                steering.localRotation*=Quaternion.Euler(0,0,-60);
                if(Vector3.Distance(center,steering.position)>.0001f)throw new InvalidDataException("Steering pivot moved during turn");
                Capture(camera,output+"/steering-turned.png");
            }
            File.WriteAllText(output+"/result.txt","Rendered actual candidate: "+prefabPath+"\nWheel pivots with geometry: "+count+"\nMotion images require visual inspection. Physics and rider fit are not tested by this review.");
        }
        finally {UnityEngine.Object.DestroyImmediate(vehicle);}
    }
    static void Capture(Camera camera,string path)
    {
        var target=new RenderTexture(1280,900,24);
        var pixels=new Texture2D(1280,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
        finally {camera.targetTexture=null;RenderTexture.active=previous;target.Release();
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
    }
}

