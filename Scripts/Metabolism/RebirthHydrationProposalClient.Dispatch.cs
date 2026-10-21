using System;
using System.Diagnostics;

// Explicit scheduling of a previously retained user intent. Receiving a packet
// never enters native item setters; no new intent/cold grant is created here.
internal static partial class RebirthHydrationProposalClient
{
    internal sealed partial class Intent
    {
        private bool advancing;private double nextAttempt,lastAttempt;
        internal bool Advance(double now)
        {
            if(!ThreadManager.IsMainThread()||advancing||double.IsNaN(now)||double.IsInfinity(now)||!Current())return false;
            if(now<lastAttempt)nextAttempt=now;
            if(now<nextAttempt)return false;
            nextAttempt=now+0.75d;lastAttempt=now;advancing=true;
            try
            {
                if(awaitingReentry&&RebirthHydrationRefusedCheckpoint.HasSavedHistorical(Player,original))
                    return RebirthHydrationDeclineClient.TrySend(this);
                if(awaitingReentry&&RebirthHydrationRemoteClientTerminal.TryRestoreHistorical(Player,original))
                    return RebirthHydrationAcknowledgmentClient.TrySend(Player,original.Operation,original.Digest);
                if(awaitingReentry&&!RebirthHydrationOwnerHold.TryReenterRemote(Player,original))return false;
                if(original==null||awaitingReentry||!originalSaved)return RebirthHydrationIntentClient.TrySend(this);
                if(RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(Player,original.Operation,original.Digest,out _))
                    return RebirthHydrationAcknowledgmentClient.TrySend(Player,original.Operation,original.Digest);
                if(RebirthHydrationRemoteClientGrant.TryGet(Player,out var grant))
                    return grant.Original.Digest==original.Digest&&RebirthHydrationRemoteClientDispatch.TryRequestConfirmation(Player);
                if(!RebirthHydrationOwnerHold.IsHeld(Player))return RebirthHydrationDeclineClient.TrySend(this);
                return RebirthHydrationPreparedClient.TrySend(this);
            }
            catch{return false;}
            finally{advancing=false;}
        }
    }
    private static double nextRecovery,lastRecovery;
    internal static bool Tick()
    {
        if(!ThreadManager.IsMainThread())return false;
        var world=GameManager.Instance?.World;var player=world?.GetPrimaryPlayer();
        if(world?.IsRemote()!=true||player==null)return false;
        double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        if(TryGetCurrent(player,out var intent))return intent.Advance(now);
        if(now<lastRecovery)nextRecovery=now;
        if(now<nextRecovery)return false;
        lastRecovery=now;nextRecovery=now+0.75d;
        return RebirthHydrationClientOriginals.TryRead(player,out var saved)&&saved!=null&&Intent.TryRecover(player,saved,out intent)&&intent.Advance(now);
    }
}
