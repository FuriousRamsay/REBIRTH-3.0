using System;
using System.Security.Cryptography;

// Recovery comparison only. Normalize transient clock/revision on a detached
// native round-trip; never rewrite live inventories or infer reward authority.
internal static class RebirthCamperStationRecoveryImage
{
    internal static string Digest(RebirthCamperStationState state,ItemStack installedItem)
    {
        if(!ThreadManager.IsMainThread()||state==null||installedItem==null)
            throw new InvalidOperationException("Native station recovery comparison requires the game thread.");
        byte[] original=RebirthCamperStationSnapshot.Capture(state.InstallationId,state);
        var detached=RebirthCamperStationSnapshot.Restore(state.InstallationId,installedItem.Clone(),original);
        detached.lastTickTime=0;
        detached.RestoreRevision(0);
        byte[] canonical=RebirthCamperStationSnapshot.Capture(detached.InstallationId,detached);
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(canonical)).Replace("-","");
    }
}
