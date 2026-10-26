#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

#nullable disable

// DEBUG-only P11 qualification hooks for the single deferred profiler batch (W6).
//
//  * RebirthP11GateOverride: eight default-OFF booleans behind the P11 qualification gates. In a Debug build the
//    production gate members (RebirthP11SmartLootGate.*, RebirthDogAutoLootBridge.SmartLootingLiveRouteQualified,
//    RebirthDogSmartLootTransfer.JournaledLegQualified) are read-only properties over these booleans; in a Release
//    build they remain `const bool = false` and this whole file is compiled out. Nothing sets them except the
//    console command below; they are never persisted and every field is false at process start.
//    An override is only honoured while the World instance it was armed in is still the current world, so a
//    stale arm can never leak into another save loaded later in the same session.
//  * ConsoleCmdRebirthP11Fixture: `rbp11`. Arming needs the same guard as the other qualification fixtures
//    (RebirthNpcQualificationMutationGuard.Allows: authoritative server, disposable save named CodexTest) AND
//    the literal confirm=CodexTest. Disarming is always allowed (it can only close gates). Every other
//    subcommand is read-only. The hook opens gates only: it never touches a custody check, a journal row, a
//    lease, an inventory or a container.
internal static class RebirthP11GateOverride
{
    internal static bool Generated, Unified, Corpse, NpcOpen, Follow, Lockpick, Live, DogLeg;
    private static World armedWorld;

    internal static readonly string[] Keys = { "generated", "unified", "corpse", "npcopen", "follow", "lockpick", "live", "dogleg" };
    internal static readonly string[] ConstNames = {
        "GeneratedStorageQualified", "UnifiedOrderingQualified", "CorpseSourceQualified", "NpcAttributedOpenQualified",
        "FollowActivityQualified", "NpcLockpickQualified", "SmartLootingLiveRouteQualified", "JournaledLegQualified"
    };

    private static bool Bound => armedWorld != null && ReferenceEquals(GameManager.Instance?.World, armedWorld);

    // The production accessors (cheap: one bool and one reference compare).
    internal static bool GeneratedOn => Generated && Bound;
    internal static bool UnifiedOn => Unified && Bound;
    internal static bool CorpseOn => Corpse && Bound;
    internal static bool NpcOpenOn => NpcOpen && Bound;
    internal static bool FollowOn => Follow && Bound;
    internal static bool LockpickOn => Lockpick && Bound;
    internal static bool LiveOn => Live && Bound;
    internal static bool DogLegOn => DogLeg && Bound;

    internal static string Normalize(string name)
    {
        string key = (name ?? string.Empty).Trim().ToLowerInvariant();
        if (key.EndsWith("qualified", StringComparison.Ordinal)) key = key.Substring(0, key.Length - "qualified".Length);
        switch (key)
        {
            case "generated": case "generatedstorage": return "generated";
            case "unified": case "unifiedordering": return "unified";
            case "corpse": case "corpsesource": return "corpse";
            case "npcopen": case "npcattributedopen": return "npcopen";
            case "follow": case "followactivity": return "follow";
            case "lockpick": case "npclockpick": return "lockpick";
            case "live": case "smartlootingliveroute": return "live";
            case "dogleg": case "journaledleg": return "dogleg";
            default: return null;
        }
    }

    internal static bool Get(string key)
    {
        switch (key)
        {
            case "generated": return Generated; case "unified": return Unified; case "corpse": return Corpse;
            case "npcopen": return NpcOpen; case "follow": return Follow; case "lockpick": return Lockpick;
            case "live": return Live; case "dogleg": return DogLeg; default: return false;
        }
    }

    internal static bool Effective(string key)
    {
        switch (key)
        {
            case "generated": return RebirthP11SmartLootGate.GeneratedStorageQualified;
            case "unified": return RebirthP11SmartLootGate.UnifiedOrderingQualified;
            case "corpse": return RebirthP11SmartLootGate.CorpseSourceQualified;
            case "npcopen": return RebirthP11SmartLootGate.NpcAttributedOpenQualified;
            case "follow": return RebirthP11SmartLootGate.FollowActivityQualified;
            case "lockpick": return RebirthP11SmartLootGate.NpcLockpickQualified;
            case "live": return RebirthDogAutoLootBridge.SmartLootingLiveRouteQualified;
            case "dogleg": return RebirthDogSmartLootTransfer.JournaledLegQualified;
            default: return false;
        }
    }

