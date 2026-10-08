using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;
public static class RebirthStationGridQueue{
public const string Prefix = "rebirth.station.queue.";
public static bool IsMarked(Recipe recipe) => recipe?.ingredients?.Count > 0 && recipe.ingredients[0]?.itemValue != null && recipe.ingredients[0].itemValue.HasMetadata(Prefix + "version");
private static string DefinitionKey(Recipe source)
    {
        using (var bytes = new MemoryStream())
        {
            using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(bytes);
                source.Write(writer);
                writer.Write(source.craftingToolType);
                writer.Write(source.UseIngredientModifier);
                writer.Write(source.tags.ToString());
                writer.Write(source.materialBasedRecipe);
                writer.Write(source.wildcardForgeCategory);
                writer.Write(source.wildcardCampfireCategory);
            }
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "");
        }
    }
public sealed class DefinitionBinding
    {
        public string RecipeName { get; private set; }
        public string DefinitionId { get; private set; }
        public int Batches { get; private set; }
        public int Tier { get; private set; }
        public int OutputCount { get; private set; }
        internal DefinitionBinding(Recipe source,string definitionId,int batches,int tier,int count)
        {RecipeName=source.GetName();DefinitionId=definitionId;Batches=batches;Tier=tier;OutputCount=count;}
    }
public static bool TryGetDefinitionBinding(Recipe recipe,IList<Recipe> definitions,out DefinitionBinding binding)
    {
        binding=null;
        Recipe source;int batches,tier;
        if(!TryResolveDefinition(recipe,definitions,out source,out batches,out tier))return false;
        // GetName identifies the output item, not a unique catalogue recipe.
        if(!recipe.ingredients[0].itemValue.TryGetMetadata(Prefix+"definition",out string definitionId))return false;
        binding=new DefinitionBinding(source,definitionId,batches,tier,recipe.count);
        return true;
    }
public static bool TryGetJobId(Recipe recipe,out string job)
    {
        job=null;
        if(!IsMarked(recipe)||!recipe.ingredients[0].itemValue.TryGetMetadata(Prefix+"job",out string value)||
            !Guid.TryParseExact(value,"N",out var id)||id==Guid.Empty)return false;
        job=id.ToString("N");return true;
    }
public static bool TryGetAdmittedSource(Recipe queued,IList<Recipe> definitions,out Recipe source)
    {
        return TryResolveDefinition(queued,definitions,out source,out _,out _);
    }
private static bool TryResolveDefinition(Recipe recipe,IList<Recipe> definitions,
        out Recipe source,out int batches,out int tier)
    {
        source=null;batches=0;tier=0;
        if (!IsMarked(recipe) || definitions == null) return false;
        // Grid payment carries at most nine exact nonempty fragments. Native ItemStack.Write
        // clips counts above UInt16; restoring such a payload would lose paid custody.
        if(recipe.ingredients.Count>9)return false;
        foreach(ItemStack fragment in recipe.ingredients)
            if(fragment==null||fragment.itemValue==null||fragment.itemValue.IsEmpty()
                ||fragment.count<1||fragment.count>ushort.MaxValue
                ||fragment.itemValue.Metadata!=null&&fragment.itemValue.Metadata.Count>byte.MaxValue)return false;
        ItemValue marker = recipe.ingredients[0].itemValue;
        if (!marker.TryGetMetadata(Prefix + "version", out int version) || version != 1
            || !marker.TryGetMetadata(Prefix + "definition", out string key) || key == null || key.Length != 64
            || !marker.TryGetMetadata(Prefix + "batches", out batches) || batches < 1 || batches > 9999
            || !marker.TryGetMetadata(Prefix + "tier", out tier) || tier < 0 || tier > 6
            || recipe.IsScrap || recipe.materialBasedRecipe || recipe.count < 1 || recipe.count > 32767 || recipe.craftExpGain < 0
            || float.IsNaN(recipe.craftingTime) || float.IsInfinity(recipe.craftingTime) || recipe.craftingTime < 0f) return false;
        source = null;
        foreach (Recipe candidate in definitions)
        {
            if (candidate == null || candidate.ingredients == null || candidate.itemValueType != recipe.itemValueType
                || candidate.craftingArea != recipe.craftingArea || candidate.IsScrap || candidate.materialBasedRecipe
                || (long)candidate.count * batches != recipe.count || DefinitionKey(candidate) != key) continue;
            if (source != null) return false; // Ambiguous current catalogue must not guess.
            source = candidate;
        }
        if (source == null || (long)source.craftExpGain * batches != recipe.craftExpGain) return false;
        return true;
    }
}