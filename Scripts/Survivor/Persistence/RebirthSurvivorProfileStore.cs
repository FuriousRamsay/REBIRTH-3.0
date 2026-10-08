using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Main-menu-safe local store for reusable Survivor templates.
/// One GUID-named XML file is used per profile so one corrupt record cannot poison the catalogue.
/// This store is never authoritative for a world character.
/// </summary>
public static class RebirthSurvivorProfileStore
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthSurvivorProfile> Profiles =
        new Dictionary<string, RebirthSurvivorProfile>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<RebirthSurvivorProfileLoadIssue> Issues =
        new List<RebirthSurvivorProfileLoadIssue>();
    private static bool loaded;

    public static string RootDirectory
    {
        get
        {
            string root = GameIO.GetUserGameDataDir();
            return string.IsNullOrEmpty(root) ? string.Empty : Path.Combine(root, "RebirthData", "SurvivorProfiles");
        }
    }

    public static void Initialize()
    {
        EnsureLoaded(true);
    }

    public static void Refresh()
    {
        EnsureLoaded(true);
    }

    public static void ResetCache()
    {
        lock (Sync)
        {
            loaded = false;
            Profiles.Clear();
            Issues.Clear();
        }
    }

    public static RebirthSurvivorProfile[] GetProfilesSnapshot()
    {
        EnsureLoaded(false);
        lock (Sync)
        {
            List<RebirthSurvivorProfile> list = new List<RebirthSurvivorProfile>();
            foreach (RebirthSurvivorProfile profile in Profiles.Values)
                if (profile != null) list.Add(profile.Clone());
            list.Sort(delegate(RebirthSurvivorProfile a, RebirthSurvivorProfile b)
            {
                int modified = b.ModifiedAtUtc.CompareTo(a.ModifiedAtUtc);
                if (modified != 0) return modified;
                return StringComparer.OrdinalIgnoreCase.Compare(a.ProfileName, b.ProfileName);
            });
            return list.ToArray();
        }
    }

    public static RebirthSurvivorProfileLoadIssue[] GetIssuesSnapshot()
    {
        EnsureLoaded(false);
        lock (Sync)
            return Issues.ToArray();
    }

    public static bool TryGet(string profileId, out RebirthSurvivorProfile profile)
    {
        profile = null;
        EnsureLoaded(false);
        string id = NormalizeProfileId(profileId);
        if (id.Length == 0)
            return false;
        lock (Sync)
        {
            RebirthSurvivorProfile stored;
            if (!Profiles.TryGetValue(id, out stored) || stored == null)
                return false;
            profile = stored.Clone();
            return true;
        }
    }

    public static bool TryCreate(string profileName, RebirthSurvivorCreationSelection selection, IDictionary<string, string> creationChoices,
        out RebirthSurvivorProfile profile, out string error)
    {
        profile = null;
        error = string.Empty;
        if (!ValidateProfileName(profileName, out error))
            return false;
        RebirthSurvivorCreationResult validation = RebirthSurvivorCreationValidator.ValidateForCommit(selection, false);
        if (validation == null || !validation.IsValid)
        {
            error = BuildValidationError(validation);
            return false;
        }

        RebirthSurvivorProfile created = RebirthSurvivorProfile.CreateNew(profileName, selection, creationChoices);
        if (!TrySaveInternal(created, false, out error))
            return false;
        profile = created.Clone();
        return true;
    }

    public static bool TrySaveEdited(RebirthSurvivorProfile profile, out string error)
    {
        error = string.Empty;
        if (profile == null)
        {
            error = "profile is null";
            return false;
        }

        // Editing changes the mechanical selection and must never rely on the caller/UI
        // to have performed validation first. Local reusable profiles intentionally
        // bypass only the current-world Rebirth-mode requirement; every definition,
        // eligibility, conflict, exclusivity, and point-budget rule is still enforced.
        string currentHash = RebirthSurvivorDefinitionRegistry.SemanticHash ?? string.Empty;
        RebirthSurvivorCreationResult validation =
            RebirthSurvivorCreationValidator.ValidateForCommit(profile.ToSelection(currentHash), false);
        if (validation == null || !validation.IsValid)
        {
            error = BuildValidationError(validation);
            return false;
        }

        return TrySaveInternal(profile, true, out error);
    }

    public static bool TryDuplicate(string profileId, string duplicateName, out RebirthSurvivorProfile duplicate, out string error)
    {
        duplicate = null;
        error = string.Empty;
        if (!ValidateProfileName(duplicateName, out error))
            return false;
        RebirthSurvivorProfile source;
        if (!TryGet(profileId, out source))
        {
            error = "profile not found";
            return false;
        }
        RebirthSurvivorProfile copy = source.Duplicate(duplicateName);
        if (!TrySaveInternal(copy, false, out error))
            return false;
        duplicate = copy.Clone();
        return true;
    }

    public static bool TryRename(string profileId, string newName, out RebirthSurvivorProfile renamed, out string error)
    {
        renamed = null;
        error = string.Empty;
        if (!ValidateProfileName(newName, out error))
            return false;
        RebirthSurvivorProfile source;
        if (!TryGet(profileId, out source))
        {
            error = "profile not found";
            return false;
        }
        RebirthSurvivorProfile updated = source.Rename(newName);
        if (!TrySaveInternal(updated, true, out error))
            return false;
        renamed = updated.Clone();
        return true;
    }

    public static bool TryDelete(string profileId,out string error)
    {
        error=string.Empty;EnsureLoaded(false);string id=NormalizeProfileId(profileId);
        if(id.Length==0){error="invalid profile ID";return false;}
        lock(Sync)
        {
            string path=GetProfilePath(id);if(string.IsNullOrEmpty(path)){error="profile storage root is unavailable";return false;}
            try
            {
                if(File.Exists(path))File.Delete(path);if(File.Exists(path+".bak"))File.Delete(path+".bak");if(File.Exists(path+".tmp"))File.Delete(path+".tmp");
                Profiles.Remove(id);return true;
            }
            catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;return false;}
        }
    }

    public static RebirthSurvivorProfileCompatibility EvaluateCompatibility(RebirthSurvivorProfile profile)
    {
        if (profile == null)
            return new RebirthSurvivorProfileCompatibility(RebirthSurvivorProfileCompatibilityKind.InvalidCombination, "profile is null", null);
        if (profile.SchemaVersion != RebirthSurvivorProfile.CurrentSchemaVersion)
            return new RebirthSurvivorProfileCompatibility(RebirthSurvivorProfileCompatibilityKind.UnsupportedSchema,
                "unsupported profile schema " + profile.SchemaVersion.ToString(CultureInfo.InvariantCulture), null);

        string currentHash = RebirthSurvivorDefinitionRegistry.SemanticHash ?? string.Empty;
        bool sameDefinitions = string.Equals(profile.AuthoredDefinitionHash, currentHash, StringComparison.OrdinalIgnoreCase);
        RebirthSurvivorCreationResult validation = RebirthSurvivorCreationValidator.ValidateForCommit(profile.ToSelection(currentHash), false);
        if (validation != null && validation.IsValid)
        {
            // A semantic-definition hash change by itself is not a player-facing error. Survivor Profiles
            // are reusable authored selections; if every saved selection still validates against the
            // current definitions, it remains usable without forcing the player through a review step.
            // Missing/invalid selections are still rejected below. Editing/saving the profile refreshes
            // its authored hash naturally.
            return new RebirthSurvivorProfileCompatibility(
                RebirthSurvivorProfileCompatibilityKind.Ready,
                sameDefinitions ? "ready" : "definitions updated; saved selections remain valid",
                null);
        }

        List<RebirthSurvivorCreationError> errors = validation != null
            ? new List<RebirthSurvivorCreationError>(validation.Errors)
            : new List<RebirthSurvivorCreationError>();
        bool missing = false;
        bool needsReview = false;
        for (int i = 0; i < errors.Count; i++)
        {
            RebirthSurvivorCreationErrorCode code = errors[i].Code;
            if (code == RebirthSurvivorCreationErrorCode.UnknownBackground ||
                code == RebirthSurvivorCreationErrorCode.UnknownDiet ||
                code == RebirthSurvivorCreationErrorCode.UnknownTrait)
            {
                missing = true;
                break;
            }
            if (code == RebirthSurvivorCreationErrorCode.InsufficientCreationPoints ||
                code == RebirthSurvivorCreationErrorCode.UnspentCreationPoints)
                needsReview = true;
        }
        RebirthSurvivorProfileCompatibilityKind kind = missing
            ? RebirthSurvivorProfileCompatibilityKind.MissingDefinition
            : (needsReview ? RebirthSurvivorProfileCompatibilityKind.NeedsReview : RebirthSurvivorProfileCompatibilityKind.InvalidCombination);
        string message = missing ? "one or more saved definitions no longer exist"
            : (needsReview ? "Trait-point balance changed under the current definitions; review required" : "saved combination is not valid under current definitions");
        return new RebirthSurvivorProfileCompatibility(kind, message, errors);
    }

    private static bool TrySaveInternal(RebirthSurvivorProfile profile,bool requireExisting,out string error)
    {
        error=string.Empty;EnsureLoaded(false);if(!ValidateStructure(profile,out error))return false;
        string id=NormalizeProfileId(profile.ProfileId);
        lock(Sync)
        {
            bool exists=Profiles.ContainsKey(id);
            if(requireExisting&&!exists){error="profile does not exist";return false;}
            if(!requireExisting&&exists){error="profile ID already exists";return false;}
            string path=GetProfilePath(id);if(string.IsNullOrEmpty(path)){error="profile storage root is unavailable";return false;}
            XDocument document=Serialize(profile);
            if(!RebirthAtomicXmlFile.TryWrite(path,document,out error))return false;
            // Publish the cache only after the durable write succeeds while holding the same
            // per-store serialization gate used by competing save/delete/refresh operations.
            Profiles[id]=profile.Clone();return true;
        }
    }

    private static void EnsureLoaded(bool forceRefresh)
    {
        lock(Sync)
        {
            if(loaded&&!forceRefresh)return;
            var previousProfiles=new Dictionary<string,RebirthSurvivorProfile>(Profiles,StringComparer.OrdinalIgnoreCase);
            var previousIssues=new List<RebirthSurvivorProfileLoadIssue>(Issues);bool previousLoaded=loaded;
            Profiles.Clear();Issues.Clear();loaded=false;
            string root=RootDirectory;
            if(string.IsNullOrEmpty(root)){RestorePrevious(previousProfiles,previousIssues,previousLoaded);return;}
            try
            {
                if(!Directory.Exists(root))Directory.CreateDirectory(root);
                string[] candidates=Directory.GetFiles(root,"*.xml*",SearchOption.TopDirectoryOnly);
                HashSet<string> basePaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for(int i=0;i<candidates.Length;i++)
                {
                    string candidate=candidates[i];
                    if(candidate.EndsWith(".xml",StringComparison.OrdinalIgnoreCase))basePaths.Add(candidate);
                    else if(candidate.EndsWith(".xml.bak",StringComparison.OrdinalIgnoreCase))basePaths.Add(candidate.Substring(0,candidate.Length-4));
                }
                string[] files=new string[basePaths.Count];basePaths.CopyTo(files);Array.Sort(files,StringComparer.OrdinalIgnoreCase);
                for(int i=0;i<files.Length;i++)LoadOne(files[i]);
                loaded=true;
            }
            catch(Exception ex)
            {
                RestorePrevious(previousProfiles,previousIssues,previousLoaded);
                Issues.Add(new RebirthSurvivorProfileLoadIssue(root,string.Empty,"refresh failed; previous cache retained: "+ex.GetType().Name+": "+ex.Message,false));
            }
        }
    }
    private static void RestorePrevious(Dictionary<string,RebirthSurvivorProfile> profiles,List<RebirthSurvivorProfileLoadIssue> issues,bool wasLoaded)
    {
        Profiles.Clear();foreach(var pair in profiles)Profiles[pair.Key]=pair.Value;Issues.Clear();Issues.AddRange(issues);loaded=wasLoaded;
    }

    private static void LoadOne(string path)
    {
        string fileId = NormalizeProfileId(Path.GetFileNameWithoutExtension(path));
        if (fileId.Length == 0)
        {
            AddIssue(path, string.Empty, "filename is not a GUID profile ID", false);
            return;
        }

        RebirthSurvivorProfile profile = null;
        bool migrated = false;
        string finalError = "missing";
        bool loadedFinal = false;
        if (File.Exists(path))
            loadedFinal = TryLoadValidatedProfile(path, out profile, out migrated, out finalError);
        bool usedBackup = false;
        string recoveryMessage = string.Empty;

        if (!loadedFinal)
        {
            string backupPath = path + ".bak";
            RebirthSurvivorProfile backupProfile = null;
            bool backupMigrated = false;
            string backupError = "missing";
            bool loadedBackup = false;
            if (File.Exists(backupPath))
                loadedBackup = TryLoadValidatedProfile(backupPath, out backupProfile, out backupMigrated, out backupError);
            if (loadedBackup)
            {
                profile = backupProfile;
                migrated = backupMigrated;
                usedBackup = true;
                recoveryMessage = "final failed (" + (finalError ?? "missing") + "); recovered from validated backup";
            }
            else
            {
                AddIssue(path, fileId, "final failed (" + (finalError ?? "missing") + "); backup failed (" + (backupError ?? "missing") + ")", false);
                return;
            }
        }

        if (!string.Equals(fileId, profile.ProfileId, StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(path, fileId, "profile ID inside XML does not match GUID filename", usedBackup);
            return;
        }

        lock (Sync)
        {
            if (Profiles.ContainsKey(profile.ProfileId))
            {
                Issues.Add(new RebirthSurvivorProfileLoadIssue(path, profile.ProfileId, "duplicate profile ID", usedBackup));
                return;
            }
            Profiles[profile.ProfileId] = profile;
            if (usedBackup)
                Issues.Add(new RebirthSurvivorProfileLoadIssue(path, profile.ProfileId, recoveryMessage, true));
        }

        // A migration or backup recovery is not authoritative until a complete replacement write
        // succeeds. Atomic writer keeps the previous bytes as .bak while repairing the final.
        if (migrated || usedBackup)
        {
            string rewriteError;
            if (!RebirthAtomicXmlFile.TryWrite(path, Serialize(profile), out rewriteError))
                AddIssue(path, profile.ProfileId, "loaded but could not persist repaired profile: " + rewriteError, usedBackup);
        }
    }

    private static bool TryLoadValidatedProfile(string candidatePath, out RebirthSurvivorProfile profile, out bool migrated, out string error)
    {
        profile = null;
        migrated = false;
        error = string.Empty;
        XDocument doc;
        if (!RebirthAtomicXmlFile.TryLoad(candidatePath, out doc, out error))
            return false;
        string migrationError;
        if (!RebirthSurvivorProfileMigrationRegistry.TryMigrateToCurrent(doc, out migrated, out migrationError))
        {
            error = migrationError;
            return false;
        }
        string parseError;
        if (!TryDeserialize(doc, out profile, out parseError))
        {
            error = parseError;
            profile = null;
            return false;
        }
        return true;
    }

    private static bool ValidateStructure(RebirthSurvivorProfile profile, out string error)
    {
        error = string.Empty;
        if (profile == null) { error = "profile is null"; return false; }
        if (profile.SchemaVersion != RebirthSurvivorProfile.CurrentSchemaVersion) { error = "unsupported profile schema"; return false; }
        if (NormalizeProfileId(profile.ProfileId).Length == 0) { error = "profile ID is not a valid GUID"; return false; }
        if (!ValidateProfileName(profile.ProfileName, out error)) return false;
        if (string.IsNullOrEmpty(profile.AuthoredDefinitionHash)) { error = "authored definition hash is missing"; return false; }
        if (string.IsNullOrEmpty(profile.AuthoredDefinitionVersion)) { error = "authored definition version is missing"; return false; }
        if (string.IsNullOrEmpty(profile.BackgroundId)) { error = "background ID is missing"; return false; }
        if (string.IsNullOrEmpty(profile.DietId)) { error = "diet ID is missing"; return false; }

        HashSet<string> traits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < profile.TraitIds.Count; i++)
        {
            string traitId = (profile.TraitIds[i] ?? string.Empty).Trim();
            if (traitId.Length == 0) { error = "empty trait ID"; return false; }
            if (!traits.Add(traitId)) { error = "duplicate trait ID " + traitId; return false; }
        }
        if (profile.CreationChoices.Count > 32) { error = "too many creation choices"; return false; }
        foreach (KeyValuePair<string, string> pair in profile.CreationChoices)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) { error = "creation choice key is empty"; return false; }
            if (pair.Key.Length > 96 || (pair.Value ?? string.Empty).Length > 256) { error = "creation choice exceeds storage limits"; return false; }
        }
        return true;
    }

    private static bool ValidateProfileName(string name, out string error)
    {
        error = string.Empty;
        string value = (name ?? string.Empty).Trim();
        if (value.Length == 0) { error = "profile name is required"; return false; }
        if (value.Length > 64) { error = "profile name exceeds 64 characters"; return false; }
        for (int i = 0; i < value.Length; i++)
            if (char.IsControl(value[i])) { error = "profile name contains control characters"; return false; }
        return true;
    }

    private static string BuildValidationError(RebirthSurvivorCreationResult validation)
    {
        if (validation == null) return "selection validation did not return a result";
        if (validation.Errors.Count == 0) return "selection is invalid";
        return validation.Errors[0].ToString();
    }

    private static XDocument Serialize(RebirthSurvivorProfile profile)
    {
        XElement traits = new XElement("traits");
        for (int i = 0; i < profile.TraitIds.Count; i++)
            traits.Add(new XElement("trait", new XAttribute("id", profile.TraitIds[i])));

        XElement choices = new XElement("creationChoices");
        List<string> keys = new List<string>(profile.CreationChoices.Keys);
        keys.Sort(StringComparer.Ordinal);
        for (int i = 0; i < keys.Count; i++)
            choices.Add(new XElement("choice", new XAttribute("key", keys[i]), new XAttribute("value", profile.CreationChoices[keys[i]] ?? string.Empty)));

        XElement root = new XElement("rebirthSurvivorProfile",
            new XAttribute("schemaVersion", profile.SchemaVersion),
            new XAttribute("profileId", profile.ProfileId),
            new XAttribute("profileName", profile.ProfileName),
            new XAttribute("authoredDefinitionHash", profile.AuthoredDefinitionHash),
            new XAttribute("authoredDefinitionVersion", profile.AuthoredDefinitionVersion),
            new XAttribute("backgroundId", profile.BackgroundId),
            new XAttribute("dietId", profile.DietId),
            new XAttribute("createdAtUtc", profile.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            new XAttribute("modifiedAtUtc", profile.ModifiedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            traits,
            choices);
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static bool TryDeserialize(XDocument document, out RebirthSurvivorProfile profile, out string error)
    {
        profile = null;
        error = string.Empty;
        try
        {
            XElement root = document != null ? document.Root : null;
            if (root == null || root.Name != "rebirthSurvivorProfile") { error = "unexpected profile root"; return false; }
            int schema;
            if (!int.TryParse(A(root, "schemaVersion"), NumberStyles.Integer, CultureInfo.InvariantCulture, out schema)) { error = "invalid schemaVersion"; return false; }

            List<string> traits = new List<string>();
            XElement traitsNode = root.Element("traits");
            if (traitsNode != null)
                foreach (XElement trait in traitsNode.Elements("trait")) traits.Add(A(trait, "id"));

            Dictionary<string, string> choices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            XElement choicesNode = root.Element("creationChoices");
            if (choicesNode != null)
                foreach (XElement choice in choicesNode.Elements("choice")) choices[A(choice, "key")] = A(choice, "value");

            DateTime created;
            DateTime modified;
            if (!DateTime.TryParse(A(root, "createdAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out created)) { error = "invalid createdAtUtc"; return false; }
            if (!DateTime.TryParse(A(root, "modifiedAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out modified)) { error = "invalid modifiedAtUtc"; return false; }

            profile = new RebirthSurvivorProfile(schema, A(root, "profileId"), A(root, "profileName"), A(root, "authoredDefinitionHash"),
                A(root, "authoredDefinitionVersion"), A(root, "backgroundId"), A(root, "dietId"), traits, choices, created, modified);
            if (!ValidateStructure(profile, out error)) { profile = null; return false; }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            profile = null;
            return false;
        }
    }

    private static string GetProfilePath(string id)
    {
        string root = RootDirectory;
        return string.IsNullOrEmpty(root) ? string.Empty : Path.Combine(root, id + ".xml");
    }

    private static string NormalizeProfileId(string profileId)
    {
        Guid guid;
        return Guid.TryParseExact((profileId ?? string.Empty).Trim(), "N", out guid)
            ? guid.ToString("N").ToLowerInvariant()
            : string.Empty;
    }

    private static void AddIssue(string path, string profileId, string reason, bool recovered)
    {
        lock (Sync)
            Issues.Add(new RebirthSurvivorProfileLoadIssue(path, profileId, reason, recovered));
        Log.Warning("[REBIRTH Survivor] profile persistence issue id=" + (profileId ?? string.Empty) + " reason=" + (reason ?? string.Empty));
    }

    private static string A(XElement element, string name)
    {
        if (element == null) return string.Empty;
        XAttribute attribute = element.Attribute(name);
        return attribute != null ? attribute.Value : string.Empty;
    }
}
