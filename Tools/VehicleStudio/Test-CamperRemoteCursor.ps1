$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$root=Join-Path $PSScriptRoot '../..'
$policy=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperRemoteCursorPolicy.cs'))
$client=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperRemoteManagement.cs'))
$doubles=@'
using System;
using System.Collections.Generic;
public static class ThreadManager {public static bool IsMainThread()=>true;}
public struct Vec {public float sqrMagnitude=>0;public static Vec operator -(Vec a,Vec b)=>a;}
public class World {public EntityVehicle Vehicle;public bool IsRemote()=>true;public object GetEntity(int id)=>Vehicle;}
public class EntityVehicle {public World world;public int entityId=7;public Vec position;}
public class Buffs {public Dictionary<string,float> Values=new Dictionary<string,float>();public float GetCustomVar(string key)=>Values.TryGetValue(key,out var n)?n:0;public void SetCustomVar(string k,float v,bool sync)=>Values[k]=v;}
public class Grid {public ItemStack[] items=new[]{new ItemStack(1),new ItemStack(0)};}
public class Bag {public Grid ItemGrid=new Grid();public void SetSlot(int slot,ItemStack item){ItemGrid.items[slot]=item;}}
public class EntityPlayerLocal {public int entityId=8;public Vec position;public Buffs Buffs=new Buffs();public Bag bag=new Bag();public LocalPlayerUI PlayerUI=>LocalPlayerUI.Ui;public bool IsDead()=>false;}
public class ItemValue {public Dictionary<string,object> Metadata=new Dictionary<string,object>();}
public class ItemStack {public ItemValue itemValue=new ItemValue();public int count;public ItemStack(int n){count=n;}public static ItemStack Empty=>new ItemStack(0);public ItemStack Clone()=>new ItemStack(count);}
public class XUiC_DragAndDropWindow {public ItemStack CurrentStack=new ItemStack(1);public int Writes;public bool FaultAfterWrite;
 public XUi xui=new XUi();public bool IsEmpty()=>CurrentStack.count==0;
 public void SetCurrentStack(ItemStack s,bool sound){CurrentStack=s;Writes++;if(FaultAfterWrite)throw new InvalidOperationException("listener fault");}}
