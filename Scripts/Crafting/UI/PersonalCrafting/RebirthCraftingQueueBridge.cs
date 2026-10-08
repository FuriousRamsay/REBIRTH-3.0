using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Read-only bridge between the native personal-crafting queue objects and the Rebirth queue presentation.
/// It never creates, reorders, pauses, clears, or completes queued recipes; those operations remain owned by
/// XUiC_CraftingQueue / XUiC_RecipeStack.
/// </summary>
public static class RebirthCraftingQueueBridge
{
    public const int VisibleRows = 4;

    public static XUiC_RecipeStack[] GetRuntimeEntries(XUiC_CraftingQueue queue)
    {
        if (queue == null)
            return Array.Empty<XUiC_RecipeStack>();

        try
        {
            return queue.GetRecipesToCraft() ?? Array.Empty<XUiC_RecipeStack>();
        }
        catch
        {
            return Array.Empty<XUiC_RecipeStack>();
        }
    }

    public static int GetCapacity(XUiC_CraftingQueue queue)
    {
        return GetRuntimeEntries(queue).Length;
    }

    public static int GetActiveCount(XUiC_CraftingQueue queue)
    {
        XUiC_RecipeStack[] entries = GetRuntimeEntries(queue);
        int count = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].HasRecipe())
                count++;
        }
        return count;
    }

    /// <summary>
    /// Native personal crafting executes the highest queue index first. Displaying the reverse native order
    /// therefore places the actively crafting row at the top and preserves the actual execution order below it.
    /// </summary>
    public static int NativeIndexToDisplayOrder(int nativeIndex, int capacity)
    {
        return Math.Max(0, capacity - 1 - nativeIndex);
    }

    public static float GetCurrentItemProgress(XUiC_RecipeStack entry)
    {
        if (entry == null || !entry.HasRecipe() || !entry.IsCrafting)
            return 0f;

        float one = Math.Max(0f, entry.GetOneItemCraftTime());
        if (one <= 0.0001f)
            return 0f;

        float left = Mathf.Clamp(entry.GetRecipeCraftingTimeLeft(), 0f, one);
        return Mathf.Clamp01(1f - left / one);
    }

    public static string GetLocalizedRecipeName(XUiC_RecipeStack entry)
    {
        if (entry == null)
            return Localize("xuiRebirthQueueEmpty", "Empty");

        Recipe recipe = entry.GetRecipe();
        if (recipe == null)
            return Localize("xuiRebirthQueueEmpty", "Empty");

        string key = string.Empty;
        try { key = recipe.GetName() ?? string.Empty; }
        catch { }

        if (!string.IsNullOrWhiteSpace(key))
        {
            string localized = Localization.Get(key);
            if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, key, StringComparison.Ordinal))
                return localized;
        }

        try
        {
            ItemClass itemClass = ItemClass.GetForId(recipe.itemValueType);
            if (itemClass != null)
            {
                string localizedItem = itemClass.GetLocalizedItemName();
                if (!string.IsNullOrWhiteSpace(localizedItem))
                    return localizedItem;
            }
        }
        catch { }

        return string.IsNullOrWhiteSpace(key) ? Localize("xuiRebirthQueueEmpty", "Empty") : key;
    }

    public static string Localize(string key, string fallback)
    {
        string value = Localization.Get(key ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
