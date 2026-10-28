// Entire actual store classes, record schemas and shared XML/durable helpers are inserted.
// Backing domain services and game authority/world bindings are disclosed doubles.
using System.Threading;using System.Security.Cryptography;using System;using System.IO;using System.Xml;using System.Xml.Linq;using System.Text;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Reflection;
public class WorldBase{public bool Remote;public bool IsRemote()=>Remote;}
public class World:WorldBase{}
public class GameManager{public static GameManager Instance=new GameManager();public World World=new World();}
public class ConnectionManager{public bool IsServer=true;}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
static class GameIO{public static string Root;public static string GetSaveGameDir()=>Root;}
static class Log{public static void Warning(string s){}public static void Error(string s){}}
static class RebirthNpcPersistenceCoordinator{public static bool IsCheckpointWrite,IsRecoveryInstalling;}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
// Explicit external-fence inputs for this ordinary typed-store cohort. They
// cannot install recovery, publish receipts or manufacture a prepared state.
static class FixtureFences{internal static bool Quarantine=false;}
public static partial class RebirthNpcPersistenceFile{internal static bool HasQuarantineWriterFence=>FixtureFences.Quarantine;}
static class RebirthNpcPersistenceTimelineService{internal static bool IsAlignmentFailed=false;}
static class RebirthNpcLiveRecoveryGroup{
 internal static readonly HashSet<string> Held=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 internal static bool Fences(string path)=>Held.Contains(Path.GetFullPath(path));
}
static class RebirthNpcRetainedWorkCustody{
 internal static readonly HashSet<string> Held=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 internal static bool BlocksWrite(string path)=>Held.Contains(Path.GetFullPath(path));
}
#if !ACTUAL_PUBLICATIONS
static class RebirthNpcRecoveryPublications{
 // Synthetic installed input exercises refusal routing only. This adapter
 // never saves a recovery candidate or returns successful installed handling.
 internal static readonly HashSet<string> InstalledInputs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);internal static int Attempts;
 internal static bool IsInstalled(string path)=>InstalledInputs.Contains(Path.GetFullPath(path));
 internal static bool TryPublishInstalled(string path,XmlDocument document,out bool handled,out string error){handled=IsInstalled(path);error="Installed recovery publications unavailable in ordinary store cohort";if(handled){Attempts++;return false;}return !File.Exists(path+".recovery-publications.xml");}
}
#endif
// PRODUCTION_TYPES
public sealed class RebirthNpcFactionStanding{public string FactionId,Counterparty;public float Standing;public uint Revision;public long LastChangedUtcTicks;}
static class RebirthNpcSocialService{
 internal static readonly object RecoverySync=new object();internal static object RecoveryEpoch=new object();
 // Administrative recovery publication is outside this ordinary store cohort.
 // Denial cannot manufacture a prepared generation or retirement permission.
 internal static bool TryPrepareRecovery(RebirthNpcRelationshipSnapshot[] a,RebirthNpcSocialMemorySnapshot[] b,Guid[] c,out RebirthNpcPreparedRecovery candidate,out string error){candidate=null;error="Administrative recovery adapter unavailable in ordinary store cohort";return false;}
 public static RebirthNpcRelationshipSnapshot[] Relationships=new RebirthNpcRelationshipSnapshot[0];public static RebirthNpcSocialMemorySnapshot[] Memories=new RebirthNpcSocialMemorySnapshot[0];public static Guid[] Replay=new Guid[0];
 public static RebirthNpcRelationshipSnapshot[] GetRelationshipSnapshots()=>Relationships;public static RebirthNpcSocialMemorySnapshot[] GetMemorySnapshots()=>Memories;public static Guid[] GetAppliedEventSnapshot()=>Replay;
 public static void Restore(RebirthNpcRelationshipSnapshot[] a,RebirthNpcSocialMemorySnapshot[] b,Guid[] c){Relationships=a;Memories=b;Replay=c;}
 public static void ResetForWorldChange()=>Restore(new RebirthNpcRelationshipSnapshot[0],new RebirthNpcSocialMemorySnapshot[0],new Guid[0]);
}
static class RebirthNpcFactionGameplayService{internal static readonly object RecoverySync=new object();internal static object RecoveryEpoch=new object();internal static bool TryPrepareRecovery(RebirthNpcFactionStanding[] rows,out RebirthNpcPreparedRecovery candidate,out string error){candidate=null;error="Administrative recovery adapter unavailable in ordinary store cohort";return false;}public static RebirthNpcFactionStanding[] Values=new RebirthNpcFactionStanding[0];public static RebirthNpcFactionStanding[] GetSnapshots()=>Values;public static void Restore(RebirthNpcFactionStanding[] v){Values=v;}public static void ResetForWorldChange(){Values=new RebirthNpcFactionStanding[0];}}
static class RebirthNpcProgressionProjectionRevisionService{
 private static readonly object Sync=new object();internal static readonly Dictionary<RebirthNpcStableId,long> Revisions=new Dictionary<RebirthNpcStableId,long>();
 // ACTUAL_REVISION_RETIREMENT
 public static void Reset(){lock(Sync)Revisions.Clear();}
}
static class RebirthNpcProgressionService{
 internal static object RecoveryEpoch=new object();
 internal static void CaptureRetirement(out RebirthNpcProgressionRecord[] rows,out Guid[] replay,out object epoch){rows=Records.ToArray();replay=Replay.ToArray();epoch=RecoveryEpoch;}
 public static RebirthNpcProgressionRecord[] Records=new RebirthNpcProgressionRecord[0];public static Guid[] Replay=new Guid[0];
 public static void ImportRecords(IEnumerable<RebirthNpcProgressionRecord> r,IEnumerable<Guid> ids,bool replace){Records=r.ToArray();Replay=ids.ToArray();}
 public static RebirthNpcProgressionRecord[] ExportRecords()=>Records;public static Guid[] ExportReplayIds()=>Replay;
}
static class RebirthNpcAdvancedProgressionService{
 internal static object RecoveryEpoch=new object();
 internal static void CaptureRetirement(out RebirthNpcAdvancedProgressionRecord[] rows,out RebirthNpcMentorshipSession[] sessions,out object epoch){rows=Records.ToArray();sessions=Sessions.ToArray();epoch=RecoveryEpoch;}
 public static RebirthNpcAdvancedProgressionRecord[] Records=new RebirthNpcAdvancedProgressionRecord[0];public static RebirthNpcMentorshipSession[] Sessions=new RebirthNpcMentorshipSession[0];
 public static void Import(IEnumerable<RebirthNpcAdvancedProgressionRecord> r,IEnumerable<RebirthNpcMentorshipSession> s){Records=r.ToArray();Sessions=s.ToArray();}
 public static RebirthNpcAdvancedProgressionRecord[] ExportRecords()=>Records;public static RebirthNpcMentorshipSession[] ExportMentorships()=>Sessions;
}
static class RebirthNpcEconomyLogisticsService{
 internal static object RecoveryEpoch=new object();internal static bool OpaqueCustody=false;
 internal static void CaptureRetirement(out Dictionary<string,long[]> rows,out Dictionary<string,Dictionary<string,long>> pending,out object epoch,out bool opaque){rows=ExportCheckpoints();pending=ExportPendingRecipeTicks();epoch=RecoveryEpoch;opaque=OpaqueCustody;}
 public static readonly Dictionary<string,long[]> Records=new Dictionary<string,long[]>();static readonly Dictionary<string,Dictionary<string,long>> Pending=new Dictionary<string,Dictionary<string,long>>();
 internal static Dictionary<string,long[]> ExportCheckpoints()=>new Dictionary<string,long[]>(Records);
 internal static Dictionary<string,Dictionary<string,long>> ExportPendingRecipeTicks()=>new Dictionary<string,Dictionary<string,long>>(Pending);
 internal static void ImportCheckpoint(string id,long ticks,long revision,IDictionary<string,long> pending=null){Records[id]=new[]{ticks,revision};Pending[id]=pending==null?new Dictionary<string,long>():new Dictionary<string,long>(pending);}
 internal static void ClearPersistentRuntimeState(bool clearEndpoints=true){Records.Clear();Pending.Clear();}
}
// PRODUCTION_HELPERS
// PRODUCTION_STORES
#if !ACTUAL_PUBLICATIONS
static class Program{
 static readonly Type[] Stores={typeof(RebirthNpcSocialPersistenceStore),typeof(RebirthNpcProgressionPersistenceStore),typeof(RebirthNpcAdvancedProgressionPersistenceStore),typeof(RebirthNpcEconomyPersistenceStore)};
 static void Need(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Folder(string root,string name){GameIO.Root=Path.Combine(root,name);Directory.CreateDirectory(GameIO.Root);GameManager.Instance.World=new World();}
 static void LoadAll(){RebirthNpcSocialPersistenceStore.EnsureLoaded();RebirthNpcProgressionPersistenceStore.EnsureLoaded();RebirthNpcAdvancedProgressionPersistenceStore.EnsureLoaded();RebirthNpcEconomyPersistenceStore.Load();}
 static bool CanResetAll()=>RebirthNpcSocialPersistenceStore.CanReset&&RebirthNpcProgressionPersistenceStore.CanReset&&RebirthNpcAdvancedProgressionPersistenceStore.CanReset&&RebirthNpcEconomyPersistenceStore.CanReset;
 static bool FlushAll(){bool a=RebirthNpcSocialPersistenceStore.TryFlush(),b=RebirthNpcProgressionPersistenceStore.TryFlush(),c=RebirthNpcAdvancedProgressionPersistenceStore.TryFlush(),d=RebirthNpcEconomyPersistenceStore.TryFlush();return a&&b&&c&&d;}
 static bool ResetAll(){if(!CanResetAll())return false;return RebirthNpcSocialPersistenceStore.TryReset(false)&&RebirthNpcProgressionPersistenceStore.TryReset(false)&&RebirthNpcAdvancedProgressionPersistenceStore.TryReset(false)&&RebirthNpcEconomyPersistenceStore.TryReset(false);}
 static void Seed(){var id=new RebirthNpcStableId(1,2);RebirthNpcSocialService.Relationships=new[]{new RebirthNpcRelationshipSnapshot{Key=id+"|owner",Trust=.5f,Revision=1}};RebirthNpcFactionGameplayService.Values=new[]{new RebirthNpcFactionStanding{FactionId="group",Counterparty="owner",Standing=.5f}};
  var p=new RebirthNpcProgressionRecord{NpcId=id};p.Entries.Add("profession.general",new RebirthNpcProgressionEntryRecord{ProgressionId="profession.general",CumulativeXp=50});RebirthNpcProgressionService.Records=new[]{p};RebirthNpcProgressionPersistenceStore.MarkDirty();
  var a=new RebirthNpcAdvancedProgressionRecord{NpcId=id};a.Certifications.Add("cert.farming.journeyman");RebirthNpcAdvancedProgressionService.Records=new[]{a};RebirthNpcAdvancedProgressionPersistenceStore.MarkDirty();RebirthNpcEconomyLogisticsService.ImportCheckpoint("settlement-a",10,1);RebirthNpcEconomyPersistenceStore.MarkDirty();}
 static void ProcessRestart(){foreach(var type in Stores)foreach(string name in new[]{"loaded","dirty","flushing","loadFailed","boundPath","boundWorld","savedState"}){var field=type.GetField(name,BindingFlags.NonPublic|BindingFlags.Static);if(field!=null)field.SetValue(null,field.FieldType==typeof(bool)?(object)false:null);}RebirthNpcSocialService.ResetForWorldChange();RebirthNpcFactionGameplayService.ResetForWorldChange();RebirthNpcProgressionService.Records=new RebirthNpcProgressionRecord[0];RebirthNpcAdvancedProgressionService.Records=new RebirthNpcAdvancedProgressionRecord[0];RebirthNpcEconomyLogisticsService.ClearPersistentRuntimeState();}
 static bool Throws(Action action){try{action();return false;}catch{return true;}}
 static void VerifyInstalledPublicationRefusal(){
  string[] files={"RebirthNpcProgression.xml","RebirthNpcAdvancedProgression.xml"};
  Action[] dirty={RebirthNpcProgressionPersistenceStore.MarkDirty,RebirthNpcAdvancedProgressionPersistenceStore.MarkDirty};
  Func<bool>[] flush={RebirthNpcProgressionPersistenceStore.TryFlush,RebirthNpcAdvancedProgressionPersistenceStore.TryFlush};
  for(int i=0;i<files.Length;i++){
   string path=Path.Combine(GameIO.Root,files[i]);byte[] saved=File.ReadAllBytes(path);int attempts=RebirthNpcRecoveryPublications.Attempts;
   dirty[i]();RebirthNpcRecoveryPublications.InstalledInputs.Add(path);
   try{Need(!flush[i]()&&RebirthNpcRecoveryPublications.Attempts==attempts+1&&File.ReadAllBytes(path).SequenceEqual(saved)&&!File.Exists(path+".tmp"),"installed publication refusal cannot fall back to ordinary write: "+files[i]);}
   finally{RebirthNpcRecoveryPublications.InstalledInputs.Remove(path);}
   Need(flush[i](),"ordinary writer remains available after synthetic installed refusal: "+files[i]);
  }
 }
 static void VerifyWriteFences(){
  string path=Path.Combine(GameIO.Root,"RebirthNpcProgression.xml");byte[] saved=File.ReadAllBytes(path);
  Action<Action,Action,string> check=(hold,release,label)=>{
   RebirthNpcProgressionPersistenceStore.MarkDirty();hold();
   try{Need(!RebirthNpcProgressionPersistenceStore.TryFlush()&&File.ReadAllBytes(path).SequenceEqual(saved),label);}
   finally{release();}
   Need(RebirthNpcProgressionPersistenceStore.TryFlush(),"ordinary writer resumes after "+label);
  };
  check(()=>FixtureFences.Quarantine=true,()=>FixtureFences.Quarantine=false,"typed quarantine blocks ordinary writer");
  check(()=>RebirthNpcPersistenceTimelineService.IsAlignmentFailed=true,()=>RebirthNpcPersistenceTimelineService.IsAlignmentFailed=false,"unresolved timeline blocks ordinary writer");
  check(()=>RebirthNpcLiveRecoveryGroup.Held.Add(path),()=>RebirthNpcLiveRecoveryGroup.Held.Remove(path),"group recovery fence blocks ordinary writer");
  check(()=>RebirthNpcRetainedWorkCustody.Held.Add(path),()=>RebirthNpcRetainedWorkCustody.Held.Remove(path),"retained work custody blocks ordinary writer");
  string intent=RebirthNpcPersistenceFile.RecoveryIntentPath(path);
  check(()=>File.WriteAllText(intent,"unresolved fixture intent"),()=>File.Delete(intent),"unknown recovery intent blocks ordinary writer");
  string manifest=path+".recovery-publications.xml";
  check(()=>File.WriteAllText(manifest,"unknown fixture publication"),()=>File.Delete(manifest),"unknown installed publication cannot borrow ordinary writer");
  check(()=>ThreadManager.Main=false,()=>ThreadManager.Main=true,"off-thread ordinary writer refused");
 }
 static void Main(string[] args){var root=args[0];Folder(root,"scope-a");LoadAll();Seed();Need(!CanResetAll()&&!ResetAll(),"dirty domain stores refuse all-store reset preflight");Need(FlushAll()&&CanResetAll(),"all four actual stores flush current state");VerifyWriteFences();VerifyInstalledPublicationRefusal();RebirthNpcProgressionProjectionRevisionService.Revisions.Add(new RebirthNpcStableId(9,9),1);Need(!RebirthNpcProgressionPersistenceStore.CanReset,"nonempty projection revisions block store retirement");RebirthNpcProgressionProjectionRevisionService.Reset();
  RebirthNpcSocialService.Relationships[0].Trust=.8f;Need(!RebirthNpcSocialPersistenceStore.CanReset&&!RebirthNpcSocialPersistenceStore.TryReset(false),"social snapshot change cannot hide behind absent dirty flag");Need(FlushAll(),"updated social state flushed");Seed();string a=GameIO.Root;
  string blocked=Path.Combine(a,"RebirthNpcProgression.xml");byte[] originalProgression=File.ReadAllBytes(blocked);File.Delete(blocked);Directory.CreateDirectory(blocked);Need(!FlushAll()&&!ResetAll()&&RebirthNpcProgressionService.Records.Length==1,"one failed store retains backing state and blocks coordinated reset");
  Folder(root,"scope-b");Need(Throws(()=>RebirthNpcProgressionPersistenceStore.EnsureLoaded())&&Throws(()=>RebirthNpcEconomyPersistenceStore.Load()),"replacement world cannot reuse loaded A stores");Need(!FlushAll()&&!File.Exists(Path.Combine(GameIO.Root,"RebirthNpcProgression.xml")),"failed A flush cannot write into B path");Directory.Delete(blocked);Need(!FlushAll()&&RebirthNpcProgressionService.Records.Length==1,"missing original backing head cannot authorize retirement");File.WriteAllBytes(blocked,originalProgression);Need(FlushAll()&&ResetAll(),"restored exact original head permits original-path flush and coordinated reset");LoadAll();Need(RebirthNpcSocialService.Relationships.Length==0&&RebirthNpcFactionGameplayService.Values.Length==0&&RebirthNpcProgressionService.Records.Length==0&&RebirthNpcAdvancedProgressionService.Records.Length==0&&RebirthNpcEconomyLogisticsService.Records.Count==0,"fresh B clears all actual backing adapters");Need(ResetAll(),"empty B reset control");
  Folder(root,"same-path-world");LoadAll();GameManager.Instance.World=new World();Need(Throws(()=>RebirthNpcSocialPersistenceStore.EnsureLoaded())&&Throws(()=>RebirthNpcAdvancedProgressionPersistenceStore.EnsureLoaded()),"same path new World requires explicit rebind");Need(FlushAll()&&ResetAll(),"same path original snapshot can retire explicitly");
  Folder(root,"bad-progression");ProcessRestart();File.WriteAllText(Path.Combine(GameIO.Root,"RebirthNpcProgression.xml"),"<rebirthNpcProgression schema='2'><npc id='00000000000000010000000000000002'><entry id='profession.general' xp='bad'/></npc></rebirthNpcProgression>");Need(Throws(()=>RebirthNpcProgressionPersistenceStore.EnsureLoaded())&&!RebirthNpcProgressionPersistenceStore.TryReset(false),"parseable bad progression numeric data remains quarantined");
  Folder(root,"bad-advanced");ProcessRestart();File.WriteAllText(Path.Combine(GameIO.Root,"RebirthNpcAdvancedProgression.xml"),"<rebirthNpcAdvancedProgression schema='1'><npc id='00000000000000010000000000000002'><spec profession='256' id='bad'/></npc></rebirthNpcAdvancedProgression>");Need(Throws(()=>RebirthNpcAdvancedProgressionPersistenceStore.EnsureLoaded())&&!RebirthNpcAdvancedProgressionPersistenceStore.TryReset(false),"out of byte-range profession cannot alias a valid enum");
  Folder(root,"bad-economy");ProcessRestart();File.WriteAllText(Path.Combine(GameIO.Root,"RebirthNpcEconomyOffline.xml"),"<rebirthNpcEconomyOffline schemaVersion='2'><checkpoint settlement='a' ticks='bad' revision='1'/></rebirthNpcEconomyOffline>");Need(Throws(()=>RebirthNpcEconomyPersistenceStore.Load())&&!RebirthNpcEconomyPersistenceStore.TryReset(false),"invalid economy checkpoint cannot become empty writable store");
  Folder(root,"bad-social");ProcessRestart();File.WriteAllText(Path.Combine(GameIO.Root,"RebirthNpcSocialState.xml"),"<rebirthNpcSocialState version='2'><relationships><relationship key='npc|owner' trust='NaN'/></relationships></rebirthNpcSocialState>");RebirthNpcSocialPersistenceStore.EnsureLoaded();Need(!RebirthNpcSocialPersistenceStore.TryFlush()&&!RebirthNpcSocialPersistenceStore.TryReset(false),"invalid social primary cannot publish or clear backing custody");
  Folder(root,"after-failed-social");Need(Throws(()=>RebirthNpcSocialPersistenceStore.EnsureLoaded())&&!RebirthNpcSocialPersistenceStore.TryReset(false),"failed old-world load cannot be replaced by fresh new-world admission");
  Console.WriteLine("Actual NPC store/helper fixture complete; native world lifecycle/coordinator and domain service mutations are doubled, not gameplay qualification.");
 }
}

#else
// ACTUAL_INSTALLED_PUBLICATION_PROGRAM
#endif
