using System.Xml.Linq;
using System.Globalization;
static class SdFile{public static Stream Open(string p,FileMode m,FileAccess a,FileShare s)=>File.Open(p,m,a,s);}
partial class Origin{public bool Complete=true;}
class RebirthWorldOriginSnapshot:Origin{}class RebirthWorldConditionState{}class RebirthWorldSupportState{}class ArchiveStub{public string CreationId;}
partial class RebirthWorldProgressionState {
 internal Dictionary<string,object> StationCompletionExpectationProjections=new(); internal readonly Dictionary<string,MixedCompletionRecord> StationMixedCompletionRecords=new(StringComparer.Ordinal);
 public Dictionary<string,object> RecipeDiscoveries=new(),StationDiscoveryAdmissions=new();public Dictionary<string,ArchiveStub> StationRefundArchives=new();public object PendingTheoryStudy,SoloTheory;
 internal RebirthWorldProgressionState Clone(){var p=new RebirthWorldProgressionState();foreach(var x in StationPreparations)p.StationPreparations.Add(x.Key,x.Value.Clone());foreach(var x in StationPublications)p.StationPublications.Add(x.Key,x.Value.Clone());foreach(var x in StationTerminalIntents)p.StationTerminalIntents.Add(x.Key,x.Value.Clone());foreach(var x in StationMixedCompletionRecords)p.StationMixedCompletionRecords.Add(x.Key,x.Value.Clone());return p;}
}
partial class RebirthWorldCharacterRecord {
 public bool Dirty;public const int CurrentSchemaVersion=1;public int SchemaVersion=1;public bool IsComplete=>Origin!=null&&Origin.Complete;
 public RebirthWorldCharacterRecord(){}
 public RebirthWorldCharacterRecord(int schema,string id,string key,long revision,DateTime created,DateTime modified,RebirthWorldOriginSnapshot o,RebirthWorldProgressionState p,RebirthWorldConditionState c,RebirthWorldSupportState s,int source,int target,string policy,bool applied,string oa,string pa){SchemaVersion=schema;StablePlayerId=id;StablePlayerKey=key;Origin=o;Progression=p;}
}
static class RebirthAtomicXmlFile{public static bool TryLoad(string path,out XDocument d,out string error){d=null;error="";try{d=XDocument.Load(path);RebirthWorldCharacterRepository.AfterLoad?.Invoke();return true;}catch(Exception e){error=e.Message;return false;}}}
static class RebirthWorldCharacterMigrationRegistry {public static bool Migrated;public static bool TryMigrateToCurrent(XDocument d,out bool m,out string e){m=Migrated;e="";return true;}}
static class RebirthStationDiscoveryAdmissionPersistence{public static bool TryRead(XElement n,string key,string creation,out Dictionary<string,object> r,out string e){r=new();e="";return true;}}
static class RebirthStationRecipeDiscoveryPersistence{public static bool MatchesCreation(IEnumerable<object> r,string c)=>true;}
static class RebirthTheoryStudyPersistence{public static bool MatchesOwner(object r,string c)=>true;}static class RebirthTheorySoloPersistence{public static bool MatchesOwner(object r,string c)=>true;}
static class RebirthStationPreparationPersistence{public static bool MatchesOwner(Dictionary<string,RebirthStationGridAdmission> r,string c)=>r.Values.All(a=>a.CreationId==c);}
static class RebirthStationSerializedContents{public static bool Matches(byte[] i,byte[] q,RebirthStationGridAdmission a,string c,int x,int y,int z,string b,int actor)=>true;}
static partial class RebirthWorldCharacterRepository {
 static bool serverAuthority=true;static object gate=new();public static string RootDirectory;public static Action AfterLoad;public static bool ThrowAfterWrite,FailSupport;public static int Saves;
 static string GetPath(string key)=>string.IsNullOrEmpty(RootDirectory)?"":Path.Combine(RootDirectory,key+".xml");static object GetWriteLock(string key)=>gate;
 public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord o)=>ReferenceEquals(o,RebirthWorldCharacterService.Owner);
 public static bool HasSavedStationPublication(RebirthStablePlayerIdentity id,RebirthStationGridAdmission a,RebirthStationPublicationRecord q)=>q.Revalidate(GameIO.Root,a);
 public static bool SaveIfDirty(RebirthStablePlayerIdentity id,string why){var o=RebirthWorldCharacterService.Owner;if(!o.Dirty)return false;Saves++;SerializeDocument(o).Save(GetPath(id.StorageKey));if(ThrowAfterWrite){ThrowAfterWrite=false;throw new IOException("uncertain after final write");}o.Dirty=false;return true;}
 static XDocument SerializeDocument(RebirthWorldCharacterRecord o)=>new(new XElement("rebirthWorldCharacter",new XAttribute("schemaVersion",1),new XAttribute("revision",1),new XAttribute("createdAtUtc",DateTime.UtcNow.ToString("o")),new XAttribute("modifiedAtUtc",DateTime.UtcNow.ToString("o")),new XAttribute("stablePlayerKey",o.StablePlayerKey),new XAttribute("stablePlayerId",o.StablePlayerId),new XElement("origin",new XAttribute("creation",o.Origin.CreationId),new XAttribute("complete",true)),SerializeProgression(o.Progression,o.StablePlayerKey),new XElement("condition"),new XElement("support")));
 static XElement SerializeProgression(RebirthWorldProgressionState p,string key){var n=new XElement("progression",new XElement("preparations",p.StationPreparations.Values.Select(a=>a.Write())),RebirthStationPublicationRecord.WriteAll(p.StationPublications,p.StationPreparations),RebirthStationTerminalIntent.WriteAll(p.StationTerminalIntents,p.StationPreparations));return AppendMixedActual(n,p);}
 static bool HasPendingItemCustody(XDocument d)=>false;static string A(XElement n,string key)=>(string)n?.Attribute(key)??"";
 static bool TryDeserializeOrigin(XElement n,out RebirthWorldOriginSnapshot o,out string e){e="";o=n==null?null:new(){CreationId=A(n,"creation"),Complete=(bool?)n.Attribute("complete")??true};return o!=null;}
 static bool TryDeserializeProgression(XElement n,string key,out RebirthWorldProgressionState p,out string e){p=null;e="";try{var state=new RebirthWorldProgressionState();foreach(var row in n.Element("preparations").Elements()){var a=new RebirthStationGridAdmission{JobId=A(row,"job"),CreationId=A(row,"creation"),DefinitionId=A(row,"definition")};if(!XNode.DeepEquals(a.Write(),row))return false;state.StationPreparations.Add(a.JobId,a);}if(!RebirthStationPublicationRecord.ReadAll(n,state.StationPreparations,out var qs)||!RebirthStationTerminalIntent.ReadAll(n,state.StationPreparations,out var intents)||!MixedCompletionRecord.TryReadAllData(n,out var mixed))return false;foreach(var x in qs)state.StationPublications.Add(x.Key,x.Value);foreach(var x in intents)state.StationTerminalIntents.Add(x.Key,x.Value);if(!ReadMixedActual(n,key,state,out e))return false;p=state;return true;}catch{return false;}}
 static bool TryDeserializeCondition(XElement n,out RebirthWorldConditionState c,out string e){c=new();e="";return true;}static bool TryDeserializeSupport(XElement n,string key,string creation,out RebirthWorldSupportState s,out string e){s=new();e="";return !FailSupport;}
}
