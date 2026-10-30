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
        camera.transform.position=position; camera.transform.LookAt(target, Mathf.Abs((target-position).normalized.y)>.99f ? Vector3.forward : Vector3.up);
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
        bool shadowReview=Environment.GetCommandLineArgs().Contains("-wrapShadowReview");
        foreach(var angle in new[]{new Vector3(45,-40,0),new Vector3(25,130,0)}){var l=new GameObject("Studio light").AddComponent<Light>();l.type=LightType.Directional;l.intensity=.8f;l.transform.rotation=Quaternion.Euler(angle);l.shadows=shadowReview?LightShadows.Hard:LightShadows.None;}
        if(shadowReview){QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=50;File.WriteAllText(Path.Combine(output,"shadow-review.txt"),"Two directional lights with hard shadows enabled; native surface shader forward shadow variant exercised by these renders.");}
        if(Environment.GetCommandLineArgs().Contains("-wrapSwitchRegression")){
            RebirthWrapSwitchRegression.Run(prefab,output);source.Unload(true);return;
        }
        foreach(string id in Arg("-wrapIds").Split(','))
        {
            bool pickupReview=Environment.GetCommandLineArgs().Contains("-wrapPickupReview");
            var root=Object.Instantiate(prefab);root.name="WrapReview_"+id;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="REBIRTHCamperBodyModule")t.gameObject.SetActive(!pickupReview);
                if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment","REBIRTHCampStairVisuals","REBIRTHCampWalkInColliders","REBIRTHPickupOnlyPaint","REBIRTHPickupOnlyBodywork"}.Contains(t.name))t.gameObject.SetActive(false);
                if(pickupReview&&(t.name=="REBIRTHPickupOnlyPaint"||t.name=="REBIRTHPickupOnlyBodywork"))t.gameObject.SetActive(true);
            }
            var originalMaterials=root.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r,r=>r.sharedMaterials);
            var nativeMeshes=root.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f=>f,f=>f.sharedMesh);
            var nativePose=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.localToWorldMatrix);
            File.WriteAllLines(Path.Combine(output,id+"-materials.tsv"),root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials.Where(m=>m).Select(m=>r.name+"\t"+m.name+"\t"+m.shader.name+"\t"+(m.HasProperty("_IsCamper")?m.GetFloat("_IsCamper").ToString():"NA")+"\t"+r.bounds.ToString())));
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
            bool rawDiagnostic=Environment.GetCommandLineArgs().Contains("-wrapDiagnosticRawDirectory");
            if(RebirthCamperWrapAppearance.IsLayered(id)&&!rawDiagnostic&&!Environment.GetCommandLineArgs().Contains("-wrapConceptLayers")&&!Environment.GetCommandLineArgs().Contains("-wrapHoodMaster")){
                string[] properties={"_ConceptHoodAtlas","_ConceptSceneAtlas","_ConceptBotanicalAtlas","_ConceptGardenCorner","_ConceptMusicCorner"};
                if(selected.Any(r=>r.sharedMaterials.Any(m=>m&&m.HasProperty("_WrapSharpPanels")&&m.GetFloat("_WrapSharpPanels")>.5f)))properties=properties.Concat(new[]{"_WrapLeftBandSharp","_WrapRightBandSharp","_WrapRearSharp","_WrapFrontSharp"}).ToArray();
                if(RebirthCamperWrapAppearance.UsesNativeBodyLayout(id))properties=properties.Concat(new[]{"_WrapRearDoorArt","_WrapRearLeftArt","_WrapRearRightArt"}).ToArray();
                var painted=selected.SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.name.EndsWith("_Wrap_"+id)).ToArray();
                foreach(string property in properties){
                    var textures=painted.Select(m=>m.GetTexture(property)).Distinct().ToArray();
                    if(textures.Length!=1||!textures[0])throw new Exception("Runtime layer binding missing or inconsistent: "+property);
                }
                if(painted.Any(m=>m.GetFloat("_ConceptHoodEnabled")!=1||m.GetFloat("_ConceptSceneEnabled")!=1))throw new Exception("Runtime composition disabled");
                if(painted.Any(m=>m.GetFloat("_WrapLayoutVariant")!=RebirthCamperWrapAppearance.NativeBodyLayoutVersion(id)))throw new Exception("Runtime native template version differs from selector");
                if(nativeMeshes.Any(p=>p.Key.sharedMesh!=p.Value)||nativePose.Any(p=>p.Key.localToWorldMatrix!=p.Value))throw new Exception("Runtime geometry changed");
                File.WriteAllText(Path.Combine(output,"runtime-layer-proof.txt"),"PASS "+properties.Length+" texture layers loaded by runtime wrap path; shared across "+painted.Length+" paint materials; no editor texture overrides; native mesh references="+nativeMeshes.Count+" and transforms="+nativePose.Count+" unchanged.");
                File.WriteAllLines(Path.Combine(output,"texture-density.tsv"),new[]{"property\twidth\theight\tformat\tmipmaps\tanisotropy\tfilter"}.Concat(properties.Select(p=>{var t=(Texture2D)painted[0].GetTexture(p);return p+"\t"+t.width+"\t"+t.height+"\t"+t.format+"\t"+t.mipmapCount+"\t"+t.anisoLevel+"\t"+t.filterMode;})));
            }
            if(Environment.GetCommandLineArgs().Contains("-wrapConceptLayers")){
                var layers=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/REBIRTHCamperWrapCollection/Artwork/UndeadChillSceneLayers.png");
                if(!layers)throw new Exception("Concept scene layers missing");
                foreach(var panel in selected)foreach(var material in panel.sharedMaterials)if(material&&material.HasProperty("_ConceptSceneEnabled")){
                    material.SetTexture("_ConceptSceneAtlas",layers);material.SetTexture("_ConceptBotanicalAtlas",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/REBIRTHCamperWrapCollection/Artwork/UndeadChillBotanicalLayers.png"));material.SetFloat("_ConceptSceneEnabled",1);
                    material.SetTexture("_ConceptGardenCorner",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/REBIRTHCamperWrapCollection/Artwork/UndeadChillGardenCorner.png"));
                    material.SetTexture("_ConceptMusicCorner",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/REBIRTHCamperWrapCollection/Artwork/UndeadChillMusicCorner.png"));
                }
                if(nativeMeshes.Any(p=>p.Key.sharedMesh!=p.Value)||nativePose.Any(p=>p.Key.localToWorldMatrix!=p.Value))throw new Exception("Native concept geometry/pose changed");
                File.WriteAllText(Path.Combine(output,"native-geometry-proof.txt"),"PASS native mesh references="+nativeMeshes.Count+"; unchanged world matrices="+nativePose.Count+"; original prefab read-only; fixed five orthographic cameras; artwork-only material changes. No AI-generated vehicle geometry.");
                File.WriteAllText(Path.Combine(output,"CONCEPT_ONLY.txt"),"Native prefab/mesh/transforms and fixed five cameras; new painted-surface proposal. Scene layer is editor-bound for concept review, not a distributable runtime wrap.");
            }
            if(Environment.GetCommandLineArgs().Contains("-wrapHoodMaster")){
                var master=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/REBIRTHCamperWrapCollection/Artwork/UndeadChillHoodMaster.png");if(!master)throw new Exception("Hood master missing");
                foreach(var panel in selected)foreach(var material in panel.sharedMaterials)if(material&&material.HasProperty("_ConceptHoodEnabled")){material.SetTexture("_ConceptHoodAtlas",master);material.SetFloat("_ConceptHoodEnabled",1);}
                File.WriteAllText(Path.Combine(output,"shared-hood-proof.txt"),"Both front and overhead cameras render the SAME model, SAME hood material, SAME master texture and SAME model-space registration. No independent AI redraw between these views.");
            }
            var diagnosticTextures=new List<Texture2D>();
            var diagnosticShaders=new Dictionary<Material,Shader>();
            if(rawDiagnostic){
                if(!RebirthCamperWrapAppearance.UsesNativeBodyLayout(id))throw new Exception("Raw diagnostic requires verified native-body mapping carrier.");
                string[] panels={"hood","left-continuous","right-continuous","camper-front-continuous","camper-rear-fixed-continuous","rear-door","camper-roof","cab-roof","truck-front-face","cab-back","pickup-tailgate","pickup-bed-interior-unfolded"};
                string[] properties={"_ConceptHoodAtlas","_ConceptSceneAtlas","_ConceptBotanicalAtlas","_ConceptGardenCorner","_ConceptMusicCorner","_WrapLeftBandSharp","_WrapRightBandSharp","_WrapRearSharp","_WrapFrontSharp","_WrapRearDoorArt","_WrapRearLeftArt","_WrapRearRightArt"};
                var provenance=new List<string>{"DIAGNOSTIC ONLY: unchanged raw source pixels on verified native mapping; no key removal, repair, export, bundle build, installation or runtime-art acceptance."};
                string directory=Arg("-wrapDiagnosticRawDirectory");
                if(Environment.GetCommandLineArgs().Contains("-wrapDiagnosticLayoutVersion")){
                    int version=int.Parse(Arg("-wrapDiagnosticLayoutVersion"));
                    if(version!=1&&version!=2)throw new Exception("Unknown diagnostic layout");
                    foreach(var material in selected.SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.name.EndsWith("_Wrap_"+id)).Distinct())material.SetFloat("_WrapLayoutVariant",version);
                    provenance.Add("Editor diagnostic layout override="+version+"; not runtime selector proof.");
                }
                for(int i=0;i<panels.Length;i++){
                    string path=Path.Combine(directory,panels[i]+"-raw.png");
                    byte[] bytes=File.ReadAllBytes(path);
                    var texture=new Texture2D(2,2,TextureFormat.RGBA32,true);
                    if(!texture.LoadImage(bytes))throw new Exception("Invalid raw diagnostic image "+path);
                    texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=16;
                    diagnosticTextures.Add(texture);
                    foreach(var material in selected.SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.name.EndsWith("_Wrap_"+id)).Distinct())material.SetTexture(properties[i],texture);
                    using(var sha=System.Security.Cryptography.SHA256.Create())provenance.Add(panels[i]+"\t"+texture.width+"x"+texture.height+"\t"+BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","")+"\t"+path);
                }
                if(nativeMeshes.Any(p=>p.Key.sharedMesh!=p.Value)||nativePose.Any(p=>p.Key.localToWorldMatrix!=p.Value))throw new Exception("Diagnostic geometry changed");
                File.WriteAllLines(Path.Combine(output,"RAW_DIAGNOSTIC_NOT_INSTALLED.txt"),provenance);
            }
            if(Environment.GetCommandLineArgs().Contains("-wrapTailgateNormalDiagnostic")){
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/RebirthTailgateNormals.shader");
                if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Tailgate diagnostic shader unavailable");
                foreach(var material in selected.SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.name.EndsWith("_Wrap_"+id)).Distinct()){
                    diagnosticShaders[material]=material.shader;material.shader=shader;
                }
                File.WriteAllText(Path.Combine(output,"NORMAL_DIAGNOSTIC_ONLY.txt"),"Rear body: yellow=above native tailgate frame Ymax1.10; red=upward dominant normal (roofAxis); green=endAxis; blue=sideAxis. Uses unchanged mesh and runtime vehicle frame; no production acceptance.");
            }
            Render(Path.Combine(output,id+"-front-full.png"),new Vector3(0,1.7f,9),new Vector3(0,1.7f,-.6f),2.1f);
            Render(Path.Combine(output,id+"-back-full.png"),new Vector3(0,1.7f,-9),new Vector3(0,1.7f,-.6f),2.1f);
            Render(Path.Combine(output,id+"-left-full.png"),new Vector3(-9,1.7f,-.6f),new Vector3(0,1.7f,-.6f),2.7f);
            Render(Path.Combine(output,id+"-right-full.png"),new Vector3(9,1.7f,-.6f),new Vector3(0,1.7f,-.6f),2.7f);
            Render(Path.Combine(output,id+"-top-full.png"),new Vector3(0,10,-.6f),new Vector3(0,1.7f,-.6f),3.7f);
            Render(Path.Combine(output,id+"-quarter.png"),new Vector3(-6,3.2f,6),new Vector3(0,1.55f,-.6f),2.7f);
            Render(Path.Combine(output,id+"-side.png"),new Vector3(-9,1.9f,-.6f),new Vector3(0,1.6f,-.6f),2.7f);
            Render(Path.Combine(output,id+"-right.png"),new Vector3(9,1.9f,-.6f),new Vector3(0,1.6f,-.6f),2.7f);
            Render(Path.Combine(output,id+"-rear.png"),new Vector3(0,2.1f,-8),new Vector3(0,1.7f,-1),2.1f);
            Render(Path.Combine(output,id+"-hood.png"),new Vector3(-1.4f,3.1f,3),new Vector3(0,1.20f,1.4f),1.3f);
            Render(Path.Combine(output,id+"-frontcanopy.png"),new Vector3(0,2.55f,6),new Vector3(0,2.2f,.5f),1.2f);
            Render(Path.Combine(output,id+"-frontcorner.png"),new Vector3(-4,2.8f,4),new Vector3(0,2.1f,.2f),1.6f);
            Render(Path.Combine(output,id+"-frontcornerRight.png"),new Vector3(4,2.8f,4),new Vector3(0,2.1f,.2f),1.6f);
            Render(Path.Combine(output,id+"-rear-quarter-detail.png"),new Vector3(-4,2.4f,-6),new Vector3(0,1.2f,-1.2f),2.1f);
            Render(Path.Combine(output,id+"-left-banner-detail.png"),new Vector3(-6,2.55f,-1.2f),new Vector3(0,2.55f,-1.2f),1.65f);
            Render(Path.Combine(output,id+"-right-banner-detail.png"),new Vector3(6,2.55f,-1.2f),new Vector3(0,2.55f,-1.2f),1.65f);
            Render(Path.Combine(output,id+"-rear-panel-detail.png"),new Vector3(0,1.7f,-7),new Vector3(0,1.7f,-3.3f),1.55f);
            if(RebirthCamperWrapAppearance.IsLayered(id)&&!Environment.GetCommandLineArgs().Contains("-wrapTailgateNormalDiagnostic")){
                var transforms=root.GetComponentsInChildren<Transform>(true);
                var door=transforms.FirstOrDefault(t=>t.name=="REBIRTHRearDoorPivot");
                var open=transforms.FirstOrDefault(t=>t.name=="REBIRTHRearDoorOpenPose");
                if(!door||!open)throw new Exception("Native rear door pose markers missing");
                var paint=door.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.HasProperty("_WrapRearDoorPanel")&&m.GetFloat("_WrapRearDoorPanel")>.5f).ToArray();
                if(paint.Length==0)throw new Exception("Rear door has no registered wrap mapping");
                var rows=paint.ToDictionary(m=>m,m=>new[]{m.GetVector("_WrapDoorRow0"),m.GetVector("_WrapDoorRow1"),m.GetVector("_WrapDoorRow2")});
                var closed=door.localRotation;
                try{
                    door.localRotation=open.localRotation;
                    Render(Path.Combine(output,id+"-rear-door-open.png"),new Vector3(-4,2.3f,-7),new Vector3(0,1.7f,-2.5f),1.9f);
                    foreach(var pair in rows)for(int row=0;row<3;row++)if(pair.Key.GetVector("_WrapDoorRow"+row)!=pair.Value[row])throw new Exception("Door-local artwork registration changed on opening");
                }finally{door.localRotation=closed;}
                File.WriteAllText(Path.Combine(output,"rear-door-pose-proof.txt"),"PASS actual prefab OpenPose marker used; door-local registration unchanged across opening; closed rotation restored. Offline articulation/render inspection, not game interaction.");
            }
            foreach(var texture in diagnosticTextures)Object.DestroyImmediate(texture);
            foreach(var pair in diagnosticShaders)pair.Key.shader=pair.Value;
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