public class Modules {public long Revision;public ItemStack GetSlot(int slot)=>ItemStack.Empty;}
public class Stations {public long Revision;public Station GetSlot(int slot)=>null;}
public class Station {public ItemStack InstalledItem;}
public class RebirthCamperTransferSnapshot {
 public Modules Storage=new Modules();public Stations Stations=new Stations();public ItemStack Cursor;public int BackpackSlot=-1;
 public static RebirthCamperTransferSnapshot Restore(Guid id,byte[] data)=>new RebirthCamperTransferSnapshot{Cursor=new ItemStack(data[0])};
}
public static class RebirthCamperInventorySnapshot {public static byte[] Capture(ItemStack[] items)=>new byte[]{(byte)items[0].count};}
public class XUiC_RebirthCamperModules {
 public Func<int,ItemStack,bool> Transfer;public Action Close;public int Refreshes;public EntityVehicle BoundVehicle;public XUiWindowGroup WindowGroup=new XUiWindowGroup();
 public void Bind(string title,Func<bool> current,Func<int,ItemStack> read,Func<ItemStack,bool> accepts,Func<int,ItemStack,bool> transfer,Action close,bool crafting,Func<int,int,bool> preferred){Transfer=transfer;Close=close;}
 public void CancelPreferredRemoval(){}public T GetChildByType<T>() where T:class=>null;
 public void RefreshModules(){Refreshes++;}
}
public class GUIWindow {}
public class Controller {public T GetChildByType<T>() where T:class=>LocalPlayerUI.Ui.View as T;}
public class XUiWindowGroup:GUIWindow {public Controller Controller=new Controller();}
public class Windows {public bool Opened,Modal,EscClosable;public bool IsWindowOpen(string name)=>false;public bool TryGetWindow(string name,out GUIWindow w){w=new XUiWindowGroup();return true;}public void Open(GUIWindow w,bool modal,bool notEscClosable){Opened=true;Modal=modal;EscClosable=!notEscClosable;}public void Close(GUIWindow w){Opened=false;LocalPlayerUI.Ui.View.Close();}public void Escape(){if(Opened&&EscClosable){Opened=false;LocalPlayerUI.Ui.View.Close();}}}
public class XUi {public T GetChildByType<T>() where T:class=>LocalPlayerUI.Ui.Cursor as T;}
public class LocalPlayerUI {
 public static LocalPlayerUI Ui=new LocalPlayerUI();public Windows windowManager=new Windows();public XUi xui=new XUi();
 public XUiC_DragAndDropWindow Cursor=new XUiC_DragAndDropWindow();public XUiC_RebirthCamperModules View=new XUiC_RebirthCamperModules();
 public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal p)=>Ui;
}
public class GameManager {public static GameManager Instance=new GameManager();public World World;public static string LastTooltip;public static void ShowTooltip(EntityPlayerLocal p,string text){LastTooltip=text;}}
public static class Log {public static void Warning(string message){}}
public static class Localization {public static string Get(string key)=>key;}
public static class RebirthCamperManagementAccess {public static bool Accepts(ItemStack stack,bool crafting)=>true;}
public static class RebirthCamperModuleInteraction {
 public static int Generation,Released;public static bool Current(EntityVehicle v,EntityPlayerLocal p)=>true;
 public static Func<bool> CaptureCurrent(EntityVehicle v,EntityPlayerLocal p){int expected=Generation;return ()=>expected==Generation;}
 public static Action CaptureRelease(EntityVehicle v,EntityPlayerLocal p){int expected=Generation;return ()=>{if(expected==Generation)Released++;};}
}
public static class RebirthCamperRadial {public const string Storage="storage",Stations="stations";}
public enum RebirthCamperRemoteStationGrid:byte {Fuel,Input,Tools,Output}
public enum RebirthCamperRemoteStationGesture:byte {Swap,Half,One,Preferred}
public static class RebirthCamperRemoteTransport {public const byte Snapshot=0,Prepare=1,Commit=2,Cancel=3,GridPrepare=4,QuickPrepare=5,View=4,Error=5,RemovalContents=6;public static bool IsRegistered=>true;}
public static class RebirthCamperTrace {public static void Write(string stage,EntityVehicle vehicle,string detail){}}
public static class RebirthCamperStationRemovalContents {public const string ContentsKey="contents";}
public static class RebirthCamperStationContentsDelivery {public static void Confirm(EntityPlayerLocal p,EntityVehicle v,XUi ui,RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after){}}
public static class RebirthCamperPreferredRemoval {public static void Confirm(EntityPlayerLocal p,EntityVehicle v,RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after){}}
public static class RebirthCamperQuickTransferFrame {public static byte[] Capture(int slot,ItemStack item)=>new byte[]{(byte)item.count};}
public class XUiC_RebirthBackpackSectionBackpack {public void SetStacks(ItemStack[] items){}}
public static class RebirthCamperTransferJournal {public enum Phase{Prepared,Uncertain,Applied,Rejected}}
public class NetPackageRebirthCamperManagement {
 public Guid Request,Operation;public byte Action;
 public NetPackageRebirthCamperManagement Setup(int p,int v,Guid r,Guid op,byte a,bool c,int s,long sr,long tr,byte[] offered,Guid installation=default,RebirthCamperRemoteStationGrid grid=RebirthCamperRemoteStationGrid.Fuel,RebirthCamperRemoteStationGesture gesture=RebirthCamperRemoteStationGesture.Swap){Request=r;Operation=op;Action=a;return this;}
}
public class NetPackagePlayerData {public int Count;public float Receipt;public NetPackagePlayerData Setup(EntityPlayerLocal player){Count=LocalPlayerUI.Ui.Cursor.CurrentStack.count;return this;}}
public static class NetPackageManager {public static T GetPackage<T>() where T:new()=>new T();}
public class ConnectionManager {public List<object> Sent=new List<object>();public void SendToServer(object p,bool immediate){Sent.Add(p);}}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public static class CamperRemoteCursorChecks {
 static EntityPlayerLocal player;static EntityVehicle vehicle;static World world;static Guid id=Guid.NewGuid();
 static ConnectionManager Wire=>SingletonMonoBehaviour<ConnectionManager>.Instance;
 static NetPackageRebirthCamperManagement Last(){for(int i=Wire.Sent.Count-1;i>=0;i--)if(Wire.Sent[i] is NetPackageRebirthCamperManagement)return (NetPackageRebirthCamperManagement)Wire.Sent[i];throw new Exception("No request");}
 static void Open(){RebirthCamperModuleInteraction.Generation++;LocalPlayerUI.Ui=new LocalPlayerUI();RebirthCamperRemoteManagement.Open(vehicle,player,false);Check(LocalPlayerUI.Ui.windowManager.Modal&&LocalPlayerUI.Ui.windowManager.EscClosable,"Actual management open suppressed native Escape close");}
 static void Reply(NetPackageRebirthCamperManagement request,byte state,Guid operation){RebirthCamperRemoteManagement.Receive(world,7,request.Request,operation,id,state,new byte[]{1},new byte[]{0});}
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 public static void Run(){
  world=new World();vehicle=new EntityVehicle{world=world};world.Vehicle=vehicle;player=new EntityPlayerLocal();GameManager.Instance.World=world;
  Open();var snapshot=Last();Reply(snapshot,4,Guid.Empty);
  Check(LocalPlayerUI.Ui.View.Transfer(0,LocalPlayerUI.Ui.Cursor.CurrentStack)==false,"Request immediately reported committed");var prepare=Last();
  Check(prepare.Action==1&&LocalPlayerUI.Ui.Cursor.Writes==0,"Prepare debited cursor");
  Reply(prepare,0,prepare.Operation);Check(LocalPlayerUI.Ui.Cursor.Writes==1&&Last().Action==2,"Prepared reply did not publish exactly once and commit");
  int packets=Wire.Sent.Count;Reply(prepare,0,prepare.Operation);Check(LocalPlayerUI.Ui.Cursor.Writes==1&&Wire.Sent.Count==packets,"Duplicate reply repeated debit/commit");
  LocalPlayerUI.Ui.Cursor.CurrentStack=new ItemStack(9);Reply(prepare,2,prepare.Operation);Check(LocalPlayerUI.Ui.Cursor.countForCheck()==9,"Applied reply reset legitimately moved cursor");
  var oldClose=LocalPlayerUI.Ui.View.Close;Open();int released=RebirthCamperModuleInteraction.Released;oldClose();Check(RebirthCamperModuleInteraction.Released==released,"Old close released new native lease");
  var query=Last();Reply(query,0,prepare.Operation);Check(LocalPlayerUI.Ui.Cursor.Writes==0&&Last().Action==2,"Receipt replay debited cursor again");LocalPlayerUI.Ui.View.Close();
  Open();query=Last();Reply(query,4,Guid.Empty);LocalPlayerUI.Ui.View.Transfer(0,LocalPlayerUI.Ui.Cursor.CurrentStack);prepare=Last();
  LocalPlayerUI.Ui.Cursor.CurrentStack=new ItemStack(8);Reply(prepare,0,prepare.Operation);Check(Last().Action==3&&LocalPlayerUI.Ui.Cursor.Writes==0,"Changed unpublished cursor was applied");LocalPlayerUI.Ui.View.Close();
  Open();query=Last();Reply(query,4,Guid.Empty);LocalPlayerUI.Ui.View.Transfer(0,LocalPlayerUI.Ui.Cursor.CurrentStack);prepare=Last();LocalPlayerUI.Ui.Cursor.FaultAfterWrite=true;
  Reply(prepare,0,prepare.Operation);Check(player.Buffs.GetCustomVar("rbCamper_"+prepare.Operation.ToString("N"))==2,"Partial callback became acknowledged receipt1");
  packets=Wire.Sent.Count;LocalPlayerUI.Ui.View.Close();Check(Wire.Sent.Count==packets,"Partial cursor callback cancelled durable prepared receipt");
  Open();query=Last();LocalPlayerUI.Ui.Cursor.CurrentStack=new ItemStack(0);Reply(query,0,prepare.Operation);Check(LocalPlayerUI.Ui.Cursor.Writes==0&&Last().Action==2,"Recognized after-cursor recovery debited twice");
  Reply(query,2,prepare.Operation);LocalPlayerUI.Ui.View.Close();
  Open();query=Last();Reply(query,4,Guid.Empty);LocalPlayerUI.Ui.View.Transfer(0,LocalPlayerUI.Ui.Cursor.CurrentStack);prepare=Last();
  Reply(prepare,3,prepare.Operation);
  Check(LocalPlayerUI.Ui.Cursor.Writes==0&&GameManager.LastTooltip=="xuiRebirthCamperModuleTransferRefused","Ordinary refusal debited or reported uncertain custody");
  packets=Wire.Sent.Count;LocalPlayerUI.Ui.View.Transfer(1,LocalPlayerUI.Ui.Cursor.CurrentStack);
  Check(Wire.Sent.Count==packets+1&&Last().Operation!=prepare.Operation,"Unprepared refusal stranded Waiting or reused custody");
  prepare=Last();Reply(prepare,0,prepare.Operation);released=RebirthCamperModuleInteraction.Released;packets=Wire.Sent.Count;
  string publishedReceipt="rbCamper_"+prepare.Operation.ToString("N");Reply(prepare,5,prepare.Operation);
  Check(!LocalPlayerUI.Ui.windowManager.Opened&&RebirthCamperModuleInteraction.Released==released+1
   &&player.Buffs.GetCustomVar(publishedReceipt)==1&&Wire.Sent.Count==packets,"Published Error failed exact close or cleared/cancelled owner receipt");
  LocalPlayerUI.Ui.View.Transfer(2,LocalPlayerUI.Ui.Cursor.CurrentStack);
  Check(Wire.Sent.Count==packets&&LocalPlayerUI.Ui.Cursor.Writes==1,"Closed Error admitted another transfer or repeated debit");
  Reply(prepare,5,prepare.Operation);Check(RebirthCamperModuleInteraction.Released==released+1,"Duplicate Error released twice");
  Open();query=Last();released=RebirthCamperModuleInteraction.Released;Reply(prepare,5,prepare.Operation);
  Check(LocalPlayerUI.Ui.windowManager.Opened&&RebirthCamperModuleInteraction.Released==released&&LocalPlayerUI.Ui.Cursor.Writes==0,"Stale Error closed replacement native lease");LocalPlayerUI.Ui.View.Close();
  var gridCursor=new XUiC_DragAndDropWindow();int refreshes=0;
  Check(RebirthCamperRemoteManagement.BeginStationGrid(vehicle,player,gridCursor,new RebirthCamperTransferSnapshot(),Guid.NewGuid(),RebirthCamperRemoteStationGrid.Input,0,RebirthCamperRemoteStationGesture.Swap,new ItemStack(4),()=>true,s=>refreshes++,()=>{},out var gridClose),"Grid request refused");
  prepare=Last();Check(prepare.Action==4&&gridCursor.Writes==0,"Grid request debited before preparation");
  Check(!RebirthCamperRemoteManagement.BeginStationGrid(vehicle,player,gridCursor,new RebirthCamperTransferSnapshot(),Guid.NewGuid(),RebirthCamperRemoteStationGrid.Input,0,RebirthCamperRemoteStationGesture.Swap,new ItemStack(4),()=>true,s=>{},()=>{},out _),"Concurrent grid custody accepted");
  Reply(prepare,0,prepare.Operation);Check(gridCursor.Writes==1&&Last().Action==2,"Grid receipt failed shared cursor publication");
  Reply(prepare,2,prepare.Operation);Check(refreshes==1,"Applied grid did not update presentation once");
  packets=Wire.Sent.Count;gridClose();Reply(prepare,2,prepare.Operation);Check(refreshes==1&&Wire.Sent.Count==packets,"Closed grid replay repeated presentation/publication");
  int recoveries=0;gridCursor=new XUiC_DragAndDropWindow();
  Check(RebirthCamperRemoteManagement.BeginStationGrid(vehicle,player,gridCursor,new RebirthCamperTransferSnapshot(),Guid.NewGuid(),RebirthCamperRemoteStationGrid.Output,0,RebirthCamperRemoteStationGesture.Swap,new ItemStack(4),()=>true,s=>refreshes++,()=>recoveries++,out gridClose),"Recovery grid refused");
  prepare=Last();Reply(prepare,0,prepare.Operation);packets=Wire.Sent.Count;string gridReceipt="rbCamper_"+prepare.Operation.ToString("N");
  Reply(prepare,5,prepare.Operation);Reply(prepare,5,prepare.Operation);gridClose();Reply(prepare,2,prepare.Operation);
  Check(recoveries==1&&refreshes==1&&gridCursor.Writes==1&&Wire.Sent.Count==packets&&player.Buffs.GetCustomVar(gridReceipt)==1,"Grid Error replayed output, cancelled receipt, repeated recovery or projected success");
  var oldGridRequest=prepare;gridCursor=new XUiC_DragAndDropWindow();
  Check(RebirthCamperRemoteManagement.BeginStationGrid(vehicle,player,gridCursor,new RebirthCamperTransferSnapshot(),Guid.NewGuid(),RebirthCamperRemoteStationGrid.Input,0,RebirthCamperRemoteStationGesture.Swap,new ItemStack(4),()=>true,s=>refreshes++,()=>recoveries++,out gridClose),"Replacement grid refused");
  prepare=Last();Reply(oldGridRequest,5,oldGridRequest.Operation);Check(recoveries==1&&gridCursor.Writes==0,"Delayed grid Error affected replacement request");
  Reply(prepare,3,prepare.Operation);Check(refreshes==2&&gridCursor.Writes==0,"Grid rejection failed null module-view settlement");gridClose();
  Check(RebirthCamperRemoteManagement.BeginStationGrid(vehicle,player,gridCursor,new RebirthCamperTransferSnapshot(),Guid.NewGuid(),RebirthCamperRemoteStationGrid.Input,0,RebirthCamperRemoteStationGesture.Swap,new ItemStack(4),()=>true,s=>refreshes++,()=>{recoveries++;throw new InvalidOperationException("recovery callback fault");},out gridClose),"Fault recovery grid refused");
  prepare=Last();packets=Wire.Sent.Count;Reply(prepare,5,prepare.Operation);Reply(prepare,5,prepare.Operation);gridClose();
  Check(recoveries==2&&gridCursor.Writes==0&&Wire.Sent.Count==packets,"Recovery callback fault replayed/cancelled or retained a duplicate active request");
  Open();released=RebirthCamperModuleInteraction.Released;
  LocalPlayerUI.Ui.windowManager.Escape();
  Check(!LocalPlayerUI.Ui.windowManager.Opened&&RebirthCamperModuleInteraction.Released==released+1,"Escape did not release management native lease");
  LocalPlayerUI.Ui.windowManager.Escape();Check(RebirthCamperModuleInteraction.Released==released+1,"Repeated management Escape released twice");
 }
}
public static class CursorCheckExtensions {public static int countForCheck(this XUiC_DragAndDropWindow c)=>c.CurrentStack.count;}
'@
# Do not use method-group inference in the independent fixture.
$doubles=$doubles.Replace('  var D=RebirthCamperRemoteCursorPolicy.Decide;','')
Add-Type -TypeDefinition ($doubles+"`n"+$policy.Replace('using System;','')+"`n"+$client.Replace('using System;','').Replace('using System.Collections.Generic;',''))
[CamperRemoteCursorChecks]::Run()
Write-Output 'PASS: actual remote client adapter waits for prepare, debits once, ignores duplicate replies, preserves moved cursor, resumes receipts, rejects stale cursor, retains partial callbacks, retries ordinary refusal, closes published Error preserving receipt, retires grid Error without cancellation/replay, settles rejected grids and protects replacement native leases. Network/UI/native snapshot services are explicit doubles; no multiplayer gameplay acceptance.'
