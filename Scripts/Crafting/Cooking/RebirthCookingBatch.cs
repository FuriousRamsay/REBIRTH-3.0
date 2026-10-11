using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>Concrete batch data travels with serialized queue ingredients and then with the output item.</summary>
public static class RebirthCookingBatch
{
    private static readonly object CompletionWitnessSync = new object();
    private static readonly Dictionary<string,int> CompletionWitnesses = new Dictionary<string,int>(StringComparer.Ordinal);

    private static readonly Dictionary<string,RebirthCraftTrainingRules.Model> CompletionTraining =
        new Dictionary<string,RebirthCraftTrainingRules.Model>(StringComparer.Ordinal);
    private const string TrainingRawKey="rebirth.cooking.training.raw35";
    private const string TrainingDifficultyKey="rebirth.cooking.training.difficulty35";
    public static void ResetCompletionWitnesses()
    {lock(CompletionWitnessSync){CompletionWitnesses.Clear();CompletionTraining.Clear();}}
    public static void RecordCompletionWitness(ItemValue receipt,int completed,Recipe source=null)
    {
        if(receipt==null||completed<=0||GameManager.Instance?.World==null||GameManager.Instance.World.IsRemote())return;string token;
        if(!receipt.TryGetMetadata(Prefix+"token",out token)||string.IsNullOrEmpty(token))return;
        var model=new RebirthCraftTrainingRules.Model();
        if(source!=null && source.GetName()==receipt.ItemClass?.GetItemName())
        {
            // Source is the native server station/queue recipe, with its paid input quantities.
            model=RebirthCraftTrainingRules.BuildModel(null,source,RebirthServiceCraftSkillService.ClassifyRecipe(source.GetName()));
            if(model.Valid){receipt.SetMetadata(TrainingRawKey,model.Raw);receipt.SetMetadata(TrainingDifficultyKey,model.Difficulty);
                receipt.SetMetadata("rebirth.cooking.training.types36",model.IngredientTypes);
                receipt.SetMetadata("rebirth.cooking.training.seconds36",model.Seconds);}
        }
        else
        {
            // This overload is used only by the existing server station-receipt recovery path,
            // not the client completion request. Never use the client's incoming model for XP.
            float raw,difficulty;
            if(receipt.TryGetMetadata(TrainingRawKey,out raw)&&receipt.TryGetMetadata(TrainingDifficultyKey,out difficulty)&&
                !float.IsNaN(raw)&&!float.IsInfinity(raw)&&raw>=0f&&raw<=10f&&
                !float.IsNaN(difficulty)&&!float.IsInfinity(difficulty)&&difficulty>=0f&&difficulty<=100f)
                {
                    string name=receipt.ItemClass?.GetItemName();
                    model=RebirthCraftTrainingRules.ModelForRecipe(null,name);
                    if(!model.Valid)model=new RebirthCraftTrainingRules.Model{Raw=raw,Difficulty=difficulty,Valid=true,RecipeId=name,SkillId=RebirthServiceCraftSkillService.ClassifyRecipe(name)};
                    if(receipt.TryGetMetadata("rebirth.cooking.training.types36",out int types) && types>=1 && types<=12)model.IngredientTypes=types;
                    if(receipt.TryGetMetadata("rebirth.cooking.training.seconds36",out float seconds) && seconds>=0 && seconds<=120)model.Seconds=seconds;
                }
        }
        lock(CompletionWitnessSync)
        {
            int old;CompletionWitnesses.TryGetValue(token,out old);if(completed>old)CompletionWitnesses[token]=completed;
            if(model.Valid)CompletionTraining[token]=model;
        }
    }
    private static RebirthCraftTrainingRules.Model TrainingModel(EntityPlayer player,string token,string name,int outputs)
    {
        RebirthCraftTrainingRules.Model model;
        lock(CompletionWitnessSync)if(CompletionTraining.TryGetValue(token,out model))return model;
        model=RebirthCraftTrainingRules.ModelForRecipe(player,name);
        // Compatibility only for an old, already-completed improvised receipt whose ingredient
        // recipe no longer exists. New completions always have the server-created effort model.
        if(!model.Valid && name.StartsWith("rebirthImprovised",StringComparison.Ordinal))
            model=new RebirthCraftTrainingRules.Model{Valid=true,Difficulty=100f,
                Raw=.008f,RecipeId=name,SkillId="skill.cooking",IngredientTypes=1,Outputs=outputs};
        return model;
    }
    private static bool HasCompletionWitness(string token,int completed)
    {
        lock(CompletionWitnessSync){int witnessed;return !string.IsNullOrEmpty(token)&&CompletionWitnesses.TryGetValue(token,out witnessed)&&witnessed>=completed;}
    }
    private const string Prefix="rebirth.cooking.";
    private const string QueuePrefix=Prefix+"queue.";
    public const string Marker=QueuePrefix+"batch";
    [ThreadStatic] private static Recipe activeRecipe;
    [ThreadStatic] private static TileEntityWorkstation activeStation;
    [ThreadStatic] private static XUiC_RecipeStack activeUi;
    private static bool installed;
    public static bool UiCompletionInProgress => activeUi != null;
    private static Recipe Current=>activeStation?.Queue?.LastOrDefault()?.Recipe??activeRecipe;
    public static void Install()
    {
        if(installed)return;
        var h=new Harmony("rebirth.cooking.workspace");
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCampfireProcessingCommands));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCampfireProcessingActivate));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCampfireProcessingLock));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCampfireProcessingRecipeFilter));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingTransfers));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingStackCompatibility));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingMergeCompatibility));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingPartialMergeScope));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingPartialMergeCheck));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingTransferCountCheck));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingSortCompatibility));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingMouseMergeCheck));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingSingleMergeCheck));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingRecipeLookupPostInit));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingRecipeLookupClear));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingLiteratureIcon));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingLiteratureHasIcon));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingLiteratureIconRefresh));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingBurntActions));
        h.Patch(AccessTools.Method(typeof(XUiC_RecipeStack),"outputStack"),prefix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(BeginUi)),postfix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(CompletedUi)),finalizer:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(EndUi)));
        h.Patch(AccessTools.Method(typeof(XUiC_RecipeStack),"Update"),prefix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(BeforeRetry)),postfix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(AfterRetry)));
        h.Patch(AccessTools.Method(typeof(TileEntityWorkstation),"HandleRecipeQueue"),prefix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(BeginStation)),finalizer:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(EndStation)));
        h.Patch(AccessTools.Method(typeof(TileEntityWorkstation),"IsActive"),postfix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(ColdStationActive)));
        h.Patch(AccessTools.Constructor(typeof(ItemValue),new[]{typeof(int),typeof(int),typeof(int),typeof(bool),typeof(string[]),typeof(float)}),postfix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(Created)));
        h.Patch(AccessTools.Method(typeof(XUiC_RecipeStack),"HandleOnPress"),prefix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(BeforeRefund)));
        h.Patch(AccessTools.Method(typeof(TileEntityWorkstation),"AddCraftComplete"),prefix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(CompletionReceipt)));
        h.Patch(AccessTools.Method(typeof(EntityPlayerLocal),"GiveExp",new[]{typeof(CraftCompleteData)}),postfix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(DeferredAward)));
        h.Patch(AccessTools.Method(typeof(Recipe),"Read"),postfix:new HarmonyMethod(typeof(RebirthCookingBatch),nameof(RestoreQueueRecipe)));
        RebirthCookingJobRuntime.Install();
        installed = true;

    }
    private static void BeginUi(XUiC_RecipeStack __instance,out Recipe __state){__state=activeRecipe;activeRecipe=__instance.recipe;activeUi=__instance;}
    private static void EndUi(Recipe __state){activeRecipe=__state;activeUi=null;}
    private struct StationState { public TileEntityWorkstation Previous; public bool FuelRequired; public bool StationEntered, FuelChanged; }
    public static bool NeedsHeat(Recipe recipe)
    {
        // Milling uses the same persisted batch lifecycle, without fuel or overcooking.
        // Keep the conservative heated default for unknown/improvised recipes.
        string area = RebirthCookingCatalogue.Get(recipe?.GetName())?.Station ?? recipe?.craftingArea;
        return area != "cold" && area != "WorkbenchMortarPestle001_FR";
    }
    private static void ColdStationActive(TileEntityWorkstation __instance,ref bool __result)
    {
        var recipe=__instance.Queue?.LastOrDefault()?.Recipe;
        if(RebirthCookingHeat.Managed(recipe)&&!NeedsHeat(recipe)&&!RebirthCookingHeat.Ready(recipe))__result=true;
    }
    private static bool BeginStation(TileEntityWorkstation __instance,ref float _timePassed,out StationState __state)
    {
        __state=new StationState{Previous=activeStation};
        __state.FuelRequired=__instance.isModuleUsed[3];
        activeStation=__instance;__state.StationEntered=true;
        if(!RebirthCookingHeat.TickTile(__instance,_timePassed))return false;
        var entry=__instance.Queue?.LastOrDefault();
        if(entry!=null&&IsBatch(entry.Recipe)&&!NeedsHeat(entry.Recipe)&&!__instance.IsBurning)
        {
            __state.FuelRequired=__instance.isModuleUsed[3];
            if(__state.FuelRequired){__instance.isModuleUsed[3]=false;__state.FuelChanged=true;}
            // Do not run a following hot recipe without fuel in the same native loop.
            entry.CraftingTimeLeft=Math.Max(0,entry.CraftingTimeLeft);
            _timePassed=Math.Min(_timePassed,entry.CraftingTimeLeft+.001f);
        }
        return true;
    }
    private static Exception EndStation(TileEntityWorkstation __instance,StationState __state,Exception __exception)
    {
        Exception restorationFailure=null;
        try
        {
            // Restore only a temporary fuel change this invocation actually made.
            if(__state.FuelChanged)__instance.isModuleUsed[3]=__state.FuelRequired;
        }
        catch(Exception error){restorationFailure=error;}
        finally
        {
            if(__state.StationEntered)activeStation=__state.Previous;
        }
        // Keep the original prefix/native exception when cleanup also fails.
        return __exception??restorationFailure;
    }
    private sealed class RetryState { public Recipe Recipe; public ItemValue Receipt; public int Count, Owner; }
    private static void BeforeRetry(XUiC_RecipeStack __instance,out RetryState __state)
    {
        __state=null;
        if(!__instance.isInventoryFull||!IsBatch(__instance.recipe))return;
        var receipt=Receipt(__instance.recipe);
        __instance.recipe.ingredients[0].itemValue.TryGetMetadata(QueuePrefix+"portions",out int total);
        receipt.SetMetadata(Prefix+"completed",Math.Max(1,total-__instance.recipeCount+1));
        __state=new RetryState{Recipe=__instance.recipe,Receipt=receipt,Count=__instance.recipeCount,Owner=__instance.startingEntityId};
    }
    private static void AfterRetry(XUiC_RecipeStack __instance,RetryState __state)
    {
        if(__state!=null&&!__instance.isInventoryFull&&(__instance.recipe!=__state.Recipe||__instance.recipeCount<__state.Count))ReportCompletion(__instance,__state.Recipe,__state.Receipt,__state.Owner);
    }
    private static void CompletedUi(XUiC_RecipeStack __instance, bool __result)
    {
        if(!__result || !IsBatch(__instance.recipe))return;
        var receipt=Receipt(__instance.recipe);
        int completed; if(__instance.xui?.playerUI?.entityPlayer?.world!=null&&!__instance.xui.playerUI.entityPlayer.world.IsRemote()&&receipt.TryGetMetadata(Prefix+"completed",out completed))RecordCompletionWitness(receipt,completed,__instance.recipe);
        ReportCompletion(__instance,__instance.recipe,receipt,__instance.startingEntityId);
    }
    private static void ReportCompletion(XUiC_RecipeStack stack,Recipe recipe,ItemValue receipt,int owner)
    {
        var player=stack.xui?.playerUI?.entityPlayer;
        if(player==null)return;
        if(owner!=player.entityId && stack.windowGroup.Controller is XUiC_WorkstationWindowGroup station)
        {
            var tile=station.WorkstationData.TileEntity;
            if(tile.CraftCompleteList==null)tile.CraftCompleteList=new List<CraftCompleteData>();
            tile.CraftCompleteList.Add(new CraftCompleteData(owner,new ItemStack(receipt,recipe.count),recipe.GetName(),"",recipe.craftExpGain,1));
            tile.setModified();
        }
        RebirthCookingSessionService.Request(player,"complete",recipe.GetName(),book:owner.ToString(),item:receipt,count:recipe.count);
    }
    private static int Ordinal(Recipe r)
    {
        r.ingredients[0].itemValue.TryGetMetadata(QueuePrefix+"portions",out int total);
        int left=activeStation?.Queue?.LastOrDefault()?.Multiplier ?? activeUi?.recipeCount ?? total;
        return Math.Max(1,total-left+1);
    }
    public static void AwardCompleted(EntityPlayer player,string name,ItemValue receipt,int outputCount)
    {
        if(player==null||player.world.IsRemote()||receipt==null||receipt.ItemClass?.GetItemName()!=name||outputCount<=0||outputCount>32767)return;
        if(!receipt.TryGetMetadata(Prefix+"token",out string token)||string.IsNullOrEmpty(token)||!receipt.TryGetMetadata(Prefix+"completed",out int completed)||completed<1||completed>9999)return;
        // Client-provided receipt metadata is never sufficient evidence of completion.
        // A server-observed native output/job must witness this token/ordinal first.
        if(!HasCompletionWitness(token,completed))return;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        // Do not consume the completion acknowledgement while a reconnecting character is held
        // or its authoritative progression has not been restored yet.
        if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record))return;
        var memory=record.Progression.Cooking;
        string skill=RebirthServiceCraftSkillService.ClassifyRecipe(name);
        if(skill!="skill.cooking"&&skill!="skill.drink_preparation")return;
        RebirthCookingMemory.Ticket pendingTicket; int previous;
        if(!memory.Tickets.TryGetValue(token,out pendingTicket) || pendingTicket == null ||
            pendingTicket.Recipe != name || pendingTicket.OutputCount != outputCount || completed > pendingTicket.Portions) return;
        memory.Awards.TryGetValue(token,out previous);
        if(completed <= previous) return;
        float current, previewGain;
        var model=TrainingModel(player,token,name,outputCount);
        bool trainingEligible=RebirthSkillAwardService.TryPreviewCraft(player,skill,model,completed-previous,pendingTicket.XpMultiplier,out current,out previewGain);
        // A witnessed, registered successful recipe can be discovered even when its
        // practice model cannot award XP. Missing XP evidence never grants XP.
        if(!memory.Acknowledge(token,name,outputCount,completed,out var ticket,out int newPortions))return;
        RebirthCraftTrainingRules.Model reflectionModel;
        lock(CompletionWitnessSync)CompletionTraining.TryGetValue(token,out reflectionModel);
        if(reflectionModel.Valid&&newPortions>0)
        {
            var solo=record.Progression.SoloTheory;
            if(solo==null)record.Progression.SoloTheory=solo=new RebirthTheorySoloState{CreationId=record.Origin.CreationId};
            if(solo.CreationId==record.Origin.CreationId)solo.RecordAcknowledged(skill,"recipe_process","cooking:"+token+":"+completed,reflectionModel.Difficulty,record.Condition.ActivePlaySeconds);
        }
        float multiplier=ticket.XpMultiplier;
        // The batch projector reevaluates practice falloff/learning modifiers at each skill level.
        // A large queue cannot bypass trivial-recipe falloff by collecting everything at once.
        bool newlyDiscovered = false;
        if(!name.StartsWith("rebirthImprovised",StringComparison.Ordinal))
        {
            var sourceRecipe = XUiM_Recipes.GetRecipes().FirstOrDefault(r => r.GetName() == name);
            bool alreadyKnown = sourceRecipe != null && RebirthCookingCatalogue.Known(player,sourceRecipe);
            bool added = record.Progression.KnowledgeIds.Add("cooking.recipe."+name);
            newlyDiscovered = added && !alreadyKnown;
            var dish=RebirthCookingCatalogue.Get(name);
            if(dish!=null)foreach(string card in dish.Cards)
                if(RebirthProgressionRuntimeConfig.TryGetLiterature(card,out var definition)&&!string.IsNullOrEmpty(definition.KnowledgeId))record.Progression.KnowledgeIds.Add(definition.KnowledgeId);

        }
        if(newlyDiscovered)memory.PendingUnlocks.Add(name);
        RebirthWorldCharacterService.MarkDirty(record,"cooking-completion");
        RebirthStatisticsService.RecordItemsCrafted(player,name,outputCount*newPortions);
        if(trainingEligible)RebirthSkillAwardService.TryAwardCraft(player,skill,model,newPortions,multiplier,"cooking:"+token+":"+completed);
        var completedDish=RebirthCookingCatalogue.Get(name);
        if(skill=="skill.cooking" && completedDish!=null &&
            (completedDish.Tool=="toolCookingPot" || completedDish.Tool=="toolCookingGrill" || completedDish.Tool=="rebirthCookingFryingPan" || completedDish.Tool=="FuriousRamsayBakingPan"))
            RebirthTheoryProgressionService.TryAwardInsight(player,"insight.cooking.successful_use_of_a_major_cooking_method",
                "server-witnessed-cooking-output",out var insightTheory,out var insightAlreadyEarned);
        if (!RebirthWorldCharacterService.FlushPlayer(player,"cooking-completion"))
        {
            // Retry saving the acknowledged state; never repeat its crafting awards.
            RebirthSkillAwardService.QueueOwnerPublication(player);
            return;
        }
        // Publish after dirty/revision and pending-notice updates so a same-revision
        // older snapshot cannot replace newly discovered state.
        if(!name.StartsWith("rebirthImprovised",StringComparison.Ordinal))
            if (!RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"cooking-discovery"))
                RebirthSkillAwardService.QueueOwnerPublication(player);
    }
    public static bool IsBatch(Recipe r)=>r?.ingredients?.Count>0&&r.ingredients[0].itemValue.HasMetadata(Marker);
    private static void RestoreQueueRecipe(Recipe __result)
    {
        if (RebirthStationGridQueue.IsMarked(__result)) RebirthStationGridQueue.TryRestore(__result,XUiM_Recipes.GetRecipes());
        if(!IsBatch(__result))return;
        if(__result.ingredients[0].itemValue.TryGetMetadata(QueuePrefix+"tool",out int tool))__result.craftingToolType=tool;
        __result.UseIngredientModifier=false;
    }
    public static float SkillMultiplier(string name)
    {
        var r=Current;if(r==null||r.GetName()!=name||!IsBatch(r))return 1;
        return r.ingredients[0].itemValue.TryGetMetadata(QueuePrefix+"xp",out float xp)?xp:1;
    }
    public static void Stamp(Recipe recipe,bool book, EntityPlayer player = null, string magazine = null, int portions = 1, bool commit = false)
    {
        if(recipe.ingredients.Count==0)return;
        var marker=recipe.ingredients[0].itemValue;
        float skill=0;
        RebirthServiceCraftSkillService.TryGetSkillValue(player,RebirthServiceCraftSkillService.ClassifyRecipe(recipe),out skill);
        if(commit){marker.SetMetadata(QueuePrefix+"token",Guid.NewGuid().ToString("N"));marker.SetMetadata(QueuePrefix+"portions",portions);}
        marker.SetMetadata(QueuePrefix+"skill",skill);
        marker.SetMetadata(QueuePrefix+"quality",RebirthCookingRules.Quality(skill));
        float nutrition=0,water=0,rawNutrition=0,energy=0;var tags=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var ingredient in recipe.ingredients)
        {
            RebirthConsumableDefinition d;
            if(RebirthConsumableResolver.TryResolve(ingredient.itemValue,out d))
            {
                nutrition+=d.NutritionUnits*ingredient.count;
                energy+=d.EnergyUnits*ingredient.count;
                if(!ingredient.itemValue.HasMetadata("rebirth.cooking.batch")&&RebirthCookingCatalogue.Get(ingredient.itemValue.ItemClass.GetItemName())==null)rawNutrition+=d.NutritionUnits*ingredient.count;
                water+=(d.IsDrink?d.InitialVolumeMl:d.FoodWaterMl)*ingredient.count;
            }
            RebirthFoodMoodDefinition mood;
            if(RebirthFoodMoodResolver.TryResolve(ingredient.itemValue,out mood))foreach(string tag in mood.DietTags)tags.Add(tag);
            else foreach(string tag in RebirthConsumableResolver.Get(ingredient.itemValue.ItemClass,"RebirthDietTags","").Split(','))if(!string.IsNullOrWhiteSpace(tag))tags.Add(tag.Trim());
        }
        bool improvised=recipe.GetName().StartsWith("rebirthImprovised",StringComparison.Ordinal);
        RebirthConsumableDefinition output;
        float cap=RebirthConsumableResolver.TryResolve(ItemClass.GetForId(recipe.itemValueType),out output)?output.NutritionUnits:0;
        float actual=nutrition/Math.Max(1,recipe.count);
        // Preserve authored balance for a named dish. Substitutions adjust it modestly;
        // the free-form dish alone derives its entire nutrition from the chosen inputs.
        float baseline=0;
        var original=XUiM_Recipes.GetRecipes().FirstOrDefault(r=>r.itemValueType==recipe.itemValueType);
        if(original!=null)foreach(var ingredient in original.ingredients)
            if(RebirthConsumableResolver.TryResolve(ingredient.itemValue,out var input))baseline+=input.NutritionUnits*ingredient.count;
        baseline/=Math.Max(1,original?.count??1);
        marker.SetMetadata(Marker,1);
        marker.SetMetadata(QueuePrefix+"tool",recipe.craftingToolType);
        float finalNutrition=RebirthCookingRules.Nutrition(improvised,actual,cap,baseline,skill);
        if(improvised)finalNutrition=actual+(rawNutrition/Math.Max(1,recipe.count))*(.05f+.05f*RebirthCookingRules.Proficiency(skill));
        marker.SetMetadata(QueuePrefix+"nutrition",finalNutrition);
        marker.SetMetadata(QueuePrefix+"energy",energy/Math.Max(1,recipe.count));
        string method=RebirthCookingHeatRules.Method(recipe);
        float retainedWater=water/Math.Max(1,recipe.count)*(output?.IsDrink==true||!NeedsHeat(recipe)?1:method=="Soup"?.9f:method=="Pan"?.5f:.6f);
        float waterCap=output!=null&&output.IsDrink?output.ContainerCapacityMl:improvised?(method=="Soup"?350:method=="Pan"?60:80):Math.Min(350,output?.FoodWaterMl??0);
        marker.SetMetadata(QueuePrefix+"water",Math.Min(waterCap,retainedWater));
        marker.SetMetadata(QueuePrefix+"tags",string.Join(",",tags.OrderBy(t=>t,StringComparer.Ordinal)));
        marker.SetMetadata(QueuePrefix+"xp",book?RebirthCookingRules.BookMultiplier:1f);
        var dish=RebirthCookingCatalogue.Get(recipe.GetName());
        bool seasoned=dish!=null&&recipe.ingredients.Any(i=>RebirthCookingCatalogue.IsHerb(i)&&dish.Herbs.IndexOf(i.itemValue.ItemClass.GetItemName().Replace("foodCrop", ""),StringComparison.OrdinalIgnoreCase)>=0);
        marker.SetMetadata(QueuePrefix+"seasoning",seasoned?2f:0f);
        RebirthFoodMoodResolver.TryResolve(ItemClass.GetForId(recipe.itemValueType),out var outputMood);
        float comfort=outputMood?.BaseMoodInfluence??0;
        string technique=RebirthCookingCatalogue.Effect(magazine);
        if(technique=="H"&&!RebirthCookingCatalogue.HerbTechniqueApplies(magazine,recipe,dish))technique="";
        marker.SetMetadata(QueuePrefix+"comfort",RebirthCookingRules.Comfort(comfort,skill,seasoned,technique));
        marker.SetMetadata(QueuePrefix+"family",improvised?"improvised-meal":outputMood?.VarietyFamilyId??recipe.GetName());
    }
    private static void Created(ItemValue __instance)
    {
        var recipe=Current;
        if(recipe==null||__instance.type!=recipe.itemValueType||!IsBatch(recipe))return;
        CopyOutputData(recipe,__instance);

    }
    public static ItemValue Preview(Recipe recipe)
    {
        var value=new ItemValue(recipe.itemValueType);
        if(IsBatch(recipe))CopyOutputData(recipe,value);
        return value;
    }
    private static void CopyOutputData(Recipe recipe,ItemValue output)
    {
        foreach(var pair in recipe.ingredients[0].itemValue.Metadata)
        {
            if(!pair.Key.StartsWith(QueuePrefix,StringComparison.Ordinal))continue;
            string field=pair.Key.Substring(QueuePrefix.Length);
            switch(field)
            {
                case "token": case "portions": case "tool": case "xp":
                case "held": case "elapsed": case "duration": case "overdue":
                case "method": case "hidden": case "reported": case "preview":
                    continue;
            }
            if(output.Metadata==null)output.Metadata=new Dictionary<string,TypedMetadataValue>();
            output.Metadata[Prefix+field]=pair.Value.Clone();
        }
    }
    public static ItemValue Receipt(Recipe recipe)
    {
        var item=Preview(recipe);
        foreach(string field in new[]{"token","portions","xp"})
            if(recipe.ingredients[0].itemValue.Metadata.TryGetValue(QueuePrefix+field,out var value))
                item.Metadata[Prefix+field]=value.Clone();
        item.SetMetadata(Prefix+"completed",Ordinal(recipe));
        return item;
    }
    private static bool CompletionReceipt(TileEntityWorkstation __instance,int crafterEntityID,ref ItemValue itemCrafted,string recipeName,string itemScrapped,int craftExpGain,int craftedCount)
    {
        if(Current==null||!IsBatch(Current))return true;
        itemCrafted=Receipt(Current);
        int witnessedCompleted;if(GameManager.Instance?.World!=null&&!GameManager.Instance.World.IsRemote()&&itemCrafted.TryGetMetadata(Prefix+"completed",out witnessedCompleted))RecordCompletionWitness(itemCrafted,witnessedCompleted,Current);
        // outputStack asks for XP before it knows whether an output slot is available.
        // Only its successful postfix may acknowledge an open-window batch.
        if(activeUi!=null)return false;
        // Native aggregation merges different batches and owners by item type. Preserve the receipt identity.
        if(__instance.CraftCompleteList==null)__instance.CraftCompleteList=new List<CraftCompleteData>();
        __instance.CraftCompleteList.Add(new CraftCompleteData(crafterEntityID,new ItemStack(itemCrafted,craftedCount),recipeName,itemScrapped,craftExpGain,1));
        __instance.setModified();
        return false;
    }
    private static void DeferredAward(EntityPlayerLocal __instance,CraftCompleteData data)
    {
        var item=data?.CraftedItemStack?.itemValue;
        if(item!=null&&item.HasMetadata(Prefix+"token"))RebirthCookingSessionService.Request(__instance,"complete",data.RecipeName,item:item,count:data.CraftedItemStack.count);
    }
    private static bool BeforeRefund(XUiC_RecipeStack __instance)
    {
        if(RebirthCookingHeat.Managed(__instance.recipe))return false;
        if(!IsBatch(__instance.recipe))return true;
        // These keys describe the cooked batch, never the refunded raw ingredient.
        var value=__instance.recipe.ingredients[0].itemValue;
        foreach(string key in value.Metadata.Keys.Where(k=>k.StartsWith(QueuePrefix,StringComparison.Ordinal)).ToArray())value.Metadata.Remove(key);
        return true;
    }
    // Apply runs for every binding evaluation of a cooked item (10 toolbelt slots x several bindings x every refresh).
    // Rebuilding the definition each time produced ~360 KB/s of garbage, and copying it ~200 KB/s, so one shared instance
    // is kept per distinct (item class, nutrition, energy, water). Definitions are never modified after they are built.
    private struct BatchKey : IEquatable<BatchKey>
    {
        public int ClassId; public float Nutrition, Energy, Water; public byte Present;
        public bool Equals(BatchKey o){return ClassId==o.ClassId&&Present==o.Present&&Nutrition.Equals(o.Nutrition)&&Energy.Equals(o.Energy)&&Water.Equals(o.Water);}
        public override bool Equals(object o){return o is BatchKey k&&Equals(k);}
        public override int GetHashCode(){unchecked{int h=ClassId;h=h*31+Nutrition.GetHashCode();h=h*31+Energy.GetHashCode();h=h*31+Water.GetHashCode();return h*31+Present;}}
    }
    private static readonly Dictionary<BatchKey,RebirthConsumableDefinition> BatchDefinitions=new Dictionary<BatchKey,RebirthConsumableDefinition>();
    public static void ClearBatchDefinitions(){lock(BatchDefinitions)BatchDefinitions.Clear();}
    public static void Apply(ItemValue item,ref RebirthConsumableDefinition definition)
    {
        if(definition==null||!item.HasMetadata(Prefix+"batch"))return;
        var key=new BatchKey{ClassId=item.ItemClass.Id};
        bool hasN=item.TryGetMetadata(Prefix+"nutrition",out float nutrition);
        bool hasE=item.TryGetMetadata(Prefix+"energy",out float energy);
        bool hasW=item.TryGetMetadata(Prefix+"water",out float water);
        key.Present=(byte)((hasN?1:0)|(hasE?2:0)|(hasW?4:0));
        if(hasN)key.Nutrition=nutrition; if(hasE)key.Energy=energy; if(hasW)key.Water=water;
        lock(BatchDefinitions)
        {
            if(BatchDefinitions.TryGetValue(key,out var shared)){definition=shared;return;}
        }
        var copy=definition.Clone();
        if(hasN)copy.NutritionUnits=Math.Max(0,nutrition);
        if(hasE)copy.EnergyUnits=Math.Max(0,energy);
        if(hasW){if(copy.IsDrink)copy.InitialVolumeMl=Math.Max(0,water);else copy.FoodWaterMl=Math.Max(0,water);}
        lock(BatchDefinitions){if(BatchDefinitions.Count>512)BatchDefinitions.Clear();BatchDefinitions[key]=copy;}
        definition=copy;
    }
    public static void ApplyMood(ItemValue item,RebirthFoodMoodDefinition definition)
    {
        if(definition==null||!item.HasMetadata(Prefix+"batch"))return;
        if(item.TryGetMetadata(Prefix+"tags",out string tags)){definition.DietTags.Clear();foreach(string tag in tags.Split(','))if(tag.Length>0)definition.DietTags.Add(tag);}
        if(item.TryGetMetadata(Prefix+"comfort",out float comfort))definition.BaseMoodInfluence=comfort;
        else if(item.TryGetMetadata(Prefix+"seasoning",out float seasoning))definition.BaseMoodInfluence+=Math.Min(2,Math.Max(0,seasoning));
        if(item.TryGetMetadata(Prefix+"family",out string family))definition.VarietyFamilyId=family;
    }
}
