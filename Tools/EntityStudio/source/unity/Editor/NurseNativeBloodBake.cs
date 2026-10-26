using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class NurseNativeBloodBake {
 const string Native="Assets/EntityStudioGenerated/RebirthNurseNative";const string Folder=NurseVisualOptimization.Folder;const string Out=NurseVisualOptimization.Out;
 static Texture2D Read(string p){var t=new Texture2D(2,2,TextureFormat.RGBA32,false,true);t.LoadImage(File.ReadAllBytes(p));return t;}
 static float S(float a,float b,float x){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));}
 static void Save(Color[] p,int size,string path){var t=new Texture2D(size,size,TextureFormat.RGBA32,false,true);t.SetPixels(p);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
 public static void Run(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var nurse=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Native+"/RebirthNurse_Authoring.prefab"));nurse.GetComponent<Animator>().enabled=false;
  var moe=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NativeReferences/Moe/PrefabInstance/ZMoe.prefab"));moe.GetComponent<Animator>().enabled=false;
  var body=nurse.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="LOD0");var source=moe.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="LOD0");
  var sourceMesh=NurseAnatomyRender.WorldSkin(source);var bodyTriangles=sourceMesh.GetTriangles(0);sourceMesh.subMeshCount=1;sourceMesh.SetTriangles(bodyTriangles,0);var sourceUV=sourceMesh.uv;var sourceTriangles=sourceMesh.triangles;var collisionObject=new GameObject("Native blood transfer surface");var collider=collisionObject.AddComponent<MeshCollider>();collider.sharedMesh=sourceMesh;
  var bones=source.bones.Where(b=>b!=null).GroupBy(b=>b.name).ToDictionary(g=>g.Key,g=>g.First());
  var texture=Read(AssetDatabase.GetAssetPath(source.sharedMaterial.GetTexture("_Albedo")));var packedSource=Read(AssetDatabase.GetAssetPath(source.sharedMaterial.GetTexture("_RMOE")));
  var mesh=body.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var weights=mesh.boneWeights;var uv=mesh.uv;var triangles=mesh.triangles;
  var mapped=new Vector3[vertices.Length];var directions=new Vector3[vertices.Length];var positions=new Vector3[vertices.Length];
  for(int i=0;i<vertices.Length;i++){var own=body.bones[weights[i].boneIndex0];positions[i]=body.transform.TransformPoint(vertices[i]);var normal=body.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
   if(bones.TryGetValue(own.name,out var reference)){mapped[i]=reference.TransformPoint(own.InverseTransformPoint(positions[i]));directions[i]=reference.TransformDirection(own.InverseTransformDirection(normal)).normalized;}else{mapped[i]=positions[i]*1.035f;directions[i]=normal;}}
  const int size=1024;var stain=new Color[size*size];var packed=new Color[size*size];int hits=0,painted=0;
  for(int j=0;j<triangles.Length;j+=3){int a=triangles[j],b=triangles[j+1],c=triangles[j+2];var aa=uv[a]*size;var bb=uv[b]*size;var cc=uv[c]*size;float d=(bb.y-cc.y)*(aa.x-cc.x)+(cc.x-bb.x)*(aa.y-cc.y);if(Mathf.Abs(d)<1e-8f)continue;
   for(int y=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(aa.y,Mathf.Min(bb.y,cc.y))));y<=Mathf.Min(size-1,Mathf.CeilToInt(Mathf.Max(aa.y,Mathf.Max(bb.y,cc.y))));y++)for(int x=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(aa.x,Mathf.Min(bb.x,cc.x))));x<=Mathf.Min(size-1,Mathf.CeilToInt(Mathf.Max(aa.x,Mathf.Max(bb.x,cc.x))));x++){
    float u=((bb.y-cc.y)*(x+.5f-cc.x)+(cc.x-bb.x)*(y+.5f-cc.y))/d,v=((cc.y-aa.y)*(x+.5f-cc.x)+(aa.x-cc.x)*(y+.5f-cc.y))/d,w=1-u-v;if(u<0||v<0||w<0)continue;
    var p=positions[a]*u+positions[b]*v+positions[c]*w;
    // Keep eyes, forehead, crown and hair clean. Mouth/chin/neck remain eligible.
    if(p.y>1.605f||(p.y>1.28f&&p.y<1.70f&&Mathf.Abs(p.x)<.24f&&(p.z<-.08f||Mathf.Abs(p.x)>.075f)))continue;
    var point=mapped[a]*u+mapped[b]*v+mapped[c]*w;var normal=(directions[a]*u+directions[b]*v+directions[c]*w).normalized;RaycastHit hit;
    if(!collider.Raycast(new Ray(point+normal*.25f,-normal),out hit,.50f)&&!collider.Raycast(new Ray(point-normal*.25f,normal),out hit,.50f))continue;hits++;
    // Exported game meshes store half precision UVs, unsupported by hit.textureCoord.
    // Read their float conversion once and interpolate the hit triangle ourselves.
    int hitIndex=hit.triangleIndex*3;var bary=hit.barycentricCoordinate;var coord=sourceUV[sourceTriangles[hitIndex]]*bary.x+sourceUV[sourceTriangles[hitIndex+1]]*bary.y+sourceUV[sourceTriangles[hitIndex+2]]*bary.z;
    var sample=texture.GetPixelBilinear(coord.x,coord.y);float redness=sample.r-Mathf.Max(sample.g,sample.b);float amount=S(.035f,.15f,redness)*S(1.35f,2.15f,sample.r/Mathf.Max(.015f,Mathf.Max(sample.g,sample.b)));amount*=1-S(1.34f,1.40f,p.y)*(1-S(1.49f,1.54f,p.y));if(amount<.02f)continue;
    int index=y*size+x;sample.a=amount*.88f;stain[index]=sample;packed[index]=packedSource.GetPixelBilinear(coord.x,coord.y);painted++;
   }
  }
  if(hits<1000||painted<300)throw new Exception("Native blood transfer did not find enough source surface detail");var decal=new Texture2D(size,size,TextureFormat.RGBA32,false,true);decal.SetPixels(stain);decal.Apply();
  foreach(string tier in new[]{"","_Feral"}){string path=Folder+"/Atlas_Albedo"+tier+".png";var baseTexture=Read(path);var colors=baseTexture.GetPixels();for(int y=0;y<2048;y++)for(int x=0;x<2048;x++){var sample=decal.GetPixelBilinear((x+.5f)/2048,(y+.5f)/2048);int index=y*2048+x;float alpha=colors[index].a;colors[index]=Color.Lerp(colors[index],sample,sample.a);colors[index].a=alpha;}Save(colors,2048,path);}
  var packedDecal=new Texture2D(size,size,TextureFormat.RGBA32,false,true);packedDecal.SetPixels(packed);packedDecal.Apply();
  foreach(string tier in new[]{"Normal","Feral","Special"}){string path=Folder+"/Atlas_RMOE_"+tier+".png";var colors=Read(path).GetPixels();for(int y=0;y<512;y++)for(int x=0;x<512;x++){var coord=new Vector2((x+.5f)/512,(y+.5f)/512);float amount=decal.GetPixelBilinear(coord.x,coord.y).a;var native=packedDecal.GetPixelBilinear(coord.x,coord.y);int index=y*512+x;float emission=colors[index].a;colors[index]=Color.Lerp(colors[index],native,amount);if(tier!="Special")colors[index].a=emission;}Save(colors,512,path);}
  File.WriteAllText(Out+"/native-blood-transfer.txt","Native Moe BODY SUBMESH ONLY albedo/RMOE transferred by matching named bone local space and raycasting the real source mesh. Samples hit="+hits+"; blood texels="+painted+" at 1024 working resolution. No invented red noise; native cloth/contact and wound patterns retained as stain colour, native blood roughness/metal/AO; no new geometry or collider changes.\n");
  UnityEngine.Object.DestroyImmediate(collisionObject);UnityEngine.Object.DestroyImmediate(sourceMesh);UnityEngine.Object.DestroyImmediate(moe);UnityEngine.Object.DestroyImmediate(nurse);AssetDatabase.SaveAssets();Debug.Log("NURSE_NATIVE_BLOOD_BAKED");
 }
}

