using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Fresh stump harvest context store.
/// 
/// Ported from old REBIRTH v2.6 HarvestContext concept, but gated and command-visible.
/// No debug strings are built in the Harmony patch.
/// </summary>
public static class RebirthStumpHarvestContextStore
{
    public struct Context
    {
        public int HeldType;
        public ushort HeldSeed;
        public ushort HeldQuality;
        public BlockValue OriginalBlockValue;
        public int DestroyerEntityId;
        public bool DestroyerAttachedToVehicle;
        public float StoredAtRealtime;
        public long Generation;
        public string WorldScope;
        public WorldBase World;
    }

    public const float MaxContextAgeSeconds = 15f;

    private static readonly Dictionary<Vector3i, Context> s_byPosition = new Dictionary<Vector3i, Context>();
    private static string s_worldScope = string.Empty;
    private static WorldBase s_world;
    private static long s_nextGeneration;

    public static int Count
    {
        get { EnsureCurrentWorldScope(); PruneStale(); return s_byPosition.Count; }
    }

    public static bool TryGet(Vector3i position, out Context context)
    {
        EnsureCurrentWorldScope();
        PruneStale();
        return s_byPosition.TryGetValue(position, out context);
    }

    public static bool Remove(Vector3i position)
    {
        EnsureCurrentWorldScope();
        return s_byPosition.Remove(position);
    }

    public static bool TryReserve(Vector3i position,out Context context)
    {
        EnsureCurrentWorldScope();
        PruneStale();
        if(!s_byPosition.TryGetValue(position,out context))return false;
        return s_byPosition.Remove(position);
    }

    public static bool TryRestoreReservation(Vector3i position,Context context)
    {
        EnsureCurrentWorldScope();
        if(context.Generation<=0 || !ReferenceEquals(context.World,s_world)
            || !string.Equals(context.WorldScope,s_worldScope,StringComparison.Ordinal)
            || s_byPosition.ContainsKey(position) || IsExpired(context,Time.realtimeSinceStartup))return false;
        s_byPosition[position]=context;
        return true;
    }

    public static bool TryGetNewest(out Vector3i position, out Context context)
    {
        EnsureCurrentWorldScope();
        PruneStale();

        position = default(Vector3i);
        context = default(Context);

        bool found = false;
        float newest = -1f;

        foreach (KeyValuePair<Vector3i, Context> kvp in s_byPosition)
        {
            if (found && kvp.Value.StoredAtRealtime <= newest)
                continue;

            found = true;
            newest = kvp.Value.StoredAtRealtime;
            position = kvp.Key;
            context = kvp.Value;
        }

        return found;
    }

    public static void Clear()
    {
        s_byPosition.Clear();
        s_worldScope=string.Empty;
        s_world=null;
    }

    public static void PruneStale()
    {
        if (s_byPosition.Count == 0)
            return;

        float now = Time.realtimeSinceStartup;
        List<Vector3i> remove = null;

        foreach (KeyValuePair<Vector3i, Context> kvp in s_byPosition)
        {
            if (!IsExpired(kvp.Value, now))
                continue;

            if (remove == null)
                remove = new List<Vector3i>();

            remove.Add(kvp.Key);
        }

        if (remove == null)
            return;

        for (int i = 0; i < remove.Count; i++)
            s_byPosition.Remove(remove[i]);
    }

