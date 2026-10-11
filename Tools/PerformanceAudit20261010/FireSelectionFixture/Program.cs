using System.Diagnostics;
record struct Vector3i(int x,int y,int z);
class RebirthFireState { public Vector3i Position; public ulong IgnitedWorldTime; }
static class RebirthFireDefaults { public const int MaxSimulatedFiresPerWindow=64, NewFrontSimulationReserve=8; }
class Fixture {
 Dictionary<Vector3i,RebirthFireState> fires=new(); Dictionary<Vector3i,ulong> lastSimulatedWorldTime=new();
 HashSet<Vector3i> simulationSelection=new(); List<RebirthFireState> simulationCandidates=new();
 int simulationEligibleLastWindow,simulationNewFrontSelectedLastWindow,simulationSelectedLastWindow,simulationNeverRunSelectedLastWindow;
 bool HasActiveSimulationVisibility(Vector3i p)=>true;
private void BuildSimulationSelection()
    {
        simulationSelection.Clear();
        simulationCandidates.Clear();

        foreach (RebirthFireState state in fires.Values)
        {
            if (HasActiveSimulationVisibility(state.Position))
                simulationCandidates.Add(state);
        }

        simulationEligibleLastWindow = simulationCandidates.Count;
        simulationNewFrontSelectedLastWindow = 0;

        // First spend at most eight of the unchanged 64 slots on the newest visible
        // positions that have never simulated. This makes a newly ignited second POI
        // start progressing promptly without allowing it to monopolize the window.
        simulationCandidates.Sort(delegate(RebirthFireState left, RebirthFireState right)
        {
            bool leftNever = !lastSimulatedWorldTime.ContainsKey(left.Position);
            bool rightNever = !lastSimulatedWorldTime.ContainsKey(right.Position);
            if (leftNever != rightNever)
                return leftNever ? -1 : 1;
            int ignition = right.IgnitedWorldTime.CompareTo(left.IgnitedWorldTime);
            if (ignition != 0)
                return ignition;
            return CompareSimulationPosition(left.Position, right.Position);
        });

        int newFrontLimit = Math.Min(
            RebirthFireDefaults.NewFrontSimulationReserve,
            RebirthFireDefaults.MaxSimulatedFiresPerWindow);
        for (int i = 0; i < simulationCandidates.Count &&
            simulationNewFrontSelectedLastWindow < newFrontLimit; i++)
        {
            RebirthFireState candidate = simulationCandidates[i];
            if (lastSimulatedWorldTime.ContainsKey(candidate.Position))
                break;
            if (simulationSelection.Add(candidate.Position))
                simulationNewFrontSelectedLastWindow++;
        }

        // Fill every remaining slot by strict fairness: never-run positions oldest
        // ignition first, then previously run positions by the oldest simulation time.
        // This guarantees progress even while another fire continues adding new blocks.
        simulationCandidates.Sort(delegate(RebirthFireState left, RebirthFireState right)
        {
            ulong leftLast;
            bool leftNever = !lastSimulatedWorldTime.TryGetValue(left.Position, out leftLast);
            ulong rightLast;
            bool rightNever = !lastSimulatedWorldTime.TryGetValue(right.Position, out rightLast);
            if (leftNever != rightNever)
                return leftNever ? -1 : 1;
            if (leftNever)
            {
                int ignition = left.IgnitedWorldTime.CompareTo(right.IgnitedWorldTime);
                if (ignition != 0)
                    return ignition;
            }
            else
            {
                int last = leftLast.CompareTo(rightLast);
                if (last != 0)
                    return last;
            }
            return CompareSimulationPosition(left.Position, right.Position);
        });

        int limit = Math.Min(
            RebirthFireDefaults.MaxSimulatedFiresPerWindow,
            simulationCandidates.Count);
        for (int i = 0; i < simulationCandidates.Count && simulationSelection.Count < limit; i++)
            simulationSelection.Add(simulationCandidates[i].Position);

        int neverRun = 0;
        foreach (Vector3i position in simulationSelection)
        {
            if (!lastSimulatedWorldTime.ContainsKey(position))
                neverRun++;
        }

        simulationSelectedLastWindow = simulationSelection.Count;
        simulationNeverRunSelectedLastWindow = neverRun;
    }
private static int CompareSimulationPosition(Vector3i left, Vector3i right)
    {
        int x = left.x.CompareTo(right.x);
        if (x != 0)
            return x;
        int z = left.z.CompareTo(right.z);
        if (z != 0)
            return z;
        return left.y.CompareTo(right.y);
    }
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
