#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

// Explicit read-only audit. Release excludes this Debug directory in the project.
[Preserve]
public sealed class ConsoleCmdRebirthRecyclingAudit : ConsoleCmdAbstract
{
    private const int MaximumSamples = 256;
    public override bool IsExecuteOnClient => true;
    public override string[] getCommands() => new[] { "rbrecyclingaudit" };
    public override string getDescription() => "Read-only REBIRTH scrap rules, regular byproducts and optional native queue audit.";
    public override string getHelp() => "rbrecyclingaudit [limit=32] [x=<integer> y=<integer> z=<integer>] (Debug only; no world mutation).";
    private static string Short(string text) => text == null ? string.Empty : text.Length <= 160 ? text : text.Substring(0, 160);
    private static bool Exists(string name)
    {
        var value = ItemClass.GetItem(name, true);
        return value != null && value.type > 0 && value.ItemClass != null
            && string.Equals(value.ItemClass.GetItemName(), name, StringComparison.OrdinalIgnoreCase);
    }
    private static JArray Sample(IEnumerable<string> values, int limit)
    {
        var result = new JArray(); int count = 0;
        foreach (string value in values) { if (count++ >= limit) break; result.Add(Short(value)); }
        return result;
    }
    private static string ConfigPath()
    {
        string location = typeof(RebirthRecyclerRules).Assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            string candidate = Path.Combine(Path.GetDirectoryName(location), "Config", RebirthRecyclerRules.ConfigName);
            if (File.Exists(candidate)) return candidate;
        }
        foreach (var mod in ModManager.GetLoadedMods())
        {
            if (mod?.Path == null) continue;
            string candidate = Path.Combine(mod.Path, "Config", RebirthRecyclerRules.ConfigName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
    private static string UnmatchedPattern(XmlNode node)
    {
        string prefix = node.Attributes?["item_prefix"]?.Value, suffix = node.Attributes?["item_suffix"]?.Value,
            contains = node.Attributes?["item_contains"]?.Value, regex = node.Attributes?["item_regex"]?.Value;
        if (!string.IsNullOrEmpty(prefix) && prefix.Contains("*"))
        {
            string raw = prefix, core = raw.Trim('*');
            if (core.Length != 0)
            {
                prefix = null;
                if (raw.StartsWith("*") && raw.EndsWith("*")) contains = core;
                else if (raw.StartsWith("*")) suffix = core;
                else if (raw.EndsWith("*")) prefix = core;
            }
        }
        string description = "prefix:" + prefix + ";suffix:" + suffix + ";contains:" + contains + ";regex:" + regex;
        Regex expression = null;
        if (!string.IsNullOrEmpty(regex))
        {
            try { expression = new Regex(regex, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)); }
            catch (ArgumentException) { return "invalid-regex:" + regex; }
        }
        if (ItemClass.list != null) foreach (var item in ItemClass.list)
        {
            string name = item?.GetItemName(); if (string.IsNullOrEmpty(name)) continue;
            bool match;
            try { match = !string.IsNullOrEmpty(prefix) ? name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                : !string.IsNullOrEmpty(suffix) ? name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                : !string.IsNullOrEmpty(contains) ? name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0
                : expression != null && expression.IsMatch(name); }
            catch (RegexMatchTimeoutException) { return "regex-timeout:" + regex; }
            if (match) return null;
        }
        return description;
    }
    private static JObject RuleAudit(int limit)
    {
        var stations = new JObject(); var missingInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var missingTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unmatchedPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int nodes = 0, outputNodes = 0, exact = 0, patterns = 0, stationless = 0;
        string path = ConfigPath();
        if (path == null) throw new InvalidOperationException("Installed scrap rule configuration unavailable.");
        var document = new XmlDocument { XmlResolver = null };
        using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
        foreach (XmlNode node in document.SelectNodes("/Config/scrap"))
        {
            nodes++; string station = RebirthRecyclerRules.NormalizeStationId(node.Attributes?["station"]?.Value);
            if (station.Length == 0) stationless++;
            else stations[station] = ((int?)stations[station] ?? 0) + 1;
            string source = node.Attributes?["item"]?.Value;
            if (!string.IsNullOrEmpty(source)) { exact++; if (!Exists(source.Trim())) missingInputs.Add(source.Trim()); }
            else
            {
                patterns++;
                if (station.Length != 0)
                { string unmatched = UnmatchedPattern(node); if (unmatched != null) unmatchedPatterns.Add(unmatched); }
            }
            foreach (XmlNode output in node.SelectNodes("./ingredient"))
            {
                outputNodes++; string name = output.Attributes?["name"]?.Value;
                if (string.IsNullOrEmpty(name) || !Exists(name.Trim())) missingOutputs.Add(name ?? "<missing-name>");
            }
            string tools = node.Attributes?["scrap_tool"]?.Value ?? node.Attributes?["scrap_tools"]?.Value;
            if (tools != null) foreach (string token in tools.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (!Exists(token.Trim())) missingTools.Add(token.Trim());
        }
        var parserDiagnostics = new JArray(); int parserTotal = 0;
        foreach (var issue in RebirthRecyclerRules.Diagnostics)
        {
            if (parserTotal++ < limit) parserDiagnostics.Add(new JObject { ["source"] = Short(issue.SourceId), ["kind"] = Short(issue.Kind), ["name"] = Short(issue.Name) });
        }
        return new JObject { ["installedParserRules"] = RebirthRecyclerRules.RuleCount, ["configNodes"] = nodes,
            ["stations"] = stations, ["stationlessNodes"] = stationless, ["exactRules"] = exact, ["patternRules"] = patterns,
            ["outputEntries"] = outputNodes, ["missingExactInputsCount"] = missingInputs.Count, ["missingExactInputs"] = Sample(missingInputs, limit),
            ["unmatchedPatternsCount"] = unmatchedPatterns.Count, ["unmatchedPatterns"] = Sample(unmatchedPatterns, limit),
            ["missingOutputsCount"] = missingOutputs.Count, ["missingOutputs"] = Sample(missingOutputs, limit),
            ["missingToolsCount"] = missingTools.Count, ["missingTools"] = Sample(missingTools, limit),
            ["parserDiagnosticsCount"] = parserTotal, ["parserDiagnostics"] = parserDiagnostics,
            ["samplesTruncated"] = missingInputs.Count > limit || unmatchedPatterns.Count > limit || missingOutputs.Count > limit || missingTools.Count > limit || parserTotal > limit };
    }
    private static JObject OrdinaryAudit(int limit)
    {
        RebirthRecipeOutputs.AuditCurrentRecipes();
        var diagnostics = RebirthRecipeOutputs.Diagnostics;
        return new JObject { ["definitions"] = RebirthRecipeOutputs.DefinitionCount, ["diagnosticsCount"] = diagnostics.Count,
            ["diagnostics"] = Sample(diagnostics, limit), ["samplesTruncated"] = diagnostics.Count > limit };
    }
    private static JObject StationAudit(Vector3i position, int limit)
    {
        var world = GameManager.Instance?.World;
        if (world == null) throw new InvalidOperationException("Current world unavailable.");
        var station = world.GetTileEntity(position) as TileEntityWorkstation;
        if (station == null) return new JObject { ["found"] = false, ["x"] = position.x, ["y"] = position.y, ["z"] = position.z };
        var rows = new JArray(); var groups = new HashSet<string>(StringComparer.Ordinal); int occupied = 0;
        var queue = station.Queue;
        if (queue != null) for (int i = 0; i < queue.Length; i++)
        {
            var row = queue[i]; if (row?.Recipe == null || row.Multiplier <= 0) continue;
            occupied++; var recipe = row.Recipe; bool grouped = RebirthRecyclerRuntime.TryGroup(recipe, out var group);
            if (grouped) groups.Add(group.ToString("N"));
            if (rows.Count >= limit) continue;
            var sample = new JObject { ["slot"] = i, ["recipe"] = Short(recipe.GetName()), ["area"] = Short(recipe.craftingArea),
                ["primaryCount"] = recipe.count, ["multiplier"] = row.Multiplier, ["secondsLeft"] = row.CraftingTimeLeft,
                ["crafting"] = row.IsCrafting, ["groupedScrap"] = grouped };
            if (grouped)
            {
                var token = recipe.ingredients[0].itemValue;
                sample["group"] = group.ToString("N");
                sample["originalInput"] = Short(token.ItemClass?.GetItemName()); sample["originalQuality"] = token.Quality;
                foreach (string key in new[] { "index", "units", "outputs", "rule" })
                    if (token.TryGetMetadata(RebirthRecyclerRuntime.Prefix + key, out string value)) sample[key] = Short(value);
            }
            rows.Add(sample);
        }
        return new JObject { ["found"] = true, ["block"] = Short(station.block?.GetBlockName()), ["x"] = position.x, ["y"] = position.y, ["z"] = position.z,
            ["recycler"] = RebirthRecyclerRuntime.Supports(station), ["queueCapacity"] = queue?.Length ?? 0,
            ["occupiedRows"] = occupied, ["distinctScrapGroups"] = groups.Count, ["rows"] = rows, ["samplesTruncated"] = occupied > limit };
    }
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        try
        {
            if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Main-thread audit required.");
            int limit = 32; var coordinates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (args != null) foreach (string argument in args)
            {
                var fields = argument.Split('=');
                if (fields.Length != 2 || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) throw new ArgumentException(getHelp());
                if (fields[0].Equals("limit", StringComparison.OrdinalIgnoreCase)) { if (value < 1 || value > MaximumSamples) throw new ArgumentException(getHelp()); limit = value; }
                else if (fields[0] == "x" || fields[0] == "y" || fields[0] == "z") coordinates[fields[0]] = value;
                else throw new ArgumentException(getHelp());
            }
            if (coordinates.Count != 0 && coordinates.Count != 3) throw new ArgumentException("All x/y/z coordinates are required.");
            var result = new JObject { ["schema"] = 1, ["ok"] = true, ["readOnly"] = true, ["itemsReady"] = ItemClass.list != null && ItemClass.list.Length > 0,
                ["rules"] = RuleAudit(limit), ["regularRecipeOutputs"] = OrdinaryAudit(limit) };
            if (coordinates.Count == 3) result["station"] = StationAudit(new Vector3i(coordinates["x"], coordinates["y"], coordinates["z"]), limit);
            SdtdConsole.Instance.Output(result.ToString(Newtonsoft.Json.Formatting.None));
        }
        catch (Exception error)
        { SdtdConsole.Instance.Output(new JObject { ["schema"] = 1, ["ok"] = false, ["readOnly"] = true, ["error"] = Short(error.GetType().Name + ": " + error.Message) }.ToString(Newtonsoft.Json.Formatting.None)); }
    }
}
#endif
