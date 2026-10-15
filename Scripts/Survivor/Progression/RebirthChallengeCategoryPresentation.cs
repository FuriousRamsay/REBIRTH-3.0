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
[HarmonyPatch(typeof(XUiC_QuestTrackerWindow), "set_CurrentChallenge")]
internal static class RebirthVanillaChallengeTrackerPresentation
{
    private static void Prefix(ref Challenge value)
    {
        if(RebirthSurvivorMode.IsEnabledForCurrentWorld()&&value!=null&&
            !string.Equals(value.ChallengeClass?.ChallengeGroup?.Category,"RebirthLearning",StringComparison.Ordinal))value=null;
    }
}

// Category filtering alone does not stop the native objective subscriptions,
// completion tooltip/sound or automatic tracker advancement.
[HarmonyPatch]
internal static class RebirthVanillaChallengeLifecycle
{
    internal static bool Suppress(Challenge challenge)
        =>challenge?.Owner?.Player!=null&&RebirthSurvivorMode.IsEnabledForCurrentWorld()
        &&!string.Equals(challenge.ChallengeClass?.ChallengeGroup?.Category,"RebirthLearning",StringComparison.Ordinal);
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Challenge),nameof(Challenge.StartChallenge));
        yield return AccessTools.Method(typeof(Challenge),nameof(Challenge.HandleComplete));
        yield return AccessTools.Method(typeof(Challenge),nameof(Challenge.Redeem));
        yield return AccessTools.Method(typeof(Challenge),nameof(Challenge.HandleTrackingStarted));
    }
    private static bool Prefix(Challenge __instance)
    {
        if(!Suppress(__instance))return true;
        __instance.IsTracked=false;
        return false;
    }
}
