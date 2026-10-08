using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#nullable disable

public enum RebirthWorkstationSecurityAction : byte
{
    Lock = 1,
    Unlock = 2,
    SetPassword = 3,
    TryPassword = 4
}

/// <summary>
/// Server-authoritative secure-container semantics for player-placed workstations.
/// Base 3.0 workstations are ILockTarget objects but do not implement ILockable and
/// do not persist an owner, lock state, passcode, or access list. This sidecar adds
/// those missing fields without replacing TileEntityWorkstation.
/// </summary>
public static class RebirthWorkstationSecurityService
{
    private const string FileName = "rebirth_workstation_owners.dat";
    private const byte FileVersion = 4; // v4 private verifier; v1-v3 migrate with owner reset for disclosed credentials.
    private const int Signature = 0x53574252; // RBWS
    private const float MaximumInteractionDistance = 8f;

    private sealed class SecurityState
    {
        public string Owner = string.Empty;
        public string StationType = string.Empty;
        public bool Locked;
        public string PasswordVerifier = string.Empty; // Authority ONLY; never projected to clients.
        public bool PasswordPresent;
        public bool ProtocolSupported = true;
        public bool PasswordResetRequired;
        public string Epoch = Guid.NewGuid().ToString("N");
        public readonly HashSet<string> AllowedUsers =
            new HashSet<string>(StringComparer.Ordinal);

        public SecurityState Clone()
        {
            SecurityState copy = new SecurityState
            {
                Owner = Owner ?? string.Empty,
                StationType = StationType ?? string.Empty,
                Locked = Locked,
                PasswordVerifier = PasswordVerifier ?? string.Empty,
                PasswordPresent = PasswordPresent,
                ProtocolSupported = ProtocolSupported,
                PasswordResetRequired = PasswordResetRequired,
                Epoch = Epoch
            };
            foreach (string user in AllowedUsers)
                copy.AllowedUsers.Add(user);
            return copy;
        }
    }

    private sealed class CommandCacheEntry
    {
        public BlockActivationCommand[] Source;
        public BlockActivationCommand[] Result;
        public int SourceLength;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<Vector3i, SecurityState> States =
        new Dictionary<Vector3i, SecurityState>();
    private static readonly Dictionary<Vector3i, float> NextStateRequestTime =
        new Dictionary<Vector3i, float>();
    private static readonly Dictionary<int, CommandCacheEntry> CommandCache =
        new Dictionary<int, CommandCacheEntry>();

    private static readonly RebirthWorkstationCredentials.Attempts Attempts = new RebirthWorkstationCredentials.Attempts();
    private static readonly Dictionary<string, float> PendingReplies = new Dictionary<string, float>(StringComparer.Ordinal);
    private static readonly Queue<string> Notices = new Queue<string>();
    private static readonly HashSet<string> CompatibilityWarnings = new HashSet<string>(StringComparer.Ordinal);

    private static long accessRevision;
    private static readonly HashSet<Vector3i> PendingAccessChanges = new HashSet<Vector3i>();
    public static long AccessRevision { get { return System.Threading.Interlocked.Read(ref accessRevision); } }

    private static void AccessChanged(Vector3i position)
    {
        // The cheap revision closes cached reads immediately; UI/network rebuilding is queued.
        System.Threading.Interlocked.Increment(ref accessRevision);
        PendingAccessChanges.Add(position);
    }

    private static bool serverWorld;
    private static bool dirty;

    public static void Initialize(bool asServer)
    {
        lock (Sync)
        {
            States.Clear();
            NextStateRequestTime.Clear();
            CommandCache.Clear();
            Attempts.Clear();
            PendingReplies.Clear();
            Notices.Clear();
            CompatibilityWarnings.Clear();
            PendingAccessChanges.Clear();
            System.Threading.Interlocked.Increment(ref accessRevision);
            dirty = false;
            serverWorld = asServer;
        }

        if (asServer)
            Load();
    }

    public static void Shutdown(bool save)
    {
        if (save)
            Save();

        lock (Sync)
        {
            States.Clear();
            NextStateRequestTime.Clear();
            CommandCache.Clear();
            Attempts.Clear();
            PendingReplies.Clear();
            Notices.Clear();
            CompatibilityWarnings.Clear();
            PendingAccessChanges.Clear();
            System.Threading.Interlocked.Increment(ref accessRevision);
            dirty = false;
            serverWorld = false;
        }
    }

    public static void RegisterPlaced(
        WorldBase world,
        Vector3i blockPos,
        EntityAlive placingEntity)
    {
        if (world == null || world.IsRemote())
            return;

        EntityPlayer player = placingEntity as EntityPlayer;
        PersistentPlayerData persistent = player != null && GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)
            : null;
        if (persistent == null || persistent.PrimaryId == null)
            return;

        SecurityState state = new SecurityState
        {
            Owner = persistent.PrimaryId.CombinedString,
            StationType = world.GetBlock(blockPos).Block.GetBlockName(),
            Locked = false,
            PasswordVerifier = string.Empty
        };
        SetState(blockPos, state, true);
        BroadcastState(blockPos, state);
    }

    public static void Remove(Vector3i blockPos)
    {
        lock (Sync)
        {
            NextStateRequestTime.Remove(blockPos);
            if (States.Remove(blockPos))
            {
                AccessChanged(blockPos);
                if (serverWorld) dirty = true;
            }
        }
    }

