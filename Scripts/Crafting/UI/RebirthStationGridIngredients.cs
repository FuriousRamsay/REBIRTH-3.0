using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.ObjectModel;

#nullable disable

/// <summary>Detached allocation for the shared station grid. No inventory or recipe mutation.</summary>
public static class RebirthStationGridIngredients
{
    public sealed class Allocation
    {
        public int Slot { get; private set; }
        public int Count { get; private set; }
        private readonly ItemStack payload;
        internal Allocation(int slot, ItemStack source, int count)
        { Slot = slot; Count = count; payload = source.Clone(); payload.count = count; }
        public ItemStack Snapshot() => payload.Clone();
    }

    public sealed class Plan
    {
        public int Batches { get; private set; }
        public int CraftingTier { get; private set; }
        public ReadOnlyCollection<Allocation> Inputs { get; private set; }
        private readonly Recipe recipe;
        private readonly ItemStack[] admittedGrid;
        internal Plan(Recipe source, int batches, int tier, List<Allocation> inputs, IList<ItemStack> grid)
        {
            recipe = CopyRecipe(source); Batches = batches; CraftingTier = tier; Inputs = inputs.AsReadOnly();
            admittedGrid = new ItemStack[grid.Count];
            for (int i=0;i<grid.Count;i++) admittedGrid[i]=grid[i]?.Clone();
        }
        internal bool MatchesGrid(IList<ItemStack> grid)
        {
            if(grid==null||grid.Count!=admittedGrid.Length)return false;
            for(int i=0;i<grid.Count;i++) if(!SameStack(admittedGrid[i],grid[i]))return false;
            return true;
        }
        internal Recipe RecipeSnapshot() => CopyRecipe(recipe);
    }

