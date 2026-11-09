using System.Collections.Generic;
using System.Runtime.CompilerServices;

#nullable disable

/// <summary>
/// The workstation window groups of one XUi, found once. Profiler 2026-11-08: the recycler searched the whole
/// UI tree for them (GetChildrenByType, a new list each time) before and after every processing step, which
/// cost 47-122 ms dips while recycling. Window groups are created when the XUi loads and live as long as it.
/// </summary>
internal static class RebirthWorkstationWindowIndex
{
    private static readonly ConditionalWeakTable<XUi, XUiC_WorkstationWindowGroup[]> cache =
        new ConditionalWeakTable<XUi, XUiC_WorkstationWindowGroup[]>();

    internal static XUiC_WorkstationWindowGroup[] For(XUi xui)
    {
        if (xui == null) return System.Array.Empty<XUiC_WorkstationWindowGroup>();
        if (cache.TryGetValue(xui, out var windows) && windows.Length > 0 && Valid(windows, xui)) return windows;
        cache.Remove(xui);
        windows = xui.GetChildrenByType<XUiC_WorkstationWindowGroup>()?.ToArray() ?? System.Array.Empty<XUiC_WorkstationWindowGroup>();
        if (windows.Length > 0) cache.Add(xui, windows);
        return windows;
    }

    private static bool Valid(XUiC_WorkstationWindowGroup[] windows, XUi xui)
    {
        for (int i = 0; i < windows.Length; i++)
            if (windows[i] == null || !ReferenceEquals(windows[i].xui, xui)) return false;
        return true;
    }
}

/// <summary>The crafting queue of a workstation window, found once per window (it is fixed after XUi load).</summary>
internal static class RebirthWorkstationQueueIndex
{
    private static readonly ConditionalWeakTable<XUiC_WorkstationWindowGroup, XUiC_CraftingQueue> cache =
        new ConditionalWeakTable<XUiC_WorkstationWindowGroup, XUiC_CraftingQueue>();
    internal static XUiC_CraftingQueue For(XUiC_WorkstationWindowGroup window)
    {
        if (window == null) return null;
        if (cache.TryGetValue(window, out var queue) && queue != null && ReferenceEquals(queue.xui, window.xui)) return queue;
        cache.Remove(window);
        queue = window.GetChildByType<XUiC_CraftingQueue>();
        if (queue != null) cache.Add(window, queue);
        return queue;
    }
}
