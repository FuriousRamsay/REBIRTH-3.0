using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

#nullable disable

public sealed class RebirthWildAffinityCategoryDefinition
{
    public string Id { get; private set; }
    public int Priority { get; private set; }
    public float ToleranceSkill { get; private set; }
    public float DefenseSkill { get; private set; }
    public ReadOnlyCollection<string> Tags { get; private set; }
    internal FastTags<TagGroup.Global> MatchTags { get; private set; }

    internal RebirthWildAffinityCategoryDefinition(string id, int priority, float toleranceSkill, float defenseSkill, IList<string> tags)
    {
        Id = (id ?? string.Empty).Trim();
        Priority = priority;
        ToleranceSkill = toleranceSkill;
        DefenseSkill = defenseSkill;
        Tags = new ReadOnlyCollection<string>(new List<string>(tags ?? new List<string>()));
        MatchTags = FastTags<TagGroup.Global>.Parse(string.Join(",", Tags));
    }
}

/// <summary>
/// Chunk-C Wild Affinity authority. Eligible living wildlife can tolerate sufficiently trained
/// Rebirth handlers and, at higher mastery, may temporarily defend a player who is attacked.
/// Wildlife remains wild: this service never changes faction, ownership, persistence or capacity.
/// </summary>
public static class RebirthWildAffinityService
{
    private static readonly object Gate = new object();
    private static readonly List<RebirthWildAffinityCategoryDefinition> Categories = new List<RebirthWildAffinityCategoryDefinition>();
    private static readonly HashSet<string> ExcludedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> ExcludedPrefixes = new List<string>();
    private static readonly List<KeyValuePair<string, FastTags<TagGroup.Global>>> ExcludedTags = new List<KeyValuePair<string, FastTags<TagGroup.Global>>>();
    private static readonly List<KeyValuePair<string, FastTags<TagGroup.Global>>> RequiredTags = new List<KeyValuePair<string, FastTags<TagGroup.Global>>>();
    private static readonly Dictionary<long, float> PlayerProvocationUntil = new Dictionary<long, float>();
    private static readonly Dictionary<long, float> DefensiveAssignmentUntil = new Dictionary<long, float>();
    private static readonly Dictionary<int, float> DefenseScanCooldownUntil = new Dictionary<int, float>();

    private static bool ready;
    private static string knowledgeId = RebirthAnimalHandlingService.KnowledgeAnimalBehavior;
    private static float defenseRadius = 24f;
    private static int maxDefenders = 3;
    private static int defenseTargetTicks = 400;
    private static float provocationSeconds = 30f;
    private static float defenseScanCooldownSeconds = 2f;

    private static long targetChecks, suppressedTargets, provocationMarks, defenseScans, defendersAssigned, excludedChecks, faults;

    public static bool IsReady { get { return ready; } }
    public static string KnowledgeId { get { EnsureReady(); return knowledgeId; } }
    public static float DefenseRadius { get { EnsureReady(); return defenseRadius; } }
    public static int MaxDefenders { get { EnsureReady(); return maxDefenders; } }
    public static int DefenseTargetTicks { get { EnsureReady(); return defenseTargetTicks; } }
    public static int CategoryCount { get { EnsureReady(); return Categories.Count; } }