    public static bool TryGetOwner(Vector3i blockPos, out PlatformUserIdentifierAbs owner)
    {
        owner = null;
        string combined;
        lock (Sync)
        {
            SecurityState state;
            if (!States.TryGetValue(blockPos, out state) || state == null)
                return false;
            combined = state.Owner;
        }

        return TryParseUser(combined, out owner);
    }

    public static bool IsStressCamp(EntityPlayer player)
    {
        var user=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
        if(user==null)return false;
        var types=new HashSet<string>();bool nearby=false;
        lock(Sync)foreach(var pair in States)
        {
            if(pair.Value.Owner!=user.CombinedString&&!pair.Value.AllowedUsers.Contains(user.CombinedString))continue;
            // Remember types across chunk unloading so station separation does not matter.
            var tile=player.world.GetTileEntity(pair.Key) as TileEntityWorkstation;
            if(tile!=null&&string.IsNullOrEmpty(pair.Value.StationType)){pair.Value.StationType=player.world.GetBlock(pair.Key).Block.GetBlockName();dirty=true;}
            if(string.IsNullOrEmpty(pair.Value.StationType))continue;
            types.Add(pair.Value.StationType);
            if(Vector3.Distance(player.position,new Vector3(pair.Key.x,pair.Key.y,pair.Key.z))<=20)nearby=true;
        }
        return nearby&&types.Count>=2;
    }

    public static bool IsLocked(Vector3i blockPos)
    {
        lock (Sync)
        {
            SecurityState state;
            return States.TryGetValue(blockPos, out state) && state != null && state.Locked;
        }
    }

    public static bool HasPassword(Vector3i blockPos)
    {
        lock (Sync)
        {
            SecurityState state;
            return States.TryGetValue(blockPos, out state) && state != null &&
                (serverWorld ? !string.IsNullOrEmpty(state.PasswordVerifier) : state.PasswordPresent);
        }
    }

    // ILockable must not provide the private verifier even on the local authority.
    public static string GetPasswordHash(Vector3i blockPos) { return string.Empty; }

    public static List<PlatformUserIdentifierAbs> GetAllowedUsers(Vector3i blockPos)
    {
        List<PlatformUserIdentifierAbs> result = new List<PlatformUserIdentifierAbs>();
        lock (Sync)
        {
            SecurityState state;
            if (!States.TryGetValue(blockPos, out state) || state == null)
                return result;

            foreach (string combined in state.AllowedUsers)
            {
                PlatformUserIdentifierAbs user;
                if (TryParseUser(combined, out user))
                    result.Add(user);
            }
        }
        return result;
    }

    public static bool IsOwner(Vector3i blockPos, PlatformUserIdentifierAbs userId)
    {
        if (userId == null)
            return false;

        lock (Sync)
        {
            SecurityState state;
            return States.TryGetValue(blockPos, out state) && state != null &&
                string.Equals(state.Owner, userId.CombinedString, StringComparison.Ordinal);
        }
    }

    private static bool Allows(SecurityState state, PlatformUserIdentifierAbs userId, bool exactOwner)
    {
        if (state == null || !state.ProtocolSupported) return false;
        bool owner = userId != null && string.Equals(state.Owner, userId.CombinedString, StringComparison.Ordinal);
        if (exactOwner) return owner;
        if (!state.Locked) return true;
        return owner || (userId != null && state.AllowedUsers.Contains(userId.CombinedString));
    }

    public static bool IsUserAllowed(Vector3i blockPos, PlatformUserIdentifierAbs userId)
    {
        lock (Sync)
        {
            SecurityState state;
            return States.TryGetValue(blockPos, out state) && Allows(state, userId, false);
        }
    }

    /// <summary>
    /// Observation and withdrawal share the authoritative physical-open rule.
    /// exactOwner is an ownership filter, not an alternative permission grant.
    /// Locked workstations retain owner/PIN-list semantics; no new ally/party expansion.
    /// Non-player-placed workstations retain public use.
    /// </summary>
    public static bool CanAccessWorkstation(WorldBase world, Vector3i blockPos, EntityPlayer player,
        PlatformUserIdentifierAbs userId, bool exactOwner, RebirthSecureAccessPurpose purpose, out string reason)
    {
        reason = string.Empty;
        TileEntityWorkstation workstation = world != null ? world.GetTileEntity(blockPos) as TileEntityWorkstation : null;
        if (player == null || workstation == null) { reason = "workstation unloaded or requester missing"; return false; }
        if (!workstation.IsPlayerPlaced)
        {
            if (exactOwner) { reason = "exact owner required"; return false; }
            return true;
        }
        if (!exactOwner && RebirthBlockPickupAccessService.IsAdminOrEditor(world, player)) return true;
        lock (Sync)
        {
            SecurityState state;
            if (!States.TryGetValue(blockPos, out state) || state == null || !state.ProtocolSupported)
            { reason = "workstation access state unavailable or incompatible"; return false; }
            if (Allows(state, userId, exactOwner)) return true;
        }
        reason = exactOwner ? "exact workstation owner required" : "workstation access denied by owner/allow-list policy";
        return false;
    }

