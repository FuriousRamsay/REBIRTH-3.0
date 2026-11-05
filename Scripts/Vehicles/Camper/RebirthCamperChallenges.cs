using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Credits the Purge camper chapter from existing camper state. The server observes each
/// camper once a second and only awards challenge stats; it never changes the camper.
/// </summary>
internal static class RebirthCamperChallenges
{
    internal const string Drive = "rebirth.camper.drive", Camp = "rebirth.camper.camp", Lights = "rebirth.camper.lights",
        Workshop = "rebirth.camper.workshop", Pantry = "rebirth.camper.pantry", Dinner = "rebirth.camper.dinner",
        ForwardBase = "rebirth.camper.forwardbase", Wrap = "rebirth.camper.wrap";
    private const string FirstCampBiomeVar = "_rbCamperFirstCampBiome";
    private const int RequiredLights = 5, DriveCreditMeters = 25;
    private const float RequiredLightSeconds = 300f, MaximumMetersPerTick = 60f, SlowPollSeconds = 5f;

    private sealed class Progress
    {
        internal Vector3 LastPosition;
        internal bool HasPosition, WasCamping, LightsCredited, WorkshopCredited, PantryCredited, WrapCredited;
        internal float DriveMeters, LightSeconds, NextSlowPoll;
    }
    private static readonly ConditionalWeakTable<EntityVehicle, Progress> Tracked = new ConditionalWeakTable<EntityVehicle, Progress>();
    private static float nextTick;

    internal static void Tick(ref ModEvents.SGameUpdateData data)
    {
        float now = Time.realtimeSinceStartup;
        if (now < nextTick) return;
        nextTick = now + 1f;
        var world = GameManager.Instance?.World;
        var vehicles = VehicleManager.Instance?.vehiclesActive;
        if (world == null || world.IsRemote() || vehicles == null || !RebirthSandboxOptionManager.Current.IsPurge) return;
        for (int i = 0; i < vehicles.Count; i++)
        {
            var vehicle = vehicles[i];
            if (vehicle == null || vehicle.world != world || !RebirthCamperRadial.IsCamper(vehicle)) continue;
            try { Observe(world, vehicle, Tracked.GetOrCreateValue(vehicle), now); }
            catch (Exception error) { Log.Warning("[REBIRTH Camper Challenges] " + error.Message); }
        }
    }

    private static void Observe(World world, EntityVehicle vehicle, Progress progress, float now)
    {
        ObserveDriving(vehicle, progress);
        var owner = Owner(world, vehicle);
        if (owner == null) return;
        ObserveCamp(world, vehicle, owner, progress);
        if (now < progress.NextSlowPoll) return;
        progress.NextSlowPoll = now + SlowPollSeconds;
        ObserveModules(vehicle, owner, progress);
        ObserveWrap(vehicle, owner, progress);
    }

    private static EntityPlayer Owner(World world, EntityVehicle vehicle)
    {
        var id = vehicle.GetOwner();
        var data = id == null ? null : GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(id);
        return data == null ? null : world.GetEntity(data.EntityId) as EntityPlayer;
    }

    private static void ObserveDriving(EntityVehicle vehicle, Progress progress)
    {
        Vector3 position = vehicle.position;
        float meters = progress.HasPosition ? Vector3.Distance(position, progress.LastPosition) : 0f;
        progress.LastPosition = position;
        progress.HasPosition = true;
        var driver = vehicle.AttachedMainEntity as EntityPlayer;
        // A long jump in one second is a teleport or recovery, not driving.
        if (driver == null || meters > MaximumMetersPerTick) return;
        progress.DriveMeters += meters;
        if (progress.DriveMeters < DriveCreditMeters) return;
        int credit = (int)progress.DriveMeters;
        progress.DriveMeters -= credit;
        Credit(driver, Drive, credit);
    }

    private static void ObserveCamp(World world, EntityVehicle vehicle, EntityPlayer owner, Progress progress)
    {
        bool camping = RebirthCamperEnergyRuntime.TryObserveMode(vehicle, out RebirthCamperEnergyMode mode) && mode == RebirthCamperEnergyMode.Camp;
        if (camping && !progress.WasCamping)
        {
            Credit(owner, Camp, 1);
            ObserveCampBiome(world, vehicle, owner);
        }
        progress.WasCamping = camping;
        if (!camping) { progress.LightSeconds = 0f; return; }
        if (progress.LightsCredited) return;
        // Demand proves the lights are metered, not merely switched on with an empty bank.
        bool on = RebirthCamperEnergyDeviceAdapter.TryObservePoiOn(vehicle, out bool poiOn) && poiOn;
        var poi = on ? RebirthCamperEnergyDeviceAdapter.ObservePoi(vehicle) : null;
        if (poi == null || poi.SelectedLights < RequiredLights || poi.DemandWatts <= 0) { progress.LightSeconds = 0f; return; }
        progress.LightSeconds += 1f;
        if (progress.LightSeconds < RequiredLightSeconds) return;
        progress.LightsCredited = true;
        Credit(owner, Lights, 1);
    }

