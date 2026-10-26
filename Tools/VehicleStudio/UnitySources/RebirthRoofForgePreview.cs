using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class RebirthRoofForgePreview
{
    static void Capture(Camera camera,string path)
    {
        var rt=new RenderTexture(1600,1100,24);var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,1100),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Rail(Vector3 a,Vector3 b,Material mat)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder);o.name="Prototype guardrail";
        o.transform.position=(a+b)/2;o.transform.up=(b-a).normalized;o.transform.localScale=new Vector3(.028f,(b-a).magnitude/2,.028f);
        o.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
    }
    public static void RunBatch()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camper=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperWrapGenerated/FinishCandidates/d976037983224f3e98a789d95227988a/rebirthCamper80s.prefab"));
        var forge=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHRoofPrototype/Forge_v8/roof-forge.fbx"));
        if(!camper || !forge)throw new InvalidDataException("Preview source missing");
        foreach(var renderer in forge.GetComponentsInChildren<Renderer>())
        {
            var material=new Material(Shader.Find("Standard"));
            bool masonry=renderer.name.Contains("dome") || renderer.name.Contains("floor");
            material.color=masonry?new Color(.28f,.25f,.21f):new Color(.12f,.11f,.09f);
            material.SetFloat("_Metallic",masonry?0:.65f);material.SetFloat("_Glossiness",masonry?.08f:.2f);
            if(renderer.name.Contains("dome"))
            {
                material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/REBIRTHRoofPrototype/Forge_v8/forge-masonry-albedo.png");material.color=Color.white;
                const string normalPath="Assets/REBIRTHRoofPrototype/Forge_v8/forge-masonry-normal.png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(normalPath);importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));material.EnableKeyword("_NORMALMAP"); material.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/REBIRTHRoofPrototype/Forge_v8/forge-masonry-ao.png"));
            }
            if(renderer.name.Contains("leather"))material.color=new Color(.16f,.08f,.035f); renderer.sharedMaterial=material;
        }
        forge.transform.position=new Vector3(-.35f,2.547f,-.1f);forge.transform.rotation=Quaternion.identity;
        var metal=new Material(Shader.Find("Standard"));metal.color=new Color(.12f,.10f,.08f);metal.SetFloat("_Metallic",.6f);
        const string sceneFolder="Assets/REBIRTHRoofPrototype/Review_v2";
        if(Directory.Exists(sceneFolder))throw new InvalidOperationException("Preserve review scene; choose a new version");
        Directory.CreateDirectory(sceneFolder);AssetDatabase.Refresh();
        int materialIndex=0;
        foreach(var renderer in forge.GetComponentsInChildren<Renderer>())
            AssetDatabase.CreateAsset(renderer.sharedMaterial,sceneFolder+"/forge-material-"+(materialIndex++)+".mat");
        AssetDatabase.CreateAsset(metal,sceneFolder+"/guardrails.mat");
        // Match measured roof: rear section is lower than the forward ribbed area.
        var corners=new[]{new Vector3(-.82f,2.72f,.55f),new Vector3(.82f,2.72f,.55f),new Vector3(.82f,2.65f,-3.22f),new Vector3(-.82f,2.65f,-3.22f)};
        for(int i=0;i<4;i++){Rail(corners[i],corners[(i+1)%4],metal);Rail(corners[i]-Vector3.up*.17f,corners[i],metal);}
        foreach(float x in new[]{-.82f,.82f})foreach(float z in new[]{-.4f,-1.3f,-2.2f})
        {
            float top=Mathf.Lerp(2.72f,2.65f,(.55f-z)/3.77f);
            Rail(new Vector3(x,2.547f,z),new Vector3(x,top,z),metal);
        }
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
        var sun=new GameObject("Preview light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(35,140,0);
        var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.20f,.22f);camera.fieldOfView=38;
        string output="E:/_Haven/shared/REBIRTHTemp/CamperRoofPrototype_20261012/ForgePreview_v10";
        if(Directory.Exists(output))throw new InvalidOperationException("Preserve existing preview evidence");
        Directory.CreateDirectory(output);
        camera.transform.position=new Vector3(-5,7,5);camera.transform.LookAt(new Vector3(0,1.8f,-1));Capture(camera,output+"/roof-layout.png");
        camera.transform.position=new Vector3(-1.7f,3.8f,1.5f);camera.transform.LookAt(new Vector3(-.35f,2.95f,-.1f));Capture(camera,output+"/forge-close.png");
        File.WriteAllText(output+"/unity-preview.txt","Geometry prototype rendered on actual approved camper; no station integration, UV textures or native style acceptance yet.");
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),sceneFolder+"/CamperRoofForgeReview.unity");
    }
}





