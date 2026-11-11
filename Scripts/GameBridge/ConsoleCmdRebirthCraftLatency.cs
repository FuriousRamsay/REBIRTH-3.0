#if REBIRTH_DEBUG
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

// An opt-in timing probe for a single native Craft gesture. Full craft tracing
// snapshots inventory and queues repeatedly, which can distort small timings.
public sealed class ConsoleCmdRebirthCraftLatency : ConsoleCmdAbstract
{
    private static readonly Harmony Harmony = new Harmony("rebirth.bridge.craft-latency");
    private static MethodInfo clickMethod;
    private static MethodInfo queueMethod;
    private static XUi ownerUi;
    private static int outputType;
    private static long expiresAt;
    private static long clickedAt;

    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;
    public override string[] getCommands() => new[] { "rbcraftlatency" };
    public override string getDescription() => "Measure one Craft gesture to its matching native queue row.";
    public override string getHelp() => "rbcraftlatency arm <output item name> [native] | off";

    public override void Execute(List<string> args, CommandSenderInfo senderInfo)
    {
        Stop();
        if (args.Count == 1 && args[0] == "off") return;
        bool native = args.Count == 3 && args[2] == "native";
        if ((args.Count != 2 && !native) || args[0] != "arm")
        {
            Log.Out(getHelp());
            return;
        }

        var item = ItemClass.GetItem(args[1], false);
        ownerUi = LocalPlayerUI.GetUIForPrimaryPlayer()?.xui;
        if (ownerUi == null || item == null || item.IsEmpty()) return;
        outputType = item.type;
        clickMethod = native
            ? AccessTools.Method(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.OnActivated))
            : AccessTools.Method(typeof(XUiC_RebirthCraftingActions), "Craft_OnPress");
        queueMethod = AccessTools.Method(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.SetRecipe),
            new[] { typeof(Recipe), typeof(int), typeof(float), typeof(bool), typeof(int), typeof(int), typeof(float) });
        if (clickMethod == null || queueMethod == null)
        {
            Stop();
            return;
        }

        try
        {
            var start = new HarmonyMethod(typeof(ConsoleCmdRebirthCraftLatency),
                native ? nameof(OnNativeActivation) : nameof(OnClick)) { priority = Priority.First };
            Harmony.Patch(clickMethod, prefix: start);
            Harmony.Patch(queueMethod, postfix: new HarmonyMethod(typeof(ConsoleCmdRebirthCraftLatency), nameof(OnQueue)));
            expiresAt = Stopwatch.GetTimestamp() + 35 * Stopwatch.Frequency;
            Log.Out("[REBIRTH CraftLatency] armed item=" + args[1]);
        }
        catch
        {
            Stop();
            throw;
        }
    }

    private static void OnClick(XUiC_RebirthCraftingActions __instance) => RecordStart(__instance.xui);

    private static void OnNativeActivation(ItemActionEntryCraft __instance)
    {
        if (__instance.ItemController is XUiC_RecipeEntry entry && entry.Recipe?.itemValueType == outputType)
            RecordStart(entry.xui);
    }

    private static void RecordStart(XUi ui)
    {
        long now = Stopwatch.GetTimestamp();
        if (clickedAt == 0 && ReferenceEquals(ui, ownerUi) && now <= expiresAt)
            clickedAt = now;
    }

    private static void OnQueue(XUiC_RecipeStack __instance, Recipe _recipe, int _count, bool __result)
    {
        if (clickedAt == 0 || !__result || _recipe?.itemValueType != outputType || _count < 1 ||
            !ReferenceEquals(__instance.xui, ownerUi) || !ReferenceEquals(__instance.GetRecipe(), _recipe)) return;
        long end = Stopwatch.GetTimestamp();
        if (end > expiresAt) return;
        double elapsedMs = (end - clickedAt) * 1000.0 / Stopwatch.Frequency;
        clickedAt = 0;
        expiresAt = 0;
        Log.Out("[REBIRTH CraftLatency] queue item=" + _recipe.GetName() +
            " elapsedMs=" + elapsedMs.ToString("F3", CultureInfo.InvariantCulture));
    }

    private static void Stop()
    {
        expiresAt = clickedAt = 0;
        ownerUi = null;
        if (clickMethod != null) Harmony.Unpatch(clickMethod, HarmonyPatchType.All, Harmony.Id);
        if (queueMethod != null) Harmony.Unpatch(queueMethod, HarmonyPatchType.All, Harmony.Id);
        clickMethod = queueMethod = null;
    }
}
#endif
