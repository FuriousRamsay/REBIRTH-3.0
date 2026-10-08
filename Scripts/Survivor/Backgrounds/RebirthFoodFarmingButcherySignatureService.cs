using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

#nullable disable

public sealed class RebirthPendingChefCraftSignature
{
    public int PlayerId;
    public int ItemType;
    public ushort Seed;
    public ushort Quality;
    public string RecipeName = string.Empty;
    public DateTime CreatedUtc;
}

public sealed class RebirthChefCraftCorrection
{
    public int PlayerId;
    public ItemValue Corrected;
    public DateTime CreatedUtc;
    public RebirthChefCraftCorrection(int playerId, ItemValue corrected)
    {
        PlayerId = playerId;
        Corrected = corrected != null ? corrected.Clone() : null;
        CreatedUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// PC023 / Chunk J authority for Chef Professional Cooking, Farmer Rapid Cultivation and the
/// shared Advanced Farming yield bridge. Butcher Whole Animal is exposed here as a bounded native
/// butcherHarvest delta and is applied by RebirthResourceFieldSkillService through the existing
/// HarvestCount channel. Primitive armor remains a policy-only contract validated by this chunk.
/// </summary>
public static class RebirthFoodFarmingButcherySignatureService
{
    public const string ProfessionalCookingBonusId = "background_bonus.professional_cooking";
    public const string RapidCultivationBonusId = "background_bonus.rapid_cultivation";
    public const string WholeAnimalBonusId = "background_bonus.whole_animal";
    public const string PreparedMealKind = "prepared_meal";

    private const double PendingSeconds = 120.0;
    private static readonly object Gate = new object();
    private static DateTime NextCleanupUtc = DateTime.MinValue;
    private static readonly Dictionary<int, List<RebirthPendingChefCraftSignature>> PendingChef = new Dictionary<int, List<RebirthPendingChefCraftSignature>>();
    private static readonly Queue<RebirthChefCraftCorrection> ChefCorrections = new Queue<RebirthChefCraftCorrection>();
    private static readonly List<RebirthChefCraftCorrection> ClientPendingCorrections = new List<RebirthChefCraftCorrection>();
    private static bool installed;
    private static bool localCraftPatched;
    private static bool workstationCraftPatched;
    private static bool inventoryCommitPatched;

    public static string Install(Harmony harmony)
    {
        if (installed) return "[REBIRTH Food/Farming/Butchery Signatures] already installed";
        installed = true;
        if (harmony != null)
        {
            MethodInfo giveExp = AccessTools.DeclaredMethod(typeof(XUiC_RecipeStack), "giveExp", new[] { typeof(ItemValue), typeof(ItemClass) });
            if (giveExp != null)
            {
                harmony.Patch(giveExp, prefix: new HarmonyMethod(typeof(RebirthFoodFarmingButcherySignatureService), nameof(LocalCraftPrefix)));
                localCraftPatched = true;
            }

            MethodInfo complete = AccessTools.Method(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.AddCraftComplete),
                new[] { typeof(int), typeof(ItemValue), typeof(string), typeof(string), typeof(int), typeof(int) });
            if (complete != null)
            {
                harmony.Patch(complete, prefix: new HarmonyMethod(typeof(RebirthFoodFarmingButcherySignatureService), nameof(WorkstationCraftPrefix)));
                workstationCraftPatched = true;
            }

            Type packageType = AccessTools.TypeByName("NetPackagePlayerInventory");
            MethodInfo process = packageType != null ? AccessTools.Method(packageType, "ProcessPackage", new[] { typeof(World), typeof(GameManager) }) : null;
            if (process != null)
            {
                harmony.Patch(process, postfix: new HarmonyMethod(typeof(RebirthFoodFarmingButcherySignatureService), nameof(NativeInventoryCommitPrefix)));
                inventoryCommitPatched = true;
            }
        }

        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Food/Farming/Butchery Signatures] installed localCraft=" + localCraftPatched
            + " workstationCraft=" + workstationCraftPatched + " inventoryCommit=" + inventoryCommitPatched;
    }

