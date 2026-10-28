// AUTHORED_NOT_RUN. Actual complete grid Tick method. Native saved-file reader,
// witness acknowledgment, scope and metadata journal are explicit adapters.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;
static class ModEvents{internal struct SGameUpdateData{}}
class ItemStack{internal int count;internal string Tag="rich";internal ItemStack Clone()=>new ItemStack{count=count,Tag=Tag};}
class RebirthCamperEnergyActorImage{internal string Tag;internal static RebirthCamperEnergyActorImage Capture(EntityPlayerLocal p)=>new RebirthCamperEnergyActorImage{Tag=p.LiveTag};internal bool Matches(RebirthCamperEnergyActorImage other)=>other?.Tag==Tag;}
class Buffs{internal int Writes;internal void SetCustomVar(string key,float value,bool sync){Writes++;}internal void RemoveCustomVar(string key){Writes++;}}
class EntityPlayerLocal{internal int Died=2;internal string LiveTag="after";internal World world;internal Buffs Buffs=new Buffs();}
class World{internal EntityPlayerLocal Player;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;internal int NativeSaves;internal void SaveLocalPlayerData(){NativeSaves++;}}
class XUiC_RebirthCookingWorkspace{internal ItemStack[] Grid;internal int Writes,Notifications;internal Action DuringWrite;internal ItemStack[] ReadCustodyGrid()=>Grid;internal void WriteCustodyGrid(int slot,ItemStack stack){Writes++;Grid[slot]=stack;DuringWrite?.Invoke();}internal void CustodyStatus(string status){}internal void CustodyTransitionFinished(string action){Notifications++;}}
class RebirthCamperTransferJournal{internal enum Phase{Prepared,Uncertain,Applied,Rejected}internal class Record{internal Guid Operation;internal Phase State=Phase.Uncertain;internal byte[] Before;}}
class JournalAdapter{internal RebirthCamperTransferJournal.Record Record;internal bool Fail;internal int Advances;internal RebirthCamperTransferJournal.Record Read(Guid id)=>Record;
 internal RebirthCamperTransferJournal.Record Advance(Guid id,RebirthCamperTransferJournal.Phase before,RebirthCamperTransferJournal.Phase after){Advances++;if(Fail)throw new IOException("metadata publication unavailable");if(Record.Operation!=id||Record.State!=before)throw new InvalidDataException();Record.State=after;return Record;}}
