// AUTHORED_NOT_RUN. Complete final cached query descriptor. The caller's
// settled-notice/native-seal admission and native owner/codec are adapters.
using System;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
class World{internal EntityPlayer Actor;internal bool Remote;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Actor;}
class EntityPlayer{internal World world;internal int entityId=7,Died=2;internal bool Dead;internal bool IsDead()=>Dead;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
class ClientInfo{internal XElement latestPlayerData;}
class Clients{internal ClientInfo Peer;internal ClientInfo ForEntityId(int id)=>Peer;}
class ConnectionManager{internal Clients Clients=new Clients();}
static class SingletonMonoBehaviour<T>{internal static ConnectionManager Instance=new ConnectionManager();}
class Identity{internal string StorageKey="owner";}
static class RebirthStablePlayerIdentity{internal static bool Valid=true;internal static bool TryFromClientInfo(ClientInfo peer,out Identity identity){identity=new Identity();return Valid;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer actor)=>"creation";}
static class RebirthWorkstationCompletionPublication{
 internal static Dictionary<string,XElement> Files=new Dictionary<string,XElement>();internal static long PublishedLength(string path)=>Files.ContainsKey(path)?100:-1;
 internal static XElement ReadPrimary(string path)=>Files.TryGetValue(path,out var value)?new XElement(value):null;
}
class RebirthWorkstationCompletionOriginal{
 internal static RebirthWorkstationCompletionOriginal Value;internal string Operation,ViewerKey="owner",ViewerCreation="creation";
 internal static bool TryRead(XElement value,out RebirthWorkstationCompletionOriginal original){original=Value;return value?.Name=="workstationCompletionOriginal"&&original!=null;}
 internal XElement Write()=>new XElement("workstationCompletionOriginal",new XElement("delivery",new XElement("nativeOwningDelivery")));
}
static class RebirthProductiveEarnedUiOwnerFile{internal static bool MatchesDeferredOwnerFrame(XElement actual,XElement expected)=>XNode.DeepEquals(actual,expected);}
static class RebirthProductiveEarnedUiDeferredTransport{
 internal static class NativeViewerPds{internal static bool SavedActor=true;internal static bool MatchesSavedCachedPhysicalActor(string path,string hash,XElement proof,XElement active,XElement delivery,Func<long,bool> reserve)=>SavedActor;}
}
internal static partial class RebirthWorkstationCompletionPipeline{
 static XElement ActiveBefore(XElement intent)=>intent?.Element("before")?.Elements().SingleOrDefault();
 static string RemoteOutputRequestDigest(XElement request)=>"actual-digest";
 static EntityPlayer actor;static Guid operation,local,nonce;static XElement request,original,physical,last;static bool Reserve=true;static int checks;
 static void Fresh(){
  Reserve=RebirthStablePlayerIdentity.Valid=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.SavedActor=true;RebirthWorkstationCompletionPublication.Files.Clear();
  actor=new EntityPlayer();actor.world=new World{Actor=actor};GameManager.Instance.World=actor.world;var peer=new ClientInfo{latestPlayerData=new XElement("nativeOwnerFrame",new XElement("bag","accepted"))};SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=peer;
  operation=Guid.NewGuid();local=Guid.NewGuid();nonce=Guid.NewGuid();RebirthWorkstationCompletionOriginal.Value=new RebirthWorkstationCompletionOriginal{Operation=operation.ToString("N")};
  original=new XElement("workstationCompletionIntent",new XAttribute("world","world"),new XElement("before",new XElement("nativeOwningBefore",new XAttribute("viewer","owner"),new XAttribute("creation","creation"),new XAttribute("deaths",2))),new XElement("cachedOriginal",new XAttribute("remoteCreator",true)));
  physical=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world","world"),new XElement("before",new XElement("nativeOwningBefore")),new XElement("actualCachedPhysical",new XElement("confirmedOriginal",RebirthWorkstationCompletionOriginal.Value.Write()),new XElement("cachedPhysicalActorSaved",new XAttribute("operation",operation.ToString("N")),new XAttribute("nonce",nonce.ToString("N")),new XAttribute("generation","generation"),new XAttribute("actionPath","cache.remote-action.0"))));
  last=new XElement("cachedViewer",new XAttribute("stage","after"),new XAttribute("action",nonce.ToString("N")),new XAttribute("source","cache-hash"),new XAttribute("generation","generation"),new XElement(peer.latestPlayerData),new XElement("nativeOutputOriginal",new XElement("nativeOutputPrefix",new XAttribute("operation",local.ToString("N")),new XAttribute("original","owner-original"),new XAttribute("count",2))));
  RebirthWorkstationCompletionPublication.Files["cache.remote-action.0.viewer.generation.after.intent"]=new XElement("workstationCompletionIntent",new XElement("before",last));
  request=new XElement("cachedOutputFinalQuery",new XAttribute("query",local.ToString("N")),new XAttribute("finalCallback",true),new XAttribute("count",2),new XAttribute("localOperation",local.ToString("N")),new XAttribute("nonce",nonce.ToString("N")),new XAttribute("generation","generation"),new XAttribute("prefixDigest","actual-digest"),new XAttribute("original","owner-original"),new XAttribute("challenge","current-transport"));
 }
 static XElement Describe()=>DescribeCachedFinalQuery(actor,operation,request,"cache","cache-hash",original,physical,size=>Reserve);
 static void Need(bool value,string name){if(!value)throw new Exception(name);checks++;}
 public static void Main(){
  Fresh();var ack=Describe();Need(ack!=null&&(bool)ack.Attribute("queueTerminal")&&(bool)ack.Attribute("finalCallback")&&(string)ack.Attribute("nativeNonce")==nonce.ToString("N"),"exact saved final owner yields terminal metadata");
  Need(!ack.HasElements&&(string)ack.Attribute("stage")=="cachedOutputFinalSettled","terminal response contains no native action source");
  Fresh();request.SetAttributeValue("localOperation",Guid.NewGuid().ToString("N"));Need(Describe()==null,"foreign local original");
  Fresh();request.SetAttributeValue("query",Guid.NewGuid().ToString("N"));Need(Describe()==null,"foreign query identity");
  Fresh();request.SetAttributeValue("nonce",Guid.NewGuid().ToString("N"));Need(Describe()==null,"foreign cached action");
  Fresh();request.SetAttributeValue("generation","foreign");Need(Describe()==null,"foreign generation");
  Fresh();request.SetAttributeValue("count",1);Need(Describe()==null,"different saved output prefix");
  Fresh();original.Element("before").Elements().Single().SetAttributeValue("viewer","foreign");Need(Describe()==null,"different creator account");
  Fresh();actor.Died++;Need(Describe()==null,"different creator life");
  Fresh();RebirthStablePlayerIdentity.Valid=false;Need(Describe()==null,"unqualified peer");
  Fresh();RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.SavedActor=false;Need(Describe()==null,"positive saved actor chain required");
  Fresh();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer.latestPlayerData=new XElement("nativeOwnerFrame",new XElement("bag","foreign"));Need(Describe()==null,"actual current native owner frame mismatch");
  Fresh();Reserve=false;Need(Describe()==null,"workspace refusal");
  Fresh();actor.world.Remote=true;Need(Describe()==null,"remote World cannot issue host terminal");
  Fresh();physical.SetAttributeValue("world","foreign");Need(Describe()==null,"foreign physical World");
  Fresh();request.SetAttributeValue("prefixDigest","foreign");Need(Describe()==null,"foreign final request digest");
  Fresh();request.SetAttributeValue("finalCallback",false);Need(Describe()==null,"earlier prefix is not final callback");
  Fresh();GameManager.Instance.World=new World();Need(Describe()==null,"different actual World instance");
  Console.WriteLine("PASS "+checks+" actual final cached query descriptor assertions; native admission adapters excluded");
 }
}
// ACTUAL_CACHED_FINAL_QUERY
