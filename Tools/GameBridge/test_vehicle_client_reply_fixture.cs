using System;
using System.Collections.Generic;
struct Vector3i {public int x;public static bool operator ==(Vector3i a,Vector3i b){return a.x==b.x;}public static bool operator !=(Vector3i a,Vector3i b){return a.x!=b.x;}public override bool Equals(object o){return o is Vector3i&&this==(Vector3i)o;}public override int GetHashCode(){return x;}}
enum RebirthVehicleAssemblyAction {Read,Install}
enum RebirthVehicleAssemblyCarrier {RepairableBlock,VehicleEntity}
enum RebirthVehicleRequestOutcome {Rejected,Applied,ReadOnly,Indeterminate}
class RebirthVehicleAssembly {public RebirthVehicleAssembly DeepClone(){return new RebirthVehicleAssembly();}}
class TestWorld {public bool Remote;public bool IsRemote(){return Remote;}}
class EntityPlayerLocal {public string Creation="11111111111111111111111111111111";public bool Complete=true;public TestWorld world=new TestWorld();}
class Origin {public string CreationId;}
class RebirthWorldCharacterRecord {public bool IsComplete;public Origin Origin;}
static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld(){return Enabled;}}
static class RebirthSurvivorClientState {public static string GetProjectedCreationId(EntityPlayerLocal p){return p.Creation;}}
static class RebirthWorldCharacterService {public static bool TryGet(EntityPlayerLocal p,out RebirthWorldCharacterRecord record){record=new RebirthWorldCharacterRecord{IsComplete=p.Complete,Origin=new Origin{CreationId=p.Creation}};return true;}}
// SCOPE
class PlayerUI {public EntityPlayerLocal entityPlayer=new EntityPlayerLocal();}
class XUi {public PlayerUI playerUI=new PlayerUI();public object PlayerInventory=new object();}
class ItemStack {public bool IsEmpty(){return false;}}
static class Localization {public static string Get(string s){return s;}}
class XUiC_RebirthVehicleRestoration {
 class PendingClientRequest {public XUiC_RebirthVehicleRestoration Controller;public RebirthVehicleAssemblyAction Action;public RebirthVehicleAssemblyCarrier Carrier;public Vector3i Position;public int EntityId;public long ExpectedRevision;public EntityPlayerLocal Owner;public object OwnerWorld;public string OwnerCreation;public bool HasConfirmedReply;public RebirthVehicleAssembly ConfirmedSnapshot;public string ConfirmedMessage;public RebirthVehicleRequestOutcome ConfirmedOutcome;public ItemStack ReservedCursorItem,PickedCursorItem;}
 static readonly object PendingClientSync=new object();static readonly Dictionary<Guid,PendingClientRequest> PendingClientRequests=new Dictionary<Guid,PendingClientRequest>();
 XUi xui=new XUi();
 RebirthVehicleAssemblyCarrier carrier;Vector3i position;int entityId;Guid lastCompletedRequestId,pendingRequestId;long pendingExpectedRevision;RebirthVehicleAssembly assembly;int Restores,Deliveries;string Status;
 void RestoreReservedCursorItem(ItemStack s){Restores++;}void DeliverConfirmedPickup(ItemStack s){Deliveries++;}void SetStatus(string s){Status=s;}void Refresh(){}

