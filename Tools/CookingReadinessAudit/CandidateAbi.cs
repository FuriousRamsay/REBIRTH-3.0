using System;using System.Collections.Generic;using System.Linq;
// Native/mod types below are actual installed DLL APIs. Private host collaborators are explicit adapters.
public class CandidateAbi : XUiController {
 private bool open=true;private int pullUiGeneration;private Recipe result,selected;private int batch=1;private bool preparingRequest,submittingCook;
 private XUiC_RebirthCookingSlot[] slots=new XUiC_RebirthCookingSlot[12];
 private XUiC_RebirthCookingStation station;
 public RebirthCookingPreparation Preparation=new RebirthCookingPreparation();
 private string ActiveMagazine=>null;
 private bool Matches(Recipe recipe,List<ItemStack> inputs,bool count){throw new NotSupportedException("Private host matcher adapter; ABI compile only");}
 private Recipe BatchRecipe(string magazine){throw new NotSupportedException("Private host builder adapter; ABI compile only");}
    private long cookingReadinessRevision,cachedCookingReadinessRevision=-1;
    private Recipe cachedCookingReadinessRecipe;
    private string cachedCookingReadinessBlocker;
    private bool cachedCookingReadinessValid;
    // A revision is one complete Render; never retain native quantity effects across renders.
    // Cook always rebuilds: presentation cache cannot authorize payment or enqueue.
    private bool TryCookingPreflight(bool fresh,out Recipe recipe,out string blocker)
    {
        if(fresh)return BuildCookingPreflight(out recipe,out blocker);
        if(cachedCookingReadinessRevision!=cookingReadinessRevision)
        {
            cachedCookingReadinessValid=BuildCookingPreflight(out cachedCookingReadinessRecipe,out cachedCookingReadinessBlocker);
            cachedCookingReadinessRevision=cookingReadinessRevision;
        }
        recipe=cachedCookingReadinessRecipe;blocker=cachedCookingReadinessBlocker;
        return cachedCookingReadinessValid;
    }
    private sealed class CookingPreflightWitness
    {
        internal EntityPlayerLocal Owner;internal World World;internal Recipe Result,Selected;internal object Station,Queue,Xui,PlayerUi,WindowGroup;internal object[] QueueEntries;internal int UiGeneration;
        internal int Batch;internal string Magazine,ResultImage,SelectedImage;internal object ResultEffects,SelectedEffects;internal ItemStack[] Inputs;
    }
    private CookingPreflightWitness lastBuiltCookingWitness;
    // Exact native recipe image plus omitted native catalogue fields; comparison only, no grant authority.
    private static string CookingRecipeImage(Recipe source)
    {
        if(source==null)return null;
        using(var bytes=new System.IO.MemoryStream())
        {
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(bytes);source.Write(writer);writer.Write(source.craftingToolType);
                writer.Write(source.UseIngredientModifier);writer.Write(source.tags.ToString());
                writer.Write(source.materialBasedRecipe);writer.Write(source.wildcardForgeCategory);writer.Write(source.wildcardCampfireCategory);
            }
            return Convert.ToBase64String(bytes.ToArray());
        }
    }
    private bool CookingPreflightContextCurrent(CookingPreflightWitness witness)
    {
        return witness!=null&&open&&windowGroup!=null&&windowGroup.isShowing&&
            ReferenceEquals(xui,witness.Xui)&&ReferenceEquals(xui?.playerUI,witness.PlayerUi)&&ReferenceEquals(windowGroup,witness.WindowGroup)&&pullUiGeneration==witness.UiGeneration&&
            !preparingRequest&&!submittingCook&&!Preparation.IsPreparing&&!RebirthBackpackLibraryReservation.BlocksResourceUse(witness.Owner)&&
            ReferenceEquals(xui?.playerUI?.entityPlayer,witness.Owner)&&witness.Owner!=null&&
            ReferenceEquals(witness.Owner.world,witness.World)&&ReferenceEquals(GameManager.Instance?.World,witness.World)&&
            ReferenceEquals(witness.World.GetEntity(witness.Owner.entityId),witness.Owner)&&
            ReferenceEquals(result,witness.Result)&&ReferenceEquals(selected,witness.Selected)&&batch==witness.Batch&&ActiveMagazine==witness.Magazine&&
            ReferenceEquals(station,witness.Station)&&ReferenceEquals(station?.craftingQueue,witness.Queue);
    }
    private bool CookingPreflightStillCurrent(CookingPreflightWitness witness)
    {
        if(!CookingPreflightContextCurrent(witness)||witness.Inputs==null||witness.Inputs.Length!=slots.Length)return false;
        // Bounded repeated readback catches a later-cell serialization reentry changing an earlier cell.
        for(int pass=0;pass<2;pass++)
        {
            if(!CookingPreflightContextCurrent(witness)||CookingRecipeImage(result)!=witness.ResultImage||CookingRecipeImage(selected)!=witness.SelectedImage||
                !ReferenceEquals(result?.Effects,witness.ResultEffects)||!ReferenceEquals(selected?.Effects,witness.SelectedEffects))return false;
            var currentQueue=station.craftingQueue.GetRecipesToCraft().ToArray();
            if(witness.QueueEntries==null||currentQueue.Length!=witness.QueueEntries.Length)return false;
            for(int i=0;i<currentQueue.Length;i++)if(!ReferenceEquals(currentQueue[i],witness.QueueEntries[i])||currentQueue[i].GetRecipe()!=null)return false;
            for(int i=0;i<slots.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(slots[i].ItemStack,witness.Inputs[i]))return false;
        }
        return CookingPreflightContextCurrent(witness);
    }
    private bool BuildCookingPreflight(out Recipe recipe,out string blocker)
    {
        recipe=null;blocker=null;
        if(RebirthBackpackLibraryReservation.BlocksResourceUse(xui?.playerUI?.entityPlayer)){blocker=Localization.Get("xuiRebirthLibraryTransferPending");return false;}
        if(Preparation.IsPreparing||preparingRequest){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingPreparing","Preparing");return false;}
        if(submittingCook){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingSubmitting","Waiting for the cooking request…");return false;}
        if(result==null){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingAddIngredients","Add ingredients");return false;}
        var originalOwner=xui?.playerUI?.entityPlayer;var originalWorld=originalOwner?.world;
        var originalResult=result;var originalSelected=selected;int originalBatch=batch;string originalMagazine=ActiveMagazine;
        var inputSnapshot=slots.Select(s=>s.ItemStack.Clone()).ToArray();
        var witness=new CookingPreflightWitness{Owner=originalOwner,World=originalWorld,Result=originalResult,Selected=originalSelected,Batch=originalBatch,Magazine=originalMagazine,Inputs=inputSnapshot,Xui=xui,PlayerUi=xui.playerUI,WindowGroup=windowGroup,UiGeneration=pullUiGeneration,Station=station,Queue=station.craftingQueue,QueueEntries=station.craftingQueue.GetRecipesToCraft().Cast<object>().ToArray(),ResultImage=CookingRecipeImage(originalResult),SelectedImage=CookingRecipeImage(originalSelected),ResultEffects=originalResult?.Effects,SelectedEffects=originalSelected?.Effects};
        var inputs=inputSnapshot.Take(9).Where(s=>!s.IsEmpty()).ToList();
        if(inputs.Count==0){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingPullOrAddFirst", "Pull or add ingredients first.");return false;}
        if(selected!=null&&!Matches(selected,inputs,true)){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingLoadRequestedServings", "Load all ingredients for the requested servings.");return false;}
        if(inputs.Any(i=>!RebirthCookingItemStats.FullIngredient(i.itemValue))){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingFullContainersOnly", "Drink ingredients must be in full containers.");return false;}
        if(inputSnapshot.Any(s=>!s.IsEmpty()&&s.count<batch)||inputSnapshot.Where(s=>!s.IsEmpty()).GroupBy(s=>s.itemValue.type).Any(g=>g.Count()>1)){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingOneSlotPerIngredient", "Use each ingredient type in one slot, with enough for the batch.");return false;}
        recipe=BatchRecipe(ActiveMagazine);
        if(station.IsMilling)
        {
            RebirthStationGridIngredients.Plan exact;
            if(inputSnapshot.Skip(9).Any(s=>!s.IsEmpty())||
                !RebirthStationGridIngredients.TryPlan(xui.playerUI.entityPlayer,result,
                    inputSnapshot.Take(9).ToArray(),batch,recipe.craftingTier,out exact))
            {blocker=Localization.Get("xuiRebirthStationExactMaterialsRequired");return false;}
            // The live queue must pay the same native-adjusted quantities admitted by the grid.
            // Milling currently permits one slot per type, so each fragment is a whole per-batch requirement.
            recipe.ingredients.Clear();
            foreach(var allocation in exact.Inputs)
            {
                if(allocation.Count%batch!=0){blocker=Localization.Get("xuiRebirthStationExactMaterialsRequired");return false;}
                var paid=allocation.Snapshot();paid.count=allocation.Count/batch;recipe.ingredients.Add(paid);
            }
        }
        if(!station.CraftingRequirementsValid(recipe)){blocker=station.CraftingRequirementsInvalidMessage(recipe);return false;} // Outcome already names the missing requirement.

        if(station.craftingQueue.GetRecipesToCraft().Any(e=>e.GetRecipe()!=null)){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingTakeCurrentBatch","Take the current batch before starting another.");return false;}
        if(!CookingPreflightStillCurrent(witness)){recipe=null;blocker=Localization.Get("xuiRebirthStationExactMaterialsRequired");return false;}
        lastBuiltCookingWitness=witness;return true;
    }


}