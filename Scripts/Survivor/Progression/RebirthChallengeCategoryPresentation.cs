using System;
using System.Collections.Generic;
using Challenges;
using HarmonyLib;

// Present only Rebirth lessons in Rebirth worlds. Native journal pruning below
// removes vanilla instances so hidden objectives cannot complete or notify.
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

// The XML definitions remain available to worlds using native progression. In a
// Rebirth world, native journals must not create or retain their challenge instances:
// hiding the tabs alone leaves the objectives subscribed and able to notify.
[HarmonyPatch]
internal static class RebirthVanillaChallengeCreation
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ChallengeGroupEntry), nameof(ChallengeGroupEntry.CreateChallenges));
        yield return AccessTools.Method(typeof(ChallengeGroupEntry), nameof(ChallengeGroupEntry.AddAnyMissingChallenges));
        // Update() re-enters ResetChallenges for timed/random groups and starts the recreated challenges.
        yield return AccessTools.Method(typeof(ChallengeGroupEntry), nameof(ChallengeGroupEntry.ResetChallenges));
    }

    private static bool Prefix(ChallengeGroupEntry __instance)
        => !RebirthSurvivorMode.IsEnabledForCurrentWorld() ||
           string.Equals(__instance?.ChallengeGroup?.Category, "RebirthLearning", StringComparison.Ordinal);
}

[HarmonyPatch(typeof(ChallengeJournal), nameof(ChallengeJournal.StartChallenges))]
internal static class RebirthVanillaChallengeJournalPruning
{
    private static void Postfix(ChallengeJournal __instance)
    {
        if (__instance == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        foreach (ChallengeGroupEntry entry in new List<ChallengeGroupEntry>(__instance.ChallengeGroups))
        {
            ChallengeGroup group = entry?.ChallengeGroup;
            if (group != null && !string.Equals(group.Category, "RebirthLearning", StringComparison.Ordinal))
                __instance.RemoveChallengesForGroup(group);
        }
        __instance.CompleteChallengesForMinEvents.RemoveAll(challenge =>
            !string.Equals(challenge?.ChallengeGroup?.Category, "RebirthLearning", StringComparison.Ordinal));
        __instance.CompleteChallengeGroupsForMinEvents.RemoveAll(group =>
            !string.Equals(group?.Category, "RebirthLearning", StringComparison.Ordinal));
        XUiM_Quest tracker = LocalPlayerUI.GetUIForPrimaryPlayer()?.xui?.QuestTracker;
        if (tracker?.TrackedChallenge != null &&
            !string.Equals(tracker.TrackedChallenge.ChallengeGroup?.Category, "RebirthLearning", StringComparison.Ordinal))
            tracker.TrackedChallenge = null;
    }
}
