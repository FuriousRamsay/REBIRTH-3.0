using System;
using System.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class RemoteHostOriginal
    {
        internal bool EnterActualHotReentry(LoadedStation loaded)
        {
            if(!OwnerCurrent()||FinancialReleased||loaded?.Current()!=true||loaded.Operation!=Operation||!loaded.MatchesOriginal(Image)||
                !ReferenceEquals(loaded.World,World)||loaded.Station.ToWorldPos()!=Station.ToWorldPos()||ReferenceEquals(loaded.Station,Station)||
                loaded.Stage!=(AcceptedStation?"after":"before"))return false;
            if(OwnerCheckpoint==null){if(AcceptedStation)return false;}
            else if(!OwnerCheckpoint.CurrentSaved()||OwnerCheckpoint.AcceptedHead==null||!OwnerCheckpoint.AcceptedHead.TryGetTarget(out var head)||
                !ReferenceEquals(Peer.latestPlayerData,head)||!OwnerCheckpoint.Matches(head))return false;
            Station=loaded.Station;PendingReentry=loaded;ColdPhase=OwnerCheckpoint?.Phase??0;ColdCompleted=OwnerCheckpoint?.Completed??0;return true;
        }
    }
    private static void RecoverRemoteRecyclerHot(World world)
    {
        if(world?.IsRemote()!=false)return;
        LoadedStation[] candidates;lock(coldStationGate)candidates=loadedRecyclerStations.Where(value=>!value.Adopted&&value.SameWorld(world)).ToArray();
        foreach(var source in remoteRecyclerOriginals.Values.ToArray())
        {
            if(source.FinancialReleased||!source.OwnerCurrent())continue;
            var loaded=source.PendingReentry;
            if(loaded==null)
            {
                loaded=candidates.SingleOrDefault(value=>value.Operation==source.Operation&&value.Station.ToWorldPos()==source.Station.ToWorldPos());
                if(loaded==null||!source.EnterActualHotReentry(loaded))continue;
            }
            if(!loaded.Current()||!source.Current())continue;
            if(source.ClosedAuthority!=null&&!source.ClosedAuthority.RebindActualStation(source,loaded))continue;
            var publication=StationPublication.FromActualCold(source,loaded);if(publication==null)continue;
            if(!source.Access.RebindActualSource(source,loaded,publication)){publication.Dispose();continue;}
            var old=source.Publication;var oldLoad=source.ColdStation;
            source.Publication=publication;source.ColdStation=loaded;source.PendingReentry=null;loaded.Adopted=true;
            old?.Dispose();if(!ReferenceEquals(oldLoad,loaded))oldLoad?.Dispose();
        }
    }
}
