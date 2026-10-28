using System;
using System.Linq;
using System.Collections.Generic;
struct Vector3i : IEquatable<Vector3i> {
 public int x,y,z; public Vector3i(int a,int b,int c){x=a;y=b;z=c;}
 public bool Equals(Vector3i p)=>x==p.x&&y==p.y&&z==p.z;
 public override bool Equals(object o)=>o is Vector3i p&&Equals(p);
 public override int GetHashCode()=>HashCode.Combine(x,y,z);
 public override string ToString()=>$"({x},{y},{z})";
}
partial class RankingFixture {
 static int count;
 static Vector3i P(int x,int y=0,int z=0)=>new Vector3i(x,y,z);
 static Vector3i[] Rank(IEnumerable<Vector3i> p)=>RankPoiLightPositions(p.OrderBy(v=>v.x).ThenBy(v=>v.y).ThenBy(v=>v.z).ToList());
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 static void Main(){
 Check(Rank(Array.Empty<Vector3i>()).Length==0,"empty");
 Check(Rank(new[]{P(7,3,2)}).Single().Equals(P(7,3,2)),"single");
 var layout=(from x in new[]{0,25,50} from y in new[]{0,3,6} from z in new[]{0,20} select P(x,y,z)).ToArray();
 var ranked=Rank(layout);
 Check(ranked.Length==layout.Length&&ranked.Distinct().Count()==layout.Length,"full unique permutation");
 Check(Rank(layout.Reverse()).SequenceEqual(ranked),"discovery order independence");
 Check(ranked.Take(6).Select(p=>p.y).Distinct().Count()==3,"wide building covers all three floors in first six");
 Check(ranked.Take(6).Min(p=>p.x)==0&&ranked.Take(6).Max(p=>p.x)==50,"wide building covers both horizontal ends");
 Check(Rank(layout.Select(p=>P(p.x+500,p.y-100,p.z+900))).SequenceEqual(ranked.Select(p=>P(p.x+500,p.y-100,p.z+900))),"translation invariant");
 for(int n=0;n<ranked.Length;n++)Check(ranked.Take(n).SequenceEqual(Rank(layout).Take(n+1).Take(n)),"nested prefix "+n);
 var dense=Enumerable.Range(0,21).Select(x=>P(x)).Concat(new[]{P(0,4),P(20,4),P(0,8),P(20,8)}).ToArray();
 Check(Rank(dense).Take(5).Select(p=>p.y).Distinct().Count()==3,"dense ground floor does not exclude upper floors");
 var flat=Rank(new[]{P(0),P(1),P(25),P(49),P(50)});
 Check(flat.Take(3).Select(p=>p.x).OrderBy(x=>x).SequenceEqual(new[]{0,25,50}),"same floor far coverage before adjacent lamps");
 Check(Rank(new[]{P(-1),P(1)}).First().Equals(P(-1)),"lexical equal distance tie");
 var jitter=Rank(new[]{P(0),P(20),P(40),P(20,1)});
 Check(jitter.Take(3).Select(p=>p.x).Distinct().Count()==3,"one block height jitter does not dominate wide coverage");
 Check(Rank(new[]{P(int.MinValue),P(0),P(int.MaxValue)}).Length==3,"extreme coordinates avoid squared integer overflow");
 Console.WriteLine(count+" checks passed");
 }
}