    public static void RequestStateIfNeeded(
        WorldBase world,
        Vector3i blockPos,
        EntityAlive focusingEntity)
    {
        var player = focusingEntity as EntityPlayerLocal;
        var game = GameManager.Instance;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (serverWorld || world == null || !world.IsRemote() || player == null ||
            game == null || !ReferenceEquals(game.World, world) ||
            !ReferenceEquals(player.world, world)) return;

        // Avoid identity lookup and pooled-packet creation on every focus refresh
        // once the state is known or a successful request is still throttled.
        lock (Sync)
        {
            float next;
            if (States.ContainsKey(blockPos) ||
                (NextStateRequestTime.TryGetValue(blockPos, out next) && Time.realtimeSinceStartup < next)) return;
        }

        PersistentPlayerData persistent = game.GetPersistentPlayerList()
            ?.GetPlayerDataFromEntityID(player.entityId);
        // The local persistent record can be ready before the entity-indexed list
        // during remote join. Never use it for another player's request.
        if (persistent?.PrimaryId == null && ReferenceEquals(game.World.GetPrimaryPlayer(), player))
            persistent = game.GetPersistentLocalPlayer();
        if (persistent?.PrimaryId == null) return;

        NetPackageRebirthWorkstationOwnerRequest package = NetPackageManager
            .GetPackage<NetPackageRebirthWorkstationOwnerRequest>();
        if (!LogisticsTransferService.ClientChannelReady(connection, package)) return;

        lock (Sync)
        {
            if (States.ContainsKey(blockPos)) return;
            float now = Time.realtimeSinceStartup;
            float next;
            if (NextStateRequestTime.TryGetValue(blockPos, out next) && now < next) return;
            NextStateRequestTime[blockPos] = now + 3f;
        }

        package.Setup(blockPos, player.entityId, persistent.PrimaryId);
        connection.SendToServer(package);
    }

    public static void ProcessOwnerRequest(
        World world,
        Vector3i blockPos,
        int playerId,
        PlatformUserIdentifierAbs requestedUserId)
    {
        EntityPlayer player;
        PersistentPlayerData persistent;
        TileEntityWorkstation workstation;
        if (!ValidateServerPlayerAndWorkstation(
                world, blockPos, playerId, requestedUserId,
                out player, out persistent, out workstation))
            return;

        SecurityState state = GetOrMigrateState(
            world, blockPos, workstation, persistent, true);
        SendState(blockPos, state, playerId);
    }

    public static void ReceiveOwnerSync(
        Vector3i blockPos, string ownerCombined, bool locked, string publicMetadata, string[] allowedUsers)
    {
        string epoch, replyId;
        bool hasPassword, resetRequired;
        int result;
        bool supported = RebirthWorkstationCredentials.TryReadPublicState(
            publicMetadata, out epoch, out hasPassword, out resetRequired, out replyId, out result);
        lock (Sync)
        {
            // Never let a remote projection overwrite authority state.
            if (serverWorld) return;
            NextStateRequestTime.Remove(blockPos);
            if (!supported && CompatibilityWarnings.Add("server-protocol"))
                QueueNotice(Localization.Get("xuiRebirthWorkstationProtocolDisabled"));
            if (supported && !string.IsNullOrEmpty(replyId) && PendingReplies.Remove(replyId))
                QueueNotice(ResultMessage(result));
            PlatformUserIdentifierAbs owner;
            if (!TryParseUser(ownerCombined, out owner))
            {
                States.Remove(blockPos);
                AccessChanged(blockPos);
                NextStateRequestTime[blockPos] = Time.realtimeSinceStartup + 5f;
                return;
            }
            SecurityState state = new SecurityState
            {
                Owner = ownerCombined,
                Locked = supported ? locked : true,
                PasswordPresent = hasPassword,
                PasswordResetRequired = resetRequired,
                ProtocolSupported = supported,
                Epoch = epoch,
                PasswordVerifier = string.Empty
            };
            if (supported && allowedUsers != null)
                for (int i = 0; i < allowedUsers.Length; i++)
                {
                    PlatformUserIdentifierAbs user;
                    if (TryParseUser(allowedUsers[i], out user) && !owner.Equals(user)) state.AllowedUsers.Add(allowedUsers[i]);
                }
            States[blockPos] = state;
            AccessChanged(blockPos);
        }
    }

