using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

#nullable disable

/// <summary>
/// Main-thread presentation cache. The source bundle does not expose a native localization
/// revision contract, so only registered labels are checked (once per second while UI reads).
/// Changes invalidate sorted projections. World/definition transitions invalidate immediately;
/// no guessed preference enum, global scene search or per-comparison localization is required.
/// </summary>
public static class RebirthUiProjectionTextCache
{
    private sealed class Entry { public string Text; public Func<string> Read; }
    private static readonly Dictionary<string, Entry> Labels = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Entry> DefinitionNames = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private static object world, definitions;
    private static string definitionHash;
    private static long revision;
    private static float nextValidation;
    public static long Revision
    {
        get
        {
            object currentWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (!ReferenceEquals(world, currentWorld) || !ReferenceEquals(definitions, RebirthSurvivorDefinitionRegistry.Bundle)
                || !string.Equals(definitionHash, RebirthSurvivorDefinitionRegistry.SemanticHash, StringComparison.Ordinal))
            {
                Labels.Clear(); DefinitionNames.Clear(); world = currentWorld;
                definitions = RebirthSurvivorDefinitionRegistry.Bundle; definitionHash = RebirthSurvivorDefinitionRegistry.SemanticHash;
                nextValidation = 0f; unchecked { revision++; }
            }
            float now = Time.realtimeSinceStartup;
            if (now < nextValidation) return revision;
            nextValidation = now + 1f;
            bool changed = Validate(Labels) | Validate(DefinitionNames);
            if (changed) unchecked { revision++; }
            return revision;
        }
    }
    private static bool Validate(Dictionary<string, Entry> entries)
    {
        bool changed = false;
        foreach (Entry entry in entries.Values)
        {
            string next = entry.Read();
            if (string.Equals(next, entry.Text, StringComparison.Ordinal)) continue;
            entry.Text = next; changed = true;
        }
        return changed;
    }
    public static string L(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;
        long ignored = Revision;
        Entry entry;
        if (!Labels.TryGetValue(key, out entry))
        {
            if (Labels.Count >= 4096) { Labels.Clear(); unchecked { revision++; } }
            entry = new Entry { Read = () => Localization.Get(key), Text = Localization.Get(key) }; Labels[key] = entry;
        }
        return string.IsNullOrEmpty(entry.Text) || string.Equals(entry.Text, key, StringComparison.Ordinal) ? (fallback ?? key) : entry.Text;
    }
    public static string DefinitionName(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        long ignored = Revision;
        Entry entry;
        if (!DefinitionNames.TryGetValue(id, out entry))
        {
            if (DefinitionNames.Count >= 4096) { DefinitionNames.Clear(); unchecked { revision++; } }
            entry = new Entry { Read = () => RebirthSurvivorUiText.ResolveDefinitionName(id), Text = RebirthSurvivorUiText.ResolveDefinitionName(id) };
            DefinitionNames[id] = entry;
        }
        return entry.Text;
    }
}

/// <summary>Instance-owned stable ID ordering; never retains mutable owner rows.</summary>
public sealed class RebirthUiSortedIdProjection
{
    private string[] input;
    private ReadOnlyCollection<string> ordered;
    private long textRevision = long.MinValue;
    public IList<string> Get(IList<string> ids, Func<string, string> name)
    {
        long current = RebirthUiProjectionTextCache.Revision;
        bool same = ordered != null && input.Length == ids.Count && textRevision == current;
        if (same) for (int i = 0; i < ids.Count; i++) if (!string.Equals(input[i], ids[i], StringComparison.Ordinal)) { same = false; break; }
        if (same) return ordered;
        input = new string[ids.Count];
        List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>(ids.Count);
        for (int i = 0; i < ids.Count; i++) { input[i] = ids[i]; pairs.Add(new KeyValuePair<string, string>(ids[i], name(ids[i]))); }
        pairs.Sort(delegate(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
        {
            int byName = string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.Compare(a.Key, b.Key, StringComparison.Ordinal);
        });
        List<string> result = new List<string>(pairs.Count);
        foreach (KeyValuePair<string, string> pair in pairs) result.Add(pair.Key);
        ordered = result.AsReadOnly(); textRevision = RebirthUiProjectionTextCache.Revision;
        return ordered;
    }
}
