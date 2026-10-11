using System.Diagnostics;
record struct Vector3i(int x,int y,int z);
static class World { public static int toChunkXZ(int x)=>x>>4; }
static class Program {
 static void Equal() { if(!Before.Coarse.SetEquals(After.Coarse)||!Before.Positions.SetEquals(After.Positions))throw new Exception("Snapshot mismatch"); }
 static void Add(Vector3i p) { Before.RegisterFarmPlot(p);After.RegisterFarmPlot(p); }
 static void Clear(){Before.Clear();After.Clear();}
 static void Publish(){Before.PublishPendingSnapshot();After.PublishPendingSnapshot();Equal();}
 static double Time(Action a,int n){var t=Stopwatch.StartNew();for(int i=0;i<n;i++)a();return t.Elapsed.TotalMilliseconds/n;}
 static void Bench(string name,IEnumerable<Vector3i> points,int iterations){Clear();foreach(var p in points)Add(p);Publish();for(int i=0;i<20;i++){Before.Rebuild();After.Rebuild();}var old=new List<double>();var now=new List<double>();for(int t=0;t<9;t++){if(t%2==0){old.Add(Time(Before.Rebuild,iterations));now.Add(Time(After.Rebuild,iterations));}else{now.Add(Time(After.Rebuild,iterations));old.Add(Time(Before.Rebuild,iterations));}}old.Sort();now.Sort();Equal();Console.WriteLine($"{name}: plots={After.Positions.Count}, oldMedianMs={old[4]:F6}, newMedianMs={now[4]:F6}, ratio={old[4]/now[4]:F2}x");}
 static void Main(){var r=new Random(20261010);var positions=new List<Vector3i>();Clear();for(int i=0;i<2000;i++){if(i%101==0){Clear();positions.Clear();}var p=new Vector3i(r.Next(-256,257),r.Next(-1,257),r.Next(-256,257));Add(p);Add(p);positions.Add(p);if(i%3==0){var q=positions[r.Next(positions.Count)];Before.UnregisterFarmPlot(q);After.UnregisterFarmPlot(q);}Publish();}Console.WriteLine("PASS 2000 mutation/publication comparisons including duplicate registration, removal, clear, invalid heights, negative coordinates.");Bench("empty",Array.Empty<Vector3i>(),1000);Bench("dense four chunks",from x in Enumerable.Range(-16,32) from z in Enumerable.Range(-16,32) select new Vector3i(x,45,z),100);Bench("sparse 64 chunks",Enumerable.Range(0,64).Select(i=>new Vector3i(i*32-1024,45,i*32-1024)),100);Console.WriteLine("Scope: actual extracted pre/post mutation and rebuild methods; .NET9 isolated timing, Vector3i/World doubles; not native Mono or FPS.");}
}
