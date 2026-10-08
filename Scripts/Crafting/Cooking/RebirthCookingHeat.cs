using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

#nullable disable
/// <summary>One persistent, manually collected batch. Stored with the native station queue for save/network fidelity.</summary>
public static class RebirthCookingHeat
{
    private const string Prefix="rebirth.cooking.queue.";
    public static bool Managed(Recipe r)=>RebirthCookingBatch.IsBatch(r)&&r.ingredients[0].itemValue.HasMetadata(Prefix+"held");
    public static float Number(Recipe r,string key){if(Managed(r)&&r.ingredients[0].itemValue.TryGetMetadata(Prefix+key,out float n))return n;return 0;}
    public static string Text(Recipe r,string key){if(Managed(r)&&r.ingredients[0].itemValue.TryGetMetadata(Prefix+key,out string s))return s;return "";}
    public static string Position(Vector3i p)=>p.x+","+p.y+","+p.z;
    public static void Mark(Recipe r,int portions,bool hidden,string method,string previewIcon="")
    {
        
        var v=r.ingredients[0].itemValue;
        v.SetMetadata(Prefix+"held",1);v.SetMetadata(Prefix+"duration",r.craftingTime*portions);
        v.SetMetadata(Prefix+"elapsed",0f);v.SetMetadata(Prefix+"overdue",0f);
        v.SetMetadata(Prefix+"hidden",hidden?1f:0f);v.SetMetadata(Prefix+"method",method);
        v.SetMetadata(Prefix+"preview",previewIcon);
    }
    public static bool Ready(Recipe r)=>Managed(r)&&Number(r,"elapsed")>=Number(r,"duration");
    public static bool Burnt(Recipe r)=>Managed(r)&&Number(r,"overdue")>=RebirthCookingHeatRules.BurnAfter(Text(r,"method"));
    public static float Remaining(Recipe r)=>Math.Max(0,Number(r,"duration")-Number(r,"elapsed"));
    public static string Name(Recipe r)=>Number(r,"hidden")>0&&!Ready(r)?r.craftingArea=="WorkbenchMortarPestle001_FR"?Localization.Get("xuiRebirthStationUnfamiliarRecipe"):"Unfamiliar dish":Localization.Get(r.GetName());
    public static string Icon(Recipe r)
    {
        if(Number(r,"hidden")>0&&!Ready(r))return string.IsNullOrEmpty(Text(r,"preview"))?"rebirthImprovised"+Text(r,"method")+"01":Text(r,"preview");
        return Burnt(r)?RebirthCookingBurntIcons.Icon(r.GetName()):r.GetIcon();
    }
    public static string Status(Recipe r,bool heated)=>Burnt(r)?"BURNT":!Ready(r)?heated||!RebirthCookingBatch.NeedsHeat(r)?r.craftingArea=="WorkbenchMortarPestle001_FR"?Localization.Get("xuiRebirthStationProcessing"):"COOKING":"PAUSED — HEAT OFF":Number(r,"overdue")>=RebirthCookingHeatRules.Grace?"OVERCOOKING — TAKE NOW":"READY TO TAKE";
    public static string Timer(Recipe r)
    {
        if(Ready(r)&&!RebirthCookingBatch.NeedsHeat(r))return "Ready";
        float seconds=!Ready(r)?Remaining(r):Burnt(r)?0:Number(r,"overdue")<RebirthCookingHeatRules.Grace?RebirthCookingHeatRules.Grace-Number(r,"overdue"):RebirthCookingHeatRules.BurnAfter(Text(r,"method"))-Number(r,"overdue");
        string clock=FormatDuration(seconds);
        return clock+(!Ready(r)?"":Burnt(r)?"":" to "+(Number(r,"overdue")<RebirthCookingHeatRules.Grace?"overcooking":"burning"));
    }
    public static string FormatDuration(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) seconds = 0;
        var time = TimeSpan.FromSeconds(Math.Ceiling(seconds));
        return time.TotalHours >= 1 ? ((long)time.TotalHours).ToString(System.Globalization.CultureInfo.InvariantCulture) + time.ToString(@"\:mm\:ss") : time.ToString(@"mm\:ss");
    }
    public static void Advance(Recipe r,float delta,bool heated)
    {
        if(!Managed(r)||delta<=0||Burnt(r))return;
        bool cold=!RebirthCookingBatch.NeedsHeat(r);
        if(!heated&&!cold)return;
        float elapsed=Number(r,"elapsed"),duration=Number(r,"duration"),overdue=Number(r,"overdue");
        RebirthCookingHeatRules.Advance(duration,ref elapsed,ref overdue,delta,heated,cold,Text(r,"method"));
        var marker=r.ingredients[0].itemValue;
        marker.SetMetadata(Prefix+"elapsed",elapsed);
        marker.SetMetadata(Prefix+"overdue",overdue);
    }
    public static void TickUi(XUiC_RecipeStack stack,float dt)
    {
        var station=stack.windowGroup.Controller as XUiC_RebirthCookingStation;
        if(station==null)return;
        bool heated=station.WorkstationData.GetIsBurning()&&station.WorkstationData.GetTotalBurnTimeLeft()>0;
        bool wasReady=Ready(stack.recipe);
        Advance(stack.recipe,dt,heated);stack.craftingTimeLeft=Remaining(stack.recipe);
        if(!wasReady&&Ready(stack.recipe))
        {
            if (RebirthDiagnosticPolicy.MayPrepare(RebirthLogSettings.CraftingUiLoggingEnabled)) Log.Out("[REBIRTH Cooking] Food ready; overcooking at "+RebirthCookingHeatRules.Grace+"s, burnt at "+RebirthCookingHeatRules.BurnAfter(Text(stack.recipe,"method"))+"s after completion.");
            
        }
        if(Ready(stack.recipe)&&Number(stack.recipe,"reported")==0)
        {
            ReportReady(station.WorkstationData.TileEntity,stack.recipe,stack.recipeCount,stack.startingEntityId);
            var receipt=RebirthCookingBatch.Receipt(stack.recipe);receipt.SetMetadata("rebirth.cooking.completed",stack.recipeCount);
            RebirthCookingSessionService.Request(stack.xui.playerUI.entityPlayer,"complete",stack.recipe.GetName(),book:stack.startingEntityId.ToString(),item:receipt,count:stack.recipe.count);
        }
        if(Burnt(stack.recipe)&&station.WorkstationData.GetIsBurning()){station.fuelWindow?.TurnOff();station.WorkstationData.TileEntity.IsBurning=false;}
    }
    public static bool TickTile(TileEntityWorkstation tile,float dt)
    {
        var entry=tile.Queue?.LastOrDefault();
        if(entry==null||!Managed(entry.Recipe))return true;
        if(!tile.bUserAccessing)
        {
            if(!RebirthCookingBatch.NeedsHeat(entry.Recipe))dt=(float)((GameTimer.Instance.ticks-tile.lastTickTime)/20.0);
            Advance(entry.Recipe,dt,tile.IsBurning);entry.CraftingTimeLeft=Remaining(entry.Recipe);
            if(Ready(entry.Recipe))ReportReady(tile,entry.Recipe,entry.Multiplier,entry.StartingEntityId);
            if(Burnt(entry.Recipe))tile.IsBurning=false;
            tile.setModified();
        }
        return false;
    }
    private static void ReportReady(TileEntityWorkstation tile,Recipe recipe,int portions,int owner)
    {
        if(Number(recipe,"reported")>0)return;
        recipe.ingredients[0].itemValue.SetMetadata(Prefix+"reported",1f);
        var receipt=RebirthCookingBatch.Receipt(recipe);receipt.SetMetadata("rebirth.cooking.completed",portions);
        if(tile.CraftCompleteList==null)tile.CraftCompleteList=new System.Collections.Generic.List<CraftCompleteData>();
        tile.CraftCompleteList.Add(new CraftCompleteData(owner,new ItemStack(receipt,recipe.count),recipe.GetName(),"",0,1));
        if (GameManager.Instance?.World != null && !GameManager.Instance.World.IsRemote())
            RebirthCookingBatch.RecordCompletionWitness(receipt, portions, recipe);
        tile.setModified();
    }
    public static ItemValue Output(Recipe recipe)
    {
        var value=RebirthCookingBatch.Preview(recipe);
        value.SetMetadata("rebirth.cooking.source",recipe.GetName());
        foreach(string stat in new[]{"nutrition","water","comfort","energy"})
            if(value.TryGetMetadata("rebirth.cooking."+stat,out float n))value.SetMetadata("rebirth.cooking."+stat,RebirthCookingHeatRules.FoodStat(stat,n,Number(recipe,"overdue"),Text(recipe,"method")));
        if(!Burnt(recipe)&&Number(recipe,"overdue")>=RebirthCookingHeatRules.Grace&&RebirthCookingBatch.NeedsHeat(recipe)&&Ready(recipe))
            value.SetMetadata("rebirth.cooking.quality","Overcooked");
        if(Burnt(recipe))
        {
            var burned=ItemClass.GetItem(RebirthCookingBurntIcons.Icon(recipe.GetName()));
            if(!burned.IsEmpty()){burned.Metadata=value.Metadata;value=burned;}
            value.SetMetadata("rebirth.cooking.water",0f);value.SetMetadata("rebirth.cooking.energy",0f);
            if(value.TryGetMetadata("rebirth.cooking.nutrition",out float remainder))value.SetMetadata("rebirth.cooking.nutrition",Math.Min(2f,Math.Max(0f,remainder)));
            value.SetMetadata("rebirth.cooking.burnt",1);value.SetMetadata("rebirth.cooking.quality","Burnt");value.SetMetadata("rebirth.cooking.comfort",-5f);}
        return value;
    }
    public static void Take(XUiC_RecipeStack stack)
    {
        if(!Ready(stack.recipe))return;
        var recipe=stack.recipe;var item=new ItemStack(Output(recipe),recipe.count*stack.recipeCount);
        if(!stack.xui.PlayerInventory.AddItem(item,true))
        {
            // AddItem can partially transfer: keep the exact remainder as output rather than duplicating it.
            if(item.count>0)GameManager.Instance.ItemDropServer(item,stack.xui.playerUI.entityPlayer.position,Vector3.zero,stack.xui.playerUI.entityPlayer.entityId,120f,false);
        }
        if(!Burnt(recipe))
        {
            var receipt=RebirthCookingBatch.Receipt(recipe);receipt.SetMetadata("rebirth.cooking.completed",stack.recipeCount);
            RebirthCookingSessionService.Request(stack.xui.playerUI.entityPlayer,"complete",recipe.GetName(),book:stack.startingEntityId.ToString(),item:receipt,count:recipe.count);
        }
        stack.ClearRecipe();stack.Owner.RefreshQueue();
        var station=stack.windowGroup.Controller as XUiC_RebirthCookingStation;
        station?.fuelWindow?.TurnOff();
        if(station?.WorkstationData?.TileEntity!=null)station.WorkstationData.TileEntity.IsBurning=false;
        station?.syncTEfromUI();
        station?.GetChildByType<XUiC_RebirthCookingWorkspace>()?.RefreshStationState();
    }

    public static void Cancel(XUiC_RecipeStack stack)
    {
        if(!Managed(stack.recipe)||Ready(stack.recipe))return;
        var refunds=stack.recipe.ingredients.Select(i=>new ItemStack(i.itemValue.Clone(),i.count*stack.recipeCount)).ToArray();
        foreach(var refund in refunds)
            if(refund.itemValue.Metadata!=null)
                foreach(var key in refund.itemValue.Metadata.Keys.Where(k=>k.StartsWith(Prefix,StringComparison.Ordinal)).ToArray())refund.itemValue.Metadata.Remove(key);
        // Remove the job before inventory callbacks can re-enter. Preserve cooked-input metadata.
        stack.ClearRecipe();stack.Owner.RefreshQueue();
        var station=stack.windowGroup.Controller as XUiC_RebirthCookingStation;
        station?.fuelWindow?.TurnOff();
        if(station?.WorkstationData?.TileEntity!=null)station.WorkstationData.TileEntity.IsBurning=false;
        station?.syncTEfromUI();
        foreach(var refund in refunds)
            if(!stack.xui.PlayerInventory.AddItem(refund,true)&&refund.count>0)
                GameManager.Instance.ItemDropServer(refund,stack.xui.playerUI.entityPlayer.position,Vector3.zero,stack.xui.playerUI.entityPlayer.entityId,120f,false);
        (stack.Owner as XUiC_RebirthCraftingQueue)?.SyncVisiblePresentationAfterNativeMutation();
        station?.GetChildByType<XUiC_RebirthCookingWorkspace>()?.RefreshStationState();
    }
}
