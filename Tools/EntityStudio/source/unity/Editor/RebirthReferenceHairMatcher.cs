using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 // Frontal silhouette ranking of actual fitted native assets. It cannot infer
 // hidden locks or curl structure absent from the local game library.
 public static class RebirthReferenceHairMatcher {
  [Serializable] public sealed class Score {public string key,preview;public float overlap,hairlineOverlap,rankScore,appearanceClassCosine,appearancePatchCosine,belowEyeExtentDifference;public bool extentAvailable;}
  [Serializable] public sealed class Report {public string method="Native strand-alpha silhouette plus upper-face hairline overlap",status,selectedKey,sourceHash,referenceSha256;public Color observedHairColour;public bool observedHairColourAvailable;public Score[] scores;public float referenceArea,bestMargin;public bool hairlineAvailable,appearanceAvailable,insufficientSupport;}
  [Serializable] sealed class Frame {public string sourceSha256;public float xMin,xMax,yMin,yMax;}
  [Serializable] sealed class Observation {public string sourceSha256;}
  public static IEnumerator Match(FaceTemplate template,FaceRecipe recipe,string folder,Action<float,string> update){
   var report=new Report();string confidencePath=Path.Combine(folder,"hairConfidence.png");
   if(!File.Exists(confidencePath)||!RebirthNativeAppearanceLibrary.Available){report.status="Not matched: reference masks or local native library unavailable.";Save(folder,report);yield break;}
   var reference=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
   if(!reference.LoadImage(File.ReadAllBytes(confidencePath))){UnityEngine.Object.DestroyImmediate(reference);throw new IOException("Hair confidence image is unreadable.");}
   const int size=256;var target=new float[size*size];float area=0;
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){float v=reference.GetPixelBilinear((x+.5f)/size,(y+.5f)/size).r;target[y*size+x]=Mathf.Clamp01((v-.5f)/.35f);area+=target[y*size+x];}
   UnityEngine.Object.DestroyImmediate(reference);report.referenceArea=area/target.Length;
   // Conservative guard against tiny false-positive scalp patches. This is a
   // support threshold, not a baldness classifier or a calibrated probability.
   if(report.referenceArea<.01f){report.status="Retained manual hair: insufficient visible reference hair.";Save(folder,report);yield break;}
   Frame frame=null;string framePath=Path.Combine(folder,"ReferenceHairFrame.json");if(File.Exists(framePath)){frame=JsonUtility.FromJson<Frame>(File.ReadAllText(framePath));if(frame==null||!(frame.xMin>=0&&frame.xMin<frame.xMax&&frame.xMax<=1&&frame.yMin>=0&&frame.yMin<frame.yMax&&frame.yMax<=1))throw new InvalidDataException("Invalid reference hairline frame.");}
   if(frame!=null){var observation=JsonUtility.FromJson<Observation>(File.ReadAllText(Path.Combine(folder,"AppearanceObservations.json")));if(observation==null||string.IsNullOrEmpty(frame.sourceSha256)||frame.sourceSha256!=observation.sourceSha256)throw new InvalidDataException("Hairline frame belongs to a different reference image.");report.referenceSha256=frame.sourceSha256;}
   var hairline=new bool[target.Length];float hairlineSupport=0;
   if(frame!=null)for(int y=0;y<size;y++)for(int x=0;x<size;x++){float px=(x+.5f)/size,py=1-(y+.5f)/size;bool inside=px>=frame.xMin&&px<=frame.xMax&&py>=frame.yMin&&py<=frame.yMax;hairline[y*size+x]=inside;if(inside)hairlineSupport+=target[y*size+x];}
   report.hairlineAvailable=hairlineSupport>=64;
   var shader=Shader.Find("Hidden/REBIRTH/HairSilhouette");if(shader==null)throw new FileNotFoundException("Hair silhouette shader is missing.");
   var head=RebirthFaceGeneratorModel.Build(template,recipe);var skin=new Material(shader);skin.SetColor("_Colour",Color.black);skin.SetFloat("_AlphaCutoff",0);
   report.sourceHash=RebirthNativeAppearanceLibrary.Catalog.sourceHash;
   var scores=new List<Score>();var styles=RebirthNativeAppearanceLibrary.Catalog.styles.Where(s=>s.family=="hair").ToArray();
   try{for(int i=0;i<styles.Length;i++){
    update((float)i/styles.Length,"Matching reference hairstyle "+(i+1)+" / "+styles.Length);
    var settings=JsonUtility.FromJson<FaceNativeAppearanceSettings>(JsonUtility.ToJson(recipe.nativeAppearance));settings.enabled=true;settings.sourceHash=RebirthNativeAppearanceLibrary.Catalog.sourceHash;settings.hairKey=styles[i].key;settings.authoredLength=-1;settings.authoredMessiness=-1;
    var hair=RebirthNativeAppearanceLibrary.Hair(head,settings);var source=RebirthNativeAppearanceLibrary.Source(styles[i]);var preview=new PreviewRenderUtility();var materials=new List<Material>();Texture2D image=null;
    try{preview.BeginStaticPreview(new Rect(0,0,size,size));RebirthFaceGeneratorWindow.ConfigurePreview(preview,Vector2.zero,.19f/Mathf.Max(.3f,recipe.portraitScale),false,recipe);preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=Color.black;
     preview.DrawMesh(head,Matrix4x4.identity,skin,0);
     for(int sub=0;sub<hair.subMeshCount;sub++){var original=source.sharedMaterials[sub];if(original==null||original.shader.name!="Game/SDCS/Hair")continue;var mat=new Material(shader);materials.Add(mat);mat.SetTexture("_Masks1",original.GetTexture("_Masks1"));mat.SetTextureScale("_Masks1",original.GetTextureScale("_Masks1"));mat.SetTextureOffset("_Masks1",original.GetTextureOffset("_Masks1"));mat.SetFloat("_AlphaCutoff",original.GetFloat("_AlphaCutoff"));preview.DrawMesh(hair,Matrix4x4.identity,mat,sub);}
     preview.Render();image=preview.EndStaticPreview();string filename="HairMatch_"+i.ToString("D2")+".png";File.WriteAllBytes(Path.Combine(folder,filename),image.EncodeToPNG());var pixels=image.GetPixels();double intersection=0,union=0,hairlineIntersection=0,hairlineUnion=0;for(int p=0;p<pixels.Length;p++){float value=pixels[p].r;float a=Math.Min(value,target[p]),b=Math.Max(value,target[p]);intersection+=a;union+=b;if(hairline[p]){hairlineIntersection+=a;hairlineUnion+=b;}}float overlap=union>0?(float)(intersection/union):0,local=hairlineUnion>0?(float)(hairlineIntersection/hairlineUnion):0;scores.Add(new Score{key=styles[i].key,preview=filename,overlap=overlap,hairlineOverlap=local,rankScore=report.hairlineAvailable?overlap*.65f+local*.35f:overlap});
    }finally{preview.Cleanup();if(image!=null)UnityEngine.Object.DestroyImmediate(image);foreach(var mat in materials)UnityEngine.Object.DestroyImmediate(mat);UnityEngine.Object.DestroyImmediate(hair);}yield return null;
   }}finally{UnityEngine.Object.DestroyImmediate(head);UnityEngine.Object.DestroyImmediate(skin);}
   report.scores=scores.OrderByDescending(s=>s.rankScore).ToArray();report.bestMargin=report.scores.Length>1?report.scores[0].rankScore-report.scores[1].rankScore:0;
   // Reviewed examples confuse loose hair with dreadlocks even with a score
   // margin. Keep these rankings as editable suggestions until appearance
   // matching is validated; never overwrite the user's hairstyle from shape.
   report.status="Retained current hair: shape suggestions require selection; automatic appearance matching is not validated.";
   Save(folder,report);
  }
  static void Save(string folder,Report report){File.WriteAllText(Path.Combine(folder,"HairMatchReport.json"),JsonUtility.ToJson(report,true));}
 }
}
