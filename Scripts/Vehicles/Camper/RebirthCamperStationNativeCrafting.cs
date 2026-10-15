using System;
using System.Collections.Generic;
using System.Linq;

// Native-mode jobs stay wholly in the native vehicle station image. They do not
// enter Survivor admissions, discoveries, training, or owner reward journals.
internal static class RebirthCamperStationNativeCrafting
{
    private const string JobKey="rebirth.camper.native.job";
    private const string BatchesKey="rebirth.camper.native.batches";
    private const string UnitsKey="rebirth.camper.native.units";
    private const string MaterialKey="rebirth.camper.native.material";
    private const string SlotKey="rebirth.camper.native.slot";
    private const string ActorSourceKey="rebirth.camper.native.actor.source";
    private const string ActorSlotKey="rebirth.camper.native.actor.slot";
    private const string ActorOwnerKey="rebirth.camper.native.actor.owner";
    private const string ActorImageKey="rebirth.camper.native.actor.image";
    internal static bool RestoreActorImages(Recipe recipe)
    {
        if(recipe==null)return true; // Native empty cancellation row.
        foreach(var fragment in recipe.ingredients)
        {
            if(fragment?.itemValue==null)return false;
            if(!fragment.itemValue.HasMetadata(ActorSourceKey))continue;
            if(!fragment.itemValue.TryGetMetadata(ActorImageKey,out string image)||
                !RebirthNativeItemCodec.TryDecode(image,out var value)||value==null||value.IsEmpty())return false;
            value.Metadata=new Dictionary<string,TypedMetadataValue>(fragment.itemValue.Metadata);
            fragment.itemValue=value;
        }
        return true;
    }
    internal static bool TryJob(Recipe recipe,out Guid job)
    {
        job=Guid.Empty;
        var value=recipe?.ingredients?.Count>0?recipe.ingredients[0]?.itemValue:null;
        return value!=null&&value.TryGetMetadata(JobKey,out string text)&&Guid.TryParseExact(text,"N",out job)&&job!=Guid.Empty;
    }
    private static bool Resolve(EntityPlayer player,RebirthCamperStationAddress address,string creation,
        out RebirthCamperStationState station,out object lease)
    {
        station=null;lease=null;
        return ThreadManager.IsMainThread()&&!RebirthSurvivorMode.IsEnabledForCurrentWorld()&&player!=null&&!player.IsDead()&&
            player.world!=null&&!player.world.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,player.world)&&
            ReferenceEquals(player.world.GetEntity(player.entityId),player)&&creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&
            RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out station,out lease)&&
            !RebirthCamperModuleRuntime.For(station.BoundVehicle).TransferPending&&!RebirthCamperStationGridCustody.IsHeld(station);
    }
    private static bool Allowed(EntityPlayer player,RebirthCamperStationState station,Recipe recipe,int tier)
    {
        if(recipe==null||recipe.IsScrap||tier<0||tier>6||!recipe.IsUnlocked(player)||
            !RebirthRecipeDiscoveryRules.Allows(player,recipe.GetName())||station?.block==null)return false;
        string areas=station.block.Properties.Contains("Workstation","CraftingAreaRecipes")?
            station.block.Properties.GetString("Workstation","CraftingAreaRecipes"):station.block.GetBlockName();
        return !string.IsNullOrEmpty(areas)&&areas.Split(',').Any(a=>string.Equals(a.Trim()=="player"?"":a.Trim(),
            recipe.craftingArea??"",StringComparison.OrdinalIgnoreCase))&&RebirthStationToolAvailability.HasRequirement(station.Tools,recipe)&&
            (recipe.GetOutputItemClass()?.HasQuality!=true?tier==0:tier>=1&&tier<=Math.Min(XUiM_Recipes.CraftingMaxTier,recipe.GetCraftingTier(player)));
    }
    private static bool Plan(EntityPlayer player,RebirthCamperStationState station,Recipe recipe,int batches,int tier,
        out ItemStack[] after,out List<ItemStack> paid,out RebirthStationPlayerIngredients.Plan actorPlan)
    {
        after=null;paid=null;actorPlan=null;
        if(batches<1||batches>9999||!Allowed(player,station,recipe,tier)||station.Input==null||
            station.InputSlotCount<0||station.InputSlotCount>station.Input.Length||recipe.count<1||recipe.count>32767)return false;
        if(!recipe.materialBasedRecipe&&RebirthStationPlayerIngredients.UsesPlayerPayment(player,station))
        {
            if(!RebirthCamperStationNativeActorPayment.IsInstalled||!(player is EntityPlayerLocal)||
                !RebirthStationPlayerIngredients.TryPlan(player,recipe,batches,tier,player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,
                    RebirthCamperStationNativeActorPayment.Locks(player.bag?.LockedSlots),
                    RebirthCamperStationNativeActorPayment.Locks(player.inventory?.ItemGrid?.SlotLocks),player.inventory.Length,out actorPlan))return false;
            paid=actorPlan.PaidSnapshots();
            if(paid.Count==0||paid.Count>9)return false;
            string owner=GameManager.Instance.getPersistentPlayerID(null)?.CombinedString;if(string.IsNullOrEmpty(owner))return false;
            for(int i=0;i<paid.Count;i++)
            {
                var value=paid[i].itemValue;
                if(value.Metadata!=null&&(value.Metadata.Count>byte.MaxValue-6||value.Metadata.Keys.Any(k=>k.StartsWith("rebirth.camper.native.",StringComparison.Ordinal))))return false;
                string image=RebirthNativeItemCodec.Encode(value);if(string.IsNullOrEmpty(image)||image.Length>196608)return false;
                value.SetMetadata(ActorImageKey,image);
                value.SetMetadata(ActorSourceKey,actorPlan.Inputs[i].IsBag?"bag":"belt");
                value.SetMetadata(ActorSlotKey,actorPlan.Inputs[i].Slot);value.SetMetadata(ActorOwnerKey,owner);
            }
            after=station.Input.Select(s=>s.Clone()).ToArray();return true;
        }
        if(recipe.materialBasedRecipe)
        {
            if(!RebirthStationGridIngredients.TryPlanMaterial(player,recipe,station.Input,station.InputSlotCount,
                station.MaterialNames,batches,tier,out var materialPlan)||
                !RebirthStationGridIngredients.TryBuildDebitedGrid(materialPlan,station.Input,out after))return false;
            var materialFragments=new List<ItemStack>();
            foreach(var allocation in materialPlan.Inputs)
            {
                var fragment=allocation.Snapshot();
                if(fragment.itemValue.Metadata!=null&&fragment.itemValue.Metadata.Count>byte.MaxValue-5)return false;
                fragment.itemValue.SetMetadata(UnitsKey,allocation.Count);
                fragment.itemValue.SetMetadata(MaterialKey,"unit_"+allocation.MaterialName);
                fragment.itemValue.SetMetadata(SlotKey,allocation.Slot);
                fragment.count=1;materialFragments.Add(fragment);
            }
            if(materialFragments.Count==0||materialFragments.Count>9)return false;
            paid=materialFragments;return true;
        }
        var need=new Dictionary<int,int>();
        foreach(var ingredient in recipe.ingredients)
        {
            if(!RebirthCraftingIngredientQuantity.TryResolveTotal(player,recipe,ingredient,tier,batches,out var count))return false;
            need.TryGetValue(ingredient.itemValue.type,out var previous);
            if((long)previous+count>int.MaxValue)return false;need[ingredient.itemValue.type]=previous+count;
        }
        var next=station.Input.Select(s=>s?.Clone()).ToArray();var fragments=new List<ItemStack>();
        for(int slot=0;slot<station.InputSlotCount;slot++)
        {
            var item=next[slot];
            if(item==null||item.itemValue==null||item.count<0||item.count>ushort.MaxValue)return false;
            if(item.IsEmpty()||item.itemValue.HasModSlots&&item.itemValue.HasMods()||!need.TryGetValue(item.itemValue.type,out var count))continue;
            int take=Math.Min(count,item.count);if(take==0)continue;
            if(item.itemValue.Metadata!=null&&item.itemValue.Metadata.Keys.Any(k=>k.StartsWith("rebirth.camper.native.",StringComparison.Ordinal)))return false;
            var fragment=item.Clone();fragment.count=take;fragments.Add(fragment);item.count-=take;
            if(item.count==0)next[slot]=ItemStack.Empty.Clone();need[item.itemValue.type]=count-take;
        }
        if(need.Values.Any(n=>n!=0)||fragments.Count==0||fragments.Count>9)return false;
        after=next;paid=fragments;return true;
    }
    internal static int Maximum(EntityPlayer player,RebirthCamperStationAddress address,string creation,Recipe recipe,int tier)
    {
        if(!Resolve(player,address,creation,out var station,out _))return 0;
        int low=0,high=9999;
        while(low<high){int mid=low+(high-low+1)/2;if(Plan(player,station,recipe,mid,tier,out _,out _,out _))low=mid;else high=mid-1;}
        return low;
    }
    internal static bool CanCraft(EntityPlayer player,RebirthCamperStationAddress address,string creation,Recipe recipe,int batches,int tier)
        =>Resolve(player,address,creation,out var station,out _)&&Plan(player,station,recipe,batches,tier,out _,out _,out _);
    internal static bool Craft(EntityPlayer player,RebirthCamperStationAddress address,string creation,Guid job,Recipe recipe,int batches,int tier)
    {
        if(RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var retained,out _)&&
            RebirthCamperStationNativeActorPayment.IsHeld(retained))
            return RebirthCamperStationNativeActorPayment.MatchesPendingOperation(retained,job)&&RebirthCamperStationNativeActorPayment.TryResume(player,retained,address,creation);
        if(job==Guid.Empty||!Resolve(player,address,creation,out var station,out var lease)||
            !RebirthStationNativeQueueEntry.TryFindVacantSlot(station.Queue,job,out var slot)||
            !Plan(player,station,recipe,batches,tier,out var after,out var paid,out var actorPlan))return false;
        foreach(var row in station.Queue)if(TryJob(row?.Recipe,out var prior)&&prior==job)return false;
        float seconds=EffectManager.GetValue(PassiveEffects.CraftingTime,_originalValue:recipe.craftingTime,
            _entity:player,_recipe:recipe,tags:recipe.tags)*XUiM_Recipes.CraftingTimeModifier;
        if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||recipe.craftExpGain<0||
            paid[0].itemValue.Metadata!=null&&paid[0].itemValue.Metadata.Count>byte.MaxValue-2)return false;
        // Copy catalogue fields, then freeze modifier-resolved paid fragments.
        var frozen=new Recipe{Version=recipe.Version,itemValueType=recipe.itemValueType,count=recipe.count,
            craftingTime=seconds,craftingArea=recipe.craftingArea,craftingToolType=recipe.craftingToolType,
            craftingTier=tier,craftExpGain=recipe.craftExpGain,unlockExpGain=recipe.unlockExpGain,tags=recipe.tags,
            Effects=recipe.Effects,UseIngredientModifier=false,tooltip=recipe.tooltip};
        frozen.wildcardForgeCategory=recipe.wildcardForgeCategory;frozen.wildcardCampfireCategory=recipe.wildcardCampfireCategory;
        frozen.IsTrackable=recipe.IsTrackable;frozen.isQuest=recipe.isQuest;frozen.isChallenge=recipe.isChallenge;
        frozen.IsTracked=recipe.IsTracked;frozen.IsLearnable=recipe.IsLearnable;
        paid[0].itemValue.SetMetadata(JobKey,job.ToString("N"));paid[0].itemValue.SetMetadata(BatchesKey,batches);
        foreach(var item in paid)frozen.ingredients.Add(item);
        var queue=(RecipeQueueItem[])station.Queue.Clone();
        queue[slot]=new RecipeQueueItem{Recipe=frozen,Multiplier=checked((short)batches),Quality=(byte)tier,CraftingTimeLeft=seconds,
            OneItemCraftTime=seconds,StartingEntityId=player.entityId,IsCrafting=slot==queue.Length-1};
        if(!Resolve(player,address,creation,out var current,out var currentLease)||!ReferenceEquals(current,station)||!ReferenceEquals(lease,currentLease))return false;
        if(actorPlan!=null)
        {
            if(!actorPlan.Matches(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,
                RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),
                RebirthCamperStationNativeActorPayment.Locks(player.inventory.ItemGrid.SlotLocks),player.inventory.Length)||
                !actorPlan.TryGetImages(out var bagBefore,out var bagAfter,out var beltBefore,out var beltAfter))return false;
            return RebirthCamperStationNativeActorPayment.Begin(player,station,address,creation,job,bagBefore,bagAfter,beltBefore,beltAfter,queue);
        }
        // Both arrays are native station-owned fields; no inventory callbacks occur
        // between debit and queue insertion. Persist the resulting vehicle image.
        station.Input=after;station.Queue=queue;station.ResetTickTime();station.setModified();
        RebirthCamperModuleRuntime.For(station.BoundVehicle).Persist(true);return true;
    }
    internal static bool BuildActorQueue(EntityPlayer player,RebirthCamperStationState station,Guid job,Recipe recipe,int batches,int tier,
        RebirthStationPlayerIngredients.Plan plan,string owner,out RecipeQueueItem[] queue)
    {
        queue=null;
        if(job==Guid.Empty||plan==null||batches<1||batches>9999||!Allowed(player,station,recipe,tier)||
            !RebirthStationNativeQueueEntry.TryFindVacantSlot(station.Queue,job,out int slot))return false;
        foreach(var row in station.Queue)if(TryJob(row?.Recipe,out var prior)&&prior==job)return false;
        var paid=plan.PaidSnapshots();if(paid.Count<1||paid.Count>9||string.IsNullOrEmpty(owner))return false;
        for(int i=0;i<paid.Count;i++)
        {
            var value=paid[i].itemValue;
            if(value.Metadata!=null&&(value.Metadata.Count>byte.MaxValue-6||value.Metadata.Keys.Any(k=>k.StartsWith("rebirth.camper.native.",StringComparison.Ordinal))))return false;
            string image=RebirthNativeItemCodec.Encode(value);if(image.Length>196608)return false;
            value.SetMetadata(ActorImageKey,image);value.SetMetadata(ActorSourceKey,plan.Inputs[i].IsBag?"bag":"belt");
            value.SetMetadata(ActorSlotKey,plan.Inputs[i].Slot);value.SetMetadata(ActorOwnerKey,owner);
        }
        float seconds=EffectManager.GetValue(PassiveEffects.CraftingTime,_originalValue:recipe.craftingTime,_entity:player,_recipe:recipe,tags:recipe.tags)*XUiM_Recipes.CraftingTimeModifier;
        if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||recipe.count<1||recipe.count>32767||recipe.craftExpGain<0)return false;
        var frozen=new Recipe{Version=recipe.Version,itemValueType=recipe.itemValueType,count=recipe.count,craftingTime=seconds,
            craftingArea=recipe.craftingArea,craftingToolType=recipe.craftingToolType,craftingTier=tier,craftExpGain=recipe.craftExpGain,
            unlockExpGain=recipe.unlockExpGain,tags=recipe.tags,Effects=recipe.Effects,UseIngredientModifier=false,tooltip=recipe.tooltip,
            wildcardForgeCategory=recipe.wildcardForgeCategory,wildcardCampfireCategory=recipe.wildcardCampfireCategory,
            IsTrackable=recipe.IsTrackable,isQuest=recipe.isQuest,isChallenge=recipe.isChallenge,IsTracked=recipe.IsTracked,IsLearnable=recipe.IsLearnable};
        paid[0].itemValue.SetMetadata(JobKey,job.ToString("N"));paid[0].itemValue.SetMetadata(BatchesKey,batches);
        foreach(var item in paid)frozen.ingredients.Add(item);
        queue=(RecipeQueueItem[])station.Queue.Clone();queue[slot]=new RecipeQueueItem{Recipe=frozen,Multiplier=(short)batches,Quality=(byte)tier,
            CraftingTimeLeft=seconds,OneItemCraftTime=seconds,StartingEntityId=player.entityId,IsCrafting=slot==queue.Length-1};return true;
    }
    internal static bool Cancel(EntityPlayer player,RebirthCamperStationAddress address,string creation,Guid job)
    {
        if(RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var retained,out _)&&RebirthCamperStationNativeActorPayment.IsHeld(retained))
            return RebirthCamperStationNativeActorPayment.MatchesPendingOperation(retained,CancelOperation(job))&&RebirthCamperStationNativeActorPayment.TryResume(player,retained,address,creation);
        if(!Resolve(player,address,creation,out var station,out var lease)||job==Guid.Empty)return false;
        int selected=-1;
        for(int i=0;i<station.Queue.Length;i++)if(TryJob(station.Queue[i]?.Recipe,out var current)&&current==job)
        {if(selected>=0)return false;selected=i;}
        if(selected<0)return false;var row=station.Queue[selected];
        if(row.RepairItem!=null||RebirthStationGridQueue.IsMarked(row.Recipe)||!TryRemainingRefund(row,out var refunds))return false;
        if(row.Recipe.ingredients.Any(f=>f.itemValue.HasMetadata(ActorSourceKey)))
            return CancelActor(player,station,address,creation,lease,job,selected,refunds);
        var input=station.Input.Select(s=>s?.Clone()).ToArray();
        foreach(var refund in refunds)
        {
            if(refund.itemValue.HasMetadata(MaterialKey))
            {
                if(!refund.itemValue.TryGetMetadata(MaterialKey,out string name)||
                    !refund.itemValue.TryGetMetadata(SlotKey,out int slot)||slot<station.InputSlotCount||slot>=input.Length||
                    station.MaterialNames==null||slot-station.InputSlotCount>=station.MaterialNames.Length||
                    name!="unit_"+station.MaterialNames[slot-station.InputSlotCount]||input[slot]?.itemValue==null||
                    input[slot].itemValue.type!=refund.itemValue.type||input[slot].count<0||
                    (long)input[slot].count+refund.count>int.MaxValue)return false;
                input[slot].count+=refund.count;continue;
            }
            // Refuse a full grid before changing either queue or stock.
            var cells=new ItemStack[station.InputSlotCount];Array.Copy(input,cells,cells.Length);
            int maximum=refund.itemValue.ItemClass?.MaxCount??0;
            if(maximum<1||ItemStack.AddToItemStackArray(cells,refund,maximum)==-1)return false;
            Array.Copy(cells,input,cells.Length);
        }
        var queue=(RecipeQueueItem[])station.Queue.Clone();queue[selected]=new RecipeQueueItem{StartingEntityId=-1};
        if(!Resolve(player,address,creation,out var live,out var liveLease)||!ReferenceEquals(live,station)||!ReferenceEquals(lease,liveLease))return false;
        station.Input=input;station.Queue=queue;station.ResetTickTime();station.setModified();
        RebirthCamperModuleRuntime.For(station.BoundVehicle).Persist(true);return true;
    }
    // Paid fragments are the original total debit. Native Multiplier is the
    // remaining batch count. Consume original fragments in stable slot order,
    // retaining exact metadata for the unconsumed suffix of each item type.
    internal static bool TryRemainingRefund(RecipeQueueItem row,out List<ItemStack> refunds)
    {
        refunds=null;
        if(row?.Recipe==null||!TryJob(row.Recipe,out _)||row.Multiplier<1||row.Multiplier>9999)return false;
        var marker=row.Recipe.ingredients[0].itemValue;
        int original=1;
        if(marker.HasMetadata(BatchesKey)&&(!marker.TryGetMetadata(BatchesKey,out original)||original<1||original>9999))return false;
        if(row.Multiplier>original)return false;
        var totals=new Dictionary<int,long>();
        foreach(var fragment in row.Recipe.ingredients)
        {
            if(fragment?.itemValue==null||fragment.itemValue.IsEmpty()||!PaidCount(fragment,out var quantity))return false;
            totals.TryGetValue(fragment.itemValue.type,out long prior);totals[fragment.itemValue.type]=prior+quantity;
        }
        var consumed=new Dictionary<int,long>();
        foreach(var pair in totals)
        {
            if(pair.Value%original!=0)return false;
            consumed[pair.Key]=(pair.Value/original)*(original-row.Multiplier);
        }
        var result=new List<ItemStack>();
        foreach(var fragment in row.Recipe.ingredients)
        {
            if(!PaidCount(fragment,out var quantity))return false;
            int type=fragment.itemValue.type;int spent=(int)Math.Min(quantity,consumed[type]);consumed[type]-=spent;
            if(spent==quantity)continue;
            var remaining=fragment.Clone();remaining.count=quantity-spent;
            if(fragment.itemValue.HasMetadata(ActorSourceKey))
            {
                if(!fragment.itemValue.TryGetMetadata(ActorImageKey,out string originalImage)||
                    !RebirthNativeItemCodec.TryDecode(originalImage,out var originalValue)||originalValue==null||originalValue.IsEmpty()||
                    !fragment.itemValue.TryGetMetadata(ActorSourceKey,out string kind)||!fragment.itemValue.TryGetMetadata(ActorSlotKey,out int sourceSlot)||
                    !fragment.itemValue.TryGetMetadata(ActorOwnerKey,out string sourceOwner))return false;
                remaining.itemValue=originalValue;remaining.itemValue.SetMetadata(ActorSourceKey,kind);
                remaining.itemValue.SetMetadata(ActorSlotKey,sourceSlot);remaining.itemValue.SetMetadata(ActorOwnerKey,sourceOwner);
            }
            remaining.itemValue.Metadata?.Remove(JobKey);remaining.itemValue.Metadata?.Remove(BatchesKey);result.Add(remaining);
        }
        refunds=result;return true;
    }
    private static bool PaidCount(ItemStack fragment,out int quantity)
    {
        quantity=fragment.count;
        if(fragment.itemValue.HasMetadata(UnitsKey))
            return fragment.count==1&&fragment.itemValue.TryGetMetadata(UnitsKey,out quantity)&&quantity>0&&
                fragment.itemValue.TryGetMetadata(MaterialKey,out string name)&&!string.IsNullOrEmpty(name)&&
                fragment.itemValue.TryGetMetadata(SlotKey,out int slot)&&slot>=0;
        return !fragment.itemValue.HasMetadata(MaterialKey)&&!fragment.itemValue.HasMetadata(SlotKey)&&quantity>0&&quantity<=ushort.MaxValue;
    }
    private static bool CancelActor(EntityPlayer player,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,object lease,Guid job,int selected,List<ItemStack> refunds)
    {
        bool remote=!(player is EntityPlayerLocal);RebirthStablePlayerIdentity remoteOwner=null;bool[] remoteLocks=null;
        ItemStack[] oldBag,oldBelt;string owner;
        if(remote)
        {
            if(!RebirthCamperRemoteStationPlayerSource.IsCurrent(player,station)||
                !RebirthCamperStationNativeActorPayment.RemoteIdentity(player,out remoteOwner,out _)||
                !RebirthCamperStationPlayerSavedSource.TryCopySavedSource(remoteOwner,out oldBag,out oldBelt,out remoteLocks))return false;
            owner=remoteOwner.CanonicalId;
        }
        else
        {
            if(!RebirthStationPlayerIngredients.UsesPlayerPayment(player,station))return false;
            owner=GameManager.Instance.getPersistentPlayerID(null)?.CombinedString;
            oldBag=player.bag.ItemGrid.items.Select(s=>s.Clone()).ToArray();oldBelt=player.inventory.ItemGrid.items.Select(s=>s.Clone()).ToArray();
        }
        var bag=oldBag.Select(s=>s.Clone()).ToArray();var belt=oldBelt.Select(s=>s.Clone()).ToArray();
        foreach(var refund in refunds)
        {
            var value=refund.itemValue;
            if(!value.TryGetMetadata(ActorSourceKey,out string kind)||(kind!="bag"&&kind!="belt")||
                !value.TryGetMetadata(ActorSlotKey,out int slot)||!value.TryGetMetadata(ActorOwnerKey,out string origin)||origin!=owner)return false;
            var cells=kind=="bag"?bag:belt;if(slot<0||slot>=cells.Length||kind=="belt"&&slot>=player.inventory.Length)return false;
            value.Metadata.Remove(ActorSourceKey);value.Metadata.Remove(ActorSlotKey);value.Metadata.Remove(ActorOwnerKey);
            var target=cells[slot];var comparable=target.Clone();comparable.count=refund.count;
            if(!target.IsEmpty()&&!RebirthStationGridIngredients.IsSameStackSnapshot(comparable,refund))return false;
            int total=checked(target.count+refund.count);if(total<1||total>(value.ItemClass?.MaxCount??0))return false;
            cells[slot]=new ItemStack(value.Clone(),total);
        }
        var queue=(RecipeQueueItem[])station.Queue.Clone();queue[selected]=new RecipeQueueItem{StartingEntityId=-1};
        if(!Resolve(player,address,creation,out var current,out var currentLease)||!ReferenceEquals(current,station)||!ReferenceEquals(currentLease,lease))return false;
        // Stable refund operation; retries resume its original exact receipt.
        return RebirthCamperStationNativeActorPayment.Begin(player,station,address,creation,CancelOperation(job),oldBag,bag,oldBelt,belt,queue,remoteOwner,remoteLocks);
    }
    private static Guid CancelOperation(Guid job)
    {
        byte[] bytes;using(var hash=System.Security.Cryptography.SHA256.Create())bytes=hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(job.ToString("N")+":native-actor-cancel"));
        var identity=new byte[16];Array.Copy(bytes,identity,16);return new Guid(identity);
    }
}
