using System;
using UnityEngine;
using HarmonyLib;

#nullable disable

[HarmonyPatch(typeof(EntityAlive), "damageEntityLocal", new System.Type[] { typeof(DamageSource), typeof(int), typeof(bool), typeof(float) })]
internal static class RebirthSurvivorCombatSkillPatch
{
    internal struct DamageState
    {
        public bool WasDead;
        public DamageSource Source;
        public int RawStrength;
        public int HealthBefore;
        public bool PreHitStealthQualified;
        public RebirthTheorySoloCombatService.Witness SoloOriginal;
    }

    private static void Prefix(EntityAlive __instance, DamageSource _damageSource, int _strength, out DamageState __state)
    {
        __state = new DamageState { WasDead = __instance != null && __instance.IsDead(), Source = _damageSource, RawStrength = _strength, HealthBefore = __instance != null ? __instance.Health : 0, PreHitStealthQualified = RebirthStealthTrainingService.IsPreHitStealthQualified(__instance, _damageSource), SoloOriginal=CaptureSoloOriginal(__instance,_damageSource) };
    }

    private static RebirthTheorySoloCombatService.Witness CaptureSoloOriginal(EntityAlive target,DamageSource source)
    {try{return RebirthTheorySoloCombatService.Before(target,source);}catch{return null;}}

    private static System.Exception Finalizer(System.Exception __exception,DamageState __state)
    {try{RebirthTheorySoloCombatService.Unknown(__state.SoloOriginal,__exception);}catch{}return __exception;}

    private static void Postfix(EntityAlive __instance,DamageResponse __result,DamageState __state,bool __runOriginal)
    {
        try{RebirthTheorySoloCombatService.After(__state.SoloOriginal,__instance,__result,__runOriginal);}catch(System.Exception observer){try{RebirthTheorySoloCombatService.Unknown(__state.SoloOriginal,observer);}catch{}}
        // A target that was already dead before the hit is carcass-processing evidence, not combat.
        // Phase 8 observes the actual resources recovered by HarvestOnAttack instead of corpse-hit damage.
        if (__state.WasDead) return;
        float actualHealthLoss = __instance != null ? System.Math.Max(0, __state.HealthBefore - __instance.Health) : 0f;
        RebirthStatisticsService.RecordDamage(__instance,__result,__state.Source,__state.RawStrength,actualHealthLoss,-1f);
        RebirthSkillEventRouter.OnCombatDamageCompleted(__instance,__result,actualHealthLoss,__state.PreHitStealthQualified,__state.HealthBefore);
    }
}

[HarmonyPatch(typeof(Block), nameof(Block.OnBlockDestroyedBy), new System.Type[] { typeof(WorldBase), typeof(BlockValueRef), typeof(BlockValue), typeof(int), typeof(bool) })]
internal static class RebirthSurvivorHarvestSkillPatch
{
    private static void Postfix(Block __instance, WorldBase _world, BlockValueRef _bvRef, BlockValue _blockValue, int _entityId, bool _bUseHarvestTool)
    {
        // Phase 8 deliberately awards no flat block-completion Skill XP here. Mining/Logging use
        // actual eligible work per HarvestOnAttack and Salvage uses actual recovered output.

        // PC017: the same source-audited destruction callback is the narrow cleanup point for
        // server-owned placed/electrical provenance. Upgrade swaps do not come through this
        // destruction callback, so workmanship is not reassigned to the upgrader.
        if (_world != null && !_world.IsRemote() && RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            RebirthPlacedWorkmanshipService.Remove(_bvRef.BlockPosition);
            RebirthInfrastructureWorkService.RemoveRecord(_bvRef.BlockPosition);
        }
    }
}

[HarmonyPatch(typeof(Recipe), "IsUnlocked")]
internal static class RebirthSurvivorRecipePresentationKnowledgePatch
{
    private static void Postfix(Recipe __instance,EntityPlayer _ep,ref bool __result)
    { RebirthRecipeCapabilityIntegration.ApplyPresentationGate(__instance,_ep,ref __result); }
}

[HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.HandleRecipeQueue))]
internal static class RebirthSurvivorWorkstationKnowledgeAuthorityPatch
{
    private static bool Prefix(TileEntityWorkstation __instance)
    { return RebirthRecipeCapabilityIntegration.AuthorizeActiveQueue(__instance); }
}

// Native open-window Update has a separate inventory-full output retry before outputStack.
// Gate both boundaries without clearing the paid recipe or changing its timing/counters.
[HarmonyPatch(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.Update))]
internal static class RebirthSurvivorOpenWorkstationQueueUpdatePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_RecipeStack __instance)
        => RebirthRecipeCapabilityIntegration.AuthorizeOpenQueue(__instance,__instance?.isInventoryFull == true);
}

[HarmonyPatch(typeof(XUiC_RecipeStack), "outputStack")]
internal static class RebirthSurvivorOpenWorkstationQueueOutputPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_RecipeStack __instance,ref bool __result)
    {
        if (RebirthRecipeCapabilityIntegration.AuthorizeOpenQueue(__instance,true)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.AddCraftComplete))]
internal static class RebirthSurvivorWorkstationSkillCompletionPatch
{
    private static void Postfix(TileEntityWorkstation __instance,int crafterEntityID,string recipeName,int craftedCount,ItemValue itemCrafted)
    {
        // giveExp may invoke a native receipt before a personal output capacity decision.
        // Personal training is credited only by the confirmed-output bridge, never here.
        if(RebirthPersonalCraftCompletionService.PersonalOutputInProgress||RebirthWorkstationCraftCompletion.OutputDepth>0)return;
        if(itemCrafted!=null && itemCrafted.HasMetadata("rebirth.cooking.batch"))
        {
            if(RebirthCookingBatch.UiCompletionInProgress)return;
            var player=GameManager.Instance?.World?.GetEntity(crafterEntityID) as EntityPlayer;
            RebirthCookingBatch.AwardCompleted(player,recipeName,itemCrafted,craftedCount);
        }
        else if(RebirthWorldCharacterRepository.IsServerAuthority&&RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            // The active queue still exists at AddCraftComplete; preserve its exact recipe variant.
            var queue=__instance.Queue;
            Recipe active=queue!=null&&queue.Length>0?queue[queue.Length-1]?.Recipe:null;
            if(active!=null&&active.GetName()==recipeName)
                RebirthSkillEventRouter.OnCraftOutputCompleted(GameManager.Instance?.World?.GetEntity(crafterEntityID) as EntityPlayer,active,craftedCount);
            else RebirthRecipeCapabilityIntegration.OnWorkstationCraftComplete(crafterEntityID,recipeName,craftedCount);
        }
        RebirthMedicalLootCraftSignatureService.OnSuccessfulWorkstationCraft(__instance,crafterEntityID,recipeName,craftedCount);
    }
}


[HarmonyPatch(typeof(ItemActionUseOther), nameof(ItemActionUseOther.ExecuteAction))]
internal static class RebirthSurvivorTreatOtherSkillPatch
{
    internal struct TreatmentState
    {
        public EntityPlayer Healer;
        public EntityAlive Patient;
        public int HealthBefore;
        public int BuffCountBefore;
        public float MedicalReserveBefore;
        public string TreatmentKey;
        public RebirthMedicalPractice.Evidence Evidence;
        public bool PracticeScope;
    }

