using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace RebirthEntityStudio {
 // Native counterpart of prepare_torso_deform_cages.py. A sampling cage,
 // not an anatomical skeleton or a clinical centre-of-bone estimate.
 public static class RebirthTorsoCage {
  public static readonly string[] Guides={"joint-pelvis","joint-spine-4","joint-spine-3","joint-spine-2","joint-spine-1","joint-neck"};
  struct P {
   public double x,y,z;
   public P(double a,double b,double c){x=a;y=b;z=c;}
   public static P operator +(P a,P b)=>new P(a.x+b.x,a.y+b.y,a.z+b.z);
   public static P operator -(P a,P b)=>new P(a.x-b.x,a.y-b.y,a.z-b.z);
   public static P operator *(P a,double b)=>new P(a.x*b,a.y*b,a.z*b);
   public double Length=>Math.Sqrt(x*x+y*y+z*z);
   public Vector3 Vector=>new Vector3((float)x,(float)y,(float)z);
  }
  struct Edge {public P a,b;public int i,j;}
  static P Point(Vector3 p)=>new P(p.x,p.y,p.z);
  static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
  static int Root(List<int> parents,int node){while(parents[node]!=node){parents[node]=parents[parents[node]];node=parents[node];}return node;}
  static int Node(P p,Dictionary<Tuple<long,long,long>,int> nodes,List<int> parents,List<int> degree){
   var key=Tuple.Create((long)Math.Round(p.x*1e7),(long)Math.Round(p.y*1e7),(long)Math.Round(p.z*1e7));int id;
   if(!nodes.TryGetValue(key,out id)){id=parents.Count;nodes.Add(key,id);parents.Add(id);degree.Add(0);}degree[id]++;return id;
  }
  static void Intersect(P a,P b,double height,ref P first,ref P second,ref int count){double d=a.y-height,e=b.y-height;if((d<0)==(e<0))return;P hit=a+(b-a)*(d/(d-e));if(count==0)first=hit;else second=hit;count++;}
  static List<Edge> Section(Vector3[] vertices,int[] triangles,P guide){
   var edges=new List<Edge>();var nodes=new Dictionary<Tuple<long,long,long>,int>();var parents=new List<int>();var degree=new List<int>();
   for(int k=0;k<triangles.Length;k+=3){
    P a=Point(vertices[triangles[k]]),b=Point(vertices[triangles[k+1]]),c=Point(vertices[triangles[k+2]]),first=new P(),second=new P();int count=0;
    Intersect(a,b,guide.y,ref first,ref second,ref count);Intersect(b,c,guide.y,ref first,ref second,ref count);Intersect(c,a,guide.y,ref first,ref second,ref count);
    if(count!=2||(first-second).Length<1e-10)continue;
    int i=Node(first,nodes,parents,degree),j=Node(second,nodes,parents,degree);if(i==j)throw new InvalidDataException("Degenerate torso section edge");
    int ri=Root(parents,i),rj=Root(parents,j);parents[rj]=ri;edges.Add(new Edge{a=first,b=second,i=i,j=j});
   }
   var groups=edges.GroupBy(e=>Root(parents,e.i));List<Edge> best=null;double score=double.PositiveInfinity;
   foreach(var group in groups){var selected=group.ToList();if(selected.Any(e=>degree[e.i]!=2||degree[e.j]!=2))continue;P mean=new P();foreach(var e in selected)mean=mean+(e.a+e.b)*(.5/selected.Count);double value=(mean.x-guide.x)*(mean.x-guide.x)+(mean.z-guide.z)*(mean.z-guide.z);if(value<score){score=value;best=selected;}}
   if(best==null)throw new InvalidDataException("No closed torso cross-section; interior fit unavailable for this body");return best;
  }
  public static Vector3[] Build(Mesh body,Dictionary<string,Vector3> current,Vector3[] reference){
   if(body==null||current==null||reference==null||reference.Length!=Guides.Length)throw new InvalidDataException("Missing torso reference guides");
   var guides=new P[Guides.Length];
   for(int i=0;i<Guides.Length;i++){
    Vector3 value;if(!current.TryGetValue(Guides[i],out value))throw new InvalidDataException("Missing torso guide");guides[i]=Point(value);
    if(!Finite(value.sqrMagnitude)||!Finite(reference[i].sqrMagnitude)||(i>0&&reference[i].y-reference[i-1].y<=.001f))throw new InvalidDataException("Invalid torso guide order or coordinates");
   }
   var vertices=body.vertices;var triangles=body.triangles;var result=new Vector3[600];
   for(int ring=0;ring<15;ring++){
    double y=.885+(1.43-.885)*ring/14;int segment=0;while(segment<reference.Length-2&&reference[segment+1].y<y)segment++;
    double t=(y-reference[segment].y)/(reference[segment+1].y-reference[segment].y);P origin=guides[segment]*(1-t)+guides[segment+1]*t;
    var edges=Section(vertices,triangles,origin);P centroid=new P();double perimeter=0;
    foreach(var edge in edges){double length=(edge.b-edge.a).Length;centroid=centroid+(edge.a+edge.b)*(.5*length);perimeter+=length;}
    if(perimeter<.01)throw new InvalidDataException("Degenerate torso section");origin=centroid*(1/perimeter);
    for(int sector=0;sector<40;sector++){
     double angle=2*Math.PI*sector/40,dx=Math.Cos(angle),dz=Math.Sin(angle),nearest=double.PositiveInfinity;
     foreach(var edge in edges){P start=edge.a-origin,delta=edge.b-edge.a;double den=dx*delta.z-dz*delta.x;if(Math.Abs(den)<=1e-12)continue;double distance=(start.x*delta.z-start.z*delta.x)/den,u=(start.x*dz-start.z*dx)/den;if(distance>0&&u>=0&&u<=1)nearest=Math.Min(nearest,distance);}
     if(!Finite(nearest)||nearest<.015||nearest>.4)throw new InvalidDataException("Torso cage ray has no valid skin intersection");result[ring*40+sector]=new P(origin.x+dx*nearest,origin.y,origin.z+dz*nearest).Vector;
    }
   }
   return result;
  }
 }
}
