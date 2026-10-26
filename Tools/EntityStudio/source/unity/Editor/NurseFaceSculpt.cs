using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;
public static class NurseFaceSculpt {
 const string Native="Assets/EntityStudioGenerated/RebirthNurseNative";public const string BaselinePrefix="BeforeFaceSculptV1_";
 static float G(float x,float y,float cx,float cy,float sx,float sy){float a=(x-cx)/sx,b=(y-cy)/sy;return Mathf.Exp(-.5f*(a*a+b*b));}
 static float S(float a,float b,float v){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));}
 static Vector3 Sculpt(Vector3 p){float x=p.x,y=p.y,ax=Mathf.Abs(x);float mask=S(1.50f,1.525f,y)*(1-S(1.70f,1.72f,y))*(1-S(.070f,.090f,ax))*S(.025f,.050f,p.z);if(mask<=0)return p;
 float dz=.0065f*G(ax,y,.030f,1.650f,.018f,.009f)+.0045f*G(x,y,0,1.651f,.012f,.020f);
 float ex=(ax-.0325f)/.015f,ey=(y-1.628f)/.009f;float eye=Mathf.Sqrt(ex*ex+ey*ey);dz-=.0038f*Mathf.Exp(-Mathf.Pow((eye-1.25f)/.48f,2));
 dz+=.0085f*G(ax,y,.047f,1.611f,.015f,.009f);dz-=.014f*G(ax,y,.051f,1.580f,.016f,.022f);
 // Wasted temples, asymmetric cheek tissue loss and a collapsed nasal tip.
 dz-=.009f*G(ax,y,.065f,1.652f,.012f,.022f);
 dz-=.008f*G(x,y,.043f,1.588f,.009f,.014f);
 dz-=.009f*G(x,y,.004f,1.588f,.010f,.010f);
 dz-=.005f*G(x,y,0,1.554f,.026f,.007f);
 // Her own asymmetric cheek lesion and mouth-corner tissue loss, aligned with her wound texture.
 float lx=(x+.045f)/.0085f,ly=(y-1.605f)/.0065f;float lesion=Mathf.Sqrt(lx*lx+ly*ly);
 dz-=.0075f*Mathf.Exp(-2.8f*lesion*lesion);dz+=.0016f*Mathf.Exp(-Mathf.Pow((lesion-.95f)/.23f,2));
 dz-=.0045f*G(x,y,-.022f,1.548f,.006f,.007f);
 float foldX=.019f+.012f*Mathf.Clamp01((1.594f-y)/.045f);dz-=.0026f*Mathf.Exp(-.5f*Mathf.Pow((ax-foldX)/.0037f,2))*S(1.544f,1.556f,y)*(1-S(1.589f,1.603f,y));
 dz-=.0013f*G(ax,y,.014f,1.674f,.0035f,.014f);dz+=.0014f*G(x,y,0,1.621f,.009f,.024f);
 // Keep the corneal aperture, lip opening and hairline anchored.
 float eyeLock=1-S(.95f,1.18f,eye);float lipLock=G(x,y,0,1.548f,.027f,.005f);float strength=mask*(1-eyeLock)*(1-lipLock*.75f);p.z+=Mathf.Clamp(dz*strength,-.018f,.013f);
 // Own feminine lower-face proportions and desiccated lip retraction; no donor-face sampling.
 p.x+=Mathf.Sign(x)*(.003f*G(ax,y,.052f,1.612f,.018f,.014f)-.0065f*G(ax,y,.055f,1.548f,.019f,.024f))*strength;
 p.y+=(.0025f*G(x,y,0,1.556f,.024f,.004f)-.002f*G(x,y,0,1.540f,.024f,.004f)-.0025f*G(ax,y,.029f,1.547f,.008f,.010f))*mask;
 // Fine forehead creases come from her own normal map; do not form regular horizontal mesh ridges.
 return p;}
 public static void Run(){var model=PrefabUtility.LoadPrefabContents(Native+"/RebirthNurse_Authoring.prefab");var report=new List<string>();try{foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("LOD"))){var target=r.sharedMesh;string path=NurseVisualOptimization.Folder+"/"+BaselinePrefix+r.name+".asset";if(!File.Exists(path))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(target),path);var source=NurseFaceTopology.Baseline(r);if(!source.uv.SequenceEqual(target.uv)||!source.triangles.SequenceEqual(target.triangles)){string targetName=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(target));EditorUtility.CopySerialized(source,target);target.name=targetName;}var v=source.vertices;var n=source.normals;var t=source.tangents;var w=source.boneWeights;int changed=0;float max=0;
 var matrices=r.bones.Select((b,i)=>b.localToWorldMatrix*source.bindposes[i]).ToArray();for(int i=0;i<v.Length;i++){var weight=w[i];int[] ids={weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};float[] ws={weight.weight0,weight.weight1,weight.weight2,weight.weight3};var skin=Matrix4x4.zero;for(int j=0;j<4;j++)for(int row=0;row<4;row++)for(int col=0;col<4;col++)skin[row,col]+=matrices[ids[j]][row,col]*ws[j];var p=skin.MultiplyPoint3x4(v[i]);var q=Sculpt(p);float distance=Vector3.Distance(p,q);if(distance<.000001f)continue;max=Mathf.Max(max,distance);changed++;v[i]=skin.inverse.MultiplyPoint3x4(q);
 var J=Matrix4x4.identity;float h=.0001f;for(int axis=0;axis<3;axis++){var d=Vector3.zero;d[axis]=h;var derivative=(Sculpt(p+d)-Sculpt(p-d))/(2*h);J.SetColumn(axis,new Vector4(derivative.x,derivative.y,derivative.z,0));}var normal=J.inverse.transpose.MultiplyVector(skin.MultiplyVector(n[i])).normalized;n[i]=skin.inverse.MultiplyVector(normal).normalized;var old=t[i];var tangent=skin.inverse.MultiplyVector(J.MultiplyVector(skin.MultiplyVector(new Vector3(old.x,old.y,old.z)))).normalized;t[i]=new Vector4(tangent.x,tangent.y,tangent.z,old.w);}
 NurseFaceTopology.CloseEditedForehead(v,source,r);target.vertices=v;target.normals=n;target.tangents=t;FitActualSurfaceNormals(target,source);target.RecalculateBounds();EditorUtility.SetDirty(target);report.Add(r.name+": sculpted vertices="+changed+" maximum world displacement="+max+" m; independently defined feminine brow, cheek/temple wasting, jaw narrowing, asymmetric lesion and lip retraction. Actual deformed triangle normals welded across coincident UV seams on the edited face; unchanged body shading retained. UVs, topology, weights and bindposes unchanged.");}AssetDatabase.SaveAssets();File.WriteAllLines(NurseVisualOptimization.Out+"/face-sculpt-fit.txt",report.Concat(new[]{"Mesh changes explicitly authorized by user. Existing skeleton, ragdoll, tags and collider fields not edited. Idempotent baseline. Moe identity/geometry is not sampled."}));}finally{PrefabUtility.UnloadPrefabContents(model);}}
 static void FitActualSurfaceNormals(Mesh target,Mesh baseline){
  var p=target.vertices;var old=baseline.vertices;var triangles=target.triangles;var sums=new Vector3[p.Length];var changed=new bool[p.Length];for(int i=0;i<p.Length;i++)changed[i]=(p[i]-old[i]).sqrMagnitude>1e-12f;
  for(int i=0;i<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var normal=Vector3.Cross(p[b]-p[a],p[c]-p[a]);sums[a]+=normal;sums[b]+=normal;sums[c]+=normal;}
  var normals=baseline.normals;int flipped=0;for(int i=0;i<sums.Length;i++)if(Vector3.Dot(sums[i],normals[i])<0){sums[i]=-sums[i];flipped++;}
  foreach(var group in Enumerable.Range(0,p.Length).GroupBy(i=>Mathf.RoundToInt(p[i].x*100000)+","+Mathf.RoundToInt(p[i].y*100000)+","+Mathf.RoundToInt(p[i].z*100000))){if(!group.Any(i=>changed[i]))continue;var sum=Vector3.zero;foreach(int i in group)sum+=sums[i];if(sum.sqrMagnitude<1e-14f)continue;sum/=Mathf.Sqrt(sum.sqrMagnitude);foreach(int i in group)normals[i]=Vector3.Dot(sum,normals[i])<0?-sum:sum;}
  target.normals=normals;target.RecalculateTangents();var tangent=target.tangents;var originalTangent=baseline.tangents;for(int i=0;i<tangent.Length;i++)if(!changed[i])tangent[i]=originalTangent[i];target.tangents=tangent;
  if(normals.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||v.sqrMagnitude<.01f))throw new Exception("Invalid sculpted normal");File.AppendAllText(NurseVisualOptimization.Out+"/face-normal-orientation.txt",target.name+": triangle-derived sums realigned to original outward normal hemisphere="+flipped+"; finite nonzero normals verified.\n");
 }
}
