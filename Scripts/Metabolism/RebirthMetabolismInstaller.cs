using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

#nullable disable

public static class RebirthMetabolismInstaller
{
    private static bool installed;
    private static float nextWorldTick;
    private static readonly Harmony Harmony = new Harmony("rebirth.player-metabolism.3.0");

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if(!RebirthMetabolismService.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        var world=GameManager.Instance?.World;
        if(world?.Players?.list==null)return;
        float now=Time.realtimeSinceStartup;
        if(now<nextWorldTick)return;
        nextWorldTick=now+Mathf.Max(.1f,RebirthMetabolismConfig.UpdateRealSeconds);
        // Remote players do not reliably enter local native stats OT callbacks.
        // Tick's per-character NextRealTickTime guards overlapping native/server paths.
        var players=world.Players.list;
        for(int i=0;i<players.Count;i++)
        {
            EntityPlayer player=players[i];
            if(player==null)continue;
            try { RebirthMetabolismService.Tick(player); }
            catch(Exception error)
            {
                if(RebirthLogSettings.HarmonyPatchLoggingEnabled)
                    Log.Warning("[REBIRTH Metabolism] player tick deferred entity="+player.entityId+" error="+error.GetType().Name);
            }
        }
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    { RebirthMetabolismClientState.Clear(); nextWorldTick=0f; }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    { RebirthMetabolismClientState.Clear(); nextWorldTick=0f; }
    public static string Install()
    {
        if (installed)
            return "[REBIRTH Metabolism] installer already active.";

        installed = true;
        RebirthMetabolismConfig.Load();
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismNativeReservePacketPatch));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthLiquidStackCapacityPatch));
        RebirthConsumableResolver.ClearCache();
        RebirthMetabolismStateRepository.Reset(RebirthMetabolismService.IsServerAuthority);

        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismAnimatedCompletionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCreativeConsumableUsePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismFoodUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismWaterUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismHealthRegenPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismStaminaRegenPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismDeathPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismDisconnectPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismSpawnPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismItemTooltipPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthMetabolismItemInfoPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDrinkSlotPresentationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSelectedInfoSourcePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSelectedInfoPresentationPatch));

        return "[REBIRTH Metabolism] installed fixed-step metabolism, digestion, partial liquids, hydration slot and UI bindings.";
    }
}

