using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

#nullable disable

/// <summary>
/// Runtime projection of the current edible-food catalogue for Survivor Diet presentation.
///
/// Revision 2.2 lifecycle rule:
/// - The authoritative Diet/Mood food list is synchronously preloaded from Config/_Survivor/food_content.xml
///   during REBIRTH InitMod, before the native main menu can be used.
/// - ItemClass.list is only an optional late enrichment source for localized names, actual icon names/tints,
///   and any runtime-added food that follows the same Mood/Diet authoring contract.
///
/// This deliberately removes the old UI race where opening the Diet step before ItemClass.list was ready
/// produced an empty catalogue and required the player to leave/re-open the screen.
/// </summary>
public sealed class RebirthDietFoodEntry
{
    public string ItemId = string.Empty;
    public string DisplayName = string.Empty;
    public string IconName = string.Empty;
    public Color IconTint = Color.white;
    public bool HasAuthoredIcon;
    public bool CreatorVisible = true;
    public readonly List<string> DietTags = new List<string>();
}

public static class RebirthDietFoodCatalogue
{
    private static readonly object Sync = new object();
    private static readonly List<RebirthDietFoodEntry> Authored = new List<RebirthDietFoodEntry>();
    private static bool authoredReady;
    private static string lastReport = "[REBIRTH Survivor Diet] authored food catalogue not preloaded";

    public static bool IsReady
    {
        get { lock (Sync) return authoredReady && Authored.Count > 0; }
    }

    public static int AuthoredCount
    {
        get { lock (Sync) return Authored.Count; }
    }

    public static string LastReport
    {
        get { lock (Sync) return lastReport; }
    }

