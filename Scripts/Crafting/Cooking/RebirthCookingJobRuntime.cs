using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

#nullable disable

/// <summary>
/// Single server GameUpdate owner for job observation, receipt reconciliation and immutable
/// presentation publication. Native TickTile/TickUi still own all heat advancement and items.
/// </summary>
public static class RebirthCookingJobRuntime
{
    private sealed class OwnerState
    {
        public float NextUnlockNotice;
        public EntityPlayer Player;
        public RebirthWorldCharacterRecord Record;
        public string Xml = "<jobs />";
        public long PositionRevision = -1L;
        public readonly Dictionary<string, Vector3i> Positions = new Dictionary<string, Vector3i>(StringComparer.Ordinal);
        public readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, float> FirstUnseen = new Dictionary<string, float>(StringComparer.Ordinal);
    }
    private sealed class QueueJob { public Recipe Recipe; public int Portions; }
    private sealed class SavedCompletion { public ItemValue Receipt; public string RecipeName; public int Outputs, Completed; }
    private sealed class StationState
    {
        public TileEntityWorkstation Tile;
        public readonly Dictionary<string, QueueJob> Jobs = new Dictionary<string, QueueJob>(StringComparer.Ordinal);
        public readonly Dictionary<string, SavedCompletion> Completions = new Dictionary<string, SavedCompletion>(StringComparer.Ordinal);
    }
    private static readonly Dictionary<int, OwnerState> Owners = new Dictionary<int, OwnerState>();
    private static World world;
    private static bool installed;
    private static float nextTick;
    public static void Install()
    {
        if (installed) return;
        ModEvents.GameUpdate.RegisterHandler(OnGameUpdate);
        installed = true;
    }
    public static void Reset()
    {
        RebirthCookingStationTracking.Reset(); Owners.Clear(); world = null; nextTick = 0f;
        RebirthCookingSessionService.ResetRuntime();
    }
    public static string GetSnapshot(EntityPlayer player, RebirthWorldCharacterRecord record)
    {
        OwnerState state;
        return player != null && ReferenceEquals(world, player.world) && Owners.TryGetValue(player.entityId, out state)
            && ReferenceEquals(state.Player, player) && ReferenceEquals(state.Record, record) ? state.Xml : "<jobs />";
    }
    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        World current = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (!ReferenceEquals(world, current))
        {
            // The first bind must not erase a same-world request queued before our first tick.
            if (world != null) Reset();
            else { RebirthCookingStationTracking.Reset(); Owners.Clear(); nextTick = 0f; }
            world = current;
        }
        RebirthCookingSessionService.PumpReplies();
        if (world == null || world.IsRemote() || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
        { if (Owners.Count != 0) { Owners.Clear(); RebirthCookingStationTracking.Reset(); } return; }
        float now = Time.realtimeSinceStartup;
        if (now < nextTick) return;
        nextTick = now + 1f;
        var stations = new Dictionary<Vector3i, StationState>();
        var online = new HashSet<int>();
        RebirthCookingStationTracking.BeginFrame(world);
        try
        {
            List<EntityPlayer> players = world.GetPlayers();
            for (int i = 0; players != null && i < players.Count; i++)
            {
                EntityPlayer player = players[i]; RebirthWorldCharacterRecord record;
                if (player == null || !RebirthWorldCharacterService.TryGet(player, out record) || record?.Progression?.Cooking == null) continue;
                online.Add(player.entityId); OwnerState owner;
                if (!Owners.TryGetValue(player.entityId, out owner) || !ReferenceEquals(owner.Player, player) || !ReferenceEquals(owner.Record, record))
                {
                    owner = new OwnerState { Player = player, Record = record }; Owners[player.entityId] = owner;
                    if (record.Progression.Cooking.CompactTerminalHistory()) RebirthWorldCharacterService.MarkDirty(record, "cooking-history-compacted");
                }
                if(record.Progression.Cooking.PendingUnlocks.Count>0&&now>=owner.NextUnlockNotice
                    &&!player.IsDead()&&RebirthSkillAwardService.TryGetEligible(player,out _,out var eligible)
                    &&ReferenceEquals(eligible,record))
                {
                    owner.NextUnlockNotice=now+5f;
                    RebirthRecipeUnlockNotifications.TrySend(record.Progression.Cooking,()=>
                    {
                        RebirthWorldCharacterService.MarkDirty(record,"recipe-unlock-notice");
                        return RebirthWorldCharacterService.FlushPlayer(player,"recipe-unlock-notice");
                    },name=>GameManager.ShowTooltipMP(player,string.Format(Localization.Get("xuiRebirthRecipeUnlocked"),Localization.Get(name)),"ui_success"));
                }
                try { Observe(owner, stations, now); }
                catch (Exception ex) { Log.Warning("[REBIRTH Cooking] job maintenance failed: " + ex.Message); }
            }
        }
        finally { RebirthCookingStationTracking.EndFrame(); }
        var gone = new List<int>(); foreach (int id in Owners.Keys) if (!online.Contains(id)) gone.Add(id);
        foreach (int id in gone) Owners.Remove(id);
    }
    private static StationState GetStation(Dictionary<Vector3i, StationState> stations, Vector3i position)
    {
        StationState state;
        if (stations.TryGetValue(position, out state)) return state;
        state = new StationState { Tile = world.GetTileEntity(position) as TileEntityWorkstation };
        var queue = state.Tile?.Queue;
        if (queue != null) foreach (var entry in queue)
        {
            if (entry == null || !RebirthCookingHeat.Managed(entry.Recipe)) continue;
            ItemValue receipt = RebirthCookingBatch.Receipt(entry.Recipe); string token;
            if (receipt != null && receipt.TryGetMetadata("rebirth.cooking.token", out token) && !string.IsNullOrEmpty(token)
                && !state.Jobs.ContainsKey(token)) state.Jobs[token] = new QueueJob { Recipe = entry.Recipe, Portions = entry.Multiplier };
        }
        // Native station data persists these receipts even if the original cook disconnects.
        // Rebuild the transient witness only from that authoritative station, matching its token.
        if (state.Tile?.CraftCompleteList != null) foreach (CraftCompleteData data in state.Tile.CraftCompleteList)
        {
            ItemValue receipt = data?.CraftedItemStack?.itemValue;
            string token; int completed;
            if (receipt == null || !receipt.TryGetMetadata("rebirth.cooking.token", out token) || string.IsNullOrEmpty(token) ||
                !receipt.TryGetMetadata("rebirth.cooking.completed", out completed) || completed < 1 || completed > 9999 ||
                data.CraftedItemStack.count < 1 || data.CraftedItemStack.count > 32767 ||
                receipt.ItemClass?.GetItemName() != data.RecipeName) continue;
            SavedCompletion old;
            if (!state.Completions.TryGetValue(token, out old) || completed > old.Completed)
                state.Completions[token] = new SavedCompletion { Receipt = receipt, RecipeName = data.RecipeName,
                    Outputs = data.CraftedItemStack.count, Completed = completed };
        }
        stations[position] = state; return state;
    }
    private static void Observe(OwnerState owner, Dictionary<Vector3i, StationState> stations, float now)
    {
        RebirthCookingMemory memory = owner.Record.Progression.Cooking;
        IList<string> tokens = memory.ActiveStationTokens();
        if (tokens.Count == 0) { owner.Xml = "<jobs />"; owner.Seen.Clear(); owner.FirstUnseen.Clear(); owner.Positions.Clear(); owner.PositionRevision = memory.JobRevision; return; }
        if (owner.PositionRevision != memory.JobRevision || owner.Positions.Count > tokens.Count)
        {
            owner.Positions.Clear();
            for (int t = 0; t < tokens.Count; t++)
            {
                RebirthCookingMemory.Ticket indexed; Vector3i parsed;
                if (memory.Tickets.TryGetValue(tokens[t], out indexed) && indexed != null && TryPosition(indexed.StationPosition, out parsed)) owner.Positions[tokens[t]] = parsed;
            }
            owner.PositionRevision = memory.JobRevision;
        }
        var root = new XElement("jobs");
        var retired = new List<string>();
        var active = new HashSet<string>(tokens, StringComparer.Ordinal);
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i]; RebirthCookingMemory.Ticket ticket;
            if (!memory.Tickets.TryGetValue(token, out ticket) || ticket == null) continue;
            Vector3i position;
            if (!owner.Positions.TryGetValue(token, out position)) continue; // Ambiguous records are preserved.
            StationState station = GetStation(stations, position); QueueJob job;
            SavedCompletion saved;
            if (station.Completions.TryGetValue(token, out saved) && saved.RecipeName == ticket.Recipe &&
                saved.Outputs == ticket.OutputCount && saved.Completed <= ticket.Portions)
            {
                // Stable ticket token, not the previous login's entity ID, identifies the owner.
                RebirthCookingBatch.RecordCompletionWitness(saved.Receipt, saved.Completed);
                RebirthCookingBatch.AwardCompleted(owner.Player, saved.RecipeName, saved.Receipt, saved.Outputs);
            }
            if (station.Jobs.TryGetValue(token, out job))
            {
                owner.Seen.Add(token); owner.FirstUnseen.Remove(token);
                RebirthCookingStationTracking.Watch(world, position, "active cooking job");
                if (RebirthCookingHeat.Ready(job.Recipe))
                {
                    int awarded;
                    if (!memory.Awards.TryGetValue(token, out awarded) || awarded < job.Portions)
                    {
                        ItemValue receipt = RebirthCookingBatch.Receipt(job.Recipe);
                        receipt.SetMetadata("rebirth.cooking.completed", job.Portions);
                        RebirthCookingBatch.RecordCompletionWitness(receipt, job.Portions);
                        RebirthCookingBatch.AwardCompleted(owner.Player, job.Recipe.GetName(), receipt, job.Recipe.count);
                    }
                }
                // Preserve the existing HUD visibility rule. Paused/cold jobs still have a
                // maintenance owner; no read request is needed to keep them alive or award them.
                if (station.Tile != null && station.Tile.IsBurning && !RebirthCookingHeat.Burnt(job.Recipe))
                    root.Add(new XElement("job", new XAttribute("name", RebirthCookingHeat.Name(job.Recipe)), new XAttribute("icon", RebirthCookingHeat.Icon(job.Recipe)),
                        new XAttribute("status", RebirthCookingHeat.Status(job.Recipe, station.Tile.IsBurning)), new XAttribute("time", RebirthCookingHeat.Timer(job.Recipe)),
                        new XAttribute("position", ticket.StationPosition)));
            }
            else if (station.Tile != null && !station.Tile.bUserAccessing &&
                (owner.Seen.Contains(token) || station.Completions.ContainsKey(token)))
            {
                // A loaded, released station which previously held this exact token no longer
                // owns the batch. Unawarded native receipts remain settleable in compact memory.
                int credited;
                if (memory.Awards.TryGetValue(token, out credited) && credited >= ticket.Portions)
                    retired.Add(token);
                else if (!station.Completions.ContainsKey(token))
                    retired.Add(token); // cancelled/absent queue: keep legacy compact receipt behavior
                // A witnessed, uncredited completion retains its station link until player restore finishes.
            }
            else
            {
                float since;
                if (!owner.FirstUnseen.TryGetValue(token, out since)) owner.FirstUnseen[token] = since = now;
                // Give new/reloaded queues a loading grace, but never retain an observer forever
                // for an unconfirmed or failed registration. The receipt itself is not discarded.
                if (now - since < 30f) RebirthCookingStationTracking.Watch(world, position, "queue loading");
            }
        }
        foreach (string token in retired)
        {
            if (memory.RetireStation(token, false)) RebirthWorldCharacterService.MarkDirty(owner.Record, "cooking-station-retired");
            owner.Seen.Remove(token); owner.FirstUnseen.Remove(token);
        }
        owner.Seen.RemoveWhere(token => !active.Contains(token));
        var stale = new List<string>(); foreach (string token in owner.FirstUnseen.Keys) if (!active.Contains(token)) stale.Add(token);
        foreach (string token in stale) owner.FirstUnseen.Remove(token);
        string xml = root.ToString(SaveOptions.DisableFormatting);
        if (!string.Equals(owner.Xml, xml, StringComparison.Ordinal)) owner.Xml = xml;
    }
    public static bool TryPosition(string value, out Vector3i position)
    {
        position = default(Vector3i);
        if (string.IsNullOrEmpty(value)) return false;
        string[] parts = value.Split(','); int x,y,z;
        if (parts.Length != 3 || !int.TryParse(parts[0],out x) || !int.TryParse(parts[1],out y) || !int.TryParse(parts[2],out z)) return false;
        position = new Vector3i(x,y,z); return true;
    }
    public static bool AbandonUnqueued(EntityPlayer player, RebirthWorldCharacterRecord record, string token)
    {
        RebirthCookingMemory.Ticket ticket;
        if (player == null || record?.Progression?.Cooking == null || !record.Progression.Cooking.Tickets.TryGetValue(token, out ticket)) return false;
        Vector3i position;
        if (!TryPosition(ticket.StationPosition, out position)) return false;
        var tile = player.world.GetTileEntity(position) as TileEntityWorkstation;
        if (tile == null) return false; // Unavailable is not proof of an absent queue.
        if (tile.Queue != null) foreach (var entry in tile.Queue)
        {
            string activeToken;
            if (entry != null && RebirthCookingHeat.Managed(entry.Recipe) && RebirthCookingBatch.Receipt(entry.Recipe).TryGetMetadata("rebirth.cooking.token", out activeToken)
                && string.Equals(activeToken, token, StringComparison.Ordinal)) return false;
        }
        if (!record.Progression.Cooking.RetireStation(token, true)) return false;
        RebirthWorldCharacterService.MarkDirty(record, "cooking-registration-abandoned");
        RebirthWorldCharacterService.FlushPlayer(player, "cooking-registration-abandoned");
        return true;
    }
}
