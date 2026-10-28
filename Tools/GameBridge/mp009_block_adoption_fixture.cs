using System;using System.Linq;using System.Xml.Linq;
// Adapters: mapped immutable admission/target, durable journal I/O, already
// saved source restitution and host decline. Actual admission/settlement body.
enum RemoteResourceClientOperation{Craft=1,BlockRepair=3,BlockUpgrade=4}
class World{internal string Guid="world";internal World worldState=>this;internal EntityPlayer Actor;internal bool IsRemote()=>false;internal EntityPlayer GetEntity(int id)=>Actor;}
class EntityPlayer{internal int Died=1,entityId=7;internal World world;internal bool IsSpawned()=>true;internal bool IsDead()=>false;}
class EntityPlayerLocal:EntityPlayer{}
static class ThreadManager{internal static bool IsMainThread()=>true;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
class Identity{internal string CanonicalId="account",StorageKey="owner";}
static class RebirthStablePlayerIdentity{internal static string ComputeStorageKey(string owner)=>"owner";internal static bool TryResolveServerEntity(EntityPlayer a,out Identity i){i=new Identity();return a!=null;}}
static class RemoteResourceWorldIdentity{internal static bool TryGet(World w,out string key){key=w?.Guid;return w!=null;}}
static class RebirthOriginalActionScope{internal static bool Matches(EntityPlayer a,string o,string c)=>c=="creation"&&o=="owner";}
static class RebirthRemoteResourceClientOriginal
{internal sealed class Intent{internal Guid Operation=Guid.NewGuid();internal string World="world",Owner="account",Creation="creation",Digest="intent";internal int Deaths;internal ulong Epoch=1,Request=2;internal RemoteResourceClientOperation Kind=RemoteResourceClientOperation.BlockRepair;internal XElement Copy()=>new XElement("intent",new XAttribute("digest",Digest));}internal static bool Current(EntityPlayerLocal p,Intent i)=>false;}
static class RebirthRemoteResourceBlockOriginal{internal static bool TerminalScope(EntityPlayerLocal p,RebirthRemoteResourceClientOriginal.Intent i)=>p.world.Guid==i.World&&i.Owner=="account"&&i.Creation=="creation";}
static class RebirthRemoteResourceBlockAdmission
{internal static bool Valid=true;internal static RebirthRemoteResourceClientOriginal.Intent Original;internal static bool TryReadMappedOriginal(Guid op,out RebirthRemoteResourceClientOriginal.Intent i){i=Original;return Valid;}internal static bool HasSavedTarget(RebirthRemoteResourceClientOriginal.Intent i)=>Valid;}
static class RebirthRemoteResourceBlockRejection
{internal static string UnissuedProof(RebirthRemoteResourceClientOriginal.Intent i,string b)=>"unissued:"+b;internal static bool ValidateIssuedRootProof(XElement r)=>(string)r.Attribute("ownerNoUseProof")=="issued";internal static bool ValidateDeclinedRootProof(XElement r)=>(string)r.Attribute("ownerNoUseProof")=="declined";}
static class RebirthRemoteResourceBlockCommand{internal static bool Declined,Absent;internal static bool CanIssueOwnerPermission(RebirthRemoteResourceClientOriginal.Intent i)=>Absent;internal static bool TryReadDeclined(RebirthRemoteResourceClientOriginal.Intent i,RebirthRemoteResourceDurableJournal.Record r,out string s){s="decline";return Declined;}internal static bool TryRead(RebirthRemoteResourceClientOriginal.Intent i,RebirthRemoteResourceDurableJournal.Record r,out object c){c=null;return Declined;}}
static class RebirthRemoteResourceBlockSettlement{internal static bool MatchesRootAction(RebirthRemoteResourceClientOriginal.Intent i,RebirthRemoteResourceDurableJournal.Record r)=>true;internal static bool CanonicalAction(RebirthRemoteResourceClientOriginal.Intent i,RebirthRemoteResourceDurableJournal.Record r,out XElement action){action=r.Copy().Element("ownerAction");return action!=null;}}
class SavedOwner{internal int id=7,deaths=1;}
static class RebirthCamperEnergySavedOwner{internal static bool Available;internal static SavedOwner Image=new SavedOwner();internal static bool TryRead(Identity owner,out object actual,out SavedOwner saved){actual=null;saved=Image;return Available;}}
static class RebirthCookingRemoteAdmissionBackend{internal static float Phase=2;internal static bool NativePhase(SavedOwner saved,string receipt,out float phase,out bool present){phase=Phase;present=true;return true;}}
static class RebirthRemoteResourceOwnerAction{internal static string Receipt(Guid operation)=>operation.ToString("N");}
static class RebirthCamperInventorySnapshot{internal static byte[] Restore(byte[] b)=>b;}
static class RebirthRemoteResourceDurableJournal
{
 internal sealed class Record{internal Guid Operation=Guid.Parse("11111111111111111111111111111111");internal string Owner="owner",Creation="creation",World="world",Binding="binding";internal int Revision;internal int Phase=> (int)Image.Attribute("phase");internal XElement Image;internal Record(){}internal Record(Guid op,int revision,XElement image){Operation=op;Revision=revision;Image=image;}internal bool HasOwnerAction=>Image.Element("ownerAction")!=null;internal XElement Copy()=>new XElement(Image);}
 internal static Record Saved;internal static bool Unknown;internal static int Writes;
 internal static Guid Operation(string w,string o,string c,ulong e,ulong q,RemoteResourceClientOperation k)=>Saved.Operation;
 internal static bool TryRead(string o,Guid op,out Record r,out bool unknown){r=Saved;unknown=Unknown;return !unknown;}
 internal static bool NeverPreparedOwnerAction(Record r)=>!r.HasOwnerAction;
 internal static string Digest(string s)=>"declined";
 internal static bool TryAdvance(Record r,Action<XElement> edit,Func<bool> current,out Record next){next=null;if(!current())return false;var b=r.Copy();edit(b);next=new Record{Image=b,Revision=r.Revision+1};Saved=next;Writes++;return true;}
}
static class RebirthRemoteResourceDurableOutcome
{internal static int Reconciles;internal static bool IsRestoredBlockRefusal(RebirthRemoteResourceDurableJournal.Record r)=>r.Phase==3&&r.Image.Element("removed").Value=="";
 internal static bool AcknowledgeBlockNoPayment(EntityPlayer a,RebirthRemoteResourceClientOriginal.Intent i,Guid op,string b){RebirthRemoteResourceDurableJournal.Saved.Image.SetAttributeValue("phase",4);return true;}
 internal static bool TryReconcileBlockSources(EntityPlayer a,RebirthRemoteResourceClientOriginal.Intent i,RebirthRemoteResourceDurableJournal.Record r,out RebirthRemoteResourceDurableJournal.Record n){Reconciles++;n=r;return true;}}
// ACTUAL_SOURCE
static class Program
{
 static EntityPlayer actor;static RebirthRemoteResourceClientOriginal.Intent intent;static int count;
 static void Reset(int phase=2,int outcome=1)
 {var world=new World();actor=new EntityPlayer{world=world};world.Actor=actor;GameManager.Instance.World=world;intent=new RebirthRemoteResourceClientOriginal.Intent();RebirthRemoteResourceBlockAdmission.Original=intent;RebirthRemoteResourceBlockAdmission.Valid=true;
 RebirthRemoteResourceDurableJournal.Saved=new RebirthRemoteResourceDurableJournal.Record{Image=new XElement("root",new XAttribute("phase",phase),new XAttribute("outcome",outcome),new XAttribute("deaths",0),new XAttribute("kind",3),new XElement("removed",Convert.ToBase64String(new byte[]{1})),new XElement("sources",new XElement("source",new XAttribute("attempted",1),new XAttribute("published",1))))};
 RebirthRemoteResourceDurableJournal.Unknown=false;RebirthRemoteResourceDurableJournal.Writes=0;RebirthRemoteResourceBlockCommand.Declined=false;RebirthRemoteResourceBlockCommand.Absent=false;RebirthCamperEnergySavedOwner.Available=false;RebirthCamperEnergySavedOwner.Image=new SavedOwner();RebirthCookingRemoteAdmissionBackend.Phase=2;RebirthRemoteResourceDurableOutcome.Reconciles=0;}
 static bool Read()=>RebirthRemoteResourceBlockAdoption.Read(actor,intent,out _);
 static void Check(bool value){count++;if(!value)throw new Exception("Historical Block assertion "+count);}
 static void Main()
 {
 Reset();Check(Read()&&RebirthRemoteResourceDurableJournal.Writes==1&&(string)RebirthRemoteResourceDurableJournal.Saved.Image.Attribute("ownerNoUseProof")=="unissued:binding");
 Check(Read()&&RebirthRemoteResourceDurableJournal.Writes==1);
 Reset();RebirthRemoteResourceBlockAdmission.Valid=false;Check(!Read()&&RebirthRemoteResourceDurableJournal.Writes==0);
 Reset();intent.Creation="changed";Check(!Read());
 Reset();RebirthRemoteResourceDurableJournal.Saved.Image.Add(new XElement("ownerAction"));Check(!Read());
 Reset();RebirthRemoteResourceDurableJournal.Saved.Image.Add(new XElement("ownerAction"));RebirthRemoteResourceBlockCommand.Declined=true;Check(Read()&&(string)RebirthRemoteResourceDurableJournal.Saved.Image.Attribute("ownerNoUseProof")=="declined");
 Reset(1,0);Check(Read()&&RebirthRemoteResourceDurableOutcome.Reconciles==1&&RebirthRemoteResourceDurableJournal.Writes==0);
 Reset(3,2);RebirthRemoteResourceDurableJournal.Saved.Image.Element("removed").Value="";Check(Read()&&RebirthRemoteResourceDurableJournal.Saved.Phase==4);
 Reset(3,2);Check(Read()&&RebirthRemoteResourceDurableJournal.Saved.Phase==3);
 Reset(3,2);RebirthRemoteResourceDurableJournal.Saved.Image.Element("sources").Element("source").SetAttributeValue("published",0);Check(!Read());
 Reset();intent.Kind=RemoteResourceClientOperation.Craft;Check(!Read());
 Reset();actor.Died=0;Check(!Read());
 Reset();actor.world.Guid="other-world";Check(!Read());
 Reset();RebirthRemoteResourceDurableJournal.Saved.Image.SetAttributeValue("ownerNoUseProof","different");Check(!Read());
 Reset();RebirthRemoteResourceDurableJournal.Unknown=true;Check(!Read());
 Reset(4,1);Check(Read()&&RebirthRemoteResourceDurableJournal.Writes==0);
 Reset();RebirthRemoteResourceDurableJournal.Saved.Image.Add(new XElement("ownerAction"));RebirthRemoteResourceBlockCommand.Absent=true;RebirthCamperEnergySavedOwner.Available=true;Check(Read()&&RebirthRemoteResourceDurableJournal.Writes==1);
 Reset();RebirthRemoteResourceDurableJournal.Saved.Image.Add(new XElement("ownerAction"));RebirthCamperEnergySavedOwner.Available=true;Check(!Read()&&RebirthRemoteResourceDurableJournal.Writes==0);
 Reset();RebirthRemoteResourceDurableJournal.Saved.Image.Add(new XElement("ownerAction"));RebirthRemoteResourceBlockCommand.Absent=true;RebirthCamperEnergySavedOwner.Available=true;RebirthCookingRemoteAdmissionBackend.Phase=1;Check(!Read()&&RebirthRemoteResourceDurableJournal.Writes==0);
 Reset();RebirthRemoteResourceDurableJournal.Saved.Image.Add(new XElement("ownerAction"));RebirthRemoteResourceBlockCommand.Absent=true;RebirthCamperEnergySavedOwner.Available=true;RebirthCamperEnergySavedOwner.Image.deaths=0;Check(!Read()&&RebirthRemoteResourceDurableJournal.Writes==0);
 Console.WriteLine(count+" historical Block adoption assertions passed");
 }
}
