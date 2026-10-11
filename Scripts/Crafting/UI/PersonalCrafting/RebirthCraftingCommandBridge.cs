using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Audio;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Bridges Rebirth-owned Crafting buttons to the verified native action objects.
/// The custom screen owns presentation only; crafting/favorite/tracking semantics stay native
/// so existing REBIRTH authority/remote-resource Harmony hooks remain on the real transaction path.
/// </summary>
[Preserve]
public sealed class RebirthCraftingCommandBridge
{
    private readonly RebirthCraftingPresentation owner;
    private readonly XUiC_RebirthCraftingRecipeCatalogue catalogue;
    private readonly XUiC_RecipeCraftCount craftCount;

    public RebirthCraftingCommandBridge(
        RebirthCraftingPresentation owner,
        XUiC_RebirthCraftingRecipeCatalogue catalogue,
        XUiC_RecipeCraftCount craftCount)
    {
        this.owner = owner;
        this.catalogue = catalogue;
        this.craftCount = craftCount;
    }

    private float nextAdmissionLog;
    private string lastAdmissionLog;
    private int admissionLogCount;

    private void LogAdmission(Recipe recipe, ItemActionEntryCraft action)
    {
        if (!RebirthLogSettings.CraftAdmissionLoggingEnabled || action == null ||
            Time.realtimeSinceStartup < nextAdmissionLog || admissionLogCount >= 120) return;
        nextAdmissionLog = Time.realtimeSinceStartup + 1f;
        try
        {
            var ui = owner?.xui;
            var player = ui?.playerUI?.entityPlayer;
            var entry = action.ItemController as XUiC_RecipeEntry;
            var nativeRecipe = entry?.Recipe;
            var tools = owner?.GetChildByType<XUiC_WorkstationToolGrid>();
            var fuel = owner?.GetChildByType<XUiC_WorkstationFuelGrid>();
            var held = player?.inventory?.GetHoldingPrimary();
            bool heldRunning = held != null && held.IsActionRunning(player.inventory.holdingItemData.actionData[0]);
            string line = "station=" + owner?.Workstation + " window=" + owner?.Controller?.ViewComponent?.ID +
                " recipe=" + recipe?.GetName() + " nativeRecipe=" + nativeRecipe?.GetName() +
                " sameRecipe=" + ReferenceEquals(recipe, nativeRecipe) + " area=" + nativeRecipe?.craftingArea +
                " rowStation=" + entry?.IsCurrentWorkstation + " enabled=" + action.Enabled + " state=" + action.state +
                " requestedTier=" + action.craftingTier + " allowedTier=" + (nativeRecipe?.GetCraftingTier(player) ?? -1) +
                " unlocked=" + (nativeRecipe != null && XUiM_Recipes.GetRecipeIsUnlocked(ui,nativeRecipe)) +
                " hasQuality=" + nativeRecipe?.GetOutputItemClass()?.HasQuality + " progression=" + XUiM_Recipes.CraftingProgression +
                " count=" + craftCount?.Count + " maxCount=" + craftCount?.MaxCount +
                " materials=" + (nativeRecipe != null && action.hasItems(ui,nativeRecipe)) +
                " ownerRequirements=" + (nativeRecipe != null && owner.CraftingRequirementsValid(nativeRecipe)) +
                " toolType=" + nativeRecipe?.craftingToolType + " tools=" + (nativeRecipe != null && (tools?.HasRequirement(nativeRecipe) ?? false)) +
                " fuel=" + (nativeRecipe != null && (fuel?.HasRequirement(nativeRecipe) ?? false)) +
                " heldRunning=" + heldRunning + " usingItem=" + ui?.IsUsingItemActionEntryUse +
                " nativeMessage=" + action.otherMessage;
            if (line == lastAdmissionLog) return;
            lastAdmissionLog = line;
            ++admissionLogCount;
            Log.Out("[REBIRTH CraftAdmission] " + line);
        }
        catch (Exception ex)
        {
            ++admissionLogCount;
            Log.Warning("[REBIRTH CraftAdmission] diagnostic failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    public string LastBlockReason { get; private set; } = string.Empty;

    public bool CanCraft(Recipe recipe, int craftingTier)
    {
        LastBlockReason = string.Empty;
        // Count is a requested number of crafts, not an ingredient multiplier that may be zero.
        // Native HasItems(..., 0) succeeds even with an empty bag; never use that as admission.
        if (craftCount == null || craftCount.Count <= 0)
        { LastBlockReason = Localization.Get(craftCount == null ? "xuiRebirthCraftMissingCount" : "xuiRebirthCraftZeroCount"); return false; }
        var queue=owner?.GetChildByType<XUiC_RebirthCraftingQueue>();
        if(queue!=null&&queue.RuntimeCapacity>0&&queue.ActiveCount>=queue.RuntimeCapacity)return false;
        var milling=owner?.Controller as XUiC_RebirthCookingStation;
        if(milling?.UsesSharedMillingPresentation==true)
            return milling.GetChildByType<XUiC_RebirthCookingWorkspace>().CanCraftShared(recipe,craftCount.Count);
        BaseItemActionEntry action = BuildCraft(recipe, craftingTier, false);
        if (action == null)
        { LastBlockReason = Localization.Get("xuiRebirthCraftMissingRow"); return false; }
        action.RefreshEnabled();
        LogAdmission(recipe, action as ItemActionEntryCraft);
        if (!action.Enabled && action is ItemActionEntryCraft native)
        {
            switch (native.state)
            {
                case ItemActionEntryCraft.StateTypes.WrongWorkStation:
                    LastBlockReason = Localization.Get("xuiRebirthCraftWrongStation"); break;
                case ItemActionEntryCraft.StateTypes.RecipeLocked:
                    LastBlockReason = Localization.Get("xuiRebirthCraftTierBlocked"); break;
                case ItemActionEntryCraft.StateTypes.NotEnoughMaterials:
                    LastBlockReason = Localization.Get("ttMissingCraftingResources"); break;
                default:
                    LastBlockReason = !string.IsNullOrWhiteSpace(native.otherMessage) ? native.otherMessage
                        : Localization.Get(owner?.xui?.IsUsingItemActionEntryUse == true
                            ? "xuiRebirthCraftItemUseBlocked" : "xuiRebirthCraftHeldActionBlocked");
                    break;
            }
        }
        return action.Enabled && craftCount.Count > 0;
    }

    public bool CanFavorite(Recipe recipe) => recipe != null;

    public bool CanTrack(Recipe recipe)
    {
        try { return recipe != null && recipe.IsTrackable; }
        catch { return false; }
    }

    public bool IsFavorite(Recipe recipe)
    {
        try { return recipe != null && CraftingManager.RecipeIsFavorite(recipe); }
        catch { return false; }
    }

    public bool IsTracked(Recipe recipe)
    {
        return recipe != null && owner != null && owner.xui != null && owner.xui.Recipes != null
            && owner.xui.Recipes.TrackedRecipe == recipe;
    }

    public void ExecuteCraft(Recipe recipe, int craftingTier)
    {
        // The button can have been drawn before the most recent inventory mutation. Recheck
        // the actual request on click, then let the native action recheck the current materials.
        // Do not normalize a zero request to one or alter an existing queue on rejection.
        if (recipe == null || craftCount == null || craftCount.Count <= 0)
        {
            RebirthPersonalCraftAdmission.RejectEmptyRequest(owner?.Personal, owner != null ? owner.xui : null);
            return;
        }
        var milling=owner?.Controller as XUiC_RebirthCookingStation;
        if(milling?.UsesSharedMillingPresentation==true)
        {
            milling.GetChildByType<XUiC_RebirthCookingWorkspace>().CraftShared(recipe,craftCount.Count);
            return;
        }
        BaseItemActionEntry action = BuildCraft(recipe, craftingTier, true);
        Execute(action);
    }

    public void ExecuteFavorite(Recipe recipe)
    {
        XUiC_RebirthCraftingRecipeEntry entry = ResolveEntry(recipe, true);
        if (entry == null || recipe == null) return;
        Execute(new ItemActionEntryFavorite(entry, recipe));
        catalogue?.NotifyExternalFavoriteChanged(recipe);
    }

    public void ExecuteTrack(Recipe recipe, int craftingTier)
    {
        XUiC_RebirthCraftingRecipeEntry entry = ResolveEntry(recipe, true);
        if (entry == null || recipe == null || !CanTrack(recipe)) return;
        Execute(new ItemActionEntryTrackRecipe(entry, craftingTier));
        catalogue?.NotifyExternalRecipeStateChanged();
    }

    private BaseItemActionEntry BuildCraft(Recipe recipe, int craftingTier, bool ensureVisible)
    {
        XUiC_RebirthCraftingRecipeEntry entry = ResolveEntry(recipe, ensureVisible);
        if (entry == null || recipe == null || craftCount == null) return null;
        // Virtual rows can have been bound before the workstation's native area was
        // initialized/refreshed. Rebind against the current area before native admission;
        // this recalculates IsCurrentWorkstation without bypassing any craft checks.
        if (!entry.IsCurrentWorkstation) entry.Recipe = recipe;
        // Non-quality outputs (for example buckshot) have native crafting tier 0.
        // The preview's minimum visible quality of 1 is not a valid requested tier
        // for those recipes: native RefreshEnabled compares it against GetCraftingTier.
        // Keep the player's selected quality unchanged for actual tiered equipment.
        int nativeTier = recipe.GetOutputItemClass()?.HasQuality == false ? 0 : craftingTier;
        return new ItemActionEntryCraft(entry, craftCount, nativeTier);
    }

    private XUiC_RebirthCraftingRecipeEntry ResolveEntry(Recipe recipe, bool ensureVisible)
    {
        if (catalogue == null || recipe == null) return null;
        return catalogue.GetBehaviorEntryForRecipe(recipe, ensureVisible);
    }

    private static void Execute(BaseItemActionEntry action)
    {
        if (action == null) return;
        action.RefreshEnabled();
        if (action.Enabled)
        {
            Manager.PlayInsidePlayerHead(action.SoundName);
            action.OnActivated();
        }
        else
        {
            Manager.PlayInsidePlayerHead(action.DisabledSound);
            action.OnDisabledActivate();
        }
    }
}

/// <summary>
/// Fix38: admission guards only. Saved queue restoration, CopyTo, ClearRecipe, repairs and
/// inventory-full output retries are deliberately not intercepted here.
/// </summary>
internal static class RebirthPersonalCraftAdmission
{
    internal static XUiC_RebirthPersonalCrafting GetOwner(ItemActionEntryCraft action)
    {
        return action?.ItemController?.GetParentByType<XUiC_RebirthPersonalCrafting>();
    }

    internal static bool HasPositiveRequest(ItemActionEntryCraft action)
    {
        return action != null && action.craftCountControl != null && action.craftCountControl.Count > 0;
    }

    internal static bool IsValidNewJob(Recipe recipe, int count)
    {
        return recipe != null && count > 0 && recipe.count > 0;
    }

    internal static void RejectEmptyRequest(XUiC_RebirthPersonalCrafting owner, XUi ui)
    {
        // Only invalidate presentation here. Recalculating the count inside an inventory
        // callback could change the multiplier in an in-progress native removal transaction.
        owner?.GetChildByType<XUiC_RebirthCraftingRecipeDetails>()?.RequestAvailabilityRefresh();
        EntityPlayerLocal player = ui?.playerUI?.entityPlayer;
        if (player == null) return;
        Manager.PlayInsidePlayerHead("ui_denied");
        GameManager.ShowTooltip(player, Localization.Get("ttMissingCraftingResources"));
    }
}

[HarmonyPatch(typeof(ItemActionEntryCraft), "hasItems")]
internal static class RebirthPersonalCraftPositiveCountPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static bool Prefix(ItemActionEntryCraft __instance, ref bool __result)
    {
        if (RebirthPersonalCraftAdmission.GetOwner(__instance) == null ||
            RebirthPersonalCraftAdmission.HasPositiveRequest(__instance)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.OnActivated))]
internal static class RebirthPersonalCraftActivationCountPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static bool Prefix(ItemActionEntryCraft __instance)
    {
        XUiC_RebirthPersonalCrafting owner = RebirthPersonalCraftAdmission.GetOwner(__instance);
        if (owner == null || RebirthPersonalCraftAdmission.HasPositiveRequest(__instance)) return true;
        // Also covers direct/native hotkeys and an asynchronous authorization/resource replay.
        // Reject before a remote preflight may reserve any materials for this invalid request.
        RebirthPersonalCraftAdmission.RejectEmptyRequest(owner, __instance.ItemController.xui);
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_CraftingQueue), nameof(XUiC_CraftingQueue.AddRecipeToCraft))]
internal static class RebirthPersonalCraftQueueAdmissionPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static bool Prefix(XUiC_CraftingQueue __instance, Recipe _recipe, int _count, ref bool __result)
    {
        if (__instance.GetParentByType<XUiC_RebirthPersonalCrafting>() == null ||
            RebirthPersonalCraftAdmission.IsValidNewJob(_recipe, _count)) return true;
        // This is the NEW-job API, not AddRecipeToCraftAtIndex (saved/paid queue restore).
        // No slot is visited, replaced, cancelled or refunded for a rejected request.
        __result = false;
        return false;
    }
}

