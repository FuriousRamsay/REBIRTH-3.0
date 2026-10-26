using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio {
 public static class RebirthNativeAuthoredHairBlend {
  sealed class Family {
   public Mesh source;
   public Vector3[] mid, longer, messy, midNormals, longNormals, messyNormals;
   public Vector4[] midTangents, longTangents, messyTangents;
  }
  static readonly Dictionary<string,Family> families=new Dictionary<string,Family>();
  public static void Clear(){families.Clear();}
  static string Kind(NativeAppearanceStyle style)=>style.key.Substring(style.key.LastIndexOf(':')+1);
  public static bool SupportsLength(NativeAppearanceStyle style)=>style.gender=="female"&&SupportsMessiness(style);
  public static bool SupportsMessiness(NativeAppearanceStyle style){string kind=Kind(style);return kind=="hair_midpart_mid"||kind=="hair_midpart_karen_messy"||(style.gender=="female"&&kind=="hair_midpart_long");}
  public static float DefaultLength(NativeAppearanceStyle style)=>Kind(style)=="hair_midpart_long"?1:0;
  public static float DefaultMessiness(NativeAppearanceStyle style)=>Kind(style)=="hair_midpart_karen_messy"?1:0;
  public static bool Active(FaceNativeAppearanceSettings settings)=>settings.authoredLength>=0||settings.authoredMessiness>=0;
  public static string[] Sources(NativeAppearanceStyle selected){string prefix=selected.gender+":hair:hair_midpart_";return selected.gender=="female"?new[]{prefix+"mid",prefix+"long",prefix+"karen_messy"}:new[]{prefix+"mid",prefix+"karen_messy"};}

  static Vector3[] Positions(NativeAppearanceStyle style){var data=RebirthNativeAppearanceLibrary.Fit(style).positions;var result=new Vector3[data.Length/3];for(int i=0;i<result.Length;i++)result[i]=new Vector3(data[i*3],data[i*3+1],data[i*3+2]);return result;}
  static void ValidateLayout(Mesh a,Mesh b){
   if(a.vertexCount!=b.vertexCount||a.subMeshCount!=b.subMeshCount)throw new InvalidDataException("Native authored hair shapes have incompatible vertex layouts.");
   for(int i=0;i<a.subMeshCount;i++)if(!a.GetTriangles(i).SequenceEqual(b.GetTriangles(i)))throw new InvalidDataException("Native authored hair shapes have incompatible triangle correspondence.");
   var au=a.uv;var bu=b.uv;if(au.Length!=bu.Length)throw new InvalidDataException("Native authored hair UV data differs.");
   for(int i=0;i<au.Length;i++)if((au[i]-bu[i]).sqrMagnitude>1e-10f)throw new InvalidDataException("Native authored hair shapes use different UV layouts.");
   var at=a.tangents;var bt=b.tangents;for(int i=0;i<at.Length;i++)if(at[i].w!=bt[i].w)throw new InvalidDataException("Native authored tangent handedness differs.");
  }
  static Family Load(NativeAppearanceStyle selected){
   string key=RebirthNativeAppearanceLibrary.Catalog.sourceHash+":"+selected.gender;
   if(families.TryGetValue(key,out var family))return family;
   string prefix=selected.gender+":hair:hair_midpart_";
   var mid=RebirthNativeAppearanceLibrary.Style(prefix+"mid");var messy=RebirthNativeAppearanceLibrary.Style(prefix+"karen_messy");
   var longer=selected.gender=="female"?RebirthNativeAppearanceLibrary.Style(prefix+"long"):mid;
   var a=RebirthNativeAppearanceLibrary.Source(mid).sharedMesh;var b=RebirthNativeAppearanceLibrary.Source(longer).sharedMesh;var c=RebirthNativeAppearanceLibrary.Source(messy).sharedMesh;
   ValidateLayout(a,b);ValidateLayout(a,c);
   family=new Family{source=a,mid=Positions(mid),longer=Positions(longer),messy=Positions(messy),midNormals=a.normals,longNormals=b.normals,messyNormals=c.normals,midTangents=a.tangents,longTangents=b.tangents,messyTangents=c.tangents};
   if(family.mid.Length!=a.vertexCount||family.longer.Length!=a.vertexCount||family.messy.Length!=a.vertexCount)throw new InvalidDataException("Native authored morph data differs from its renderer.");
   families.Add(key,family);return family;
  }
  public static Mesh Build(Mesh head,NativeAppearanceStyle selected,FaceNativeAppearanceSettings settings){
   if(!SupportsMessiness(selected))throw new InvalidDataException("This native style has no verified authored blend family.");
   if(settings.authoredLength>=0&&!SupportsLength(selected))throw new InvalidDataException("This native style has no verified authored length correspondence.");
   var family=Load(selected);float length=settings.authoredLength>=0?settings.authoredLength:DefaultLength(selected),messiness=settings.authoredMessiness>=0?settings.authoredMessiness:DefaultMessiness(selected);
   int count=family.mid.Length;var data=new NativeFittedVertices{name="Native authored hair blend",positions=new float[count*3],normals=new float[count*3],tangents=new float[count*4]};
   for(int i=0;i<count;i++){
    var point=(length<=0?family.mid[i]:length>=1?family.longer[i]:Vector3.Lerp(family.mid[i],family.longer[i],length))+(family.messy[i]-family.mid[i])*messiness;
    if(length==0&&messiness==1)point=family.messy[i];
    var normal=(Vector3.Lerp(family.midNormals[i],family.longNormals[i],length)+(family.messyNormals[i]-family.midNormals[i])*messiness).normalized;
    Vector3 tangent=Vector4.Lerp(family.midTangents[i],family.longTangents[i],length)+(family.messyTangents[i]-family.midTangents[i])*messiness;tangent=(tangent-normal*Vector3.Dot(tangent,normal)).normalized;
    data.positions[i*3]=point.x;data.positions[i*3+1]=point.y;data.positions[i*3+2]=point.z;
    data.normals[i*3]=normal.x;data.normals[i*3+1]=normal.y;data.normals[i*3+2]=normal.z;
    data.tangents[i*4]=tangent.x;data.tangents[i*4+1]=tangent.y;data.tangents[i*4+2]=tangent.z;data.tangents[i*4+3]=family.midTangents[i].w;
   }
   return RebirthNativeHairFit.Build(family.source,data,RebirthNativeAppearanceLibrary.Crown(RebirthNativeAppearanceLibrary.Head(selected.gender),head));
  }
 }
}
