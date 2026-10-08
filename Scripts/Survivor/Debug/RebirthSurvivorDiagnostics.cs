using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#nullable disable

/// <summary>
/// Manual consolidated observations. EnsureDefinitions may initialize definitions and
/// repository reads may perform existing lazy recovery. This is not a side-effect-free
/// API. It does not deliberately edit profiles, grant progression or commit a creator.
/// </summary>
public static class RebirthSurvivorDiagnostics
{
    private sealed class Context
    {
        public EntityPlayerLocal LocalPlayer;
        public RebirthWorldCharacterRecord ServerRecord;
        public RebirthSurvivorOwnerStateSnapshot ClientSnapshot;
        public RebirthSurvivorCreatorViewModel Creator;
        public bool CreatorNextVisible;
        public bool CreatorNextEnabled;
        public string CreatorStatus = string.Empty;
        public string Source = "none";
        public Dictionary<string, string> CreationChoices;

    }

    private sealed class SkillContribution
    {
        public float Background;
        public float Aptitude;
        public float Weakness;
        public float OtherTrait;
    }

    public static string BuildDefaultReport()
    {
        EnsureDefinitions();
        Context c = CaptureContext();
        StringBuilder b = new StringBuilder(16384);
        Header(b, "CONSOLIDATED SURVIVOR DEBUG");
        AppendContext(b, c);
        AppendDefinitions(b, c);
        AppendIdentity(b, c);
        AppendTraitReconciliation(b, c);
        AppendPoints(b, c);
        AppendSkills(b, c, string.Empty);
        AppendSkillKnowledge(b, c);
        AppendKnowledge(b, c);
        AppendMigration(b, c);
        AppendCreator(b, c);
        b.AppendLine("[DebugPolicy] mutationGate=" + RebirthSurvivorDebug.Enabled + " explicitEdits=False initializationOrRecoveryReadsPossible=True source=" + c.Source);
        return b.ToString().TrimEnd();
    }

    public static string BuildCreatorReport()
    {
        EnsureDefinitions();
        Context c = CaptureContext(true);
        StringBuilder b = new StringBuilder(4096);
        Header(b, "SURVIVOR CREATOR DEBUG");
        AppendContext(b, c);
        AppendCreator(b, c);
        AppendPoints(b, c);
        return b.ToString().TrimEnd();
    }

    public static string BuildProfileReport()
    {
        EnsureDefinitions();
        Context c = CaptureContext();
        StringBuilder b = new StringBuilder(4096);
        Header(b, "SURVIVOR PROFILE DEBUG");
        AppendContext(b, c);
        AppendIdentity(b, c);
        AppendDefinitions(b, c);
        return b.ToString().TrimEnd();
    }

    public static string BuildMigrationReport()
    {
        EnsureDefinitions();
        Context c = CaptureContext();
        StringBuilder b = new StringBuilder(4096);
        Header(b, "SURVIVOR MIGRATION DEBUG");
        AppendContext(b, c);
        AppendMigration(b, c);
        AppendSkillKnowledge(b, c);
        AppendKnowledge(b, c);
        return b.ToString().TrimEnd();
    }

    public static string BuildPointsReport()
    {
        EnsureDefinitions();
        Context c = CaptureContext();
        StringBuilder b = new StringBuilder(4096);
        Header(b, "SURVIVOR POINTS DEBUG");
        AppendContext(b, c);
        AppendPoints(b, c);
        return b.ToString().TrimEnd();
    }

    public static string BuildSkillReport(string idOrName)
    {
        EnsureDefinitions();
        Context c = CaptureContext();
        StringBuilder b = new StringBuilder(4096);
        Header(b, "SURVIVOR SKILL DEBUG");
        AppendContext(b, c);
        AppendSkills(b, c, idOrName ?? string.Empty);
        return b.ToString().TrimEnd();
    }

    private static void EnsureDefinitions()
    {
        if (!RebirthSurvivorDefinitionRegistry.IsReady)
            RebirthSurvivorInstaller.Install();
    }

