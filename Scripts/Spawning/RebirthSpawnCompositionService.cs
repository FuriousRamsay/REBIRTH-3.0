using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

#nullable disable

public enum RebirthSpawnProgressionMode { Biome, Gamestage }
public enum RebirthSpawnCategory { RegularLow, RegularMedium, RegularHigh, FeralLow, FeralMedium, FeralHigh, RadiatedLow, RadiatedMedium, RadiatedHigh, Charged, Infernal, Special, Other }
public enum RebirthSleeperGroupPolicy { RebirthRandomized, VanillaPreserved }

public sealed class RebirthSpawnContext
{
    public RebirthSpawnSurface Surface;
    public RebirthSpawnProgressionMode ProgressionMode;
    public int GameStage;
    public string Biome;
    public string RequestedGroup;
    public string PrefabName;
    public string HistoryKey;
}

public sealed class RebirthSpawnTrace
{
    public string Text;
    public int EntityClassId;
    public string EntityName;
    public RebirthSpawnCategory Category;
}

internal sealed class RebirthWeightPoint { public int X; public double Weight; }
internal sealed class RebirthEntityRule
{
    public string Name;
    public int Id;
    public RebirthSpawnCategory Category;
    public int MinGs;
    public int MaxGs;
    public double EntityWeight;
    public HashSet<RebirthSpawnSurface> Contexts;
    public HashSet<string> Biomes;
    public HashSet<string> ThemeTags;
}
internal sealed class RebirthCategoryRule
{
    public RebirthSpawnCategory Category;
    public List<RebirthWeightPoint> GameStageCurve = new List<RebirthWeightPoint>();
    public Dictionary<string, double> BiomeWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
}
internal sealed class RebirthTheme
{
    public string Name;
    public double General = 1.0;
    public Dictionary<string, double> TagMultipliers = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
}
internal sealed class RebirthPrefabTheme
{
    public string Prefab;
    public List<KeyValuePair<string, double>> Themes = new List<KeyValuePair<string, double>>();
}
internal sealed class RebirthCompiledSpawnData
{
    public readonly List<RebirthEntityRule> Entities = new List<RebirthEntityRule>();
    public readonly Dictionary<RebirthSpawnCategory, RebirthCategoryRule> Categories = new Dictionary<RebirthSpawnCategory, RebirthCategoryRule>();
    public readonly Dictionary<string, RebirthTheme> Themes = new Dictionary<string, RebirthTheme>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, RebirthPrefabTheme> Prefabs = new Dictionary<string, RebirthPrefabTheme>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> SleeperExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly string[] SleeperKeywords;
    public readonly List<string> Validation = new List<string>();
    public readonly Dictionary<string, string> VanillaPreservedEntities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public RebirthCompiledSpawnData(string[] keywords) { SleeperKeywords = keywords ?? new string[0]; }
}

public static class RebirthSpawnCompositionService
{
    private static readonly FastTags<TagGroup.Global> s_forbiddenRestrictedSpawnTags =
        FastTags<TagGroup.Global>.Parse("crawler");