    public static bool ProcessSecurityAction(
        World world, Vector3i blockPos, int playerId, PlatformUserIdentifierAbs requestedUserId,
        RebirthWorkstationSecurityAction action, string payload)
    {
        EntityPlayer player;
        PersistentPlayerData persistent;
        TileEntityWorkstation workstation;
        if (!serverWorld || world == null || world.IsRemote() ||
            !ValidateServerPlayerAndWorkstation(world, blockPos, playerId, requestedUserId,
                out player, out persistent, out workstation)) return false;
        SecurityState state = GetOrMigrateState(world, blockPos, workstation, persistent, true);
        if (state == null) return false;
        string epoch, requestId, credential;
        if (!RebirthWorkstationCredentials.TryReadRequest(payload, (byte)action, out epoch, out requestId, out credential))
        {
            lock (Sync)
                if (CompatibilityWarnings.Count < 1024 && CompatibilityWarnings.Add(persistent.PrimaryId.CombinedString))
                    Log.Warning("[REBIRTH Workstation] Rejected unsupported credential protocol; matched server/client update required.");
            SendState(blockPos, state, playerId, string.Empty, 6);
            return false;
        }
        int failure = 0;
        lock (Sync)
        {
            if (!string.Equals(epoch, state.Epoch, StringComparison.Ordinal)) failure = 3;
            else if (!Attempts.Begin(persistent.PrimaryId.CombinedString, requestId, Time.realtimeSinceStartup,
                action == RebirthWorkstationSecurityAction.TryPassword || action == RebirthWorkstationSecurityAction.SetPassword)) failure = 4;
        }
        if (failure != 0) { SendState(blockPos, state, playerId, requestId, failure); return false; }

        bool admin = RebirthBlockPickupAccessService.IsAdminOrEditor(world, player);
        bool owner = string.Equals(state.Owner, persistent.PrimaryId.CombinedString, StringComparison.Ordinal);
        bool changed = false, accepted = false;
        int result = 2;
        switch (action)
        {
            case RebirthWorkstationSecurityAction.Lock:
            case RebirthWorkstationSecurityAction.Unlock:
                if (owner || admin)
                {
                    accepted = true;
                    bool desired = action == RebirthWorkstationSecurityAction.Lock;
                    changed = state.Locked != desired;
                    state.Locked = desired;
                }
                break;
            case RebirthWorkstationSecurityAction.SetPassword:
                if (owner || admin)
                {
                    // A deliberate reset always revokes previous grants, even for the same password.
                    state.PasswordVerifier = RebirthWorkstationCredentials.CreateVerifier(credential);
                    state.PasswordPresent = !string.IsNullOrEmpty(state.PasswordVerifier);
                    state.PasswordResetRequired = false;
                    state.AllowedUsers.Clear();
                    changed = accepted = true;
                }
                break;
            case RebirthWorkstationSecurityAction.TryPassword:
                if (state.PasswordResetRequired) result = 5;
                else if (owner || RebirthWorkstationCredentials.Verify(state.PasswordVerifier, credential))
                {
                    accepted = true;
                    if (!owner) changed = state.AllowedUsers.Add(persistent.PrimaryId.CombinedString);
                }
                break;
        }
        // Crypto runs outside the state lock. Recheck the precise generation before publishing.
        lock (Sync)
        {
            SecurityState current = null;
            if (!serverWorld || !States.TryGetValue(blockPos, out current) || current.Epoch != epoch)
            {
                accepted = changed = false; result = 3;
                state = current;
            }
            else if (changed)
            {
                state.Epoch = Guid.NewGuid().ToString("N");
                SetState(blockPos, state, true);
            }
        }
        if (changed) BroadcastState(blockPos, state);
        SendState(blockPos, state, playerId, requestId, accepted ? 1 : result);
        return accepted;
    }

    private static string ResultMessage(int result)
    {
        switch (result)
        {
            case 1: return Localization.Get("xuiRebirthWorkstationRequestAccepted");
            case 3: return Localization.Get("xuiRebirthWorkstationStateChanged");
            case 4: return Localization.Get("xuiRebirthWorkstationRequestLimited");
            case 5: return Localization.Get("xuiRebirthWorkstationPasswordResetRequired");
            case 6: return Localization.Get("xuiRebirthWorkstationProtocolMismatch");
            default: return Localization.Get("xuiRebirthWorkstationRequestDenied");
        }
    }

    // Called with Sync held; messages contain no credential/input text.
    private static void QueueNotice(string text)
    {
        if (Notices.Count < 32) Notices.Enqueue(text);
    }

    public static void PumpNotifications()
    {
        // ModEvents.GameUpdate owns UI interaction; packet receipt only queues data.
        EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
        Vector3i[] changed = null;
        bool authority;
        lock (Sync)
        {
            authority = serverWorld;
            if (PendingAccessChanges.Count != 0)
            {
                changed = new Vector3i[PendingAccessChanges.Count];
                PendingAccessChanges.CopyTo(changed);
                PendingAccessChanges.Clear();
            }
        }
        if (changed != null)
        {
            if (authority)
                for (int i = 0; i < changed.Length; i++)
                    RemoteResourceLiveSync.NotifySourceChanged(RemoteResourceIdentity.Workstation(changed[i]), changed[i].ToVector3(), "workstation access changed");
            else
            {
                RemoteResourceClientAvailability.Clear();
                if (player != null) RemoteResourceClientAvailability.InvalidateAndRequest(player);
            }
        }
        if (player == null) return; // Dedicated authority has still flushed the access changes above.
        string notice = null;
        lock (Sync)
        {
            if (PendingReplies.Count != 0)
            {
                float now = Time.realtimeSinceStartup;
                List<string> expired = null;
                foreach (var pair in PendingReplies)
                {
                    if (now < pair.Value) continue;
                    if (expired == null) expired = new List<string>();
                    expired.Add(pair.Key);
                }
                if (expired != null)
                {
                    foreach (string id in expired) PendingReplies.Remove(id);
                    QueueNotice(Localization.Get("xuiRebirthWorkstationReplyMissing"));
                }
            }
            if (Notices.Count != 0) notice = Notices.Dequeue();
        }
        if (notice != null) GameManager.ShowTooltip(player, notice);
    }

    public static bool IsCompletelyEmpty(TileEntityWorkstation workstation)
    {
        return workstation != null && workstation.IsEmpty &&
            !workstation.IsCrafting && !workstation.IsBurning;
    }

    public static bool CanOpen(WorldBase world, Vector3i blockPos, EntityPlayer player, PlatformUserIdentifierAbs userId)
    {
        string reason;
        return CanAccessWorkstation(world, blockPos, player, userId, false,
            RebirthSecureAccessPurpose.DirectInteraction, out reason);
    }

