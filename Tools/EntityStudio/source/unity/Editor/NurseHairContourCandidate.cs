using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class NurseHairContourCandidate {
 public static void Run(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EntityStudioGenerated/RebirthNurseNative/RebirthNurse_Authoring.prefab"));go.GetComponent<Animator>().enabled=false;go.GetComponent<LODGroup>().ForceLOD(0);
  var r=go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(x=>x.name=="LOD0");var m=UnityEngine.Object.Instantiate(r.sharedMesh);r.sharedMesh=m;
  foreach(int a in new[]{0,180}){go.transform.rotation=Quaternion.Euler(0,a,0);NurseVisualOptimization.Render(go,"ContourBaseline_Hair_"+a,0,true);}go.transform.rotation=Quaternion.identity;var originalNormals=m.normals;var originalTangents=m.tangents;var original=m.vertices;var p=(Vector3[])original.Clone();var hair=m.GetTriangles(1);var body=new HashSet<int>(m.GetTriangles(0));var ids=new HashSet<int>(hair);var links=new Dictionary<int,HashSet<int>>();
  foreach(int i in ids)links[i]=new HashSet<int>();for(int k=0;k<hair.Length;k+=3)for(int a=0;a<3;a++)for(int b=0;b<3;b++)if(a!=b)links[hair[k+a]].Add(hair[k+b]);
  // Connect coincident UV seam vertices so smoothing cannot open cracks.
  var groups=Enumerable.Range(0,p.Length).GroupBy(i=>Mathf.RoundToInt(p[i].x*100000)+","+Mathf.RoundToInt(p[i].y*100000)+","+Mathf.RoundToInt(p[i].z*100000)).Select(g=>g.ToArray()).ToArray();
  int moved=0;float max=0;
  for(int step=0;step<8;step++){var next=(Vector3[])p.Clone();foreach(var group in groups){if(group.Any(i=>!ids.Contains(i)||body.Contains(i)))continue;int i=group[0];var w=r.transform.TransformPoint(original[i]);if(w.y>1.66f||w.y<1.30f||Mathf.Abs(w.x)<.09f&&w.z>.015f&&w.y<1.69f)continue;var neighbours=group.SelectMany(v=>links[v]).Distinct().ToArray();if(neighbours.Length<3)continue;var centre=Vector3.zero;foreach(int j in neighbours)centre+=p[j];centre/=neighbours.Length;var value=Vector3.Lerp(p[i],centre,.30f);var delta=value-original[i];float limit=.004f;if(delta.magnitude>limit)value=original[i]+delta.normalized*limit;foreach(int v in group)next[v]=value;}p=next;}
  for(int i=0;i<p.Length;i++){float d=Vector3.Distance(p[i],original[i]);if(d>.000001f)moved++;max=Mathf.Max(max,d);}m.vertices=p;m.normals=originalNormals;m.tangents=originalTangents;m.RecalculateBounds();if(!m.normals.SequenceEqual(originalNormals)||!m.tangents.SequenceEqual(originalTangents))throw new Exception("Source shading attributes changed");foreach(int i in body)if(p[i]!=original[i])throw new Exception("Body geometry moved");
  foreach(int angle in new[]{0,90,180,270}){go.transform.rotation=Quaternion.Euler(0,angle,0);NurseVisualOptimization.Render(go,"ContourCandidate_Hair_"+angle,0,true);}
  File.WriteAllText(NurseVisualOptimization.Out+"/hair-contour-preserved.txt","Candidate only, no asset/prefab writes. Hair-only coincident-seam smoothing; body/shared boundary pinned. Moved vertices="+moved+" maximum local displacement="+max+"; existing topology/UV/weights unchanged. Face/body vertices and ALL source normals/tangents preserved exactly. Requires visual review before adoption.");UnityEngine.Object.DestroyImmediate(go);
 }
}



