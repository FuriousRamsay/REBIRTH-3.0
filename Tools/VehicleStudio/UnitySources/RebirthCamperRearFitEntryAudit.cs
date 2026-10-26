using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using Object=UnityEngine.Object;
public static class RebirthCamperRearFitEntryAudit
{
 public static void Run()
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var lines=new List<string>();
  foreach(string variant in new[]{"baseline","no-ramp"})foreach(float x in new[]{-.15f,0,.15f})
  {
   var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperRearFit.Folder+"/rebirthCamper80s.prefab"));root.transform.rotation=Quaternion.Euler(.848603f,0,2.034616f);
   RebirthCamperCampIntegration.SetMode(root,true);var ts=root.GetComponentsInChildren<Transform>(true);var pivot=ts.Single(t=>t.name=="REBIRTHRearDoorPivot");var marker=ts.Single(t=>t.name=="REBIRTHRearDoorOpenPose");pivot.localRotation=marker.localRotation;
   foreach(var rb in root.GetComponentsInChildren<Rigidbody>(true))rb.isKinematic=true;
   foreach(var c in root.GetComponentsInChildren<Collider>(true))
   {
    if(variant=="no-door"&&c.transform==pivot)c.enabled=false;if(variant=="no-lintel"&&c.name=="Camp rear lintel")c.enabled=false;if(variant=="no-roof"&&c.name=="Camp roof")c.enabled=false;if(variant=="no-ramp"&&c.name=="Camp floor ramp")c.enabled=false;
    if(x==0&&variant=="baseline"&&c is BoxCollider box&&c.enabled&&c.gameObject.activeInHierarchy)lines.Add("BOX "+c.name+" center="+box.center.ToString("F4")+" size="+box.size.ToString("F4")+" local="+c.transform.localPosition.ToString("F4"));
   }
   var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=new Vector3(0,-.15f,-4);ground.transform.localScale=new Vector3(10,.3f,10);
   var go=new GameObject("Measured player capsule");var cc=go.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.283322f;cc.center=Vector3.up*.9f;cc.skinWidth=.08f;cc.stepOffset=.1f;cc.slopeLimit=50;cc.minMoveDistance=0;var contacts=go.AddComponent<CamperAuditContacts>();go.transform.position=root.transform.TransformPoint(new Vector3(x,.03f,-5.3f));Physics.SyncTransforms();
   for(int i=0;i<700&&root.transform.InverseTransformPoint(go.transform.position).z< -1.6f;i++)cc.Move(new Vector3(0,-.03f,.012f));
   var end=root.transform.InverseTransformPoint(go.transform.position);lines.Add(variant+" x="+x+" end="+end.ToString("F4")+" PASS="+(end.z>-2)+" contacts="+string.Join(";",contacts.Contacts));Object.DestroyImmediate(go);Object.DestroyImmediate(ground);Object.DestroyImmediate(root);
  }
  File.WriteAllLines(RebirthCamperRearFit.Output+"/entry-measured-audit.txt",lines);
 }
}

