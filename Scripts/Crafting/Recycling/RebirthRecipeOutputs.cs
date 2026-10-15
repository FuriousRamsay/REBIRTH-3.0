using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;

public sealed class RebirthAdditionalRecipeOutput
{
    public string ItemName { get; internal set; }
    public int Count { get; internal set; }
}
public sealed class RebirthRecipeOutputDefinition
{
    public string RecipeName { get; internal set; }
    public string CraftingArea { get; internal set; }
    public string SourceId { get; internal set; }
    public IReadOnlyList<RebirthAdditionalRecipeOutput> Outputs { get; internal set; }
}

// <output> is distinct from Scrap.xml's <ingredient> yield rules. It adds
// byproducts to one regular recipe application; no extra timer/payment/queue row.
// This registry never publishes inventories, grants rewards or refunds inputs.
public static class RebirthRecipeOutputs
{
    public const string ConfigName = "RebirthRecipeOutputs.xml";
    private static readonly Dictionary<string, RebirthRecipeOutputDefinition> definitions
        = new Dictionary<string, RebirthRecipeOutputDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> diagnostics = new List<string>();
    private static readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
    private static bool loaded, installed;
    private static string preferredPath;
    private static string Key(string name, string area) => (name ?? string.Empty) + "#" + (area ?? string.Empty);
    public static int DefinitionCount { get { EnsureLoaded(); return definitions.Count; } }
    public static IReadOnlyList<string> Diagnostics { get { EnsureLoaded(); return diagnostics.AsReadOnly(); } }
    private static void Issue(string issue) { if (reported.Add(issue)) diagnostics.Add(issue); }
    public static void Install()
    {
        if (installed) return;
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Recipe output registration requires the main thread.");
        new Harmony("rebirth.recipes.additionaloutputs.v1").CreateClassProcessor(typeof(RebirthRecipeOutputCapture)).Patch();
        installed = true;
    }
    public static void SetConfigPath(string path)
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Recipe output registration requires the main thread.");
        preferredPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path); Reload();
    }
    public static void Reload()
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Recipe output registration requires the main thread.");
        loaded = false; definitions.Clear(); diagnostics.Clear(); reported.Clear(); EnsureLoaded();
    }
    private static string FindConfig()
    {
        if (!string.IsNullOrEmpty(preferredPath)) return preferredPath;
        string location = typeof(RebirthRecipeOutputs).Assembly.Location;
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
    public static void EnsureLoaded()
    {
        if (loaded || !ThreadManager.IsMainThread()) return;
        string path = FindConfig();
        if (path == null || !File.Exists(path)) { Issue("missing-config:" + ConfigName); return; }
        using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            var document = XDocument.Load(reader);
            if (document.Root?.Name != "recipeOutputs") { Issue("invalid-config-root:" + ConfigName); loaded = true; return; }
            foreach (var recipe in document.Root.Elements("recipe")) Register(recipe);
        }
        loaded = true;
    }
    // Receive the actual patched native document at load time so newly authored
    // recipes keep the familiar 2.6 <output name="..." count="..."/> format.
    public static void CaptureEffectiveRecipes(XElement root)
    {
        if (!ThreadManager.IsMainThread() || root == null) return;
        Reload();
        foreach (var recipe in root.Elements("recipe"))
            if (recipe.Element("output") != null) Register(recipe);
    }
    private static void Register(XElement recipe)
    {
        string name = (string)recipe.Attribute("name"), area = (string)recipe.Attribute("craft_area") ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Issue("recipe-name-missing"); return; }
        string key = Key(name, area);
        if ((string)recipe.Attribute("disable") == "true") { definitions.Remove(key); return; }
        var outputs = new List<RebirthAdditionalRecipeOutput>();
        foreach (var output in recipe.Elements("output"))
        {
            string itemName = (string)output.Attribute("name");
            string countText = (string)output.Attribute("count");
            int count = 1;
            if (string.IsNullOrEmpty(itemName) || countText != null
                && (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count <= 0))
            { Issue("invalid-output:" + key); return; }
            outputs.Add(new RebirthAdditionalRecipeOutput { ItemName = itemName, Count = count });
        }
        if (outputs.Count == 0) return;
        string source;
        using (var hash = SHA256.Create()) source = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(recipe.ToString(SaveOptions.DisableFormatting)))).Replace("-", "").ToLowerInvariant();
        // Later definitions replace the whole list, matching 2.6 registry semantics.
        definitions[key] = new RebirthRecipeOutputDefinition { RecipeName = name, CraftingArea = area, SourceId = source, Outputs = outputs.AsReadOnly() };
    }
    public static bool TryGet(Recipe recipe, out RebirthRecipeOutputDefinition definition)
    {
        definition = null;
        if (!ThreadManager.IsMainThread() || recipe == null || recipe.IsScrap) return false;
        EnsureLoaded(); return definitions.TryGetValue(Key(recipe.GetName(), recipe.craftingArea), out definition);
    }
    public static bool HasOutputs(Recipe recipe) => TryGet(recipe, out _);
    public static bool IsAvailable(Recipe recipe) => TryPlan(recipe, 1, out _, out _);
    // Call after the native recipe enumerator finishes, not from its load Prefix.
    // Retain unavailable historical definitions for repair; never invent aliases.
    public static void AuditCurrentRecipes()
    {
        if (!ThreadManager.IsMainThread()) return;
        EnsureLoaded(); var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var recipe in CraftingManager.GetAllRecipes())
            if (recipe != null && !recipe.IsScrap) current.Add(Key(recipe.GetName(), recipe.craftingArea));
        foreach (var pair in definitions)
        {
            if (!current.Contains(pair.Key)) Issue("missing-current-recipe:" + pair.Key);
            foreach (var output in pair.Value.Outputs)
            {
                var item = ItemClass.GetItem(output.ItemName, true);
                if (item == null || item.type <= 0 || item.ItemClass == null
                    || !string.Equals(item.ItemClass.GetItemName(), output.ItemName, StringComparison.OrdinalIgnoreCase))
                    Issue("missing-output-item:" + pair.Key + ":" + output.ItemName);
            }
        }
    }
    public static bool TryPlan(Recipe recipe, int completedApplications, out IReadOnlyList<ItemStack> outputs, out string reason)
    {
        var result = new List<ItemStack>(); outputs = result.AsReadOnly(); reason = null;
        if (!ThreadManager.IsMainThread() || recipe == null || completedApplications <= 0)
        { reason = "xuiRebirthRecipeOutputsUnavailable"; return false; }
        if (!TryGet(recipe, out var definition)) return true;
        foreach (var entry in definition.Outputs)
        {
            var item = ItemClass.GetItem(entry.ItemName, true);
            if (item == null || item.type <= 0 || item.ItemClass == null
                || !string.Equals(item.ItemClass.GetItemName(), entry.ItemName, StringComparison.OrdinalIgnoreCase))
            {
                Issue("missing-output-item:" + definition.RecipeName + "#" + definition.CraftingArea + ":" + entry.ItemName);
                reason = "xuiRebirthRecipeOutputsUnavailable"; outputs = new List<ItemStack>().AsReadOnly(); return false;
            }
            long count = (long)entry.Count * completedApplications;
            if (count > int.MaxValue) { reason = "xuiRebirthRecipeOutputsUnavailable"; outputs = new List<ItemStack>().AsReadOnly(); return false; }
            result.Add(new ItemStack(item.Clone(), (int)count));
        }
        // Multiplicity is completed recipe applications, NEVER recipe.count or
        // AddCraftComplete.craftedCount. Extra outputs receive no input modifier.
        return true;
    }
}

[HarmonyPatch(typeof(RecipesFromXml), nameof(RecipesFromXml.LoadRecipies))]
internal static class RebirthRecipeOutputCapture
{
    private static void Prefix(XmlFile _xmlFile) => RebirthRecipeOutputs.CaptureEffectiveRecipes(_xmlFile?.XmlDoc?.Root);
}