 // SOURCE
 static void CheckIdentity(){
 string normalized;var player=new EntityPlayerLocal();
 foreach(bool remote in new[]{false,true}){
 player.world.Remote=remote;
 foreach(string id in new[]{Guid.NewGuid().ToString("D"),"legacy-"+new string('a',64)}){
 player.Creation=id;if(!TryGetRequestCharacter(player,out normalized)||!RebirthSurvivorRequestScope.Matches(id,normalized))throw new Exception("Host/remote identity rejected");}
 foreach(string id in new[]{null,"","legacy-"+new string('A',64),Guid.Empty.ToString()}){
 player.Creation=id;if(TryGetRequestCharacter(player,out normalized))throw new Exception("Invalid identity accepted");}
 }
 player.world.Remote=false;player.Creation=Guid.NewGuid().ToString();player.Complete=false;
 if(TryGetRequestCharacter(player,out normalized))throw new Exception("Incomplete host character admitted");
 RebirthSurvivorMode.Enabled=false;
 if(!TryGetRequestCharacter(player,out normalized)||normalized!="")throw new Exception("Non-Survivor mode broken");
 player.world=null;if(TryGetRequestCharacter(player,out normalized))throw new Exception("Missing world admitted");
 RebirthSurvivorMode.Enabled=true;
 }
 static void Main(){CheckIdentity();foreach(var unknown in new[]{RebirthVehicleRequestOutcome.Indeterminate,(RebirthVehicleRequestOutcome)99,RebirthVehicleRequestOutcome.ReadOnly}){
 var c=new XUiC_RebirthVehicleRestoration();var id=Guid.NewGuid();c.pendingRequestId=id;var p=new PendingClientRequest{Controller=c,Owner=c.xui.playerUI.entityPlayer,OwnerWorld=c.xui.playerUI.entityPlayer.world,OwnerCreation=c.xui.playerUI.entityPlayer.Creation,Action=RebirthVehicleAssemblyAction.Install,ReservedCursorItem=new ItemStack()};PendingClientRequests.Add(id,p);
 Receive(id,c.carrier,c.position,0,null,"uncertain",unknown);
 if(!PendingClientRequests.ContainsKey(id)||c.pendingRequestId!=id||c.lastCompletedRequestId!=Guid.Empty||c.Restores!=0||c.Deliveries!=0||c.Status!="uncertain")throw new Exception("Uncertainty settled custody");
 Receive(id,c.carrier,c.position,0,null,"rejected",RebirthVehicleRequestOutcome.Rejected);
 if(PendingClientRequests.ContainsKey(id)||c.pendingRequestId!=Guid.Empty||c.Restores!=1)throw new Exception("Definitive rejection failed");
 Receive(id,c.carrier,c.position,0,null,"duplicate",RebirthVehicleRequestOutcome.Rejected);if(c.Restores!=1)throw new Exception("Repeated refund");
 }
 foreach(bool pickup in new[]{false,true}){
 var c=new XUiC_RebirthVehicleRestoration();var id=Guid.NewGuid();c.pendingRequestId=id;c.xui.PlayerInventory=null;
 PendingClientRequests.Add(id,new PendingClientRequest{Controller=c,Owner=c.xui.playerUI.entityPlayer,OwnerWorld=c.xui.playerUI.entityPlayer.world,OwnerCreation=c.xui.playerUI.entityPlayer.Creation,Action=RebirthVehicleAssemblyAction.Install,ReservedCursorItem=pickup?null:new ItemStack(),PickedCursorItem=pickup?new ItemStack():null});
 var result=pickup?RebirthVehicleRequestOutcome.Applied:RebirthVehicleRequestOutcome.Rejected;
 Receive(id,c.carrier,c.position,0,null,"delivery",result);
 if(!PendingClientRequests.ContainsKey(id)||c.pendingRequestId!=id||c.Restores!=0||c.Deliveries!=0)throw new Exception("Missing inventory lost custody");
 c.xui.PlayerInventory=new object();
 Receive(id,c.carrier,c.position,0,null,"conflict",pickup?RebirthVehicleRequestOutcome.Rejected:RebirthVehicleRequestOutcome.Applied);
 if(!PendingClientRequests.ContainsKey(id)||c.Restores+c.Deliveries!=0)throw new Exception("Conflicting reply replaced retained outcome");
  var original=c.xui.playerUI.entityPlayer;c.xui.PlayerInventory=new object();
 c.xui.playerUI.entityPlayer=new EntityPlayerLocal();c.RetryConfirmedReply();
 if(!PendingClientRequests.ContainsKey(id)||c.Restores+c.Deliveries!=0)throw new Exception("Replacement player received pending item");
 c.xui.playerUI.entityPlayer=original;var world=original.world;original.world=new TestWorld();c.RetryConfirmedReply();
 if(!PendingClientRequests.ContainsKey(id)||c.Restores+c.Deliveries!=0)throw new Exception("Replacement world settled item");
 original.world=world;original.Creation="22222222222222222222222222222222";c.RetryConfirmedReply();
 if(!PendingClientRequests.ContainsKey(id)||c.Restores+c.Deliveries!=0)throw new Exception("Replacement character settled item");
 original.Creation="11111111111111111111111111111111";c.RetryConfirmedReply();
 if(PendingClientRequests.ContainsKey(id)||c.Restores+c.Deliveries!=1)throw new Exception("Available inventory failed settlement");
 }
 var reader=new XUiC_RebirthVehicleRestoration();var readId=Guid.NewGuid();reader.pendingRequestId=readId;PendingClientRequests.Add(readId,new PendingClientRequest{Controller=reader,Owner=reader.xui.playerUI.entityPlayer,OwnerWorld=reader.xui.playerUI.entityPlayer.world,OwnerCreation=reader.xui.playerUI.entityPlayer.Creation,Action=RebirthVehicleAssemblyAction.Read});
 Receive(readId,reader.carrier,reader.position,0,null,"read",RebirthVehicleRequestOutcome.ReadOnly);if(PendingClientRequests.ContainsKey(readId)||reader.pendingRequestId!=Guid.Empty)throw new Exception("Real read did not settle");
 Console.WriteLine("PASS actual Receive: uncertain/unknown outcomes retain identity and custody, later rejection settles once; UI/inventory adapters doubled");}
}