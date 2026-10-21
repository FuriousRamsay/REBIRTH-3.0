using System;
using System.Collections.Generic;
using System.Reflection;

class Program
{
    static int checks;
    static void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
    static EntityActivationCommand[] Commands(bool body, bool energy)
    {
        var list = new List<EntityActivationCommand> { new EntityActivationCommand("drive"), new EntityActivationCommand("service") };
        if (body) foreach (string id in new[] { RebirthCamperRadial.Storage, RebirthCamperRadial.Stations, RebirthCamperRadial.Turrets, RebirthCamperRadial.Craft }) list.Add(new EntityActivationCommand(id));
        if (body && energy) foreach (string id in new[] { RebirthCamperRadial.Energy, RebirthCamperRadial.PoiOn, RebirthCamperRadial.PoiOff }) list.Add(new EntityActivationCommand(id));
        return list.ToArray();
    }
    static void Main()
    {
        Check(!RebirthCamperRadialCache.NeedsRebuild(null, true, true), "Null cache must use native build");
        foreach (bool energy in new[] { false, true })
        {
            var bare = Commands(false, energy); var camper = Commands(true, energy);
            Check(!RebirthCamperRadialCache.NeedsRebuild(bare, false, energy), "Stable bare cache");
            Check(RebirthCamperRadialCache.NeedsRebuild(bare, true, energy), "Body installation must refresh");
            Check(!RebirthCamperRadialCache.NeedsRebuild(camper, true, energy), "Stable camper cache");
            Check(RebirthCamperRadialCache.NeedsRebuild(camper, false, energy), "Body removal must refresh");
            for (int i = 2; i < camper.Length; i++)
            {
                var incomplete = new List<EntityActivationCommand>(camper); incomplete.RemoveAt(i);
                Check(RebirthCamperRadialCache.NeedsRebuild(incomplete.ToArray(), true, energy), "Every required command must exist");
            }
        }
        Check(RebirthCamperRadialCache.NeedsRebuild(Commands(true, false), true, true), "Late energy registration");
        Check(RebirthCamperRadialCache.NeedsRebuild(Commands(true, true), true, false), "Energy unregistration");
        var prefix = typeof(RebirthCamperRadialRefresh).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);
        var vehicle = new EntityVehicle { IsCamper = true, HasBody = true, activationCommands = Commands(false, true), lastUpdateFrameOfActivationCommands = 90 };
        RebirthCamperEnergyAccess.Registered = true;
        prefix.Invoke(null, new object[] { vehicle });
        Check(vehicle.activationCommands == null && vehicle.lastUpdateFrameOfActivationCommands == -1, "Actual prefix must clear stale native caches");
        vehicle.activationCommands = Commands(true, true); var stable = vehicle.activationCommands;
        prefix.Invoke(null, new object[] { vehicle });
        Check(ReferenceEquals(stable, vehicle.activationCommands), "Stable state must keep cache");
        vehicle.IsCamper = false; vehicle.HasBody = false;
        prefix.Invoke(null, new object[] { vehicle });
        Check(ReferenceEquals(stable, vehicle.activationCommands), "Other vehicles must remain untouched");
        Console.WriteLine("PASS " + checks + " production radial-cache checks");
    }
}

// Only native object shapes are stubbed; cache decisions and the Harmony prefix
// are compiled directly from the production source file.
struct EntityActivationCommand { public string commandId; public EntityActivationCommand(string id) { commandId = id; } }
class Entity { public EntityActivationCommand[] activationCommands; public int lastUpdateFrameOfActivationCommands; public EntityActivationCommand[] GetActivationCommands() => activationCommands; }
class EntityVehicle : Entity { public bool IsCamper, HasBody; }
static class RebirthCamperBodyAttachment { public static bool HasBody(EntityVehicle v) => v.HasBody; }
static class RebirthCamperEnergyAccess { public static bool Registered; }
static class RebirthCamperRadial
{
    public const string Storage="rebirthCamperStorageManagement", Stations="rebirthCamperStationManagement", Turrets="rebirthCamperTurrets", Craft="rebirthCamperUseStation", Energy="rebirthCamperEnergyManagement", PoiOn="rebirthCamperPoiLightsOn", PoiOff="rebirthCamperPoiLightsOff";
    public static bool IsCamper(EntityVehicle v) => v != null && v.IsCamper;
}
namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)] class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) {} } }
