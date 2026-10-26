using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class RebirthCamperDefectAudit {
 public const string O="E:/_Haven/shared/REBIRTHTemp/CamperDefectCorrection_20261014";
 public const string P="Assets/REBIRTHCamperTallIntegrated_20261014/rebirthCamper80s.prefab";
 public static Transform Find(GameObject o,string n)=>o.GetComponentsInChildren<Transform>(true).Single(t=>t.name==n);
 public static void Run(){Directory.CreateDirectory(O);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(P));RebirthCamperCampIntegration.SetMode(root,true);
 var log=new List<string>();foreach(var c in root.GetComponentsInChildren<Collider>(true))if(c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy)log.Add(c.name+" layer="+c.gameObject.layer+" "+c.bounds);
 foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="camper_body"))foreach(var m in r.sharedMaterials){log.Add(m.name+" shader="+m.shader.name);foreach(var prop in m.GetTexturePropertyNames()){var t=m.GetTexture(prop);if(t)log.Add(prop+" "+AssetDatabase.GetAssetPath(t)+" "+t.width+"x"+t.height);}}
 foreach(var height in new[]{1.72f,1.8f})foreach(var step in new[]{.1f,.3f,.5f,.7f})log.Add(Walk(root,height,step));
 File.WriteAllLines(O+"/baseline-audit.txt",log);
 }
 public static string Walk(GameObject root,float height,float step){
 var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="AuditGround";ground.transform.position=new Vector3(0,-.15f,-4);ground.transform.localScale=new Vector3(10,.3f,10);
 var go=new GameObject("AuditPlayer");var cc=go.AddComponent<CharacterController>();cc.height=height;cc.radius=height==1.8f?.35f:.28665f;cc.center=Vector3.up*(height/2);cc.skinWidth=.08f;cc.stepOffset=step;cc.slopeLimit=50;cc.minMoveDistance=0;
 var contacts=go.AddComponent<CamperAuditContacts>();go.transform.position=new Vector3(0,.09f,-5.5f);Physics.SyncTransforms();CollisionFlags flags=0;
 for(int i=0;i<600&&go.transform.position.z< -1.6f;i++)flags|=cc.Move(new Vector3(0,-.03f,.012f));
 string result="Controller h="+height+" step="+step+" final="+go.transform.position+" flags="+flags+" entry="+(go.transform.position.z>-2f)+" contacts="+string.Join(";",contacts.Contacts);
 Object.DestroyImmediate(go);Object.DestroyImmediate(ground);return result;
 }
}
public class CamperAuditContacts:MonoBehaviour {public HashSet<string> Contacts=new HashSet<string>();void OnControllerColliderHit(ControllerColliderHit hit){if(Mathf.Abs(hit.normal.y)<.8f)Contacts.Add(hit.collider.name+" n="+hit.normal+" y="+hit.point.y.ToString("F2"));}}

