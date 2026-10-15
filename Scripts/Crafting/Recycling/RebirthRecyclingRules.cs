using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

// Source-compatible rule semantics from 2.6 ScrapDB. Recipes/inventories and
// grouped queue authority belong to the workstation backend, not this registry.
public sealed class RebirthRecyclerOutput
{
    private readonly ItemStack stack;
    public ItemStack Stack => stack.Clone();
    public float TimeSeconds { get; }
    internal RebirthRecyclerOutput(ItemStack value, float seconds) { stack = value.Clone(); TimeSeconds = seconds; }
}
public sealed class RebirthRecyclerRule
{
    public int InputType { get; internal set; }
    public string StationId { get; internal set; }
    public string SourceId { get; internal set; }
    public string MatcherSummary { get; internal set; }
    public int SpecificityScore { get; internal set; }
    public float LegacyDefaultTime { get; internal set; }
    public IReadOnlyList<RebirthRecyclerOutput> Outputs { get; internal set; }
    public IReadOnlyList<string> RequiredTools { get; internal set; }
}
public sealed class RebirthRecyclerRuleIssue
{
    public string SourceId { get; internal set; }
    public string Kind { get; internal set; }
    public string Name { get; internal set; }
}
public static class RebirthRecyclerRules
{
    public const string ConfigName = "RebirthScrap.xml";
    private sealed class Output { internal string Name; internal int Count; internal float Time; }
    private sealed class Template
    {
        internal string Station, SourceId, Exact, Prefix, Suffix, Contains, RegexText, Summary;
        internal Regex Regex;
        internal int Specificity;
        internal float DefaultTime;
        internal readonly List<string> Tools = new List<string>();
        internal readonly List<Output> Outputs = new List<Output>();
        internal bool Matches(string name)
        {
            if (!string.IsNullOrEmpty(Exact)) return string.Equals(name, Exact.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(Prefix)) return name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(Suffix)) return name.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(Contains)) return name.IndexOf(Contains, StringComparison.OrdinalIgnoreCase) >= 0;
            try { return Regex != null && Regex.IsMatch(name); } catch (RegexMatchTimeoutException) { return false; }
        }
    }
    private static readonly List<Template> templates = new List<Template>();
    private static readonly List<RebirthRecyclerRuleIssue> issues = new List<RebirthRecyclerRuleIssue>();
    private static readonly HashSet<string> issueKeys = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<int, Dictionary<string, RebirthRecyclerRule>> resolved = new Dictionary<int, Dictionary<string, RebirthRecyclerRule>>();
    private static bool loaded;
    private static ItemClass[] mapping;
    private static string preferredPath;
    public static IReadOnlyList<RebirthRecyclerRuleIssue> Diagnostics { get { EnsureLoaded(); return new ReadOnlyCollection<RebirthRecyclerRuleIssue>(issues); } }
    public static int RuleCount { get { EnsureLoaded(); return templates.Count; } }
    public static string NormalizeStationId(string station)
    {
        if (string.IsNullOrEmpty(station)) return string.Empty;
        station = station.Trim();
        if (station.StartsWith("workstation_", StringComparison.OrdinalIgnoreCase)) station = station.Substring(12);
        return station.ToLowerInvariant();
    }
    public static bool Supports(string station)
    {
        string normalized = NormalizeStationId(station);
        return normalized == "sonjarecyclerstation" || normalized == "sonjaammorecyclerstation"
            || normalized == "workbenchshredder001_fr" || normalized == "sonjaoutfitdesigner";
    }
    public static void SetConfigPath(string path)
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Recycling rule registration requires the main thread.");
        preferredPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        Invalidate();
    }
    public static void Invalidate()
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Recycling rules require the main thread.");
        loaded = false; mapping = null; templates.Clear(); resolved.Clear(); issues.Clear(); issueKeys.Clear();
    }
    public static void Reload() { Invalidate(); EnsureLoaded(); }
    private static string ConfigPath()
    {
        if (!string.IsNullOrEmpty(preferredPath)) return preferredPath;
        string location = typeof(RebirthRecyclerRules).Assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            string candidate = Path.Combine(Path.GetDirectoryName(location), "Config", ConfigName);
            if (File.Exists(candidate)) return candidate;
        }
        foreach (var mod in ModManager.GetLoadedMods())
        {
            if (mod?.Path == null) continue;
            string candidate = Path.Combine(mod.Path, "Config", ConfigName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
    private static void Issue(string source, string kind, string name)
    {
        string key = source + "|" + kind + "|" + name;
        if (issueKeys.Add(key)) issues.Add(new RebirthRecyclerRuleIssue { SourceId = source, Kind = kind, Name = name });
    }
    private static int ResolveType(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || ItemClass.list == null) return -1;
        // Never accept the native missing-item fallback as a migrated item.
        var item = ItemClass.GetItem(name.Trim(), true);
        return item != null && item.type > 0 && item.ItemClass != null
            && string.Equals(item.ItemClass.GetItemName(), name.Trim(), StringComparison.OrdinalIgnoreCase) ? item.type : -1;
    }
    private static float Number(string text)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            && !float.IsInfinity(value) && !float.IsNaN(value) ? value : 0;
    }
    private static string Attribute(XmlNode n, string key) => n.Attributes?[key]?.Value;
    public static void EnsureLoaded()
    {
        if (loaded || !ThreadManager.IsMainThread()) return;
        string path = ConfigPath();
        if (path == null || !File.Exists(path)) { Issue(ConfigName, "missing-config", path ?? ConfigName); return; }
        var document = new XmlDocument { XmlResolver = null };
        using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
        foreach (XmlNode node in document.SelectNodes("/Config/scrap"))
        {
            var rule = new Template { Station = NormalizeStationId(Attribute(node, "station")), Exact = Attribute(node, "item"),
                Prefix = Attribute(node, "item_prefix"), Suffix = Attribute(node, "item_suffix"), Contains = Attribute(node, "item_contains"),
                RegexText = Attribute(node, "item_regex"), DefaultTime = Number(Attribute(node, "time")) };
            using (var hash = SHA256.Create()) rule.SourceId = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(node.OuterXml))).Replace("-", "").ToLowerInvariant();
            bool star = false;
            if (!string.IsNullOrEmpty(rule.Prefix) && rule.Prefix.Contains("*"))
            {
                string raw = rule.Prefix, core = raw.Trim('*');
                if (core.Length > 0)
                {
                    star = true; rule.Prefix = null;
                    if (raw.StartsWith("*", StringComparison.Ordinal) && raw.EndsWith("*", StringComparison.Ordinal)) rule.Contains = core;
                    else if (raw.StartsWith("*", StringComparison.Ordinal)) rule.Suffix = core;
                    else if (raw.EndsWith("*", StringComparison.Ordinal)) rule.Prefix = core;
                }
            }
            int matchers = 0;
            foreach (string value in new[] { rule.Exact, rule.Prefix, rule.Suffix, rule.Contains, rule.RegexText }) if (!string.IsNullOrEmpty(value)) matchers++;
            if (matchers != 1 || !Supports(rule.Station)) { Issue(rule.SourceId, "invalid-rule", rule.Station); continue; }
            string pattern = !string.IsNullOrEmpty(rule.Exact) ? rule.Exact : !string.IsNullOrEmpty(rule.RegexText) ? rule.RegexText
                : !string.IsNullOrEmpty(rule.Prefix) ? rule.Prefix : !string.IsNullOrEmpty(rule.Suffix) ? rule.Suffix : rule.Contains;
            // These weights, including '*' outranking exact rules, are intentional legacy behavior.
            rule.Specificity = (star ? 5000 : !string.IsNullOrEmpty(rule.Exact) ? 4000 : !string.IsNullOrEmpty(rule.RegexText) ? 3000
                : !string.IsNullOrEmpty(rule.Prefix) || !string.IsNullOrEmpty(rule.Suffix) ? 2000 : 1000) + pattern.Length;
            rule.Summary = (!string.IsNullOrEmpty(rule.Exact) ? "exact:" : !string.IsNullOrEmpty(rule.RegexText) ? "regex:"
                : !string.IsNullOrEmpty(rule.Prefix) ? "prefix:" : !string.IsNullOrEmpty(rule.Suffix) ? "suffix:" : "contains:") + pattern;
            if (!string.IsNullOrEmpty(rule.RegexText))
            {
                try { rule.Regex = new Regex(rule.RegexText, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)); }
                catch (ArgumentException) { Issue(rule.SourceId, "invalid-regex", rule.RegexText); continue; }
            }
            string tools = Attribute(node, "scrap_tool") ?? Attribute(node, "scrap_tools");
            if (tools != null) foreach (string token in tools.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            { string tool = token.Trim(); if (tool.Length != 0) rule.Tools.Add(tool); }
            foreach (XmlNode ingredient in node.SelectNodes("./ingredient"))
            {
                string name = Attribute(ingredient, "name");
                if (string.IsNullOrEmpty(name)) { Issue(rule.SourceId, "missing-output-name", ""); continue; }
                if (!int.TryParse(Attribute(ingredient, "count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count < 1) count = 1;
                float time = Number(Attribute(ingredient, "time"));
                rule.Outputs.Add(new Output { Name = name, Count = count, Time = time > 0 ? time : rule.DefaultTime > 0 ? rule.DefaultTime : 1f });
            }
            if (rule.Outputs.Count == 0) { Issue(rule.SourceId, "no-outputs", rule.Summary); continue; }
            templates.Add(rule);
            // Defer reference diagnostics until item mappings actually exist.
            if (ItemClass.list != null && ItemClass.list.Length > 0) Audit(rule);
        }
        loaded = true;
    }
    private static void Audit(Template rule)
    {
        if (!string.IsNullOrEmpty(rule.Exact) && ResolveType(rule.Exact) < 0) Issue(rule.SourceId, "missing-input", rule.Exact);
        foreach (var output in rule.Outputs) if (ResolveType(output.Name) < 0) Issue(rule.SourceId, "missing-output", output.Name);
        foreach (var tool in rule.Tools) if (ResolveType(tool) < 0) Issue(rule.SourceId, "missing-tool", tool);
    }
    public static bool TryResolve(int inputType, string station, out RebirthRecyclerRule rule)
        => TryResolve(inputType, station, out rule, out _);
    public static bool TryResolve(int inputType, string station, out RebirthRecyclerRule rule, out string reason)
    {
        rule = null; reason = "xuiNoScrapOutputsDefined";
        if (!ThreadManager.IsMainThread() || inputType <= 0 || ItemClass.list == null || inputType >= ItemClass.list.Length
            || ItemClass.list[inputType] == null || !Supports(station)) return false;
        EnsureLoaded(); string normalized = NormalizeStationId(station);
        if (!ReferenceEquals(mapping, ItemClass.list)) { mapping = ItemClass.list; resolved.Clear(); }
        if (resolved.TryGetValue(inputType, out var stations) && stations.TryGetValue(normalized, out rule))
        { if (rule == null) return false; reason = null; return true; }
        string name = ItemClass.list[inputType].GetItemName(); if (string.IsNullOrEmpty(name)) return false;
        Template best = null;
        foreach (var candidate in templates)
            if (candidate.Station == normalized && candidate.Matches(name)
                && (best == null || candidate.Specificity > best.Specificity)) best = candidate; // First wins specificity ties, as legacy load expansion did.
        if (best == null)
        {
            if (stations == null) resolved[inputType] = stations = new Dictionary<string, RebirthRecyclerRule>(StringComparer.Ordinal);
            stations[normalized] = null; return false;
        }
        var outputs = new List<RebirthRecyclerOutput>(); bool missing = false;
        foreach (var output in best.Outputs)
        {
            int type = ResolveType(output.Name);
            if (type < 0) { Issue(best.SourceId, "missing-output", output.Name); missing = true; continue; }
            outputs.Add(new RebirthRecyclerOutput(new ItemStack(new ItemValue(type, false), output.Count), output.Time));
        }
        foreach (string tool in best.Tools) if (ResolveType(tool) < 0) { Issue(best.SourceId, "missing-tool", tool); missing = true; }
        // Removed 2.6 item definitions must never silently reduce a multioutput yield
        // or select a less-specific replacement. Retain their literal rules for repair.
        if (missing) { reason = "xuiRebirthRecyclingRuleUnavailable"; return false; }
        rule = new RebirthRecyclerRule { InputType = inputType, StationId = normalized, SourceId = best.SourceId,
            MatcherSummary = best.Summary, SpecificityScore = best.Specificity, LegacyDefaultTime = best.DefaultTime,
            Outputs = outputs.AsReadOnly(), RequiredTools = best.Tools.AsReadOnly() };
        if (stations == null) resolved[inputType] = stations = new Dictionary<string, RebirthRecyclerRule>(StringComparer.Ordinal);
        stations[normalized] = rule; reason = null; return true;
    }
    public static bool ToolsHealthy(ItemStack[] tools, RebirthRecyclerRule rule, out IReadOnlyList<string> missing, out IReadOnlyList<string> broken)
    {
        var absent = new List<string>(); var damaged = new List<string>();
        if (rule == null) { missing = absent.AsReadOnly(); broken = damaged.AsReadOnly(); return false; }
        foreach (string requirement in rule.RequiredTools)
        {
            bool present = false, healthy = false;
            if (tools != null) foreach (var stack in tools)
            {
                if (stack == null || stack.IsEmpty() || stack.itemValue?.ItemClass == null
                    || !string.Equals(stack.itemValue.ItemClass.GetItemName(), requirement, StringComparison.OrdinalIgnoreCase)) continue;
                present = true;
                if (stack.itemValue.MaxUseTimes <= 0 || stack.itemValue.UseTimes < stack.itemValue.MaxUseTimes) healthy = true;
            }
            if (!present) absent.Add(requirement); else if (!healthy) damaged.Add(requirement);
        }
        missing = absent.AsReadOnly(); broken = damaged.AsReadOnly(); return absent.Count == 0 && damaged.Count == 0;
    }
}
