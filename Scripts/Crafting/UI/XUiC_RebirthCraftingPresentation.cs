using System;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Native CraftingInfoWindow with additional read-only bindings for the approved REBIRTH layout.
/// Native recipe selection, actions, count controls, requirements, quality selection and craft time remain owned by the base controller.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingInfoWindow : XUiC_CraftingInfoWindow
{
    private static readonly System.Reflection.FieldInfo RecipeField = AccessTools.Field(typeof(XUiC_CraftingInfoWindow), "recipe");
    private XUiController viewKnowledgeButton;

    public override void Init()
    {
        base.Init();
        viewKnowledgeButton = GetChildById("btnRebirthCraftKnowledge");
        if (viewKnowledgeButton != null) viewKnowledgeButton.OnPress += ViewKnowledge_OnPress;
    }

    [PublicizedFrom(EAccessModifier.Protected)]
    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        RebirthCraftOutcomeService.Snapshot snapshot;
        switch (bindingName)
        {
            case "itemdescription": value = RebirthRecipeDescriptionText.GetForRecipe(GetRecipe()); return true;
            case "rebirthknowledge": snapshot = BuildSnapshot(); value = snapshot.Knowledge; return true;
            case "rebirthknowledgecolor": snapshot = BuildSnapshot(); value = snapshot.KnowledgeColor; return true;
            case "rebirthtimelabel": snapshot = BuildSnapshot(); value = snapshot.TimeLabel; return true;
            case "rebirthknowledgefocusavailable": value = !string.IsNullOrEmpty(GetKnowledgeFocusId(GetRecipe())) ? "true" : "false"; return true;
            default: return base.GetBindingValueInternal(ref value, bindingName);
        }
    }

    private void ViewKnowledge_OnPress(XUiController sender, int mouseButton)
    {
        Recipe selected = GetRecipe();
        string focusId = GetKnowledgeFocusId(selected);
        if (string.IsNullOrEmpty(focusId) || xui == null) return;

        string error;
        if (!RebirthProgressionExplorerUiService.Open(xui, focusId, RebirthProgressionExplorerMode.LiveCharacter, out error))
            Log.Error("[REBIRTH Crafting UI] Could not open Knowledge in Progression Explorer. focus=" + focusId + " error=" + error);
    }

    private Recipe GetRecipe()
    {
        try { return RecipeField != null ? RecipeField.GetValue(this) as Recipe : null; }
        catch { return null; }
    }

    private RebirthCraftOutcomeService.Snapshot BuildSnapshot()
    {
        Recipe selected = GetRecipe();
        XUiC_RecipeCraftCount count = GetChildByType<XUiC_RecipeCraftCount>();
        int batches = count != null ? Math.Max(1, count.Count) : 1;
        EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        return RebirthCraftOutcomeService.Build(player, selected, batches, SelectedCraftingTier);
    }

    private static string GetKnowledgeFocusId(Recipe selected)
    {
        if (selected == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return string.Empty;
        string name;
        try { name = selected.GetName() ?? string.Empty; } catch { return string.Empty; }
        if (name.Length == 0) return string.Empty;

        RebirthCapabilityDefinition definition;
        if (!RebirthCapabilityRegistry.TryGetRecipe(name, out definition) || definition == null) return string.Empty;
        return FindFirstKnowledge(definition.Requirement);
    }

    private static string FindFirstKnowledge(RebirthCapabilityRequirement requirement)
    {
        if (requirement == null) return string.Empty;
        if (string.Equals(requirement.Kind, RebirthCapabilityKinds.Knowledge, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(requirement.Id))
            return requirement.Id;
        if (requirement.Children == null) return string.Empty;
        for (int i = 0; i < requirement.Children.Count; i++)
        {
            string value = FindFirstKnowledge(requirement.Children[i]);
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return string.Empty;
    }
}

/// <summary>
/// Lower-left Expected Outcome panel. This is presentation-only: it listens to the native
/// recipe/count/tier controls and cannot queue, consume ingredients or alter a crafted output.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftOutcomePanel : XUiController
{
    private Recipe recipe;
    private XUiC_RecipeList recipeList;
    private XUiC_RecipeCraftCount craftCount;
    private XUiC_CraftingInfoWindow info;
    private int lastCount = -1;
    private int lastTier = -1;
    private float refreshTimer;
    private bool subscribed;
    private RebirthCraftOutcomeService.Snapshot cachedOutcome;
    private EntityPlayer outcomePlayer;
    private Recipe outcomeRecipe;
    private int outcomeFrame = -1, outcomeCount, outcomeTier;

    public override void Init()
    {
        base.Init();
        recipeList = windowGroup != null && windowGroup.Controller != null ? windowGroup.Controller.GetChildByType<XUiC_RecipeList>() : null;
        craftCount = windowGroup != null && windowGroup.Controller != null ? windowGroup.Controller.GetChildByType<XUiC_RecipeCraftCount>() : null;
        info = windowGroup != null && windowGroup.Controller != null ? windowGroup.Controller.GetChildByType<XUiC_CraftingInfoWindow>() : null;
        RefreshBindings();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (!subscribed)
        {
            if (recipeList != null) recipeList.RecipeChanged += HandleRecipeChanged;
            if (craftCount != null) craftCount.OnCountChanged += HandleCountChanged;
            subscribed = true;
        }
        recipe = recipeList != null ? recipeList.CurrentRecipe : null;
        lastCount = -1;
        lastTier = -1;
        RefreshBindings();
    }

    public override void OnClose()
    {
        if (subscribed)
        {
            if (recipeList != null) recipeList.RecipeChanged -= HandleRecipeChanged;
            if (craftCount != null) craftCount.OnCountChanged -= HandleCountChanged;
            subscribed = false;
        }
        base.OnClose();
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        if (!IsOpen) return;
        refreshTimer += Math.Max(0f, _dt);
        if (refreshTimer < 0.20f) return;
        refreshTimer = 0f;
        int count = craftCount != null ? craftCount.Count : 1;
        int tier = info != null ? info.SelectedCraftingTier : 1;
        Recipe current = recipeList != null ? recipeList.CurrentRecipe : recipe;
        if (current != recipe)
        {
            recipe = current;
            lastCount = -1;
            lastTier = -1;
        }
        if (count == lastCount && tier == lastTier) return;
        lastCount = count;
        lastTier = tier;
        IsDirty = true;
        RefreshBindings();
    }

    private void HandleRecipeChanged(Recipe selected, XUiC_RecipeEntry entry)
    {
        recipe = selected;
        lastCount = -1;
        lastTier = -1;
        IsDirty = true;
        RefreshBindings();
    }

    private void HandleCountChanged(XUiController sender, OnCountChangedEventArgs args)
    {
        lastCount = -1;
        IsDirty = true;
        RefreshBindings();
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if (bindingName != "rebirthoutcomesuccess" && bindingName != "rebirthoutcomequality"
            && bindingName != "rebirthoutcomexp" && bindingName != "rebirthoutcomeresults"
            && bindingName != "rebirthoutcomehigh" && bindingName != "rebirthoutcomenormal"
            && bindingName != "rebirthoutcomelow")
            return base.GetBindingValueInternal(ref value, bindingName);
        RebirthCraftOutcomeService.Snapshot s = BuildSnapshot();
        switch (bindingName)
        {
            case "rebirthoutcomesuccess": value = s.SuccessChance; return true;
            case "rebirthoutcomequality": value = s.QualityRange; return true;
            case "rebirthoutcomexp": value = s.CraftingXp; return true;
            case "rebirthoutcomeresults": value = s.PossibleResults; return true;
            case "rebirthoutcomehigh": value = s.HighQuality; return true;
            case "rebirthoutcomenormal": value = s.NormalQuality; return true;
            case "rebirthoutcomelow": value = s.LowQuality; return true;
            default: return base.GetBindingValueInternal(ref value, bindingName);
        }
    }

    private RebirthCraftOutcomeService.Snapshot BuildSnapshot()
    {
        EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        int count = craftCount != null ? Math.Max(1, craftCount.Count) : 1;
        int tier = info != null ? Math.Max(1, info.SelectedCraftingTier) : 1;
        int frame = Time.frameCount;
        if (cachedOutcome == null || outcomeFrame != frame || outcomePlayer != player
            || outcomeRecipe != recipe || outcomeCount != count || outcomeTier != tier)
        {
            cachedOutcome = RebirthCraftOutcomeService.Build(player, recipe, count, tier);
            outcomeFrame = frame; outcomePlayer = player; outcomeRecipe = recipe;
            outcomeCount = count; outcomeTier = tier;
        }
        return cachedOutcome;
    }
}