    private static bool Prefix(ItemActionUseOther __instance, ItemActionData _actionData, bool _bReleased, out TreatmentState __state)
    {
        __state = new TreatmentState();
        if (!_bReleased || __instance == null || _actionData == null || _actionData.invData == null) return true;
        ItemActionUseOther.FeedInventoryData feed = _actionData as ItemActionUseOther.FeedInventoryData;
        if (feed == null || feed.TargetEntity == null || _actionData.invData.item == null || !_actionData.invData.item.HasAnyTags(__instance.medicalItemTag)) return true;
        string itemName = _actionData.invData.itemValue?.ItemClass?.GetItemName();
        bool external = RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(itemName);
        EntityAlive actor = _actionData.invData.holdingEntity;
        // Release isolation: only authored medicine used on a different player is deferred.
        // Native self-use and nonmedicine interactions keep their existing paths.
        if (RebirthOtherPlayerMedicineReleasePolicy.Refuses(actor, feed.TargetEntity, itemName))
        {
            feed.bFeedingStarted = false;
            if (actor?.MinEventContext != null && ReferenceEquals(actor.MinEventContext.Other, feed.TargetEntity))
                actor.MinEventContext.Other = null;
            feed.TargetEntity = null;
            return false; // Skip original native effects/consumption; no evidence scope was opened.
        }
        // Native CanExecute caches the target. Recheck at consumption so movement, death or
        // world changes cannot apply a previously selected external treatment at a distance.
        if (external && !RebirthExternalTreatmentAdmission.Allows(__instance, _actionData, feed.TargetEntity))
        {
            feed.bFeedingStarted = false;
            if (actor?.MinEventContext != null && ReferenceEquals(actor.MinEventContext.Other, feed.TargetEntity))
                actor.MinEventContext.Other = null;
            feed.TargetEntity = null;
            return false;
        }
        if (external && RebirthExternalTreatmentAdmission.HasNativeEarlyRefusal(__instance, _actionData, feed.TargetEntity))
            return true;
        __state.Healer = _actionData.invData.holdingEntity as EntityPlayer;
        __state.Patient = feed.TargetEntity;
        __state.HealthBefore = feed.TargetEntity.Health;
        __state.BuffCountBefore = feed.TargetEntity.Buffs != null && feed.TargetEntity.Buffs.ActiveBuffs != null ? feed.TargetEntity.Buffs.ActiveBuffs.Count : 0;
        __state.MedicalReserveBefore = feed.TargetEntity.Buffs != null ? feed.TargetEntity.Buffs.GetCustomVar("medicalRegHealthAmount") : 0f;
        __state.TreatmentKey = _actionData.invData.itemValue != null && _actionData.invData.itemValue.ItemClass != null ? _actionData.invData.itemValue.ItemClass.GetItemName() : string.Empty;
        __state.PracticeScope=true;
        __state.Evidence=RebirthMedicalPractice.Begin(__state.Healer,__state.Patient,__state.TreatmentKey);
        return true;
    }

    private static void Postfix(TreatmentState __state, bool __runOriginal)
    {
        if (!__runOriginal || __state.Healer == null || __state.Patient == null) return;
        if (!ReferenceEquals(__state.Healer, __state.Patient) &&
            RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(__state.TreatmentKey) &&
            (__state.Evidence?.ExternalScope == null ||
             !__state.Evidence.ExternalScope.IsCurrent(__state.Healer, __state.Patient))) return;
        RebirthMedicalPractice.Complete(__state.Evidence);
        RebirthMedicalLootCraftSignatureService.OnSuccessfulMedicalTreatment(__state.Healer,__state.Patient);
    }
    private static void Finalizer(TreatmentState __state){if(__state.PracticeScope)RebirthMedicalPractice.Exit();}
}

[HarmonyPatch(typeof(XUiC_RecipeStack), "outputStack")]
internal static class RebirthSurvivorEquipmentRepairSkillPatch
{
    internal struct RepairState
    {
        public int PlayerEntityId;
        public int ItemType;
        public ushort Seed;
        public int AmountToRepair;
        public int MaxUseTimes;
        public string ItemName;
    }

