using System;

// Original local request lifetime. Explicit Advance only; no UI hook or background dispatcher activated.
internal sealed class RebirthStationCancellationSession:IDisposable
{
    private readonly EntityPlayerLocal player;
    private readonly object world;
    private readonly Vector3i position;
    private readonly string creation;
    private readonly Guid job,delivery;
    private readonly int actor,thread;
    private RebirthStationCancellationObservation observation;
    private RebirthStationRefundPreparationService.ApplicationPermit permit;
    private bool applicationAttempted,nativeConfirmed,completed,disposed;
    private RebirthStationCancellationSession(EntityPlayerLocal p,Vector3i pos,string c,Guid j,RebirthStationCancellationObservation watch)
    {player=p;world=p.world;position=pos;creation=c;job=j;delivery=Guid.NewGuid();actor=p.entityId;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;observation=watch;}
    internal static bool TryBegin(EntityPlayerLocal player,Vector3i position,string creation,Guid job,out RebirthStationCancellationSession session)
    {
        session=null;
        if(!RebirthStationCancellationRequest.TryRequest(player,position,creation,job,out var watch)||watch==null)return false;
        session=new RebirthStationCancellationSession(player,position,creation,job,watch);return true;
    }
    internal bool IsCompleted=>completed;
    // False is pending/held, never permission to repeat removal or an inventory effect.
    internal bool Advance(string saveRoot)
    {
        if(completed)return true;
        if(disposed||thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)return false;
        if(player.entityId!=actor||!ReferenceEquals(player.world,world)||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player))
        {Dispose();return false;}
        try
        {
            if(nativeConfirmed)return TryFinishArchive();
            if(!RebirthStationCancellationAttemptService.TryConfirmPublication(player,position,creation,job,saveRoot,out _))return false;
            if(!applicationAttempted)
            {
                if(!RebirthStationRefundPreparationService.TryBeginApplication(player,position,creation,job,delivery,saveRoot,out var original))return false;
                permit=original;applicationAttempted=true; // Consume application path even on exception/failure.
                if(permit==null||!permit.TryApply())return false;
            }
            if(permit==null||!permit.TryConfirmSaved())return false;
            nativeConfirmed=true;return TryFinishArchive();
        }
        catch{return false;}
    }
    private bool TryFinishArchive()
    {
        if(!RebirthStationRefundArchiveService.TryArchive(player,position,creation,job,delivery))return false;
        completed=true;Dispose();return true;
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;observation?.Dispose();observation=null;permit=null;
        // Durable pending journal remains; disposal cannot cancel obligations or mint replay authority.
    }
}