using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;

namespace RebirthEntityStudio {
 [Serializable] public sealed class NativeFacialCardAttachment {
  public int[] vertices;
  public int a,b,c;
  public Vector3 barycentric,pivot;
  public Quaternion rotation;
  public int[] normalTriangles;
  public float[] normalWeights;
  public Vector3 normal;
 }
 [Serializable] public sealed class NativeFacialStyleAttachment {
  public string key;
  public NativeFittedVertices reference;
  public NativeFacialCardAttachment[] cards;
 }
 [Serializable] public sealed class NativeFacialAttachmentData {
  public int version=2;
  public string sourceHash,skinTopologyHash;
  public NativeFacialStyleAttachment[] styles;
 }

 // A local authoring binding. Registration creates the reference groom once;
 // subsequent head edits move complete native cards through skin attachments.
 // This never changes the head, oral meshes, native indices or UVs.
 public static class NativeFacialRootAttachment {
  sealed class Cached {public Hash128 revision;public NativeFacialAttachmentData data;}
  static readonly Dictionary<string,Cached> loaded=new Dictionary<string,Cached>();
  public static void Clear()=>loaded.Clear();
  public static NativeFacialAttachmentData Load(string guid){
   string path=AssetDatabase.GUIDToAssetPath(guid);
   if(string.IsNullOrEmpty(path))throw new FileNotFoundException("The saved facial-hair fit is missing. Rebuild its attachment.");
   var revision=AssetDatabase.GetAssetDependencyHash(path);
   if(loaded.TryGetValue(guid,out var cached)&&cached.revision==revision)return cached.data;
   var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(path);if(asset==null)throw new InvalidDataException("The saved facial-hair fit is not a text asset.");
   var data=JsonUtility.FromJson<NativeFacialAttachmentData>(asset.text);
   if(data==null||data.version!=2||data.styles==null)throw new InvalidDataException("The saved facial-hair fit uses an unsupported format.");
   loaded[guid]=new Cached{revision=revision,data=data};return data;
  }
  public static string CopyForExport(string guid,string folder){
   if(string.IsNullOrEmpty(guid))return "";Load(guid);
   string destination=AssetDatabase.GenerateUniqueAssetPath(folder+"/FacialHairFit.json");
   if(!AssetDatabase.CopyAsset(AssetDatabase.GUIDToAssetPath(guid),destination))throw new IOException("Could not copy the exported facial-hair fit.");
   return AssetDatabase.AssetPathToGUID(destination);
  }
  public static string TopologyHash(Mesh head){
   var triangles=head.GetTriangles(0);var bytes=new byte[triangles.Length*4];
   Buffer.BlockCopy(triangles,0,bytes,0,bytes.Length);
   using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
  }
  static Quaternion Frame(Vector3 a,Vector3 b,Vector3 c){
   var edge=b-a;var normal=Vector3.Cross(edge,c-a);
   if(edge.sqrMagnitude<1e-14f||normal.sqrMagnitude<1e-20f)throw new InvalidDataException("A facial attachment skin triangle collapsed.");
   return Quaternion.LookRotation(edge.normalized,normal.normalized);
  }
  public static NativeFittedVertices Apply(Mesh head,NativeFacialAttachmentData data,string sourceHash,string key,float length=0,float fullness=0){
   if(float.IsNaN(length)||float.IsInfinity(length)||float.IsNaN(fullness)||float.IsInfinity(fullness))throw new InvalidDataException("Invalid facial groom shape.");
   fullness=Mathf.Clamp(fullness,-.5f,1);
   float lengthScale=1+Mathf.Clamp(length,-.5f,1);
   if(data==null||data.version!=2||data.sourceHash!=sourceHash)throw new InvalidDataException("Facial attachment source/version differs; rebuild its binding.");
   if(TopologyHash(head)!=data.skinTopologyHash)throw new InvalidDataException("Head skin topology differs from its facial attachment; rebuild its binding.");
   var style=data.styles.SingleOrDefault(s=>s.key==key);
   if(style==null)throw new InvalidDataException("The facial style has no surface attachment: "+key);
   var rest=style.reference;int count=rest.positions.Length/3;
   if(rest.normals.Length!=count*3||rest.tangents.Length!=count*4)throw new InvalidDataException("Facial attachment attributes differ from its vertices.");
   var result=new NativeFittedVertices{name=rest.name,positions=new float[count*3],normals=new float[count*3],tangents=new float[count*4]};
   var skin=head.vertices;var visited=new bool[count];
   foreach(var card in style.cards){
    var a=skin[card.a];var b=skin[card.b];var c=skin[card.c];
    var pivot=a*card.barycentric.x+b*card.barycentric.y+c*card.barycentric.z;
    var normal=Vector3.zero;
    for(int f=0;f<card.normalWeights.Length;f++){int i=f*3;normal+=Vector3.Cross(skin[card.normalTriangles[i+1]]-skin[card.normalTriangles[i]],skin[card.normalTriangles[i+2]]-skin[card.normalTriangles[i]])*card.normalWeights[f];}
    if(normal.sqrMagnitude<1e-20f)throw new InvalidDataException("A facial attachment skin patch collapsed.");
    var turn=Quaternion.FromToRotation(card.normal,normal.normalized);
    // Explicit style-space control, not inferred per-strand flow. Native
    // components can span several locks, so a farthest-vertex axis is unsafe.
    bool moustache=key.Contains(":moustache:");
    foreach(int i in card.vertices){
     if(i<0||i>=count||visited[i])throw new InvalidDataException("Facial attachment vertex coverage overlaps or leaves its mesh.");
     visited[i]=true;var p=new Vector3(rest.positions[i*3],rest.positions[i*3+1],rest.positions[i*3+2]);
     var n=new Vector3(rest.normals[i*3],rest.normals[i*3+1],rest.normals[i*3+2]);
     var t=new Vector3(rest.tangents[i*4],rest.tangents[i*4+1],rest.tangents[i*4+2]);
     var local=p-card.pivot;
     if(length!=0){
      var lengthAxis=moustache?(local.x<0?Vector3.left:Vector3.right):Vector3.down;
      // Extend only away from the root. Symmetric scaling also extended the
      // attachment-side cheek edges backwards, producing visible spikes.
      const float rootBand=.003f; // Explicit authoring transition, metres.
      float coordinate=Vector3.Dot(local,lengthAxis),u=Mathf.Clamp01(coordinate/rootBand);
      float blend=u*u*(3-2*u),derivative=coordinate>0?blend+coordinate*(6*u-6*u*u)/rootBand:0;
      float jacobian=1+(lengthScale-1)*derivative;
      if(jacobian<=0)throw new InvalidDataException("Facial length deformation inverted its root transition.");
      local+=lengthAxis*Mathf.Max(0,coordinate)*blend*(lengthScale-1);
      n+=lengthAxis*Vector3.Dot(n,lengthAxis)*(1/jacobian-1);
      t+=lengthAxis*Vector3.Dot(t,lengthAxis)*(jacobian-1);
      n.Normalize();t=(t-n*Vector3.Dot(t,n)).normalized;
     }
     if(fullness!=0){
      // Lift only the free outward portion of the existing cohesive component.
      // The first2mm along the original skin normal stay fixed; the next3mm
      // blend C1. Compression cannot move the free portion through that band.
      var axis=card.normal.normalized;const float root=.002f,band=.003f;
      float coordinate=Vector3.Dot(local,axis)-root,u=Mathf.Clamp01(coordinate/band);
      if(coordinate>0){
      float blend=u*u*(3-2*u),derivative=coordinate>0?blend+coordinate*(6*u-6*u*u)/band:0;
      float jacobian=1+fullness*derivative;if(jacobian<=0)throw new InvalidDataException("Facial fullness inverted its root transition.");
      local+=axis*Mathf.Max(0,coordinate)*blend*fullness;
      n+=axis*Vector3.Dot(n,axis)*(1/jacobian-1);t+=axis*Vector3.Dot(t,axis)*(jacobian-1);
      n.Normalize();t=(t-n*Vector3.Dot(t,n)).normalized;
      }
     }
     p=pivot+turn*local;n=turn*n;t=turn*t;
     for(int axis=0;axis<3;axis++){result.positions[i*3+axis]=p[axis];result.normals[i*3+axis]=n[axis];result.tangents[i*4+axis]=t[axis];}
     result.tangents[i*4+3]=rest.tangents[i*4+3];
    }
   }
   if(visited.Any(v=>!v))throw new InvalidDataException("Facial attachment does not cover every native vertex.");
   return result;
  }