    /// <summary>
    /// Client-side projection is deliberately stamped before the native inventory add so a newly
    /// prepared Chef meal cannot merge into an unprepared stack. The server request and inventory
    /// commit path revalidates the recipe, crafter Background and authoritative tuning before the
    /// item is accepted as prepared provenance.
    /// </summary>
    public static void LocalCraftPrefix(XUiC_RecipeStack __instance, ItemValue _iv, ItemClass _ic)
    {
        if (__instance == null || _iv == null || _iv.ItemClass == null || __instance.recipe == null || __instance.AmountToRepair > 0) return;
        EntityPlayer player = __instance.xui != null && __instance.xui.playerUI != null ? __instance.xui.playerUI.entityPlayer : null;
        if (player == null || player.world == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        string recipeName = __instance.recipe.GetName();
        if (!IsEligibleChefMeal(player, recipeName, _iv)) return;

        if (player.world.IsRemote())
        {
            StampChefMeal(player, recipeName, _iv, false, "client-prestack-projection");
            ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (c != null)
                c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthChefCraftSignatureRequest>()
                    .Setup(player.entityId, _iv.type, _iv.Seed, _iv.Quality, recipeName));
        }
        else
        {
            StampChefMeal(player, recipeName, _iv, true, "local-authoritative-craft");
        }
    }

    public static void WorkstationCraftPrefix(TileEntityWorkstation __instance, int crafterEntityID, ItemValue itemCrafted, string recipeName, int craftedCount)
    {
        if (craftedCount <= 0 || itemCrafted == null || GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.IsRemote()) return;
        EntityPlayer player = GameManager.Instance.World.GetEntity(crafterEntityID) as EntityPlayer;
        if (player != null)
            StampChefMeal(player, recipeName, itemCrafted, true, "server-workstation-complete");
    }