    private static void Prefix(XUiC_RecipeStack __instance, out RepairState __state)
    {
        __state = new RepairState();
        if (__instance == null || __instance.AmountToRepair <= 0 || __instance.OriginalItem == null || __instance.OriginalItem.ItemClass == null) return;
        EntityPlayer player=__instance.xui!=null&&__instance.xui.playerUI!=null?__instance.xui.playerUI.entityPlayer:null;
        __state.PlayerEntityId = __instance.StartingEntityId > 0 ? __instance.StartingEntityId : (player!=null?player.entityId:-1);
        __state.ItemType = __instance.OriginalItem.type;
        __state.Seed = __instance.OriginalItem.Seed;
        __state.AmountToRepair = __instance.AmountToRepair;
        __state.MaxUseTimes = Math.Max(0,__instance.OriginalItem.MaxUseTimes);
        __state.ItemName = __instance.OriginalItem.ItemClass.GetItemName();
    }

    private static void Postfix(bool __result, RepairState __state)
    {
        if (!__result || __state.AmountToRepair <= 0 || __state.ItemType <= 0 || GameManager.Instance == null || GameManager.Instance.World == null) return;
        EntityPlayer player = GameManager.Instance.World.GetEntity(__state.PlayerEntityId) as EntityPlayer;
        if(player==null)player=GameManager.Instance.World.GetPrimaryPlayer();
        if (player != null) RebirthEquipmentRepairTrainingService.ReportCompleted(player,__state.ItemType,__state.Seed,__state.ItemName,__state.AmountToRepair,__state.MaxUseTimes);
    }
}

[HarmonyPatch(typeof(XUiM_Recipes), nameof(XUiM_Recipes.GetRecipeCraftTime))]
internal static class RebirthSurvivorServiceCraftTimePatch
{
    private static void Postfix(XUi xui, Recipe _recipe, ref float __result)
    {
        EntityPlayer player=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        __result=RebirthServiceCraftSkillService.ApplyCraftTime(player,_recipe,__result);
    }
}

[HarmonyPatch(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.SetRepairRecipe))]
internal static class RebirthSurvivorServiceRepairQueuePatch
{
    private static void Prefix(XUiC_RecipeStack __instance, ref float _repairTimeLeft, ItemValue _itemToRepair, ref int _amountToRepair)
    {
        EntityPlayer player=__instance!=null&&__instance.xui!=null&&__instance.xui.playerUI!=null?__instance.xui.playerUI.entityPlayer:null;
        RebirthServiceCraftSkillService.AdjustRepairQueue(player,_itemToRepair,ref _repairTimeLeft,ref _amountToRepair);
    }
}

[HarmonyPatch(typeof(GameUtils), nameof(GameUtils.HarvestOnAttack))]
internal static class RebirthSurvivorResourceHarvestEffectContextPatch
{
    internal struct HarvestScopeState
    {
        public EntityPlayer Player;
        public ItemActionData ActionData;
        public float PriorValue;
        public bool Captured;
    }

    private static void Prefix(ItemActionData _actionData,out HarvestScopeState __state)
    {
        __state=new HarvestScopeState{ActionData=_actionData};
        EntityPlayer player=_actionData!=null&&_actionData.invData!=null?_actionData.invData.holdingEntity as EntityPlayer:null;
        if(player!=null&&player.Buffs!=null)
        {
            __state.Player=player;
            __state.PriorValue=player.Buffs.GetCustomVar(RebirthResourceFieldSkillService.ResourceHarvestCVar);
            __state.Captured=true;
        }
        if (RebirthSkillEvalDiagnostics.On && _actionData != null && _actionData.attackDetails != null) { try { var ad = _actionData.attackDetails; ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance; Log.Out("[REBIRTH SkillEval] HarvestOnAttack block=" + (ad.bBlockHit && ad.blockBeingDamaged.Block != null ? ad.blockBeingDamaged.Block.GetBlockName() : "(entity)") + " damageGiven=" + ad.damageGiven + " damageMax=" + ad.damageMax + " total=" + ad.damageTotalOfTarget + " class=" + RebirthProgressionRuntimeConfig.ClassifyHarvest(_actionData.invData.itemValue, ad.bBlockHit && ad.blockBeingDamaged.Block != null ? ad.blockBeingDamaged.Block.GetBlockName() : "") + " survivorMode=" + RebirthSurvivorMode.IsEnabledForCurrentWorld() + " server=" + (cm != null && cm.IsServer) + " remote=" + (_actionData.invData.holdingEntity != null && _actionData.invData.holdingEntity.world != null && _actionData.invData.holdingEntity.world.IsRemote())); } catch { } }
        RebirthResourceFieldSkillService.BeginResourceHarvestAttack(_actionData);
        RebirthPhase8WorldOutputTrainingService.BeginHarvest(_actionData);
    }

