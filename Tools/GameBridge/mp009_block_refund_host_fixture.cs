using System;using System.Linq;using System.Xml.Linq;
// Explicit adapters: immutable source/owed/plan validators, native saved owner
// read and journal storage. Actual Host branch ordering/qualification is used.
class World{internal EntityPlayer Actor;internal World worldState=>this;internal string Guid="world";internal bool IsRemote()=>false;internal EntityPlayer GetEntity(int id)=>Actor;}
class EntityPlayer{internal int Died=1,entityId=7;internal World world;internal bool IsDead()=>false;internal bool IsSpawned()=>true;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
class Identity{internal string CanonicalId="account",StorageKey="owner";}
static class RebirthStablePlayerIdentity{internal static bool TryResolveServerEntity(EntityPlayer a,out Identity i){i=new Identity();return a!=null;}}
static class RebirthOriginalActionScope{internal static bool Matches(EntityPlayer a,string owner,string creation)=>creation=="creation";}
static class RemoteResourceWorldIdentity{internal static bool TryGet(World w,out string key){key=w?.Guid;return w!=null;}}
class ItemStack{}
class RebirthCamperEnergyActorImage{internal string Frame;internal bool Matches(RebirthCamperEnergyActorImage other)=>Frame==other?.Frame;}
static class RebirthRemoteResourceClientOriginal{internal class Intent{internal string World="world",Owner="account",Creation="creation";internal int Deaths;internal ulong Epoch=1,Request=2;internal int Kind=3;}internal static RebirthCamperEnergyActorImage ReadActor(string s)=>new RebirthCamperEnergyActorImage{Frame=s};internal static string ActorImage(RebirthCamperEnergyActorImage image)=>image.Frame;}
static class RebirthRemoteResourceBlockAdoption{internal static bool Admission=true;internal static bool HasSavedAdmission(EntityPlayer a,RebirthRemoteResourceClientOriginal.Intent i,RebirthRemoteResourceDurableJournal.Record r)=>Admission;}
class SavedOwner{internal int id=7,deaths=1;}
static class RebirthCamperEnergySavedOwner{internal static SavedOwner Saved=new SavedOwner();internal static RebirthCamperEnergyActorImage Actual=new RebirthCamperEnergyActorImage();internal static bool TryRead(Identity i,out RebirthCamperEnergyActorImage actual,out SavedOwner saved){actual=Actual;saved=Saved;return true;}}
static class RebirthCookingRemoteAdmissionBackend{internal static float Phase=1;internal static bool NativePhase(SavedOwner saved,string receipt,out float phase,out bool present){phase=Phase;present=true;return true;}}
static class RebirthRemoteResourceOwnerAction{internal static string Receipt(Guid operation)=>operation.ToString("N");}
static class RebirthRemoteResourceDurableJournal
{
 internal sealed class Record{internal Guid Operation;internal string Binding="binding";internal XElement Image;internal int Phase=2;internal XElement Copy()=>new XElement(Image);}
 internal static Record Saved;internal static int Writes;
 internal static Guid Operation(string w,string owner,string creation,ulong epoch,ulong request,int kind)=>Guid.Empty;
 internal static bool TryRead(string owner,Guid source,out Record root,out bool unknown){root=Saved;unknown=false;return true;}
 internal static string Digest(string text)=>text;
 internal static bool TryAdvance(Record old,Action<XElement> edit,Func<bool> current,out Record result){result=null;if(!current())return false;var image=old.Copy();edit(image);Saved=result=new Record{Image=image,Phase=(int?)image.Attribute("phase")??old.Phase};Writes++;return true;}
}
internal static partial class RebirthRemoteResourceBlockRefund
{
 internal static bool ValidOwed=true,PolicyCurrent=true;
 private static Guid ChildFor(RebirthRemoteResourceClientOriginal.Intent i,int deaths)=>Guid.Empty;
 internal static bool Owed(RebirthRemoteResourceClientOriginal.Intent i,RebirthRemoteResourceDurableJournal.Record root,out ItemStack[] payload,out string noUse){payload=new[]{new ItemStack()};noUse="no-use";return ValidOwed;}
 internal static bool Plan(RebirthCamperEnergyActorImage before,ItemStack[] payload,out RebirthCamperEnergyActorImage after){after=new RebirthCamperEnergyActorImage{Frame="after"};return before.Frame=="before";}
 internal static bool Plan(RebirthCamperEnergyActorImage before,ItemStack[] payload,XElement policy,out RebirthCamperEnergyActorImage after)=>Plan(before,payload,out after);
 private static bool SavedBeltPolicy(EntityPlayer actor,SavedOwner saved,XElement policy)=>policy==null||PolicyCurrent;
 internal static XElement Body(RebirthRemoteResourceClientOriginal.Intent i,Guid source,string binding,string noUse,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,int deaths,XElement policy=null){var body=new XElement("blockRefund",new XAttribute("deaths",deaths),new XElement("beforeActor",before.Frame),new XElement("afterActor",after.Frame));if(policy!=null)body.Add(new XElement(policy));return body;}
 internal static string TerminalProof(string binding,string noUse,XElement request)=>"proof:"+request;
 // ACTUAL_HOST
}
static class Program
{
 static EntityPlayer actor;static RebirthRemoteResourceClientOriginal.Intent original;static XElement request;static int count;
 static void Reset(int recipient=1)
 {
  var world=new World();actor=new EntityPlayer{world=world};world.Actor=actor;GameManager.Instance.World=world;original=new RebirthRemoteResourceClientOriginal.Intent();
  request=RebirthRemoteResourceBlockRefund.Body(original,Guid.Empty,"binding","no-use",new RebirthCamperEnergyActorImage{Frame="before"},new RebirthCamperEnergyActorImage{Frame="after"},recipient);
  RebirthRemoteResourceDurableJournal.Saved=new RebirthRemoteResourceDurableJournal.Record{Image=new XElement("root")};RebirthRemoteResourceDurableJournal.Writes=0;
  RebirthRemoteResourceBlockAdoption.Admission=true;RebirthRemoteResourceBlockRefund.ValidOwed=true;RebirthRemoteResourceBlockRefund.PolicyCurrent=true;RebirthCamperEnergySavedOwner.Saved=new SavedOwner();RebirthCamperEnergySavedOwner.Actual.Frame="before";RebirthCookingRemoteAdmissionBackend.Phase=1;
 }
 static void Retain()=>RebirthRemoteResourceDurableJournal.Saved.Image.Add(new XElement("ownerRefund",new XElement(request)));
 static bool Host(bool complete,out bool permit)=>RebirthRemoteResourceBlockRefund.Host(actor,original,request,complete,out permit);
 static void Check(bool value){if(!value)throw new Exception("Actual BlockRefund.Host assertion "+(count+1));count++;}
 static void Main()
 {
  Reset();Check(Host(false,out var permit)&&permit&&RebirthRemoteResourceDurableJournal.Writes==1);
  Check(Host(false,out permit)&&permit&&RebirthRemoteResourceDurableJournal.Writes==1);
  RebirthCookingRemoteAdmissionBackend.Phase=2;RebirthCamperEnergySavedOwner.Actual.Frame="after";Check(Host(true,out permit)&&!permit&&RebirthRemoteResourceDurableJournal.Saved.Phase==4);
  Reset();Check(!Host(true,out permit)&&RebirthRemoteResourceDurableJournal.Writes==0);
  Reset();RebirthRemoteResourceBlockAdoption.Admission=false;Check(!Host(false,out permit));
  Reset();RebirthCamperEnergySavedOwner.Actual.Frame="changed";Check(!Host(false,out permit));
  Reset(0);Check(!Host(false,out permit));
  Reset(2);Check(!Host(false,out permit));
  Reset();Retain();actor.Died=2;RebirthCamperEnergySavedOwner.Saved.deaths=2;RebirthCookingRemoteAdmissionBackend.Phase=2;RebirthCamperEnergySavedOwner.Actual.Frame="current-life-unrelated";Check(Host(true,out permit)&&RebirthRemoteResourceDurableJournal.Saved.Phase==4);
  Reset();Retain();actor.Died=2;RebirthCamperEnergySavedOwner.Saved.deaths=2;Check(!Host(false,out permit)&&!permit&&RebirthRemoteResourceDurableJournal.Writes==0);
  Reset();Retain();actor.Died=2;RebirthCamperEnergySavedOwner.Saved.deaths=2;Check(!Host(true,out permit));
  Reset();Retain();request.Element("afterActor").Value="substituted";Check(!Host(true,out permit));
  Reset();RebirthCamperEnergySavedOwner.Saved.id=8;Check(!Host(false,out permit));
  Reset();RebirthRemoteResourceBlockRefund.ValidOwed=false;Check(!Host(false,out permit));
  Reset();request.Add(new XElement("beltPolicy"));RebirthRemoteResourceBlockRefund.PolicyCurrent=false;Check(!Host(false,out permit)&&RebirthRemoteResourceDurableJournal.Writes==0);
  Reset();request.Add(new XElement("beltPolicy"));Check(Host(false,out permit)&&permit);
  Console.WriteLine(count+" actual BlockRefund.Host assertions passed");
 }
}