[HarmonyPatch(typeof(PlayerEntityStats), nameof(PlayerEntityStats.UpdatePlayerFoodOT))]
public static class RebirthMetabolismFoodUpdatePatch
{
    [HarmonyPrefix]
    public static bool Prefix(PlayerEntityStats __instance, float dt)
    {
        EntityPlayer player = __instance != null ? __instance.m_entity as EntityPlayer : null;
        if (player == null)
            return true;
        if (RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            return false;

        // Discard any legacy FoodChangeOT and any FoodLossPer* regeneration charges
        // accumulated by Health/Stamina ticks. The metabolism service owns food use.
        float nutritionBeforeNativeTick = __instance.Food.Value;
        __instance.Food.RegenerationAmount = 0f;
        __instance.Food.MaxModifier = -EffectManager.GetValue(PassiveEffects.FoodMaxBlockage, _entity: __instance.m_entity);
        __instance.Food.Tick(dt);

        // Item/buff authoring in vanilla can still queue positive FoodChangeOT after an item-use
        // event (for example buffProcessConsumables). REBIRTH Nutrition must only increase when
        // nutrients have physically reached the intestine and are absorbed by our metabolism tick.
        // Preserve native decreases/damage paths, but reject any native positive Nutrition gain.
        if (__instance.Food.Value > nutritionBeforeNativeTick)
        {
            if (RebirthMetabolismService.DebugEnabled)
                Log.Out("[REBIRTH Metabolism] blocked pre-digestion native Nutrition gain="
                    + (__instance.Food.Value - nutritionBeforeNativeTick).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            __instance.Food.Value = nutritionBeforeNativeTick;
        }

        __instance.Food.RegenerationAmount = 0f;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerEntityStats), nameof(PlayerEntityStats.UpdatePlayerWaterOT))]
public static class RebirthMetabolismWaterUpdatePatch
{
    [HarmonyPrefix]
    public static bool Prefix(PlayerEntityStats __instance, float dt)
    {
        EntityPlayer player = __instance != null ? __instance.m_entity as EntityPlayer : null;
        if (player == null)
            return true;
        if (RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            return false;

        // Same ownership rule as Food: clear legacy WaterChangeOT and WaterLossPer*
        // charges before Stat.Tick can apply them.
        float hydrationBeforeNativeTick = __instance.Water.Value;
        __instance.Water.RegenerationAmount = 0f;
        __instance.Water.MaxModifier = -EffectManager.GetValue(PassiveEffects.WaterMaxBlockage, _entity: __instance.m_entity);
        __instance.Water.Tick(dt);

        // Same ownership boundary as Nutrition: authored consumable effects may not bypass
        // stomach/intestine processing and directly increase the body's Hydration reserve.
        if (__instance.Water.Value > hydrationBeforeNativeTick)
        {
            if (RebirthMetabolismService.DebugEnabled)
                Log.Out("[REBIRTH Metabolism] blocked pre-absorption native Hydration gain="
                    + (__instance.Water.Value - hydrationBeforeNativeTick).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            __instance.Water.Value = hydrationBeforeNativeTick;
        }

        __instance.Water.RegenerationAmount = 0f;

        if (RebirthMetabolismService.IsServerAuthority)
        {
            RebirthMetabolismService.EnsurePlayerReady(player);
            RebirthMetabolismService.Tick(player);
        }
        return false;
    }
}

[HarmonyPatch(typeof(PlayerEntityStats), nameof(PlayerEntityStats.UpdatePlayerHealthOT))]
public static class RebirthMetabolismHealthRegenPatch
{
    [HarmonyPrefix]
    public static bool Prefix(PlayerEntityStats __instance, float dt)
    {
        EntityPlayer player = __instance != null ? __instance.m_entity as EntityPlayer : null;
        if (player == null)
            return true;
        if (RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            return false;

        float rate = EffectManager.GetValue(PassiveEffects.HealthChangeOT, _entity: __instance.m_entity);
        // Let vanilla process damage-over-time paths. Replace only positive natural healing
        // so hydration no longer scales every point of healing.
        if (rate <= 0f || __instance.Health.ValuePercent >= 1f)
            return true;

        __instance.Health.RegenerationAmount = rate * dt;
        RebirthHealthCapacityService.ApplyNativeHealthCapacity(__instance, player);
        __instance.Health.Tick(dt);
        return false;
    }

    [HarmonyPostfix]
    public static void Postfix(PlayerEntityStats __instance)
    {
        EntityPlayer player = __instance != null ? __instance.m_entity as EntityPlayer : null;
        if (player == null || RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            return;
        // Vanilla damage/heal paths can rewrite HealthMaxBlockage during UpdatePlayerHealthOT.
        // Re-assert the one canonical Survivor Health Capacity representation after the native tick.
        RebirthHealthCapacityService.ApplyNativeHealthCapacity(__instance, player);
    }
}

[HarmonyPatch(typeof(PlayerEntityStats), nameof(PlayerEntityStats.UpdatePlayerStaminaOT))]
public static class RebirthMetabolismStaminaRegenPatch
{
    [HarmonyPrefix]
    public static bool Prefix(PlayerEntityStats __instance, float _dt)
    {
        EntityPlayer player = __instance != null ? __instance.m_entity as EntityPlayer : null;
        if (player == null)
            return true;
        if (RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            return false;

        // This patch owns the live Stamina MaxModifier, so it must explicitly honor native
        // god mode instead of immediately re-applying injury/exhaustion blockage afterward.
        if (player.IsGodMode.Value)
        {
            __instance.Stamina.RegenerationAmount = 0f;
            __instance.Stamina.MaxModifier = 0f;
            __instance.Stamina.Value = __instance.Stamina.ModifiedMax;
            return false;
        }

        float baseRate = EffectManager.GetValue(PassiveEffects.StaminaChangeOT, _entity: __instance.m_entity);
        if (__instance.Stamina.ValuePercent < 1f && baseRate > 0f)
            __instance.Stamina.RegenerationAmount = baseRate * _dt;
        else if (baseRate < 0f)
            __instance.Stamina.RegenerationAmount = baseRate * _dt;

        float resolved = EffectManager.GetValue(
            PassiveEffects.StaminaChangeOT,
            _originalValue: __instance.Stamina.RegenerationAmount,
            _entity: __instance.m_entity,
            tags: __instance.m_entity.CurrentMovementTag | __instance.m_entity.CurrentStanceTag);

        // Intentionally omit vanilla GetWaterPercent() multiplication. REBIRTH routes
        // Hydration/Nutrition through the Energy reserve, and Energy alone gates this
        // metabolism stamina-recovery multiplier.
        float energyMultiplier = RebirthMetabolismService.GetStaminaRecoveryMultiplier(player);
        __instance.Stamina.RegenerationAmount = resolved * energyMultiplier * _dt;
        __instance.Stamina.MaxModifier = -EffectManager.GetValue(PassiveEffects.StaminaMaxBlockage, _entity: __instance.m_entity);
        __instance.Stamina.Tick(_dt);
        return false;
    }
}


[HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerDisconnected))]
public static class RebirthMetabolismDisconnectPatch
{
    [HarmonyPrefix]
    public static void Prefix(ClientInfo _cInfo)
    {
        if (!RebirthMetabolismService.IsServerAuthority || _cInfo == null || _cInfo.entityId < 0 || GameManager.Instance == null)
            return;
        EntityPlayer player = GameManager.Instance.World != null ? GameManager.Instance.World.GetEntity(_cInfo.entityId) as EntityPlayer : null;
        if (player != null)
        {
            RebirthMetabolismState state;
            if (RebirthMetabolismStateRepository.TryGet(player, out state) && state != null)
                state.Touch();
        }
        RebirthMetabolismStateRepository.SaveIfDirty("player-disconnect");
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerSpawnedInWorld))]
public static class RebirthMetabolismSpawnPatch
{
    [HarmonyPostfix]
    public static void Postfix(ClientInfo _cInfo, RespawnType _respawnReason, Vector3i _pos, int _entityId)
    {
        if (!RebirthMetabolismService.IsServerAuthority || GameManager.Instance == null || GameManager.Instance.World == null)
            return;
        EntityPlayer player = GameManager.Instance.World.GetEntity(_entityId) as EntityPlayer;
        if (player == null)
            return;

        bool engineFreshSpawn = _respawnReason == RespawnType.NewGame || _respawnReason == RespawnType.EnterMultiplayer;

        if (RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            // In Rebirth progression mode, engine spawn is no longer the creation commit boundary.
            // A NewGame/EnterMultiplayer entity exists while the Survivor creator is open. Do not
            // reset, initialize, drain, sanitize, or persist metabolism until the authoritative
            // world origin has been committed by RebirthSurvivorCreationService.
            if (RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            {
                if (RebirthMetabolismService.DebugEnabled)
                    Log.Out("[REBIRTH Metabolism] spawn initialization deferred pending Survivor origin entity="
                        + player.entityId + " reason=" + _respawnReason.ToStringCached<RespawnType>());
                return;
            }

            RebirthMetabolismService.EnsurePlayerReady(player);
            RebirthMetabolismState survivorState = RebirthMetabolismStateRepository.GetOrCreate(player);
            if (survivorState == null)
                return;
            if (!RebirthSurvivorMetabolismCreationGate.IsLinkedToCommittedOrigin(player, survivorState))
            {
                Log.Error("[REBIRTH Metabolism] committed Survivor exists but metabolism generation does not match; spawn initialization blocked.");
                return;
            }

            // Fresh Survivor startup sanitization is performed exactly once by the creation service
            // after durable origin commit. Returning characters/respawns use non-fresh sanitation.
            RebirthToolbeltCapacity.SanitizePlayerStartup(player, survivorState, false);
            RebirthMetabolismStateRepository.SaveIfDirty("survivor-player-spawn");
            return;
        }

        // Base Game progression retains the established metabolism lifecycle. Here the engine's
        // NewGame/EnterMultiplayer reason is still the correct fresh-character boundary.
        if (engineFreshSpawn)
            RebirthMetabolismStateRepository.ResetForFreshCharacter(player, _respawnReason.ToStringCached<RespawnType>());

        RebirthMetabolismService.EnsurePlayerReady(player);
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null)
            return;
        RebirthToolbeltCapacity.SanitizePlayerStartup(player, state, engineFreshSpawn);
        RebirthMetabolismStateRepository.SaveIfDirty(engineFreshSpawn ? "fresh-player-spawn" : "player-spawn");
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnEntityDeath))]
public static class RebirthMetabolismDeathPatch
{
    [HarmonyPrefix]
    public static void Prefix(EntityAlive __instance)
    {
        EntityPlayer player = __instance as EntityPlayer;
        if (player != null && RebirthMetabolismService.IsServerAuthority)
            RebirthMetabolismService.HandlePlayerDeath(player);
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.GetBindingValueInternal))]
[HarmonyPriority(Priority.Last)]
public static class RebirthMetabolismItemTooltipPatch
{
    private const string LiquidBarColor = "66,139,190,255";
    private sealed class DefinitionFrame { internal int Frame=-1; internal RebirthConsumableDefinition Definition; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ItemValue,DefinitionFrame> Definitions=new System.Runtime.CompilerServices.ConditionalWeakTable<ItemValue,DefinitionFrame>();
    private static bool ResolveFrame(ItemValue item,out RebirthConsumableDefinition definition)
    {
        var entry=Definitions.GetValue(item,_=>new DefinitionFrame());
        if(entry.Frame!=UnityEngine.Time.frameCount){entry.Frame=UnityEngine.Time.frameCount;RebirthConsumableResolver.TryResolve(item,out entry.Definition);}
        definition=entry.Definition;return definition!=null;
    }

