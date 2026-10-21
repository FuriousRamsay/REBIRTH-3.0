using System;
using System.Collections.Generic;
using System.Linq;

// Detached mobile native actor plan. No Begin, setters, source effects or
// payment permission are issued here. The coupled publisher must retain this
// exact plan BEFORE owner effects and observe its exact native queue afterward.
internal static partial class RebirthRemoteResourceFetchedStationPlan
{
    internal const string SourceKey="rebirth.remote.station.source",BindingKey="rebirth.remote.station.binding";
    internal const string ToolKey="rebirth.remote.station.tool",TierKey="rebirth.remote.station.tier";
    private const string Prefix="rebirth.camper.native.",JobKey=Prefix+"job",BatchesKey=Prefix+"batches";
    private const string ActorImage=Prefix+"actor.image",ActorSource=Prefix+"actor.source",ActorSlot=Prefix+"actor.slot",ActorOwner=Prefix+"actor.owner";
    internal sealed class Plan
    {
        internal readonly Guid Job,Source;internal readonly string Binding,Address,Creation,OwnerKey,CanonicalOwner,SourceWorld,World,BaseRecipe;
        internal readonly int Deaths,QueueSlot,Batches,Tier,OwnedBelt,RequiredTool;internal readonly string InstalledDefinition;
        private readonly RebirthCamperEnergyActorImage before,after;private readonly byte[] stationBefore,stationAfter;
        internal Plan(Guid job,RebirthRemoteResourceDurableJournal.Record source,RebirthCamperStationAddress address,string canonical,string world,int deaths,int slot,int batches,int tier,int ownedBelt,int requiredTool,
            string recipe,string installed,RebirthCamperEnergyActorImage prior,RebirthCamperEnergyActorImage next,byte[] oldStation,byte[] newStation)
        {Job=job;Source=source.Operation;Binding=source.Binding;Address=address.ToString();Creation=source.Creation;OwnerKey=source.Owner;CanonicalOwner=canonical;Deaths=deaths;
            QueueSlot=slot;Batches=batches;Tier=tier;OwnedBelt=ownedBelt;RequiredTool=requiredTool;BaseRecipe=recipe;World=world;SourceWorld=source.World;InstalledDefinition=installed;before=prior.Copy();after=next.Copy();stationBefore=(byte[])oldStation.Clone();stationAfter=(byte[])newStation.Clone();}
        internal RebirthCamperEnergyActorImage CopyBeforeActor()=>before.Copy();
        internal RebirthCamperEnergyActorImage CopyAfterActor()=>after.Copy();
        internal byte[] CopyBeforeStation()=>(byte[])stationBefore.Clone();
        internal byte[] CopyAfterStation()=>(byte[])stationAfter.Clone();
        internal RebirthCamperStationState RestoreAfterStation()
        {
            if(!RebirthCamperStationAddress.TryParse(Address,out var address)||!RebirthNativeItemCodec.TryDecode(InstalledDefinition,out var installed))throw new InvalidOperationException();
            var restored=RebirthCamperStationSnapshot.Restore(address.Installation,new ItemStack(installed,1),CopyAfterStation());
            if(QueueSlot<0||QueueSlot>=restored.Queue.Length||!RebirthCamperStationNativeCrafting.RestoreActorImages(restored.Queue[QueueSlot]?.Recipe)||!RestoreRecipeFields(restored.Queue[QueueSlot]?.Recipe))throw new InvalidOperationException();
            return restored;
        }
        internal RecipeQueueItem[] CopyQueueFor(RebirthCamperStationState actual)
        {
            if(!RebirthCamperStationAddress.TryParse(Address,out var address)||actual?.InstallationId!=address.Installation||actual.Queue==null||
                !RebirthStationNativeQueueEntry.TryFindVacantSlot(actual.Queue,Job,out int slot)||slot!=QueueSlot)throw new InvalidOperationException();
            var detached=RestoreAfterStation();if(detached.Queue.Length!=actual.Queue.Length)throw new InvalidOperationException();
            // Preserve every unrelated current native row/reference. Passing
            // a whole detached queue to Begin would incorrectly claim all
            // hydrated rows as changed original admissions.
            var queue=(RecipeQueueItem[])actual.Queue.Clone();queue[QueueSlot]=detached.Queue[QueueSlot];return queue;
        }
    }
    // Native Recipe.Read omits these fields. Restore only this bounded own
    // marker's authored values; this is no source/payment/queue authority.
    internal static bool RestoreRecipeFields(Recipe recipe)
    {
        var marker=recipe?.ingredients?.FirstOrDefault()?.itemValue;
        if(marker==null||!marker.TryGetMetadata(SourceKey,out string source)||!Guid.TryParseExact(source,"N",out var operation)||operation==Guid.Empty||operation.ToString("N")!=source||
            !marker.TryGetMetadata(BindingKey,out string binding)||binding?.Length!=64||binding.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||
            !marker.TryGetMetadata(ToolKey,out int tool)||tool<0||tool!=0&&ItemClass.GetForId(tool)==null||!marker.TryGetMetadata(TierKey,out int tier)||tier<0||tier>6)return false;
        recipe.craftingToolType=tool;recipe.craftingTier=tier;recipe.UseIngredientModifier=false;return true;
    }
    private static bool Allowed(EntityPlayer actor,RebirthCamperStationState station,Recipe recipe,int tier)
    {
        if(recipe==null||recipe.materialBasedRecipe||!RebirthCamperStationNativeCrafting.AllowedFetchedOriginal(actor,station,recipe,tier))return false;
        if(!CraftingManager.GetAllRecipes().Any(candidate=>ReferenceEquals(candidate,recipe)))return false;
        return true;
    }
    private static bool Add(List<ItemStack> fragments,ItemStack actual,string kind,int slot,string owner,int count)
    {
        if(actual==null||actual.IsEmpty()||count<1||count>ushort.MaxValue||actual.itemValue.Metadata?.Keys.Any(key=>key.StartsWith(Prefix,StringComparison.Ordinal)||key==SourceKey||key==BindingKey||key==ToolKey||key==TierKey)==true)return false;
        if(!actual.itemValue.ItemClass.CanStack()&&actual.count!=1)return false;
        string image=RebirthNativeItemCodec.Encode(actual.itemValue);
        if(!RebirthNativeItemCodec.TryDecode(image,out _)||actual.itemValue.Metadata?.Count>byte.MaxValue-4)return false;
        var fragment=actual.Clone();fragment.count=count;fragment.itemValue.SetMetadata(ActorImage,image);fragment.itemValue.SetMetadata(ActorSource,kind);
        fragment.itemValue.SetMetadata(ActorSlot,slot);fragment.itemValue.SetMetadata(ActorOwner,owner);fragments.Add(fragment);return true;
    }
    internal static bool TryCreatePaid(EntityPlayer actor,RebirthCamperStationAddress address,string creation,Guid sourceOperation,Guid job,Recipe recipe,int batches,int tier,out Plan plan)
        =>TryCreate(actor,address,creation,sourceOperation,job,recipe,batches,tier,false,out plan);
    // Preview only from the actual retained immutable source plan, with all
    // source attempts still zero. No caller-provided paid snapshots are used.
    internal static bool TryCreatePlanned(EntityPlayer actor,RebirthCamperStationAddress address,string creation,Guid sourceOperation,Guid job,Recipe recipe,int batches,int tier,out Plan plan)
        =>TryCreate(actor,address,creation,sourceOperation,job,recipe,batches,tier,true,out plan);
    private static bool TryCreate(EntityPlayer actor,RebirthCamperStationAddress address,string creation,Guid sourceOperation,Guid job,Recipe recipe,int batches,int tier,bool preview,out Plan plan)
    {
        plan=null;
        try
        {
            if(!ThreadManager.IsMainThread()||RebirthSurvivorMode.IsEnabledForCurrentWorld()||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||job==Guid.Empty||batches<1||batches>9999||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthOriginalActionScope.Matches(actor,owner.StorageKey,creation)||!RemoteResourceWorldIdentity.TryGet(actor.world,out var world)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,sourceOperation,out var source,out bool unknown)||unknown||source==null||(preview?source.Phase>1:source.Phase!=2)||source.World!=world||source.Creation!=creation)return false;
            var image=source.Copy();
            if((int)image.Attribute("kind")!=(int)RemoteResourceClientOperation.Craft||(int)image.Attribute("deaths")!=actor.Died||(int)image.Attribute("outcome")!=(preview?0:1)||
                (int?)image.Attribute("ownerRejecting")==1||image.Element("ownerAction")!=null||image.Element("ownerRefund")!=null||image.Attribute("ownerNoUseProof")!=null||
                !RebirthStationLiveAccess.TryGetMobileLease(actor,address,creation,out var station,out var lease)||!Allowed(actor,station,recipe,tier)||
                RebirthCamperModuleRuntime.For(station.BoundVehicle).TransferPending||RebirthCamperStationGridCustody.IsHeld(station)||RebirthCamperStationNativeActorPayment.IsHeld(station)||RebirthRemoteResourceSourceCustody.BlocksStation(station)||
                !RebirthStationNativeQueueEntry.TryFindVacantSlot(station.Queue,job,out int slot)||!RebirthCamperEnergySavedOwner.TryRead(owner,out var before,out var saved)||saved.id!=actor.entityId||saved.deaths!=actor.Died)return false;
            if(preview&&image.Element("sources").Elements("source").Any(node=>(int)node.Attribute("attempted")!=0))return false;
            var remote=preview?image.Element("sources").Elements("source").SelectMany(node=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("paid").Value))).ToArray():
                RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(image.Element("removed").Value));
            if(remote.Length<1||remote.Length>8192||remote.Any(stack=>stack==null||stack.IsEmpty()||stack.count<1||stack.count>ushort.MaxValue))return false;
            var requirements=new List<ItemStack>();
            foreach(var ingredient in recipe.ingredients)
            {
                if(!RebirthCraftingIngredientQuantity.TryResolveTotal(actor,recipe,ingredient,tier,batches,out int total)||total<1)return false;
                requirements.Add(new ItemStack(ingredient.itemValue.Clone(),total));
            }
            if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,requirements,remote,actor.inventory.Length)||
                !RebirthRemoteResourceCraftPlan.TryDebit(before,requirements,remote,actor.inventory.Length,out var after,out _))return false;
            var fragments=new List<ItemStack>();
            foreach(int type in requirements.Select(stack=>stack.itemValue.type).Distinct())for(int area=0;area<2;area++)
            {
                var prior=area==0?before.Bag:before.Belt;var next=area==0?after.Bag:after.Belt;int length=area==0?prior.Length:actor.inventory.Length;
                for(int index=0;index<length;index++)if(!prior[index].IsEmpty()&&prior[index].itemValue.type==type)
                {
                    int count=prior[index].count-(next[index].IsEmpty()?0:next[index].count);
                    if(count>0&&!Add(fragments,prior[index],area==0?"bag":"belt",index,owner.CanonicalId,count))return false;
                }
            }
            foreach(var paid in remote)if(!Add(fragments,paid,"fetch",-1,owner.CanonicalId,paid.count))return false;
            if(fragments.Count<1||fragments.Count>8192+189)return false;
            float seconds=EffectManager.GetValue(PassiveEffects.CraftingTime,_originalValue:recipe.craftingTime,_entity:actor,_recipe:recipe,tags:recipe.tags)*XUiM_Recipes.CraftingTimeModifier;
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||recipe.count<1||recipe.count>32767||recipe.craftExpGain<0)return false;
            var frozen=new Recipe{Version=recipe.Version,itemValueType=recipe.itemValueType,count=recipe.count,craftingTime=seconds,craftingArea=recipe.craftingArea,craftingToolType=recipe.craftingToolType,
                craftingTier=tier,craftExpGain=recipe.craftExpGain,unlockExpGain=recipe.unlockExpGain,tags=recipe.tags,Effects=recipe.Effects,UseIngredientModifier=false,tooltip=recipe.tooltip,
                wildcardForgeCategory=recipe.wildcardForgeCategory,wildcardCampfireCategory=recipe.wildcardCampfireCategory,IsTrackable=recipe.IsTrackable,isQuest=recipe.isQuest,isChallenge=recipe.isChallenge,IsTracked=recipe.IsTracked,IsLearnable=recipe.IsLearnable};
            // Preserve inherited marker only inside ActorImage for cancellation;
            // it cannot select this new recipe's canonical output catalogue.
            RebirthRecipeOutputs.ClearFrozen(fragments[0].itemValue);
            if(fragments[0].itemValue.Metadata?.Count>byte.MaxValue-7)return false; // Original scope, native missing fields, frozen real outputs.
            fragments[0].itemValue.SetMetadata(JobKey,job.ToString("N"));fragments[0].itemValue.SetMetadata(BatchesKey,batches);
            fragments[0].itemValue.SetMetadata(SourceKey,source.Operation.ToString("N"));fragments[0].itemValue.SetMetadata(BindingKey,source.Binding);
            fragments[0].itemValue.SetMetadata(ToolKey,recipe.craftingToolType);fragments[0].itemValue.SetMetadata(TierKey,tier);
            frozen.ingredients.AddRange(fragments);if(!RebirthRecipeOutputs.Freeze(frozen))return false;
            var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(address.Installation)?.InstalledItem;
            if(installed==null)return false;string definition=RebirthNativeItemCodec.Encode(installed.itemValue);if(!RebirthNativeItemCodec.TryDecode(definition,out _))return false;
            var oldStation=RebirthCamperStationSnapshot.Capture(address.Installation,station);var candidate=RebirthCamperStationSnapshot.Restore(address.Installation,installed,oldStation);
            candidate.Tools=ItemStack.Clone(station.Tools);candidate.Fuel=ItemStack.Clone(station.Fuel);candidate.Input=ItemStack.Clone(station.Input);candidate.Output=ItemStack.Clone(station.Output);
            candidate.lastInput=ItemStack.Clone(station.lastInput);candidate.CraftCompleteList=station.CraftCompleteList==null?null:new List<CraftCompleteData>(station.CraftCompleteList);
            // Match native actor payment's zero clock baseline without
            // mutating the live station during this detached planning pass.
            candidate.lastTickTime=0;candidate.Queue=(RecipeQueueItem[])station.Queue.Clone();oldStation=RebirthCamperStationSnapshot.Capture(address.Installation,candidate);
            candidate.Queue=(RecipeQueueItem[])station.Queue.Clone();candidate.Queue[slot]=new RecipeQueueItem{Recipe=frozen,Multiplier=(short)batches,Quality=(byte)tier,CraftingTimeLeft=seconds,OneItemCraftTime=seconds,StartingEntityId=actor.entityId,IsCrafting=slot==station.Queue.Length-1};
            candidate.lastTickTime=0;candidate.setModified();var newStation=RebirthCamperStationSnapshot.Capture(address.Installation,candidate);
            if(!RebirthStationLiveAccess.TryGetMobileLease(actor,address,creation,out var current,out var currentLease)||!ReferenceEquals(current,station)||!ReferenceEquals(currentLease,lease))return false;
            plan=new Plan(job,source,address,owner.CanonicalId,actor.world.worldState.Guid,actor.Died,slot,batches,tier,actor.inventory.Length,recipe.craftingToolType,
                RebirthRemoteResourceOwnerAction.RecipeImage(recipe),definition,before,after,oldStation,newStation);return true;
        }
        catch{plan=null;return false;}
    }
}
