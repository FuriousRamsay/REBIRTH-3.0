using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

/// <summary>
/// Optional-section view model for Expected Outcome. It deliberately contains no probability
/// or random-quality distribution fields because no authoritative mechanic currently supplies them.
/// </summary>
public sealed class RebirthCraftingOutcomeViewModel
{
    public string StatusTitle = string.Empty;
    public string StatusValue = string.Empty;
    public string StatusColor = "210,210,210,255";
    public string QualityTitle = string.Empty;
    public string QualityValue = string.Empty;
    public string XpTitle = string.Empty;
    public string XpValue = string.Empty;
    public string ResultsTitle = string.Empty;
    public string ResultsValue = string.Empty;
    public string Explanation = string.Empty;
    public bool HasRecipe;
    public bool HasSkill;
    public string SkillIcon = string.Empty;
    public string SkillValue = string.Empty;

    public static RebirthCraftingOutcomeViewModel Build(
        XUi xui,
        RebirthCraftingPresentation owner,
        XUiC_RebirthCraftingRecipeDetails details,
        Recipe recipe,
        int batch,
        IList<RebirthCraftingRequirementProjectionService.Requirement> requirements,
        RebirthCraftSkillPreview skillPreview)
    {
        RebirthCraftingOutcomeViewModel vm = new RebirthCraftingOutcomeViewModel();
        vm.StatusTitle = Localize("xuiRebirthCraftStatus", "Craft Status");
        vm.QualityTitle = Localize("xuiRebirthQualityRange", "Quality");
        vm.XpTitle = Localize("xuiRebirthCraftSkillGainTitle", "SKILL GAIN");
        vm.ResultsTitle = Localize("xuiRebirthPossibleResults", "Result");
        if (recipe == null || xui == null || xui.playerUI == null)
            return vm;

        vm.HasRecipe = true;
        int tier = details != null ? Math.Max(1, details.SelectedCraftingTier) : 1;
        EntityPlayer player = xui.playerUI.entityPlayer;
        RebirthCraftOutcomeService.Snapshot source = RebirthCraftOutcomeService.Build(player, recipe, Math.Max(1, batch), tier);
        vm.QualityValue = source.QualityRange;
        string skillId = RebirthServiceCraftSkillService.ClassifyRecipe(recipe);
        vm.HasSkill = !string.IsNullOrEmpty(skillId);
        vm.ResultsValue = BuildResult(xui, recipe, batch);
        if (vm.HasSkill)
        {
            RebirthCraftSkillPreview.Snapshot preview = skillPreview != null
                ? skillPreview.Get(xui.playerUI.entityPlayer, recipe, Math.Max(1, batch), false, OutputCount(xui, recipe)) : null;
            vm.XpValue = RebirthCraftSkillPreview.GainText(preview);
            vm.SkillValue = RebirthCraftSkillPreview.SkillText(player, skillId, preview);
            vm.SkillIcon = RebirthSkillAptitudeTraitFactory.SkillIconKey(skillId);
            vm.Explanation = Localize("xuiRebirthCraftPracticeExplanation", "Skill gain includes your current learning bonuses.");
        }
        else
        {
            vm.XpValue = string.Empty;
            vm.SkillValue = Localize("xuiRebirthCraftNoSkill", "No associated skill");
            vm.Explanation = string.Empty;
        }
        // Quality remains meaningful for tiered output, but is not an unrelated metric column.
        ItemClass outputClass = recipe.GetOutputItemClass();
        if (outputClass != null && outputClass.HasQuality)
            vm.Explanation = string.Format(vm.HasSkill ? Localize("xuiRebirthCraftQualityLearningExplanation", "Quality: {0} • Skill gain includes your current learning bonuses.") : Localize("xuiRebirthCraftQualityExplanation", "Quality: {0}"), source.QualityRange);

        bool materials = true;
        for (int i = 0; requirements != null && i < requirements.Count; i++)
            if (!requirements[i].HasEnough) { materials = false; break; }

        bool unlocked = false;
        try { unlocked = XUiM_Recipes.GetRecipeIsUnlocked(xui, recipe); } catch { }
        bool structural = owner == null || owner.CraftingRequirementsValid(recipe);
        bool commandReady = details?.CommandBridge != null && details.CommandBridge.CanCraft(recipe, tier);

        if (!unlocked)
        {
            vm.StatusValue = Localize("xuiRebirthRecipeLocked", "Recipe locked");
            vm.StatusColor = "228,92,92,255";
        }
        else if (!structural)
        {
            vm.StatusValue = owner?.Controller.CraftingRequirementsInvalidMessage(recipe);
            if(string.IsNullOrWhiteSpace(vm.StatusValue))vm.StatusValue = Localize("xuiRebirthCraftRequirementsBlocked", "Crafting requirement blocked");
            vm.StatusColor = "228,92,92,255";
        }
        else if (!materials)
        {
            vm.StatusValue = Localize("xuiRebirthMissingMaterials", "Missing materials");
            vm.StatusColor = "228,92,92,255";
        }
        else if (QueueFull(owner))
        {
            vm.StatusValue = Localize("xuiRebirthStationQueueFull", "Crafting queue is full");
            vm.StatusColor = "232,192,100,255";
        }
        else if (!commandReady)
        {
            string reason = (owner?.Controller as XUiC_RebirthCookingStation)?.GetChildByType<XUiC_RebirthCookingWorkspace>()?.SharedCraftBlocker(recipe,batch);
            if(string.IsNullOrEmpty(reason))reason=ResolveCapabilityReason(player, recipe);
            if(string.IsNullOrEmpty(reason))reason=details?.CommandBridge?.LastBlockReason;
            vm.StatusValue = string.IsNullOrWhiteSpace(reason) ? Localize("xuiRebirthCraftTemporarilyBlocked", "Temporarily blocked") : reason;
            vm.StatusColor = "232,192,100,255";
        }
        else
        {
            vm.StatusValue = Localize("xuiRebirthReadyToCraft", "Ready to craft");
            vm.StatusColor = "112,196,126,255";
        }
        return vm;
    }

