using Audio;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Server-validated admission handshake for personal/inventory crafting.
///
/// 7DTD's personal crafting queue and player inventory are client-owned. This service therefore
/// does not pretend to replace the stock inventory authority model. Instead, every normal REBIRTH
/// GATED/DISABLED personal craft activation is deferred on remote clients until the server evaluates
/// the exact recipe policy and, when applicable, its declared Capability against the server-side EntityPlayer.
/// UNIVERSAL and external compatibility recipes preserve the native transaction directly. An allow response re-enters the native
/// ItemActionEntryCraft.OnActivated transaction once, preserving vanilla queue insertion, ingredient
/// removal, tracked-recipe handling and UI refresh. Saved queues intentionally bypass this admission
/// path and continue to restore through XUiC_CraftingQueue.AddRecipeToCraftAtIndex.
/// </summary>
public static class RebirthPersonalCraftAuthorizationService
{
    public const int ProtocolVersion = 1;
    public const int MaxPendingRequests = 32;
    public const int MaxRecipeNameLength = 128;
    public const int MaxReasonLength = 256;
    public const int RequestTimeoutSeconds = 10;

    private sealed class PendingRequest
    {
        public ulong RequestId;
        public int PlayerEntityId;
        public string RecipeName;
        public long ExpiresUtcTicks;
        public ItemActionEntryCraft Action;
        public XUiC_RebirthPersonalCrafting Owner;
        public long UiEpoch;
        public int CraftCount;
        public int CraftingTier;
        public Func<bool> IsOriginalScope;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<ulong, PendingRequest> Pending = new Dictionary<ulong, PendingRequest>();
    private static ulong nextRequestId;
    private static bool installed;

    // ProcessPackage is executed on the game thread. The replay marker only exists while the
    // server-approved activation is synchronously re-entering OnActivated.
    private static ItemActionEntryCraft replayAction;
    private static string replayRecipeName = string.Empty;

