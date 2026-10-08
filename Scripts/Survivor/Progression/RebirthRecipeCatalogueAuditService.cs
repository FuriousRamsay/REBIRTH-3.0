using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

#nullable disable

public sealed class RebirthNativeRecipeCatalogueRow
{
    public string RecipeName = string.Empty;
    public string CraftArea = string.Empty;
    public string Tags = string.Empty;
    public string AlwaysUnlocked = string.Empty;
    public string Count = string.Empty;
    public string CraftTime = string.Empty;
    public bool NativeLearnable;
    public bool RebirthMapped;
    public bool RuntimeRecipeResolved;
    public string RebirthKnowledgeId = string.Empty;
    public string LegacyKnowledgeId = string.Empty;
    public string LiteratureItemId = string.Empty;
    public string RebirthCategory = string.Empty;
    public string Classification = string.Empty;
    public string Reason = string.Empty;
}

public sealed class RebirthNativeRecipeCatalogueSnapshot
{
    public string SourcePath = string.Empty;
    public string SourceSha256 = string.Empty;
    public DateTime CapturedUtc;
    public readonly List<RebirthNativeRecipeCatalogueRow> Rows = new List<RebirthNativeRecipeCatalogueRow>();
    public int NativeLearnableCount;
    public int NativeAlwaysUnlockedCount;
    public int RebirthMappedCount;
    public int RebirthMappedMissingFromNativeCount;
    public int RebirthMappedUnresolvedAtRuntimeCount;
    public readonly List<string> RebirthMappedMissingFromNative = new List<string>();
    public readonly List<string> RebirthMappedUnresolvedAtRuntime = new List<string>();
}

/// <summary>
/// Exact installed-game recipe census for the REBIRTH progression redesign.
///
/// This intentionally does not make authoring decisions at runtime.  It captures the stock
/// Data/Config/recipes.xml that is actually installed on the tester's machine, annotates it
/// with current REBIRTH recipe-discovery authority, and exports a deterministic CSV for the
/// authored Pass 3C normalization review.
/// </summary>
public static class RebirthRecipeCatalogueAuditService
{
    private static readonly object Sync = new object();
    private static RebirthNativeRecipeCatalogueSnapshot cached;
    private static DateTime cachedFileWriteUtc;

