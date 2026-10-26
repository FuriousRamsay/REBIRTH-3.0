using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Render only the actual production turret; no mesh/material asset mutation.
public static class RebirthRobotStandaloneReview
{
    public static void Run()
    {
        const string output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperTurretProduction_20261020/WithLods/rebirthCamper80s.prefab");
        if(!prefab)throw new Exception("Current production prefab required");
        var model=prefab.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Optimized robot turret 1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(model.gameObject);root.SetActive(true);root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;
        var lod=root.GetComponentsInChildren<LODGroup>(true).Single(g=>g.GetLODs().Length==3&&g.GetLODs()[0].renderers.Any(r=>r.name=="PitchBody_LOD0"));
        lod.ForceLOD(0);
        var renderers=lod.GetLODs()[0].renderers;
        int triangles=renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
        if(triangles!=39999)throw new Exception("Production LOD0 budget changed");
        var block=new MaterialPropertyBlock();
        foreach(var r in renderers)
        {
            if(!r.sharedMaterials.Any(m=>m&&m.shader.name=="REBIRTH/Robot Weathered Steel"))throw new Exception("Approved brown production material required");
            r.GetPropertyBlock(block);block.SetFloat("_TurretActive",1);r.SetPropertyBlock(block);
        }
        var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
        RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.29f,.29f,.29f);
        Key("Studio key",new Vector3(35,-35,0),1.5f,new Color(1,.95f,.88f));
        Key("Studio fill",new Vector3(25,145,0),.7f,new Color(.75f,.83f,1));
        Key("Studio rim",new Vector3(145,0,0),.6f,Color.white);
        var camera=new GameObject("Standalone production turret camera").AddComponent<Camera>();
        camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.085f,.10f);camera.cullingMask=-1;
        Render(camera,bounds,new Vector3(0,.08f,1),output+"/turret-front.png");
        Render(camera,bounds,new Vector3(0,.08f,-1),output+"/turret-back.png");
        Render(camera,bounds,new Vector3(-1,.08f,0),output+"/turret-left.png");
        Render(camera,bounds,new Vector3(1,.08f,0),output+"/turret-right.png");
        var detail=new Bounds(bounds.center+new Vector3(0,bounds.size.y*.19f,0),bounds.size*.60f);
        Render(camera,detail,new Vector3(-2.2f,1.0f,3.2f),output+"/turret-body-detail.png");
        Render(camera,detail,new Vector3(3,0.7f,-2),output+"/turret-rear-pivot-detail.png");
        File.WriteAllText(output+"/turret-multiple-angles.txt","Actual production turret, LOD0="+triangles+" triangles; unchanged production materials and geometry.\n");
        Object.DestroyImmediate(root);
    }
    static void Render(Camera camera,Bounds bounds,Vector3 direction,string path)
    {
        camera.transform.position=bounds.center+direction.normalized*bounds.size.magnitude*4;
        camera.transform.LookAt(bounds.center);camera.nearClipPlane=.001f;camera.farClipPlane=100;
        float halfWidth=Project(bounds.extents,camera.transform.right),halfHeight=Project(bounds.extents,camera.transform.up);
        camera.aspect=1600f/1400;camera.orthographicSize=Mathf.Max(halfHeight,halfWidth/camera.aspect)*1.06f;
        var target=new RenderTexture(1600,1400,24){antiAliasing=4};var pixels=new Texture2D(1600,1400,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,1400),0,0);pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        finally{RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(pixels);Object.DestroyImmediate(target);}
    }    static float Project(Vector3 e,Vector3 axis)=>Mathf.Abs(axis.x)*e.x+Mathf.Abs(axis.y)*e.y+Mathf.Abs(axis.z)*e.z;
    static void Key(string name,Vector3 rotation,float intensity,Color color)
    {var light=new GameObject(name).AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(rotation);light.intensity=intensity;light.color=color;}
}

