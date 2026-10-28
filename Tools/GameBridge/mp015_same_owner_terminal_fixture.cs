// AUTHORED_NOT_RUN. Production query/retirement methods are inserted by runner.
// Native identity, native saved publication, transport and disk are explicit
// adapters: this cannot certify native saving or independent multiplayer.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;using System.Security.Cryptography;
class World {internal string Id=Guid.NewGuid().ToString();}
class EntityPlayer {internal World world;internal int entityId=7;}
class ClientInfo {}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthWorkstationCompletionOutbox {internal static string Root(string side)=>"fixture";internal static string CurrentGuid(World world)=>world.Id;}
static class RebirthProductiveEarnedObservationBudget {sealed class Lease:IDisposable{public void Dispose(){}}internal static bool TryRetainDetached(long n,out IDisposable value){value=new Lease();return true;}}
static class RebirthWorkstationCompletionPublication
{
 internal static Dictionary<string,XElement> Files=new Dictionary<string,XElement>();internal static bool RefuseReceived;
 internal static bool Write(string path,XElement image){if(image.Name!="workstationCompletionIntent"||image.Element("before")?.Elements().Count()!=1)return false;if(RefuseReceived&&path.EndsWith(".same-owner-received"))return false;if(Files.TryGetValue(path,out var old))return XNode.DeepEquals(old,image);Files.Add(path,new XElement(image));return true;}
 internal static XElement ReadPrimary(string path)=>Files.TryGetValue(path,out var found)?new XElement(found):null;
 internal static byte[] CopyPublishedBytes(string path)=>Files.TryGetValue(path,out var found)?Encoding.UTF8.GetBytes(found.ToString(SaveOptions.DisableFormatting)):null;
 internal static long PublishedLength(string path)=>CopyPublishedBytes(path)?.LongLength??0;
}
static partial class RebirthWorkstationCompletionPipeline
{
 internal class NativeHostOriginal
 {
  internal Guid Operation,Nonce;internal string SourceHash;internal EntityPlayer Viewer;internal ClientInfo Sender;internal bool Closed,NativeLive=true;
  internal bool CurrentPeer()=>!Closed&&NativeLive;internal bool SourceCurrent()=>!Closed&&NativeLive;
  internal void Dispose(){Closed=true;nativeHostOriginals.Remove(Operation);}
 }
 static Dictionary<Guid,NativeHostOriginal> nativeHostOriginals=new Dictionary<Guid,NativeHostOriginal>();
 static List<XElement> Sent=new List<XElement>();
 static void Send(bool server,XElement body,Guid operation,int player){if(server)throw new Exception("host query dispatched owner action");Sent.Add(new XElement(body));}
 static string HistoricalHash(byte[] bytes){using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(bytes));}
 // This fixture exercises the genuine same-owner source only. The distinct
 // cache-native/save issuer is deliberately an explicit refusing adapter;
 // cached producer coverage belongs to the actual hook-backed live driver.
 internal sealed class CachedPhysicalPublication {}
 [ThreadStatic]private static CachedPhysicalPublication issuingCachedPhysicalReceived;
 internal sealed class CachedPhysicalReceived {internal static CachedPhysicalReceived Issue(CachedPhysicalPublication source,string path,XElement image)=>null;}
 static bool RetireCachedSameOwnerReceived(CachedPhysicalReceived proof)=>false;
 // PRODUCTION_RECEIVED
 // PRODUCTION_QUERY
 static Guid operation;static EntityPlayer viewer;static ClientInfo peer;static NativeHostOriginal source;static string path,nonce,identity,digest;
 static void Check(bool pass,string reason){if(!pass)throw new Exception(reason);}
 static void Setup()
 {
  RebirthWorkstationCompletionPublication.Files.Clear();RebirthWorkstationCompletionPublication.RefuseReceived=false;Sent.Clear();nativeHostOriginals.Clear();
  operation=Guid.NewGuid();viewer=new EntityPlayer{world=new World()};peer=new ClientInfo();nonce=Guid.NewGuid().ToString("N");
  source=new NativeHostOriginal{Operation=operation,Viewer=viewer,Sender=peer};nativeHostOriginals.Add(operation,source);
  path=Path.Combine("fixture","remoteNative",operation.ToString("N")+".xml");
  var original=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",viewer.world.Id),
   new XElement("before",new XElement("remoteNativeOriginal",new XAttribute("viewer","owner"),new XAttribute("creation","life"),new XAttribute("nonce",nonce))));
  RebirthWorkstationCompletionPublication.Write(path,original);identity=HistoricalHash(RebirthWorkstationCompletionPublication.CopyPublishedBytes(path));source.SourceHash=identity;source.Nonce=Guid.ParseExact(nonce,"N");
  var terminal=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",viewer.world.Id),
   new XElement("before",new XElement("nativeSameOwnerSaved",new XAttribute("nonce",nonce),new XAttribute("source",identity))));
  RebirthWorkstationCompletionPublication.Write(path+".same-owner-terminal",terminal);digest=HistoricalHash(RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".same-owner-terminal"));
  RebirthProductiveEarnedUiDeferredTransport.Seed(source);
 }
 static XElement Request(bool received=false)=>new XElement("nativeSameOwnerQuery",new XAttribute("stage",received?"received":"query"),new XAttribute("nonce",nonce),new XAttribute("source",identity),new XAttribute("challenge",new string('a',64)),received?new XAttribute("terminal",digest):null);
 public static void Main()
 {
  Setup();QuerySameOwnerTerminal(viewer,peer,"owner","life",operation,Request());Check(Sent.Count==1&&(string)Sent[0].Attribute("stage")=="nativeSameOwnerTerminal"&&!source.Closed,"query did not return metadata without retiring");
  var wrong=Request();wrong.SetAttributeValue("nonce",Guid.NewGuid().ToString("N"));Sent.Clear();QuerySameOwnerTerminal(viewer,peer,"owner","life",operation,wrong);Check(Sent.Count==0&&!source.Closed,"foreign nonce adopted terminal");
  QuerySameOwnerTerminal(viewer,peer,"other","life",operation,Request());Check(Sent.Count==0&&!source.Closed,"foreign owner adopted terminal");
  RebirthWorkstationCompletionPublication.RefuseReceived=true;QuerySameOwnerTerminal(viewer,peer,"owner","life",operation,Request(true));Check(Sent.Count==0&&!source.Closed,"failed received publication retired original");
  RebirthWorkstationCompletionPublication.RefuseReceived=false;QuerySameOwnerTerminal(viewer,peer,"owner","life",operation,Request(true));Check(source.Closed&&Sent.Count==1&&(string)Sent[0].Attribute("stage")=="nativeSameOwnerRetired","positive saved received did not retire original");
  Sent.Clear();QuerySameOwnerTerminal(viewer,peer,"owner","life",operation,Request(true));Check(Sent.Count==1&&(string)Sent[0].Attribute("stage")=="nativeSameOwnerRetired","lost retirement response could not be answered historically");
  Setup();RebirthWorkstationCompletionPublication.Files[path+".same-owner-terminal"].SetAttributeValue("world",Guid.NewGuid().ToString());QuerySameOwnerTerminal(viewer,peer,"owner","life",operation,Request(true));Check(!source.Closed&&Sent.Count==0,"changed terminal world released resources");
  Setup();var changed=Request(true);changed.SetAttributeValue("terminal","wrong");QuerySameOwnerTerminal(viewer,peer,"owner","life",operation,changed);Check(!source.Closed&&Sent.Count==0,"mismatched client terminal confirmation retired original");
  Setup();source.NativeLive=false;source.Sender=new ClientInfo();QuerySameOwnerTerminal(viewer,peer,"owner","life",operation,Request(true));Check(source.Closed&&Sent.Count==1,"positive terminal metadata depended on old live actor/peer/station scope");
  Console.WriteLine("PASS extracted same-owner terminal query, failure retention, authenticated binding, retirement and historical retry; native/save/transport adapters doubled");
 }
}
static class RebirthProductiveEarnedUiDeferredTransport
{
 internal sealed class SameOwnerStationPublication:IDisposable{internal RebirthWorkstationCompletionPipeline.NativeHostOriginal Source;internal void DisposeForFixture(){}public void Dispose(){}}
 static Dictionary<Guid,SameOwnerStationPublication> sameOwnerNative=new Dictionary<Guid,SameOwnerStationPublication>();
 internal static void Seed(RebirthWorkstationCompletionPipeline.NativeHostOriginal source){sameOwnerNative.Clear();sameOwnerNative.Add(source.Operation,new SameOwnerStationPublication{Source=source});}
 internal static bool TryGetSavedSameOwnerTerminal(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,out SameOwnerStationPublication value)=>sameOwnerNative.TryGetValue(source.Operation,out value)&&!source.Closed;
 internal static bool TryGetSavedCachedSameOwnerTerminal(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,out RebirthWorkstationCompletionPipeline.CachedPhysicalPublication value){value=null;return false;}
 internal static bool TryGetSavedCachedSameOwnerTerminal(Guid operation,out RebirthWorkstationCompletionPipeline.CachedPhysicalPublication value){value=null;return false;}
 // PRODUCTION_RETIRE
}
