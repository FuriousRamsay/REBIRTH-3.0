using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#nullable disable

/// <summary>
/// Chunk M server authority for player-to-player teaching. Lessons transfer only continuous
/// Theory and never binary recipe/procedure/discipline ownership. Student acceptance,
/// instructor competence, uninterrupted proximity, a persisted pair+subject cooldown, and a
/// real applied outcome are required before Teaching LBD is awarded.
/// </summary>
public static class RebirthTeachingService
{
    public const string TeacherBonusId = "background_bonus.scholar_and_mentor";
    public const float OfferLifetimeSeconds = 30f;
    public const float SessionDurationSeconds = 20f;
    public const float MaximumDistance = 6f;
    public const float MinimumInstructorSkill = 10f;
    public const float MinimumInstructorKnowledge = 20f;
    public const float MinimumKnowledgeGap = 5f;
    public const double PairSubjectCooldownSeconds = 1800.0;
    public const float DefaultTeacherSoloStudyTimeMultiplier = 0.8f;
    public const float DefaultLastingLessonsGainMultiplier = 1.25f;
    public const float DefaultLastingLessonsActiveSeconds = 1800f;

    private sealed class PendingOffer
    {
        public long OfferId;
        public int InstructorId;
        public int StudentId;
        public string SkillId = string.Empty;
        public string OutcomeId = string.Empty;
        public string InstructorCreationId = string.Empty;
        public string StudentCreationId = string.Empty;
        public float ExpiresRealtime;
    }

    private sealed class ActiveSession
    {
        public long OfferId;
        public int InstructorId;
        public int StudentId;
        public string SkillId = string.Empty;
        public string OutcomeId = string.Empty;
        public string InstructorCreationId = string.Empty;
        public string StudentCreationId = string.Empty;
        public float StartedRealtime;
        public RebirthTeachingDurableOutcome PreparedOutcome;
        public float PreparedTransfer;
    }

    private static readonly object Gate = new object();
    private static readonly Dictionary<long, PendingOffer> Pending = new Dictionary<long, PendingOffer>();
    private static readonly Dictionary<long, ActiveSession> Active = new Dictionary<long, ActiveSession>();
    private static bool installed;
    private static long nextOfferId = 1L;
    private static float nextLessonPersistenceSave;
    private static float nextLessonDiscoveryRealtime;
    private static float nextOutcomeRecoveryRealtime;
    private static readonly HashSet<int> LessonPlayers = new HashSet<int>();
    private static readonly HashSet<int> DirtyLessonPlayers = new HashSet<int>();
    private static readonly Dictionary<int, float> NextLessonRevisionPublish = new Dictionary<int, float>();