    public static string Install()
    {
        if (installed) return "[REBIRTH Personal Craft Authority] already installed";
        ModEvents.SavePlayerData.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SSavePlayerDataData>(RebirthTheorySoloNativeUploadReceipt.OnSaved));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH Personal Craft Authority] installed";
    }

    /// <summary>
    /// Harmony prefix contract for ItemActionEntryCraft.OnActivated.
    /// true = run the native transaction now; false = activation is denied/deferred.
    /// </summary>
    public static bool AuthorizeActivation(ItemActionEntryCraft action)
    {
        if (action == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return true;

        XUiC_RecipeEntry entry = action.ItemController as XUiC_RecipeEntry;
        Recipe recipe = entry != null ? entry.Recipe : null;
        if (recipe == null) return true;

        // Workstation recipes already have server-authoritative queue validation in
        // TileEntityWorkstation.HandleRecipeQueue. This handshake is intentionally personal-only.
        if (!string.IsNullOrEmpty(recipe.craftingArea)) return true;

        string recipeName = NormalizeRecipeName(recipe.GetName());
        if (string.IsNullOrEmpty(recipeName)) return true;

        // UNIVERSAL and external compatibility recipes run natively. GATED and DISABLED owned
        // recipes must cross the same server-authoritative policy evaluator.
        if (!RebirthCraftingProgressionRegistry.RequiresServerAuthorization(recipeName))
            return true;

        if (ReferenceEquals(replayAction, action) &&
            string.Equals(replayRecipeName, recipeName, StringComparison.OrdinalIgnoreCase))
            return true;

        EntityPlayerLocal local = action.ItemController != null && action.ItemController.xui != null &&
                                  action.ItemController.xui.playerUI != null
            ? action.ItemController.xui.playerUI.entityPlayer as EntityPlayerLocal
            : null;
        if (local == null)
        {
            Denied(recipeName, -1, "local-player-unavailable");
            return false;
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null)
        {
            Denied(recipeName, local.entityId, "connection-unavailable");
            return false;
        }

        // Single-player/listen-server path: evaluate the same server-side EntityPlayer synchronously.
        if (connection.IsServer)
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayer serverPlayer = world != null ? world.GetEntity(local.entityId) as EntityPlayer : null;
            if (serverPlayer == null)
            {
                Denied(recipeName, local.entityId, "server-player-unavailable");
                return false;
            }
            RebirthCapabilityEvaluation evaluation = RebirthCapabilityService.EvaluateRecipe(serverPlayer, recipeName);
            if (evaluation.IsAllowed) return true;
            Denied(recipeName, local.entityId, "missing-capability:" + SafeReason(evaluation.FirstMissingReason));
            return false;
        }

        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        PlatformUserIdentifierAbs userId = persistent != null ? persistent.PrimaryId : null;
        if (userId == null)
        {
            Denied(recipeName, local.entityId, "player-identity-unavailable");
            return false;
        }

        XUiC_RebirthPersonalCrafting owner = action.ItemController != null ? action.ItemController.GetParentByType<XUiC_RebirthPersonalCrafting>() : null;
        XUiC_RecipeCraftCount countControl = owner != null ? owner.GetChildByType<XUiC_RecipeCraftCount>() : null;
        XUiC_RebirthCraftingRecipeDetails details = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingRecipeDetails>() : null;
        if (owner == null || !owner.State.IsOpen || countControl == null || details == null || details.SelectedRecipe != recipe)
        {
            Denied(recipeName, local.entityId, "craft-intent-unavailable");
            return false;
        }
        int requestedCount = Math.Max(1, countControl.Count);
        int requestedTier = Math.Max(1, details.SelectedCraftingTier);
        long requestedEpoch = owner.CraftIntentEpoch;

        // Approval belongs to this exact native session and character, not merely an entity number.
        var originalGame = GameManager.Instance;
        var originalWorld = local.world;
        var originalState = originalWorld != null ? originalWorld.worldState : null;
        var originalPeer = connection.connectionToServer != null && connection.connectionToServer.Length > 0
            ? connection.connectionToServer[0] : null;
        string originalCreation = RebirthSurvivorClientState.GetProjectedCreationId(local);
        Guid originalServerWorld;
        if (originalGame == null || originalWorld == null || originalState == null || originalPeer == null ||
            originalPeer.IsDisconnected() || !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient), out originalServerWorld) ||
            originalServerWorld == Guid.Empty || string.IsNullOrEmpty(originalCreation))
        {
            Denied(recipeName, local.entityId, "craft-session-unavailable");
            return false;
        }
        Func<bool> originalScope = () => ThreadManager.IsMainThread() &&
            ReferenceEquals(GameManager.Instance, originalGame) && ReferenceEquals(originalGame.World, originalWorld) &&
            ReferenceEquals(local.world, originalWorld) && ReferenceEquals(originalWorld.worldState, originalState) &&
            originalWorld.IsRemote() && ReferenceEquals(originalWorld.GetPrimaryPlayer(), local) &&
            ReferenceEquals(originalWorld.GetEntity(local.entityId), local) && local.IsSpawned() && !local.IsDead() &&
            RebirthSurvivorMode.IsEnabledForCurrentWorld() && !RebirthCharacterCreationHoldService.IsHeld(local) &&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance, connection) && !connection.IsServer &&
            connection.connectionToServer != null && connection.connectionToServer.Length > 0 &&
            ReferenceEquals(connection.connectionToServer[0], originalPeer) && !originalPeer.IsDisconnected() &&
            Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient), out var currentServerWorld) && currentServerWorld == originalServerWorld &&
            RebirthSurvivorRequestScope.Matches(originalCreation, RebirthSurvivorClientState.GetProjectedCreationId(local)) &&
            originalGame.GetPersistentLocalPlayer()?.PrimaryId != null &&
            originalGame.GetPersistentLocalPlayer().PrimaryId.Equals(userId);
        if (!originalScope()) return false;

        ulong requestId;
        lock (Sync)
        {
            CleanupExpiredLocked(DateTime.UtcNow.Ticks);
            foreach (KeyValuePair<ulong, PendingRequest> pair in Pending)
            {
                PendingRequest existing = pair.Value;
                if (existing != null && ReferenceEquals(existing.Action, action) &&
                    existing.UiEpoch == requestedEpoch && existing.CraftCount == requestedCount && existing.CraftingTier == requestedTier &&
                    string.Equals(existing.RecipeName, recipeName, StringComparison.OrdinalIgnoreCase))
                    return false; // one in-flight authorization per craft action/recipe
            }
            if (Pending.Count >= MaxPendingRequests)
            {
                Denied(recipeName, local.entityId, "authorization-queue-busy");
                return false;
            }
            requestId = NextRequestIdLocked();
            Pending[requestId] = new PendingRequest
            {
                RequestId = requestId,
                PlayerEntityId = local.entityId,
                RecipeName = recipeName,
                ExpiresUtcTicks = DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds).Ticks,
                Action = action,
                Owner = owner,
                UiEpoch = requestedEpoch,
                CraftCount = requestedCount,
                CraftingTier = requestedTier,
                IsOriginalScope = originalScope
            };
        }

        connection.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthPersonalCraftAuthorizationRequest>()
                .Setup(local.entityId, userId, requestId, recipeName));
        return false;
    }

    internal static void Receive(ulong requestId, string recipeName, bool allowed, string reason)
    {
        PendingRequest request = null;
        long now = DateTime.UtcNow.Ticks;
        lock (Sync)
        {
            CleanupExpiredLocked(now);
            if (Pending.TryGetValue(requestId, out request)) Pending.Remove(requestId);
        }
        if (request == null) return;
        if (request.ExpiresUtcTicks <= now || request.IsOriginalScope == null || !request.IsOriginalScope()) return;

        recipeName = NormalizeRecipeName(recipeName);
        if (!string.Equals(request.RecipeName, recipeName, StringComparison.OrdinalIgnoreCase))
        {
            Denied(request.RecipeName, request.PlayerEntityId, "authorization-recipe-mismatch");
            return;
        }
        if (!allowed)
        {
            Denied(request.RecipeName, request.PlayerEntityId, SafeReason(reason));
            return;
        }

        ItemActionEntryCraft action = request.Action;
        XUiC_RecipeEntry currentEntry = action != null ? action.ItemController as XUiC_RecipeEntry : null;
        Recipe currentRecipe = currentEntry != null ? currentEntry.Recipe : null;
        string currentName = currentRecipe != null ? NormalizeRecipeName(currentRecipe.GetName()) : string.Empty;
        if (action == null || currentRecipe == null ||
            !string.Equals(currentName, request.RecipeName, StringComparison.OrdinalIgnoreCase))
            return; // window/list changed while the server response was in flight

        XUiC_RebirthPersonalCrafting owner = request.Owner;
        XUiC_RecipeCraftCount countControl = owner != null ? owner.GetChildByType<XUiC_RecipeCraftCount>() : null;
        XUiC_RebirthCraftingRecipeDetails details = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingRecipeDetails>() : null;
        if (owner == null || !owner.State.IsOpen || owner.CraftIntentEpoch != request.UiEpoch ||
            countControl == null || Math.Max(1, countControl.Count) != request.CraftCount ||
            details == null || details.SelectedRecipe != currentRecipe || Math.Max(1, details.SelectedCraftingTier) != request.CraftingTier)
            return; // exact deferred intent changed, closed or reopened; never replay approval into new UI state

        replayAction = action;
        replayRecipeName = request.RecipeName;
        try
        {
            action.OnActivated();
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Survivor] personal craft authorized replay failed recipe='" + request.RecipeName + "': " + ex);
        }
        finally
        {
            replayAction = null;
            replayRecipeName = string.Empty;
        }
    }

    internal static void ProcessServerRequest(World world, int playerEntityId, ulong requestId, string recipeName)
    {
        recipeName = NormalizeRecipeName(recipeName);
        bool allowed = false;
        string reason = string.Empty;

        if (world == null || world.IsRemote()) reason = "server-world-unavailable";
        else if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) allowed = true;
        else if (string.IsNullOrEmpty(recipeName)) reason = "invalid-recipe";
        else
        {
            Recipe canonical = CraftingManager.GetRecipe(recipeName);
            if (canonical == null) { reason = "unknown-recipe"; Reply(playerEntityId, requestId, recipeName, false, reason); return; }
            if (!string.IsNullOrEmpty(canonical.craftingArea)) { reason = "not-personal-recipe"; Reply(playerEntityId, requestId, recipeName, false, reason); return; }
            EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
            if (player == null) reason = "server-player-unavailable";
            else
            {
                RebirthCapabilityEvaluation evaluation = RebirthCapabilityService.EvaluateRecipe(player, recipeName);
                allowed = evaluation.IsAllowed;
                if (!allowed) reason = "crafting-policy:" + SafeReason(evaluation.FirstMissingReason);
            }
        }

        Reply(playerEntityId, requestId, recipeName, allowed, reason);
    }

    private static void Reply(int playerEntityId, ulong requestId, string recipeName, bool allowed, string reason)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection != null && connection.IsServer)
        {
            connection.SendPackage(
                NetPackageManager.GetPackage<NetPackageRebirthPersonalCraftAuthorizationResult>()
                    .Setup(requestId, recipeName, allowed, SafeReason(reason)),
                _attachedToEntityId: playerEntityId);
        }
        else
        {
            Receive(requestId, recipeName, allowed, reason);
        }
    }

    private static string NormalizeRecipeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Trim();
        return value.Length <= MaxRecipeNameLength ? value : value.Substring(0, MaxRecipeNameLength);
    }

    private static string SafeReason(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= MaxReasonLength ? value : value.Substring(0, MaxReasonLength);
    }

    private static void Denied(string recipeName, int entityId, string reason)
    {
        Manager.PlayInsidePlayerHead("ui_denied");
        Log.Warning("[REBIRTH Survivor] blocked personal crafting recipe='" + (recipeName ?? string.Empty) +
                    "' entity=" + entityId + " reason=" + (reason ?? string.Empty));
    }

    private static ulong NextRequestIdLocked()
    {
        unchecked { ++nextRequestId; }
        if (nextRequestId == 0) ++nextRequestId;
        return nextRequestId;
    }

    private static void CleanupExpiredLocked(long now)
    {
        if (Pending.Count == 0) return;
        List<ulong> expired = null;
        foreach (KeyValuePair<ulong, PendingRequest> pair in Pending)
        {
            if (pair.Value == null || pair.Value.ExpiresUtcTicks <= now)
            {
                if (expired == null) expired = new List<ulong>();
                expired.Add(pair.Key);
            }
        }
        if (expired == null) return;
        for (int i = 0; i < expired.Count; ++i) Pending.Remove(expired[i]);
    }

    public static void Reset()
    {
        RebirthTheorySoloNativeUploadReceipt.Reset();
        RebirthTheorySoloOriginalAdmission.Reset();
        lock (Sync)
        {
            Pending.Clear();
            nextRequestId = 0;
        }
        replayAction = null;
        replayRecipeName = string.Empty;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data) { Reset(); }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { Reset(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { Reset(); }
}

[Preserve]
public sealed class NetPackageRebirthPersonalCraftAuthorizationRequest : NetPackage
{
    private int protocolVersion;
    private int playerEntityId;
    private PlatformUserIdentifierAbs userId;
    private ulong requestId;
    private string recipeName;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthPersonalCraftAuthorizationRequest Setup(int playerEntityId, PlatformUserIdentifierAbs userId, ulong requestId, string recipeName)
    {
        this.protocolVersion = RebirthPersonalCraftAuthorizationService.ProtocolVersion;
        this.playerEntityId = playerEntityId;
        this.userId = userId;
        this.requestId = requestId;
        this.recipeName = recipeName ?? string.Empty;
        return this;
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(protocolVersion);
        binary.Write(playerEntityId);
        userId.ToStream(binary);
        binary.Write(requestId);
        binary.Write(recipeName ?? string.Empty);
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        protocolVersion = binary.ReadInt32();
        playerEntityId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        requestId = binary.ReadUInt64();
        recipeName = binary.ReadString();
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || protocolVersion != RebirthPersonalCraftAuthorizationService.ProtocolVersion ||
            userId == null || !ValidEntityIdForSender(playerEntityId) || !ValidUserIdForSender(userId)) return;
        if (string.IsNullOrEmpty(recipeName) || recipeName.Length > RebirthPersonalCraftAuthorizationService.MaxRecipeNameLength) return;
        RebirthPersonalCraftAuthorizationService.ProcessServerRequest(world, playerEntityId, requestId, recipeName);
    }

    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthPersonalCraftAuthorizationResult : NetPackage
{
    private int protocolVersion;
    private ulong requestId;
    private string recipeName;
    private bool allowed;
    private string reason;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthPersonalCraftAuthorizationResult Setup(ulong requestId, string recipeName, bool allowed, string reason)
    {
        this.protocolVersion = RebirthPersonalCraftAuthorizationService.ProtocolVersion;
        this.requestId = requestId;
        this.recipeName = recipeName ?? string.Empty;
        this.allowed = allowed;
        this.reason = reason ?? string.Empty;
        return this;
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(protocolVersion);
        binary.Write(requestId);
        binary.Write(recipeName ?? string.Empty);
        binary.Write(allowed);
        binary.Write(reason ?? string.Empty);
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        protocolVersion = binary.ReadInt32();
        requestId = binary.ReadUInt64();
        recipeName = binary.ReadString();
        allowed = binary.ReadBoolean();
        reason = binary.ReadString();
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (protocolVersion != RebirthPersonalCraftAuthorizationService.ProtocolVersion) return;
        if (recipeName != null && recipeName.Length > RebirthPersonalCraftAuthorizationService.MaxRecipeNameLength) return;
        RebirthPersonalCraftAuthorizationService.Receive(requestId, recipeName, allowed, reason);
    }

    public int GetLength() => 0;
}

/// <summary>
/// Personal queues are native/client-owned, unlike TileEntityWorkstation. Observe successful output,
/// never giveExp (which runs before the inventory-capacity decision). Remote notices carry identity,
/// recipe and a monotonic receipt only: the server computes all XP and requires an independently
/// received native inventory increase. This preserves the game's client-inventory trust boundary;
/// it is not a replacement server-authoritative inventory/crafting implementation.
/// </summary>
public static class RebirthPersonalCraftCompletionService
{
    internal sealed class Witness
    {
        public EntityPlayerLocal Player;
        public Recipe Recipe;
        public int BeforeCount, QueueCount;
        public long Serial;
        public bool Retry;
    }
    private sealed class Notice
    {
        public EntityPlayerLocal Player;
        public string Session, Recipe;
        public long Sequence;
        public float NextSend, Expires;
        public RebirthCraftTrainingRules.Model Model;
        public int Count;
        public bool Local;
    }
    private sealed class Pending
    {
        public string Session, Recipe;
        public long Sequence;
        public int Count, Type;
        public float Expires;
        public bool Reserved;
    }
    private sealed class Addition { public int Count; public float Expires; }
    private sealed class Peer
    {
        public EntityPlayer Player;
        public string Session;
        public long HighestSeen;
        public readonly SortedDictionary<long, Pending> Pending = new SortedDictionary<long, Pending>();
        public readonly HashSet<long> Done = new HashSet<long>();
        public readonly Dictionary<int, Addition> Additions = new Dictionary<int, Addition>();
    }
    internal sealed class InventoryWitness
    {
        public EntityPlayer Player;
        public Dictionary<int, int> Before;
    }
    private static readonly Dictionary<long, Notice> Outbox = new Dictionary<long, Notice>();
    private static readonly Dictionary<int, Peer> Peers = new Dictionary<int, Peer>();
    private static readonly List<long> Work = new List<long>();
    private static World runtimeWorld;
    private static string session = Guid.NewGuid().ToString("N");
    private static long sequence, outputSerial;
    private static float nextPump;
    private static bool installed, syncWarning, remoteObservationReady;
    private static System.Reflection.MethodInfo syncSetup;
    private static System.Reflection.FieldInfo senderField;
    private static System.Reflection.PropertyInfo senderProperty;
    [ThreadStatic] private static int outputDepth;
    public static bool PersonalOutputInProgress { get { return outputDepth > 0; } }

    public static void Install(HarmonyLib.Harmony harmony)
    {
        if (installed) return;
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthPersonalCraftOutputPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthPersonalCraftRetryPatch));
        Type packet = typeof(NetPackagePlayerInventory);
        var process = HarmonyLib.AccessTools.Method(packet, "ProcessPackage", new[] { typeof(World), typeof(GameManager) });
        senderProperty = HarmonyLib.AccessTools.Property(packet, "Sender");
        // 3.2 exposes Sender as an inherited property. Only probe the older field
        // contract if needed; AccessTools logs a warning for an expected missing field.
        senderField = senderProperty == null ? HarmonyLib.AccessTools.Field(packet, "Sender") : null;
        syncSetup = HarmonyLib.AccessTools.Method(packet, "Setup", new[] {
            typeof(EntityPlayerLocal), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
        if (process != null && (senderField != null || senderProperty != null))
        {
            try
            {
                harmony.Patch(process,
                    prefix: new HarmonyLib.HarmonyMethod(typeof(RebirthPersonalCraftCompletionService), nameof(BeforeInventory)),
                    postfix: new HarmonyLib.HarmonyMethod(typeof(RebirthPersonalCraftCompletionService), nameof(AfterInventory)));
                remoteObservationReady = true;
            }
            catch (Exception ex)
            { Log.Warning("[REBIRTH Crafting] Native inventory completion adapter unavailable; remote credit is disabled: " + ex.Message); }
        }
        else Log.Warning("[REBIRTH Crafting] Native inventory/Sender contract unavailable; remote credit is disabled.");
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(Starting));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(Stopping));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Pump));
        installed = true;
    }

    private static void Starting(ref ModEvents.SGameStartingData data) { Reset(); }
    private static void Stopping(ref ModEvents.SWorldShuttingDownData data) { Reset(); }
    private static void Reset()
    {
        Outbox.Clear(); Peers.Clear(); Work.Clear(); runtimeWorld = null;
        session = Guid.NewGuid().ToString("N"); sequence = outputSerial = 0; nextPump = 0f;
        outputDepth = 0; syncWarning = false;
    }
    private static void WorldReady(World world)
    {
        if (ReferenceEquals(runtimeWorld, world)) return;
        Reset(); runtimeWorld = world;
    }
    internal static int EnterOutput(Witness witness)
    { int previous = outputDepth; if (witness != null) ++outputDepth; return previous; }
    internal static void LeaveOutput(int previous) { outputDepth = previous; }

    internal static Witness BeforeOutput(XUiC_RecipeStack entry, bool retry)
    {
        if (entry == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || entry.AmountToRepair > 0 ||
            entry.windowGroup?.Controller is XUiC_WorkstationWindowGroup) return null;
        Recipe recipe = entry.GetRecipe();
        if (recipe == null || !string.IsNullOrEmpty(recipe.craftingArea) || RebirthCookingBatch.IsBatch(recipe) ||
            string.IsNullOrEmpty(RebirthServiceCraftSkillService.ClassifyRecipe(recipe))) return null;
        var player = entry.xui?.playerUI?.entityPlayer as EntityPlayerLocal;
        if (player == null || player.world == null) return null;
        // A personal queue belongs to this XUi player. Native saved personal entries may have a
        // legacy/default StartingEntityId; do not confuse it with workstation ownership.
        if (retry && !entry.isInventoryFull) return null;
        WorldReady(player.world);
        return new Witness { Player = player, Recipe = recipe, QueueCount = entry.recipeCount,
            BeforeCount = Count(player, recipe.itemValueType), Serial = outputSerial, Retry = retry };
    }

    internal static void AfterOutput(XUiC_RecipeStack entry, Witness witness, bool success)
    {
        if (RebirthSkillEvalDiagnostics.On) Log.Out("[REBIRTH SkillEval] AfterOutput witness=" + (witness != null) + " success=" + success + " sameWorld=" + (witness != null && ReferenceEquals(witness.Player.world, runtimeWorld)) + " recipe=" + (witness != null ? witness.Recipe.GetName() : "-") + " before=" + (witness != null ? witness.BeforeCount : -1) + " now=" + (witness != null ? Count(witness.Player, witness.Recipe.itemValueType) : -1) + " retry=" + (witness != null && witness.Retry));
        if (witness == null || !success || !ReferenceEquals(witness.Player.world, runtimeWorld)) return;
        // Retry Update may call the patched outputStack itself. Never credit both observers.
        if (witness.Retry && (outputSerial != witness.Serial || entry.isInventoryFull ||
            ReferenceEquals(entry.recipe, witness.Recipe) && entry.recipeCount >= witness.QueueCount)) return;
        if (Count(witness.Player, witness.Recipe.itemValueType) <= witness.BeforeCount) return;
        ++outputSerial;
        long id = ++sequence;
        string name = witness.Recipe.GetName();
        bool local = !witness.Player.world.IsRemote();
        var model = local ? RebirthCraftTrainingRules.BuildModel(witness.Player, witness.Recipe,
            RebirthServiceCraftSkillService.ClassifyRecipe(witness.Recipe)) : new RebirthCraftTrainingRules.Model();
        int delivered = Math.Max(1, Count(witness.Player, witness.Recipe.itemValueType) - witness.BeforeCount);
        if (local && Credit(witness.Player,name,model,"pc35:"+session+":"+id,delivered))return;
        if (Outbox.Count >= 256)
        {
            Log.Warning("[REBIRTH Crafting] Completion receipt queue is full; no unverified XP was granted.");
            return;
        }
        var notice = new Notice { Player = witness.Player, Session = session, Sequence = id, Recipe = name,
            Expires = UnityEngine.Time.realtimeSinceStartup + 120f, Local = local, Model = model, Count = delivered };
        Outbox.Add(id, notice);
        // Snapshot the newly delivered output now, before the player can use/move it. Both sends
        // use the standard reliable server route. This does not add, remove or normalize items.
        if(!local)Send(notice, true);
    }

    private static int Count(EntityPlayer player, int type)
    {
        long sum = 0;
        AddCount(player.bag?.ItemGrid.items, type, ref sum);
        AddCount(player.inventory?.ItemGrid.items, type, ref sum);
        return (int)Math.Min(int.MaxValue, sum);
    }
    private static void AddCount(ItemStack[] slots, int type, ref long sum)
    {
        if (slots == null) return;
        foreach (var slot in slots) if (slot != null && !slot.IsEmpty() && slot.itemValue.type == type) sum += Math.Max(0, slot.count);
    }
    private static Dictionary<int, int> Inventory(EntityPlayer player)
    {
        var values = new Dictionary<int, int>();
        Inventory(player.bag?.ItemGrid.items, values); Inventory(player.inventory?.ItemGrid.items, values);
        return values;
    }
    private static void Inventory(ItemStack[] slots, Dictionary<int, int> values)
    {
        if (slots == null) return;
        foreach (var stack in slots)
        {
            if (stack == null || stack.IsEmpty()) continue;
            int count; values.TryGetValue(stack.itemValue.type, out count);
            values[stack.itemValue.type] = (int)Math.Min(int.MaxValue, (long)count + Math.Max(0, stack.count));
        }
    }
    private static Peer GetPeer(EntityPlayer player)
    {
        Peer peer;
        if (!Peers.TryGetValue(player.entityId, out peer) || !ReferenceEquals(peer.Player, player))
        {
            peer = new Peer { Player = player };
            Peers[player.entityId] = peer;
        }
        return peer;
    }
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]
    private static void BeforeInventory(NetPackage __instance, World _world, out InventoryWitness __state)
    {
        __state = null;
        if (_world == null || _world.IsRemote() || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        // Sender is the authenticated connection, not an entity ID taken from packet payload.
        var sender = (senderField != null ? senderField.GetValue(__instance) : senderProperty?.GetValue(__instance, null)) as ClientInfo;
        if (sender == null) return;
        EntityPlayer player = _world.GetEntity(sender.entityId) as EntityPlayer;
        if (player == null) return;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record)) return;
        WorldReady(_world);
        __state = new InventoryWitness { Player = player, Before = Inventory(player) };
    }
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]
    private static void AfterInventory(InventoryWitness __state)
    {
        if (__state == null) return;
        Peer peer = GetPeer(__state.Player);
        float now = UnityEngine.Time.realtimeSinceStartup;
        var after = Inventory(__state.Player);
        foreach (var pair in after)
        {
            int before; __state.Before.TryGetValue(pair.Key, out before);
            if (pair.Value <= before) continue;
            Addition old;
            if (!peer.Additions.TryGetValue(pair.Key, out old) || old.Expires < now)
                old = new Addition();
            old.Count = (int)Math.Min(3276700, (long)old.Count + pair.Value - before);
            old.Expires = now + 120f;
            if (peer.Additions.Count < 512 || peer.Additions.ContainsKey(pair.Key)) peer.Additions[pair.Key] = old;
        }
        Drain(peer);
    }

    internal static void ReceiveServer(EntityPlayer player, string clientSession, long id, string recipeName)
    {
        if (player == null || player.world == null || player.world.IsRemote() || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        Guid parsed;
        if (clientSession == null || clientSession.Length != 32 || !Guid.TryParseExact(clientSession, "N", out parsed) ||
            id < 1 || string.IsNullOrEmpty(recipeName) || recipeName.Length > 160) return;
        if (!remoteObservationReady) { Ack(player.entityId, clientSession, id, false); return; }
        WorldReady(player.world);
        Peer peer = GetPeer(player);
        if (peer.Session == null) peer.Session = clientSession;
        if (!string.Equals(peer.Session, clientSession, StringComparison.Ordinal)) return;
        if (peer.Done.Contains(id)) { Ack(player.entityId, clientSession, id, true); return; }
        // Bounded sliding receipt window; old IDs cannot be replayed after their Done entry is pruned.
        if (id <= peer.HighestSeen - 4096 || id > peer.HighestSeen + 4096) return;
        if (peer.Pending.ContainsKey(id)) { Drain(peer); return; }
        if (peer.Pending.Count >= 256) return;
        Recipe recipe = CraftingManager.GetRecipe(recipeName);
        if (recipe == null || !string.IsNullOrEmpty(recipe.craftingArea) || recipe.count < 1 || recipe.count > 32767 ||
            string.IsNullOrEmpty(RebirthServiceCraftSkillService.ClassifyRecipe(recipe))) return;
        // Reject disabled/locked recipes rather than turning the completion channel into an unlock.
        if (!RebirthCapabilityService.EvaluateRecipe(player, recipeName).IsAllowed) return;
        peer.HighestSeen = Math.Max(peer.HighestSeen, id);
        peer.Pending.Add(id, new Pending { Session = clientSession, Sequence = id, Recipe = recipeName,
            Count = recipe.count, Type = recipe.itemValueType, Expires = UnityEngine.Time.realtimeSinceStartup + 120f });
        Drain(peer);
    }
    private static void Drain(Peer peer)
    {
        if (peer.Pending.Count == 0) return;
        // No shared scratch list: completion/owner-state sends can synchronously re-enter on a host.
        var done = new List<long>();
        float now = UnityEngine.Time.realtimeSinceStartup;
        foreach (var pair in peer.Pending)
        {
            Pending p = pair.Value;
            if (p.Expires < now) { done.Add(pair.Key); Ack(peer.Player.entityId, p.Session, p.Sequence, false); continue; }
            RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
            if (!RebirthSkillAwardService.TryGetEligible(peer.Player, out identity, out record)) break;
            string receipt = "pc35:" + p.Session + ":" + p.Sequence;
            bool already = record.Progression.SkillAwardReceipts.Contains(receipt);
            if (!p.Reserved && !already)
            {
                Addition added;
                if (!peer.Additions.TryGetValue(p.Type, out added) || added.Expires < now || added.Count < p.Count) continue;
                added.Count -= p.Count; p.Reserved = true;
            }
            var model = RebirthCraftTrainingRules.ModelForRecipe(peer.Player, p.Recipe);
            if (!Credit(peer.Player, p.Recipe, model, receipt, p.Count)) continue;
            done.Add(pair.Key); peer.Done.Add(pair.Key);
            Ack(peer.Player.entityId, p.Session, p.Sequence, true);
        }
        foreach (long id in done) peer.Pending.Remove(id);
        peer.Done.RemoveWhere(id => id <= peer.HighestSeen - 4096);
    }
    private static bool Credit(EntityPlayer player, string recipeName, RebirthCraftTrainingRules.Model model, string receipt, int count)
    {
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record)) return false;
        string skill = RebirthServiceCraftSkillService.ClassifyRecipe(recipeName);
        if (string.IsNullOrEmpty(skill)) return false;
        bool hadReceipt = record.Progression.SkillAwardReceipts.Contains(receipt);
        bool accepted = RebirthSkillAwardService.TryAwardCraft(player,skill,model,1,1f,"personal-craft:"+recipeName,receipt);
        if (RebirthSkillEvalDiagnostics.On) Log.Out("[REBIRTH SkillEval] Credit recipe=" + recipeName + " skill=" + skill + " modelValid=" + model.Valid + " raw=" + model.Raw + " hadReceipt=" + hadReceipt + " accepted=" + accepted);
        if (accepted && !hadReceipt) RebirthStatisticsService.RecordItemsCrafted(player, recipeName, count);
        return accepted;
    }
    private static void Send(Notice n, bool forceInventory)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        var persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        if (connection == null || persistent?.PrimaryId == null) return;
        n.NextSend = UnityEngine.Time.realtimeSinceStartup + 2f;
        if (forceInventory)
        {
            if (syncSetup != null)
            {
                try
                {
                    var snapshot = NetPackageManager.GetPackage<NetPackagePlayerInventory>();
                    syncSetup.Invoke(snapshot, new object[] { n.Player, true, true, false, false });
                    connection.SendToServer(snapshot);
                }
                catch (Exception ex)
                { if (!syncWarning) { syncWarning = true; Log.Warning("[REBIRTH Crafting] Native output snapshot unavailable: " + ex.Message); } }
            }
            else if (!syncWarning)
            { syncWarning = true; Log.Warning("[REBIRTH Crafting] Native inventory Setup signature unavailable; waiting for ordinary inventory replication."); }
        }
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPersonalCraftCompletion>()
            .Setup(n.Player.entityId, persistent.PrimaryId, n.Session, n.Sequence, n.Recipe));
    }
    private static void Ack(int playerId, string sessionId, long id, bool accepted)
    {
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthPersonalCraftCompletionAck>().Setup(sessionId, id, accepted),
            _attachedToEntityId: playerId);
    }
    internal static void ReceiveAck(string sessionId, long id, bool accepted)
    {
        if (!string.Equals(session, sessionId, StringComparison.Ordinal)) return;
        if (Outbox.Remove(id) && !accepted)
            Log.Warning("[REBIRTH Crafting] A completion could not be matched to the server inventory; no unverified skill credit was granted.");
    }
    private static void Pump(ref ModEvents.SGameUpdateData data)
    {
        if (Outbox.Count == 0 && Peers.Count == 0) return;
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (now < nextPump) return; nextPump = now + 0.5f;
        World world = GameManager.Instance?.World;
        if (world == null || !ReferenceEquals(world, runtimeWorld)) { Reset(); return; }
        Work.Clear();
        foreach (var pair in Outbox)
        {
            Notice n = pair.Value;
            if (now > n.Expires) { Work.Add(pair.Key); continue; }
            if (now >= n.NextSend)
            {
                if(n.Local)
                {
                    n.NextSend=now+1f;
                    if(Credit(n.Player,n.Recipe,n.Model,"pc35:"+n.Session+":"+n.Sequence,n.Count))Work.Add(pair.Key);
                }
                else Send(n, false);
            }
        }
        foreach (long id in Work)
        {
            Notice removed=Outbox[id]; Outbox.Remove(id);
            if(now>removed.Expires)Log.Warning("[REBIRTH Crafting] Personal crafting completion acknowledgement timed out.");
        }
        if (!world.IsRemote())
        {
            var gone = new List<int>();
            foreach (var pair in Peers)
            {
                if (!ReferenceEquals(world.GetEntity(pair.Key), pair.Value.Player)) { gone.Add(pair.Key); continue; }
                Drain(pair.Value);
                var stale = new List<int>();
                foreach (var add in pair.Value.Additions) if (add.Value.Expires < now) stale.Add(add.Key);
                foreach (int type in stale) pair.Value.Additions.Remove(type);
            }
            foreach (int id in gone) Peers.Remove(id);
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(XUiC_RecipeStack), "outputStack")]
internal static class RebirthPersonalCraftOutputPatch
{
    internal struct State { public RebirthPersonalCraftCompletionService.Witness Witness; public int Depth; }
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]
    private static void Prefix(XUiC_RecipeStack __instance, out State __state)
    {
        var witness = RebirthPersonalCraftCompletionService.BeforeOutput(__instance, false);
        __state = new State { Witness = witness, Depth = RebirthPersonalCraftCompletionService.EnterOutput(witness) };
    }
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]
    private static void Postfix(XUiC_RecipeStack __instance, bool __result, State __state)
    { RebirthPersonalCraftCompletionService.AfterOutput(__instance, __state.Witness, __result); }
    private static Exception Finalizer(Exception __exception, State __state)
    { RebirthPersonalCraftCompletionService.LeaveOutput(__state.Depth); return __exception; }
}
[HarmonyLib.HarmonyPatch(typeof(XUiC_RecipeStack), "Update", new[] { typeof(float) })]
internal static class RebirthPersonalCraftRetryPatch
{
    internal struct State { public RebirthPersonalCraftCompletionService.Witness Witness; public int Depth; }
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]
    private static void Prefix(XUiC_RecipeStack __instance, out State __state)
    {
        var witness = __instance != null && __instance.isInventoryFull
            ? RebirthPersonalCraftCompletionService.BeforeOutput(__instance, true) : null;
        __state = new State { Witness = witness, Depth = RebirthPersonalCraftCompletionService.EnterOutput(witness) };
    }
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]
    private static void Postfix(XUiC_RecipeStack __instance, State __state)
    { if (__state.Witness != null) RebirthPersonalCraftCompletionService.AfterOutput(__instance, __state.Witness, true); }
    private static Exception Finalizer(Exception __exception, State __state)
    { RebirthPersonalCraftCompletionService.LeaveOutput(__state.Depth); return __exception; }
}

