using System;
using System.Collections.Generic;
using System.Globalization;

// The displayed total includes only complete, sellable bundles, quoted by the native trader.
internal static class RebirthBackpackSaleQuote
{
    internal static int TradableCount(ItemStack stack) => TradableCount(stack, out _);

    private static int TradableCount(ItemStack stack, out int bundle)
    {
        bundle = 1;
        var item = stack?.itemValue?.ItemClass;
        if (item == null || stack.IsEmpty()) return 0;

        bool isBlock = item.IsBlock();
        int type = stack.itemValue.type;
        if (!(isBlock ? Block.list[type].SellableToTrader : item.SellableToTrader)) return 0;

        bundle = Math.Max(1, isBlock ? Block.list[type].EconomicBundleSize : item.EconomicBundleSize);
        return stack.count / bundle * bundle;
    }

    internal static string Format(XUi ui, RebirthBackpackSellStashView view)
    {
        if (view == null) return "—";

        var trader = ui?.Trader?.TraderData;
        Dictionary<int, int> remainingByType = null;
        long total = 0;
        for (int i = 0; i < view.Capacity; i++)
        {
            if (!view.TryGetSlot(i, out var stack)) continue;
            int count = TradableCount(stack, out int bundle);
            if (count <= 0) continue;

            var value = stack.itemValue;
            int limit = value.ItemClass.MaxCount * TraderInfo.TraderBuyLimit;
            if (trader != null && limit > 0)
            {
                // Native primary stock counts by type. Keep the cap local to
                // this quote while pricing each exact item and bundle separately.
                remainingByType ??= new Dictionary<int, int>();
                if (!remainingByType.TryGetValue(value.type, out int remaining))
                    remaining = Math.Max(0, limit - trader.GetPrimaryItemCount(value));
                count = Math.Min(count, remaining) / bundle * bundle;
                remainingByType[value.type] = remaining - count;
                if (count <= 0) continue;
            }

            // TryGetSlot detached this stack; quote count changes cannot mutate the view.
            stack.count = count;
            if (!RebirthItemSaleEstimate.TryGet(ui, stack, out int price)) return "—";
            total += Math.Max(0, price);
        }
        return total.ToString("N0", CultureInfo.InvariantCulture) + " $";
    }
}
