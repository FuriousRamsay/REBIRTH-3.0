// AUTHORED_NOT_RUN. Actual overflow/grid retirement/conservation. Native owner,
// saved file, overflow publication and immutable journal APIs are adapters.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;
class ItemValue{internal string Rich="A";internal ItemValue Clone()=>new ItemValue{Rich=Rich};}
class ItemStack{internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue value,int count){itemValue=value;this.count=count;}internal bool IsEmpty()=>count<=0;internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);}
static class RebirthNativeItemCodec{internal static string Encode(ItemValue value)=>value.Rich;}
static class RebirthCamperInventorySnapshot{internal static byte[] Capture(ItemStack[] items)=>Encoding.UTF8.GetBytes(string.Join(";",items.Select(item=>item.count+":"+item.itemValue.Rich)));internal static ItemStack[] Restore(byte[] bytes)=>Encoding.UTF8.GetString(bytes).Split(';').Select(value=>{var pair=value.Split(':');return new ItemStack(new ItemValue{Rich=pair[1]},int.Parse(pair[0]));}).ToArray();}
class World{internal EntityPlayerLocal Player;internal State worldState=new State();internal bool IsRemote()=>false;internal class State{internal string Guid="native-world";}}
class EntityPlayerLocal{internal World world;internal int entityId=1,Died=1,Count=2;internal Buffs Buffs=new Buffs();}
class Buffs{internal void SetCustomVar(string key,float phase,bool sync){Model.LivePhase=phase;}}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;internal void SaveLocalPlayerData(){Model.Saves++;if(Model.SaveAllowed)Model.SavedPhase=Model.LivePhase;}}
static class GameIO{internal static string GetSaveGameDir()=>"same-save";}
class RebirthRemoteResourceNetworkScope{internal bool Current=>Model.Scope;internal static RebirthRemoteResourceNetworkScope Capture(EntityPlayerLocal player)=>new RebirthRemoteResourceNetworkScope();}
class RebirthStablePlayerIdentity{internal string CanonicalId="owner";internal static bool TryResolveServerEntity(EntityPlayerLocal player,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayerLocal player)=>"creation";}
class PlayerDataFile{internal int id=1,deaths;}
class RebirthCamperEnergyActorImage{internal ItemStack[] Bag,Belt;internal ItemStack Cursor;internal int Count=>Bag[0].count;internal static RebirthCamperEnergyActorImage Make(int count)=>new RebirthCamperEnergyActorImage{Bag=new[]{new ItemStack(new ItemValue(),count)},Belt=new ItemStack[0],Cursor=new ItemStack(new ItemValue(),0)};}
static class RebirthCamperEnergySavedOwner{internal static bool TryRead(RebirthStablePlayerIdentity owner,out RebirthCamperEnergyActorImage actor,out PlayerDataFile file){actor=RebirthCamperEnergyActorImage.Make(Model.Player.Count);file=new PlayerDataFile{deaths=Model.Player.Died};return Model.Readable;}}
static class RebirthCookingRemoteAdmissionBackend{internal static bool NativePhase(PlayerDataFile file,string receipt,out float phase,out bool present){phase=Model.SavedPhase;present=phase>0;return true;}}
class XUiC_RebirthCookingWorkspace{internal ItemStack[] Grid;internal bool Suppress;internal int Writes,Notifications;internal ItemStack[] ReadCustodyGrid()=>Grid;internal void WriteCustodyGrid(int slot,ItemStack value){Writes++;if(!Suppress)Grid[slot]=value;}internal void CustodyStatus(string text){}internal void CustodyTransitionFinished(string action){Notifications++;}}
class RebirthCamperTransferJournal{internal enum Phase{Prepared,Uncertain,Applied,Rejected}internal class Record{internal Guid Operation=Guid.NewGuid();internal string OwnerCreation="creation";internal byte[] Before;internal Phase State=Phase.Uncertain;}}
class JournalAdapter{internal int Advances;internal RebirthCamperTransferJournal.Record Advance(Guid operation,RebirthCamperTransferJournal.Phase from,RebirthCamperTransferJournal.Phase to){if(Model.Record.Operation!=operation||Model.Record.State!=from)throw new IOException();Model.Record.State=to;Advances++;return Model.Record;}}
static class RebirthCookingOverflowOriginal
{
 internal static bool Advance(RebirthCamperTransferJournal.Record parent,EntityPlayerLocal actor,string owner,string creation,ItemStack[] outputs,int deaths,int entity,Func<bool> current){Model.DropQueries++;if(!current()||Model.SavedPhase!=2||parent.State!=RebirthCamperTransferJournal.Phase.Uncertain)throw new Exception("overflow before saved owner admission");if(Model.Spawns==0)Model.Spawns++;return Model.DropSaved;}
 internal static bool Terminal(RebirthCamperTransferJournal.Record parent,EntityPlayerLocal actor,string owner,string creation,ItemStack[] outputs,int deaths,int entity)=>Model.DropSaved&&Model.Spawns==1;
}
static class Model{internal static EntityPlayerLocal Player;internal static RebirthCamperTransferJournal.Record Record;internal static bool Scope=true,SaveAllowed=true,Readable=true,DropSaved;internal static float LivePhase,SavedPhase;internal static int Saves,Spawns,DropQueries,OwnerCredits,Applies;}
// ACTUAL_OWNER_OVERFLOW
internal static partial class RebirthCookingGridCustody
{
 private sealed class Intent{internal Guid Operation;internal EntityPlayerLocal Player;internal World WorldObject;internal int Deaths=1;internal string Owner="owner",Creation="creation";internal RebirthCamperTransferJournal.Record Record;internal RebirthCamperEnergyActorImage Before,After;internal ItemStack[] GridBefore,GridAfter;internal XUiC_RebirthCookingWorkspace Workspace;}
 private static List<Intent> Active=new List<Intent>();private static Dictionary<XUiC_RebirthCookingWorkspace,World> RecoveryHolds=new Dictionary<XUiC_RebirthCookingWorkspace,World>();private static JournalAdapter journal;
 private static bool CurrentOwner(EntityPlayerLocal player)=>Model.Scope&&player!=null&&ReferenceEquals(player.world,GameManager.Instance.World)&&ReferenceEquals(player.world.Player,player);
 private static bool Current(Intent intent)=>CurrentOwner(intent.Player)&&Active.Contains(intent)&&intent.Player.Died==intent.Deaths;
 private static bool Same(ItemStack a,ItemStack b)=>a.count==b.count&&(a.IsEmpty()&&b.IsEmpty()||a.itemValue.Rich==b.itemValue.Rich);private static bool Same(ItemStack[] a,ItemStack[] b)=>a.Length==b.Length&&a.Select((value,index)=>Same(value,b[index])).All(value=>value);
 private static bool Compatible(ItemStack[] image,ItemStack[] before,ItemStack[] after)=>image.Length==before.Length&&image.Select((value,index)=>Same(value,before[index])||Same(value,after[index])).All(value=>value);
 private static void Apply(Intent intent,bool projectGrid=true){Model.Applies++;if(projectGrid)throw new Exception("owner admission removed grid before overflow");if(Model.Player.Count!=intent.After.Count){Model.OwnerCredits++;Model.Player.Count=intent.After.Count;}}
 private static bool Saved(Intent intent,int phase,RebirthCamperEnergyActorImage image)=>Model.SavedPhase==phase&&Model.Player.Count==image.Count;
 private static string Receipt(Guid operation)=>"same-original-receipt";private static JournalAdapter Journal()=>journal;
 private static RebirthCamperEnergyActorImage Actor(string text)=>RebirthCamperEnergyActorImage.Make(int.Parse(text));
 // ACTUAL_CONSERVATION
 private static int assertions;private static void Need(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 private static Intent Reset(int ownerInserted=2,int overflow=1)
 {
  Model.Scope=Model.SaveAllowed=Model.Readable=true;Model.DropSaved=false;Model.LivePhase=Model.SavedPhase=0;Model.Saves=Model.Spawns=Model.DropQueries=Model.OwnerCredits=Model.Applies=0;
  var world=new World();Model.Player=new EntityPlayerLocal{world=world};world.Player=Model.Player;GameManager.Instance=new GameManager{World=world};
  ItemStack[] before=new[]{new ItemStack(new ItemValue(),3)},after=new[]{new ItemStack(new ItemValue(),0)};
  var root=new XElement("cookingGridOriginal",new XAttribute("action","return"),new XAttribute("world","native-world"),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XAttribute("deaths",1),new XAttribute("overflowEntity",1),new XElement("beforeActor",2),new XElement("afterActor",2+ownerInserted),new XElement("beforeGrid",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(before))),new XElement("afterGrid",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(after))),new XElement("overflow",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new[]{new ItemStack(new ItemValue(),overflow)}))));
  Model.Record=new RebirthCamperTransferJournal.Record{Before=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting))};
  var intent=new Intent{Operation=Model.Record.Operation,Player=Model.Player,WorldObject=world,Record=Model.Record,Before=RebirthCamperEnergyActorImage.Make(2),After=RebirthCamperEnergyActorImage.Make(2+ownerInserted),GridBefore=before,GridAfter=after,Workspace=new XUiC_RebirthCookingWorkspace{Grid=before.Select(item=>item.Clone()).ToArray()}};
  Active.Clear();Active.Add(intent);RecoveryHolds.Clear();journal=new JournalAdapter();return intent;
 }
 public static void Main()
 {
  var intent=Reset();Model.SaveAllowed=false;Need(!AdvanceOwnerReturnOverflow(intent)&&Model.DropQueries==0&&intent.Workspace.Grid[0].count==3,"unsaved owner credit admitted overflow or grid clear");
  Model.SaveAllowed=true;Need(!AdvanceOwnerReturnOverflow(intent)&&Model.OwnerCredits==1&&Model.Spawns==1&&intent.Workspace.Grid[0].count==3,"saved owner admission did not hold grid through unknown overflow");
  Model.Player.Count=99;Model.DropSaved=true;Need(AdvanceOwnerReturnOverflow(intent)&&Model.Player.Count==99&&Model.OwnerCredits==1&&Model.Spawns==1&&intent.Workspace.Grid[0].count==0,"positive original drops replayed owner credit or failed final grid debit");
  intent=Reset(0,3);Need(!AdvanceOwnerReturnOverflow(intent)&&Model.OwnerCredits==0&&intent.Workspace.Grid[0].count==3,"full owner did not retain grid until native drop publication");Model.DropSaved=true;Need(AdvanceOwnerReturnOverflow(intent)&&Model.Spawns==1&&intent.Workspace.Grid[0].count==0,"full owner original did not finish saved native overflow");
  intent=Reset();AdvanceOwnerReturnOverflow(intent);Model.DropSaved=true;intent.Workspace.Suppress=true;Need(!AdvanceOwnerReturnOverflow(intent)&&Model.OwnerCredits==1&&Model.Spawns==1,"suppressed grid write retired original");intent.Workspace.Suppress=false;Need(AdvanceOwnerReturnOverflow(intent)&&Model.OwnerCredits==1&&Model.Spawns==1,"grid-only retry repeated financial effect");
  intent=Reset();AdvanceOwnerReturnOverflow(intent);Model.Scope=false;Model.DropSaved=true;Need(!AdvanceOwnerReturnOverflow(intent)&&intent.Workspace.Grid[0].count==3,"revoked original scope projected grid");
  intent=Reset();AdvanceOwnerReturnOverflow(intent);Model.DropSaved=true;Model.Player.Died=2;Model.Player.Count=99;Need(TryRecoverSavedOwnerReturn(intent.Workspace,Model.Player,Model.Record,out bool recovered)&&recovered&&Model.Player.Count==99&&Model.OwnerCredits==1&&journal.Advances==1&&Model.Record.State==RebirthCamperTransferJournal.Phase.Applied&&intent.Workspace.Grid[0].count==0,"new-life positive owner/drop facts did not finish metadata only");
  intent=Reset();AdvanceOwnerReturnOverflow(intent);Model.Player.Died=2;Need(TryRecoverSavedOwnerReturn(intent.Workspace,Model.Player,Model.Record,out recovered)&&!recovered&&journal.Advances==0&&intent.Workspace.Grid[0].count==3,"unsaved old-life overflow cleared grid");
  intent=Reset();AdvanceOwnerReturnOverflow(intent);Model.DropSaved=true;Model.Player.Died=2;intent.Workspace.Suppress=true;Need(TryRecoverSavedOwnerReturn(intent.Workspace,Model.Player,Model.Record,out recovered)&&!recovered&&Model.Record.State==RebirthCamperTransferJournal.Phase.Applied,"metadata original retired despite suppressed grid projection");intent.Workspace.Suppress=false;Need(TryRecoverSavedOwnerReturn(intent.Workspace,Model.Player,Model.Record,out recovered)&&recovered&&Model.OwnerCredits==1&&journal.Advances==1,"positive Applied projection retry repeated owner credit or journal advance");
  intent=Reset();AdvanceOwnerReturnOverflow(intent);Model.DropSaved=true;Model.SavedPhase=1;Model.Player.Died=2;Need(TryRecoverSavedOwnerReturn(intent.Workspace,Model.Player,Model.Record,out recovered)&&!recovered&&journal.Advances==0,"earlier native phase authorized old-life grid retirement");
  Console.WriteLine("PASS actual ordinary return owner/overflow/grid assertions="+assertions+"; native file, drop publication and journal are adapters");
 }
}
