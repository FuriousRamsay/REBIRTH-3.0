// AUTHORED_NOT_RUN. Entire production handoff; native/source producers are adapters.
using System;using System.Runtime.CompilerServices;using System.Threading;using System.Collections.Generic;using System.Linq;using System.Xml.Linq;
class INetConnection{internal bool Closed;internal bool IsDisconnected()=>Closed;}
class ClientInfo{internal INetConnection[] netConnection;internal object latestPlayerData=new object();}
class NetPackagePlayerData{internal ClientInfo Sender;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthProductiveEarnedUiOwnerFile{internal static bool Match=true;internal static bool MatchesDeferredOwnerFrame(object data,object frame)=>Match;}
static partial class RebirthWorkstationCompletionPipeline{
 internal class Issue{internal object Cache,IssuedWorld,IssuedStation;internal Guid Ticket;internal string Owner="owner",Creation="creation";internal int Deaths;internal ClientInfo Sender;internal INetConnection Connection;}
 internal class CachedPhysicalHostAction{internal Issue Issue;internal bool Live,Cleared;internal Guid Action;internal long Session=1;internal string Path="path",BeforeGeneration="generation";internal bool Current()=>Live;internal void ClearEvidence(){Cleared=true;}}
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal static partial class NativeViewerPds{
  static readonly object gate=new object();static readonly Dictionary<string,ViewerSource> sources=new Dictionary<string,ViewerSource>();
  class Saved{internal bool Live=true;internal string Digest="digest";internal object Frame=new object();internal XElement Request;internal bool Current()=>Live;internal bool PrefixInventoryCurrent()=>Live;}
  class Published{internal int Count;}
  class ViewerSource:IDisposable{internal RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction Cached;internal int Workers,PrefixCount=2;internal string Generation="generation";internal object RestoredCachedInputs=new object();internal Saved Before=new Saved(),After,Prefix;internal ClientInfo SourceSender;internal List<Published> PublishedPrefixes=new List<Published>();internal bool Disposed;internal bool Current()=>!Disposed;internal bool BeforeInventoryCurrent()=>true;internal bool InventoryCurrent()=>true;public void Dispose(){Disposed=true;}}
  static string CachedKey(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,string generation)=>action.Action+generation;
  static void DiscardRestoredCachedAction(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action){foreach(var key in sources.Where(p=>ReferenceEquals(p.Value.Cached,action)).Select(p=>p.Key).ToArray()){sources[key].Dispose();sources.Remove(key);}}
  static RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction oldAction,newAction;static ViewerSource oldSource,newSource;static int checks;
  static XElement Prefix(int count)=>new XElement("nativeOutputPrefix",new XAttribute("count",count),new XAttribute("operation","local"),new XAttribute("original","original"));
  static void Fresh(){
   sources.Clear();ThreadManager.Main=true;RebirthProductiveEarnedUiOwnerFile.Match=true;
   var cache=new object();var world=new object();var station=new object();var ticket=Guid.NewGuid();var action=Guid.NewGuid();
   var connection=new INetConnection{Closed=true};var peer=new ClientInfo{netConnection=new[]{connection}};
   oldAction=new RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction{Action=action,Issue=new RebirthWorkstationCompletionPipeline.Issue{Cache=cache,IssuedWorld=world,IssuedStation=station,Ticket=ticket,Sender=peer,Connection=connection}};
   var nextConnection=new INetConnection();var nextPeer=new ClientInfo{netConnection=new[]{nextConnection}};
   newAction=new RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction{Action=action,Live=true,Issue=new RebirthWorkstationCompletionPipeline.Issue{Cache=cache,IssuedWorld=world,IssuedStation=station,Ticket=ticket,Sender=nextPeer,Connection=nextConnection}};
   oldSource=Source(oldAction,2);newSource=Source(newAction,3);sources.Add(CachedKey(oldAction,"generation"),oldSource);
  }
  static ViewerSource Source(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,int count)=>new ViewerSource{Cached=action,SourceSender=action.Issue.Sender,PrefixCount=count,Prefix=new Saved{Request=new XElement("request",Prefix(count))}};
  static void Install()=>sources.Add(CachedKey(newAction,"generation"),newSource);
  static void Need(bool value){if(!value)throw new Exception("handoff assertion "+checks);checks++;}
  public static void Main(){
   Fresh();using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Need(tx!=null&&sources.Count==0&&ReferenceEquals(tx.Previous,oldAction));}Need(sources.Values.Single()==oldSource&&!oldSource.Disposed);
   Fresh();using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();}Need(sources.Values.Single()==oldSource&&newSource.Disposed&&!oldAction.Cleared);
   Fresh();bool installed=false;using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();Need(tx.Commit(()=>installed=true));}Need(installed&&oldSource.Disposed&&oldAction.Cleared&&sources.Values.Single()==newSource);
   Need(IsRetiredCachedOwnerPeer(new NetPackagePlayerData{Sender=oldAction.Issue.Sender}));Need(!IsRetiredCachedOwnerPeer(new NetPackagePlayerData{Sender=newAction.Issue.Sender}));
   oldAction.Issue.Sender.netConnection=new[]{new INetConnection()};Need(!IsRetiredCachedOwnerPeer(new NetPackagePlayerData{Sender=oldAction.Issue.Sender}));
   Fresh();oldAction.Issue.Connection.Closed=false;Need(CachedOwnerHandoff.Begin(oldAction,newAction)==null);
   Fresh();oldAction.Live=true;Need(CachedOwnerHandoff.Begin(oldAction,newAction)==null);
   Fresh();oldSource.Workers=1;Need(CachedOwnerHandoff.Begin(oldAction,newAction)==null&&sources.Values.Single()==oldSource);
   Fresh();newAction.Issue.IssuedStation=new object();Need(CachedOwnerHandoff.Begin(oldAction,newAction)==null);
   Fresh();newAction.Issue.IssuedWorld=new object();Need(CachedOwnerHandoff.Begin(oldAction,newAction)==null);
   Fresh();newAction.Issue.Owner="other";Need(CachedOwnerHandoff.Begin(oldAction,newAction)==null);
   Fresh();ThreadManager.Main=false;Need(CachedOwnerHandoff.Begin(oldAction,newAction)==null);
   Fresh();newSource.Prefix.Request.Element("nativeOutputPrefix").SetAttributeValue("count",1);using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();Need(!tx.Commit(()=>throw new Exception("must not install")));}Need(sources.Values.Single()==oldSource&&!oldSource.Disposed);
   Fresh();oldSource.PublishedPrefixes.Add(new Published{Count=4});using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();Need(!tx.Commit(()=>{}));}
   Fresh();RebirthProductiveEarnedUiOwnerFile.Match=false;using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();Need(!tx.Commit(()=>{}));}
   Fresh();oldSource.After=new Saved{Request=new XElement("request")};using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();Need(!tx.Commit(()=>{}));}
   Fresh();newSource.Generation="other";using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();Need(!tx.Commit(()=>{}));}
   Fresh();newSource.Prefix.Request.Element("nativeOutputPrefix").SetAttributeValue("original","foreign");using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();Need(!tx.Commit(()=>{}));}
   Fresh();oldSource.After=new Saved();newSource.After=new Saved{Request=new XElement("request",new XElement("nativeOutputOriginal",Prefix(3)))};using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();Need(tx.Commit(()=>{}));Need(!tx.Commit(()=>throw new Exception("duplicate install")));}Need(oldAction.Cleared&&oldSource.Disposed);
   Fresh();oldSource.After=new Saved{Digest="old-final"};newSource.After=new Saved{Digest="different-final",Request=new XElement("request",new XElement("nativeOutputOriginal",Prefix(3)))};using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();Need(!tx.Commit(()=>{}));}Need(sources.Values.Single()==oldSource&&!oldAction.Cleared);
   Fresh();bool threw=false;try{using(var tx=CachedOwnerHandoff.Begin(oldAction,newAction)){Install();tx.Commit(()=>throw new InvalidOperationException("install refused"));}}catch(InvalidOperationException){threw=true;}Need(threw&&sources.Values.Single()==oldSource&&!oldSource.Disposed);Need(!IsRetiredCachedOwnerPeer(new NetPackagePlayerData{Sender=oldAction.Issue.Sender}));
   Console.WriteLine("PASS "+checks+" production cached owner handoff assertions; native adapters excluded");
  }
 }
}
// ACTUAL_CACHED_OWNER_HANDOFF
