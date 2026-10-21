using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Shared productive-time accounting. No inventory rewards or secondary queue.
internal static class RebirthProductiveToolWork
{
    private static readonly Dictionary<int,string[]> required=new Dictionary<int,string[]>();
    internal static IReadOnlyList<string> Required(Recipe recipe)
    {
        int type=recipe?.craftingToolType??0;
        if(type==0)return Array.Empty<string>();
        if(!required.TryGetValue(type,out var names))
        {var item=ItemClass.GetForId(type);names=item==null?Array.Empty<string>():new[]{item.GetItemName()};required[type]=names;}
        return names;
    }
    internal static float Limit(ItemStack[] tools,Recipe recipe,Block block,float requested)
    {
        if(float.IsNaN(requested)||float.IsInfinity(requested)||requested<0||!RebirthStationToolAvailability.HasRequirement(tools,recipe))return 0;
        return Math.Min(requested,RebirthRecyclerBonuses.MaxProcessingSeconds(tools,Required(recipe),RebirthRecyclerBonuses.WearRatePerSecond(block)));
    }
    internal static ItemStack[] Wear(ItemStack[] tools,Recipe recipe,Block block,float seconds)
        =>RebirthRecyclerBonuses.ApplyWear(tools,Required(recipe),RebirthRecyclerBonuses.WearRatePerSecond(block),seconds);
}

