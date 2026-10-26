using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 // PreviewRenderUtility creates a single-sample target in Unity2022.3.
 // Called after BeginPreview/BeginStaticPreview, before drawing authored cards.
 public static class RebirthPreviewAntialiasing {
  public static int Configure(PreviewRenderUtility preview,bool enabled=true){
   var target=preview.camera.targetTexture;if(target==null)return 1;
   int desired=1;if(enabled){var descriptor=target.descriptor;descriptor.msaaSamples=4;desired=SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);}
   preview.camera.allowMSAA=enabled;
   if(target.antiAliasing==desired)return desired;
   var active=RenderTexture.active;bool wasActive=active==target;
   if(wasActive)RenderTexture.active=null;
   try{target.Release();target.antiAliasing=desired;target.Create();}
   finally{if(wasActive)RenderTexture.active=target;else RenderTexture.active=active;}
   return target.antiAliasing;
  }
 }
}
