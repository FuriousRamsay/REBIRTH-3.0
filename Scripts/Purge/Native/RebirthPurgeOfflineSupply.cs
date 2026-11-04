using System;
using System.Collections.Generic;

internal static class RebirthPurgeOfflineSupply
{
    internal static HashSet<string> Capture(World world,RebirthPoiPartialObservation observations)
    {
        var offline=new HashSet<string>(StringComparer.Ordinal);
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(observations==null||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||connection==null||!connection.IsServer)return offline;
        foreach(var volume in observations.Volumes.Values)foreach(var actor in volume.Actors.Values)
            if(actor.Contributor!=null)offline.Add(actor.Contributor);
        // The exact registered/authenticated current actors are the online set.
        // No name or stale entity ID can stand in for a contributor's stable key.
        var players=world?.aiDirector?.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
        if(players!=null)foreach(var tracked in players)
        {string key=RebirthPurgeKillContributor.Identify(tracked.Player,world);if(key!=null)offline.Remove(key);}
        return offline;
    }
}
