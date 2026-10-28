// Complete actual native-card projection; codecs and native UI setters are
// explicit adapters. AUTHORED_NOT_RUN, no inventory/grant interface exists here.
using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
namespace HarmonyLib {class HarmonyPriority:Attribute {internal HarmonyPriority(int p){}}static class Priority {internal const int First=1;}}
class EntityPlayerLocal {internal World world;internal int entityId=1;internal bool Dead,Spawned=true;internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;}
class World {internal EntityPlayerLocal Player,Registered;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal object GetEntity(int id)=>Registered;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
class XUiM_Workstation {internal TileEntityWorkstation TileEntity;}
static class RebirthCookingRemoteCancellation {internal static bool Held;internal static bool PresentationHeld(TileEntityWorkstation s)=>Held;}
static class RebirthCookingHiddenOutputRecovery {internal static bool Hidden;internal static bool Held(TileEntityWorkstation station)=>Hidden;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
class ItemValue {internal string Token;internal bool TryGetMetadata(string key,out string token){token=Token;return token!=null;}}
class ItemStack {internal ItemValue itemValue=new ItemValue();}
class Recipe {internal ItemStack[] ingredients={new ItemStack()};}
class RecipeQueueItem {internal Recipe Recipe;internal short Multiplier;internal byte Quality;internal float CraftingTimeLeft,OneItemCraftTime;internal int StartingEntityId,AmountToRepair;internal object RepairItem;}
class TileEntityWorkstation {internal RecipeQueueItem[] Queue;internal string Location="station";}
class WorkstationData {internal TileEntityWorkstation TileEntity;}
class GuiWindow {internal bool isShowing=true;}
class XUiC_CraftingQueue {internal XUiC_RecipeStack[] Cards;internal XUiC_RecipeStack[] GetRecipesToCraft()=>Cards;}
class XUiC_WorkstationWindowGroup {internal GuiWindow windowGroup=new GuiWindow();internal WorkstationData WorkstationData=new WorkstationData();internal XUiC_CraftingQueue Queue=new XUiC_CraftingQueue();internal T GetChildByType<T>() where T:class=>Queue as T;}
class XUiC_RecipeStack
{
    internal XUiC_WorkstationWindowGroup Window;internal bool Accept=true,IsCrafting=true,IsDirty;internal int Sets;internal Action OnSet;internal Recipe Recipe;
    internal T GetParentByType<T>() where T:class=>Window as T;
    internal bool SetRecipe(Recipe recipe,int batches,float remaining,bool server,int owner,int quality,float one){Sets++;if(!Accept)return false;Recipe=recipe;OnSet?.Invoke();return true;}
}
static class RebirthCookingStationLocator {internal static string Encode(TileEntityWorkstation station)=>station.Location;}
static class RebirthCookingHeat {internal static bool Managed(Recipe r)=>r!=null;}
static class RebirthCookingRemoteAdmissionBackend
{
    internal static bool NativeHeld;internal static bool Held(TileEntityWorkstation s)=>NativeHeld;
    private static Dictionary<string,RecipeQueueItem[]> frames=new Dictionary<string,RecipeQueueItem[]>();
    internal static string Queue(RecipeQueueItem[] rows){string key=string.Join("|",rows.Select(r=>r==null?"null":(r.Recipe?.ingredients[0].itemValue.Token??"empty")+":"+r.Multiplier+":"+r.Quality+":"+r.AmountToRepair+":"+(r.RepairItem==null?0:1)));frames[key]=rows;return key;}
    internal static RecipeQueueItem[] ReadQueue(string key)=>frames[key].Select(r=>r==null?null:new RecipeQueueItem{Recipe=r.Recipe,Multiplier=r.Multiplier,Quality=r.Quality,RepairItem=r.RepairItem,AmountToRepair=r.AmountToRepair}).ToArray();
}
// ACTUAL_PROJECTION
// ACTUAL_QUEUE_GUARD
static class Program
{
    static void Need(bool b,string why){if(!b)throw new Exception(why);}
    public static void Main()
    {
        for(int variant=0;variant<9;variant++)
        {
            var station=new TileEntityWorkstation{Queue=new[]{new RecipeQueueItem{Recipe=null}}};var window=new XUiC_WorkstationWindowGroup();window.WorkstationData.TileEntity=station;var source=new XUiC_RecipeStack{Window=window};window.Queue.Cards=new[]{source};string token=Guid.NewGuid().ToString("N");bool valid=true;
            switch(variant){case 1:station.Queue[0].Recipe=new Recipe();station.Queue[0].Recipe.ingredients[0].itemValue.Token=token;break;case 2:window.windowGroup.isShowing=false;break;case 3:source.Accept=false;break;
                case 4:source.OnSet=()=>valid=false;break;case 5:station.Location="other";break;case 6:station.Queue[0].RepairItem=new object();break;case 7:source.OnSet=()=>station.Queue[0].Multiplier++;break;case 8:source.OnSet=()=>source.Window=new XUiC_WorkstationWindowGroup();break;}
            bool result=RebirthCookingCancellationProjection.Refresh(source,"station",token,()=>valid);Need(result==(variant==0||variant==2),"native cancellation projection scope/result incorrect "+variant);
            if(variant==0)Need(source.Sets==1&&source.IsDirty&&!source.IsCrafting&&source.Recipe==null,"native card not projected after saved removal");
            if(variant==1||variant==2||variant==5||variant==6)Need(source.Sets==0,"ineligible/closed/replacement card mutated");
        }
        for(int variant=0;variant<7;variant++)
        {
            var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=world.Registered=player;GameManager.Instance.World=world;RebirthCookingRemoteAdmissionBackend.NativeHeld=RebirthCookingRemoteCancellation.Held=false;
            var model=new XUiM_Workstation{TileEntity=new TileEntityWorkstation{Queue=new[]{new RecipeQueueItem()}}};RecipeQueueItem[] incoming={new RecipeQueueItem{Recipe=new Recipe()}};
            switch(variant){case 1:RebirthCookingRemoteAdmissionBackend.NativeHeld=true;break;case 2:RebirthCookingRemoteCancellation.Held=true;break;case 3:player.Dead=true;break;case 4:player.Spawned=false;break;case 5:world.Registered=new EntityPlayerLocal();break;case 6:player.Dead=true;incoming=null;break;}
            Need(RebirthCookingCancellationQueueWrite.Invoke(model,incoming)==(variant==0||variant==6),"managed queue write custody/scope incorrect "+variant);
        }
        RebirthCookingHiddenOutputRecovery.Hidden=true;
        Need(!RebirthCookingCancellationQueueWrite.Invoke(new XUiM_Workstation{TileEntity=new TileEntityWorkstation{Queue=new[]{new RecipeQueueItem()}}},null),"hidden output custody must also hold unmanaged queue writeback");
        Console.WriteLine("MP042 actual cancellation card projection and queue guard: PASS (native codec/setters are adapters)");
    }
}