    private static Exception Finalizer(Exception __exception,HarvestScopeState __state)
    {
        if(__exception==null)RebirthPhase8WorldOutputTrainingService.CompleteHarvest(__state.ActionData,true);
        else RebirthPhase8WorldOutputTrainingService.AbortHarvest(__state.ActionData);
        if(__state.Captured&&__state.Player!=null)
            RebirthResourceFieldSkillService.RestoreResourceHarvestValue(__state.Player,__state.PriorValue);
        return __exception;
    }
}



// Successor candidate: real native item-effect scope; all self treatment-quality Medicine then optional Trauma.
// New native treatment-buff admission is the success witness; released input is never success.
internal static class RebirthSelfMedicalNewTreatment
{
    internal struct Before
    {
        internal BuffValue Exact;
        internal bool GroupTreated;
    }

    static string Canonical(string n)
    {
        if (string.Equals(n, "buffInjuryAbrasionTreated", StringComparison.OrdinalIgnoreCase))
            return "buffInjuryAbrasionTreated";
        if (string.Equals(n, "buffLegSplinted", StringComparison.OrdinalIgnoreCase))
            return "buffLegSplinted";
        if (string.Equals(n, "buffLegCast", StringComparison.OrdinalIgnoreCase))
            return "buffLegCast";
        if (string.Equals(n, "buffArmSplinted", StringComparison.OrdinalIgnoreCase))
            return "buffArmSplinted";
        return string.Equals(n, "buffArmCast", StringComparison.OrdinalIgnoreCase)
        ? "buffArmCast"
        : null;
    }

