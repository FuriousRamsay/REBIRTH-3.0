// AUTHORED_NOT_RUN. Complete production retained client binding with explicit
// native grant/owner hydration, publication and allocator adapters.
using System;using System.IO;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
class World{}
class EntityPlayerLocal{}
class XUiC_RecipeStack{}
static class RebirthProductiveEarnedObservationBudget{
 internal static bool Admit=true;internal static int Live;
 internal sealed class Lease:IDisposable{bool closed;internal Lease(){Live++;}public void Dispose(){if(!closed){closed=true;Live--;}}}
 internal static bool TryRetainDetached(long size,out IDisposable memory){memory=Admit?new Lease():null;return Admit;}
 internal static bool TryResizeDetached(IDisposable memory,long size)=>Admit&&memory!=null;
}
static class RebirthWorkstationCompletionOutbox{internal static string Root(string role)=>"client-root";internal static string CurrentGuid(World world)=>"world";}
static class RebirthWorkstationCompletionOriginal{
 internal static bool ValidKey(string text)=>text?.Length==64&&text.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
 internal static string Hash(string text){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
}
static class RebirthWorkstationCompletionPublication{
 internal static Dictionary<string,XElement> Files=new Dictionary<string,XElement>();internal static bool Match=true;
 internal static long PublishedLength(string path)=>Files.ContainsKey(path)?100:-1;
 internal static XElement ReadPrimary(string path)=>new XElement(Files[path]);
 internal static byte[] CopyPublishedBytes(string path)=>Files.ContainsKey(path)?new byte[100]:null;
 internal static bool MatchesPublishedBytes(string path,byte[] image)=>Match&&Files.ContainsKey(path)&&image?.Length==100;
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal sealed class RemoteCachedPrimaryGrant{
  internal Guid Operation=Guid.NewGuid(),RetainedAction=Guid.NewGuid(),Ticket=Guid.NewGuid(),Group=Guid.NewGuid();internal string Original="original";
  internal EntityPlayerLocal Actor=new EntityPlayerLocal();internal World World=new World();internal XUiC_RecipeStack Card=new XUiC_RecipeStack();
  internal bool CurrentValue=true,Native=true,RetainAllowed=true;internal int Leases;
  internal XElement Full=new XElement("nativeOwningFullBoundary",new XAttribute("value","full"));
  internal XElement Before;
  internal bool CurrentWindow()=>CurrentValue;internal bool CurrentWindowSource()=>CurrentValue;internal bool NativeCurrent()=>Native;
  internal XElement CopyFull()=>new XElement(Full);
  internal XElement CopyBefore()=>new XElement(Before);
  internal IDisposable Retain()=>RetainAllowed?new GrantLease(this):null;
  sealed class GrantLease:IDisposable{RemoteCachedPrimaryGrant source;internal GrantLease(RemoteCachedPrimaryGrant value){source=value;value.Leases++;}public void Dispose(){if(source!=null){source.Leases--;source=null;}}}
 }
}
public static partial class RebirthRecipeOutputIntegration{
 internal sealed partial class RemoteOutputColdClient{
  internal bool ExternalCached=true,Attached,EpisodeCurrent=true,BeforeCurrent=true,FinalCallbackSaved,QueueOnlyAuthorized;internal Guid Operation,LocalOperation=Guid.NewGuid();internal string FramePath,OriginalHash="owner-original";internal int Prefix=1;
  internal EntityPlayerLocal Actor;internal XElement Remote,PrefixRequest;internal XUiC_RecipeStack ExpectedCard;
  internal XElement Source=>new XElement(Remote);internal XElement Request=>new XElement(PrefixRequest);
  internal bool BeforeBindingCurrent()=>BeforeCurrent&&EpisodeCurrent;internal bool MatchesCard(XUiC_RecipeStack card)=>ReferenceEquals(card,ExpectedCard);
 }
}
internal static partial class RebirthWorkstationCompletionPipeline{
 static string RemoteOutputRequestHash(XElement request)=>RebirthWorkstationCompletionOriginal.Hash(request.ToString(SaveOptions.DisableFormatting));
 static bool PathEquals(string first,string second)=>string.Equals(Path.GetFullPath(first),Path.GetFullPath(second),StringComparison.Ordinal);
 static XElement ActiveBefore(XElement frame)=>frame.Element("before")?.Elements().SingleOrDefault();
 static RebirthRecipeOutputIntegration.RemoteOutputColdClient owner;static RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant grant;
 static XElement action,frame;static Guid oldGroup;static string actionPath;static int checks;
 static void Fresh(){
  if(RebirthProductiveEarnedObservationBudget.Live!=0)throw new Exception("Prior binding leaked allocation");
  RebirthProductiveEarnedObservationBudget.Admit=true;RebirthWorkstationCompletionPublication.Match=true;RebirthWorkstationCompletionPublication.Files.Clear();
  grant=new RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant();oldGroup=Guid.NewGuid();
  var active=new XElement("nativeOwningBefore",new XElement("cachedPredecessor",grant.CopyFull()));
  grant.Before=new XElement(active);
  owner=new RebirthRecipeOutputIntegration.RemoteOutputColdClient{Operation=grant.Operation,Actor=grant.Actor,ExpectedCard=grant.Card,
   Remote=new XElement("remoteOutputSource",new XAttribute("nonce",grant.RetainedAction.ToString("N")),new XAttribute("source",grant.Original),new XElement("activeBefore",active)),
   PrefixRequest=new XElement("cachedViewer",new XAttribute("action",grant.RetainedAction.ToString("N")),new XAttribute("source",grant.Original),new XAttribute("generation",RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting))))};
  actionPath=Path.Combine("client-root","cachedPhysical",grant.Operation.ToString("N")+"."+grant.RetainedAction.ToString("N")+".xml");owner.FramePath=actionPath+".intent."+new string('a',64);
  action=new XElement("workstationCompletionIntent",new XAttribute("operation",grant.Operation.ToString("N")),new XAttribute("world","world"),
   new XElement("cachedPhysicalPrepare",new XAttribute("ticket",grant.Ticket.ToString("N")),new XAttribute("action",grant.RetainedAction.ToString("N")),new XAttribute("group",oldGroup.ToString("N")),new XAttribute("session",73),new XAttribute("original",grant.Original)));
  frame=new XElement("workstationCompletionIntent",new XAttribute("operation",grant.Operation.ToString("N")),new XAttribute("world","world"),new XElement("before",active),
   new XElement("cachedPhysicalSource",new XAttribute("path",actionPath),new XAttribute("ticket",grant.Ticket.ToString("N")),new XAttribute("action",grant.RetainedAction.ToString("N")),new XAttribute("original",grant.Original)));
  RebirthWorkstationCompletionPublication.Files.Add(actionPath,action);RebirthWorkstationCompletionPublication.Files.Add(owner.FramePath,frame);
 }
 static RetainedCachedClientBinding Capture()=>RetainedCachedClientBinding.TryCapture(owner,grant);
 static XElement HostAck(RetainedCachedClientBinding value)=>new XElement("ack",new XAttribute("stage","cachedOutputRestartReady"),new XAttribute("action",value.Action.ToString("N")),new XAttribute("ticket",grant.Ticket.ToString("N")),new XAttribute("source",grant.Original),new XAttribute("group",oldGroup.ToString("N")),new XAttribute("session",73),new XAttribute("generation",(string)owner.PrefixRequest.Attribute("generation")),new XAttribute("count",owner.Prefix),new XAttribute("prefixDigest",RemoteOutputRequestHash(owner.PrefixRequest)),new XAttribute("localOperation",owner.LocalOperation.ToString("N")),new XAttribute("original",owner.OriginalHash),new XAttribute("challenge","transport"));
 static void Need(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
 public static void Main(){
  Fresh();using(var value=Capture()){
   Need(value!=null&&value.Current()&&grant.Leases==1,"actual grant and owner source binding");
   Need(value.OriginalGroup==oldGroup&&value.OriginalGroup!=grant.Group&&value.OriginalSession==73,"old group/session retained separately");
   var copy=value.CopyPrepare();copy.SetAttributeValue("session",99);Need((long)value.CopyPrepare().Attribute("session")==73,"request copy is isolated");
   grant.Native=false;Need(value.Current(),"later native change does not mint a new admission or discard retained identity");
   grant.CurrentValue=false;Need(!value.Current()&&value.CopyPrepare()==null,"stale actual native window source refuses");
  }
  Need(grant.Leases==0&&RebirthProductiveEarnedObservationBudget.Live==0,"leases released");
  Fresh();grant.Native=false;Need(Capture()==null,"native mismatch before admission");
  Fresh();grant.RetainedAction=Guid.Empty;Need(Capture()==null,"fresh grant cannot adopt old action");
  Fresh();owner.Attached=true;Need(Capture()==null,"already attached original refuses");
  Fresh();owner.EpisodeCurrent=false;Need(Capture()==null,"foreign owner hydration refuses");
  Fresh();grant.Operation=Guid.NewGuid();Need(Capture()==null,"operation substitution refuses");
  Fresh();owner.Remote.SetAttributeValue("source","foreign");Need(Capture()==null,"source substitution refuses");
  Fresh();owner.Remote.Element("activeBefore").Elements().Single().Element("cachedPredecessor").Elements().Single().SetAttributeValue("value","foreign");Need(Capture()==null,"changed cached full refuses");
  Fresh();frame.Element("cachedPhysicalSource").SetAttributeValue("ticket",Guid.NewGuid().ToString("N"));Need(Capture()==null,"frame ticket substitution refuses");
  Fresh();action.Element("cachedPhysicalPrepare").SetAttributeValue("session",0);Need(Capture()==null,"invalid historical session refuses");
  Fresh();owner.FramePath+=".foreign";Need(Capture()==null,"foreign frame namespace refuses");
  Fresh();RebirthProductiveEarnedObservationBudget.Admit=false;Need(Capture()==null,"allocation refusal");
  Fresh();grant.RetainAllowed=false;Need(Capture()==null&&RebirthProductiveEarnedObservationBudget.Live==0,"failed grant retain rolls back allocation");
  Fresh();using(var value=Capture()){RebirthWorkstationCompletionPublication.Match=false;Need(!value.Current(),"changed pinned files refuse");}
  Fresh();action.Element("cachedPhysicalPrepare").SetAttributeValue("challenge",new string('a',64));using(var value=Capture())Need(value!=null&&value.CopyPrepare().Attribute("challenge")==null,"old transport challenge excluded");
  Fresh();action.Element("cachedPhysicalPrepare").SetAttributeValue("foreign","extra");Need(Capture()==null,"unknown fields refuse");
  Fresh();grant.Before.SetAttributeValue("viewer","foreign");Need(Capture()==null,"native grant original owner differs from active frame");
  Fresh();owner.PrefixRequest.SetAttributeValue("generation",new string('b',64));Need(Capture()==null,"current generation must hash complete active frame");
  Fresh();using(var value=Capture()){
   Need(value.AcceptHostPrefix(HostAck(value))&&value.HostPrefixRestored,"exact authenticated host prefix metadata binds");
   var wrong=HostAck(value);wrong.SetAttributeValue("session",74);Need(!value.AcceptHostPrefix(wrong)&&value.HostPrefixRestored,"foreign reply cannot replace retained positive identity");
   wrong=HostAck(value);wrong.Add(new XElement("unexpected"));Need(!value.AcceptHostPrefix(wrong),"reply body cannot smuggle extra source");
  }
  Fresh();using(var value=Capture()){
   value.AcceptHostPrefix(HostAck(value));owner.Attached=true;var delivery=new XElement("workstationCompletionOriginal",new XAttribute("native","actual"));
   var published=new XElement(frame);published.Add(new XElement("confirmedOriginal",new XElement(delivery)));RebirthWorkstationCompletionPublication.Files[owner.FramePath]=published;
   Need(value.ObserveConfirmedFramePublication(delivery,published)&&value.Current(),"same actual delivery publication advances pinned frame");
   Need(!value.ObserveConfirmedFramePublication(new XElement("workstationCompletionOriginal",new XAttribute("native","foreign")),published),"different delivery cannot replace retained original");
   var foreign=new XElement(published);foreign.SetAttributeValue("owner","foreign");RebirthWorkstationCompletionPublication.Files[owner.FramePath]=foreign;
   Need(!value.ObserveConfirmedFramePublication(delivery,foreign),"delivery publication cannot change unrelated original fields");
  }
  Fresh();owner.FinalCallbackSaved=true;using(var value=Capture()){var ack=HostAck(value);ack.SetAttributeValue("finalCallback",true);ack.SetAttributeValue("queueOnly",true);Need(value.AcceptHostPrefix(ack)&&value.HostFinalOwnerRestored&&owner.QueueOnlyAuthorized,"actual final reply grants only final queue role");}
  Fresh();owner.FinalCallbackSaved=true;using(var value=Capture())Need(!value.AcceptHostPrefix(HostAck(value))&&!owner.QueueOnlyAuthorized,"prefix reply cannot authorize final queue");
  Fresh();using(var value=Capture()){var ack=HostAck(value);ack.SetAttributeValue("finalCallback",true);ack.SetAttributeValue("queueOnly",true);Need(!value.AcceptHostPrefix(ack)&&!owner.QueueOnlyAuthorized,"ordinary prefix cannot assume final callback role");}
  Need(grant.Leases==0&&RebirthProductiveEarnedObservationBudget.Live==0,"all refusal paths conserve leases");
  Console.WriteLine("PASS "+checks+" actual retained cached client binding assertions; native/file adapters excluded");
 }
}
// ACTUAL_CACHED_CLIENT_BINDING
