using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RebirthCamperTurretClearanceAudit
{
 public static void Run()
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  string path=File.Exists(RebirthCamperTurretClearanceBuild.Folder+"/rebirthCamper80s.prefab")?RebirthCamperTurretClearanceBuild.Folder+"/rebirthCamper80s.prefab":"Assets/REBIRTHCamperTurrets_20261019/rebirthCamper80s.prefab";
  var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
  var rows=new List<string>{"SOURCE "+path};
  foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.IndexOf("roof",StringComparison.OrdinalIgnoreCase)>=0||r.name.IndexOf("camper",StringComparison.OrdinalIgnoreCase)>=0))rows.Add("MESH "+r.name+" bounds="+r.bounds.ToString("F4"));
  foreach(var c in root.GetComponentsInChildren<Collider>(true).Where(c=>c.name=="CamperHull"))rows.Add("HULL "+c.bounds.ToString("F4"));
  for(int i=0;i<4;i++)
  {
   var slot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot"+i);slot.gameObject.SetActive(true);
   var yaw=slot.GetComponentsInChildren<Transform>().Single(t=>t.name=="TurretYaw");var pitch=slot.GetComponentsInChildren<Transform>().Single(t=>t.name=="TurretPitch");var muzzle=slot.GetComponentsInChildren<Transform>().Single(t=>t.name=="TurretMuzzle");var rest=yaw.localRotation;
   foreach(int y in new[]{-125,0,125})foreach(int p in new[]{-75,0,75})
   {
    yaw.localRotation=rest*Quaternion.Euler(0,y,0);pitch.localRotation=Quaternion.Euler(p,0,0);Physics.SyncTransforms();
    var hits=Physics.OverlapCapsule(pitch.position,muzzle.position,.025f,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(root.transform)).ToArray();
    rows.Add("POSE "+i+" yaw="+y+" pitch="+p+" pivot="+pitch.position.ToString("F4")+" muzzle="+muzzle.position.ToString("F4")+" hits="+string.Join(",",hits.Select(c=>c.name)));
   }
   yaw.localRotation=rest;pitch.localRotation=Quaternion.identity;
   var assembly=yaw.parent;var original=assembly.position;
   foreach(float offset in new[]{0f,.20f,.30f,.35f,.40f})
   {
    assembly.position=original+Vector3.right*(i%2==0?-offset:offset);int blocked=0;var names=new HashSet<string>();
    for(int y=-125;y<=125;y+=5)for(int p=-75;p<=75;p+=5)
    {
     yaw.localRotation=rest*Quaternion.Euler(0,y,0);pitch.localRotation=Quaternion.Euler(p,0,0);Physics.SyncTransforms();
     var hits=Physics.OverlapCapsule(pitch.position,muzzle.position,.025f,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(root.transform)).ToArray();
     if(hits.Length>0){blocked++;foreach(var hit in hits)names.Add(hit.name);}
    }
    rows.Add("SWEEP corner="+i+" outward="+offset+" blocked="+blocked+" of1581 hits="+string.Join(",",names));
   }
   assembly.position=original;yaw.localRotation=rest;pitch.localRotation=Quaternion.identity;
  }
  File.WriteAllLines("E:/_Haven/shared/REBIRTHStaged/CamperTurretRefresh_20261019/Artifacts/clearance_audit.txt",rows);
  Debug.Log("PASS clearance audit written");
 }
}
