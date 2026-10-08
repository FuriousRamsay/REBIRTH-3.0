using System;
using System.Runtime.CompilerServices;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

#nullable disable

/// <summary>Definition binding for generic paid grid jobs in the existing native recipe codec.</summary>
public static class RebirthStationGridQueue
{
    private sealed class DefinitionState { internal bool Resolved; }
    private static readonly ConditionalWeakTable<Recipe,DefinitionState> Definitions =
        new ConditionalWeakTable<Recipe,DefinitionState>();
    // Definition resolution is necessary, but never evidence of ingredient payment or owner authority.
    public static bool HasResolvedDefinition(Recipe recipe)
        => !IsMarked(recipe) || Definitions.TryGetValue(recipe,out var state) && state.Resolved;

    // Concrete grid recipes already contain the whole batch's output and paid fragments.
    public static bool HasValidMultiplier(Recipe recipe,int multiplier)
        => !IsMarked(recipe) || multiplier==1;

    public const string Prefix = "rebirth.station.queue.";
    public static bool IsMarked(Recipe recipe)
        => recipe?.ingredients?.Count > 0 && recipe.ingredients[0]?.itemValue != null
            && recipe.ingredients[0].itemValue.HasMetadata(Prefix + "version");

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

    public static bool Stamp(Recipe concrete, Recipe admitted, int batches, int tier)
    {
        if (concrete == null || concrete.ingredients == null || concrete.ingredients.Count < 1 || admitted == null || admitted.ingredients == null || batches < 1 || batches > 9999
            || IsMarked(concrete) || tier < 0 || tier > 6 || admitted.IsScrap || admitted.materialBasedRecipe
            || concrete.itemValueType != admitted.itemValueType || concrete.craftingArea != admitted.craftingArea
            || (long)admitted.count * batches != concrete.count) return false;
        // Refuse nonroundtrippable paid custody before changing any item metadata.
        if(concrete.ingredients.Count>9||concrete.count<1||concrete.count>32767||concrete.craftExpGain<0||
            (long)admitted.craftExpGain*batches!=concrete.craftExpGain||
            float.IsNaN(concrete.craftingTime)||float.IsInfinity(concrete.craftingTime)||concrete.craftingTime<0f)return false;
        foreach(ItemStack fragment in concrete.ingredients)
            if(fragment==null||fragment.itemValue==null||fragment.itemValue.IsEmpty()||fragment.count<1||fragment.count>ushort.MaxValue
                ||fragment.itemValue.Metadata!=null&&fragment.itemValue.Metadata.Count>byte.MaxValue)return false;
        // Queue markers belong to detached paid payloads, never the catalogue definition.
        if(ReferenceEquals(concrete,admitted))return false;
        foreach(ItemStack fragment in concrete.ingredients)
            foreach(ItemStack sourceFragment in admitted.ingredients)
                if(sourceFragment!=null&&ReferenceEquals(fragment.itemValue,sourceFragment.itemValue))return false;
        ItemValue marker = concrete.ingredients[0].itemValue;
        if (marker == null || marker.IsEmpty()) return false;
        // Native ItemValue.Write stores metadata count in a byte. Reserve all four new keys
        // before mutation; reject existing queue namespace so refund cannot erase input data.
        foreach(ItemStack fragment in concrete.ingredients)
        {
            var metadata=fragment.itemValue.Metadata;
            if(metadata==null)continue;
            if(metadata.Count>(ReferenceEquals(fragment.itemValue,marker)?byte.MaxValue-4:byte.MaxValue))return false;
            foreach(string key in metadata.Keys)
                if(key.StartsWith(Prefix,StringComparison.Ordinal))return false;
        }
        string definition = DefinitionKey(admitted);
        marker.SetMetadata(Prefix + "version", 1);
        marker.SetMetadata(Prefix + "definition", definition);
        marker.SetMetadata(Prefix + "batches", batches);
        marker.SetMetadata(Prefix + "tier", tier);
        Definitions.GetValue(concrete,_ => new DefinitionState()).Resolved=true;
        return true;
    }

