using System;
using System.Runtime.CompilerServices;

// Presentation intent only. The picker and server still validate installation access.
internal static class RebirthCamperStationSelectionIntent
{
    private sealed class Intent { internal EntityVehicle Vehicle; internal Guid Station; internal float Expires; }
    private static readonly ConditionalWeakTable<EntityPlayerLocal,Intent> pending = new ConditionalWeakTable<EntityPlayerLocal,Intent>();
    internal static void Set(EntityPlayerLocal player,EntityVehicle vehicle,Guid station)
    {
        pending.Remove(player);
        if(station!=Guid.Empty)pending.Add(player,new Intent{Vehicle=vehicle,Station=station,Expires=UnityEngine.Time.realtimeSinceStartup+30f});
    }
    internal static Guid Take(EntityPlayerLocal player,EntityVehicle vehicle)
    {
        if(player==null||!pending.TryGetValue(player,out var intent))return Guid.Empty;
        pending.Remove(player);
        return ReferenceEquals(intent.Vehicle,vehicle)&&UnityEngine.Time.realtimeSinceStartup<=intent.Expires?intent.Station:Guid.Empty;
    }
}
