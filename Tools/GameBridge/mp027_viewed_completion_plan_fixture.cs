// AUTHORED_NOT_RUN. Entire actual PlacedPlan partial. Native item insertion,
// lease, identity, rules and tools are adapters; no native/topology PASS claim.
using System;
using System.Collections.Generic;
using System.Linq;
class ItemValue{internal int type=1;internal ItemValue(int type=1,bool native=false){this.type=type;}}
class ItemStack{
    internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue value,int n){itemValue=value;count=n;}
    internal ItemStack Clone()=>new ItemStack(new ItemValue(itemValue.type),count);
    internal static ItemStack[] Clone(ItemStack[] values)=>values.Select(value=>value.Clone()).ToArray();
    internal static int AddToItemStackArray(ItemStack[] values,ItemStack offer){if(!Model.Room)return -1;values[0].count+=offer.count;return 0;}
}
class Recipe{internal int itemValueType=4,count=3;internal Guid Group;}
class RecipeQueueItem{internal Recipe Recipe;internal float CraftingTimeLeft=2;internal int StartingEntityId;}
class Block{internal string GetBlockName()=>"recycler";}
class TileEntityWorkstation{
    internal enum Module{Fuel}
    internal Block block=new Block();internal RecipeQueueItem[] Queue;internal ItemStack[] Input=new[]{new ItemStack(new ItemValue(2),7)},Output=new[]{new ItemStack(new ItemValue(4),0)},Tools=new[]{new ItemStack(new ItemValue(3),1)};
    internal bool[] isModuleUsed=new[]{false};internal bool IsBurning=true;internal int ToWorldPos()=>1;
}
class RebirthCamperStationState:TileEntityWorkstation{}
class EntityPlayer{internal World world;internal int entityId;internal string Owner,Creation="creation";internal bool Dead,Spawned=true,Lease;internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;}
class EntityPlayerLocal:EntityPlayer{}
class World{internal bool Remote;internal TileEntityWorkstation Station;internal class Roster{internal List<EntityPlayer> list=new List<EntityPlayer>();}internal Roster Players=new Roster();internal Dictionary<int,EntityPlayer> Registered=new Dictionary<int,EntityPlayer>();internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Registered.TryGetValue(id,out var player)?player:null;internal object GetTileEntity(int position)=>Station;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer actor)=>actor.Creation;}
class Rule{internal int[] RequiredTools=new[]{0};}
static class RebirthRecyclerRules{internal static bool TryResolve(int type,string name,out Rule rule){rule=new Rule();return Model.Rule;}internal static bool ToolsHealthy(ItemStack[] tools,Rule rule,out int a,out int b){a=b=0;return Model.Healthy;}}
static class RebirthRecyclerBonuses{
    internal static float WearRatePerSecond(Block block)=>1;
    internal static float MaxProcessingSeconds(ItemStack[] tools,int[] required,float rate)=>Model.MaxWork;
    internal static List<ItemStack> CompletionRewards(ItemValue original,Guid group)=>new List<ItemStack>{new ItemStack(new ItemValue(5),2)};
    internal static ItemStack[] ApplyWear(ItemStack[] tools,int[] required,float rate,float seconds)=>ItemStack.Clone(tools);
}
static class Model{internal static bool Room=true,Rule=true,Healthy=true,Valid=true;internal static float MaxWork=2;}
public static partial class RebirthRecyclerRuntime{
    private sealed class Group{internal Guid Id;internal string Owner="recipient",Creation="creation";internal ItemValue Original=new ItemValue(1);}
    private static Group group;
    private static bool Access(EntityPlayer actor,TileEntityWorkstation station,Func<bool> current)=>actor!=null&&actor.Lease&&current();
    private static bool TryCancel(EntityPlayer actor,TileEntityWorkstation station,Guid op,Func<bool> current,out string reason){reason=null;return false;}
    private static bool TryQueue(EntityPlayer actor,TileEntityWorkstation station,int slot,int units,Guid op,Func<bool> current,out string reason){reason=null;return false;}
    private static bool ValidRow(TileEntityWorkstation station,RecipeQueueItem row)=>Model.Valid&&row?.Recipe!=null;
    private static bool Read(Recipe recipe,out Group result){result=group;return recipe!=null&&recipe.Group==group.Id;}
    internal static bool TryGroup(Recipe recipe,out Guid result){result=recipe?.Group??Guid.Empty;return result!=Guid.Empty;}
    private static string Identity(EntityPlayer actor)=>actor.Owner;
    private static Guid CompletionOperation(Guid value)=>value;
    private static void Need(bool value,string text){if(!value)throw new Exception(text);}
    internal static void Exercise(){
        var world=new World();var station=new TileEntityWorkstation();world.Station=station;GameManager.Instance.World=world;
        group=new Group{Id=Guid.NewGuid()};station.Queue=new[]{new RecipeQueueItem{Recipe=new Recipe{Group=group.Id}}};
        var viewer=new EntityPlayerLocal{world=world,entityId=1,Owner="viewer",Lease=true};var recipient=new EntityPlayer{world=world,entityId=2,Owner="recipient",Lease=false};
        world.Registered[1]=viewer;world.Registered[2]=recipient;world.Players.list.Add(viewer);world.Players.list.Add(recipient);
        Need(TryPlanPlacedCompletionForViewer(viewer,recipient,station,2,out var plan)&&ReferenceEquals(plan.Actor,recipient)&&ReferenceEquals(plan.Viewer,viewer),"foreign viewer/recipient roles conflated");
        Need(plan.Operation==group.Id&&plan.RecipientOwner=="recipient"&&plan.RecipientCreation=="creation"&&plan.Returns[0].itemValue.type==5&&plan.Returns[0].count==2,"original reward identity changed");
        Need(plan.Output[0].count==3&&plan.Queue[0].Recipe==null&&station.Output[0].count==0&&station.Queue[0].Recipe!=null&&station.Input[0].count==7,"detached plan changed native station");
        Need(ReferenceEquals(FindPlacedCompletionRecipient(world,station),recipient),"host failed original queued recipient lookup");
        viewer.Lease=false;recipient.Lease=true;Need(!TryPlanPlacedCompletionForViewer(viewer,recipient,station,2,out plan),"recipient station access substituted for viewer access");viewer.Lease=true;recipient.Lease=false;
        recipient.Creation="other";Need(!TryPlanPlacedCompletionForViewer(viewer,recipient,station,2,out plan)&&FindPlacedCompletionRecipient(world,station)==null,"wrong creation received reward");recipient.Creation="creation";
        world.Registered[2]=new EntityPlayer();Need(!TryPlanPlacedCompletionForViewer(viewer,recipient,station,2,out plan)&&FindPlacedCompletionRecipient(world,station)==null,"unregistered recipient accepted");world.Registered[2]=recipient;
        recipient.Dead=true;Need(!TryPlanPlacedCompletionForViewer(viewer,recipient,station,2,out plan),"dead recipient planned");recipient.Dead=false;
        world.Remote=true;world.Registered.Remove(2);world.Players.list.Remove(recipient);
        Need(TryPlanPlacedViewerCompletion(viewer,station,2,out plan)&&plan.Actor==null&&ReferenceEquals(plan.Viewer,viewer)&&plan.RecipientOwner=="recipient","viewer required distant recipient avatar");
        Need(FindPlacedCompletionRecipient(world,station)==null,"client resolved authoritative recipient");world.Remote=false;world.Registered[2]=recipient;world.Players.list.Add(recipient);
        Need(!TryPlanPlacedViewerCompletion(viewer,station,2,out plan),"host admitted recipientless client planner");
        foreach(float seconds in new[]{-1f,1f,3f,float.NaN,float.PositiveInfinity})Need(!TryPlanPlacedCompletionForViewer(viewer,recipient,station,seconds,out plan),"invalid completion work accepted "+seconds);
        Model.Room=false;Need(!TryPlanPlacedCompletionForViewer(viewer,recipient,station,2,out plan),"full native output accepted");Model.Room=true;
        Model.Healthy=false;Need(!TryPlanPlacedCompletionForViewer(viewer,recipient,station,2,out plan),"unhealthy tools accepted");Model.Healthy=true;
        Model.MaxWork=1;Need(!TryPlanPlacedCompletionForViewer(viewer,recipient,station,2,out plan),"tool lifetime exceeded");Model.MaxWork=2;
        station.Queue=new[]{station.Queue[0],new RecipeQueueItem{Recipe=new Recipe{Group=group.Id}}};Need(!TryPlanPlacedCompletionForViewer(viewer,recipient,station,2,out plan),"nonfinal sibling awarded completion bonus");
        station.Queue=new[]{station.Queue[0]};viewer.Owner="recipient";Need(TryPlanPlacedCompletion(viewer,station,2,out plan)&&ReferenceEquals(plan.Actor,viewer)&&ReferenceEquals(plan.Viewer,viewer),"same owner completion changed");
        Console.WriteLine("MP027 full actual completion planner: ordinary role, distant recipient, source and work rules; native boundaries are adapters");
    }
}
// ACTUAL_PLANNER
static class Program{public static void Main()=>RebirthRecyclerRuntime.Exercise();}