    // A marked job is not trusted merely because its client supplied a marker. This method
    // restores definition fields only; the future coordinator must separately verify paid custody.
    public static bool TryRestore(Recipe recipe, IList<Recipe> definitions)
    {
        bool resolved=RestoreDefinition(recipe,definitions);
        if(recipe!=null)Definitions.GetValue(recipe,_ => new DefinitionState()).Resolved=resolved;
        return resolved;
    }
    /// <summary>Verified catalogue binding only; never evidence of payment, ownership or completion.</summary>
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
    // Job identity survives native Recipe serialization in the detached paid fragment.
    // This marker identifies a job; it is never evidence that payment was committed.
    public static bool TryBindJob(Recipe recipe,Guid job)
    {
        if(job==Guid.Empty||!IsMarked(recipe))return false;
        var marker=recipe.ingredients[0].itemValue;
        string key=Prefix+"job";
        if(marker.HasMetadata(key))return false;
        if(marker.Metadata!=null&&marker.Metadata.Count>=byte.MaxValue)return false;
        marker.SetMetadata(key,job.ToString("N"));return true;
    }
    public static bool TryGetJobId(Recipe recipe,out string job)
    {
        job=null;
        if(!IsMarked(recipe)||!recipe.ingredients[0].itemValue.TryGetMetadata(Prefix+"job",out string value)||
            !Guid.TryParseExact(value,"N",out var id)||id==Guid.Empty)return false;
        job=id.ToString("N");return true;
    }
    // Exact bound catalogue source for authority recovery; no payment/publication permission.
    public static bool TryGetAdmittedSource(Recipe queued,IList<Recipe> definitions,out Recipe source)
    {
        return TryResolveDefinition(queued,definitions,out source,out _,out _);
    }    private static bool TryResolveDefinition(Recipe recipe,IList<Recipe> definitions,
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
    private static bool RestoreDefinition(Recipe recipe, IList<Recipe> definitions)
    {
        Recipe source;int batches,tier;
        if(!TryResolveDefinition(recipe,definitions,out source,out batches,out tier))return false;
        // Keep serialized totals, timing and full paid ingredient fragments exactly as loaded.
        recipe.craftingToolType = source.craftingToolType;
        recipe.UseIngredientModifier = false;
        recipe.craftingTier = tier;
        recipe.tags = source.tags;
        recipe.Effects = source.Effects;
        recipe.tooltip = source.tooltip;
        recipe.unlockExpGain = source.unlockExpGain;
        recipe.IsLearnable = source.IsLearnable;
        recipe.IsTrackable = source.IsTrackable;
        recipe.wildcardForgeCategory = source.wildcardForgeCategory;
        recipe.wildcardCampfireCategory = source.wildcardCampfireCategory;
        recipe.isQuest = source.isQuest;
        recipe.isChallenge = source.isChallenge;
        recipe.IsTracked = source.IsTracked;
        return true;
    }

    /// <summary>
    /// Prepares paired detached queue/payment images from one admitted plan. No live mutation.
    /// Caller must still authenticate owner/station and persist its job before committing either.
    /// </summary>
    public static bool TryPrepare(RebirthStationGridIngredients.Plan plan,IList<ItemStack> current,
        float oneBatchSeconds,IList<Recipe> definitions,out Recipe queued,out ItemStack[] remainder,
        out DefinitionBinding binding)
    {
        queued=null;remainder=null;binding=null;
        // Resolve the current catalogue and serialization bounds before exposing a debit image.
        if(!RebirthStationGridIngredients.TryBuildQueueRecipe(plan,oneBatchSeconds,out var candidate)||
            !TryGetDefinitionBinding(candidate,definitions,out var resolved)||
            !RebirthStationGridIngredients.TryBuildDebitedGrid(plan,current,out var after)||
            !ConservesPayment(current,after,candidate))return false;
        queued=candidate;remainder=after;binding=resolved;return true;
    }
    // Validates detached payment images, not ownership or permission to enqueue.
    // The allocator emits one fragment for each debited slot in ascending slot order.
    public static bool ConservesPayment(IList<ItemStack> before,IList<ItemStack> after,Recipe queued)
    {
        if(before==null||after==null||before.Count!=after.Count||before.Count>9||
            queued?.ingredients==null||queued.ingredients.Count>9)return false;
        int fragment=0;
        for(int i=0;i<before.Count;i++)
        {
            var source=before[i];var remaining=after[i];
            if(RebirthStationGridIngredients.IsSameStackSnapshot(source,remaining))continue;
            if(source?.itemValue==null||source.count<1||source.count>ushort.MaxValue||source.itemValue.IsEmpty()||
                remaining?.itemValue==null||remaining.count<0||remaining.count>=source.count)return false;
            if(remaining.count==0)
            {if(!remaining.itemValue.IsEmpty())return false;}
            else
            {
                var comparable=remaining.Clone();comparable.count=source.count;
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(source,comparable))return false;
            }
            if(fragment>=queued.ingredients.Count)return false;
            var removed=source.Clone();removed.count=source.count-remaining.count;
            var refund=RefundSnapshot(queued.ingredients[fragment++]);
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(removed,refund))return false;
        }
        return fragment==queued.ingredients.Count;
    }
    public static ItemStack RefundSnapshot(ItemStack paid)
    {
        if (paid == null) return null;
        ItemStack refund = paid.Clone();
        if (refund.itemValue?.Metadata != null)
        {
            var keys = new List<string>(refund.itemValue.Metadata.Keys);
            foreach (string key in keys) if (key.StartsWith(Prefix, StringComparison.Ordinal)) refund.itemValue.Metadata.Remove(key);
        }
        return refund;
    }
}