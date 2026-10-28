// AUTHORED_NOT_RUN. Entire production GroupTerminal partial. Individual member
// factories have separate production cohorts; native/PDS publishers are adapters.
using System;using System.Collections.Generic;using System.Linq;using System.Xml.Linq;
class World{}class TileEntityWorkstation{}
class ClientInfo{internal int Replies;internal void SendPackage(NetPackageRebirthUiDeferredOwnerReplyV1 value){Replies++;}}
class NetPackageRebirthUiDeferredOwnerReplyV1{internal NetPackageRebirthUiDeferredOwnerReplyV1 Setup(Guid group,int status,string digest)=>this;}
static class NetPackageManager{internal static T GetPackage<T>() where T:new()=>new T();}
class FixtureLease:IDisposable{internal static int Held;bool closed;internal FixtureLease(){Held++;}public void Dispose(){if(!closed){closed=true;Held--;}}}
static class RebirthWorkstationCompletionOutbox{internal static string CurrentGuid(World world)=>"world";}
static class RebirthWorkstationCompletionPublication{internal static bool Writable=true,Readable=true;static Dictionary<string,XElement> files=new Dictionary<string,XElement>();internal static bool MatchesPublishedBytes(string path,byte[] bytes)=>true;internal static bool Write(string path,XElement record){if(!Writable)return false;files[path]=new XElement(record);return true;}internal static XElement ReadPrimary(string path)=>Readable&&files.TryGetValue(path,out var record)?new XElement(record):null;}
static class RebirthWorkstationCompletionPipeline{
 internal class NativeAddOriginal{internal XElement Before;internal bool RemoteTerminalSaved;internal bool SourceCurrentWithoutRow()=>true;internal IDisposable RetainForCompletion()=>new FixtureLease();internal bool SaveHostOwnerTerminal(RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication proof){RemoteTerminalSaved=true;return true;}}
 internal class NativeHostOriginal{internal Guid Operation;internal XElement Before;internal bool RemoteOwnerTerminalSaved,Save=true,Pending,Closed;internal bool SourceCurrent()=>!Closed;internal IDisposable Retain()=>new FixtureLease();internal bool SaveRemoteOwnerTerminal(RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication proof){if(!Save||!proof.IncludesRemoteNativeOriginal(this))return false;RemoteOwnerTerminalSaved=true;return true;}}
 internal class CachedHostOriginal{internal bool OwnerTerminalSaved,IsPhysicalPending;internal NativeHostOriginal NativeSource;internal bool SourceCurrentWithoutRows()=>true;internal IDisposable Retain()=>new FixtureLease();internal bool TrySaveOwnerTerminal(RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication proof){OwnerTerminalSaved=true;return true;}}
 internal class CachedCustody{internal object OwnerTerminal;internal string WorldGuid="world";internal CachedHostOriginal HostOriginal;internal NativeHostOriginal RemoteSource;internal bool PrimaryCurrent()=>true;}
 internal static readonly Dictionary<Guid,NativeHostOriginal> Remote=new Dictionary<Guid,NativeHostOriginal>();internal static readonly Dictionary<Guid,NativeAddOriginal> Local=new Dictionary<Guid,NativeAddOriginal>();
 internal static bool TryFindNativeHostOriginal(Guid operation,World world,TileEntityWorkstation station,out NativeHostOriginal source)=>Remote.TryGetValue(operation,out source);
 internal static bool TryFindRetainedNativeAddOriginal(Guid operation,World world,TileEntityWorkstation station,out NativeAddOriginal source)=>Local.TryGetValue(operation,out source);
 internal static Guid FailPrepare;internal static List<Guid> Prepared=new List<Guid>(),Retired=new List<Guid>();internal static bool HasPendingRemoteCachedPrimary(NativeHostOriginal source)=>source?.Pending==true;
 internal class NativeRetirement{internal NativeHostOriginal Source;internal void Retire(){Source.Closed=true;Retired.Add(Source.Operation);}}
 internal class CachedPhysicalPublication{internal NativeHostOriginal NativeSource;internal bool Fail,Retired;}
 internal class CachedGroupRetirement{internal CachedPhysicalPublication Source;internal static CachedGroupRetirement Prepare(CachedPhysicalPublication source,RebirthProductiveEarnedUiDeferredTransport.HostCachedPhysicalReceived received)=>source.Fail?null:new CachedGroupRetirement{Source=source};internal void Retire(){Source.Retired=true;}}
 internal static bool TryPrepareRetiredRemoteNativeOriginal(NativeHostOriginal source,CachedPhysicalPublication successor,out NativeRetirement value)=>TryPrepareRetiredRemoteNativeOriginal(source,out value);
 internal static bool TryPrepareRetiredRemoteNativeOriginal(NativeHostOriginal source,out NativeRetirement value){Prepared.Add(source.Operation);value=null;if(source.Operation==FailPrepare)return false;value=new NativeRetirement{Source=source};return true;}
}
static class RebirthProductiveEarnedUiRemote{
 internal class Registration{internal RebirthProductiveEarnedUiDeferredTransport.HostContribution Deferred;internal string Hash;internal Guid Nonce;internal TileEntityWorkstation Station;internal bool Completed;}
 internal static readonly Dictionary<Guid,Registration> Values=new Dictionary<Guid,Registration>();
 internal static bool TryFindRegisteredContribution(Guid operation,out Registration registration)=>Values.TryGetValue(operation,out registration);
 internal static IDisposable RetainDeferredGroup(Registration registration)=>new FixtureLease();
 internal static bool AcceptDeferredTerminal(RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication proof){proof.Original.Original.Completed=true;return true;}
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal sealed class HostContribution{
  internal Guid Operation;internal World World;internal TileEntityWorkstation Station;internal XElement ImmutableMembership;internal bool Live=true,Seal=true,RoutingSettled;internal int NativePhase=3;
  internal RebirthWorkstationCompletionPipeline.NativeHostOriginal NativeOriginal;internal RebirthWorkstationCompletionPipeline.CachedHostOriginal CachedOriginal;internal RebirthProductiveEarnedUiRemote.Registration Original;internal Authority AuthorityOriginal;
  internal List<LoadedNativeMember> LoadedNativeMembers=new List<LoadedNativeMember>();internal List<LoadedUiMember> LoadedUiMembers=new List<LoadedUiMember>();
  internal bool Closed;internal bool SourceCurrentWithoutViewer()=>Live&&!Closed;internal bool TrySealStation()=>Live&&!Closed&&Seal;internal void Dispose(){Closed=true;if(host.TryGetValue(Operation,out var source)&&ReferenceEquals(source,this))host.Remove(Operation);}
 }
 internal class Authority{internal RebirthWorkstationCompletionPipeline.NativeAddOriginal NativeOriginal;}
 internal class LoadedNativeMember{
  internal Guid Operation;internal HostContribution Aggregate;internal XElement Node;internal bool Live=true,Save=true;private bool saved;internal bool TerminalSaved{get=>Live&&saved;set=>saved=value;}internal int Writes;
  internal bool Current()=>Live;internal bool Matches(XElement node)=>Live&&XNode.DeepEquals(node,Node);internal IDisposable Retain()=>Live?new FixtureLease():null;
  internal bool History=true;internal bool HistoricalTerminalSaved=>History&&saved;
  internal bool SaveTerminal(HostTerminalPublication proof){if(!Save||!proof.IncludesLoadedNativeMember(this))return false;if(!TerminalSaved){Writes++;TerminalSaved=true;}return true;}
 }
 internal class LoadedUiMember{
  internal Guid Operation;internal HostContribution Aggregate;internal XElement Node;internal bool Live=true,Save=true;private bool saved;internal bool TerminalSaved{get=>Live&&saved;set=>saved=value;}internal int Writes;
  internal bool Current()=>Live;internal bool Matches(XElement node)=>Live&&XNode.DeepEquals(node,Node);internal IDisposable Retain()=>Live?new FixtureLease():null;
  internal bool History=true;internal bool HistoricalTerminalSaved=>History&&saved;
  internal bool SaveTerminal(HostTerminalPublication proof){if(!Save||!proof.IncludesLoadedUiMember(this))return false;if(!TerminalSaved){Writes++;TerminalSaved=true;}return true;}
 }
 internal class Hydration{internal Guid Group=Guid.NewGuid();internal HostContribution[] Sources;internal bool Closed;internal void Close(){Closed=true;Sources=null;}}
 internal class RecipientScope{internal bool Live=true,Closed;internal Guid Operation;internal World World;internal TileEntityWorkstation Station;internal HostContribution Source;internal Hydration Hydration;internal string Owner="recipient",Creation="creation";internal bool Current()=>Live&&!Closed;internal IDisposable TryEnterPublicationWorkspace(long size)=>new FixtureLease();internal void Dispose(){Closed=true;}}
 internal class Removal{internal RecipientScope Scope;internal bool Live=true;internal byte[] Body={1};internal HostTerminalPublication GroupTerminal;internal List<IDisposable> GroupRetentions=new List<IDisposable>();internal bool Current()=>Live;internal void Dispose(){foreach(var lease in GroupRetentions)lease.Dispose();GroupRetentions.Clear();}}
 static class OwnerPds{internal class SavedOwnerFile{internal string Path,NativeDigest;}internal static bool TryReadBefore(RecipientScope scope,out SavedOwnerFile file){file=new SavedOwnerFile{Path="before-file",NativeDigest="before-digest"};return true;}internal static bool TryReadAfter(RecipientScope scope,out SavedOwnerFile file){file=new SavedOwnerFile{Path="after-file",NativeDigest="after-digest"};return true;}}
 internal sealed partial class HostTerminalPublication{
  private readonly Removal removal;private readonly HostContribution subject;private readonly string path;private readonly byte[] receipt={1};
  private HostTerminalPublication(Removal removal,HostContribution subject){this.removal=removal;this.subject=subject;path=subject.Operation.ToString("N");}
  internal HostContribution Original=>subject;internal bool Current()=>removal.Current()&&subject.SourceCurrentWithoutViewer()&&subject.TrySealStation();
  internal static bool TryPublishSubject(RecipientScope scope,HostContribution subject,out HostTerminalPublication proof){Publications++;proof=null;if(!Publish)return false;proof=new HostTerminalPublication(removals[scope.Operation],subject);return proof.Current();}
 }
 static readonly Dictionary<Guid,HostContribution> host=new Dictionary<Guid,HostContribution>();static readonly Dictionary<Guid,Removal> removals=new Dictionary<Guid,Removal>();
 static long MeasurePublicationElement(XElement node)=>node.ToString().Length;static Guid ReadGuid(XElement node,string field)=>Guid.ParseExact((string)node.Attribute(field),"N");static string HydrationHash(byte[] bytes)=>"digest";
 static int checks,Publications;static bool Publish=true;static RecipientScope scope;static HostContribution parent;static Removal removal;static LoadedNativeMember remoteMember,localMember;static LoadedUiMember uiMember;
 static XElement Node(Guid operation,string kind)=>new XElement("member",new XAttribute("operation",operation.ToString("N")),new XAttribute("kind",kind),kind=="native"?new XElement("nativeOriginal",new XElement("nativeOwningBefore",new XAttribute("operation",operation.ToString("N")))):null);
 static void Seed(){
  if(removal!=null){foreach(var lease in removal.GroupRetentions)lease.Dispose();removal.GroupRetentions.Clear();}
  host.Clear();removals.Clear();RebirthWorkstationCompletionPipeline.Remote.Clear();RebirthWorkstationCompletionPipeline.Local.Clear();RebirthProductiveEarnedUiRemote.Values.Clear();Publications=0;Publish=true;
  var world=new World();var station=new TileEntityWorkstation();Guid operation=Guid.NewGuid();var own=Node(operation,"native");
  parent=new HostContribution{World=world,Station=station,Operation=operation,NativeOriginal=new RebirthWorkstationCompletionPipeline.NativeHostOriginal{Operation=operation,Before=new XElement(own.Element("nativeOriginal").Elements().Single())}};
  remoteMember=new LoadedNativeMember{Operation=Guid.NewGuid(),Aggregate=parent};remoteMember.Node=Node(remoteMember.Operation,"native");localMember=new LoadedNativeMember{Operation=Guid.NewGuid(),Aggregate=parent};localMember.Node=Node(localMember.Operation,"native");uiMember=new LoadedUiMember{Operation=Guid.NewGuid(),Aggregate=parent};uiMember.Node=Node(uiMember.Operation,"uio");
  parent.LoadedNativeMembers.AddRange(new[]{remoteMember,localMember});parent.LoadedUiMembers.Add(uiMember);parent.ImmutableMembership=new XElement("contribution",new XElement("members",own,new XElement(remoteMember.Node),new XElement(localMember.Node),new XElement(uiMember.Node)));
  host.Add(operation,parent);RebirthWorkstationCompletionPipeline.Remote.Add(operation,parent.NativeOriginal);
  scope=new RecipientScope{Operation=operation,World=world,Station=station,Source=parent,Hydration=new Hydration{Sources=new[]{parent}}};removal=new Removal{Scope=scope};removals.Add(operation,removal);
 }
 static bool Finalize(out HostTerminalPublication proof)=>TryFinalizeHostGroup(scope,out proof);static void Need(bool value){if(!value)throw new Exception("mixed loaded group assertion "+checks);checks++;}
 public static void Main(string[] args){if(args.Length==1&&args[0]=="release"){RunRelease();return;}if(args.Length==1&&args[0]=="cached"){RunCached();return;}try{
  Seed();Need(Finalize(out var proof)&&proof.groupComplete);Need(proof.loadedNativeMembers.Length==2&&proof.loadedUiMembers.Length==1&&proof.remoteNativeMembers.Length==1);Need(Publications==1&&proof.groupMembers.Length==1);Need(remoteMember.TerminalSaved&&localMember.TerminalSaved&&uiMember.TerminalSaved&&parent.NativeOriginal.RemoteOwnerTerminalSaved);Need(removal.GroupRetentions.Count==4&&FixtureLease.Held==4);
  Need(Finalize(out var again)&&ReferenceEquals(proof,again)&&Publications==1);var evidence=proof.CopyGroupEvidence();Need((string)evidence.Attribute("recipient")=="recipient"&&evidence.Attribute("owner")==null);Need(RebirthWorkstationCompletionPipeline.Remote.Count==1&&RebirthWorkstationCompletionPipeline.Local.Count==0&&RebirthProductiveEarnedUiRemote.Values.Count==0);
  Seed();parent.NativePhase=2;Need(!Finalize(out _)&&!parent.RoutingSettled&&FixtureLease.Held==0);
  Seed();parent.Seal=false;Need(!Finalize(out _)&&FixtureLease.Held==0);
  Seed();remoteMember.Live=false;Need(!Finalize(out _)&&FixtureLease.Held==0);
  Seed();remoteMember.Node.SetAttributeValue("changed",true);Need(!Finalize(out _)&&FixtureLease.Held==0);
  Seed();parent.LoadedNativeMembers.Add(remoteMember);Need(!Finalize(out _)&&FixtureLease.Held==0);
  Seed();parent.LoadedNativeMembers.Remove(remoteMember);Need(!Finalize(out _)&&FixtureLease.Held==0);
  Seed();parent.LoadedUiMembers.Clear();Need(!Finalize(out _)&&FixtureLease.Held==0);
  Seed();parent.ImmutableMembership.Element("members").Elements().Last().SetAttributeValue("kind","unknown");Need(!Finalize(out _)&&FixtureLease.Held==0);
  Seed();localMember.Save=false;Need(!Finalize(out _)&&remoteMember.TerminalSaved&&!localMember.TerminalSaved&&removal.GroupTerminal==null&&!parent.RoutingSettled&&FixtureLease.Held==0);localMember.Save=true;Need(Finalize(out proof)&&proof.groupComplete&&remoteMember.Writes==1&&FixtureLease.Held==4);
  Seed();uiMember.Save=false;Need(!Finalize(out _)&&removal.GroupTerminal==null&&!parent.RoutingSettled&&FixtureLease.Held==0);
  Seed();Publish=false;Need(!Finalize(out _)&&!remoteMember.TerminalSaved&&!uiMember.TerminalSaved&&FixtureLease.Held==0);
  Seed();Need(Finalize(out proof));uiMember.Live=false;Need(!Finalize(out _)&&Publications==1);
  Seed();scope.Live=false;Need(!Finalize(out _)&&Publications==0);
  Seed();parent.NativeOriginal.Save=false;Need(!Finalize(out _)&&!remoteMember.TerminalSaved&&FixtureLease.Held==0);
  Seed();Need(Finalize(out proof));remoteMember.Aggregate=new HostContribution();Need(!proof.IncludesLoadedNativeMember(remoteMember));
  Seed();Need(Finalize(out proof));uiMember.TerminalSaved=false;Need(!Finalize(out _));
  Console.WriteLine("PASS "+checks+" production mixed loaded group assertions; native/PDS/member publishers excluded");
 }finally{if(removal!=null)foreach(var lease in removal.GroupRetentions)lease.Dispose();}}
}
// ACTUAL_LOADED_GROUP