    public static bool CanLockOnServer(
        World world,
        Vector3i blockPos,
        int lockingPlayerId)
    {
        if (world == null || GameManager.Instance == null)
            return false;

        EntityPlayer player = world.GetEntity(lockingPlayerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()
            ?.GetPlayerDataFromEntityID(lockingPlayerId);
        return player != null && persistent != null && persistent.PrimaryId != null &&
            CanOpen(world, blockPos, player, persistent.PrimaryId);
    }

    public static bool CanPickup(
        WorldBase world,
        Vector3i blockPos,
        TileEntityWorkstation workstation,
        PersistentPlayerData requestingPlayer,
        EntityPlayer player,
        bool allowMigration,
        out string denial)
    {
        denial = string.Empty;
        if (workstation == null || !workstation.IsPlayerPlaced)
        {
            denial = Localization.Get("xuiRebirthWorkstationPlayerPlacedOnly");
            return false;
        }

        if (!IsCompletelyEmpty(workstation))
        {
            denial = Localization.Get("ttWorkstationNotEmpty");
            return false;
        }

        PlatformUserIdentifierAbs userId = requestingPlayer != null
            ? requestingPlayer.PrimaryId
            : null;
        if (userId == null)
        {
            denial = Localization.Get("xuiRebirthPickupOwnerDenied");
            return false;
        }

        SecurityState state;
        lock (Sync)
            States.TryGetValue(blockPos, out state);

        if (state == null && allowMigration && serverWorld && world is World)
            state = GetOrMigrateState((World)world, blockPos, workstation, requestingPlayer, true);

        if (state == null)
        {
            if (RebirthBlockPickupAccessService.IsAdminOrEditor(world, player))
                return true;
            if (!serverWorld)
                RequestStateIfNeeded(world, blockPos, player);
            denial = Localization.Get("xuiRebirthWorkstationOwnerMissing");
            return false;
        }

        if (!state.ProtocolSupported)
        {
            denial = Localization.Get("xuiRebirthWorkstationProtocolDenied");
            return false;
        }
        if (state.Locked)
        {
            denial = Localization.Get("xuiRebirthPickupUnlockFirst");
            return false;
        }

        if (RebirthBlockPickupAccessService.IsAdminOrEditor(world, player))
            return true;

        string combined = userId.CombinedString;
        if (string.Equals(state.Owner, combined, StringComparison.Ordinal) ||
            state.AllowedUsers.Contains(combined))
            return true;

        denial = Localization.Get("xuiRebirthPickupOwnerDenied");
        return false;
    }

    public static BlockActivationCommand[] ConfigureCommands(
        BlockActivationCommand[] source,
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        EntityAlive focusingEntity)
    {
        if (source == null || world == null || blockValue.Block == null)
            return source;

        RequestStateIfNeeded(world, blockPos, focusingEntity);

        int blockId = blockValue.type;
        CommandCacheEntry entry;
        if (!CommandCache.TryGetValue(blockId, out entry) || entry == null ||
            !ReferenceEquals(entry.Source, source) || entry.SourceLength != source.Length)
        {
            entry = new CommandCacheEntry
            {
                Source = source,
                SourceLength = source.Length,
                Result = new BlockActivationCommand[source.Length + 3]
            };
            entry.Result[source.Length] = new BlockActivationCommand("lock", "lock", false);
            entry.Result[source.Length + 1] = new BlockActivationCommand("unlock", "unlock", false);
            entry.Result[source.Length + 2] = new BlockActivationCommand("keypad", "keypad", false);
            CommandCache[blockId] = entry;
        }

        Array.Copy(source, entry.Result, source.Length);

        EntityPlayer player = focusingEntity as EntityPlayer;
        PersistentPlayerData persistent = player != null && GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)
            : GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        // Match remote ownership requests: the exact local player may have a
        // persistent identity before the entity-indexed list is populated.
        var game = GameManager.Instance;
        if (persistent?.PrimaryId == null && focusingEntity is EntityPlayerLocal &&
            world.IsRemote() && game != null && ReferenceEquals(game.World, world) &&
            ReferenceEquals(player.world, world) && ReferenceEquals(game.World.GetPrimaryPlayer(), player))
            persistent = game.GetPersistentLocalPlayer();
        PlatformUserIdentifierAbs userId = persistent != null ? persistent.PrimaryId : null;
        bool local = focusingEntity is EntityPlayerLocal;
        bool admin = RebirthBlockPickupAccessService.IsAdminOrEditor(world, player);
        bool owner = IsOwner(blockPos, userId);
        bool allowed = IsUserAllowed(blockPos, userId);
        bool locked = IsLocked(blockPos);
        bool hasPassword = HasPassword(blockPos);

        SetCommandEnabled(entry.Result, "open",
            local && CanOpen(world, blockPos, player, userId));

        TileEntityWorkstation workstation = world.GetTileEntity(blockPos) as TileEntityWorkstation;
        string denial;
        SetCommandEnabled(entry.Result, RebirthBlockPickupService.CommandName,
            local && CanPickup(world, blockPos, workstation, persistent, player, false, out denial));

        entry.Result[source.Length].enabled = local && (owner || admin) && !locked;
        entry.Result[source.Length + 1].enabled = local && (owner || admin) && locked;
        entry.Result[source.Length + 2].enabled = local &&
            (owner || (locked && hasPassword && !allowed));
        return entry.Result;
    }

