using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class QuickStackAcceptedCategoryRegistry
{
    private const string FileName = "RebirthQuickStackContainerCategories.xml";
    private static readonly Dictionary<Vector3i, HashSet<QuickStackCategory>> accepted = new Dictionary<Vector3i, HashSet<QuickStackCategory>>();
    private static bool loaded;
    private static bool dirty;

    public static bool Accepts(Vector3i position, QuickStackCategory category)
    {
        if (category == QuickStackCategory.None) return false;
        EnsureLoaded();
        HashSet<QuickStackCategory> values;
        return accepted.TryGetValue(position, out values) && values.Contains(category);
    }

    public static bool HasAny(Vector3i position)
    {
        EnsureLoaded();
        HashSet<QuickStackCategory> values;
        return accepted.TryGetValue(position, out values) && values.Count > 0;
    }

    public static QuickStackCategory[] Get(Vector3i position)
    {
        EnsureLoaded();
        HashSet<QuickStackCategory> values;
        if (!accepted.TryGetValue(position, out values)) return new QuickStackCategory[0];
        QuickStackCategory[] result = new QuickStackCategory[values.Count];
        values.CopyTo(result);
        return result;
    }

    /// <summary>
    /// Resolves a storage tile entity from either its canonical block position or
    /// a clicked child position of a multiblock container.
    /// </summary>
    public static TileEntity ResolveTileEntity(WorldBase world, Vector3i position)
    {
        if (world == null) return null;
        TileEntity tileEntity = world.GetTileEntity(position);
        if (tileEntity != null) return tileEntity;

        BlockValue blockValue = world.GetBlock(position);
        if (!blockValue.ischild) return null;
        Vector3i parent = blockValue.Block.multiBlockPos.GetParentPos(position, blockValue);
        return world.GetTileEntity(parent);
    }

    /// <summary>
    /// Returns category-assigned storage positions within range. This deliberately
    /// does not depend on the Remote Resource registry: assigning Quick Stack
    /// categories is itself sufficient to make a persistent storage container a
    /// Quick Stack candidate. Saved child/multiblock positions are normalized to
    /// the tile entity's canonical world position when they can be resolved.
    /// </summary>
    public static List<Vector3i> QueryPositions(World world, Vector3 center, float radius)
    {
        EnsureLoaded();
        List<Vector3i> result = new List<Vector3i>();
        HashSet<Vector3i> seen = new HashSet<Vector3i>();
        float radiusSquared = radius * radius;

        // Keep old saves compatible if a category was stored against a clicked
        // child/multiblock coordinate rather than TileEntity.ToWorldPos().
        Dictionary<Vector3i, HashSet<QuickStackCategory>> migrations =
            new Dictionary<Vector3i, HashSet<QuickStackCategory>>();
        List<Vector3i> removeAliases = new List<Vector3i>();

        foreach (KeyValuePair<Vector3i, HashSet<QuickStackCategory>> pair in accepted)
        {
            if (pair.Value == null || pair.Value.Count == 0) continue;

            Vector3i canonical = pair.Key;
            TileEntity tileEntity = ResolveTileEntity(world, pair.Key);
            if (tileEntity != null) canonical = tileEntity.ToWorldPos();

            if (canonical != pair.Key)
            {
                HashSet<QuickStackCategory> merged;
                if (!migrations.TryGetValue(canonical, out merged))
                {
                    merged = new HashSet<QuickStackCategory>();
                    migrations[canonical] = merged;
                }
                merged.UnionWith(pair.Value);
                removeAliases.Add(pair.Key);
            }

            if ((canonical.ToVector3() - center).sqrMagnitude <= radiusSquared &&
                seen.Add(canonical))
                result.Add(canonical);
        }

        bool changed = false;
        for (int i = 0; i < removeAliases.Count; i++)
            changed |= accepted.Remove(removeAliases[i]);

        foreach (KeyValuePair<Vector3i, HashSet<QuickStackCategory>> migration in migrations)
        {
            HashSet<QuickStackCategory> values;
            if (!accepted.TryGetValue(migration.Key, out values))
            {
                values = new HashSet<QuickStackCategory>();
                accepted[migration.Key] = values;
                changed = true;
            }
            int before = values.Count;
            values.UnionWith(migration.Value);
            if (values.Count != before) changed = true;
        }

        if (changed)
        {
            // Query paths may normalize legacy multiblock aliases in memory, but must not
            // synchronously write storage while ordinary crafting/Quick Stack is reading.
            dirty = true;
            RemoteResourceSnapshotCache.InvalidateAll();
        }

        result.Sort(delegate(Vector3i a, Vector3i b)
        {
            int distance = (a.ToVector3() - center).sqrMagnitude.CompareTo(
                (b.ToVector3() - center).sqrMagnitude);
            if (distance != 0) return distance;
            int x = a.x.CompareTo(b.x); if (x != 0) return x;
            int y = a.y.CompareTo(b.y); if (y != 0) return y;
            return a.z.CompareTo(b.z);
        });
        return result;
    }


    public static int GetMask(Vector3i position)
    {
        EnsureLoaded();
        HashSet<QuickStackCategory> values;
        if (!accepted.TryGetValue(position, out values)) return 0;
        int mask = 0;
        foreach (QuickStackCategory value in values) mask |= 1 << ((int)value - 1);
        return mask;
    }

    public static void SetMask(Vector3i position, int mask)
    {
        EnsureLoaded();
        HashSet<QuickStackCategory> values = new HashSet<QuickStackCategory>();
        foreach (QuickStackCategory value in Enum.GetValues(typeof(QuickStackCategory)))
        {
            if (value == QuickStackCategory.None) continue;
            int bit = 1 << ((int)value - 1);
            if ((mask & bit) != 0) values.Add(value);
        }
        if (values.Count == 0) accepted.Remove(position); else accepted[position] = values;
        Save();
        RemoteResourceSnapshotCache.InvalidateAll();
    }

    public static void Set(Vector3i position, QuickStackCategory category, bool enabled)
    {
        if (category == QuickStackCategory.None) return;
        EnsureLoaded();
        HashSet<QuickStackCategory> values;
        if (!accepted.TryGetValue(position, out values))
        {
            values = new HashSet<QuickStackCategory>();
            accepted[position] = values;
        }
        if (enabled) values.Add(category); else values.Remove(category);
        if (values.Count == 0) accepted.Remove(position);
        Save();
        RemoteResourceSnapshotCache.InvalidateAll();
    }

    public static void FlushPending()
    {
        if (!loaded || !dirty) return;
        Save();
    }

    public static void ClearWorld()
    {
        accepted.Clear();
        loaded = false;
        dirty = false;
    }

    private static string PathName { get { return Path.Combine(GameIO.GetSaveGameDir(), FileName); } }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        dirty = false;
        accepted.Clear();
        string path = PathName;
        if (string.IsNullOrEmpty(path)) return;
        Dictionary<Vector3i, HashSet<QuickStackCategory>> staged;
        string error;
        if (!TryLoadFile(path, out staged, out error))
        {
            string primaryError = error;
            if (!TryLoadFile(path + ".bak", out staged, out error))
            {
                if (File.Exists(path) || File.Exists(path + ".bak"))
                    Log.Warning("[QuickStack] category persistence load failed: primary=" + primaryError + " backup=" + error);
                return;
            }
            Log.Warning("[QuickStack] category persistence recovered from backup after primary=" + primaryError);
        }
        foreach (KeyValuePair<Vector3i, HashSet<QuickStackCategory>> pair in staged)
            accepted[pair.Key] = pair.Value;
    }

    private static bool TryLoadFile(string path, out Dictionary<Vector3i, HashSet<QuickStackCategory>> staged, out string error)
    {
        staged = new Dictionary<Vector3i, HashSet<QuickStackCategory>>();
        error = string.Empty;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) { error = "missing"; return false; }
        try
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "quickStackCategories") throw new InvalidDataException("invalid root");
            XmlNodeList nodes = root.SelectNodes("container");
            if (nodes != null) foreach (XmlNode node in nodes)
            {
                XmlAttribute ax=node.Attributes["x"], ay=node.Attributes["y"], az=node.Attributes["z"];
                int x,y,z;
                if (ax==null||ay==null||az==null || !int.TryParse(ax.Value,out x)||!int.TryParse(ay.Value,out y)||!int.TryParse(az.Value,out z))
                    throw new InvalidDataException("invalid container coordinate");
                HashSet<QuickStackCategory> values = new HashSet<QuickStackCategory>();
                XmlAttribute av=node.Attributes["values"];
                string[] parts=(av!=null?av.Value:string.Empty).Split(',');
                for(int i=0;i<parts.Length;i++){QuickStackCategory value;if(Enum.TryParse(parts[i],true,out value)&&value!=QuickStackCategory.None)values.Add(value);}
                if(values.Count>0)staged[new Vector3i(x,y,z)]=values;
            }
            return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;staged.Clear();return false;}
    }

    private static bool Save()
    {
        string path = PathName;
        if (string.IsNullOrEmpty(path)) return false;
        dirty = true;
        string temp = path + ".tmp";
        try
        {
            string directory=Path.GetDirectoryName(path);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true };
            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                writer.WriteStartDocument(); writer.WriteStartElement("quickStackCategories");
                foreach (KeyValuePair<Vector3i, HashSet<QuickStackCategory>> pair in accepted)
                {
                    writer.WriteStartElement("container");
                    writer.WriteAttributeString("x", pair.Key.x.ToString()); writer.WriteAttributeString("y", pair.Key.y.ToString()); writer.WriteAttributeString("z", pair.Key.z.ToString());
                    writer.WriteAttributeString("values", string.Join(",", pair.Value)); writer.WriteEndElement();
                }
                writer.WriteEndElement(); writer.WriteEndDocument(); writer.Flush(); stream.Flush(true);
            }
            string publishError;
            if(!RebirthDurableFileCommit.TryPublish(temp,path,out publishError))throw new IOException("durable publication failed: "+publishError);
            dirty=false; return true;
        }
        catch(Exception ex){Log.Warning("[QuickStack] category persistence save failed; state remains dirty for retry: "+ex.Message);return false;}
    }

}

