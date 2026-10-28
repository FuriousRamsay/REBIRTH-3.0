// AUTHORED_NOT_RUN. Complete actual outbox; simple file/native/original adapters.
// ACTUAL_OUTBOX
// BOUNDED_NATIVE_STATION_ADAPTER_BEGIN
// This older outbox cohort supplies no persistent native station identity and
// cannot qualify cold material mutation. The dedicated cohort injects the real partial.
internal static partial class RebirthWorkstationCompletionOutbox {
 private static bool BindActualNativeStation(State state,World world,TileEntityWorkstation station,bool publish=true)=>world==null&&station==null||ReferenceEquals(state.NativeWorld,world)&&ReferenceEquals(state.NativeStation,station);
 private static bool BindAcceptedStationDescription(State state,string birth)=>birth==null;
 internal static bool TryRestoreUnattemptedNativeStation(State state)=>false;
}
// BOUNDED_NATIVE_STATION_ADAPTER_END
// NATIVE_SCRAP_DISPOSITION_ADAPTER_BEGIN
// This ordinary settlement cohort has no native additional-scrap source.
// The separate MP028 cohort composes the complete actual disposition partial.
internal static partial class RebirthWorkstationCompletionOutbox {
 private static bool ValidNativeScrapNoAwards(State state)=>false;
 internal static bool CompleteNativeScrapNoAwards(State state)=>false;
}
// NATIVE_SCRAP_DISPOSITION_ADAPTER_END
internal sealed partial class World {internal string Guid="original-world";internal bool Remote;internal bool IsRemote()=>Remote;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager{World=new World()};internal World World;}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal static class GameIO {internal static string GetSaveGameDir()=>System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"outbox-native-save");}
internal enum EnumGamePrefs {GameGuidClient}
internal static class GamePrefs {internal static string GetString(EnumGamePrefs preference)=>"original-world";}
internal sealed partial class TileEntityWorkstation {}
internal static partial class RebirthWorkstationReagentOriginal {internal sealed class NativeObserved {}}
internal sealed partial class RebirthWorkstationCompletionOriginal {
 internal string Operation=Guid.NewGuid().ToString("N"),WorldGuid="original-world",Digest="original-digest";
 internal string DeliveryBirth;
 internal bool NativeAdditionalScrap;
 internal XElement Write()=>new XElement("nativeOriginal",new XAttribute("operation",Operation),new XAttribute("world",WorldGuid),new XAttribute("digest",Digest),NativeAdditionalScrap?new XAttribute("nativeAdditionalScrap",true):null,DeliveryBirth==null?null:new XElement("delivery",new XElement("accepted",new XAttribute("stationBirth",DeliveryBirth))));
 internal void ConfirmSaved(object authority){}
 internal static bool TryRead(XElement node,out RebirthWorkstationCompletionOriginal original){original=new RebirthWorkstationCompletionOriginal{Operation=(string)node.Attribute("operation"),WorldGuid=(string)node.Attribute("world"),Digest=(string)node.Attribute("digest"),NativeAdditionalScrap=(bool?)node.Attribute("nativeAdditionalScrap")==true,DeliveryBirth=(string)node.Element("delivery")?.Elements().SingleOrDefault()?.Attribute("stationBirth")};return true;}
}
internal static class RebirthWorkstationCompletionPublication {
 // This adapter deliberately supplies file persistence only. Production writer
 // candidate ownership/fsync/reparse handling is covered by its separate fixture.
 internal static void RequireSafeDirectory(string path){}
 internal static void RequireClearDestination(string path){}
 internal static int Writes,FailAt;
 internal static bool Write(string path,XElement image){Writes++;if(Writes==FailAt)return false;Directory.CreateDirectory(Path.GetDirectoryName(path));image.Save(path);return true;}
 internal static Action<string> BeforeRead;
 internal static XElement ReadPrimary(string path){BeforeRead?.Invoke(path);return XElement.Load(path);}
}
internal static class Program {
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static RebirthWorkstationCompletionOutbox.State Complete(){
  var state=RebirthWorkstationCompletionOutbox.Retain(new RebirthWorkstationCompletionOriginal(),"server");
  state.Skill=state.SkillAttempted=state.Statistics=state.StatisticsAttempted=state.Decision=state.Terminal=true;
  Need(RebirthWorkstationCompletionOutbox.Save(state),"fixture save failed");return state;
 }
 public static void Main(){
  var state=Complete();var original=state.Original;var exact=RebirthWorkstationCompletionPublication.ReadPrimary(state.Path);
  Need(RebirthWorkstationCompletionOutbox.TryReadSaved(original,out var saved)&&ReferenceEquals(saved,state),"positive exact saved state lost native custody object");
  Need(RebirthWorkstationCompletionOutbox.HasStage(original,"skill")&&RebirthWorkstationCompletionOutbox.HasStage(original,"statistics")&&RebirthWorkstationCompletionOutbox.HasTerminal(original.Operation,original.Digest),"positive stage unavailable");
  var changed=new XElement(exact);changed.SetAttributeValue("terminal",false);changed.Save(state.Path);
  Need(!RebirthWorkstationCompletionOutbox.TryReadSaved(original,out _),"same digest masked changed disk stages");
  Need(RebirthWorkstationCompletionOutbox.Retain(original,"server")==null,"repeat acceptance reused stale cached Saved flag");
  Need(!RebirthWorkstationCompletionOutbox.HasStage(original,"statistics"),"cached statistics bypassed changed saved image");
  Need(!RebirthWorkstationCompletionOutbox.HasTerminal(original.Operation,original.Digest),"cached terminal bypassed changed saved image");
  Need(!RebirthWorkstationCompletionOutbox.Archive(state)&&File.Exists(state.Path),"changed saved image retired original");
  exact.Save(state.Path);Need(RebirthWorkstationCompletionOutbox.TryReadSaved(original,out _),"exact original readback could not resume");
  ThreadManager.Main=false;Need(!RebirthWorkstationCompletionOutbox.HasTerminal(original.Operation,original.Digest)&&!RebirthWorkstationCompletionOutbox.Archive(state),"worker retired native custody");ThreadManager.Main=true;
  GameManager.Instance.World=new World{Guid="different-world"};Need(!RebirthWorkstationCompletionOutbox.Archive(state),"wrong World archived original");GameManager.Instance.World=new World();
  Need(RebirthWorkstationCompletionOutbox.Archive(state)&&File.Exists(state.Path),"exact saved terminal not archived");Need(RebirthWorkstationCompletionOutbox.HasTerminal(original.Operation,original.Digest),"archived terminal lost");
  state=Complete();state.Statistics=false;Need(!RebirthWorkstationCompletionOutbox.TryReadSaved(state.Original,out _),"unsaved mutable stage certified from matching original digest");
  state=Complete();state.NativeWorld=GameManager.Instance.World;state.NativeStation=new TileEntityWorkstation();
  string priorPath=state.Path,archive=Path.Combine(state.Root,"archive",state.Original.Operation+".xml");
  RebirthWorkstationCompletionPublication.BeforeRead=path=>{if(path==archive)throw new IOException("archive final readback interrupted");};
  bool interrupted=false;try{RebirthWorkstationCompletionOutbox.Archive(state);}catch(IOException){interrupted=true;}
  Need(interrupted&&!File.Exists(priorPath)&&File.Exists(archive)&&state.Path==priorPath&&state.NativeStation!=null,"readback failure did not retain exact rename outcome and native handles");
  RebirthWorkstationCompletionPublication.BeforeRead=null;
  var archiveExact=RebirthWorkstationCompletionPublication.ReadPrimary(archive);var conflict=new XElement(archiveExact);conflict.SetAttributeValue("terminal",false);conflict.Save(archive);
  Need(!RebirthWorkstationCompletionOutbox.Archive(state)&&state.NativeStation!=null,"same digest conflicting archive retired custody");archiveExact.Save(archive);
  File.WriteAllText(priorPath+".tmp","unknown original candidate");Need(!RebirthWorkstationCompletionOutbox.Archive(state)&&state.NativeStation!=null,"staged original ignored during rename recovery");
  // Remove only this fixture-created sentinel; production never removes unknown candidates.
  File.Delete(priorPath+".tmp");Need(RebirthWorkstationCompletionOutbox.Archive(state)&&state.Path==archive&&state.NativeStation==null,"exact archive rename outcome could not finish metadata retirement");
  state=Complete();priorPath=state.Path;archive=Path.Combine(state.Root,"archive",state.Original.Operation+".xml");File.Copy(priorPath,archive);
  Need(RebirthWorkstationCompletionOutbox.Archive(state)&&!File.Exists(priorPath)&&File.Exists(archive),"exact resolved duplicate remained in cold pending scan");
  GameManager.Instance.World=new World{Remote=true};
  var client=RebirthWorkstationCompletionOutbox.Retain(new RebirthWorkstationCompletionOriginal(),"client");
  var clientExact=RebirthWorkstationCompletionPublication.ReadPrimary(client.Path);var clientChanged=new XElement(clientExact);clientChanged.SetAttributeValue("decision",true);clientChanged.Save(client.Path);
  Need(!RebirthWorkstationCompletionOutbox.Acknowledge(client)&&!client.Acknowledged,"ACK overwrote changed client stage head");clientExact.Save(client.Path);
  ThreadManager.Main=false;Need(!RebirthWorkstationCompletionOutbox.Acknowledge(client),"worker acknowledged original");ThreadManager.Main=true;
  GameManager.Instance.World.Remote=false;Need(!RebirthWorkstationCompletionOutbox.Acknowledge(client),"authority world accepted client ACK");GameManager.Instance.World.Remote=true;
  RebirthWorkstationCompletionPublication.FailAt=RebirthWorkstationCompletionPublication.Writes+1;
  Need(!RebirthWorkstationCompletionOutbox.Acknowledge(client)&&client.Acknowledged&&!client.Saved,"failed ACK publication lost pending known metadata outcome");
  RebirthWorkstationCompletionPublication.FailAt=0;Need(RebirthWorkstationCompletionOutbox.Acknowledge(client)&&client.Saved,"known ACK metadata cannot resume");
  int writes=RebirthWorkstationCompletionPublication.Writes;Need(RebirthWorkstationCompletionOutbox.Acknowledge(client)&&RebirthWorkstationCompletionPublication.Writes==writes,"saved ACK repeated publication");
  Need(RebirthWorkstationCompletionOutbox.Archive(client),"saved client ACK not retired");
  GameManager.Instance.World=new World();
  var cold=RebirthWorkstationCompletionOutbox.Retain(new RebirthWorkstationCompletionOriginal(),"server");cold.SkillAttempted=true;cold.SkillMayInvoke=true;
  Need(RebirthWorkstationCompletionOutbox.Save(cold),"cold fixture attempted stage not saved");string coldOperation=cold.Original.Operation;
  // Discard only fixture process caches to model a restart, never a production recovery operation.
  var activeField=typeof(RebirthWorkstationCompletionOutbox).GetField("active",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
  ((System.Collections.IDictionary)activeField.GetValue(null)).Clear();
  var loaded=RebirthWorkstationCompletionOutbox.Pending("server").Single(value=>value.Original.Operation==coldOperation);
  Need(loaded.SkillAttempted&&!loaded.Skill&&!loaded.SkillMayInvoke&&!loaded.StatisticsMayInvoke&&loaded.Saved,"cold attempted original regained native effect permission");
  Console.WriteLine("PASS actual outbox full saved-stage qualification; native writer/crash/topology NOT RUN");
 }
}