static class RebirthCookingPull{internal static int Completes;internal static void CompleteGridOriginal(EntityPlayerLocal player,long id){Completes++;}}
static class RebirthCookingRemoteAdmission{internal static void CompleteUnpaidAdoption(EntityPlayerLocal player,Guid parent)=>throw new Exception("unrelated adoption");}
static class RebirthCookingGridCustody{
 private class Intent{internal Guid Operation,Key,ParentAdmission;internal string NativeReceipt,Action="fetched";internal int Deaths=2,ServerPhase;internal long Revision=1,FetchId=17;internal bool EvidenceOnly;internal World WorldObject;internal EntityPlayerLocal Player;internal XUiC_RebirthCookingWorkspace Workspace;internal RebirthCamperTransferJournal.Record Record;internal RebirthCamperEnergyActorImage Before=new RebirthCamperEnergyActorImage{Tag="before"},After=new RebirthCamperEnergyActorImage{Tag="after"};internal ItemStack[] GridBefore,GridAfter;}
 private class AuditCounts{internal long Applied;}
 private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayerLocal,AuditCounts> Audit=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayerLocal,AuditCounts>();
 private static readonly List<Intent> Active=new List<Intent>();
 private static JournalAdapter journal;private static bool Scope=true,Acknowledged=true;private static Action DuringWitness;private static int ApplyCalls;
 private static JournalAdapter Journal()=>journal;
 private static void Bootstrap(EntityPlayerLocal player){}
 private static bool Current(Intent intent)=>Scope&&ReferenceEquals(GameManager.Instance.World,intent.WorldObject)&&ReferenceEquals(intent.WorldObject.Player,intent.Player)&&intent.Player.Died==intent.Deaths;
 private static bool Saved(Intent intent,int phase,RebirthCamperEnergyActorImage image)=>phase==2;
 private static bool HostSaved(Intent intent,int phase,RebirthCamperEnergyActorImage image){var action=DuringWitness;DuringWitness=null;action?.Invoke();return phase==2&&Acknowledged;}
 private static bool Compatible(ItemStack[] live,ItemStack[] before,ItemStack[] after)=>live.Length==before.Length&&live.Select((s,i)=>Same(s,before[i])||Same(s,after[i])).All(v=>v);
 private static bool Same(ItemStack a,ItemStack b)=>a.count==b.count&&a.Tag==b.Tag;
 private static bool Same(ItemStack[] a,ItemStack[] b)=>a.Length==b.Length&&a.Select((s,i)=>Same(s,b[i])).All(v=>v);
 private static void Apply(Intent intent,bool projectGrid=true){ApplyCalls++;throw new Exception("saved native effect was replayed");}
 private static bool TryRecoverSavedOwnerReturn(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,RebirthCamperTransferJournal.Record record,out bool recovered){recovered=false;return false;}
 private static bool AdvanceOwnerReturnOverflow(Intent intent)=>false;
 private static int AdvanceSourceReturn(Intent intent)=>-1;
 private static void AdvanceDetachedSourceReturns(){}
 private static string Receipt(Guid operation)=>"receipt";
 private static Intent Parse(RebirthCamperTransferJournal.Record record,XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player)=>throw new Exception("unrelated gesture cleanup");
 // ACTUAL_TICK
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Run(){
  foreach(bool prepared in new[]{false,true})for(int variant=0;variant<10;variant++){
   Active.Clear();Scope=Acknowledged=true;DuringWitness=null;ApplyCalls=RebirthCookingPull.Completes=0;
   var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance=new GameManager{World=world};
   var intent=new Intent{Operation=Guid.NewGuid(),WorldObject=world,Player=player,GridBefore=new[]{new ItemStack(),new ItemStack()},GridAfter=new[]{new ItemStack{count=2},new ItemStack{count=1}}};
   intent.Record=new RebirthCamperTransferJournal.Record{Operation=intent.Operation,State=prepared?RebirthCamperTransferJournal.Phase.Prepared:RebirthCamperTransferJournal.Phase.Uncertain,Before=Encoding.UTF8.GetBytes("<cookingGridOriginal />")};journal=new JournalAdapter{Record=intent.Record};
   if(variant!=0)intent.Workspace=new XUiC_RebirthCookingWorkspace{Grid=intent.GridBefore.Select(i=>i.Clone()).ToArray()};
   if(variant==2)Acknowledged=false;if(variant==3)player.LiveTag="foreign";
   if(variant==4)DuringWitness=()=>player.LiveTag="foreign";if(variant==5)DuringWitness=()=>Scope=false;
   if(variant==6)intent.Workspace.Grid[0]=new ItemStack{count=99,Tag="foreign"};if(variant==7)journal.Fail=true;
   if(variant==8)intent.Workspace.DuringWrite=()=>player.LiveTag="foreign";if(variant==9)player.Died++;
   Active.Add(intent);var update=new ModEvents.SGameUpdateData();Tick(ref update);
   Need(ApplyCalls==0&&player.Buffs.Writes==0&&GameManager.Instance.NativeSaves==0,"saved owner delivery must never replay native Apply, receipt writes or saves "+variant);
   bool complete=variant<2;
   Need((intent.Record.State==RebirthCamperTransferJournal.Phase.Applied)==complete&&Active.Contains(intent)!=complete&&RebirthCookingPull.Completes==(complete?1:0),"saved-delivery acknowledgment/retirement differs "+variant);
   if(complete&&intent.Workspace!=null)Need(intent.Workspace.Writes==2&&intent.Workspace.Notifications==1&&Same(intent.Workspace.Grid,intent.GridAfter),"saved delivery must restore only exact custom-grid cells");
   if(variant==7){int writes=intent.Workspace.Writes;journal.Fail=false;Tick(ref update);Need(ApplyCalls==0&&intent.Workspace.Writes==Math.Max(writes,2)&&RebirthCookingPull.Completes==1&&!Active.Contains(intent),"metadata publication retry must not repeat owner or completed grid delivery");int completedWrites=intent.Workspace.Writes;Tick(ref update);Need(intent.Workspace.Writes==completedWrites&&RebirthCookingPull.Completes==1,"retired saved-after runner cannot repeat completion");}
  }
  Console.WriteLine("PASS actual saved-after grid Tick: acknowledgment-only recovery, native effect replay refusal, scope/image conflicts and metadata retries; native readers/topology NOT_RUN");
 }
}
static class Program{static void Main()=>RebirthCookingGridCustody.Run();}