    public static bool TryStoreFromBlockDestroyed(
        Block block,
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        int entityId)
    {
        if (!RebirthStumpHarvestContextPolicy.CanStoreContext(world))
            return false;

        if (block == null)
            return false;

        if (RebirthStumpHarvestContextPolicy.RequireStumpName && !IsLikelyStump(block))
            return false;

        EntityPlayer player = TryGetPlayer(world, entityId);
        if (RebirthStumpHarvestContextPolicy.RequirePlayerDestroyer && player == null)
            return false;

        bool attachedToVehicle = IsPlayerAttachedToVehicle(player);
        if (RebirthStumpHarvestContextPolicy.RequireVehicleAttachedPlayer && !attachedToVehicle)
            return false;

        int heldType=0; ushort heldSeed=0; ushort heldQuality=0;
        if (player != null)
        {
            try
            {
                ItemValue held=player.inventory.holdingItemItemValue;
                if(held!=null){heldType=held.type;heldSeed=held.Seed;heldQuality=held.Quality;}
            }
            catch{}
        }

        EnsureScope(world);
        PruneStale();

        s_byPosition[blockPos] = new Context
        {
            HeldType = heldType,
            HeldSeed = heldSeed,
            HeldQuality = heldQuality,
            OriginalBlockValue = blockValue,
            DestroyerEntityId = player != null ? player.entityId : 0,
            DestroyerAttachedToVehicle = attachedToVehicle,
            StoredAtRealtime = Time.realtimeSinceStartup,
            Generation = System.Threading.Interlocked.Increment(ref s_nextGeneration),
            WorldScope = s_worldScope,
            World = s_world
        };

        return true;
    }

    public static string GetSummaryReport()
    {
        EnsureCurrentWorldScope();
        PruneStale();

        return "[RebirthStumpHarvestContextStore] count: " + s_byPosition.Count
            + "; maxAgeSeconds: " + MaxContextAgeSeconds;
    }

    public static string GetListReport()
    {
        EnsureCurrentWorldScope();
        PruneStale();

        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine(GetSummaryReport());

        foreach (KeyValuePair<Vector3i, Context> kvp in s_byPosition)
        {
            Context c = kvp.Value;
            sb.Append("  pos=").Append(kvp.Key)
              .Append(" destroyerEntityId=").Append(c.DestroyerEntityId)
              .Append(" attachedToVehicle=").Append(c.DestroyerAttachedToVehicle)
              .Append(" heldType=").Append(c.HeldType)
              .Append(" heldSeed=").Append(c.HeldSeed)
              .Append(" heldQuality=").Append(c.HeldQuality)
              .Append(" origType=").Append(c.OriginalBlockValue.type)
              .AppendLine();
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        return "[RebirthStumpHarvestContextStore] gated by policy; server-only default; prunes stale entries; no patch logging.";
    }

    private static bool IsExpired(Context context, float now)
    {
        // A retry retains the original event lifetime. A clock reset also invalidates evidence.
        return now < context.StoredAtRealtime || now - context.StoredAtRealtime > MaxContextAgeSeconds;
    }

    private static void EnsureCurrentWorldScope()
    {
        WorldBase world=GameManager.Instance!=null?GameManager.Instance.World:null;
        EnsureScope(world);
    }

    private static void EnsureScope(WorldBase world)
    {
        string save=string.Empty;
        try{save=GameIO.GetSaveGameDir()??string.Empty;}catch{}
        if(ReferenceEquals(s_world,world) && string.Equals(s_worldScope,save,StringComparison.Ordinal))return;
        s_byPosition.Clear();
        s_worldScope=save;
        s_world=world;
    }

    private static bool IsLikelyStump(Block block)
    {
        string blockName = string.Empty;
        string indexName = string.Empty;

        try { blockName = block.GetBlockName() ?? string.Empty; } catch { blockName = string.Empty; }
        try { indexName = block.IndexName ?? string.Empty; } catch { indexName = string.Empty; }

        return (!string.IsNullOrEmpty(blockName) && blockName.IndexOf("stump", System.StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrEmpty(indexName) && indexName.IndexOf("stump", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static EntityPlayer TryGetPlayer(WorldBase world, int entityId)
    {
        if (world == null)
            return null;

        try
        {
            Entity entity = world.GetEntity(entityId);
            return entity as EntityPlayer;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPlayerAttachedToVehicle(EntityPlayer player)
    {
        if (player == null)
            return false;

        try
        {
            return player.AttachedToEntity is EntityVehicle;
        }
        catch
        {
            return false;
        }
    }
}
