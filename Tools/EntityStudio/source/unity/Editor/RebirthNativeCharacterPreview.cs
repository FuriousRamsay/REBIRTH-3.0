using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RebirthEntityStudio
{
    public static class RebirthNativeCharacterPreview
    {
        public static void Run()
        {
            var bundles=new List<AssetBundle>();var owned=new List<UnityEngine.Object>();
            try
            {
                string root="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Addressables/Standalone";
                foreach(string path in Directory.GetFiles(root,"*unitybuiltinshaders.bundle"))Load(path);
                Load(Path.Combine(root,"shaders_assets_all.bundle"));Load(Path.Combine(root,"zombies_assets_entities/zombies/common.bundle"));var worker=Load(Path.Combine(root,"zombies_assets_entities/zombies/worker.bundle"));
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity",OpenSceneMode.Single);
                var workspace=scene.GetRootGameObjects().Select(value=>value.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
                RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);
                RebirthEntitySkinnedBounds.FitRestPose(workspace);
                var output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/NativeCharacterReview");Directory.CreateDirectory(output);
                var cameraObject=new GameObject("__NativeReviewCamera");owned.Add(cameraObject);Camera camera=cameraObject.AddComponent<Camera>();
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.fieldOfView=32;camera.nearClipPlane=.01f;camera.farClipPlane=30;
                var lightObject=new GameObject("__NativeReviewKey");owned.Add(lightObject);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;lightObject.transform.rotation=Quaternion.Euler(35,145,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.43f,.43f,.43f);
                var target=new RenderTexture(1200,1600,24);target.Create();owned.Add(target);camera.targetTexture=target;camera.aspect=.75f;
                var readback=new Texture2D(1200,1600,TextureFormat.RGB24,false);owned.Add(readback);
                var notes=new List<string>{"Installed native Game/Character shader; locally loaded game bundles, not redistributed.","Authoring scene remains unchanged; material clones are temporary."};
                bool channelsOnly=Environment.GetCommandLineArgs().Contains("-nativeOnlyChannels");
                var eyeNotes=new List<string>();
                foreach(ZombieTier tier in new[]{ZombieTier.Normal,ZombieTier.Feral,ZombieTier.Radiated})
                {
                    foreach(var eye in Prefab(tier).GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(value=>value.name.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)>=0))
                    {
                        eyeNotes.Add(tier+" EYE "+eye.name+" active="+eye.gameObject.activeSelf+" enabled="+eye.enabled+" root="+(eye.rootBone==null?"null":eye.rootBone.name)+" bones="+string.Join(",",eye.bones.Select(value=>value==null?"null":value.name)));
                        if(eye.sharedMesh!=null)eyeNotes.Add("  mesh="+eye.sharedMesh.name+" vertices="+eye.sharedMesh.vertexCount+" bounds="+eye.sharedMesh.bounds+" renderer="+UnityEditor.EditorJsonUtility.ToJson(eye));
                    }
                    foreach(var renderer in Prefab(tier).GetComponentsInChildren<Renderer>(true))
                        foreach(var material in renderer.sharedMaterials)
                        {
                            if(material==null||material.shader==null||material.shader.name=="Game/Character")continue;
                            eyeNotes.Add(tier+" renderer="+AnimationUtility.CalculateTransformPath(renderer.transform,Prefab(tier).transform)+" material="+material.name+" shader="+material.shader.name+" keywords="+string.Join(",",material.shaderKeywords));
                            for(int propertyIndex=0;propertyIndex<material.shader.GetPropertyCount();propertyIndex++)
                            {
                                string property=material.shader.GetPropertyName(propertyIndex);var type=material.shader.GetPropertyType(propertyIndex);
                                string value="";
                                if(type==ShaderPropertyType.Texture){var texture=material.GetTexture(property);value=texture==null?"null":texture.name+" "+texture.width+"x"+texture.height;}
                                else if(type==ShaderPropertyType.Color)value=material.GetColor(property).ToString();
                                else if(type==ShaderPropertyType.Vector)value=material.GetVector(property).ToString();
                                else value=material.GetFloat(property).ToString(System.Globalization.CultureInfo.InvariantCulture);
                                eyeNotes.Add("  "+property+" "+type+"="+value);
                            }
                        }
                }
                File.WriteAllLines(Path.Combine(output,"NativeAccessoryMaterials.txt"),eyeNotes);
                foreach(ZombieTier tier in channelsOnly?Array.Empty<ZombieTier>():Enum.GetValues(typeof(ZombieTier)))
                {
                    RebirthEntityRigBuilder.ApplyTierMaterials(workspace,tier);
                    GameObject prefab=Prefab(tier);Material template=BodyMaterial(prefab);
                    if(template==null||template.shader==null||template.shader.name!="Game/Character")throw new InvalidDataException("Native body shader missing for "+tier);
                    notes.Add(tier+": "+template.name+" keywords="+string.Join(",",template.shaderKeywords));
                    for(int i=0;i<template.shader.GetPropertyCount();i++)
                    {
                        string property=template.shader.GetPropertyName(i);var type=template.shader.GetPropertyType(i);
                        notes.Add(tier+" PROPERTY "+property+" "+template.shader.GetPropertyDescription(i)+" "+type+"="+(type==ShaderPropertyType.Color?template.GetColor(property).ToString():type==ShaderPropertyType.Float||type==ShaderPropertyType.Range?template.GetFloat(property).ToString():""));
                    }
                    foreach(var set in workspace.rendererTierMaterials)
                    {
                        if(set.renderer.name=="FeralEye"&&set.renderer.CompareTag("E_BP_Eye"))
                        {
                            Material eyeTemplate=Prefab(tier).GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(value=>value.name=="FeralEye").sharedMaterial;
                            var eyeMaterial=new Material(eyeTemplate);owned.Add(eyeMaterial);Material authored=set.variants[(int)tier*set.slotCount];
                            eyeMaterial.SetTexture("_MainTex",authored.GetTexture("_MainTex"));eyeMaterial.SetTexture("_EmissionMap",authored.GetTexture("_EmissionMap"));eyeMaterial.SetColor("_EmissionColor",authored.GetColor("_EmissionColor"));eyeMaterial.EnableKeyword("_EMISSION");set.renderer.sharedMaterial=eyeMaterial;continue;
                        }
                        var slots=new Material[set.slotCount];
                        for(int slot=0;slot<slots.Length;slot++)
                        {
                            Material authored=set.variants[(int)tier*set.slotCount+slot];var material=new Material(template);owned.Add(material);
                            material.SetTexture("_Albedo",authored.GetTexture("_MainTex"));material.SetTexture("_Normal",authored.GetTexture("_BumpMap"));
                            string mask=tier==ZombieTier.Normal?"Normal":tier==ZombieTier.Feral?"Feral":"Special";
                            var textureOverride=RebirthEntityMaterialMaps.FindOverride(workspace.profile,slot<set.sourceMaterials.Count?set.sourceMaterials[slot]:null);
                            string mapName=workspace.profile.entityClass+"_"+(textureOverride==null?"":textureOverride.mapName+"_")+"RMOE_"+mask;
                            material.SetTexture("_RMOE",workspace.profile.nativeRmoeMaps.Single(value=>value.name==mapName));
                            if(tier!=ZombieTier.Normal)
                            {
                                material.EnableKeyword("_EMISSION");
                                if(tier==ZombieTier.Feral)material.DisableKeyword("_IRRADIATED_ON");else material.EnableKeyword("_IRRADIATED_ON");
                                Color color=tier==ZombieTier.Feral?new Color(1,.55f,.08f,1):tier==ZombieTier.Radiated?workspace.profile.radiatedTint:tier==ZombieTier.Charged?workspace.profile.chargedTint:workspace.profile.infernalTint;
                                material.SetColor("_EmissiveColor",color);
                            }
                            slots[slot]=material;
                        }
                        set.renderer.sharedMaterials=slots;foreach(var lod in set.lodRenderers)if(lod!=null)lod.sharedMaterials=slots;
                    }
                    if(workspace.lodGroup!=null)workspace.lodGroup.ForceLOD(0);
                    Frame(workspace.sourceSkinnedMeshes);Save("RebirthFarmer_Native_"+tier+".png");
                    if(tier==ZombieTier.Normal)
                    {
                        var currentOverlay=new List<GameObject>();RebirthFarmerReviewCapture.CreateOverlay(workspace.generatedSkeleton,currentOverlay);
                        Save("RebirthFarmer_Native_Normal_Colliders.png");foreach(var overlayObject in currentOverlay)UnityEngine.Object.DestroyImmediate(overlayObject);
                    }
                    foreach(var renderer in workspace.GetComponentsInChildren<SkinnedMeshRenderer>(true))notes.Add("FARMER_RENDERER "+tier+" "+renderer.name+" active="+renderer.gameObject.activeInHierarchy+" enabled="+renderer.enabled+" forcedOff="+renderer.forceRenderingOff+" bounds="+renderer.bounds);
                    Vector3 face=workspace.transform.position+new Vector3(0f,workspace.profile.estimatedHeight*.934f,.02f);
                    camera.transform.position=face+new Vector3(.03f,.015f,.85f);camera.transform.LookAt(face);camera.fieldOfView=28f;Save("RebirthFarmer_Native_"+tier+"_Face.png");camera.fieldOfView=32f;
                }
                if(Environment.GetCommandLineArgs().Contains("-nativeFarmerOnly")){File.WriteAllLines(Path.Combine(output,"NativeFarmerMaterialReview.txt"),notes);Debug.Log("NATIVE FARMER PREVIEWS: "+output);return;}
                workspace.gameObject.SetActive(false);
                GameObject native=UnityEngine.Object.Instantiate(Prefab(ZombieTier.Normal));owned.Add(native);
                foreach(var animator in native.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                foreach(var group in native.GetComponentsInChildren<LODGroup>(true))group.ForceLOD(0);
                foreach(var renderer in native.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if(!renderer.sharedMaterials.Any(material=>material!=null&&material.shader!=null&&material.shader.name=="Game/Character"))renderer.enabled=false;
                Frame(native.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(value=>value.enabled&&value.sharedMaterials.Any(material=>material!=null&&material.shader.name=="Game/Character")));
                Save("NativeWorker_Model.png");
                var overlay=new List<GameObject>();RebirthFarmerReviewCapture.CreateOverlay(native.transform,overlay);foreach(var value in overlay)owned.Add(value);Save("NativeWorker_Colliders.png");
                foreach(var value in overlay)value.SetActive(false);
                foreach(string side in new[]{"Left","Right"})
                {
                    Vector3 direction=side=="Left"?Vector3.left:Vector3.right;
                    Aim(side+"Arm",side+"ForeArm",direction);Aim(side+"ForeArm",side+"Hand",direction);
                }
                var nativeLod=native.GetComponentInChildren<LODGroup>();
                var nativeSkin=(nativeLod!=null?nativeLod.GetLODs()[0].renderers.OfType<SkinnedMeshRenderer>():native.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    .Where(value=>value.sharedMaterials.Any(material=>material!=null&&material.shader.name=="Game/Character")).ToList();
                foreach(var renderer in nativeSkin)
                {
                    if(!renderer.transform.IsChildOf(native.transform))throw new InvalidDataException("Native LOD renderer does not belong to the inspected instance: "+renderer.name);
                    renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);owned.Add(renderer.sharedMesh);RebirthEntityHumanoidBuilder.RebindCurrentPose(renderer);
                    // Render the exact CPU geometry used by the coverage audit.
                    // A live native SkinnedMeshRenderer can retain its prior skin
                    // buffers after imported mesh/bind-pose replacement in batchmode.
                    var display=new GameObject("__NativeAuditedMesh");owned.Add(display);display.transform.SetParent(renderer.transform,false);
                    display.AddComponent<MeshFilter>().sharedMesh=renderer.sharedMesh;display.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;renderer.enabled=false;
                }
                var nativeProfile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();owned.Add(nativeProfile);nativeProfile.estimatedHeight=1.8f;
                var nativeWorkspace=native.AddComponent<RebirthEntityRigWorkspace>();nativeWorkspace.profile=nativeProfile;nativeWorkspace.sourceSkinnedMeshes=nativeSkin;
                foreach(var collider in native.GetComponentsInChildren<Collider>(true))nativeWorkspace.colliderFitReports.Add(new EntityColliderFitReport{colliderName=collider.name,collider=collider});
                RebirthEntityCoverageReview.Write(nativeWorkspace,Path.Combine(output,"NativeWorker_Coverage.json"));Frame(nativeSkin);Save("NativeWorker_TPose.png");
                var tposeOverlay=new List<GameObject>();RebirthFarmerReviewCapture.CreateOverlay(native.transform,tposeOverlay);foreach(var value in tposeOverlay)owned.Add(value);Save("NativeWorker_TPose_Colliders.png");foreach(var value in tposeOverlay)value.SetActive(false);
                native.SetActive(false);
                void Aim(string startName,string endName,Vector3 direction){Transform start=RebirthEntityRigBuilder.FindBone(native.transform,startName),end=RebirthEntityRigBuilder.FindBone(native.transform,endName);start.rotation=Quaternion.FromToRotation(end.position-start.position,direction)*start.rotation;}
                var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);owned.Add(sphere);sphere.transform.position=Vector3.zero;
                var probe=new Material(BodyMaterial(Prefab(ZombieTier.Normal)));owned.Add(probe);probe.EnableKeyword("_EMISSION");probe.SetColor("_EmissiveColor",new Color(1,0,0,1));sphere.GetComponent<MeshRenderer>().sharedMaterial=probe;
                var albedo=new Texture2D(4,4,TextureFormat.RGBA32,false,false);owned.Add(albedo);albedo.SetPixels(Enumerable.Repeat(new Color(.45f,.45f,.45f,1),16).ToArray());albedo.Apply();probe.SetTexture("_Albedo",albedo);
                var flatNormal=new Texture2D(4,4,TextureFormat.RGBA32,false,true);owned.Add(flatNormal);flatNormal.SetPixels(Enumerable.Repeat(new Color(.5f,.5f,1,.5f),16).ToArray());flatNormal.Apply();probe.SetTexture("_Normal",flatNormal);
                var packed=new Texture2D(4,4,TextureFormat.RGBA32,false,true);owned.Add(packed);probe.SetTexture("_RMOE",packed);
                camera.transform.position=new Vector3(0,0,2.5f);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=32;
                string[] cases={"Baseline","R_zero","G_one","B_zero","A_one"};Color[] values={new Color(1,0,1,0),new Color(0,0,1,0),new Color(1,1,1,0),new Color(1,0,0,0),new Color(1,0,1,1)};
                notes.Add("Controlled native shader channel probe: constant nonmetal grey albedo, flat normal, one RMOE channel changed per image; red emissive color.");
                for(int i=0;i<cases.Length;i++)
                {
                    packed.SetPixels(Enumerable.Repeat(values[i],16).ToArray());packed.Apply();Save("Native_RMOE_"+cases[i]+".png");
                    Color sample=readback.GetPixel(600,800);notes.Add("CHANNEL_PROBE "+cases[i]+" RMOE="+values[i]+" centerRGB="+sample);
                }
                File.WriteAllLines(Path.Combine(output,channelsOnly?"NativeChannelProbe.txt":"NativeMaterialProperties.txt"),notes);Debug.Log("NATIVE CHARACTER PREVIEWS: "+output);
                AssetBundle Load(string path){var bundle=AssetBundle.LoadFromFile(path);if(bundle==null)throw new IOException("Cannot load "+path);bundles.Add(bundle);return bundle;}
                GameObject Prefab(ZombieTier tier){string name=tier==ZombieTier.Normal?"ZWorker.prefab":tier==ZombieTier.Feral?"ZWorkerFeral.prefab":"ZWorkerRadiated.prefab";return worker.LoadAsset<GameObject>(worker.GetAllAssetNames().Single(value=>value.EndsWith("/"+name,StringComparison.OrdinalIgnoreCase)));}
                Material BodyMaterial(GameObject prefab){return prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(value=>value.sharedMaterials).FirstOrDefault(value=>value!=null&&value.shader!=null&&value.shader.name=="Game/Character");}
                void Frame(IEnumerable<SkinnedMeshRenderer> renderers)
                {
                    Bounds bounds=new Bounds();bool has=false;
                    foreach(var renderer in renderers)foreach(Vector3 point in renderer.sharedMesh.vertices)
                    {Vector3 world=renderer.transform.TransformPoint(point);if(!has){bounds=new Bounds(world,Vector3.zero);has=true;}else bounds.Encapsulate(world);}
                    if(!has)throw new InvalidDataException("No native preview geometry.");
                    float size=Mathf.Max(bounds.size.y,bounds.size.x/.75f);camera.transform.position=bounds.center+new Vector3(size*.04f,size*.02f,size*2f);camera.transform.LookAt(bounds.center);
                }
                void Save(string name){camera.Render();RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,1200,1600),0,0);readback.Apply();File.WriteAllBytes(Path.Combine(output,name),readback.EncodeToPNG());RenderTexture.active=null;}
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
            finally
            {
                foreach(var value in owned)if(value!=null)UnityEngine.Object.DestroyImmediate(value);
                for(int i=bundles.Count-1;i>=0;i--)bundles[i].Unload(true);
            }
        }
    }
}
