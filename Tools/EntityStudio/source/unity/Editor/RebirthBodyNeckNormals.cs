using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
namespace RebirthEntityStudio {
 // A second normal-map frame; UV0 and its tangent frame remain body-owned.
 public static class RebirthBodyNeckNormals {
  static float Angle(Vector3 p,Vector3 center){float a=Mathf.Atan2(p.x-center.x,p.z-center.z);return a<0?a+2*Mathf.PI:a;}
  public static void Capture(BodyHeadConnection connection,int[] bodyUpper,int[] headRing,Vector3 bodyCenter,float[] ownership){
   var source=connection.head.tangents;var sourcePoints=connection.head.vertices;
   if(source.Length!=sourcePoints.Length||bodyUpper.Length!=headRing.Length||ownership.Length!=connection.body.vertexCount)throw new InvalidOperationException("Incomplete head normal-map correspondence");
   var points=connection.body.vertices;var normals=connection.body.normals;var frames=new Vector4[points.Length];
   var ring=headRing.Select(i=>connection.headTransform.MultiplyPoint3x4(sourcePoints[i])).ToArray();var center=ring.Aggregate(Vector3.zero,(sum,p)=>sum+p)/ring.Length;
   Func<int,Vector4> frame=i=>{var t=source[headRing[i]];var direction=connection.headTransform.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;return new Vector4(direction.x,direction.y,direction.z,t.w);};
   var angles=ring.Select(p=>Angle(p,center)).ToArray();
   for(int i=0;i<points.Length;i++){
    if(ownership[i]<=.2f)continue;float angle=Angle(points[i],bodyCenter);int right=Array.FindIndex(angles,a=>a>=angle);if(right<0)right=0;int left=(right+ring.Length-1)%ring.Length;
    float a=angles[left],b=angles[right];if(b<=a)b+=2*Mathf.PI;if(angle<a)angle+=2*Mathf.PI;float weight=Mathf.InverseLerp(a,b,angle);var x=frame(left);var y=frame(right);
    // Mirrored UV islands cannot interpolate handedness through zero.
    var value=x.w*y.w<0||Vector3.Dot((Vector3)x,(Vector3)y)<0?(weight<.5f?x:y):Vector4.Lerp(x,y,weight);
    Vector3 tangent=Vector3.ProjectOnPlane((Vector3)value,normals[i]);if(tangent.sqrMagnitude<1e-10f)continue;tangent.Normalize();frames[i]=new Vector4(tangent.x,tangent.y,tangent.z,value.w<0?-1:1);
   }
   // Exact boundary correspondence takes precedence over angular interpolation.
   for(int i=0;i<bodyUpper.Length;i++)frames[bodyUpper[i]]=frame(i);
   connection.body.SetUVs(2,frames.ToList());
  }
  public static void Bind(Material body,Material head,Mesh joinedBody){
   if(!body.HasProperty("_NeckNormalBlend"))return;
   var frames=new List<Vector4>();joinedBody.GetUVs(2,frames);var normal=head.HasProperty("_BumpMap")?head.GetTexture("_BumpMap"):null;
   bool standard=head.shader.name=="Standard"||head.shader.name=="Standard (Specular setup)";
   bool enabled=normal!=null&&(!standard||head.IsKeywordEnabled("_NORMALMAP"))&&frames.Count==joinedBody.vertexCount&&frames.Any(f=>Mathf.Abs(f.w)>.5f);
   body.SetFloat("_NeckNormalBlend",enabled?1:0);body.SetTexture("_NeckBumpMap",enabled?normal:null);
   body.SetFloat("_NeckBumpScale",enabled&&head.HasProperty("_BumpScale")?head.GetFloat("_BumpScale"):1);
   if(enabled){body.SetTextureScale("_NeckBumpMap",head.GetTextureScale("_BumpMap"));body.SetTextureOffset("_NeckBumpMap",head.GetTextureOffset("_BumpMap"));}
  }
 }
}
