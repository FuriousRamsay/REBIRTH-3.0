using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEngine.Rendering;
namespace RebirthEntityStudio {
[InitializeOnLoad] public static class RebirthRigOverlay {
 const string Key="REBIRTH.RigOverlay.";
 static bool Setting(string n,bool value){return EditorPrefs.GetBool(Key+n,value);}
 static RebirthRigOverlay(){SceneView.duringSceneGui+=Draw;EditorApplication.delayCall+=SceneView.RepaintAll;}
 [MenuItem("Tools/REBIRTH/Rig Overlay/Toggle All")]
 static void Toggle(){EditorPrefs.SetBool(Key+"Enabled",!Setting("Enabled",true));SceneView.RepaintAll();}
 [MenuItem("Tools/REBIRTH/Rig Overlay/Show Both Models")]
 static void Both(){foreach(string key in new[]{"Enabled","Nurse","Moe","Skeleton","Ragdoll"})EditorPrefs.SetBool(Key+key,true);SceneView.RepaintAll();}
 public static IEnumerable<GameObject> Models(){return Resources.FindObjectsOfTypeAll<Animator>().Where(a=>a.gameObject.scene.IsValid()&&a.gameObject.activeInHierarchy).Select(a=>a.gameObject).Where(g=>{string p=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g);return !string.IsNullOrEmpty(p)&&(p.Contains("/RebirthNurseNative/")||p.Contains("/NativeReferences/Moe/"));});}
 static void Draw(SceneView view){
  if(Setting("Enabled",true)&&Event.current.type==EventType.Repaint){
   var color=Handles.color;var matrix=Handles.matrix;var z=Handles.zTest;Handles.zTest=CompareFunction.Always;
   foreach(var model in Models()){
    bool nurse=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model).Contains("/RebirthNurseNative/");if(!Setting(nurse?"Nurse":"Moe",true))continue;
    var origin=model.transform.Find("Origin");if(origin==null)continue;
    if(Setting("Skeleton",true)){Handles.color=nurse?new Color(.1f,1,1):new Color(.5f,1,.2f);Handles.matrix=Matrix4x4.identity;foreach(var bone in origin.GetComponentsInChildren<Transform>(true))if(bone!=origin){Handles.DrawAAPolyLine(Width(),bone.parent.position,bone.position);Handles.SphereHandleCap(0,bone.position,Quaternion.identity,HandleUtility.GetHandleSize(bone.position)*.018f,EventType.Repaint);if(Setting("Names",false))Handles.Label(bone.position,bone.name);}}
    foreach(var collider in model.GetComponentsInChildren<Collider>(true)){
     bool blocker=!collider.transform.IsChildOf(origin);if(!Setting(blocker?"Blocker":"Ragdoll",!blocker))continue;
     Handles.color=blocker?new Color(1,.2f,.9f):nurse?new Color(1,.65f,.05f):new Color(1,.35f,.12f);
     Handles.matrix=collider.transform.localToWorldMatrix;
     if(collider is CapsuleCollider cap)Capsule(cap);else if(collider is BoxCollider box)Handles.DrawWireCube(box.center,box.size);else if(collider is SphereCollider sphere){foreach(var axis in new[]{Vector3.up,Vector3.right,Vector3.forward})Handles.DrawWireDisc(sphere.center,axis,sphere.radius);}
    }
   }
   Handles.color=color;Handles.matrix=matrix;Handles.zTest=z;
  }
  Handles.BeginGUI();bool panel=Setting("Panel",false);GUILayout.BeginArea(new Rect(8,8,panel?270:135,panel?138:26),GUI.skin.box);if(GUILayout.Button(panel?"Rig overlays ▾":"Rig overlays ▸",EditorStyles.miniButton)){EditorPrefs.SetBool(Key+"Panel",!panel);SceneView.RepaintAll();}if(!panel){GUILayout.EndArea();Handles.EndGUI();return;}
  GUILayout.BeginHorizontal();Switch("Enabled",true,"Overlay");Switch("Nurse",true,"Nurse");Switch("Moe",true,"Moe");GUILayout.EndHorizontal();
  GUILayout.BeginHorizontal();Switch("Skeleton",true,"Skeleton");Switch("Ragdoll",true,"Ragdoll colliders");GUILayout.EndHorizontal();
  GUILayout.BeginHorizontal();Switch("Blocker",false,"Large blocker");Switch("Names",false,"Bone names");GUILayout.EndHorizontal();
  GUILayout.BeginHorizontal();GUILayout.Label("Line width",GUILayout.Width(70));float w=EditorPrefs.GetFloat(Key+"Width",3);float next=GUILayout.HorizontalSlider(w,1,6);if(next!=w){EditorPrefs.SetFloat(Key+"Width",next);SceneView.RepaintAll();}GUILayout.EndHorizontal();GUILayout.EndArea();Handles.EndGUI();
 }
 static float Width(){return EditorPrefs.GetFloat(Key+"Width",3);}
 static void Switch(string key,bool initial,string label){bool old=Setting(key,initial);bool value=GUILayout.Toggle(old,label);if(value!=old){EditorPrefs.SetBool(Key+key,value);SceneView.RepaintAll();}}
 static void Capsule(CapsuleCollider c){Vector3 axis=c.direction==0?Vector3.right:c.direction==1?Vector3.up:Vector3.forward;Vector3 u=c.direction==0?Vector3.up:Vector3.right;Vector3 v=Vector3.Cross(axis,u);float half=Mathf.Max(0,c.height*.5f-c.radius);
  foreach(float sign in new[]{-1f,1f}){var ring=new Vector3[49];for(int i=0;i<49;i++){float a=i*Mathf.PI*2/48;ring[i]=c.center+axis*(half*sign)+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*c.radius;}Handles.DrawAAPolyLine(Width(),ring);}
  foreach(var radial in new[]{u,-u,v,-v}){Handles.DrawAAPolyLine(Width(),c.center+axis*half+radial*c.radius,c.center-axis*half+radial*c.radius);foreach(float sign in new[]{-1f,1f}){var arc=new Vector3[25];for(int i=0;i<25;i++){float a=i*Mathf.PI/48;arc[i]=c.center+axis*sign*(half+c.radius*Mathf.Sin(a))+radial*c.radius*Mathf.Cos(a);}Handles.DrawAAPolyLine(Width(),arc);}}
 }
}
}