    [HarmonyPostfix]
    public static void Postfix(XUiC_ItemStack __instance, ref bool __result, ref string _value, string _bindingName)
    {
        // Item slots refresh many unrelated bindings. Resolving cooked metadata for
        // colour, selection and visibility creates work that this patch never uses.
        if (_bindingName != "tooltip" && _bindingName != "itemcount" &&
            _bindingName != "hasdurability" && _bindingName != "durabilityfill" &&
            _bindingName != "durabilitycolor")
            return;
        if (__instance == null)
            return;

        ItemStack stack = __instance.ItemStack;
        if (stack == null || stack.IsEmpty() || stack.itemValue == null)
            return;

        if (_bindingName=="tooltip" && RebirthCharacterItemStatsTooltip.Build(__instance,stack)!=null){_value="";__result=true;return;}
        // Food/other items do not use liquid bar bindings; do not clone cooked definitions for them.
        if(_bindingName!="tooltip"&&(!RebirthConsumableResolver.TryResolve(stack.itemValue.ItemClass,out var rawDefinition)||!rawDefinition.IsDrink))return;        RebirthConsumableDefinition d;
        if (!ResolveFrame(stack.itemValue, out d) || d == null)
            return;

        // A liquid container uses the game's familiar degradation bar as a volume bar.
        // DegradationMax == capacity (mL); UseTimes == amount consumed.
        if (d.IsDrink)
        {
            if (_bindingName == "itemcount")
            {
                _value = stack.count.ToString(CultureInfo.InvariantCulture);
                __result = true;
                return;
            }
            if (_bindingName == "hasdurability")
            {
                _value = "true";
                __result = true;
                return;
            }
            if (_bindingName == "durabilityfill")
            {
                _value = RebirthLiquidContainerService.GetFill01(stack.itemValue, d).ToString("0.####", CultureInfo.InvariantCulture);
                __result = true;
                return;
            }
            if (_bindingName == "durabilitycolor")
            {
                _value = LiquidBarColor;
                __result = true;
                return;
            }
        }

        if (_bindingName != "tooltip")
            return;

        if(RebirthCharacterItemStatsTooltip.Build(__instance,stack)!=null){_value="";__result=true;return;}
        string tail;
        if (d.IsDrink)
        {
            float remaining = RebirthLiquidContainerService.GetRemainingMl(stack.itemValue, d);
            tail = "\nCapacity: " + RebirthLiquidContainerService.FormatVolume(d.ContainerCapacityMl)
                 + "\nRemaining: " + RebirthLiquidContainerService.FormatVolume(remaining)
                 + "\nSip per use: " + RebirthLiquidContainerService.FormatVolume(d.ManualSipMl)
                 + (d.HydrationEquippable ? "\nHydration Slot: compatible" : string.Empty);
        }
        else if (d.IsFood)
        {
            tail = "\nNutrition: " + d.NutritionUnits.ToString("0.#", CultureInfo.InvariantCulture)
                 + "\nWater content: " + RebirthLiquidContainerService.FormatVolume(d.FoodWaterMl)
                 + "\nStomach volume: " + RebirthLiquidContainerService.FormatVolume(d.StomachVolumeMl + d.FoodWaterMl)
                 + "\nDigestion: " + PrettyProfile(d.DigestionProfile, "Normal");
        }
        else if (d.IsSupplement)
        {
            tail = "\nMetabolism supplement: " + PrettyProfile(d.SupplementProfile, "General");
        }
        else
            return;

        if(stack.itemValue.TryGetMetadata("rebirth.cooking.quality",out string cookingQuality))
        {
            tail+="\nCooking quality: "+cookingQuality;
            if(RebirthFoodMoodResolver.TryResolve(stack.itemValue,out var cookedMood))tail+="\nBase comfort: "+cookedMood.BaseMoodInfluence.ToString("0.#",CultureInfo.InvariantCulture);
        }
        _value = (_value ?? string.Empty) + tail;
        __result = true;
    }

