using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

namespace RebirthEntityStudio {
 // Assembly operates on copies. All facial/accessory vertices receive one
 // similarity transform; only skin triangles below the neck are trimmed.
 public sealed class BodyHeadConnection:IDisposable {
  public Mesh body,head;public Matrix4x4 headTransform;
  public float scale,headCut,bodyCut;public Vector3 translation;public int bodyRingCount,headRingCount;
  public void Dispose(){if(body!=null)UnityEngine.Object.DestroyImmediate(body);if(head!=null)UnityEngine.Object.DestroyImmediate(head);body=null;head=null;}
 }
 public static class RebirthBodyHeadConnection {
  public static void BindNeck(Material body,Material head,Mesh joinedBody,float skinSmoothness=.25f){
   if(!body.HasProperty("_NeckTex"))throw new InvalidOperationException("Install the body shader with neck transitions");body.SetTexture("_NeckTex",head.mainTexture);body.SetColor("_NeckTint",head.HasProperty("_Color")?head.color:Color.white);body.SetFloat("_NeckBlend",1);body.SetFloat("_NeckSmoothness",Mathf.Clamp01(skinSmoothness));
   RebirthBodyNeckNormals.Bind(body,head,joinedBody);
   var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);try{string path=UnityEditor.AssetDatabase.GetAssetPath(head.mainTexture);if(!System.IO.File.Exists(path)||!ImageConversion.LoadImage(texture,System.IO.File.ReadAllBytes(path)))throw new InvalidOperationException("Neck colour needs the saved generated head texture");var uv=joinedBody.uv2;var colours=joinedBody.colors;var scale=head.mainTextureScale;var offset=head.mainTextureOffset;for(int i=0;i<colours.Length;i++){var coord=Vector2.Scale(uv[i],scale)+offset;var sample=texture.GetPixelBilinear(coord.x,coord.y);if(QualitySettings.activeColorSpace==ColorSpace.Linear)sample=sample.linear;sample.a=colours[i].a;colours[i]=sample;}joinedBody.colors=colours;}finally{UnityEngine.Object.DestroyImmediate(texture);}
  }
  sealed class Vertex {
   public Vector3 p,n;public Vector2 uv,anatomical;public float blend,ownership;
   public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),anatomical=Vector2.Lerp(a.anatomical,b.anatomical,t),ownership=Mathf.Lerp(a.ownership,b.ownership,t)};
  }
  sealed class Cut {
   public List<Vertex> points;public List<int>[] triangles;public List<int> ring;
   public Mesh Mesh(string name){var result=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};result.SetVertices(points.Select(v=>v.p).ToList());result.SetNormals(points.Select(v=>v.n).ToList());result.SetUVs(0,points.Select(v=>v.uv).ToList());result.SetUVs(1,points.Select(v=>v.anatomical).ToList());result.SetColors(points.Select(v=>new Color(1,1,1,v.blend)).ToList());result.subMeshCount=triangles.Length;for(int i=0;i<triangles.Length;i++)result.SetTriangles(triangles[i],i);result.RecalculateTangents();result.RecalculateBounds();return result;}
  }
  static void JoinNeckNormals(BodyHeadConnection connection,float band,Vector3[] bottom,Vector3[] top,float[] bodyOwnership,float[] headOwnership){
   Vector3 center=bottom.Aggregate(Vector3.zero,(sum,p)=>sum+p)/bottom.Length;bottom=bottom.OrderBy(p=>Angle(p,center)).ToArray();top=top.OrderBy(p=>Angle(p,center)).ToArray();
   Func<Vector3[],float,Vector3> sample=(ring,angle)=>{int right=Array.FindIndex(ring,p=>Angle(p,center)>=angle);if(right<0)right=0;int left=(right+ring.Length-1)%ring.Length;float a=Angle(ring[left],center),b=Angle(ring[right],center);if(b<=a)b+=2*Mathf.PI;float q=angle;if(q<a)q+=2*Mathf.PI;return Vector3.Lerp(ring[left],ring[right],Mathf.InverseLerp(a,b,q));};
   Func<Vector3,Vector3> normalAt=point=>{float angle=Angle(point,center),delta=.08f;var lower=sample(bottom,angle);var upper=sample(top,angle);var direction=upper-lower;float t=direction.sqrMagnitude<1e-12f?0:Mathf.Clamp01(Vector3.Dot(point-lower,direction)/direction.sqrMagnitude);float before=(angle-delta+2*Mathf.PI)%(2*Mathf.PI),after=(angle+delta)%(2*Mathf.PI);var around=Vector3.Lerp(sample(bottom,after)-sample(bottom,before),sample(top,after)-sample(top,before),t);var along=sample(top,angle)-sample(bottom,angle);var normal=Vector3.Cross(around,along).normalized;var radial=point-center;radial.y=0;if(Vector3.Dot(normal,radial)<0)normal=-normal;return normal;};
   Action<Mesh,Matrix4x4,bool> apply=(mesh,matrix,isHead)=>{var points=mesh.vertices;var normals=mesh.normals;foreach(int i in new HashSet<int>(mesh.GetTriangles(0))){var point=matrix.MultiplyPoint3x4(points[i]);float ownership=isHead?headOwnership[i]:bodyOwnership[i];if(isHead?ownership>.7f:ownership<.2f)continue;float weight=isHead?1-Mathf.SmoothStep(0,1,(ownership-.5f)/.2f):Mathf.SmoothStep(0,1,(ownership-.2f)/.25f);var normal=normalAt(point);if(normal.sqrMagnitude>.5f)normals[i]=Vector3.Slerp(normals[i],normal,weight).normalized;}mesh.normals=normals;mesh.RecalculateTangents();};
   apply(connection.body,Matrix4x4.identity,false);apply(connection.head,connection.headTransform,true);
  }
  // A tiny valid triangle can carry a boundary edge. World-space area
  // tolerances must not turn a closed ownership cut into an open ring.
  static bool HasArea(Vector3 a,Vector3 b,Vector3 c){
   double x=(double)b.x-a.x,y=(double)b.y-a.y,z=(double)b.z-a.z,u=(double)c.x-a.x,v=(double)c.y-a.y,w=(double)c.z-a.z;
   return y*w-z*v!=0||z*u-x*w!=0||x*v-y*u!=0;
  }
  static Vector3Int Key(Vector3 p)=>Vector3Int.RoundToInt(p*1000000);
  static Cut Clip(Mesh mesh,float y,bool above,float[] ownership=null){
   var p=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var uv2=mesh.uv2;var values=ownership??p.Select(v=>v.y).ToArray();if(values.Length!=p.Length)throw new InvalidDataException("Ownership field does not match mesh vertices");
   var cut=new Cut{points=Enumerable.Range(0,p.Length).Select(i=>new Vertex{p=p[i],n=normals[i],uv=uv[i],anatomical=uv2.Length==p.Length?uv2[i]:uv[i],ownership=ownership==null?0:ownership[i]}).ToList(),triangles=Enumerable.Range(0,mesh.subMeshCount).Select(i=>new List<int>()).ToArray()};
   // UV duplicates share exact source positions. Keep their topology identities
   // through clipping rather than merging distinct close intersections: dense
   // smooth meshes can have legitimate neck slivers smaller than a micrometre.
   var sourceWeld=new Dictionary<Vector3,int>();var geometry=new List<int>();for(int i=0;i<p.Length;i++){int first;if(!sourceWeld.TryGetValue(p[i],out first))sourceWeld[p[i]]=first=i;geometry.Add(first);}
   var onCut=new HashSet<int>(Enumerable.Range(0,p.Length).Where(i=>values[i]==y).Select(i=>geometry[i]));var edges=new Dictionary<Tuple<int,int>,int>();var geometricEdges=new Dictionary<Tuple<int,int>,int>();
   Func<int,int,int> intersection=(a,b)=>{if(values[a]==y)return a;if(values[b]==y)return b;var key=Tuple.Create(Math.Min(a,b),Math.Max(a,b));int id;if(edges.TryGetValue(key,out id))return id;float t=(y-values[a])/(values[b]-values[a]);var v=Vertex.Lerp(cut.points[a],cut.points[b],t);if(ownership==null)v.p.y=y;else v.ownership=y;id=cut.points.Count;var ge=Tuple.Create(Math.Min(geometry[a],geometry[b]),Math.Max(geometry[a],geometry[b]));int shared;if(geometricEdges.TryGetValue(ge,out shared)){v.p=cut.points[shared].p;geometry.Add(shared);}else{geometricEdges[ge]=id;geometry.Add(id);}cut.points.Add(v);onCut.Add(geometry[id]);edges[key]=id;return id;};
   Func<int,bool> inside=i=>above?values[i]>=y:values[i]<=y;
   for(int sub=0;sub<mesh.subMeshCount;sub++){
    var source=mesh.GetTriangles(sub);if(sub!=0){cut.triangles[sub].AddRange(source);continue;}
    for(int t=0;t<source.Length;t+=3){var poly=new List<int>();for(int k=0;k<3;k++){int a=source[t+k],b=source[t+(k+1)%3];bool ia=inside(a),ib=inside(b);if(ia)poly.Add(a);if(ia!=ib)poly.Add(intersection(a,b));}for(int k=1;k+1<poly.Count;k++){var a=cut.points[poly[0]].p;var b=cut.points[poly[k]].p;var c=cut.points[poly[k+1]].p;if(HasArea(a,b,c))cut.triangles[0].AddRange(new[]{poly[0],poly[k],poly[k+1]});}}
   }
   // Weld only for boundary discovery. Texture seams keep distinct UV vertices.
   var remap=new Dictionary<int,int>();foreach(int i in cut.triangles[0].Distinct())remap[i]=geometry[i];
   var counts=new Dictionary<Tuple<int,int>,int>();var triangles=cut.triangles[0];for(int t=0;t<triangles.Count;t+=3)for(int k=0;k<3;k++){int a=remap[triangles[t+k]],b=remap[triangles[t+(k+1)%3]];if(a==b)continue;var key=Tuple.Create(Math.Min(a,b),Math.Max(a,b));int count;counts.TryGetValue(key,out count);counts[key]=count+1;}
   var boundary=counts.Where(e=>e.Value==1&&onCut.Contains(e.Key.Item1)&&onCut.Contains(e.Key.Item2)).Select(e=>e.Key).ToArray();
   var neighbors=new Dictionary<int,List<int>>();foreach(var edge in boundary){foreach(var pair in new[]{new[]{edge.Item1,edge.Item2},new[]{edge.Item2,edge.Item1}}){List<int> list;if(!neighbors.TryGetValue(pair[0],out list))neighbors[pair[0]]=list=new List<int>();list.Add(pair[1]);}}
   if(neighbors.Count<8||neighbors.Any(k=>k.Value.Count!=2))throw new InvalidOperationException("Neck cut did not produce a closed ring on "+mesh.name+" at "+y+" (points "+neighbors.Count+", invalid degrees "+neighbors.Count(k=>k.Value.Count!=2)+"). Adjust the neck join height.");
   cut.ring=new List<int>();int start=neighbors.Keys.Min(),current=start,previous=-1;do{cut.ring.Add(current);var n=neighbors[current];int next=n[0]==previous?n[1]:n[0];previous=current;current=next;if(cut.ring.Count>neighbors.Count)throw new InvalidOperationException("Invalid neck boundary");}while(current!=start);if(cut.ring.Count!=neighbors.Count)throw new InvalidOperationException("Neck cut intersects more than one surface. Adjust the join height.");
   return cut;
  }
  // Use the body-generated guide head as the proportional reference, not a
  // fixed authoring height. Uniform scaling preserves authored facial shape.
  static Bounds HeadBounds(Mesh mesh,float[] ownership,float threshold){
   var points=mesh.vertices;var ids=mesh.GetTriangles(0).Distinct().Where(i=>i<ownership.Length&&ownership[i]>=threshold).ToArray();
   if(ids.Length<10)throw new InvalidDataException("Missing anatomical guide head surface");
   var bounds=new Bounds(points[ids[0]],Vector3.zero);foreach(int i in ids)bounds.Encapsulate(points[i]);
   if(bounds.size.y<.05f||float.IsNaN(bounds.size.y)||float.IsInfinity(bounds.size.y))throw new InvalidDataException("Invalid anatomical guide head height");return bounds;
  }
  static Vector3 Center(Cut cut)=>cut.ring.Aggregate(Vector3.zero,(sum,i)=>sum+cut.points[i].p)/cut.ring.Count;
  static float Angle(Vector3 p,Vector3 center){float a=Mathf.Atan2(p.x-center.x,p.z-center.z);return a<0?a+Mathf.PI*2:a;}
  public static BodyHeadConnection Build(Mesh body,Mesh head,float height,float headSize=1,float joinHeight=1.49f,float neckShortening=.04f,float[] bodyOwnership=null,float[] headOwnership=null){
   if(height<1.2f||height>2.3f||headSize<.8f||headSize>1.2f)throw new ArgumentOutOfRangeException("Invalid head connection settings");
   if(bodyOwnership==null||headOwnership==null)throw new InvalidDataException("Anatomical head and body ownership fields are required");
   // Legacy joinHeight/neckShortening fields remain readable in old recipes.
   // The body neck controls now define the neck; no height plane partitions it.
   var result=new BodyHeadConnection();try{
    const float bodyLimit=.45f,headLimit=.5f;
    var bodyCut=Clip(body,bodyLimit,false,bodyOwnership);var headCut=Clip(head,headLimit,true,headOwnership);var guide=Clip(head,bodyLimit,true,headOwnership);
    var bc=Center(bodyCut);var gc=Center(guide);var hc=Center(headCut);var bodyHeadBounds=HeadBounds(body,bodyOwnership,headLimit);var authoredHeadBounds=HeadBounds(head,headOwnership,headLimit);result.scale=bodyHeadBounds.size.y/authoredHeadBounds.size.y*headSize;result.translation=new Vector3(bc.x-gc.x*result.scale,bodyHeadBounds.max.y-authoredHeadBounds.max.y*result.scale,bc.z-gc.z*result.scale);result.headTransform=Matrix4x4.TRS(result.translation,Quaternion.identity,Vector3.one*result.scale);
    var bottom=bodyCut.ring.OrderBy(i=>Angle(bodyCut.points[i].p,bc)).ToArray();var topRing=headCut.ring.OrderBy(i=>Angle(headCut.points[i].p,hc)).ToArray();var guideRing=guide.ring.OrderBy(i=>Angle(guide.points[i].p,gc)).ToArray();var original=bottom.Select(i=>bodyCut.points[i].p).ToArray();
    Func<Cut,int[],Vector3,float,Vertex> sample=(cut,ring,center,angle)=>{int right=Array.FindIndex(ring,i=>Angle(cut.points[i].p,center)>=angle);if(right<0)right=0;int left=(right+ring.Length-1)%ring.Length;float a=Angle(cut.points[ring[left]].p,center),b=Angle(cut.points[ring[right]].p,center);if(b<=a)b+=2*Mathf.PI;float q=angle;if(q<a)q+=2*Mathf.PI;return Vertex.Lerp(cut.points[ring[left]],cut.points[ring[right]],Mathf.InverseLerp(a,b,q));};
    // The body owns the complete neck. Fit its upper end to the head's
    // anatomical underside while leaving zero-head-weight torso vertices alone.
    var originalCut=new Cut{points=original.Select(p=>new Vertex{p=p}).ToList()};var originalRing=Enumerable.Range(0,original.Length).ToArray();
    foreach(var point in bodyCut.points){if(point.ownership<=0||point.ownership>bodyLimit)continue;float angle=Angle(point.p,bc);var target=sample(guide,guideRing,gc,angle);var start=sample(originalCut,originalRing,bc,angle);float weight=Mathf.SmoothStep(0,1,point.ownership/bodyLimit);point.p+=(result.headTransform.MultiplyPoint3x4(target.p)-start.p)*weight;point.anatomical=target.uv;point.blend=Mathf.SmoothStep(0,1,(point.ownership-.2f)/.25f);}
    var upper=new int[topRing.Length];for(int i=0;i<topRing.Length;i++){var v=headCut.points[topRing[i]];upper[i]=bodyCut.points.Count;bodyCut.points.Add(new Vertex{p=result.headTransform.MultiplyPoint3x4(v.p),n=v.n,uv=v.anatomical,anatomical=v.uv,blend=1,ownership=headLimit});}
    Action<int,int,int> triangle=(a,b,c)=>{var pa=bodyCut.points[a].p;var pb=bodyCut.points[b].p;var pc=bodyCut.points[c].p;var radial=(pa+pb+pc)/3-bc;radial.y=0;if(Vector3.Dot(Vector3.Cross(pb-pa,pc-pa),radial)<0){int swap=b;b=c;c=swap;}if(HasArea(pa,pb,pc))bodyCut.triangles[0].AddRange(new[]{a,b,c});};
    int lowerIndex=0,upperIndex=0;while(lowerIndex<bottom.Length||upperIndex<upper.Length){float nextLower=lowerIndex+1>=bottom.Length?2*Mathf.PI:Angle(original[lowerIndex+1],bc),nextUpper=upperIndex+1>=upper.Length?2*Mathf.PI:Angle(headCut.points[topRing[upperIndex+1]].p,hc);if(lowerIndex<bottom.Length&&(upperIndex>=upper.Length||nextLower<=nextUpper)){triangle(bottom[lowerIndex%bottom.Length],bottom[(lowerIndex+1)%bottom.Length],upper[upperIndex%upper.Length]);lowerIndex++;}else{triangle(bottom[lowerIndex%bottom.Length],upper[(upperIndex+1)%upper.Length],upper[upperIndex%upper.Length]);upperIndex++;}}
    result.bodyCut=bc.y;result.headCut=hc.y;result.bodyRingCount=bottom.Length;result.headRingCount=topRing.Length;result.body=bodyCut.Mesh("Body including anatomical neck");result.head=headCut.Mesh("Head above anatomical jaw and skull-base boundary");JoinNeckNormals(result,.045f*result.scale,bottom.Select(i=>bodyCut.points[i].p).ToArray(),upper.Select(i=>bodyCut.points[i].p).ToArray(),bodyCut.points.Select(v=>v.ownership).ToArray(),headCut.points.Select(v=>v.ownership).ToArray());RebirthBodyNeckNormals.Capture(result,upper,topRing,bc,bodyCut.points.Select(v=>v.ownership).ToArray());return result;
   }catch{result.Dispose();throw;}
  }
 }
}