    private static Context CaptureContext(bool preferCreator = false)
    {
        Context c = new Context();
        try
        {
            if (GameManager.Instance != null && GameManager.Instance.World != null)
                c.LocalPlayer = GameManager.Instance.World.GetPrimaryPlayer();
        }
        catch { }

        if (c.LocalPlayer != null)
        {
            RebirthWorldCharacterRecord record;
            if (RebirthWorldCharacterService.TryGet(c.LocalPlayer, out record) && record != null)
            {
                c.ServerRecord = record;
                c.Source = "server-authoritative/local-world";
            }
        }

        c.ClientSnapshot = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        if (c.ServerRecord == null && c.ClientSnapshot != null)
            c.Source = "client-owner-snapshot";

        RebirthSurvivorCreatorViewModel creator;
        bool nextVisible, nextEnabled;
        string status;
        if (XUiC_RebirthSurvivorCreator.TryGetActiveDebugState(out creator, out nextVisible, out nextEnabled, out status))
        {
            if (preferCreator || (c.ServerRecord == null && (c.ClientSnapshot == null || !c.ClientSnapshot.HasCharacter)))
            {
                c.Creator = creator;
                c.CreatorNextVisible = nextVisible;
                c.CreatorNextEnabled = nextEnabled;
                c.CreatorStatus = status ?? string.Empty;
                c.CreationChoices = creator.GetCreationChoicesSnapshot();
                c.ServerRecord = null;
                c.ClientSnapshot = null;
                c.Source = "creator-preview (not committed)";
            }
        }
        if (preferCreator && c.Creator == null)
        {
            c.ServerRecord = null; c.ClientSnapshot = null;
            c.Source = "creator-preview unavailable (no committed fallback)";
        }
        if (c.ServerRecord != null)
        {
            c.ClientSnapshot = null;
            if (c.ServerRecord.Origin != null)
                c.CreationChoices = new Dictionary<string, string>(c.ServerRecord.Origin.CreationChoices, StringComparer.OrdinalIgnoreCase);
        }
        return c;
    }

    private static void AppendContext(StringBuilder b, Context c)
    {
        b.AppendLine("[ReportContext] source=" + c.Source + "; manual observation; definition initialization/repository recovery may occur.");
        if (c.CreationChoices == null)
        {
            b.AppendLine("  creationChoices=unavailable in this projection; no choices inferred or invented.");
            return;
        }
        List<string> keys = new List<string>(c.CreationChoices.Keys);
        keys.Sort(StringComparer.Ordinal);
        b.AppendLine("  creationChoices count=" + keys.Count);
        foreach (string key in keys) b.AppendLine("    " + Safe(key) + "=" + Safe(c.CreationChoices[key]));
    }