    static string Marker(string n) => n == "buffInjuryAbrasionTreated"
        ? RebirthMedicalLootCraftSignatureService.ParamedicAbrasionMarker
        : n.StartsWith("buffLeg", StringComparison.Ordinal)
        ? RebirthMedicalLootCraftSignatureService.ParamedicLegMarker
        : RebirthMedicalLootCraftSignatureService.ParamedicArmMarker;
    static string Base(string n) => n == "buffInjuryAbrasionTreated"
        ? "healAbrasionMult"
        : n.StartsWith("buffLeg", StringComparison.Ordinal)
        ? "$legTreatedCritHealingBase"
        : "$armTreatedCritHealingBase";
    static string Pending(string n) => n == "buffInjuryAbrasionTreated"
        ? "$rbParamedicNewSelf_buffInjuryAbrasionTreated"
        : n == "buffLegSplinted"
        ? "$rbParamedicNewSelf_buffLegSplinted"
        : n == "buffLegCast"
        ? "$rbParamedicNewSelf_buffLegCast"
        : n == "buffArmSplinted"
        ? "$rbParamedicNewSelf_buffArmSplinted"
        : "$rbParamedicNewSelf_buffArmCast";
    static string Quality(string n) => n == "buffInjuryAbrasionTreated"
        ? "$rbSelfQualityAbrasion"
        : n == "buffLegSplinted"
        ? "$rbSelfQualityLegSplint"
        : n == "buffLegCast"
        ? "$rbSelfQualityLegCast"
        : n == "buffArmSplinted"
        ? "$rbSelfQualityArmSplint"
        : "$rbSelfQualityArmCast";
    static string Trauma(string n) => n == "buffInjuryAbrasionTreated"
        ? "$rbSelfTraumaAbrasion"
        : n == "buffLegSplinted"
        ? "$rbSelfTraumaLegSplint"
        : n == "buffLegCast"
        ? "$rbSelfTraumaLegCast"
        : n == "buffArmSplinted"
        ? "$rbSelfTraumaArmSplint"
        : "$rbSelfTraumaArmCast";
    static string Sibling(string n) => n == "buffLegSplinted"
        ? "buffLegCast"
        : n == "buffLegCast"
        ? "buffLegSplinted"
        : n == "buffArmSplinted"
        ? "buffArmCast"
        : n == "buffArmCast"
        ? "buffArmSplinted"
        : null;
    static void Removed(EntityBuffs b, string n)
    {
        if (b.GetCustomVar(Pending(n)) != 0f)
            RebirthSelfMedicalSimulationOwner.Set(b, Pending(n), 0f, false);
        if (b.GetCustomVar(Quality(n)) != 0f)
            RebirthSelfMedicalSimulationOwner.Set(b, Quality(n), 0f, false);
        if (b.GetCustomVar(Trauma(n)) != 0f)
            RebirthSelfMedicalSimulationOwner.Set(b, Trauma(n), 0f, false);
        // Native RemoveCVar can omit an absent key from unreliable deltas. Publish explicit
        // retirement SETs for the current owner before any verified sibling restoration.
        RebirthSelfMedicalSimulationOwner.Set(b, Base(n), b.GetCustomVar(Base(n)));
        RebirthSelfMedicalSimulationOwner.Set(b, Marker(n), b.GetCustomVar(Marker(n)));
        string sibling = Sibling(n);
        if (sibling == null)
            return;
        var value = b.GetBuff(sibling);
        float quality = b.GetCustomVar(Quality(sibling));
        if (value == null
            || value.Remove
            || value.Invalid
            || !value.Started
            || !(quality > 0f)
            || float.IsInfinity(quality)
            || !Current(b, out var p)
            || value.InstigatorId != p.entityId)
            return;
        RebirthSelfMedicalSimulationOwner.Set(b, Base(sibling), quality);
        RebirthSelfMedicalSimulationOwner.Set(b, Marker(sibling), b.GetCustomVar(Trauma(sibling)) > 0f
        ? 1f
        : 0f);
    }

    static bool Group(EntityBuffs b, string n) => n == "buffInjuryAbrasionTreated"
        ? b.HasBuff(n)
        : n.StartsWith("buffLeg", StringComparison.Ordinal)
        ? b.HasBuff("buffLegSplinted")
            || b.HasBuff("buffLegCast")
        : b.HasBuff("buffArmSplinted")
            || b.HasBuff("buffArmCast");
    static bool Current(EntityBuffs buffs, out EntityPlayer player)
    {
        player = buffs?.parent as EntityPlayer;
        return RebirthSelfMedicalSimulationOwner.Current(player);
    }
    internal static Before Capture(EntityBuffs b, string name)
    {
        string n = Canonical(name);
        return b == null
            || n == null
        ? default
        : new Before{Exact = b.GetBuff(n), GroupTreated = Group(b, n)};
    }

