using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    private const string FrozenField="rebirth.recipe.outputs.frozen.v1";
    internal static void ClearFrozen(ItemValue value)=>value?.Metadata?.Remove(FrozenField);
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
    // Catalogue identity only; never payment or delivery evidence.
    internal static string DefinitionIdentity(Recipe recipe)
    {
        if(!ThreadManager.IsMainThread()||recipe==null)throw new InvalidOperationException("Current recipe catalogue required for output binding.");
        if(!TryGet(recipe,out var definition))return "none";
        var text=new StringBuilder();text.Append(definition.SourceId).Append('|');
        foreach(var output in definition.Outputs)
            text.Append(output.ItemName.Length).Append(':').Append(output.ItemName).Append(':')
                .Append(output.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';');
        return text.ToString();
    }
    public static bool HasOutputs(Recipe recipe) => recipe?.ingredients?.Count>0&&recipe.ingredients[0].itemValue.HasMetadata(FrozenField)||TryGet(recipe, out _);
    internal static bool HasFrozenPlan(Recipe recipe) => recipe?.ingredients?.Count>0&&recipe.ingredients[0]?.itemValue?.HasMetadata(FrozenField)==true;
    // Only call on an owned concrete queue/batch recipe, never the global recipe.
    internal static bool Freeze(Recipe recipe)
    {
        if(recipe==null||recipe.ingredients==null||recipe.ingredients.Count<1||recipe.ingredients[0]?.itemValue==null)return false;
        var marker=recipe.ingredients[0].itemValue;
        if(marker.HasMetadata(FrozenField))return TryPlan(recipe,1,out _,out _);
        if(marker.Metadata?.Count>=byte.MaxValue||!TryPlan(recipe,1,out var outputs,out _)||outputs.Count>64)return false;
        string image=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(outputs.Select(item=>item.Clone()).ToArray()));
        if(image.Length>1048576)return false;marker.SetMetadata(FrozenField,image);return true;
    }
    // Detached plan data only. Neither method authenticates payment/ownership.
    internal static bool TryCaptureFrozenOutputs(Recipe recipe,out string image)
    {
        image=null;
        try
        {
            if(!TryPlan(recipe,1,out var outputs,out _)||outputs.Count>64)return false;
            string candidate=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(outputs.Select(x=>x.Clone()).ToArray()));
            if(!TryReadFrozenOutputs(candidate,out _))return false;image=candidate;return true;
        }catch{return false;}
    }
    // Caller supplies an owned detached recipe and an already-authorized original
    // plan. Never call on the catalogue or use an image as payment evidence.
    internal static bool TryApplyFrozenOutputs(Recipe detached,string image)
    {
        if(detached?.ingredients==null||detached.ingredients.Count<1||detached.ingredients[0]?.itemValue==null||
            !TryReadFrozenOutputs(image,out _))return false;
        var marker=detached.ingredients[0].itemValue;
        if(!marker.HasMetadata(FrozenField)&&marker.Metadata?.Count>=byte.MaxValue)return false;
        marker.SetMetadata(FrozenField,image);return true;
    }
    private static bool TryReadFrozenOutputs(string image,out ItemStack[] items)
    {
        items=null;
        try
        {
            if(!ThreadManager.IsMainThread()||string.IsNullOrEmpty(image)||image.Length>1048576)return false;
            byte[] frame=Convert.FromBase64String(image);
            // Bound dimensions before the shared snapshot decoder allocates rows.
            using(var stream=new MemoryStream(frame,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadUInt32()!=0x31534352)return false;
                int count=reader.ReadInt32();if(count<0||count>64)return false;
            }
            var decoded=RebirthCamperInventorySnapshot.Restore(frame);
            if(decoded.Length>64||decoded.Any(x=>x==null||x.IsEmpty()||x.count<1)||
                Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(decoded))!=image)return false;
            items=decoded;return true;
        }catch{return false;}
    }
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
        if(recipe.ingredients?.Count>0&&recipe.ingredients[0]?.itemValue?.HasMetadata(FrozenField)==true)
        {
            try
            {
                if(!recipe.ingredients[0].itemValue.TryGetMetadata(FrozenField,out string frame)||!TryReadFrozenOutputs(frame,out var frozen))return false;
                foreach(var item in frozen)
                {
                    if(item==null||item.IsEmpty()||item.count<1)return false;
                    long count=(long)item.count*completedApplications;if(count>int.MaxValue)return false;
                    var copy=item.Clone();copy.count=(int)count;result.Add(copy);
                }
                return true;
            }
            catch{reason="xuiRebirthRecipeOutputsUnavailable";return false;}
        }
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