// Thread-bound native invocation, including each timer write after queue cycling.
// The original native insertion, XP callback and ordering stay in native code.
internal static class RebirthRecyclerNativeLoop
{
    private sealed class Frame
    {
        internal TileEntityWorkstation Station;internal Frame Previous;
        internal readonly Dictionary<RecipeQueueItem,float> Before=new Dictionary<RecipeQueueItem,float>();
        internal Carryover Credit;
    }
    private sealed class Carryover
    {
        internal RecipeQueueItem Row;internal Recipe Recipe;internal int Multiplier;
        internal float Budget;internal bool Used;
    }
    private sealed class Earned {internal Recipe Recipe;internal int Multiplier;internal TileEntityWorkstation Station;internal World World;}
    private static readonly ConditionalWeakTable<RecipeQueueItem,Earned> earned=new ConditionalWeakTable<RecipeQueueItem,Earned>();
    private static bool HasEarned(TileEntityWorkstation station,RecipeQueueItem row)
    {
        if(row==null||!earned.TryGetValue(row,out var proof))return false;
        if(ReferenceEquals(proof.Station,station)&&ReferenceEquals(GameManager.Instance?.World,proof.World)&&row.CraftingTimeLeft<=0&&ReferenceEquals(row.Recipe,proof.Recipe)&&row.Multiplier==proof.Multiplier&&row.Multiplier>0)return true;
        earned.Remove(row);return false;
    }
    internal static bool HasObservedEarned(TileEntityWorkstation station,RecipeQueueItem row)=>HasEarned(station,row);
    [ThreadStatic] private static Frame current;
    internal static object Enter(TileEntityWorkstation station)
    {
        var world=GameManager.Instance?.World;
        if(world==null||world.IsRemote()&&!RebirthRecyclerTransport.IsNativeOwner(station,world.GetPrimaryPlayer()))return null;
        var frame=new Frame{Station=station,Previous=current};current=frame;return frame;
    }
    internal static void Leave(object token,bool returned=true)
    {
        if(token is not Frame frame||!ReferenceEquals(current,frame))return;
        try{RebirthProductiveEarnedCoordinator.ObserveNativeReturned(frame.Station,returned);}
        finally{current=frame.Previous;}
    }
    internal static object InvocationToken(TileEntityWorkstation station)=>IsCurrentInvocation(station)?current:null;
    internal static bool IsCurrentInvocation(TileEntityWorkstation station)
        =>current!=null&&station!=null&&ReferenceEquals(current.Station,station);
    internal static bool TryConsumeNativeCarryover(TileEntityWorkstation station,float budget)
    {
        var frame=current;var credit=frame?.Credit;
        if(frame==null||!ReferenceEquals(frame.Station,station)||credit==null||credit.Used||credit.Budget!=budget||
            station.Queue==null||station.Queue.Length==0||!ReferenceEquals(station.Queue[station.Queue.Length-1],credit.Row)||!ReferenceEquals(credit.Row.Recipe,credit.Recipe)||credit.Row.Multiplier!=credit.Multiplier)return false;
        credit.Used=true;return true;
    }
    private static void DispatchMarked(TileEntityWorkstation station,RecipeQueueItem row,float budget)
    {
        var frame=current;if(!IsCurrentInvocation(station)||frame.Credit!=null||float.IsNaN(budget)||float.IsInfinity(budget)||budget<=0)return;
        var credit=new Carryover{Row=row,Recipe=row.Recipe,Multiplier=row.Multiplier,Budget=budget};frame.Credit=credit;
        try{RebirthRecyclerRuntime.ProcessNativeCarryover(station,budget);}
        finally{if(ReferenceEquals(frame.Credit,credit))frame.Credit=null;}
    }
    internal static void SetTime(RecipeQueueItem row,float target,TileEntityWorkstation station)
    {
        if(row==null)return;
        var frame=current;float before=row.CraftingTimeLeft;
        RebirthProductiveEarnedCoordinator.BeforeNativeTimerWrite(station,row);
        bool wasEarned=HasEarned(station,row); // Retire changed row/recipe/application before writes.
        if(frame==null||!ReferenceEquals(frame.Station,station)){row.CraftingTimeLeft=target;return;}
        if(RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,row,before,target))return;
        if(before>0&&RebirthProductiveEarnedCoordinator.BlocksNewWork(station))
        {RebirthProductiveEarnedCoordinator.ObserveDeferredNativeTimer(station,row,before,target);return;}
        // Output-full retries cannot bank new elapsed time for the next unit.
        // Retain only the original productive invocation's native carryover.
        if((wasEarned||RebirthProductiveEarnedCoordinator.HoldTimer(station,row))&&before<=0&&target<before)
            target=before<0?before:-float.Epsilon; // Enter native completion without banking retry elapsed.
        if(!frame.Before.ContainsKey(row))frame.Before[row]=before;
        if(row.Recipe==null||row.Multiplier<=0){row.CraftingTimeLeft=target;return;}
        if(RebirthRecyclerRuntime.IsMarked(row.Recipe)||RebirthCookingHeat.Managed(row.Recipe))
        {
            if(target<=before)
            {
                // Dispatch at the write itself, including exactly-zero or still
                // positive carryover where native's while predicate is skipped.
                float budget=Math.Max(0,before-target);
                if(RebirthRecyclerRuntime.IsMarked(row.Recipe))DispatchMarked(station,row,budget);
                else RebirthCookingHeat.TickTile(station,budget);
                return;
            }
            row.CraftingTimeLeft=target;return;
        }
        if(!RebirthStationObservationDispatcher.CanProcessNativeQueue(station))return;
        // Decrease is initial/carryover work; increase after a completed
        // application can still contain productive catch-up for its next unit.
        float requested=target<before?Math.Min(Math.Max(0,before),before-target):
            before<0&&target-before>0?Math.Min(target-before,-before):0f;
        if(requested>0)
        {
            float allowed=RebirthProductiveToolWork.Limit(station.Tools,row.Recipe,station.block,requested);
            if(allowed<requested)
                target=target<before?Math.Max(0,before-allowed):Math.Max(0,target-before-allowed);
            if(allowed>0)
            {
                var worn=RebirthProductiveToolWork.Wear(station.Tools,row.Recipe,station.block,allowed);
                RebirthProductiveEarnedCoordinator.Attempt durable=null;
                if(target<=0&&allowed>=requested&&!RebirthProductiveEarnedCoordinator.PrepareFinal(station,row,ref target,allowed,worn,out durable))return;
                station.Tools=worn;station.setModified();
                if(durable!=null){row.CraftingTimeLeft=target;RebirthProductiveEarnedCoordinator.Applied(durable);}
                if(target<=0&&allowed>=requested)
                {earned.Remove(row);earned.Add(row,new Earned{Recipe=row.Recipe,Multiplier=row.Multiplier,Station=station,World=GameManager.Instance.World});}
            }
        }
        row.CraftingTimeLeft=target;
    }
    internal static bool Continue(TileEntityWorkstation station)
    {
        if(!station.hasRecipeInQueue())return false;
        var row=station.Queue?.Length>0?station.Queue[station.Queue.Length-1]:null;
        if(row?.Recipe==null||row.Multiplier<=0)return true;
        var frame=current;
        if(RebirthRecyclerRuntime.IsMarked(row.Recipe)||RebirthCookingHeat.Managed(row.Recipe))
        {
            // Native has already subtracted carryover from this newly active
            // row. Restore its original timer before the specialized owner runs.
            if(frame==null||!ReferenceEquals(frame.Station,station)||!frame.Before.TryGetValue(row,out float original))return false;
            float budget=Math.Max(0,original-row.CraftingTimeLeft);row.CraftingTimeLeft=Math.Max(0,original);
            if(RebirthRecyclerRuntime.IsMarked(row.Recipe))DispatchMarked(station,row,budget);
            else RebirthCookingHeat.TickTile(station,budget);
            return false;
        }
        return RebirthStationObservationDispatcher.CanProcessNativeQueue(station)&&RebirthProductiveEarnedCoordinator.AllowsCompletion(station,row)&&
            (RebirthStationToolAvailability.HasRequirement(station.Tools,row.Recipe)||HasEarned(station,row)||
             RebirthProductiveEarnedCoordinator.HasSavedEarned(station,row));
    }
    internal static bool UserAccessing(TileEntityWorkstation station)
    {
        if(!station.bUserAccessing)return false;
        if(RebirthProductiveEarnedCoordinator.IsExecutingMobilePaidZeroCompletion(station))return false;
        var world=GameManager.Instance?.World;
        return world==null||!world.IsRemote()||!RebirthRecyclerTransport.IsNativeOwner(station,world.GetPrimaryPlayer());
    }
    internal static bool BurningForPaidCompletion(TileEntityWorkstation station)
        =>station.isBurning||RebirthProductiveEarnedCoordinator.IsExecutingPaidNativeZeroCompletion(station);
    // IL resolves inherited fields on their declaring type; reflection through
    // a derived workstation can produce an unequal ReflectedType wrapper for
    // that same field. Compare exact metadata identity, never just its name.
    private static bool SameField(object operand,System.Reflection.FieldInfo expected)
        =>expected!=null&&operand is System.Reflection.FieldInfo actual&&actual.Module==expected.Module&&
            actual.MetadataToken==expected.MetadataToken&&actual.DeclaringType==expected.DeclaringType;
    internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
    {
        var field=AccessTools.Field(typeof(RecipeQueueItem),nameof(RecipeQueueItem.CraftingTimeLeft));
        var has=AccessTools.Method(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.hasRecipeInQueue));
        var accessing=AccessTools.Field(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.bUserAccessing));
        var burning=AccessTools.Field(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.isBurning));
        int writes=0,gates=0,ownerGates=0,burningGates=0;
        foreach(var original in instructions)
        {
            var code=new CodeInstruction(original);
            if(code.opcode==OpCodes.Stfld&&SameField(code.operand,field))
            {
                var station=new CodeInstruction(OpCodes.Ldarg_0);station.labels.AddRange(code.labels);station.blocks.AddRange(code.blocks);
                code.labels.Clear();code.blocks.Clear();yield return station;
                code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RebirthRecyclerNativeLoop),nameof(SetTime));writes++;
            }
            else if(code.opcode==OpCodes.Ldfld&&SameField(code.operand,accessing))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RebirthRecyclerNativeLoop),nameof(UserAccessing));ownerGates++;}
            else if(code.opcode==OpCodes.Ldfld&&SameField(code.operand,burning))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RebirthRecyclerNativeLoop),nameof(BurningForPaidCompletion));burningGates++;}
            else if((code.opcode==OpCodes.Call||code.opcode==OpCodes.Callvirt)&&Equals(code.operand,has))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RebirthRecyclerNativeLoop),nameof(Continue));gates++;}
            yield return code;
        }
        if(writes!=3||gates!=1||ownerGates!=1||burningGates!=1)throw new InvalidOperationException("Native workstation queue requires exactly3 timer writes,1 loop admission gate,1 native owner-access field gate and1 paid-completion burning field gate; observed "+writes+"/"+gates+"/"+ownerGates+"/"+burningGates+".");
    }
}