    public static void RegisterChefCraftRequest(EntityPlayer player, int itemType, ushort seed, ushort quality, string recipeName)
    {
        if (player == null || player.world == null || player.world.IsRemote() || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        Recipe recipe = null;
        try { recipe = CraftingManager.GetRecipe(recipeName ?? string.Empty); } catch { }
        if (recipe == null || recipe.itemValueType != itemType || !string.Equals(RebirthServiceCraftSkillService.ClassifyRecipe(recipe), "skill.cooking", StringComparison.OrdinalIgnoreCase)) return;
        ItemClass output = ItemClass.GetForId(itemType);
        RebirthConsumableDefinition def;
        if (output == null || !RebirthConsumableResolver.TryResolve(output, out def) || def == null || !def.IsFood) return;
        if (!RebirthBackgroundBonusService.HasBonus(player, ProfessionalCookingBonusId)) return;

        lock (Gate)
        {
            CleanupLocked();
            List<RebirthPendingChefCraftSignature> list;
            if (!PendingChef.TryGetValue(player.entityId, out list))
                PendingChef[player.entityId] = list = new List<RebirthPendingChefCraftSignature>();
            list.RemoveAll(delegate(RebirthPendingChefCraftSignature p)
            {
                return p != null && p.ItemType == itemType && p.Seed == seed && p.Quality == quality && string.Equals(p.RecipeName, recipeName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            });
            list.Add(new RebirthPendingChefCraftSignature
            {
                PlayerId = player.entityId,
                ItemType = itemType,
                Seed = seed,
                Quality = quality,
                RecipeName = recipeName ?? string.Empty,
                CreatedUtc = DateTime.UtcNow
            });
        }
    }

    public static void NativeInventoryCommitPrefix(object __instance, World _world)
    {
        if (__instance == null || !IsServerWorld(_world)) return;
        object sender = ReadMember(__instance, "Sender");
        int playerId = ReadInt(sender, "entityId", -1);
        if (playerId < 0) playerId = ReadInt(sender, "EntityId", -1);
        if (playerId < 0) return;
        EntityPlayer player = _world.GetEntity(playerId) as EntityPlayer;
        if (player == null) return;

        List<RebirthPendingChefCraftSignature> pending = GetPendingSnapshot(playerId);
        if (pending.Count == 0) return;
        ItemStack[] incomingTool = player.inventory != null ? player.inventory.ItemGrid.items : null;
        ItemStack[] incomingBag = player.bag != null ? player.bag.ItemGrid.items : null;

        for (int i = 0; i < pending.Count; i++)
        {
            RebirthPendingChefCraftSignature p = pending[i];
            ItemValue candidate = FindChefCandidate(incomingTool, incomingBag, p);
            if (candidate == null) continue;
            if (!StampChefMeal(player, p.RecipeName, candidate, true, "server-inventory-commit")) continue;
            RemovePending(p);
            lock (Gate) ChefCorrections.Enqueue(new RebirthChefCraftCorrection(playerId, candidate));
        }
    }

    private static bool StampChefMeal(EntityPlayer player, string recipeName, ItemValue item, bool authoritative, string source)
    {
        if (player == null || item == null || item.ItemClass == null || player.world == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        if (authoritative && player.world.IsRemote()) return false;
        if (!IsEligibleChefMeal(player, recipeName, item)) return false;

        float cookingSkill = 0f;
        RebirthServiceCraftSkillService.TryGetSkillValue(player, "skill.cooking", out cookingSkill);
        float efficiency = Mathf.Clamp(GetBonusTuning(ProfessionalCookingBonusId, "meal_energy_efficiency_multiplier", 1.25f), 1f, 2f);

        RebirthProvenanceAuthorSnapshot author;
        if (!RebirthProvenanceIdentity.TryCapture(player, out author)) return false;
        RebirthItemProvenanceSnapshot snapshot = new RebirthItemProvenanceSnapshot
        {
            Version = RebirthItemProvenanceAdapter.CurrentVersion,
            Kind = PreparedMealKind,
            SourceId = recipeName ?? string.Empty,
            CreatorStableId = author.StablePlayerId,
            BackgroundId = author.BackgroundId,
            BonusId = ProfessionalCookingBonusId,
            SkillId = "skill.cooking",
            SkillValue = cookingSkill,
            Workmanship = efficiency,
            OriginalMaxUseTimes = Math.Max(0f, item.MaxUseTimes),
            BatchToken = string.Empty
        };
        RebirthItemProvenanceAdapter.Stamp(item, snapshot);
        return true;
    }

    private static bool IsEligibleChefMeal(EntityPlayer player, string recipeName, ItemValue item)
    {
        if (player == null || item == null || item.ItemClass == null || !RebirthBackgroundBonusService.HasBonus(player, ProfessionalCookingBonusId)) return false;
        if (!string.Equals(RebirthServiceCraftSkillService.ClassifyRecipe(recipeName), "skill.cooking", StringComparison.OrdinalIgnoreCase)) return false;
        RebirthConsumableDefinition def;
        return RebirthConsumableResolver.TryResolve(item, out def) && def != null && def.IsFood;
    }

    /// <summary>Returns the preparation multiplier stored at craft time. Unprepared/invalid items are neutral.</summary>
    public static float GetPreparedMealEnergyEfficiency(ItemValue item)
    {
        RebirthItemProvenanceSnapshot p;
        if (!RebirthItemProvenanceAdapter.TryRead(item, out p) || p == null) return 1f;
        if (!string.Equals(p.Kind, PreparedMealKind, StringComparison.OrdinalIgnoreCase) || !string.Equals(p.BonusId, ProfessionalCookingBonusId, StringComparison.OrdinalIgnoreCase)) return 1f;
        return Mathf.Clamp(p.Workmanship, 1f, 2f);
    }

    /// <summary>Persisted Farmer provenance, not the current harvester, owns growth acceleration.</summary>
    public static float GetRapidCultivationGrowthMultiplier(WorldBase world, Vector3i pos)
    {
        if (world == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return 1f;
        TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if (te == null || !string.Equals(te.RebirthGrowerBonusId, RapidCultivationBonusId, StringComparison.OrdinalIgnoreCase)) return 1f;
        return Mathf.Clamp(GetBonusTuning(RapidCultivationBonusId, "growth_rate_multiplier", 1.50f), 1f, 4f);
    }

    public static int GetEffectiveCropGrowthSeconds(WorldBase world, Vector3i pos, int baseSeconds)
    {
        baseSeconds = Math.Max(1, baseSeconds);
        float multiplier = GetRapidCultivationGrowthMultiplier(world, pos);
        return Math.Max(1, Mathf.CeilToInt(baseSeconds / Mathf.Max(1f, multiplier)));
    }

    /// <summary>
    /// Advanced Farming grants its player-grown crop output directly and therefore bypasses native
    /// HarvestCount. Reproduce the already-authored Farming Skill + global Trait yield deltas once,
    /// server-side, with stochastic fractional rounding so the one-crop baseline can scale both up
    /// and down without allowing a zero-yield harvest to be rerolled.
    /// </summary>
    public static int RollAdvancedFarmingHarvestCount(WorldBase world, EntityPlayer player, int baseCount)
    {
        if (baseCount <= 0) return 0;
        if (world == null || player == null || world.IsRemote() || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || player.Buffs == null) return baseCount;
        float farmingDelta = player.Buffs.GetCustomVar(RebirthResourceFieldSkillService.FarmingHarvestCVar);
        float traitDelta = player.Buffs.GetCustomVar(RebirthTraitGameplayModifierService.HarvestCountCVar);
        float multiplier = Mathf.Clamp(1f + farmingDelta + traitDelta, 0.10f, 2.50f);
        float expected = baseCount * multiplier;
        int count = Mathf.FloorToInt(expected);
        float fraction = expected - count;
        if (fraction > 0f && world.GetGameRandom().RandomFloat < fraction) count++;
        return Math.Max(0, count);
    }

    public static float GetWholeAnimalHarvestDelta(EntityPlayer player)
    {
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || !RebirthBackgroundBonusService.HasBonus(player, WholeAnimalBonusId)) return 0f;
        float multiplier = Mathf.Clamp(GetBonusTuning(WholeAnimalBonusId, "carcass_resource_multiplier", 1.50f), 1f, 3f);
        return multiplier - 1f;
    }

    public static float GetBonusTuning(string bonusId, string key, float fallback)
    {
        RebirthBackgroundBonusDefinition d;
        RebirthBackgroundBonusTuningValue v;
        float f;
        return RebirthBackgroundBonusRegistry.TryGet(bonusId, out d) && d != null && d.TryGetTuning(key, out v) && v != null
            && float.TryParse(v.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : fallback;
    }

    public static string BuildDebugReport(EntityPlayer player, Vector3i? cropPos)
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Chunk J Signatures]");
        b.AppendLine("chefEfficiency=" + GetBonusTuning(ProfessionalCookingBonusId, "meal_energy_efficiency_multiplier", 1.25f).ToString("0.###", CultureInfo.InvariantCulture)
            + " farmerGrowth=" + GetBonusTuning(RapidCultivationBonusId, "growth_rate_multiplier", 1.50f).ToString("0.###", CultureInfo.InvariantCulture)
            + " butcherResources=" + GetBonusTuning(WholeAnimalBonusId, "carcass_resource_multiplier", 1.50f).ToString("0.###", CultureInfo.InvariantCulture));
        if (player != null)
        {
            b.AppendLine("player=" + player.entityId
                + " chef=" + RebirthBackgroundBonusService.HasBonus(player, ProfessionalCookingBonusId)
                + " farmer=" + RebirthBackgroundBonusService.HasBonus(player, RapidCultivationBonusId)
                + " butcher=" + RebirthBackgroundBonusService.HasBonus(player, WholeAnimalBonusId)
                + " farmingYieldDelta=" + (player.Buffs != null ? player.Buffs.GetCustomVar(RebirthResourceFieldSkillService.FarmingHarvestCVar).ToString("0.###", CultureInfo.InvariantCulture) : "0")
                + " wholeAnimalDelta=" + GetWholeAnimalHarvestDelta(player).ToString("0.###", CultureInfo.InvariantCulture));
        }
        if (cropPos.HasValue && GameManager.Instance != null && GameManager.Instance.World != null)
        {
            int baseSeconds = AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds();
            b.AppendLine("crop=" + cropPos.Value + " baseStageSeconds=" + baseSeconds
                + " growthMultiplier=" + GetRapidCultivationGrowthMultiplier(GameManager.Instance.World, cropPos.Value).ToString("0.###", CultureInfo.InvariantCulture)
                + " effectiveStageSeconds=" + GetEffectiveCropGrowthSeconds(GameManager.Instance.World, cropPos.Value, baseSeconds));
        }
        return b.ToString().TrimEnd();
    }

    public static string RunVectors()
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Chunk J Vectors]");
        b.AppendLine("Chef 1.25x: 4 Energy/min -> 5 Energy/min while prepared nutrition is intestinal; Nutrition/Energy cost 1.00 -> 0.80.");
        b.AppendLine("Farmer 1.50x: 3600 second stage -> " + Math.Max(1, Mathf.CeilToInt(3600f / 1.5f)) + " seconds; provenance survives stage/reseed.");
        b.AppendLine("Farming +25% on one-crop baseline: expected yield=1.25 via server fractional rounding; zero-yield weakness outcomes still consume/reseed the crop.");
        b.AppendLine("Butcher 1.50x: Whole Animal contributes +0.50 to native butcherHarvest HarvestCount; Skill/Trait channels remain separate.");
        b.AppendLine("Primitive armor: universal policy remains inherited from PC017; advanced Tailoring remains gated.");
        return b.ToString().TrimEnd();
    }

