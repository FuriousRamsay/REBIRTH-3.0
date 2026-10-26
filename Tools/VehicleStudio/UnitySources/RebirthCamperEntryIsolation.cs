using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RebirthCamperEntryIsolation {
 public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperCorrected_20261014/rebirthCamper80s.prefab"));RebirthCamperCampIntegration.SetMode(root,true);var log=new List<string>();var cs=root.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&!(c is WheelCollider)).ToArray();foreach(var c in cs){c.enabled=false;Physics.SyncTransforms();log.Add("Disabled "+c.name+": "+RebirthCamperDefectAudit.Walk(root,1.8f,.7f));c.enabled=true;}foreach(var c in cs.Where(c=>c.name=="Camp roof"||c.name=="Camp rear lintel"))c.enabled=false;log.Add("Both overhead disabled: "+RebirthCamperDefectAudit.Walk(root,1.8f,.7f));File.WriteAllLines(RebirthCamperDefectAudit.O+"/entry-isolation.txt",log);}
}