    private static bool QueueFull(RebirthCraftingPresentation owner)
    {
        var queue=owner?.GetChildByType<XUiC_RebirthCraftingQueue>();
        return queue!=null&&queue.RuntimeCapacity>0&&queue.ActiveCount>=queue.RuntimeCapacity;
    }

    private static string BuildResult(XUi xui, Recipe recipe, int batch)
    {
        ItemClass itemClass = null;
        try { itemClass = recipe.GetOutputItemClass(); } catch { }
        string name = itemClass != null ? itemClass.GetLocalizedItemName() : SafeLocalized(recipe.GetName());
        long total = (long)OutputCount(xui, recipe) * Math.Max(1, batch);
        return (string.IsNullOrWhiteSpace(name) ? "—" : name) + "  x" + total.ToString(CultureInfo.InvariantCulture);
    }

    private static int OutputCount(XUi xui, Recipe recipe)
    {
        try { return Math.Max(1, XUiM_Recipes.GetRecipeCraftOutputCount(xui, recipe)); }
        catch { return Math.Max(1, recipe.count); }
    }

    private static string ResolveCapabilityReason(EntityPlayer player, Recipe recipe)
    {
        if (player == null || recipe == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return string.Empty;
        string name;
        try { name = recipe.GetName() ?? string.Empty; } catch { return string.Empty; }
        if (name.Length == 0) return string.Empty;
        try
        {
            RebirthCapabilityEvaluation evaluation = RebirthCapabilityService.EvaluateRecipe(player, name);
            if (evaluation != null && !evaluation.IsAllowed) return evaluation.FirstMissingReason;
        }
        catch { }
        return string.Empty;
    }

    private static string SafeLocalized(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        string value = Localization.Get(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private static string Localize(string key, string fallback)
    {
        string value = Localization.Get(key ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

/// <summary>
/// One bounded preview cache per open outcome panel. Remote players ask the authoritative server:
/// teaching/support multipliers must not be guessed from incomplete client owner snapshots.
/// This endpoint has no award path, inventory writes or queue mutation.
/// </summary>
public sealed class RebirthCraftSkillPreview
{
    public sealed class Snapshot
    {
        public string SkillId;
        public float CurrentValue, Gain;
    }
    private Snapshot snapshot;
    private World world;
    private int playerId = -1;
    private string key = string.Empty;
    private int generation;
    private bool pending;
    private float nextRequest, expires;

    public Snapshot Get(EntityPlayerLocal player, Recipe recipe, int batch, bool preparedBook, int outputCount = 0)
    {
        if (player == null || player.world == null || recipe == null) return null;
        string name = recipe.GetName();
        string skillId = RebirthServiceCraftSkillService.ClassifyRecipe(recipe);
        if (string.IsNullOrEmpty(skillId)) return null;
        batch = Math.Max(1, Math.Min(9999, batch));
        int outputs = Math.Max(1, Math.Min(32767, outputCount > 0 ? outputCount : recipe.count));
        string inputDescription=RebirthCraftTrainingRules.PreviewDescription(recipe);
        string requestedKey = name + "|" + outputs + "|" + batch + "|" + preparedBook + "|" + inputDescription;
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (!ReferenceEquals(world, player.world) || playerId != player.entityId || key != requestedKey)
        {
            world = player.world; playerId = player.entityId; key = requestedKey;
            snapshot = null; pending = false; nextRequest = expires = 0f; unchecked { generation++; }
        }
        if (!pending && now >= nextRequest)
        {
            pending = true;
            nextRequest = now + 1f;
            int requestGeneration = generation;
            RebirthCookingSessionService.Request(player, "skillPreview", name, preparedBook ? "prepared" : "",
                outputs.ToString(CultureInfo.InvariantCulture), item: RebirthCraftTrainingRules.PreviewCarrier(recipe,inputDescription), count: batch, reply: response =>
                {
                    if (generation != requestGeneration || !ReferenceEquals(world, player.world) ||
                        !ReferenceEquals(world, GameManager.Instance != null ? GameManager.Instance.World : null)) return;
                    pending = false;
                    string[] parts = (response ?? string.Empty).Split('|');
                    float current, gain;
                    if (parts.Length == 4 && parts[0] == "skill34" && parts[1] == skillId &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out current) &&
                        float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out gain) &&
                        !float.IsNaN(current) && !float.IsInfinity(current) && !float.IsNaN(gain) && !float.IsInfinity(gain))
                    {
                        snapshot = new Snapshot { SkillId = skillId, CurrentValue = current, Gain = Math.Max(0f, gain) };
                        expires = UnityEngine.Time.realtimeSinceStartup + 3f;
                    }
                    else { snapshot = null; nextRequest = UnityEngine.Time.realtimeSinceStartup + 2f; }
                });
        }
        return snapshot != null && now <= expires ? snapshot : null;
    }

    public void Reset()
    {
        snapshot = null; world = null; key = string.Empty; playerId = -1; pending = false;
        nextRequest = expires = 0f; unchecked { generation++; }
    }

    public static string ServerReply(EntityPlayer player, string recipeName, int batches, string outputText, bool preparedBook, ItemValue previewInputs=null)
    {
        if (player == null || player.world == null || player.world.IsRemote() || string.IsNullOrEmpty(recipeName) ||
            recipeName.Length > 160 || batches < 1 || batches > 9999) return string.Empty;
        Recipe recipe = CraftingManager.GetRecipe(recipeName);
        bool improvised = recipeName.StartsWith("rebirthImprovised", StringComparison.Ordinal);
        if (recipe == null && (!improvised || ItemClass.GetItem(recipeName).IsEmpty())) return string.Empty;
        int outputs;
        if (!int.TryParse(outputText, NumberStyles.Integer, CultureInfo.InvariantCulture, out outputs) || outputs < 1 || outputs > 32767)
            return string.Empty;
        // Output count is preview-only input (improvised dishes can vary); it can never grant XP.
        Recipe previewRecipe=RebirthCraftTrainingRules.DecodePreview(recipeName,previewInputs)??recipe;
        string skillId = RebirthServiceCraftSkillService.ClassifyRecipe(previewRecipe);
        if (string.IsNullOrEmpty(skillId)) return string.Empty;
        float multiplier = 1f;
        if (preparedBook && (skillId == "skill.cooking" || skillId == "skill.drink_preparation"))
        {
            var ready = RebirthCookingSessionService.Ready(player, recipeName);
            if (!string.IsNullOrEmpty(ready?.Book)) multiplier = RebirthCookingRules.BookMultiplier;
        }
        var model=RebirthCraftTrainingRules.BuildModel(player,previewRecipe,skillId);
        float current, gain;
        if (!RebirthSkillAwardService.TryPreviewCraft(player,skillId,model,batches,multiplier,out current,out gain)) return string.Empty;
        return "skill34|" + skillId + "|" + current.ToString("R", CultureInfo.InvariantCulture)
            + "|" + gain.ToString("R", CultureInfo.InvariantCulture);
    }

    public static string GainText(Snapshot value)
    {
        if (value == null) return "…";
        if (value.Gain > 0f && value.Gain < 0.0005f) return "<0.001";
        string format = value.Gain > 0f && value.Gain < 0.1f ? "+0.000;−0.000;0.000" : "+0.00;−0.00;0.00";
        return value.Gain.ToString(format, CultureInfo.InvariantCulture);
    }

    public static string SkillText(EntityPlayer player, string skillId, Snapshot value)
    {
        string name = RebirthSkillDisplayNames.Get(skillId);
        float current, progress;
        if (value != null) current = value.CurrentValue;
        else if (RebirthServiceCraftSkillService.TryGetPracticalSkillProgress(player, skillId, out current, out progress)) current += progress;
        else return name + " —";
        return name + " " + current.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
