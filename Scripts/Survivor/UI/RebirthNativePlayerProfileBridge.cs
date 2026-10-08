using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

#nullable disable

/// <summary>
/// Bridges the Rebirth Survivor editor to the game's native Player Profile screen.
/// Rebirth never clones or replaces the native appearance editor: it opens the stock
/// playerProfiles/playerProfilesCreate window groups, observes the selected native profile,
/// and stores only that profile name in the Survivor's creation choices.
/// </summary>
public static class RebirthNativePlayerProfileBridge
{
    private const int MaxEmbeddedProfileRoster = 128;
    private const string LogPrefix = "[REBIRTH Survivor][PlayerProfileBridge]";
    private const string NativeProfilesGroup = "playerProfiles";
    private const string NativeCreateGroup = "playerProfilesCreate";
    public const int ProfileAccessorContractVersion = 2;
    private static int profileCatalogueGeneration = 1;

    private static XUi activeXui;
    private static XUiC_RebirthSurvivorCreator owner;
    private static RebirthSurvivorCreatorViewModel model;
    private static bool active;
    private static bool sawProfilesOpen;
    private static bool lastProfilesOpen;
    private static bool lastCreateOpen;
    private static bool dumpedReflection;
    private static string lastCandidate = string.Empty;
    private static string lastCandidateSource = string.Empty;
    private static string createDialogText = string.Empty;

    public static bool IsActive { get { return active; } }

    // The list itself is always read fresh, while this generation versions every derived
    // positive/negative lookup and render identity. Same-name edit/delete/recreate therefore
    // cannot inherit stale native/archetype cache state.
    public static int ProfileCatalogueGeneration { get { return profileCatalogueGeneration; } }
    public static string DisplayProfileName(string profileName)
    {
        return IsReservedTemporaryProfileName(profileName)
            ? "[FF9999](" + RebirthSurvivorUiText.L("xuiRebirthTemporaryPlayerProfile", "Temporary Profile - Please Save") + ")[-]"
            : profileName ?? string.Empty;
    }
    public static void InvalidateEmbeddedProfileSnapshot()
    {
        profileCatalogueGeneration = profileCatalogueGeneration == int.MaxValue ? 1 : profileCatalogueGeneration + 1;
        NamedArchetypeCache.Clear();
        NamedArchetypeMisses.Clear();
    }

    public sealed class EmbeddedProfileInfo
    {
        public string Name;
        public object NativeObject;
        public Archetype Archetype;
        public int NativeIndex;
        public int AccessorContractVersion;
        public int CatalogueGeneration;
        public long AppearanceSignature;

        public EmbeddedProfileInfo(string name, object nativeObject, Archetype archetype, int nativeIndex)
        {
            Name = name ?? string.Empty;
            NativeObject = nativeObject;
            Archetype = archetype;
            NativeIndex = nativeIndex;
            AccessorContractVersion = ProfileAccessorContractVersion;
            CatalogueGeneration = ProfileCatalogueGeneration;
            AppearanceSignature = ComputeAppearanceSignature(archetype);
        }
    }

    /// <summary>
    /// Returns the actual native Player Profile data collection rather than the visual
    /// grid-entry pool. ProfilesList preallocates rows and pages them, so counting XUi
    /// entries produces empty rows and an invalid scroll range.
    /// </summary>
    public static bool TryGetEmbeddedPlayerProfiles(XUiController creatorRoot, out List<EmbeddedProfileInfo> result)
    {
        result = new List<EmbeddedProfileInfo>();
        if (creatorRoot == null) return false;

        XUiController host = creatorRoot.GetChildById("nativePlayerProfileHost");
        XUiController profiles = host != null ? host.GetChildById("profiles") : creatorRoot.GetChildById("profiles");
        if (profiles == null) return false;

        List<EmbeddedProfileInfo> best = null;
        string bestSource = string.Empty;
        int bestScore = int.MinValue;
        Dictionary<string, Archetype> archetypesByName = new Dictionary<string, Archetype>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object> objectsByName = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        Type type = profiles.GetType();
        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                object value;
                try { value = fields[i].GetValue(profiles); } catch { continue; }
                List<EmbeddedProfileInfo> candidate;
                if (TryBuildProfileSnapshot(value, fields[i].Name, out candidate))
                {
                    MergeProfileDetails(candidate, archetypesByName, objectsByName);
                    int score = ProfileCollectionScore(fields[i].Name, candidate);
                    if (best == null || score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                        bestSource = scan.Name + "." + fields[i].Name;
                    }
                }
            }