    internal static string PrettyProfile(string value, string fallback)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;
        if (value.Length == 1)
            return value.ToUpperInvariant();
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
}

/// <summary>
/// Replaces obsolete instant Food/Water item-stat rows with the physical values the
/// metabolism implementation actually uses. Secondary vanilla/REBIRTH effects such as
/// stamina regeneration, healing, dysentery risk, resistance and duration are retained.
/// </summary>
[HarmonyPatch(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.GetBindingValueInternal))]
[HarmonyPriority(Priority.Last)]
public static class RebirthMetabolismItemInfoPatch
{
    private const string LiquidBarColor = "66,139,190,255";
    private sealed class DefinitionFrame { internal int Frame=-1; internal RebirthConsumableDefinition Definition; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ItemValue,DefinitionFrame> Definitions=new System.Runtime.CompilerServices.ConditionalWeakTable<ItemValue,DefinitionFrame>();
    private static bool ResolveFrame(ItemValue item,out RebirthConsumableDefinition definition)
    {
        var entry=Definitions.GetValue(item,_=>new DefinitionFrame());
        if(entry.Frame!=UnityEngine.Time.frameCount){entry.Frame=UnityEngine.Time.frameCount;RebirthConsumableResolver.TryResolve(item,out entry.Definition);}
        definition=entry.Definition;return definition!=null;
    }

    [HarmonyPostfix]
    public static void Postfix(XUiC_ItemInfoWindow __instance, ref bool __result, ref string value, string bindingName)
    {
        if (__instance == null || __instance.itemStack == null || __instance.itemStack.IsEmpty() || __instance.itemStack.itemValue == null)
            return;

        bool weaponTitle=bindingName.StartsWith("itemstattitle",StringComparison.Ordinal);
        bool weaponValue=bindingName.StartsWith("itemstat",StringComparison.Ordinal)&&!weaponTitle;
        if((weaponTitle||weaponValue)&&int.TryParse(bindingName.Substring(weaponTitle?13:8),out var weaponIndex)&&
            RebirthWeaponDetailRows.TryGet(__instance.xui,__instance.itemStack,null,weaponIndex-1,out var weaponLabel,out var weaponText))
        {value=weaponTitle?weaponLabel:weaponText;__result=true;return;}        RebirthConsumableDefinition d;
        if (!RebirthConsumableResolver.TryResolve(__instance.itemStack.itemValue, out d) || d == null)
            return;

        if (d.IsDrink)
        {
            if (bindingName == "hasdurability")
            {
                value = "true";
                __result = true;
                return;
            }
            if (bindingName == "durabilityfill")
            {
                value = RebirthLiquidContainerService.GetFill01(__instance.itemStack.itemValue, d).ToString("0.####", CultureInfo.InvariantCulture);
                __result = true;
                return;
            }
            if (bindingName == "durabilitycolor")
            {
                value = LiquidBarColor;
                __result = true;
                return;
            }
            if (bindingName == "durabilityjustify")
            {
                value = __instance.itemStack.count>1 ? "right" : "center";
                __result = true;
                return;
            }
            if (bindingName == "durabilitytext")
            {
                value = __instance.itemStack.count>1?__instance.itemStack.count.ToString(CultureInfo.InvariantCulture):RebirthLiquidContainerService.FormatVolume(RebirthLiquidContainerService.GetRemainingMl(__instance.itemStack.itemValue, d));
                __result = true;
                return;
            }
        }

        bool titleBinding;
        int statIndex;
        if (!TryGetStatIndex(bindingName, out titleBinding, out statIndex))
            return;

        if (!d.IsFood && !d.IsDrink) return;

        // Fix32: native item-info and custom selected/hover panels use one row source.
        // Cache a projection across the title/value binding batch; never alter inventory data.
        var rows = GetPresentationRows(__instance);
        if (rows == null) return;
        if (statIndex >= rows.Count) value = string.Empty;
        else
        {
            var row = rows[statIndex];
            // Preserve native comparison/player-effect formatting on secondary effect rows.
            value = titleBinding ? row.Title : row.NativeIndex >= 0
                ? RebirthItemStatColors.Format(__instance.GetStatValue(row.NativeIndex)) : row.Value;
        }
        __result = true;
    }