    public static bool TryHandleActivation(
        string commandName,
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        EntityPlayerLocal player)
    {
        if (world == null || player == null || !(blockValue.Block is BlockWorkstation))
            return false;

        RequestStateIfNeeded(world, blockPos, player);
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        PlatformUserIdentifierAbs userId = persistent != null
            ? persistent.PrimaryId
            : PlatformManager.InternalLocalUserIdentifier;

        if (string.Equals(commandName, "open", StringComparison.OrdinalIgnoreCase))
        {
            if (CanOpen(world, blockPos, player, userId))
                return false;
            GameManager.ShowTooltip(player, Localization.Get("xuiRebirthWorkstationLockedDenied"), string.Empty, "ui_denied");
            return true;
        }

        if (string.Equals(commandName, "lock", StringComparison.OrdinalIgnoreCase))
        {
            SubmitAction(blockPos, player, RebirthWorkstationSecurityAction.Lock, string.Empty);
            return true;
        }

        if (string.Equals(commandName, "unlock", StringComparison.OrdinalIgnoreCase))
        {
            SubmitAction(blockPos, player, RebirthWorkstationSecurityAction.Unlock, string.Empty);
            return true;
        }

        if (string.Equals(commandName, "keypad", StringComparison.OrdinalIgnoreCase))
        {
            LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(player);
            if ((UnityEngine.Object)ui != (UnityEngine.Object)null)
            {
                XUiC_KeypadWindow.Open(
                    ui,
                    new RebirthWorkstationLockable(blockPos, player),
                    null,
                    delegate { return IsInteractionValid(world, blockPos, player); });
            }
            return true;
        }

        return false;
    }

    public static bool SetPasswordHash(Vector3i blockPos, EntityPlayerLocal player, string passwordHash, PlatformUserIdentifierAbs userId)
    {
        if (!IsOwner(blockPos, userId)) return false;
        return SubmitAction(blockPos, player, RebirthWorkstationSecurityAction.SetPassword, passwordHash);
    }

    public static bool CheckPasswordHash(Vector3i blockPos, EntityPlayerLocal player, string passwordHash, PlatformUserIdentifierAbs userId)
    {
        if (IsOwner(blockPos, userId) || IsUserAllowed(blockPos, userId)) return true;
        if (!HasPassword(blockPos)) return false;
        // Remote true means submission only (the native keypad has no asynchronous result API).
        // No optimistic grant is made. Native open/withdrawal still revalidates on the server.
        return SubmitAction(blockPos, player, RebirthWorkstationSecurityAction.TryPassword, passwordHash);
    }

    public static void ApplyActivationText(
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        EntityAlive focusingEntity,
        ref string activationText)
    {
        if (world == null || blockValue.Block == null)
            return;

        TileEntityWorkstation workstation = world.GetTileEntity(blockPos) as TileEntityWorkstation;
        if (workstation == null || !workstation.IsPlayerPlaced)
            return;

        RequestStateIfNeeded(world, blockPos, focusingEntity);
        string name = blockValue.Block.GetLocalizedBlockName();
        string remainder = string.Empty;
        if (!string.IsNullOrEmpty(activationText))
        {
            int newline = activationText.IndexOf('\n');
            if (newline >= 0 && newline + 1 < activationText.Length)
                remainder = activationText.Substring(newline + 1);
        }

        activationText = name;
        PlatformUserIdentifierAbs owner;
        if (TryGetOwner(blockPos, out owner))
        {
            string ownerName = RebirthBlockPickupAccessService.GetOwnerDisplayName(owner);
            if (!string.IsNullOrEmpty(ownerName))
                activationText += "\n" + Localization.Get("xuiRebirthOwnedBy") +
                    ": [00FF00]" + ownerName + "[-]";
        }
        if (!string.IsNullOrEmpty(remainder))
            activationText += "\n" + remainder;
    }

