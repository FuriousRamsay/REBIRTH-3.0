using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Server-authoritative REBIRTH group identity layered over the native Party system.
/// Native Party owns membership and leadership. This service owns only the approved
/// persistent Group Name / Group Color metadata and never replaces native party state.
/// </summary>
public static class RebirthPartyIdentityService
{
    private const int SchemaVersion = 1;
    public const int MaxGroupNameLength = 32;
    public const int ColorCount = 8;
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, PersonalRecord> Personal = new Dictionary<string, PersonalRecord>(StringComparer.Ordinal);
    private static readonly Dictionary<string, GroupRecord> Groups = new Dictionary<string, GroupRecord>(StringComparer.Ordinal);
    private static readonly Dictionary<int, string> RuntimePartyGroups = new Dictionary<int, string>();
    private static readonly Harmony HarmonyInstance = new Harmony("rebirth.party.identity.3.0");
    private static bool installed;
    private static bool serverAuthority;
    private static long revision = 1L;
    private static bool dirty;
    private static RebirthPartyIdentitySnapshot cachedSnapshot;
    private static long cachedSnapshotRevision = -1L;
    private static int cachedRosterSignature;

    private static bool RebirthModeActive { get { return RebirthSurvivorMode.IsEnabledForCurrentWorld(); } }

    private sealed class PersonalRecord
    {
        public string StableKey = string.Empty;
        public string Name = string.Empty;
        public int ColorId = 0;
        public long UpdatedRevision;
    }

    private sealed class GroupRecord
    {
        public string GroupId = string.Empty;
        public string Name = string.Empty;
        public int ColorId = 0;
        public string LeaderStableKey = string.Empty;
        public readonly HashSet<string> Members = new HashSet<string>(StringComparer.Ordinal);
        public long UpdatedRevision;
    }

    public static string RootDirectory
    {
        get
        {
            string save = GameIO.GetSaveGameDir();
            return string.IsNullOrEmpty(save) ? string.Empty : Path.Combine(save, "RebirthData", "PartyIdentity");
        }
    }

    private static string SavePath
    {
        get
        {
            string root = RootDirectory;
            if (string.IsNullOrEmpty(root)) return string.Empty;
            Directory.CreateDirectory(root);
            return Path.Combine(root, "groups.xml");
        }
    }

