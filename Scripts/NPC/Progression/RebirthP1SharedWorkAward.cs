using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

#nullable disable

internal enum RebirthP1AwardQueueResult { Acknowledged, Journaled, Invalid, Unavailable }

// Single entry for real committed NPC work to advance the human's SHARED skill, and for B13 owner awards.
// Callers pass a durable receipt unique to the physical action. A failed attempt is journaled in the NPC aggregate
// (optional element p1PendingAwards) and drained on Tick until a positive once-only acknowledgement: correctness never
// depends on an attempt count. Replay of an acknowledged receipt is a no-op (RebirthHumanProgression.Award / the B13 helper).
internal static class RebirthP1SharedWorkAward
{
    private const string Element = "p1PendingAwards";
    private const int MaxEntries = 256;
    private static readonly HashSet<RebirthNpcStableId> Hinted = new HashSet<RebirthNpcStableId>();
    private static World scope;
    private static float nextDrain;
    private static float nextLog;
    private static int cursor;

    // Called only when the bounded outcome log needs an eviction. Scan the
    // authoritative NPC aggregate rather than a process-local hint: an offline
    // owner's saved award intent must still protect its exact outcome after a
    // restart. Failure is not an empty set; the caller must retain the log.
    internal static bool TryGetPendingOwnerOutcomeIds(out HashSet<Guid> pending)
    {
        pending = new HashSet<Guid>();
        if(!ThreadManager.IsMainThread())return false;
        try
        {
            var people = RebirthNpcAggregatePersistenceStore.Snapshot();
            if(!RebirthNpcAggregatePersistenceStore.IsRecoveryBackingLoaded)return false;
            foreach(var person in people)
                foreach(string raw in person.UnknownOptionalElements ?? new string[0])
                {
                    var node = RebirthCompanionDeskStore.Parse(raw);
                    if(node.Name != Element)continue;
                    foreach(var entry in node.Elements("owner"))
                    {
                        if(!Guid.TryParseExact((string)entry.Attribute("outcome") ?? "", "N", out var id))
                            return false;
                        pending.Add(id);
                    }
                }
            return true;
        }
        catch(Exception){pending.Clear();return false;}
    }

    // Direct attempt, no journal (for callers that own their own retry).
    internal static bool TryAward(RebirthNpcStableId npc, string skillId, string durableReceiptId, float rawAward, string sourceKey, string description)
    {
        if(!Valid(npc, skillId, durableReceiptId, rawAward, description) || !ThreadManager.IsMainThread()) return false;
        return RebirthHumanProgression.Award(npc, Evidence(skillId, durableReceiptId, rawAward, sourceKey, description));
    }
    private static bool Valid(RebirthNpcStableId npc, string skillId, string receipt, float raw, string description) =>
        !npc.IsEmpty && !string.IsNullOrEmpty(skillId) && !string.IsNullOrEmpty(receipt) && !string.IsNullOrWhiteSpace(description) &&
        !float.IsNaN(raw) && !float.IsInfinity(raw) && raw > 0f;
    private static RebirthSkillTrainingEvidence Evidence(string skillId, string receipt, float raw, string sourceKey, string description) => new RebirthSkillTrainingEvidence
    {
        SkillId = skillId, SourceKey = string.IsNullOrEmpty(sourceKey) ? "npc:work:" + skillId : sourceKey, DurableReceiptId = receipt,
        ReferenceDescription = description, AuthoritativeSuccess = true,
        Mode = RebirthSkillTrainingEvidenceMode.DiscreteAward, CreditedWork = raw, DiscreteRawAward = raw
    };

    // Contract for P7 (AwardCraft): Acknowledged or Journaled both mean the award can no longer be lost, so mark awarded=1.
    // Unavailable (aggregate or save temporarily unwritable): leave the row un-awarded and call again; custody must not wait on it.
    // Invalid: the arguments can never qualify; skip the award.
    internal static RebirthP1AwardQueueResult QueueAward(RebirthNpcStableId npc, string skillId, string durableReceiptId, float rawAward, string sourceKey, string description)
    {
        if(!Valid(npc, skillId, durableReceiptId, rawAward, description) || !RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out _)) return RebirthP1AwardQueueResult.Invalid;
        if(!ThreadManager.IsMainThread()) return RebirthP1AwardQueueResult.Unavailable;
        try { if(RebirthHumanProgression.Award(npc, Evidence(skillId, durableReceiptId, rawAward, sourceKey, description))) return RebirthP1AwardQueueResult.Acknowledged; }
        catch(Exception) { }
        return Add(npc, new XElement("npc", new XAttribute("receipt", durableReceiptId), new XAttribute("skill", skillId),
            new XAttribute("raw", rawAward.ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute("source", sourceKey ?? ""), new XAttribute("desc", description)), "receipt", durableReceiptId);
    }

