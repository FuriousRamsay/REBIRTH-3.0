using System;using System.Linq;using UnityEditor;using UnityEngine;
namespace RebirthEntityStudio {
 public static class RebirthBodyDeepWoundControls {
  public static bool SetInterior(BodyDamageSettings settings,BodyDeepWound wound,int choice){
   if(choice<0||choice>3)throw new ArgumentOutOfRangeException("choice");
   if(choice>=2&&!RebirthTorsoAnatomyCache.AssignDefault(settings))return false;
   wound.exposeBone=choice==1;wound.torsoInterior=choice>=2?(TorsoWoundInterior)(choice-1):TorsoWoundInterior.None;return true;
  }
  public static bool ApplyPreset(BodyDamageSettings settings,BodyDeepWound wound,int preset){
   int choice=preset==1?0:preset==2?2:preset==3?3:-1;if(choice<0)throw new ArgumentOutOfRangeException("preset");if(!SetInterior(settings,wound,choice))return false;
   wound.width=preset==1?.044f:preset==2?.1f:.12f;wound.height=preset==1?.11f:.18f;wound.depth=preset==1?.027f:preset==2?.05f:.07f;wound.Validate();return true;
  }
  public static BodyDamageSettings Draw(BodyDamageSettings current,ref int selected){
   var wounds=current.deepWounds??new BodyDeepWound[0];EditorGUILayout.LabelField("Deep wounds",EditorStyles.boldLabel);
   if(wounds.Length==0){EditorGUILayout.LabelField("Enable deep wound placement and Ctrl-click the body to add one.",EditorStyles.wordWrappedMiniLabel);return null;}
   selected=EditorGUILayout.Popup("Selected wound",Mathf.Clamp(selected,0,wounds.Length-1),Enumerable.Range(1,wounds.Length).Select(i=>"Wound "+i).ToArray());
   var edited=JsonUtility.FromJson<BodyDamageSettings>(JsonUtility.ToJson(current));var wound=edited.deepWounds[selected];bool missing=false;
   EditorGUI.BeginChangeCheck();int previous=wound.torsoInterior!=TorsoWoundInterior.None?(int)wound.torsoInterior+1:wound.exposeBone?1:0;
   int choice=EditorGUILayout.Popup("Interior",previous,new[]{"Tissue lining","Shin bone","Ribs","Abdominal contents"});
   if(choice!=previous&&!SetInterior(edited,wound,choice))missing=true;
   bool torso=wound.torsoInterior!=TorsoWoundInterior.None;
   wound.width=EditorGUILayout.Slider("Width (m)",wound.width,.008f,torso?.3f:.12f);wound.height=EditorGUILayout.Slider("Height (m)",wound.height,.012f,torso?.4f:.24f);wound.depth=EditorGUILayout.Slider("Depth (m)",wound.depth,.002f,torso?.12f:.05f);wound.rotation=EditorGUILayout.Slider("Rotation",wound.rotation,-180,180);
   wound.tissueTint=EditorGUILayout.ColorField("Tissue colour tint",wound.tissueTint);wound.tissueSmoothness=EditorGUILayout.Slider("Tissue shine",wound.tissueSmoothness,0,1);wound.tissueTileSize=EditorGUILayout.Slider("Tissue texture size (m)",wound.tissueTileSize,.005f,.2f);
   wound.bloodCoverage=EditorGUILayout.Slider("Interior blood coverage",wound.bloodCoverage,0,1);
   edited.woundTextures=edited.woundTextures??new WoundSurfaceTextures();RebirthWoundSurfaceTextures.Draw(edited.woundTextures);
   if(wound.exposeBone||wound.torsoInterior==TorsoWoundInterior.Ribs)wound.boneBloodCoverage=EditorGUILayout.Slider("Bone blood coverage",wound.boneBloodCoverage,0,1);
   EditorGUILayout.LabelField("Blood colour, freshness and wetness follow the surface damage controls above.",EditorStyles.wordWrappedMiniLabel);
   if(wound.exposeBone||wound.torsoInterior==TorsoWoundInterior.Ribs){wound.boneTint=EditorGUILayout.ColorField("Bone colour tint",wound.boneTint);wound.boneSmoothness=EditorGUILayout.Slider("Bone shine",wound.boneSmoothness,0,1);}
   int preset=EditorGUILayout.Popup("Apply wound preset",0,new[]{"Choose…","Tissue tear","Rib exposure","Abdominal opening"});bool changed=EditorGUI.EndChangeCheck();
   if(preset>0&&!ApplyPreset(edited,wound,preset))missing=true;
   if(missing)EditorGUILayout.HelpBox("Torso anatomy assets are not installed. The selected interior was retained.",MessageType.Warning);
   EditorGUILayout.LabelField("Place rib wounds on the chest and abdominal wounds over the belly. Depth and body thickness determine which internal structures can be exposed.",EditorStyles.wordWrappedMiniLabel);
   if(GUILayout.Button("Remove selected wound")){int removed=selected;edited.deepWounds=edited.deepWounds.Where((w,i)=>i!=removed).ToArray();selected=Mathf.Max(0,selected-1);changed=true;}
   if(GUILayout.Button("Clear deep wounds")){edited.deepWounds=new BodyDeepWound[0];selected=0;changed=true;}
   return changed?edited:null;
  }
 }
}
