using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class RebirthCamperMeshCount {
 public static void RunBatch(){
  var lines=new List<string>();
  foreach(var path in new[]{"Assets/REBIRTHCamperWrapGenerated/BundleCandidates/3a11c6dfad874047ad18f6ffddec5d7b/rebirthCamper80s.prefab","Assets/Perfectvertex/80sCamperTruck/Prefabs/assembled_vehicle.prefab"}){
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new System.Exception("Missing prefab "+path);
   long all=0;int renderers=0;var seen=new HashSet<Mesh>();long unique=0;
   foreach(var r in prefab.GetComponentsInChildren<Renderer>(true)){
    Mesh m=null;var skin=r as SkinnedMeshRenderer;if(skin!=null)m=skin.sharedMesh;else{var f=r.GetComponent<MeshFilter>();if(f!=null)m=f.sharedMesh;}
    if(m==null)continue;long count=0;for(int s=0;s<m.subMeshCount;s++)if(m.GetTopology(s)==MeshTopology.Triangles)count+=(long)m.GetIndexCount(s)/3;
    all+=count;renderers++;if(seen.Add(m))unique+=count;lines.Add(r.name+": "+count+" triangles; enabled="+r.enabled+"; activeSelf="+r.gameObject.activeSelf);
   }
   lines.Add("TOTAL path="+path+" triangleInstancesAllRenderers="+all+" uniqueMeshTriangles="+unique+" renderers="+renderers+" (includes inactive children and all LOD meshes)");
  }
  File.WriteAllLines("E:/_Haven/shared/REBIRTHTemp/CamperMeshCount_20261013.txt",lines);
 }
}
