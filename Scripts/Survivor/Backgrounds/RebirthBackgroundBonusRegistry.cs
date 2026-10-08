using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

#nullable disable

/// <summary>
/// Data-driven authority for Background Signature Bonus definitions. This registry contains no
/// gameplay mutations; owning chunks consume the stable handler/profile tokens after source audit.
/// </summary>
public static class RebirthBackgroundBonusRegistry
{
    public const int SchemaVersion = 1;
    public const string FileName = "background_bonuses.xml";
    public const string CleanSlateBackgroundId = "background.clean_slate";
    public const string PendingIconState = "pending_chunk_c";

    private static readonly object Gate = new object();
    private static Dictionary<string, RebirthBackgroundBonusDefinition> byId = Empty();
    private static Dictionary<string, RebirthBackgroundBonusDefinition> byBackground = Empty();
    private static ReadOnlyCollection<RebirthBackgroundBonusDefinition> all = new ReadOnlyCollection<RebirthBackgroundBonusDefinition>(new List<RebirthBackgroundBonusDefinition>());
    private static string semanticHash = string.Empty;
    private static bool ready;

    public static bool IsReady { get { return ready; } }
    public static int Count { get { return all.Count; } }
    public static string SemanticHash { get { return semanticHash; } }
    public static ReadOnlyCollection<RebirthBackgroundBonusDefinition> All { get { return all; } }

