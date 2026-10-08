using System.Xml.Linq;
using System.Text;
using System.Reflection;
class Program {
 static int count;static void A(bool b,string s){if(!b)throw new Exception(s);count++;}
 static void Main(){
 GameIO.Root=Path.Combine(Path.GetTempPath(),"mixed-persist-original");var paidQueue=new RecipeQueueItem{StartingEntityId=1,Recipe=new(){Marked=true,Name="job"}};
 var station=new TileEntityWorkstation{Queue=new[]{paidQueue},Output=new[]{new ItemStack(new ItemValue(10),3),new ItemStack(new ItemValue(0),0)}};
 var world=new World{Station=station};world.Player=new(){world=world};GameManager.Instance=new(){World=world};RebirthWorldCharacterService.Owner=new();
 var admission=new RebirthStationGridAdmission();RebirthWorldCharacterService.Owner.Progression.StationPreparations.Add("job",admission);RebirthWorldCharacterService.Owner.Progression.StationPublications.Add("job",new());RebirthStationObservationDispatcher.Verified=true;
 A(RebirthStationCompletionCapture.TryObserveBefore(station,1,new ItemValue(10),"job","",7,2,out var before),"original before");station.Output[0].count=5;
 A(RebirthStationCompletionCapture.TryAppend(station,1,new ItemValue(10),"job","",7,2,before,out var paid)&&OrdinaryNativeCallsiteCandidate.Bind(paid),"original paid event");
 MixedExpectationCandidate.Anchor anchor=null; A(NativePaidOrdinaryRouteLink.TryStageBeforeOrdinary(paid,out var route)&&MixedExpectationCandidate.Anchor.TryCapture(paid,route,out anchor),"original beforeordinary anchor");
 A(NativePaidOrdinaryExitCandidate.AfterPaidRetained(paid,paidQueue,1,new ItemValue(10),"job","",7,2),"original exit exporter");
 station.Queue=new[]{new RecipeQueueItem{StartingEntityId=1,Recipe=new(){Name="ordinary"}}};station.ActualObservedWhile(1);NativePaidOrdinaryExitCandidate.OnOriginalExit(station,null);
 A(NativePaidOrdinaryExitCandidate.TryGetOriginal(paid,out var held)&&held.Witness!=null,"original successful queue exit");
 A(MixedExitExpectationCandidate.TryCreate(anchor,paid,held.Route,held.Witness,out var expectation),"typed exit expectation");
 var input=string.Join("|",station.Input.Select(x=>x.count+":"+RebirthNativeItemCodec.Encode(x.itemValue)));
 var queue=(string)typeof(RebirthStationNativeQueueSnapshot).GetMethod("Encode",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{station.Queue});
 var spans=new[]{Encoding.UTF8.GetBytes(input),Encoding.UTF8.GetBytes(queue),new byte[]{2},new byte[]{1}};
 RebirthStationTerminalContents.Decoded=station.Output.Select(s=>s.Clone()).ToArray();RebirthNativeItemConformanceReader.Decoded=station.CraftCompleteList.ToArray();
 XElement O(string name)=>new(name,new XAttribute("job","job"),new XAttribute("creation","creation"));
 A(MixedLiveRecordCandidate.TryCapture(expectation,admission.Write(),O("stationTerminalIntent"),O("stationPublication"),O("stationCompletionPublication"),spans,out var live),"private typed record capture");
 A(live.MatchesRetainedOriginalAndNative(live.Data,spans),"exact original typed semantics");A(!live.MatchesRetainedOriginalAndNative(live.Data.Clone(),spans),"cold identical data cannot rebind original");
 var changed=spans.Select(b=>(byte[])b.Clone()).ToArray();changed[1][0]^=1;A(!live.MatchesRetainedOriginalAndNative(live.Data,changed),"changed queue span refuses");
 station.Input[0].count++;A(!live.MatchesRetainedOriginalAndNative(live.Data,spans),"original live input changed refuses");station.Input[0].count--;
 A(live.MatchesRetainedOriginalAndNative(live.Data,spans),"same original retry returns");
  var identity=new RebirthStablePlayerIdentity();var owner=RebirthWorldCharacterService.Owner;owner.Progression.Mixed.Add(live.Data.Job,live.Data);
 string temp=Path.Combine(Path.GetTempPath(),"mixed-final-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);string path=Path.Combine(temp,"character.xml");MixedFinalFileRepository.PathNow=path;
 var progression=new XElement("progression");A(MixedCompletionRecord.TryAppendData(progression,new[]{live.Data},out progression),"final data serialization");
 var doc=new XDocument(new XElement("rebirthWorldCharacter",new XAttribute("schemaVersion",1),new XAttribute("revision",1),new XAttribute("createdAtUtc",DateTime.UtcNow.ToString("o")),new XAttribute("modifiedAtUtc",DateTime.UtcNow.ToString("o")),new XAttribute("stablePlayerKey",identity.StorageKey),new XAttribute("stablePlayerId",identity.CanonicalId),new XElement("origin",new XAttribute("creation",owner.Origin.CreationId),new XAttribute("complete",true)),progression,new XElement("condition"),new XElement("support")));
 void Save()=>doc.Save(path);bool Witness()=>MixedFinalFileRepository.HasSavedOriginalData(identity,owner,live,spans);
 Save();A(Witness(),"actual final loader positive DATA plus retained typed original");
 foreach(var key in new[]{"stablePlayerKey","stablePlayerId"}){var badDoc=new XDocument(doc);badDoc.Root.SetAttributeValue(key,"foreign");badDoc.Save(path);A(!Witness(),"actual loader identity refuses "+key);Save();}
 var altered=new XDocument(doc);altered.Root.Element("origin").SetAttributeValue("creation","foreign");altered.Save(path);A(!Witness(),"original creation refuses");Save();
 altered=new XDocument(doc);altered.Root.Element("origin").SetAttributeValue("complete",false);altered.Save(path);A(!Witness(),"incomplete origin refuses");Save();
 File.Copy(path,path+".bak",true);File.WriteAllText(path,"<broken>");A(!Witness(),"actual corrupt final valid backup cannot witness");Save();
 RebirthWorldCharacterMigrationRegistry.Migrated=true;A(!Witness(),"migrated final refuses");RebirthWorldCharacterMigrationRegistry.Migrated=false;
 RebirthWorldCharacterMigrationRegistry.Fail=true;A(!Witness(),"migration failure refuses");RebirthWorldCharacterMigrationRegistry.Fail=false;
 MixedFinalFileRepository.FailSupport=true;A(!Witness(),"actual outer later support failure atomic");MixedFinalFileRepository.FailSupport=false;
 MixedFinalFileRepository.Server=false;A(!Witness(),"server authority absent");MixedFinalFileRepository.Server=true;
 MixedFinalFileRepository.AfterLoad=()=>MixedFinalFileRepository.PathNow=path+".other";A(!Witness(),"current path replacement during actual disk read");MixedFinalFileRepository.AfterLoad=null;MixedFinalFileRepository.PathNow=path;
 MixedFinalFileRepository.AfterLoad=()=>MixedFinalFileRepository.Server=false;A(!Witness(),"authority loss during read");MixedFinalFileRepository.AfterLoad=null;MixedFinalFileRepository.Server=true;
 MixedFinalFileRepository.AfterLoad=()=>owner.Progression.Mixed[live.Data.Job]=live.Data.Clone();A(!Witness(),"same data cache original reference replaced during read");MixedFinalFileRepository.AfterLoad=null;owner.Progression.Mixed[live.Data.Job]=live.Data;
 var originalWorldRoot=GameIO.Root;MixedFinalFileRepository.AfterLoad=()=>GameIO.Root=originalWorldRoot+".other";A(!Witness(),"native world save root replacement during read");MixedFinalFileRepository.AfterLoad=null;GameIO.Root=originalWorldRoot;
 var originalWorld=GameManager.Instance.World;MixedFinalFileRepository.AfterLoad=()=>GameManager.Instance.World=new World();A(!Witness(),"native world replacement during read");MixedFinalFileRepository.AfterLoad=null;GameManager.Instance.World=originalWorld;
 altered=new XDocument(doc);altered.Root.Element("progression").Element("stationMixedCompletionRecords").Add(live.Data.Write());altered.Save(path);A(!Witness(),"malformed duplicate data whole outer refusal");Save();
 A(Witness(),"uncertain write exact original retry after final restored");
 owner.Progression.Mixed[live.Data.Job]=live.Data.Clone();A(!Witness(),"cold reconstructed equal data never original live record");owner.Progression.Mixed[live.Data.Job]=live.Data;
 File.Delete(path);A(!Witness(),"absent final valid backup refuses");File.Delete(path+".bak");Directory.Delete(temp);
Console.WriteLine("PASS "+count+" typed original live record; synthetic span parser adapters explicit");
 }
}
