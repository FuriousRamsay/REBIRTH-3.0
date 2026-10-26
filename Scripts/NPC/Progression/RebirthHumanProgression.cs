using System;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// A human's shared progression is an optional, checksummed part of the existing NPC aggregate.
// It is never stored under the owner's player identity or inferred from their skills.
internal static class RebirthHumanProgression
{
    private const string Element = "sharedHumanProgression";
    private static World scannedWorld;
    private static float nextScan;
    private static readonly HashSet<RebirthNpcStableId> Initialized = new HashSet<RebirthNpcStableId>();
    private static bool b13ProtectionInstalled;
    private static int b13Attempts;
    private static float b13NextAttempt;
    internal static void Tick()
    {
        // Codex helper (primary checkout, not merged here): idempotent registration of the lifetime B13 owner-work receipt guard.
        // Runs on the first NPC progression tick; P3 may also call it from the player installer for an earlier guarantee.
        // Throttled: at most 5 attempts, 10 s apart, main thread only; one log on success and one on giving up.
        if(!b13ProtectionInstalled && b13Attempts < 5 && Time.unscaledTime >= b13NextAttempt && ThreadManager.IsMainThread())
        {
            b13NextAttempt = Time.unscaledTime + 10f; b13Attempts++;
            try { RebirthB13OwnerWorkAwards.EnsureReceiptProtection(); b13ProtectionInstalled = true; Log.Out("[REBIRTH B13] receipt protection installed"); }
            catch(Exception error) { if(b13Attempts >= 5) Log.Warning("[REBIRTH B13] receipt protection not installed, giving up: " + error.Message); }
        }
        var world = GameManager.Instance?.World;
        if(world == null || world.IsRemote() || RebirthNpcPersistenceCoordinator.IsRecoveryInstalling || !RebirthSurvivorDefinitionRegistry.IsReady) return;
        RebirthNpcWorkNativeTransfer.Tick();
        RebirthNpcWorkNativeCredit.Tick();
        RebirthNpcFarmOriginal.Tick();
        RebirthHumanCombatPractice.Tick();
        RebirthP1SharedWorkAward.Tick();
        RebirthHumanRecipeReading.Tick();
        RebirthHumanWeaponPassives.Tick();
        RebirthHumanStartingLoadout.RetryTick();
        RebirthG01NpcBackgroundEffects.TickBagReconcile();
        RebirthHumanGroupRoles.Tick();
        if(!ReferenceEquals(world, scannedWorld)) { scannedWorld = world; Initialized.Clear(); nextScan = 0; }
        if(Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1;
        int remaining = 8;
        foreach(var runtime in RebirthNpcRuntimeRegistry.GetSnapshot())
        {
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId, out var entityId)) continue;
            var actor = world.GetEntity(entityId) as EntityRebirthHumanoidNPC;
            if(actor == null || runtime.PreparedRestorationPending || actor.IsDead() || Initialized.Contains(runtime.StableId)) continue;
            try
            {
                if(TryGet(runtime.StableId,out _)) { Initialized.Add(runtime.StableId); RebirthHumanTeaching.Recover(runtime.StableId); RebirthP1SharedWorkAward.Recover(runtime.StableId); }
            }
            catch(Exception error) { nextScan=Time.unscaledTime+10;Log.Warning("[REBIRTH Human Progression] retained: "+error.Message);break; }
            if(--remaining == 0) break;
        }
    }
    internal static bool TryGet(RebirthNpcStableId id, out RebirthWorldCharacterRecord character)
    {
        character = null;
        var world = GameManager.Instance?.World;
        if(id.IsEmpty || world == null || world.IsRemote() || !ThreadManager.IsMainThread() || RebirthNpcPersistenceCoordinator.IsRecoveryInstalling) return false;
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out var person))
        {
            // Capture creates the canonical person row; no synthetic replacement identity.
            RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
            if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out person)) return false;
        }
        if(person.Identity==null||person.Identity.Category != "Survivor" &&
           person.Identity.Category != "Bandit" &&person.Identity.Category != "SpecialHumanoid") return false;
        XElement extension = Find(person);
        if(extension == null)
        {
            if(!Create(id, person, out character)) return false;
            var encoded = Encode(id, character);
            if(!RebirthNpcAggregatePersistenceStore.TryMutateExisting(id, person.AggregateRevision, candidate =>
            {
                if(Find(candidate) != null) return false;
                candidate.UnknownOptionalElements = (candidate.UnknownOptionalElements ?? new string[0]).Concat(new[] { encoded.ToString(SaveOptions.DisableFormatting) }).ToArray();
                return true;
            })) { character = null; return false; }
            RebirthNpcAggregatePersistenceStore.Save();
            if(!SavedProgressionMatches(id,encoded)) { character = null; return false; }
            RebirthHumanProgressionProjectionService.Publish(id);
            return TryGet(id, out character);
        }
        if((int?)extension.Attribute("schema") != 1 || (string)extension.Attribute("npc") != id.ToString()) throw new InvalidDataException("Invalid human progression binding");
        string owner = "npc:" + id;
        if(!RebirthWorldCharacterRepository.ReadNpcOrigin(extension.Element("origin"), out var origin, out var error) ||
           !RebirthWorldCharacterRepository.ReadNpcProgression(extension.Element("progression"), owner, out var state, out error))
            throw new InvalidDataException("Invalid human progression: " + error);
        character = new RebirthWorldCharacterRecord(1, owner, owner, person.AggregateRevision, origin.CommittedAtUtc, DateTime.UtcNow, origin, state, null, null);
        if((string)extension.Attribute("legacyMapped") != "1")
        {
            // Preserve the old progression store for compatibility, but carry
            // directly equivalent earned mastery into the shared skill once.
            MergeDirectLegacySkills(id, state);
            if(!Save(id, character)) { character = null; return false; }
            return TryGet(id, out character);
        }
        return true;
    }
    // Read-only: never creates, merges, saves or publishes. Only returns an already migrated row (legacyMapped=1);
    // anything else (absent, unmigrated, duplicate, corrupt) is false so callers fall back without side effects.
    internal static bool TryPeek(RebirthNpcStableId id, out RebirthWorldCharacterRecord character)
    {
        character = null;
        try
        {
            var world = GameManager.Instance?.World;
            if(id.IsEmpty || world == null || world.IsRemote() || !ThreadManager.IsMainThread() || RebirthNpcPersistenceCoordinator.IsRecoveryInstalling) return false;
            if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out var person)) return false;
            XElement extension = Find(person);
            if(extension == null || (int?)extension.Attribute("schema") != 1 || (string)extension.Attribute("npc") != id.ToString() ||
               (string)extension.Attribute("legacyMapped") != "1") return false;
            string owner = "npc:" + id;
            if(!RebirthWorldCharacterRepository.ReadNpcOrigin(extension.Element("origin"), out var origin, out var error) ||
               !RebirthWorldCharacterRepository.ReadNpcProgression(extension.Element("progression"), owner, out var state, out error)) return false;
            character = new RebirthWorldCharacterRecord(1, owner, owner, person.AggregateRevision, origin.CommittedAtUtc, DateTime.UtcNow, origin, state, null, null);
            return true;
        }
        catch(Exception) { character = null; return false; }
    }
    private static XElement Find(RebirthNpcPersistentRecord person)
    {
        XElement result = null;
        foreach(string raw in person.UnknownOptionalElements ?? new string[0])
        {
            var node = RebirthCompanionDeskStore.Parse(raw);
            if(node.Name != Element) continue;
            if(result != null) throw new InvalidDataException("Duplicate human progression extension");
            result = node;
        }
        return result;
    }
    private static bool SavedProgressionMatches(RebirthNpcStableId id,XElement expected)
    {
        if(expected==null||!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var saved))return false;
        var actual=Find(saved);
        return actual!=null&&XNode.DeepEquals(actual,expected);
    }
    private static bool Create(RebirthNpcStableId id, RebirthNpcPersistentRecord person, out RebirthWorldCharacterRecord record)
    {
        record = null;
        var bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle?.Backgrounds == null || bundle.Backgrounds.Count == 0 || bundle.Diets.Count == 0) return false;
        byte[] seed;
        using(var sha = SHA256.Create()) seed = sha.ComputeHash(Encoding.UTF8.GetBytes(id.ToString() + ":human-origin-v1"));
        var random = new System.Random(BitConverter.ToInt32(seed, 0));
        int first = random.Next(bundle.Backgrounds.Count);
        RebirthSurvivorCreationResult resolved = null;
        for(int i = 0; i < bundle.Backgrounds.Count; i++)
        {
            resolved = RebirthSurvivorCreationValidator.Validate(new RebirthSurvivorCreationSelection(bundle.Backgrounds[(first+i)%bundle.Backgrounds.Count].Id,
                bundle.Diets[0].Id, null, RebirthSurvivorDefinitionRegistry.SemanticHash), false);
            if(resolved.IsValid) break;
        }
        if(resolved == null || !resolved.IsValid) return false;
        var state = RebirthSurvivorStartingStateInitializer.BuildProgression(resolved);
        // Background-authored starting skills are the only eligible variation.
        // The shared initializer also creates baseline entries for every player
        // skill; varying all of them gives an NPC unrelated background benefits.
        var selectedBackground = bundle.Backgrounds.FirstOrDefault(b =>
            string.Equals(b.Id, resolved.BackgroundId, StringComparison.OrdinalIgnoreCase));
        if(selectedBackground == null) return false;
        foreach(var skillId in selectedBackground.StartingSkills.Select(s => s.SkillId)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.Ordinal))
        {
            if(!state.Skills.TryGetValue(skillId, out var skill) ||
               !RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out var definition)) continue;
            // Modest saved variation on top of the real background starts, never a reload reroll.
            skill.Value = Math.Min(definition.Max, skill.Value + random.Next(0, 6));
        }
        var origin = new RebirthWorldOriginSnapshot("npc:"+id, resolved.DefinitionHash, resolved.DefinitionVersion, "", "", resolved.BackgroundId, resolved.DietId,
            resolved.TraitIds, resolved.RemainingCreationPoints, resolved.Attributes.Select(a => new RebirthWorldOriginAttribute(a.AttributeId,a.Current,a.Potential)),
            resolved.HealthPotential, resolved.UnencumberedSlotDelta, state.Skills.ToDictionary(p=>p.Key,p=>p.Value.Value),
            state.SkillKnowledge.ToDictionary(p=>p.Key,p=>p.Value.Value), state.KnowledgeIds, new Dictionary<string,string>{{"generation","human-origin-v1"}}, DateTime.UtcNow);
        // The origin records the actual generated starts. Legacy earned mastery
        // belongs in current progression, so migration must follow the snapshot.
        MergeDirectLegacySkills(id, state);
        record = new RebirthWorldCharacterRecord(1,"npc:"+id,"npc:"+id,person.AggregateRevision,origin.CommittedAtUtc,origin.CommittedAtUtc,origin,state,null,null);
        return true;
    }
    private static XElement Encode(RebirthNpcStableId id, RebirthWorldCharacterRecord record) => new XElement(Element,
        new XAttribute("schema",1),new XAttribute("npc",id.ToString()),new XAttribute("legacyMapped",1), RebirthWorldCharacterRepository.WriteNpcOrigin(record.Origin),
        RebirthWorldCharacterRepository.WriteNpcProgression(record.Progression,"npc:"+id));
    private static void MergeDirectLegacySkills(RebirthNpcStableId id, RebirthWorldProgressionState state)
    {
        // Looting and Gathering have no one-to-one player skill. Broad legacy
        // weapon families (Blades, Rifles, Automatic and Heavy Weapons) also
        // split into several shared skills. Keep their old records intact and
        // migrate only unambiguous identities; never invent a specialty.
        var mappings = new[]
        {
            (RebirthNpcProfession.Farming, RebirthSurvivorIds.SkillFarming),
            (RebirthNpcProfession.Mining, RebirthSurvivorIds.SkillMining),
            (RebirthNpcProfession.Salvaging, RebirthSurvivorIds.SkillSalvage),
            (RebirthNpcProfession.Cooking, RebirthSurvivorIds.SkillCooking),
            (RebirthNpcProfession.BaseRepairs, RebirthSurvivorIds.SkillConstruction),
            (RebirthNpcProfession.Medicine, RebirthSurvivorIds.SkillMedicine)
        };
        RebirthNpcProfessionProgressionService.EnsureInitialized();
        foreach(var mapping in mappings)
        {
            MergeLegacyTrack(id, state, RebirthNpcProfessionDefinitions.TrackId(mapping.Item1), mapping.Item2);
        }
        RebirthNpcProgressionModifierService.EnsureInitialized();
        var weaponMappings = new[]
        {
            (RebirthNpcWeaponSpecialty.Unarmed, RebirthSurvivorIds.SkillUnarmed),
            (RebirthNpcWeaponSpecialty.Clubs, RebirthSurvivorIds.SkillClubs),
            (RebirthNpcWeaponSpecialty.Spears, RebirthSurvivorIds.SkillSpears),
            (RebirthNpcWeaponSpecialty.Archery, RebirthSurvivorIds.SkillArchery),
            (RebirthNpcWeaponSpecialty.Pistols, RebirthSurvivorIds.SkillPistols),
            (RebirthNpcWeaponSpecialty.Shotguns, RebirthSurvivorIds.SkillShotguns)
        };
        foreach(var mapping in weaponMappings)
            MergeLegacyTrack(id, state, RebirthNpcProgressionModifierService.WeaponTrack(mapping.Item1), mapping.Item2);
    }
    private static void MergeLegacyTrack(RebirthNpcStableId id, RebirthWorldProgressionState state, string trackId, string skillId)
    {
        if(!state.Skills.TryGetValue(skillId, out var skill) ||
           !RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out var definition) ||
           !RebirthNpcProgressionService.TryGetView(id, trackId, out var legacy) ||
           legacy.MaximumLevel <= 1) return;
        float fraction = legacy.Level >= legacy.MaximumLevel ? 0f :
            (float)legacy.ProgressNumerator / Math.Max(1L, legacy.ProgressDenominator);
        // D-01: Max * ((Level - 1 + fraction) / (MaxLevel - 1)); legacy tracks run 1..MaximumLevel (20 for professions and weapons).
        float mastery = legacy.MaximumLevel == RebirthG02SkillScale.MaxLevel
            ? RebirthG02SkillScale.ToSkillValue(legacy.Level, fraction, definition.Max)
            : definition.Max * ((legacy.Level - 1f + fraction) / (legacy.MaximumLevel - 1f));
        skill.Value = Math.Min(definition.Max, Math.Max(skill.Value, mastery));
    }
    internal static bool Save(RebirthNpcStableId id, RebirthWorldCharacterRecord record, XElement lesson=null)
    {
        if(record == null || record.StablePlayerKey != "npc:"+id || RebirthNpcPersistenceCoordinator.IsRecoveryInstalling) return false;
        var encoded = Encode(id,record);
        // Validate the shared codec before changing the aggregate.
        if(!RebirthWorldCharacterRepository.ReadNpcProgression(encoded.Element("progression"), "npc:"+id, out _, out _)) return false;
        if(!RebirthNpcAggregatePersistenceStore.TryMutateExisting(id,checked((uint)record.Revision),person =>
        {
            person.UnknownOptionalElements = (person.UnknownOptionalElements ?? new string[0]).Where(raw => RebirthCompanionDeskStore.Parse(raw).Name != Element)
                .Concat(new[]{encoded.ToString(SaveOptions.DisableFormatting)}).ToArray();
            if(lesson!=null) person.UnknownOptionalElements=person.UnknownOptionalElements.Where(raw=>RebirthCompanionDeskStore.Parse(raw).Name!="humanLesson")
                .Concat(new[]{lesson.ToString(SaveOptions.DisableFormatting)}).ToArray();
            return true;
        })) return false;
        RebirthNpcAggregatePersistenceStore.Save();
        // Save may return without writing when the native save scope is not yet
        // available. Never acknowledge an award or a two-party lesson from the
        // in-memory aggregate alone; replay must see this exact primary row.
        if(!SavedProgressionMatches(id,encoded))return false;
        if(lesson!=null)
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var saved))return false;
            XElement savedLesson=null;
            foreach(string raw in saved.UnknownOptionalElements??new string[0])
            {
                var node=RebirthCompanionDeskStore.Parse(raw);
                if(node.Name!="humanLesson")continue;
                if(savedLesson!=null)return false;
                savedLesson=node;
            }
            if(savedLesson==null||!XNode.DeepEquals(savedLesson,lesson))return false;
        }
        RebirthHumanProgressionProjectionService.Publish(id);
        return true;
    }
    internal static bool Award(RebirthNpcStableId id, RebirthSkillTrainingEvidence evidence)
    {
        if(evidence == null || !evidence.AuthoritativeSuccess || !TryGet(id,out var record)) return false;
        var state = record.Progression;
        if(!string.IsNullOrEmpty(evidence.DurableReceiptId) && state.SkillAwardReceipts.Contains(evidence.DurableReceiptId))
        {
            // A prior Save can publish the award into the in-memory aggregate while
            // the native primary save is unavailable. Retry that same image rather
            // than treating the receipt as permanently unacknowledged.
            return SavedProgressionMatches(id,Encode(id,record)) || Save(id,record);
        }
        if(!state.Skills.TryGetValue(evidence.SkillId,out var skill) || !RebirthSurvivorDefinitionRegistry.TryGetSkill(evidence.SkillId,out var definition) ||
            !RebirthSkillTrainingCalculator.TryCalculate(evidence,skill.Value+skill.Progress,out var computation)) return false;
        float value,progress;
        float lessonMultiplier=1f;
        if(state.TeachingLessons.TryGetValue(evidence.SkillId,out var lesson)&&lesson!=null&&
           lesson.RemainingActiveSeconds>0f&&float.IsNaN(lesson.GainMultiplier)==false&&
           float.IsInfinity(lesson.GainMultiplier)==false)
            lessonMultiplier=Mathf.Clamp(lesson.GainMultiplier,1f,3f);
        if(!RebirthSkillAwardService.TryProjectShared(record,evidence.SkillId,definition,skill.Value,skill.Progress,
            computation.RawBeforeLearningModifiers,lessonMultiplier,out value,out progress)) return false;
        // Membership, not the numerical capacity threshold, forbids nested
        // leadership. Keep a follower below 20 even after legitimate lessons or
        // work/combat awards; the receipt still settles exactly once below.
        if(string.Equals(evidence.SkillId,RebirthSurvivorIds.SkillLeadership,StringComparison.OrdinalIgnoreCase) &&
           RebirthHumanGroupRoles.TryRead(id,out _,out var role,out _) && role=="follower")
        {
            value=Math.Min(value,19f);
            progress=Math.Min(progress,Math.Max(0f,19.999f-value));
        }
        float applied = value+progress-skill.Value-skill.Progress;
        skill.Value=value;skill.Progress=progress;
        RebirthAttributeProgressionService.ApplyTraining(record,definition.PrimaryAttributeId,applied*RebirthProgressionRuntimeConfig.SkillProgressToAttribute,out _);
        if(!string.IsNullOrEmpty(evidence.DurableReceiptId)) state.SkillAwardReceipts.Add(evidence.DurableReceiptId);
        return Save(id,record);
    }
}