    // Continuous variant for work whose executor measured real effort (e.g. P12 mining/logging: block damage dealt over the NPC's
    // live sustained rate, the same shape as the player's resource LBD). creditedWork and liveWorkRate must be action-derived
    // from the saved strike receipt; the framework computes equivalent seconds = creditedWork / liveWorkRate and caps it.
    internal static RebirthP1AwardQueueResult QueueContinuousAward(RebirthNpcStableId npc, string skillId, string durableReceiptId, float creditedWork, float liveWorkRate, string sourceKey, string description)
    {
        if(!Valid(npc, skillId, durableReceiptId, creditedWork, description) || float.IsNaN(liveWorkRate) || float.IsInfinity(liveWorkRate) || liveWorkRate <= 0f ||
           !RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out _)) return RebirthP1AwardQueueResult.Invalid;
        if(!ThreadManager.IsMainThread()) return RebirthP1AwardQueueResult.Unavailable;
        try { if(RebirthHumanProgression.Award(npc, ContinuousEvidence(skillId, durableReceiptId, creditedWork, liveWorkRate, sourceKey, description))) return RebirthP1AwardQueueResult.Acknowledged; }
        catch(Exception) { }
        return Add(npc, new XElement("npc", new XAttribute("receipt", durableReceiptId), new XAttribute("skill", skillId),
            new XAttribute("raw", creditedWork.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("rate", liveWorkRate.ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute("source", sourceKey ?? ""), new XAttribute("desc", description)), "receipt", durableReceiptId);
    }
    private static RebirthSkillTrainingEvidence ContinuousEvidence(string skillId, string receipt, float work, float rate, string sourceKey, string description)
    {
        var evidence = Evidence(skillId, receipt, work, sourceKey, description);
        evidence.Mode = RebirthSkillTrainingEvidenceMode.ContinuousWork;
        evidence.LiveWorkRate = rate;
        evidence.DiscreteRawAward = 0f;
        return evidence;
    }

    // Contract for P6 (Offer): call once per recorded outcome, whatever the pickup/phase state. The intent stores only the
    // outcome identity; RebirthP1B13Policy decides Ready/NotYet/Never at drain time from retained, saved evidence.
    // Journaled = durable; Unavailable = call again later (P6 keeps no list of its own beyond that retry).
    internal static RebirthP1AwardQueueResult QueueOwnerAward(RebirthNpcWorkOutcomeRecord outcome)
    {
        if(outcome == null || outcome.OutcomeId == Guid.Empty || outcome.NpcId.IsEmpty) return RebirthP1AwardQueueResult.Invalid;
        if(!ThreadManager.IsMainThread()) return RebirthP1AwardQueueResult.Unavailable;
        RebirthP1OwnerAwardPlan plan;
        if(RebirthP1B13Policy.Resolve(outcome.OutcomeId, out plan) == RebirthP1OwnerAwardState.Never) return RebirthP1AwardQueueResult.Invalid;
        return Add(outcome.NpcId, OwnerEntry(outcome, 0f), "outcome", outcome.OutcomeId.ToString("N"));
    }

    // effort="0" means the executor has not supplied its action-derived metric yet; Drain then waits (never invents one).
    private static XElement OwnerEntry(RebirthNpcWorkOutcomeRecord outcome, float effortSeconds) =>
        new XElement("owner", new XAttribute("outcome", outcome.OutcomeId.ToString("N")), new XAttribute("assignment", outcome.AssignmentId.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("done", ""), new XAttribute("effort", effortSeconds.ToString("R", CultureInfo.InvariantCulture)));

    // Contract for the physical executors (P7 pickup, P12 strike, P8 refill, P10 relocation/deposit). After the outcome is
    // recorded call EXACTLY ONE of these, and only with a truthful saved witness. Both create the intent when absent; the
    // first supplied evidence kind is immutable. Never assignment wall time, never a placeholder constant.
    //  SupplyOwnerMetric: the saved native duration (seconds) of THIS single action (craft time, summed strike time, ...).
    //    effort caps CREDIT at RebirthP1B13Policy.MaxCreditSeconds and claims nothing about how long the job lasted.
    //  SupplyOwnerDiscrete: the action is a saved one-shot completion with no truthful duration (a relocation, a refill
    //    without a saved duration): awarded once as a discrete, receipt-backed raw award.
    internal static RebirthP1AwardQueueResult SupplyOwnerMetric(RebirthNpcWorkOutcomeRecord outcome, float effortSeconds)
    {
        if(float.IsNaN(effortSeconds) || float.IsInfinity(effortSeconds) || effortSeconds <= 0f) return RebirthP1AwardQueueResult.Invalid;
        return Supply(outcome, Math.Min(RebirthP1B13Policy.MaxCreditSeconds, effortSeconds), false);
    }
    internal static RebirthP1AwardQueueResult SupplyOwnerDiscrete(RebirthNpcWorkOutcomeRecord outcome) => Supply(outcome, 0f, true);

    private static RebirthP1AwardQueueResult Supply(RebirthNpcWorkOutcomeRecord outcome, float effortSeconds, bool discrete)
    {
        if(outcome == null || outcome.OutcomeId == Guid.Empty || outcome.NpcId.IsEmpty) return RebirthP1AwardQueueResult.Invalid;
        if(!ThreadManager.IsMainThread()) return RebirthP1AwardQueueResult.Unavailable;
        try
        {
            RebirthNpcPersistentRecord person;
            if(!RebirthNpcAggregatePersistenceStore.TryGet(outcome.NpcId, out person)) return RebirthP1AwardQueueResult.Unavailable;
            var existing = Read(person);
            var journal = existing == null ? new XElement(Element, new XAttribute("schema", 1)) : new XElement(existing);
            string key = outcome.OutcomeId.ToString("N");
            var entry = journal.Elements("owner").FirstOrDefault(e => (string)e.Attribute("outcome") == key);
            if(entry == null)
            {
                if(journal.Elements().Count() >= MaxEntries) return RebirthP1AwardQueueResult.Unavailable;
                entry = OwnerEntry(outcome, 0f);
                journal.Add(entry);
            }
            float have;
            if(((string)entry.Attribute("discrete")) == "1" || float.TryParse((string)entry.Attribute("effort"), NumberStyles.Float, CultureInfo.InvariantCulture, out have) && have > 0f)
                return RebirthP1AwardQueueResult.Journaled;
            if(discrete) entry.SetAttributeValue("discrete", "1");
            else entry.SetAttributeValue("effort", effortSeconds.ToString("R", CultureInfo.InvariantCulture));
            if(!Write(outcome.NpcId, journal)) return RebirthP1AwardQueueResult.Unavailable;
            Hinted.Add(outcome.NpcId);
            return RebirthP1AwardQueueResult.Journaled;
        }
        catch(Exception) { return RebirthP1AwardQueueResult.Unavailable; }
    }

    // Retention hook for P6's assignment history (TrimHistoryLocked, MaxHistory=2048): the B13 helper binds to the ORIGINAL
    // assignment, so every assignment id listed here must be exempt from trimming until its journal entry is acknowledged.
    // Same contract as TryGetPendingOwnerOutcomeIds: false means unknown, so the caller must retain everything.
    internal static bool TryGetPendingOwnerAssignmentIds(out HashSet<ulong> pending)
    {
        pending = new HashSet<ulong>();
        if(!ThreadManager.IsMainThread()) return false;
        try
        {
            var people = RebirthNpcAggregatePersistenceStore.Snapshot();
            if(!RebirthNpcAggregatePersistenceStore.IsRecoveryBackingLoaded) return false;
            foreach(var person in people)
                foreach(string raw in person.UnknownOptionalElements ?? new string[0])
                {
                    var node = RebirthCompanionDeskStore.Parse(raw);
                    if(node.Name != Element) continue;
                    foreach(var entry in node.Elements("owner"))
                    {
                        ulong assignment;
                        // Entries saved without an assignment id cannot be bound; the outcome pin still applies to them.
                        if(ulong.TryParse((string)entry.Attribute("assignment"), NumberStyles.None, CultureInfo.InvariantCulture, out assignment)) pending.Add(assignment);
                    }
                }
            return true;
        }
        catch(Exception) { pending.Clear(); return false; }
    }

    private static XElement Read(RebirthNpcPersistentRecord person)
    {
        foreach(string raw in person.UnknownOptionalElements ?? new string[0])
        { var node = RebirthCompanionDeskStore.Parse(raw); if(node.Name == Element) return node; }
        return null;
    }
    private static bool Write(RebirthNpcStableId id, XElement next)
    {
        RebirthNpcPersistentRecord person;
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out person)) return false;
        bool ok = RebirthNpcAggregatePersistenceStore.TryMutateExisting(id, person.AggregateRevision, c =>
        {
            var rest = (c.UnknownOptionalElements ?? new string[0]).Where(raw => RebirthCompanionDeskStore.Parse(raw).Name != Element);
            c.UnknownOptionalElements = (next.HasElements ? rest.Concat(new[] { next.ToString(SaveOptions.DisableFormatting) }) : rest).ToArray();
            return true;
        });
        if(!ok) return false;
        RebirthNpcAggregatePersistenceStore.Save();
        RebirthNpcPersistentRecord saved;
        if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id, out saved)) return false;
        var actual = Read(saved);
        return next.HasElements ? actual != null && XNode.DeepEquals(actual, next) : actual == null;
    }
    private static RebirthP1AwardQueueResult Add(RebirthNpcStableId id, XElement entry, string key, string value)
    {
        try
        {
            RebirthNpcPersistentRecord person;
            if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out person)) return RebirthP1AwardQueueResult.Unavailable;
            var existing = Read(person);
            var journal = existing == null ? new XElement(Element, new XAttribute("schema", 1)) : new XElement(existing);
            if(journal.Elements().Any(e => e.Name == entry.Name && (string)e.Attribute(key) == value)) { Hinted.Add(id); return RebirthP1AwardQueueResult.Journaled; }
            if(journal.Elements().Count() >= MaxEntries) return RebirthP1AwardQueueResult.Unavailable;
            journal.Add(entry);
            if(!Write(id, journal)) return RebirthP1AwardQueueResult.Unavailable;
            Hinted.Add(id);
            return RebirthP1AwardQueueResult.Journaled;
        }
        catch(Exception) { return RebirthP1AwardQueueResult.Unavailable; }
    }

    // Called from RebirthHumanProgression's per-NPC recovery so a reload re-arms the drain.
    internal static void Recover(RebirthNpcStableId id)
    {
        try
        {
            RebirthNpcPersistentRecord person;
            if(RebirthNpcAggregatePersistenceStore.TryGet(id, out person) && Read(person) != null) Hinted.Add(id);
        }
        catch(Exception) { }
    }

    internal static void Tick()
    {
        var world = GameManager.Instance?.World;
        if(world == null || world.IsRemote() || !ThreadManager.IsMainThread() || RebirthNpcPersistenceCoordinator.IsRecoveryInstalling) return;
        if(!ReferenceEquals(scope, world)) { scope = world; Hinted.Clear(); cursor = 0; nextDrain = 0f; }
        if(Hinted.Count == 0 || Time.unscaledTime < nextDrain) return;
        nextDrain = Time.unscaledTime + 2f;
        var ids = Hinted.ToArray();
        for(int n = 0; n < Math.Min(2, ids.Length); n++)
        {
            var id = ids[(cursor + n) % ids.Length];
            try { if(Drain(id)) Hinted.Remove(id); }
            catch(Exception error)
            {
                // The attempt count never gates correctness; throttle only the log.
                if(Time.unscaledTime >= nextLog) { nextLog = Time.unscaledTime + 60f; Log.Warning("[REBIRTH P1 Awards] journal drain retained: " + error.Message); }
            }
        }
        cursor++;
    }

    // true when the journal is empty (nothing left to hint).
    private static bool Drain(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecord person;
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out person)) return false;
        var journal = Read(person);
        if(journal == null) return true;
        var next = new XElement(journal);
        bool changed = false;
        foreach(var entry in next.Elements().ToArray())
        {
            if(entry.Name == "npc")
            {
                float raw;
                string skill = (string)entry.Attribute("skill"), receipt = (string)entry.Attribute("receipt"), desc = (string)entry.Attribute("desc");
                if(!float.TryParse((string)entry.Attribute("raw"), NumberStyles.Float, CultureInfo.InvariantCulture, out raw) || !Valid(id, skill, receipt, raw, desc))
                { entry.Remove(); changed = true; continue; }
                // Award returns true for a fresh credit and for an already-consumed receipt: the positive once-only acknowledgement.
                float rate;
                bool continuous = float.TryParse((string)entry.Attribute("rate"), NumberStyles.Float, CultureInfo.InvariantCulture, out rate) && rate > 0f;
                var drained = continuous ? ContinuousEvidence(skill, receipt, raw, rate, (string)entry.Attribute("source"), desc) : Evidence(skill, receipt, raw, (string)entry.Attribute("source"), desc);
                if(RebirthHumanProgression.Award(id, drained)) { entry.Remove(); changed = true; }
            }
            else if(entry.Name == "owner")
            {
                Guid outcomeId;
                if(!Guid.TryParseExact((string)entry.Attribute("outcome") ?? "", "N", out outcomeId)) { entry.Remove(); changed = true; continue; }
                RebirthP1OwnerAwardPlan plan;
                var state = RebirthP1B13Policy.Resolve(outcomeId, out plan);
                if(state == RebirthP1OwnerAwardState.Never) { entry.Remove(); changed = true; continue; }
                if(state != RebirthP1OwnerAwardState.Ready) continue;
                var done = new HashSet<string>(((string)entry.Attribute("done") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
                // Evidence kind is whatever the executor truthfully saved. effort > 0: continuous (framework equivalent seconds =
                // CreditedWork / LiveWorkRate, so rate = credited / effort reproduces the supplied effort; effort only caps credit).
                // discrete = "1" (or a relocation, whose saved move is inherently one-shot): one receipt-backed discrete raw award.
                // Neither supplied: wait. Nothing is invented.
                float effort;
                if(!float.TryParse((string)entry.Attribute("effort"), NumberStyles.Float, CultureInfo.InvariantCulture, out effort) || float.IsNaN(effort) || float.IsInfinity(effort) || effort < 0f)
                    effort = 0f;
                effort = Math.Min(RebirthP1B13Policy.MaxCreditSeconds, effort);
                bool discrete = effort <= 0f && ((string)entry.Attribute("discrete") == "1" || plan.ImplicitDiscrete);
                if(effort <= 0f && !discrete) continue;
                foreach(var item in plan.Items)
                {
                    if(done.Contains(item.SkillId)) continue;
                    float applied;
                    // The helper's per-outcome/per-skill receipt is the once-only guard; false = owner offline or outcome not yet primary-saved.
                    bool ok = discrete
                        ? RebirthB13OwnerWorkAwards.TryAwardCommittedDiscrete(plan.Outcome, item.SkillId, RebirthP1B13Policy.DiscreteRawPerReceipt, item.Description, out applied)
                        : RebirthB13OwnerWorkAwards.TryAwardCommitted(plan.Outcome, item.SkillId, item.CreditedWork, item.CreditedWork / effort, item.Description, out applied);
                    if(ok) done.Add(item.SkillId);
                }
                string doneText = string.Join(",", done.OrderBy(s => s, StringComparer.Ordinal).ToArray());
                if(plan.Items.All(i => done.Contains(i.SkillId))) { entry.Remove(); changed = true; }
                else if(doneText != (string)entry.Attribute("done")) { entry.SetAttributeValue("done", doneText); changed = true; }
            }
            else { entry.Remove(); changed = true; }
        }
        if(!changed) return !next.HasElements;
        // If the save does not read back the old journal stays; the awards are idempotent so a repeat is harmless.
        if(!Write(id, next)) return false;
        return !next.HasElements;
    }
}