// Ordinary open cards keep native output and owner transport. Wear is based on
// verified positive timer movement, never output-full retry time or mere UI time.
[HarmonyPatch(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.Update))]
internal static class RebirthOrdinaryStationToolWork
{
    internal sealed class Work
    {internal XUiC_RecipeStack Card;internal XUiC_WorkstationWindowGroup Window;internal Work Previous;}
    [ThreadStatic] private static Work current;
    [HarmonyPriority(Priority.High)]
    private static bool Prefix(XUiC_RecipeStack __instance,out Work __state)
    {
        __state=null;var recipe=__instance.GetRecipe();var window=__instance.GetParentByType<XUiC_WorkstationWindowGroup>();
        if(!__instance.IsCrafting||recipe==null||recipe.IsScrap||RebirthCookingHeat.Managed(recipe)||
            window?.WorkstationData?.TileEntity==null||window.WorkstationData.TileEntity is RebirthCamperStationState)return true;
        var player=__instance.xui?.playerUI?.entityPlayer;
        if(player==null||player.IsDead()||!ReferenceEquals(player.world,GameManager.Instance?.World)||
            !ReferenceEquals(player.world.GetPrimaryPlayer(),player)||LockManager.Instance?.IsLockedByLocalPlayer(window.WorkstationData.TileEntity,0)!=true)return true;
        var tools=window.GetChildByType<XUiC_WorkstationToolGrid>()?.GetSlots();
        if(!RebirthStationToolAvailability.HasRequirement(tools,recipe))return false;
        __state=new Work{Card=__instance,Window=window,Previous=current};current=__state;return true;
    }
    private static void SetTime(XUiC_RecipeStack card,float target)
    {
        var work=current;float before=card.craftingTimeLeft;var recipe=card.GetRecipe();
        if(work==null||!ReferenceEquals(work.Card,card)||recipe==null||card.recipeCount<=0)
        {card.craftingTimeLeft=target;return;}
        // Output-full retry is a stall, not new time debt for another unit.
        if(card.isInventoryFull&&before<=0&&target<before){card.craftingTimeLeft=0;return;}
        float requested=target<before?Math.Min(Math.Max(0,before),before-target):
            before<0&&target-before>0?Math.Min(target-before,-before):0;
        if(requested>0)
        {
            var grid=work.Window.GetChildByType<XUiC_WorkstationToolGrid>();var tools=grid?.GetSlots();
            float allowed=RebirthProductiveToolWork.Limit(tools,recipe,work.Window.WorkstationData.TileEntity.block,requested);
            if(allowed<requested)target=target<before?Math.Max(0,before-allowed):Math.Max(0,target-before-allowed);
            if(allowed>0)
            {
                var worn=RebirthProductiveToolWork.Wear(tools,recipe,work.Window.WorkstationData.TileEntity.block,allowed);
                work.Window.WorkstationData.SetToolStacks(worn);grid?.SetSlots(worn);
            }
        }
        card.craftingTimeLeft=target;
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var field=AccessTools.Field(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.craftingTimeLeft));int writes=0;
        foreach(var original in instructions)
        {
            var code=new CodeInstruction(original);
            if(code.opcode==OpCodes.Stfld&&Equals(code.operand,field))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RebirthOrdinaryStationToolWork),nameof(SetTime));writes++;}
            yield return code;
        }
        if(writes!=6)throw new InvalidOperationException("Native open recipe Update requires exactly6 timer writes.");
    }
    private static Exception Finalizer(Exception __exception,Work __state)
    {if(__state!=null&&ReferenceEquals(current,__state))current=__state.Previous;return __exception;}
}