    private static void Put(string key, bool value)
    {
        switch (key)
        {
            case "generated": Generated = value; break; case "unified": Unified = value; break;
            case "corpse": Corpse = value; break; case "npcopen": NpcOpen = value; break;
            case "follow": Follow = value; break; case "lockpick": Lockpick = value; break;
            case "live": Live = value; break; case "dogleg": DogLeg = value; break;
        }
    }

    internal static int ArmedCount => Keys.Count(Get);
    internal static bool BoundNow => Bound;

    internal static void DisarmAll()
    {
        foreach (string key in Keys) Put(key, false);
        armedWorld = null;
    }

    // Caller has already passed the guard. A different world than the armed one clears everything first.
    internal static void Arm(string key)
    {
        World world = GameManager.Instance?.World;
        if (armedWorld != null && !ReferenceEquals(armedWorld, world)) DisarmAll();
        armedWorld = world;
        Put(key, true);
    }

    internal static void Disarm(string key)
    {
        Put(key, false);
        if (ArmedCount == 0) armedWorld = null;
    }
}

public sealed class ConsoleCmdRebirthP11Fixture : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => false;
    public override int DefaultPermissionLevel => 0;
    public override string[] getCommands() => new[] { "rbp11" };
    public override string getDescription() => "DEBUG P11 smart-loot / dog custody qualification hooks: gate overrides (guarded) and read-only observers.";
    public override string getHelp() =>
        "rbp11 gates <generated|unified|corpse|npcopen|follow|lockpick|live|dogleg> on|off confirm=CodexTest\n" +
        "rbp11 gates all off\n" +
        "rbp11 status\n" +
        "rbp11 dogrow [dogStableId|owner]\n" +
        "rbp11 tile | rbp11 corpse\n" +
        "rbp11 grid <x> <y> <z> | rbp11 bag <npcEntityId> | rbp11 receipts <npcEntityId>\n" +
        "rbp11 mark <label> grid:x:y:z|bag:npcEntityId | rbp11 conserve <beforeLabel> <spec[:baselineLabel]>...  (per-item-name totals; read-only)\n" +
        "Arming requires the disposable CodexTest save, an authoritative server and confirm=CodexTest; default OFF at every launch; never persisted. Disarm and every observer are read-only/safe. One [RBP11] line per record.";

    private static string B(bool value) => value ? "1" : "0";
    private static string N(Guid value) => value.ToString("N");
    private static string P(Vector3i value) => value.x.ToString(CultureInfo.InvariantCulture) + "," +
        value.y.ToString(CultureInfo.InvariantCulture) + "," + value.z.ToString(CultureInfo.InvariantCulture);

    private static void Emit(string line)
    {
        Log.Out(line);
        try { SdtdConsole.Instance.Output(line); } catch { }
    }

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        try
        {
            if (args == null || args.Count == 0) { Emit("[RBP11] help " + getHelp().Replace("\n", " | ")); return; }
            switch (args[0].ToLowerInvariant())
            {
                case "gates": Gates(args, sender); break;
                case "status": Status(); break;
                case "dogrow": DogRow(args); break;
                case "tile": Tile(); break;
                case "corpse": Corpse(); break;
                case "grid": Grid(args); break;
                case "bag": Bag(args); break;
                case "receipts": Receipts(args); break;
                case "mark": Mark(args); break;
                case "conserve": Conserve(args); break;
                default: Emit("[RBP11] result=FAIL unsupported subcommand '" + args[0] + "'"); break;
            }
        }
        catch (Exception error) { Emit("[RBP11] result=FAIL exception " + error.GetType().Name + ": " + error.Message); }
    }

    // -- gates --------------------------------------------------------------------------------------
    private static void Gates(List<string> args, CommandSenderInfo sender)
    {
        if (args.Count < 3) { Emit("[RBP11] gates result=FAIL usage: rbp11 gates <name> on|off confirm=CodexTest"); return; }
        string verb = args[2].ToLowerInvariant();
        if (verb != "on" && verb != "off") { Emit("[RBP11] gates result=FAIL expected on|off"); return; }
        if (args[1].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (verb != "off") { Emit("[RBP11] gates result=FAIL 'all' can only be turned off; arm one named gate at a time"); return; }
            RebirthP11GateOverride.DisarmAll();
            Emit("[RBP11] gates all=OFF result=OK armed=0");
            return;
        }
        string key = RebirthP11GateOverride.Normalize(args[1]);
        if (key == null) { Emit("[RBP11] gates result=FAIL unknown gate '" + args[1] + "'"); return; }
        if (verb == "on")
        {
            if (sender.RemoteClientInfo != null || args.Count != 4 || args[3] != "confirm=CodexTest" ||
                !RebirthNpcQualificationMutationGuard.Allows("CodexTest"))
            {
                Emit("[RBP11] gates name=" + key + " result=FAIL " + RebirthNpcQualificationMutationGuard.Refused);
                return;
            }
            RebirthP11GateOverride.Arm(key);
        }
        else RebirthP11GateOverride.Disarm(key);
        Emit("[RBP11] gate name=" + key + " state=" + (RebirthP11GateOverride.Get(key) ? "ON" : "OFF") +
            " effective=" + B(RebirthP11GateOverride.Effective(key)) + " result=OK armed=" + RebirthP11GateOverride.ArmedCount);
    }

    private static void Status()
    {
        Emit("[RBP11] status begin build=DEBUG bound=" + B(RebirthP11GateOverride.BoundNow) +
            " guard=" + (RebirthNpcQualificationMutationGuard.Allows("CodexTest") ? "ALLOWED" : "REFUSED"));
        for (int i = 0; i < RebirthP11GateOverride.Keys.Length; i++)
        {
            string key = RebirthP11GateOverride.Keys[i];
            Emit("[RBP11] gate name=" + key + " const=" + RebirthP11GateOverride.ConstNames[i] +
                " override=" + B(RebirthP11GateOverride.Get(key)) + " effective=" + B(RebirthP11GateOverride.Effective(key)));
        }
        bool option = false;
        try { option = RebirthSandboxOptionManager.Current.SmartLooting; } catch { }
        Emit("[RBP11] status dogTransferActive=" + B(RebirthDogSmartLootTransfer.Active) +
            " dogSourceCustody=" + B(RebirthDogSmartLootTransfer.SourceCustody != null) +
            " smartLootingOption=" + B(option) + " end armed=" + RebirthP11GateOverride.ArmedCount);
    }

    // -- dog rows (read-only) ------------------------------------------------------------------------
    private static void DogRow(List<string> args)
    {
        World world = GameManager.Instance?.World;
        if (world == null || world.IsRemote()) { Emit("[RBP11] dogrow result=FAIL no authoritative world"); return; }
        RebirthNpcStableId only = default;
        bool byId = false;
        string ownerId = null;
        if (args.Count > 1)
        {
            if (args[1].Equals("owner", StringComparison.OrdinalIgnoreCase))
            {
                var player = world.GetPrimaryPlayer();
                if (player == null || !RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId))
                { Emit("[RBP11] dogrow result=FAIL owner not resolvable"); return; }
            }
            else if (RebirthNpcStableId.TryParse(args[1], out only)) byId = true;
            else { Emit("[RBP11] dogrow result=FAIL expected dog stable id or 'owner'"); return; }
        }
        int dogs = 0, rows = 0;
        foreach (var record in RebirthNpcAggregatePersistenceStore.Snapshot())
        {
            if (!RebirthDogStateService.IsDog(record)) continue;
            var id = record.Identity.StableNpcId;
            if (byId && id != only) continue;
            string lifecycle = "?", owner = "";
            if (RebirthDogStateService.TryGetView(id, out var view, out var dogView) && dogView != null)
            { lifecycle = dogView.Lifecycle.ToString(); owner = view.Ownership?.OwnerPlatformIdOrPersistentPlayerId ?? ""; }
            if (ownerId != null && !string.Equals(owner, ownerId, StringComparison.OrdinalIgnoreCase)) continue;
            dogs++;
            if (EmitDog(world, record, id, lifecycle, owner)) rows++;
        }
        Emit("[RBP11] dogrow summary dogs=" + dogs + " rows=" + rows);
    }

    private static bool EmitDog(World world, RebirthNpcPersistentRecord record, RebirthNpcStableId id, string lifecycle, string owner)
    {
        uint revisionNow = 0;
        try { revisionNow = RebirthDogInventoryService.GetSnapshot(id).Revision; } catch { }
        int elements = 0;
        foreach (string text in record.UnknownOptionalElements ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(text) &&
                text.TrimStart().StartsWith("<" + RebirthDogSmartLootOriginal.ElementName, StringComparison.Ordinal)) elements++;
        string head = "[RBP11] dogrow dog=" + id + " lifecycle=" + lifecycle + " ownerKnown=" + B(owner.Length > 0) +
            " dogRevisionNow=" + revisionNow;
        if (elements == 0) { Emit(head + " row=NONE"); return false; }
        if (!RebirthDogSmartLootOriginal.TryFind(record, out var row))
        { Emit(head + " row=MALFORMED elements=" + elements + " binding=FAIL"); return true; }

        string src = row.SourceMatches(world, false) ? "before" : row.SourceMatches(world, true) ? "after" :
            row.SourceDiverged(world) ? "diverged" : "unresolved";
        string dog = row.DogMatches(false) ? "before" : row.DogMatches(true) ? "after" : "other";
        string conservation = "OK";
        if (src == "unresolved" || src == "diverged") conservation = "UNKNOWN";
        else if (row.Cancelled && src == "after") conservation = "VIOLATION";
        else if (!row.Cancelled && row.Phase <= 1 && dog == "after") conservation = "VIOLATION";
        else if (!row.Cancelled && row.Phase >= 2 && src == "before") conservation = "VIOLATION";
        else if (!row.Cancelled && row.Phase >= 3 && dog == "before") conservation = "VIOLATION";

        string lease = " leaseRead=FAIL";
        if (RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting,
                row.Owner, row.Operation, RemoteResourceIdentity.Static(row.SourcePosition), out var original))
            lease = " leaseRead=OK leaseSavedTerminal=" + B(original.SavedTerminal()) +
                " leaseAttemptedStage=" + original.AttemptedStage() + " leaseMayApplyStage1=" + B(original.MayApply(1)) +
                " leaseGraphRetired=" + B(original.GraphRetired);

        string item = "?";
        try { item = row.SourceBefore[row.SourceSlot].itemValue.ItemClass?.Name ?? "?"; } catch { }
        Emit(head + " op=" + N(row.Operation) + " phase=" + row.Phase + " attempted=" + row.Attempted +
            " published=" + row.Published + " cancelled=" + B(row.Cancelled) + " npcActivity=" + B(row.NpcActivity) +
            " src=" + P(row.SourcePosition) + " srcId=" + RemoteResourceIdentity.Static(row.SourcePosition) +
            " slot=" + row.SourceSlot + " qty=" + row.Quantity + " item=" + item + " binding=OK" +
            " dogRevisionRow=" + row.DogRevision + " deaths=" + row.Deaths +
            " hasPendingAt=" + B(RebirthDogSmartLootTransfer.HasPendingAt(row.SourcePosition)) +
            " sourceState=" + src + " dogState=" + dog + lease + " conservation=" + conservation);
        return true;
    }

    // -- human pending rows (read-only, non-terminal only) ---------------------------------------------
    private static void Tile()
    {
        World world = GameManager.Instance?.World;
        if (!RebirthSmartLootingHumanOriginal.TryReadPending(world, out var rows))
        { Emit("[RBP11] tile pending=UNREADABLE"); return; }
        Emit("[RBP11] tile pending=" + rows.Length);
        foreach (var row in rows)
            Emit("[RBP11] tile npc=" + row.Npc + " op=" + N(row.Operation) + " phase=" + row.Phase +
                " attempted=" + row.Attempted + " published=" + row.Published + " cancelled=" + B(row.Cancelled) +
                " npcActivity=" + B(row.NpcActivity) + " src=" + P(row.SourcePosition) + " slot=" + row.SourceSlot +
                " dest=" + row.DestinationSlot + " qty=" + row.Quantity);
    }

    private static void Corpse()
    {
        World world = GameManager.Instance?.World;
        if (!RebirthP11CorpseOriginal.TryReadPending(world, out var rows))
        { Emit("[RBP11] corpse pending=UNREADABLE"); return; }
        Emit("[RBP11] corpse pending=" + rows.Length);
        foreach (var row in rows)
            Emit("[RBP11] corpse npc=" + row.Npc + " op=" + N(row.Operation) + " phase=" + row.Phase +
                " attempted=" + row.Attempted + " published=" + row.Published + " cancelled=" + B(row.Cancelled) +
                " npcActivity=" + B(row.NpcActivity) + " key=" + row.SourceKey + " slot=" + row.SourceSlot +
                " dest=" + row.DestinationSlot + " qty=" + row.Quantity);
    }

    // -- container / bag payload (read-only) -------------------------------------------------------------
    private static SortedDictionary<string, long> Totals(ItemStack[] items, out int used)
    {
        used = 0;
        var totals = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var stack in items)
        {
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            used++;
            string name = stack.itemValue?.ItemClass?.Name ?? "?";
            totals.TryGetValue(name, out long count);
            totals[name] = count + stack.count;
        }
        return totals;
    }

    private static string Format(SortedDictionary<string, long> totals)
    {
        var text = new StringBuilder();
        foreach (var pair in totals) { if (text.Length > 0) text.Append(';'); text.Append(pair.Key).Append('*').Append(pair.Value); }
        return text.Length == 0 ? "-" : text.ToString();
    }

    private static string Payload(ItemStack[] items, out int used) => Format(Totals(items, out used));

    // -- mark / conserve (read-only; per-item-name totals, NOT exact frames) -----------------------------
    // spec = grid:x:y:z | bag:npcEntityId   (conserve additionally accepts a trailing :baselineLabel)
    private static readonly Dictionary<string, SortedDictionary<string, long>> Marks =
        new Dictionary<string, SortedDictionary<string, long>>(StringComparer.Ordinal);

    private static bool TryItems(World world, string[] part, out ItemStack[] items, out string baseline)
    {
        items = null; baseline = null;
        if (part.Length >= 2 && part[0] == "bag" && int.TryParse(part[1], out int id))
        {
            items = (world.GetEntity(id) as EntityRebirthHumanoidNPC)?.bag?.ItemGrid?.items;
            baseline = part.Length == 3 ? part[2] : null;
            return items != null && part.Length <= 3;
        }
        if (part.Length >= 4 && part[0] == "grid" && int.TryParse(part[1], out int x) && int.TryParse(part[2], out int y) &&
            int.TryParse(part[3], out int z))
        {
            items = world.GetTileEntity(new Vector3i(x, y, z))?.GetSelfOrFeature<TEFeatureStorage>()?.ItemGrid?.items;
            baseline = part.Length == 5 ? part[4] : null;
            return items != null && part.Length <= 5;
        }
        return false;
    }

    private static void Mark(List<string> args)
    {
        World world = GameManager.Instance?.World;
        if (world == null || args.Count != 3 || !TryItems(world, args[2].Split(':'), out var items, out var baseline) || baseline != null)
        { Emit("[RBP11] mark result=FAIL usage: rbp11 mark <label> grid:x:y:z|bag:npcEntityId"); return; }
        var totals = Totals(items, out int used);
        Marks[args[1]] = totals;
        Emit("[RBP11] mark label=" + args[1] + " cells=" + items.Length + " used=" + used + " totals=" + Format(totals));
    }

    // conserve <beforeLabel> <spec>...: sum over specs of (now - baseline label if given) must equal the before mark.
    private static void Conserve(List<string> args)
    {
        World world = GameManager.Instance?.World;
        if (world == null || args.Count < 3 || !Marks.TryGetValue(args[1], out var before))
        { Emit("[RBP11] conserve result=FAIL usage: rbp11 conserve <beforeLabel> <spec>... (label must exist)"); return; }
        var sum = new SortedDictionary<string, long>(StringComparer.Ordinal);
        for (int i = 2; i < args.Count; i++)
        {
            if (!TryItems(world, args[i].Split(':'), out var items, out var baseline) ||
                baseline != null && !Marks.ContainsKey(baseline))
            { Emit("[RBP11] conserve result=FAIL unresolvable spec '" + args[i] + "'"); return; }
            foreach (var pair in Totals(items, out _)) { sum.TryGetValue(pair.Key, out long v); sum[pair.Key] = v + pair.Value; }
            if (baseline != null)
                foreach (var pair in Marks[baseline]) { sum.TryGetValue(pair.Key, out long v); sum[pair.Key] = v - pair.Value; }
        }
        var diff = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (string key in before.Keys.Concat(sum.Keys).Distinct())
        {
            before.TryGetValue(key, out long b); sum.TryGetValue(key, out long s);
            if (b != s) diff[key] = s - b;
        }
        Emit("[RBP11] conserve before=" + args[1] + " result=" + (diff.Count == 0 ? "OK" : "MISMATCH") +
            " diff=" + Format(diff) + " beforeTotals=" + Format(before) + " nowTotals=" + Format(sum));
    }

    private static string Digest(ItemStack[] items) =>
        RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items)));

    private static void Grid(List<string> args)
    {
        World world = GameManager.Instance?.World;
        if (world == null || args.Count != 4 || !int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y) ||
            !int.TryParse(args[3], out int z)) { Emit("[RBP11] grid result=FAIL usage: rbp11 grid <x> <y> <z>"); return; }
        var position = new Vector3i(x, y, z);
        TEFeatureStorage source = world.GetTileEntity(position)?.GetSelfOrFeature<TEFeatureStorage>();
        if (source?.ItemGrid?.items == null) { Emit("[RBP11] grid at=" + P(position) + " storage=NONE"); return; }
        bool witnessed = RebirthP11StorageIncarnation.TryResolve(world, source, false, out _);
        string totals = Payload(source.ItemGrid.items, out int used);
        Emit("[RBP11] grid at=" + P(position) + " touched=" + B(source.ItemGrid.Touched) + " cells=" +
            source.ItemGrid.items.Length + " used=" + used + " incarnationWitnessed=" + B(witnessed) +
            " digest=" + Digest(source.ItemGrid.items) + " totals=" + totals);
    }

    // Saved human loot receipts of one NPC: npcLootOpen rows and ALL smartLootMove rows (terminal ones included).
    private static void Receipts(List<string> args)
    {
        World world = GameManager.Instance?.World;
        if (world == null || args.Count != 2 || !int.TryParse(args[1], out int entityId) ||
            !(world.GetEntity(entityId) is EntityRebirthHumanoidNPC npc))
        { Emit("[RBP11] receipts result=FAIL usage: rbp11 receipts <npcEntityId>"); return; }
        XElement equipment = RebirthHumanEquipment.Read(npc);
        var opens = equipment?.Elements("npcLootOpen").ToArray() ?? new XElement[0];
        var moves = equipment?.Elements("smartLootMove").ToArray() ?? new XElement[0];
        Emit("[RBP11] receipts npc=" + entityId + " npcLootOpen=" + opens.Length + " smartLootMove=" + moves.Length);
        foreach (var node in opens)
        {
            if (!RebirthP11NpcLootOpenReceipt.TryRead(node, out var open)) { Emit("[RBP11] receipt kind=npcLootOpen row=MALFORMED"); continue; }
            Emit("[RBP11] receipt kind=npcLootOpen npc=" + entityId + " op=" + N(open.Operation) + " phase=" + open.Phase +
                " stage=" + open.Stage + " count=" + open.Count + " at=" + P(open.Position) +
                " effectsApplied=" + B(open.EffectsApplied) + " disposition=" + (string.IsNullOrEmpty(open.Disposition) ? "-" : open.Disposition));
        }
        foreach (var node in moves)
        {
            if (!RebirthSmartLootingHumanOriginal.TryRead(node, out var move)) { Emit("[RBP11] receipt kind=smartLootMove row=MALFORMED"); continue; }
            Emit("[RBP11] receipt kind=smartLootMove npc=" + entityId + " op=" + N(move.Operation) + " phase=" + move.Phase +
                " attempted=" + move.Attempted + " published=" + move.Published + " cancelled=" + B(move.Cancelled) +
                " src=" + P(move.SourcePosition) + " slot=" + move.SourceSlot + " dest=" + move.DestinationSlot + " qty=" + move.Quantity);
        }
    }

    private static void Bag(List<string> args)
    {
        World world = GameManager.Instance?.World;
        if (world == null || args.Count != 2 || !int.TryParse(args[1], out int entityId))
        { Emit("[RBP11] bag result=FAIL usage: rbp11 bag <npcEntityId>"); return; }
        if (!(world.GetEntity(entityId) is EntityRebirthHumanoidNPC npc) || npc.bag?.ItemGrid?.items == null)
        { Emit("[RBP11] bag npc=" + entityId + " bag=NONE"); return; }
        string totals = Payload(npc.bag.ItemGrid.items, out int used);
        Emit("[RBP11] bag npc=" + entityId + " cells=" + npc.bag.ItemGrid.items.Length + " used=" + used +
            " digest=" + Digest(npc.bag.ItemGrid.items) + " totals=" + totals);
    }
}
#endif
