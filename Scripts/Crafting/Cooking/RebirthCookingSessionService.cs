using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>Server-owned preparation and completion acknowledgements. UI timing never grants a bonus by itself.</summary>
public static class RebirthCookingSessionService
{
    private sealed class Pending { public string Recipe, Book, Magazine; public double Started; public Vector3 Position; }
    private static readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
    private sealed class Reply { public Action<string> Callback; public float Expires; public World World; }
    private static readonly Dictionary<long, Reply> replies = new Dictionary<long, Reply>();
    private static readonly Queue<KeyValuePair<long,string>> received = new Queue<KeyValuePair<long,string>>();
    private static readonly object ReplySync = new object();
    private static long sequence;
    private static World world;
    public static RebirthCookingMemory ClientMemory = new RebirthCookingMemory();
    public static void Request(EntityPlayerLocal player, string action, string recipe = "", string book = "", string magazine = "", ItemValue item = null, int count = 0, Action<string> reply = null)
    {
        if (player == null || player.world == null) { reply?.Invoke("Character unavailable."); return; }
        if(!ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player))
        { reply?.Invoke("Cooking request belongs to an inactive character or world."); return; }
        if (world != player.world) { world = player.world; pending.Clear(); replies.Clear(); ClientMemory = new RebirthCookingMemory(); }
        if (!player.world.IsRemote()) { string result = Handle(player, action, recipe, book, magazine, item, count); reply?.Invoke(result); return; }
        long id = ++sequence;
        if (reply != null)
        {
            if (replies.Count >= 256) { reply("Too many pending cooking requests; retry after current requests complete."); return; }
            replies[id] = new Reply { Callback = reply, Expires = Time.realtimeSinceStartup + 15f, World = player.world };
        }
        var persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (persistent?.PrimaryId == null || connection == null)
        { replies.Remove(id); reply?.Invoke("Cooking request was not sent: player identity or connection unavailable."); return; }
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCookingSession>().Setup(player.entityId, persistent.PrimaryId, id, action, recipe, book, magazine, item, count));
    }
    public static void Receive(long id, string response)
    { lock (ReplySync) { if (received.Count < 256) received.Enqueue(new KeyValuePair<long,string>(id,response)); } }
    public static void ResetRuntime()
    { pending.Clear(); replies.Clear(); lock (ReplySync) received.Clear(); world = null; ClientMemory = new RebirthCookingMemory(); RebirthCookingBatch.ResetCompletionWitnesses(); }
    public static void PumpReplies()
    {
        KeyValuePair<long,string>[] results;
        lock (ReplySync) { results = received.Count == 0 ? null : received.ToArray(); received.Clear(); }
        if (results != null) foreach (var result in results)
        {
            Reply reply;
            if (!replies.TryGetValue(result.Key, out reply)) continue;
            replies.Remove(result.Key);
            if (!ReferenceEquals(reply.World, GameManager.Instance != null ? GameManager.Instance.World : null)) continue;
            try { reply.Callback(result.Value ?? string.Empty); }
            catch (Exception ex) { Log.Warning("[REBIRTH Cooking] response callback failed: " + ex.Message); }
        }
        if (replies.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        List<KeyValuePair<long, Reply>> expired = null;
        foreach (var pair in replies)
            if (now >= pair.Value.Expires)
            {
                if (expired == null) expired = new List<KeyValuePair<long, Reply>>();
                expired.Add(pair);
            }
        if (expired == null) return;
        foreach (var entry in expired)
        {
            // A previous callback can reset the session or replace a pending request.
            Reply reply;
            if (!replies.TryGetValue(entry.Key, out reply) || !ReferenceEquals(reply, entry.Value)) continue;
            replies.Remove(entry.Key);
            try { reply.Callback("Cooking request timed out; authoritative state was not assumed to change."); }
            catch (Exception ex) { Log.Warning("[REBIRTH Cooking] timeout callback failed: " + ex.Message); }
        }
    }
    public static RebirthCookingMemory Memory(EntityPlayer player)
    {
        if (player.world.IsRemote()) return ClientMemory;
        return RebirthWorldCharacterService.TryGet(player, out var record) ? record.Progression.Cooking : new RebirthCookingMemory();
    }
    public static RebirthCookingMemory.Ready Ready(EntityPlayer player, string recipe)
    {
        return recipe != null && Memory(player).Recipes.TryGetValue(recipe, out var r) && r.Remaining > 0 ? r : null;
    }
    private static bool Available(EntityPlayer player, string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!RebirthCookingCatalogue.Studied(player, id)) return false;
        var value = ItemClass.GetItem(id);
        return player.bag.GetItemCount(value) + player.inventory.GetItemCount(value) + (RemoteResourcesRuntimePolicy.Enabled ? RemoteResourceTransactions.CountRemote(player, value) : 0) > 0;
    }
    private static string ReadyState(RebirthCookingMemory memory)
    {
        var node=memory.Write();node.Elements("award").Remove();node.Elements("batch").Remove();node.Elements("unlock-notice").Remove();
        return node.ToString(SaveOptions.DisableFormatting);
    }
    public static string Handle(EntityPlayer player, string action, string recipe, string book, string magazine, ItemValue item, int count)
    {
        if (player == null || player.world.IsRemote() || !RebirthWorldCharacterService.TryGet(player, out var record)) return "Character unavailable.";
        if (world != player.world) { world = player.world; pending.Clear(); }
        if (action == "treatmentSkillPreview") return RebirthMedicalPractice.Preview(player,recipe);
        if (action == "skillPreview")
            return RebirthCraftSkillPreview.ServerReply(player, recipe, count, magazine, book == "prepared", item);
        if (action == "cancel") { pending.Remove(player.entityId); return ""; }
        if (action == "fetch") return ReadyState(record.Progression.Cooking);
        if(action=="register")
        {
            var source=XUiM_Recipes.GetRecipes().FirstOrDefault(r=>r.GetName()==recipe);
            bool improvised=recipe.StartsWith("rebirthImprovised",StringComparison.Ordinal);
            if(item==null||item.ItemClass?.GetItemName()!=recipe||(!improvised&&(source==null||!RebirthCookingCatalogue.IsCooking(source)))||!item.TryGetMetadata("rebirth.cooking.token",out string token)||!Guid.TryParse(token,out _)||!item.TryGetMetadata("rebirth.cooking.portions",out int portions)||portions<1||portions>9999||(count<1||count>32767||!improvised&&count!=source.count))return "Invalid cooking batch.";
            if(!RebirthRecipeDiscoveryRules.Allows(player,recipe))return Localization.Get("xuiRebirthRecipeReadingRequired");
            var capability = RebirthCapabilityService.EvaluateRecipeForDiscovery(player,recipe);
            if (!capability.IsAllowed) return capability.FirstMissingReason;
            if(record.Progression.Cooking.IsKnownBatch(token))return "Batch already registered.";
            var prepared=Ready(player,recipe);
            record.Progression.Cooking.Tickets[token]=new RebirthCookingMemory.Ticket{Recipe=recipe,Portions=portions,OutputCount=count,StationPosition=magazine,XpMultiplier=string.IsNullOrEmpty(prepared?.Book)?1:RebirthCookingRules.BookMultiplier};
            record.Progression.Cooking.JobsChanged();
            RebirthWorldCharacterService.MarkDirty(record,"cooking-batch");
            if(!RebirthWorldCharacterService.FlushPlayer(player,"cooking-batch"))
            {
                // No successful admission was returned; the UI still owns the full input grid.
                record.Progression.Cooking.Tickets.Remove(token);
                record.Progression.Cooking.JobsChanged();
                RebirthWorldCharacterService.MarkDirty(record,"cooking-batch-admission-rollback");
                return "Crafting could not be saved. Ingredients were not admitted; retry shortly.";
            }
            return "";
        }
        if(action=="abandonRegistration")
        {
            string token;
            return item != null && item.TryGetMetadata("rebirth.cooking.token", out token)
                && RebirthCookingJobRuntime.AbandonUnqueued(player, record, token) ? "" : "Batch retained for authoritative reconciliation.";
        }
        if(action=="jobs")return RebirthCookingHudService.Snapshot(player,record);
        if (action == "preview")
        {
            if (item == null || !RebirthFoodMoodResolver.TryResolve(item, out var mood)) return "";
            var result = RebirthDietSatisfactionService.RecordMeaningfulMeal(record.Clone(), mood);
            return result.Valid ? result.EffectiveMoodInfluence.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "|" + (!result.CompatibleWithDiet ? "Diet mismatch" : result.RepetitionMultiplier < 1 ? "Reduced by recent repeats" : "Matches your diet") : "";
        }
        if (action == "complete")
        {
            var cook=int.TryParse(book,out int owner)?player.world.GetEntity(owner) as EntityPlayer:player;
            if(item!=null&&item.TryGetMetadata("rebirth.cooking.token",out string completedToken))
                cook=player.world.Players.list.FirstOrDefault(p=>RebirthWorldCharacterService.TryGet(p,out var ownerRecord)&&ownerRecord.Progression.Cooking.Tickets.ContainsKey(completedToken));
            RebirthCookingBatch.AwardCompleted(cook, recipe, item, count);
            return "";
        }
        var dish = RebirthCookingCatalogue.Get(recipe);
        if (dish == null || player.IsDead()) return "No eligible recipe.";
        bool validBook = !string.IsNullOrEmpty(book) && dish.Books.Contains(book) && Available(player, book);
        bool validMagazine = !string.IsNullOrEmpty(magazine) && dish.Magazines.Contains(magazine) && Available(player, magazine);
        if (action == "begin")
        {
            if (!validBook && !validMagazine) return "Study a matching book or magazine and keep it accessible.";
            pending[player.entityId] = new Pending { Recipe = recipe, Book = validBook ? book : "", Magazine = validMagazine ? magazine : "", Started = Time.realtimeSinceStartupAsDouble, Position = player.position };
            return "";
        }
        if (action != "finish" || !pending.TryGetValue(player.entityId, out var p)) return "Preparation was interrupted.";
        pending.Remove(player.entityId);
        if (p.Recipe != recipe || Time.realtimeSinceStartupAsDouble - p.Started < RebirthCookingPreparation.StudySeconds - .05 || Vector3.Distance(p.Position, player.position) > 3 || (!string.IsNullOrEmpty(p.Book) && !Available(player, p.Book)) || (!string.IsNullOrEmpty(p.Magazine) && !Available(player, p.Magazine))) return "Preparation was interrupted or its references are unavailable.";
        record.Progression.Cooking.Recipes.TryGetValue(recipe,out var previousPreparation);
        record.Progression.Cooking.Recipes[recipe] = new RebirthCookingMemory.Ready { Book = p.Book, Magazine = p.Magazine, Expires = DateTime.UtcNow.AddSeconds(RebirthCookingPreparation.BenefitSeconds).Ticks };
        RebirthWorldCharacterService.MarkDirty(record, "cooking-preparation");
        if(!RebirthWorldCharacterService.FlushPlayer(player, "cooking-preparation"))
        {
            // Preserve an older earned timed benefit; do not acknowledge an unsaved replacement.
            if(previousPreparation==null)record.Progression.Cooking.Recipes.Remove(recipe);
            else record.Progression.Cooking.Recipes[recipe]=previousPreparation;
            RebirthWorldCharacterService.MarkDirty(record,"cooking-preparation-rollback");
            return "Preparation could not be saved. Prepare again after storage recovers.";
        }
        return ReadyState(record.Progression.Cooking);
    }
}

