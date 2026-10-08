using System;
using System.Collections.Generic;
using System.Reflection;
public struct Vector3i : IEquatable<Vector3i> {
 public int x,y,z;
 public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}
 public bool Equals(Vector3i o){return x==o.x&&y==o.y&&z==o.z;}
 public override bool Equals(object o){return o is Vector3i&&Equals((Vector3i)o);}
 public override int GetHashCode(){unchecked{return ((x*397)^y)*397^z;}}
}
// PRODUCTION_CLASSES
public static class FireClusterChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Add(int x,int y,int z,bool normal){var p=new Vector3i(x,y,z);Before.FireCandidatePositions.Add(p);After.FireCandidatePositions.Add(p);if(normal){Before.NormalCoverage.Add(p);After.NormalCoverage.Add(p);}}
 public static void Run(){
  var offsets=(Vector3i[])typeof(After).GetField("ClusterNeighborOffsets",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  Check(offsets.Length==24,"neighbor count");int k=0;
  for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)for(int z=-2;z<=2;z++){int d=Math.Abs(x)+Math.Abs(y)+Math.Abs(z);if(d>0&&d<=2)Check(offsets[k++].Equals(new Vector3i(x,y,z)),"offset order");}
  var random=new Random(71006);
  for(int test=0;test<256;test++){
   Before.FireCandidatePositions.Clear();After.FireCandidatePositions.Clear();Before.NormalCoverage.Clear();After.NormalCoverage.Clear();
   if(test==1){Add(0,0,0,true);Add(2,0,0,false);Add(4,0,0,true);Add(7,0,0,true);Add(7,1,1,false);}
   else if(test==2){Add(int.MaxValue,0,0,true);Add(int.MinValue,0,0,false);Add(-1,-1,-1,true);}
   else if(test==3){for(int x=-8;x<=8;x++)for(int z=-8;z<=8;z++)Add(x,0,z,(x+z)%3==0);}
   else if(test>3){int count=random.Next(1,401);for(int i=0;i<count;i++)Add(random.Next(-15,16),random.Next(-4,5),random.Next(-15,16),random.Next(4)==0);}
   Before.Run();After.Run();Check(Before.ClusterByPosition.Count==After.ClusterByPosition.Count,"position count");
   foreach(var pair in Before.ClusterByPosition){int found;Check(After.ClusterByPosition.TryGetValue(pair.Key,out found)&&found==pair.Value,"exact cluster ID layout "+test);}
   Check(Before.ClusterNormalCounts.Count==After.ClusterNormalCounts.Count,"cluster count");
   for(int i=0;i<Before.ClusterNormalCounts.Count;i++)Check(Before.ClusterNormalCounts[i]==After.ClusterNormalCounts[i],"normal count");
  }
 }
}