    // Compare full native item serialization, not type/stackability: quality, use state,
    // modifications and metadata must all still be the admitted payload at payment time.
    public static bool IsSameStackSnapshot(ItemStack current,ItemStack admitted)
    {
        try{return SameStack(current,admitted);}catch{return false;}
    }
    private static bool SameStack(ItemStack a, ItemStack b)
    {
        if(a==null||b==null)return a==null&&b==null;
        if(a.count!=b.count)return false;
        if(a.itemValue==null||b.itemValue==null)return a.itemValue==null&&b.itemValue==null;
        using(var left=new MemoryStream()) using(var right=new MemoryStream())
        {
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(left);a.itemValue.Write(writer);writer.Flush();}
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(right);b.itemValue.Write(writer);writer.Flush();}
            if(left.Length!=right.Length)return false;
            byte[] x=left.GetBuffer(),y=right.GetBuffer();
            for(int i=0;i<left.Length;i++)if(x[i]!=y[i])return false;
            return true;
        }
    }

    /// <summary>Detached exact post-payment grid. Caller commits it only with successful paid-job admission.</summary>
    public static bool TryBuildDebitedGrid(Plan plan, IList<ItemStack> current, out ItemStack[] remainder)
    {
        remainder=null;
        if(plan==null||!plan.MatchesGrid(current))return false;
        var next=new ItemStack[current.Count];
        for(int i=0;i<current.Count;i++)next[i]=current[i]?.Clone();
        foreach(Allocation input in plan.Inputs)
        {
            ItemStack stack=next[input.Slot];
            if(stack==null||stack.count<input.Count)return false;
            stack.count-=input.Count;
            if(stack.count==0)next[input.Slot]=ItemStack.Empty;
        }
        remainder=next;
        return true;
    }

    private static Recipe CopyRecipe(Recipe source)
    {
        var copy = new Recipe {
            Version=source.Version, itemValueType=source.itemValueType, count=source.count,
            wildcardForgeCategory=source.wildcardForgeCategory, wildcardCampfireCategory=source.wildcardCampfireCategory,
            materialBasedRecipe=source.materialBasedRecipe, craftingToolType=source.craftingToolType,
            craftingTime=source.craftingTime, craftingArea=source.craftingArea, tooltip=source.tooltip,
            unlockExpGain=source.unlockExpGain, craftExpGain=source.craftExpGain,
            UseIngredientModifier=source.UseIngredientModifier, tags=source.tags,
            IsTrackable=source.IsTrackable, isQuest=source.isQuest, isChallenge=source.isChallenge,
            IsTracked=source.IsTracked, IsScrap=source.IsScrap, craftingTier=source.craftingTier,
            Effects=source.Effects, IsLearnable=source.IsLearnable
        };
        foreach (ItemStack ingredient in source.ingredients) copy.ingredients.Add(ingredient.Clone());
        return copy;
    }

    /// <summary>
    /// One atomic batch completion, queued with native Multiplier=1. Splitting total input
    /// fragments by portion would lose provenance when differing slot counts are indivisible.
    /// Native output quality must be supplied separately as plan.CraftingTier at enqueue time.
    /// </summary>
    public static bool TryBuildQueueRecipe(Plan plan, float oneBatchSeconds, out Recipe recipe)
    {
        recipe = null;
        if (plan == null || float.IsNaN(oneBatchSeconds) || float.IsInfinity(oneBatchSeconds)
            || oneBatchSeconds < 0f) return false;
        Recipe concrete = plan.RecipeSnapshot();
        long outputs = (long)concrete.count * plan.Batches;
        long experience = (long)concrete.craftExpGain * plan.Batches;
        double seconds = (double)oneBatchSeconds * plan.Batches;
        if (concrete.itemValueType <= 0 || outputs < 1 || outputs > 32767
            || experience < 0 || experience > int.MaxValue || seconds > float.MaxValue) return false;
        concrete.count = (int)outputs;
        concrete.craftExpGain = (int)experience;
        concrete.craftingTime = (float)seconds;
        concrete.craftingTier = plan.CraftingTier;
        concrete.UseIngredientModifier = false; // Native modifiers were already resolved at admission.
        concrete.ingredients.Clear();
        foreach (Allocation input in plan.Inputs) concrete.ingredients.Add(input.Snapshot());
        if (concrete.ingredients.Count > 0 && !RebirthStationGridQueue.Stamp(concrete,plan.RecipeSnapshot(),plan.Batches,plan.CraftingTier)) return false;
        recipe = concrete;
        return true;
    }

    // Native filtering retains workstation aliases, biome/tech limits, smelter options and
    // WorkstationCrafting. The caller retains specialized material-input panels for forge recipes.
    public static List<Recipe> Candidates(string station, IList<Recipe> recipes)
        => XUiM_Recipes.FilterRecipesByWorkstation(station, recipes);

    public static bool TryPlan(EntityPlayer player, Recipe recipe, IList<ItemStack> grid,
        int batches, int craftingTier, out Plan plan)
    {
        plan = null;
        if (recipe == null || recipe.ingredients == null || grid == null || grid.Count > 9
            || batches < 1 || batches > 9999 || craftingTier < 0 || craftingTier > 6
            || recipe.IsScrap || recipe.materialBasedRecipe) return false;

        var required = new Dictionary<int, int>();
        foreach (ItemStack ingredient in recipe.ingredients)
        {
            if (!RebirthCraftingIngredientQuantity.TryResolveTotal(player, recipe, ingredient,
                craftingTier, batches, out int count)) return false;
            // Native zero-cost ingredient effects do not require occupying a grid slot.
            if (count == 0) continue;
            int type = ingredient.itemValue.type;
            required.TryGetValue(type, out int prior);
            long sum = (long)prior + count;
            if (sum > int.MaxValue) return false;
            required[type] = (int)sum;
        }

        var remaining = new Dictionary<int, int>(required);
        var inputs = new List<Allocation>();
        for (int slot = 0; slot < grid.Count; slot++)
        {
            ItemStack stack = grid[slot];
            if (stack == null) continue;
            // Native ItemStack.Write stores an unsigned 16-bit count. Reject malformed or
            // non-roundtrippable input before IsEmpty (which dereferences itemValue).
            if (stack.itemValue == null || stack.count < 0 || stack.count > ushort.MaxValue) return false;
            if (stack.count > 0 && stack.itemValue.IsEmpty()) return false;
            if (stack.IsEmpty()) continue;
            // Match native Recipe.CanCraft: installed modifications make an input ineligible.
            // Exact metadata is retained in the allocation for cancellation/refund and output work.
            if (stack.count <= 0 || stack.itemValue == null || stack.itemValue.IsEmpty()
                || stack.itemValue.HasModSlots && stack.itemValue.HasMods()
                || !required.ContainsKey(stack.itemValue.type)) return false;
            int needed = remaining[stack.itemValue.type];
            int take = Math.Min(needed, stack.count);
            if (take > 0) inputs.Add(new Allocation(slot, stack, take));
            remaining[stack.itemValue.type] = needed - take;
        }
        foreach (int count in remaining.Values) if (count != 0) return false;
        plan = new Plan(recipe, batches, craftingTier, inputs, grid);
        return true;
    }
}