    private static void ObserveCampBiome(World world, EntityVehicle vehicle, EntityPlayer owner)
    {
        var provider = world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
        var biome = provider?.GetBiomeProvider()?.GetBiomeAt((int)vehicle.position.x, (int)vehicle.position.z);
        if (biome == null || owner.Buffs == null) return;
        // Stored as id+1 because an unset custom variable reads as zero.
        float current = biome.m_Id + 1;
        float first = owner.Buffs.GetCustomVar(FirstCampBiomeVar);
        if (first == 0f) owner.Buffs.SetCustomVar(FirstCampBiomeVar, current);
        else if (first != current) Credit(owner, ForwardBase, 1);
    }

    private static void ObserveModules(EntityVehicle vehicle, EntityPlayer owner, Progress progress)
    {
        if (progress.WorkshopCredited && progress.PantryCredited) return;
        if (!RebirthCamperModuleRuntime.TryGetExisting(vehicle, out var runtime) || runtime?.Storage == null || runtime.Stations == null) return;
        for (int slot = 0; slot < RebirthCamperCraftingStation.ModuleSlots; slot++)
        {
            if (!progress.WorkshopCredited && runtime.Stations.GetSlot(slot) != null)
            { progress.WorkshopCredited = true; Credit(owner, Workshop, 1); }
            if (!progress.PantryCredited && runtime.Storage.GetSlot(slot)?.IsEmpty() == false)
            { progress.PantryCredited = true; Credit(owner, Pantry, 1); }
        }
    }

    private static void ObserveWrap(EntityVehicle vehicle, EntityPlayer owner, Progress progress)
    {
        if (progress.WrapCredited) return;
        var item = vehicle.vehicle?.GetUpdatedItemValue();
        var mods = item?.cosmeticMods;
        if (mods == null) return;
        foreach (var mod in mods)
        {
            string name = mod == null || mod.IsEmpty() ? null : mod.ItemClass?.GetItemName();
            if (name == null || !name.StartsWith("modVehicleWrap", StringComparison.Ordinal) ||
                RebirthPurgePlayerWraps.IsStarterWrap(item, name)) continue;
            progress.WrapCredited = true;
            Credit(owner, Wrap, 1);
            return;
        }
    }

    internal static void CreditMeal(EntityPlayer player, Recipe recipe)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge || recipe == null) return;
        if (RebirthServiceCraftSkillService.ClassifyRecipe(recipe) == "skill.cooking") Credit(player, Dinner, 1);
    }

    private static void Credit(EntityPlayer player, string stat, int count)
    {
        if (player == null) return;
        if (player is EntityPlayerLocal) { QuestEventManager.Current?.ChallengeAwardCredited(stat, count); return; }
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection != null && connection.IsServer)
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperChallengeCredit>().Setup(stat, count), _attachedToEntityId: player.entityId);
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperChallengeCredit : NetPackage
{
    private string stat;
    private int count;
    internal NetPackageRebirthCamperChallengeCredit Setup(string challengeStat, int amount) { stat = challengeStat; count = amount; return this; }
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public override void read(PooledBinaryReader reader) { stat = reader.ReadString(); count = reader.ReadInt32(); }
    public override void write(PooledBinaryWriter writer) { base.write(writer); writer.Write(stat ?? string.Empty); writer.Write(count); }
    public int GetLength() => 64;
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        // Only this feature's stats, in a sane range: a package cannot complete other lessons.
        if (string.IsNullOrEmpty(stat) || !stat.StartsWith("rebirth.camper.", StringComparison.Ordinal) || count <= 0 || count > 1000) return;
        QuestEventManager.Current?.ChallengeAwardCredited(stat, count);
    }
}

[Preserve]
public sealed class RebirthCamperChallengesModApi : IModApi
{
    public void InitMod(Mod mod) { ModEvents.GameUpdate.RegisterHandler(RebirthCamperChallenges.Tick); }
}
