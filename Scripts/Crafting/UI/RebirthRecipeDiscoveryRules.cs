using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

public static class RebirthRecipeDiscoveryRules
{
    private static readonly object Sync = new object();
    private static bool loaded;
    private static RebirthRecipeDiscoveryPolicy policy;
    // Called with Sync held; presentation, admission and owner snapshots share one load.
    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(typeof(RebirthRecipeDiscoveryRules).Assembly.Location), "Config", "_CraftingDiscovery", "recipe_policy.xml");
            using (var reader = XmlReader.Create(path,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=2097152 }))
                policy = RebirthRecipeDiscoveryPolicy.Read(XDocument.Load(reader).Root);
        }
        catch (Exception ex) { Log.Error("[REBIRTH Crafting] Recipe discovery policy unavailable: " + ex.Message); }
    }
    public static bool Allows(EntityPlayer player, string recipe)
    {
        lock (Sync)
        {
            EnsureLoaded();
            return policy != null && policy.Allows(recipe, id => player != null && RebirthKnowledgeService.HasKnowledge(player,RebirthLiteratureService.RecipeReadMarker(id)));
        }
    }
    public static bool TryGetRequiredReading(string canonicalRecipe,out string knowledgeId)
    {
        lock(Sync)
        {
            EnsureLoaded();knowledgeId=string.Empty;
            return policy!=null&&policy.TryGetRequiredReading(canonicalRecipe,out knowledgeId);
        }
    }
    public static bool RequiresReading(string knowledgeId)
    {
        lock (Sync)
        {
            EnsureLoaded();
            return policy != null && policy.RequiresReading(knowledgeId);
        }
    }
}