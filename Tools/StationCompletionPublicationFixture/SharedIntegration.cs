using System;using System.IO;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
public class RebirthStablePlayerIdentity{public string StorageKey="owner",CanonicalId="player";}
public class RebirthWorldOriginSnapshot{public string CreationId;public bool Complete=true;}
public class RebirthWorldConditionState{}public class RebirthWorldSupportState{}
public class ArchiveStub{public string CreationId;}
public partial class RebirthWorldProgressionState{
 public Dictionary<string,RebirthStationGridAdmission> StationPreparations=new Dictionary<string,RebirthStationGridAdmission>(StringComparer.Ordinal);
 public Dictionary<string,RebirthStationTerminalIntent> StationTerminalIntents=new Dictionary<string,RebirthStationTerminalIntent>(StringComparer.Ordinal);
 public Dictionary<string,RebirthStationPublicationRecord> StationPublications=new Dictionary<string,RebirthStationPublicationRecord>(StringComparer.Ordinal);
 public Dictionary<string,object> RecipeDiscoveries=new Dictionary<string,object>(),StationDiscoveryAdmissions=new Dictionary<string,object>();
 public Dictionary<string,ArchiveStub> StationRefundArchives=new Dictionary<string,ArchiveStub>();public object PendingTheoryStudy,SoloTheory;
}
public class RebirthWorldCharacterRecord{
 public const int CurrentSchemaVersion=1;public int SchemaVersion;public string StablePlayerId,StablePlayerKey;public RebirthWorldOriginSnapshot Origin;public RebirthWorldProgressionState Progression;
 public bool IsComplete=>Origin!=null&&Origin.Complete;
 public RebirthWorldCharacterRecord(int schema,string id,string key,long revision,DateTime created,DateTime modified,RebirthWorldOriginSnapshot origin,RebirthWorldProgressionState p,RebirthWorldConditionState c,RebirthWorldSupportState s,int source,int target,string policy,bool applied,string oa,string pa){SchemaVersion=schema;StablePlayerId=id;StablePlayerKey=key;Origin=origin;Progression=p;}
}
public static class RebirthAtomicXmlFile{public static bool TryLoad(string path,out XDocument doc,out string error){doc=null;error="";try{doc=XDocument.Load(path);ExtractedRepository.AfterLoad?.Invoke();return true;}catch(Exception e){error=e.Message;return false;}}}
public static class RebirthWorldCharacterMigrationRegistry{public static bool Fail,Migrated;public static bool TryMigrateToCurrent(XDocument doc,out bool migrated,out string error){migrated=Migrated;error="";return !Fail;}}
public static class RebirthStationDiscoveryAdmissionPersistence{public static bool TryRead(XElement n,string key,string creation,out Dictionary<string,object> records,out string error){records=new Dictionary<string,object>();error="";return true;}}
public static class RebirthStationRecipeDiscoveryPersistence{public static bool MatchesCreation(IEnumerable<object> items,string c)=>true;}
public static class RebirthTheoryStudyPersistence{public static bool MatchesOwner(object x,string c)=>true;}public static class RebirthTheorySoloPersistence{public static bool MatchesOwner(object x,string c)=>true;}
public static class RebirthStationPreparationPersistence{public static bool MatchesOwner(Dictionary<string,RebirthStationGridAdmission> items,string c)=>items.Values.All(a=>a.CreationId==c);}
public static partial class ExtractedRepository{
 private static bool serverAuthority=true;private static string CurrentPath;private static readonly object Gate=new object();
 internal static Dictionary<string,RebirthStationGridAdmission> Preparations;internal static Action AfterLoad;internal static bool FailCondition,FailSupport,ThrowSupport;
 private static string GetPath(string key)=>CurrentPath;private static object GetWriteLock(string key)=>Gate;
 private static bool HasPendingItemCustody(XDocument d)=>false;
 private static string A(XElement n,string key)=>(string)n.Attribute(key)??"";
 private static bool TryDeserializeOrigin(XElement n,out RebirthWorldOriginSnapshot origin,out string error){error="";origin=n==null?null:new RebirthWorldOriginSnapshot{CreationId=(string)n.Attribute("creation"),Complete=(bool?)n.Attribute("complete")??true};return origin!=null;}
 private static bool TryDeserializeCondition(XElement n,out RebirthWorldConditionState condition,out string error){condition=new RebirthWorldConditionState();error="";return !FailCondition;}
 private static bool TryDeserializeSupport(XElement n,string key,string creation,out RebirthWorldSupportState support,out string error){support=new RebirthWorldSupportState();error="";if(ThrowSupport)throw new IOException("support");return !FailSupport;}
 private static int count;
 private static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL shared "+name);count++;Console.WriteLine("PASS shared "+name);}
 internal static void Run(string root,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q,RebirthStationCompletionPublication c){
 var identity=new RebirthStablePlayerIdentity();CurrentPath=Path.Combine(root,"character.xml");Preparations=new Dictionary<string,RebirthStationGridAdmission>{{a.JobId,a}};
 var state=new RebirthWorldProgressionState();state.StationPreparations.Add(a.JobId,a);state.StationTerminalIntents.Add(a.JobId,i);state.StationPublications.Add(a.JobId,q);state.StationCompletionPublications.Add(a.JobId,c);
 var clone=state.Clone();Check(clone.StationCompletionPublications.Comparer.Equals(StringComparer.Ordinal),"actual model Ordinal dictionary");Check(!ReferenceEquals(clone.StationCompletionPublications[a.JobId],c)&&XNode.DeepEquals(clone.StationCompletionPublications[a.JobId].Write(),c.Write()),"actual clone loop deep detached");
 clone.StationCompletionPublications.Clear();Check(state.StationCompletionPublications.Count==1,"clone dictionary independent");
 var progress=SaveCompletion(state);Check(progress.Elements("stationCompletionPublications").Count()==1,"actual write hook one section");
 var doc=new XDocument(new XElement("rebirthWorldCharacter",new XAttribute("schemaVersion",1),new XAttribute("revision",1),new XAttribute("createdAtUtc",DateTime.UtcNow.ToString("o")),new XAttribute("modifiedAtUtc",DateTime.UtcNow.ToString("o")),new XAttribute("stablePlayerId","player"),new XAttribute("stablePlayerKey","owner"),new XElement("origin",new XAttribute("creation",a.CreationId)),progress));
 Action save=()=>doc.Save(CurrentPath);save();Check(HasSavedStationCompletionPublication(identity,a,i,q,c),"actual final file witness");
 Check(TryLoadValidatedRecord(CurrentPath,identity,out var loaded,out _,out _,out _)&&loaded.Progression.StationCompletionPublications.Count==1,"actual loader outer read hooks");
 var empty=new XDocument(doc);empty.Root.Element("progression").Element("stationCompletionPublications").Remove();empty.Save(CurrentPath);Check(TryLoadValidatedRecord(CurrentPath,identity,out loaded,out _,out _,out _)&&loaded.Progression.StationCompletionPublications.Count==0,"old absence accepted");Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"absent publication cannot witness");save();
 var changed=new XDocument(doc);changed.Root.Element("progression").Element("stationCompletionPublications").Elements().First().SetAttributeValue("job","foreign");changed.Save(CurrentPath);
 Check(!TryLoadValidatedRecord(CurrentPath,identity,out loaded,out _,out _,out _)&&loaded==null,"malformed completion no outer partial record");Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"bad completed binding refuses witness");save();
 foreach(var key in new[]{"intent","queued","admission","definition","creation"}){changed=new XDocument(doc);changed.Root.Element("progression").Element("stationCompletionPublications").Elements().First().SetAttributeValue(key,"bad");changed.Save(CurrentPath);Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"bad original "+key);save();}
 changed=new XDocument(doc);changed.Root.Element("progression").Add(new XElement(changed.Root.Element("progression").Element("stationCompletionPublications")));changed.Save(CurrentPath);Check(!TryLoadValidatedRecord(CurrentPath,identity,out loaded,out _,out _,out _)&&loaded==null,"duplicate section no partial record");save();
 FailCondition=true;Check(!TryLoadValidatedRecord(CurrentPath,identity,out loaded,out _,out _,out _)&&loaded==null,"later condition failure no partial publication");FailCondition=false;
 FailSupport=true;Check(!TryLoadValidatedRecord(CurrentPath,identity,out loaded,out _,out _,out _)&&loaded==null,"later support failure no partial publication");FailSupport=false;
 ThrowSupport=true;Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"later support exception refused");ThrowSupport=false;
 serverAuthority=false;Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"no server authority");serverAuthority=true;
 string originalPath=CurrentPath;AfterLoad=()=>CurrentPath=originalPath+".other";Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"path replaced during read refuses");AfterLoad=null;CurrentPath=originalPath;
 AfterLoad=()=>serverAuthority=false;Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"authority lost during read refuses");AfterLoad=null;serverAuthority=true;
 RebirthWorldCharacterMigrationRegistry.Migrated=true;Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"migration cannot exact final witness");RebirthWorldCharacterMigrationRegistry.Migrated=false;
 RebirthWorldCharacterMigrationRegistry.Fail=true;Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"migration failure refuses");RebirthWorldCharacterMigrationRegistry.Fail=false;
 changed=new XDocument(doc);changed.Root.SetAttributeValue("stablePlayerKey","foreign");changed.Save(CurrentPath);Check(!TryLoadValidatedRecord(CurrentPath,identity,out loaded,out _,out _,out _)&&loaded==null,"loader stable key mismatch no partial record");save();
 changed=new XDocument(doc);changed.Root.SetAttributeValue("stablePlayerId","foreign");changed.Save(CurrentPath);Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"stable canonical id mismatch");save();
 changed=new XDocument(doc);changed.Root.Element("origin").SetAttributeValue("creation","foreign");changed.Save(CurrentPath);Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"original creation mismatch");save();
 changed=new XDocument(doc);changed.Root.Element("origin").SetAttributeValue("complete",false);changed.Save(CurrentPath);Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"incomplete committed origin");save();
 File.Copy(CurrentPath,CurrentPath+".bak",true);File.WriteAllText(CurrentPath,"<broken>");Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"corrupt final valid backup cannot witness");save();
 CurrentPath="";Check(!HasSavedStationCompletionPublication(identity,a,i,q,c),"missing current path");CurrentPath=originalPath;
 Check(!HasSavedStationCompletionPublication(null,a,i,q,c),"null identity");Check(HasSavedStationCompletionPublication(identity,a,i,q,c),"exact final file restored retries same original");
 File.Delete(CurrentPath);File.Delete(CurrentPath+".bak");Console.WriteLine(count+" PASS exact extracted model clone/save/read/outer loader/final witness; unrelated parser/native authority adapters explicit");
 }
}
