using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
namespace RebirthEntityStudio {
 // Sparse linear stencils of the same Catmull-Clark rules used by the head.
 // Topology/UV refinement is prepared once; body slider edits evaluate weights.
 public static class RebirthBodySubdivisionCache {
  sealed class Poly {public int[] v;public Vector2[] uv;}
  sealed class Edge {public int a,b,index;public List<int> faces=new List<int>();}
  sealed class Stencil {public int[] offsets,indices,triangles;public float[] weights;public Vector2[] uv;}
  [Serializable] public sealed class Sampling {public int[] offsets,indices,triangles;public float[] weights;}
  public static Sampling ExportSampling(BodyTemplate template,int levels){var stencil=Prepare(template,levels);return new Sampling{offsets=stencil.offsets,indices=stencil.indices,weights=stencil.weights,triangles=stencil.triangles};}
  static string source;static readonly Dictionary<int,Stencil> cache=new Dictionary<int,Stencil>();
  static ulong Key(int a,int b)=>((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
  static Dictionary<int,float> Mix(IEnumerable<KeyValuePair<Dictionary<int,float>,float>> terms){var result=new Dictionary<int,float>();foreach(var term in terms)foreach(var item in term.Key){float current;result.TryGetValue(item.Key,out current);result[item.Key]=current+item.Value*term.Value;}return result;}
  static KeyValuePair<Dictionary<int,float>,float> Term(Dictionary<int,float> row,float weight)=>new KeyValuePair<Dictionary<int,float>,float>(row,weight);
  static Stencil Prepare(BodyTemplate template,int levels){
   var lookup=new Dictionary<int,int>();var rows=new List<Dictionary<int,float>>();var remap=new int[template.sourceIds.Length];for(int i=0;i<remap.Length;i++){int id;if(!lookup.TryGetValue(template.sourceIds[i],out id)){id=rows.Count;lookup[template.sourceIds[i]]=id;rows.Add(new Dictionary<int,float>{{i,1}});}remap[i]=id;}
   var polys=new List<Poly>();int offset=0;foreach(int count in template.topology.faceSizes){var poly=new Poly{v=new int[count],uv=new Vector2[count]};for(int i=0;i<count;i++){int id=template.topology.faceIndices[offset++];poly.v[i]=remap[id];poly.uv[i]=new Vector2(template.uv[id*2],template.uv[id*2+1]);}polys.Add(poly);}
   for(int level=0;level<levels;level++){
    var faces=polys.Select(p=>Mix(p.v.Select(i=>Term(rows[i],1f/p.v.Length)))).ToArray();var edges=new Dictionary<ulong,Edge>();var incident=Enumerable.Range(0,rows.Count).Select(i=>new List<int>()).ToArray();
    for(int fi=0;fi<polys.Count;fi++){var poly=polys[fi];for(int k=0;k<poly.v.Length;k++){int a=poly.v[k],b=poly.v[(k+1)%poly.v.Length];incident[a].Add(fi);ulong key=Key(a,b);Edge edge;if(!edges.TryGetValue(key,out edge))edges[key]=edge=new Edge{a=a,b=b};edge.faces.Add(fi);}}
    var neighbors=Enumerable.Range(0,rows.Count).Select(i=>new List<Edge>()).ToArray();foreach(var edge in edges.Values){neighbors[edge.a].Add(edge);neighbors[edge.b].Add(edge);}
    var next=new List<Dictionary<int,float>>(rows.Count+faces.Length+edges.Count);
    for(int i=0;i<rows.Count;i++){var boundary=neighbors[i].Where(e=>e.faces.Count==1).ToArray();if(boundary.Length==2){next.Add(Mix(new[]{Term(rows[i],.75f),Term(rows[boundary[0].a==i?boundary[0].b:boundary[0].a],.125f),Term(rows[boundary[1].a==i?boundary[1].b:boundary[1].a],.125f)}));continue;}int n=incident[i].Count;if(n==0){next.Add(rows[i]);continue;}var terms=new List<KeyValuePair<Dictionary<int,float>,float>>();foreach(int fi in incident[i])terms.Add(Term(faces[fi],1f/(n*n)));foreach(var edge in neighbors[i]){terms.Add(Term(rows[edge.a],1f/(n*neighbors[i].Count)));terms.Add(Term(rows[edge.b],1f/(n*neighbors[i].Count)));}terms.Add(Term(rows[i],(n-3f)/n));next.Add(Mix(terms));}
    int faceStart=next.Count;next.AddRange(faces);foreach(var edge in edges.Values){edge.index=next.Count;next.Add(edge.faces.Count==2?Mix(new[]{Term(rows[edge.a],.25f),Term(rows[edge.b],.25f),Term(faces[edge.faces[0]],.25f),Term(faces[edge.faces[1]],.25f)}):Mix(new[]{Term(rows[edge.a],.5f),Term(rows[edge.b],.5f)}));}
    var children=new List<Poly>(polys.Count*4);for(int fi=0;fi<polys.Count;fi++){var p=polys[fi];var center=p.uv.Aggregate(Vector2.zero,(sum,v)=>sum+v)/p.uv.Length;for(int k=0;k<p.v.Length;k++){int following=(k+1)%p.v.Length,previous=(k+p.v.Length-1)%p.v.Length;children.Add(new Poly{v=new[]{p.v[k],edges[Key(p.v[k],p.v[following])].index,faceStart+fi,edges[Key(p.v[previous],p.v[k])].index},uv=new[]{p.uv[k],(p.uv[k]+p.uv[following])*.5f,center,(p.uv[previous]+p.uv[k])*.5f}});}}rows=next;polys=children;
   }
   var corners=new Dictionary<Tuple<int,Vector2>,int>();var outputRows=new List<Dictionary<int,float>>();var outputUv=new List<Vector2>();var triangles=new List<int>();foreach(var poly in polys){var ids=new int[poly.v.Length];for(int k=0;k<ids.Length;k++){var key=Tuple.Create(poly.v[k],poly.uv[k]);int id;if(!corners.TryGetValue(key,out id)){id=outputRows.Count;corners[key]=id;outputRows.Add(rows[poly.v[k]]);outputUv.Add(poly.uv[k]);}ids[k]=id;}for(int k=1;k<ids.Length-1;k++)triangles.AddRange(new[]{ids[0],ids[k],ids[k+1]});}
   var starts=new List<int>{0};var indices=new List<int>();var weights=new List<float>();foreach(var row in outputRows){foreach(var item in row.OrderBy(p=>p.Key)){if(Mathf.Abs(item.Value)<1e-10f)continue;indices.Add(item.Key);weights.Add(item.Value);}starts.Add(indices.Count);}return new Stencil{offsets=starts.ToArray(),indices=indices.ToArray(),weights=weights.ToArray(),uv=outputUv.ToArray(),triangles=triangles.ToArray()};
  }
  public static void Evaluate(BodyTemplate template,int levels,Vector3[] input,out Vector3[] points,out Vector2[] uv,out int[] triangles){
   string key=template.contentHash+":"+template.topologyHash;if(source!=key){cache.Clear();source=key;}Stencil stencil;if(!cache.TryGetValue(levels,out stencil))cache[levels]=stencil=Prepare(template,levels);points=new Vector3[stencil.uv.Length];for(int i=0;i<points.Length;i++){Vector3 p=Vector3.zero;for(int j=stencil.offsets[i];j<stencil.offsets[i+1];j++)p+=input[stencil.indices[j]]*stencil.weights[j];points[i]=p;}uv=stencil.uv;triangles=stencil.triangles;
  }
 }
}
