from pathlib import Path
import hashlib
root=Path('Scripts/Fire/RebirthFireSystem.cs')
s=root.read_text(encoding='utf-8-sig')
def member(sig):
 start=s.index(sig); p=s.index('{',start); level=1; end=p+1
 while level:
  level += (s[end]=='{')-(s[end]=='}'); end+=1
 return s[start:end]
code='''using System.Diagnostics;
record struct Vector3i(int x,int y,int z);
class RebirthFireState { public Vector3i Position; public ulong IgnitedWorldTime; }
static class RebirthFireDefaults { public const int MaxSimulatedFiresPerWindow=64, NewFrontSimulationReserve=8; }
class Fixture {
 Dictionary<Vector3i,RebirthFireState> fires=new(); Dictionary<Vector3i,ulong> lastSimulatedWorldTime=new();
 HashSet<Vector3i> simulationSelection=new(); List<RebirthFireState> simulationCandidates=new();
 int simulationEligibleLastWindow,simulationNewFrontSelectedLastWindow,simulationSelectedLastWindow,simulationNeverRunSelectedLastWindow;
 bool HasActiveSimulationVisibility(Vector3i p)=>true;
'''+member('private void BuildSimulationSelection()')+'\n'+member('private static int CompareSimulationPosition(')+'''
 public static void Main() {
  foreach(int count in new[]{0,64,512}) {
   var f=new Fixture(); for(int i=0;i<count;i++){var p=new Vector3i(i,0,0);f.fires.Add(p,new(){Position=p,IgnitedWorldTime=(ulong)i});if(i%2==0)f.lastSimulatedWorldTime.Add(p,(ulong)i);}
   for(int i=0;i<100;i++)f.BuildSimulationSelection();
   if(f.simulationSelection.Count!=Math.Min(64,count))throw new Exception("selection size");
   if(count>=64 && f.simulationNewFrontSelectedLastWindow!=8)throw new Exception("new-front reserve");
   long allocated=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();const int iterations=3000;
   for(int i=0;i<iterations;i++)f.BuildSimulationSelection();sw.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
   Console.WriteLine($"count={count} selected={f.simulationSelection.Count} mean_ms={sw.Elapsed.TotalMilliseconds/iterations:F6} bytes_per_call={(double)allocated/iterations:F2}");
  }
  Console.WriteLine("PASS size and reserve. Production selection body; visibility/native vector hashing doubled. Not native runtime/frame timing.");
 }
}
'''
Path('Tools/PerformanceAudit20261010/FireSelectionFixture/Program.cs').write_text(code)
print('production_source_sha256='+hashlib.sha256(root.read_bytes()).hexdigest())