/// <summary>
/// Fix36: opt-in, read-only transaction trace. No hooks or event handlers are installed at
/// startup. The command installs only this trace's hooks and removes only its own hooks.
/// Do not enable the old Creative profiler to investigate crafting transactions.
/// </summary>
[Preserve]
public sealed class ConsoleCmdRebirthCraftTrace : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;
    public override string[] getCommands() => new[] { "rbcrafttrace" };
    public override string getDescription() => "Capture material checks and crafting queue mutations; off by default.";
    public override string getHelp() => "rbcrafttrace on | off | status | mark\n" +
        "On: trace crafting for up to 5 minutes. Reproduce once, then off and send the session log. " +
        "Mark records the queue as it appears now. No items, recipes or rewards are changed by the trace.";
    public override void Execute(List<string> args, CommandSenderInfo senderInfo)
    {
        string command = args != null && args.Count > 0 ? (args[0] ?? "").ToLowerInvariant() : "status";
        switch (command)
        {
            case "on": case "start": RebirthCraftQueueTrace.Start(); break;
            case "off": case "stop": RebirthCraftQueueTrace.Stop("owner stopped"); break;
            case "mark": RebirthCraftQueueTrace.Mark(); break;
            case "status": Log.Out(RebirthCraftQueueTrace.Status()); break;
            default: Log.Out(getHelp()); break;
        }
    }
}

