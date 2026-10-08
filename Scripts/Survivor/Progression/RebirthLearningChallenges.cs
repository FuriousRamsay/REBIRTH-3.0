using System;
using System.Collections.Generic;
using UnityEngine;

// Native ChallengeStatAwarded objectives keep their own saved completion state.
// Only the current owner's server-accepted projection supplies learning evidence;
// no award code, skill values or network payload formats are changed here.
public static class RebirthLearningChallenges
{
    private static readonly Dictionary<string, float> Skills = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> Theory = new Dictionary<string, float>();
    private static readonly HashSet<string> Knowledge = new HashSet<string>();
    private static readonly HashSet<string> ReadingCredits = new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> PendingReading = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string,int> PendingCredits=new Dictionary<string,int>(StringComparer.Ordinal);
    private static string readingCreationId;
    private static bool initialized;
    private static bool flushingCredits;
    private static float nextDigestCheck;
    private static float previousEnergy = -1f;

    public static void Reset()
    {
        PendingCredits.Clear(); Skills.Clear(); Theory.Clear(); Knowledge.Clear(); ReadingCredits.Clear(); PendingReading.Clear(); readingCreationId = null;
        initialized = false; nextDigestCheck = 0f; previousEnergy = -1f;
    }

    private static void Credit(string key)
    {
        int count;PendingCredits.TryGetValue(key,out count);
        PendingCredits[key]=count<int.MaxValue?count+1:count;
    }
    private static void FlushPendingCredits()
    {
        var manager=QuestEventManager.Current;
        if(flushingCredits||manager==null||PendingCredits.Count==0)return;
        string creation=readingCreationId;
        flushingCredits=true;
        try
        {
            foreach(var pair in new List<KeyValuePair<string,int>>(PendingCredits))
            {
                if(creation!=readingCreationId||!ReferenceEquals(manager,QuestEventManager.Current))break;
                if(!PendingCredits.TryGetValue(pair.Key,out var count)||count!=pair.Value)continue;
                // Never replay an uncertain callback that may already have credited objectives.
                PendingCredits.Remove(pair.Key);
                manager.ChallengeAwardCredited(pair.Key,count);
            }
        }
        finally {flushingCredits=false;}
    }

    public static void Observe(RebirthSurvivorOwnerStateSnapshot value)
    {
        if (value == null || !value.RebirthModeEnabled || !value.HasCharacter || !value.DefinitionsCompatible || string.IsNullOrWhiteSpace(value.CreationId))
        { Reset(); return; }
        // Starter values on another Survivor establish a baseline, never learning evidence.
        if(!string.Equals(readingCreationId,value.CreationId,StringComparison.Ordinal))Reset();
        readingCreationId=value.CreationId;
        FlushPendingCredits();
        if(readingCreationId!=value.CreationId)return;
        foreach (RebirthSurvivorOwnerSkillSnapshot skill in value.Skills)
        {
            float old;
            float current = skill.Value + skill.Progress;
            if (initialized && Skills.TryGetValue(skill.Id, out old) && current > old + 0.000001f)
            {
                Credit("rebirth.practice");
                Credit("rebirth.practice." + skill.Id);
            }
            Skills[skill.Id] = current;
        }
        foreach (RebirthSurvivorOwnerSkillKnowledgeSnapshot theory in value.SkillKnowledge)
        {
            float old;
            if (initialized && Theory.TryGetValue(theory.Id, out old) && theory.Value > old + 0.000001f)
            {
                Credit("rebirth.theory");
                Credit("rebirth.theory." + theory.Id);
            }
            Theory[theory.Id] = theory.Value;
        }
        foreach (string id in value.KnowledgeIds)
            if (Knowledge.Add(id) && initialized && !RebirthLiteratureService.IsInternalReadMarker(id))
                Credit("rebirth.knowledge");
        // Existing saved reading receipts also qualify after reconnect; no new study/XP is awarded.
        PendingReading.Clear();
        foreach(string id in value.KnowledgeIds)
            if(RebirthLearningReadEvidence.IsTutorialMarker(id))PendingReading.Add(id);
        RebirthLearningReadEvidence.CreditVerified(PendingReading,ReadingCredits,TryCreditReading);
        initialized = true;
        FlushPendingCredits();
    }

    private static bool TryCreditReading(string stat)
    {
        if(QuestEventManager.Current==null)return false;
        string name;
        switch(stat)
        {
            case "rebirth.study.cooking_principles": name="rebirthLessonStudyTheory"; break;
            case "rebirth.study.first_aid_recipe": name="rebirthLessonReadRecipe"; break;
            case "rebirth.study.repair_manual": name="rebirthLessonStudyManual"; break;
            default: return false;
        }
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        var journal=player?.challengeJournal;
        Challenges.Challenge lesson;
        if(journal==null||!journal.ChallengeDictionary.TryGetValue(name,out lesson))return false;
        foreach(var objective in lesson.ObjectiveList)
        {
            var reading=objective as Challenges.ChallengeObjectiveChallengeStatAwarded;
            if(reading==null||!string.Equals(reading.challengeStat,stat,StringComparison.Ordinal))continue;
            if(reading.Complete||reading.Current>=reading.MaxCount)return true;
            Credit(stat);
            FlushPendingCredits();
            return reading.Complete||reading.Current>=reading.MaxCount;
        }
        return false;
    }
    public static void Tick()
    {
        if(!initialized)return;
        var local=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(local==null||!RebirthSurvivorRequestScope.Matches(readingCreationId,
            RebirthSurvivorClientState.GetProjectedCreationId(local)))
        {Reset();return;}
        FlushPendingCredits();
        if(Time.realtimeSinceStartup < nextDigestCheck)return;
        nextDigestCheck = Time.realtimeSinceStartup + 1f;
        RebirthLearningReadEvidence.CreditVerified(PendingReading,ReadingCredits,TryCreditReading);
        RebirthMetabolismSnapshot value;
        if (!RebirthMetabolismClientState.TryGet(out value)||
            !RebirthSurvivorRequestScope.Matches(readingCreationId,value.CreationId))
        {previousEnergy=-1f;return;}
        if (value.NutritionGainPointsPerRealMinute > 0f) Credit("rebirth.digest.food");
        if (value.HydrationGainPointsPerRealMinute > 0f) Credit("rebirth.digest.water");
        if (value.AutoSipEnabled && value.HydrationSlotVolumeMl > 0f) Credit("rebirth.hydration.slot");
        if (value.GastricFluidTransferMlPerRealMinute > 0f || value.GastricSolidTransferMlPerRealMinute > 0f)
            Credit("rebirth.digest.transfer");
        if (previousEnergy >= 0f && value.Energy > previousEnergy + 0.001f && value.EnergyRecoveryPerRealMinute > 0f)
            Credit("rebirth.energy.recovery");
        previousEnergy = value.Energy;
        FlushPendingCredits();
    }
}
