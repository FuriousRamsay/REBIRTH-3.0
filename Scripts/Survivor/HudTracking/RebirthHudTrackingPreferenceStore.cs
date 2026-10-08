using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

#nullable disable

public sealed class RebirthHudTrackingPreferenceContext
{
    public string WorldKey = string.Empty;
    public string PlayerStorageKey = string.Empty;
    public string FilePath = string.Empty;
    public string DebugKey { get { return WorldKey + "/" + PlayerStorageKey; } }
}

/// <summary>
/// Client-local HUD preference store. The dedicated server never owns these choices. Values in
/// the file are presentation selections only; authoritative progression remains in owner state.
/// </summary>
public static class RebirthHudTrackingPreferenceStore
{
    public const int CurrentSchemaVersion = 1;
    private const string RootName = "rebirthHudTracking";

    public static bool TryResolveCurrentContext(out RebirthHudTrackingPreferenceContext context, out string error)
    {
        context = null;
        error = string.Empty;
        try
        {
            RebirthStablePlayerIdentity identity;
            if (!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity) || identity == null || string.IsNullOrEmpty(identity.StorageKey))
            {
                error = "local stable player identity is unavailable";
                return false;
            }
            string saveDir = (GameIO.GetSaveGameDir() ?? string.Empty).Trim();
            if (saveDir.Length == 0)
            {
                error = "current save-game directory is unavailable";
                return false;
            }
            string normalizedWorldPath = NormalizeWorldPath(saveDir);
            if (normalizedWorldPath.Length == 0)
            {
                error = "normalized world path is empty";
                return false;
            }
            string worldKey = RebirthStablePlayerIdentity.ComputeStorageKey(normalizedWorldPath);
            string userData = (GameIO.GetUserGameDataDir() ?? string.Empty).Trim();
            if (userData.Length == 0)
            {
                error = "user game-data directory is unavailable";
                return false;
            }
            string path = Path.Combine(userData, "RebirthData", "HudTracking", worldKey, identity.StorageKey + ".xml");
            context = new RebirthHudTrackingPreferenceContext
            {
                WorldKey = worldKey,
                PlayerStorageKey = identity.StorageKey,
                FilePath = path
            };
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    public static bool TryLoadCurrent(out RebirthHudTrackingPreferences preferences, out RebirthHudTrackingPreferenceContext context, out bool recoveredBackup, out string error)
    {
        preferences = new RebirthHudTrackingPreferences();
        recoveredBackup = false;
        if (!TryResolveCurrentContext(out context, out error)) return false;
        return TryLoad(context, out preferences, out recoveredBackup, out error);
    }

    public static bool TryLoad(RebirthHudTrackingPreferenceContext context, out RebirthHudTrackingPreferences preferences, out bool recoveredBackup, out string error)
    {
        preferences = new RebirthHudTrackingPreferences();
        recoveredBackup = false;
        error = string.Empty;
        if (context == null || string.IsNullOrEmpty(context.FilePath))
        {
            error = "preference context is missing";
            return false;
        }
        if (!File.Exists(context.FilePath) && !File.Exists(context.FilePath + ".bak"))
            return true;
        XDocument document;
        if (!RebirthAtomicXmlFile.TryLoadFinalThenBackup(context.FilePath, out document, out recoveredBackup, out error))
            return false;
        string parseError;
        if (TryParse(document, out preferences, out parseError))
        {
            error = recoveredBackup ? error : string.Empty;
            return true;
        }

        // A newer schema is intentionally protected from downgrade/overwrite. Do not hide it by
        // silently selecting an older backup. For other semantic corruption, a last-good backup
        // remains a valid recovery source.
        if (parseError.IndexOf("newer than supported schema", StringComparison.OrdinalIgnoreCase) >= 0 || recoveredBackup)
        {
            error = parseError;
            return false;
        }

        XDocument backupDocument;
        string backupLoadError;
        string backupPath = context.FilePath + ".bak";
        if (RebirthAtomicXmlFile.TryLoad(backupPath, out backupDocument, out backupLoadError))
        {
            RebirthHudTrackingPreferences backupPreferences;
            string backupParseError;
            if (TryParse(backupDocument, out backupPreferences, out backupParseError))
            {
                preferences = backupPreferences;
                recoveredBackup = true;
                error = "final semantic parse failed (" + parseError + "); recovered from backup";
                return true;
            }
            backupLoadError = backupParseError;
        }
        error = "final semantic parse failed (" + parseError + "); backup failed (" + backupLoadError + ")";
        return false;
    }

    public static bool TrySaveCurrent(RebirthHudTrackingPreferences preferences, out RebirthHudTrackingPreferenceContext context, out string error)
    {
        if (!TryResolveCurrentContext(out context, out error)) return false;
        return TrySave(context, preferences, out error);
    }

    public static bool TrySave(RebirthHudTrackingPreferenceContext context, RebirthHudTrackingPreferences preferences, out string error)
    {
        error = string.Empty;
        if (context == null || string.IsNullOrEmpty(context.FilePath)) { error = "preference context is missing"; return false; }
        RebirthHudTrackingPreferences value = preferences != null ? preferences.Clone() : new RebirthHudTrackingPreferences();
        value.SchemaVersion = CurrentSchemaVersion;
        value.NormalizeOrder();
        XDocument document = Serialize(value);
        return RebirthAtomicXmlFile.TryWrite(context.FilePath, document, out error);
    }

    public static XDocument Serialize(RebirthHudTrackingPreferences preferences)
    {
        RebirthHudTrackingPreferences value = preferences != null ? preferences : new RebirthHudTrackingPreferences();
        value.NormalizeOrder();
        XElement root = new XElement(RootName,
            new XAttribute("schema_version", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("enabled", value.Enabled ? "true" : "false"),
            new XAttribute("show_contextual_active_skill", value.ShowContextualActiveSkill ? "true" : "false"));
        List<Tuple<int, XElement>> rows = new List<Tuple<int, XElement>>();
        for (int i = 0; i < value.Entries.Count; i++)
        {
            RebirthHudTrackingEntryPreference row = value.Entries[i];
            if (row == null || string.IsNullOrEmpty(row.StableId)) continue;
            rows.Add(Tuple.Create(row.Order, new XElement("entry",
                new XAttribute("type", RebirthHudTrackingId.ToTypeToken(row.Type)),
                new XAttribute("id", row.StableId),
                new XAttribute("order", row.Order.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("enabled", row.Enabled ? "true" : "false"))));
        }
        for (int i = 0; i < value.UnknownEntries.Count; i++)
        {
            RebirthHudTrackingUnknownEntry row = value.UnknownEntries[i];
            if (row == null || string.IsNullOrEmpty(row.TypeToken) || string.IsNullOrEmpty(row.StableId)) continue;
            rows.Add(Tuple.Create(row.Order, new XElement("entry",
                new XAttribute("type", row.TypeToken),
                new XAttribute("id", row.StableId),
                new XAttribute("order", row.Order.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("enabled", row.Enabled ? "true" : "false"))));
        }
        rows.Sort(delegate(Tuple<int, XElement> a, Tuple<int, XElement> b) { return a.Item1.CompareTo(b.Item1); });
        for (int i = 0; i < rows.Count; i++) root.Add(rows[i].Item2);
        return new XDocument(root);
    }

    public static bool TryParse(XDocument document, out RebirthHudTrackingPreferences preferences, out string error)
    {
        preferences = new RebirthHudTrackingPreferences();
        error = string.Empty;
        try
        {
            if (document == null || document.Root == null || !string.Equals(document.Root.Name.LocalName, RootName, StringComparison.OrdinalIgnoreCase))
            {
                error = "HUD tracking XML root is invalid";
                return false;
            }
            XElement root = document.Root;
            int schema = ReadInt(root.Attribute("schema_version"), 0);
            if (schema > CurrentSchemaVersion)
            {
                error = "HUD tracking schema " + schema + " is newer than supported schema " + CurrentSchemaVersion;
                return false;
            }
            RebirthHudTrackingPreferences result = new RebirthHudTrackingPreferences
            {
                SchemaVersion = CurrentSchemaVersion,
                Enabled = ReadBool(root.Attribute("enabled"), true),
                ShowContextualActiveSkill = ReadBool(root.Attribute("show_contextual_active_skill"), false)
            };
            int fallbackOrder = 0;
            foreach (XElement element in root.Elements("entry"))
            {
                string typeToken = ((string)element.Attribute("type") ?? string.Empty).Trim();
                string id = RebirthHudTrackingId.NormalizeStableId((string)element.Attribute("id"));
                if (typeToken.Length == 0 || id.Length == 0) continue;
                int order = ReadInt(element.Attribute("order"), fallbackOrder++);
                bool enabled = ReadBool(element.Attribute("enabled"), true);
                RebirthHudTrackType type;
                if (RebirthHudTrackingId.TryParseTypeToken(typeToken, out type))
                {
                    bool duplicate = false;
                    for (int i = 0; i < result.Entries.Count; i++)
                    {
                        RebirthHudTrackingEntryPreference existing = result.Entries[i];
                        if (existing != null && existing.Type == type && string.Equals(existing.StableId, id, StringComparison.OrdinalIgnoreCase)) { duplicate = true; break; }
                    }
                    if (!duplicate) result.Entries.Add(new RebirthHudTrackingEntryPreference { Type = type, StableId = id, Order = order, Enabled = enabled });
                }
                else
                {
                    result.UnknownEntries.Add(new RebirthHudTrackingUnknownEntry { TypeToken = typeToken, StableId = id, Order = order, Enabled = enabled });
                }
            }
            result.NormalizeOrder();
            preferences = result;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static string NormalizeWorldPath(string value)
    {
        string path = (value ?? string.Empty).Trim();
        try { path = Path.GetFullPath(path); } catch { }
        path = path.Replace('\\', '/').TrimEnd('/');
        return path.ToLowerInvariant();
    }

    private static int ReadInt(XAttribute attribute, int fallback)
    {
        int value;
        return attribute != null && int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    private static bool ReadBool(XAttribute attribute, bool fallback)
    {
        bool value;
        return attribute != null && bool.TryParse(attribute.Value, out value) ? value : fallback;
    }
}