            PropertyInfo[] properties;
            try { properties = scan.GetProperties(flags); } catch { properties = new PropertyInfo[0]; }
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                object value;
                try { value = property.GetValue(profiles, null); } catch { continue; }
                List<EmbeddedProfileInfo> candidate;
                if (TryBuildProfileSnapshot(value, property.Name, out candidate))
                {
                    MergeProfileDetails(candidate, archetypesByName, objectsByName);
                    int score = ProfileCollectionScore(property.Name, candidate);
                    if (best == null || score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                        bestSource = scan.Name + "." + property.Name;
                    }
                }
            }
        }

        if (best == null || best.Count == 0)
        {
            if (lastSnapshotCount != 0 || !string.Equals(lastSnapshotSource, "<none>", StringComparison.Ordinal))
            {
                lastSnapshotSource = "<none>";
                lastSnapshotCount = 0;
                Log.Warning(LogPrefix + " EMBEDDED-SNAPSHOT unavailable controller=" + profiles.GetType().FullName);
            }
            return false;
        }
        for (int i = 0; i < best.Count; i++)
        {
            EmbeddedProfileInfo profile = best[i];
            Archetype archetype;
            object nativeObject;
            if (profile.Archetype == null && archetypesByName.TryGetValue(profile.Name, out archetype)) profile.Archetype = archetype;
            if (profile.Archetype == null)
            {
                string namedSource;
                if (TryResolveNamedArchetype(profile.Name, out archetype, out namedSource))
                    profile.Archetype = archetype;
            }
            if ((profile.NativeObject == null || profile.NativeObject is string) && objectsByName.TryGetValue(profile.Name, out nativeObject)) profile.NativeObject = nativeObject;
            profile.AccessorContractVersion = ProfileAccessorContractVersion;
            profile.CatalogueGeneration = ProfileCatalogueGeneration;
            profile.AppearanceSignature = ComputeAppearanceSignature(profile.Archetype);
        }
        result = best;
        if (!string.Equals(lastSnapshotSource, bestSource, StringComparison.Ordinal) || lastSnapshotCount != best.Count)
        {
            lastSnapshotSource = bestSource;
            lastSnapshotCount = best.Count;
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" EMBEDDED-SNAPSHOT source=" + Safe(bestSource) + " profiles=" + best.Count); }
        }
        return true;
    }

    public static bool TrySelectEmbeddedPlayerProfile(XUiController creatorRoot, EmbeddedProfileInfo target, out string error)
    {
        error = string.Empty;
        if (creatorRoot == null || target == null || string.IsNullOrEmpty(target.Name))
        {
            error = "Player Profile selection is unavailable.";
            return false;
        }

        XUiController host = creatorRoot.GetChildById("nativePlayerProfileHost");
        XUiController profiles = host != null ? host.GetChildById("profiles") : creatorRoot.GetChildById("profiles");
        if (profiles == null)
        {
            error = "Native Player Profile controller is unavailable.";
            return false;
        }

        // Prefer the native list PROPERTY setter. XUiC_List<T>.SelectedEntryIndex drives
        // SelectedEntry/currentSelectedEntry and emits SelectionChanged, which is what the
        // parent PlayerProfile controller uses to rebuild SDCSPreviewWindow. Generic selector
        // methods are only a fallback because several 3.1 list helpers merely mutate backing
        // state and do not notify the preview controller.
        string member;
        if (TrySetSelectedIndex(profiles, target.NativeIndex, out member))
        {
            // The Survivor Creator intentionally uses only the native ProfilesList + preview,
            // not a second PlayerProfile parent controller. Keep ProfileSDF synchronized
            // explicitly so SDCSPreviewWindow follows this temporary draft selection.
            ProfileSDF.SetSelectedProfile(target.Name);
            TryInvokeSelectionRefresh(profiles);
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" EMBEDDED-SELECT profile='" + Safe(target.Name) +
                "' mode=index member=" + Safe(member) + " profileSdf=explicit"); }
            return true;
        }

        if (TryInvokeProfileSelector(profiles, target) || TryInvokeProfileSelector(host, target))
        {
            ProfileSDF.SetSelectedProfile(target.Name);
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" EMBEDDED-SELECT profile='" + Safe(target.Name) +
                "' mode=method-fallback profileSdf=explicit"); }
            return true;
        }

        error = "The native Player Profile could not be selected. See [REBIRTH Survivor][PlayerProfileBridge] in the output log.";
        Log.Warning(LogPrefix + " EMBEDDED-SELECT failed profile='" + Safe(target.Name) + "' nativeIndex=" + target.NativeIndex);
        DumpObjectMembers(profiles, "EmbeddedProfilesList");
        return false;
    }

    private static string lastSnapshotSource = string.Empty;
    private static int lastSnapshotCount = -1;
    private static readonly Dictionary<string, Archetype> NamedArchetypeCache = new Dictionary<string, Archetype>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> NamedArchetypeMisses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static MethodInfo namedArchetypeGetter;
    private static Type namedArchetypeRegistryType;
    private static bool namedArchetypeRegistryResolved;

    private static bool TryBuildProfileSnapshot(object value, string memberName, out List<EmbeddedProfileInfo> profiles)
    {
        profiles = new List<EmbeddedProfileInfo>();
        if (value == null || value is string) return false;
        IEnumerable enumerable = value as IEnumerable;
        if (enumerable == null) return false;

        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int index = 0;
        try
        {
            foreach (object item in enumerable)
            {
                if (index >= MaxEmbeddedProfileRoster) break;
                string name = string.Empty;
                string text = item as string;
                if (text != null && LooksProfileCollection(memberName) && IsPlausibleName(text)) name = text.Trim();
                else TryExtractProfileObjectName(item, out name);
                if (!string.IsNullOrEmpty(name) && names.Add(name))
                {
                    Archetype archetype;
                    TryResolveArchetypeFromObject(item, out archetype);
                    profiles.Add(new EmbeddedProfileInfo(name, item, archetype, index));
                }
                index++;
            }
        }
        catch
        {
            profiles.Clear();
            return false;
        }
        if (profiles.Count > MaxEmbeddedProfileRoster) { profiles.Clear(); return false; }
        return profiles.Count > 0;
    }

    private static int ProfileCollectionScore(string memberName, List<EmbeddedProfileInfo> profiles)
    {
        if (profiles == null || profiles.Count == 0) return int.MinValue;
        string n = (memberName ?? string.Empty).ToLowerInvariant();
        int score = profiles.Count * 10;
        if (n.Contains("profile")) score += 1000;
        if (n.Contains("all") || n.Contains("available")) score += 200;
        for (int i = 0; i < profiles.Count; i++)
        {
            EmbeddedProfileInfo profile = profiles[i];
            if (profile == null) continue;
            if (profile.Archetype != null) score += 20;
            if (profile.NativeObject != null && profile.NativeObject.GetType().Name.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0) score += 10;
        }
        return score;
    }

    private static void MergeProfileDetails(List<EmbeddedProfileInfo> candidate, Dictionary<string, Archetype> archetypes, Dictionary<string, object> objects)
    {
        if (candidate == null) return;
        for (int i = 0; i < candidate.Count; i++)
        {
            EmbeddedProfileInfo profile = candidate[i];
            if (profile == null || string.IsNullOrEmpty(profile.Name)) continue;
            if (profile.Archetype != null && !archetypes.ContainsKey(profile.Name)) archetypes.Add(profile.Name, profile.Archetype);
            if (profile.NativeObject != null && !(profile.NativeObject is string) && !objects.ContainsKey(profile.Name)) objects.Add(profile.Name, profile.NativeObject);
        }
    }

    public static bool TryResolveArchetypeFromObject(object root, out Archetype archetype)
    {
        archetype = null;
        if (root == null) return false;
        HashSet<object> visited = new HashSet<object>();
        int visitedCount = 0;
        return TryResolveArchetypeRecursive(root, 0, 6, visited, ref visitedCount, out archetype);
    }

    /// <summary>
    /// Resolves a stock/default Player Profile without changing the native ProfilesList
    /// selection. 3.1 ships the default profile names as SDCS Archetype names, so this
    /// bounded/cached lookup lets the roster render those faces without driving the live
    /// preview behind the player's back. Custom profiles intentionally fall through and are
    /// captured only when they are genuinely selected in SDCSPreviewWindow.
    /// </summary>
    public static bool TryResolveNamedArchetype(string profileName, out Archetype archetype, out string source)
    {
        archetype = null;
        source = string.Empty;
        string name = (profileName ?? string.Empty).Trim();
        if (name.Length == 0) return false;

        if (NamedArchetypeCache.TryGetValue(name, out archetype) && archetype != null)
        {
            source = "cache:" + name;
            return true;
        }
        if (NamedArchetypeMisses.Contains(name)) return false;

        ResolveNamedArchetypeRegistry();
        if (namedArchetypeGetter != null)
        {
            try
            {
                archetype = namedArchetypeGetter.Invoke(null, new object[] { name }) as Archetype;
                if (archetype != null)
                {
                    NamedArchetypeCache[name] = archetype;
                    source = (namedArchetypeRegistryType != null ? namedArchetypeRegistryType.Name : "Archetypes") + "." + namedArchetypeGetter.Name;
                    return true;
                }
            }
            catch { archetype = null; }
        }

        if (namedArchetypeRegistryType != null && TryResolveNamedArchetypeFromStaticCollections(namedArchetypeRegistryType, name, out archetype, out source))
        {
            NamedArchetypeCache[name] = archetype;
            return true;
        }

        NamedArchetypeMisses.Add(name);
        return false;
    }

    /// <summary>
    /// Resolves the actual appearance stored by a named native Player Profile. Custom Player
    /// Profiles are not archetype registry keys (their ProfileArchetype is usually BaseMale/
    /// BaseFemale plus SDCS choices), so TryResolveNamedArchetype alone cannot render them.
    /// This helper temporarily loads the requested profile through the same ProfileSDF /
    /// PlayerProfile path used by the base game, builds its temporary SDCS Archetype, then restores
    /// the previously selected native profile before returning.
    /// </summary>
    public static bool TryResolvePlayerProfileArchetype(string profileName, out Archetype archetype, out string source)
    {
        archetype = null;
        source = string.Empty;
        string name = (profileName ?? string.Empty).Trim();
        if (name.Length == 0) return false;

        if (TryResolveNamedArchetype(name, out archetype, out source) && archetype != null)
            return true;

        string restoreName = string.Empty;
        string restoreSource = string.Empty;
        try
        {
            TryResolveActivePlayerProfileName(activeXui, null, out restoreName, out restoreSource);
        }
        catch
        {
            restoreName = string.Empty;
        }

        try
        {
            if (!ProfileSDF.ProfileExists(name))
            {
                source = "ProfileSDF.ProfileExists=false";
                return false;
            }

            ProfileSDF.SetSelectedProfile(name);
            PlayerProfile playerProfile = PlayerProfile.LoadLocalProfile();
            if (playerProfile == null)
            {
                source = "PlayerProfile.LoadLocalProfile=null";
                return false;
            }

            Archetype current = null;
            try { current = playerProfile.CreateTempArchetype(); } catch { current = null; }
            if (current == null)
            {
                current = Archetype.GetArchetype(playerProfile.ProfileArchetype) ??
                    Archetype.GetArchetype(playerProfile.IsMale ? "BaseMale" : "BaseFemale");
            }
            if (current == null)
            {
                source = "PlayerProfile archetype unresolved";
                return false;
            }

            archetype = CloneArchetype(current) ?? current;
            source = "ProfileSDF->PlayerProfile.LoadLocalProfile.CreateTempArchetype";
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" PROFILE-ARCHETYPE profile='" + Safe(name) +
                "' source=" + Safe(source) + " restore='" + Safe(restoreName) + "'"); }
            return true;
        }
        catch (Exception ex)
        {
            source = "profile-load-exception:" + ex.GetType().Name;
            Log.Warning(LogPrefix + " PROFILE-ARCHETYPE failed profile='" + Safe(name) +
                "' error=" + Safe(ex.Message));
            archetype = null;
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(restoreName) &&
                !string.Equals(restoreName, name, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (ProfileSDF.ProfileExists(restoreName))
                        ProfileSDF.SetSelectedProfile(restoreName);
                }
                catch { }
            }
        }
    }


    private static void ResolveNamedArchetypeRegistry()
    {
        if (namedArchetypeRegistryResolved) return;
        namedArchetypeRegistryResolved = true;
        try
        {
            Assembly assembly = typeof(Archetype).Assembly;
            namedArchetypeRegistryType = assembly.GetType("Archetypes");
            if (namedArchetypeRegistryType == null)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types != null)
                {
                    for (int i = 0; i < types.Length; i++)
                    {
                        Type candidate = types[i];
                        if (candidate != null && string.Equals(candidate.Name, "Archetypes", StringComparison.OrdinalIgnoreCase))
                        {
                            namedArchetypeRegistryType = candidate;
                            break;
                        }
                    }
                }
            }
            if (namedArchetypeRegistryType == null) return;

            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods;
            try { methods = namedArchetypeRegistryType.GetMethods(flags); } catch { methods = new MethodInfo[0]; }
            string[] preferred = new string[] { "Get", "GetArchetype", "Find", "FindArchetype" };
            for (int p = 0; p < preferred.Length && namedArchetypeGetter == null; p++)
            {
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, preferred[p], StringComparison.OrdinalIgnoreCase)) continue;
                    if (!typeof(Archetype).IsAssignableFrom(method.ReturnType)) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        namedArchetypeGetter = method;
                        break;
                    }
                }
            }
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" ARCHETYPE-REGISTRY type=" + namedArchetypeRegistryType.FullName +
                " getter=" + (namedArchetypeGetter != null ? namedArchetypeGetter.Name : "<collection-scan>")); }
        }
        catch (Exception ex)
        {
            Log.Warning(LogPrefix + " ARCHETYPE-REGISTRY unavailable: " + ex.GetType().Name + ": " + ex.Message);
            namedArchetypeRegistryType = null;
            namedArchetypeGetter = null;
        }
    }

    private static bool TryResolveNamedArchetypeFromStaticCollections(Type registryType, string name, out Archetype archetype, out string source)
    {
        archetype = null;
        source = string.Empty;
        if (registryType == null) return false;
        BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo[] fields;
        try { fields = registryType.GetFields(flags); } catch { fields = new FieldInfo[0]; }
        for (int i = 0; i < fields.Length; i++)
        {
            object value;
            try { value = fields[i].GetValue(null); } catch { continue; }
            if (TryFindNamedArchetypeInValue(value, name, out archetype))
            {
                source = registryType.Name + "." + fields[i].Name;
                return true;
            }
        }

        PropertyInfo[] properties;
        try { properties = registryType.GetProperties(flags); } catch { properties = new PropertyInfo[0]; }
        for (int i = 0; i < properties.Length; i++)
        {
            PropertyInfo property = properties[i];
            if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
            object value;
            try { value = property.GetValue(null, null); } catch { continue; }
            if (TryFindNamedArchetypeInValue(value, name, out archetype))
            {
                source = registryType.Name + "." + property.Name;
                return true;
            }
        }
        return false;
    }

    private static bool TryFindNamedArchetypeInValue(object value, string name, out Archetype archetype)
    {
        archetype = null;
        if (value == null) return false;
        IDictionary dictionary = value as IDictionary;
        if (dictionary != null)
        {
            try
            {
                if (dictionary.Contains(name))
                {
                    archetype = dictionary[name] as Archetype;
                    if (archetype != null) return true;
                }
                foreach (DictionaryEntry entry in dictionary)
                {
                    Archetype candidate = entry.Value as Archetype;
                    if (candidate == null) continue;
                    string key = entry.Key as string;
                    if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        archetype = candidate;
                        return true;
                    }
                }
            }
            catch { }
        }

        IEnumerable enumerable = value as IEnumerable;
        if (enumerable != null && !(value is string))
        {
            int count = 0;
            try
            {
                foreach (object item in enumerable)
                {
                    if (++count > 256) break;
                    Archetype candidate = item as Archetype;
                    if (candidate != null && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        archetype = candidate;
                        return true;
                    }
                }
            }
            catch { }
        }
        return false;
    }

    /// <summary>
    /// Resolves the Archetype currently rendered by the embedded native SDCS preview.
    /// Player-profile list data in 3.1 contains profile metadata, not an Archetype instance,
    /// so row portraits must source appearance from the same preview controller the game uses.
    /// </summary>
    public static bool TryResolveEmbeddedPreviewArchetype(XUiController creatorRoot, out Archetype archetype, out string source)
    {
        archetype = null;
        source = string.Empty;
        if (creatorRoot == null) return false;

        XUiController preview = creatorRoot.GetChildById("nativePlayerPreview");
        if (preview != null && TryResolveArchetypeFromObject(preview, out archetype) && archetype != null)
        {
            source = "nativePlayerPreview:" + preview.GetType().Name;
            return true;
        }

        XUiController host = creatorRoot.GetChildById("nativePlayerProfileHost");
        if (host != null && TryResolveArchetypeFromObject(host, out archetype) && archetype != null)
        {
            source = "nativePlayerProfileHost:" + host.GetType().Name;
            return true;
        }

        archetype = null;
        return false;
    }

    private static bool TryResolveArchetypeRecursive(object value, int depth, int maxDepth,
        HashSet<object> visited, ref int visitedCount, out Archetype archetype)
    {
        archetype = value as Archetype;
        if (archetype != null) return true;
        archetype = null;
        if (value == null || depth >= maxDepth || value is string || visitedCount >= 192) return false;
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is XUi) return false;
        if (!visited.Add(value)) return false;
        visitedCount++;

        // EModelSDCS and other appearance-bearing components are UnityEngine.Objects. Do not
        // reject them before inspecting their direct Archetype field/property: the live party
        // portrait implementation gets the exact appearance from EModelSDCS.Archetype.
        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType == typeof(Archetype) || typeof(Archetype).IsAssignableFrom(field.FieldType))
                {
                    try { archetype = field.GetValue(value) as Archetype; if (archetype != null) return true; } catch { }
                }
            }
            PropertyInfo[] properties;
            try { properties = scan.GetProperties(flags); } catch { properties = new PropertyInfo[0]; }
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                if (property.PropertyType == typeof(Archetype) || typeof(Archetype).IsAssignableFrom(property.PropertyType))
                {
                    try { archetype = property.GetValue(value, null) as Archetype; if (archetype != null) return true; } catch { }
                }
            }
        }

        // Do not recurse through arbitrary Unity scene objects; direct Archetype members above
        // were already inspected. This keeps the reflection walk bounded while still allowing
        // EModelSDCS.Archetype to resolve.
        if (value is UnityEngine.Object) return false;

        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!LooksAppearanceRelated(field.Name, field.FieldType)) continue;
                object child;
                try { child = field.GetValue(value); } catch { continue; }
                if (TryResolveArchetypeRecursive(child, depth + 1, maxDepth, visited, ref visitedCount, out archetype)) return true;
            }
            PropertyInfo[] properties;
            try { properties = scan.GetProperties(flags); } catch { properties = new PropertyInfo[0]; }
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0 || !LooksAppearanceRelated(property.Name, property.PropertyType)) continue;
                object child;
                try { child = property.GetValue(value, null); } catch { continue; }
                if (TryResolveArchetypeRecursive(child, depth + 1, maxDepth, visited, ref visitedCount, out archetype)) return true;
            }
        }
        return false;
    }

    private static bool LooksAppearanceRelated(string name, Type memberType)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        string t = memberType != null ? memberType.Name.ToLowerInvariant() : string.Empty;
        return n.Contains("profile") || n.Contains("archetype") || n.Contains("character") ||
               n.Contains("appearance") || n.Contains("avatar") || n.Contains("data") ||
               t.Contains("profile") || t.Contains("archetype") || t.Contains("character") ||
               t.Contains("appearance") || t.Contains("sdcs");
    }

    private static bool TryInvokeProfileSelector(object controller, EmbeddedProfileInfo target)
    {
        if (controller == null || target == null) return false;
        Type type = controller.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo[] methods;
        try { methods = type.GetMethods(flags); } catch { return false; }
        string[] preferred = new string[] { "SelectProfile", "SetSelectedProfile", "Select", "SetSelection", "SetSelectedIndex", "SelectIndex", "SetCurrentProfile" };
        for (int p = 0; p < preferred.Length; p++)
        {
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, preferred[p], StringComparison.OrdinalIgnoreCase)) continue;
                ParameterInfo[] args = method.GetParameters();
                if (args.Length != 1) continue;
                object argument;
                if (args[0].ParameterType == typeof(int)) argument = target.NativeIndex;
                else if (target.NativeObject != null && args[0].ParameterType.IsInstanceOfType(target.NativeObject)) argument = target.NativeObject;
                else if (args[0].ParameterType == typeof(string)) argument = target.Name;
                else continue;
                try { method.Invoke(controller, new object[] { argument }); return true; } catch { }
            }
        }
        return false;
    }

    private static bool TrySetSelectedIndex(object controller, int index, out string member)
    {
        member = string.Empty;
        if (controller == null || index < 0) return false;
        Type type = controller.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // IMPORTANT: go through XUiC_List<T>.SelectedEntryIndex's PROPERTY setter first.
        // The setter updates SelectedEntry/CurrentSelectedEntry and fires SelectionChanged,
        // which is what the native PlayerProfile controller uses to rebuild SDCSPreviewWindow.
        // Writing the private selectedEntryIndex backing field (the previous implementation)
        // changes the index textually but bypasses every one of those side effects.
        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            PropertyInfo[] properties;
            try { properties = scan.GetProperties(flags | BindingFlags.DeclaredOnly); } catch { properties = new PropertyInfo[0]; }

            // Prefer the exact native property name before any heuristic match.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (!property.CanWrite || property.PropertyType != typeof(int) || property.GetIndexParameters().Length != 0) continue;
                    bool exact = string.Equals(property.Name, "SelectedEntryIndex", StringComparison.OrdinalIgnoreCase);
                    if ((pass == 0 && !exact) || (pass == 1 && (exact || !LooksSelectionIndex(property.Name)))) continue;
                    try
                    {
                        property.SetValue(controller, index, null);
                        member = scan.Name + "." + property.Name + "(setter)";
                        return true;
                    }
                    catch { }
                }
            }
        }

        // Only fall back to a field on builds where no writable list property exists.
        // This cannot drive the native preview, so the caller logs it distinctly.
        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags | BindingFlags.DeclaredOnly); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType != typeof(int) || !LooksSelectionIndex(field.Name)) continue;
                try
                {
                    field.SetValue(controller, index);
                    member = scan.Name + "." + field.Name + "(field-fallback)";
                    return true;
                }
                catch { }
            }
        }
        return false;
    }

    private static void TryInvokeSelectionRefresh(object controller)
    {
        if (controller == null) return;
        Type type = controller.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo[] methods;
        try { methods = type.GetMethods(flags); } catch { return; }
        string[] names = new string[] { "Refresh", "UpdateSelection", "SelectionChanged", "OnSelectionChanged", "UpdateProfile", "UpdatePreview" };
        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, names[n], StringComparison.OrdinalIgnoreCase) || method.GetParameters().Length != 0) continue;
                try { method.Invoke(controller, null); return; } catch { }
            }
        }
    }

    public static bool OpenNativePicker(XUi xui, XUiC_RebirthSurvivorCreator creator,
        RebirthSurvivorCreatorViewModel creatorModel, out string error)
    {
        error = string.Empty;
        if (active)
        {
            error = "The Player Profile selector is already open.";
            return false;
        }
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null || creator == null || creatorModel == null)
        {
            error = "The native Player Profile selector is unavailable.";
            Log.Error(LogPrefix + " OPEN rejected reason=missing-xui-or-model");
            return false;
        }
        XUiController nativeGroup = xui.FindWindowGroupByName(NativeProfilesGroup);
        if (nativeGroup == null)
        {
            error = "The base-game Player Profile screen was not found in this XUi context.";
            Log.Error(LogPrefix + " OPEN rejected reason=playerProfiles-group-not-found purpose=" + creatorModel.Purpose);
            return false;
        }

        activeXui = xui;
        owner = creator;
        model = creatorModel;
        active = true;
        sawProfilesOpen = false;
        lastProfilesOpen = false;
        lastCreateOpen = false;
        dumpedReflection = false;
        createDialogText = string.Empty;
        lastCandidate = creatorModel.PlayerProfileName ?? string.Empty;
        lastCandidateSource = lastCandidate.Length > 0 ? "survivor-existing-choice" : string.Empty;

        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" OPEN begin purpose=" + creatorModel.Purpose +
            " existing='" + Safe(lastCandidate) + "'"); }

        try
        {
            creator.SuspendForNativePlayerProfilePicker();
            xui.playerUI.windowManager.Open(NativeProfilesGroup, true);
            bool opened = xui.playerUI.windowManager.IsWindowOpen(NativeProfilesGroup);
            sawProfilesOpen = opened;
            lastProfilesOpen = opened;
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" OPEN native playerProfiles requested open=" + opened); }
            if (!opened)
            {
                error = "The base-game Player Profile screen did not open.";
                Finish(false, error);
                return false;
            }
            CaptureNativeSelection(xui, "open");
            return true;
        }
        catch (Exception ex)
        {
            error = "The base-game Player Profile screen could not be opened.";
            Log.Error(LogPrefix + " OPEN exception=" + ex);
            Finish(false, error);
            return false;
        }
    }

    /// <summary>
    /// Called by the still-open Survivor Profile manager while the native menu is on top.
    /// This avoids adding another Harmony patch just to poll menu XUi state.
    /// </summary>
    public static void Tick(XUi xui)
    {
        if (!active || xui == null || xui != activeXui || xui.playerUI == null || xui.playerUI.windowManager == null)
            return;

        GUIWindowManager manager = xui.playerUI.windowManager;
        bool profilesOpen = manager.IsWindowOpen(NativeProfilesGroup);
        bool createOpen = manager.IsWindowOpen(NativeCreateGroup);

        if (profilesOpen != lastProfilesOpen || createOpen != lastCreateOpen)
        {
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" STATE playerProfiles=" + profilesOpen +
                " playerProfilesCreate=" + createOpen +
                " candidate='" + Safe(lastCandidate) + "' source=" + Safe(lastCandidateSource)); }
            lastProfilesOpen = profilesOpen;
            lastCreateOpen = createOpen;
        }

        if (profilesOpen)
        {
            sawProfilesOpen = true;
            CaptureNativeSelection(xui, "tick");
        }
        if (createOpen)
            CaptureCreateDialogText(xui);

        // The native Create dialog closes back to playerProfiles. Only return to Rebirth
        // once both native groups are gone (the user pressed Back on the profile picker).
        if (sawProfilesOpen && !profilesOpen && !createOpen)
        {
            string fallback;
            string fallbackSource;
            if (string.IsNullOrEmpty(lastCandidate) && TryResolveActivePlayerProfileName(xui, null, out fallback, out fallbackSource))
            {
                lastCandidate = fallback;
                lastCandidateSource = fallbackSource;
            }
            string message = string.IsNullOrEmpty(lastCandidate)
                ? "No Player Profile selection could be resolved. Reopen Player Profile and select one; diagnostic details were written to the output log."
                : "Player Profile selected: " + lastCandidate;
            Finish(!string.IsNullOrEmpty(lastCandidate), message);
        }
    }

    /// <summary>
    /// Resolves the selection from the native ProfilesList embedded directly inside
    /// the Rebirth Survivor Creator. This lets Rebirth reuse the base game's profile
    /// data source and selection logic without opening the separate playerProfiles page.
    /// </summary>
    public static bool TryResolveEmbeddedPlayerProfileName(XUiController creatorRoot, out string name, out string source)
    {
        name = string.Empty;
        source = string.Empty;
        if (creatorRoot == null) return false;

        XUiController profiles = creatorRoot.GetChildById("profiles");
        if (profiles != null && TryResolveSelectedMember(profiles.GetType(), profiles, false, "EmbeddedProfilesList", out name, out source))
            return true;

        XUiController host = creatorRoot.GetChildById("nativePlayerProfileHost");
        if (host != null && TryResolveSelectedMember(host.GetType(), host, false, "EmbeddedPlayerProfile", out name, out source))
            return true;

        return false;
    }

    public static bool TryGetEmbeddedPlayerProfileSelectedIndex(XUiController creatorRoot, out int index)
    {
        index = -1;
        if (creatorRoot == null) return false;
        XUiController host = creatorRoot.GetChildById("nativePlayerProfileHost");
        XUiController profiles = host != null ? host.GetChildById("profiles") : null;
        if (profiles == null) return false;
        string source;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return TryGetSelectedIndex(profiles.GetType(), profiles, flags, out index, out source);
    }

    public static bool TryGetEmbeddedPlayerProfileCount(XUiController creatorRoot, out int count)
    {
        count = 0;
        if (creatorRoot == null) return false;
        XUiController host = creatorRoot.GetChildById("nativePlayerProfileHost");
        XUiController profiles = host != null ? host.GetChildById("profiles") : null;
        return TryGetProfileCollectionCount(profiles, out count);
    }

    private static bool TryGetProfileCollectionCount(object instance, out int count)
    {
        count = 0;
        if (instance == null) return false;
        Type type = instance.GetType();
        int best = 0;
        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!LooksProfileCollection(field.Name)) continue;
                object value;
                try { value = field.GetValue(instance); } catch { continue; }
                int candidate;
                if (TryGetCollectionCount(value, out candidate) && candidate > best && candidate <= MaxEmbeddedProfileRoster) best = candidate;
            }
            PropertyInfo[] properties;
            try { properties = scan.GetProperties(flags); } catch { properties = new PropertyInfo[0]; }
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0 || !LooksProfileCollection(property.Name)) continue;
                object value;
                try { value = property.GetValue(instance, null); } catch { continue; }
                int candidate;
                if (TryGetCollectionCount(value, out candidate) && candidate > best && candidate <= MaxEmbeddedProfileRoster) best = candidate;
            }
        }
        if (best <= 0) return false;
        count = best;
        return true;
    }

    private static bool TryGetCollectionCount(object value, out int count)
    {
        count = 0;
        if (value == null || value is string) return false;
        ICollection collection = value as ICollection;
        if (collection != null)
        {
            count = collection.Count;
            return true;
        }
        IEnumerable enumerable = value as IEnumerable;
        if (enumerable == null) return false;
        int n = 0;
        try
        {
            foreach (object item in enumerable)
            {
                n++;
                if (n > 128) break;
            }
        }
        catch { return false; }
        count = n;
        return n > 0;
    }

    public static bool TryResolveActivePlayerProfileName(XUi xui, EntityPlayerLocal player, out string name)
    {
        string source;
        return TryResolveActivePlayerProfileName(xui, player, out name, out source);
    }

    public static bool TryResolveActivePlayerProfileName(XUi xui, EntityPlayerLocal player, out string name, out string source)
    {
        name = string.Empty;
        source = string.Empty;

        // The embedded ProfilesList selection is only a UI cursor and may default to the
        // first/list-local entry. It is NOT the player's globally active Player Profile.
        // Resolve durable/current game state first so opening Survivor Creator defaults to
        // the same appearance the player would use when starting a normal game.
        if (TryResolveProfileFromObject(player, "EntityPlayerLocal", out name, out source))
            return true;
        if (xui != null && xui.playerUI != null && TryResolveProfileFromObject(xui.playerUI, "PlayerUI", out name, out source))
            return true;
        if (GameManager.Instance != null && TryResolveProfileFromObject(GameManager.Instance, "GameManager", out name, out source))
            return true;

        // Last-resort static scan. Keep it conservative: only inspect types whose names
        // explicitly contain PlayerProfile, and only members that look current/selected/active.
        try
        {
            Assembly assembly = typeof(GameManager).Assembly;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            if (types != null)
            {
                for (int i = 0; i < types.Length; i++)
                {
                    Type t = types[i];
                    if (t == null || t.Name.IndexOf("PlayerProfile", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (TryResolveSelectedMember(t, null, true, "static:" + t.FullName, out name, out source))
                        return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(LogPrefix + " active-profile static scan exception=" + ex.GetType().Name + ": " + ex.Message);
        }

        // Last fallback only: a native list cursor is better than no initial association, but
        // callers can see from the source string that this was not resolved from global state.
        if (xui != null && TryResolveNativeSelectedProfileName(xui, out name, out source))
        {
            source = "fallback-native-list:" + source;
            return true;
        }
        return false;
    }

    private static void CaptureNativeSelection(XUi xui, string phase)
    {
        string name;
        string source;
        if (TryResolveNativeSelectedProfileName(xui, out name, out source) && !string.IsNullOrEmpty(name))
        {
            if (!string.Equals(lastCandidate, name, StringComparison.Ordinal) || !string.Equals(lastCandidateSource, source, StringComparison.Ordinal))
            {
                lastCandidate = name;
                lastCandidateSource = source;
                { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" SELECT phase=" + phase + " profile='" + Safe(name) + "' source=" + Safe(source)); }
            }
            return;
        }

        if (!dumpedReflection)
        {
            dumpedReflection = true;
            DumpNativeProfileReflection(xui);
        }
    }

    private static void CaptureCreateDialogText(XUi xui)
    {
        try
        {
            XUiController group = xui.FindWindowGroupByName(NativeCreateGroup);
            XUiC_TextInput input = group != null ? group.GetChildById("createProfileName") as XUiC_TextInput : null;
            string value = input != null ? (input.Text ?? string.Empty).Trim() : string.Empty;
            if (value.Length > 0 && !string.Equals(value, createDialogText, StringComparison.Ordinal))
            {
                createDialogText = value;
                { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" CREATE-DIALOG name-entered='" + Safe(value) + "'"); }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(LogPrefix + " CREATE-DIALOG probe exception=" + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TryResolveNativeSelectedProfileName(XUi xui, out string name, out string source)
    {
        name = string.Empty;
        source = string.Empty;
        if (xui == null) return false;
        XUiController group = xui.FindWindowGroupByName(NativeProfilesGroup);
        if (group == null) return false;
        XUiController profiles = group.GetChildById("profiles");
        if (profiles != null && TryResolveSelectedMember(profiles.GetType(), profiles, false, "ProfilesList", out name, out source))
            return true;
        if (TryResolveSelectedMember(group.GetType(), group, false, "PlayerProfileWindow", out name, out source))
            return true;
        return false;
    }

    private static bool TryResolveProfileFromObject(object root, string prefix, out string name, out string source)
    {
        name = string.Empty;
        source = string.Empty;
        if (root == null) return false;
        Type type = root.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo field in type.GetFields(flags))
        {
            string member = field.Name ?? string.Empty;
            object value;
            try { value = field.GetValue(root); } catch { continue; }
            if (value == null) continue;
            if (value.GetType().Name.IndexOf("PlayerProfile", StringComparison.OrdinalIgnoreCase) >= 0 &&
                TryExtractProfileObjectName(value, out name))
            {
                source = prefix + "." + member;
                return true;
            }
        }
        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
            object value;
            try { value = property.GetValue(root, null); } catch { continue; }
            if (value == null) continue;
            if (value.GetType().Name.IndexOf("PlayerProfile", StringComparison.OrdinalIgnoreCase) >= 0 &&
                TryExtractProfileObjectName(value, out name))
            {
                source = prefix + "." + property.Name;
                return true;
            }
        }
        return false;
    }

    private static bool TryResolveSelectedMember(Type type, object instance, bool staticOnly, string prefix,
        out string name, out string source)
    {
        name = string.Empty;
        source = string.Empty;
        if (type == null) return false;
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (staticOnly ? BindingFlags.Static : BindingFlags.Instance);

        // Prefer explicitly selected/current/active members.
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (!LooksSelected(field.Name)) continue;
            object value;
            try { value = field.GetValue(instance); } catch { continue; }
            if (TryExtractCandidate(value, field.Name, out name))
            {
                source = prefix + "." + field.Name;
                return true;
            }
        }
        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0 || !LooksSelected(property.Name)) continue;
            object value;
            try { value = property.GetValue(instance, null); } catch { continue; }
            if (TryExtractCandidate(value, property.Name, out name))
            {
                source = prefix + "." + property.Name;
                return true;
            }
        }

        // Common list-controller shape: selected index + profiles/items collection.
        int selectedIndex;
        string indexSource;
        if (TryGetSelectedIndex(type, instance, flags, out selectedIndex, out indexSource) && selectedIndex >= 0)
        {
            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (!LooksProfileCollection(field.Name)) continue;
                object value;
                try { value = field.GetValue(instance); } catch { continue; }
                if (TryExtractIndexed(value, selectedIndex, out name))
                {
                    source = prefix + "." + field.Name + "[" + selectedIndex + "] via " + indexSource;
                    return true;
                }
            }
            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0 || !LooksProfileCollection(property.Name)) continue;
                object value;
                try { value = property.GetValue(instance, null); } catch { continue; }
                if (TryExtractIndexed(value, selectedIndex, out name))
                {
                    source = prefix + "." + property.Name + "[" + selectedIndex + "] via " + indexSource;
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TryGetSelectedIndex(Type type, object instance, BindingFlags flags, out int index, out string source)
    {
        index = -1;
        source = string.Empty;
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (!LooksSelectionIndex(field.Name)) continue;
            try
            {
                object value = field.GetValue(instance);
                if (value is int) { index = (int)value; source = field.Name; return true; }
            }
            catch { }
        }
        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0 || !LooksSelectionIndex(property.Name)) continue;
            try
            {
                object value = property.GetValue(instance, null);
                if (value is int) { index = (int)value; source = property.Name; return true; }
            }
            catch { }
        }
        return false;
    }

    private static bool TryExtractIndexed(object value, int index, out string name)
    {
        name = string.Empty;
        if (value == null || index < 0) return false;
        IList list = value as IList;
        if (list != null && index < list.Count)
            return TryExtractCandidate(list[index], "indexed", out name);
        IEnumerable enumerable = value as IEnumerable;
        if (enumerable == null || value is string) return false;
        int i = 0;
        foreach (object item in enumerable)
        {
            if (i++ == index) return TryExtractCandidate(item, "indexed", out name);
        }
        return false;
    }

    private static bool TryExtractCandidate(object value, string memberName, out string name)
    {
        name = string.Empty;
        if (value == null) return false;
        string text = value as string;
        if (text != null && LooksProfileNameMember(memberName) && IsPlausibleName(text))
        {
            name = text.Trim();
            return true;
        }
        return TryExtractProfileObjectName(value, out name);
    }

    private static bool TryExtractProfileObjectName(object value, out string name)
    {
        name = string.Empty;
        if (value == null) return false;
        Type type = value.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string[] preferred = new string[] { "ProfileName", "profileName", "Name", "name", "PlayerName", "playerName" };
        for (int i = 0; i < preferred.Length; i++)
        {
            FieldInfo field = type.GetField(preferred[i], flags);
            if (field != null && field.FieldType == typeof(string))
            {
                string text;
                try { text = field.GetValue(value) as string; } catch { text = null; }
                if (IsPlausibleName(text)) { name = text.Trim(); return true; }
            }
            PropertyInfo property = type.GetProperty(preferred[i], flags);
            if (property != null && property.CanRead && property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
            {
                string text;
                try { text = property.GetValue(value, null) as string; } catch { text = null; }
                if (IsPlausibleName(text)) { name = text.Trim(); return true; }
            }
        }
        return false;
    }

    private static void DumpNativeProfileReflection(XUi xui)
    {
        try
        {
            XUiController group = xui != null ? xui.FindWindowGroupByName(NativeProfilesGroup) : null;
            XUiController profiles = group != null ? group.GetChildById("profiles") : null;
            Log.Warning(LogPrefix + " RESOLVE no selected Player Profile candidate yet; dumping relevant native controller members.");
            DumpObjectMembers(profiles, "ProfilesList");
            DumpObjectMembers(group, "PlayerProfileWindow");
        }
        catch (Exception ex)
        {
            Log.Warning(LogPrefix + " RESOLVE reflection dump failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void DumpObjectMembers(object value, string prefix)
    {
        if (value == null)
        {
            Log.Warning(LogPrefix + " REFLECT " + prefix + "=<null>");
            return;
        }
        Type type = value.GetType();
        Log.Warning(LogPrefix + " REFLECT " + prefix + " type=" + type.FullName);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        int emitted = 0;
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (!LooksDiagnostic(field.Name) || emitted >= 40) continue;
            object memberValue;
            try { memberValue = field.GetValue(value); } catch { memberValue = null; }
            Log.Warning(LogPrefix + " REFLECT field " + field.Name + " type=" + field.FieldType.Name + " value=" + Describe(memberValue));
            emitted++;
        }
        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0 || !LooksDiagnostic(property.Name) || emitted >= 40) continue;
            object memberValue;
            try { memberValue = property.GetValue(value, null); } catch { memberValue = null; }
            Log.Warning(LogPrefix + " REFLECT property " + property.Name + " type=" + property.PropertyType.Name + " value=" + Describe(memberValue));
            emitted++;
        }
    }

    private static string Describe(object value)
    {
        if (value == null) return "<null>";
        string text = value as string;
        if (text != null) return "'" + Safe(text) + "'";
        return "<" + value.GetType().FullName + ">";
    }

    private static bool LooksSelected(string name)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        return n.Contains("selected") || n.Contains("current") || n.Contains("active");
    }

    private static bool LooksSelectionIndex(string name)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        return (n.Contains("selected") || n.Contains("selection") || n.Contains("current")) && n.Contains("index");
    }

    private static bool LooksProfileCollection(string name)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        return n.Contains("profile") || n.Contains("item") || n.Contains("entry") || n.Contains("data") || n.Contains("list");
    }

    private static bool LooksProfileNameMember(string name)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        return n.Contains("name") && (n.Contains("profile") || n.Contains("selected") || n.Contains("current") || n.Contains("active"));
    }

    private static bool LooksDiagnostic(string name)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        return n.Contains("profile") || n.Contains("selected") || n.Contains("selection") || n.Contains("current") ||
               n.Contains("active") || n.Contains("index") || n == "name";
    }

    private static bool IsPlausibleName(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        string text = value.Trim();
        if (text.Length == 0 || text.Length > 80) return false;
        if (text.IndexOf('/') >= 0 || text.IndexOf('\\') >= 0 || text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0) return false;
        return true;
    }


    public static bool TryFindEmbeddedProfile(XUiController creatorRoot, string profileName, out EmbeddedProfileInfo profile)
    {
        profile = null;
        if (string.IsNullOrEmpty(profileName)) return false;
        List<EmbeddedProfileInfo> profiles;
        if (!TryGetEmbeddedPlayerProfiles(creatorRoot, out profiles) || profiles == null) return false;
        for (int i = 0; i < profiles.Count; i++)
        {
            EmbeddedProfileInfo candidate = profiles[i];
            if (candidate != null && string.Equals(candidate.Name, profileName, StringComparison.OrdinalIgnoreCase))
            {
                profile = candidate;
                return true;
            }
        }
        return false;
    }

    public static bool TrySelectEmbeddedPlayerProfileByName(XUiController creatorRoot, string profileName, out string error)
    {
        EmbeddedProfileInfo profile;
        if (!TryFindEmbeddedProfile(creatorRoot, profileName, out profile))
        {
            error = "Player Profile '" + (profileName ?? string.Empty) + "' was not found.";
            return false;
        }
        return TrySelectEmbeddedPlayerProfile(creatorRoot, profile, out error);
    }

    private static bool TryReadEditorMember<T>(object instance, string memberName, out T value, out string source) where T : class
    {
        value = null;
        source = string.Empty;
        if (instance == null || string.IsNullOrEmpty(memberName)) return false;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type scan = instance.GetType(); scan != null; scan = scan.BaseType)
        {
            try
            {
                FieldInfo field = scan.GetField(memberName, flags | BindingFlags.IgnoreCase);
                if (field != null && typeof(T).IsAssignableFrom(field.FieldType))
                {
                    value = field.GetValue(instance) as T;
                    if (value != null)
                    {
                        source = scan.Name + "." + field.Name;
                        return true;
                    }
                }
            }
            catch { value = null; }

            try
            {
                PropertyInfo property = scan.GetProperty(memberName, flags | BindingFlags.IgnoreCase);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0 && typeof(T).IsAssignableFrom(property.PropertyType))
                {
                    value = property.GetValue(instance, null) as T;
                    if (value != null)
                    {
                        source = scan.Name + "." + property.Name;
                        return true;
                    }
                }
            }
            catch { value = null; }
        }
        return false;
    }

    private static bool TryReadEditorMemberByType<T>(object instance, out T value, out string source) where T : class
    {
        value = null;
        source = string.Empty;
        if (instance == null) return false;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type scan = instance.GetType(); scan != null; scan = scan.BaseType)
        {
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!typeof(T).IsAssignableFrom(field.FieldType)) continue;
                try { value = field.GetValue(instance) as T; } catch { value = null; }
                if (value != null)
                {
                    source = scan.Name + "." + field.Name;
                    return true;
                }
            }

            PropertyInfo[] properties;
            try { properties = scan.GetProperties(flags); } catch { properties = new PropertyInfo[0]; }
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0 || !typeof(T).IsAssignableFrom(property.PropertyType)) continue;
                try { value = property.GetValue(instance, null) as T; } catch { value = null; }
                if (value != null)
                {
                    source = scan.Name + "." + property.Name;
                    return true;
                }
            }
        }
        return false;
    }

    public static bool TryResolveCustomCharacterPlayerProfile(XUi xui, out PlayerProfile playerProfile, out string source)
    {
        playerProfile = null;
        source = string.Empty;
        if (xui == null) return false;

        XUiController group = xui.FindWindowGroupByName("customCharacterSystem");
        if (group == null) return false;

        // FindWindowGroupByName returns the generic window-group root (XUiController), not the
        // XUiC_CustomCharacterWindowGroup controller that owns the live editor state. 3.1's
        // own OptionsProfiles.OpenCustomCharacterWindow path resolves the typed controller as
        // a child of that root. Do the same here and read the exact PlayerProfile clone that
        // the native editor mutates while the user changes race/face/hair/etc.
        XUiC_CustomCharacterWindowGroup editor = group as XUiC_CustomCharacterWindowGroup;
        if (editor == null)
        {
            try { editor = group.GetChildByType<XUiC_CustomCharacterWindowGroup>(); }
            catch { editor = null; }
        }

        // The stock game keeps this member non-public in the assembly referenced by the mod
        // project even though decompiled/publicized source can display it as public. Never take a
        // compile-time dependency on editor.playerProfile; resolve the live clone by reflection.
        if (editor != null && TryReadEditorMember<PlayerProfile>(editor, "playerProfile", out playerProfile, out source))
        {
            source = "customCharacterSystem:" + editor.GetType().Name + "." + source;
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" EDITOR-PLAYERPROFILE resolved rootType=" + group.GetType().FullName +
                " editorType=" + editor.GetType().FullName + " member=" + Safe(source) +
                " profileArchetype='" + Safe(playerProfile.ProfileArchetype) + "'"); }
            return true;
        }

        // Bounded type-based fallback for a future game build that renames the member while still
        // retaining a PlayerProfile field/property on the concrete editor controller.
        if (editor != null && TryReadEditorMemberByType<PlayerProfile>(editor, out playerProfile, out source))
        {
            source = "customCharacterSystem:" + editor.GetType().Name + "." + source;
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" EDITOR-PLAYERPROFILE resolved-type-fallback source=" + Safe(source)); }
            return true;
        }

        Log.Warning(LogPrefix + " EDITOR-PLAYERPROFILE unresolved rootType=" + group.GetType().FullName +
            " editorType=" + (editor != null ? editor.GetType().FullName : "<null>"));
        return false;
    }

    public static bool TryResolveCustomCharacterArchetype(XUi xui, out Archetype archetype, out string source)
    {
        archetype = null;
        source = string.Empty;
        if (xui == null) return false;

        // Prefer the exact PlayerProfile that the 3.1 editor is mutating. This is important for
        // user-created BaseMale/BaseFemale profiles because the editor has a PlayerProfile and
        // may have no standalone Archetype object for the current unsaved changes.
        PlayerProfile playerProfile;
        string playerProfileSource;
        if (TryResolveCustomCharacterPlayerProfile(xui, out playerProfile, out playerProfileSource) && playerProfile != null)
        {
            try
            {
                Archetype current = playerProfile.CreateTempArchetype();
                if (current != null)
                {
                    archetype = CloneArchetype(current) ?? current;
                    source = playerProfileSource + ".CreateTempArchetype";
                    return true;
                }
            }
            catch { archetype = null; }
        }

        XUiController group = xui.FindWindowGroupByName("customCharacterSystem");
        if (group != null)
        {
            XUiC_CustomCharacterWindowGroup editor = group as XUiC_CustomCharacterWindowGroup;
            if (editor == null)
            {
                try { editor = group.GetChildByType<XUiC_CustomCharacterWindowGroup>(); }
                catch { editor = null; }
            }

            Archetype editorArchetype;
            string editorArchetypeMember;
            if (editor != null && TryReadEditorMember<Archetype>(editor, "archetype", out editorArchetype, out editorArchetypeMember) && editorArchetype != null)
            {
                archetype = CloneArchetype(editorArchetype) ?? editorArchetype;
                source = "customCharacterSystem:" + editor.GetType().Name + "." + editorArchetypeMember;
                return true;
            }

            if (editor != null && TryResolveArchetypeFromObject(editor, out archetype) && archetype != null)
            {
                source = "customCharacterSystem:" + editor.GetType().Name + ":archetype-fallback";
                return true;
            }
        }
        return false;
    }

    public static bool TrySaveCustomCharacterPlayerProfile(XUi xui, string profileName, out string source, out string error)
    {
        source = string.Empty;
        error = string.Empty;
        if (string.IsNullOrEmpty(profileName))
        {
            error = "The Player Profile name is unavailable.";
            return false;
        }

        if (IsReservedTemporaryProfileName(profileName) && !ProfileSDF.ProfileExists(profileName))
        {
            error = "The RBTemp_ prefix is reserved for REBIRTH temporary Player Profiles.";
            return false;
        }

        PlayerProfile playerProfile;
        if (!TryResolveCustomCharacterPlayerProfile(xui, out playerProfile, out source) || playerProfile == null)
        {
            error = "The Player Profile appearance could not be read from the native editor.";
            return false;
        }

        try
        {
            // Exact persistence shape used by XUiC_CustomCharacterWindowGroup.BtnApply_OnPress
            // when its playerProfile field is non-null. We intentionally do the save here so
            // the stock Apply handler cannot navigate away to the vanilla OptionsProfiles page.
            ProfileSDF.SaveProfile(profileName, playerProfile.ProfileArchetype, playerProfile.IsMale,
                playerProfile.RaceName, playerProfile.VariantNumber, playerProfile.EyeColor,
                playerProfile.HairName, playerProfile.HairColor, playerProfile.MustacheName,
                playerProfile.ChopsName, playerProfile.BeardName);
            ProfileSDF.SetSelectedProfile(profileName);
            ProfileSDF.Save();
            InvalidateEmbeddedProfileSnapshot();
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" PROFILE-SAVE-EDITOR profile='" + Safe(profileName) +
                "' source=" + Safe(source) + " archetype='" + Safe(playerProfile.ProfileArchetype) + "'"); }
            return true;
        }
        catch (Exception ex)
        {
            error = "Player Profile save failed: " + ex.Message;
            Log.Warning(LogPrefix + " PROFILE-SAVE-EDITOR failed profile='" + Safe(profileName) +
                "' source=" + Safe(source) + " error=" + Safe(ex.Message));
            return false;
        }
    }

    public static long ComputeAppearanceSignature(Archetype archetype)
    {
        if (archetype == null) return 0L;
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            Action<string> add = delegate(string value)
            {
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++) { hash ^= text[i]; hash *= 1099511628211UL; }
                hash ^= 0xff; hash *= 1099511628211UL;
            };
            add("rebirth-profile-accessor-v" + ProfileAccessorContractVersion.ToString());
            try { add(typeof(Archetype).Assembly.FullName); } catch { }
            add(archetype.Name); add(archetype.Race); add(archetype.Variant.ToString());
            add(archetype.Hair); add(archetype.HairColor); add(archetype.MustacheName);
            add(archetype.ChopsName); add(archetype.BeardName); add(archetype.EyeColorName);
            add(archetype.IsMale ? "male" : "female");

            // Hash every readable scalar member as well as the known public appearance contract.
            // Reference-valued native internals are deliberately not shared or serialized into the key.
            Type type = archetype.GetType();
            for (Type scan = type; scan != null && scan != typeof(object); scan = scan.BaseType)
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                FieldInfo[] fields; try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
                Array.Sort(fields, delegate(FieldInfo a, FieldInfo b) { return string.Compare(a.Name,b.Name,StringComparison.Ordinal); });
                for (int i=0;i<fields.Length;i++)
                {
                    FieldInfo field=fields[i]; if(field.IsStatic || !IsAppearanceScalar(field.FieldType)) continue;
                    object value; try { value=field.GetValue(archetype); } catch { continue; }
                    add(scan.FullName+".f:"+field.Name+"="+Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture));
                }
                PropertyInfo[] props; try { props=scan.GetProperties(flags); } catch { props=new PropertyInfo[0]; }
                Array.Sort(props, delegate(PropertyInfo a, PropertyInfo b) { return string.Compare(a.Name,b.Name,StringComparison.Ordinal); });
                for(int i=0;i<props.Length;i++)
                {
                    PropertyInfo prop=props[i]; if(!prop.CanRead || prop.GetIndexParameters().Length!=0 || !IsAppearanceScalar(prop.PropertyType)) continue;
                    object value; try { value=prop.GetValue(archetype,null); } catch { continue; }
                    add(scan.FullName+".p:"+prop.Name+"="+Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            long result=(long)(hash & 0x7fffffffffffffffUL);
            return result==0L?1L:result;
        }
    }

    public static string BuildAppearanceCacheKey(string profileName, Archetype archetype, string renderIdentity)
    {
        return (profileName ?? string.Empty).Trim().ToLowerInvariant() + "|g=" + ProfileCatalogueGeneration.ToString() +
            "|a=" + ComputeAppearanceSignature(archetype).ToString("X16") + "|r=" + (renderIdentity ?? string.Empty);
    }

    private static bool IsAppearanceScalar(Type type)
    {
        if(type==null)return false;
        return type.IsEnum || type.IsPrimitive || type==typeof(string) || type==typeof(decimal);
    }

    public static Archetype CloneArchetype(Archetype source)
    {
        if (source == null) return null;
        try
        {
            MethodInfo clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            return clone != null ? clone.Invoke(source, null) as Archetype : null;
        }
        catch { return null; }
    }

    public static bool CopyArchetype(Archetype source, Archetype target, out string error)
    {
        error = string.Empty;
        if (source == null || target == null)
        {
            error = "The temporary Player Profile appearance is unavailable.";
            return false;
        }

        int copied = 0;
        Type type = typeof(Archetype);
        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic || field.IsInitOnly || field.IsLiteral) continue;
                try { field.SetValue(target, field.GetValue(source)); copied++; } catch { }
            }
            PropertyInfo[] properties;
            try { properties = scan.GetProperties(flags); } catch { properties = new PropertyInfo[0]; }
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0) continue;
                try { property.SetValue(target, property.GetValue(source, null), null); copied++; } catch { }
            }
        }
        if (copied <= 0)
        {
            error = "The Player Profile appearance could not be copied into the final native profile.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Invokes a native XUi button's already-wired press delegate. Used only for the
    /// temporary unsaved-profile create flow so Rebirth can auto-submit an internal
    /// temporary name without showing the native name-first dialog to the player.
    /// </summary>
    public static bool TryInvokeButtonPress(XUiController button, out string error)
    {
        error = string.Empty;
        if (button == null)
        {
            error = "Native button is unavailable.";
            return false;
        }

        Type type = button.GetType();
        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                string name = field.Name ?? string.Empty;
                if (name.IndexOf("press", StringComparison.OrdinalIgnoreCase) < 0 || !typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                Delegate handler;
                try { handler = field.GetValue(button) as Delegate; } catch { handler = null; }
                if (handler == null) continue;
                try
                {
                    handler.DynamicInvoke(button, 0);
                    return true;
                }
                catch { }
            }
        }

        // Fallback for controllers exposing an OnPressed/Press method rather than a delegate field.
        BindingFlags methodFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo[] methods;
        try { methods = type.GetMethods(methodFlags); } catch { methods = new MethodInfo[0]; }
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            string name = method.Name ?? string.Empty;
            if (name.IndexOf("press", StringComparison.OrdinalIgnoreCase) < 0) continue;
            ParameterInfo[] args = method.GetParameters();
            try
            {
                if (args.Length == 0) { method.Invoke(button, null); return true; }
                if (args.Length == 2 && args[0].ParameterType.IsAssignableFrom(typeof(XUiController)) && args[1].ParameterType == typeof(int))
                { method.Invoke(button, new object[] { button, 0 }); return true; }
            }
            catch { }
        }

        error = "The native button press handler could not be invoked.";
        return false;
    }

    /// <summary>
    /// Resolves the base game's own "can_modify_profile" state for the currently selected
    /// Player Profile. Bundled/default profiles are intentionally read-only in 3.1; user-created
    /// profiles may be edited/deleted. Rebirth owns the visible buttons, so it queries the native
    /// controller instead of leaving an XML binding attached to a stale hidden list cursor.
    /// </summary>
    public static bool TryCanModifyEmbeddedPlayerProfile(XUiController creatorRoot, out bool canModify, out string source)
    {
        canModify = false;
        source = string.Empty;
        if (creatorRoot == null) return false;

        string selectedName;
        string selectedSource;
        if (!TryResolveEmbeddedPlayerProfileName(creatorRoot, out selectedName, out selectedSource) ||
            string.IsNullOrEmpty(selectedName))
        {
            source = "base-logic:selected-profile-unresolved";
            Log.Warning(LogPrefix + " MODIFY-BASE unresolved selectedSource=" + Safe(selectedSource));
            return false;
        }

        try
        {
            // Exact 3.1 XUiC_OptionsProfiles.updateButtonStates behavior:
            // load the selected PlayerProfile, resolve its archetype (falling back to BaseMale/
            // BaseFemale), and use Archetype.CanCustomize for both Edit and Delete.
            ProfileSDF.SetSelectedProfile(selectedName);
            PlayerProfile playerProfile = PlayerProfile.LoadLocalProfile();
            if (playerProfile == null)
            {
                source = "base-logic:PlayerProfile.LoadLocalProfile=null";
                return false;
            }

            Archetype archetype = Archetype.GetArchetype(playerProfile.ProfileArchetype) ??
                Archetype.GetArchetype(playerProfile.IsMale ? "BaseMale" : "BaseFemale");
            canModify = archetype != null && archetype.CanCustomize;
            source = "base-logic:Archetype.CanCustomize archetype=" +
                (archetype != null ? archetype.Name : "<null>");
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" MODIFY-BASE profile='" + Safe(selectedName) +
                "' canModify=" + canModify + " selectedSource=" + Safe(selectedSource) +
                " profileArchetype='" + Safe(playerProfile.ProfileArchetype) +
                "' source=" + Safe(source)); }
            return true;
        }
        catch (Exception ex)
        {
            source = "base-logic:exception:" + ex.GetType().Name;
            Log.Warning(LogPrefix + " MODIFY-BASE failed profile='" + Safe(selectedName) +
                "' error=" + Safe(ex.Message));
            return false;
        }
    }

    private static bool TryReadBooleanBinding(object controller, string bindingName, out bool value, out string source)
    {
        value = false;
        source = string.Empty;
        if (controller == null) return false;
        Type type = controller.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            MethodInfo[] methods;
            try { methods = scan.GetMethods(flags | BindingFlags.DeclaredOnly); } catch { methods = new MethodInfo[0]; }
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name.IndexOf("GetBindingValue", StringComparison.OrdinalIgnoreCase) < 0) continue;
                ParameterInfo[] args = method.GetParameters();
                if (args.Length != 2 || args[1].ParameterType != typeof(string)) continue;
                Type first = args[0].ParameterType;
                if (first != typeof(string).MakeByRefType() && first != typeof(string)) continue;
                object[] invokeArgs = new object[] { string.Empty, bindingName };
                try
                {
                    object handled = method.Invoke(controller, invokeArgs);
                    if (handled is bool && !(bool)handled) continue;
                    string text = invokeArgs[0] != null ? invokeArgs[0].ToString() : string.Empty;
                    bool parsed;
                    if (bool.TryParse(text, out parsed))
                    {
                        value = parsed;
                        source = scan.Name + "." + method.Name + "(" + bindingName + ")";
                        return true;
                    }
                    int number;
                    if (int.TryParse(text, out number))
                    {
                        value = number != 0;
                        source = scan.Name + "." + method.Name + "(" + bindingName + ")";
                        return true;
                    }
                }
                catch { }
            }
        }
        return false;
    }

    private static bool TryReadProfileModifyBooleanRecursive(object root, out bool value, out string source)
    {
        value = false;
        source = string.Empty;
        if (root == null) return false;
        HashSet<object> visited = new HashSet<object>();
        int visitedCount = 0;
        return TryReadProfileModifyBooleanRecursive(root, 0, visited, ref visitedCount, out value, out source);
    }

    private static bool TryReadProfileModifyBooleanRecursive(object instance, int depth, HashSet<object> visited, ref int visitedCount, out bool value, out string source)
    {
        value = false;
        source = string.Empty;
        if (instance == null || depth > 4 || visitedCount >= 96 || instance is string) return false;
        Type type = instance.GetType();
        if (type.IsPrimitive || type.IsEnum || instance is UnityEngine.Object || instance is XUi) return false;
        if (!visited.Add(instance)) return false;
        visitedCount++;

        string directSource;
        if (TryReadBooleanMember(instance, new string[] { "CanModifyProfile", "CanModify", "CanEdit", "IsCustom", "IsUserProfile", "IsUser" }, false, out value, out directSource))
        {
            source = "recursive:" + directSource;
            return true;
        }
        if (TryReadBooleanMember(instance, new string[] { "IsDefault", "IsBuiltIn", "IsBuiltin", "IsStock", "IsReadOnly" }, true, out value, out directSource))
        {
            source = "recursive:" + directSource;
            return true;
        }

        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                if (!LooksProfileModifyContainer(fields[i].Name, fields[i].FieldType)) continue;
                object child;
                try { child = fields[i].GetValue(instance); } catch { continue; }
                string childSource;
                if (TryReadProfileModifyBooleanRecursive(child, depth + 1, visited, ref visitedCount, out value, out childSource))
                {
                    source = scan.Name + "." + fields[i].Name + "->" + childSource;
                    return true;
                }
            }

            PropertyInfo[] properties;
            try { properties = scan.GetProperties(flags); } catch { properties = new PropertyInfo[0]; }
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0 || !LooksProfileModifyContainer(property.Name, property.PropertyType)) continue;
                object child;
                try { child = property.GetValue(instance, null); } catch { continue; }
                string childSource;
                if (TryReadProfileModifyBooleanRecursive(child, depth + 1, visited, ref visitedCount, out value, out childSource))
                {
                    source = scan.Name + "." + property.Name + "->" + childSource;
                    return true;
                }
            }
        }
        return false;
    }

    private static bool LooksProfileModifyContainer(string name, Type memberType)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        string t = memberType != null ? memberType.Name.ToLowerInvariant() : string.Empty;
        return n.Contains("profile") || n.Contains("entry") || n.Contains("item") || n.Contains("data") ||
               n.Contains("selected") || n.Contains("value") || n.Contains("character") ||
               t.Contains("profile") || t.Contains("entry") || t.Contains("character");
    }

    private static bool TryReadBooleanMember(object instance, string[] names, bool invert, out bool value, out string source)
    {
        value = false;
        source = string.Empty;
        if (instance == null || names == null) return false;
        Type type = instance.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            for (int n = 0; n < names.Length; n++)
            {
                string wanted = names[n];
                FieldInfo field = scan.GetField(wanted, flags | BindingFlags.DeclaredOnly | BindingFlags.IgnoreCase);
                if (field != null && field.FieldType == typeof(bool))
                {
                    try
                    {
                        bool raw = (bool)field.GetValue(instance);
                        value = invert ? !raw : raw;
                        source = scan.Name + "." + field.Name;
                        return true;
                    }
                    catch { }
                }
                PropertyInfo property = scan.GetProperty(wanted, flags | BindingFlags.DeclaredOnly | BindingFlags.IgnoreCase);
                if (property != null && property.CanRead && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        bool raw = (bool)property.GetValue(instance, null);
                        value = invert ? !raw : raw;
                        source = scan.Name + "." + property.Name;
                        return true;
                    }
                    catch { }
                }
            }
        }
        return false;
    }

    public static bool TrySelectPlayerProfileForPreview(XUiController root, string previewId, string profileName, out string error)
    {
        error=string.Empty;
        if(root==null||string.IsNullOrEmpty(previewId)||string.IsNullOrEmpty(profileName)){error="Player preview selection is unavailable.";return false;}
        try
        {
            if(!ProfileSDF.ProfileExists(profileName)){error="Player Profile '"+profileName+"' does not exist.";return false;}
            ProfileSDF.SetSelectedProfile(profileName);
            XUiController preview=root.GetChildById(previewId);
            if(preview==null){error="SDCS preview controller '"+previewId+"' is unavailable.";return false;}
            TryInvokeSelectionRefresh(preview);
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" PREVIEW-SELECT profile='"+Safe(profileName)+"' preview="+Safe(previewId)); }
            return true;
        }
        catch(Exception ex){error="Player preview selection failed: "+ex.Message;Log.Warning(LogPrefix+" PREVIEW-SELECT failed profile='"+Safe(profileName)+"' error="+Safe(ex.Message));return false;}
    }

    public static bool TryTogglePlayerPreviewView(XUiController root,string previewId,out string error)
    {
        error=string.Empty;
        if(root==null||string.IsNullOrEmpty(previewId)){error="Player preview is unavailable.";return false;}
        XUiController preview=root.GetChildById(previewId);
        if(preview==null){error="SDCS preview controller '"+previewId+"' is unavailable.";return false;}
        string[] names={"ToggleView","ChangeView","ToggleZoom","SwitchView","CycleView","ToggleCamera"};
        BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        MethodInfo[] methods;
        try{methods=preview.GetType().GetMethods(flags);}catch(Exception ex){error=ex.Message;return false;}
        for(int n=0;n<names.Length;n++)for(int i=0;i<methods.Length;i++)
        {
            MethodInfo method=methods[i];
            if(!string.Equals(method.Name,names[n],StringComparison.OrdinalIgnoreCase)||method.GetParameters().Length!=0)continue;
            try{method.Invoke(preview,null);{ if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" PREVIEW-VIEW method="+method.Name); }return true;}catch{}
        }
        error="This game build did not expose a compatible SDCS preview view-toggle method.";
        return false;
    }

    private static string TemporaryOwnershipDirectory
    {
        get { return Path.Combine(GameIO.GetUserGameDataDir(), "Rebirth", "TemporaryPlayerProfiles", "v1"); }
    }

    public static bool IsReservedTemporaryProfileName(string profileName)
    {
        return RebirthTemporaryProfileOwnership.IsReservedName(profileName);
    }

    internal static void ForgetTemporaryOwnership(string profileName)
    {
        RebirthTemporaryProfileOwnership.Forget(TemporaryOwnershipDirectory, profileName);
    }

    public static bool IsOwnedTemporaryProfile(string profileName)
    {
        return RebirthTemporaryProfileWriteGuard.IsReady &&
            RebirthTemporaryProfileOwnership.IsOwned(TemporaryOwnershipDirectory, profileName);
    }

    public static System.Collections.Generic.IEnumerable<string> GetOwnedTemporaryProfileNames(int limit)
    {
        return RebirthTemporaryProfileOwnership.GetOwnedNames(TemporaryOwnershipDirectory, limit);
    }

    public static bool TryCreateOwnedTemporaryProfile(out string profileName, out string error)
    {
        profileName = string.Empty;
        error = string.Empty;
        try
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string token;
                string candidate = RebirthTemporaryProfileOwnership.NewName(out token);
                if (ProfileSDF.ProfileExists(candidate)) continue;
                RebirthTemporaryProfileOwnership.Begin(TemporaryOwnershipDirectory, candidate, token);
                // Retain the name on failure for diagnosis, but a pending marker grants no
                // cleanup permission. Never infer a successful native commit from an exception.
                profileName = candidate;
                using (RebirthTemporaryProfileWriteGuard.OwnWrite(candidate))
                    if (!TryCreateBlankPlayerProfileCore(candidate, out error)) return false;
                RebirthTemporaryProfileOwnership.Commit(TemporaryOwnershipDirectory, candidate);
                return true;
            }
            error = "Could not allocate a unique temporary Player Profile name.";
        }
        catch (Exception ex)
        {
            error = "Temporary Player Profile creation/ownership failed; any ambiguous profile was preserved: " + ex.Message;
            Log.Warning(LogPrefix + " " + error);
        }
        return false;
    }

    public static bool TryCreateBlankPlayerProfile(string profileName, out string error)
    {
        if (IsReservedTemporaryProfileName(profileName))
        {
            error = "The RBTemp_ prefix is reserved for REBIRTH temporary Player Profiles.";
            return false;
        }
        return TryCreateBlankPlayerProfileCore(profileName, out error);
    }

    private static bool TryCreateBlankPlayerProfileCore(string profileName, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrEmpty(profileName))
        {
            error = "Player Profile name is empty.";
            return false;
        }
        try
        {
            if (ProfileSDF.ProfileExists(profileName))
            {
                error = "A Player Profile with that name already exists.";
                return false;
            }
            // Exact defaults used by 3.1 XUiC_OptionsProfiles.BtnConfirmCreate_OnPressed.
            ProfileSDF.SaveProfile(profileName, string.Empty, true, "White", 1, "Blue01",
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            ProfileSDF.SetSelectedProfile(profileName);
            ProfileSDF.Save();
            InvalidateEmbeddedProfileSnapshot();
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" PROFILE-CREATE-DIRECT profile='" + Safe(profileName) + "'"); }
            return true;
        }
        catch (Exception ex)
        {
            error = "Player Profile creation failed: " + ex.Message;
            Log.Warning(LogPrefix + " PROFILE-CREATE-DIRECT failed profile='" + Safe(profileName) +
                "' error=" + Safe(ex.Message));
            return false;
        }
    }

    public static bool TrySavePlayerProfileFromArchetype(string profileName, Archetype appearance, out string error)
    {
        if (IsReservedTemporaryProfileName(profileName))
        {
            error = "The RBTemp_ prefix is reserved for REBIRTH temporary Player Profiles.";
            return false;
        }
        return TrySavePlayerProfileFromArchetypeCore(profileName, appearance, out error);
    }

    internal static bool TrySaveOwnedTemporaryProfileFromArchetype(string profileName, Archetype appearance, out string error)
    {
        if (!IsOwnedTemporaryProfile(profileName))
        {
            error = "Temporary Player Profile ownership could not be verified; profile preserved.";
            return false;
        }
        using (RebirthTemporaryProfileWriteGuard.OwnWrite(profileName))
            return TrySavePlayerProfileFromArchetypeCore(profileName, appearance, out error);
    }

    private static bool TrySavePlayerProfileFromArchetypeCore(string profileName, Archetype appearance, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrEmpty(profileName) || appearance == null)
        {
            error = "Player Profile appearance is unavailable.";
            return false;
        }
        try
        {
            string baseArchetype = appearance.IsMale ? "BaseMale" : "BaseFemale";
            ProfileSDF.SaveProfile(profileName, baseArchetype, appearance.IsMale,
                appearance.Race ?? string.Empty, appearance.Variant, appearance.EyeColorName ?? "Blue01",
                appearance.Hair ?? string.Empty, appearance.HairColor ?? string.Empty,
                appearance.MustacheName ?? string.Empty, appearance.ChopsName ?? string.Empty,
                appearance.BeardName ?? string.Empty);
            ProfileSDF.SetSelectedProfile(profileName);
            ProfileSDF.Save();
            InvalidateEmbeddedProfileSnapshot();
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" PROFILE-SAVE-DIRECT profile='" + Safe(profileName) +
                "' archetype='" + Safe(baseArchetype) + "'"); }
            return true;
        }
        catch (Exception ex)
        {
            error = "Player Profile save failed: " + ex.Message;
            Log.Warning(LogPrefix + " PROFILE-SAVE-DIRECT failed profile='" + Safe(profileName) +
                "' error=" + Safe(ex.Message));
            return false;
        }
    }

    public static bool TryDeletePlayerProfileDirect(string profileName, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrEmpty(profileName)) return true;
        try
        {
            if (ProfileSDF.ProfileExists(profileName))
            {
                ProfileSDF.DeleteProfile(profileName);
                ProfileSDF.Save();
            }
            InvalidateEmbeddedProfileSnapshot();
            try { RebirthTemporaryProfileOwnership.Forget(TemporaryOwnershipDirectory, profileName); }
            catch (Exception ex) { Log.Warning(LogPrefix + " Temporary ownership record cleanup failed: " + ex.Message); }
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" PROFILE-DELETE-DIRECT profile='" + Safe(profileName) + "'"); }
            return true;
        }
        catch (Exception ex)
        {
            error = "Player Profile deletion failed: " + ex.Message;
            Log.Warning(LogPrefix + " PROFILE-DELETE-DIRECT failed profile='" + Safe(profileName) +
                "' error=" + Safe(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Removes an internal RBTemp_* profile as part of a Rebirth-owned cancellation.
    /// This path is restricted to internal temporary names and must never leave a delete prompt
    /// for the player or be used for a player-requested permanent-profile deletion.
    /// </summary>
    public static bool TryDeleteEmbeddedPlayerProfileSilently(XUiController creatorRoot, string profileName, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrEmpty(profileName)) return true;
        if (!IsOwnedTemporaryProfile(profileName))
        {
            error = "Silent deletion requires a verified REBIRTH ownership record; profile preserved.";
            return false;
        }

        // Never drive the native Delete button for an internal REBIRTH temporary profile.
        // Native deletion intentionally creates user-facing confirmation UI. RBTemp_* is an
        // implementation detail, so remove it directly from ProfileSDF with no message box.
        bool deleted = TryDeletePlayerProfileDirect(profileName, out error);
        if (deleted)
        {
            // Deleting ProfileSDF data does not refresh the already-open native list.
            // A stale row can reselect the deleted name, producing an empty SDCS
            // archetype (and a headless preview) on the next frame.
            XUiController host = creatorRoot?.GetChildById("nativePlayerProfileHost");
            var profiles = (host?.GetChildById("profiles") ?? creatorRoot?.GetChildById("profiles")) as XUiC_ProfilesList;
            profiles?.RebuildList(false);
            try { RebirthTemporaryProfileOwnership.Forget(TemporaryOwnershipDirectory, profileName); }
            catch (Exception ex) { Log.Warning(LogPrefix + " Temporary ownership record cleanup failed: " + ex.Message); }
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" TEMP-DELETE-SILENT profile='" + Safe(profileName) + "' via=ProfileSDF.DeleteProfile"); }
        }
        return deleted;
    }

    private static string Safe(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\r", " ").Replace("\n", " ").Replace("'", "");
    }

    private static void Finish(bool resolved, string message)
    {
        XUiC_RebirthSurvivorCreator resumeOwner = owner;
        RebirthSurvivorCreatorViewModel resumeModel = model;
        string candidate = lastCandidate;
        string source = lastCandidateSource;
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge(" RETURN resolved=" + resolved + " profile='" + Safe(candidate) +
            "' source=" + Safe(source) + " createDialogName='" + Safe(createDialogText) + "'"); }

        active = false;
        activeXui = null;
        owner = null;
        model = null;
        sawProfilesOpen = false;
        lastProfilesOpen = false;
        lastCreateOpen = false;
        dumpedReflection = false;
        lastCandidate = string.Empty;
        lastCandidateSource = string.Empty;
        createDialogText = string.Empty;

        if (resumeOwner != null && resumeModel != null)
            resumeOwner.ResumeAfterNativePlayerProfilePicker(resolved ? candidate : string.Empty, resolved, message);
    }
}
