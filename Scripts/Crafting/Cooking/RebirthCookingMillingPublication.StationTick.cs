using System;
using System.Linq;

internal static partial class RebirthCookingMillingPublication
{
    private sealed class MillingStationTick{internal TileEntityWorkstation Station,Previous;}
    [ThreadStatic]private static TileEntityWorkstation millingStationTick;
    internal static object EnterMillingStationTick(TileEntityWorkstation station,bool ran)
    {
        if(!ran||station==null||station is RebirthCamperStationState)return null;
        var scope=new MillingStationTick{Station=station,Previous=millingStationTick};millingStationTick=station;return scope;
    }
    internal static void ObserveMillingStationTickReturned(TileEntityWorkstation station,bool ran,object state)
    {
        if(!ran||state is not MillingStationTick scope||!ReferenceEquals(scope.Station,station)||!ReferenceEquals(millingStationTick,station))return;
        foreach(var original in pending.Where(value=>ReferenceEquals(value.Station,station)&&value.Vehicle==null&&value.NativeAccepted&&!value.StationTickReturned).ToArray())
        {
            if(!original.MetadataCurrent()||!original.NativeTerminal()||
                !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out var body,out int elapsed))continue;
            original.NativeAfter=body;original.AfterOffset=elapsed;original.NativeAfterMode="ToServer";original.StationTickReturned=true;TryFinish(original);
        }
    }
    internal static void EndMillingStationTick(object state)
    {if(state is MillingStationTick scope&&ReferenceEquals(millingStationTick,scope.Station))millingStationTick=scope.Previous;}
}