    public static string Install(string configRoot)
    {
        if (!RebirthSurvivorDefinitionRegistry.IsReady) throw new InvalidOperationException("Survivor definition registry must be installed before Background Signature Bonuses.");
        if (string.IsNullOrWhiteSpace(configRoot)) throw new ArgumentException("Config root is unavailable.", "configRoot");
        string path = Path.Combine(configRoot, FileName);
        if (!File.Exists(path)) throw new FileNotFoundException("Background Signature Bonus data file is missing.", path);

        XmlDocument doc = new XmlDocument();
        doc.PreserveWhitespace = false;
        doc.Load(path);
        XmlElement root = doc.DocumentElement;
        if (root == null || !string.Equals(root.Name, "background_signature_bonuses", StringComparison.Ordinal))
            throw new InvalidDataException("Background Signature Bonus root element is invalid.");
        int schema;
        if (!int.TryParse(root.GetAttribute("schema_version"), NumberStyles.Integer, CultureInfo.InvariantCulture, out schema) || schema != SchemaVersion)
            throw new InvalidDataException("Background Signature Bonus schema_version must be " + SchemaVersion + ".");
        string cleanSlate = Clean(root.GetAttribute("clean_slate_background_id"));
        if (!string.Equals(cleanSlate, CleanSlateBackgroundId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Clean Slate authority must remain '" + CleanSlateBackgroundId + "'.");

        Dictionary<string, RebirthBackgroundBonusDefinition> nextById = Empty();
        Dictionary<string, RebirthBackgroundBonusDefinition> nextByBackground = Empty();
        HashSet<string> iconKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<RebirthBackgroundBonusDefinition> ordered = new List<RebirthBackgroundBonusDefinition>();
        foreach (XmlNode node in root.ChildNodes)
        {
            XmlElement e = node as XmlElement;
            if (e == null || !string.Equals(e.Name, "bonus", StringComparison.Ordinal)) continue;
            string id = Required(e, "id");
            string backgroundId = RebirthSurvivorDefinitionRegistry.NormalizeLegacyBackgroundId(Required(e, "background_id"));
            string nameKey = Required(e, "name_key");
            string descriptionKey = Required(e, "description_key");
            string iconKey = Required(e, "icon_key");
            string iconState = Required(e, "icon_state");
            string category = Required(e, "category");
            string handler = Required(e, "handler");
            string profile = Required(e, "profile");
            if (!id.StartsWith("background_bonus.", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid bonus ID prefix: " + id);
            if (string.Equals(backgroundId, CleanSlateBackgroundId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Clean Slate cannot bind a Signature Bonus.");
            RebirthBackgroundDefinition background;
            if (!RebirthSurvivorDefinitionRegistry.TryGetBackground(backgroundId, out background) || background == null)
                throw new InvalidDataException("Signature Bonus references unknown Background: " + backgroundId);
            if (nextById.ContainsKey(id)) throw new InvalidDataException("Duplicate Signature Bonus ID: " + id);
            if (nextByBackground.ContainsKey(backgroundId)) throw new InvalidDataException("Background has more than one Signature Bonus: " + backgroundId);
            if (!iconKeys.Add(iconKey)) throw new InvalidDataException("Duplicate Signature Bonus icon key: " + iconKey);

            List<RebirthBackgroundBonusTuningValue> tuning = new List<RebirthBackgroundBonusTuningValue>();
            HashSet<string> tuningKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode child in e.ChildNodes)
            {
                XmlElement t = child as XmlElement;
                if (t == null || !string.Equals(t.Name, "tuning", StringComparison.Ordinal)) continue;
                string key = Required(t, "key");
                string value = Required(t, "value");
                bool locked;
                if (!bool.TryParse(Required(t, "locked"), out locked)) throw new InvalidDataException("Invalid tuning locked flag for " + id + "/" + key);
                if (!tuningKeys.Add(key)) throw new InvalidDataException("Duplicate tuning key for " + id + ": " + key);
                tuning.Add(new RebirthBackgroundBonusTuningValue(key, value, locked));
            }

            RebirthBackgroundBonusDefinition definition = new RebirthBackgroundBonusDefinition(id, backgroundId, nameKey, descriptionKey, iconKey, iconState, category, handler, profile, tuning);
            nextById.Add(id, definition);
            nextByBackground.Add(backgroundId, definition);
            ordered.Add(definition);
        }

        ValidateCoverage(nextByBackground);
        string hash = ComputeHash(ordered);
        lock (Gate)
        {
            byId = nextById;
            byBackground = nextByBackground;
            all = new ReadOnlyCollection<RebirthBackgroundBonusDefinition>(ordered);
            semanticHash = hash;
            ready = true;
        }
        return "background signature bonus registry installed bonuses=" + ordered.Count + " cleanSlate=none hash=" + hash;
    }

    public static void Clear()
    {
        lock (Gate)
        {
            byId = Empty();
            byBackground = Empty();
            all = new ReadOnlyCollection<RebirthBackgroundBonusDefinition>(new List<RebirthBackgroundBonusDefinition>());
            semanticHash = string.Empty;
            ready = false;
        }
    }

    public static bool TryGet(string id, out RebirthBackgroundBonusDefinition definition)
    {
        definition = null;
        return ready && !string.IsNullOrWhiteSpace(id) && byId.TryGetValue(id.Trim(), out definition);
    }

    public static bool TryGetByBackground(string backgroundId, out RebirthBackgroundBonusDefinition definition)
    {
        definition = null;
        if (!ready || string.IsNullOrWhiteSpace(backgroundId)) return false;
        string normalized = RebirthSurvivorDefinitionRegistry.NormalizeLegacyBackgroundId(backgroundId);
        if (string.Equals(normalized, CleanSlateBackgroundId, StringComparison.OrdinalIgnoreCase)) return false;
        return byBackground.TryGetValue(normalized, out definition);
    }

    private static void ValidateCoverage(Dictionary<string, RebirthBackgroundBonusDefinition> bonusByBackground)
    {
        int backgroundCount = 0;
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null) throw new InvalidOperationException("Survivor definitions disappeared while validating Background Signature Bonuses.");
        for (int i = 0; i < bundle.Backgrounds.Count; i++)
        {
            RebirthBackgroundDefinition background = bundle.Backgrounds[i];
            if (background == null) continue;
            backgroundCount++;
            bool clean = string.Equals(background.Id, CleanSlateBackgroundId, StringComparison.OrdinalIgnoreCase);
            bool has = bonusByBackground.ContainsKey(background.Id);
            if (clean && has) throw new InvalidDataException("Clean Slate must not have a Signature Bonus.");
            if (!clean && !has) throw new InvalidDataException("Background is missing a Signature Bonus: " + background.Id);
        }
        if (backgroundCount != 28) throw new InvalidDataException("Expected authoritative 28 Backgrounds but found " + backgroundCount + ".");
        if (bonusByBackground.Count != backgroundCount - 1) throw new InvalidDataException("Expected exactly 27 Signature Bonus bindings but found " + bonusByBackground.Count + ".");
    }

    private static string ComputeHash(List<RebirthBackgroundBonusDefinition> values)
    {
        StringBuilder b = new StringBuilder();
        List<RebirthBackgroundBonusDefinition> sorted = new List<RebirthBackgroundBonusDefinition>(values);
        sorted.Sort(delegate(RebirthBackgroundBonusDefinition a, RebirthBackgroundBonusDefinition c) { return string.Compare(a.Id, c.Id, StringComparison.OrdinalIgnoreCase); });
        for (int i = 0; i < sorted.Count; i++)
        {
            RebirthBackgroundBonusDefinition d = sorted[i];
            b.Append(d.Id).Append('|').Append(d.BackgroundId).Append('|').Append(d.NameKey).Append('|').Append(d.DescriptionKey).Append('|')
                .Append(d.IconKey).Append('|').Append(d.IconState).Append('|').Append(d.Category).Append('|').Append(d.Handler).Append('|').Append(d.Profile).Append('\n');
            for (int t = 0; t < d.Tuning.Count; t++)
            {
                RebirthBackgroundBonusTuningValue v = d.Tuning[t];
                b.Append("  ").Append(v.Key).Append('=').Append(v.Value).Append('|').Append(v.Locked).Append('\n');
            }
        }
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()));
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }
    }

    private static Dictionary<string, RebirthBackgroundBonusDefinition> Empty() { return new Dictionary<string, RebirthBackgroundBonusDefinition>(StringComparer.OrdinalIgnoreCase); }
    private static string Required(XmlElement e, string attribute)
    {
        string value = Clean(e.GetAttribute(attribute));
        if (value.Length == 0) throw new InvalidDataException("Missing required '" + attribute + "' on <" + e.Name + ">.");
        return value;
    }
    private static string Clean(string value) { return (value ?? string.Empty).Trim(); }
}