    public static void ApplyClientChefCorrection(int playerId, ItemValue corrected)
    {
        if (corrected == null || GameManager.Instance == null || GameManager.Instance.World == null) return;
        EntityPlayerLocal local = GameManager.Instance.World.GetPrimaryPlayer();
        if (local == null || local.entityId != playerId) return;
        if (!ReplacePlayerItem(local, corrected))
            lock (Gate) ClientPendingCorrections.Add(new RebirthChefCraftCorrection(playerId, corrected));
    }

    private static ItemValue FindChefCandidate(ItemStack[] a, ItemStack[] b, RebirthPendingChefCraftSignature p)
    {
        // Fail closed: the server will only bless the exact client-projected prepared stack from
        // this pending craft. Falling back to an arbitrary same-type stack could relabel an older
        // unprepared meal when inventory stacking/order changes.
        ItemValue v = FindChefCandidate(a, p, true);
        return v ?? FindChefCandidate(b, p, true);
    }

    private static ItemValue FindChefCandidate(ItemStack[] slots, RebirthPendingChefCraftSignature p, bool requireProjectedProvenance)
    {
        if (slots == null || p == null) return null;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemValue v = slots[i] != null ? slots[i].itemValue : null;
            if (v == null || v.type != p.ItemType || v.Seed != p.Seed || v.Quality != p.Quality) continue;
            RebirthItemProvenanceSnapshot prov;
            bool projected = RebirthItemProvenanceAdapter.TryRead(v, out prov) && prov != null
                && string.Equals(prov.Kind, PreparedMealKind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(prov.BonusId, ProfessionalCookingBonusId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(prov.SourceId, p.RecipeName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (projected == requireProjectedProvenance) return v;
        }
        return null;
    }

    private static bool ReplacePlayerItem(EntityPlayer player, ItemValue corrected)
    {
        if (player == null || corrected == null) return false;
        RebirthItemProvenanceSnapshot targetProv;
        RebirthItemProvenanceAdapter.TryRead(corrected, out targetProv);
        ItemStack[] tool = player.inventory != null ? player.inventory.ItemGrid.items : null;
        if (ReplacePlayerItemInSlots(player, tool, true, corrected, targetProv)) return true;
        ItemStack[] bag = player.bag != null ? player.bag.ItemGrid.items : null;
        return ReplacePlayerItemInSlots(player, bag, false, corrected, targetProv);
    }

    private static bool ReplacePlayerItemInSlots(EntityPlayer player, ItemStack[] slots, bool toolbelt, ItemValue corrected, RebirthItemProvenanceSnapshot targetProv)
    {
        if (slots == null) return false;
        int fallback = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemValue v = slots[i] != null ? slots[i].itemValue : null;
            if (v == null || v.type != corrected.type || v.Seed != corrected.Seed || v.Quality != corrected.Quality) continue;
            if (fallback < 0) fallback = i;
            RebirthItemProvenanceSnapshot p;
            if (targetProv != null && RebirthItemProvenanceAdapter.TryRead(v, out p) && p != null
                && string.Equals(p.SourceId, targetProv.SourceId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.BonusId, targetProv.BonusId, StringComparison.OrdinalIgnoreCase))
            {
                fallback = i;
                break;
            }
        }
        if (fallback < 0) return false;
        ItemStack s = slots[fallback];
        ItemValue merged = s.itemValue != null ? s.itemValue.Clone() : null;
        if (merged == null || !RebirthItemProvenanceAdapter.TryApplyOwnedCorrection(merged, corrected)) return false;
        s.itemValue = merged;
        if (toolbelt) player.inventory.SetItem(fallback, s); else player.bag.SetSlot(fallback, s);
        return true;
    }

    private static bool IsServerWorld(WorldBase world)
    {
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return world != null && !world.IsRemote() && c != null && c.IsServer && RebirthSurvivorMode.IsEnabledForCurrentWorld();
    }

    private static object ReadMember(object target, string name)
    {
        if (target == null) return null;
        Type t = target.GetType();
        BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo fi = t.GetField(name, f);
        if (fi != null) return fi.GetValue(target);
        PropertyInfo pi = t.GetProperty(name, f);
        return pi != null ? pi.GetValue(target, null) : null;
    }

    private static int ReadInt(object target, string name, int fallback)
    {
        try { object o = ReadMember(target, name); return o != null ? Convert.ToInt32(o) : fallback; }
        catch { return fallback; }
    }

    private static List<RebirthPendingChefCraftSignature> GetPendingSnapshot(int playerId)
    {
        lock (Gate)
        {
            CleanupLocked();
            List<RebirthPendingChefCraftSignature> list;
            return PendingChef.TryGetValue(playerId, out list) ? new List<RebirthPendingChefCraftSignature>(list) : new List<RebirthPendingChefCraftSignature>();
        }
    }

    private static void RemovePending(RebirthPendingChefCraftSignature p)
    {
        if (p == null) return;
        lock (Gate)
        {
            List<RebirthPendingChefCraftSignature> list;
            if (PendingChef.TryGetValue(p.PlayerId, out list)) list.Remove(p);
        }
    }

    private static void CleanupLocked()
    {
        DateTime now = DateTime.UtcNow;
        if (now < NextCleanupUtc) return;
        NextCleanupUtc = now.AddSeconds(1);
        DateTime cutoff = now.AddSeconds(-PendingSeconds);
        List<int> empty = new List<int>();
        foreach (KeyValuePair<int, List<RebirthPendingChefCraftSignature>> kv in PendingChef)
        {
            kv.Value.RemoveAll(delegate(RebirthPendingChefCraftSignature p) { return p == null || p.CreatedUtc < cutoff; });
            if (kv.Value.Count == 0) empty.Add(kv.Key);
        }
        for (int i = 0; i < empty.Count; i++) PendingChef.Remove(empty[i]);
        ClientPendingCorrections.RemoveAll(delegate(RebirthChefCraftCorrection x) { return x == null || x.CreatedUtc < cutoff; });
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c != null && c.IsServer)
        {
            for (;;)
            {
                RebirthChefCraftCorrection x;
                lock (Gate)
                {
                    if (ChefCorrections.Count == 0) break;
                    x = ChefCorrections.Dequeue();
                }
                EntityPlayer p = GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetEntity(x.PlayerId) as EntityPlayer : null;
                if (p is EntityPlayerLocal) ApplyClientChefCorrection(x.PlayerId, x.Corrected);
                else if (x.Corrected != null) c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthChefCraftCorrection>().Setup(x.PlayerId, x.Corrected), _attachedToEntityId: x.PlayerId);
            }
        }

        lock (Gate)
        {
            for (int i = ClientPendingCorrections.Count - 1; i >= 0; i--)
            {
                RebirthChefCraftCorrection x = ClientPendingCorrections[i];
                EntityPlayerLocal local = GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
                if (local != null && local.entityId == x.PlayerId && ReplacePlayerItem(local, x.Corrected)) ClientPendingCorrections.RemoveAt(i);
            }
            CleanupLocked();
        }
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { ClearRuntime(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { ClearRuntime(); }
    private static void ClearRuntime()
    {
        lock (Gate)
        {
            PendingChef.Clear();
            ChefCorrections.Clear();
            ClientPendingCorrections.Clear();
        }
    }
}