[Preserve]
public sealed class NetPackageRebirthPersonalCraftCompletion : NetPackage
{
    private int version, player;
    private PlatformUserIdentifierAbs user;
    private string session, recipe;
    private long sequence;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthPersonalCraftCompletion Setup(int p, PlatformUserIdentifierAbs u, string s, long id, string r)
    { version = 35; player = p; user = u; session = s; sequence = id; recipe = r; return this; }
    public override void write(PooledBinaryWriter w)
    { base.write(w); var b = (BinaryWriter)w; b.Write(version); b.Write(player); user.ToStream(b); b.Write(session); b.Write(sequence); b.Write(recipe); }
    public override void read(PooledBinaryReader r)
    { var b = (BinaryReader)r; version = b.ReadInt32(); player = b.ReadInt32(); user = PlatformUserIdentifierAbs.FromStream(b); session = b.ReadString(); sequence = b.ReadInt64(); recipe = b.ReadString(); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (version != 35 || world == null || world.IsRemote() || user == null ||
            !ValidEntityIdForSender(player) || !ValidUserIdForSender(user)) return;
        RebirthPersonalCraftCompletionService.ReceiveServer(world.GetEntity(player) as EntityPlayer, session, sequence, recipe);
    }
    public int GetLength() => 0;
}
[Preserve]
public sealed class NetPackageRebirthPersonalCraftCompletionAck : NetPackage
{
    private string session; private long sequence; private bool accepted;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthPersonalCraftCompletionAck Setup(string s, long id, bool ok)
    { session = s; sequence = id; accepted = ok; return this; }
    public override void write(PooledBinaryWriter w)
    { base.write(w); var b = (BinaryWriter)w; b.Write(session); b.Write(sequence); b.Write(accepted); }
    public override void read(PooledBinaryReader r)
    { session = r.ReadString(); sequence = r.ReadInt64(); accepted = r.ReadBoolean(); }
    public override void ProcessPackage(World world, GameManager callbacks)
    { if (world != null && world.IsRemote()) RebirthPersonalCraftCompletionService.ReceiveAck(session, sequence, accepted); }
    public int GetLength() => 0;
}
