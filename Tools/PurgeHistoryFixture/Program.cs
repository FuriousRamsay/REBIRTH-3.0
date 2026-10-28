using System;
using System.IO;
using System.Xml.Linq;

// Production history implementation; only game/world dependencies are substituted.
namespace UnityEngine { static class Time { internal static float realtimeSinceStartup; } }
static class Log { internal static int Warnings; internal static void Warning(string text) { Warnings++; Console.WriteLine(text); } }
sealed class World { internal ulong worldTime=100; internal bool IsRemote()=>false; }
sealed class GameManager { internal static GameManager Instance=new GameManager(); internal World World=new World(); }
sealed class RebirthPoiWorldBinding {
 internal static RebirthPoiWorldBinding Current; internal string Directory,NativeWorldId="fixture"; internal bool IsCurrent=true;
 internal static bool TryCapture(World world,out RebirthPoiWorldBinding value){value=Current;return value!=null&&value.IsCurrent;}
}
sealed class RebirthPoiIdentity { internal string Key="poi"; }
sealed class ClearRecord { internal ulong WorldTime; }
sealed class RebirthPoiClearanceRecord { internal RebirthPoiIdentity Identity=new RebirthPoiIdentity(); internal long Epoch; internal ClearRecord Clear; }
static class RebirthPoiClearanceLedger { internal const int MaximumRecords=200000; }
sealed class Snapshot { internal RebirthPoiClearanceRecord Record=new RebirthPoiClearanceRecord(); internal bool TryGet(RebirthPoiIdentity id,out RebirthPoiClearanceRecord record){record=Record;return id.Key==record.Identity.Key;} }
sealed class RebirthPoiWorldStore { internal Snapshot Published=new Snapshot(); }
sealed class RebirthPoiWorldLifecycle { internal static RebirthPoiWorldLifecycle Instance=new RebirthPoiWorldLifecycle(); internal RebirthPoiWorldStore Store=new RebirthPoiWorldStore(); internal bool TryGetEvidenceStore(out RebirthPoiWorldStore store){store=Store;return true;} }
static class RebirthPurgeClearNotification { internal static string ContributorNames(RebirthPoiClearanceRecord record)=>"Recovered"; }
static class RebirthPurgeDiscoveryNotification { internal static string CleanDisplayName(string name)=>(name??"").Trim(); }

static class Program {
 static int checks;
 static void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static void Main(){
  string root=Path.Combine(Path.GetTempPath(),"rebirth-history-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(root);
  var binding=new RebirthPoiWorldBinding{Directory=root};RebirthPoiWorldBinding.Current=binding;
  string file=Path.Combine(root,"poi_history.xml"),temporary=file+".tmp";
  var record=RebirthPoiWorldLifecycle.Instance.Store.Published.Record;
  try {
   // A directory at the exact temporary-file path deterministically rejects Save.
   Directory.CreateDirectory(temporary);
   RebirthPurgePoiHistory.Discover(record.Identity,"First visitor");
   Check(Log.Warnings==1&&!File.Exists(file),"failed history save remains unsaved");
   long revision=RebirthPurgePoiHistory.Revision;
   UnityEngine.Time.realtimeSinceStartup=4;RebirthPurgePoiHistory.Ensure();
   Check(Log.Warnings==1,"retry is bounded, not performed on every observation");
   Directory.Delete(temporary);
   GameManager.Instance.World.worldTime=999;
   UnityEngine.Time.realtimeSinceStartup=5;RebirthPurgePoiHistory.Ensure();
   var node=XDocument.Load(file).Root.Element("poi");
   Check((string)node.Attribute("discoverer")=="First visitor"&&(ulong)node.Attribute("discovered")==100,"retry saves original discoverer and time");
   Check(RebirthPurgePoiHistory.Revision==revision,"save-only retry does not republish unchanged map data");
   RebirthPurgePoiHistory.Purged(record,"First visitor\nSecond visitor");
   RebirthPurgePoiHistory.Reset();RebirthPurgePoiHistory.Ensure();
   var restored=RebirthPurgePoiHistory.Get(record);
   Check(restored.Discoverer=="First visitor"&&restored.DiscoveredAt==100&&restored.Contributors=="First visitor\nSecond visitor"&&restored.PurgedAt==999,"reload retains discovery and multiline purge history");
   record.Epoch=1;
   Check(RebirthPurgePoiHistory.Get(record)==null,"old epoch history is not attributed to a reset epoch");
   RebirthPurgePoiHistory.Reset();
   const string damaged="<poi_history broken";
   File.WriteAllText(file,damaged);
   RebirthPurgePoiHistory.Ensure();
   RebirthPurgePoiHistory.Discover(record.Identity,"New observation");
   UnityEngine.Time.realtimeSinceStartup=20;RebirthPurgePoiHistory.Ensure();
   RebirthPurgePoiHistory.Reset();
   Check(File.ReadAllText(file)==damaged,"unreadable original history is never replaced by partial observations");
   Console.WriteLine("RESULT "+checks+" PASS; production history file behavior only, no native game qualification.");
  } finally {
   binding.IsCurrent=false;RebirthPurgePoiHistory.Reset();
   Directory.Delete(root,true);
  }
 }
}
