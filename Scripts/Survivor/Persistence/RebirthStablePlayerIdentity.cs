using Platform;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

#nullable disable

/// <summary>
/// Server-derived durable identity used by Survivor world persistence.
/// The canonical string is ClientInfo.InternalId.CombinedString; the filename key is SHA-256.
/// No network request may provide or override this value.
/// </summary>
public sealed class RebirthStablePlayerIdentity
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthStablePlayerIdentity> ByEntityId =
        new Dictionary<int, RebirthStablePlayerIdentity>();
    // Identity and filename derivation are pure. These bounded caches prevent
    // hot repository/authorization reads from allocating a SHA provider and
    // several transient buffers for the same server-owned account each time.
    private const int CanonicalCacheLimit = 1024;
    private static readonly Dictionary<string, RebirthStablePlayerIdentity> ByCanonical =
        new Dictionary<string, RebirthStablePlayerIdentity>(StringComparer.Ordinal);
    private static readonly Queue<string> CanonicalOrder = new Queue<string>();
    private static readonly Dictionary<string, string> StorageKeys =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Queue<string> StorageKeyOrder = new Queue<string>();

    public string CanonicalId { get; private set; }
    public string StorageKey { get; private set; }

    private RebirthStablePlayerIdentity(string canonicalId)
    {
        CanonicalId = canonicalId ?? string.Empty;
        StorageKey = ComputeStorageKey(CanonicalId);
    }

    public static void ResetRuntimeCache()
    {
        lock (Sync)
            ByEntityId.Clear();
    }

    private static RebirthStablePlayerIdentity FromCanonical(string canonical)
    {
        lock (Sync)
        {
            if (ByCanonical.TryGetValue(canonical, out var existing)) return existing;
            var identity = new RebirthStablePlayerIdentity(canonical);
            if (ByCanonical.Count >= CanonicalCacheLimit)
                ByCanonical.Remove(CanonicalOrder.Dequeue());
            ByCanonical.Add(canonical, identity);
            CanonicalOrder.Enqueue(canonical);
            return identity;
        }
    }

    // Server native persisted identity; never register a stale entity-id alias.
    internal static bool TryFromPersistentPlayer(PersistentPlayerData player,out RebirthStablePlayerIdentity identity)
    {
        identity=null;
        if(!ThreadManager.IsMainThread()||!RebirthWorldCharacterRepository.IsServerAuthority||player?.PrimaryId==null)return false;
        string canonical=(player.PrimaryId.CombinedString??string.Empty).Trim();
        if(canonical.Length==0)return false;
        identity=FromCanonical(canonical);return true;
    }

    public static bool TryFromClientInfo(ClientInfo clientInfo, out RebirthStablePlayerIdentity identity)
    {
        identity = null;
        if (clientInfo == null || clientInfo.InternalId == null)
            return false;

        string canonical = (clientInfo.InternalId.CombinedString ?? string.Empty).Trim();
        if (canonical.Length == 0)
            return false;

        identity = FromCanonical(canonical);
        if (clientInfo.entityId >= 0)
        {
            lock (Sync)
                ByEntityId[clientInfo.entityId] = identity;
        }
        return true;
    }

    public static void Remember(ClientInfo clientInfo)
    {
        RebirthStablePlayerIdentity ignored;
        TryFromClientInfo(clientInfo, out ignored);
    }

    /// <summary>
    /// Server/listen-server fallback for the primary local user before a ClientInfo/entity mapping
    /// exists. PlatformManager.InternalLocalUserIdentifier is server-owned platform state and is
    /// therefore safe to use as the same canonical identity source as ClientInfo.InternalId.
    /// </summary>
    internal static bool TryFromLocalPlatform(EntityPlayer player,out RebirthStablePlayerIdentity identity)
    {
        identity=null;
        return ThreadManager.IsMainThread()&&player is EntityPlayerLocal&&player.world!=null&&
            ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&
            TryFromLocalPlatform(out identity);
    }

    public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity)
    {
        identity = null;
        PlatformUserIdentifierAbs localId = PlatformManager.InternalLocalUserIdentifier;
        if (localId == null)
            return false;
        string canonical = (localId.CombinedString ?? string.Empty).Trim();
        if (canonical.Length == 0)
            return false;
        identity = FromCanonical(canonical);
        return true;
    }

    public static bool TryResolveServerEntity(EntityPlayer player, out RebirthStablePlayerIdentity identity)
    {
        identity = null;
        if (player == null)
            return false;

        lock (Sync)
        {
            if (ByEntityId.TryGetValue(player.entityId, out identity) && identity != null)
                return true;
        }

        // Compatibility fallback for an already server-owned EntityPlayer when a call occurs
        // before the spawn callback has populated the ClientInfo cache. PersistentPlayerData is
        // authoritative server state, not a client-provided identity string.
        if (GameManager.Instance == null)
            return false;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if (persistent != null && persistent.PrimaryId != null)
        {
            string canonical = (persistent.PrimaryId.CombinedString ?? string.Empty).Trim();
            if (canonical.Length > 0)
            {
                identity = FromCanonical(canonical);
                lock (Sync)
                    ByEntityId[player.entityId] = identity;
                return true;
            }
        }

        // Listen-server first-spawn edge: PlayerSpawnedInWorld can arrive without ClientInfo and
        // before PersistentPlayerData has been indexed. The primary local platform identifier is
        // already available and represents the same server-owned internal identity.
        if (player is EntityPlayerLocal && TryFromLocalPlatform(out identity))
        {
            lock (Sync)
                ByEntityId[player.entityId] = identity;
            return true;
        }
        return false;
    }

    public static string ComputeStorageKey(string canonicalId)
    {
        string value = canonicalId ?? string.Empty;
        lock (Sync)
            if (StorageKeys.TryGetValue(value, out var existing)) return existing;
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            StringBuilder result = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                result.Append(hash[i].ToString("x2"));
            string key = result.ToString();
            lock (Sync)
            {
                if (StorageKeys.TryGetValue(value, out var racedKey)) return racedKey;
                if (StorageKeys.Count >= CanonicalCacheLimit)
                    StorageKeys.Remove(StorageKeyOrder.Dequeue());
                StorageKeys.Add(value, key);
                StorageKeyOrder.Enqueue(value);
            }
            return key;
        }
    }

    public override string ToString()
    {
        return StorageKey;
    }
}
