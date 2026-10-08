using System;
using System.Collections.Generic;
using Challenges;
using HarmonyLib;

// Filter only the UI argument: native definitions and saved challenge progress remain intact.
[HarmonyPatch(typeof(XUiC_CategoryList), nameof(XUiC_CategoryList.SetupCategoriesBasedOnChallengeCategories))]
public static class RebirthChallengeCategoryPresentation
{
    private const string LearningCategory = "RebirthLearning";

    [HarmonyPrefix]
    public static void BeforeSetup(ref List<ChallengeCategory> _items)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || _items == null) return;
        _items = _items.FindAll(category => category != null &&
            string.Equals(category.Name, LearningCategory, StringComparison.Ordinal));
    }

    [HarmonyPostfix]
    public static void AfterSetup(XUiC_CategoryList __instance)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || __instance == null) return;
        // A previously selected vanilla tab can survive a category rebuild. Select the
        // rebuilt REBIRTH entry and notify the native group list to refresh its lessons.
        if (__instance.CategoryButtons.Count > 0 &&
            string.Equals(__instance.CategoryButtons[0].CategoryName, LearningCategory, StringComparison.Ordinal))
        {
            __instance.SetCategoryToFirst();
        }
        else
        {
            __instance.CurrentCategory = null;
        }
    }
}