public static class QuickStackRestockService
{
    public static void RequestLocal(bool owned)
    {
        if (!QuickStackRuntimePolicy.Enabled) return;
        EntityPlayerLocal player = GameManager.Instance != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (player == null || persistent == null || persistent.PrimaryId == null) return;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c.IsServer) ProcessServer(GameManager.Instance.World, player.entityId, persistent.PrimaryId, owned);
        else c.SendToServer(NetPackageManager.GetPackage<NetPackageQuickStackRestockRequest>().Setup(player.entityId, persistent, owned));
    }

    public static void ProcessServer(World world, int playerId, PlatformUserIdentifierAbs userId, bool owned)
    {
        ProcessServerCore(world, playerId, userId, owned, null);
    }

    // Quick Stack radial selected-mode entry point. Empty selectedSourceKeys means
    // every visible retrieval row was unchecked, so nothing should be moved.
    public static void ProcessServerSelected(World world, int playerId, PlatformUserIdentifierAbs userId, bool owned, string selectedSourceKeys)
    {
        ProcessServerCore(world, playerId, userId, owned, selectedSourceKeys ?? string.Empty);
    }

    private static void ProcessServerCore(World world, int playerId, PlatformUserIdentifierAbs userId, bool owned, string selectedSourceKeys)
    {
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData pp = GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId);
        if (player == null || pp == null || pp.PrimaryId == null || userId == null || !pp.PrimaryId.Equals(userId) || RebirthSandboxOptionManager.Current.QuickStack == RebirthQuickStackMode.Off) return;

        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locked = player.bag.LockedSlots;
        HashSet<int> bagWanted = new HashSet<int>();
        for (int i = 0; i < bag.Length; i++)
            // Existing protected stacks are valid restock destinations and therefore
            // their item types belong in the wanted set.
            if (bag[i] != null && !bag[i].IsEmpty()) bagWanted.Add(bag[i].itemValue.type);

        Dictionary<int, int> selectedLimits = selectedSourceKeys != null
            ? ParseSelectedRestockLimits(selectedSourceKeys)
            : null;
        if (selectedLimits != null && selectedLimits.Count == 0) return;

        HashSet<int> wanted = new HashSet<int>();
        if (selectedLimits == null)
        {
            foreach (int type in bagWanted) wanted.Add(type);
        }
        else
        {
            foreach (KeyValuePair<int, int> pair in selectedLimits)
                if (pair.Value > 0 && bagWanted.Contains(pair.Key)) wanted.Add(pair.Key);
            if (wanted.Count == 0) return;
        }

        int moved = 0;
        List<QuickStackContainerEntry> candidates = QuickStackContainerRegistry.Query(world, player.position, QuickStackService.Radius);
        foreach (int type in wanted)
        {
            for (int c = 0; c < candidates.Count; c++)
            {
                TileEntity te = world.GetTileEntity(candidates[c].Position);
                TEFeatureStorage loot;
                string reason;
                if (!QuickStackService.CanUse(world, player, te, owned, out loot, out reason)) continue;
                PackedBoolArray sourceLocks = loot.ItemGrid.SlotLocks;
                for (int s = 0; s < loot.ItemGrid.items.Length; s++)
                {
                    if (sourceLocks != null && s < sourceLocks.Length && sourceLocks[s]) continue;
                    ItemStack source = loot.ItemGrid.items[s];
                    if (source == null || source.IsEmpty() || source.itemValue.type != type) continue;
                    int allowed = int.MaxValue;
                    if (selectedLimits != null)
                    {
                        if (!selectedLimits.TryGetValue(type, out allowed) || allowed <= 0) break;
                    }

                    ItemStack transfer = source.Clone();
                    if (selectedLimits != null) transfer.count = Math.Min(transfer.count, allowed);
                    int before = transfer.count;
                    FillBag(player, transfer, locked);
                    int taken = before - transfer.count;
                    if (taken > 0)
                    {
                        ItemStack after = source.Clone();
                        after.count -= taken;
                        loot.UpdateSlot(s, after.count > 0 ? after : ItemStack.Empty);
                        moved += taken;
                        if (selectedLimits != null) selectedLimits[type] = allowed - taken;
                    }
                }
            }
        }
        QuickStackDiagnostics.Write("restock committed moved=" + moved + " itemTypes=" + wanted.Count);
    }

    private static Dictionary<int, int> ParseSelectedRestockLimits(string selectedSourceKeys)
    {
        Dictionary<int, int> result = new Dictionary<int, int>();
        string[] values = (selectedSourceKeys ?? string.Empty).Split('\u001f');
        for (int i = 0; i < values.Length; i++)
        {
            string value = (values[i] ?? string.Empty).Trim();
            if (value.Length == 0) continue;
            int sep = value.LastIndexOf('\u001d');
            string key = sep >= 0 ? value.Substring(0, sep) : value;
            int maxCount = int.MaxValue;
            if (sep >= 0 && (!int.TryParse(value.Substring(sep + 1), out maxCount) || maxCount <= 0)) continue;

            string kind, sourceId; int itemType;
            if (!LogisticsPreviewService.TryParseSourceSelectionKey(key, out kind, out sourceId, out itemType) ||
                kind != "T" || !string.Equals(sourceId, "RESTOCK", StringComparison.Ordinal)) continue;
            result[itemType] = maxCount;
        }
        return result;
    }

    private static void FillBag(EntityPlayer player, ItemStack source, PackedBoolArray locked)
    {
        ItemStack[] bag = player.bag.ItemGrid.items;
        for (int i = 0; i < bag.Length && source.count > 0; i++)
        {
            // Protected occupied stacks can be topped up. They remain protected from
            // source removal and protected empty slots remain unavailable below.
            if (bag[i] == null || bag[i].IsEmpty()) continue;
            int amount;
            if (!bag[i].CanStackPartlyWith(source, out amount)) continue;
            ItemStack after = bag[i].Clone();
            after.count += amount;
            source.count -= amount;
            player.bag.SetSlot(i, after);
        }
        int maxStack = ItemClass.GetForId(source.itemValue.type).Stacknumber.Value;
        for (int i = 0; i < bag.Length && source.count > 0; i++)
        {
            if (locked != null && i < locked.Length && locked[i]) continue;
            if (bag[i] != null && !bag[i].IsEmpty()) continue;
            int amount = Math.Min(maxStack, source.count);
            player.bag.SetSlot(i, new ItemStack(source.itemValue.Clone(), amount));
            source.count -= amount;
        }
    }
}

