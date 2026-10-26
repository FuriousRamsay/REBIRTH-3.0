using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RebirthForgeMaterialReview
{
    const string AssetRoot="Assets/REBIRTHStationAddons/ForgeCandidate_20261013_v1";
    const string Output="E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/UnityMaterialReview_v3";
    public static void RunBatch()
    {
        Directory.CreateDirectory(Output);
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var importer=(ModelImporter)AssetImporter.GetAtPath(AssetRoot+"/ForgeCompact.fbx");
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.importNormals=ModelImporterNormals.Import;
            importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot+"/ForgeCompact.fbx");
            if(source==null)throw new Exception("FBX import missing");
            var root=UnityEngine.Object.Instantiate(source);
            foreach(string group in new[]{"masonry","metal","organic"})
            {
                string folder=AssetRoot+"/"+group;
                var albedo=Load(folder+"/forge-basecolor-2k.png",true,false);
                var normal=Load(folder+"/forge-normal-2k.png",false,true);
                var emission=Load(folder+"/forge-emission-2k.png",true,false);
                var rough=Load(folder+"/forge-roughness-2k.png",false,false);
                var metal=Load(folder+"/forge-metallic-2k.png",false,false);
                var rp=rough.GetPixels();var mp=metal.GetPixels();
                var packed=new Texture2D(2048,2048,TextureFormat.RGBA32,false,true);
                for(int i=0;i<mp.Length;i++)mp[i]=new Color(mp[i].r,0,0,1-rp[i].r);
                packed.SetPixels(mp);packed.Apply();
                string packedPath=folder+"/metallic-smoothness-2k.png";
                File.WriteAllBytes(packedPath,packed.EncodeToPNG());AssetDatabase.ImportAsset(packedPath);
                var gloss=Load(packedPath,false,false);
                var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Forge.mat");
                bool newMaterial=mat==null;
                if(newMaterial)mat=new Material(Shader.Find("Standard"));mat.name="Forge_"+group;
                mat.SetTexture("_MainTex",albedo);mat.SetTexture("_BumpMap",normal);
                mat.SetTexture("_MetallicGlossMap",gloss);mat.SetFloat("_GlossMapScale",1);
                mat.SetTexture("_EmissionMap",emission);mat.SetColor("_EmissionColor",Color.white*2.8f);
                mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICGLOSSMAP");mat.EnableKeyword("_EMISSION");
                if(newMaterial)AssetDatabase.CreateAsset(mat,folder+"/Forge.mat");else EditorUtility.SetDirty(mat);
                bool found=false;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                    if(renderer.name=="Forge_"+group){var slots=renderer.sharedMaterials;for(int s=0;s<slots.Length;s++)slots[s]=mat;renderer.sharedMaterials=slots;found=true;}
                if(!found)throw new Exception("Missing renderer "+group);
            }
            PrefabUtility.SaveAsPrefabAsset(root,AssetRoot+"/ForgeCompact.prefab");
            var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.55f,.55f,.55f);RenderSettings.fog=false;
            var lamp=new GameObject("PreviewKey").AddComponent<Light>();lamp.type=LightType.Directional;
            lamp.intensity=1.15f;lamp.transform.rotation=Quaternion.Euler(35,-35,0);lamp.shadows=LightShadows.Soft;
            var fill=new GameObject("PreviewFill").AddComponent<Light>();fill.type=LightType.Directional;
            fill.intensity=.7f;fill.transform.rotation=Quaternion.Euler(30,145,0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.position=new Vector3(0,bounds.min.y-.01f,0);
            floor.transform.localScale=Vector3.one*3;var ground=new Material(Shader.Find("Standard"));
            ground.color=new Color(.12f,.12f,.12f);ground.SetFloat("_Glossiness",0);floor.GetComponent<Renderer>().sharedMaterial=ground;
            var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.055f,.055f,.055f);camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.fieldOfView=38;
            float radius=bounds.size.magnitude*1.2f;
            int index=0;
            foreach(var direction in new[]{new Vector3(-1,.65f,-2),new Vector3(1,.65f,-2),new Vector3(-1,.65f,2),new Vector3(1,.65f,2)})
            {
                camera.transform.position=bounds.center+direction.normalized*radius;camera.transform.LookAt(bounds.center);
                var rt=new RenderTexture(1448,1086,24);var image=new Texture2D(1448,1086,TextureFormat.RGB24,false);
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1448,1086),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,"quarter-"+(++index)+".png"),image.EncodeToPNG());
                camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Output,"receipt.txt"),"UNITY_IMPORT_RENDER_COMPLETE_VISUAL_REVIEW_PENDING\nColorSpace="+QualitySettings.activeColorSpace+"\nBounds="+bounds+"\nRenderers="+renderers.Length+"\nNo bundle or installation.");
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Output,"failure.txt"),e.ToString());throw;}
    }
    static Texture2D Load(string path,bool srgb,bool normal)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=srgb;importer.maxTextureSize=2048;importer.isReadable=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture.width!=2048||texture.height!=2048)throw new Exception("Wrong map dimensions: "+path);
        return texture;
    }
}
