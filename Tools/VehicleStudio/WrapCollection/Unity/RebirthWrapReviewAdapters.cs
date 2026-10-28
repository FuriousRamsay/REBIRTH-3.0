// Editor-only adapters. Native renderers, materials, bundles and production
// appearance code are real; these adapters replace unrelated game services.
using UnityEngine;
public class EntityVehicle { public Transform ModelTransform; }
internal static class RebirthCamperPickupPaint { internal static void Apply(EntityVehicle e) {} }
internal static class RebirthCamperPattern
{
    internal static string Value;
    internal static string Current(EntityVehicle e)=>Value;
}
internal static class GameManager { internal static bool IsDedicatedServer=>false; }
internal sealed class Mod { internal string Path; }
internal static class ModManager
{
    internal static string Root;
    internal static Mod GetMod(string name)=>new Mod{Path=Root};
}
internal static class Log { internal static void Warning(string text)=>Debug.LogWarning(text); }
internal static class ModEvents
{
    internal struct SWorldShuttingDownData {}
    internal delegate void Handler(ref SWorldShuttingDownData data);
    internal static class WorldShuttingDown
    {
        static event Handler Handlers;
        internal static void RegisterHandler(Handler handler)=>Handlers+=handler;
        internal static void Shutdown(){var data=new SWorldShuttingDownData();Handlers?.Invoke(ref data);}
    }
}