internal static class RebirthCraftQueueTrace
{
    private const string OwnerId = "rebirth.crafting.transaction-trace.fix36";
    private const int MaxEvents = 6000;
    private const int MaxTextChars = 8 * 1024 * 1024;
    private const int MaxLineChars = 12000;
    private const int MaxStackTraces = 48;
    private const float MaxSeconds = 300f;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private static readonly Dictionary<MethodBase, Hook> Hooks = new Dictionary<MethodBase, Hook>();
    private static readonly List<MethodBase> Attached = new List<MethodBase>();
    private static readonly Dictionary<string, string> LastQueries = new Dictionary<string, string>();
    private static readonly Dictionary<string, int> SuppressedQueries = new Dictionary<string, int>();
    private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
    private static Harmony harmony;
    private static bool enabled, lifecycleInstalled, internalWork, detachPending;
    private static int mainThread, generation, eventCount, textChars, errors, stackTraces, missingRequired;
    private static long nextOperation;
    private static float started, nextPulse;
    private static string capture = "none", stopReason = "not started";
    private static World world;
    private static XUi sourceUi;
    private static Snapshot lastPoll;
    private static string lastStateKey;
    private static long stateNumber;
    [ThreadStatic] private static Scope activeScope;

    private sealed class Hook
    {
        public MethodInfo Method;
        public string Name;
        public ParameterInfo[] Parameters;
        public bool Query;
    }
    private sealed class Context
    {
        public XUi Ui;
        public EntityPlayer Player;
        public XUiC_CraftingQueue Queue;
        public XUiC_RecipeStack Entry;
        public ItemActionEntryCraft Action;
        public Recipe Recipe;
    }
    private sealed class Snapshot
    {
        public string Key;
        public string Text;
        public long QueuedCycles, QueuedItems;
        public readonly Dictionary<int, long> Counts = new Dictionary<int, long>();
    }
    // Harmony state belongs to one invocation; nested output/queue compaction keeps its parent.
    private sealed class Scope
    {
        public long Id;
        public int Generation;
        public Scope Parent;
        public Hook Hook;
        public Context Context;
        public Snapshot Before;
        public string ArgsBefore, ContextBefore;
        public long BeforeState;
        public object Instance;
        public object[] Arguments;
    }

