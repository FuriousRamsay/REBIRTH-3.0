// AUTHORED_NOT_RUN. Complete production cached native issuer. Actual selected
// send admission, native cache/load/fence and immutable filesystem are adapters.
using System;using System.Linq;using System.Xml.Linq;
class World{internal EntityPlayer Actor;internal object GetEntity(int id)=>Actor;}
class TileEntityWorkstation{}
class EntityPlayer{internal int entityId=7,Died=2;internal bool IsDead()=>false;}
class INetConnection{internal bool IsDisconnected()=>false;}
class ClientInfo{internal int entityId=7;internal bool disconnecting,loginDone=true;internal INetConnection[] netConnection;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
class Identity{internal string StorageKey="owner";}
static class RebirthStablePlayerIdentity{internal static bool TryFromClientInfo(ClientInfo sender,out Identity value){value=new Identity();return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer player)=>"creation";}
static class RebirthProductiveEarnedArtifact{internal static bool RemoteAuthority(EntityPlayer player,ClientInfo sender,object station,string creation)=>true;}
static class RebirthProductiveEarnedUiDeferredTransport{internal static long Fence=1;internal static bool TryReadNativeObservationFence(World world,object station,out long fence){fence=Fence;return true;}}
static class RebirthWorkstationCompletionPublication{internal const int MaximumBytes=1048576;internal static bool Match=true;internal static XElement Record;internal static XElement ReadPrimary(string path)=>new XElement(Record);internal static long PublishedLength(string path)=>100;internal static byte[] CopyPublishedBytes(string path)=>new byte[100];internal static bool MatchesPublishedBytes(string path,byte[] bytes)=>Match;}
internal static partial class RebirthWorkstationCompletionPipeline{
 internal class CachedCustody{
  internal bool Active=true,Memory=true;internal int Epoch=1;internal string Path="cache";internal World World;internal TileEntityWorkstation Station=new TileEntityWorkstation();internal XElement Original;internal byte[] Native={1},SavedImage={2};internal CachedCreatorIssue CreatorIssue;
  internal bool SourceCurrent()=>true;internal bool PrimaryCurrent()=>true;internal bool NativeCurrent()=>true;internal bool SavedCurrent()=>true;internal bool Physical,PhysicalObserved,RestoreAttempted;
  internal bool ReserveRetirementWorkspace(long size)=>Memory;
 }
 static bool CurrentCachedCreatorFence(CachedCustody cache,World world)=>true;
 static Guid OldTicket,OldAction;static bool History=true;
 static bool TryDescribeRetainedCachedAction(CachedCustody cache,out Guid ticket,out Guid action){ticket=OldTicket;action=OldAction;return History;}
 static CachedCustody cache;static EntityPlayer actor;static ClientInfo peer;static INetConnection connection;static XElement grant;static Guid group;static int checks;
 static void Fresh(bool retained=true){
  RebirthProductiveEarnedUiDeferredTransport.Fence=1;RebirthWorkstationCompletionPublication.Match=true;History=true;
  OldTicket=retained?Guid.NewGuid():Guid.Empty;OldAction=retained?Guid.NewGuid():Guid.Empty;
  actor=new EntityPlayer();var world=new World{Actor=actor};GameManager.Instance.World=world;connection=new INetConnection();peer=new ClientInfo{netConnection=new[]{connection}};
  cache=new CachedCustody{World=world,Original=new XElement("workstationCompletionIntent",new XElement("before",new XElement("nativeOwningBefore",new XAttribute("viewer","owner"),new XAttribute("creation","creation"),new XAttribute("deaths",2))))};
  group=Guid.NewGuid();grant=new XElement("cachedCreatorGrant",new XAttribute("ticket",(retained?OldTicket:Guid.NewGuid()).ToString("N")),retained?new XAttribute("retainedAction",OldAction.ToString("N")):null);
  issuingCachedCreatorCache=cache;
  RebirthWorkstationCompletionPublication.Record=new XElement("workstationCompletionIntent",new XElement(cache.Original.Element("before")),
   new XElement("cachedPhysicalAttempt",new XElement("cachedPhysicalPrepare",new XAttribute("ticket",OldTicket.ToString("N")),new XAttribute("action",OldAction.ToString("N")),
    new XAttribute("group",Guid.NewGuid().ToString("N")),new XAttribute("session",73),new XAttribute("original","original-hash")),new XElement(cache.Original)));
 }
 static CachedCreatorIssue Issue()=>CachedCreatorIssue.FromActualEnqueue(cache,grant,group,actor,peer,connection);
 static void Need(bool value){if(!value)throw new Exception("Retained native issue "+checks);checks++;}
 public static void Main(){
  Fresh();var issue=Issue();Need(issue!=null&&issue.RetainedAction==OldAction&&issue.Ticket==OldTicket);Need(!issue.RecordedIdentity());cache.CreatorIssue=issue;Need(issue.Current());
  RebirthWorkstationCompletionPublication.Match=false;Need(!issue.RecordedIdentity());
  Fresh();grant.SetAttributeValue("retainedAction",Guid.NewGuid().ToString("N"));Need(Issue()==null);
  Fresh();cache.Memory=false;Need(Issue()==null);
  Fresh();History=false;Need(Issue()==null);
  Fresh();issue=Issue();cache.CreatorIssue=issue;cache.Native=new byte[]{1};Need(!issue.Current());
  Fresh();issue=Issue();cache.CreatorIssue=issue;cache.Epoch++;Need(!issue.Current());
  Fresh();issue=Issue();cache.CreatorIssue=issue;cache.World=new World{Actor=actor};GameManager.Instance.World=cache.World;Need(!issue.RecordedIdentity());
  Fresh();issue=Issue();cache.CreatorIssue=issue;cache.Station=new TileEntityWorkstation();Need(!issue.RecordedIdentity());
  Fresh();issue=Issue();cache.CreatorIssue=issue;RebirthProductiveEarnedUiDeferredTransport.Fence++;Need(!issue.Current());
  Fresh();issue=Issue();cache.CreatorIssue=issue;peer.netConnection=new[]{new INetConnection()};Need(!issue.RecordedIdentity());
  Fresh();issuingCachedCreatorCache=null;Need(Issue()==null);
  Fresh(false);issue=Issue();cache.CreatorIssue=issue;Need(issue.RetainedAction==Guid.Empty&&issue.Current());
  Need(!issue.TryCopyRetainedPrepare(out var copied)&&copied==null);
  Fresh();issue=Issue();cache.CreatorIssue=issue;Need(issue.TryCopyRetainedPrepare(out copied)&&(long)copied.Attribute("session")==73&&(string)copied.Attribute("group")!=group.ToString("N"));
  copied.SetAttributeValue("session",99);Need(issue.TryCopyRetainedPrepare(out copied)&&(long)copied.Attribute("session")==73);
  RebirthWorkstationCompletionPublication.Record.Element("cachedPhysicalAttempt").Element("cachedPhysicalPrepare").SetAttributeValue("action",Guid.NewGuid().ToString("N"));Need(!issue.TryCopyRetainedPrepare(out copied)&&copied==null);
  Fresh();issue=Issue();cache.CreatorIssue=issue;RebirthWorkstationCompletionPublication.Record.Element("cachedPhysicalAttempt").Element("cachedPhysicalPrepare").SetAttributeValue("session",0);Need(!issue.TryCopyRetainedPrepare(out copied)&&copied==null);
  Fresh();issue=Issue();cache.CreatorIssue=issue;RebirthWorkstationCompletionPublication.Match=false;Need(!issue.TryCopyRetainedPrepare(out copied)&&copied==null);
  Fresh();issue=Issue();cache.CreatorIssue=issue;issue.TryCopyRetainedPrepare(out copied);Need(issue.MatchesPrepareRequest(copied));
  var wrong=new XElement(copied);wrong.SetAttributeValue("group",group.ToString("N"));Need(!issue.MatchesPrepareRequest(wrong));
  wrong=new XElement(copied);wrong.SetAttributeValue("session",74);Need(!issue.MatchesPrepareRequest(wrong));
  copied.SetAttributeValue("challenge","fresh-query");Need(issue.MatchesPrepareRequest(copied));
  cache.Native=new byte[]{9};Need(!issue.Current()&&issue.MatchesPrepareRequest(copied));
  cache.CreatorIssue=null;Need(!issue.MatchesPrepareRequest(copied));
  Fresh(false);issue=Issue();cache.CreatorIssue=issue;Need(issue.MatchesPrepareRequest(new XElement("cachedPhysicalPrepare",new XAttribute("ticket",issue.Ticket.ToString("N")),new XAttribute("action",Guid.NewGuid().ToString("N")),new XAttribute("group",group.ToString("N")),new XAttribute("session",1),new XAttribute("original","hash"))));
  Fresh();issue=Issue();cache.CreatorIssue=issue;RebirthWorkstationCompletionPublication.Record.Element("cachedPhysicalAttempt").Element("cachedPhysicalPrepare").SetAttributeValue("challenge",new string('a',64));
  Need(issue.TryCopyRetainedPrepare(out copied)&&copied.Attribute("challenge")==null&&copied.Attributes().Count()==5);
  copied.SetAttributeValue("challenge",new string('b',64));Need(issue.MatchesPrepareRequest(copied));
  RebirthWorkstationCompletionPublication.Record.Element("cachedPhysicalAttempt").Element("cachedPhysicalPrepare").SetAttributeValue("foreign","extra");Need(!issue.TryCopyRetainedPrepare(out copied)&&copied==null);
  Fresh();cache.RestoreAttempted=true;issue=Issue();cache.CreatorIssue=issue;Need(issue!=null&&issue.Current()&&!issue.CurrentBeforeAction()&&issue.TryCopyRetainedPrepare(out copied));
  Fresh(false);cache.RestoreAttempted=true;Need(Issue()==null);
  Console.WriteLine("PASS "+checks+" actual retained cached native issuer assertions; native adapters excluded");
 }
}
// ACTUAL_RETAINED_ISSUE
