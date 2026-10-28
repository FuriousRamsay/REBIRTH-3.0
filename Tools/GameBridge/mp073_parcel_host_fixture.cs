using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;
// AUTHORED_NOT_RUN. Actual complete host source below; explicit adapters for
// native save I/O, source journal durability, detached plan and native overflow.
class WorldState{internal string Guid="world";}class World{internal WorldState worldState=new WorldState();internal EntityPlayer Actor;internal bool Remote;internal bool IsRemote()=>Remote;internal EntityPlayer GetEntity(int id)=>Actor?.entityId==id?Actor:null;}
class EntityPlayer{internal World world;internal int entityId=7,Died;internal bool Dead;internal bool IsDead()=>Dead;internal bool IsSpawned()=>true;}class EntityPlayerLocal:EntityPlayer{}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool IsMainThread()=>true;}static class GameIO{internal static string GetSaveGameDir()=>"save";}
class RebirthStablePlayerIdentity{internal string StorageKey="owner",CanonicalId="canonical";internal static bool TryResolveServerEntity(EntityPlayer actor,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return actor!=null;}}
static class RebirthOriginalActionScope{internal static bool Matches(EntityPlayer actor,string owner,string creation)=>owner=="owner"&&creation=="creation";}
class RebirthCamperEnergyActorImage{internal string Value;internal bool Matches(RebirthCamperEnergyActorImage other)=>other?.Value==Value;}
class PlayerDataFile{internal int id=7,deaths;internal float Phase=1;}
static class RebirthCamperEnergySavedOwner{internal static PlayerDataFile File=new PlayerDataFile();internal static RebirthCamperEnergyActorImage Frame=new RebirthCamperEnergyActorImage{Value="before"};internal static bool TryRead(RebirthStablePlayerIdentity identity,out RebirthCamperEnergyActorImage frame,out PlayerDataFile file){frame=Frame;file=File;return true;}}
static class RebirthCookingRemoteAdmissionBackend{internal static bool NativePhase(PlayerDataFile file,string key,out float value,out bool present){value=file.Phase;present=true;return true;}}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int maximum)=>XElement.Parse(text);}
class Lease:IDisposable{public void Dispose(){}}static class RebirthProductiveEarnedObservationBudget{internal static bool TryRetainDetached(long bytes,out IDisposable lease){lease=new Lease();return true;}}
static class RebirthStationPublicationRecord{internal static string Digest(byte[] bytes)=>new string((char)bytes[0],64);}
class RebirthCamperTransferJournal
{
 internal enum Phase{Prepared,Uncertain,Applied}
 internal class Record{internal Guid Operation,Vehicle;internal string OwnerCreation;internal byte[] Before,After;internal Phase State;}
 internal static Dictionary<string,Dictionary<Guid,Record>> Stores=new Dictionary<string,Dictionary<Guid,Record>>();string name;internal RebirthCamperTransferJournal(string path){name=path;if(!Stores.ContainsKey(name))Stores[name]=new Dictionary<Guid,Record>();}
 internal Record Read(Guid operation)=>Stores[name].TryGetValue(operation,out var record)?record:null;
 internal Record Prepare(Guid operation,Guid vehicle,string owner,int revision,byte[] before,byte[] after){var old=Read(operation);if(old!=null)return old;var record=new Record{Operation=operation,Vehicle=vehicle,OwnerCreation=owner,Before=before,After=after};Stores[name][operation]=record;return record;}
 internal Record Advance(Guid operation,Phase before,Phase after){var record=Read(operation);if(record.State!=before)throw new Exception();record.State=after;return record;}
 internal IEnumerable<Record> FindApplied(Guid vehicle,string owner)=>Stores[name].Values.Where(r=>r.Vehicle==vehicle&&r.OwnerCreation==owner&&r.State==Phase.Applied);
}
class RebirthCamperRemovalParcelPlan
{
 internal static RebirthCamperRemovalParcelPlan Current;internal static int Creates;
 internal Guid Operation,Removal,Vehicle,Installation;internal string Source,Owner="owner",Creation="creation",World="world",Binding="binding",Receipt="receipt";internal int Deaths,Entity=7;
 internal RebirthCamperEnergyActorImage Credited=new RebirthCamperEnergyActorImage{Value="credited"},Retired=new RebirthCamperEnergyActorImage{Value="retired"};
 internal static Guid Key(string world,string source,string owner,string creation)=>Current.Operation;
 internal XElement Write()=>new XElement("plan",new XAttribute("operation",Operation.ToString("N")));
 internal static bool TryRead(XElement node,out RebirthCamperRemovalParcelPlan plan){plan=Current;return XNode.DeepEquals(node,plan.Write());}
 internal static bool TryCreate(EntityPlayer actor,RebirthStablePlayerIdentity identity,string creation,RebirthCamperTransferJournal.Record removal,RebirthCamperEnergyActorImage saved,out RebirthCamperRemovalParcelPlan plan){Creates++;plan=Current;return removal.Operation==plan.Removal&&saved.Value=="before";}
 internal RebirthCamperTransferJournal.Record Retain(){var bytes=Encoding.UTF8.GetBytes(Write().ToString(SaveOptions.DisableFormatting));return new RebirthCamperTransferJournal(Path.Combine("save","REBIRTH","RemovalParcelOriginals")).Prepare(Operation,Vehicle,Creation,0,bytes,bytes);}
}
static class RebirthCamperRemovalParcelOverflow{internal static bool Ready,RetainReady=true;internal static int Calls;internal static bool RetainUnattempted(RebirthCamperRemovalParcelPlan plan){var root=new RebirthCamperTransferJournal(Path.Combine("save","REBIRTH","RemovalParcelOriginals")).Read(plan.Operation);if(root?.State!=RebirthCamperTransferJournal.Phase.Prepared)throw new Exception("Child retention must precede root uncertainty");return RetainReady;}internal static bool Advance(EntityPlayer actor,RebirthCamperRemovalParcelPlan plan,Func<bool> current,out string proof){Calls++;proof=Ready?"saved-overflow":null;return current()&&Ready;}internal static bool Terminal(RebirthCamperRemovalParcelPlan plan,out string proof){proof="saved-overflow";return Ready;}}
// ACTUAL_HOST
static class Program
{
 static int count;static EntityPlayer actor;static RebirthCamperRemovalParcelPlan plan;static RebirthCamperTransferJournal sources;
 static void Check(bool value){count++;if(!value)throw new Exception("Actual parcel host assertion "+count);}
 static XElement Request(int stage=0)=>new XElement("removalParcelRequest",new XAttribute("version",1),new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("source",plan.Source),new XAttribute("vehicle",plan.Vehicle.ToString("N")),new XAttribute("installation",plan.Installation.ToString("N")),new XAttribute("creation","creation"),new XAttribute("nonce",Guid.NewGuid().ToString("N")),new XAttribute("stage",stage));
 static RebirthCamperTransferJournal.Record Root()=>new RebirthCamperTransferJournal(Path.Combine("save","REBIRTH","RemovalParcelOriginals")).Read(plan.Operation);
 static void Main()
 {
  actor=new EntityPlayer();actor.world=new World{Actor=actor};GameManager.Instance.World=actor.world;
  plan=RebirthCamperRemovalParcelPlan.Current=new RebirthCamperRemovalParcelPlan{Operation=Guid.NewGuid(),Removal=Guid.NewGuid(),Vehicle=Guid.NewGuid(),Installation=Guid.NewGuid(),Source=new string('a',64)+new string('b',64)};
  sources=new RebirthCamperTransferJournal(Path.Combine("save","REBIRTH","CamperTransfers"));var source=sources.Prepare(plan.Removal,plan.Vehicle,"remote:canonical:creation",0,new[]{(byte)'a'},new[]{(byte)'b'});
  Check(RebirthCamperRemovalParcelHost.Request(actor,Request())==null&&Root()==null);source.State=RebirthCamperTransferJournal.Phase.Applied;
  var malformed=Request();malformed.SetAttributeValue("extra",1);Check(RebirthCamperRemovalParcelHost.Request(actor,malformed)==null&&Root()==null);
  RebirthCamperEnergySavedOwner.File.Phase=0;Check(RebirthCamperRemovalParcelHost.Request(actor,Request())==null&&Root()==null);RebirthCamperEnergySavedOwner.File.Phase=1;
  RebirthCamperEnergySavedOwner.File.deaths=1;Check(RebirthCamperRemovalParcelHost.Request(actor,Request())==null&&Root()==null);RebirthCamperEnergySavedOwner.File.deaths=0;
  var result=RebirthCamperRemovalParcelHost.Request(actor,Request());Check((int)result.Attribute("status")==0&&Root().State==RebirthCamperTransferJournal.Phase.Prepared&&RebirthCamperRemovalParcelPlan.Creates==1);
  result=RebirthCamperRemovalParcelHost.Request(actor,Request());Check(result!=null&&RebirthCamperRemovalParcelPlan.Creates==1);
  Check(RebirthCamperRemovalParcelHost.Request(actor,Request(1))==null&&RebirthCamperRemovalParcelOverflow.Calls==0);
  RebirthCamperEnergySavedOwner.File.Phase=500;Check(RebirthCamperRemovalParcelHost.Request(actor,Request(1))==null&&Root().State==RebirthCamperTransferJournal.Phase.Prepared);
  actor.entityId=8;Check(RebirthCamperRemovalParcelHost.Request(actor,Request(1))==null&&Root().State==RebirthCamperTransferJournal.Phase.Prepared);RebirthCamperEnergySavedOwner.File.id=8;
  RebirthCamperEnergySavedOwner.Frame.Value="credited";RebirthCamperRemovalParcelOverflow.RetainReady=false;Check(RebirthCamperRemovalParcelHost.Request(actor,Request(1))==null&&Root().State==RebirthCamperTransferJournal.Phase.Prepared&&RebirthCamperRemovalParcelOverflow.Calls==0);RebirthCamperRemovalParcelOverflow.RetainReady=true;result=RebirthCamperRemovalParcelHost.Request(actor,Request(1));Check((int)result.Attribute("status")==0&&Root().State==RebirthCamperTransferJournal.Phase.Uncertain);
  Check(plan.Entity==7&&actor.entityId==8);
  RebirthCamperRemovalParcelOverflow.Ready=true;result=RebirthCamperRemovalParcelHost.Request(actor,Request(1));Check((int)result.Attribute("status")==1&&Root().State==RebirthCamperTransferJournal.Phase.Uncertain);
  Check(RebirthCamperRemovalParcelHost.Request(actor,Request(2))==null&&Root().State==RebirthCamperTransferJournal.Phase.Uncertain);
  RebirthCamperEnergySavedOwner.Frame.Value="retired";RebirthCamperRemovalParcelOverflow.Ready=false;int beforeRecoveryCalls=RebirthCamperRemovalParcelOverflow.Calls;
  Check(RebirthCamperRemovalParcelHost.Request(actor,Request(1))==null&&RebirthCamperRemovalParcelOverflow.Calls==beforeRecoveryCalls);
  RebirthCamperRemovalParcelOverflow.Ready=true;result=RebirthCamperRemovalParcelHost.Request(actor,Request(1));
  Check(result!=null&&(int)result.Attribute("status")==1&&Root().State==RebirthCamperTransferJournal.Phase.Uncertain);
  Check(RebirthCamperRemovalParcelHost.Request(actor,Request(2))==null&&Root().State==RebirthCamperTransferJournal.Phase.Uncertain);
  RebirthCamperEnergySavedOwner.Frame.Value="credited";
  RebirthCamperEnergySavedOwner.File.Phase=1000;Check(RebirthCamperRemovalParcelHost.Request(actor,Request(2))==null);
  RebirthCamperEnergySavedOwner.Frame.Value="retired";result=RebirthCamperRemovalParcelHost.Request(actor,Request(2));Check((int)result.Attribute("status")==2&&Root().State==RebirthCamperTransferJournal.Phase.Applied);
  RebirthCamperRemovalParcelOverflow.Ready=false;Check(RebirthCamperRemovalParcelHost.Request(actor,Request(2))==null);RebirthCamperRemovalParcelOverflow.Ready=true;
  source.After=new[]{(byte)'c'};Check(RebirthCamperRemovalParcelHost.Request(actor,Request(2))==null);source.After=new[]{(byte)'b'};
  var duplicate=sources.Prepare(Guid.NewGuid(),plan.Vehicle,"remote:canonical:creation",0,new[]{(byte)'a'},new[]{(byte)'b'});duplicate.State=RebirthCamperTransferJournal.Phase.Applied;
  // Existing original remains bound to its exact removal; new ambiguous initial source cannot admit.
  result=RebirthCamperRemovalParcelHost.Request(actor,Request(2));Check(result!=null&&(int)result.Attribute("status")==2);
  actor.world.Remote=true;Check(RebirthCamperRemovalParcelHost.Request(actor,Request())==null);
  Console.WriteLine(count+" actual parcel host assertions passed");
 }
}