    private sealed class PresentationCache
    {
        public PresentationCache() { }
        public int Frame = -1, Fingerprint;
        public ItemStack Source;
        public System.Collections.Generic.List<RebirthConsumableItemPresentation.Row> Rows;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemInfoWindow, PresentationCache>
        PresentationCaches = new System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemInfoWindow, PresentationCache>();

    private static System.Collections.Generic.List<RebirthConsumableItemPresentation.Row> GetPresentationRows(XUiC_ItemInfoWindow window)
    {
        var cache = PresentationCaches.GetOrCreateValue(window);
        ItemStack source = window.itemStack;
        int stamp = RebirthConsumableItemPresentation.BindingFingerprint(source);
        if (cache.Frame != Time.frameCount || cache.Source != source || cache.Fingerprint != stamp)
        {
            cache.Frame = Time.frameCount; cache.Source = source; cache.Fingerprint = stamp;
            RebirthConsumableItemPresentation.TryBuild(source, null, window.xui, out cache.Rows);
        }
        return cache.Rows;
    }

    private static bool TryGetStatIndex(string bindingName, out bool titleBinding, out int zeroBasedIndex)
    {
        titleBinding = false;
        zeroBasedIndex = -1;
        if (string.IsNullOrEmpty(bindingName))
            return false;

        const string titlePrefix = "itemstattitle";
        const string valuePrefix = "itemstat";
        string number;
        if (bindingName.StartsWith(titlePrefix, StringComparison.Ordinal))
        {
            titleBinding = true;
            number = bindingName.Substring(titlePrefix.Length);
        }
        else if (bindingName.StartsWith(valuePrefix, StringComparison.Ordinal))
        {
            number = bindingName.Substring(valuePrefix.Length);
        }
        else
            return false;

        int oneBased;
        if (!int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out oneBased) || oneBased <= 0)
            return false;
        zeroBasedIndex = oneBased - 1;
        return true;
    }

}

/// <summary>
/// Read-only food/drink presentation shared by the selected-item panels, native item-info
/// bindings and hover cards. Container amounts are not food quality or instant hydration.
/// Kept in an existing source file so explicit project lists need no new Compile entry.
/// </summary>
public static class RebirthConsumableItemPresentation
{
    // The existing fixed selected-item panels have seven rows. Use the same bounded list in
    // the popup, rather than silently showing different secondary effects in the two views.
    public const int MaxRows = 7;

    public sealed class Row
    {
        public string Title = "", Value = "", SelectedValue = "";
        public string Icon = "ui_game_symbol_tool", Atlas = "UIAtlas";
        public int NativeIndex = -1;
    }

    // Fix33: energy belongs to the player's metabolism, not the item-stat presentation.
    // Nutrition/water metadata and the actual digestion/absorption calculations are unchanged.
    private static readonly string[] FoodKeys = { "nutrition", "water", "comfort" };
    private static readonly string[] FoodTitles = { "Nutrition", "Water", "Base comfort" };

