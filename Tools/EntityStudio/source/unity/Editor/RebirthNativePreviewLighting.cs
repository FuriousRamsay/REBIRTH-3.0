using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
namespace RebirthEntityStudio {
 // Reproduce the native sun shadow binding only while rendering this preview.
 public sealed class RebirthNativePreviewLighting : IDisposable {
  static readonly Dictionary<PreviewRenderUtility,RebirthNativePreviewLighting> active=new Dictionary<PreviewRenderUtility,RebirthNativePreviewLighting>();
  readonly PreviewRenderUtility preview;readonly CommandBuffer command;readonly MaterialPropertyBlock properties;readonly Texture previousMap;readonly Vector4 previousTexel;readonly ShadowQuality shadows;readonly RenderingPath path;readonly LightShadows lightShadows;bool disposed;readonly float reflectionScale;float reflectionBefore;bool reflectionActive;DefaultReflectionMode reflectionMode;Texture reflectionTexture;static Cubemap studioReflection;
  public RebirthNativePreviewLighting(PreviewRenderUtility value,float environmentReflections=1,RenderingPath nativePath=RenderingPath.DeferredShading){reflectionScale=Mathf.Clamp(environmentReflections,0,2);preview=value;if(active.Count!=0)throw new InvalidOperationException("Native preview lighting scopes cannot overlap.");previousMap=Shader.GetGlobalTexture("_SunCascadedShadowMap");previousTexel=Shader.GetGlobalVector("_SunCascadedShadowMap_TexelSize");shadows=QualitySettings.shadows;path=preview.camera.renderingPath;lightShadows=preview.lights[0].shadows;command=new CommandBuffer{name="REBIRTH native preview sun"};command.SetGlobalTexture("_SunCascadedShadowMap",new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive));preview.lights[0].AddCommandBuffer(LightEvent.AfterShadowMap,command);preview.lights[0].shadows=LightShadows.Soft;if(QualitySettings.shadows==ShadowQuality.Disable)QualitySettings.shadows=ShadowQuality.All;preview.camera.renderingPath=nativePath;var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(preview.ambientColor);properties=new MaterialPropertyBlock();properties.CopySHCoefficientArraysFrom(new[]{sh});active.Add(preview,this);Camera.onPreCull+=BeforeCamera;Camera.onPostRender+=AfterCamera;}
  void BeforeCamera(Camera camera){if(camera!=preview.camera)return;reflectionBefore=RenderSettings.reflectionIntensity;reflectionMode=RenderSettings.defaultReflectionMode;reflectionTexture=RenderSettings.customReflectionTexture;if(studioReflection==null)studioReflection=EditorGUIUtility.Load("PrefabMode/DefaultReflectionForPrefabMode.exr") as Cubemap;if(studioReflection!=null){RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=studioReflection;}reflectionActive=true;RenderSettings.reflectionIntensity=reflectionBefore*reflectionScale;}
  void AfterCamera(Camera camera){if(camera!=preview.camera||!reflectionActive)return;RenderSettings.reflectionIntensity=reflectionBefore;RenderSettings.defaultReflectionMode=reflectionMode;RenderSettings.customReflectionTexture=reflectionTexture;reflectionActive=false;}
  public static void Draw(PreviewRenderUtility preview,Mesh mesh,Matrix4x4 matrix,Material material,int submesh){if(active.TryGetValue(preview,out var scope))Graphics.DrawMesh(mesh,matrix,material,0,preview.camera,submesh,scope.properties,ShadowCastingMode.On,true,null,LightProbeUsage.CustomProvided);else preview.DrawMesh(mesh,matrix,material,submesh);}
  public void Dispose(){if(disposed)return;disposed=true;Camera.onPreCull-=BeforeCamera;Camera.onPostRender-=AfterCamera;AfterCamera(preview.camera);active.Remove(preview);preview.lights[0].RemoveCommandBuffer(LightEvent.AfterShadowMap,command);command.Dispose();preview.lights[0].shadows=lightShadows;if(QualitySettings.shadows!=shadows)QualitySettings.shadows=shadows;preview.camera.renderingPath=path;Shader.SetGlobalTexture("_SunCascadedShadowMap",previousMap);Shader.SetGlobalVector("_SunCascadedShadowMap_TexelSize",previousTexel);}
 }
}