    public static void Save()
    {
        KeyValuePair<Vector3i, SecurityState>[] snapshot;
        lock (Sync)
        {
            if (!serverWorld || !dirty)
                return;

            snapshot = new KeyValuePair<Vector3i, SecurityState>[States.Count];
            int index = 0;
            foreach (KeyValuePair<Vector3i, SecurityState> pair in States)
                snapshot[index++] = new KeyValuePair<Vector3i, SecurityState>(pair.Key, pair.Value.Clone());
            dirty = false;
        }

        string path = GetPath();
        if (string.IsNullOrEmpty(path))
        {
            lock (Sync)
                dirty = true;
            return;
        }

        try
        {
            string backup = path + ".bak";
            if (SdFile.Exists(path))
                SdFile.Copy(path, backup, true);

            using (Stream stream = SdFile.Create(path))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Signature);
                writer.Write(FileVersion);
                writer.Write(snapshot.Length);
                for (int i = 0; i < snapshot.Length; i++)
                {
                    SecurityState state = snapshot[i].Value;
                    writer.Write(snapshot[i].Key.x);
                    writer.Write(snapshot[i].Key.y);
                    writer.Write(snapshot[i].Key.z);
                    writer.Write(state.Owner ?? string.Empty);
                    writer.Write(state.Locked);
                    writer.Write(state.PasswordVerifier ?? string.Empty);
                    writer.Write(state.AllowedUsers.Count);
                    foreach (string user in state.AllowedUsers)
                        writer.Write(user ?? string.Empty);
                    writer.Write(state.StationType ?? string.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            lock (Sync)
                dirty = true;
            Log.Error("[REBIRTH BlockPickup] Failed to save workstation security: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void Load()
    {
        string path = GetPath();
        if (string.IsNullOrEmpty(path) || !SdFile.Exists(path))
            return;

        try
        {
            LoadFile(path);
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH BlockPickup] Failed to load workstation security: " +
                ex.GetType().Name + ": " + ex.Message);
            string backup = path + ".bak";
            if (!SdFile.Exists(backup))
                return;
            try { LoadFile(backup); }
            catch (Exception backupEx)
            {
                Log.Error("[REBIRTH BlockPickup] Failed to load workstation security backup: " +
                    backupEx.GetType().Name + ": " + backupEx.Message);
            }
        }
    }

    private static void LoadFile(string path)
    {
        Dictionary<Vector3i, SecurityState> loaded =
            new Dictionary<Vector3i, SecurityState>();
        using (Stream stream = SdFile.OpenRead(path))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            if (reader.ReadInt32() != Signature)
                throw new InvalidDataException("Invalid signature.");
            byte version = reader.ReadByte();
            if (version < 1 || version > FileVersion)
                throw new InvalidDataException("Unsupported version.");

            int count = reader.ReadInt32();
            if (count < 0 || count > 1000000)
                throw new InvalidDataException("Invalid workstation count.");

            for (int i = 0; i < count; i++)
            {
                Vector3i pos = new Vector3i(
                    reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                string owner = reader.ReadString();
                bool locked = false;
                string passwordHash = string.Empty;
                List<string> serializedUsers = null;
                if (version >= 2)
                {
                    locked = reader.ReadBoolean();
                    passwordHash = reader.ReadString();
                    int userCount = reader.ReadInt32();
                    if (userCount < 0 || userCount > 1024)
                        throw new InvalidDataException("Invalid access-list count.");
                    serializedUsers = new List<string>(userCount);
                    for (int j = 0; j < userCount; j++)
                        serializedUsers.Add(reader.ReadString());
                }

                string stationType=version>=3?reader.ReadString():string.Empty;
                PlatformUserIdentifierAbs parsedOwner;
                if (!TryParseUser(owner, out parsedOwner))
                    continue;

                SecurityState state = new SecurityState
                {
                    Owner = owner,
                    StationType = stationType,
                    Locked = locked,
                    PasswordVerifier = string.IsNullOrEmpty(passwordHash) ? string.Empty :
                        (version >= 4 && RebirthWorkstationCredentials.IsCurrentVerifier(passwordHash) ? passwordHash : RebirthWorkstationCredentials.ResetRequired),
                    PasswordPresent = !string.IsNullOrEmpty(passwordHash),
                    PasswordResetRequired = !string.IsNullOrEmpty(passwordHash) &&
                        !(version >= 4 && RebirthWorkstationCredentials.IsCurrentVerifier(passwordHash))
                };
                // Pre-v4 verifiers were public bearer credentials. Existing non-owner grants
                // cannot be distinguished from grants obtained with a disclosed value.
                if (serializedUsers != null && !state.PasswordResetRequired)
                {
                    for (int j = 0; j < serializedUsers.Count; j++)
                    {
                        string user = serializedUsers[j];
                        PlatformUserIdentifierAbs parsedUser;
                        if (TryParseUser(user, out parsedUser) && !parsedOwner.Equals(parsedUser))
                            state.AllowedUsers.Add(user);
                    }
                }
                loaded[pos] = state;
            }
        }

        lock (Sync)
        {
            States.Clear();
            foreach (KeyValuePair<Vector3i, SecurityState> pair in loaded)
                States[pair.Key] = pair.Value;
            System.Threading.Interlocked.Increment(ref accessRevision);
            dirty = false;
            foreach (var pair in loaded) if (pair.Value.PasswordResetRequired) { dirty = true; break; }
        }
        { if (RebirthLogSettings.BlockPickupLoggingEnabled) Log.Out("[REBIRTH BlockPickup] Loaded " + loaded.Count +
            " secured workstation records."); }
    }

    private static bool ValidateServerPlayerAndWorkstation(
        World world,
        Vector3i blockPos,
        int playerId,
        PlatformUserIdentifierAbs requestedUserId,
        out EntityPlayer player,
        out PersistentPlayerData persistent,
        out TileEntityWorkstation workstation)
    {
        player = null;
        persistent = null;
        workstation = null;
        if (world == null || requestedUserId == null || GameManager.Instance == null)
            return false;

        player = world.GetEntity(playerId) as EntityPlayer;
        persistent = GameManager.Instance.GetPersistentPlayerList()
            ?.GetPlayerDataFromEntityID(playerId);
        if (player == null || persistent == null || persistent.PrimaryId == null ||
            !persistent.PrimaryId.Equals(requestedUserId))
            return false;

        Vector3 center = blockPos.ToVector3() + Vector3.one * 0.5f;
        if ((player.position - center).sqrMagnitude >
            MaximumInteractionDistance * MaximumInteractionDistance)
            return false;

        BlockValue blockValue = world.GetBlock(blockPos);
        workstation = world.GetTileEntity(blockPos) as TileEntityWorkstation;
        return blockValue.Block is BlockWorkstation && workstation != null &&
            workstation.IsPlayerPlaced;
    }

    private static SecurityState GetOrMigrateState(
        World world,
        Vector3i blockPos,
        TileEntityWorkstation workstation,
        PersistentPlayerData requestingPlayer,
        bool requireLandClaimForMigration)
    {
        lock (Sync)
        {
            SecurityState existing;
            if (States.TryGetValue(blockPos, out existing) && existing != null)
                return existing.Clone();
        }

        if (world == null || workstation == null || !workstation.IsPlayerPlaced ||
            requestingPlayer == null || requestingPlayer.PrimaryId == null)
            return null;

        if (requireLandClaimForMigration &&
            world.GetLandClaimOwner(blockPos, requestingPlayer) != EnumLandClaimOwner.Self)
            return null;

        SecurityState migrated = new SecurityState
        {
            Owner = requestingPlayer.PrimaryId.CombinedString,
            Locked = false,
            PasswordVerifier = string.Empty
        };
        SetState(blockPos, migrated, true);
        return migrated;
    }

    private static bool SubmitAction(Vector3i blockPos, EntityPlayerLocal player, RebirthWorkstationSecurityAction action, string payload)
    {
        if (player == null || GameManager.Instance == null) return false;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentLocalPlayer();
        if (persistent == null || persistent.PrimaryId == null) return false;
        World world = GameManager.Instance.World;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (world == null || connection == null || !ReferenceEquals(player.world, world) ||
            (world.IsRemote() ? connection.IsServer : !connection.IsServer)) return false;
        SecurityState state;
        lock (Sync) States.TryGetValue(blockPos, out state);
        if (state == null && connection.IsServer)
            state = GetOrMigrateState(world, blockPos, world.GetTileEntity(blockPos) as TileEntityWorkstation, persistent, true);
        if (state == null || !state.ProtocolSupported || !RebirthWorkstationCredentials.IsToken(state.Epoch))
        {
            RequestStateIfNeeded(world, blockPos, player);
            lock (Sync) QueueNotice(Localization.Get("xuiRebirthWorkstationStateUnavailable"));
            return false;
        }
        string id = Guid.NewGuid().ToString("N");
        string wire = RebirthWorkstationCredentials.Request(state.Epoch, id, (byte)action, payload);
        string ignoredEpoch, ignoredId, ignoredCredential;
        if (!RebirthWorkstationCredentials.TryReadRequest(wire, (byte)action, out ignoredEpoch, out ignoredId, out ignoredCredential)) return false;
        if (connection.IsServer)
        {
            bool accepted = ProcessSecurityAction(world, blockPos, player.entityId, persistent.PrimaryId, action, wire);
            lock (Sync) QueueNotice(accepted ? ResultMessage(1) : Localization.Get("xuiRebirthWorkstationHostDenied"));
            return accepted;
        }
        NetPackageRebirthWorkstationSecurityAction package = NetPackageManager.GetPackage<NetPackageRebirthWorkstationSecurityAction>()
            .Setup(blockPos, player.entityId, persistent.PrimaryId, action, wire);
        if (!LogisticsTransferService.ClientChannelReady(connection, package))
        {
            lock (Sync) QueueNotice(Localization.Get("xuiRebirthWorkstationConnectionUnavailable"));
            return false;
        }
        lock (Sync)
        {
            if (PendingReplies.Count >= 16) return false;
            PendingReplies[id] = Time.realtimeSinceStartup + 15f;
            QueueNotice(Localization.Get("xuiRebirthWorkstationRequestPending"));
        }
        connection.SendToServer(package);
        return true;
    }

    private static bool IsInteractionValid(
        WorldBase world,
        Vector3i blockPos,
        EntityPlayerLocal player)
    {
        if (world == null || player == null ||
            !(world.GetBlock(blockPos).Block is BlockWorkstation))
            return false;
        Vector3 center = blockPos.ToVector3() + Vector3.one * 0.5f;
        return (player.position - center).sqrMagnitude <=
            MaximumInteractionDistance * MaximumInteractionDistance;
    }

    private static void SetCommandEnabled(
        BlockActivationCommand[] commands,
        string commandName,
        bool enabled)
    {
        for (int i = 0; i < commands.Length; i++)
        {
            if (string.Equals(commands[i].text, commandName, StringComparison.OrdinalIgnoreCase))
                commands[i].enabled = enabled;
        }
    }

    private static void SetState(Vector3i blockPos, SecurityState state, bool markDirty)
    {
        if (state == null)
            return;
        lock (Sync)
        {
            States[blockPos] = state.Clone();
            AccessChanged(blockPos);
            NextStateRequestTime.Remove(blockPos);
            if (serverWorld && markDirty)
                dirty = true;
        }
    }

    private static void SendState(Vector3i blockPos, SecurityState state, int playerId, string replyId = "", int result = 0)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!serverWorld || connection == null)
            return;

        NetPackageRebirthWorkstationOwnerSync response = BuildStatePackage(blockPos, state, replyId, result);
        connection.SendPackage(response, _attachedToEntityId: playerId);
    }

