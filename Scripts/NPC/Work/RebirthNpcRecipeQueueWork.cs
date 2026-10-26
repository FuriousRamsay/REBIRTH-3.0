using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Queue admission only. The source assignment owns one saved native ingredient;
// neither staging nor this outcome asserts cooking/output completion.
internal static class RebirthNpcRecipeQueueWork
{
    internal const string DefinitionId="rebirth.work.recipe-queue-one";
    internal static bool IsDefinition(string definition)=>definition==DefinitionId;
    internal static void Register()=>RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(DefinitionId,
        RebirthNpcWorkKind.Crafting,RebirthNpcWorkCapability.Crafting,275,1,TimeSpan.FromSeconds(90),false),true);
    private static bool SourceId(string key,out ulong id)
    {id=0;return key?.StartsWith("recipe-queue:",StringComparison.Ordinal)==true&&key.Length<64&&
        ulong.TryParse(key.Substring(13),NumberStyles.None,CultureInfo.InvariantCulture,out id)&&id!=0;}
    private static bool Shape(string definition,string key,bool hasPosition,Vector3 position)=>IsDefinition(definition)&&RebirthNpcRecipeIngredientSet.TryParseKey(key,out _)&&hasPosition&&
        !float.IsNaN(position.x)&&!float.IsNaN(position.y)&&!float.IsNaN(position.z)&&!float.IsInfinity(position.x)&&!float.IsInfinity(position.y)&&!float.IsInfinity(position.z)&&
        position==new Vector3i(Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.y),Mathf.FloorToInt(position.z)).ToVector3();
    internal static bool Allows(RebirthNpcWorkAssignment a)=>a!=null&&Shape(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition);
    internal static bool Allows(RebirthNpcWorkAssignmentRequest a)=>a!=null&&Shape(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition);
    internal static bool Allows(RebirthNpcEmbodiedWorkIntent i)=>i!=null&&i.Path=="concrete"&&i.PrimaryId!=0&&i.SecondaryId!=0&&
        i.ExecutorId=="rebirth.work.crafting-workstation"&&i.Actor is EntityRebirthHumanoidNPC&&i.MatchesOriginal()&&
        RebirthNpcWorkAssignmentService.TryGet(i.PrimaryId,out var a)&&Allows(a)&&a.NpcId==i.StableId&&
        RebirthNpcWorkReservationService.TryGetForAssignment(i.PrimaryId,out var r)&&r.ReservationId==i.SecondaryId;
    internal static string TargetId(RebirthNpcWorkAssignment a)=>"block:"+Mathf.FloorToInt(a.TargetPosition.x).ToString(CultureInfo.InvariantCulture)+":"+
        Mathf.FloorToInt(a.TargetPosition.y).ToString(CultureInfo.InvariantCulture)+":"+Mathf.FloorToInt(a.TargetPosition.z).ToString(CultureInfo.InvariantCulture);
    internal static bool Resolve(RebirthNpcWorkAssignment a,out RebirthNpcWorkAssignment staged,out RebirthNpcRecipeStagingWork.Target target,
        out RebirthNpcWorkNativeTransferOriginal.Record source)
    {
        staged=null;target=null;source=null;
        try
        {
        return Allows(a)&&SourceId(a.TargetKey,out var id)&&
            RebirthNpcRecipeStagingWork.TryDescribeSavedStationStage(a.NpcId,a.ActorId,id,out staged,out target,out source)&&
            staged.TargetPosition==a.TargetPosition;
        }
        catch{staged=null;target=null;source=null;return false;}
    }
    internal static bool MatchesBoundSource(RebirthNpcWorkAssignment a,ulong sourceAssignment,Vector3 station)=>
        Allows(a)&&RebirthNpcRecipeIngredientSet.TryParseKey(a.TargetKey,out var ids)&&ids[0]==sourceAssignment&&a.TargetPosition==station;
    internal sealed class Resolved
    {
        internal RebirthNpcWorkAssignment Staged;internal RebirthNpcRecipeStagingWork.Target Target;internal RebirthNpcWorkNativeTransferOriginal.Record Source;
    }
    // Every staged id of the key, in key order, all at the same station incarnation, recipe and owner.
    internal static bool ResolveSet(RebirthNpcWorkAssignment a,out List<Resolved> set)
    {
        set=null;
        try
        {
            if(!Allows(a)||!RebirthNpcRecipeIngredientSet.TryParseKey(a.TargetKey,out var ids))return false;
            var list=new List<Resolved>();
            foreach(ulong id in ids)
            {
                if(!RebirthNpcRecipeStagingWork.TryDescribeSavedStationStage(a.NpcId,a.ActorId,id,out var staged,out var target,out var source)||
                    staged.TargetPosition!=a.TargetPosition)return false;
                if(list.Count>0&&(target.Incarnation!=list[0].Target.Incarnation||target.Station!=list[0].Target.Station||
                    target.Recipe.GetName()!=list[0].Target.Recipe.GetName()||source.Owner!=list[0].Source.Owner))return false;
                list.Add(new Resolved{Staged=staged,Target=target,Source=source});
            }
            set=list;return true;
        }
        catch{set=null;return false;}
    }
    // One staged unit: the existing single-ingredient rule. Several: the gated exact-cover rule over the same recipe shape checks.
    internal static bool EligibleSet(IList<Resolved> set)
    {
        try
        {
            if(set==null||set.Count<1)return false;
            if(set.Count==1)return Eligible(set[0].Target.Recipe,set[0].Source);
            var recipe=set[0].Target.Recipe;
            if(!RebirthNpcRecipeIngredientSet.Qualified||recipe==null||recipe.IsScrap||RebirthNpcRecipeIngredientSet.Wildcard(recipe)||!OutputFitsOneCell(recipe)||recipe.craftingTier>1||
                recipe.craftingTime<=0f||float.IsNaN(recipe.craftingTime)||float.IsInfinity(recipe.craftingTime)||string.IsNullOrEmpty(recipe.craftingArea)||
                RebirthCookingHeat.Managed(recipe)||RebirthRecipeOutputs.HasOutputs(recipe)||RebirthCookingBatch.IsBatch(recipe))return false;
            return RebirthNpcRecipeIngredientSet.ValidateStagedSet(recipe,set.Select(x=>x.Source.Frame).ToList())==null;
        }
        catch{return false;}
    }
    // count>1 is supported while the whole yield lands in ONE Output cell, so the exact one-cell/one-receipt attribution still holds.
    internal static bool OutputFitsOneCell(Recipe recipe)
    {
        var item=recipe==null?null:ItemClass.GetForId(recipe.itemValueType);
        return item!=null&&recipe.count>=1&&recipe.count<=item.Stacknumber.Value;
    }
    // Exact refusal reason for the Desk (English source string; the UI maps it). Null = this recipe shape is supported.
    internal static string RefusalReason(Recipe recipe)
    {
        if(recipe==null||recipe.IsScrap)return "Scrap recipes are not supported.";
        if(RebirthNpcRecipeIngredientSet.Wildcard(recipe))return Localization.Get("P7RefusalWildcardRecipe");
        if(!RebirthNpcRecipeIngredientSet.ShapeSupported(recipe))
            return "Only recipes whose ingredients total at most "+RebirthNpcRecipeIngredientSet.MaxUnits+" single units are supported: the saved native transfer moves one unit per staging assignment and the queue original debits every staged unit of the set.";
        if(!OutputFitsOneCell(recipe))return "The recipe yield does not fit one station output cell, so the finished item cannot be attributed exactly.";
        if(recipe.craftingTier>1)return "Quality-tier recipes are not supported.";
        if(RebirthCookingHeat.Managed(recipe))return "Heat-managed cooking recipes are not supported.";
        if(RebirthRecipeOutputs.HasOutputs(recipe)||RebirthCookingBatch.IsBatch(recipe))return "Recipes with multiple or batched outputs are not supported.";
        return null;
    }
    internal static bool Eligible(Recipe recipe,RebirthNpcWorkNativeTransferOriginal.Record source)
    {
        try
        {
        if(recipe==null||recipe.IsScrap||RebirthNpcRecipeIngredientSet.Wildcard(recipe)||recipe.ingredients?.Count!=1||recipe.ingredients[0].count!=1||!OutputFitsOneCell(recipe)||
            recipe.craftingTier>1||recipe.craftingTime<=0f||float.IsNaN(recipe.craftingTime)||float.IsInfinity(recipe.craftingTime)||
            string.IsNullOrEmpty(recipe.craftingArea)||RebirthCookingHeat.Managed(recipe)||RebirthRecipeOutputs.HasOutputs(recipe)||source==null)return false;
        var stack=RebirthHumanEquipment.Decode(source.Frame);
        return stack!=null&&!stack.IsEmpty()&&stack.count==1&&
            string.Equals(RebirthHumanEquipment.Frame(stack),source.Frame,StringComparison.Ordinal)&&
            stack.itemValue.type==recipe.ingredients[0].itemValue.type&&
            !(stack.itemValue.HasModSlots&&stack.itemValue.HasMods())&&
            RebirthCookingItemStats.FullIngredient(stack.itemValue)&&
            RebirthCookingItemStats.Compatible(stack.itemValue,recipe.ingredients[0].itemValue);
        }
        catch{return false;}
    }
    internal static bool PotentiallyReady(RebirthNpcWorkAssignment a,EntityRebirthHumanoidNPC npc)
    {
        try
        {
        if(RebirthNpcStationQueueOriginal.TryRead(a.NpcId,RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId),out var retained))
            return RebirthNpcStationQueueOriginal.CanObserveExistingAssignment(a,npc,retained);
        if(RebirthNpcStationQueueOriginal.HasPending(a.NpcId)||!RebirthNpcWorkOutcomeService.CanAdmitExactOutcomeIdentity(a))return false;
        if(!ResolveSet(a,out var set)||!EligibleSet(set)||
            !RebirthHumanRecipeReading.Knows(a.NpcId,set[0].Target.Recipe.GetName())||
            !RebirthNpcRecipeStagingWork.StationCurrent(set[0].Target,RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId)))return false;
        return npc.bag?.ItemGrid!=null&&set.All(x=>x.Source.BagSlot<npc.bag.SlotCount&&
            RebirthHumanEquipment.Frame(npc.bag.ItemGrid[x.Source.BagSlot])==x.Source.Frame);
        }
        catch{return false;}
    }
    private static bool StagedStillBound(RebirthNpcStableId npc,string actor,ulong id)
    {
        try
        {
            if(!RebirthNpcRecipeStagingWork.TryDescribeSavedStationStage(npc,actor,id,out _,out _,out var source)||
                !RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(System.Xml.Linq.XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
            return equipment?.Element("bindings")?.Elements("binding").Any(b=>(string)b.Attribute("area")=="bag"&&
                (string)b.Attribute("id")==source.CreditId.ToString("N"))==true;
        }
        catch{return false;}
    }
    internal static RebirthNpcWorkAssignmentResult Assign(string session,ulong stagedAssignment,out RebirthNpcWorkAssignment assignment,out string detail)=>
        AssignSet(session,new[]{stagedAssignment},out assignment,out detail);
    // Staged SET (several ids; more than one is gated by RebirthNpcRecipeIngredientSet.Qualified through TryParseKey/EligibleSet).
    internal static RebirthNpcWorkAssignmentResult AssignSet(string session,ulong[] stagedAssignments,out RebirthNpcWorkAssignment assignment,out string detail)
    {
        assignment=null;detail="Saved staged native ingredients must pay the complete known recipe at its owned stationary workstation.";
        if(stagedAssignments==null||stagedAssignments.Length<1||
            RebirthNpcInteractionSessionService.Authorize(session,RebirthNpcInteractionAction.AssignWork,out var c)!=RebirthNpcInteractionResult.Allowed||c==null||
            !RebirthNpcRecipeStagingWork.TryDescribeSavedStationStage(c.NpcId,c.ActorId,stagedAssignments[0],out var staged,out _,out _))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        // A completed staging whose ingredient was already consumed (its bag binding is gone) is not part of a new set; ids are de-duplicated,
        // sorted ascending and must still number at most RebirthNpcRecipeIngredientSet.MaxUnits (TryParseKey refuses more / duplicates).
        ulong[] ids=stagedAssignments.Length==1?stagedAssignments:
            stagedAssignments.Distinct().Where(x=>StagedStillBound(c.NpcId,c.ActorId,x)).OrderBy(x=>x).ToArray();
        if(ids.Length<1)return RebirthNpcWorkAssignmentResult.PermissionDenied;
        string key="recipe-queue:"+string.Join("+",ids.Select(x=>x.ToString(CultureInfo.InvariantCulture)));
        var probe=new RebirthNpcWorkAssignment{DefinitionId=DefinitionId,TargetKey=key,HasTargetPosition=true,TargetPosition=staged.TargetPosition,ActorId=c.ActorId,NpcId=c.NpcId};
        if(!ResolveSet(probe,out var set)||!EligibleSet(set)||
            !RebirthHumanRecipeReading.Knows(c.NpcId,set[0].Target.Recipe.GetName())||
            !RebirthNpcRecipeStagingWork.StationCurrent(set[0].Target,RebirthNpcInteractionMutationHandlers.FindActorPlayer(c.ActorId)))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        return RebirthNpcWorkAssignmentService.Create(new RebirthNpcWorkAssignmentRequest{SessionKey=session,DefinitionId=DefinitionId,TargetKey=key,
            HasTargetPosition=true,TargetPosition=staged.TargetPosition,ExpectedRuntimeRevision=c.RuntimeRevision},out assignment,out detail);
    }
    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)=>RebirthNpcStationQueueOriginal.AllowsOutcome(outcome);
}

public sealed class RebirthNpcRecipeQueueWorkAdapter:IRebirthNpcCraftingWorkAdapter
{
    public string AdapterId=>"rebirth.recipe.native-queue-one";
    public int Priority=>800;
    public bool CanHandle(RebirthNpcConcreteWorkContext c)=>RebirthNpcRecipeQueueWork.Allows(c?.Assignment);
    public RebirthNpcWorkMutationResult BeginOperation(RebirthNpcConcreteWorkContext c)=>CanHandle(c)?
        RebirthNpcWorkMutationResult.Continue(0f,"One saved staged ingredient accepted for queue admission; worker must reach the station."):
        new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Suspend,FailureCategory=RebirthNpcWorkFailureCategory.MissingInput,Detail="Saved NPC queue source is unavailable."};
    public RebirthNpcWorkMutationResult TickOperation(RebirthNpcConcreteWorkContext c)=>RebirthNpcStationQueueOriginal.Advance(c);
    public void CancelOperation(RebirthNpcConcreteWorkContext c,string reason){}
}