    public static string Install(Harmony harmony)
    {
        string report = LoadDefinitions();
        ClearRuntime();
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWildAffinityAttackTargetPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWildAffinityNearestTargetPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWildAffinityApproachTargetPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWildAffinityDamagePatch));
        return report + " patches=4";
    }

    public static string LoadDefinitions()
    {
        Categories.Clear();
        ExcludedClasses.Clear();
        ExcludedPrefixes.Clear();
        ExcludedTags.Clear();
        RequiredTags.Clear();
        ready = false;

        string path = RebirthAdvancedDisciplineRegistry.SourcePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("Advanced Disciplines configuration is unavailable for Wild Affinity.", path);
        XDocument doc = XDocument.Load(path);
        XElement root = doc.Root;
        XElement wild = root != null ? root.Element("wild_affinity") : null;
        if (wild == null) throw new InvalidDataException("advanced_disciplines.xml is missing the Chunk-C wild_affinity definition.");

        knowledgeId = Attr(wild, "knowledge_id");
        if (knowledgeId.Length == 0) knowledgeId = RebirthAnimalHandlingService.KnowledgeAnimalBehavior;
        defenseRadius = Math.Max(1f, FloatAttr(wild, "defense_radius", 24f));
        maxDefenders = Math.Max(1, IntAttr(wild, "max_defenders", 3));
        defenseTargetTicks = Math.Max(20, IntAttr(wild, "defense_target_ticks", 400));
        provocationSeconds = Math.Max(1f, FloatAttr(wild, "provocation_seconds", 30f));
        defenseScanCooldownSeconds = Math.Max(0f, FloatAttr(wild, "defense_scan_cooldown_seconds", 2f));

        foreach (XElement tag in wild.Elements("required_tag"))
        {
            string id = Attr(tag, "id");
            if (id.Length > 0) RequiredTags.Add(new KeyValuePair<string, FastTags<TagGroup.Global>>(id, FastTags<TagGroup.Global>.Parse(id)));
        }

        XElement exclusions = wild.Element("exclusions");
        if (exclusions != null)
        {
            foreach (XElement e in exclusions.Elements("entity_class")) { string id = Attr(e, "id"); if (id.Length > 0) ExcludedClasses.Add(id); }
            foreach (XElement e in exclusions.Elements("entity_class_prefix")) { string id = Attr(e, "value"); if (id.Length > 0) ExcludedPrefixes.Add(id); }
            foreach (XElement e in exclusions.Elements("tag"))
            {
                string id = Attr(e, "id");
                if (id.Length > 0) ExcludedTags.Add(new KeyValuePair<string, FastTags<TagGroup.Global>>(id, FastTags<TagGroup.Global>.Parse(id)));
            }
        }

        foreach (XElement e in wild.Elements("category"))
        {
            string id = Attr(e, "id");
            List<string> tags = new List<string>();
            foreach (XElement t in e.Elements("tag")) { string tag = Attr(t, "id"); if (tag.Length > 0) tags.Add(tag); }
            Categories.Add(new RebirthWildAffinityCategoryDefinition(id, IntAttr(e, "priority", 100), FloatAttr(e, "tolerance_skill", 0f), FloatAttr(e, "defense_skill", 0f), tags));
        }
        Categories.Sort(delegate(RebirthWildAffinityCategoryDefinition a, RebirthWildAffinityCategoryDefinition b)
        {
            int byPriority = a.Priority.CompareTo(b.Priority);
            return byPriority != 0 ? byPriority : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        });

        List<string> errors = ValidateAuthoring();
        if (errors.Count > 0) throw new InvalidDataException("Wild Affinity authoring is invalid: " + string.Join(" | ", errors.ToArray()));
        ready = true;
        return "wildAffinity categories=" + Categories.Count + " knowledge=" + knowledgeId + " radius=" + defenseRadius.ToString("0.##", CultureInfo.InvariantCulture) + " defenders=" + maxDefenders;
    }

    public static RebirthWildAffinityCategoryDefinition[] GetCategoriesSnapshot()
    {
        EnsureReady();
        return Categories.ToArray();
    }

    public static bool TryClassify(EntityAlive animal, out RebirthWildAffinityCategoryDefinition category, out string reason)
    {
        EnsureReady();
        category = null;
        if (animal == null || animal.EntityClass == null) { reason = "entity-unavailable"; return false; }
        EntityClass ec = animal.EntityClass;
        // Hot-path fast reject: most entities that target players are not animals.
        for (int i = 0; i < RequiredTags.Count; i++)
            if (!ec.Tags.Test_AnySet(RequiredTags[i].Value)) { reason = "missing-required-wildlife-tag"; return false; }

        if (RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded(ec))
        {
            Interlocked.Increment(ref excludedChecks);
            reason = "pc002-hard-exclusion";
            return false;
        }

        string cls = ec.entityClassName ?? string.Empty;
        if (ExcludedClasses.Contains(cls)) { Interlocked.Increment(ref excludedChecks); reason = "authored-class-exclusion"; return false; }
        for (int i = 0; i < ExcludedPrefixes.Count; i++)
            if (cls.StartsWith(ExcludedPrefixes[i], StringComparison.OrdinalIgnoreCase)) { Interlocked.Increment(ref excludedChecks); reason = "authored-prefix-exclusion"; return false; }
        for (int i = 0; i < ExcludedTags.Count; i++)
            if (ec.Tags.Test_AnySet(ExcludedTags[i].Value)) { Interlocked.Increment(ref excludedChecks); reason = "authored-tag-exclusion"; return false; }
        for (int i = 0; i < Categories.Count; i++)
        {
            RebirthWildAffinityCategoryDefinition candidate = Categories[i];
            if (ec.Tags.Test_AnySet(candidate.MatchTags)) { category = candidate; reason = string.Empty; return true; }
        }
        reason = "species-not-authored";
        return false;
    }

    public static bool IsTolerant(EntityAlive animal, EntityPlayer player, out RebirthWildAffinityCategoryDefinition category, out string reason)
    {
        Interlocked.Increment(ref targetChecks);
        category = null;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) { reason = "not-rebirth-mode"; return false; }
        if (animal == null || player == null || player.IsDead()) { reason = "animal-or-player-unavailable"; return false; }
        if (!TryClassify(animal, out category, out reason)) return false;
        float skill = RebirthAnimalHandlingService.GetPlayerSkill(player);
        if (skill + 0.0001f < category.ToleranceSkill) { reason = "skill-below-tolerance"; return false; }
        if (!RebirthKnowledgeService.HasKnowledge(player, knowledgeId)) { reason = "missing-animal-behavior-knowledge"; return false; }
        if (IsProvoked(animal.entityId, player.entityId)) { reason = "player-provoked-animal"; return false; }
        if (animal.GetRevengeTarget() == player) { reason = "animal-revenge-target"; return false; }
        reason = "tolerant";
        return true;
    }

    public static bool ShouldSuppressPlayerTarget(EntityAlive animal, EntityPlayer player, out string reason)
    {
        reason = string.Empty;
        if (!IsAuthoritative() || animal == null || player == null) return false;
        if (IsDefensiveAssignment(animal.entityId, player.entityId)) { reason = "defensive-affinity-assignment"; return false; }
        RebirthWildAffinityCategoryDefinition category;
        bool tolerant = IsTolerant(animal, player, out category, out reason);
        if (tolerant)
        {
            Interlocked.Increment(ref suppressedTargets);
            return true;
        }
        string calmingReason;
        if (RebirthCombatPatrolTrackingSignatureService.ShouldDelayWildlifeHostility(animal, player, out calmingReason))
        {
            reason = calmingReason;
            Interlocked.Increment(ref suppressedTargets);
            return true;
        }
        return false;
    }

    public static void MarkPlayerProvocation(EntityAlive animal, EntityPlayer player)
    {
        if (!IsAuthoritative() || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || animal == null || player == null) return;
        RebirthWildAffinityCategoryDefinition category; string reason;
        if (!TryClassify(animal, out category, out reason)) return;
        float until = Time.realtimeSinceStartup + provocationSeconds;
        lock (Gate) { PlayerProvocationUntil[PairKey(animal.entityId, player.entityId)] = until; }
        RebirthCombatPatrolTrackingSignatureService.MarkWildlifeProvocation(animal, player);
        Interlocked.Increment(ref provocationMarks);
    }

    public static int TriggerDefensiveAffinity(EntityPlayer protectedPlayer, EntityAlive aggressor)
    {
        if (!IsAuthoritative() || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || protectedPlayer == null || protectedPlayer.world == null || protectedPlayer.IsDead() || aggressor == null || aggressor.IsDead()) return 0;
        if (aggressor == protectedPlayer) return 0;
        float now = Time.realtimeSinceStartup;
        lock (Gate)
        {
            float cooldown;
            if (DefenseScanCooldownUntil.TryGetValue(protectedPlayer.entityId, out cooldown) && cooldown > now) return 0;
            DefenseScanCooldownUntil[protectedPlayer.entityId] = now + defenseScanCooldownSeconds;
        }
        Interlocked.Increment(ref defenseScans);

        World world = protectedPlayer.world;
        List<Entity> found = world.GetEntitiesInBounds(typeof(EntityAlive), new Bounds(protectedPlayer.position, Vector3.one * (defenseRadius * 2f)), new List<Entity>());
        List<EntityAlive> candidates = new List<EntityAlive>();
        for (int i = 0; i < found.Count; i++)
        {
            EntityAlive animal = found[i] as EntityAlive;
            if (animal == null || animal == protectedPlayer || animal == aggressor || animal.IsDead()) continue;
            RebirthWildAffinityCategoryDefinition category; string reason;
            if (!IsTolerant(animal, protectedPlayer, out category, out reason)) continue;
            float skill = RebirthAnimalHandlingService.GetPlayerSkill(protectedPlayer);
            if (skill + 0.0001f < category.DefenseSkill) continue;
            EntityAlive revenge = animal.GetRevengeTarget();
            EntityAlive attack = animal.GetAttackTarget();
            if (revenge != null && revenge != aggressor) continue;
            if (attack != null && attack != aggressor) continue;
            candidates.Add(animal);
        }
        candidates.Sort(delegate(EntityAlive a, EntityAlive b)
        {
            return (a.position - protectedPlayer.position).sqrMagnitude.CompareTo((b.position - protectedPlayer.position).sqrMagnitude);
        });

        int assigned = 0;
        for (int i = 0; i < candidates.Count && assigned < maxDefenders; i++)
        {
            EntityAlive animal = candidates[i];
            lock (Gate) { DefensiveAssignmentUntil[PairKey(animal.entityId, aggressor.entityId)] = now + Math.Max(2f, defenseTargetTicks / 20f); }
            animal.SetAttackTarget(aggressor, defenseTargetTicks);
            assigned++;
            Interlocked.Increment(ref defendersAssigned);
        }
        CleanupExpired(now);
        return assigned;
    }

    public static string BuildDebugSummary(EntityPlayer player, int animalEntityId)
    {
        EnsureReady();
        StringBuilder b = new StringBuilder(900);
        b.Append("[REBIRTH WildAffinity] ready=").Append(ready)
         .Append(" rebirthMode=").Append(RebirthSurvivorMode.IsEnabledForCurrentWorld())
         .Append(" knowledge=").Append(knowledgeId)
         .Append(" skill=").Append(RebirthAnimalHandlingService.GetPlayerSkill(player).ToString("0.##", CultureInfo.InvariantCulture))
         .Append(" categories=").Append(Categories.Count)
         .Append(" defenseRadius=").Append(defenseRadius.ToString("0.##", CultureInfo.InvariantCulture))
         .Append(" maxDefenders=").Append(maxDefenders)
         .Append(" counters{checks=").Append(Interlocked.Read(ref targetChecks))
         .Append(" suppressed=").Append(Interlocked.Read(ref suppressedTargets))
         .Append(" provoked=").Append(Interlocked.Read(ref provocationMarks))
         .Append(" scans=").Append(Interlocked.Read(ref defenseScans))
         .Append(" defenders=").Append(Interlocked.Read(ref defendersAssigned))
         .Append(" excluded=").Append(Interlocked.Read(ref excludedChecks))
         .Append(" faults=").Append(Interlocked.Read(ref faults)).Append('}');
        for (int i = 0; i < Categories.Count; i++)
        {
            RebirthWildAffinityCategoryDefinition c = Categories[i];
            b.Append("\n  ").Append(c.Id).Append(" tolerance=").Append(c.ToleranceSkill.ToString("0.##", CultureInfo.InvariantCulture))
             .Append(" defense=").Append(c.DefenseSkill.ToString("0.##", CultureInfo.InvariantCulture)).Append(" tags=").Append(string.Join(",", c.Tags));
        }
        if (animalEntityId > 0 && player != null && player.world != null)
        {
            EntityAlive animal = player.world.GetEntity(animalEntityId) as EntityAlive;
            if (animal == null) b.Append("\n  animal=").Append(animalEntityId).Append(" unavailable");
            else
            {
                RebirthWildAffinityCategoryDefinition c; string reason;
                bool tolerant = IsTolerant(animal, player, out c, out reason);
                b.Append("\n  animal=").Append(animal.entityId).Append(" class=").Append(animal.EntityClass != null ? animal.EntityClass.entityClassName : "<none>")
                 .Append(" category=").Append(c != null ? c.Id : "none").Append(" tolerant=").Append(tolerant ? "true" : "false").Append(" reason=").Append(reason);
                if (c != null)
                {
                    float skill = RebirthAnimalHandlingService.GetPlayerSkill(player);
                    bool defenseEligible = tolerant && skill + 0.0001f >= c.DefenseSkill;
                    b.Append(" defenseEligible=").Append(defenseEligible ? "true" : "false")
                     .Append(" defenseReason=").Append(!tolerant ? "not-tolerant" : (defenseEligible ? "eligible" : ("Animal Handling " + skill.ToString("0.##", CultureInfo.InvariantCulture) + " < defense " + c.DefenseSkill.ToString("0.##", CultureInfo.InvariantCulture))));
                }
            }
        }
        return b.ToString();
    }

    public static string RunVectors()
    {
        EnsureReady();
        List<string> failures = ValidateAuthoring();
        RebirthWildAffinityCategoryDefinition coyote = FindCategory("coyote"), wolf = FindCategory("wolf"), advanced = FindCategory("advanced_wolf"), lion = FindCategory("mountain_lion"), bear = FindCategory("bear");
        if (coyote == null || wolf == null || advanced == null || lion == null || bear == null) failures.Add("required living-wildlife categories are missing");
        if (coyote != null && wolf != null && !(coyote.ToleranceSkill < wolf.ToleranceSkill)) failures.Add("coyote must precede wolf tolerance");
        if (wolf != null && advanced != null && !(wolf.ToleranceSkill < advanced.ToleranceSkill)) failures.Add("wolf must precede advanced wolf tolerance");
        if (advanced != null && lion != null && !(advanced.ToleranceSkill < lion.ToleranceSkill)) failures.Add("advanced wolf must precede mountain lion tolerance");
        if (lion != null && bear != null && !(lion.ToleranceSkill < bear.ToleranceSkill)) failures.Add("mountain lion must precede bear tolerance");
        string hardReason;
        if (!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded("animalZombieDog", null, out hardReason)) failures.Add("PC002 zombie dog exclusion missing");
        if (!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded("animalZombieBear", null, out hardReason)) failures.Add("PC002 zombie bear exclusion missing");
        if (!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded("animalDireWolf", null, out hardReason)) failures.Add("PC002 dire wolf exclusion missing");
        if (!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded("animalBossGrace", null, out hardReason)) failures.Add("PC002 Grace exclusion missing");
        if (!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded("futureAnimal", new string[] { "zombieAnimal" }, out hardReason)) failures.Add("PC002 zombieAnimal-tag exclusion missing");
        return "[REBIRTH WildAffinity] vectors=" + (failures.Count == 0 ? "PASS" : "FAIL") + " failures=" + failures.Count + (failures.Count == 0 ? string.Empty : " " + string.Join(" | ", failures.ToArray()));
    }

    public static void ClearRuntime()
    {
        lock (Gate) { PlayerProvocationUntil.Clear(); DefensiveAssignmentUntil.Clear(); DefenseScanCooldownUntil.Clear(); }
        Interlocked.Exchange(ref targetChecks, 0); Interlocked.Exchange(ref suppressedTargets, 0); Interlocked.Exchange(ref provocationMarks, 0);
        Interlocked.Exchange(ref defenseScans, 0); Interlocked.Exchange(ref defendersAssigned, 0); Interlocked.Exchange(ref excludedChecks, 0); Interlocked.Exchange(ref faults, 0);
    }

    internal static void RecordFault(Exception ex)
    {
        Interlocked.Increment(ref faults);
        if (RebirthSurvivorDebug.Enabled) Log.Warning("[REBIRTH WildAffinity] hook fault: " + ex.GetType().Name + ": " + ex.Message);
    }

    private static bool IsProvoked(int animalId, int playerId)
    {
        float now = Time.realtimeSinceStartup;
        lock (Gate)
        {
            float until;
            if (!PlayerProvocationUntil.TryGetValue(PairKey(animalId, playerId), out until)) return false;
            if (until > now) return true;
            PlayerProvocationUntil.Remove(PairKey(animalId, playerId));
            return false;
        }
    }

    private static bool IsDefensiveAssignment(int animalId, int targetId)
    {
        float now = Time.realtimeSinceStartup;
        lock (Gate)
        {
            float until;
            long key = PairKey(animalId, targetId);
            if (!DefensiveAssignmentUntil.TryGetValue(key, out until)) return false;
            if (until > now) return true;
            DefensiveAssignmentUntil.Remove(key);
            return false;
        }
    }

    private static void CleanupExpired(float now)
    {
        lock (Gate)
        {
            Cleanup(PlayerProvocationUntil, now);
            Cleanup(DefensiveAssignmentUntil, now);
            List<int> expired = null;
            foreach (KeyValuePair<int, float> pair in DefenseScanCooldownUntil) if (pair.Value <= now) { if (expired == null) expired = new List<int>(); expired.Add(pair.Key); }
            if (expired != null) for (int i = 0; i < expired.Count; i++) DefenseScanCooldownUntil.Remove(expired[i]);
        }
    }

    private static void Cleanup(Dictionary<long, float> map, float now)
    {
        List<long> expired = null;
        foreach (KeyValuePair<long, float> pair in map) if (pair.Value <= now) { if (expired == null) expired = new List<long>(); expired.Add(pair.Key); }
        if (expired != null) for (int i = 0; i < expired.Count; i++) map.Remove(expired[i]);
    }

    private static long PairKey(int left, int right) { return ((long)left << 32) ^ (uint)right; }

    private static bool IsAuthoritative()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote()) return false;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return connection != null && connection.IsServer;
    }

    private static RebirthWildAffinityCategoryDefinition FindCategory(string id)
    {
        for (int i = 0; i < Categories.Count; i++) if (string.Equals(Categories[i].Id, id, StringComparison.OrdinalIgnoreCase)) return Categories[i];
        return null;
    }

    private static List<string> ValidateAuthoring()
    {
        List<string> errors = new List<string>();
        if (Categories.Count == 0) errors.Add("no Wild Affinity categories authored");
        if (RequiredTags.Count == 0) errors.Add("Wild Affinity must require living hostile animal tags");
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Categories.Count; i++)
        {
            RebirthWildAffinityCategoryDefinition c = Categories[i];
            if (c.Id.Length == 0) errors.Add("category with empty id");
            else if (!ids.Add(c.Id)) errors.Add("duplicate category " + c.Id);
            if (c.Tags.Count == 0) errors.Add(c.Id + " has no authored match tags");
            if (c.ToleranceSkill < 0f || c.ToleranceSkill > 100f) errors.Add(c.Id + " tolerance_skill outside 0..100");
            if (c.DefenseSkill < c.ToleranceSkill || c.DefenseSkill > 100f) errors.Add(c.Id + " defense_skill must be >= tolerance and <=100");
            if (c.Id.IndexOf("zombie", StringComparison.OrdinalIgnoreCase) >= 0 || c.Id.IndexOf("infect", StringComparison.OrdinalIgnoreCase) >= 0 || c.Id.IndexOf("undead", StringComparison.OrdinalIgnoreCase) >= 0) errors.Add(c.Id + " violates PC002");
        }
        if (maxDefenders < 1 || maxDefenders > 8) errors.Add("max_defenders must be 1..8");
        if (defenseRadius > 50f) errors.Add("defense_radius exceeds bounded Chunk-C maximum 50m");
        if (!string.Equals(knowledgeId, RebirthAnimalHandlingService.KnowledgeAnimalBehavior, StringComparison.OrdinalIgnoreCase)) errors.Add("Chunk C must use the existing Animal Behavior knowledge milestone");
        return errors;
    }

    private static void EnsureReady() { if (!ready) LoadDefinitions(); }
    private static string Attr(XElement e, string name) { return e == null ? string.Empty : ((string)e.Attribute(name) ?? string.Empty).Trim(); }
    private static int IntAttr(XElement e, string name, int fallback) { int value; return int.TryParse(Attr(e, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback; }
    private static float FloatAttr(XElement e, string name, float fallback) { float value; return float.TryParse(Attr(e, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback; }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetAttackTarget))]
internal static class RebirthWildAffinityAttackTargetPatch
{
    private static bool Prefix(EntityAlive __instance, EntityAlive _attackTarget)
    {
        try
        {
            EntityPlayer player = _attackTarget as EntityPlayer;
            if (player == null) return true;
            string reason;
            return !RebirthWildAffinityService.ShouldSuppressPlayerTarget(__instance, player, out reason);
        }
        catch (Exception ex) { RebirthWildAffinityService.RecordFault(ex); return true; }
    }
}

[HarmonyPatch(typeof(EAISetNearestEntityAsTarget), nameof(EAISetNearestEntityAsTarget.CanExecute))]
internal static class RebirthWildAffinityNearestTargetPatch
{
    private static void Postfix(EAISetNearestEntityAsTarget __instance, ref bool __result)
    {
        try
        {
            if (!__result || __instance == null || __instance.theEntity == null) return;
            EntityPlayer player = __instance.targetEntity as EntityPlayer;
            if (player == null) return;
            string reason;
            if (!RebirthWildAffinityService.ShouldSuppressPlayerTarget(__instance.theEntity, player, out reason)) return;
            if (__instance.theEntity.GetAttackTarget() == player) __instance.theEntity.SetAttackTarget(null, 0);
            __instance.targetEntity = null;
            __instance.closeTargetEntity = null;
            __instance.targetPlayer = null;
            __result = false;
        }
        catch (Exception ex) { RebirthWildAffinityService.RecordFault(ex); }
    }
}

[HarmonyPatch(typeof(EAIApproachAndAttackTarget), nameof(EAIApproachAndAttackTarget.CanExecute))]
internal static class RebirthWildAffinityApproachTargetPatch
{
    private static void Postfix(EAIApproachAndAttackTarget __instance, ref bool __result)
    {
        try
        {
            if (!__result || __instance == null || __instance.theEntity == null) return;
            EntityPlayer player = __instance.theEntity.GetAttackTarget() as EntityPlayer;
            if (player == null) return;
            string reason;
            if (!RebirthWildAffinityService.ShouldSuppressPlayerTarget(__instance.theEntity, player, out reason)) return;
            __instance.theEntity.SetAttackTarget(null, 0);
            __result = false;
        }
        catch (Exception ex) { RebirthWildAffinityService.RecordFault(ex); }
    }
}

[HarmonyPatch(typeof(EntityAlive), "damageEntityLocal", new Type[] { typeof(DamageSource), typeof(int), typeof(bool), typeof(float) })]
internal static class RebirthWildAffinityDamagePatch
{
    private static void Prefix(EntityAlive __instance, DamageSource _damageSource)
    {
        try
        {
            if (__instance == null || _damageSource == null || __instance.world == null) return;
            EntityPlayer attacker = __instance.world.GetEntity(_damageSource.getEntityId()) as EntityPlayer;
            if (attacker != null && attacker != __instance) RebirthWildAffinityService.MarkPlayerProvocation(__instance, attacker);
        }
        catch (Exception ex) { RebirthWildAffinityService.RecordFault(ex); }
    }

    private static void Postfix(EntityAlive __instance, DamageSource _damageSource, DamageResponse __result)
    {
        try
        {
            EntityPlayer protectedPlayer = __instance as EntityPlayer;
            if (protectedPlayer == null || __result.Strength <= 0 || _damageSource == null || protectedPlayer.world == null) return;
            EntityAlive aggressor = protectedPlayer.world.GetEntity(_damageSource.getEntityId()) as EntityAlive;
            if (aggressor == null || aggressor == protectedPlayer) return;
            RebirthWildAffinityService.TriggerDefensiveAffinity(protectedPlayer, aggressor);
        }
        catch (Exception ex) { RebirthWildAffinityService.RecordFault(ex); }
    }
}
