using System.Diagnostics;
class GameManager { public static GameManager Instance=new(); public object World=new(); }
static class GameIO { public static string DirectoryName=Path.GetFullPath("FixtureSaveA"); public static string GetSaveGameDir()=>DirectoryName; }
class Program {
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(){
  var a=RebirthNpcSaveScope.ObserveCurrent();var b=RebirthNpcSaveScope.ObserveCurrent();
  Check(a.Fingerprint==RebirthNpcSaveScope.ComputeFingerprint(GameIO.DirectoryName),"fingerprint compatibility");
  Check(a.ProcessSaveGeneration==b.ProcessSaveGeneration && object.ReferenceEquals(a.Fingerprint,b.Fingerprint),"unchanged directory reuses fingerprint");
  a.Fingerprint="tampered";Check(RebirthNpcSaveScope.ObserveCurrent().Fingerprint==b.Fingerprint,"caller snapshot mutation cannot corrupt cache");
  GameManager.Instance.World=null;Check(RebirthNpcSaveScope.ObserveCurrent().SaveDirectory=="","menu exposes no previous save");
  GameManager.Instance.World=new();Check(RebirthNpcSaveScope.ObserveCurrent().ProcessSaveGeneration==b.ProcessSaveGeneration,"same-save reload preserves generation");
  GameIO.DirectoryName=Path.GetFullPath("FixtureSaveB");var c=RebirthNpcSaveScope.ObserveCurrent();
  Check(c.ProcessSaveGeneration==b.ProcessSaveGeneration+1 && c.Fingerprint!=b.Fingerprint && c.IsSecondOrLaterSave,"new save changes identity and generation");
  GameIO.DirectoryName=GameIO.DirectoryName.ToUpperInvariant()+Path.DirectorySeparatorChar;
  Check(RebirthNpcSaveScope.ObserveCurrent().Fingerprint==c.Fingerprint,"case and trailing separator equivalence");
  GameIO.DirectoryName="";Check(RebirthNpcSaveScope.ObserveCurrent().Fingerprint=="","unavailable directory exposes no fingerprint");
  GameIO.DirectoryName=Path.GetFullPath("FixtureSaveB");for(int i=0;i<100;i++)RebirthNpcSaveScope.ObserveCurrent();
  long before=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();for(int i=0;i<10000;i++)RebirthNpcSaveScope.ObserveCurrent();sw.Stop();
  Console.WriteLine($"Current ObserveCurrent: mean_us={sw.Elapsed.TotalMilliseconds/10:F3} bytes_per_call={(GC.GetAllocatedBytesForCurrentThread()-before)/10000.0:F2}; native GameIO/World doubled, not Unity runtime.");
  for(int i=0;i<100;i++)BaselineNpcSaveScope.ObserveCurrent();
  Check(BaselineNpcSaveScope.ObserveCurrent().Fingerprint==RebirthNpcSaveScope.ObserveCurrent().Fingerprint,"baseline/current fingerprint parity");
  before=GC.GetAllocatedBytesForCurrentThread();sw.Restart();for(int i=0;i<10000;i++)BaselineNpcSaveScope.ObserveCurrent();sw.Stop();
  Console.WriteLine($"Baseline ObserveCurrent: mean_us={sw.Elapsed.TotalMilliseconds/10:F3} bytes_per_call={(GC.GetAllocatedBytesForCurrentThread()-before)/10000.0:F2}; identical stub world/directory, not Unity runtime.");
 }
}
