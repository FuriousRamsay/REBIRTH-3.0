using System;
using System.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static void RecoverAuthorityHot(World world)
    {
        foreach(var root in authorityOriginals.ToArray())
        {
            if(root.Source==null||root.UnusedAdmission||root.Terminal!=null||root.Station==null||
                !ReferenceEquals(root.Source.World,world)||root.PendingReentry==null&&root.Source.Current())continue;
            foreach(var loaded in loadedRecyclerStations.ToArray())
            {
                try
                {
                    if(loaded.Adopted||loaded.Operation!=root.Source.Operation||!loaded.Current())continue;
                    bool rebound=ReferenceEquals(root.Source.Station,loaded.Station);
                    if(!rebound&&!root.Source.RebindActualStation(loaded,root.Owner,root.Station))continue;
                    root.PendingReentry=loaded;
                    // The actual load source remains retained if publication
                    // allocation is busy. No old native setter is repeated.
                    var station=StationPublication.FromActualCold(root.Source,loaded);if(station==null)continue;
                    var access=AccessHold.Capture(root.Source,station);if(access==null){station.Dispose();continue;}
                    var previous=root.Station;var oldReentry=root.Reentry;
                    root.Station=station;root.Access=access;root.Reentry=loaded;root.PendingReentry=null;loaded.Adopted=true;
                    previous.Dispose();if(!ReferenceEquals(oldReentry,loaded))oldReentry?.Dispose();
                    break;
                }
                catch{/* Same actual original remains unknown; no equality rebind. */}
            }
        }
    }
}
