// Inventory-independent writeback plan. All ItemValues are cloned: planning must
// not alter a live container before its source transaction accepts the write.
internal sealed class RebirthDrinkSourcePlan
{
    internal ItemStack ConsumedSource { get; private set; }
    internal ItemStack Replacement { get; private set; }
    internal ItemStack UntouchedSiblings { get; private set; }
    internal float RemainingMl { get; private set; }
    internal bool CreatedEmptyItem { get; private set; }

    internal static RebirthDrinkSourcePlan Create(ItemStack source,
        RebirthConsumableDefinition definition, float remainingMl)
    {
        if (source == null || source.IsEmpty() || source.itemValue == null || definition == null)
            throw new System.ArgumentException("A valid drink source and definition are required.");
        var plan = new RebirthDrinkSourcePlan();
        plan.ConsumedSource = new ItemStack(source.itemValue.Clone(), 1);
        plan.UntouchedSiblings = source.count > 1
            ? new ItemStack(source.itemValue.Clone(), source.count - 1) : null;
        plan.RemainingMl = remainingMl <= 0.5f ? 0f : remainingMl;
        if (plan.RemainingMl > 0f || definition.ReusableContainer)
        {
            plan.Replacement = plan.ConsumedSource.Clone();
            RebirthLiquidContainerService.SetRemainingMl(plan.Replacement.itemValue, definition, plan.RemainingMl);
        }
        else
        {
            plan.Replacement = string.IsNullOrEmpty(definition.EmptyItem)
                ? ItemStack.Empty.Clone() : new ItemStack(ItemClass.GetItem(definition.EmptyItem), 1);
            plan.CreatedEmptyItem = !plan.Replacement.IsEmpty();
        }
        return plan;
    }
}
