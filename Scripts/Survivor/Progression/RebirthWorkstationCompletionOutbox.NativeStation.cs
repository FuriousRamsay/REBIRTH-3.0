using System;

internal static partial class RebirthWorkstationCompletionOutbox
{
    private static bool BindAcceptedStationDescription(State state,string birth)
    {
        if(birth==null)return true;
        if(state.NativeStationBirth!=null)return state.NativeStationBirth==birth;
        state.NativeStationBirth=birth;return Save(state);
    }
    // Only existing actual delivery/physical-successor producers supply these
    // native arguments. Recovery reads supply none and cannot mint a binding.
    private static bool BindActualNativeStation(State state,World world,TileEntityWorkstation station,bool publish=true)
    {
        if(world==null&&station==null)return true;
        if(state?.Role=="client")return true; // Existing native client resend handles do not issue host station identity.
        if(state==null||world==null||station==null||!ThreadManager.IsMainThread()||state.Role!="server"||world.IsRemote()||
            !ReferenceEquals(GameManager.Instance?.World,world)||CurrentGuid(world)!=state.Original.WorldGuid||
            !ReferenceEquals(world.GetTileEntity(state.Original.Position),station)||station.block?.GetBlockName()!=state.Original.Block)return false;
        if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var birth))return true; // Legacy warm custody remains valid; no cold binding is invented.
        if(state.NativeStationBirth!=null&&state.NativeStationBirth!=birth)return false;
        bool changed=state.NativeStationBirth==null;state.NativeStationBirth=birth;state.NativeWorld=world;state.NativeStation=station;
        return !changed||!publish||Save(state);
    }
    internal static bool TryRestoreUnattemptedNativeStation(State state)
    {
        if(state==null||state.Role!="server"||state.ReagentAttempted||!state.Saved||state.NativeStationBirth==null||!ThreadManager.IsMainThread()||
            GameManager.Instance?.World is not World world||world.IsRemote()||CurrentGuid(world)!=state.Original.WorldGuid||Root("server")!=state.Root||
            !TryReadSaved(state.Original,out var exact)||!ReferenceEquals(exact,state)||world.GetTileEntity(state.Original.Position) is not TileEntityWorkstation station||
            station.block?.GetBlockName()!=state.Original.Block||!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var birth)||birth!=state.NativeStationBirth)return false;
        state.NativeWorld=world;state.NativeStation=station;return true;
    }
}