    private static readonly object s_sync = new object();
    private static RebirthCompiledSpawnData s_data;
    private static string s_status = "not initialized";
    private static int s_definitionGeneration;
    private static World s_historyWorld;
    private static readonly Dictionary<string, Queue<int>> s_history = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);

    public static void Initialize()
    {
        if (s_data != null) return;
        lock (s_sync)
        {
            if (s_data != null) return;
            try
            {
                string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", "zzz_REBIRTH__3_0");
                string composition = Path.Combine(root, "Config", "_spawn_composition.xml");
                string themes = Path.Combine(root, "Config", "_prefab_spawn_themes.xml");
                RebirthCompiledSpawnData compiled = Compile(composition, themes);
                // Publish only a complete successful generation. A failed compile must remain
                // retryable instead of installing an empty sentinel for the rest of the process.
                s_data = compiled;
                unchecked { s_definitionGeneration++; }
                if (s_definitionGeneration == 0) s_definitionGeneration = 1;
                s_history.Clear();
                s_historyWorld = null;
                s_status = "compiled generation=" + s_definitionGeneration + " entities=" + compiled.Entities.Count + " categories=" + compiled.Categories.Count + " themes=" + compiled.Themes.Count + " prefabs=" + compiled.Prefabs.Count + " preserved=" + compiled.VanillaPreservedEntities.Count + " validation=" + compiled.Validation.Count;
                { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[RebirthSpawn] " + s_status); }
                for (int i = 0; i < compiled.Validation.Count; i++) Log.Warning("[RebirthSpawn] " + compiled.Validation[i]);
            }
            catch (Exception ex)
            {
                s_status = "compile failed (retryable): " + ex.GetType().Name + ": " + ex.Message;
                Log.Error("[RebirthSpawn] " + s_status);
            }
        }
    }

    private static void EnsureLiveHistoryWorld()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (ReferenceEquals(world, s_historyWorld))
            return;
        s_history.Clear();
        s_historyWorld = world;
    }

    public static RebirthSleeperGroupPolicy GetSleeperGroupPolicy(string requestedGroupName)
    {
        Initialize();
        // If compilation is currently unavailable, preserve native sleeper routing rather than
        // publishing an incomplete randomized generation. A later call will retry Initialize().
        if (s_data == null) return RebirthSleeperGroupPolicy.VanillaPreserved;
        string name = requestedGroupName ?? string.Empty;
        if (s_data.SleeperExact.Contains(name)) return RebirthSleeperGroupPolicy.VanillaPreserved;
        for (int i = 0; i < s_data.SleeperKeywords.Length; i++)
            if (name.IndexOf(s_data.SleeperKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return RebirthSleeperGroupPolicy.VanillaPreserved;
        return RebirthSleeperGroupPolicy.RebirthRandomized;
    }

    public static bool IsRestrictedNoSpecialSurface(RebirthSpawnSurface surface)
    {
        return surface == RebirthSpawnSurface.Sleeper ||
               surface == RebirthSpawnSurface.WanderingHorde ||
               surface == RebirthSpawnSurface.EventSpawn;
    }

    public static bool IsForbiddenRestrictedSpawnEntity(EntityClass entityClass)
    {
        if (entityClass == null) return false;

        if (entityClass.Tags.Test_AnySet(s_forbiddenRestrictedSpawnTags))
            return true;

        string name = entityClass.entityClassName ?? string.Empty;
        return name.IndexOf("screamer", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("demolition", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("demolisher", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsForbiddenRestrictedSpawnEntity(int entityClassId)
    {
        return IsForbiddenRestrictedSpawnEntity(EntityClass.GetEntityClass(entityClassId));
    }

    /// <summary>Read-only current b259 spawn-composition classification used by Advanced Disciplines. Display names are never used.</summary>
    public static bool TryGetManagedCategory(string entityClassName, out RebirthSpawnCategory category)
    {
        category=RebirthSpawnCategory.Other;Initialize();if(s_data==null||string.IsNullOrEmpty(entityClassName))return false;
        for(int i=0;i<s_data.Entities.Count;i++){RebirthEntityRule e=s_data.Entities[i];if(e!=null&&string.Equals(e.Name,entityClassName,StringComparison.OrdinalIgnoreCase)){category=e.Category;return true;}}
        return false;
    }

    public static bool TrySelect(RebirthSpawnContext context, Func<double> random01, out RebirthSpawnTrace trace)
    {
        Initialize();
        EnsureLiveHistoryWorld();
        return TrySelectCore(context, random01, s_history, false, out trace);
    }

    public static bool TrySelectDetailed(RebirthSpawnContext context, Func<double> random01, out RebirthSpawnTrace trace)
    {
        Initialize();
        EnsureLiveHistoryWorld();
        return TrySelectCore(context, random01, s_history, true, out trace);
    }

    private static bool TrySelectCore(
        RebirthSpawnContext context,
        Func<double> random01,
        Dictionary<string, Queue<int>> history,
        bool buildTrace,
        out RebirthSpawnTrace trace)
    {
        trace = new RebirthSpawnTrace();
        RebirthCompiledSpawnData data = s_data;
        if (context == null || random01 == null || data == null || data.Entities.Count == 0)
        {
            if (buildTrace) trace.Text = "No compiled population.";
            return false;
        }
        if (context.Surface == RebirthSpawnSurface.Sleeper && GetSleeperGroupPolicy(context.RequestedGroup) == RebirthSleeperGroupPolicy.VanillaPreserved)
        {
            if (buildTrace)
                trace.Text = "Requested Group: " + context.RequestedGroup + "\nSleeper Routing: VanillaPreserved\nTheme Lookup: skipped\nResolution: vanilla";
            return false;
        }

        Dictionary<RebirthSpawnCategory, List<RebirthEntityRule>> eligible = new Dictionary<RebirthSpawnCategory, List<RebirthEntityRule>>();
        string normalizedBiome = null; // Resolve lazily once for biome-restricted candidates.
        for (int i = 0; i < data.Entities.Count; i++)
        {
            RebirthEntityRule e = data.Entities[i];
            if (!e.Contexts.Contains(context.Surface)) continue;
            if (!IsRuntimeEntityRegistered(e)) continue;
            if (IsRestrictedNoSpecialSurface(context.Surface) && IsForbiddenRestrictedSpawnEntity(e.Id)) continue;
            if (context.ProgressionMode == RebirthSpawnProgressionMode.Gamestage && (context.GameStage < e.MinGs || context.GameStage > e.MaxGs)) continue;
            if (context.ProgressionMode == RebirthSpawnProgressionMode.Biome && e.Biomes.Count > 0)
            {
                if (normalizedBiome == null) normalizedBiome = NormalizeBiome(context.Biome);
                if (!e.Biomes.Contains(normalizedBiome)) continue;
            }
            List<RebirthEntityRule> list;
            if (!eligible.TryGetValue(e.Category, out list)) eligible[e.Category] = list = new List<RebirthEntityRule>();
            list.Add(e);
        }

        List<RebirthSpawnCategory> categories = new List<RebirthSpawnCategory>();
        List<double> categoryWeights = new List<double>();
        foreach (KeyValuePair<RebirthSpawnCategory, List<RebirthEntityRule>> pair in eligible)
        {
            RebirthCategoryRule rule;
            if (!data.Categories.TryGetValue(pair.Key, out rule) || pair.Value.Count == 0) continue;
            double weight = context.ProgressionMode == RebirthSpawnProgressionMode.Gamestage
                ? Evaluate(rule.GameStageCurve, context.GameStage)
                : GetBiomeWeight(rule, context.Biome);
            if (!IsFinitePositive(weight)) continue;
            categories.Add(pair.Key);
            categoryWeights.Add(weight);
        }
        double categoryRoll, categoryTotal;
        int categoryIndex = WeightedIndex(categoryWeights, random01(), out categoryRoll, out categoryTotal);
        if (categoryIndex < 0)
        {
            if (buildTrace) trace.Text = "No category has positive finite weight for this context.";
            return false;
        }

        RebirthSpawnCategory selectedCategory = categories[categoryIndex];
        List<RebirthEntityRule> candidates = eligible[selectedCategory];
        using (RebirthSpawnSelectionScratchLease buffers = RebirthSpawnSelectionScratchLease.Acquire(candidates.Count))
        {
            List<double> entityWeights = buffers.EntityWeights;
            List<double> themeMultipliers = buildTrace ? new List<double>(candidates.Count) : null;
            List<double> historyMultipliers = buildTrace ? new List<double>(candidates.Count) : null;
            List<double> logWeights = buffers.LogWeights;
            double maxLogWeight = double.NegativeInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                double tm = GetThemeMultiplier(context.PrefabName, candidates[i]);
                double hm = GetHistoryMultiplier(history, context.HistoryKey, candidates[i].Id);
                if (buildTrace) { themeMultipliers.Add(tm); historyMultipliers.Add(hm); }
                double logWeight = IsFinitePositive(candidates[i].EntityWeight) && IsFinitePositive(tm) && IsFinitePositive(hm)
                    ? Math.Log(candidates[i].EntityWeight) + Math.Log(tm) + Math.Log(hm)
                    : double.NegativeInfinity;
                if (!IsFinite(logWeight)) logWeight = double.NegativeInfinity;
                logWeights.Add(logWeight);
                if (logWeight > maxLogWeight) maxLogWeight = logWeight;
            }
            for (int i = 0; i < logWeights.Count; i++)
            {
                double scaled = IsFinite(maxLogWeight) && IsFinite(logWeights[i])
                    ? Math.Exp(logWeights[i] - maxLogWeight)
                    : 0.0;
                entityWeights.Add(IsFinitePositive(scaled) ? scaled : 0.0);
            }

            double entityRoll, entityTotal;
            int entityIndex = WeightedIndex(entityWeights, random01(), out entityRoll, out entityTotal);
            if (entityIndex < 0)
            {
                if (buildTrace) trace.Text = "Selected category has no positive finite entity weights.";
                return false;
            }
            RebirthEntityRule selected = candidates[entityIndex];
            RecordHistory(history, context.HistoryKey, selected.Id);
            trace.EntityClassId = selected.Id;
            trace.EntityName = selected.Name;
            trace.Category = selected.Category;

            if (buildTrace)
            {
                StringBuilder sb = new StringBuilder(512);
                sb.AppendLine("Progression Mode: " + context.ProgressionMode)
                  .AppendLine("Evaluated Gamestage: " + context.GameStage)
                  .AppendLine("Biome: " + NormalizeBiome(context.Biome))
                  .AppendLine("Requested Group: " + (context.RequestedGroup ?? string.Empty))
                  .AppendLine("Sleeper Routing: " + (context.Surface == RebirthSpawnSurface.Sleeper ? "RebirthRandomized" : "n/a"))
                  .AppendLine("Eligible Candidates: " + CountEligible(eligible))
                  .AppendLine("Selected Category: " + selectedCategory)
                  .AppendLine("Selected Entity: " + selected.Name)
                  .AppendLine("Theme: " + GetThemeDescription(context.PrefabName))
                  .AppendLine("Base Weight: " + selected.EntityWeight.ToString("0.###", CultureInfo.InvariantCulture))
                  .AppendLine("Theme Multiplier: " + themeMultipliers[entityIndex].ToString("0.###", CultureInfo.InvariantCulture))
                  .AppendLine("History Multiplier: " + historyMultipliers[entityIndex].ToString("0.###", CultureInfo.InvariantCulture))
                  .AppendLine("Selection Weight (scaled): " + entityWeights[entityIndex].ToString("0.###", CultureInfo.InvariantCulture))
                  .AppendLine("Category Roll: " + categoryRoll.ToString("0.###", CultureInfo.InvariantCulture) + " / " + categoryTotal.ToString("0.###", CultureInfo.InvariantCulture))
                  .Append("Entity Roll: " + entityRoll.ToString("0.###", CultureInfo.InvariantCulture) + " / " + entityTotal.ToString("0.###", CultureInfo.InvariantCulture));
                trace.Text = sb.ToString();
            }
            return true;
        }
    }

    public static string Simulate(int count, RebirthSpawnContext context, int seed)
    {
        Initialize();
        System.Random rng = new System.Random(seed);
        Dictionary<string, int> totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Queue<int>> isolatedHistory = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        RebirthSpawnTrace t;
        for (int i = 0; i < count; i++)
        {
            if (!TrySelectCore(context, rng.NextDouble, isolatedHistory, false, out t)) continue;
            int n;
            totals.TryGetValue(t.EntityName, out n);
            totals[t.EntityName] = n + 1;
        }
        List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(totals);
        rows.Sort((a,b) => b.Value.CompareTo(a.Value));
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[RebirthSpawn] simulation count=" + count + " seed=" + seed);
        for (int i = 0; i < rows.Count; i++)
            sb.AppendLine(rows[i].Key + " | " + rows[i].Value + " | " + (100.0 * rows[i].Value / Math.Max(1, count)).ToString("0.00", CultureInfo.InvariantCulture) + "%");
        return sb.ToString();
    }

    public static int GetCompiledEntityCount()
    {
        Initialize();
        return s_data != null ? s_data.Entities.Count : 0;
    }

    public static int GetRuntimeRegisteredEntityCount()
    {
        Initialize();

        if (s_data == null)
            return 0;

        int count = 0;

        for (int i = 0; i < s_data.Entities.Count; i++)
        {
            if (IsRuntimeEntityRegistered(s_data.Entities[i]))
                count++;
        }

        return count;
    }

    public static bool IsRuntimeReady(out string detail)
    {
        Initialize();

        int compiled =
            s_data != null ? s_data.Entities.Count : 0;

        int registered =
            GetRuntimeRegisteredEntityCount();

        detail =
            "compiled=" + compiled +
            " registered=" + registered +
            " status='" + s_status + "'";

        return compiled > 0 && registered > 0;
    }

    public static string GetStatus()
    {
        Initialize();

        return "[RebirthSpawn] " + s_status +
               "; registered=" +
               GetRuntimeRegisteredEntityCount() +
               "; runtimeInstalled=" +
               RebirthSpawnCompositionRuntimeIntegration.IsInstalled +
               "; runtimePatches=" +
               RebirthSpawnCompositionRuntimeIntegration.InstalledPatchCount;
    }

    public static string GetValidationReport()
    {
        Initialize();

        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(GetStatus());

        if (s_data != null)
            for (int i = 0; i < s_data.Validation.Count; i++)
                sb.AppendLine(s_data.Validation[i]);

        return sb.ToString();
    }

    private static RebirthCompiledSpawnData Compile(string compositionPath, string themePath)
    {
        XmlDocument doc = new XmlDocument(); doc.Load(compositionPath);
        XmlElement root = doc.DocumentElement; if (root == null || root.Name != "spawn_composition") throw new InvalidDataException("Invalid _spawn_composition.xml root.");
        XmlNodeList keywordNodes = root.SelectNodes("sleeper_policy/keyword"); List<string> keywords = new List<string>(); foreach (XmlNode n in keywordNodes) { string v = Attr(n,"value"); if(v.Length>0) keywords.Add(v); }
        RebirthCompiledSpawnData data = new RebirthCompiledSpawnData(keywords.ToArray());
        foreach (XmlNode n in root.SelectNodes("vanilla_preserved_entities/entity")) { string v=Attr(n,"name"); if(v.Length>0) data.VanillaPreservedEntities[v]=Attr(n,"reason"); }
        foreach (XmlNode n in root.SelectNodes("sleeper_policy/exact")) { string v=Attr(n,"group"); if(v.Length>0) data.SleeperExact.Add(v); }
        foreach (XmlNode n in root.SelectNodes("categories/category"))
        {
            RebirthSpawnCategory c; if(!Enum.TryParse(Attr(n,"name"), true, out c)) { data.Validation.Add("CONFLICTING_METADATA unknown category " + Attr(n,"name")); continue; }
            RebirthCategoryRule cr = new RebirthCategoryRule { Category=c };
            foreach(XmlNode p in n.SelectNodes("gamestage/point")) cr.GameStageCurve.Add(new RebirthWeightPoint { X=IntAttr(p,"gs",0), Weight=DoubleAttr(p,"weight",0) });
            cr.GameStageCurve.Sort((a,b)=>a.X.CompareTo(b.X));
            foreach(XmlNode b in n.SelectNodes("biomes/biome")) cr.BiomeWeights[NormalizeBiome(Attr(b,"name"))]=DoubleAttr(b,"weight",0);
            data.Categories[c]=cr;
        }
        HashSet<string> classifiedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (XmlNode n in root.SelectNodes("entities/entity"))
        {
            string name=Attr(n,"name"); if(!classifiedNames.Add(name)) { data.Validation.Add("CONFLICTING_METADATA duplicate entity " + name); continue; } if(data.VanillaPreservedEntities.ContainsKey(name)) { data.Validation.Add("CONFLICTING_METADATA managed and preserved " + name); continue; } RebirthSpawnCategory c; if(name.Length==0 || !Enum.TryParse(Attr(n,"category"),true,out c)) { data.Validation.Add("UNCLASSIFIED " + name); continue; }
            // 3.1 entity-class IDs are signed string hashes. Negative values are valid and
            // must not be treated as missing. Runtime registration is verified when a spawn
            // is resolved, after entityclasses.xml has been loaded.
            int id=EntityClass.FromString(name);
            RebirthEntityRule e = new RebirthEntityRule { Name=name, Id=id, Category=c, MinGs=IntAttr(n,"min_gs",0), MaxGs=IntAttr(n,"max_gs",int.MaxValue), EntityWeight=DoubleAttr(n,"weight",1), Contexts=ParseEnumSet<RebirthSpawnSurface>(Attr(n,"contexts")), Biomes=ParseStringSet(Attr(n,"biomes"), true), ThemeTags=ParseStringSet(Attr(n,"theme_tags"), false) };
            if(e.Contexts.Count==0) data.Validation.Add("CONTEXT_MISMATCH " + name + " has no contexts");
            if(e.MinGs>e.MaxGs) data.Validation.Add("DEAD_WINDOW " + name);
            data.Entities.Add(e);
        }
        LoadThemes(themePath, data);
        foreach(RebirthSpawnCategory c in Enum.GetValues(typeof(RebirthSpawnCategory))) { if(!data.Categories.ContainsKey(c)) data.Validation.Add("EMPTY_CATEGORY definition missing " + c); else if(c != RebirthSpawnCategory.Other) { bool found=false; for(int i=0;i<data.Entities.Count;i++) if(data.Entities[i].Category==c){found=true;break;} if(!found)data.Validation.Add("EMPTY_CATEGORY no entities " + c); } }
        return data;
    }

    private static void LoadThemes(string path, RebirthCompiledSpawnData data)
    {
        if(!File.Exists(path)) { data.Validation.Add("Theme file missing; themes disabled."); return; }
        XmlDocument doc=new XmlDocument(); doc.Load(path); XmlElement root=doc.DocumentElement; if(root==null || root.Name!="prefab_spawn_themes") { data.Validation.Add("Unknown theme file root."); return; }
        foreach(XmlNode n in root.SelectNodes("themes/theme")) { string name=Attr(n,"name"); if(name.Length==0) continue; RebirthTheme t=new RebirthTheme{Name=name}; XmlNode g=n.SelectSingleNode("general"); if(g!=null)t.General=DoubleAttr(g,"multiplier",1); foreach(XmlNode p in n.SelectNodes("preference")) t.TagMultipliers[Attr(p,"entity_tag")]=DoubleAttr(p,"multiplier",1); data.Themes[name]=t; }
        foreach(XmlNode n in root.SelectNodes("prefabs/prefab")) { string name=Attr(n,"name"); if(name.Length==0)continue; if(data.Prefabs.ContainsKey(name)){data.Validation.Add("DUPLICATE_PREFAB_MAPPING "+name);continue;} RebirthPrefabTheme p=new RebirthPrefabTheme{Prefab=name}; foreach(XmlNode t in n.SelectNodes("theme")){string tn=Attr(t,"name"); if(!data.Themes.ContainsKey(tn)){data.Validation.Add("UNKNOWN_THEME_REFERENCE "+tn+" on "+name);continue;} p.Themes.Add(new KeyValuePair<string,double>(tn,DoubleAttr(t,"weight",1)));} NormalizeThemeWeights(p); data.Prefabs[name]=p; }
    }

    private static bool IsRuntimeEntityRegistered(RebirthEntityRule rule)
    {
        if (rule == null || string.IsNullOrEmpty(rule.Name)) return false;
        EntityClass entityClass = EntityClass.GetEntityClass(rule.Id);
        return entityClass != null
            && string.Equals(entityClass.entityClassName, rule.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static double GetThemeMultiplier(string prefab, RebirthEntityRule e)
    {
        RebirthPrefabTheme p;
        if (string.IsNullOrEmpty(prefab) || s_data == null || !s_data.Prefabs.TryGetValue(prefab, out p) || p.Themes.Count == 0) return 1.0;
        using (RebirthThemeScratchLease buffers = RebirthThemeScratchLease.Acquire(p.Themes.Count))
        {
            double maxMultiplier = 0.0;
            double validWeightTotal = 0.0;
            List<double> multipliers = buffers.Multipliers;
            List<double> weights = buffers.Weights;
            for (int i = 0; i < p.Themes.Count; i++)
            {
                RebirthTheme t;
                if (!s_data.Themes.TryGetValue(p.Themes[i].Key, out t)) { multipliers.Add(0.0); weights.Add(0.0); continue; }
                double m = IsFinitePositive(t.General) ? t.General : 1.0;
                foreach (string tag in e.ThemeTags)
                {
                    double x;
                    if (t.TagMultipliers.TryGetValue(tag, out x) && IsFinitePositive(x) && x > m) m = x;
                }
                double w = IsFinitePositive(p.Themes[i].Value) ? p.Themes[i].Value : 0.0;
                multipliers.Add(m);
                weights.Add(w);
                if (m > maxMultiplier) maxMultiplier = m;
                validWeightTotal += w;
            }
            if (!IsFinitePositive(maxMultiplier) || !IsFinitePositive(validWeightTotal)) return 1.0;
            double scaled = 0.0;
            for (int i = 0; i < multipliers.Count; i++)
                if (weights[i] > 0.0) scaled += (multipliers[i] / maxMultiplier) * (weights[i] / validWeightTotal);
            double result = maxMultiplier * scaled;
            return IsFinitePositive(result) ? result : 1.0;
        }
    }
    private static string GetThemeDescription(string prefab){RebirthPrefabTheme p;if(string.IsNullOrEmpty(prefab)||s_data==null||!s_data.Prefabs.TryGetValue(prefab,out p))return "none";StringBuilder sb=new StringBuilder();for(int i=0;i<p.Themes.Count;i++){if(i>0)sb.Append(',');sb.Append(p.Themes[i].Key).Append('@').Append(p.Themes[i].Value.ToString("0.###",CultureInfo.InvariantCulture));}return sb.ToString();}
    private static double GetHistoryMultiplier(Dictionary<string, Queue<int>> history,string key,int id)
    {
        if(history==null||string.IsNullOrEmpty(key))return 1;
        Queue<int> q;
        if(!history.TryGetValue(key,out q)||q==null||q.Count==0)return 1;
        int matchIndex=-1,index=0;
        foreach(int value in q){if(value==id)matchIndex=index;index++;}
        if(matchIndex<0)return 1;
        int pos=q.Count-1-matchIndex;
        if(pos==0)return .10;
        if(pos==1)return .35;
        if(pos==2)return .65;
        return 1;
    }
    private static void RecordHistory(Dictionary<string, Queue<int>> history,string key,int id){if(history==null||string.IsNullOrEmpty(key))return;Queue<int> q;if(!history.TryGetValue(key,out q))history[key]=q=new Queue<int>(4);q.Enqueue(id);while(q.Count>3)q.Dequeue();}
    private static int WeightedIndex(List<double> weights,double random,out double roll,out double total)
    {
        double max=0.0;
        for(int i=0;i<weights.Count;i++)if(IsFinitePositive(weights[i])&&weights[i]>max)max=weights[i];
        if(!IsFinitePositive(max)){roll=0;total=0;return-1;}
        total=0.0;
        for(int i=0;i<weights.Count;i++)if(IsFinitePositive(weights[i]))total+=weights[i]/max;
        if(!IsFinitePositive(total)){roll=0;return-1;}
        double r=IsFinite(random)?random:0.0;
        roll=Math.Max(0,Math.Min(.999999999999,r))*total;
        double x=0.0;int last=-1;
        for(int i=0;i<weights.Count;i++)
        {
            if(!IsFinitePositive(weights[i]))continue;
            last=i;x+=weights[i]/max;
            if(roll<x)return i;
        }
        return last;
    }
    private static double Evaluate(List<RebirthWeightPoint> points,int x){if(points.Count==0)return 0;if(x<=points[0].X)return points[0].Weight;for(int i=1;i<points.Count;i++){if(x<=points[i].X){RebirthWeightPoint a=points[i-1],b=points[i];if(a.X==b.X)return b.Weight;double t=(x-a.X)/(double)(b.X-a.X);return a.Weight+(b.Weight-a.Weight)*t;}}return points[points.Count-1].Weight;}
    private static double GetBiomeWeight(RebirthCategoryRule r,string biome){double w;return r.BiomeWeights.TryGetValue(NormalizeBiome(biome),out w)?w:0;}
    private static int CountEligible(Dictionary<RebirthSpawnCategory,List<RebirthEntityRule>> d){int n=0;foreach(var p in d)n+=p.Value.Count;return n;}
    private static string NormalizeBiome(string s){s=(s??string.Empty).Trim().ToLowerInvariant().Replace(" ","_");if(s=="pine_forest"||s=="forest")return"forest";if(s=="burnt_forest"||s=="burnt")return"burnt";return s;}
    private static void NormalizeThemeWeights(RebirthPrefabTheme p){double sum=0;for(int i=0;i<p.Themes.Count;i++)if(p.Themes[i].Value>0)sum+=p.Themes[i].Value;if(sum<=0){p.Themes.Clear();return;}for(int i=0;i<p.Themes.Count;i++)p.Themes[i]=new KeyValuePair<string,double>(p.Themes[i].Key,p.Themes[i].Value/sum);}
    private static HashSet<T> ParseEnumSet<T>(string s) where T:struct{HashSet<T> set=new HashSet<T>();foreach(string p in (s??"").Split(',')){T v;if(Enum.TryParse(p.Trim(),true,out v))set.Add(v);}return set;}
    private static HashSet<string> ParseStringSet(string s,bool normalizeBiome){HashSet<string> set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string p in (s??"").Split(',')){string v=p.Trim();if(v.Length>0)set.Add(normalizeBiome?NormalizeBiome(v):v);}return set;}
    private static string Attr(XmlNode n,string name){return n.Attributes?[name]?.Value?.Trim()??string.Empty;}
    private static int IntAttr(XmlNode n,string name,int d){int v;return int.TryParse(Attr(n,name),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:d;}
    private static double DoubleAttr(XmlNode n,string name,double d){double v;return double.TryParse(Attr(n,name),NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&IsFinite(v)&&v>=0?v:d;}
    private static bool IsFinite(double v){return !double.IsNaN(v)&&!double.IsInfinity(v);}
    private static bool IsFinitePositive(double v){return v>0.0&&IsFinite(v);}
}