public static class QuickStackPreviewService
{
    private static Action<QuickStackRadialAction, string> radialCallback;
    private static QuickStackRadialAction pendingAction;

    public static void RequestLocal(bool owned)
    {
        RequestForRadial(owned ? QuickStackRadialAction.DepositOwned : QuickStackRadialAction.Deposit, delegate(QuickStackRadialAction a, string text) { Show(text); });
    }

    public static void RequestForRadial(QuickStackRadialAction action, Action<QuickStackRadialAction, string> callback)
    {
        if (!QuickStackRuntimePolicy.Enabled) return;
        radialCallback = callback;
        pendingAction = action;
        EntityPlayerLocal player = GameManager.Instance != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (player == null || persistent == null || persistent.PrimaryId == null) return;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c.IsServer)
            ReceiveRadialPreview(action, BuildForAction(GameManager.Instance.World, player, action));
        else
            c.SendToServer(NetPackageManager.GetPackage<NetPackageQuickStackPreviewRequest>().Setup(player.entityId, persistent, action));
    }

    public static void ClearRadialCallback() { radialCallback = null; }

    public static void ReceiveRadialPreview(QuickStackRadialAction action, string text)
    {
        Action<QuickStackRadialAction, string> callback = radialCallback;
        if (callback != null && action == pendingAction) callback(action, text);
    }

    public static string BuildForAction(World world, EntityPlayer player, QuickStackRadialAction action)
    {
        if (action == QuickStackRadialAction.Restock) return BuildRestock(world, player);
        if (action == QuickStackRadialAction.CollectWorkstationOutputs) return QuickStackWorkstationOutputService.BuildPreview(world, player);
        return Build(world, player, action == QuickStackRadialAction.DepositOwned);
    }

    public static string Build(World world, EntityPlayer player, bool owned)
    {
        StringBuilder text = new StringBuilder();
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locked = player.bag.LockedSlots;
        RebirthQuickStackMode mode = RebirthSandboxOptionManager.Current.QuickStack;
        int lines = 0, total = 0;
        for (int i = 0; i < bag.Length && lines < 12; i++)
        {
            if (locked != null && i < locked.Length && locked[i]) continue;
            if (bag[i] == null || bag[i].IsEmpty()) continue;
            QuickStackTransferPlanner.Plan plan = QuickStackTransferPlanner.Find(world, player, bag[i], owned, mode);
            if (plan == null || plan.Destinations.Count == 0) continue;
            if (lines > 0) text.AppendLine();
            text.Append("• ").Append(ItemClass.GetForId(bag[i].itemValue.type).GetLocalizedItemName());
            text.Append(" x").Append(bag[i].count);
            total += bag[i].count;
            lines++;
        }
        if (lines == 0) return Localization.Get("xuiRebirthQuickStackPreviewNone");
        text.AppendLine().AppendLine();
        text.Append(string.Format(Localization.Get("xuiRebirthQuickStackPreviewTotals"), lines, total));
        return text.ToString();
    }

    private static string BuildRestock(World world, EntityPlayer player)
    {
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locked = player.bag.LockedSlots;
        HashSet<int> wanted = new HashSet<int>();
        for (int i = 0; i < bag.Length; i++)
            if (bag[i] != null && !bag[i].IsEmpty()) wanted.Add(bag[i].itemValue.type);

        Dictionary<int, int> amounts = new Dictionary<int, int>();
        List<QuickStackContainerEntry> candidates = QuickStackContainerRegistry.Query(world, player.position, QuickStackService.Radius);
        for (int c = 0; c < candidates.Count; c++)
        {
            TileEntity te = world.GetTileEntity(candidates[c].Position);
            TEFeatureStorage loot; string reason;
            if (!QuickStackService.CanUse(world, player, te, false, out loot, out reason)) continue;
            PackedBoolArray sourceLocks = loot.ItemGrid.SlotLocks;
            for (int s = 0; s < loot.ItemGrid.items.Length; s++)
            {
                if (sourceLocks != null && s < sourceLocks.Length && sourceLocks[s]) continue;
                ItemStack stack = loot.ItemGrid.items[s];
                if (stack == null || stack.IsEmpty() || !wanted.Contains(stack.itemValue.type)) continue;
                int current; amounts.TryGetValue(stack.itemValue.type, out current);
                amounts[stack.itemValue.type] = current + stack.count;
            }
        }
        if (amounts.Count == 0) return Localization.Get("xuiRebirthQuickStackRestockPreviewNone");
        StringBuilder text = new StringBuilder(); int lines = 0, total = 0;
        foreach (KeyValuePair<int,int> pair in amounts)
        {
            if (lines >= 12) break;
            if (lines > 0) text.AppendLine();
            text.Append("• ").Append(ItemClass.GetForId(pair.Key).GetLocalizedItemName()).Append(" x").Append(pair.Value);
            total += pair.Value; lines++;
        }
        text.AppendLine().AppendLine();
        text.Append(string.Format(Localization.Get("xuiRebirthQuickStackPreviewTotals"), lines, total));
        return text.ToString();
    }

    public static void Show(string text)
    {
        EntityPlayerLocal player = GameManager.Instance != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player != null) GameManager.ShowTooltip(player, text);
    }
}