    public static bool TryBuild(ItemStack stack, ItemStack comparison, XUi ui,
        out System.Collections.Generic.List<Row> rows, bool comparisonIsCandidate = false)
    {
        rows = null;
        if (stack == null || stack.IsEmpty() || stack.itemValue == null) return false;
        ItemValue item = stack.itemValue;
        var viewer = ui?.playerUI?.entityPlayer;
        RebirthConsumableDefinition definition;
        if (!RebirthConsumableResolver.TryResolve(item, out definition)) definition = null;
        bool drink = definition != null && definition.IsDrink;
        if (!drink && !(definition != null && definition.IsFood) &&
            !RebirthCharacterItemStatsTooltip.FoodPopup(item)) return false;

        ItemValue reference = null;
        RebirthConsumableDefinition referenceDefinition = null;
        if (comparison != null && !comparison.IsEmpty() && comparison.itemValue != null &&
            XUiM_ItemStack.CanCompare(item.ItemClass, comparison.itemValue.ItemClass))
        {
            if (!RebirthConsumableResolver.TryResolve(comparison.itemValue, out referenceDefinition)) referenceDefinition = null;
            bool referenceDrink = referenceDefinition != null && referenceDefinition.IsDrink;
            if (drink == referenceDrink && (referenceDrink ||
                RebirthCharacterItemStatsTooltip.FoodPopup(comparison.itemValue)))
                reference = comparison.itemValue;
        }

        rows = new System.Collections.Generic.List<Row>(MaxRows);
        if (drink)
        {
            // No synthetic full-bottle comparison: 375 mL remaining must not acquire a red
            // '-125' quality delta merely because an earlier sip depleted a 500 mL bottle.
            for (int i = 0; i < 3; ++i)
                rows.Add(LiquidRow(item, definition, i, reference, referenceDefinition, comparisonIsCandidate));

            // Keep nonzero cooked-drink nutrition/comfort metadata as well. Their values
            // retain their existing per-item meaning; do not relabel them as hydration/sip.
            float nutrition = RebirthCookingItemStats.Value(item, "nutrition");
            float comfort = RebirthCookingItemStats.Value(item, "comfort");
            float referenceNutrition = reference != null ? RebirthCookingItemStats.Value(reference, "nutrition") : 0f;
            float referenceComfort = reference != null ? RebirthCookingItemStats.Value(reference, "comfort") : 0f;
            if (nutrition != 0f || comfort != 0f || referenceNutrition != 0f || referenceComfort != 0f)
            {
                ItemValue foodReference = reference ?? RebirthCookingItemStats.BaseItem(item);
                if (nutrition != 0f || referenceNutrition != 0f) rows.Add(FoodRow(item, foodReference, 0, reference != null, comparisonIsCandidate));
                if (comfort != 0f || referenceComfort != 0f) rows.Add(FoodRow(item, foodReference, 2, reference != null, comparisonIsCandidate));
            }

            // Preserve the native non-reserve effects that the original metabolism item-info
            // patch retained (e.g. stamina regeneration, healing, dysentery). The obsolete
            // instant Food/Water reserve rows are excluded in every consumer of this model.
            var display = UIDisplayInfoManager.Current.GetDisplayStatsForTag(item.ItemClass.DisplayType);
            if (display != null && display.DisplayStats != null && viewer != null)
            {
                for (int nativeIndex = 0; nativeIndex < display.DisplayStats.Count; ++nativeIndex)
                {
                    if (rows.Count >= MaxRows) break;
                    DisplayInfoEntry stat = display.DisplayStats[nativeIndex];
                    if (stat == null || IsLegacyReserveEntry(stat) ||
                        !ShouldShowNativeStat(stat, item, reference, ui)) continue;
                    string value = reference != null && comparisonIsCandidate
                        ? XUiM_ItemStack.GetStatItemValueTextWithCompareInfo(item, reference, viewer, stat)
                        : RebirthItemStatColors.NativeValue(stack, viewer, stat);
                    string selectedValue = reference != null
                        ? RebirthItemStatColors.NativeValue(comparison, viewer, stat) : "";
                    string id = stat.StatType.ToString();
                    rows.Add(new Row {
                        NativeIndex = nativeIndex,
                        Title = stat.TitleOverride ?? UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType),
                        Value = RebirthItemStatColors.Format(value),
                        SelectedValue = RebirthItemStatColors.Format(selectedValue),
                        Icon = id.IndexOf("Stamina", StringComparison.OrdinalIgnoreCase) >= 0 ? "ui_game_symbol_run"
                            : "ui_game_symbol_medical"
                    });
                }
            }
        }
        else
        {
            // Keep cooked-food quality/comfort/nutrition metadata and the existing ingredient
            // card policy. A real comparison uses that item; a single food uses its authored
            // base item as before. This base-item comparison never applies to bottle volume.
            bool hasReference = reference != null;
            reference = reference ?? RebirthCookingItemStats.BaseItem(item);
            for (int i = 0; i < FoodKeys.Length; ++i)
                rows.Add(FoodRow(item, reference, i, hasReference, comparisonIsCandidate));
            reference = hasReference ? reference : null;
            var display = UIDisplayInfoManager.Current.GetDisplayStatsForTag(item.ItemClass.DisplayType);
            if (display != null && display.DisplayStats != null && viewer != null)
            {
                for (int nativeIndex = 0; nativeIndex < display.DisplayStats.Count; ++nativeIndex)
                {
                    if (rows.Count >= MaxRows) break;
                    DisplayInfoEntry stat = display.DisplayStats[nativeIndex];
                    if (stat == null || IsLegacyReserveEntry(stat) ||
                        !ShouldShowNativeStat(stat, item, reference, ui)) continue;
                    string value = reference != null && comparisonIsCandidate
                        ? XUiM_ItemStack.GetStatItemValueTextWithCompareInfo(item, reference, viewer, stat)
                        : RebirthItemStatColors.NativeValue(stack, viewer, stat);
                    string selectedValue = reference != null
                        ? RebirthItemStatColors.NativeValue(comparison, viewer, stat) : "";
                    string id = stat.StatType.ToString();
                    rows.Add(new Row {
                        NativeIndex = nativeIndex,
                        Title = stat.TitleOverride ?? UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType),
                        Value = RebirthItemStatColors.Format(value),
                        SelectedValue = RebirthItemStatColors.Format(selectedValue),
                        Icon = id.IndexOf("Stamina", StringComparison.OrdinalIgnoreCase) >= 0 ? "ui_game_symbol_run"
                            : "ui_game_symbol_medical"
                    });
                }
            }

        }
        return true;
    }

