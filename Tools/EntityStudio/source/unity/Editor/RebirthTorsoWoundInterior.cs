using System;using System.Linq;using UnityEngine;using UnityEngine.Rendering;
namespace RebirthEntityStudio {
 // Optional torso attachment for the existing opening/lining pipeline.
 // The caller owns the cached library and supplies the qualified cage pose.
 public static class RebirthTorsoWoundInterior {
  public static int Append(BodyDeepWoundAssembly assembly,RebirthTorsoAnatomyLibrary library,Vector3[] cage,Mesh body,BodyDeepWound wound,int index,int group){
   if(group!=1&&group!=2)throw new ArgumentOutOfRangeException("group");
   string prefix="DeepWound_"+(index+1);var lining=assembly.parts.Single(p=>p.name==prefix+"_Lining");var material=lining.material;
   Vector3 center=material.GetVector("_DeepCenter0"),x=material.GetVector("_DeepX0"),y=material.GetVector("_DeepY0"),z=material.GetVector("_DeepZ0");
   Vector3 extent=new Vector3(Mathf.Abs(x.x)/x.sqrMagnitude+Mathf.Abs(y.x)/y.sqrMagnitude+Mathf.Abs(z.x)/z.sqrMagnitude,Mathf.Abs(x.y)/x.sqrMagnitude+Mathf.Abs(y.y)/y.sqrMagnitude+Mathf.Abs(z.y)/z.sqrMagnitude,Mathf.Abs(x.z)/x.sqrMagnitude+Mathf.Abs(y.z)/y.sqrMagnitude+Mathf.Abs(z.z)/z.sqrMagnitude);
   if(float.IsNaN(extent.sqrMagnitude)||float.IsInfinity(extent.sqrMagnitude))throw new InvalidOperationException("Invalid torso wound frame");
   var volume=new Bounds(center,extent*2);var local=new BodyWoundSurface(body).Local(center,extent.magnitude+.03f);int appended=0;Texture2D tissueFloor=null;
   foreach(var part in library.Evaluate(cage,volume,group)){
    int visible=0;
    foreach(var point in part.positions){Vector3 delta=point-center;float u=Vector3.Dot(delta,x),v=Vector3.Dot(delta,y),depth=Vector3.Dot(delta,z);if(Mathf.Abs(u)>=1||Mathf.Abs(v)>=1||Mathf.Abs(depth)>=1||assembly.field.GetPixelBilinear(u*.5f+.5f,v*.5f+.5f).r<=0)continue;visible++;if(local.SignedDistance(point)>.001f)throw new InvalidOperationException("Torso anatomy protrudes through this body; adjust the body or wound before exposure");}
    if(visible==0)continue;
    Mesh mesh=null;Material interior=null;
    try{
     string meshName=prefix+"_"+part.source.name;
     if(part.source.cornerNormals!=null&&part.source.cornerNormals.Length>0)mesh=RebirthTriangleNormalTransfer.Build(part.restPositions,part.positions,part.source.triangles,part.source.cornerNormals,meshName);
     else{mesh=new Mesh{name=meshName,indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};mesh.vertices=part.positions;mesh.triangles=part.source.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();}
     string role=part.source.ResolvedSurfaceRole;bool bone=role=="bone",cartilage=role=="cartilage";
     // Provisional tissue colours retain the existing bone/bowel paths. Authored
     // muscle detail and cartilage material qualification remain separate work.
     interior=new Material(material){name="Exposed anatomical "+role,hideFlags=HideFlags.HideAndDontSave};interior.SetFloat("_ClipInside",1);interior.SetFloat("_UseTissue",bone||cartilage?0:1);interior.SetColor("_Color",bone?new Color(.78f,.74f,.64f)*wound.boneTint:cartilage?new Color(.65f,.59f,.49f)*wound.tissueTint:new Color(.38f,.08f,.055f)*wound.tissueTint);interior.SetFloat("_Smoothness",bone?wound.boneSmoothness:wound.tissueSmoothness);
     if(role=="muscle"){if(tissueFloor==null){tissueFloor=RebirthWoundTissueExcavation.Build(lining.mesh,material);assembly.tissueCutFloors.Add(tissueFloor);}RebirthWoundTissueExcavation.Apply(interior,tissueFloor);}
     assembly.parts.Add(new BodyDeepWoundPart{name=mesh.name,mesh=mesh,material=interior,surfaceRole=role,useBoneBloodCoverage=bone});appended++;
    }catch{if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);if(interior!=null)UnityEngine.Object.DestroyImmediate(interior);throw;}
   }
   if(appended==0)throw new InvalidOperationException(group==1?"No ribs intersect this wound volume; adjust placement or depth":"No bowel intersects this wound volume; adjust placement or depth");return appended;
  }
 }
}
