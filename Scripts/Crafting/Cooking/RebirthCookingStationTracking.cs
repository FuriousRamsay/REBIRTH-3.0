using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authoritative active-station leases; presentation never owns a chunk observer.</summary>
public static class RebirthCookingStationTracking
{
    private sealed class Lease { public ChunkManager.ChunkObserver Observer; public bool Wanted; }
    private static readonly Dictionary<Vector3i, Lease> leases = new Dictionary<Vector3i, Lease>();
    private static World world;
    public static void BeginFrame(World current)
    {
        if (!ReferenceEquals(world, current)) { Reset(); world = current; }
        foreach (Lease lease in leases.Values) lease.Wanted = false;
    }
    public static void Watch(World current, Vector3i position, string reason)
    {
        if (current == null || current.IsRemote() || current.m_ChunkManager == null) return;
        if (!ReferenceEquals(world, current)) { Reset(); world = current; }
        Lease lease;
        if (!leases.TryGetValue(position, out lease))
        {
            // Several jobs/players at one station share this observer. No meshes requested.
            lease = new Lease { Observer = current.m_ChunkManager.AddChunkObserver(new Vector3(position.x,position.y,position.z), false, 1, -1) };
            leases[position] = lease;
        }
        lease.Wanted = true;
    }
    public static void EndFrame()
    {
        var remove = new List<Vector3i>();
        foreach (var pair in leases) if (!pair.Value.Wanted) remove.Add(pair.Key);
        foreach (Vector3i key in remove)
        {
            Lease lease = leases[key];
            try { if (world?.m_ChunkManager != null && lease.Observer != null) world.m_ChunkManager.RemoveChunkObserver(lease.Observer); }
            catch (Exception ex) { Log.Warning("[REBIRTH Cooking] observer release failed: " + ex.Message); continue; }
            leases.Remove(key);
        }
    }
    public static void Reset()
    {
        foreach (Lease lease in leases.Values)
            try { if (world?.m_ChunkManager != null && lease.Observer != null) world.m_ChunkManager.RemoveChunkObserver(lease.Observer); }
            catch (Exception ex) { Log.Warning("[REBIRTH Cooking] observer reset failed: " + ex.Message); }
        leases.Clear(); world = null;
    }
}