    private static void AppendDefinitions(StringBuilder b, Context c)
    {
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        b.AppendLine("[Definitions]");
        if (bundle == null || bundle.Progression == null)
        {
            b.AppendLine("  ready=False installer='" + Safe(RebirthSurvivorInstaller.LastReport) + "'");
            return;
        }
        int universal = 0, restricted = 0, deferred = 0, aptitudes = 0;
        for (int i = 0; i < bundle.Traits.Count; i++)
        {
            RebirthTraitDefinition t = bundle.Traits[i];
            if (t == null) continue;
            if (t.Availability == RebirthDefinitionAvailability.Universal) universal++;
            else if (t.Availability == RebirthDefinitionAvailability.Restricted) restricted++;
            else if (t.Availability == RebirthDefinitionAvailability.Deferred) deferred++;
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(t)) aptitudes++;
        }
        bool compatible = true;
        string compatibilitySource = "local-registry";
        if (c.ServerRecord != null && c.ServerRecord.Origin != null)
        {
            compatible = string.Equals(c.ServerRecord.Origin.DefinitionHash, RebirthSurvivorDefinitionRegistry.SemanticHash, StringComparison.OrdinalIgnoreCase);
            compatibilitySource = "world-origin";
        }
        else if (c.ClientSnapshot != null)
        {
            compatible = c.ClientSnapshot.DefinitionsCompatible;
            compatibilitySource = "server-owner-snapshot";
        }
        b.AppendLine("  authoringSchema=" + RebirthSurvivorDefinitionVersion.SchemaVersion + " version=" + Safe(RebirthSurvivorDefinitionRegistry.DefinitionVersion) + " hash=" + Safe(RebirthSurvivorDefinitionRegistry.SemanticHash));
        b.AppendLine("  counts skills=" + bundle.Progression.Skills.Count + " skillKnowledgeAreas=" + bundle.Progression.Skills.Count + " backgrounds=" + bundle.Backgrounds.Count + " traits=" + bundle.Traits.Count + " diets=" + bundle.Diets.Count + " legacyKnowledge=" + bundle.Progression.Knowledge.Count);
        b.AppendLine("  traits universal=" + universal + " restricted=" + restricted + " deferred=" + deferred + " generatedAptitudes=" + aptitudes);
        b.AppendLine("  "+RebirthTraitRuntimeReconciliation.BuildSummary());
        b.AppendLine("  "+RebirthDietRuntimeReconciliation.BuildSummary().Replace("\n","\n  "));
        b.AppendLine("  compatibility=" + compatible + " source=" + compatibilitySource + " runtimeBounds=-50..100 creationBounds=" + F(bundle.Progression.CreationSkillMin) + ".." + F(bundle.Progression.CreationSkillMax));
    }

    private static void AppendIdentity(StringBuilder b, Context c)
    {
        b.AppendLine("[SurvivorIdentity]");
        RebirthSurvivorCreatorViewModel vm = c.Creator;
        if (vm != null)
        {
            string profileId = vm.SelectedSourceProfileId;
            b.AppendLine("  creator purpose=" + vm.Purpose + " step=" + vm.Step + " profileId=" + Safe(profileId) + " profileName='" + Safe(vm.ProfileName) + "'");
            b.AppendLine("  basePlayerProfile='" + Safe(vm.PlayerProfileName) + "' background=" + Safe(vm.BackgroundId) + " diet=" + Safe(vm.DietId));
            AppendTraitCategories(b, vm.SelectedTraitIds);
            return;
        }

        if (c.ServerRecord != null && c.ServerRecord.Origin != null)
        {
            RebirthWorldOriginSnapshot o = c.ServerRecord.Origin;
            string baseProfile = Choice(o.CreationChoices, RebirthSurvivorCreationChoiceKeys.PlayerProfileName);
            b.AppendLine("  worldCharacter key=" + Safe(c.ServerRecord.StablePlayerKey) + " stableId=" + Safe(c.ServerRecord.StablePlayerId) + " revision=" + c.ServerRecord.Revision);
            b.AppendLine("  reusableProfile id=" + Safe(o.SourceProfileId) + " name='" + Safe(o.SourceProfileName) + "' basePlayerProfile='" + Safe(baseProfile) + "'");
            b.AppendLine("  background=" + Safe(o.BackgroundId) + " diet=" + Safe(o.DietId));
            AppendTraitCategories(b, o.TraitIds);
            return;
        }

        if (c.ClientSnapshot != null && c.ClientSnapshot.HasCharacter)
        {
            b.AppendLine("  source=client-owner-snapshot characterRevision=" + c.ClientSnapshot.CharacterRevision + " sourceProfileName='" + Safe(c.ClientSnapshot.SourceProfileName) + "'");
            b.AppendLine("  background=" + Safe(c.ClientSnapshot.BackgroundId) + " diet=" + Safe(c.ClientSnapshot.DietId));
            AppendTraitCategories(b, c.ClientSnapshot.TraitIds);
            b.AppendLine("  basePlayerProfile=<not transmitted in owner snapshot>");
            return;
        }
        b.AppendLine("  no active creator or committed Survivor character is available.");
    }

    private static void AppendTraitReconciliation(StringBuilder b, Context c)
    {
        b.AppendLine("[TraitRuntimeReconciliation]");
        b.AppendLine("  "+RebirthTraitRuntimeReconciliation.BuildSummary());
        IList<string> ids=null;
        if(c.Creator!=null)ids=c.Creator.SelectedTraitIds;
        else if(c.ServerRecord!=null&&c.ServerRecord.Origin!=null)ids=c.ServerRecord.Origin.TraitIds;
        else if(c.ClientSnapshot!=null&&c.ClientSnapshot.HasCharacter)ids=c.ClientSnapshot.TraitIds;
        if(ids==null||ids.Count==0){b.AppendLine("  selected=<none>");return;}
        for(int i=0;i<ids.Count;i++)
        {
            RebirthTraitDefinition t;
            if(!RebirthSurvivorDefinitionRegistry.TryGetTrait(ids[i],out t)||t==null){b.AppendLine("  "+ids[i]+" state=UNKNOWN definitionMissing=True");continue;}
            b.AppendLine("  "+t.Id+" state="+RebirthTraitRuntimeReconciliation.Token(RebirthTraitRuntimeReconciliation.Resolve(t))+
                " creation="+RebirthTraitRuntimeReconciliation.HasCreationComponent(t)+
                " runtime="+RebirthTraitRuntimeReconciliation.HasRuntimeComponent(t)+
                " support="+RebirthTraitRuntimeReconciliation.HasSupportBinding(t));
        }
    }

    private static void AppendPoints(StringBuilder b, Context c)
    {
        b.AppendLine("[TraitPointEconomy]");
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null)
        {
            b.AppendLine("  unavailable: definitions not loaded.");
            return;
        }

        RebirthSurvivorCreationSelection selection = ResolveSelection(c);
        if (selection == null)
        {
            b.AppendLine("  unavailable: no active creator selection or immutable world origin.");
            return;
        }

        RebirthBackgroundDefinition background;
        RebirthDietDefinition diet;
        RebirthSurvivorDefinitionRegistry.TryGetBackground(selection.BackgroundId, out background);
        RebirthSurvivorDefinitionRegistry.TryGetDiet(selection.DietId, out diet);
        int backgroundAdjustment = background != null ? background.CreationPointModifier : 0;
        if (background != null && string.Equals(background.Id, RebirthSurvivorIds.BackgroundCleanSlate, StringComparison.OrdinalIgnoreCase))
            backgroundAdjustment += bundle.Progression.CleanSlateBonus;
        int dietAdjustment = diet != null ? diet.Points : 0;
        int negativeRaw = 0, positiveSpend = 0, aptitudeSpend = 0, otherPositiveSpend = 0;
        for (int i = 0; i < selection.TraitIds.Count; i++)
        {
            RebirthTraitDefinition t;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(selection.TraitIds[i], out t) || t == null) continue;
            if (t.Polarity == RebirthTraitPolarity.Negative) negativeRaw += t.Points;
            else if (t.Polarity == RebirthTraitPolarity.Positive)
            {
                positiveSpend += t.Points;
                if (RebirthSkillAptitudeTraitFactory.IsAptitude(t)) aptitudeSpend += t.Points;
                else otherPositiveSpend += t.Points;
            }
        }
        int negativeCapped = RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund(bundle.Progression.MaxNegativeTraitRefund, negativeRaw);
        int calculatedRemaining = bundle.Progression.BaseCreationPoints + backgroundAdjustment + dietAdjustment + negativeCapped - positiveSpend;
        RebirthSurvivorCreationResult result = RebirthSurvivorCreationValidator.Validate(selection, false);
        b.AppendLine("  base=" + bundle.Progression.BaseCreationPoints + " background=" + Signed(backgroundAdjustment) + " diet=" + Signed(dietAdjustment));
        b.AppendLine("  negativeRefund raw=+" + negativeRaw + " applied=+" + negativeCapped + " cap=" + (RebirthSurvivorTraitPointEconomy.HasRefundCap(bundle.Progression.MaxNegativeTraitRefund) ? bundle.Progression.MaxNegativeTraitRefund.ToString(CultureInfo.InvariantCulture) : "unlimited"));
        b.AppendLine("  positiveSpend=" + positiveSpend + " aptitudeSpend=" + aptitudeSpend + " otherPositiveSpend=" + otherPositiveSpend);
        b.AppendLine("  remaining calculated=" + calculatedRemaining + " validator=" + (result != null ? result.RemainingCreationPoints.ToString(CultureInfo.InvariantCulture) : "<none>") + " valid=" + (result != null && result.IsValid));
        if (result != null && result.Errors.Count > 0)
            b.AppendLine("  errors=" + JoinErrors(result.Errors));
    }

    private static void AppendSkills(StringBuilder b, Context c, string filter)
    {
        b.AppendLine("[Skills]");
        if (c.ClientSnapshot != null)
            b.AppendLine("  origin baseline/contribution reconstruction=definition-only estimate; origin choices are not transmitted. Current values below are owner-synchronized.");
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null)
        {
            b.AppendLine("  unavailable: definitions not loaded.");
            return;
        }
        RebirthSurvivorCreationSelection selection = ResolveSelection(c);
        Dictionary<string, SkillContribution> contributions = BuildSkillContributions(bundle, selection);
        Dictionary<string, float> starting = ResolveStartingSkills(c, selection);
        Dictionary<string, RebirthSkillRuntimeState> current = new Dictionary<string, RebirthSkillRuntimeState>(StringComparer.OrdinalIgnoreCase);
        if (c.ServerRecord != null && c.ServerRecord.Progression != null)
            foreach (KeyValuePair<string, RebirthSkillRuntimeState> pair in c.ServerRecord.Progression.Skills)
                if (pair.Value != null) current[pair.Key] = pair.Value;

        Dictionary<string, RebirthSurvivorOwnerSkillSnapshot> clientCurrent = new Dictionary<string, RebirthSurvivorOwnerSkillSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (c.ServerRecord == null && c.ClientSnapshot != null)
            for (int i = 0; i < c.ClientSnapshot.Skills.Count; i++)
                if (c.ClientSnapshot.Skills[i] != null) clientCurrent[c.ClientSnapshot.Skills[i].Id] = c.ClientSnapshot.Skills[i];

        string normalizedFilter = (filter ?? string.Empty).Trim();
        int emitted = 0;
        for (int i = 0; i < bundle.Progression.Skills.Count; i++)
        {
            RebirthSkillDefinition s = bundle.Progression.Skills[i];
            if (s == null) continue;
            if (normalizedFilter.Length > 0 && !SkillMatches(s, normalizedFilter)) continue;
            SkillContribution q;
            if (!contributions.TryGetValue(s.Id, out q)) q = new SkillContribution();
            float start;
            if (!starting.TryGetValue(s.Id, out start)) start = 0f;
            float value = start, progress = 0f;
            RebirthSkillRuntimeState runtime;
            RebirthSurvivorOwnerSkillSnapshot client;
            if (current.TryGetValue(s.Id, out runtime)) { value = runtime.Value; progress = runtime.Progress; }
            else if (clientCurrent.TryGetValue(s.Id, out client)) { value = client.Value; progress = client.Progress; }
            float progressionContribution = value - start;
            float raw = q.Background + q.Aptitude + q.Weakness + q.OtherTrait;
            float clampMin = Math.Max(s.Min, bundle.Progression.CreationSkillMin);
            float clampMax = Math.Min(s.Max, bundle.Progression.CreationSkillMax);
            float clamped = Math.Max(clampMin, Math.Min(clampMax, raw));
            b.AppendLine("  " + s.Id + " start=" + F(start) + " current=" + F(value) + " background=" + SignedF(q.Background) + " aptitude=" + SignedF(q.Aptitude) + " weakness=" + SignedF(q.Weakness) + " otherTrait=" + SignedF(q.OtherTrait) + " progression=" + SignedF(progressionContribution) + " progress=" + F(progress) + " clamp=" + F(raw) + "->" + F(clamped) + " [" + F(clampMin) + "," + F(clampMax) + "]");
            emitted++;
        }
        if (normalizedFilter.Length > 0 && emitted == 0)
            b.AppendLine("  no Skill matched '" + Safe(normalizedFilter) + "'. Use a full ID (skill.*) or a localized/internal name fragment.");
        else
            b.AppendLine("  emitted=" + emitted + " expected=" + (normalizedFilter.Length == 0 ? bundle.Progression.Skills.Count : emitted) + " source=" + c.Source);
    }

    private static void AppendSkillKnowledge(StringBuilder b, Context c)
    {
        b.AppendLine("[SkillKnowledge]");
        if (c.ClientSnapshot != null) b.AppendLine("  starting/studied values are definition-only estimates; exact committed origin is unavailable on this client.");
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null)
        {
            b.AppendLine("  unavailable: definitions not loaded.");
            return;
        }

        Dictionary<string, float> starting = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> current = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (c.ServerRecord != null && c.ServerRecord.Progression != null)
        {
            if (c.ServerRecord.Origin != null)
                foreach (KeyValuePair<string, float> pair in c.ServerRecord.Origin.StartingSkillKnowledge) starting[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, RebirthSkillKnowledgeRuntimeState> pair in c.ServerRecord.Progression.SkillKnowledge)
                if (pair.Value != null) current[pair.Key] = pair.Value.Value;
        }
        else if (c.ClientSnapshot != null && c.ClientSnapshot.HasCharacter)
        {
            for (int i = 0; i < c.ClientSnapshot.SkillKnowledge.Count; i++)
            {
                RebirthSurvivorOwnerSkillKnowledgeSnapshot entry = c.ClientSnapshot.SkillKnowledge[i];
                if (entry != null) current[entry.Id] = entry.Value;
            }
            RebirthSurvivorCreationSelection selection = ResolveSelection(c);
            RebirthSurvivorCreationResult result = selection != null ? RebirthSurvivorCreationValidator.Validate(selection, false) : null;
            if (result != null) foreach (KeyValuePair<string, float> pair in result.StartingSkillKnowledge) starting[pair.Key] = pair.Value;
        }
        else
        {
            RebirthSurvivorCreationSelection selection = ResolveSelection(c);
            RebirthSurvivorCreationResult result = selection != null ? RebirthSurvivorCreationValidator.Validate(selection, false) : null;
            if (result != null)
            {
                foreach (KeyValuePair<string, float> pair in result.StartingSkillKnowledge)
                {
                    starting[pair.Key] = pair.Value;
                    current[pair.Key] = pair.Value;
                }
            }
        }

        int emitted = 0;
        for (int i = 0; i < bundle.Progression.Skills.Count; i++)
        {
            RebirthSkillDefinition skill = bundle.Progression.Skills[i];
            if (skill == null) continue;
            float start;
            if (!starting.TryGetValue(skill.Id, out start)) start = bundle.Progression.SkillKnowledgeMin;
            float value;
            if (!current.TryGetValue(skill.Id, out value)) value = start;
            b.AppendLine("  " + skill.Id + " start=" + F(start) + " current=" + F(value) + " studied=" + SignedF(value - start));
            emitted++;
        }
        b.AppendLine("  emitted=" + emitted + " expected=" + bundle.Progression.Skills.Count + " bounds=" + F(bundle.Progression.SkillKnowledgeMin) + ".." + F(bundle.Progression.SkillKnowledgeMax) + " source=" + c.Source);
    }

    private static void AppendKnowledge(StringBuilder b, Context c)
    {
        b.AppendLine("[LegacyBinaryKnowledge]");
        HashSet<string> knownDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle != null && bundle.Progression != null)
            for (int i = 0; i < bundle.Progression.Knowledge.Count; i++) if (bundle.Progression.Knowledge[i] != null) knownDefinitions.Add(bundle.Progression.Knowledge[i].Id);

        List<string> start = new List<string>();
        List<string> acquired = new List<string>();
        if (c.ServerRecord != null)
        {
            if (c.ServerRecord.Origin != null) start.AddRange(c.ServerRecord.Origin.StartingKnowledgeIds);
            if (c.ServerRecord.Progression != null) acquired.AddRange(c.ServerRecord.Progression.KnowledgeIds);
        }
        else if (c.ClientSnapshot != null)
        {
            acquired.AddRange(c.ClientSnapshot.KnowledgeIds);
            RebirthSurvivorCreationSelection selection = ResolveSelection(c);
            RebirthSurvivorCreationResult result = selection != null ? RebirthSurvivorCreationValidator.Validate(selection, false) : null;
            if (result != null) start.AddRange(result.StartingKnowledgeIds);
        }
        else
        {
            RebirthSurvivorCreationSelection selection = ResolveSelection(c);
            RebirthSurvivorCreationResult result = selection != null ? RebirthSurvivorCreationValidator.Validate(selection, false) : null;
            if (result != null) start.AddRange(result.StartingKnowledgeIds);
        }
        List<string> unresolved = new List<string>();
        for (int i = 0; i < acquired.Count; i++) if (!knownDefinitions.Contains(acquired[i])) unresolved.Add(acquired[i]);
        for (int i = 0; i < start.Count; i++) if (!knownDefinitions.Contains(start[i]) && !unresolved.Contains(start[i])) unresolved.Add(start[i]);
        start.Sort(StringComparer.Ordinal); acquired.Sort(StringComparer.Ordinal); unresolved.Sort(StringComparer.Ordinal);
        b.AppendLine("  starting=" + Join(start));
        b.AppendLine("  current=" + Join(acquired));
        b.AppendLine("  unresolvedOrRetired=" + Join(unresolved));
    }

    private static void AppendMigration(StringBuilder b, Context c)
    {
        b.AppendLine("[PersistenceMigration]");
        if (c.ServerRecord != null)
        {
            RebirthWorldCharacterRecord r = c.ServerRecord;
            b.AppendLine("  source=server-authoritative storedSchema=" + r.SchemaVersion + " targetSchema=" + RebirthWorldCharacterRecord.CurrentSchemaVersion
                + " migrationSource=" + r.MigrationSourceSchema + " migrationTarget=" + r.MigrationTargetSchema + " migrationApplied=" + r.MigrationApplied
                + " policy='" + Safe(string.IsNullOrEmpty(r.MigrationPolicyId) ? "native-schema6" : r.MigrationPolicyId) + "'");
            b.AppendLine("  origin=" + (r.Origin != null) + " mutableProgression=" + (r.Progression != null) + " complete=" + r.IsComplete + " revision=" + r.Revision + " dirty=" + r.Dirty + " dirtyReason='" + Safe(r.LastDirtyReason) + "'");
            b.AppendLine("  migrationAudit origin='" + Safe(r.MigrationOriginAudit) + "' progression='" + Safe(r.MigrationProgressionAudit) + "'");
            if (r.Origin != null)
            {
                bool compatible = string.Equals(r.Origin.DefinitionHash, RebirthSurvivorDefinitionRegistry.SemanticHash, StringComparison.OrdinalIgnoreCase);
                b.AppendLine("  originDefinition version=" + Safe(r.Origin.DefinitionVersion) + " hash=" + Safe(r.Origin.DefinitionHash) + " compatible=" + compatible);
                RebirthSurvivorProfile profile;
                bool profileExists = !string.IsNullOrEmpty(r.Origin.SourceProfileId) && RebirthSurvivorProfileStore.TryGet(r.Origin.SourceProfileId, out profile);
                b.AppendLine("  profileBinding sourceProfileId=" + Safe(r.Origin.SourceProfileId) + " localProfileExists=" + profileExists);
            }
            return;
        }
        if (c.ClientSnapshot != null)
        {
            b.AppendLine("  source=client-owner-snapshot storedSchema=<not-transmitted> targetSchema=" + RebirthWorldCharacterRecord.CurrentSchemaVersion + " migrationSource=<not-transmitted> migrationApplied=<unknown-client-side>");
            b.AppendLine("  hasCharacter=" + c.ClientSnapshot.HasCharacter + " creationState=" + c.ClientSnapshot.CreationState + " revision=" + c.ClientSnapshot.CharacterRevision + " definitionCompatible=" + c.ClientSnapshot.DefinitionsCompatible);
            b.AppendLine("  originDefinition version=" + Safe(c.ClientSnapshot.OriginDefinitionVersion) + " hash=" + Safe(c.ClientSnapshot.OriginDefinitionHash));
            return;
        }
        b.AppendLine("  no committed world-character record/snapshot is available. targetSchema=" + RebirthWorldCharacterRecord.CurrentSchemaVersion + " profileSchema=" + RebirthSurvivorProfile.CurrentSchemaVersion);
    }

    private static void AppendCreator(StringBuilder b, Context c)
    {
        b.AppendLine("[Creator]");
        RebirthSurvivorCreatorViewModel vm = c.Creator;
        if (vm == null)
        {
            b.AppendLine("  previewIncluded=False; use the creator report for an open preview, not committed values.");
            return;
        }
        string reason;
        bool canAdvance = vm.CanAdvanceCurrentStep(out reason);
        b.AppendLine("  open=True purpose=" + vm.Purpose + " step=" + vm.Step + " highestUnlocked=" + vm.HighestUnlockedStep + " readOnly=" + vm.IsReadOnly);
        b.AppendLine("  selections playerProfile='" + Safe(vm.PlayerProfileName) + "' background=" + Safe(vm.BackgroundId) + " diet=" + Safe(vm.DietId) + " traits=" + Join(vm.SelectedTraitIds));
        b.AppendLine("  next visible=" + c.CreatorNextVisible + " interactable=" + c.CreatorNextEnabled + " CanAdvance=" + canAdvance + " rejectionReason='" + Safe(reason) + "'");
        b.AppendLine("  transientStatus='" + Safe(c.CreatorStatus) + "' validation=" + (vm.Validation != null ? (vm.Validation.IsValid ? "PASS" : "FAIL") : "<none>"));
        if (vm.Validation != null && vm.Validation.Errors.Count > 0)
            b.AppendLine("  validationErrors=" + JoinErrors(vm.Validation.Errors));
    }

    private static void AppendTraitCategories(StringBuilder b, IEnumerable<string> traitIds)
    {
        List<string> authored = new List<string>();
        List<string> aptitudes = new List<string>();
        List<string> backgroundRestricted = new List<string>();
        if (traitIds != null) foreach (string id in traitIds)
        {
            RebirthTraitDefinition trait;
            if (RebirthSkillAptitudeTraitFactory.IsAptitudeId(id)) aptitudes.Add(id ?? string.Empty);
            else authored.Add(id ?? string.Empty);
            if (RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out trait) && trait != null && trait.Availability == RebirthDefinitionAvailability.Restricted)
                backgroundRestricted.Add(trait.Id);
        }
        b.AppendLine("  authoredTraits=" + Join(authored));
        b.AppendLine("  generatedAptitudes=" + Join(aptitudes));
        b.AppendLine("  backgroundSpecificTraits=" + Join(backgroundRestricted));
    }

    private static RebirthSurvivorCreationSelection ResolveSelection(Context c)
    {
        if (c.Creator != null) return c.Creator.BuildSelection();
        if (c.ServerRecord != null && c.ServerRecord.Origin != null)
            return new RebirthSurvivorCreationSelection(c.ServerRecord.Origin.BackgroundId, c.ServerRecord.Origin.DietId, c.ServerRecord.Origin.TraitIds, RebirthSurvivorDefinitionRegistry.SemanticHash);
        if (c.ClientSnapshot != null && c.ClientSnapshot.HasCharacter)
            return new RebirthSurvivorCreationSelection(c.ClientSnapshot.BackgroundId, c.ClientSnapshot.DietId, c.ClientSnapshot.TraitIds, RebirthSurvivorDefinitionRegistry.SemanticHash);
        return null;
    }

    private static Dictionary<string, float> ResolveStartingSkills(Context c, RebirthSurvivorCreationSelection selection)
    {
        Dictionary<string, float> result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (c.ServerRecord != null && c.ServerRecord.Origin != null)
        {
            foreach (KeyValuePair<string, float> pair in c.ServerRecord.Origin.StartingSkills) result[pair.Key] = pair.Value;
            return result;
        }
        RebirthSurvivorCreationResult validation = selection != null ? RebirthSurvivorCreationValidator.Validate(selection, false) : null;
        if (validation != null)
            foreach (KeyValuePair<string, float> pair in validation.StartingSkills) result[pair.Key] = pair.Value;
        return result;
    }

    private static Dictionary<string, SkillContribution> BuildSkillContributions(RebirthSurvivorDefinitionBundle bundle, RebirthSurvivorCreationSelection selection)
    {
        Dictionary<string, SkillContribution> result = new Dictionary<string, SkillContribution>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < bundle.Progression.Skills.Count; i++) if (bundle.Progression.Skills[i] != null) result[bundle.Progression.Skills[i].Id] = new SkillContribution();
        if (selection == null) return result;

        RebirthBackgroundDefinition background;
        if (RebirthSurvivorDefinitionRegistry.TryGetBackground(selection.BackgroundId, out background) && background != null)
        {
            for (int i = 0; i < background.StartingSkills.Count; i++)
            {
                RebirthStartingSkillBiasDefinition bias = background.StartingSkills[i];
                SkillContribution q;
                if (bias == null || !result.TryGetValue(bias.SkillId, out q)) continue;
                if (bias.HasExplicitValue) q.Background += bias.Value;
                else
                {
                    int tier;
                    if (bundle.Progression.SkillBiasTiers.TryGetValue(bias.TierId ?? string.Empty, out tier)) q.Background += tier;
                }
            }
        }

        for (int i = 0; i < selection.TraitIds.Count; i++)
        {
            RebirthTraitDefinition trait;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(selection.TraitIds[i], out trait) || trait == null) continue;
            string aptitudeSkill; int aptitudeTier, aptitudeBonus, aptitudeCost;
            if (RebirthSkillAptitudeTraitFactory.TryParse(trait.Id, out aptitudeSkill, out aptitudeTier, out aptitudeBonus, out aptitudeCost))
            {
                SkillContribution q;
                if (result.TryGetValue(aptitudeSkill, out q)) q.Aptitude += aptitudeBonus;
                continue;
            }
            RebirthConditionModifierProfileDefinition profile;
            if (!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId, out profile) || profile == null) continue;
            for (int j = 0; j < profile.Components.Count; j++)
            {
                RebirthConditionModifierComponent component = profile.Components[j];
                if (component == null || !string.Equals(component.Phase, "creation", StringComparison.OrdinalIgnoreCase)) continue;
                string skillId = string.Empty;
                float delta = 0f;
                if (component.Target.StartsWith("skill.start.", StringComparison.OrdinalIgnoreCase))
                {
                    skillId = component.Target.Substring("skill.start.".Length);
                    if (!skillId.StartsWith("skill.", StringComparison.OrdinalIgnoreCase)) skillId = "skill." + skillId;
                    delta = ParseFloat(component.Value);
                }
                else if (string.Equals(component.Target, "skill.start_bias", StringComparison.OrdinalIgnoreCase))
                {
                    skillId = component.Value;
                    int tier;
                    if (bundle.Progression.SkillBiasTiers.TryGetValue(component.Note ?? string.Empty, out tier)) delta = tier;
                }
                SkillContribution q;
                if (skillId.Length == 0 || !result.TryGetValue(skillId, out q)) continue;
                if (trait.Polarity == RebirthTraitPolarity.Negative) q.Weakness += delta;
                else q.OtherTrait += delta;
            }
        }
        return result;
    }

    private static bool SkillMatches(RebirthSkillDefinition skill, string filter)
    {
        if (skill == null) return false;
        if (skill.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (!string.IsNullOrEmpty(skill.NameKey) && skill.NameKey.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        try
        {
            string localized = Localization.Get(skill.NameKey);
            if (!string.IsNullOrEmpty(localized) && localized.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        catch { }
        return false;
    }

    private static float ParseFloat(string value)
    {
        float v;
        return float.TryParse(value ?? string.Empty, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
    }

    private static string Choice(System.Collections.ObjectModel.ReadOnlyDictionary<string, string> choices, string key)
    {
        string value;
        return choices != null && choices.TryGetValue(key ?? string.Empty, out value) ? value ?? string.Empty : string.Empty;
    }

    private static void Header(StringBuilder b, string title)
    {
        b.AppendLine("[REBIRTH Survivor] ====================================================");
        b.AppendLine("[REBIRTH Survivor] " + title);
        b.AppendLine("[REBIRTH Survivor] ====================================================");
    }

    private static string Join(IEnumerable<string> values)
    {
        if (values == null) return "[]";
        List<string> list = new List<string>();
        foreach (string value in values) list.Add(value ?? string.Empty);
        list.Sort(StringComparer.Ordinal);
        return "[" + string.Join(",", list.ToArray()) + "]";
    }

    private static string JoinErrors(System.Collections.ObjectModel.ReadOnlyCollection<RebirthSurvivorCreationError> errors)
    {
        if (errors == null || errors.Count == 0) return "[]";
        string[] values = new string[errors.Count];
        for (int i = 0; i < errors.Count; i++) values[i] = errors[i] != null ? errors[i].ToString() : "<null>";
        return "[" + string.Join("; ", values) + "]";
    }

    private static string Signed(int value) { return value >= 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture); }
    private static string SignedF(float value) { return value >= 0f ? "+" + F(value) : F(value); }
    private static string F(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
    private static string Safe(string value) { return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " "); }
}
