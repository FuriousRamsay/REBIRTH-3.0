using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Anatomy-registered reconstruction from the native donor, with matching geometry and material bake.
// Runs in the isolated authoring project; promotion is deliberately a separate operation.
public static class NurseNativeFaceReconstruction {
 const string Native="Assets/EntityStudioGenerated/RebirthNurseNative";
 const string Folder=NurseVisualOptimization.Folder, Out=NurseVisualOptimization.Out;
 static Mesh donor; static MeshCollider donorCollider;
 static Vector3[] dv,dn; static Vector4[] dt; static Vector2[] du; static int[] di;
 static float depthOffset;
 static float S(float a,float b,float x){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));}
 static Vector3 Registered(Vector3 p){float scale=1.06f+.20f*S(.040f,.075f,Mathf.Abs(p.x));float forehead=S(1.650f,1.675f,p.y);return new Vector3(p.x*scale*Mathf.Lerp(1,.25f,forehead),Mathf.Lerp(p.y+.08866f,Mathf.Min(p.y+.08866f,1.763f),forehead),.6f);}
 static bool Sample(Vector3 p,out RaycastHit hit){return donorCollider.Raycast(new Ray(Registered(p),Vector3.back),out hit,1.5f);}
 static float Mask(Vector3 p){float amount=S(1.505f,1.535f,p.y)*(1-S(1.695f,1.720f,p.y))*(1-S(.062f,.082f,Mathf.Abs(p.x)))*S(.025f,.050f,p.z);if(p.x<0){float ex=(p.x+.0338f)/.0125f,ey=(p.y-1.628f)/.0065f;amount*=S(.80f,1.15f,ex*ex+ey*ey);}return amount;}
 static Texture2D Read(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false,true);t.LoadImage(File.ReadAllBytes(path));return t;}
 static void Save(Texture2D t,string path){File.WriteAllBytes(path,t.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
 static void Backup(string path){string copy=Folder+"/BeforeNativeFaceV1_"+Path.GetFileName(path);if(!File.Exists(copy))AssetDatabase.CopyAsset(path,copy);}
 static Vector3 Point(Vector3 p){float amount=Mask(p);if(amount<=0)return p;RaycastHit hit;if(!Sample(p,out hit))return p;float z=hit.point.z/1.06f+depthOffset;float delta=Mathf.Clamp(z-p.z,-.035f,.025f);p.z+=delta*amount;return p;}
 public static void Run(){try{Execute();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void RestoreRejectedCandidate(){
  foreach(string name in new[]{"RebirthNurse_LOD0_0.asset","RebirthNurse_LOD1_0.asset","RebirthNurse_LOD2_0.asset"}){var source=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/BeforeNativeFaceV1_"+name);var target=AssetDatabase.LoadAssetAtPath<Mesh>(Native+"/"+name);if(source==null)throw new Exception("Missing pre-candidate mesh "+name);EditorUtility.CopySerialized(source,target);EditorUtility.SetDirty(target);}
  foreach(string map in new[]{"Atlas_Albedo.png","Atlas_Albedo_Feral.png","Atlas_Normal.png","Atlas_RMOE_Normal.png","Atlas_RMOE_Feral.png","Atlas_RMOE_Special.png"}){File.Copy(Folder+"/BeforeNativeFaceV1_"+map,Folder+"/"+map,true);AssetDatabase.ImportAsset(Folder+"/"+map,ImportAssetOptions.ForceSynchronousImport);}
  AssetDatabase.SaveAssets();NurseEyeAlignment.Run();File.WriteAllText(Out+"/native-face-candidate-rejected.txt","Direct donor face reconstruction rejected by owner: Nurse must retain a distinct female identity. Pre-candidate meshes/maps restored; both eye patches restored. Installed mod and D: project were never replaced by the rejected candidate. Moe is a quality reference, not a replacement face.\n");
 }
 static void Execute(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var nurse=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Native+"/RebirthNurse_Authoring.prefab"));
  var moe=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NativeReferences/Moe/PrefabInstance/ZMoe.prefab"));
  nurse.GetComponent<Animator>().enabled=false;moe.GetComponent<Animator>().enabled=false;
  var source=moe.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="LOD0");
  donor=NurseAnatomyRender.WorldSkin(source);var bodyTriangles=donor.GetTriangles(0);donor.subMeshCount=1;donor.SetTriangles(bodyTriangles,0);
  dv=donor.vertices;dn=donor.normals;dt=donor.tangents;du=donor.uv;di=donor.triangles;
  var probe=new GameObject("Native donor face registration");donorCollider=probe.AddComponent<MeshCollider>();donorCollider.sharedMesh=donor;
  var r0=nurse.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="LOD0");
  var base0=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+NurseFaceSculpt.BaselinePrefix+"LOD0.asset");
  var prior=r0.sharedMesh;r0.sharedMesh=base0;var world=NurseAnatomyRender.WorldSkin(r0);r0.sharedMesh=prior;
  var targetProbe=new GameObject("Target face registration").AddComponent<MeshCollider>();targetProbe.sharedMesh=world;
  RaycastHit th,sh;var nose=new Vector3(0,1.603f,.6f);
  if(!targetProbe.Raycast(new Ray(nose,Vector3.back),out th,1.5f)||!Sample(nose,out sh))throw new Exception("Nasal registration missed");
  depthOffset=th.point.z-sh.point.z/1.06f;
  Bake(world,source.sharedMaterial);
  var reports=new List<string>{"Native Moe donor body surface; x registration 1.06 to 1.26, y offset 0.08866 m; nasal depth offset="+depthOffset};
  foreach(var renderer in nurse.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("LOD"))){
   var target=renderer.sharedMesh;Backup(AssetDatabase.GetAssetPath(target));
   var baseline=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+NurseFaceSculpt.BaselinePrefix+renderer.name+".asset");
   var vertices=baseline.vertices;var normals=baseline.normals;var tangents=baseline.tangents;var weights=baseline.boneWeights;
   var matrices=renderer.bones.Select((b,i)=>b.localToWorldMatrix*baseline.bindposes[i]).ToArray();int changed=0;float max=0;
   for(int i=0;i<vertices.Length;i++){
    var w=weights[i];int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] ws={w.weight0,w.weight1,w.weight2,w.weight3};
    var skin=Matrix4x4.zero;for(int k=0;k<4;k++)for(int a=0;a<4;a++)for(int b=0;b<4;b++)skin[a,b]+=matrices[ids[k]][a,b]*ws[k];
    var p=skin.MultiplyPoint3x4(vertices[i]);var q=Point(p);float delta=Vector3.Distance(p,q);if(delta<.000001f)continue;
    changed++;max=Mathf.Max(max,delta);vertices[i]=skin.inverse.MultiplyPoint3x4(q);
    // Transform the original shading frame with the local anatomical deformation, preserving UV seams.
    var J=Matrix4x4.identity;float h=.00035f;for(int axis=0;axis<2;axis++){var d=Vector3.zero;d[axis]=h;var derivative=(Point(p+d)-Point(p-d))/(2*h);J.SetColumn(axis,new Vector4(derivative.x,derivative.y,derivative.z,0));}
    // The fitted surface is a height field. Its thickness axis must remain invertible;
    // differentiating an absolute depth projection along Z would produce a zero column.
    J.SetColumn(2,new Vector4(0,0,1,0));
    normals[i]=skin.inverse.MultiplyVector(J.inverse.transpose.MultiplyVector(skin.MultiplyVector(normals[i]))).normalized;
    var t=tangents[i];var v=skin.inverse.MultiplyVector(J.MultiplyVector(skin.MultiplyVector(new Vector3(t.x,t.y,t.z)))).normalized;tangents[i]=new Vector4(v.x,v.y,v.z,t.w);
   }
   target.vertices=vertices;target.normals=normals;target.tangents=tangents;target.RecalculateBounds();EditorUtility.SetDirty(target);
   reports.Add(renderer.name+": reconstructed vertices="+changed+", max displacement="+max+" m; original skin weights, UVs and triangle layout retained");
  }
  AssetDatabase.SaveAssets();
  UnityEngine.Object.DestroyImmediate(targetProbe.gameObject);UnityEngine.Object.DestroyImmediate(world);UnityEngine.Object.DestroyImmediate(probe);UnityEngine.Object.DestroyImmediate(donor);
  UnityEngine.Object.DestroyImmediate(nurse);UnityEngine.Object.DestroyImmediate(moe);
  File.WriteAllLines(Out+"/native-face-reconstruction.txt",reports);
  NurseEyeAlignment.Run();RemoveDestroyedEyePatch();Review();Debug.Log("NURSE_NATIVE_FACE_RECONSTRUCTION_REVIEW_READY");
 }
 static void Bake(Mesh world,Material donorMaterial){
  var albedo=Read(AssetDatabase.GetAssetPath(donorMaterial.GetTexture("_Albedo")));
  var normal=Read(AssetDatabase.GetAssetPath(donorMaterial.GetTexture("_Normal")));
  var packed=Read(AssetDatabase.GetAssetPath(donorMaterial.GetTexture("_RMOE")));
  foreach(string map in new[]{"Atlas_Albedo.png","Atlas_Albedo_Feral.png","Atlas_Normal.png","Atlas_RMOE_Normal.png","Atlas_RMOE_Feral.png","Atlas_RMOE_Special.png"})Backup(Folder+"/"+map);
  const int n=2048;var mask=new float[n*n];var paint=new Color[n*n];var normals=new Color[n*n];var pm=new Color[512*512];var pw=new float[512*512];
  var v=world.vertices;var uv=world.uv;var wn=world.normals;var wt=world.tangents;var tri=world.GetTriangles(0);int hits=0;
  for(int j=0;j<tri.Length;j+=3){int a=tri[j],b=tri[j+1],c=tri[j+2];if(Mathf.Max(v[a].y,Mathf.Max(v[b].y,v[c].y))<1.505f||Mathf.Min(v[a].y,Mathf.Min(v[b].y,v[c].y))>1.720f)continue;
   var aa=uv[a]*n;var bb=uv[b]*n;var cc=uv[c]*n;float det=(bb.y-cc.y)*(aa.x-cc.x)+(cc.x-bb.x)*(aa.y-cc.y);if(Mathf.Abs(det)<1e-8f)continue;
   int xmin=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(aa.x,Mathf.Min(bb.x,cc.x)))),xmax=Mathf.Min(n-1,Mathf.CeilToInt(Mathf.Max(aa.x,Mathf.Max(bb.x,cc.x))));
   int ymin=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(aa.y,Mathf.Min(bb.y,cc.y)))),ymax=Mathf.Min(n-1,Mathf.CeilToInt(Mathf.Max(aa.y,Mathf.Max(bb.y,cc.y))));
   for(int y=ymin;y<=ymax;y++)for(int x=xmin;x<=xmax;x++){
    float u=((bb.y-cc.y)*(x+.5f-cc.x)+(cc.x-bb.x)*(y+.5f-cc.y))/det,w=((cc.y-aa.y)*(x+.5f-cc.x)+(aa.x-cc.x)*(y+.5f-cc.y))/det,z=1-u-w;if(u<0||w<0||z<0)continue;
    var p=v[a]*u+v[b]*w+v[c]*z;float amount=Mask(p);if(amount<.001f)continue;RaycastHit hit;if(!Sample(p,out hit))continue;
    int k=hit.triangleIndex*3;var bary=hit.barycentricCoordinate;var coord=du[di[k]]*bary.x+du[di[k+1]]*bary.y+du[di[k+2]]*bary.z;
    int i=y*n+x;mask[i]=amount;paint[i]=albedo.GetPixelBilinear(coord.x,coord.y);
    // Both face charts have horizontally aligned front-facing tangent frames. Convert donor tangent detail through world space.
    var sn=(dn[di[k]]*bary.x+dn[di[k+1]]*bary.y+dn[di[k+2]]*bary.z).normalized;
    var st=dt[di[k]]*bary.x+dt[di[k+1]]*bary.y+dt[di[k+2]]*bary.z;var tangent=new Vector3(st.x,st.y,st.z).normalized;var bitangent=Vector3.Cross(sn,tangent)*Mathf.Sign(st.w);
    var nc=normal.GetPixelBilinear(coord.x,coord.y);var tn=new Vector3(nc.r*2-1,nc.g*2-1,nc.b*2-1).normalized;
    var detail=tangent*tn.x+bitangent*tn.y;var targetN=(wn[a]*u+wn[b]*w+wn[c]*z).normalized;
    var tt=wt[a]*u+wt[b]*w+wt[c]*z;var tx=new Vector3(tt.x,tt.y,tt.z).normalized;var ty=Vector3.Cross(targetN,tx)*Mathf.Sign(tt.w);
    var converted=new Vector3(Vector3.Dot(detail,tx),Vector3.Dot(detail,ty),Mathf.Max(.25f,tn.z)).normalized;normals[i]=new Color(converted.x*.5f+.5f,converted.y*.5f+.5f,converted.z*.5f+.5f,1);
    int pi=(y/4)*512+x/4;float weight=amount/16f;pm[pi]+=packed.GetPixelBilinear(coord.x,coord.y)*weight;pw[pi]+=weight;hits++;
   }
  }
  foreach(string map in new[]{"Atlas_Albedo.png","Atlas_Albedo_Feral.png","Atlas_Normal.png"}){
   var texture=Read(Folder+"/BeforeNativeFaceV1_"+map);var pixels=texture.GetPixels();for(int i=0;i<pixels.Length;i++)if(mask[i]>0){float alpha=pixels[i].a;pixels[i]=Color.Lerp(pixels[i],map.Contains("Normal")?normals[i]:paint[i],mask[i]);pixels[i].a=alpha;}texture.SetPixels(pixels);texture.Apply();Save(texture,Folder+"/"+map);
  }
  foreach(string tier in new[]{"Normal","Feral","Special"}){string map="Atlas_RMOE_"+tier+".png";var texture=Read(Folder+"/BeforeNativeFaceV1_"+map);var pixels=texture.GetPixels();for(int i=0;i<pixels.Length;i++)if(pw[i]>0){var c=pm[i]/pw[i];if(tier!="Normal")c.a=pixels[i].a;pixels[i]=Color.Lerp(pixels[i],c,Mathf.Clamp01(pw[i]));}texture.SetPixels(pixels);texture.Apply();Save(texture,Folder+"/"+map);}
  File.WriteAllText(Out+"/native-face-material-bake.txt","Anatomically registered native Moe albedo/normal/RMOE bake; texels="+hits+"; includes native wound and cavity surfaces. Albedo/normal 2048, RMOE512. Feral/special tier emission retained. Native normal damage and wound roughness transferred together with corresponding geometry.\n");
 }
 static void RemoveDestroyedEyePatch(){foreach(string path in new[]{Native+"/RebirthNurse.prefab",Native+"/RebirthNurse_Authoring.prefab"}){var go=PrefabUtility.LoadPrefabContents(path);foreach(var eye in go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name=="NormalEye"||r.name=="FeralEye")){var mesh=eye.sharedMesh;var points=mesh.vertices;var indices=mesh.triangles;var keep=new List<int>();for(int i=0;i<indices.Length;i+=3){float x=(points[indices[i]].x+points[indices[i+1]].x+points[indices[i+2]].x)/3;if(eye.transform.TransformPoint(new Vector3(x,0,0)).x<0)keep.AddRange(new[]{indices[i],indices[i+1],indices[i+2]});}mesh.triangles=keep.ToArray();EditorUtility.SetDirty(mesh);}PrefabUtility.SaveAsPrefabAsset(go,path);PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();}
 static void Review(){foreach(int angle in new[]{0,-30,30}){
   foreach(bool isMoe in new[]{true,false}){
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    string path=isMoe?"Assets/NativeReferences/Moe/PrefabInstance/ZMoe.prefab":Native+"/RebirthNurse_Authoring.prefab";
    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));go.GetComponent<Animator>().enabled=false;go.GetComponent<LODGroup>().ForceLOD(0);
    // Rotate about the face centre rather than the entity origin to keep the comparison framing stable.
    var pivot=new Vector3(0,isMoe?1.72f:1.64f,0);go.transform.RotateAround(pivot,Vector3.up,angle);
    NurseVisualOptimization.Render(go,(isMoe?"MoeFace_Reconstruction_":"NurseFace_Reconstruction_")+angle,0,true);
    UnityEngine.Object.DestroyImmediate(go);
   }
  }}
}
