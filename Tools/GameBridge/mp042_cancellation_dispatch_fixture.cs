using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// AUTHORED_NOT_RUN. Actual cancellation UI/pump; server journal/owner delta adapters.
namespace UnityEngine {internal static class Time {internal static float realtimeSinceStartup;}}
internal class EntityPlayer {internal World world;internal int entityId=1,Died=2;internal string Creation="creation",Owner="owner";internal bool Dead;internal bool IsDead()=>Dead;internal bool IsSpawned()=>true;}
internal sealed class EntityPlayerLocal:EntityPlayer {}
internal sealed class World {internal bool Remote;internal EntityPlayerLocal Primary;internal EntityPlayer Registered;internal State worldState=new State();internal sealed class State {internal string Guid="world";}internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;internal object GetEntity(int id)=>Registered;}
internal static class ThreadManager {internal static bool IsMainThread()=>true;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;internal int Saves;internal void SaveLocalPlayerData(){Saves++;}}
internal sealed class RebirthStablePlayerIdentity {internal string CanonicalId;internal static bool TryResolveServerEntity(EntityPlayer actor,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{CanonicalId=actor.Owner};return actor.Owner!=null;}}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer p)=>p.Creation;}
internal sealed class ItemValue {internal string Token;internal Dictionary<string,string> Metadata=new Dictionary<string,string>();internal ItemValue(int type){}internal bool TryGetMetadata(string key,out string text){text=Token;return Token!=null;}internal void SetMetadata(string key,string value){Token=value;}internal void SetMetadata(string key,int value){Metadata[key]=value.ToString();}internal ItemValue Clone()=>new ItemValue(1){Token=Token,Metadata=new Dictionary<string,string>(Metadata)};}
internal sealed class ItemStack {internal ItemValue itemValue=new ItemValue(1);internal int count=1;internal ItemStack(){}internal ItemStack(ItemValue value,int quantity){itemValue=value;count=quantity;}internal bool IsEmpty()=>count==0;internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);internal static ItemStack Empty=>new ItemStack(new ItemValue(1),0);internal static ItemStack[] Clone(ItemStack[] a)=>a.Select(i=>i.Clone()).ToArray();}
internal static class RebirthRecipeOutputs {internal static void ClearFrozen(ItemValue value){value.Metadata.Remove("frozen");}}
internal sealed class Recipe {internal List<ItemStack> ingredients=new List<ItemStack>{new ItemStack()};internal bool Managed=true,Ready;}
internal static class RebirthCookingHeat {internal static bool Managed(Recipe r)=>r?.Managed==true;internal static bool Ready(Recipe r)=>r?.Ready==true;}
internal sealed class TileEntityWorkstation {}
internal sealed class WorkstationData {internal TileEntityWorkstation TileEntity=new TileEntityWorkstation();}
internal sealed class XUiC_WorkstationWindowGroup {internal WorkstationData WorkstationData=new WorkstationData();}
internal sealed class PlayerUI {internal EntityPlayer entityPlayer;}
internal sealed class Xui {internal PlayerUI playerUI=new PlayerUI();}
internal sealed class XUiC_CraftingQueue {internal XUiC_RecipeStack[] Cards;internal XUiC_RecipeStack[] GetRecipesToCraft()=>Cards;}
internal sealed class XUiC_RecipeStack {internal XUiC_CraftingQueue Owner;internal XUiC_RecipeStack(){Owner=new XUiC_CraftingQueue{Cards=new[]{this}};}internal int recipeCount=1,OutputQuality;internal Xui xui=new Xui();internal Recipe recipe=new Recipe();internal XUiC_WorkstationWindowGroup Station=new XUiC_WorkstationWindowGroup();internal T GetParentByType<T>() where T:class=>Station as T;}
internal static class RebirthCookingStationLocator {internal static string Encode(TileEntityWorkstation station)=>"station";}
internal sealed class ConnectionManager {internal int Sends;internal bool Throw;internal void SendToServer(object packet,bool option){Sends++;if(Throw)throw new Exception("native send failed");}}
internal static class SingletonMonoBehaviour<T> where T:new() {internal static T Instance=new T();}
internal sealed class NetPackagePlayerData {internal object Setup(EntityPlayer p)=>this;}
internal static class NetPackageManager {internal static T GetPackage<T>() where T:new()=>new T();}
internal static class Log {internal static void Warning(string text){}}
internal static class RebirthCookingCancellationProjection {internal static bool Pass=true;internal static int Calls;internal static bool Refresh(XUiC_RecipeStack card,string location,string token,Func<bool> current){Calls++;return current()&&Pass;}}
internal static class RebirthCookingOwnerDelta {
 internal sealed class Plan {internal Guid Operation=Guid.Parse("11111111111111111111111111111111");internal int Deaths=2;internal string World="world",Creation="creation",Owner="owner";}
 internal static int Advances,Retired,Historical;internal static int PlanDeaths=2;internal static bool HistoricalResult=true;internal static Plan Decode(string text)=>new Plan{Deaths=PlanDeaths};
 internal static bool RetireHistoricalMetadata(EntityPlayerLocal p,Plan plan,Func<bool> current){if(!current())throw new Exception("expired historical terminal");Historical++;return HistoricalResult;}
 internal static bool Advance(EntityPlayerLocal p,Plan plan,int phase,Func<bool> current,Func<bool> source){if(!current()||!source())throw new Exception("stale owner callback");Advances++;return phase>=1;}
 internal static bool Retire(EntityPlayerLocal p,Plan plan){Retired++;return true;}
 internal static bool AbandonRejectedWithoutEffect(EntityPlayerLocal p,Plan plan)=>true;
}
internal static class RebirthCookingSessionService {
 internal static int Calls;internal static string Response;internal static bool Defer;internal static Action<string> Pending;
 internal static void Request(EntityPlayerLocal player,string action,string recipe="",string book="",string magazine="",ItemValue item=null,int count=0,Action<string> reply=null){Calls++;if(action!="cancelRemoteCookingBatch"||magazine!="station")throw new Exception("changed source cancellation route");if(Defer)Pending=reply;else reply(Response);}
}
internal static class RebirthCookingRemoteCancellation {
 private static string LegacySignature(Recipe recipe,int count,int quality)=>recipe.ingredients[0].itemValue.Token==null?new string('a',64):null; // selector adapter; actual selector composed with server cohort
// ACTUAL_CLIENT
// ACTUAL_COUNTS
// ACTUAL_ENTRY
// ACTUAL_TICK
// ACTUAL_REFUNDS
 internal static void Exercise(){
  string payload=Convert.ToBase64String(Encoding.UTF8.GetBytes("explicit-owner-plan-adapter"));
  foreach(bool remote in new[]{false,true}){
   Clients.Clear();var w=new World{Remote=remote};var p=new EntityPlayerLocal{world=w};w.Primary=p;w.Registered=p;GameManager.Instance=new GameManager{World=w};
   var connection=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=connection;UnityEngine.Time.realtimeSinceStartup=1;
   RebirthCookingOwnerDelta.Advances=RebirthCookingOwnerDelta.Retired=RebirthCookingSessionService.Calls=0;RebirthCookingSessionService.Defer=false;RebirthCookingCancellationProjection.Pass=true;
   var card=new XUiC_RecipeStack();card.xui.playerUI.entityPlayer=p;card.recipe.ingredients[0].itemValue.Token=Guid.NewGuid().ToString("N");
   RebirthCookingSessionService.Response="cancel:0:"+payload;Need(TryCancel(card)&&Clients.Count==1,"canonical cancellation not retained");
   UnityEngine.Time.realtimeSinceStartup+=2;RebirthCookingSessionService.Response="cancel:1:"+payload;TickClients();
   UnityEngine.Time.realtimeSinceStartup+=2;RebirthCookingSessionService.Response="cancelled:"+payload;TickClients();
   Need(Clients.Count==0&&RebirthCookingOwnerDelta.Advances==3&&RebirthCookingOwnerDelta.Retired==1,"canonical cancellation lost handshake");
   Need(RebirthCookingSessionService.Calls==3&&connection.Sends==(remote?3:0)&&GameManager.Instance.Saves==(remote?0:3),"host local cancellation used remote player-data send");
  }
  {
   Clients.Clear();var w=new World();var p=new EntityPlayerLocal{world=w};w.Primary=p;w.Registered=p;GameManager.Instance=new GameManager{World=w};UnityEngine.Time.realtimeSinceStartup+=2;RebirthCookingSessionService.Response="cancelled:"+payload;
   RebirthCookingCancellationProjection.Pass=false;RebirthCookingOwnerDelta.Advances=RebirthCookingOwnerDelta.Retired=0;var card=new XUiC_RecipeStack();card.xui.playerUI.entityPlayer=p;card.recipe.ingredients[0].itemValue.Token=Guid.NewGuid().ToString("N");TryCancel(card);
   Need(Clients.Count==1&&Clients[0].Terminal&&RebirthCookingOwnerDelta.Advances==1&&RebirthCookingOwnerDelta.Retired==1,"failed settled projection lost terminal state");int requests=RebirthCookingSessionService.Calls;
   UnityEngine.Time.realtimeSinceStartup+=2;TickClients();Need(Clients.Count==1&&RebirthCookingSessionService.Calls==requests&&RebirthCookingOwnerDelta.Advances==1,"projection retry replayed financial/network action");RebirthCookingCancellationProjection.Pass=true;UnityEngine.Time.realtimeSinceStartup+=2;TickClients();Need(Clients.Count==0&&RebirthCookingOwnerDelta.Retired==1,"settled projection did not recover metadata only");
  }
  {
   Clients.Clear();var w=new World{Remote=true};var p=new EntityPlayerLocal{world=w};w.Primary=p;w.Registered=p;GameManager.Instance=new GameManager{World=w};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();UnityEngine.Time.realtimeSinceStartup+=2;RebirthCookingSessionService.Defer=true;RebirthCookingOwnerDelta.Advances=RebirthCookingOwnerDelta.Retired=0;
   var first=new XUiC_RecipeStack();var second=new XUiC_RecipeStack();first.xui.playerUI.entityPlayer=second.xui.playerUI.entityPlayer=p;var owner=new XUiC_CraftingQueue{Cards=new[]{first,second}};first.Owner=second.Owner=owner;TryCancel(first);TryCancel(second);Need(Clients.Count==2&&Clients[0].LegacySlot==0&&Clients[1].LegacySlot==1&&Clients[0].Token!=Clients[1].Token,"identical legacy cards lost explicit native slot identity");
   var client=Clients[1];RebirthCookingSessionService.Pending("cancel-unused:"+Guid.NewGuid().ToString("N"));Need(Clients.Count==2,"foreign no-tag reply retired legacy original");client.Plan=new RebirthCookingOwnerDelta.Plan();RebirthCookingSessionService.Pending("cancel-unused:"+client.Token);Need(Clients.Count==2,"no-tag reply retired an entered owner plan");client.Plan=null;RebirthCookingSessionService.Pending("cancel-unused:"+client.Token);Need(Clients.Count==1&&RebirthCookingOwnerDelta.Advances==0&&RebirthCookingOwnerDelta.Retired==0,"positive no-tag reply changed owner financial state");Clients.Clear();
  }
  for(int variant=0;variant<4;variant++){
   Clients.Clear();var w=new World{Remote=true};var p=new EntityPlayerLocal{world=w};w.Primary=p;w.Registered=p;GameManager.Instance=new GameManager{World=w};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();UnityEngine.Time.realtimeSinceStartup+=2;
   var card=new XUiC_RecipeStack();card.xui.playerUI.entityPlayer=p;card.recipe.ingredients[0].itemValue.Token=Guid.NewGuid().ToString("N");RebirthCookingSessionService.Defer=true;RebirthCookingOwnerDelta.Advances=0;
   Need(TryCancel(card)&&RebirthCookingSessionService.Pending!=null,"request callback missing");
   if(variant==0)p.Died++;if(variant==1)p.Owner="foreign";if(variant==2)w.Registered=new EntityPlayer();if(variant==3)p.Dead=true;
   RebirthCookingSessionService.Pending("cancelled:"+payload);Need(RebirthCookingOwnerDelta.Advances==0,"stale cancellation callback applied owner delta");TickClients();Need(Clients.Count==0,"stale UI runner consumed cancellation capacity");
  }
  RebirthCookingSessionService.Defer=false;Clients.Clear();var localWorld=new World();var local=new EntityPlayerLocal{world=localWorld};localWorld.Primary=local;localWorld.Registered=local;GameManager.Instance=new GameManager{World=localWorld};
  var legacy=new XUiC_RecipeStack();legacy.xui.playerUI.entityPlayer=local;RebirthCookingSessionService.Response="cancel:0:"+payload;Need(TryCancel(legacy)&&Clients.Count==1&&Clients[0].Legacy!=null,"legacy tokenless row bypassed retained cancellation route");Clients.Clear();
  localWorld.Remote=true;legacy.recipe.ingredients[0].itemValue.Token=Guid.NewGuid().ToString("N");UnityEngine.Time.realtimeSinceStartup+=2;
  var failing=new ConnectionManager{Throw=true};SingletonMonoBehaviour<ConnectionManager>.Instance=failing;int calls=RebirthCookingSessionService.Calls;
  Need(TryCancel(legacy)&&Clients.Count==1&&!Clients[0].Waiting&&RebirthCookingSessionService.Calls==calls,"send exception stranded waiting cancellation");
  failing.Throw=false;UnityEngine.Time.realtimeSinceStartup+=2;RebirthCookingSessionService.Response="cancel:0:"+payload;TickClients();
  Need(Clients.Count==1&&!Clients[0].Waiting&&RebirthCookingSessionService.Calls==calls+1,"same cancellation did not recover after send exception");
  for(int variant=0;variant<6;variant++){
   Clients.Clear();var w=new World{Remote=true};var p=new EntityPlayerLocal{world=w,Died=3};w.Primary=p;w.Registered=p;GameManager.Instance=new GameManager{World=w};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();UnityEngine.Time.realtimeSinceStartup+=2;
   RebirthCookingOwnerDelta.PlanDeaths=variant==2?-1:variant==3?4:2;RebirthCookingOwnerDelta.Historical=RebirthCookingOwnerDelta.Advances=RebirthCookingOwnerDelta.Retired=0;RebirthCookingOwnerDelta.HistoricalResult=variant!=4;
   RebirthCookingSessionService.Response=(variant==1?"cancel:1:":variant==5?"retry-cancel:":"cancelled:")+payload;
   var card=new XUiC_RecipeStack();card.xui.playerUI.entityPlayer=p;card.recipe.ingredients[0].itemValue.Token=Guid.NewGuid().ToString("N");Need(TryCancel(card),"historical terminal entry refused");
   Need(RebirthCookingOwnerDelta.Advances==0&&RebirthCookingOwnerDelta.Retired==0,"earlier/future/legacy life replayed owner action");
   Need(Clients.Count==(variant==0?0:1)&&RebirthCookingOwnerDelta.Historical==((variant==0||variant==4)?1:0),"historical terminal qualification incorrect "+variant);
  }
  RebirthCookingOwnerDelta.PlanDeaths=2;RebirthCookingOwnerDelta.HistoricalResult=true;RefundQuantityCases();
  Console.WriteLine("PASS actual host/remote cancellation dispatch and stale callback scope; native server journals and topology NOT RUN");
 }
 private static void RefundQuantityCases(){
  var recipe=new Recipe();recipe.ingredients[0].itemValue.Metadata["rich-original"]="keep";recipe.ingredients[0].itemValue.Metadata["rebirth.cooking.queue.held"]="1";recipe.ingredients[0].itemValue.Metadata["rebirth.cooking.registeredOwner"]="owner";
  var refund=Refunds(recipe,3);Need(refund.Length==1&&refund[0].count==3&&refund[0].itemValue.Metadata["rich-original"]=="keep"&&!refund[0].itemValue.Metadata.ContainsKey("rebirth.cooking.queue.held")&&!refund[0].itemValue.Metadata.ContainsKey("rebirth.cooking.registeredOwner")&&recipe.ingredients[0].itemValue.Metadata.Count==3,"refund changed paid native ingredient or retained queue authority");
  Need(Refunds(recipe,int.MaxValue)[0].count==int.MaxValue,"valid maximum quantity rejected");
  foreach(int variant in new[]{0,1,2,3}){
   recipe.ingredients[0].count=variant==0?2:variant==1?0:variant==2?-1:1;bool threw=false;
   try{Refunds(recipe,variant==3?0:int.MaxValue);}catch{threw=true;}
   Need(threw,"invalid/overflowed refund quantity admitted "+variant);
  }
 }
 private static void Need(bool b,string m){if(!b)throw new Exception(m);}
}
internal static class Program {public static void Main()=>RebirthCookingRemoteCancellation.Exercise();}
