using System;

// Player request orchestration. Refund is a later step after native cancelled publication evidence.
internal static class RebirthStationCancellationRequest
{
    internal static bool TryRequest(EntityPlayerLocal player,Vector3i position,string creation,Guid job,
        out RebirthStationCancellationObservation observation)
    {
        observation=null;
        // Remote cancellation requires its owner inventory protocol before player-facing admission.
        if(player==null||job==Guid.Empty||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player))return false;
        try
        {
            if(!RebirthStationTerminalIntentService.TrySave(player,position,creation,job,false,out _)||
                !RebirthStationCancellationAttemptService.TryBeginRemoval(player,position,creation,job,out var permit)||permit==null)return false;
            return permit.TryApply(out observation);
        }
        catch
        {
            observation?.Dispose();observation=null;return false;
        }
    }
}