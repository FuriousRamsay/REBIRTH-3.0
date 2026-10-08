using System;
using System.Collections.Generic;
class World {public EntityPlayerLocal Player;public bool Remote=true;public bool IsRemote(){return Remote;}public EntityPlayerLocal GetPrimaryPlayer(){return Player;}}
class Buffs {Dictionary<string,float> values=new Dictionary<string,float>();public float GetCustomVar(string k){float v;return values.TryGetValue(k,out v)?v:0;}public void SetCustomVar(string k,float v,bool b){values[k]=v;}}
class EntityPlayerLocal {public Inventory bag=LocalPlayerUI.UI.xui.PlayerInventory;public World world;public int entityId=7;public Buffs Buffs=new Buffs();public bool IsSpawned(){return true;}public bool IsDead(){return false;}}
class GameManager {public static GameManager Instance=new GameManager();public World World;public static void ShowTooltip(params object[] args){}}
class Time {public static float realtimeSinceStartup;}
class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld(){return true;}}
class RebirthStartingItemDefinition {public string ItemId="food";public int Count=3,Quality=1;public bool HasQuality;}
class RebirthBackgroundDefinition {public List<RebirthStartingItemDefinition> StartingItems=new List<RebirthStartingItemDefinition>{new RebirthStartingItemDefinition()};}
class RebirthSurvivorDefinitionRegistry {public static string SemanticHash="hash";public static bool TryGetBackground(string id,out RebirthBackgroundDefinition b){b=new RebirthBackgroundDefinition();return id=="chef";}}
class ItemClass {public int MaxCount=50;public static ItemValue GetItem(string id){return new ItemValue();}public static ItemValue CreateItemValue(string id,int quality){return new ItemValue();}}
class ItemValue {public int type=1;public ItemClass ItemClass=new ItemClass();}
class ItemStack {public int count;public ItemStack(ItemValue v,int c){count=c;}}
class RebirthBackpackLibraryReservation {public static bool IsHeld(EntityPlayerLocal p){return false;}}
class Inventory {public bool ThrowAfter;public bool CanStack(ItemStack s){return Capacity>=s.count;}public bool AddItem(ItemStack s){if(Capacity<s.count)return false;Capacity-=s.count;Granted+=s.count;if(ThrowAfter)throw new Exception("listener");return true;}public int Capacity,Granted;public void AddItemNoPartial(ItemStack s,bool b){int n=Math.Min(s.count,Capacity);Capacity-=n;Granted+=n;s.count-=n;}}
class Xui {public Inventory PlayerInventory=new Inventory();}
class LocalPlayerUI {public Xui xui=new Xui();public static LocalPlayerUI UI=new LocalPlayerUI();public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal p){return UI;}}
class Origin {public string CreationId,BackgroundId;}
class RebirthWorldCharacterRecord {public bool IsComplete;public Origin Origin;}
class RebirthWorldCharacterService {public static RebirthWorldCharacterRecord Record;public static bool TryGet(EntityPlayerLocal p,out RebirthWorldCharacterRecord r){r=Record;return r!=null;}}
class OwnerSnapshot {public bool RebirthModeEnabled=true,HasCharacter=true;public string CreationId,BackgroundId="chef",ServerDefinitionHash="hash";}
class RebirthSurvivorClientState {public static OwnerSnapshot Snapshot;public static OwnerSnapshot GetOwnerStateSnapshot(){return Snapshot;}public static string Creation;public static string GetProjectedCreationId(EntityPlayerLocal p){return Creation;}}
// ACTUAL_SCOPE
class Localization {public static string Get(string s){return s;}}
class Check {
 static World pendingWorld;static int pendingPlayerId;static string creationId,backgroundId,definitionHash;static float nextAttempt;static bool spaceNoticeShown;
// METHODS
 static void Assert(bool b,string message){if(!b)throw new Exception(message);}
 static void TickNext(){Time.realtimeSinceStartup+=3;Tick();}
 static void Main(){int receiptCount;Assert(!TryReadDelivered(float.NaN,3,out receiptCount)&&!TryReadDelivered(float.PositiveInfinity,3,out receiptCount)&&!TryReadDelivered(-1,3,out receiptCount)&&!TryReadDelivered(1.5f,3,out receiptCount),"malformed receipt refused");Assert(TryReadDelivered(float.MaxValue,3,out receiptCount)&&receiptCount==3,"large paid receipt bounded without int overflow");Assert(TryReadDelivered(2,3,out receiptCount)&&receiptCount==2,"valid receipt preserved");
 var w=new World();var p=new EntityPlayerLocal{world=w};w.Player=p;GameManager.Instance.World=w;
 string id=Guid.NewGuid().ToString("N");var inv=LocalPlayerUI.UI.xui.PlayerInventory;
 inv.Capacity=10;Receive(w,7,id,"chef","hash");TickNext();Assert(inv.Granted==0,"early offer");
 RebirthSurvivorClientState.Creation=id;RebirthSurvivorClientState.Snapshot=new OwnerSnapshot{CreationId=id};inv.Capacity=0;TickNext();Assert(inv.Granted==0&&pendingWorld!=null,"full bag retained");
 inv.Capacity=1;TickNext();Assert(inv.Granted==1,"partial moved");TickNext();Assert(inv.Granted==1,"full repeat");
 inv.Capacity=2;TickNext();Assert(inv.Granted==3&&pendingWorld==null,"remaining delivered");
 Receive(w,7,id,"chef","hash");inv.Capacity=10;TickNext();Assert(inv.Granted==3,"duplicate offer receipt");
 string other=Guid.NewGuid().ToString("N");Receive(w,7,other,"chef","hash");TickNext();Assert(inv.Granted==3,"stale identity");
 RebirthSurvivorClientState.Creation=other;RebirthSurvivorClientState.Snapshot=new OwnerSnapshot{CreationId=other};TickNext();Assert(inv.Granted==6,"new identity after projection");
 Receive(w,7,Guid.Empty.ToString(),"chef","hash");Assert(pendingWorld==null,"empty GUID");
 var hostId=Guid.NewGuid().ToString("N");w.Remote=false;
 RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord{IsComplete=true,Origin=new Origin{CreationId=hostId,BackgroundId="chef"}};
 inv.Capacity=3;Receive(w,7,hostId,"chef","wrong-hash");TickNext();Assert(inv.Granted==6&&pendingWorld!=null,"definition mismatch cannot grant wrong kit");
 Receive(w,7,hostId,"chef","hash");TickNext();Assert(inv.Granted==9&&pendingWorld==null,"host current kit delivered to backpack");
 Receive(w,7,hostId,"chef","hash");TickNext();Assert(inv.Granted==9,"host duplicate kit does not grant twice");
 var recoveredId=Guid.NewGuid().ToString("N");w.Remote=true;RebirthSurvivorClientState.Creation=recoveredId;
 RebirthSurvivorClientState.Snapshot=new OwnerSnapshot{CreationId=recoveredId};inv.Capacity=3;Reset();TickNext();Assert(inv.Granted==12&&pendingWorld==null,"client missed offer recovered from exact authoritative owner snapshot");TickNext();Assert(inv.Granted==12,"recovered client kit receipt prevents repeat");
 RebirthSurvivorClientState.Creation=Guid.Parse(recoveredId).ToString("D");RecoverOwnerOffer();Assert(pendingWorld==null,"completed client receipt recognizes dashed creation ID before reoffer");
w.Remote=false;RebirthWorldCharacterService.Record.Origin.CreationId=Guid.Parse(hostId).ToString("D");RecoverOwnerOffer();Assert(pendingWorld==null,"completed host receipt recognizes dashed creation ID before reoffer");
RebirthWorldCharacterService.Record.Origin.CreationId=Guid.Empty.ToString("D");RecoverOwnerOffer();Assert(pendingWorld==null,"empty host creation cannot recover offer");
w.Remote=true;
RebirthSurvivorClientState.Creation=Guid.NewGuid().ToString("N");inv.Capacity=3;TickNext();Assert(inv.Granted==12,"old snapshot cannot grant changed character kit");var legacy="legacy-"+new string('a',64);Reset();w.Remote=true;RebirthSurvivorClientState.Creation=legacy;RebirthSurvivorClientState.Snapshot=new OwnerSnapshot{CreationId=legacy};inv.Capacity=3;TickNext();Assert(inv.Granted==15&&pendingWorld==null,"migrated owner recovers exact starter offer");Receive(w,7,legacy,"chef","hash");TickNext();Assert(inv.Granted==15,"migrated receipt prevents duplicate grant");
Reset();w.Remote=false;var legacyHost="legacy-"+new string('b',64);RebirthWorldCharacterService.Record.Origin.CreationId=legacyHost;inv.Capacity=3;TickNext();Assert(inv.Granted==18&&pendingWorld==null,"migrated host recovers starter offer");TickNext();Assert(inv.Granted==18,"migrated host receipt prevents duplicate grant"); Reset();var faultCreation=Guid.NewGuid().ToString("N");RebirthWorldCharacterService.Record.Origin.CreationId=faultCreation;inv.Capacity=3;inv.ThrowAfter=true;int beforeFailure=inv.Granted;TickNext();Assert(inv.Granted>beforeFailure,"fault did not exercise native mutation");int afterFailure=inv.Granted;inv.ThrowAfter=false;inv.Capacity=3;TickNext();Assert(inv.Granted==afterFailure,"uncertain starter grant replayed");Assert(p.Buffs.GetCustomVar("rbStarter_"+faultCreation)==0f&&pendingWorld!=null,"uncertain entry falsely completed kit");
 Reset();w.Remote=true;
 foreach(string staleKind in new[]{"character","background","hash","player"}){
  string current=Guid.NewGuid().ToString("N");RebirthSurvivorClientState.Creation=current;RebirthSurvivorClientState.Snapshot=new OwnerSnapshot{CreationId=current};
  int before=inv.Granted;inv.Capacity=3;
  Receive(w,staleKind=="player"?99:7,staleKind=="character"?Guid.NewGuid().ToString("N"):current,staleKind=="background"?"logger":"chef",staleKind=="hash"?"old":"hash");
  TickNext();Assert(inv.Granted==before,"stale "+staleKind+" granted before authoritative recovery");
  Assert(creationId==current&&pendingPlayerId==7&&backgroundId=="chef"&&definitionHash=="hash","current offer not recovered from stale "+staleKind);
  Assert(nextAttempt>Time.realtimeSinceStartup,"recovery lost retry throttle");
  TickNext();Assert(inv.Granted==before+3&&pendingWorld==null,"current kit starved by stale "+staleKind);
  TickNext();Assert(inv.Granted==before+3,"recovered kit duplicated");
 }
 string incompatible=Guid.NewGuid().ToString("N");RebirthSurvivorClientState.Creation=incompatible;RebirthSurvivorClientState.Snapshot=new OwnerSnapshot{CreationId=incompatible,ServerDefinitionHash="different"};
 int beforeIncompatible=inv.Granted;inv.Capacity=3;Receive(w,7,incompatible,"chef","different");TickNext();Assert(inv.Granted==beforeIncompatible&&nextAttempt>Time.realtimeSinceStartup,"incompatible authoritative projection bypassed hash/throttle");
 Console.WriteLine("PASS actual starter loop: early projection, full bag, partial remainder, repeat receipt, identity switch, empty GUID");
 }
}