    public static string Status()
    {
        return "[REBIRTH CraftTrace36] active=" + enabled + " capture=" + capture +
            " events=" + eventCount + " errors=" + errors + " remainingHooks=" + Attached.Count +
            " missingRequired=" + missingRequired + " reason=" + stopReason;
    }
    public static void Start()
    {
        if (enabled || Attached.Count != 0) Stop("restart requested");
        if (Attached.Count != 0) { SafeWarning("[REBIRTH CraftTrace36] Hooks remain; restart the game before another capture."); return; }
        World currentWorld = GameManager.Instance?.World;
        if (currentWorld == null) { SafeOut("[REBIRTH CraftTrace36] Load a game first."); return; }
        mainThread = Thread.CurrentThread.ManagedThreadId;
        world = currentWorld;
        sourceUi = LocalPlayerUI.GetUIForPrimaryPlayer()?.xui;
        generation++; eventCount = textChars = errors = stackTraces = missingRequired = 0;
        nextOperation = stateNumber = 0; lastStateKey = null; started = Time.realtimeSinceStartup; nextPulse = started;
        capture = Guid.NewGuid().ToString("N").Substring(0, 12);
        stopReason = "capturing"; lastPoll = null; activeScope = null;
        Hooks.Clear(); LastQueries.Clear(); SuppressedQueries.Clear(); Fields.Clear();
        try
        {
            EnsureLifecycle();
            harmony = harmony ?? new Harmony(OwnerId);
            SafeOut("[REBIRTH CraftTrace36] BEGIN capture=" + capture +
                " sourceRevision=Fix38+Trace36 limitSeconds=300 maxEvents=6000 maxTextChars=8388608" +
                " mode=observation-only utc=" + DateTime.UtcNow.ToString("o", Invariant));

            // These are cold mutation/transaction boundaries. No hot Update method uses __args.
            Attach(typeof(RebirthCraftingCommandBridge), "ExecuteCraft", true);
            Attach(typeof(XUiC_RebirthCraftingActions), "Craft_OnPress", true);
            Attach(typeof(XUiC_RebirthCraftingQueueEntry), "Cancel_OnPress", true);
            Attach(typeof(ItemActionEntryCraft), "OnActivated", true);
            Attach(typeof(ItemActionEntryCraft), "OnDisabledActivate", false);
            Attach(typeof(XUiC_CraftingWindowGroup), "AddItemToQueue", true);
            Attach(typeof(XUiC_CraftingQueue), "AddRecipeToCraft", true);
            Attach(typeof(XUiC_CraftingQueue), "ClearQueue", true);
            Attach(typeof(XUiC_CraftingQueue), "AddRecipeToCraftAtIndex", false);
            Attach(typeof(XUiC_CraftingQueue), "RefreshQueue", true);
            Attach(typeof(XUiC_RecipeStack), "SetRecipe", true);
            Attach(typeof(XUiC_RecipeStack), "ClearRecipe", true);
            Attach(typeof(XUiC_RecipeStack), "CopyTo", true);
            Attach(typeof(XUiC_RecipeStack), "ForceCancel", true);
            Attach(typeof(XUiC_RecipeStack), "HandleOnPress", true);
            Attach(typeof(XUiC_RecipeStack), "outputStack", true);
            Attach(typeof(XUiM_PlayerInventory), "RemoveItems", true);
            Attach(typeof(XUiM_PlayerInventory), "AddItem", false);
            Attach(typeof(XUiM_PlayerInventory), "AddItems", false);
            Attach(typeof(XUiC_WorkstationInputGrid), "RemoveItems", false);
            Attach(typeof(XUiC_RebirthPersonalCrafting), "OnOpen", false);
            Attach(typeof(XUiC_RebirthPersonalCrafting), "OnClose", false);

            Attach(typeof(RebirthPersonalCraftAuthorizationService), "AuthorizeActivation", true);
            Attach(typeof(RebirthPersonalCraftAuthorizationService), "Denied", false);
            Attach(typeof(RebirthPersonalCraftAuthorizationService), "Receive", false);
            Attach(typeof(RebirthPersonalCraftAuthorizationService), "ProcessServerRequest", false);
            Attach(typeof(RebirthPersonalCraftAuthorizationService), "Reply", false);
            Attach(typeof(RemoteResourceClientTransactionCoordinator), "BeforeCraftActivated", true);
            Attach(typeof(RemoteResourceClientTransactionCoordinator), "ReceiveResult", false);
            Attach(typeof(RemoteResourceClientTransactionCoordinator), "RefundRemoteToLocal", false);
            Attach(typeof(RemoteResourceClientTransactionCoordinator), "FailUi", false);
            Attach(typeof(RemoteResourceConsumerPatchInstaller), "BeforeRemoveItems", true);
            Attach(typeof(RemoteResourceTransactions), "TryConsume", true);
            Attach(typeof(RemoteResourceClientGrantContext), "ConsumeLocalRemainder", false);

            Attach(typeof(RebirthPersonalCraftCompletionService), "AfterOutput", true);
            Attach(typeof(RebirthPersonalCraftCompletionService), "Credit", true);
            Attach(typeof(RebirthPersonalCraftCompletionService), "Send", false);
            Attach(typeof(RebirthPersonalCraftCompletionService), "ReceiveServer", false);
            Attach(typeof(RebirthPersonalCraftCompletionService), "ReceiveAck", false);
            Attach(typeof(RebirthPersonalCraftCompletionService), "Ack", false);
            Attach(typeof(RebirthPersonalCraftCompletionService), "Reset", false);

            // Typed hot queries: do not log passive UI refreshes every frame. Record only real
            // query invocations made inside a traced transaction (never invoke them ourselves).
            AttachQuery(typeof(ItemActionEntryCraft), "RefreshEnabled", Type.EmptyTypes, typeof(void), nameof(BeforeEnabled));
            AttachQuery(typeof(ItemActionEntryCraft), "hasItems", new[] { typeof(XUi), typeof(Recipe) }, typeof(bool), nameof(BeforeActionItems));
            AttachQuery(typeof(XUiM_PlayerInventory), "HasItems", new[] { typeof(IList<ItemStack>), typeof(int) }, typeof(bool), nameof(BeforeInventoryItems));
            AttachQuery(typeof(XUiM_PlayerInventory), "GetItemCount", new[] { typeof(ItemValue) }, typeof(int), nameof(BeforeItemCount));
            AttachQuery(typeof(XUiM_PlayerInventory), "GetAllItemStacks", Type.EmptyTypes, typeof(List<ItemStack>), nameof(BeforeAllItems));
            AttachQuery(typeof(RemoteResourceTransactions), "HasLocalItems", new[] { typeof(EntityPlayer), typeof(IList<ItemStack>), typeof(int) }, typeof(bool), nameof(BeforeLocalAvailability));
            AttachQuery(typeof(RemoteResourceTransactions), "HasItems", new[] { typeof(EntityPlayer), typeof(IList<ItemStack>), typeof(int), typeof(bool) }, typeof(bool), nameof(BeforeCombinedAvailability));

            enabled = true;
            Context context = Resolve(null, null, null);
            lastPoll = Take(context);
            Write("BASELINE", 0, 0, "physical counts exclude remote availability; original query results below include their actual modifiers. state=" + RecordState(lastPoll));
            SafeOut("[REBIRTH CraftTrace36] " + (missingRequired == 0 ? "READY" : "PARTIAL (missing targets; see HOOK lines)") +
                " capture=" + capture + " hooks=" + Attached.Count + ". Reproduce once, then rbcrafttrace off. Auto-stop in 5 minutes.");
        }
        catch (Exception ex)
        {
            errors++;
            SafeWarning("[REBIRTH CraftTrace36] Setup failed: " + Clean(ex.ToString()));
            Stop("setup failed");
        }
    }
    private static void EnsureLifecycle()
    {
        if (lifecycleInstalled) return;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Pulse));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(WorldStopping));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(GameStopping));
        lifecycleInstalled = true;
    }
    private static void WorldStopping(ref ModEvents.SWorldShuttingDownData data) { if (enabled || Attached.Count != 0) Stop("world shutdown"); }
    private static void GameStopping(ref ModEvents.SGameShutdownData data) { if (enabled || Attached.Count != 0) Stop("game shutdown"); }
    private static void Pulse(ref ModEvents.SGameUpdateData data)
    {
        if (!enabled)
        {
            if (detachPending && activeScope == null) FinishDetach();
            return;
        }
        if (Thread.CurrentThread.ManagedThreadId != mainThread || internalWork) return;
        try
        {
            if (!ReferenceEquals(world, GameManager.Instance?.World)) { Stop("world changed"); return; }
            if (Time.realtimeSinceStartup - started >= MaxSeconds) { Stop("5-minute limit"); return; }
            if (Time.realtimeSinceStartup < nextPulse) return;
            nextPulse = Time.realtimeSinceStartup + 0.25f;
            internalWork = true;
            Snapshot now = Take(Resolve(null, null, null));
            if (lastPoll == null || now.Key != lastPoll.Key)
                Write("OBSERVED", 0, 0, Delta(lastPoll, now) + " state=" + RecordState(now));
            lastPoll = now;
        }
        catch (Exception ex) { RecorderError(ex); }
        finally { internalWork = false; }
    }
    public static void Mark()
    {
        if (!enabled) { SafeOut(Status()); return; }
        try
        {
            internalWork = true;
            Snapshot now = Take(Resolve(null, null, null));
            Write("OWNER-MARK", 0, 0, Delta(lastPoll, now) + " state=" + RecordState(now)); lastPoll = now;
        }
        catch (Exception ex) { RecorderError(ex); }
        finally { internalWork = false; }
    }
    public static void Stop(string reason)
    {
        if (!enabled && Attached.Count == 0) { SafeOut(Status()); return; }
        if (enabled && Thread.CurrentThread.ManagedThreadId == mainThread)
        {
            try { internalWork = true; Write("FINAL-STATE", 0, 0, "state=" + RecordState(Take(Resolve(null, null, null)))); }
            catch (Exception ex) { RecorderError(ex); }
            finally { internalWork = false; }
        }
        enabled = false; stopReason = reason; detachPending = true;
        // A limit reached inside a native hook is detached on the next game event, not midway
        // through the method's Harmony wrapper. Exceptions/results are always left untouched.
        if (activeScope == null) FinishDetach();
    }
    private static void FinishDetach()
    {
        bool prior = internalWork; internalWork = true;
        try
        {
            for (int i = Attached.Count - 1; i >= 0; --i)
            {
                try
                {
                    harmony.Unpatch(Attached[i], HarmonyPatchType.All, OwnerId);
                    if (OwnHookCount(Attached[i]) != 0)
                        throw new InvalidOperationException("trace hooks still present on " + Attached[i]);
                    Attached.RemoveAt(i);
                }
                catch (Exception ex) { errors++; SafeWarning("[REBIRTH CraftTrace36] Detach failed: " + Clean(ex.Message)); }
            }
            SafeOut(Status() + " chars=" + textChars + " destructiveStacks=" + stackTraces +
                " elapsedSeconds=" + F(Time.realtimeSinceStartup - started));
            Hooks.Clear(); Fields.Clear(); LastQueries.Clear(); SuppressedQueries.Clear();
            sourceUi = null; world = null; lastPoll = null;
            detachPending = false;
        }
        finally { internalWork = prior; }
    }

    private static void Attach(Type type, string name, bool required)
    {
        int found = 0;
        foreach (MethodInfo method in type.GetMethods(Declared))
        {
            if (method.Name != name || method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
            if (!SupportedReturn(method.ReturnType)) { HookStatus(type.Name + "." + name, "unsupported return=" + method.ReturnType); continue; }
            bool supported = true;
            foreach (ParameterInfo p in method.GetParameters())
                if (p.ParameterType.IsPointer) supported = false;
            if (!supported) continue;
            if (Install(method, method.IsStatic ? nameof(BeforeStatic) : nameof(BeforeInstance), false)) ++found;
        }
        if (found == 0)
        {
            if (required) missingRequired++;
            HookStatus(type.Name + "." + name, (required ? "MISSING-REQUIRED" : "unavailable-optional") + " (no compatible declared body attached)");
        }
    }
    private static void AttachQuery(Type type, string name, Type[] parameters, Type result, string prefix)
    {
        MethodInfo method = type.GetMethod(name, Declared, null, parameters, null);
        if (method == null || method.ReturnType != result || method.GetMethodBody() == null)
        { missingRequired++; HookStatus(type.Name + "." + name, "MISSING-QUERY (expected typed signature)"); return; }
        if (!Install(method, prefix, true)) missingRequired++;
    }
    private static bool SupportedReturn(Type t) => t == typeof(void) || t == typeof(bool) || t == typeof(int) || !t.IsValueType && !t.IsByRef && !t.IsPointer;
    private static bool Install(MethodInfo method, string prefix, bool query)
    {
        if (Hooks.ContainsKey(method)) return true;
        if (Attached.Count >= 80) { HookStatus(method.ToString(), "target limit"); return false; }
        string finalizer = method.ReturnType == typeof(void) ? nameof(FinishVoid) : method.ReturnType == typeof(bool)
            ? nameof(FinishBool) : method.ReturnType == typeof(int) ? nameof(FinishInt) : nameof(FinishObject);
        try
        {
            string previous = PatchList(method);
            var before = new HarmonyMethod(typeof(RebirthCraftQueueTrace), prefix) { priority = Priority.First + 100 };
            var after = new HarmonyMethod(typeof(RebirthCraftQueueTrace), finalizer) { priority = Priority.Last - 100 };
            Hooks.Add(method, new Hook { Method = method, Name = method.DeclaringType.Name + "." + method.Name,
                Parameters = method.GetParameters(), Query = query });
            // Track before applying, so even a partly failed patch attempt is included in cleanup.
            Attached.Add(method);
            harmony.Patch(method, prefix: before, finalizer: after);
            if (OwnHookCount(method) < 2)
                throw new InvalidOperationException("trace prefix/finalizer not both registered");
            HookStatus(method.DeclaringType.Name + "." + method.Name, "attached signature=" + method +
                " mvid=" + method.Module.ModuleVersionId + " prior=" + previous);
            return true;
        }
        catch (Exception ex)
        {
            Hooks.Remove(method);
            try
            {
                harmony.Unpatch(method, HarmonyPatchType.All, OwnerId);
                if (OwnHookCount(method) == 0) Attached.Remove(method);
            }
            catch { /* Retained in Attached; Stop retries only our owner ID. */ }
            HookStatus(method.DeclaringType.Name + "." + method.Name, "FAILED " + ex.GetType().Name + " " + ex.Message);
            return false;
        }
    }
    private static int OwnHookCount(MethodBase method)
    {
        Patches info = Harmony.GetPatchInfo(method);
        if (info == null) return 0;
        return OwnHookCount(info.Prefixes) + OwnHookCount(info.Postfixes) +
            OwnHookCount(info.Transpilers) + OwnHookCount(info.Finalizers);
    }
    private static int OwnHookCount(IEnumerable<Patch> patches)
    {
        int count = 0;
        foreach (Patch patch in patches) if (patch.owner == OwnerId) ++count;
        return count;
    }
    private static string PatchList(MethodBase method)
    {
        Patches info = Harmony.GetPatchInfo(method);
        if (info == null) return "none";
        var b = new StringBuilder();
        PatchList(b, "pre", info.Prefixes); PatchList(b, "post", info.Postfixes);
        PatchList(b, "il", info.Transpilers); PatchList(b, "final", info.Finalizers);
        return b.Length == 0 ? "none" : b.ToString();
    }
    private static void PatchList(StringBuilder b, string kind, IEnumerable<Patch> patches)
    {
        foreach (Patch patch in patches)
            b.Append(kind).Append(':').Append(patch.owner).Append('@').Append(patch.priority).Append(';');
    }
    private static void HookStatus(string method, string status) => SafeOut("[REBIRTH CraftTrace36] HOOK capture=" + capture + " " + Clean(method) + " " + Clean(status));

    // These have NO HarmonyPatch attributes. Only rbcrafttrace on attaches them.
    private static void BeforeInstance(object __instance, object[] __args, MethodBase __originalMethod, out Scope __state)
    { __state = Begin(__originalMethod, __instance, __args, false); }
    private static void BeforeStatic(object[] __args, MethodBase __originalMethod, out Scope __state)
    { __state = Begin(__originalMethod, null, __args, false); }
    private static void BeforeEnabled(ItemActionEntryCraft __instance, MethodBase __originalMethod, out Scope __state)
    { __state = CanQuery() ? Begin(__originalMethod, __instance, null, true) : null; }
    private static void BeforeActionItems(ItemActionEntryCraft __instance, XUi __0, Recipe __1, MethodBase __originalMethod, out Scope __state)
    { __state = CanQuery() ? Begin(__originalMethod, __instance, new object[] { __0, __1 }, true) : null; }
    private static void BeforeInventoryItems(XUiM_PlayerInventory __instance, IList<ItemStack> __0, int __1, MethodBase __originalMethod, out Scope __state)
    { __state = CanQuery() ? Begin(__originalMethod, __instance, new object[] { __0, __1 }, true) : null; }
    private static void BeforeItemCount(XUiM_PlayerInventory __instance, ItemValue __0, MethodBase __originalMethod, out Scope __state)
    { __state = CanQuery() ? Begin(__originalMethod, __instance, new object[] { __0 }, true) : null; }
    private static void BeforeAllItems(XUiM_PlayerInventory __instance, MethodBase __originalMethod, out Scope __state)
    { __state = CanQuery() ? Begin(__originalMethod, __instance, null, true) : null; }
    private static void BeforeLocalAvailability(EntityPlayer __0, IList<ItemStack> __1, int __2, MethodBase __originalMethod, out Scope __state)
    { __state = CanQuery() ? Begin(__originalMethod, null, new object[] { __0, __1, __2 }, true) : null; }
    private static void BeforeCombinedAvailability(EntityPlayer __0, IList<ItemStack> __1, int __2, bool __3, MethodBase __originalMethod, out Scope __state)
    { __state = CanQuery() ? Begin(__originalMethod, null, new object[] { __0, __1, __2, __3 }, true) : null; }
    private static bool CanQuery() => enabled && !internalWork && activeScope != null && Thread.CurrentThread.ManagedThreadId == mainThread;
    private static void FinishVoid(Scope __state, Exception __exception) { if (__state != null) End(__state, null, __exception); }
    private static void FinishBool(Scope __state, bool __result, Exception __exception) { if (__state != null) End(__state, __result, __exception); }
    private static void FinishInt(Scope __state, int __result, Exception __exception) { if (__state != null) End(__state, __result, __exception); }
    private static void FinishObject(Scope __state, object __result, Exception __exception) { if (__state != null) End(__state, __result, __exception); }

    private static Scope Begin(MethodBase method, object instance, object[] arguments, bool query)
    {
        if (!enabled || internalWork || Thread.CurrentThread.ManagedThreadId != mainThread) return null;
        Scope scope = null;
        try
        {
            internalWork = true;
            Hook hook;
            if (!Hooks.TryGetValue(method, out hook)) return null;
            Context context = Resolve(instance, arguments, activeScope?.Context, hook);
            // Inventory hooks should not trace unrelated looting/other players when no craft is in progress.
            if (activeScope == null && (instance is XUiM_PlayerInventory || instance is XUiC_WorkstationInputGrid)) return null;
            if (context.Player != null && sourceUi?.playerUI?.entityPlayer != null &&
                !ReferenceEquals(context.Player, sourceUi.playerUI.entityPlayer)) return null;
            scope = new Scope { Id = ++nextOperation, Generation = generation, Parent = activeScope,
                Hook = hook, Context = context, Before = Take(context), ArgsBefore = Arguments(hook, arguments), ContextBefore = ContextText(context),
                Instance = instance, Arguments = arguments };
            activeScope = scope;
            scope.BeforeState = RecordState(scope.Before);
            if (!query)
            {
                Write("ENTER", scope.Id, scope.Parent?.Id ?? 0, hook.Name + " instance=" + Ref(instance) +
                    " args=" + scope.ArgsBefore + " state=" + scope.BeforeState + " " + scope.ContextBefore);
                bool replacingWithEmpty = method.Name == "SetRecipe" && context.Entry?.GetRecipe() != null &&
                    arguments != null && arguments.Length > 0 && arguments[0] == null;
                if ((method.Name == "ClearQueue" || method.Name == "ForceCancel" || method.Name == "HandleOnPress" ||
                    method.Name == "ClearRecipe" && context.Entry?.GetRecipe() != null || replacingWithEmpty) && stackTraces < MaxStackTraces)
                {
                    stackTraces++;
                    Write("CALLER", scope.Id, scope.Parent?.Id ?? 0, hook.Name + " " + Caller());
                }
            }
            return scope;
        }
        catch (Exception ex)
        {
            if (scope != null && ReferenceEquals(activeScope, scope)) activeScope = scope.Parent;
            RecorderError(ex); return null;
        }
        finally { internalWork = false; }
    }
    private static void End(Scope scope, object result, Exception exception)
    {
        if (scope == null) return;
        try
        {
            if (!enabled || scope.Generation != generation) return;
            internalWork = true;
            Snapshot after = Take(scope.Context);
            string ending = Arguments(scope.Hook, scope.Arguments);
            long afterState = RecordState(after);
            string resultText = scope.Hook.Method.ReturnType == typeof(void) ? "void" : Value(result);
            string content = scope.Hook.Name + " finalResult=" + resultText +
                " args=" + scope.ArgsBefore + (ending != scope.ArgsBefore ? " argsAfter=" + ending : "") +
                " " + Delta(scope.Before, after) + " states=" + scope.BeforeState + "->" + afterState + " " + ContextText(scope.Context);
            if (scope.Hook.Query)
            {
                string key = scope.Hook.Name + "/" + Ref(scope.Instance) + "/" + scope.ArgsBefore;
                string old;
                // Queries inside successive clicks with unchanged inputs can repeat hundreds of times.
                // Keep changed decisions and the first occurrence per outer transaction; suppress duplicates.
                key += "/root=" + Root(scope).ToString(Invariant);
                if (!LastQueries.TryGetValue(key, out old) || old != content || exception != null)
                {
                    int repeats; SuppressedQueries.TryGetValue(key, out repeats);
                    Write("QUERY", scope.Id, scope.Parent?.Id ?? 0, content + " root=" + Root(scope) + " identicalSuppressed=" + repeats);
                    if (LastQueries.Count < 2048) { LastQueries[key] = content; SuppressedQueries[key] = 0; }
                }
                else { int repeats; SuppressedQueries.TryGetValue(key, out repeats); SuppressedQueries[key] = repeats + 1; }
            }
            else Write("EXIT", scope.Id, scope.Parent?.Id ?? 0, content);
            if (exception != null) Write("GAME-EXCEPTION", scope.Id, scope.Parent?.Id ?? 0, scope.Hook.Name + " " + exception);
        }
        catch (Exception ex) { RecorderError(ex); }
        finally
        {
            // Observer failure never changes native result/exception or leaves the logging scope active.
            if (scope.Generation == generation) activeScope = scope.Parent;
            internalWork = false;
        }
    }
    private static long Root(Scope scope) { while (scope.Parent != null) scope = scope.Parent; return scope.Id; }

    private static Context Resolve(object instance, object[] args, Context parent, Hook hook = null)
    {
        var c = new Context();
        ReadContext(c, instance);
        if (args != null) foreach (object a in args) ReadContext(c, a);
        if (c.Player == null && hook != null && args != null)
            for (int i = 0; i < hook.Parameters.Length && i < args.Length; i++)
                if (args[i] is int id && (hook.Parameters[i].Name == "playerEntityId" || hook.Parameters[i].Name == "playerId"))
                    c.Player = world?.GetEntity(id) as EntityPlayer;
        if (c.Ui == null) c.Ui = parent?.Ui;
        if (c.Player == null) c.Player = c.Ui?.playerUI?.entityPlayer ?? parent?.Player;
        if (c.Ui == null && c.Player == null) { c.Ui = sourceUi; c.Player = sourceUi?.playerUI?.entityPlayer; }
        if (c.Queue == null) c.Queue = (c.Entry?.Owner as XUiC_CraftingQueue) ?? parent?.Queue;
        if (c.Queue == null && c.Ui != null)
            c.Queue = c.Ui.FindWindowGroupByName("crafting")?.GetChildByType<XUiC_CraftingQueue>();
        if (c.Recipe == null) c.Recipe = c.Entry?.GetRecipe() ?? parent?.Recipe;
        if (c.Action == null) c.Action = parent?.Action;
        return c;
    }
    private static void ReadContext(Context c, object value)
    {
        if (value is ItemActionEntryCraft a)
        { c.Action = a; c.Ui = a.ItemController?.xui; c.Recipe = (a.ItemController as XUiC_RecipeEntry)?.Recipe; }
        else if (value is XUiC_RecipeStack entry)
        { if (c.Entry == null) { c.Entry = entry; c.Queue = entry.Owner as XUiC_CraftingQueue; c.Ui = entry.xui; c.Recipe = entry.GetRecipe(); } }
        else if (value is XUiC_CraftingQueue queue) { c.Queue = queue; c.Ui = queue.xui; }
        else if (value is XUiController control) c.Ui = control.xui;
        else if (value is XUi ui) c.Ui = ui;
        else if (value is XUiM_PlayerInventory inventory) c.Player = inventory.localPlayer;
        else if (value is EntityPlayer player) c.Player = player;
        else if (value is Recipe recipe) c.Recipe = recipe;
        else if (value is RebirthPersonalCraftCompletionService.Witness witness)
        { c.Player = witness.Player; c.Recipe = witness.Recipe; }
        else if (value != null && value.GetType().DeclaringType == typeof(RebirthPersonalCraftCompletionService))
        { if (ReadField(value, "Player") is EntityPlayer owner) c.Player = owner; }
    }
    private static Snapshot Take(Context c)
    {
        var s = new Snapshot();
        var structural = new StringBuilder();
        var details = new StringBuilder();
        structural.Append("player=").Append(c.Player?.entityId ?? -1).Append(" queue=").Append(Ref(c.Queue));
        XUiC_RecipeStack[] entries = c.Queue?.GetRecipesToCraft();
        structural.Append(" slots=").Append(entries?.Length ?? 0).Append(" rows=[");
        details.Append("timers=[");
        for (int i = 0; entries != null && i < entries.Length && i < 100; i++)
        {
            XUiC_RecipeStack e = entries[i]; Recipe r = e?.GetRecipe();
            if (r == null) continue;
            long count = e.GetRecipeCount();
            s.QueuedCycles += count; s.QueuedItems += count * Math.Max(1, r.count);
            structural.Append(i).Append(':').Append(Ref(e)).Append(':').Append(RecipeText(r)).Append(" cycles=").Append(count)
                .Append(" crafting=").Append(e.IsCrafting).Append(" full=").Append(e.isInventoryFull)
                .Append(" repair=").Append(e.AmountToRepair).Append(" visible=").Append(e.ViewComponent?.IsVisible == true)
                .Append(" enabled=").Append(e.ViewComponent?.Enabled == true).Append(';');
            details.Append(i).Append(':').Append(F(e.GetRecipeCraftingTimeLeft())).Append('/').Append(F(e.GetTotalRecipeCraftingTimeLeft())).Append(';');
        }
        structural.Append("] queuedCycles=").Append(s.QueuedCycles).Append(" queuedItems=").Append(s.QueuedItems);
        if (entries != null && entries.Length > 100) structural.Append(" QUEUE-TRUNCATED");
        details.Append(']');
        structural.Append(" bag=").Append(InventoryText(c.Player?.bag?.ItemGrid.items, s.Counts))
            .Append(" belt=").Append(InventoryText(c.Player?.inventory?.ItemGrid.items, s.Counts));
        if (c.Ui?.DragAndDropWindow != null) structural.Append(" held=").Append(StackText(c.Ui.DragAndDropWindow.CurrentStack));
        var owner = c.Ui?.FindWindowGroupByName("crafting") as XUiC_RebirthPersonalCrafting;
        structural.Append(" uiOpen=").Append(owner?.State.IsOpen == true).Append(" epoch=").Append(owner?.CraftIntentEpoch ?? -1);
        if (owner != null)
        {
            structural.Append(" countControl=").Append(owner.GetChildByType<XUiC_RecipeCraftCount>()?.Count ?? -1)
                .Append(" uiBatch=").Append(owner.State.BatchCount)
                .Append(" uiMaterialsKnown=").Append(owner.State.MaterialsStateKnown)
                .Append(" uiMaterialsSufficient=").Append(owner.State.MaterialsSufficient)
                .Append(" uiQueueEntries=").Append(owner.State.QueueActiveCount)
                .Append('/').Append(owner.State.QueueCapacity);
            var selected = owner.GetChildByType<XUiC_RebirthCraftingRecipeDetails>();
            structural.Append(" selected=").Append(RecipeText(selected?.SelectedRecipe))
                .Append(" tier=").Append(selected?.SelectedCraftingTier ?? -1);
        }
        structural.Append(" authorityPending=").Append(StaticCount(typeof(RebirthPersonalCraftAuthorizationService), "Pending"))
            .Append(" remotePending=").Append(StaticCount(typeof(RemoteResourceClientTransactionCoordinator), "pendingCraft"))
            .Append(" remoteGrantActive=").Append(RemoteResourceClientGrantContext.Active)
            .Append(" completionOutbox=").Append(StaticCount(typeof(RebirthPersonalCraftCompletionService), "Outbox"))
            .Append(" completionPeers=").Append(StaticCount(typeof(RebirthPersonalCraftCompletionService), "Peers"));
        s.Key = structural.ToString(); s.Text = s.Key + " " + details;
        return s;
    }
    private static string ContextText(Context c)
    {
        var b = new StringBuilder();
        if (c.Entry != null) b.Append("entry=").Append(EntryText(c.Entry, true));
        if (c.Action != null)
            b.Append(" action=").Append(Ref(c.Action)).Append(" enabled=").Append(c.Action.Enabled)
                .Append(" state=").Append(Convert.ToString(ReadField(c.Action, "state"), Invariant))
                .Append(" count=").Append(c.Action.craftCountControl?.Count ?? -1)
                .Append(" tier=").Append(Convert.ToString(ReadField(c.Action, "craftingTier"), Invariant))
                .Append(" tempIngredients=").Append(Ref(c.Action.tempIngredientList)).Append(Stacks(c.Action.tempIngredientList));
        return b.ToString();
    }
    private static long RecordState(Snapshot state)
    {
        if (lastStateKey == state.Key) return stateNumber;
        lastStateKey = state.Key; ++stateNumber;
        // A full snapshot is emitted only when its data/visibility changes. Transactions refer
        // to its number, so empty-cell queue shifts do not dump the same inventory 100 times.
        const int chunk = 10000;
        int parts = Math.Max(1, (state.Text.Length + chunk - 1) / chunk);
        for (int i = 0; i < parts && enabled; i++)
            Write("STATE", 0, 0, "state=" + stateNumber + " part=" + (i + 1) + "/" + parts + " " +
                state.Text.Substring(i * chunk, Math.Min(chunk, state.Text.Length - i * chunk)));
        return stateNumber;
    }
    private static string Delta(Snapshot before, Snapshot after)
    {
        if (before == null) return "delta=baseline";
        var b = new StringBuilder("queueCycleDelta=").Append(after.QueuedCycles - before.QueuedCycles)
            .Append(" queueItemDelta=").Append(after.QueuedItems - before.QueuedItems).Append(" carriedDelta=[");
        var keys = new SortedSet<int>(before.Counts.Keys); keys.UnionWith(after.Counts.Keys);
        foreach (int type in keys)
        {
            long a, z; before.Counts.TryGetValue(type, out a); after.Counts.TryGetValue(type, out z);
            if (a != z) b.Append(ItemName(type)).Append(':').Append(z - a).Append(';');
        }
        return b.Append(']').ToString();
    }
    private static string InventoryText(ItemStack[] slots, Dictionary<int, long> combined)
    {
        if (slots == null) return "unavailable";
        var local = new SortedDictionary<int, long>(); int used = 0;
        foreach (ItemStack stack in slots)
        {
            if (stack == null || stack.itemValue == null || stack.count < 1 || stack.itemValue.type == 0) continue;
            int type = stack.itemValue.type; long prior;
            local.TryGetValue(type, out prior); local[type] = prior + stack.count;
            combined.TryGetValue(type, out prior); combined[type] = prior + stack.count;
            used++;
        }
        var b = new StringBuilder().Append(used).Append('/').Append(slots.Length).Append('{');
        foreach (var pair in local) b.Append(ItemName(pair.Key)).Append(':').Append(pair.Value).Append(';');
        return b.Append('}').ToString();
    }
    private static string Arguments(Hook hook, object[] args)
    {
        if (args == null) return "[]";
        var b = new StringBuilder("[");
        for (int i = 0; i < args.Length; i++)
            b.Append(i < hook.Parameters.Length ? hook.Parameters[i].Name : "arg" + i)
                .Append('=').Append(Value(args[i])).Append(';');
        return b.Append(']').ToString();
    }
    private static string Value(object value)
    {
        if (value == null) return "null";
        if (value is string text) return "\"" + Clean(text, 384) + "\"";
        if (value is bool flag) return flag ? "true" : "false";
        if (value is float f) return F(f);
        if (value is double d) return d.ToString("0.###", Invariant);
        if (value is byte || value is int || value is long || value is uint || value is ulong || value is short || value is ushort || value.GetType().IsEnum)
            return Convert.ToString(value, Invariant);
        if (value is Recipe recipe) return RecipeText(recipe);
        if (value is ItemStack stack) return StackText(stack);
        if (value is ItemValue item) return ItemName(item.type) + " q=" + item.Quality + " uses=" + F(item.UseTimes);
        if (value is IList<ItemStack> stacks) return Stacks(stacks);
        if (value is XUiC_RecipeStack entry) return EntryText(entry, true);
        if (value is ItemActionEntryCraft action) return Ref(action) + " enabled=" + action.Enabled +
            " recipe=" + RecipeText((action.ItemController as XUiC_RecipeEntry)?.Recipe) + " count=" + (action.craftCountControl?.Count ?? -1);
        if (value is EntityPlayer player) return "player:" + player.entityId;
        if (value is RemoteResourceTransactions.ConsumptionResult consumed) return "consumed=" + consumed.Success +
            " reason=" + Clean(consumed.FailureReason) + " local=" + consumed.LocalItemsConsumed + " remote=" + consumed.RemoteItemsConsumed + " sources=" + consumed.SourcesChanged;
        if (value is RebirthPersonalCraftCompletionService.Witness witness) return "witness{player=" + (witness.Player?.entityId ?? -1) +
            " recipe=" + RecipeText(witness.Recipe) + " beforeOutput=" + witness.BeforeCount + " queueCount=" + witness.QueueCount +
            " serial=" + witness.Serial + " retry=" + witness.Retry + "}";
        if (value.GetType().DeclaringType == typeof(RebirthPersonalCraftCompletionService))
        {
            var b = new StringBuilder(Ref(value)).Append('{');
            foreach (string name in new[] { "Recipe", "Sequence", "Count", "Local", "Reserved", "BeforeCount", "QueueCount" })
                b.Append(name).Append('=').Append(Convert.ToString(ReadField(value, name), Invariant)).Append(';');
            return b.Append('}').ToString();
        }
        return Ref(value);
    }
    private static string Stacks(IList<ItemStack> stacks)
    {
        if (stacks == null) return "null";
        var b = new StringBuilder("[");
        for (int i = 0; i < stacks.Count && i < 64; i++) b.Append(StackText(stacks[i])).Append(';');
        if (stacks.Count > 64) b.Append("TRUNCATED total=").Append(stacks.Count);
        return b.Append(']').ToString();
    }
    private static string StackText(ItemStack stack) => stack == null ? "null" : stack.itemValue == null ? "missingItemValue*" + stack.count :
        ItemName(stack.itemValue.type) + "*" + stack.count + "{q=" + stack.itemValue.Quality + ",use=" + F(stack.itemValue.UseTimes) + "}";
    private static string EntryText(XUiC_RecipeStack e, bool timer) => Ref(e) + "{" + RecipeText(e.GetRecipe()) +
        " cycles=" + e.GetRecipeCount() + " crafting=" + e.IsCrafting + " full=" + e.isInventoryFull +
        " repair=" + e.AmountToRepair + (timer ? " left=" + F(e.GetRecipeCraftingTimeLeft()) : "") + "}";
    private static string RecipeText(Recipe recipe) => recipe == null ? "none" : Clean(recipe.GetName(), 160) +
        "#" + Ref(recipe) + " output=" + recipe.count + " area=" + Clean(recipe.craftingArea, 64) +
        " ingredients=" + Ref(recipe.ingredients) + Stacks(recipe.ingredients);
    private static string ItemName(int type) => Clean(ItemClass.GetForId(type)?.GetItemName() ?? "type" + type, 128);
    private static string Ref(object o) => o == null ? "null" : o.GetType().Name + "@" + RuntimeHelpers.GetHashCode(o).ToString("x", Invariant);
    private static string F(float f) => f.ToString("0.###", Invariant);
    private static int StaticCount(Type t, string name) => (ReadField(null, name, t) as System.Collections.IDictionary)?.Count ?? -1;
    private static object ReadField(object instance, string name, Type staticType = null)
    {
        Type t = staticType ?? instance?.GetType(); if (t == null) return null;
        string key = t.FullName + "/" + name; FieldInfo field;
        if (!Fields.TryGetValue(key, out field)) { field = t.GetField(name, Declared); Fields[key] = field; }
        return field?.GetValue(instance);
    }
    private static string Caller()
    {
        var trace = new System.Diagnostics.StackTrace(2, false);
        var b = new StringBuilder();
        for (int i = 0; i < trace.FrameCount && i < 20; i++)
        {
            MethodBase method = trace.GetFrame(i)?.GetMethod();
            b.Append(method?.DeclaringType?.Name).Append('.').Append(method?.Name).Append(" <- ");
        }
        return b.ToString();
    }
    private static string Clean(string s, int max = MaxLineChars)
    {
        if (s == null) return "";
        s = s.Replace("\r", " ").Replace("\n", " | ").Replace("\t", " ");
        return s.Length <= max ? s : s.Substring(0, max) + "[TRUNCATED]";
    }
    private static void Write(string phase, long id, long parent, string text)
    {
        if (!enabled) return;
        if (eventCount >= MaxEvents || textChars >= MaxTextChars || Time.realtimeSinceStartup - started >= MaxSeconds)
        {
            enabled = false; stopReason = Time.realtimeSinceStartup - started >= MaxSeconds ? "5-minute limit" : "event/text limit"; detachPending = true;
            SafeOut("[REBIRTH CraftTrace36] LIMIT capture=" + capture + " trace stopped; hooks detach next game event.");
            return;
        }
        string line = "[REBIRTH CraftTrace36] capture=" + capture + " event=" + (++eventCount) +
            " t=" + F(Time.realtimeSinceStartup - started) + " frame=" + Time.frameCount + " op=" + id + " parent=" + parent +
            " " + phase + " " + Clean(text);
        textChars += line.Length; SafeOut(line);
    }
    private static void SafeOut(string text)
    {
        try { Log.Out(text); }
        catch { ++errors; enabled = false; stopReason = "trace log write failed"; detachPending = Attached.Count != 0; }
    }
    private static void SafeWarning(string text)
    {
        try { Log.Warning(text); }
        catch { ++errors; enabled = false; stopReason = "trace log write failed"; detachPending = Attached.Count != 0; }
    }
    private static void RecorderError(Exception ex)
    {
        ++errors;
        if (errors <= 3) SafeWarning("[REBIRTH CraftTrace36] OBSERVER-ERROR (gameplay unchanged) " + Clean(ex.ToString()));
        if (errors >= 8) { enabled = false; stopReason = "observer error limit"; detachPending = true; }
    }
}
