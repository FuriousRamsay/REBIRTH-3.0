using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class BodyDamageSettings {
  public int version=1;public string deepLibraryGuid="",torsoLibraryGuid="",torsoLibraryHash="";public BodyDeepWound[] deepWounds=new BodyDeepWound[0];
  public string injuryMaskGuid="",bloodMaskGuid="";
  public string deepBloodMaskGuid="";public float deepBloodStrength=1,deepBloodSpread=.35f;
  public WoundSurfaceTextures woundTextures=new WoundSurfaceTextures();
  public Color bloodTint=Color.white;
  public float bruising,abrasion,cuts,decay,blood,freshness=.7f,wetness=.5f;
  public void Validate(){
   woundTextures=woundTextures??new WoundSurfaceTextures();
   deepWounds=deepWounds??new BodyDeepWound[0];if(deepWounds.Length>RebirthBodyDeepWounds.Maximum||Array.Exists(deepWounds,w=>w==null))throw new InvalidDataException("Invalid deep wound collection");foreach(var wound in deepWounds)wound.Validate();if(version!=1)throw new InvalidDataException("Unsupported body damage settings");
   foreach(float v in new[]{bruising,abrasion,cuts,decay,blood,freshness,wetness,deepBloodStrength,deepBloodSpread})if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidDataException("Invalid body damage setting");
   foreach(float v in new[]{bloodTint.r,bloodTint.g,bloodTint.b})if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidDataException("Invalid blood tint");bloodTint=new Color(Mathf.Clamp01(bloodTint.r),Mathf.Clamp01(bloodTint.g),Mathf.Clamp01(bloodTint.b),1);
   bruising=Mathf.Clamp01(bruising);abrasion=Mathf.Clamp01(abrasion);cuts=Mathf.Clamp01(cuts);decay=Mathf.Clamp01(decay);blood=Mathf.Clamp01(blood);freshness=Mathf.Clamp01(freshness);wetness=Mathf.Clamp01(wetness);
   deepBloodStrength=Mathf.Clamp01(deepBloodStrength);deepBloodSpread=Mathf.Clamp(deepBloodSpread,0,1.5f);
  }
 }
 public static class RebirthBodyDamage {
  static Texture2D Load(string guid){if(string.IsNullOrEmpty(guid))return null;string path=AssetDatabase.GUIDToAssetPath(guid);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture==null)throw new FileNotFoundException("Body injury map is missing");var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer!=null&&(importer.sRGBTexture||importer.alphaIsTransparency))throw new InvalidDataException("Body injury masks require sRGB and Alpha Is Transparency disabled");return texture;}
  public static void Apply(Material material,BodyDamageSettings settings){
   settings.Validate();var injuries=Load(settings.injuryMaskGuid);var blood=Load(settings.bloodMaskGuid);var woundBlood=Load(settings.deepBloodMaskGuid);
   if(!material.HasProperty("_InjuryAmounts")){if(injuries!=null||blood!=null||woundBlood!=null)throw new InvalidDataException("Body shader does not support injury layers");return;}
   material.SetTexture("_InjuryMask",injuries!=null?injuries:Texture2D.blackTexture);material.SetTexture("_BloodMask",blood!=null?blood:Texture2D.blackTexture);
   material.SetVector("_InjuryAmounts",injuries==null?Vector4.zero:new Vector4(settings.bruising,settings.abrasion,settings.cuts,settings.decay));
   material.SetVector("_BloodAmounts",new Vector4(blood==null?0:settings.blood,settings.freshness,settings.wetness,0));
   if(material.HasProperty("_BloodTint"))material.SetColor("_BloodTint",settings.bloodTint);else if(settings.bloodTint!=Color.white)throw new InvalidDataException("Install the body shader with blood colour controls");
   material.DisableKeyword("_DEEP_BLOOD_STAIN");
   if(woundBlood!=null){
    if(!material.HasProperty("_DeepBloodTex"))throw new InvalidDataException("Install the body shader with wound blood placement support");
    material.SetTexture("_DeepBloodTex",woundBlood);material.SetVector("_DeepBloodSettings",new Vector4(settings.deepBloodStrength,settings.deepBloodSpread,0,0));material.EnableKeyword("_DEEP_BLOOD_STAIN");
   }
  }
  public static BodyDamageSettings Draw(BodyDamageSettings current){
   var edited=JsonUtility.FromJson<BodyDamageSettings>(JsonUtility.ToJson(current));EditorGUI.BeginChangeCheck();
   var injury=(Texture2D)EditorGUILayout.ObjectField("Injury region mask",Load(current.injuryMaskGuid),typeof(Texture2D),false);
   var blood=(Texture2D)EditorGUILayout.ObjectField("Blood region mask",Load(current.bloodMaskGuid),typeof(Texture2D),false);
   var woundBlood=(Texture2D)EditorGUILayout.ObjectField("Blood around wounds",Load(current.deepBloodMaskGuid),typeof(Texture2D),false);
   edited.deepBloodStrength=EditorGUILayout.Slider("Wound blood strength",edited.deepBloodStrength,0,1);edited.deepBloodSpread=EditorGUILayout.Slider("Wound blood spread",edited.deepBloodSpread,0,1.5f);
   EditorGUILayout.LabelField("Optional stain follows placed wound openings. It uses the shared blood colour, freshness and wetness.",EditorStyles.wordWrappedMiniLabel);
   EditorGUILayout.LabelField("Body UV masks: injury R bruises, G abrasions, B cuts, A decay; blood R coverage. Linear data, alpha transparency off. Maps define placement; sliders control strength.",EditorStyles.wordWrappedMiniLabel);
   edited.bruising=EditorGUILayout.Slider("Bruising",edited.bruising,0,1);edited.abrasion=EditorGUILayout.Slider("Abrasions",edited.abrasion,0,1);edited.cuts=EditorGUILayout.Slider("Cuts",edited.cuts,0,1);edited.decay=EditorGUILayout.Slider("Decay",edited.decay,0,1);edited.blood=EditorGUILayout.Slider("Blood",edited.blood,0,1);edited.freshness=EditorGUILayout.Slider("Blood freshness",edited.freshness,0,1);edited.wetness=EditorGUILayout.Slider("Blood wetness",edited.wetness,0,1);
   edited.bloodTint=EditorGUILayout.ColorField("Blood colour tint",edited.bloodTint);EditorGUILayout.LabelField("White retains the source blood colour; tint also affects deep-wound edges.",EditorStyles.wordWrappedMiniLabel);
   int preset=EditorGUILayout.Popup("Apply surface preset",0,new[]{"Choose…","Bruised","Recently injured","Decayed"});
   bool changed=EditorGUI.EndChangeCheck();
   if(preset>0){edited.bruising=preset==1?.8f:.35f;edited.abrasion=preset==1?.1f:.55f;edited.cuts=preset==1?0:.6f;edited.decay=preset==3?.8f:0;edited.blood=preset==1?0:.65f;edited.deepBloodStrength=preset==1?0:1;edited.freshness=preset==3?.1f:.85f;edited.wetness=preset==3?.1f:.7f;changed=true;}
   if(GUILayout.Button("Clear surface damage")){edited.bruising=edited.abrasion=edited.cuts=edited.decay=edited.blood=edited.deepBloodStrength=0;changed=true;}
   EditorGUILayout.HelpBox("Surface layers require authored injury maps. Soft and shaped coverage placement is available above. Deep wound geometry has separate controls below. Tissue and bone material quality remain under review.",MessageType.Info);
   if(!changed)return null;edited.injuryMaskGuid=injury==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(injury));edited.bloodMaskGuid=blood==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(blood));edited.deepBloodMaskGuid=woundBlood==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(woundBlood));edited.Validate();return edited;
  }
  public static void Export(Material material,BodyDamageSettings saved,string folder){
   foreach(bool injury in new[]{true,false}){string guid=injury?saved.injuryMaskGuid:saved.bloodMaskGuid;if(string.IsNullOrEmpty(guid))continue;string source=AssetDatabase.GUIDToAssetPath(guid),target=folder+"/"+(injury?"BodyInjuryMask":"BodyBloodMask")+Path.GetExtension(source);if(!AssetDatabase.CopyAsset(source,target))throw new IOException("Could not copy body injury mask");material.SetTexture(injury?"_InjuryMask":"_BloodMask",AssetDatabase.LoadAssetAtPath<Texture2D>(target));if(injury)saved.injuryMaskGuid=AssetDatabase.AssetPathToGUID(target);else saved.bloodMaskGuid=AssetDatabase.AssetPathToGUID(target);}
   if(!string.IsNullOrEmpty(saved.deepBloodMaskGuid)){
    Load(saved.deepBloodMaskGuid);string source=AssetDatabase.GUIDToAssetPath(saved.deepBloodMaskGuid),target=folder+"/WoundBloodMask"+Path.GetExtension(source);
    if(!AssetDatabase.CopyAsset(source,target))throw new IOException("Could not copy wound blood placement mask");material.SetTexture("_DeepBloodTex",AssetDatabase.LoadAssetAtPath<Texture2D>(target));saved.deepBloodMaskGuid=AssetDatabase.AssetPathToGUID(target);
   }
  }
 }
}
