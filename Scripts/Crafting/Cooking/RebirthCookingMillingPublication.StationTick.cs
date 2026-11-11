using System;
using System.Linq;

internal static partial class RebirthCookingMillingPublication
{
    internal readonly struct MillingStationTick
    {
        internal readonly TileEntityWorkstation Station, Previous;
        internal MillingStationTick(TileEntityWorkstation station, TileEntityWorkstation previous)
        { Station=station; Previous=previous; }
    }

    [ThreadStatic]private static TileEntityWorkstation millingStationTick;
    internal static MillingStationTick EnterMillingStationTick(TileEntityWorkstation station,bool ran)
    {
        if(!ran||station==null||station is RebirthCamperStationState)return default;
        var scope=new MillingStationTick(station,millingStationTick);
        millingStationTick=station;
        return scope;
    }

    internal static void ObserveMillingStationTickReturned(TileEntityWorkstation station,bool ran,MillingStationTick state)
    {
        if(!ran||state.Station==null||!ReferenceEquals(state.Station,station)||!ReferenceEquals(millingStationTick,station))return;
        foreach(var original in pending)
        {
            if(!IsPendingMillingStationTick(original,station))continue;
            ObservePendingMillingStationTick(station);
            return;
        }
    }

    private static bool IsPendingMillingStationTick(Original original,TileEntityWorkstation station)
        =>ReferenceEquals(original.Station,station)&&original.Vehicle==null&&original.NativeAccepted&&!original.StationTickReturned;

    private static void ObservePendingMillingStationTick(TileEntityWorkstation station)
    {
        foreach(var original in pending.Where(value=>IsPendingMillingStationTick(value,station)).ToArray())
        {
            if(!original.MetadataCurrent()||!original.NativeTerminal()||
                !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out var body,out int elapsed))continue;
            original.NativeAfter=body;original.AfterOffset=elapsed;original.NativeAfterMode="ToServer";original.StationTickReturned=true;TryFinish(original);
        }
    }
    internal static void EndMillingStationTick(MillingStationTick state)
    {if(state.Station!=null&&ReferenceEquals(millingStationTick,state.Station))millingStationTick=state.Previous;}
}
