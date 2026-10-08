using System;
using System.Collections.Generic;
using HarmonyLib;

#nullable disable

public static class RebirthInfestedJobsRuntimePolicy
{
    private sealed class OriginalQuestPresentation
    {
        public string Name;
        public string GroupName;
        public string SubTitle;
        public string Description;
        public string Offer;
        public string StatementText;
        public string ResponseText;
        public byte DifficultyTier;
    }

    private static readonly Dictionary<QuestClass, OriginalQuestPresentation> Originals =
        new Dictionary<QuestClass, OriginalQuestPresentation>();
    private static RebirthInfestedJobsMode mode = RebirthInfestedJobsMode.Default;

    public static RebirthInfestedJobsMode Mode { get { return mode; } }
    public static bool HideIdentity { get { return mode == RebirthInfestedJobsMode.Hide || mode == RebirthInfestedJobsMode.Surprise; } }
    public static bool Surprise { get { return mode == RebirthInfestedJobsMode.Surprise; } }

    public static void SetMode(RebirthInfestedJobsMode value)
    {
        mode = value < RebirthInfestedJobsMode.Default || value > RebirthInfestedJobsMode.Surprise
            ? RebirthInfestedJobsMode.Default : value;
        ApplyAll();
    }

    public static bool IsInfestedId(string id)
    {
        return !string.IsNullOrEmpty(id) && id.IndexOf("_infested", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static void RegisterAndApply(QuestClass questClass)
    {
        if (questClass == null || !IsInfestedId(questClass.ID)) return;
        if (!Originals.ContainsKey(questClass))
        {
            Originals.Add(questClass, new OriginalQuestPresentation
            {
                Name = questClass.Name,
                GroupName = questClass.GroupName,
                SubTitle = questClass.SubTitle,
                Description = questClass.Description,
                Offer = questClass.Offer,
                StatementText = questClass.StatementText,
                ResponseText = questClass.ResponseText,
                DifficultyTier = questClass.DifficultyTier
            });
        }
        Apply(questClass, Originals[questClass]);
    }

    private static void ApplyAll()
    {
        foreach (KeyValuePair<QuestClass, OriginalQuestPresentation> pair in Originals)
            Apply(pair.Key, pair.Value);
    }

    private static void Apply(QuestClass questClass, OriginalQuestPresentation original)
    {
        if (questClass == null || original == null) return;

        questClass.Name = original.Name;
        questClass.GroupName = original.GroupName;
        questClass.SubTitle = original.SubTitle;
        questClass.Description = original.Description;
        questClass.Offer = original.Offer;
        questClass.StatementText = original.StatementText;
        questClass.ResponseText = original.ResponseText;
        questClass.DifficultyTier = original.DifficultyTier;

        if (!HideIdentity) return;

        int visibleTier = Surprise ? Math.Max(1, original.DifficultyTier - 1) : original.DifficultyTier;
        string visibleQuestKey = "quest_tier" + visibleTier + "_clear";
        questClass.Name = Localization.Get(visibleQuestKey);
        questClass.StatementText = Localization.Get("quest_clear_statement");
        questClass.ResponseText = Localization.Get("quest_clear_response");
        questClass.SubTitle = Localization.Get("quest_clear_subtitle");
        questClass.Description = Localization.Get("quest_clear_description");
        questClass.GroupName = Localization.Get(visibleQuestKey);
        questClass.Offer = Localization.Get(visibleQuestKey + "_offer");
        if (Surprise) questClass.DifficultyTier = (byte)visibleTier;
    }
}

[HarmonyPatch(typeof(QuestClass), nameof(QuestClass.Init))]
public static class RebirthInfestedQuestClassInitPatch
{
    public static void Postfix(QuestClass __instance)
    {
        RebirthInfestedJobsRuntimePolicy.RegisterAndApply(__instance);
    }
}
