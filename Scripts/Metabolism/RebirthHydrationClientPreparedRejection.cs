using System;

// Created only by the matching server rejection receiver. Positive Before is
// necessary but cannot itself manufacture this server-outcome capability.
internal sealed class RebirthHydrationClientPreparedRejection
{
    internal readonly EntityPlayerLocal Player;internal readonly RebirthHydrationTransferOriginal Original;
    private RebirthHydrationClientPreparedRejection(EntityPlayerLocal player,RebirthHydrationTransferOriginal original){Player=player;Original=original;}
    internal bool Current()=>RebirthHydrationOwnerHold.Matches(Player,Original)&&RebirthHydrationOwnerNoEffectWitness.HasBefore(Player,Original);
    internal static bool Receive(World world,int playerId,Guid operation,string digest)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World))return false;
            var player=world.GetPrimaryPlayer();
            if(player==null||player.entityId!=playerId)return false;
            if(RebirthHydrationProposalClient.TryGetCurrent(player,out var retained)&&retained.Operation==operation&&retained.Original?.Digest==digest&&
                RebirthHydrationRefusedCheckpoint.HasSavedHistorical(player,retained.Original))
                return RebirthHydrationOwnerHold.RetireHistoricalRefusal(player,retained.Original)&&
                    RebirthHydrationProposalClient.TryRefuseUnheld(player,operation,digest);
            if(!RebirthHydrationOwnerHold.TryGetRemoteOriginal(player,out var original))return RebirthHydrationProposalClient.TryRefuseUnheld(player,operation,digest);
            if(original.Operation!=operation||original.Digest!=digest)return false;
            var rejection=new RebirthHydrationClientPreparedRejection(player,original);
            if(!rejection.Current()||!RebirthHydrationRefusedCheckpoint.TryPersist(rejection)||!RebirthHydrationOwnerHold.TryReleasePreparedRefusal(rejection))return false;
            RebirthHydrationProposalClient.TryRefuseUnheld(player,operation,digest);return true;
        }
        catch{return false;}
    }
}
