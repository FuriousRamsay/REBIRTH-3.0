using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
// Actual trade completion store. Scope, atomic save/readback and publisher are
// explicit adapters; this cohort cannot certify real filesystem atomicity.
class World{}
class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
public static partial class RebirthNpcSocialPersistenceStore
{
 private static readonly object Sync=new object();private static bool loaded=true,flushing,authoritative=true,saveWorks=true,readback=true;
 private static World boundWorld=GameManager.Instance.World;private static string boundPath="save",PathName="save";
 private static int publishes,saves,mutations;
 private static bool HasAuthoritativeWorld()=>authoritative;
 private static bool CanReset=>readback;
 private static void EnsureLoaded(){}
 private static void ObserveOwnedMutation(){mutations++;}
 private static bool TryFlush(){saves++;return saveWorks;}
 internal static void Run()
 {
  int checks=0;void Check(bool result){checks++;if(!result)throw new Exception("Social completion assertion "+checks);}
  var id=Guid.NewGuid();string identity=id.ToString("N")+":"+new string('a',64)+":"+new string('b',64);
  Func<bool> publish=()=>{publishes++;return true;};
  ThreadManager.Main=false;Check(!TrySaveTradeCompletion(id,identity,publish)&&publishes==0);ThreadManager.Main=true;
  authoritative=false;Check(!TrySaveTradeCompletion(id,identity,publish)&&publishes==0);authoritative=true;
  PathName="foreign";Check(!TrySaveTradeCompletion(id,identity,publish)&&publishes==0);PathName="save";
  Check(!TrySaveTradeCompletion(id,"malformed",publish)&&publishes==0);
  Check(!TrySaveTradeCompletion(id,identity,()=>false)&&tradeCompletions.Count==0);
  saveWorks=false;Check(!TrySaveTradeCompletion(id,identity,publish)&&publishes==1&&tradeCompletions.Count==1);
  Check(!TrySaveTradeCompletion(id,identity,publish)&&publishes==1&&mutations==1);
  saveWorks=true;readback=false;Check(!TrySaveTradeCompletion(id,identity,publish)&&publishes==1);
  readback=true;Check(TrySaveTradeCompletion(id,identity,publish)&&publishes==1);
  Check(IsTradeCompletionSaved(id,identity));Check(!IsTradeCompletionSaved(id,identity.Replace('b','c')));
  Check(!TrySaveTradeCompletion(id,identity.Replace('b','c'),publish)&&publishes==1);
  // Model later eviction of the general social replay window: this actual
  // trade journal has no dependency on that bounded queue.
  Check(TrySaveTradeCompletion(id,identity,publish)&&publishes==1);
  var document=new XmlDocument();document.LoadXml("<rebirthNpcSocialState><tradeCompletions><event id='"+id.ToString("N")+"' identity='"+identity+"'/></tradeCompletions></rebirthNpcSocialState>");
  tradeCompletions=ReadTradeCompletions(document);Check(TrySaveTradeCompletion(id,identity,publish)&&publishes==1);
  document.DocumentElement.FirstChild.AppendChild(document.DocumentElement.FirstChild.FirstChild.CloneNode(true));
  bool rejected=false;try{ReadTradeCompletions(document);}catch{rejected=true;}Check(rejected);
  tradeCompletions=new Dictionary<Guid,string>();for(int i=0;i<MaxTradeCompletions;i++)tradeCompletions.Add(Guid.NewGuid(),identity);
  Check(!TrySaveTradeCompletion(id,identity,publish)&&publishes==1);
  Console.WriteLine(checks+" actual social completion assertions passed; save/scope/native adapters NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcSocialPersistenceStore.Run();}
