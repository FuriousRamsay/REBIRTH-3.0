using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthRecyclerRuntime
{
    // Detached validation only. This runs the same original action planner on
    // the authentic native owner body; it never invokes its delivery/setters.
    // In particular it is not server execution of a remote player's inventory.
    internal sealed class PlacedActionPlan
    {
        internal EntityPlayer Actor,Viewer;internal TileEntityWorkstation Station;internal Guid Operation;internal string Purpose,RecipientOwner,RecipientCreation;
        internal ItemStack[] Input,Output,Tools,Returns;internal RecipeQueueItem[] Queue;internal bool Cycle;
    }
    private sealed class PlacedPlanning
    {internal EntityPlayer Actor;internal TileEntityWorkstation Station;internal PlacedActionPlan Result;}
    [ThreadStatic]private static PlacedPlanning placedPlanning;
    internal static bool HasPlacedNativeAccess(EntityPlayer actor,TileEntityWorkstation station)=>Access(actor,station,()=>true);
    internal static bool TryPlanPlacedAction(EntityPlayer actor,TileEntityWorkstation station,int inputSlot,int units,Guid operation,bool cancel,
        Func<bool> current,out PlacedActionPlan plan)
    {
        plan=null;
        if(placedPlanning!=null||actor==null||actor.world?.IsRemote()!=false||station==null||station is RebirthCamperStationState||
            operation==Guid.Empty||!Access(actor,station,current))return false;
        var scope=new PlacedPlanning{Actor=actor,Station=station};placedPlanning=scope;
        try
        {
            bool planned=cancel?TryCancel(actor,station,operation,current,out _):TryQueue(actor,station,inputSlot,units,operation,current,out _);
            if(!planned||scope.Result==null||current?.Invoke()!=true)return false;
            plan=scope.Result;return true;
        }
        catch{return false;}
        finally{scope.Actor=null;scope.Station=null;scope.Result=null;placedPlanning=null;}
    }
    private static bool CapturePlacedActionPlan(EntityPlayer actor,TileEntityWorkstation station,Guid operation,string purpose,
        ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> returns,ItemStack[] output,ItemStack[] tools,bool cycle)
    {
        var scope=placedPlanning;
        if(scope==null||scope.Result!=null||!ReferenceEquals(scope.Actor,actor)||!ReferenceEquals(scope.Station,station))return false;
        scope.Result=new PlacedActionPlan{Actor=actor,Viewer=actor,Station=station,Operation=operation,Purpose=purpose,Input=ItemStack.Clone(input),
            Queue=(RecipeQueueItem[])queue.Clone(),Returns=returns.Select(value=>value.Clone()).ToArray(),Output=ItemStack.Clone(output),Tools=ItemStack.Clone(tools),Cycle=cycle};
        return true;
    }
    internal static bool TryPlanPlacedCompletion(EntityPlayer actor,TileEntityWorkstation station,float seconds,out PlacedActionPlan plan)
        =>TryPlanPlacedCompletionForViewer(actor,actor,station,seconds,out plan);

    // Station permission belongs to its actual viewer. Queue ownership and
    // rewards belong to the original recipient; neither authorizes the other.
    // This planner only reads station/identity state, never recipient inventory.
    internal static bool TryPlanPlacedCompletionForViewer(EntityPlayer viewer,EntityPlayer recipient,TileEntityWorkstation station,float seconds,out PlacedActionPlan plan)
        =>TryPlanPlacedCompletionCore(viewer,recipient,station,seconds,out plan);
    internal static bool TryPlanPlacedViewerCompletion(EntityPlayerLocal viewer,TileEntityWorkstation station,float seconds,out PlacedActionPlan plan)
    {
        plan=null;
        return viewer?.world?.IsRemote()==true&&TryPlanPlacedCompletionCore(viewer,null,station,seconds,out plan);
    }
    internal static EntityPlayer FindPlacedCompletionRecipient(World world,TileEntityWorkstation station)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||
            station==null||!ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)||!Read(station.Queue?.LastOrDefault()?.Recipe,out var group))return null;
        return world.Players?.list?.FirstOrDefault(candidate=>candidate!=null&&candidate.IsSpawned()&&!candidate.IsDead()&&ReferenceEquals(candidate.world,world)&&
            ReferenceEquals(world.GetEntity(candidate.entityId),candidate)&&Identity(candidate)==group.Owner&&RebirthCamperCharacterAccess.CurrentCreation(candidate)==group.Creation);
    }
    private static bool TryPlanPlacedCompletionCore(EntityPlayer viewer,EntityPlayer recipient,TileEntityWorkstation station,float seconds,out PlacedActionPlan plan)
    {
        plan=null;
        if(viewer==null||recipient==null&&viewer.world?.IsRemote()!=true||recipient!=null&&(!recipient.IsSpawned()||recipient.IsDead()||!ReferenceEquals(viewer.world,recipient.world)||
            !ReferenceEquals(recipient.world?.GetEntity(recipient.entityId),recipient))||station==null||station is RebirthCamperStationState||
            !HasPlacedNativeAccess(viewer,station)||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)return false;
        var row=station.Queue?.LastOrDefault();
        if(!ValidRow(station,row)||!Read(row.Recipe,out var group)||recipient!=null&&(group.Owner!=Identity(recipient)||group.Creation!=RebirthCamperCharacterAccess.CurrentCreation(recipient))||
            !RebirthRecyclerRules.TryResolve(group.Original.type,station.block.GetBlockName(),out var rule)||
            station.isModuleUsed[(int)TileEntityWorkstation.Module.Fuel]&&!station.IsBurning)return false;
        float rate=RebirthRecyclerBonuses.WearRatePerSecond(station.block);
        if(seconds>Math.Max(0,row.CraftingTimeLeft)||row.CraftingTimeLeft-seconds>.0001f||
            seconds>RebirthRecyclerBonuses.MaxProcessingSeconds(station.Tools,rule.RequiredTools,rate)||
            row.CraftingTimeLeft>0&&!RebirthRecyclerRules.ToolsHealthy(station.Tools,rule,out _,out _))return false;
        var output=ItemStack.Clone(station.Output);
        if(ItemStack.AddToItemStackArray(output,new ItemStack(new ItemValue(row.Recipe.itemValueType,false),row.Recipe.count))==-1)return false;
        var queue=(RecipeQueueItem[])station.Queue.Clone();queue[queue.Length-1]=new RecipeQueueItem{StartingEntityId=-1};
        if(queue.Any(sibling=>TryGroup(sibling?.Recipe,out var id)&&id==group.Id))return false;
        var returns=RebirthRecyclerBonuses.CompletionRewards(group.Original,group.Id);
        if(returns.Count==0)return false; // Station-only output belongs to its native processing source.
        plan=new PlacedActionPlan{Actor=recipient,Viewer=viewer,RecipientOwner=group.Owner,RecipientCreation=group.Creation,Station=station,Operation=CompletionOperation(group.Id),Purpose="complete",Input=ItemStack.Clone(station.Input),
            Queue=queue,Output=output,Tools=RebirthRecyclerBonuses.ApplyWear(station.Tools,rule.RequiredTools,rate,seconds),Returns=returns.ToArray(),Cycle=true};return true;
    }
}
