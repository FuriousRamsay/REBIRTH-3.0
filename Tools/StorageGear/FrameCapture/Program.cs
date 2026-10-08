using System.Text.Json;
namespace RebirthProfiler;
class FrameCapture {
 static void Main(string[] args) {
  using var capture=new EtwLive(int.Parse(args[0]));
  if(!capture.Start()) throw new Exception(capture.Error);
  string output=args[1], marker=output+".mark", stop=output+".stop";
  var results=new List<object>(); string label="warmup"; DateTime start=DateTime.UtcNow;
  while(!File.Exists(stop)) {
   Thread.Sleep(100);
   if(!File.Exists(marker))continue;
   string next=File.ReadAllText(marker);File.Delete(marker);
   var times=capture.Snapshot().Where(t=>t>=start.ToFileTimeUtc()).ToList();
   var ms=times.Zip(times.Skip(1),(a,b)=>(b-a)/10000.0).Where(t=>t>1).OrderBy(t=>t).ToArray();
   if(ms.Length>0)results.Add(new {label,seconds=(DateTime.UtcNow-start).TotalSeconds,frames=ms.Length,fps=1000/ms.Average(),medianMs=ms[ms.Length/2],p99Ms=ms[(int)(ms.Length*.99)],worstMs=ms[^1]});
   File.WriteAllText(output,JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
   label=next;start=DateTime.UtcNow;
  }
 }
}