[Preserve]
public sealed class NetPackageQuickStackRestockRequest : NetPackage
{
    private int playerId; private PlatformUserIdentifierAbs userId; private bool owned;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageQuickStackRestockRequest Setup(int id, PersistentPlayerData pp, bool own) { playerId=id; userId=pp.PrimaryId; owned=own; return this; }
    public override void read(PooledBinaryReader r) { BinaryReader b=(BinaryReader)r; playerId=b.ReadInt32(); userId=PlatformUserIdentifierAbs.FromStream(b); owned=b.ReadBoolean(); }
    public override void write(PooledBinaryWriter w) { base.write(w); BinaryWriter b=(BinaryWriter)w; b.Write(playerId); userId.ToStream(b); b.Write(owned); }
    public override void ProcessPackage(World world, GameManager callbacks) { if (world != null && ValidEntityIdForSender(playerId) && ValidUserIdForSender(userId) && !world.IsRemote()) QuickStackRestockService.ProcessServer(world, playerId, userId, owned); }
    public int GetLength() { return 32; }
}

[Preserve]
public sealed class NetPackageQuickStackPreviewRequest : NetPackage
{
    private int playerId; private PlatformUserIdentifierAbs userId; private QuickStackRadialAction action;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageQuickStackPreviewRequest Setup(int id, PersistentPlayerData pp, QuickStackRadialAction value) { playerId=id; userId=pp.PrimaryId; action=value; return this; }
    public override void read(PooledBinaryReader r) { BinaryReader b=(BinaryReader)r; playerId=b.ReadInt32(); userId=PlatformUserIdentifierAbs.FromStream(b); action=(QuickStackRadialAction)b.ReadByte(); }
    public override void write(PooledBinaryWriter w) { base.write(w); BinaryWriter b=(BinaryWriter)w; b.Write(playerId); userId.ToStream(b); b.Write((byte)action); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId) || world.IsRemote()) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null || RebirthSandboxOptionManager.Current.QuickStack == RebirthQuickStackMode.Off) return;
        string preview = QuickStackPreviewService.BuildForAction(world, player, action);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageQuickStackPreviewResult>().Setup(action, preview), _attachedToEntityId: playerId);
    }
    public int GetLength() { return 32; }
}

[Preserve]
public sealed class NetPackageQuickStackPreviewResult : NetPackage
{
    private QuickStackRadialAction action; private string text;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageQuickStackPreviewResult Setup(QuickStackRadialAction value, string preview) { action=value; text=preview ?? string.Empty; return this; }
    public override void read(PooledBinaryReader r) { BinaryReader b=(BinaryReader)r; action=(QuickStackRadialAction)b.ReadByte(); text=b.ReadString(); }
    public override void write(PooledBinaryWriter w) { base.write(w); BinaryWriter b=(BinaryWriter)w; b.Write((byte)action); b.Write(text ?? string.Empty); }
    public override void ProcessPackage(World world, GameManager callbacks) { QuickStackPreviewService.ReceiveRadialPreview(action, text); }
    public int GetLength() { return 2048; }
}