    /// <summary>
    /// Synchronously preload the food catalogue from REBIRTH authoring. This is called from the Survivor
    /// installer during InitMod and is treated as required startup data, not an asynchronous UI concern.
    /// </summary>
    public static string PreloadAuthored(string configRoot)
    {
        if (string.IsNullOrWhiteSpace(configRoot))
            throw new InvalidDataException("Survivor config root is empty while preloading Diet foods.");

        string path = Path.Combine(configRoot, "food_content.xml");
        if (!File.Exists(path))
            throw new FileNotFoundException("Survivor Diet food authoring file was not found.", path);

        XmlDocument document = new XmlDocument();
        document.PreserveWhitespace = false;
        document.Load(path);

        XmlElement root = document.DocumentElement;
        if (root == null || !string.Equals(root.Name, "configs", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Survivor Diet food authoring root must be <configs>.");

        Dictionary<string, RebirthDietFoodEntry> parsed = new Dictionary<string, RebirthDietFoodEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (XmlNode node in root.ChildNodes)
        {
            XmlElement append = node as XmlElement;
            if (append == null || append.Name != "append") continue;

            string xpath = (append.GetAttribute("xpath") ?? string.Empty).Trim();
            string itemId = ExtractItemId(xpath);
            if (itemId.Length > 0)
            {
                AddAuthoredEntry(parsed, itemId, append);
                continue;
            }

            // REBIRTH-owned food definitions are authored as <append xpath="/items"><item .../></append>.
            // They must be present in the same pre-main-menu snapshot as appended vanilla foods.
            if (string.Equals(xpath, "/items", StringComparison.OrdinalIgnoreCase))
            {
                foreach (XmlNode itemNode in append.ChildNodes)
                {
                    XmlElement item = itemNode as XmlElement;
                    if (item == null || item.Name != "item") continue;
                    string nestedItemId = (item.GetAttribute("name") ?? string.Empty).Trim();
                    if (nestedItemId.Length > 0) AddAuthoredEntry(parsed, nestedItemId, item);
                }
            }
        }

        if (parsed.Count == 0)
            throw new InvalidDataException("Survivor Diet food authoring produced zero Mood/Diet food entries.");

        List<RebirthDietFoodEntry> sorted = new List<RebirthDietFoodEntry>(parsed.Values);
        SortEntries(sorted);

        lock (Sync)
        {
            Authored.Clear();
            for (int i = 0; i < sorted.Count; i++) Authored.Add(Clone(sorted[i]));
            authoredReady = true;
            lastReport = "[REBIRTH Survivor Diet] authored food catalogue ready entries=" + Authored.Count;
        }

        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(lastReport + " source='" + path + "'."); }
        return lastReport;
    }

    public static RebirthDietFoodEntry[] BuildCurrent()
    {
        // Start from the guaranteed pre-main-menu authoring snapshot. The UI never needs to wait for
        // ItemClass.list and never asks the player to re-open a step.
        Dictionary<string, RebirthDietFoodEntry> merged = new Dictionary<string, RebirthDietFoodEntry>(StringComparer.OrdinalIgnoreCase);
        lock (Sync)
        {
            for (int i = 0; i < Authored.Count; i++)
            {
                RebirthDietFoodEntry entry = Clone(Authored[i]);
                LocalizeEntry(entry);
                merged[entry.ItemId] = entry;
            }
        }

        // Late native enrichment is optional. It may improve names/icons/tints or discover a compatible
        // runtime-added food, but catalogue availability never depends on it.
        if (ItemClass.list != null)
        {
            for (int i = 0; i < ItemClass.list.Length; i++)
            {
                ItemClass item = ItemClass.list[i];
                if (item == null || !HasTag(item, "food")) continue;
                string moodProfile = RebirthConsumableResolver.Get(item, RebirthFoodMoodResolver.MoodProfileProperty, string.Empty).Trim();
                if (moodProfile.Length == 0) continue;

                string itemId = item.GetItemName() ?? string.Empty;
                if (itemId.Length == 0) continue;

                RebirthDietFoodEntry entry;
                if (!merged.TryGetValue(itemId, out entry) || entry == null)
                {
                    entry = new RebirthDietFoodEntry();
                    entry.ItemId = itemId;
                    merged[itemId] = entry;
                }

                entry.DisplayName = LocalizedItemName(itemId);
                if (!entry.HasAuthoredIcon) entry.IconName = item.GetIconName() ?? itemId;
                entry.IconTint = item.GetIconTint();
                entry.DietTags.Clear();
                AddDietTags(entry.DietTags, RebirthConsumableResolver.Get(item, RebirthFoodMoodResolver.DietTagsProperty, string.Empty));
            }
        }

        List<RebirthDietFoodEntry> result = new List<RebirthDietFoodEntry>();
        foreach (RebirthDietFoodEntry entry in merged.Values)
            if (entry != null && entry.CreatorVisible) result.Add(entry);
        SortEntries(result);
        return result.ToArray();
    }


    private static void AddAuthoredEntry(Dictionary<string, RebirthDietFoodEntry> parsed, string itemId, XmlElement source)
    {
        if (parsed == null || source == null || string.IsNullOrWhiteSpace(itemId)) return;

        string moodProfile = string.Empty;
        string rawDietTags = string.Empty;
        string authoredIcon = string.Empty;
        bool creatorVisible = true;
        foreach (XmlNode propertyNode in source.ChildNodes)
        {
            XmlElement property = propertyNode as XmlElement;
            if (property == null || property.Name != "property") continue;
            string name = (property.GetAttribute("name") ?? string.Empty).Trim();
            string value = (property.GetAttribute("value") ?? string.Empty).Trim();
            if (string.Equals(name, RebirthFoodMoodResolver.MoodProfileProperty, StringComparison.OrdinalIgnoreCase)) moodProfile = value;
            else if (string.Equals(name, RebirthFoodMoodResolver.DietTagsProperty, StringComparison.OrdinalIgnoreCase)) rawDietTags = value;
            else if (string.Equals(name, "RebirthDietIcon", StringComparison.OrdinalIgnoreCase)) authoredIcon = value;
            else if (string.Equals(name, "CustomIcon", StringComparison.OrdinalIgnoreCase) && authoredIcon.Length == 0) authoredIcon = value;
            else if (string.Equals(name, "RebirthDietCatalogueHidden", StringComparison.OrdinalIgnoreCase))
            {
                bool hidden;
                if (bool.TryParse(value, out hidden) && hidden) creatorVisible = false;
            }
        }

        // Trait-support consumables may exist beside foods; only Mood/Diet meal entries belong here.
        if (moodProfile.Length == 0) return;

        RebirthDietFoodEntry entry = new RebirthDietFoodEntry();
        entry.ItemId = itemId.Trim();
        entry.DisplayName = entry.ItemId; // Localized lazily once the menu/localization system is ready.
        entry.IconName = authoredIcon.Length > 0 ? authoredIcon : entry.ItemId;
        entry.HasAuthoredIcon = authoredIcon.Length > 0;
        entry.CreatorVisible = creatorVisible;
        entry.IconTint = Color.white;
        AddDietTags(entry.DietTags, rawDietTags);
        parsed[entry.ItemId] = entry;
    }

    private static string ExtractItemId(string xpath)
    {
        string raw = (xpath ?? string.Empty).Trim();
        if (raw.Length == 0) return string.Empty;

        const string single = "@name='";
        int start = raw.IndexOf(single, StringComparison.OrdinalIgnoreCase);
        char quote = '\'';
        if (start >= 0) start += single.Length;
        else
        {
            const string dbl = "@name=\"";
            start = raw.IndexOf(dbl, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return string.Empty;
            start += dbl.Length;
            quote = '"';
        }
        int end = raw.IndexOf(quote, start);
        return end > start ? raw.Substring(start, end - start).Trim() : string.Empty;
    }

    private static void LocalizeEntry(RebirthDietFoodEntry entry)
    {
        if (entry == null) return;
        entry.DisplayName = LocalizedItemName(entry.ItemId);
        if (string.IsNullOrEmpty(entry.IconName)) entry.IconName = entry.ItemId;
    }

    private static string LocalizedItemName(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return string.Empty;
        string localized = Localization.Get(itemId);
        return string.IsNullOrEmpty(localized) ? itemId : localized;
    }

    private static void AddDietTags(List<string> destination, string rawDietTags)
    {
        if (destination == null) return;
        string[] parts = (rawDietTags ?? string.Empty).Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            string tag = (parts[i] ?? string.Empty).Trim();
            if (tag.Length > 0) destination.Add(tag);
        }
    }

    private static RebirthDietFoodEntry Clone(RebirthDietFoodEntry source)
    {
        RebirthDietFoodEntry copy = new RebirthDietFoodEntry();
        if (source == null) return copy;
        copy.ItemId = source.ItemId ?? string.Empty;
        copy.DisplayName = source.DisplayName ?? string.Empty;
        copy.IconName = source.IconName ?? string.Empty;
        copy.IconTint = source.IconTint;
        copy.HasAuthoredIcon = source.HasAuthoredIcon;
        copy.CreatorVisible = source.CreatorVisible;
        for (int i = 0; i < source.DietTags.Count; i++) copy.DietTags.Add(source.DietTags[i]);
        return copy;
    }

    private static void SortEntries(List<RebirthDietFoodEntry> result)
    {
        if (result == null) return;
        result.Sort(delegate(RebirthDietFoodEntry a, RebirthDietFoodEntry b)
        {
            int byName = string.Compare(a != null ? a.DisplayName : string.Empty,
                b != null ? b.DisplayName : string.Empty, StringComparison.OrdinalIgnoreCase);
            if (byName != 0) return byName;
            return string.Compare(a != null ? a.ItemId : string.Empty,
                b != null ? b.ItemId : string.Empty, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool HasTag(ItemClass item, string wanted)
    {
        string raw = RebirthConsumableResolver.Get(item, "Tags", string.Empty);
        if (raw.Length == 0) return false;
        string[] parts = raw.Split(',');
        for (int i = 0; i < parts.Length; i++)
            if (string.Equals((parts[i] ?? string.Empty).Trim(), wanted, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