    public static Row LiquidRow(ItemValue item, RebirthConsumableDefinition definition, int index,
        ItemValue reference = null, RebirthConsumableDefinition referenceDefinition = null, bool referenceIsCandidate = false)
    {
        var row = new Row { Icon = "ui_game_symbol_water" };
        if (definition == null || !definition.IsDrink || index < 0 || index >= 3) return row;
        bool compare = reference != null && referenceDefinition != null && referenceDefinition.IsDrink;
        switch (index)
        {
            case 0: row.Title = Localize("xuiRebirthMetabolismCapacity", "Capacity"); break;
            case 1: row.Title = Localize("xuiRebirthMetabolismRemaining", "Remaining"); break;
            case 2: row.Title = Localize("xuiRebirthMetabolismSipSize", "Sip per use"); break;
        }
        float amount = LiquidAmount(item, definition, index);
        row.Value = LiquidText(amount);
        if (compare)
        {
            float other = LiquidAmount(reference, referenceDefinition, index);
            row.SelectedValue = LiquidText(other);
            // Quantity differences are neutral, not a food-quality downgrade or an instant
            // hydration gain. Side-by-side values remain readable in the approved columns.
            float delta = referenceIsCandidate ? other - amount : amount - other;
            if (Mathf.Abs(delta) > .001f)
                row.Value += " [A0A0AC](" + (delta > 0f ? "+" : "-") +
                    RebirthLiquidContainerService.FormatVolume(Mathf.Abs(delta)) + ")[-]";
        }
        row.Value = RebirthItemStatColors.Format(row.Value);
        return row;
    }

    private static float LiquidAmount(ItemValue item, RebirthConsumableDefinition definition, int index)
    {
        if (index == 0) return definition.ContainerCapacityMl;
        float remaining = RebirthLiquidContainerService.GetRemainingMl(item, definition);
        if (index == 1) return remaining;
        // This is the configured manual sip, just as in the pre-rewrite native binding. Do
        // not predict stomach admission, perform consumption or imply immediate absorption.
        return index == 2 ? definition.ManualSipMl : 0f;
    }

    private static string LiquidText(float amount) => RebirthLiquidContainerService.FormatVolume(amount);

    public static Row FoodRow(ItemValue item, ItemValue reference, int index, bool isComparison = false, bool referenceIsCandidate = false)
    {
        if (index < 0 || index >= FoodKeys.Length) return new Row();
        string key = FoodKeys[index], unit = index == 1 ? " mL" : "";
        float amount = RebirthCookingItemStats.Value(item, key);
        float baseline = RebirthCookingItemStats.Value(reference, key);
        return new Row {
            Title = Localize("rbCookingStat" + key, FoodTitles[index]),
            Value = RebirthItemStatColors.Format(isComparison
                ? FoodComparisonText(amount, baseline, unit, referenceIsCandidate)
                : RebirthCookingItemStats.Display(amount, baseline, unit)),
            SelectedValue = baseline.ToString("0.#", CultureInfo.InvariantCulture) + unit,
            Atlas = index == 2 ? "RebirthSurvivorIcons" : "UIAtlas",
            Icon = index == 0 ? "ui_game_symbol_fork" : index == 1 ? "ui_game_symbol_water"
                : "rb_condition_mood_good"
        };
    }

    private static string FoodComparisonText(float amount, float reference, string unit, bool referenceIsCandidate)
    {
        string text = amount.ToString("0.#", CultureInfo.InvariantCulture) + unit;
        // Selected-panel deltas show the hovered candidate's change. A popup shows the
        // hovered item's own change against the selected baseline. Neither replaces selection.
        float delta = referenceIsCandidate ? reference - amount : amount - reference;
        if (Mathf.Abs(delta) <= .001f) return text;
        return text + (delta > 0f ? " [70DD70](+" : " [F07070](")
            + delta.ToString("0.#", CultureInfo.InvariantCulture) + ")"
            + (delta > 0f ? " \u25B2[-]" : " \u25BC[-]");
    }

