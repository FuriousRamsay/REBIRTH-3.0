// AUTHORED_NOT_RUN. Actual CraftAdoption and RepairAdoption classes plus refund Host.
// Native saved file/actor codec/source admission/queue proof/journal are explicit adapters.
using System;using System.Linq;using System.Xml.Linq;
enum RemoteResourceClientOperation{Craft=1,ItemRepair=2,BlockRepair=3,BlockUpgrade=4}
// Block-original adoption has its independent native/action cohort. This
// personal craft/repair fixture cannot issue a block query capability.
static class RebirthRemoteResourceBlockAdoption{internal static bool QueryScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)=>false;}
class World{internal bool Remote;internal EntityPlayerLocal Player;internal State worldState=new State();internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal object GetEntity(int id)=>Player;}
class State{internal string Guid="native-world";}
class EntityPlayer{internal World world;internal int entityId=1,Died=2;internal bool Dead;internal bool IsDead()=>Dead;}
class EntityPlayerLocal:EntityPlayer{internal bool Spawned=true;internal bool IsSpawned()=>Spawned;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
class RebirthStablePlayerIdentity{internal string StorageKey="owner-key",CanonicalId="owner";internal static bool TryResolveServerEntity(EntityPlayer player,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return player!=null;}}
static class RebirthOriginalActionScope{internal static bool Valid=true;internal static bool Matches(EntityPlayer actor,string owner,string creation)=>Valid&&owner=="owner-key"&&creation=="creation";}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer player)=>"creation";}
static class RemoteResourceWorldIdentity{internal static bool TryGet(World world,out string id){id="source-world";return world!=null;}}
class RebirthRemoteResourceNetworkScope{internal static bool Live=true;internal bool Current=>Live;internal static RebirthRemoteResourceNetworkScope Capture(EntityPlayer actor)=>new RebirthRemoteResourceNetworkScope();}
class ItemStack{internal int count;}
static class RebirthCamperInventorySnapshot{internal static ItemStack[] Restore(byte[] bytes)=>bytes.Length==0?new ItemStack[0]:new[]{new ItemStack{count=bytes[0]}};}
class RebirthCamperEnergyActorImage{internal int Count;internal bool Matches(RebirthCamperEnergyActorImage other)=>other!=null&&Count==other.Count;}
class PlayerDataFile{internal int id=1,deaths=2;}
static class RebirthCamperEnergySavedOwner{internal static bool Readable=true;internal static int Count=2;internal static PlayerDataFile File=new PlayerDataFile();internal static bool TryRead(RebirthStablePlayerIdentity identity,out RebirthCamperEnergyActorImage actor,out PlayerDataFile file){actor=new RebirthCamperEnergyActorImage{Count=Count};file=File;return Readable;}}
static class RebirthCookingRemoteAdmissionBackend{internal static bool Present=true;internal static float Phase=2;internal static bool NativePhase(PlayerDataFile file,string receipt,out float phase,out bool present){phase=Phase;present=Present;return true;}}
static class RebirthRemoteResourceClientOriginal
{
 internal sealed class Intent{internal RemoteResourceClientOperation Kind=RemoteResourceClientOperation.Craft;internal Guid Operation=Guid.NewGuid();internal string World="native-world",Owner="owner",Creation="creation",Digest="original-digest";internal int Deaths=1;internal ulong Epoch=1,Request=5;internal XElement Copy()=>new XElement("original");}
 internal static bool Current(EntityPlayerLocal player,Intent original)=>player!=null&&original!=null&&player.Died==original.Deaths;
 internal static bool HasNeverPreparedRepairAction(Intent original)=>true;internal static RebirthCamperEnergyActorImage ReadActor(string text)=>new RebirthCamperEnergyActorImage{Count=int.Parse(text)};internal static string ActorImage(RebirthCamperEnergyActorImage actor)=>actor.Count.ToString();
}
static class RebirthRemoteResourceCraftOriginal{internal static bool Input=true;internal static bool ValidateInput(RebirthRemoteResourceClientOriginal.Intent original,out object recipe,out int count,out int quality,out int belt,out bool rich){recipe=null;count=quality=belt=0;rich=false;return Input;}}
static class RebirthRemoteResourceCraftProtocol{internal static bool Admitted=true;internal static bool HasSavedPersonalAdmission(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)=>Admitted;}
static class RebirthRemoteResourceCraftSettlement
{
 internal static bool Matches=true;internal static bool CanonicalAction(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,out XElement action){action=new XElement("action",new XElement("afterActor","0"));return Matches;}
 internal static bool MatchesRootAction(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)=>Matches;
 internal static string UsedProof(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,XElement action)=>"used-proof";
}
static class RebirthRemoteResourceFetchedStationOriginal{internal static bool IsCandidate(RebirthRemoteResourceClientOriginal.Intent original)=>false;internal static bool ValidateFetchedNoUseRoot(XElement root)=>false;}
static class RebirthRemoteResourceOwnerAction{internal sealed class Payment{internal Guid ClientOriginal;}internal static string Receipt(Guid op)=>"receipt-"+op;internal static bool TryDecodeRepairPaymentIntent(string text,out Payment payment){payment=null;return false;}}
static class RebirthRemoteResourceDurableOutcome{internal static bool TryReconcilePersonalCraftSources(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,out RebirthRemoteResourceDurableJournal.Record next){next=null;return false;}}
static class RebirthRemoteResourceDurableJournal
{
 internal sealed class Record{internal string Owner="owner-key",Creation="creation",World="source-world",Binding="binding";internal Guid Operation=Guid.NewGuid();internal long Revision;internal XElement Root;internal int Phase=>(int)Root.Attribute("phase");internal bool HasOwnerAction=>Root.Element("ownerAction")!=null;internal XElement Copy()=>new XElement(Root);}
 internal static Record Saved;internal static bool Never=true,Fail;internal static int Advances;
 internal static Guid Operation(string world,string owner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation kind)=>Saved.Operation;
 internal static string Digest(string text)=>"digest-"+text;internal static bool NeverPreparedOwnerAction(Record root)=>Never&&!root.HasOwnerAction;
 internal static bool TryRead(string owner,Guid operation,out Record record,out bool unknown){record=Saved;unknown=false;return true;}
 internal static bool TryAdvance(Record original,Action<XElement> write,Func<bool> scope,out Record next){next=null;Advances++;if(Fail||!scope())return false;write(original.Root);original.Revision++;next=original;return true;}
}
static class RebirthRemoteResourceRepairRefund
{
 static bool Hex(string value)=>!string.IsNullOrEmpty(value);static Guid ChildFor(RebirthRemoteResourceClientOriginal.Intent original,int deaths)=>original.Operation;
 static bool Plan(RebirthCamperEnergyActorImage before,ItemStack[] paid,out RebirthCamperEnergyActorImage after){after=new RebirthCamperEnergyActorImage{Count=before.Count+paid.Sum(item=>item.count)};return paid.Length>0;}
 // ACTUAL_REFUND_HOST
}
// ACTUAL_REPAIR_ADOPTION
// ACTUAL_CRAFT_ADOPTION
class Program
{
 static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
 static EntityPlayerLocal Setup(int phase,bool issued=false)
 {
  var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;RebirthOriginalActionScope.Valid=RebirthRemoteResourceNetworkScope.Live=RebirthRemoteResourceCraftOriginal.Input=RebirthRemoteResourceCraftProtocol.Admitted=RebirthRemoteResourceDurableJournal.Never=RebirthRemoteResourceCraftSettlement.Matches=RebirthCamperEnergySavedOwner.Readable=RebirthCookingRemoteAdmissionBackend.Present=true;RebirthRemoteResourceDurableJournal.Fail=false;RebirthRemoteResourceDurableJournal.Advances=0;RebirthCookingRemoteAdmissionBackend.Phase=2;RebirthCamperEnergySavedOwner.Count=2;RebirthCamperEnergySavedOwner.File=new PlayerDataFile();
  var sources=new XElement("sources",new XElement("source",new XAttribute("attempted",1),new XAttribute("published",1)));if(phase==3)sources.Add(new XElement("source",new XAttribute("attempted",0),new XAttribute("published",0)));
  RebirthRemoteResourceDurableJournal.Saved=new RebirthRemoteResourceDurableJournal.Record{Root=new XElement("root",new XAttribute("phase",phase),new XAttribute("outcome",phase==3?2:1),new XAttribute("kind",1),new XAttribute("deaths",1),sources,new XElement("removed",Convert.ToBase64String(new byte[]{3})))};if(issued)RebirthRemoteResourceDurableJournal.Saved.Root.Add(new XElement("ownerAction",new XAttribute("domain","native-craft")));
  return player;
 }
 static void Main()
 {
  foreach(int phase in new[]{2,3})
  {
   var player=Setup(phase);var original=new RebirthRemoteResourceClientOriginal.Intent();var source=RebirthRemoteResourceDurableJournal.Saved;string paid=source.Root.Element("removed").Value,stages=source.Root.Element("sources").ToString();int outcome=(int)source.Root.Attribute("outcome");
   Need(RebirthRemoteResourceCraftAdoption.QueryScope(player,original),"historical paid original query scope");
   Need(RebirthRemoteResourceCraftAdoption.Read(player,original,out var owed)&&owed.Phase==phase,"historical paid/full or partial original has positive no-use disposition");string noUse=(string)owed.Root.Attribute("ownerNoUseProof");Need(!string.IsNullOrEmpty(noUse)&&owed.Root.Element("ownerAction")==null,"never-issued original did not mint an action");
   var refund=new XElement("repairRefund",new XAttribute("original",original.Digest),new XAttribute("source",source.Operation.ToString("N")),new XAttribute("binding",source.Binding),new XAttribute("noUseProof",noUse),new XAttribute("child",original.Operation.ToString("N")),new XAttribute("deaths",2),new XElement("beforeActor","2"),new XElement("afterActor","5"));RebirthCookingRemoteAdmissionBackend.Phase=1;
   Need(RebirthRemoteResourceRepairRefund.Host(player,original,refund,false,out bool permission)&&permission&&source.Phase==phase,"saved current-life Before grants only exact owed refund");Need(!RebirthRemoteResourceRepairRefund.Host(player,original,refund,true,out _),"Before receipt cannot acknowledge owner credit");
   RebirthCookingRemoteAdmissionBackend.Phase=2;RebirthCamperEnergySavedOwner.Count=5;Need(RebirthRemoteResourceRepairRefund.Host(player,original,refund,true,out _)&&source.Phase==4&&(string)source.Root.Attribute("disposition")=="refunded","saved current-life After retires exact owed source");Need(source.Root.Element("removed").Value==paid&&source.Root.Element("sources").ToString()==stages&&(int)source.Root.Attribute("outcome")==outcome,"refund changed immutable source payments");
  }
  for(int variant=0;variant<10;variant++){var player=Setup(2,true);var original=new RebirthRemoteResourceClientOriginal.Intent();var source=RebirthRemoteResourceDurableJournal.Saved;switch(variant){case 1:RebirthCookingRemoteAdmissionBackend.Present=false;break;case 2:RebirthCookingRemoteAdmissionBackend.Phase=1;break;case 3:RebirthCamperEnergySavedOwner.File.deaths=1;break;case 4:RebirthRemoteResourceCraftSettlement.Matches=false;break;case 5:RebirthRemoteResourceCraftProtocol.Admitted=false;break;case 6:RebirthRemoteResourceNetworkScope.Live=false;break;case 7:RebirthRemoteResourceDurableJournal.Fail=true;break;case 8:player.Died=1;break;case 9:original.Creation="other";break;}bool accepted=RebirthRemoteResourceCraftAdoption.Read(player,original,out var used);Need(accepted==(variant==0),"saved historical accepted queue qualification "+variant);if(accepted)Need(used.Phase==4&&(string)used.Root.Attribute("disposition")=="used"&&used.Root.Element("ownerRefund")==null,"positive original usage must not refund or replay queue");else Need(source.Phase==2,"unknown old effect changed source terminal");}
  var actor=Setup(2);var intent=new RebirthRemoteResourceClientOriginal.Intent();actor.world.Remote=true;Need(RebirthRemoteResourceCraftAdoption.QueryScope(actor,intent),"genuine remote metadata scope supported");actor.world.Player=new EntityPlayerLocal();Need(!RebirthRemoteResourceCraftAdoption.QueryScope(actor,intent),"replacement native primary cannot query prior original");
  Console.WriteLine("MP042 historical personal Craft: actual no-use/adoption/refund Host and saved terminal source authored; native and persistence services are disclosed adapters.");
 }
}