    internal static void Added(EntityBuffs b, string name, int instigator, EntityBuffs.BuffStatus status, Before before)
    {
        string n = Canonical(name);
        if (n == null
            || b == null
            || status != EntityBuffs.BuffStatus.Added || !Current(b, out _))
            return;
        BuffValue added = b.GetBuff(n);
        if (added == null
            || ReferenceEquals(added, before.Exact))
            return; // Added also means native refresh: not new.
        if (b.GetCustomVar(Pending(n)) != 0f)
            RebirthSelfMedicalSimulationOwner.Set(b, Pending(n), 0f, false); // A replacement cannot inherit a prior pending factor.
        if (b.GetCustomVar(Quality(n)) != 0f)
            RebirthSelfMedicalSimulationOwner.Set(b, Quality(n), 0f, false);
        if (b.GetCustomVar(Trauma(n)) != 0f)
            RebirthSelfMedicalSimulationOwner.Set(b, Trauma(n), 0f, false);
        if (!Current(b, out var p)
            || instigator != p.entityId
            || added.InstigatorId != instigator
            || added.Started
            || added.Remove
            || added.Invalid)
            return;
        bool trauma = RebirthBackgroundBonusService.HasBonus(p, RebirthMedicalLootCraftSignatureService.TraumaSpecialistBonusId);
        float factor = trauma
        ? 2f
        : 1f;
        RebirthBackgroundBonusDefinition definition;
        RebirthBackgroundBonusTuningValue tuning;
        float parsed;
        if (trauma
            && RebirthBackgroundBonusService.TryGetSignatureBonus(p, out definition)
            && definition != null
            && definition.TryGetTuning("treated_healing_multiplier", out tuning)
            && tuning != null
            && float.TryParse(tuning.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)
            && parsed >= 1f
            && !float.IsInfinity(parsed)
            && !float.IsNaN(parsed))
            factor = parsed;
        {
            float skill;
            if (!RebirthServiceCraftSkillService.TryGetSkillValue(p, "skill.medicine", out skill)
            || float.IsNaN(skill)
            || float.IsInfinity(skill))
                return;
            factor *= Math.Max(0.10f, 1f + RebirthServiceCraftSkillService.SignedEndpoint(skill, RebirthProgressionRuntimeConfig.MedicineFractureHealingNegative, RebirthProgressionRuntimeConfig.MedicineFractureHealingPositive));
        }

        // Standard saved CVar plus native BuffValue's saved instigator/start flags; no timer/entity-ID lookup.
        if (!(factor > 0f)
            || float.IsNaN(factor)
            || float.IsInfinity(factor))
            return;
        float frozen = factor;
        RebirthSelfMedicalItemEffectScope.Stage(p, () =>
        {
            if (Current(b, out var same)
            && ReferenceEquals(same, p)
            && ReferenceEquals(b.GetBuff(n), added)
            && !added.Started
            && !added.Remove
            && !added.Invalid)
            {
                RebirthSelfMedicalSimulationOwner.Set(b, Pending(n), frozen, false);
                if (trauma
            || b.GetCustomVar(Trauma(n)) != 0f)
                    RebirthSelfMedicalSimulationOwner.Set(b, Trauma(n), trauma
        ? 1f
        : 0f, false);
            }
        });
    }

    internal static void Event(MinEventTypes type, BuffClass definition, MinEventParams context)
    {
        if (type != MinEventTypes.onSelfBuffStart
            && type != MinEventTypes.onSelfBuffRemove)
            return;
        string n = Canonical(definition?.Name);
        var b = context?.Self?.Buffs;
        if (n == null
            || b == null)
            return;
        if (!Current(b, out _))
            return;
        if (type == MinEventTypes.onSelfBuffRemove)
        {
            Removed(b, n);
            return;
        }

        var buff = context.Buff;
        if (type != MinEventTypes.onSelfBuffStart
            || !Current(b, out var p)
            || buff == null
            || !ReferenceEquals(b.GetBuff(n), buff)
            || buff.Started
            || buff.Remove
            || buff.Invalid
            || buff.InstigatorId != p.entityId
            || !ReferenceEquals(context.Instigator, p))
            return;
        float factor = b.GetCustomVar(Pending(n));
        if (factor == 0f)
            return;
        RebirthSelfMedicalSimulationOwner.Set(b, Pending(n), 0f, false); // burn once, even if base refuses
        float value = b.GetCustomVar(Base(n));
        if (!(factor > 0f)
            || float.IsInfinity(factor)
            || !(value > 0f)
            || float.IsInfinity(value)
            || float.IsNaN(value)
            || float.IsInfinity(value * factor))
            return;
        RebirthSelfMedicalSimulationOwner.Set(b, Base(n), value * factor);
        RebirthSelfMedicalSimulationOwner.Set(b, Quality(n), value * factor, false);
        RebirthSelfMedicalSimulationOwner.Set(b, Marker(n), b.GetCustomVar(Trauma(n)) > 0f
        ? 1f
        : 0f);
    }
}

