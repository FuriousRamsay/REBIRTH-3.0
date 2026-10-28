using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Render the EXACT installed prefab bundle read-only. No source prefab assumptions.
public static class RebirthWrapCollectionReview
{
    sealed class ContactSheet
    {
        internal Texture2D texture;
        internal readonly List<string> ids=new List<string>();
        internal int page;
    }
    static readonly Dictionary<string,ContactSheet> contacts=new Dictionary<string,ContactSheet>();
    static void FlushContact(string directory,string view,ContactSheet sheet)
    {
        if(sheet.texture==null)return;
        sheet.texture.Apply();
        string stem=Path.Combine(directory,"contact-"+view+"-"+(++sheet.page).ToString("D2"));
        File.WriteAllBytes(stem+".png",sheet.texture.EncodeToPNG());
        File.WriteAllLines(stem+".txt",sheet.ids);
        Object.DestroyImmediate(sheet.texture);sheet.texture=null;sheet.ids.Clear();
    }
    static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);if(i<0||i+1>=args.Length)throw new Exception(key);return args[i+1];}
    static void Render(string file,Vector3 position,Vector3 target,float size)
    {
        var camera=new GameObject("Wrap review camera").AddComponent<Camera>();
        camera.transform.position=position; camera.transform.LookAt(target);
        camera.orthographic=true;camera.orthographicSize=size;camera.nearClipPlane=.01f;camera.farClipPlane=50;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.16f);
        var rt=new RenderTexture(1600,1100,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();
        File.WriteAllBytes(file,texture.EncodeToPNG());
        string view=Path.GetFileNameWithoutExtension(file).Split('-').Last();
        if(new[]{"quarter","side","right","rear","hood"}.Contains(view))
        {
            // Render a second native camera view directly into a compact review
            // page. No resampling or alteration of the authored artwork.
            ContactSheet sheet;
            if(!contacts.TryGetValue(view,out sheet))contacts[view]=sheet=new ContactSheet();
            if(sheet.texture==null)
            {
                sheet.texture=new Texture2D(1600,825,TextureFormat.RGB24,false);
                sheet.texture.SetPixels(Enumerable.Repeat(new Color(.12f,.14f,.16f),1600*825).ToArray());
            }
            var tile=new RenderTexture(400,275,24){antiAliasing=4};
            camera.targetTexture=tile;camera.Render();RenderTexture.active=tile;
            int index=sheet.ids.Count;
            sheet.texture.ReadPixels(new Rect(0,0,400,275),400*(index%4),275*(2-index/4),false);
            sheet.ids.Add(Path.GetFileName(file).Replace("-"+view+".png",""));
            RenderTexture.active=rt;camera.targetTexture=rt;tile.Release();Object.DestroyImmediate(tile);
            if(sheet.ids.Count==12)FlushContact(Path.GetDirectoryName(file),view,sheet);
        }
        RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(texture);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    public static void Run()
    {
        string output=Arg("-wrapReviewOutput");Directory.CreateDirectory(output);
        contacts.Clear();
        PlayerSettings.colorSpace=ColorSpace.Linear;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var source=AssetBundle.LoadFromFile(Arg("-wrapVehicleBundle"));
        ModManager.Root=Arg("-wrapRuntimeRoot");
        if(!source)throw new Exception("Vehicle bundle open failed.");
        var prefab=source.LoadAsset<GameObject>("rebirthCamper80s");
        if(!prefab)throw new Exception("Current installed prefab missing.");
        var shaders=prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Select(m=>m.shader.name).Distinct().OrderBy(s=>s).ToArray();
        File.WriteAllText(Path.Combine(output,"installed-shaders.txt"),string.Join("\n",shaders));
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
        foreach(var angle in new[]{new Vector3(45,-40,0),new Vector3(25,130,0)}){var l=new GameObject("Studio light").AddComponent<Light>();l.type=LightType.Directional;l.intensity=.8f;l.transform.rotation=Quaternion.Euler(angle);}
        foreach(string id in Arg("-wrapIds").Split(','))
        {
            var root=Object.Instantiate(prefab);root.name="WrapReview_"+id;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="REBIRTHCamperBodyModule")t.gameObject.SetActive(true);
                if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment","REBIRTHCampStairVisuals","REBIRTHCampWalkInColliders","REBIRTHPickupOnlyPaint","REBIRTHPickupOnlyBodywork"}.Contains(t.name))t.gameObject.SetActive(false);
            }
            var originalMaterials=root.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r,r=>r.sharedMaterials);
            var model=root.transform.Find("GameObject");if(!model)throw new Exception("Native model root missing.");
            var actor=new EntityVehicle{ModelTransform=model};
            RebirthCamperPattern.Value=id;RebirthCamperPatternAppearance.Apply(actor);
            int changed=root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Count(m=>m&&m.name.EndsWith("_Wrap_"+id));
            if(changed==0)throw new Exception("No qualified paint materials.");
            var selected=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m&&m.name.EndsWith("_Wrap_"+id))).ToArray();
            var selectedMaterials=selected.ToDictionary(r=>r,r=>r.sharedMaterials);
            foreach(var renderer in selected)
            {
                var block=new MaterialPropertyBlock();block.SetFloat("_WrapReviewSentinel",.37f);block.SetFloat("_PatternEnabled",0);renderer.SetPropertyBlock(block);
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)if(materials[i]&&materials[i].HasProperty("_PatternEnabled"))renderer.SetPropertyBlock(block,i);
            }
            RebirthCamperPatternAppearance.Apply(actor);
            foreach(var renderer in selected)
            {
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                if(block.GetFloat("_WrapReviewSentinel")!=.37f||block.GetFloat("_PatternEnabled")!=1)throw new Exception("Renderer property restoration failed.");
                var current=renderer.sharedMaterials;var expected=selectedMaterials[renderer];
                for(int i=0;i<current.Length;i++)
                {
                    if(!ReferenceEquals(current[i],expected[i]))throw new Exception("Stable selection recloned material.");
                    if(current[i]&&current[i].HasProperty("_PatternEnabled"))
                    {
                        renderer.GetPropertyBlock(block,i);
                        if(block.GetFloat("_WrapReviewSentinel")!=.37f||block.GetFloat("_PatternEnabled")!=1)throw new Exception("Indexed property restoration failed.");
                    }
                }
            }
            Render(Path.Combine(output,id+"-quarter.png"),new Vector3(-6,3.2f,6),new Vector3(0,1.55f,-.6f),2.7f);
            Render(Path.Combine(output,id+"-side.png"),new Vector3(-9,1.9f,-.6f),new Vector3(0,1.6f,-.6f),2.7f);
            Render(Path.Combine(output,id+"-right.png"),new Vector3(9,1.9f,-.6f),new Vector3(0,1.6f,-.6f),2.7f);
            Render(Path.Combine(output,id+"-rear.png"),new Vector3(0,2.1f,-8),new Vector3(0,1.7f,-1),2.1f);
            Render(Path.Combine(output,id+"-hood.png"),new Vector3(-1.4f,3.1f,3),new Vector3(0,1.20f,1.4f),1.3f);
            var probe=Object.Instantiate(prefab);var second=new EntityVehicle{ModelTransform=probe.transform.Find("GameObject")};
            RebirthCamperPatternAppearance.Apply(second);
            if(RebirthCamperWrapAppearance.CachedAtlasCount!=1)throw new Exception("Identical wraps should share one atlas.");
            RebirthCamperPattern.Value="none";RebirthCamperPatternAppearance.Apply(actor);
            if(RebirthCamperWrapAppearance.CachedAtlasCount!=1)throw new Exception("Removing one wrap unloaded another camper's artwork.");
            foreach(var pair in originalMaterials)
            {
                var current=pair.Key.sharedMaterials;
                if(current.Length!=pair.Value.Length)throw new Exception("Material count changed.");
                for(int i=0;i<current.Length;i++)if(!ReferenceEquals(current[i],pair.Value[i]))throw new Exception("Original material not restored: "+pair.Key.name);
            }
            RebirthCamperPatternAppearance.Apply(second);
            if(RebirthCamperWrapAppearance.CachedAtlasCount!=0)throw new Exception("Unused artwork retained in cache.");
            Object.DestroyImmediate(probe);
            RebirthCamperPattern.Value=id;RebirthCamperPatternAppearance.Apply(actor);
            ModEvents.WorldShuttingDown.Shutdown();
            RebirthCamperPatternAppearance.Apply(actor);
            if(RebirthCamperWrapAppearance.CachedAtlasCount!=1)throw new Exception("World reset failed to rebind artwork.");
            RebirthCamperPattern.Value="disco";RebirthCamperPatternAppearance.Apply(actor);
            if(RebirthCamperWrapAppearance.CachedAtlasCount!=0)throw new Exception("Legacy Disco retained custom artwork cache.");
            foreach(var renderer in selected)
            {
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                if(block.GetFloat("_PatternEnabled")!=1)throw new Exception("Legacy Disco did not resume.");
                if(renderer.sharedMaterials.Any(m=>m&&m.name.EndsWith("_Wrap_"+id)))throw new Exception("Legacy Disco retained collection clone.");
            }
            Object.DestroyImmediate(root);
            Debug.Log("WRAP_MESH_REVIEW_RENDERED "+id+" paintMaterials="+changed);
        }
        foreach(var pair in contacts)FlushContact(output,pair.Key,pair.Value);
        ModEvents.WorldShuttingDown.Shutdown();source.Unload(true);
    }
}
