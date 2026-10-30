using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Stages and reopens a candidate bundle. Deployment remains a separate host step.
public static class RebirthCamperTurretRelease
{
    const string Prefab=RebirthCamperTurretProduction.Folder+"/WithLods/rebirthCamper80s.prefab";
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output";
    static string PathOf(Transform t,Transform root)
    {var parts=new List<string>();while(t&&t!=root){parts.Add(t.name);t=t.parent;}parts.Reverse();return string.Join("/",parts);}
    static string[] Colliders(GameObject root)=>root.GetComponentsInChildren<Collider>(true).Select(c=>PathOf(c.transform,root.transform)+"|"+c.GetType().Name+"|"+c.enabled+"|"+c.isTrigger+"|"+c.gameObject.layer+"|"+(c is MeshCollider mesh?AssetDatabase.GetAssetPath(mesh.sharedMesh)+":"+mesh.sharedMesh.name+":"+mesh.convex:c is BoxCollider box?box.center+":"+box.size:c is WheelCollider wheel?wheel.center+":"+wheel.radius+":"+wheel.suspensionDistance:"other")).OrderBy(x=>x).ToArray();
    static void Verify(GameObject root,List<string> lines)
    {
        var all=root.GetComponentsInChildren<Transform>(true);
        for(int i=0;i<4;i++)
        {
            var slot=all.Single(t=>t.name=="REBIRTHCamperTurretSlot"+i);if(slot.gameObject.activeSelf)throw new Exception("Installed-only slot default violated");
            foreach(string marker in new[]{"TurretYaw","TurretPitch","TurretMuzzle","TurretPitchLimits","TurretYawLimits"})if(slot.GetComponentsInChildren<Transform>(true).Count(t=>t.name==marker)!=1)throw new Exception("Missing/duplicate rig marker: "+i+" "+marker);
            var lod=slot.GetComponentsInChildren<LODGroup>(true).Single(g=>g.GetLODs().Length==3&&g.GetLODs()[0].renderers.Any(r=>r.name=="PitchBody_LOD0"));
            var counts=lod.GetLODs().Select(level=>level.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();
            if(counts[0]<39000||counts[0]>41000||counts[1]<15000||counts[1]>17000||counts[2]<5500||counts[2]>6500)throw new Exception("Turret LOD budget changed");
            lines.Add("Corner "+i+" rig and budgets="+string.Join(",",counts));
        }
        var baseline=AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperTurretProduction.Folder+"/rebirthCamper80s.prefab");
        if(!Colliders(baseline).SequenceEqual(Colliders(root)))throw new Exception("LOD import changed native collider hierarchy/configuration");
        if(root.GetComponentsInChildren<WheelCollider>(true).Length!=4)throw new Exception("Four wheels required");
        var robotMaterials=root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.shader.name=="REBIRTH/Robot Weathered Steel").Distinct().ToArray();
        if(robotMaterials.Length==0)throw new Exception("Approved weathered turret material missing");
        foreach(var material in robotMaterials)
        {
            if(!material.HasProperty("_TurretActive")||!material.HasProperty("_LensLocalCenter"))throw new Exception("Current selective lens shader missing");
            foreach(var check in new[]{Tuple.Create("_MainTex",2048),Tuple.Create("_BumpMap",1024),Tuple.Create("_MetallicGlossMap",256)})
            {
                var texture=material.GetTexture(check.Item1) as Texture2D;var importer=texture?AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter:null;
                if(!texture||texture.width!=check.Item2||texture.height!=check.Item2||importer==null||!importer.crunchedCompression)throw new Exception("Texture dimensions/Crunch mismatch: "+check.Item1);
                lines.Add(check.Item1+" "+texture.width+"x"+texture.height+" "+texture.format+" Crunch="+importer.crunchedCompression);
            }
        }
        lines.Add("Native collider signatures retained; staged default-hidden installation slots; authoring validation only.");
    }
    public static void Run()
    {
        RebirthCamperStationLodImport.Run();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var root=Object.Instantiate(source);var lines=new List<string>();
        try{Verify(root,lines);Review(root,lines);}
        finally{Object.DestroyImmediate(root);}
        string bundles=Output+"/ProductionBundles";Directory.CreateDirectory(bundles);
        var build=new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Prefab},addressableNames=new[]{"rebirthCamper80s"}};
        if(!RebirthAssetBundlePackaging.BuildAssetBundles(bundles,new[]{build},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Candidate bundle build failed");
        string path=bundles+"/rebirth_camper80s.unity3d";
        if(!BuildPipeline.GetCRCForAssetBundle(path,out uint crc))throw new Exception("Bundle CRC unavailable");
        var bundle=AssetBundle.LoadFromFile(path,crc);if(!bundle)throw new Exception("Candidate bundle cannot reopen");
        try
        {
            var prefab=bundle.LoadAsset<GameObject>("rebirthCamper80s");if(!prefab)throw new Exception("Native prefab address missing");
            if(prefab.GetComponentsInChildren<WheelCollider>(true).Length!=4||prefab.GetComponentsInChildren<LODGroup>(true).Length<=4)throw new Exception("Reopened candidate lost native wheels/extended LODs");
            lines.Add("CRC reopen passed="+crc+" LOD groups="+prefab.GetComponentsInChildren<LODGroup>(true).Length);
        }
        finally{bundle.Unload(true);}
        File.WriteAllLines(Output+"/production-release-verification.txt",lines);
    }
    static void Review(GameObject root,List<string> lines)
    {
        RebirthCamperCampIntegration.SetMode(root,true);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("REBIRTHCamperTurretSlot")||t.name=="REBIRTHModArmor"||t.name=="REBIRTHModArmorDoor"||t.name=="REBIRTHModPlow")t.gameObject.SetActive(true);
        RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.23f,.23f,.23f);
        var light=new GameObject("Production review key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,135,0);
        try
        {
            Render(new Vector3(0,10,-1.2f),new Vector3(0,2,-1.2f),Vector3.forward,2.85f,"production-top");
            Render(new Vector3(-3.8f,4.3f,-5.5f),new Vector3(-.8f,2.8f,-3.3f),Vector3.up,.65f,"production-rear-close");
            var block=new MaterialPropertyBlock();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))if(renderer.sharedMaterials.Any(m=>m&&m.HasProperty("_TurretActive"))){renderer.GetPropertyBlock(block);block.SetFloat("_TurretActive",1);renderer.SetPropertyBlock(block);}
            Render(new Vector3(-3.8f,4.3f,-5.5f),new Vector3(-.8f,2.8f,-3.3f),Vector3.up,.65f,"production-rear-active-eye");
            lines.Add("Actual candidate top/rear and active-eye previews captured; inspect images before deployment.");
        }
        finally{Object.DestroyImmediate(light.gameObject);}
    }
    static void Render(Vector3 eye,Vector3 target,Vector3 up,float size,string label)
    {
        var camera=new GameObject("Production review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=size;camera.transform.position=eye;camera.transform.rotation=Quaternion.LookRotation(target-eye,up);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.15f,.16f);
        var rt=new RenderTexture(1400,1400,24){antiAliasing=4};var image=new Texture2D(1400,1400,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,1400),0,0);image.Apply();File.WriteAllBytes(Output+"/"+label+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);}
    }
}
