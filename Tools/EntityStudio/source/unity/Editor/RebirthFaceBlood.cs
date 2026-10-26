using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class FaceBloodSettings {
  public string skinMaskGuid="",teethMaskGuid="",tongueMaskGuid="",liningMaskGuid="";
  public Color tint=Color.white;
  public float amount=1,freshness=.7f,wetness=.5f;
  public string Mask(int slot){return slot==0?skinMaskGuid:slot==1?teethMaskGuid:slot==2?tongueMaskGuid:slot==3?liningMaskGuid:throw new ArgumentOutOfRangeException("slot");}
  public void SetMask(int slot,string guid){switch(slot){case 0:skinMaskGuid=guid;break;case 1:teethMaskGuid=guid;break;case 2:tongueMaskGuid=guid;break;case 3:liningMaskGuid=guid;break;default:throw new ArgumentOutOfRangeException("slot");}}
  public void Validate(){foreach(float v in new[]{amount,freshness,wetness,tint.r,tint.g,tint.b})if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidDataException("Invalid head blood appearance");amount=Mathf.Clamp01(amount);freshness=Mathf.Clamp01(freshness);wetness=Mathf.Clamp01(wetness);tint=new Color(Mathf.Clamp01(tint.r),Mathf.Clamp01(tint.g),Mathf.Clamp01(tint.b),1);}
 }
 public static class RebirthFaceBlood {
  static Texture2D Load(string guid){if(string.IsNullOrEmpty(guid))return null;string path=AssetDatabase.GUIDToAssetPath(guid);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture==null)throw new FileNotFoundException("Head blood mask is missing",path);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer!=null&&(importer.sRGBTexture||importer.alphaIsTransparency))throw new InvalidDataException("Blood masks require sRGB and Alpha Is Transparency disabled");return texture;}
  public static void Apply(Material material,FaceRecipe recipe,int slot){
   if(material==null||recipe==null||recipe.blood==null)return;var settings=recipe.blood;settings.Validate();var mask=Load(settings.Mask(slot));if(mask==null){if(material.HasProperty("_BloodAmounts")&&material.HasProperty("_BloodMask")){material.SetTexture("_BloodMask",Texture2D.blackTexture);material.SetVector("_BloodAmounts",Vector4.zero);}return;}
   bool ours=material.HasProperty("_BloodAmounts")&&material.HasProperty("_BloodMask");
   if(!ours){
    if(material.shader==null||material.shader.name!="Standard"||material.GetFloat("_Mode")!=0||material.IsKeywordEnabled("_PARALLAXMAP")||material.IsKeywordEnabled("_DETAIL_MULX2"))throw new InvalidOperationException("Blood masks currently support opaque Standard materials without parallax/detail layers");
    string path=AssetDatabase.FindAssets("MaskedBloodSurface t:Shader").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/Shaders/MaskedBloodSurface.shader",StringComparison.Ordinal));
    var shader=path==null?null:AssetDatabase.LoadAssetAtPath<Shader>(path);if(shader==null)throw new FileNotFoundException("Install MaskedBloodSurface.shader");
    // Changing the clone's shader retains matching Standard properties and keywords.
    var keywords=material.shaderKeywords;bool highlightsOff=material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"),reflectionsOff=material.IsKeywordEnabled("_GLOSSYREFLECTIONS_OFF");material.shader=shader;material.shaderKeywords=keywords;if(highlightsOff)material.SetFloat("_SpecularHighlights",0);if(reflectionsOff)material.SetFloat("_GlossyReflections",0);
   }
   material.SetTexture("_BloodMask",mask);material.SetTextureScale("_BloodMask",Vector2.one);material.SetTextureOffset("_BloodMask",Vector2.zero);
   material.SetVector("_BloodAmounts",new Vector4(settings.amount,settings.freshness,settings.wetness,0));material.SetColor("_BloodTint",settings.tint);
  }
  public static void FollowBody(FaceRecipe face,BodyDamageSettings body){if(face.blood==null)face.blood=new FaceBloodSettings();face.blood.tint=body.bloodTint;face.blood.freshness=body.freshness;face.blood.wetness=body.wetness;}
  public static void CaptureExport(FaceRecipe saved,int slot,Material material){if(saved.blood==null||string.IsNullOrEmpty(saved.blood.Mask(slot)))return;var texture=material.GetTexture("_BloodMask");string path=AssetDatabase.GetAssetPath(texture);if(string.IsNullOrEmpty(path))throw new IOException("Exported blood mask has no asset path");saved.blood.SetMask(slot,AssetDatabase.AssetPathToGUID(path));}
  public static FaceBloodSettings Draw(FaceBloodSettings current){
   var edited=JsonUtility.FromJson<FaceBloodSettings>(JsonUtility.ToJson(current));EditorGUILayout.Space(8);EditorGUILayout.LabelField("Blood layers",EditorStyles.boldLabel);EditorGUI.BeginChangeCheck();
   string[] labels={"Skin blood mask","Teeth / gums blood mask","Tongue blood mask","Mouth lining blood mask"};for(int i=0;i<4;i++){var currentTexture=string.IsNullOrEmpty(edited.Mask(i))?null:AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(edited.Mask(i)));var texture=(Texture2D)EditorGUILayout.ObjectField(labels[i],currentTexture,typeof(Texture2D),false);edited.SetMask(i,texture==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture)));}
   edited.amount=EditorGUILayout.Slider("Blood coverage",edited.amount,0,1);edited.tint=EditorGUILayout.ColorField("Blood colour tint",edited.tint);edited.freshness=EditorGUILayout.Slider("Blood freshness",edited.freshness,0,1);edited.wetness=EditorGUILayout.Slider("Blood wetness",edited.wetness,0,1);
   EditorGUILayout.LabelField("Mask R uses raw head mesh UVs; import as linear data. No mask preserves the source material. Baked portrait blood is not automatically separated. Attached heads follow the body's blood colour, freshness and wetness.",EditorStyles.wordWrappedMiniLabel);
   bool changed=EditorGUI.EndChangeCheck();if(GUILayout.Button("Clear head blood layers")){for(int i=0;i<4;i++)edited.SetMask(i,"");changed=true;}
   if(!changed)return null;edited.Validate();try{for(int i=0;i<4;i++)Load(edited.Mask(i));}catch(Exception e){EditorGUILayout.HelpBox(e.Message,MessageType.Error);return null;}return edited;
  }
 }
}
