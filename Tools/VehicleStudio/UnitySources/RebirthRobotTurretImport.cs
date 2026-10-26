using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Object=UnityEngine.Object;

// Staged importer only. Does not overwrite the installed camper bundle.
public static class RebirthRobotTurretImport
{
    public const string Folder="Assets/REBIRTHRobotTurret_20261019";
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output";
    [Serializable] sealed class MeshData
    {
        public string name,parent;
        public Vector3[] vertices,normals;
        public Vector2[] uv;
        public int[] surfaceTriangles,capTriangles;
    }
    [Serializable] sealed class RigData
    {
        public Vector3 pitchPivot,muzzlePosition;
        public MeshData[] meshes;
    }
    static Transform Child(Transform owner,string name,Vector3 position)
    {
        var t=new GameObject(name).transform;t.SetParent(owner,false);t.localPosition=position;return t;
    }
    static void SaveAsset(Object value,string path)
    {
        var existing=AssetDatabase.LoadAssetAtPath<Object>(path);
        if(existing){EditorUtility.CopySerialized(value,existing);Object.DestroyImmediate(value);}
        else AssetDatabase.CreateAsset(value,path);
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        foreach(var name in new[]{"Robot_Albedo","Robot_Normal","Robot_Roughness","Robot_Metallic","Robot_MetallicSmoothness"})
            File.Copy(Output+"/"+name+".png",Folder+"/"+name+".png",true);
        AssetDatabase.Refresh();var report=new List<string>();
        foreach(string file in Directory.GetFiles(Folder,"*.png"))
        {
            string path=file.Replace('\\','/');bool albedo=path.Contains("Albedo"),normal=path.Contains("Normal"),packed=path.Contains("MetallicSmoothness");
            int size=albedo?2048:normal?1024:256;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=albedo;importer.maxTextureSize=size;importer.mipmapEnabled=true;importer.streamingMipmaps=true;importer.isReadable=false;
            importer.alphaSource=packed?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            importer.textureCompression=TextureImporterCompression.Compressed;importer.crunchedCompression=true;importer.compressionQuality=90;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=size,
                format=normal||packed?TextureImporterFormat.DXT5Crunched:TextureImporterFormat.DXT1Crunched,crunchedCompression=true,compressionQuality=90});
            importer.SaveAndReimport();var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture.width!=size||texture.height!=size||(texture.format!=TextureFormat.DXT1Crunched&&texture.format!=TextureFormat.DXT5Crunched))
                throw new Exception("Texture size or Crunch failed: "+path);
            report.Add(path+" "+texture.width+"x"+texture.height+" "+texture.format);
        }
        var material=new Material(Shader.Find("Standard")){name="Robot source finish"};
        material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Robot_Albedo.png");
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Robot_Normal.png"));material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Robot_MetallicSmoothness.png"));material.EnableKeyword("_METALLICGLOSSMAP");material.SetFloat("_GlossMapScale",1);
        SaveAsset(material,Folder+"/Robot.mat");material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Robot.mat");
        var cap=new Material(Shader.Find("Standard")){name="Concealed bearing steel",color=new Color(.09f,.07f,.05f)};cap.SetFloat("_Metallic",.8f);cap.SetFloat("_Glossiness",.5f);
        SaveAsset(cap,Folder+"/BearingSteel.mat");cap=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/BearingSteel.mat");
        var data=JsonUtility.FromJson<RigData>(File.ReadAllText(Output+"/RobotTurret_UnityMeshes.json"));
        if(data.meshes.Length!=9||data.pitchPivot.y<.5f||data.muzzlePosition.z<.6f)throw new Exception("Unexpected rig coordinates");
        var root=new GameObject("RustyRobotTurret");
        try
        {
            var yaw=Child(root.transform,"TurretYaw",Vector3.zero);var pitch=Child(yaw,"TurretPitch",data.pitchPivot);
            Child(pitch,"TurretMuzzle",data.muzzlePosition);
            // New model's measured range; runtime integration must consume this
            // marker before this prefab can replace the old game bundle.
            Child(root.transform,"TurretPitchLimits",new Vector3(-75,29.5f,90));
            var groups=new List<Renderer>[] {new List<Renderer>(),new List<Renderer>(),new List<Renderer>()};
            var counts=new int[3];
            foreach(var entry in data.meshes)
            {
                int level=int.Parse(entry.name.Substring(entry.name.Length-1));
                var mesh=new Mesh{name=entry.name,indexFormat=IndexFormat.UInt32};
                mesh.vertices=entry.vertices;mesh.normals=entry.normals;mesh.uv=entry.uv;mesh.subMeshCount=2;
                mesh.SetTriangles(entry.surfaceTriangles,0);mesh.SetTriangles(entry.capTriangles,1);mesh.RecalculateBounds();mesh.RecalculateTangents();
                MeshUtility.Optimize(mesh);SaveAsset(mesh,Folder+"/"+entry.name+".asset");
                var t=Child(entry.parent=="TurretPitch"?pitch:entry.parent=="TurretYaw"?yaw:root.transform,entry.name,Vector3.zero);
                t.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+entry.name+".asset");
                var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterials=new[]{material,cap};groups[level].Add(r);
                counts[level]+=(entry.surfaceTriangles.Length+entry.capTriangles.Length)/3;
            }
            int[] budgets={40000,16000,6000};for(int i=0;i<3;i++)
            {
                if(groups[i].Count!=3||counts[i]>budgets[i])throw new Exception("LOD budget/group mismatch");
                report.Add("LOD"+i+" triangles="+counts[i]+" renderers="+groups[i].Count);
            }
            // Screen-height fractions retain the 40k mesh during close inspection.
            var lod=root.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.CrossFade;lod.animateCrossFading=true;
            lod.SetLODs(new[]{new LOD(.08f,groups[0].ToArray()),new LOD(.025f,groups[1].ToArray()),new LOD(.003f,groups[2].ToArray())});lod.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/RustyRobotTurret.prefab");AssetDatabase.SaveAssets();
            AssetDatabase.ExportPackage(Folder,Output+"/RustyRobotTurret.unitypackage",ExportPackageOptions.Recurse);
            report.Add("Standalone asset only; camper replacement and live range/LOD validation remain separate steps.");
            File.WriteAllLines(Output+"/unity-import-verification.txt",report);
        }
        finally{Object.DestroyImmediate(root);}
    }
    public static void StageCamper()
    {
        Run();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperMountFinish_20261019/rebirthCamper80s.prefab");
        var turret=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/RustyRobotTurret.prefab");
        if(!source||!turret)throw new Exception("Approved camper or optimized turret asset missing");
        var root=Object.Instantiate(source);root.name="rebirthCamper80s";
        try
        {
            for(int i=0;i<4;i++)
            {
                var slot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot"+i);
                var old=slot.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("Optimized Tripo turret"));
                var oldRenderers=old.GetComponent<LODGroup>().GetLODs()[0].renderers;
                var bounds=oldRenderers[0].bounds;foreach(var r in oldRenderers.Skip(1))bounds.Encapsulate(r.bounds);
                var platform=slot.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="Circular fixed turret platform").bounds;
                var rotation=old.rotation;float scale=bounds.size.y/.8364258f;
                var replacement=Object.Instantiate(turret,slot);replacement.name="Optimized robot turret "+(i+1);
                replacement.transform.position=new Vector3(platform.center.x,platform.max.y,platform.center.z);
                replacement.transform.rotation=rotation;
                var parentScale=slot.lossyScale;
                replacement.transform.localScale=new Vector3(scale/parentScale.x,scale/parentScale.y,scale/parentScale.z);
                Object.DestroyImmediate(old.gameObject);slot.gameObject.SetActive(false);
            }
            if(!root.transform.Find("GameObject/M/VisualBody")||!root.transform.Find("Physics")||root.GetComponentsInChildren<WheelCollider>(true).Length!=4)
                throw new Exception("Camper native hierarchy/physics changed");
            string path=Folder+"/rebirthCamper80s.prefab";
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
            AssetDatabase.ExportPackage(path,Output+"/CamperRobotTurret_Staged.unitypackage",ExportPackageOptions.IncludeDependencies|ExportPackageOptions.Recurse);
            File.WriteAllText(Output+"/camper-staging.txt","Four replacement turrets, original world height retained, existing armor-material mounts preserved. Default-hidden slots. Requires rendered full-camper sweep and Game Bridge checks before deployment.");
        }
        finally{Object.DestroyImmediate(root);}
    }
}
