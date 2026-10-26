using System;
using System.IO;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class NativeCrownRing {public float drop,sourceX,sourceZ,targetX,targetZ,scaleX,scaleZ;}
 [Serializable] public sealed class NativeCrownCage {public float sourceTop,targetTop;public NativeCrownRing[] rings;
  public Vector3 Warp(Vector3 p){float d=sourceTop-p.y;int index=0;while(index<rings.Length-2&&d>rings[index+1].drop)index++;var a=rings[index];var b=rings[index+1];float t=Mathf.InverseLerp(a.drop,b.drop,d);float sx=Mathf.Lerp(a.scaleX,b.scaleX,t),sz=Mathf.Lerp(a.scaleZ,b.scaleZ,t);return new Vector3((p.x-Mathf.Lerp(a.sourceX,b.sourceX,t))*sx+Mathf.Lerp(a.targetX,b.targetX,t),p.y+targetTop-sourceTop,(p.z-Mathf.Lerp(a.sourceZ,b.sourceZ,t))*sz+Mathf.Lerp(a.targetZ,b.targetZ,t));}
 }
 [Serializable] public sealed class NativeFittedVertices {public string name;public float[] positions,normals,tangents;}
 [Serializable] public sealed class NativeHairColourSwatch {public string name;public Color tint1,tint2,tint3,cuticleSpecularColor,cortexSpecularColor,subsurfaceAmbient,subsurfaceColor;public float tintSharpness,idMapStrength,rootDarkening,metallic,smoothnessMultiplier;}
 [Serializable] public sealed class NativeHairColourCatalog {public NativeHairColourSwatch[] swatches;}
 // Editor adapter for installed native data. Does not reconstruct or replace a face.
 public static class RebirthNativeHairFit {
  // Keep the attachment band fixed; expand free hair relative to the nearest crown point.
  // Uses the same geometry path for preview and export. No source assets are modified.
  public static void AdjustSize(Mesh mesh,Mesh head,float volume,float size,float width=0,float depth=0){
   if(volume==0&&size==0&&width==0&&depth==0)return;
   var skin=head.vertices;float top=float.MinValue;foreach(int i in head.GetTriangles(0))top=Mathf.Max(top,skin[i].y);
   var crown=new System.Collections.Generic.List<Vector3>();var seen=new System.Collections.Generic.HashSet<int>();
   foreach(int i in head.GetTriangles(0))if(skin[i].y>=top-.14f&&seen.Add(i))crown.Add(skin[i]);
   if(crown.Count==0)throw new InvalidDataException("Cannot size hair without a crown surface.");
   var points=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;
   Vector3 amount=new Vector3(size+volume+width,size,size+volume+depth);
   // Bound compression so the attachment transition cannot invert under combined minima.
   for(int axis=0;axis<3;axis++)if(amount[axis]<0)amount[axis]/=1-3*amount[axis];
   for(int i=0;i<points.Length;i++){
    Vector3 anchor=crown[0];float best=float.MaxValue;
    foreach(var q in crown){float d=(points[i]-q).sqrMagnitude;if(d<best){best=d;anchor=q;}}
    Vector3 offset=points[i]-anchor;float distance=Mathf.Sqrt(best);
    // First 3 mm stay fixed, with a smooth transition over the next 25 mm.
    float t=Mathf.Clamp01((distance-.003f)/.025f),weight=t*t*(3-2*t);
    Vector3 gradient=distance>1e-8f?offset/distance*(6*t*(1-t)/.025f):Vector3.zero;
    var jacobian=Matrix4x4.identity;
    for(int axis=0;axis<3;axis++)for(int col=0;col<3;col++)jacobian[axis,col]=(axis==col?1+amount[axis]*weight:0)+amount[axis]*offset[axis]*gradient[col];
    points[i]+=Vector3.Scale(amount,offset)*weight;
    if(normals.Length==points.Length){normals[i]=jacobian.inverse.transpose.MultiplyVector(normals[i]).normalized;
     if(tangents.Length==points.Length){var v=jacobian.MultiplyVector(tangents[i]);v=(v-normals[i]*Vector3.Dot(v,normals[i])).normalized;tangents[i]=new Vector4(v.x,v.y,v.z,tangents[i].w);}}
   }
   mesh.vertices=points;if(normals.Length==points.Length)mesh.normals=normals;if(tangents.Length==points.Length)mesh.tangents=tangents;mesh.RecalculateBounds();
  }
  // Vertical hanging-length deformation below the nape. Crown and upper flow stay fixed.
  // The C1 transition has derivative in [0,1], so the minimum stretch is 0.4.
  public static void AdjustLength(Mesh mesh,Mesh head,float length){
   if(length==0)return;
   if(float.IsNaN(length)||float.IsInfinity(length)||length<-.6f||length>1)throw new InvalidDataException("Invalid hanging hair length");
   var skin=head.vertices;float top=float.MinValue;foreach(int i in head.GetTriangles(0))top=Mathf.Max(top,skin[i].y);
   if(top==float.MinValue)throw new InvalidDataException("Cannot lengthen hair without a head surface");
   float pivot=top-.24f;const float band=.05f;var points=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;
   for(int i=0;i<points.Length;i++){
    float depth=pivot-points[i].y;if(depth<=0)continue;
    float extension=depth<band?depth*depth/(2*band):depth-band*.5f;
    float stretch=1+length*Mathf.Clamp01(depth/band);
    points[i].y-=length*extension;
    if(normals.Length==points.Length){normals[i]=new Vector3(normals[i].x,normals[i].y/stretch,normals[i].z).normalized;
     if(tangents.Length==points.Length){var t=tangents[i];var v=new Vector3(t.x,t.y*stretch,t.z);v=(v-normals[i]*Vector3.Dot(v,normals[i])).normalized;tangents[i]=new Vector4(v.x,v.y,v.z,t.w);}}
   }
   mesh.vertices=points;if(normals.Length==points.Length)mesh.normals=normals;if(tangents.Length==points.Length)mesh.tangents=tangents;mesh.RecalculateBounds();
  }
  public static Mesh BuildSurfaceFit(Mesh source,NativeFittedVertices fitted){
   if(fitted.positions.Length!=source.vertexCount*3||fitted.normals==null||fitted.normals.Length!=source.vertexCount*3||fitted.tangents==null||fitted.tangents.Length!=source.vertexCount*4)throw new InvalidDataException("Facial fit attributes do not match the native source mesh.");
   var mesh=UnityEngine.Object.Instantiate(source);var points=new Vector3[source.vertexCount];var normals=new Vector3[points.Length];var tangents=new Vector4[points.Length];for(int i=0;i<points.Length;i++){points[i]=new Vector3(fitted.positions[i*3],fitted.positions[i*3+1],fitted.positions[i*3+2]);normals[i]=new Vector3(fitted.normals[i*3],fitted.normals[i*3+1],fitted.normals[i*3+2]);tangents[i]=new Vector4(fitted.tangents[i*4],fitted.tangents[i*4+1],fitted.tangents[i*4+2],fitted.tangents[i*4+3]);}
   mesh.name="Surface fitted "+source.name;mesh.vertices=points;mesh.normals=normals;mesh.tangents=tangents;mesh.RecalculateBounds();return mesh;
  }
  public static Mesh Build(Mesh source,NativeFittedVertices fitted,NativeCrownCage cage){if(fitted.positions.Length!=source.vertexCount*3)throw new InvalidDataException("Native head fit vertex count differs from source mesh.");var mesh=UnityEngine.Object.Instantiate(source);var points=new Vector3[source.vertexCount];var normals=source.normals;var tangents=source.tangents;if(fitted.normals!=null){if(fitted.normals.Length!=source.vertexCount*3)throw new InvalidDataException("Native normal attributes differ from source.");for(int i=0;i<normals.Length;i++)normals[i]=new Vector3(fitted.normals[i*3],fitted.normals[i*3+1],fitted.normals[i*3+2]);}if(fitted.tangents!=null){if(fitted.tangents.Length!=source.vertexCount*4)throw new InvalidDataException("Native tangent attributes differ from source.");for(int i=0;i<tangents.Length;i++)tangents[i]=new Vector4(fitted.tangents[i*4],fitted.tangents[i*4+1],fitted.tangents[i*4+2],fitted.tangents[i*4+3]);}const float epsilon=.00005f;
   for(int i=0;i<points.Length;i++){var p=new Vector3(fitted.positions[i*3],fitted.positions[i*3+1],fitted.positions[i*3+2]);points[i]=cage.Warp(p);var jacobian=Matrix4x4.identity;jacobian.SetColumn(0,(cage.Warp(p+Vector3.right*epsilon)-cage.Warp(p-Vector3.right*epsilon))/(2*epsilon));jacobian.SetColumn(1,(cage.Warp(p+Vector3.up*epsilon)-cage.Warp(p-Vector3.up*epsilon))/(2*epsilon));jacobian.SetColumn(2,(cage.Warp(p+Vector3.forward*epsilon)-cage.Warp(p-Vector3.forward*epsilon))/(2*epsilon));normals[i]=jacobian.inverse.transpose.MultiplyVector(normals[i]).normalized;var tangent=jacobian.MultiplyVector(tangents[i]);tangent=(tangent-normals[i]*Vector3.Dot(tangent,normals[i])).normalized;tangents[i]=new Vector4(tangent.x,tangent.y,tangent.z,tangents[i].w);}
   mesh.name="Native fitted "+source.name;mesh.vertices=points;mesh.normals=normals;mesh.tangents=tangents;mesh.RecalculateBounds();return mesh;
  }
  // Artist controls on the verified native shader. Kept separate from the
  // untouched game swatch so a custom finish is never reported as native data.
  public static void Finish(Material material,float highlightStrength,float idVariation,float rootDarkening){
   material.SetFloat("_IDMapStrength",Mathf.Clamp01(idVariation));material.SetFloat("_RootDarkening",Mathf.Clamp01(rootDarkening));
   material.SetColor("_CuticleSpecularColor",material.GetColor("_CuticleSpecularColor")*Mathf.Clamp01(highlightStrength));
   material.SetColor("_CortexSpecularColor",material.GetColor("_CortexSpecularColor")*Mathf.Sqrt(Mathf.Clamp01(highlightStrength)));
  }
  public static Material Colour(Material original,NativeHairColourSwatch swatch){var material=new Material(original);if(material.GetFloat("_HoldColor")>.5f)return material;material.SetColor("_Tint1",swatch.tint1);material.SetColor("_Tint2",swatch.tint2);material.SetColor("_Tint3",swatch.tint3);material.SetFloat("_TintSharpness",swatch.tintSharpness);material.SetFloat("_IDMapStrength",swatch.idMapStrength);material.SetFloat("_RootDarkening",swatch.rootDarkening);material.SetFloat("_Metallic",swatch.metallic);material.SetColor("_CuticleSpecularColor",swatch.cuticleSpecularColor);material.SetColor("_CortexSpecularColor",swatch.cortexSpecularColor);material.SetFloat("_SmoothnessMultiplier",swatch.smoothnessMultiplier);material.SetColor("_SubsurfaceAmbient",swatch.subsurfaceAmbient);material.SetColor("_SubsurfaceColor",swatch.subsurfaceColor);return material;}
 }
}