[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.AddBuff), new Type[]{typeof(string), typeof(Vector3i), typeof(int), typeof(bool), typeof(bool), typeof(float)})]
internal static class RebirthSelfMedicalNewBuffAddedPatch
{
    static void Prefix(EntityBuffs __instance, string _name, out RebirthSelfMedicalNewTreatment.Before __state) => __state = RebirthSelfMedicalNewTreatment.Capture(__instance, _name);
    static void Postfix(EntityBuffs __instance, string _name, int _instigatorId, EntityBuffs.BuffStatus __result, RebirthSelfMedicalNewTreatment.Before __state) => RebirthSelfMedicalNewTreatment.Added(__instance, _name, _instigatorId, __result, __state);
}

[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.FireEvent), new Type[]{typeof(MinEventTypes), typeof(BuffClass), typeof(MinEventParams)})]
internal static class RebirthSelfMedicalNewBuffStartPatch
{
    static void Postfix(MinEventTypes _eventType, BuffClass _buffClass, MinEventParams _params) => RebirthSelfMedicalNewTreatment.Event(_eventType, _buffClass, _params);
}



[HarmonyPatch(typeof(LootManager), nameof(LootManager.LootContainerOpened))]
internal static class RebirthBackgroundSpecializedLootPatch
{
    internal struct State { public bool WasTouched; public string LootList; public int Opener; }
    private static void Prefix(TEFeatureStorage _tileEntity,int _entityIdThatOpenedIt,out State __state)
    {
        __state=new State{WasTouched=_tileEntity!=null&&_tileEntity.ItemGrid.Touched,LootList=_tileEntity!=null?_tileEntity.lootListName:string.Empty,Opener=_entityIdThatOpenedIt};
    }
    private static void Postfix(LootManager __instance,TEFeatureStorage _tileEntity,State __state)
    {
        if(GameManager.Instance==null||GameManager.Instance.World==null)return;
        EntityPlayer opener=GameManager.Instance.World.GetEntity(__state.Opener) as EntityPlayer;
        RebirthMedicalLootCraftSignatureService.ApplySpecializedLoot(__instance,_tileEntity,opener,__state.WasTouched,__state.LootList);
    }
}



[HarmonyPatch(typeof(BlockModelTree), nameof(BlockModelTree.OnBlockDestroyedBy), new System.Type[] { typeof(WorldBase), typeof(BlockValueRef), typeof(BlockValue), typeof(int), typeof(bool) })]
internal static class RebirthTreeDestroyedSettlementPatch
{
    private static void Postfix(WorldBase _world,BlockValueRef _bvRef,BlockValue _blockValue,int _entityId,bool _bUseHarvestTool)
    {
        Vector3i position;
        if (_bvRef.TryGetBlockPos(out position))
            RebirthResourceSignatureService.OnTreeDestroyedServer(_world,position,_blockValue,_entityId,_bUseHarvestTool);
    }
}

[HarmonyPatch(typeof(BlockModelTree), nameof(BlockModelTree.OnBlockDamaged), new System.Type[] { typeof(WorldBase), typeof(BlockValueRef), typeof(BlockValue), typeof(int), typeof(int), typeof(ItemActionAttack.AttackHitInfo), typeof(bool), typeof(bool), typeof(int) })]
internal static class RebirthTreeDamageEvidencePatch
{
    private static void Prefix(WorldBase _world,BlockValueRef _bvRef,BlockValue _blockValue,int _damagePoints,int _entityIdThatDamaged,bool _bUseHarvestTool)
    {
        Vector3i blockPos;
        if(!_bvRef.TryGetBlockPos(out blockPos))return;
        RebirthResourceSignatureService.RecordTreeDamageEvidence(_world,blockPos,_blockValue,_entityIdThatDamaged,_damagePoints,_bUseHarvestTool);
    }
}