    public static bool TryCapture(bool forceRefresh, out RebirthNativeRecipeCatalogueSnapshot snapshot, out string error)
    {
        lock(Sync)
        {
            snapshot = null;
            error = string.Empty;
            string path = ResolveVanillaRecipesPath();
            if(string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "Could not locate installed Data/Config/recipes.xml. currentDirectory='" + SafePath(Directory.GetCurrentDirectory()) + "' baseDirectory='" + SafePath(AppDomain.CurrentDomain.BaseDirectory) + "'.";
                return false;
            }

            DateTime writeUtc;
            try { writeUtc = File.GetLastWriteTimeUtc(path); }
            catch { writeUtc = DateTime.MinValue; }

            if(!forceRefresh && cached != null && string.Equals(cached.SourcePath,path,StringComparison.OrdinalIgnoreCase) && writeUtc == cachedFileWriteUtc)
            {
                snapshot = cached;
                return true;
            }

            try
            {
                RebirthNativeRecipeCatalogueSnapshot result = BuildSnapshot(path);
                cached = result;
                cachedFileWriteUtc = writeUtc;
                snapshot = result;
                return true;
            }
            catch(Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }
    }

    public static string BuildSummary(bool forceRefresh)
    {
        RebirthNativeRecipeCatalogueSnapshot snapshot;
        string error;
        if(!TryCapture(forceRefresh,out snapshot,out error))
            return "[REBIRTH Survivor][RecipeCatalogue] ERROR " + error;
        return "[REBIRTH Survivor][RecipeCatalogue] source='" + snapshot.SourcePath + "' sha256=" + snapshot.SourceSha256
            + " recipes=" + snapshot.Rows.Count
            + " nativeLearnable=" + snapshot.NativeLearnableCount
            + " nativeAlwaysUnlocked=" + snapshot.NativeAlwaysUnlockedCount
            + " rebirthMapped=" + snapshot.RebirthMappedCount
            + " mappedMissingNative=" + snapshot.RebirthMappedMissingFromNativeCount
            + " mappedRuntimeUnresolved=" + snapshot.RebirthMappedUnresolvedAtRuntimeCount
            + " capturedUtc=" + snapshot.CapturedUtc.ToString("o",CultureInfo.InvariantCulture);
    }

    public static string BuildRecipeDiagnostic(string recipeName)
    {
        RebirthNativeRecipeCatalogueSnapshot snapshot;
        string error;
        if(!TryCapture(false,out snapshot,out error)) return "[REBIRTH Survivor][RecipeCatalogue] ERROR " + error;
        string wanted=(recipeName??string.Empty).Trim();
        for(int i=0;i<snapshot.Rows.Count;i++)
        {
            RebirthNativeRecipeCatalogueRow r=snapshot.Rows[i];
            if(!string.Equals(r.RecipeName,wanted,StringComparison.OrdinalIgnoreCase)) continue;
            return "[REBIRTH Survivor][RecipeCatalogue] recipe="+r.RecipeName
                +" craftArea="+BlankAs(r.CraftArea,"inventory")
                +" tags="+BlankAs(r.Tags,"none")
                +" alwaysUnlocked="+BlankAs(r.AlwaysUnlocked,"false")
                +" nativeLearnable="+r.NativeLearnable
                +" rebirthMapped="+r.RebirthMapped
                +" runtimeResolved="+r.RuntimeRecipeResolved
                +" knowledge="+BlankAs(r.RebirthKnowledgeId,"none")
                +" legacy="+BlankAs(r.LegacyKnowledgeId,"none")
                +" literature="+BlankAs(r.LiteratureItemId,"none")
                +" classification="+r.Classification
                +" reason="+r.Reason;
        }
        return "[REBIRTH Survivor][RecipeCatalogue] recipe not found in installed native catalogue: " + wanted;
    }

    public static string BuildUnmappedLearnableReport(int maxRows)
    {
        RebirthNativeRecipeCatalogueSnapshot snapshot;
        string error;
        if(!TryCapture(false,out snapshot,out error)) return "[REBIRTH Survivor][RecipeCatalogue] ERROR " + error;
        if(maxRows<1)maxRows=1; if(maxRows>500)maxRows=500;
        StringBuilder b=new StringBuilder();
        int total=0,shown=0;
        for(int i=0;i<snapshot.Rows.Count;i++)
        {
            RebirthNativeRecipeCatalogueRow r=snapshot.Rows[i];
            if(!r.NativeLearnable || r.RebirthMapped)continue;
            total++;
            if(shown>=maxRows)continue;
            if(shown==0)b.Append("[REBIRTH Survivor][RecipeCatalogue] unmapped native learnables");
            b.Append("\n  ").Append(r.RecipeName)
                .Append(" area=").Append(BlankAs(r.CraftArea,"inventory"))
                .Append(" tags=").Append(BlankAs(r.Tags,"none"))
                .Append(" class=").Append(r.Classification);
            shown++;
        }
        if(shown==0)b.Append("[REBIRTH Survivor][RecipeCatalogue] unmapped native learnables=0");
        b.Append("\n  total=").Append(total).Append(" shown=").Append(shown).Append(" max=").Append(maxRows);
        return b.ToString();
    }

    public static bool TryExport(out string outputPath, out string detail)
    {
        outputPath=string.Empty;detail=string.Empty;
        RebirthNativeRecipeCatalogueSnapshot snapshot;
        string error;
        if(!TryCapture(true,out snapshot,out error)){detail=error;return false;}
        try
        {
            string save=GameIO.GetSaveGameDir();
            if(string.IsNullOrEmpty(save)){detail="Save-game directory is unavailable.";return false;}
            string dir=Path.Combine(save,"RebirthData","Survivor","Diagnostics");
            Directory.CreateDirectory(dir);
            outputPath=Path.Combine(dir,"rebirth_native_recipe_catalogue_"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+".csv");
            WriteCsv(snapshot,outputPath);
            string meta=Path.ChangeExtension(outputPath,".txt");
            File.WriteAllText(meta,BuildMetadata(snapshot),new UTF8Encoding(false));
            detail="Wrote exact installed recipe catalogue CSV and metadata. csv='"+outputPath+"' metadata='"+meta+"'.";
            return true;
        }
        catch(Exception ex){detail=ex.GetType().Name+": "+ex.Message;return false;}
    }

    private static RebirthNativeRecipeCatalogueSnapshot BuildSnapshot(string path)
    {
        XDocument doc=XDocument.Load(path,LoadOptions.None);
        XElement root=doc.Root;
        if(root==null || !string.Equals(root.Name.LocalName,"recipes",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Native recipes.xml root element is not <recipes>.");

        Dictionary<string,RebirthRecipeKnowledgeRule> mapped=new Dictionary<string,RebirthRecipeKnowledgeRule>(StringComparer.OrdinalIgnoreCase);
        RebirthRecipeKnowledgeRule[] rules=RebirthProgressionRuntimeConfig.GetRecipeRulesSnapshot();
        for(int i=0;i<rules.Length;i++) if(rules[i]!=null && !string.IsNullOrEmpty(rules[i].RecipeName)) mapped[rules[i].RecipeName]=rules[i];

        RebirthNativeRecipeCatalogueSnapshot result=new RebirthNativeRecipeCatalogueSnapshot();
        result.SourcePath=path;
        result.SourceSha256=ComputeSha256(path);
        result.CapturedUtc=DateTime.UtcNow;
        HashSet<string> nativeNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach(XElement e in root.Elements("recipe"))
        {
            string name=Attr(e,"name"); if(string.IsNullOrEmpty(name))continue;
            nativeNames.Add(name);
            string tags=Attr(e,"tags");
            bool nativeLearnable=HasTag(tags,"learnable");
            bool alwaysUnlocked=string.Equals(Attr(e,"always_unlocked"),"true",StringComparison.OrdinalIgnoreCase);
            RebirthRecipeKnowledgeRule rule; bool isMapped=mapped.TryGetValue(name,out rule);
            bool runtimeResolved=TryResolveRuntimeRecipe(name);
            RebirthNativeRecipeCatalogueRow row=new RebirthNativeRecipeCatalogueRow();
            row.RecipeName=name;
            row.CraftArea=Attr(e,"craft_area");
            row.Tags=tags;
            row.AlwaysUnlocked=Attr(e,"always_unlocked");
            row.Count=Attr(e,"count");
            row.CraftTime=Attr(e,"craft_time");
            row.NativeLearnable=nativeLearnable;
            row.RebirthMapped=isMapped;
            row.RuntimeRecipeResolved=runtimeResolved;
            if(rule!=null)
            {
                row.RebirthKnowledgeId=rule.KnowledgeId??string.Empty;
                row.LegacyKnowledgeId=rule.LegacyKnowledgeId??string.Empty;
                row.LiteratureItemId=rule.LiteratureItemId??string.Empty;
                row.RebirthCategory=rule.Category??string.Empty;
            }
            Classify(row,alwaysUnlocked);
            result.Rows.Add(row);
            if(nativeLearnable)result.NativeLearnableCount++;
            if(alwaysUnlocked)result.NativeAlwaysUnlockedCount++;
            if(isMapped)result.RebirthMappedCount++;
        }

        result.Rows.Sort(delegate(RebirthNativeRecipeCatalogueRow a,RebirthNativeRecipeCatalogueRow b){return string.Compare(a.RecipeName,b.RecipeName,StringComparison.OrdinalIgnoreCase);});
        foreach(KeyValuePair<string,RebirthRecipeKnowledgeRule> kv in mapped)
        {
            if(!nativeNames.Contains(kv.Key))
            {
                result.RebirthMappedMissingFromNativeCount++;
                result.RebirthMappedMissingFromNative.Add(kv.Key);
            }
            if(!TryResolveRuntimeRecipe(kv.Key))
            {
                result.RebirthMappedUnresolvedAtRuntimeCount++;
                result.RebirthMappedUnresolvedAtRuntime.Add(kv.Key);
            }
        }
        result.RebirthMappedMissingFromNative.Sort(StringComparer.OrdinalIgnoreCase);
        result.RebirthMappedUnresolvedAtRuntime.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static void Classify(RebirthNativeRecipeCatalogueRow row,bool alwaysUnlocked)
    {
        if(row.RebirthMapped)
        {
            row.Classification="REBIRTH_DISCOVERY_ACTIVE";
            row.Reason="Already has explicit individual REBIRTH recipe/schematic discovery authority.";
            return;
        }
        if(alwaysUnlocked)
        {
            row.Classification="UNIVERSAL_NATIVE";
            row.Reason="Native catalogue explicitly marks this recipe always_unlocked; keep universal unless an authored REBIRTH exception is approved.";
            return;
        }
        if(!row.NativeLearnable)
        {
            row.Classification="NATIVE_NONLEARNABLE_REVIEW";
            row.Reason="Not natively tagged learnable. Do not introduce a new discovery requirement automatically.";
            return;
        }
        string n=(row.RecipeName??string.Empty).ToLowerInvariant();
        if(n.Contains("bundle"))
        {
            row.Classification="CONVENIENCE_RECIPE_CANDIDATE";
            row.Reason="Native learnable bulk/bundle craft. Usually progression-neutral; authored review required.";
        }
        else if(n.StartsWith("food") || n.StartsWith("drink"))
        {
            row.Classification="INDIVIDUAL_RECIPE_CANDIDATE";
            row.Reason="Native learnable food/drink recipe; candidate for an individual reusable recipe source.";
        }
        else if(n.Contains("vehicle") || n.Contains("station") || n.Contains("workbench") || n.Contains("forge") || n.Contains("cementmixer") || n.Contains("generator") || n.Contains("batterybank") || n.Contains("electric") || n.Contains("relay") || n.Contains("switch") || n.Contains("turret") || n.Contains("drone"))
        {
            row.Classification="INDIVIDUAL_SCHEMATIC_CANDIDATE";
            row.Reason="Native learnable technical fabrication/device recipe; candidate for an individual reusable schematic/manual.";
        }
        else if(n.Contains("armor") || n.Contains("gun") || n.Contains("ammo") || n.Contains("rocket") || n.Contains("grenade") || n.Contains("mine") || n.Contains("mod"))
        {
            row.Classification="EQUIPMENT_SCHEMATIC_POLICY_REVIEW";
            row.Reason="Native learnable equipment/ammunition recipe. Requires authored review against REBIRTH's loot-first modern-equipment policy.";
        }
        else if(n.Contains("medical") || n.Contains("drug") || n.Contains("firstaid") || n.Contains("bandage") || n.Contains("splint") || n.Contains("cast"))
        {
            row.Classification="MEDICAL_RECIPE_OR_PROCEDURE_REVIEW";
            row.Reason="Native learnable medical content; decide whether the knowledge object is a craft recipe, treatment procedure, or both.";
        }
        else
        {
            row.Classification="AUTHORED_REVIEW_REQUIRED";
            row.Reason="Native learnable recipe with no safe automatic REBIRTH classification.";
        }
    }

    private static void WriteCsv(RebirthNativeRecipeCatalogueSnapshot snapshot,string path)
    {
        StringBuilder b=new StringBuilder();
        b.AppendLine("recipe,craft_area,tags,always_unlocked,count,craft_time,native_learnable,rebirth_mapped,runtime_recipe_resolved,rebirth_knowledge,legacy_knowledge,literature_item,rebirth_category,classification,reason");
        for(int i=0;i<snapshot.Rows.Count;i++)
        {
            RebirthNativeRecipeCatalogueRow r=snapshot.Rows[i];
            AppendCsv(b,r.RecipeName);b.Append(',');AppendCsv(b,r.CraftArea);b.Append(',');AppendCsv(b,r.Tags);b.Append(',');AppendCsv(b,r.AlwaysUnlocked);b.Append(',');AppendCsv(b,r.Count);b.Append(',');AppendCsv(b,r.CraftTime);b.Append(',');
            AppendCsv(b,r.NativeLearnable?"true":"false");b.Append(',');AppendCsv(b,r.RebirthMapped?"true":"false");b.Append(',');AppendCsv(b,r.RuntimeRecipeResolved?"true":"false");b.Append(',');
            AppendCsv(b,r.RebirthKnowledgeId);b.Append(',');AppendCsv(b,r.LegacyKnowledgeId);b.Append(',');AppendCsv(b,r.LiteratureItemId);b.Append(',');AppendCsv(b,r.RebirthCategory);b.Append(',');AppendCsv(b,r.Classification);b.Append(',');AppendCsv(b,r.Reason);b.AppendLine();
        }
        File.WriteAllText(path,b.ToString(),new UTF8Encoding(true));
    }

    private static string BuildMetadata(RebirthNativeRecipeCatalogueSnapshot s)
    {
        StringBuilder b=new StringBuilder();
        b.AppendLine("REBIRTH 3.0 exact installed native recipe catalogue capture");
        b.AppendLine("Captured UTC: "+s.CapturedUtc.ToString("o",CultureInfo.InvariantCulture));
        b.AppendLine("Native source: "+s.SourcePath);
        b.AppendLine("Native SHA-256: "+s.SourceSha256);
        b.AppendLine("Recipes: "+s.Rows.Count);
        b.AppendLine("Native learnable: "+s.NativeLearnableCount);
        b.AppendLine("Native always unlocked: "+s.NativeAlwaysUnlockedCount);
        b.AppendLine("REBIRTH mapped: "+s.RebirthMappedCount);
        b.AppendLine("REBIRTH mapped but absent from stock XML (normally mod-added recipes): "+s.RebirthMappedMissingFromNativeCount);
        for(int i=0;i<s.RebirthMappedMissingFromNative.Count;i++)b.AppendLine("  "+s.RebirthMappedMissingFromNative[i]);
        b.AppendLine("REBIRTH mapped but unresolved by CraftingManager after game load: "+s.RebirthMappedUnresolvedAtRuntimeCount);
        for(int i=0;i<s.RebirthMappedUnresolvedAtRuntime.Count;i++)b.AppendLine("  "+s.RebirthMappedUnresolvedAtRuntime[i]);
        b.AppendLine();
        b.AppendLine("Classification is intentionally conservative. It is an audit aid, not runtime progression authority.");
        return b.ToString();
    }

    private static string ResolveVanillaRecipesPath()
    {
        List<string> roots=new List<string>();
        try { AddRoot(roots,Directory.GetCurrentDirectory()); } catch { }
        try { AddRoot(roots,AppDomain.CurrentDomain.BaseDirectory); } catch { }
        for(int i=0;i<roots.Count;i++)
        {
            DirectoryInfo d=null;
            try { d=new DirectoryInfo(roots[i]); } catch { }
            for(int depth=0;d!=null&&depth<6;depth++,d=d.Parent)
            {
                string p=Path.Combine(d.FullName,"Data","Config","recipes.xml");
                if(File.Exists(p))return Path.GetFullPath(p);
            }
        }
        return string.Empty;
    }

    private static void AddRoot(List<string> roots,string value)
    {
        if(string.IsNullOrEmpty(value))return;
        string full=Path.GetFullPath(value);
        for(int i=0;i<roots.Count;i++)if(string.Equals(roots[i],full,StringComparison.OrdinalIgnoreCase))return;
        roots.Add(full);
    }

    private static bool TryResolveRuntimeRecipe(string recipeName)
    {
        try { return CraftingManager.GetRecipe(recipeName??string.Empty)!=null; }
        catch { return false; }
    }

    private static string ComputeSha256(string path)
    {
        using(FileStream s=File.OpenRead(path))using(SHA256 sha=SHA256.Create())
        {
            byte[] h=sha.ComputeHash(s);StringBuilder b=new StringBuilder(h.Length*2);
            for(int i=0;i<h.Length;i++)b.Append(h[i].ToString("x2",CultureInfo.InvariantCulture));
            return b.ToString();
        }
    }

    private static string Attr(XElement e,string name){XAttribute a=e.Attribute(name);return a==null?string.Empty:(a.Value??string.Empty).Trim();}
    private static bool HasTag(string tags,string wanted){string[] p=(tags??string.Empty).Split(',');for(int i=0;i<p.Length;i++)if(string.Equals((p[i]??string.Empty).Trim(),wanted,StringComparison.OrdinalIgnoreCase))return true;return false;}
    private static string BlankAs(string value,string fallback){return string.IsNullOrEmpty(value)?fallback:value;}
    private static string SafePath(string value){return (value??string.Empty).Replace("\r"," ").Replace("\n"," ");}
    private static void AppendCsv(StringBuilder b,string value){string v=value??string.Empty;b.Append('"').Append(v.Replace("\"","\"\"")).Append('"');}
}
