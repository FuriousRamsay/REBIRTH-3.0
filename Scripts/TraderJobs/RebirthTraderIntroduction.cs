using HarmonyLib;

// Keep the native definition for save compatibility, but never assign the obsolete gate.
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.AddQuest))]
internal static class RebirthTraderIntroductionAssignmentPatch
{
    private static bool Prefix(Quest q)
    {
        return !RebirthSurvivorMode.IsEnabledForCurrentWorld() || q == null ||
            q.ID != "intro_buried_supplies";
    }
}
