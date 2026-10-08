using System.Collections.Generic;
using UnityEngine;

/// <summary>Server-only, module-free practice for actually travelling with an owned drone.</summary>
public static class RebirthDroneTravelTrainingService
{
    private sealed class Trip
    {
        public int Drone;
        public Vector3 Anchor, Owner, Device;
        public float SampleTime, Distance, MovingSeconds;
    }
    private static readonly Dictionary<int, Trip> Trips = new Dictionary<int, Trip>();
    private static readonly HashSet<int> Seen = new HashSet<int>();
    private static readonly List<int> Remove = new List<int>();
    private static float nextSample;

    public static void Reset() { Trips.Clear(); Seen.Clear(); Remove.Clear(); nextSample = 0f; }

    // Called only after the existing server/world-progression authority gate.
    public static void Tick(World world, float now)
    {
        if (now < nextSample) return;
        nextSample = now + 1f;
        Seen.Clear();
        var entities = world.Entities?.list;
        if (entities == null) { Trips.Clear(); return; }
        for (int i = 0; i < entities.Count; i++)
        {
            var drone = entities[i] as EntityDrone;
            if (drone == null || drone.IsDead() || drone.OrderState != EntityDrone.Orders.Follow) continue;
            var owner = world.GetEntity(drone.belongsPlayerId) as EntityPlayer;
            if (owner == null || owner.IsDead() || Vector3.Distance(owner.position, drone.position) > 30f) continue;
            if (!Seen.Add(owner.entityId)) continue; // Multiple drones never multiply XP.
            Trip trip;
            if (!Trips.TryGetValue(owner.entityId, out trip) || trip.Drone != drone.entityId)
            {
                Trips[owner.entityId] = NewTrip(owner, drone, now);
                continue;
            }
            float dt = now - trip.SampleTime;
            float ownerStep = Vector3.Distance(owner.position, trip.Owner);
            float droneStep = Vector3.Distance(drone.position, trip.Device);
            // Gaps, teleports and recovery warps are not travelled distance.
            if (dt <= 0f || dt > 5f || ownerStep > 12f * dt || droneStep > 20f * dt)
            {
                Trips[owner.entityId] = NewTrip(owner, drone, now);
                continue;
            }
            trip.SampleTime = now; trip.Owner = owner.position; trip.Device = drone.position;
            if (ownerStep < 0.5f || droneStep < 0.25f) continue;
            trip.Distance += ownerStep;
            trip.MovingSeconds += dt;
            if (trip.Distance < 200f || trip.MovingSeconds < 60f || Vector3.Distance(trip.Anchor, owner.position) < 100f) continue;
            // Persisted owner-scoped cooldown survives pickup, drone changes and reconnects.
            RebirthSkillAwardService.TryAward(owner, "skill.drone_operations",
                RebirthProgressionRuntimeConfig.DroneStockRecoveryAward, "drone-stock-travel",
                RebirthProgressionRuntimeConfig.DroneStockRecoveryRepeatSeconds, out _, out _);
            Trips[owner.entityId] = NewTrip(owner, drone, now);
        }
        Remove.Clear();
        foreach (int id in Trips.Keys) if (!Seen.Contains(id)) Remove.Add(id);
        foreach (int id in Remove) Trips.Remove(id);
    }

    private static Trip NewTrip(EntityPlayer owner, EntityDrone drone, float now)
    {
        return new Trip { Drone = drone.entityId, Anchor = owner.position, Owner = owner.position,
            Device = drone.position, SampleTime = now };
    }
}