    /// <summary>
    /// Presentation-only filter. Read the native numeric health value (including item mods),
    /// never parse localized/colored labels and never edit item effects to hide a row.
    /// In a real comparison, retain the aligned row if either item has a health effect.
    /// </summary>
    public static bool ShouldShowNativeStat(DisplayInfoEntry entry, ItemValue item, ItemValue comparison, XUi ui)
    {
        if (entry == null) return false;
        string customName = entry.CustomName ?? "";
        string effectName = entry.StatType.ToString();
        if (customName.IndexOf("Energy", StringComparison.OrdinalIgnoreCase) >= 0 ||
            effectName.IndexOf("Energy", StringComparison.OrdinalIgnoreCase) >= 0) return false;

        // These identify health granted/lost on use, not maximum health, abrasion healing
        // multipliers, infection risk or other unrelated zero-valued statistics.
        bool healthAmount = string.Equals(customName, "foodHealthAmount", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(customName, "dHealthAmount", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(customName, "dInstantHealth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(customName, "$healthAmountAdd", StringComparison.OrdinalIgnoreCase) ||
            (customName.Length == 0 && entry.StatType == PassiveEffects.HealthChangeOT);
        if (!healthAmount) return true;
        // Fail open while the viewer/source is unavailable; do not pretend unknown = zero.
        if (item == null || item.IsEmpty() || ui?.playerUI?.entityPlayer == null) return true;
        if (NativeHealthAmount(item, entry, ui) != 0f) return true;
        return comparison != null && !comparison.IsEmpty() &&
            XUiM_ItemStack.CanCompare(item.ItemClass, comparison.ItemClass) &&
            NativeHealthAmount(comparison, entry, ui) != 0f;
    }

    private static float NativeHealthAmount(ItemValue item, DisplayInfoEntry entry, XUi ui)
    {
        // Same evaluation path already used by the shared stats popup's Number method.
        var tags = entry.TagsSet ? entry.Tags : XUiM_ItemStack.primaryFastTags | XUiM_ItemStack.physicalDamageFastTags;
        float amount = string.IsNullOrEmpty(entry.CustomName)
            ? EffectManager.GetValue(entry.StatType, item, _entity: ui.playerUI.entityPlayer, tags: tags,
                calcEquipment: false, calcHoldingItem: false, calcProgression: false, calcBuffs: false, useMods: true)
            : XUiM_ItemStack.GetCustomValue(entry, item, true);
        XUiM_ItemStack.degradationMaxMod(entry.StatType, item, ui.playerUI.entityPlayer, tags, true, ref amount);
        return amount;
    }

    public static bool IsLegacyReserveEntry(DisplayInfoEntry entry)
    {
        string name = entry != null ? entry.CustomName : null;
        return string.Equals(name, "$waterAmountAdd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "$foodAmountAdd", StringComparison.OrdinalIgnoreCase);
    }

    public static string Footer(ItemValue item, bool compare)
    {
        RebirthConsumableDefinition definition;
        if (RebirthConsumableResolver.TryResolve(item, out definition) && definition != null && definition.IsDrink)
            return "Bottle contents \u2022 Configured sip size";
        return compare ? "Hovered compared with selected item" : "Per serving \u2022 Changes from base stats";
    }

    // Compact hosts (Character/Creative) have a single summary line rather than the large
    // stat column. Show the same three liquid quantities without resizing their panels.
    public static string LiquidSummary(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty() || stack.itemValue == null) return "";
        RebirthConsumableDefinition definition;
        if (!RebirthConsumableResolver.TryResolve(stack.itemValue, out definition) ||
            definition == null || !definition.IsDrink) return "";
        return RebirthLiquidContainerService.FormatVolume(RebirthLiquidContainerService.GetRemainingMl(stack.itemValue, definition))
            + " / " + RebirthLiquidContainerService.FormatVolume(definition.ContainerCapacityMl)
            + " \u2022 Sip: " + RebirthLiquidContainerService.FormatVolume(definition.ManualSipMl);
    }

    // Selected panels may compare cloned ItemStacks. Include the actual liquid readout in
    // their change key instead of depending on the native stack equality's metadata policy.
    public static int LiquidFingerprint(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty() || stack.itemValue == null) return 0;
        RebirthConsumableDefinition definition;
        if (!RebirthConsumableResolver.TryResolve(stack.itemValue, out definition) ||
            definition == null || !definition.IsDrink) return 0;
        unchecked
        {
            int hash = definition.ContainerCapacityMl.GetHashCode();
            hash = hash * 31 + RebirthLiquidContainerService.GetRemainingMl(stack.itemValue, definition).GetHashCode();
            hash = hash * 31 + definition.ManualSipMl.GetHashCode();
            hash = hash * 31 + definition.EnergyUnits.GetHashCode();
            return hash * 31 + definition.InitialVolumeMl.GetHashCode();
        }
    }

    public static int BindingFingerprint(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty() || stack.itemValue == null) return 0;
        ItemValue item = stack.itemValue;
        unchecked
        {
            int hash = ((item.type * 31 + stack.count) * 31 + item.Meta) * 31 + item.Quality;
            hash = hash * 31 + item.UseTimes.GetHashCode();
            // Order-independent: metadata dictionaries need not preserve insertion order.
            int metadataHash = 0;
            if (item.Metadata != null)
                foreach (var entry in item.Metadata)
                {
                    object metadata = entry.Value;
                    metadataHash += (StringComparer.Ordinal.GetHashCode(entry.Key ?? "") * 397)
                        ^ (metadata != null ? metadata.GetHashCode() : 0);
                }
            hash = hash * 31 + metadataHash;
            ItemValue[] mods = item.modifications;
            for (int i = 0; mods != null && i < mods.Length; ++i)
                hash = hash * 31 + (mods[i] != null ? mods[i].type : 0);
            return hash;
        }
    }

    private static string Localize(string key, string fallback)
    {
        string text = Localization.Get(key);
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }
}

/// <summary>Native quality initialization may reset Stacknumber after XML has supplied ten.</summary>
[HarmonyPatch]
public static class RebirthLiquidStackCapacityPatch
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        var late=AccessTools.Method(typeof(ItemClass),"LateInit",Type.EmptyTypes);
        if(late!=null)yield return late;
        else Log.Warning("[REBIRTH Liquids] ItemClass.LateInit unavailable; using transfer-time stack-cap correction.");
    }
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(ItemClass __instance) { Apply(__instance); }
    public static void Apply(ItemClass __instance)
    {
        // Read the loaded class directly: do not populate the consumable cache during item loading.
        if (__instance == null || __instance.Stacknumber.Value == 10 || __instance.Properties == null) return;
        string kind;
        if (__instance.Properties.Values.TryGetValue("RebirthMetabolismType", out kind) &&
            string.Equals(kind, "Drink", StringComparison.OrdinalIgnoreCase))
            __instance.Stacknumber.Value = 10;
    }
}

[HarmonyPatch(typeof(NetPackageEntityStatChanged),nameof(NetPackageEntityStatChanged.ProcessPackage))]
public static class RebirthMetabolismNativeReservePacketPatch
{
    [HarmonyPrefix]
    public static bool Prefix(NetPackageEntityStatChanged __instance,World _world)
    {
        if(_world==null||_world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return true;
        if(__instance.m_enumStat!=NetPackageEntityStatChanged.EnumStat.Food&&
            __instance.m_enumStat!=NetPackageEntityStatChanged.EnumStat.Water)return true;
        return !(_world.GetEntity(__instance.entityId) is EntityPlayer);
    }
}