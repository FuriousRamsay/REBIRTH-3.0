using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Continuous theoretical knowledge for each practical Survivor Skill.
/// The dictionary key is the authoritative Skill ID, so the theory areas cannot drift away
/// from the practical Skills. Values are per-world/per-character and server authoritative.
///
/// Pass 2 deliberately does not decide literature item effects or recipe discovery. Future study
/// items call TryStudy on the server; individual recipe/procedure ownership remains separate.
/// </summary>
public static class RebirthSkillKnowledgeService
{
    private static readonly object Gate = new object();
    internal static object SyncRoot { get { return Gate; } }

    public static bool TryGetValue(EntityPlayer player, string skillId, out float value)
    {
        value = 0f;
        if (player == null || string.IsNullOrEmpty(skillId)) return false;

        RebirthSkillDefinition definition;
        if (!RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out definition) || definition == null) return false;

        // Authoritative/server path.
        if (player.world != null && !player.world.IsRemote() && RebirthWorldCharacterRepository.IsServerAuthority)
        {
            RebirthWorldCharacterRecord record;
            RebirthSkillKnowledgeRuntimeState state;
            if (RebirthWorldCharacterService.TryGet(player, out record) && record != null && record.Progression != null
                && record.Progression.SkillKnowledge.TryGetValue(skillId, out state) && state != null)
            {
                value = state.Value;
                return true;
            }
        }

