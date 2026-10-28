// AUTHORED_NOT_RUN. Native UI/network/file/owner methods are adapters.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
class ItemValue {internal Dictionary<string,string> Metadata=new Dictionary<string,string>();internal ItemValue(int value){}internal void SetMetadata(string key,string value)=>Metadata[key]=value;}
class ItemStack {internal int count;internal bool IsEmpty()=>count==0;internal static ItemStack[] Clone(ItemStack[] values)=>values.Select(v=>new ItemStack{count=v.count}).ToArray();}
class TileEntityWorkstation {internal string Location="station";internal ItemStack[] Output=new[]{new ItemStack{count=3}};}
class StationData {internal TileEntityWorkstation TileEntity=new TileEntityWorkstation();}
class WindowGroup {internal bool isShowing=true;}
class PlayerUI {internal object entityPlayer;}
class XUi {internal PlayerUI playerUI=new PlayerUI();}
class XUiC_RebirthCookingStation {
    internal bool IsMilling;internal XUi xui=new XUi();internal WindowGroup windowGroup=new WindowGroup();internal StationData WorkstationData=new StationData();
    internal XUiC_WorkstationOutputGrid Grid=new XUiC_WorkstationOutputGrid();internal XUiC_CraftingQueue Queue=new XUiC_CraftingQueue();
    internal T GetChildByType<T>() where T:class=>typeof(T)==typeof(XUiC_WorkstationOutputGrid)?Grid as T:Queue as T;
}
class XUiC_WorkstationOutputGrid {internal ItemStack[] Slots=new[]{new ItemStack{count=3}};internal int Writes;internal bool Suppress;internal Action AfterWrite;internal void SetSlots(ItemStack[] values){Writes++;if(!Suppress)Slots=ItemStack.Clone(values);AfterWrite?.Invoke();}internal ItemStack[] GetSlots()=>Slots;}
class XUiC_RecipeStack {}
class XUiC_CraftingQueue {internal XUiC_RecipeStack[] Cards=new[]{new XUiC_RecipeStack()};internal XUiC_RecipeStack[] GetRecipesToCraft()=>Cards;}
class EntityPlayerLocal {internal World world;internal int entityId=1,Died=2;internal bool Dead,Spawned=true;internal string Owner="owner",Creation="creation";internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;}
class World {internal class State {internal string Guid=System.Guid.NewGuid().ToString("N");}internal State worldState=new State();internal EntityPlayerLocal Primary,Registered;internal bool Remote;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Registered;internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;internal int Saves;internal void SaveLocalPlayerData(){Saves++;}}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class Log {internal static void Warning(string text){}}
namespace UnityEngine {static class Time {internal static float realtimeSinceStartup;}}
static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayerLocal p)=>p.Creation;}
class RebirthStablePlayerIdentity {internal string CanonicalId;internal static bool TryResolveServerEntity(EntityPlayerLocal p,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{CanonicalId=p.Owner};return p.Owner!=null;}}
static class RebirthCookingStationLocator {internal static string Encode(TileEntityWorkstation station)=>station.Location;}
static class SingletonMonoBehaviour<T> {internal static T Instance;}
class ConnectionManager {internal bool Throw;internal int Sends;internal void SendToServer(object packet,bool reliable){if(Throw)throw new Exception();Sends++;}}
class NetPackagePlayerData {internal object Setup(EntityPlayerLocal p)=>this;}
static class NetPackageManager {internal static T GetPackage<T>() where T:new()=>new T();}
static class RebirthCookingCancellationProjection {internal static bool Allowed=true;internal static int Calls;internal static bool Refresh(XUiC_RecipeStack card,string location,string token,Func<bool> current){Calls++;return Allowed&&card!=null&&current();}}
static class RebirthCookingSessionService {
    internal static Action<string> Pending;internal static ItemValue Hint;internal static string RequestId;internal static int Requests;
    internal static void Request(EntityPlayerLocal player,string action,string recipe,string book,string magazine,ItemValue item,Action<string> reply){if(action!="recoverCookingHiddenOutput")throw new Exception();Requests++;RequestId=recipe;Hint=item;Pending=reply;}
}
static class RebirthCookingOwnerDelta {
    internal class Plan {internal Guid Operation=Guid.NewGuid();internal string World,Owner="owner",Creation="creation";internal int Deaths=2;}
    internal static Plan Decoded;internal static int Advances,Retirements,Abandons;internal static bool Retires=true,Rejects=true;
    internal static Plan Decode(string text)=>Decoded;
    internal static bool Advance(EntityPlayerLocal p,Plan plan,int phase,Func<bool> current,Func<bool> clear){if(!current()||!clear())throw new Exception();Advances++;return true;}
    internal static bool RetireAuthenticatedTerminal(EntityPlayerLocal p,Plan plan,Func<bool> current){Retirements++;return Retires&&current();}
    internal static bool AbandonRejectedWithoutEffect(EntityPlayerLocal p,Plan plan){Abandons++;return Rejects;}
}
// ACTUAL_CLIENT
internal static partial class RebirthCookingHiddenOutputRecovery {
    private static string Cells(ItemStack[] values)=>string.Join(",",values.Select(i=>i.count));
    private static bool Same(ItemStack[] a,ItemStack[] b)=>Cells(a)==Cells(b);
    internal static bool ServerHeld(TileEntityWorkstation station)=>false;
    static XUiC_RebirthCookingStation Setup(){
        clients.Clear();var world=new World();var player=new EntityPlayerLocal{world=world};world.Primary=world.Registered=player;GameManager.Instance.World=world;
        var window=new XUiC_RebirthCookingStation();window.xui.playerUI.entityPlayer=player;
        SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();UnityEngine.Time.realtimeSinceStartup=0;
        RebirthCookingOwnerDelta.Decoded=new RebirthCookingOwnerDelta.Plan{World=world.worldState.Guid};RebirthCookingOwnerDelta.Advances=RebirthCookingOwnerDelta.Retirements=RebirthCookingOwnerDelta.Abandons=0;
        RebirthCookingOwnerDelta.Retires=RebirthCookingOwnerDelta.Rejects=RebirthCookingCancellationProjection.Allowed=true;RebirthCookingCancellationProjection.Calls=0;
        RebirthCookingSessionService.Pending=null;RebirthCookingSessionService.Requests=0;Begin(window);return window;
    }
    static void Need(bool value,string label){if(!value)throw new Exception(label);}
    static void Tick(){UnityEngine.Time.realtimeSinceStartup+=2;TickClients();}
    static string Reply(string prefix,Guid parent)=>prefix+parent.ToString("N")+":"+Convert.ToBase64String(Encoding.UTF8.GetBytes("native-plan-adapter"));
    internal static void Exercise(){
        var window=Setup();var parent=Guid.NewGuid();Tick();RebirthCookingSessionService.Pending(Reply("hidden:0:",parent));
        Need(RebirthCookingOwnerDelta.Advances==1&&window.WorkstationData.TileEntity.Output[0].count==3&&window.Grid.Writes==0,"phase zero changed native output or UI");
        Tick();RebirthCookingSessionService.Pending(Reply("hidden-done:",parent));Need(clients[0].Retired&&RebirthCookingOwnerDelta.Retirements==1,"terminal not retained for projection");
        Tick();Need(window.Grid.Writes==0&&!clients[0].Projected,"nonempty native replica projected empty");
        window.WorkstationData.TileEntity.Output[0].count=0;window.Grid.Suppress=true;Tick();Need(!clients[0].Projected,"suppressed UI projection accepted");
        window.Grid.Suppress=false;Tick();Need(clients[0].Projected&&RebirthCookingSessionService.Hint.Metadata.ContainsKey("rebirth.cooking.hidden.ack"),"positive projection did not send ack");
        RebirthCookingSessionService.Pending(Reply("hidden-done:",parent));Need(RebirthCookingOwnerDelta.Retirements==1&&RebirthCookingOwnerDelta.Advances==1,"terminal polling replayed financial path");
        Tick();RebirthCookingSessionService.Pending("hidden-acked:"+parent.ToString("N"));Need(clients.Count==0,"positive acknowledgement retained hold");
        for(int variant=0;variant<8;variant++){
            window=Setup();Tick();var callback=RebirthCookingSessionService.Pending;var c=clients[0];
            switch(variant){case 0:c.Player.Died++;break;case 1:c.Player.Owner="foreign";break;case 2:c.Player.Creation="foreign";break;case 3:c.Player.Dead=true;break;case 4:c.World.Registered=new EntityPlayerLocal();break;case 5:GameManager.Instance.World=new World();break;case 6:RebirthCookingOwnerDelta.Decoded.Owner="foreign";break;case 7:RebirthCookingOwnerDelta.Decoded.World=Guid.NewGuid().ToString("N");break;}
            callback(Reply("hidden:1:",Guid.NewGuid()));Need(RebirthCookingOwnerDelta.Advances==0&&RebirthCookingOwnerDelta.Retirements==0&&window.Grid.Writes==0,"stale callback reached owner/UI "+variant);
        }
        window=Setup();Tick();RebirthCookingSessionService.Pending("hidden-empty:"+RebirthCookingSessionService.RequestId);window.WorkstationData.TileEntity.Output[0].count=0;Tick();Need(clients.Count==0&&RebirthCookingOwnerDelta.Advances==0,"empty response granted items or kept hold");
        window=Setup();Tick();RebirthCookingSessionService.Pending(Reply("hidden:0:",parent));var old=clients[0].Plan;RebirthCookingOwnerDelta.Rejects=false;Tick();RebirthCookingSessionService.Pending(Reply("hidden-retry:",parent));Need(ReferenceEquals(clients[0].Plan,old),"unsaved rejection forgot original");
        RebirthCookingOwnerDelta.Rejects=true;Tick();RebirthCookingSessionService.Pending(Reply("hidden-retry:",parent));Need(clients[0].Plan==null&&clients[0].Parent==parent,"positive rejection lost source original");
        window=Setup();clients[0].World.Remote=true;SingletonMonoBehaviour<ConnectionManager>.Instance.Throw=true;Tick();Need(!clients[0].Waiting&&clients.Count==1,"send exception stranded waiting state");
        window=Setup();Tick();RebirthCookingOwnerDelta.Decoded.Deaths=1;RebirthCookingSessionService.Pending(Reply("hidden:1:",parent));Need(RebirthCookingOwnerDelta.Advances==0,"earlier life reached credit");
        Tick();RebirthCookingSessionService.Pending(Reply("hidden-done:",parent));window.windowGroup.isShowing=false;Tick();Need(clients[0].Projected&&window.Grid.Writes==0,"closed window mutated presentation");
        window=Setup();Tick();var late=RebirthCookingSessionService.Pending;Tick();Need(RebirthCookingSessionService.Requests==1,"waiting request was dispatched twice");
        clients[0].Player.Died++;Tick();late(Reply("hidden-done:",parent));Need(clients.Count==0&&RebirthCookingOwnerDelta.Retirements==0,"removed old-life runner accepted late terminal");
        window=Setup();Tick();RebirthCookingSessionService.Pending(Reply("hidden:0:",parent));Tick();RebirthCookingSessionService.Pending(Reply("hidden-done:",Guid.NewGuid()));Need(!clients[0].Retired&&RebirthCookingOwnerDelta.Retirements==0,"foreign parent terminal retired original");
        window=Setup();Tick();RebirthCookingOwnerDelta.Retires=false;RebirthCookingSessionService.Pending(Reply("hidden-done:",parent));Need(!clients[0].Retired&&window.Grid.Writes==0,"unsaved retirement reached presentation");
        RebirthCookingOwnerDelta.Retires=true;Tick();RebirthCookingSessionService.Pending(Reply("hidden-done:",parent));window.WorkstationData.TileEntity.Output[0].count=0;RebirthCookingCancellationProjection.Allowed=false;Tick();Need(!clients[0].Projected&&window.Grid.Writes==0,"failed native queue projection reached output grid");
        RebirthCookingCancellationProjection.Allowed=true;window.Grid.AfterWrite=()=>clients[0].Player.Died++;Tick();Need(!clients[0].Projected,"life change during presentation acknowledged stale original");
        Console.WriteLine("MP042 actual hidden-output client lifecycle composition; native owner, projection and network are adapters");
    }
}
static class Program {public static void Main()=>RebirthCookingHiddenOutputRecovery.Exercise();}