    private static void BroadcastState(Vector3i blockPos, SecurityState state)
    {
        if (!serverWorld || SingletonMonoBehaviour<ConnectionManager>.Instance == null)
            return;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            BuildStatePackage(blockPos, state));
    }

    private static NetPackageRebirthWorkstationOwnerSync BuildStatePackage(
        Vector3i blockPos,
        SecurityState state, string replyId = "", int result = 0)
    {
        string[] users = state != null
            ? new List<string>(state.AllowedUsers).ToArray()
            : new string[0];
        return NetPackageManager.GetPackage<NetPackageRebirthWorkstationOwnerSync>()
            .Setup(
                blockPos,
                state != null ? state.Owner : string.Empty,
                state != null && state.Locked,
                RebirthWorkstationCredentials.PublicState(
                    state != null ? state.Epoch : Guid.Empty.ToString("N"),
                    state != null && !string.IsNullOrEmpty(state.PasswordVerifier),
                    state != null && state.PasswordResetRequired, replyId, result),
                users);
    }

    private static bool TryParseUser(
        string combined,
        out PlatformUserIdentifierAbs user)
    {
        user = null;
        return !string.IsNullOrEmpty(combined) &&
            PlatformUserIdentifierAbs.TryFromCombinedString(combined, out user) &&
            user != null;
    }

    private static string GetPath()
    {
        string directory = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(directory)
            ? string.Empty
            : Path.Combine(directory, FileName);
    }
}
