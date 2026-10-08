using System;
using System.Diagnostics;

// Main-thread revision bootstrap and continuation of an authenticated custody hold. Initial
// selection and cold-start admission are separate; this never creates an intent.
internal static class RebirthGearClientTransferPump
{
    private static EntityPlayerLocal owner;
    private static World world;
    private static object session;
    private static string creation;
    private static double nextAttempt;
    internal static void Reset(){owner=null;world=null;session=null;creation=null;nextAttempt=0;}
    internal static void Tick()
    {
        try
        {
            if(!ThreadManager.IsMainThread())return;
            var game=GameManager.Instance;var currentWorld=game?.World;
            var player=currentWorld?.GetPrimaryPlayer();
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(currentWorld==null||!currentWorld.IsRemote()||player==null||manager==null||manager.IsServer||
                !ReferenceEquals(player.world,currentWorld)||!ReferenceEquals(currentWorld.GetEntity(player.entityId),player)||
                !player.IsSpawned()||player.IsDead()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
                manager.connectionToServer==null||
                manager.connectionToServer.Length==0||manager.connectionToServer[0]==null||
                manager.connectionToServer[0].IsDisconnected()||
                !RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out var currentCreation))
            {Reset();return;} // Reset scheduling only; never releases inventory custody.
            var connection=manager.connectionToServer[0];
            if(!ReferenceEquals(owner,player)||!ReferenceEquals(world,currentWorld)||
                !ReferenceEquals(session,connection)||creation!=currentCreation)
            {Reset();owner=player;world=currentWorld;session=connection;creation=currentCreation;}
            double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
            if(now<nextAttempt)return;
            nextAttempt=now+0.75;
            if(!RebirthGearOwnerReservation.IsHeld(player))
            {
                // A first backpack/belt request needs the authenticated server
                // revision even before the player can open an equipped library.
                // Display-only bootstrap never creates an intent or takes custody.
                bool hasRevision=RebirthBackpackLibraryClientViews.TryGetRevision(currentWorld,player.entityId,out var viewRevision);
                bool terminalAhead=RebirthGearTerminalClient.TryGetCurrent(player,out _,out var settledTerminal)&&
                    settledTerminal.CreationId==currentCreation&&settledTerminal.GearRevision>viewRevision;
                bool libraryAhead=RebirthBackpackLibraryClientOffers.TryGetSettledRevision(currentWorld,player.entityId,out var libraryRevision)&&libraryRevision>viewRevision;
                bool refusalAhead=RebirthGearPreparationRefusalClient.TryGetCurrent(player,connection,out var refusal)&&
                    refusal.CreationId==currentCreation&&refusal.ObservedRevision>viewRevision;
                if(!hasRevision||terminalAhead||libraryAhead||refusalAhead)
                {
                    nextAttempt=now+5;
                    RebirthBackpackLibraryClientViews.Request(currentWorld,player.entityId);
                }
                return;
            }
            if(!RebirthGearOfferClient.TryGetCurrentOffer(currentWorld,player.entityId,out var offer))
            {
                if(RebirthGearPreparationRefusalFlow.TryAdvance(player,connection))return;
                if(RebirthGearOwnerReservation.TryGetInitialIntent(player,connection,out var original))
                    RebirthGearPreparationClient.TryRequestOriginal(player,original);
                else RebirthGearPreparationClient.TryRequestSaved(player);
                return;
            }
            // A server terminal outcome owns cleanup. Never apply again merely
            // because its final local saved-marker retirement is temporarily refused.
            if(RebirthGearTerminalClient.TryGetCurrent(player,out _,out var terminal)&&
                terminal.TransactionId==offer.TransactionId&&terminal.CreationId==offer.CreationId)
            {
                RebirthGearOfferClient.TryFinishCurrentTerminal(currentWorld,player.entityId);
                return;
            }
            // A prior retired terminal may remain cached for duplicate replies.
            // It has no authority over this different original transfer.
            if(player.Buffs==null)return;
            float receipt=player.Buffs.GetCustomVar("rbGear_"+offer.TransactionId);
            if(receipt==-1f){RebirthGearOfferClient.RequestRejectedConfirmation(currentWorld,player.entityId);return;}
            if(receipt==1f){RebirthGearOfferClient.RequestAppliedConfirmation(currentWorld,player.entityId);return;}
            if(receipt!=0f&&receipt!=2f)return; // Unknown receipt never authorizes writes.
            if(RebirthGearOfferClient.TryApplyCurrentOffer(currentWorld,player.entityId)==RebirthGearOwnerApplySequence.Result.Applied)
                RebirthGearOfferClient.RequestAppliedConfirmation(currentWorld,player.entityId);
        }
        catch { } // Uncertain native save/delivery retains original custody for retry.
    }
}