  public sealed class Builder {
   readonly Vector3[] skin;readonly int[] triangles;readonly Node tree;
   public readonly string skinTopologyHash;
   sealed class Node {public Bounds bounds;public Node left,right;public int[] faces;}
   public Builder(Mesh reference){skin=reference.vertices;triangles=reference.GetTriangles(0);skinTopologyHash=TopologyHash(reference);var faces=Enumerable.Range(0,triangles.Length/3).Where(i=>Vector3.Cross(skin[triangles[i*3+1]]-skin[triangles[i*3]],skin[triangles[i*3+2]]-skin[triangles[i*3]]).sqrMagnitude>1e-20f).ToArray();if(faces.Length==0)throw new InvalidDataException("No valid skin surface for facial attachments.");tree=Build(faces);}
   Vector3 Center(int face)=>(skin[triangles[face*3]]+skin[triangles[face*3+1]]+skin[triangles[face*3+2]])/3;
   Node Build(int[] faces){
    var node=new Node{bounds=new Bounds(skin[triangles[faces[0]*3]],Vector3.zero)};
    foreach(int f in faces)for(int i=0;i<3;i++)node.bounds.Encapsulate(skin[triangles[f*3+i]]);
    if(faces.Length<=12){node.faces=faces;return node;}
    var size=node.bounds.size;int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
    Array.Sort(faces,(a,b)=>Center(a)[axis].CompareTo(Center(b)[axis]));int middle=faces.Length/2;
    node.left=Build(faces.Take(middle).ToArray());node.right=Build(faces.Skip(middle).ToArray());return node;
   }
   // Closest triangle point in barycentric coordinates (vertex/edge/face regions).
   static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c){
    var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
    if(d1<=0&&d2<=0)return new Vector3(1,0,0);
    var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return new Vector3(0,1,0);
    float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0){float v=d1/(d1-d3);return new Vector3(1-v,v,0);}
    var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return new Vector3(0,0,1);
    float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0){float w=d2/(d2-d6);return new Vector3(1-w,0,w);}
    float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0){float w=(d4-d3)/((d4-d3)+(d5-d6));return new Vector3(0,1-w,w);}
    float inverse=1/(va+vb+vc);float y=vb*inverse,z=vc*inverse;return new Vector3(1-y-z,y,z);
   }
   void Nearest(Node node,Vector3 point,ref float best,ref int face,ref Vector3 bary){
    if(node.bounds.SqrDistance(point)>best)return;
    if(node.faces!=null){foreach(int f in node.faces){var a=skin[triangles[f*3]];var b=skin[triangles[f*3+1]];var c=skin[triangles[f*3+2]];var bc=Closest(point,a,b,c);float distance=(a*bc.x+b*bc.y+c*bc.z-point).sqrMagnitude;if(distance<best){best=distance;face=f;bary=bc;}}return;}
    var first=node.left.bounds.SqrDistance(point)<node.right.bounds.SqrDistance(point)?node.left:node.right;
    Nearest(first,point,ref best,ref face,ref bary);Nearest(first==node.left?node.right:node.left,point,ref best,ref face,ref bary);
   }
   void Patch(Node node,Vector3 point,Vector3 normal,List<int> faces){
    const float radius=.012f;
    if(node.bounds.SqrDistance(point)>radius*radius)return;
    if(node.faces==null){Patch(node.left,point,normal,faces);Patch(node.right,point,normal,faces);return;}
    foreach(int f in node.faces){var a=skin[triangles[f*3]];var b=skin[triangles[f*3+1]];var c=skin[triangles[f*3+2]];var n=Vector3.Cross(b-a,c-a);if((Center(f)-point).sqrMagnitude<radius*radius&&Vector3.Dot(n.normalized,normal)>.3f)faces.Add(f);}
   }
   static int Find(int[] parent,int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
   public NativeFacialStyleAttachment Bind(string key,NativeFittedVertices fitted,Mesh nativeMesh){
    int count=fitted.positions.Length/3;if(nativeMesh.vertexCount!=count)throw new InvalidDataException("Native facial attachment topology differs.");
    var points=Enumerable.Range(0,count).Select(i=>new Vector3(fitted.positions[i*3],fitted.positions[i*3+1],fitted.positions[i*3+2])).ToArray();
    var parent=Enumerable.Range(0,count).ToArray();var indices=nativeMesh.triangles;
    for(int i=0;i<indices.Length;i+=3)for(int j=1;j<3;j++)parent[Find(parent,indices[i+j])]=Find(parent,indices[i]);
    var uv=nativeMesh.uv;var seams=new Dictionary<string,int>();
    for(int i=0;i<count;i++){var p=points[i];var t=uv[i];string signature=string.Join(",",new[]{p.x,p.y,p.z,t.x,t.y}.Select(v=>Math.Round(v,6).ToString("R",System.Globalization.CultureInfo.InvariantCulture)));if(seams.TryGetValue(signature,out int other))parent[Find(parent,i)]=Find(parent,other);else seams.Add(signature,i);}
    var cards=new List<NativeFacialCardAttachment>();
    foreach(var group in Enumerable.Range(0,count).GroupBy(i=>Find(parent,i))){
     var vertices=group.ToArray();var candidates=new List<Tuple<float,Vector3>>();
     foreach(int i in vertices){float best=float.PositiveInfinity;int face=-1;var bary=Vector3.zero;Nearest(tree,points[i],ref best,ref face,ref bary);candidates.Add(Tuple.Create(best,points[i]));}
     candidates.Sort((a,b)=>a.Item1.CompareTo(b.Item1));float threshold=Mathf.Sqrt(candidates[(int)((candidates.Count-1)*.2f)].Item1)+.001f;var roots=candidates.Where(v=>v.Item1<=threshold*threshold).Select(v=>v.Item2).ToArray();var root=roots.Aggregate(Vector3.zero,(a,b)=>a+b)/roots.Length;
     float distance=float.PositiveInfinity;int closest=-1;var bc=Vector3.zero;Nearest(tree,root,ref distance,ref closest,ref bc);int aIndex=triangles[closest*3],bIndex=triangles[closest*3+1],cIndex=triangles[closest*3+2];
     var patch=new List<int>();var faceNormal=Vector3.Cross(skin[bIndex]-skin[aIndex],skin[cIndex]-skin[aIndex]).normalized;Patch(tree,root,faceNormal,patch);if(patch.Count==0)patch.Add(closest);
     var weights=patch.Select(f=>Mathf.Pow(Mathf.Max(0,1-(Center(f)-root).magnitude/.012f),2)).ToArray();if(weights.All(w=>w==0))weights[0]=1;
     float weightSum=weights.Sum();for(int f=0;f<weights.Length;f++)weights[f]/=weightSum;
     var patchNormal=Vector3.zero;for(int f=0;f<patch.Count;f++){int i=patch[f]*3;patchNormal+=Vector3.Cross(skin[triangles[i+1]]-skin[triangles[i]],skin[triangles[i+2]]-skin[triangles[i]])*weights[f];}
     cards.Add(new NativeFacialCardAttachment{vertices=vertices,a=aIndex,b=bIndex,c=cIndex,barycentric=bc,pivot=skin[aIndex]*bc.x+skin[bIndex]*bc.y+skin[cIndex]*bc.z,rotation=Frame(skin[aIndex],skin[bIndex],skin[cIndex]),normalTriangles=patch.SelectMany(f=>new[]{triangles[f*3],triangles[f*3+1],triangles[f*3+2]}).ToArray(),normalWeights=weights,normal=patchNormal.normalized});
    }
    return new NativeFacialStyleAttachment{key=key,reference=fitted,cards=cards.ToArray()};
   }
  }
 }
}