[Preserve]
public sealed class NetPackageRebirthCookingSession : NetPackage
{
    private int playerId, count; private PlatformUserIdentifierAbs user; private long id;
    private string action, recipe, book, magazine; private ItemValue item;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthCookingSession Setup(int player, PlatformUserIdentifierAbs uid, long request, string op, string dish, string b, string m, ItemValue value, int n)
    { playerId = player; user = uid; id = request; action = op; recipe = dish ?? ""; book = b ?? ""; magazine = m ?? ""; item = value ?? ItemValue.None.Clone(); count = n; return this; }
    public override void read(PooledBinaryReader r)
    { playerId = r.ReadInt32(); user = PlatformUserIdentifierAbs.FromStream(r); id = r.ReadInt64(); action = r.ReadString(); recipe = r.ReadString(); book = r.ReadString(); magazine = r.ReadString(); item = new ItemValue(); item.Read(r); count = r.ReadInt32(); }
    public override void write(PooledBinaryWriter w)
    { base.write(w); var b = (BinaryWriter)w; b.Write(playerId); user.ToStream(w); b.Write(id); b.Write(action); b.Write(recipe); b.Write(book); b.Write(magazine); item.Write(w); b.Write(count); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(user)) return;
        string response = RebirthCookingSessionService.Handle(world.GetEntity(playerId) as EntityPlayer, action, recipe, book, magazine, item, count);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCookingSessionResult>().Setup(id, response), _attachedToEntityId: playerId);
    }
    public int GetLength() => 0;
}
[Preserve]
public sealed class NetPackageRebirthCookingSessionResult : NetPackage
{
    private long id; private string response;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthCookingSessionResult Setup(long request, string text) { id = request; response = text; return this; }
    public override void read(PooledBinaryReader r) { id = r.ReadInt64(); response = r.ReadString(); }
    public override void write(PooledBinaryWriter w) { base.write(w); ((BinaryWriter)w).Write(id); ((BinaryWriter)w).Write(response); }
    public override void ProcessPackage(World world, GameManager callbacks) { if (world != null && world.IsRemote()) RebirthCookingSessionService.Receive(id, response); }
    public int GetLength() => 0;
}