    public static string Install()
    {
        if (installed) return "[REBIRTH PartyIdentity] already installed";
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthPartyIdentityAddPlayerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthPartyIdentityRemovePlayerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthPartyIdentityKickPlayerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthPartyIdentitySetLeaderPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthPartyIdentityDisbandPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthPartyIdentityLeaveSettledPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthPartyIdentityDisconnectSettledPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthPartyIdentityPartyRemovedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthPartyIdentityWorldSavePatch));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.PlayerJoinedGame.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerJoinedGameData>(OnPlayerJoined));
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawned));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH PartyIdentity] installed schema=" + SchemaVersion + " colors=" + ColorCount;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        ResetRuntime(data.AsServer);
        if (serverAuthority && RebirthModeActive) Load();
        RebirthPartyIdentityClientState.Reset();
    }

    private static void OnPlayerJoined(ref ModEvents.SPlayerJoinedGameData data)
    {
        if (!serverAuthority || !RebirthModeActive || data.ClientInfo == null) return;
        RebirthStablePlayerIdentity.Remember(data.ClientInfo);
    }

    private static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!serverAuthority || !RebirthModeActive || data.ClientInfo == null) return;
        RebirthStablePlayerIdentity.Remember(data.ClientInfo);
        EntityPlayer player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetEntity(data.ClientInfo.entityId) as EntityPlayer
            : null;
        if (player != null)
        {
            EnsureResolved(player);
            SendSnapshotTo(player, true, "player-spawned");
        }
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        SaveIfDirty("world-shutdown");
        ResetRuntime(false);
        RebirthPartyIdentityClientState.Reset();
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        SaveIfDirty("game-shutdown");
        ResetRuntime(false);
        RebirthPartyIdentityClientState.Reset();
    }

    private static void ResetRuntime(bool asServer)
    {
        lock (Sync)
        {
            Personal.Clear();
            Groups.Clear();
            RuntimePartyGroups.Clear();
            revision = 1L;
            dirty = false;
            cachedSnapshot = null; cachedSnapshotRevision = -1L; cachedRosterSignature = 0;
        }
        serverAuthority = asServer;
    }

    public static string NormalizeName(string value)
    {
        string raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0) return string.Empty;
        char[] buffer = new char[Math.Min(raw.Length, MaxGroupNameLength * 2)];
        int count = 0;
        bool previousSpace = false;
        for (int i = 0; i < raw.Length && count < MaxGroupNameLength; ++i)
        {
            char c = raw[i];
            if (char.IsControl(c) || c == '[' || c == ']' || c == '<' || c == '>') continue;
            if (char.IsWhiteSpace(c))
            {
                if (previousSpace || count == 0) continue;
                c = ' ';
                previousSpace = true;
            }
            else previousSpace = false;
            buffer[count++] = c;
        }
        while (count > 0 && buffer[count - 1] == ' ') count--;
        return count == 0 ? string.Empty : new string(buffer, 0, count);
    }

    public static int NormalizeColor(int value)
    {
        if(value>=0x1000000 && value<=0x1ffffff)return value;
        if (value < 0) return 0;
        if (value >= ColorCount) return (byte)(ColorCount - 1);
        return (byte)value;
    }

    public static int EncodeColor(Color32 color) => 0x1000000 | (color.r<<16) | (color.g<<8) | color.b;

    public static Color32 GetColor(int colorId)
    {
        if(colorId>=0x1000000 && colorId<=0x1ffffff)return new Color32((byte)(colorId>>16),(byte)(colorId>>8),(byte)colorId,255);
        switch (NormalizeColor(colorId))
        {
            case 0: return new Color32(228, 18, 21, 255);       // REBIRTH red
            case 1: return new Color32(204, 167, 56, 255);      // amber
            case 2: return new Color32(66, 139, 190, 255);      // blue
            case 3: return new Color32(67, 144, 41, 255);       // green
            case 4: return new Color32(150, 94, 184, 255);      // purple
            case 5: return new Color32(52, 156, 151, 255);      // teal
            case 6: return new Color32(210, 119, 47, 255);      // orange
            default: return new Color32(185, 185, 185, 255);    // neutral
        }
    }

    public static bool ProcessServerRequest(EntityPlayer player, string requestedName, int requestedColor, out string message)
    {
        message = string.Empty;
        if (!serverAuthority || !RebirthModeActive || player == null)
        {
            message = "server authority unavailable";
            return false;
        }

        RebirthPartyIdentityView current;
        if (!TryResolve(player, out current) || current == null)
        {
            message = "group identity unavailable";
            return false;
        }
        if (!current.CanEdit)
        {
            message = "only the party leader can edit group identity";
            return false;
        }

        string name = NormalizeName(requestedName);
        int color = NormalizeColor(requestedColor);
        bool changed = false;
        RebirthStablePlayerIdentity identity;
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) || identity == null)
        {
            message = "stable player identity unavailable";
            return false;
        }

        lock (Sync)
        {
            PersonalRecord personal = GetOrCreatePersonalNoLock(identity.StorageKey);
            if (personal.Name != name || personal.ColorId != color)
            {
                personal.Name = name;
                personal.ColorId = color;
                personal.UpdatedRevision = NextRevisionNoLock();
                changed = true;
            }

            if (player.Party != null)
            {
                GroupRecord group = ResolvePartyNoLock(player.Party, true);
                if (group != null && (group.Name != name || group.ColorId != color))
                {
                    group.Name = name;
                    group.ColorId = color;
                    group.UpdatedRevision = NextRevisionNoLock();
                    changed = true;
                }
            }
            if (changed) dirty = true;
        }

        if (changed)
        {
            SaveIfDirty("group-edit");
            BroadcastSnapshot("group-edit");
            message = "group identity updated";
        }
        else
        {
            SendSnapshotTo(player, true, "group-edit-nochange");
            message = "group identity unchanged";
        }
        return true;
    }

    public static bool TryResolve(EntityPlayer player, out RebirthPartyIdentityView view)
    {
        view = null;
        if (player == null || !RebirthModeActive) return false;
        if (!serverAuthority)
            return RebirthPartyIdentityClientState.TryGet(player.entityId, out view);

        RebirthStablePlayerIdentity identity;
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) || identity == null) return false;
        lock (Sync)
        {
            if (player.Party == null)
            {
                PersonalRecord personal = GetOrCreatePersonalNoLock(identity.StorageKey);
                view = new RebirthPartyIdentityView
                {
                    EntityId = player.entityId,
                    GroupId = "solo:" + identity.StorageKey,
                    Name = personal.Name,
                    ColorId = personal.ColorId,
                    IsParty = false,
                    IsLeader = true,
                    CanEdit = true,
                    Revision = personal.UpdatedRevision
                };
                return true;
            }
            GroupRecord group = ResolvePartyNoLock(player.Party, true);
            if (group == null) return false;
            bool isLeader = player.Party.Leader == player;
            view = new RebirthPartyIdentityView
            {
                EntityId = player.entityId,
                GroupId = group.GroupId,
                Name = group.Name,
                ColorId = group.ColorId,
                IsParty = true,
                IsLeader = isLeader,
                CanEdit = isLeader,
                Revision = group.UpdatedRevision
            };
            return true;
        }
    }

    private static void EnsureResolved(EntityPlayer player)
    {
        RebirthPartyIdentityView ignored;
        TryResolve(player, out ignored);
    }

    private static PersonalRecord GetOrCreatePersonalNoLock(string stableKey)
    {
        PersonalRecord record;
        if (!Personal.TryGetValue(stableKey, out record) || record == null)
        {
            record = new PersonalRecord { StableKey = stableKey ?? string.Empty, ColorId = 0, UpdatedRevision = revision };
            Personal[record.StableKey] = record;
        }
        return record;
    }

    private static GroupRecord ResolvePartyNoLock(Party party, bool create)
    {
        if (party == null || party.MemberList == null || party.MemberList.Count == 0) return null;
        string groupId;
        GroupRecord record;
        if (RuntimePartyGroups.TryGetValue(party.PartyID, out groupId) && Groups.TryGetValue(groupId, out record) && record != null)
        {
            RefreshPartyRecordNoLock(record, party);
            return record;
        }

        EntityPlayer leader = party.Leader;
        if (leader == null && party.MemberList.Count > 0) leader = party.MemberList[0];
        RebirthStablePlayerIdentity leaderIdentity;
        string leaderKey = leader != null && RebirthStablePlayerIdentity.TryResolveServerEntity(leader, out leaderIdentity) && leaderIdentity != null
            ? leaderIdentity.StorageKey
            : string.Empty;

        // First choice: the most recently persisted group led by the current native leader.
        record = Groups.Values
            .Where(g => g != null && g.LeaderStableKey == leaderKey)
            .OrderByDescending(g => g.UpdatedRevision)
            .FirstOrDefault();

        // Defensive reload/reconnect reconciliation: use the strongest member overlap if
        // the previous leader is no longer available, but require at least two matches so
        // unrelated one-player history cannot silently hijack a new party.
        if (record == null)
        {
            HashSet<string> currentMembers = ResolveMemberKeysNoLock(party);
            int bestOverlap = 1;
            foreach (GroupRecord candidate in Groups.Values)
            {
                if (candidate == null) continue;
                int overlap = 0;
                foreach (string key in currentMembers) if (candidate.Members.Contains(key)) overlap++;
                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    record = candidate;
                }
            }
        }

        if (record == null && create)
        {
            PersonalRecord personal = !string.IsNullOrEmpty(leaderKey) ? GetOrCreatePersonalNoLock(leaderKey) : null;
            record = new GroupRecord
            {
                GroupId = Guid.NewGuid().ToString("N"),
                Name = personal != null ? personal.Name : string.Empty,
                ColorId = personal != null ? personal.ColorId : (byte)0,
                LeaderStableKey = leaderKey,
                UpdatedRevision = NextRevisionNoLock()
            };
            Groups[record.GroupId] = record;
            dirty = true;
        }
        if (record == null) return null;

        RuntimePartyGroups[party.PartyID] = record.GroupId;
        RefreshPartyRecordNoLock(record, party);
        return record;
    }

    private static HashSet<string> ResolveMemberKeysNoLock(Party party)
    {
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        if (party == null || party.MemberList == null) return keys;
        for (int i = 0; i < party.MemberList.Count; ++i)
        {
            RebirthStablePlayerIdentity id;
            if (party.MemberList[i] != null && RebirthStablePlayerIdentity.TryResolveServerEntity(party.MemberList[i], out id) && id != null)
                keys.Add(id.StorageKey);
        }
        return keys;
    }

    private static void RefreshPartyRecordNoLock(GroupRecord record, Party party)
    {
        if (record == null || party == null) return;
        string leaderKey = string.Empty;
        RebirthStablePlayerIdentity leaderIdentity;
        if (party.Leader != null && RebirthStablePlayerIdentity.TryResolveServerEntity(party.Leader, out leaderIdentity) && leaderIdentity != null)
            leaderKey = leaderIdentity.StorageKey;
        HashSet<string> members = ResolveMemberKeysNoLock(party);
        bool leaderChanged = record.LeaderStableKey != leaderKey;
        bool changed = leaderChanged || !record.Members.SetEquals(members);
        if (!changed) return;
        record.LeaderStableKey = leaderKey;
        record.Members.Clear();
        foreach (string key in members) record.Members.Add(key);
        record.UpdatedRevision = NextRevisionNoLock();
        if (leaderChanged && !string.IsNullOrEmpty(leaderKey))
        {
            // Ownership follows the verified native leader while the party record itself
            // remains stable. Carry the active identity into the new leader's solo seed so
            // disband/reform does not silently revert to unrelated personal metadata.
            PersonalRecord personal = GetOrCreatePersonalNoLock(leaderKey);
            personal.Name = record.Name;
            personal.ColorId = record.ColorId;
            personal.UpdatedRevision = revision;
        }
        dirty = true;
    }

    private static long NextRevisionNoLock()
    {
        revision = Math.Max(1L, revision + 1L);
        return revision;
    }

    internal static void OnPartyChanged(Party party, string reason)
    {
        if (!serverAuthority || !RebirthModeActive || party == null) return;
        lock (Sync)
        {
            if (party.MemberList == null || party.MemberList.Count == 0)
            {
                RuntimePartyGroups.Remove(party.PartyID);
            }
            else ResolvePartyNoLock(party, true);
        }
        SaveIfDirty(reason);
        BroadcastSnapshot(reason);
    }

    internal static void OnPartyRemoved(Party party, string reason)
    {
        if (!serverAuthority || !RebirthModeActive || party == null) return;
        lock (Sync) RuntimePartyGroups.Remove(party.PartyID);
        SaveIfDirty(reason);
        BroadcastSnapshot(reason);
    }

    public static RebirthPartyIdentitySnapshot BuildSnapshot()
    {
        RebirthPartyIdentitySnapshot snapshot = new RebirthPartyIdentitySnapshot { ProtocolVersion = RebirthPartyIdentityProtocol.Version };
        if (!RebirthModeActive) return snapshot;
        if (GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.Players == null || GameManager.Instance.World.Players.list == null)
            return snapshot;
        List<EntityPlayer> players = GameManager.Instance.World.Players.list;
        int rosterSignature = 17;
        for (int i = 0; i < players.Count; ++i)
            rosterSignature = unchecked(rosterSignature * 31 + (players[i] != null ? players[i].entityId : 0));
        lock (Sync)
        {
            if (cachedSnapshot != null && cachedSnapshotRevision == revision && cachedRosterSignature == rosterSignature)
                return cachedSnapshot.Clone();
        }
        for (int i = 0; i < players.Count; ++i)
        {
            EntityPlayer player = players[i];
            RebirthPartyIdentityView view;
            if (player != null && TryResolve(player, out view) && view != null)
                snapshot.Rows.Add(view);
        }
        lock (Sync)
        {
            snapshot.Revision = revision;
            cachedSnapshot = snapshot.Clone();
            cachedSnapshotRevision = revision;
            cachedRosterSignature = rosterSignature;
        }
        return snapshot;
    }

    public static void RequestSnapshot(EntityPlayerLocal player, bool force, string reason)
    {
        if (player == null) return;
        if (!RebirthModeActive) { RebirthPartyIdentityClientState.Reset(); return; }
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c == null) return;
        RebirthPartyIdentitySnapshot known = RebirthPartyIdentityClientState.GetSnapshot();
        long knownRevision = known != null ? known.Revision : 0L;
        if (c.IsServer) SendSnapshotTo(player, force, reason);
        else c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPartyIdentityRequest>()
            .SetupSnapshotRequest(player.entityId, knownRevision, force));
    }

    public static void SendSnapshotTo(EntityPlayer player, bool force, string reason)
    {
        if (!serverAuthority || !RebirthModeActive || player == null) return;
        RebirthPartyIdentitySnapshot snapshot = BuildSnapshot();
        RebirthPartyIdentitySnapshot existing = RebirthPartyIdentityClientState.GetSnapshot();
        if (!force && existing != null && existing.Revision >= snapshot.Revision && player is EntityPlayerLocal) return;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player is EntityPlayerLocal)
            RebirthPartyIdentityClientState.Receive(snapshot);
        if (c != null && c.IsServer && !(player is EntityPlayerLocal))
            c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPartyIdentitySnapshot>().Setup(snapshot), _attachedToEntityId: player.entityId);
    }

    public static void BroadcastSnapshot(string reason)
    {
        if (!serverAuthority || !RebirthModeActive) return;
        RebirthPartyIdentitySnapshot snapshot = BuildSnapshot();
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!GameManager.IsDedicatedServer) RebirthPartyIdentityClientState.Receive(snapshot);
        if (c != null && c.IsServer)
            c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPartyIdentitySnapshot>().Setup(snapshot));
    }

    public static bool SaveIfDirty(string reason)
    {
        if (!serverAuthority || !RebirthModeActive) return false;
        XDocument doc;
        lock (Sync)
        {
            if (!dirty) return false;
            doc = SerializeNoLock();
        }
        string path = SavePath;
        if (string.IsNullOrEmpty(path)) return false;
        string error;
        if (!RebirthAtomicXmlFile.TryWrite(path, doc, out error))
        {
            Log.Warning("[REBIRTH PartyIdentity] save failed reason=" + (reason ?? string.Empty) + " error=" + error);
            return false;
        }
        lock (Sync) dirty = false;
        return true;
    }

    private static XDocument SerializeNoLock()
    {
        XElement root = new XElement("rebirthPartyIdentity",
            new XAttribute("schema_version", SchemaVersion),
            new XAttribute("revision", revision));
        XElement personal = new XElement("personal");
        foreach (PersonalRecord p in Personal.Values.OrderBy(v => v.StableKey, StringComparer.Ordinal))
            personal.Add(new XElement("identity", new XAttribute("key", p.StableKey), new XAttribute("name", p.Name ?? string.Empty), new XAttribute("color", p.ColorId), new XAttribute("revision", p.UpdatedRevision)));
        root.Add(personal);
        XElement groups = new XElement("groups");
        foreach (GroupRecord g in Groups.Values.OrderBy(v => v.GroupId, StringComparer.Ordinal))
        {
            XElement e = new XElement("group", new XAttribute("id", g.GroupId), new XAttribute("name", g.Name ?? string.Empty), new XAttribute("color", g.ColorId), new XAttribute("leader", g.LeaderStableKey ?? string.Empty), new XAttribute("revision", g.UpdatedRevision));
            foreach (string member in g.Members.OrderBy(v => v, StringComparer.Ordinal)) e.Add(new XElement("member", new XAttribute("key", member)));
            groups.Add(e);
        }
        root.Add(groups);
        return new XDocument(root);
    }

    private static void Load()
    {
        string path = SavePath;
        if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !File.Exists(path + ".bak"))) return;
        XDocument doc;
        string error;
        if (!RebirthAtomicXmlFile.TryLoad(path, out doc, out error) && !RebirthAtomicXmlFile.TryLoad(path + ".bak", out doc, out error))
        {
            Log.Warning("[REBIRTH PartyIdentity] load failed error=" + error);
            return;
        }
        try
        {
            XElement root = doc.Root;
            if (root == null || root.Name.LocalName != "rebirthPartyIdentity") throw new InvalidDataException("invalid root");
            int schema = ParseInt(root, "schema_version");
            if (schema != SchemaVersion) throw new InvalidDataException("unsupported schema " + schema);
            lock (Sync)
            {
                revision = Math.Max(1L, ParseLong(root, "revision"));
                XElement pRoot = root.Element("personal");
                if (pRoot != null)
                    foreach (XElement e in pRoot.Elements("identity").Take(4096))
                    {
                        string key = Attr(e, "key"); if (key.Length == 0) continue;
                        Personal[key] = new PersonalRecord { StableKey = key, Name = NormalizeName(Attr(e, "name")), ColorId = NormalizeColor(ParseInt(e, "color")), UpdatedRevision = ParseLong(e, "revision") };
                    }
                XElement gRoot = root.Element("groups");
                if (gRoot != null)
                    foreach (XElement e in gRoot.Elements("group").Take(2048))
                    {
                        string id = Attr(e, "id"); if (id.Length == 0) continue;
                        GroupRecord g = new GroupRecord { GroupId = id, Name = NormalizeName(Attr(e, "name")), ColorId = NormalizeColor(ParseInt(e, "color")), LeaderStableKey = Attr(e, "leader"), UpdatedRevision = ParseLong(e, "revision") };
                        foreach (XElement m in e.Elements("member").Take(8)) { string key = Attr(m, "key"); if (key.Length > 0) g.Members.Add(key); }
                        Groups[id] = g;
                    }
                dirty = false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH PartyIdentity] invalid persistence: " + ex.Message);
        }
    }

    private static string Attr(XElement e, string name) { XAttribute a = e != null ? e.Attribute(name) : null; return a != null ? (a.Value ?? string.Empty) : string.Empty; }
    private static int ParseInt(XElement e, string name) { int v; return int.TryParse(Attr(e, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
    private static long ParseLong(XElement e, string name) { long v; return long.TryParse(Attr(e, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0L; }
}

[HarmonyPatch(typeof(Party), nameof(Party.AddPlayer))]
public static class RebirthPartyIdentityAddPlayerPatch { public static void Postfix(Party __instance) { RebirthPartyIdentityService.OnPartyChanged(__instance, "party-add"); } }
[HarmonyPatch(typeof(Party), nameof(Party.RemovePlayer))]
public static class RebirthPartyIdentityRemovePlayerPatch { public static void Postfix(Party __instance) { RebirthPartyIdentityService.OnPartyChanged(__instance, "party-remove"); } }
[HarmonyPatch(typeof(Party), nameof(Party.KickPlayer))]
public static class RebirthPartyIdentityKickPlayerPatch { public static void Postfix(Party __instance) { RebirthPartyIdentityService.OnPartyChanged(__instance, "party-kick"); } }
[HarmonyPatch(typeof(Party), "SetLeader")]
public static class RebirthPartyIdentitySetLeaderPatch { public static void Postfix(Party __instance) { RebirthPartyIdentityService.OnPartyChanged(__instance, "party-leader"); } }
[HarmonyPatch(typeof(Party), nameof(Party.Disband))]
public static class RebirthPartyIdentityDisbandPatch { public static void Postfix(Party __instance) { RebirthPartyIdentityService.OnPartyRemoved(__instance, "party-disband-settled"); } }
[HarmonyPatch(typeof(Party), nameof(Party.ServerHandleLeaveParty))]
public static class RebirthPartyIdentityLeaveSettledPatch { public static void Postfix() { RebirthPartyIdentityService.BroadcastSnapshot("party-leave-settled"); } }
[HarmonyPatch(typeof(Party), nameof(Party.ServerHandleDisconnectParty))]
public static class RebirthPartyIdentityDisconnectSettledPatch { public static void Postfix() { RebirthPartyIdentityService.BroadcastSnapshot("party-disconnect-settled"); } }
[HarmonyPatch(typeof(PartyManager), nameof(PartyManager.RemoveParty))]
public static class RebirthPartyIdentityPartyRemovedPatch { public static void Prefix(Party party) { RebirthPartyIdentityService.OnPartyRemoved(party, "party-manager-remove"); } }
[HarmonyPatch(typeof(GameManager), "SaveWorld")]
public static class RebirthPartyIdentityWorldSavePatch { public static void Prefix() { RebirthPartyIdentityService.SaveIfDirty("world-save"); } }