        // Owning-client projection path. The owner snapshot never exposes another player's data.
        RebirthSurvivorOwnerScalars scalars;
        return RebirthSurvivorClientState.TryGetOwnerScalars(player, out scalars)
            && scalars.TryGetTheory(skillId, out value);
    }

    public static float GetValue(EntityPlayer player, string skillId)
    {
        float value;
        return TryGetValue(player, skillId, out value) ? value : GetMinimum();
    }

    /// <summary>
    /// Server-authoritative theory award used by future books/manuals and current debug acceptance.
    /// This is intentionally additive and bounded. The caller owns study-time, useful-range,
    /// duplicate-source and prerequisite policy; those content rules are Pass 3 work.
    /// </summary>
    public static bool TryStudy(EntityPlayer player, string skillId, float amount, string sourceKey, out float applied)
    {
        applied = 0f;
        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) || string.IsNullOrEmpty(skillId)) return false;

        RebirthStablePlayerIdentity identity;
        RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record)) return false;

        RebirthSkillDefinition definition;
        if (!RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out definition) || definition == null) return false;

        RebirthSkillKnowledgeRuntimeState state;
        if (!record.Progression.SkillKnowledge.TryGetValue(skillId, out state) || state == null) return false;

        float min = GetMinimum();
        float max = GetMaximum();
        float before;
        float after;
        lock (Gate)
        {
            before = Math.Max(min, Math.Min(max, state.Value));
            float lessonMultiplier=RebirthTeachingService.GetSubjectLearningMultiplier(player,skillId);
            after = Math.Max(min, Math.Min(max, before + amount*lessonMultiplier));
            applied = after - before;
            if (applied <= 0f) return false;
            state.Value = after;
            record.Touch("skill-knowledge-study:" + skillId);
        }
        RebirthWorldCharacterRepository.SaveIfDirty(identity, "skill-knowledge-study:" + skillId);
        RebirthSurvivorNetworkService.SendOwnerState(player, 0L, true, "skill-knowledge-study:" + skillId);
        { if (RebirthLogSettings.SkillKnowledgeLoggingEnabled) RebirthLogSettings.TraceSkillKnowledge("study skill=" + skillId
            + " amount=" + amount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " applied=" + applied.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " before=" + before.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " after=" + after.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " source=" + (sourceKey ?? string.Empty)); }
        return true;
    }


    /// <summary>
    /// Atomic one-time reusable-literature study. The physical item is not consumed. Marker IDs are
    /// persisted server-side in the binary knowledge set but are deliberately excluded from owner
    /// snapshots/UI. A duplicate copy of the same title therefore cannot be farmed by one character,
    /// while the same physical copy can still teach other characters.
    /// </summary>
    /// <summary>
    /// Atomic one-time Theory award used by finite Insights and any future one-shot authored Theory source.
    /// The marker is persisted with the character in the same mutation as the Theory value.
    /// </summary>
    public static bool TryAwardOneTimeTheory(EntityPlayer player, string skillId, float amount, string markerId, string sourceKey, out float applied, out bool alreadyAwarded)
    {
        applied=0f;alreadyAwarded=false;
        if(amount<=0f || float.IsNaN(amount) || float.IsInfinity(amount) || string.IsNullOrEmpty(skillId) || string.IsNullOrEmpty(markerId))return false;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record))return false;
        RebirthSkillDefinition definition;if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId,out definition)||definition==null)return false;
        RebirthSkillKnowledgeRuntimeState state;if(!record.Progression.SkillKnowledge.TryGetValue(skillId,out state)||state==null)return false;
        float before,after;
        lock(Gate)
        {
            if(record.Progression.KnowledgeIds.Contains(markerId)){alreadyAwarded=true;before=after=state.Value;}
            else
            {
            before=Math.Max(GetMinimum(),Math.Min(GetMaximum(),state.Value));
            after=Math.Max(GetMinimum(),Math.Min(GetMaximum(),before+amount));
            applied=after-before;state.Value=after;record.Progression.KnowledgeIds.Add(markerId);record.Touch("theory-one-time:"+skillId+":"+markerId);
            }
        }
        // Queue before the synchronous save: failed persistence keeps the exact pending
        // marker/value for the shared save-gated scheduler, without replaying the event.
        RebirthSkillAwardService.QueueOwnerPublication(player);
        return RebirthWorldCharacterRepository.SaveIfDirty(identity,"theory-one-time:"+skillId);
    }

    public static bool TryStudyLiterature(EntityPlayer player, string skillId, float amount, string markerId, string sourceKey, out float applied, out bool alreadyStudied)
    {
        applied=0f; alreadyStudied=false;
        if(amount<=0f || float.IsNaN(amount) || float.IsInfinity(amount) || string.IsNullOrEmpty(skillId) || string.IsNullOrEmpty(markerId))return false;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record))return false;
        RebirthSkillDefinition definition; if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId,out definition)||definition==null)return false;
        RebirthSkillKnowledgeRuntimeState state; if(!record.Progression.SkillKnowledge.TryGetValue(skillId,out state)||state==null)return false;
        float before,after;
        lock(Gate)
        {
            if(record.Progression.KnowledgeIds.Contains(markerId)){alreadyStudied=true;before=after=state.Value;}
            else
            {
            before=Math.Max(GetMinimum(),Math.Min(GetMaximum(),state.Value));
            float lessonMultiplier=RebirthTeachingService.GetSubjectLearningMultiplier(player,skillId);
            after=Math.Max(GetMinimum(),Math.Min(GetMaximum(),before+amount*lessonMultiplier));
            applied=after-before;
            state.Value=after;
            record.Progression.KnowledgeIds.Add(markerId);
            record.Touch("literature-study:"+skillId+":"+markerId);
            }
        }
        if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"literature-study:"+skillId))
        {
            if(!alreadyStudied)
            {
                lock(Gate)
                {
                    // Undo only this unacknowledged lesson; never overwrite a later state change.
                    if(state.Value==after)
                    {
                        state.Value=before;
                        record.Progression.KnowledgeIds.Remove(markerId);
                        record.Touch("literature-study-save-rollback:"+markerId);
                    }
                }
            }
            applied=0f;
            return false;
        }
        if (!RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"literature-study:"+skillId))
            RebirthSkillAwardService.QueueOwnerPublication(player);
        { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("study item="+(sourceKey??string.Empty)+" skill="+skillId+" applied="+applied.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" marker="+markerId); }
        return true;
    }

    public static Dictionary<string, float> BuildCompleteStartingMap(RebirthBackgroundDefinition background)
    {
        Dictionary<string, float> values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null) return values;

        float min = bundle.Progression.SkillKnowledgeMin;
        float max = bundle.Progression.SkillKnowledgeMax;
        for (int i = 0; i < bundle.Progression.Skills.Count; i++)
        {
            RebirthSkillDefinition skill = bundle.Progression.Skills[i];
            if (skill != null) values[skill.Id] = min;
        }
        if (background != null)
        {
            for (int i = 0; i < background.StartingSkillKnowledge.Count; i++)
            {
                RebirthStartingSkillKnowledgeDefinition entry = background.StartingSkillKnowledge[i];
                if (entry != null && values.ContainsKey(entry.SkillId))
                    values[entry.SkillId] = Math.Max(min, Math.Min(max, entry.Value));
            }
        }
        return values;
    }

    public static float GetMinimum()
    {
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        return bundle != null && bundle.Progression != null ? bundle.Progression.SkillKnowledgeMin : 0f;
    }

    public static float GetMaximum()
    {
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        return bundle != null && bundle.Progression != null ? bundle.Progression.SkillKnowledgeMax : 100f;
    }
}
