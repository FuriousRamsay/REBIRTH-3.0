using System;
using System.Runtime.CompilerServices;
internal static class RebirthCamperEnergySelectionIntent
{
    internal sealed class Intent {internal EntityVehicle Vehicle;internal Guid Source;internal Func<bool> Current;}
    private static readonly ConditionalWeakTable<EntityPlayerLocal,Intent> pending=new ConditionalWeakTable<EntityPlayerLocal,Intent>();
    internal static void Set(EntityPlayerLocal player,EntityVehicle vehicle,Guid source,Func<bool> current)
    {pending.Remove(player);pending.Add(player,new Intent {Vehicle=vehicle,Source=source,Current=current});}
    internal static Intent Take(EntityPlayerLocal player,EntityVehicle vehicle)
    {if(player==null||!pending.TryGetValue(player,out var intent))return null;pending.Remove(player);return ReferenceEquals(intent.Vehicle,vehicle)?intent:null;}
}
