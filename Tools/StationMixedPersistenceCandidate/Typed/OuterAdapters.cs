using System.Xml.Linq;
using System.Globalization;
partial class Origin {public bool Complete=true;}
class RebirthWorldOriginSnapshot:Origin{}
class RebirthWorldConditionState{}class RebirthWorldSupportState{}class ArchiveStub{public string CreationId;}
partial class RebirthWorldProgressionState {
 internal Dictionary<string,MixedCompletionRecord> Mixed=new();
 public Dictionary<string,object> RecipeDiscoveries=new(),StationDiscoveryAdmissions=new();
 public Dictionary<string,ArchiveStub> StationRefundArchives=new();public object PendingTheoryStudy,SoloTheory;
}
partial class RebirthWorldCharacterRecord {
 public const int CurrentSchemaVersion=1;public int SchemaVersion=1;public bool IsComplete=>Origin!=null&&Origin.Complete;
 public RebirthWorldCharacterRecord(){}
 public RebirthWorldCharacterRecord(int schema,string id,string key,long revision,DateTime created,DateTime modified,RebirthWorldOriginSnapshot origin,RebirthWorldProgressionState p,RebirthWorldConditionState c,RebirthWorldSupportState s,int source,int target,string policy,bool applied,string oa,string pa){SchemaVersion=schema;StablePlayerId=id;StablePlayerKey=key;Origin=origin;Progression=p;}
}
static class RebirthAtomicXmlFile {public static bool TryLoad(string path,out XDocument d,out string error){d=null;error="";try{d=XDocument.Load(path);MixedFinalFileRepository.AfterLoad?.Invoke();return true;}catch(Exception e){error=e.Message;return false;}}}
static class RebirthWorldCharacterMigrationRegistry {public static bool Migrated,Fail;public static bool TryMigrateToCurrent(XDocument d,out bool migrated,out string error){migrated=Migrated;error="";return !Fail;}}
static class RebirthStationDiscoveryAdmissionPersistence {public static bool TryRead(XElement n,string key,string creation,out Dictionary<string,object> result,out string error){result=new();error="";return true;}}
static class RebirthStationRecipeDiscoveryPersistence {public static bool MatchesCreation(IEnumerable<object> r,string c)=>true;}
static class RebirthTheoryStudyPersistence {public static bool MatchesOwner(object o,string c)=>true;}
static class RebirthTheorySoloPersistence {public static bool MatchesOwner(object o,string c)=>true;}
static class RebirthStationPreparationPersistence {public static bool MatchesOwner(Dictionary<string,RebirthStationGridAdmission> d,string c)=>d.Values.All(a=>a.CreationId==c);}
internal static partial class MixedFinalFileRepository {
 internal static bool Server=true,FailSupport;internal static string PathNow;internal static Action AfterLoad;static object gate=new();
 static bool HasPendingItemCustody(XDocument d)=>false;
 static string A(XElement n,string key)=>(string)n?.Attribute(key)??"";
 static bool TryDeserializeOrigin(XElement n,out RebirthWorldOriginSnapshot o,out string error){error="";o=n==null?null:new(){CreationId=A(n,"creation"),Complete=(bool?)n.Attribute("complete")??true};return o!=null;}
 static bool TryDeserializeProgression(XElement n,string key,out RebirthWorldProgressionState state,out string error){state=null;error="";if(!MixedCompletionRecord.TryReadAllData(n,out var rows))return false;var p=new RebirthWorldProgressionState();foreach(var pair in rows)p.Mixed.Add(pair.Key,pair.Value);state=p;return true;}
 static bool TryDeserializeCondition(XElement n,out RebirthWorldConditionState c,out string e){c=new();e="";return true;}
 static bool TryDeserializeSupport(XElement n,string key,string creation,out RebirthWorldSupportState s,out string e){s=new();e="";return !FailSupport;}
 // Exact DATA witness with retained original identity and actual final loader; no backup fallback.
 internal static bool HasSavedOriginalData(RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord owner,MixedLiveRecordCandidate original,byte[][] nativeSpans){
  if(!Server||identity==null||owner==null||original==null||!ReferenceEquals(owner,RebirthWorldCharacterService.Owner)||!owner.Progression.Mixed.TryGetValue(original.Data.Job,out var held)||!ReferenceEquals(held,original.Data)||!original.MatchesRetainedOriginalAndNative(held,nativeSpans))return false;
  string path=PathNow;if(string.IsNullOrEmpty(path))return false;
  lock(gate){try{
   if(!Server||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||saved?.Progression==null||saved.Origin.CreationId!=owner.Origin.CreationId||!saved.Progression.Mixed.TryGetValue(held.Job,out var data)||!XNode.DeepEquals(data.Write(),held.Write()))return false;
   return Server&&path==PathNow&&ReferenceEquals(owner,RebirthWorldCharacterService.Owner)&&owner.Progression.Mixed.TryGetValue(held.Job,out var current)&&ReferenceEquals(current,held)&&original.MatchesRetainedOriginalAndNative(held,nativeSpans);
  }catch{return false;}}
 }
}