    public static string Install()
    {
        if (installed) return "[REBIRTH Teaching] already installed";
        installed = true;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Teaching] installed server-owned accepted/proximity/cooldown lesson authority";
    }

    public static float GetSubjectLearningMultiplier(EntityPlayer player, string skillId)
    {
        if (player == null || string.IsNullOrEmpty(skillId) || player.world == null || player.world.IsRemote()) return 1f;
        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || record.Progression == null) return 1f;
        RebirthTeachingLessonRuntimeState lesson;
        if (!record.Progression.TeachingLessons.TryGetValue(skillId, out lesson) || lesson == null || lesson.RemainingActiveSeconds <= 0f) return 1f;
        return Mathf.Clamp(lesson.GainMultiplier, 1f, 3f);
    }

    public static float GetSoloStudyTimeMultiplier(EntityPlayer player)
    {
        if (!HasTeacherBonus(player)) return 1f;
        return Mathf.Clamp(GetTuning(player, "solo_study_time_multiplier", DefaultTeacherSoloStudyTimeMultiplier), 0.25f, 1f);
    }

    public static List<string> BuildEligibleSubjects(EntityPlayer instructor, EntityPlayer student)
    {
        List<string> result = new List<string>();
        string reason;
        RebirthStablePlayerIdentity instructorIdentity, studentIdentity;
        RebirthWorldCharacterRecord instructorRecord, studentRecord;
        if (!TryValidateParticipants(instructor, student, out instructorIdentity, out instructorRecord, out studentIdentity, out studentRecord, out reason)) return result;
        if (RebirthSurvivorDefinitionRegistry.Bundle == null || RebirthSurvivorDefinitionRegistry.Bundle.Progression == null) return result;

        IList<RebirthSkillDefinition> skills = RebirthSurvivorDefinitionRegistry.Bundle.Progression.Skills;
        for (int i = 0; i < skills.Count; ++i)
        {
            RebirthSkillDefinition def = skills[i];
            if (def == null || !CanTeachSkillDefinition(def)) continue;
            if (TryValidateSubject(instructor, instructorIdentity, instructorRecord, student, studentIdentity, studentRecord, def.Id, false, out reason)) result.Add(def.Id);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// Solo-parity teaching route for an owned NPC/companion. The companion's subject Theory is
    /// persisted in the instructor's teaching history under the NPC stable id. The server chooses
    /// the largest currently valid knowledge gap when no preferred subject is supplied. Teaching
    /// Skill is awarded only through the same snapshot that contains the increased student Theory.
    /// </summary>
    public static bool TryTeachOwnedNpc(EntityPlayer instructor, string npcStableKey, string preferredSkillId, out string detail)
    {
        detail = string.Empty;
        if (!IsServerAuthority() || instructor == null || instructor.IsDead()) { detail = "Teaching authority is unavailable."; return false; }
        npcStableKey = (npcStableKey ?? string.Empty).Trim();
        preferredSkillId = (preferredSkillId ?? string.Empty).Trim();
        if (npcStableKey.Length == 0) { detail = "The companion identity is unavailable."; return false; }

        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(instructor, out identity, out record) || record == null || record.Progression == null)
        { detail = "Instructor progression state is unavailable."; return false; }
        if (RebirthSurvivorDefinitionRegistry.Bundle == null || RebirthSurvivorDefinitionRegistry.Bundle.Progression == null)
        { detail = "Teaching definitions are unavailable."; return false; }

        RebirthSkillRuntimeState teachingState;
        float teachingValue = record.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillTeaching, out teachingState) && teachingState != null
            ? teachingState.Value : 0f;
        string studentStorageKey = "npc:" + npcStableKey;
        RebirthSkillDefinition chosen = null;
        RebirthTeachingHistoryRuntimeState chosenHistory = null;
        RebirthSkillKnowledgeRuntimeState chosenInstructorTheory = null;
        float chosenStudentTheory = 0f, chosenTransfer = 0f, chosenGap = -1f;
        string preferredFailure = string.Empty;

        IList<RebirthSkillDefinition> defs = RebirthSurvivorDefinitionRegistry.Bundle.Progression.Skills;
        for (int i = 0; i < defs.Count; ++i)
        {
            RebirthSkillDefinition def = defs[i];
            if (def == null || !CanTeachSkillDefinition(def)) continue;
            if (preferredSkillId.Length > 0 && !string.Equals(preferredSkillId, def.Id, StringComparison.OrdinalIgnoreCase)) continue;

            RebirthSkillRuntimeState practical; RebirthSkillKnowledgeRuntimeState instructorTheory;
            if (!record.Progression.Skills.TryGetValue(def.Id, out practical) || !HasInstructorPracticalSkill(practical))
            { if (preferredSkillId.Length > 0) preferredFailure = "More practical experience is required in " + FriendlySkill(def.Id) + "."; continue; }
            if (!record.Progression.SkillKnowledge.TryGetValue(def.Id, out instructorTheory) || instructorTheory == null || instructorTheory.Value < MinimumInstructorKnowledge)
            { if (preferredSkillId.Length > 0) preferredFailure = "More Theory is required in " + FriendlySkill(def.Id) + "."; continue; }

            string historyKey = BuildHistoryKey(studentStorageKey, def.Id);
            RebirthTeachingHistoryRuntimeState history;
            record.Progression.TeachingHistory.TryGetValue(historyKey, out history);
            float studentTheory = history != null ? Mathf.Clamp(history.StudentTheoryValue, 0f, 100f) : 0f;
            float gap = instructorTheory.Value - studentTheory;
            if (gap < MinimumKnowledgeGap)
            { if (preferredSkillId.Length > 0) preferredFailure = "The companion is already too close to your Theory in " + FriendlySkill(def.Id) + "."; continue; }
            string cooldownReason;
            if (IsOnCooldown(record, studentStorageKey, def.Id, out cooldownReason))
            { if (preferredSkillId.Length > 0) preferredFailure = cooldownReason; continue; }

            float transfer = CalculateLessonTransfer(instructorTheory.Value, studentTheory, teachingValue);
            if (transfer <= 0.0001f) continue;
            if (chosen == null || gap > chosenGap + 0.0001f || (Math.Abs(gap - chosenGap) <= 0.0001f && string.Compare(def.Id, chosen.Id, StringComparison.OrdinalIgnoreCase) < 0))
            {
                chosen = def; chosenHistory = history; chosenInstructorTheory = instructorTheory;
                chosenStudentTheory = studentTheory; chosenTransfer = transfer; chosenGap = gap;
            }
        }
        if (chosen == null)
        {
            detail = preferredSkillId.Length > 0 && preferredFailure.Length > 0 ? preferredFailure : "No companion teaching subject currently has a valid Theory gap.";
            return false;
        }

        float target = Mathf.Clamp(chosenStudentTheory + chosenTransfer, 0f, Math.Min(100f, chosenInstructorTheory.Value - 0.01f));
        float applied = target - chosenStudentTheory;
        if (applied <= 0.0001f) { detail = "The companion has no meaningful Theory gap for that subject."; return false; }
        string historyKeyChosen = BuildHistoryKey(studentStorageKey, chosen.Id);
        bool historyExisted = chosenHistory != null;
        RebirthTeachingHistoryRuntimeState backup = historyExisted ? chosenHistory.Clone() : null;
        if (chosenHistory == null)
        {
            chosenHistory = new RebirthTeachingHistoryRuntimeState { Key = historyKeyChosen, StudentStorageKey = studentStorageKey, SkillId = chosen.Id };
            record.Progression.TeachingHistory[historyKeyChosen] = chosenHistory;
        }
        int completion = Math.Max(0, chosenHistory.CompletionCount) + 1;
        chosenHistory.StudentTheoryValue = target;
        chosenHistory.LastCompletedUtcTicks = DateTime.UtcNow.Ticks;
        chosenHistory.CompletionCount = completion;

        float teachingAward = Mathf.Clamp(0.25f + applied * 0.10f, 0.25f, 0.60f);
        string receipt = "teaching:npc:" + npcStableKey + ":" + chosen.Id + ":" + completion.ToString(CultureInfo.InvariantCulture);
        RebirthSkillTrainingEvidence evidence = new RebirthSkillTrainingEvidence
        {
            SkillId = RebirthSurvivorIds.SkillTeaching,
            SourceKey = "teaching:npc:" + npcStableKey + ":" + chosen.Id,
            DurableReceiptId = receipt,
            ReferenceDescription = "persisted Theory transferred to owned NPC companion",
            AuthoritativeSuccess = true,
            Mode = RebirthSkillTrainingEvidenceMode.DiscreteAward,
            CreditedWork = 1f,
            DiscreteRawAward = teachingAward
        };
        RebirthSkillTrainingComputation computation; float skillAward, attributeAward;
        bool awarded = RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(instructor, evidence, out computation, out skillAward, out attributeAward);
        if (!awarded)
        {
            // If the durable receipt exists, the award mutation happened and only persistence may
            // have failed. Retry the exact dirty snapshot rather than rolling back half an event.
            if (record.Progression.SkillAwardReceipts.Contains(receipt))
            {
                if (!RebirthWorldCharacterRepository.SaveIfDirty(identity, "teaching-npc-retry:" + chosen.Id))
                { detail = "The companion lesson is pending durable storage; retry after persistence recovers."; return false; }
                awarded = true;
            }
            else
            {
                if (historyExisted) record.Progression.TeachingHistory[historyKeyChosen] = backup;
                else record.Progression.TeachingHistory.Remove(historyKeyChosen);
                detail = "The Teaching award could not be committed.";
                return false;
            }
        }

        // The lesson is already committed. Insight save retry uses the shared owner scheduler;
        // never repeat or roll back the completed companion lesson to settle this finite reward.
        RebirthTheoryProgressionService.TryAwardInsight(instructor, "insight.teaching.successful_lesson",
            "persisted-companion-lesson", out var insightTheory, out var insightAlreadyEarned);
        PublishSavedOutcome(instructor, "teaching-npc:" + chosen.Id);
        detail = "Taught " + FriendlySkill(chosen.Id) + " Theory to your companion: +" + applied.ToString("0.##", CultureInfo.InvariantCulture) + " Theory; +" + skillAward.ToString("0.###", CultureInfo.InvariantCulture) + " Teaching.";
        return true;
    }

    public static void ProcessSubjectsRequest(int instructorId, int studentId, long requestId)
    {
        if (!IsServerAuthority()) return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayer instructor = world != null ? world.GetEntity(instructorId) as EntityPlayer : null;
        EntityPlayer student = world != null ? world.GetEntity(studentId) as EntityPlayer : null;
        string reason;
        RebirthStablePlayerIdentity ii, si; RebirthWorldCharacterRecord ir, sr;
        List<string> subjects = new List<string>();
        if (TryValidateParticipants(instructor, student, out ii, out ir, out si, out sr, out reason)) subjects = BuildEligibleSubjects(instructor, student);
        else Notify(instructor, false, "Teaching unavailable: " + reason);
        SendSubjects(instructor, student, subjects, requestId);
    }

    public static void ProcessOfferRequest(int instructorId, int studentId, string skillId)
    {
        if (!IsServerAuthority()) return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayer instructor = world != null ? world.GetEntity(instructorId) as EntityPlayer : null;
        EntityPlayer student = world != null ? world.GetEntity(studentId) as EntityPlayer : null;
        RebirthStablePlayerIdentity ii, si; RebirthWorldCharacterRecord ir, sr; string reason;
        if (!TryValidateParticipants(instructor, student, out ii, out ir, out si, out sr, out reason) ||
            !TryValidateSubject(instructor, ii, ir, student, si, sr, skillId, true, out reason))
        {
            Notify(instructor, false, "Lesson offer rejected: " + reason);
            return;
        }
        if (HasParticipantSession(instructorId) || HasParticipantSession(studentId))
        {
            Notify(instructor, false, "Lesson offer rejected: one of you is already in a teaching session.");
            return;
        }

        PendingOffer offer = new PendingOffer();
        lock (Gate)
        {
            RemovePendingForPairLocked(instructorId, studentId);
            offer.OfferId = nextOfferId++;
            if (nextOfferId <= 0L) nextOfferId = 1L;
            offer.InstructorId = instructorId;
            offer.StudentId = studentId;
            offer.InstructorCreationId = ir.Origin.CreationId;
            offer.StudentCreationId = sr.Origin.CreationId;
            offer.SkillId = skillId ?? string.Empty;
            offer.OutcomeId = DateTime.UtcNow.Ticks.ToString("D19", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            offer.ExpiresRealtime = Time.realtimeSinceStartup + OfferLifetimeSeconds;
            Pending[offer.OfferId] = offer;
        }
        SendOffer(student, offer, GetPlayerName(instructor));
        Notify(instructor, true, "Lesson offer sent to " + GetPlayerName(student) + ".");
    }

    public static void ProcessOfferResponse(int studentId, long offerId, bool accept)
    {
        if (!IsServerAuthority()) return;
        PendingOffer offer;
        bool expired = false;
        lock (Gate)
        {
            if (!Pending.TryGetValue(offerId, out offer) || offer == null || offer.StudentId != studentId) return;
            // Expiry is checked under the same lock that consumes the offer. The update-loop cleanup
            // is only housekeeping; it is never the authority boundary for consent.
            expired = Time.realtimeSinceStartup >= offer.ExpiresRealtime;
            Pending.Remove(offerId);
        }
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayer expiredInstructor = world != null ? world.GetEntity(offer.InstructorId) as EntityPlayer : null;
        EntityPlayer expiredStudent = world != null ? world.GetEntity(offer.StudentId) as EntityPlayer : null;
        if (expired)
        {
            Notify(expiredInstructor, false, "Teaching offer expired.");
            Notify(expiredStudent, false, "That teaching offer has expired.");
            return;
        }
        EntityPlayer instructor = world != null ? world.GetEntity(offer.InstructorId) as EntityPlayer : null;
        EntityPlayer student = world != null ? world.GetEntity(offer.StudentId) as EntityPlayer : null;
        if (!accept)
        {
            Notify(instructor, false, GetPlayerName(student) + " declined the teaching offer.");
            Notify(student, true, "Teaching offer declined.");
            return;
        }

        RebirthStablePlayerIdentity ii, si; RebirthWorldCharacterRecord ir, sr; string reason;
        if (!TryValidateParticipants(instructor, student, out ii, out ir, out si, out sr, out reason) ||
            !MatchesLessonCharacters(offer.InstructorCreationId, offer.StudentCreationId, ir, sr, out reason) ||
            !TryValidateSubject(instructor, ii, ir, student, si, sr, offer.SkillId, true, out reason) ||
            HasParticipantSession(instructor.entityId) || HasParticipantSession(student.entityId))
        {
            Notify(instructor, false, "Lesson could not start: " + (string.IsNullOrEmpty(reason) ? "session unavailable" : reason));
            Notify(student, false, "Lesson could not start.");
            return;
        }

        lock (Gate)
        {
            Active[offer.OfferId] = new ActiveSession
            {
                OfferId = offer.OfferId, InstructorId = offer.InstructorId, StudentId = offer.StudentId,
                InstructorCreationId = offer.InstructorCreationId, StudentCreationId = offer.StudentCreationId,
                SkillId = offer.SkillId, OutcomeId = offer.OutcomeId, StartedRealtime = Time.realtimeSinceStartup
            };
        }
        string subject = FriendlySkill(offer.SkillId);
        Notify(instructor, true, "Teaching " + subject + " started. Stay near " + GetPlayerName(student) + " for " + SessionDurationSeconds.ToString("0", CultureInfo.InvariantCulture) + " seconds.");
        Notify(student, true, "Lesson in " + subject + " started. Stay near " + GetPlayerName(instructor) + ".");
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if (!IsServerAuthority()) return;
        World world = GameManager.Instance.World;
        float now = Time.realtimeSinceStartup;
        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.25f);

        List<PendingOffer> expired = null;
        List<ActiveSession> sessions = null;
        lock (Gate)
        {
            List<long> remove = null;
            foreach (KeyValuePair<long, PendingOffer> pair in Pending)
                if (pair.Value == null || now >= pair.Value.ExpiresRealtime)
                {
                    if (remove == null) remove = new List<long>();
                    remove.Add(pair.Key);
                    if (pair.Value != null)
                    {
                        if (expired == null) expired = new List<PendingOffer>();
                        expired.Add(pair.Value);
                    }
                }
            if (remove != null)
                for (int i = 0; i < remove.Count; ++i) Pending.Remove(remove[i]);
            if (Active.Count > 0) sessions = new List<ActiveSession>(Active.Values);
        }
        for (int i = 0; expired != null && i < expired.Count; ++i)
        {
            EntityPlayer instructor = world.GetEntity(expired[i].InstructorId) as EntityPlayer;
            Notify(instructor, false, "Teaching offer expired.");
        }

        for (int i = 0; sessions != null && i < sessions.Count; ++i)
        {
            ActiveSession s = sessions[i]; if (s == null) continue;
            EntityPlayer instructor = world.GetEntity(s.InstructorId) as EntityPlayer;
            EntityPlayer student = world.GetEntity(s.StudentId) as EntityPlayer;
            string cancel;
            if (!SessionParticipantsRemainValid(s, instructor, student, out cancel)) { CancelSession(s, instructor, student, cancel); continue; }
            if (now - s.StartedRealtime >= SessionDurationSeconds) CompleteSession(s, instructor, student);
        }

        if (now >= nextOutcomeRecoveryRealtime)
        {
            nextOutcomeRecoveryRealtime = now + 1f;
            RecoverDurableOutcomes(world);
        }

        TickLastingLessons(world, now, dt);
    }

    private static void TickLastingLessons(World world, float now, float dt)
    {
        if (world == null) return;

        // Persisted lesson state is rediscovered slowly after load. Ordinary frames only touch the
        // small set of players known to have active lessons rather than scanning the whole roster.
        if (now >= nextLessonDiscoveryRealtime)
        {
            nextLessonDiscoveryRealtime = now + 5f;
            if (world.Players != null && world.Players.list != null)
            {
                List<EntityPlayer> players = world.Players.list;
                lock (Gate)
                {
                    for (int i = 0; i < players.Count; ++i)
                    {
                        EntityPlayer player = players[i]; RebirthWorldCharacterRecord record;
                        if (player != null && RebirthWorldCharacterService.TryGet(player, out record) && record != null &&
                            record.Progression != null && record.Progression.TeachingLessons.Count > 0)
                            LessonPlayers.Add(player.entityId);
                    }
                }
            }
        }

        int[] playerIds;
        lock (Gate)
        {
            if (LessonPlayers.Count == 0) return;
            playerIds = new int[LessonPlayers.Count];
            LessonPlayers.CopyTo(playerIds);
        }

        for (int i = 0; i < playerIds.Length; ++i)
        {
            int entityId = playerIds[i];
            EntityPlayer player = world.GetEntity(entityId) as EntityPlayer;
            RebirthWorldCharacterRecord record;
            if (player == null || !RebirthWorldCharacterService.TryGet(player, out record) || record == null || record.Progression == null)
            {
                lock (Gate) { LessonPlayers.Remove(entityId); NextLessonRevisionPublish.Remove(entityId); }
                continue;
            }

            if (record.Progression.TeachingLessons.Count == 0)
            {
                lock (Gate) { LessonPlayers.Remove(entityId); NextLessonRevisionPublish.Remove(entityId); }
                continue;
            }

            List<string> remove = null;
            if (dt > 0f)
            {
                foreach (KeyValuePair<string, RebirthTeachingLessonRuntimeState> pair in record.Progression.TeachingLessons)
                {
                    RebirthTeachingLessonRuntimeState lesson = pair.Value;
                    if (lesson == null)
                    {
                        if (remove == null) remove = new List<string>();
                        remove.Add(pair.Key);
                        continue;
                    }
                    lesson.RemainingActiveSeconds = Mathf.Max(0f, lesson.RemainingActiveSeconds - dt);
                    if (lesson.RemainingActiveSeconds <= 0f)
                    {
                        if (remove == null) remove = new List<string>();
                        remove.Add(pair.Key);
                    }
                }
            }
            if (remove != null) for (int r = 0; r < remove.Count; ++r) record.Progression.TeachingLessons.Remove(remove[r]);

            bool publish;
            lock (Gate)
            {
                float next;
                publish = remove != null || !NextLessonRevisionPublish.TryGetValue(entityId, out next) || now >= next;
                if (publish) NextLessonRevisionPublish[entityId] = now + 1f;
                if (record.Progression.TeachingLessons.Count == 0)
                {
                    LessonPlayers.Remove(entityId);
                    NextLessonRevisionPublish.Remove(entityId);
                }
            }
            if (publish)
            {
                record.Touch(remove != null ? "teaching-lasting-lessons-expired" : "teaching-lasting-lessons-tick");
                lock (Gate) DirtyLessonPlayers.Add(entityId);
            }
        }

        if (now < nextLessonPersistenceSave) return;
        nextLessonPersistenceSave = now + 30f;
        int[] dirtyIds;
        lock (Gate)
        {
            if (DirtyLessonPlayers.Count == 0) return;
            dirtyIds = new int[DirtyLessonPlayers.Count];
            DirtyLessonPlayers.CopyTo(dirtyIds);
            DirtyLessonPlayers.Clear();
        }
        for (int i = 0; i < dirtyIds.Length; ++i)
        {
            EntityPlayer player = world.GetEntity(dirtyIds[i]) as EntityPlayer;
            if (player != null) RebirthWorldCharacterService.FlushPlayer(player, "teaching-lasting-lessons-periodic");
        }
    }

    private static void CompleteSession(ActiveSession session, EntityPlayer instructor, EntityPlayer student)
    {
        lock (Gate) { if (session == null || !Active.TryGetValue(session.OfferId, out var original) || !ReferenceEquals(original, session)) return; }
        RebirthStablePlayerIdentity ii, si; RebirthWorldCharacterRecord ir, sr; string reason;
        if (!TryValidateParticipants(instructor, student, out ii, out ir, out si, out sr, out reason) ||
            !MatchesLessonCharacters(session.InstructorCreationId, session.StudentCreationId, ir, sr, out reason) ||
            !TryValidateSubject(instructor, ii, ir, student, si, sr, session.SkillId, true, out reason))
        { lock (Gate) Active.Remove(session.OfferId); Notify(instructor, false, "Lesson ended without progress: " + reason); Notify(student, false, "Lesson ended without progress."); return; }

        RebirthTeachingDurableOutcome outcome = session.PreparedOutcome;
        float applied = session.PreparedTransfer;
        if (outcome == null)
        {
            RebirthSkillKnowledgeRuntimeState instructorTheory, studentTheory;
            if (!ir.Progression.SkillKnowledge.TryGetValue(session.SkillId, out instructorTheory) || instructorTheory == null ||
                !sr.Progression.SkillKnowledge.TryGetValue(session.SkillId, out studentTheory) || studentTheory == null)
            { lock (Gate) Active.Remove(session.OfferId); Notify(instructor, false, "Lesson ended without progress: subject theory state unavailable."); return; }

            RebirthSkillRuntimeState teaching;
            float teachingValue = ir.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillTeaching, out teaching) && teaching != null ? teaching.Value : 0f;
            float requested = CalculateLessonTransfer(instructorTheory.Value, studentTheory.Value, teachingValue);
            float before = studentTheory.Value;
            float target = Mathf.Clamp(before + requested, 0f, 100f);
            applied = target - before;
            if (applied <= 0.0001f) { lock (Gate) Active.Remove(session.OfferId); Notify(instructor, false, "Lesson ended without progress because the student no longer had a meaningful knowledge gap."); return; }

            string historyKey = BuildHistoryKey(si.StorageKey, session.SkillId);
            RebirthTeachingHistoryRuntimeState existingHistory;
            int historyTarget = ir.Progression.TeachingHistory.TryGetValue(historyKey, out existingHistory) && existingHistory != null ? Math.Max(0, existingHistory.CompletionCount) + 1 : 1;
            bool lasting = HasTeacherBonus(instructor);
            float multiplier = lasting ? Mathf.Clamp(GetTuning(instructor, "lasting_lessons_gain_multiplier", DefaultLastingLessonsGainMultiplier), 1f, 3f) : 1f;
            float seconds = lasting ? Mathf.Clamp(GetTuning(instructor, "lasting_lessons_active_seconds", DefaultLastingLessonsActiveSeconds), 60f, 7200f) : 0f;
            float teachingAward = Mathf.Clamp(0.25f + applied * 0.10f, 0.25f, 0.60f);

            outcome = new RebirthTeachingDurableOutcome
            {
                OutcomeId = string.IsNullOrEmpty(session.OutcomeId) ? (DateTime.UtcNow.Ticks.ToString("D19", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")) : session.OutcomeId,
                InstructorStorageKey = ii.StorageKey, StudentStorageKey = si.StorageKey, SkillId = session.SkillId,
                InstructorCreationId = session.InstructorCreationId, StudentCreationId = session.StudentCreationId,
                StudentKnowledgeTarget = target, HistoryKey = historyKey, HistoryTargetCount = historyTarget, CompletedUtcTicks = DateTime.UtcNow.Ticks,
                HasLastingLesson = lasting, LastingLessonSeconds = seconds, LastingLessonMultiplier = multiplier, TeacherAward = teachingAward
            };
            // Freeze the original completed lesson before any uncertain journal write.
            session.PreparedOutcome = outcome; session.PreparedTransfer = applied;
        }
        string reserveError;
        if (!RebirthTeachingOutcomeStore.TryReserveOriginal(outcome, ir.Progression.SoloTheory?.TeachingOriginal?.Through ?? 0L, out reserveError))
        {
            Notify(instructor, false, "Lesson completion is waiting for durable storage; it will retry.");
            return; // Keep Active so the same logical session retries without inventing another outcome.
        }

        lock (Gate) Active.Remove(session.OfferId);
        bool complete = TryApplyDurableOutcome(outcome, instructor, student, ii, ir, si, sr);
        if (!complete)
        {
            Notify(instructor, true, "Lesson completed; durable participant updates are still being reconciled.");
            Notify(student, true, "Lesson completed; your durable lesson update is being reconciled.");
            return;
        }
        NotifyLessonCompletion(outcome, instructor, student, applied);
    }

    private static bool TryApplyDurableOutcome(RebirthTeachingDurableOutcome outcome, EntityPlayer instructor, EntityPlayer student,
        RebirthStablePlayerIdentity ii, RebirthWorldCharacterRecord ir, RebirthStablePlayerIdentity si, RebirthWorldCharacterRecord sr)
    {
        string identityReason;
        if (outcome == null || ii == null || si == null ||
            !string.Equals(ii.StorageKey,outcome.InstructorStorageKey,StringComparison.Ordinal) ||
            !string.Equals(si.StorageKey,outcome.StudentStorageKey,StringComparison.Ordinal) ||
            !MatchesLessonCharacters(outcome.InstructorCreationId,outcome.StudentCreationId,ir,sr,out identityReason) ||
            !ir.IsComplete || !sr.IsComplete || ir.Progression==null || sr.Progression==null) return false;
        if(outcome.OriginalOrdinal>0)
        {
            if(!RebirthTeachingOutcomeStore.TryGetOriginal(outcome,out var savedOriginal)||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(ir)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(sr)||
                instructor?.world==null||!ReferenceEquals(instructor.world,GameManager.Instance?.World)||instructor.world.IsRemote()||
                !ReferenceEquals(student?.world,instructor.world)||!ReferenceEquals(instructor.world.GetEntity(instructor.entityId),instructor)||!ReferenceEquals(instructor.world.GetEntity(student.entityId),student))return false;
            if(outcome.SoloEvidenceApplied && (ir.Progression.SoloTheory?.TeachingOriginal?.IsAcknowledged(outcome.OriginalOrdinal)!=true||
                !RebirthWorldCharacterRepository.HasSavedSoloTeachingOriginal(ii,ir.Progression.SoloTheory)))return false;
        }
        string studentReceipt = "teaching:" + outcome.OutcomeId + ":student";
        string instructorReceipt = "teaching:" + outcome.OutcomeId + ":instructor";
        string rewardReceipt = "teaching:" + outcome.OutcomeId + ":reward";

        // Journal acknowledgement only proves a particular character snapshot was saved.
        // A restored/older character snapshot may have lost both its receipt and its effects.
        // Replaying here can restart consumed Lasting Lessons or duplicate additive rewards;
        // skipping the stage can silently retire an outcome whose effects are missing.
        // Preserve the pending outcome until its character evidence can be reconciled.
        if ((outcome.StudentApplied && !sr.Progression.SkillAwardReceipts.Contains(studentReceipt)) ||
            (outcome.InstructorApplied && !ir.Progression.SkillAwardReceipts.Contains(instructorReceipt)) ||
            (outcome.RewardApplied && !ir.Progression.SkillAwardReceipts.Contains(rewardReceipt))) return false;
        if (!outcome.StudentApplied)
        {
            bool already = sr.Progression.SkillAwardReceipts.Contains(studentReceipt);
            if (!already)
            {
                RebirthSkillKnowledgeRuntimeState theory;
                if (!sr.Progression.SkillKnowledge.TryGetValue(outcome.SkillId, out theory) || theory == null) return false;
                theory.Value = Math.Max(theory.Value, outcome.StudentKnowledgeTarget);
                if (outcome.HasLastingLesson)
                {
                    RebirthTeachingLessonRuntimeState lesson;
                    if (!sr.Progression.TeachingLessons.TryGetValue(outcome.SkillId, out lesson) || lesson == null || string.Equals(lesson.OutcomeId, outcome.OutcomeId, StringComparison.Ordinal))
                    {
                        sr.Progression.TeachingLessons[outcome.SkillId] = new RebirthTeachingLessonRuntimeState
                        { SkillId = outcome.SkillId, RemainingActiveSeconds = outcome.LastingLessonSeconds, GainMultiplier = outcome.LastingLessonMultiplier, InstructorStorageKey = outcome.InstructorStorageKey, OutcomeId = outcome.OutcomeId };
                        if (student != null) lock (Gate) { LessonPlayers.Add(student.entityId); DirtyLessonPlayers.Add(student.entityId); NextLessonRevisionPublish[student.entityId] = Time.realtimeSinceStartup + 1f; }
                    }
                }
                AddBoundedReceipt(sr.Progression.SkillAwardReceipts, studentReceipt,sr.Progression.PendingTheoryStudy?.Receipt);
                sr.Touch("teaching-outcome-student:" + outcome.SkillId);
            }
            if (sr.Dirty && !RebirthWorldCharacterRepository.SaveIfDirty(si, "teaching-outcome-student:" + outcome.SkillId)) return false;
            if (!RebirthTeachingOutcomeStore.AcknowledgeStudent(outcome.OutcomeId)) return false;
            outcome.StudentApplied = true;
            PublishSavedOutcome(student,"teaching-learned:"+outcome.SkillId);
        }

        if (!outcome.InstructorApplied)
        {
            bool already = ir.Progression.SkillAwardReceipts.Contains(instructorReceipt);
            if (!already)
            {
                RebirthTeachingHistoryRuntimeState history;
                if (!ir.Progression.TeachingHistory.TryGetValue(outcome.HistoryKey, out history) || history == null)
                    ir.Progression.TeachingHistory[outcome.HistoryKey] = history = new RebirthTeachingHistoryRuntimeState { Key = outcome.HistoryKey, StudentStorageKey = outcome.StudentStorageKey, SkillId = outcome.SkillId };
                history.LastCompletedUtcTicks = Math.Max(history.LastCompletedUtcTicks, outcome.CompletedUtcTicks);
                history.CompletionCount = Math.Max(history.CompletionCount, outcome.HistoryTargetCount);
                PruneHistory(ir.Progression);
                AddBoundedReceipt(ir.Progression.SkillAwardReceipts, instructorReceipt,ir.Progression.PendingTheoryStudy?.Receipt);
                ir.Touch("teaching-outcome-instructor:" + outcome.SkillId);
            }
            if (ir.Dirty && !RebirthWorldCharacterRepository.SaveIfDirty(ii, "teaching-outcome-instructor:" + outcome.SkillId)) return false;
            if (!RebirthTeachingOutcomeStore.AcknowledgeInstructor(outcome.OutcomeId)) return false;
            outcome.InstructorApplied = true;
            PublishSavedOutcome(instructor,"teaching-complete:"+outcome.SkillId);
        }

        if(outcome.OriginalOrdinal>0 && !outcome.SoloEvidenceApplied)
        {
            if(!RebirthTheorySoloTeachingCompletion.TryApply(outcome,instructor,ii,ir))return false;
        }

        if (!outcome.RewardApplied)
        {
            RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence
            {
                SkillId=RebirthSurvivorIds.SkillTeaching,SourceKey="teaching:outcome:"+outcome.OutcomeId,DurableReceiptId=rewardReceipt,
                ReferenceDescription="persisted Theory transferred to student",AuthoritativeSuccess=true,
                Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=1f,DiscreteRawAward=outcome.TeacherAward
            };
            RebirthSkillTrainingComputation computation;float skillAward,attributeAward;
            if(!RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(instructor,evidence,out computation,out skillAward,out attributeAward))return false;
            if (!RebirthTheoryProgressionService.TryAwardInsight(instructor,
                "insight.teaching.successful_lesson", "persisted-teaching-outcome",
                out var insightTheory, out var insightAlreadyEarned)) return false;
            if (!RebirthTeachingOutcomeStore.AcknowledgeReward(outcome.OutcomeId)) return false;
            outcome.RewardApplied = true;
        }
        return true;
    }

    private static void PublishSavedOutcome(EntityPlayer player,string reason)
    {
        if(player==null)return;
        try
        {
            if(RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,reason))return;
        }
        catch(Exception)
        {
            // Participant data is already saved and acknowledged; publication failure
            // must not interrupt the other participant or repeat the lesson award.
        }
        RebirthSkillAwardService.QueueOwnerPublication(player);
    }
    private static void RecoverDurableOutcomes(World world)
    {
        if (world == null || world.Players == null || world.Players.list == null) return;
        RebirthTeachingDurableOutcome[] outcomes = RebirthTeachingOutcomeStore.Snapshot();
        for (int i = 0; i < outcomes.Length; ++i)
        {
            RebirthTeachingDurableOutcome o = outcomes[i]; if (!RebirthTeachingOutcomeStore.HasCharacterBinding(o)) continue;
            EntityPlayer instructor = null, student = null; RebirthStablePlayerIdentity ii = null, si = null; RebirthWorldCharacterRecord ir = null, sr = null;
            List<EntityPlayer> players = world.Players.list;
            for (int p = 0; p < players.Count && (instructor == null || student == null); ++p)
            {
                EntityPlayer candidate = players[p]; RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
                if (candidate == null || !RebirthStablePlayerIdentity.TryResolveServerEntity(candidate, out identity) || identity == null ||
                    !RebirthWorldCharacterRepository.TryGet(identity, out record) || record == null) continue;
                if (instructor == null && string.Equals(identity.StorageKey, o.InstructorStorageKey, StringComparison.Ordinal)) { instructor = candidate; ii = identity; ir = record; }
                if (student == null && string.Equals(identity.StorageKey, o.StudentStorageKey, StringComparison.Ordinal)) { student = candidate; si = identity; sr = record; }
            }
            if (instructor == null || student == null || ii == null || si == null || ir == null || sr == null) continue;
            TryApplyDurableOutcome(o, instructor, student, ii, ir, si, sr);
        }
    }

    private static void NotifyLessonCompletion(RebirthTeachingDurableOutcome outcome, EntityPlayer instructor, EntityPlayer student, float applied)
    {
        string subject = FriendlySkill(outcome.SkillId);
        Notify(instructor, true, "Lesson complete: " + GetPlayerName(student) + " gained " + applied.ToString("0.##", CultureInfo.InvariantCulture) + " " + subject + " Theory.");
        Notify(student, true, "Lesson complete: you gained " + applied.ToString("0.##", CultureInfo.InvariantCulture) + " " + subject + " Theory" + (outcome.HasLastingLesson ? " and Lasting Lessons." : "."));
    }

    private static void AddBoundedReceipt(HashSet<string> receipts, string receipt,string protectedReceipt)
    {
        if (receipts == null || string.IsNullOrEmpty(receipt)) return; receipts.Add(receipt);
        RebirthTeachingOutcomeStore.PruneAwardReceipts(receipts,protectedReceipt);
    }

    // Value is the earned practical level; Progress is progress toward its next increase.
    // Player and companion lessons use the same requirement, including fractional saved levels.
    private static bool HasInstructorPracticalSkill(RebirthSkillRuntimeState state)
    {
        return state != null && !float.IsNaN(state.Value) && !float.IsInfinity(state.Value)
            && state.Value >= MinimumInstructorSkill;
    }
    public static float CalculateLessonTransfer(float instructorKnowledge, float studentKnowledge, float teachingSkill)
    {
        float gap = Math.Max(0f, instructorKnowledge - studentKnowledge);
        if (gap < MinimumKnowledgeGap) return 0f;
        float requested = Mathf.Clamp(1.5f + Mathf.Clamp01(teachingSkill / 100f) * 1.5f, 1.5f, 3f);
        requested = Math.Min(requested, Math.Max(0f, gap * 0.5f));
        return Math.Min(requested, Math.Max(0f, instructorKnowledge - studentKnowledge - 0.01f));
    }

    private static bool TryValidateParticipants(EntityPlayer instructor, EntityPlayer student,
        out RebirthStablePlayerIdentity instructorIdentity, out RebirthWorldCharacterRecord instructorRecord,
        out RebirthStablePlayerIdentity studentIdentity, out RebirthWorldCharacterRecord studentRecord, out string reason)
    {
        instructorIdentity = null; studentIdentity = null; instructorRecord = null; studentRecord = null; reason = string.Empty;
        if (!IsServerAuthority()) { reason = "server authority unavailable"; return false; }
        if (instructor == null || student == null || instructor == student) { reason = "both distinct players must be online"; return false; }
        if (instructor.IsDead() || student.IsDead()) { reason = "both players must be alive"; return false; }
        if (RebirthCharacterCreationHoldService.IsHeld(instructor) || RebirthCharacterCreationHoldService.IsHeld(student)) { reason = "character creation is not complete"; return false; }
        if ((instructor.position - student.position).sqrMagnitude > MaximumDistance * MaximumDistance) { reason = "move within " + MaximumDistance.ToString("0", CultureInfo.InvariantCulture) + " metres"; return false; }
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(instructor, out instructorIdentity) || instructorIdentity == null ||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(student, out studentIdentity) || studentIdentity == null) { reason = "stable player identity unavailable"; return false; }
        if (!RebirthWorldCharacterRepository.TryGet(instructorIdentity, out instructorRecord) || instructorRecord == null || !instructorRecord.IsComplete || instructorRecord.Progression == null ||
            !RebirthWorldCharacterRepository.TryGet(studentIdentity, out studentRecord) || studentRecord == null || !studentRecord.IsComplete || studentRecord.Progression == null) { reason = "both players need complete Rebirth Survivor characters"; return false; }
        string instructorCreation, studentCreation;
        if (instructorRecord.Origin == null || studentRecord.Origin == null ||
            !RebirthSurvivorRequestScope.TryNormalize(instructorRecord.Origin.CreationId, out instructorCreation) ||
            !RebirthSurvivorRequestScope.TryNormalize(studentRecord.Origin.CreationId, out studentCreation))
        { reason = Localization.Get("xuiRebirthTeachingCharacterChanged"); return false; }
        return true;
    }

    private static bool TryValidateSubject(EntityPlayer instructor, RebirthStablePlayerIdentity instructorIdentity, RebirthWorldCharacterRecord instructorRecord,
        EntityPlayer student, RebirthStablePlayerIdentity studentIdentity, RebirthWorldCharacterRecord studentRecord, string skillId, bool enforceCooldown, out string reason)
    {
        reason = string.Empty; RebirthSkillDefinition def;
        if (!RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out def) || def == null || !CanTeachSkillDefinition(def)) { reason = "subject is not teachable"; return false; }
        RebirthSkillRuntimeState practical; RebirthSkillKnowledgeRuntimeState theory, studentTheory;
        if (!instructorRecord.Progression.Skills.TryGetValue(skillId, out practical) || !HasInstructorPracticalSkill(practical)) { reason = "instructor needs more practical experience in " + FriendlySkill(skillId); return false; }
        if (!instructorRecord.Progression.SkillKnowledge.TryGetValue(skillId, out theory) || theory == null || theory.Value < MinimumInstructorKnowledge) { reason = "instructor needs more Theory in " + FriendlySkill(skillId); return false; }
        if (!studentRecord.Progression.SkillKnowledge.TryGetValue(skillId, out studentTheory) || studentTheory == null) { reason = "student theory state unavailable"; return false; }
        if (theory.Value - studentTheory.Value < MinimumKnowledgeGap) { reason = "student is already too close to the instructor's Theory"; return false; }
        if (enforceCooldown && IsOnCooldown(instructorRecord, studentIdentity.StorageKey, skillId, out reason)) return false;
        return true;
    }

    private static bool SessionParticipantsRemainValid(ActiveSession session, EntityPlayer instructor, EntityPlayer student, out string reason)
    {
        reason = string.Empty;
        if (instructor == null || student == null) { reason = "a participant disconnected"; return false; }
        if (instructor.IsDead() || student.IsDead()) { reason = "a participant died"; return false; }
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) { reason = "Rebirth progression is no longer active"; return false; }
        if (RebirthCharacterCreationHoldService.IsHeld(instructor) || RebirthCharacterCreationHoldService.IsHeld(student)) { reason = "a participant became unavailable"; return false; }
        if ((instructor.position - student.position).sqrMagnitude > MaximumDistance * MaximumDistance) { reason = "the participants moved too far apart"; return false; }
        RebirthWorldCharacterRecord a, b; if (!RebirthWorldCharacterService.TryGet(instructor, out a) || !RebirthWorldCharacterService.TryGet(student, out b)) { reason = "Survivor character state became unavailable"; return false; }
        if (!MatchesLessonCharacters(session.InstructorCreationId, session.StudentCreationId, a, b, out reason)) return false;
        return true;
    }

    private static bool MatchesLessonCharacters(string instructorCreation, string studentCreation,
        RebirthWorldCharacterRecord instructor, RebirthWorldCharacterRecord student, out string reason)
    {
        reason = string.Empty;
        if (instructor == null || student == null || instructor.Origin == null || student.Origin == null ||
            !RebirthSurvivorRequestScope.Matches(instructorCreation, instructor.Origin.CreationId) ||
            !RebirthSurvivorRequestScope.Matches(studentCreation, student.Origin.CreationId))
        { reason = Localization.Get("xuiRebirthTeachingCharacterChanged"); return false; }
        return true;
    }

    private static bool CanTeachSkillDefinition(RebirthSkillDefinition def)
    {
        if (def == null || def.Advanced) return false;
        string id = def.Id ?? string.Empty;
        return !string.Equals(id, RebirthSurvivorIds.SkillTeaching, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOnCooldown(RebirthWorldCharacterRecord instructorRecord, string studentStorageKey, string skillId, out string reason)
    {
        reason = string.Empty; if (instructorRecord == null || instructorRecord.Progression == null) return false;
        RebirthTeachingHistoryRuntimeState history;
        if (!instructorRecord.Progression.TeachingHistory.TryGetValue(BuildHistoryKey(studentStorageKey, skillId), out history) || history == null || history.LastCompletedUtcTicks <= 0L) return false;
        double age = (DateTime.UtcNow - new DateTime(history.LastCompletedUtcTicks, DateTimeKind.Utc)).TotalSeconds;
        if (age >= PairSubjectCooldownSeconds) return false;
        reason = "that instructor/student/subject lesson is on cooldown for about " + Math.Ceiling(PairSubjectCooldownSeconds - Math.Max(0.0, age)).ToString("0", CultureInfo.InvariantCulture) + " seconds";
        return true;
    }

    private static string BuildHistoryKey(string studentStorageKey, string skillId) { return (studentStorageKey ?? string.Empty) + "|" + (skillId ?? string.Empty); }
    private static bool HasParticipantSession(int entityId) { lock (Gate) foreach (ActiveSession s in Active.Values) if (s != null && (s.InstructorId == entityId || s.StudentId == entityId)) return true; return false; }
    private static void RemovePendingForPairLocked(int instructorId, int studentId) { List<long> ids = new List<long>(); foreach (KeyValuePair<long, PendingOffer> pair in Pending) if (pair.Value != null && (pair.Value.InstructorId == instructorId || pair.Value.StudentId == studentId || pair.Value.InstructorId == studentId || pair.Value.StudentId == instructorId)) ids.Add(pair.Key); for (int i=0;i<ids.Count;i++) Pending.Remove(ids[i]); }
    private static void CancelSession(ActiveSession s, EntityPlayer instructor, EntityPlayer student, string reason) { lock (Gate) Active.Remove(s.OfferId); Notify(instructor, false, "Lesson cancelled: " + reason + "."); Notify(student, false, "Lesson cancelled: " + reason + "."); }

    private static void PruneHistory(RebirthWorldProgressionState progression)
    {
        if (progression == null || progression.TeachingHistory.Count <= 128) return;
        // Owned companion Theory has no separate student record: this history entry is its
        // authoritative progression, not disposable cooldown history. Never evict that state.
        List<KeyValuePair<string, RebirthTeachingHistoryRuntimeState>> ordinary = new List<KeyValuePair<string, RebirthTeachingHistoryRuntimeState>>();
        foreach (KeyValuePair<string, RebirthTeachingHistoryRuntimeState> pair in progression.TeachingHistory)
        {
            RebirthTeachingHistoryRuntimeState history = pair.Value;
            if (history != null && (history.StudentStorageKey ?? string.Empty).StartsWith("npc:", StringComparison.OrdinalIgnoreCase)) continue;
            ordinary.Add(pair);
        }
        if (ordinary.Count <= 128) return;
        ordinary.Sort(delegate(KeyValuePair<string, RebirthTeachingHistoryRuntimeState> a, KeyValuePair<string, RebirthTeachingHistoryRuntimeState> b)
        {
            int age = (a.Value != null ? a.Value.LastCompletedUtcTicks : 0L).CompareTo(b.Value != null ? b.Value.LastCompletedUtcTicks : 0L);
            return age != 0 ? age : StringComparer.Ordinal.Compare(a.Key, b.Key);
        });
        for (int i = 0, remove = ordinary.Count - 128; i < remove; ++i)
            progression.TeachingHistory.Remove(ordinary[i].Key);
    }
    private static bool HasTeacherBonus(EntityPlayer player) { return RebirthBackgroundBonusService.HasBonus(player, TeacherBonusId); }
    private static float GetTuning(EntityPlayer player, string key, float fallback)
    {
        RebirthBackgroundBonusDefinition bonus; RebirthBackgroundBonusTuningValue tuning; float parsed;
        if (RebirthBackgroundBonusService.TryGetSignatureBonus(player, out bonus) && bonus != null && bonus.TryGetTuning(key, out tuning) && tuning != null &&
            float.TryParse(tuning.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) return parsed;
        return fallback;
    }

    private static bool IsServerAuthority()
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || !RebirthWorldCharacterRepository.IsServerAuthority || GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.IsRemote()) return false;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance; return c != null && c.IsServer;
    }

    private static void SendSubjects(EntityPlayer instructor, EntityPlayer student, List<string> subjects, long requestId)
    {
        if (instructor == null) return; string name = GetPlayerName(student);
        if (instructor is EntityPlayerLocal) { RebirthTeachingUiService.ReceiveSubjects(requestId, student != null ? student.entityId : -1, name, subjects != null ? subjects.ToArray() : new string[0]); return; }
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c != null && c.IsServer) c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthTeachingSubjectsResponse>().Setup(requestId, student != null ? student.entityId : -1, name, subjects), _attachedToEntityId: instructor.entityId);
    }

    private static void SendOffer(EntityPlayer student, PendingOffer offer, string instructorName)
    {
        if (student == null || offer == null) return;
        if (student is EntityPlayerLocal) { RebirthTeachingOfferUiService.Receive(offer.OfferId, offer.InstructorId, instructorName, offer.SkillId); return; }
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c != null && c.IsServer) c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthTeachingOffer>().Setup(offer.OfferId, offer.InstructorId, instructorName, offer.SkillId), _attachedToEntityId: student.entityId);
    }

    internal static void NotifyStudy(EntityPlayer player,bool success,string message)
    {
        // Feedback failure must never replay or cancel a saved progression effect.
        try{Notify(player,success,message);}catch(Exception ex){Log.Warning("[REBIRTH Study] feedback deferred: "+ex.GetType().Name);}
    }
    private static void Notify(EntityPlayer player, bool success, string message)
    {
        if (player == null || string.IsNullOrEmpty(message)) return;
        if (player is EntityPlayerLocal) { RebirthSurvivorSupportUiFeedback.Receive(success, message); return; }
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c != null && c.IsServer) c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionResult>().Setup(success, message), _attachedToEntityId: player.entityId);
    }

    public static string FriendlySkill(string skillId)
    {
        return RebirthSkillDisplayNames.Get(skillId);
    }

    private static string GetPlayerName(EntityPlayer player)
    {
        if (player == null) return "the other survivor";
        string n = player.EntityName; return string.IsNullOrEmpty(n) ? ("Player " + player.entityId.ToString(CultureInfo.InvariantCulture)) : n;
    }

    private static void ResetRuntime()
    {
        lock (Gate)
        {
            Pending.Clear(); Active.Clear(); LessonPlayers.Clear(); DirtyLessonPlayers.Clear(); NextLessonRevisionPublish.Clear();
        }
        nextLessonPersistenceSave = 0f;
        nextLessonDiscoveryRealtime = 0f;
        nextOutcomeRecoveryRealtime = 0f;
        RebirthTeachingOutcomeStore.Reset();
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { ResetRuntime(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { ResetRuntime(); }
}
