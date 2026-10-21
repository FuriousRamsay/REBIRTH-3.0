using System;

internal sealed partial class RebirthHydrationTransferGrant
{
    private RebirthHydrationTransferGrant previous;
    internal static bool TryIssueLocalReentered(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out RebirthHydrationTransferGrant grant)
    {
        grant=null;
        try
        {
            if(player?.world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
                RebirthHydrationRemoteGrant.HasIssued(original.Operation)||RebirthHydrationRemoteClientGrant.HasIssued(original.Operation)||
                !RebirthHydrationOwnerHold.TryReenterRemote(player,original))return false;
            bool Saved()
            {
                bool ownerBefore=RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original);
                bool ownerAfter=RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied);
                return RebirthHydrationOwnerHold.Matches(player,original)&&
                    (ownerBefore&&!RebirthHydrationTransferJournal.HasApplied(player,original)&&RebirthMetabolismStateRepository.HasSavedLocalHydrationReentrySource(player,original,false)||
                     ownerAfter&&(RebirthMetabolismStateRepository.HasSavedLocalHydrationReentrySource(player,original,false)||RebirthMetabolismStateRepository.HasSavedLocalHydrationReentrySource(player,original,true)))&&
                    (RebirthHydrationTransferJournal.HasPrepared(player,original)||RebirthHydrationTransferJournal.HasAttempted(player,original));
            }
            if(!Saved()||!Source(player,original,false,out var state))return false;
            issued.TryGetValue(original.Operation,out var old);
            if(old!=null&&old.Current())
            {if(!ReferenceEquals(old.Player,player)||old.Original.Digest!=original.Digest)return false;grant=old;return true;}
            if(old!=null&&old.Original.Digest!=original.Digest||old==null&&issued.Count>=64)return false;
            int count=0;for(var cursor=old;cursor!=null;cursor=cursor.previous)if(++count>=64)return false;
            if(RebirthHydrationTransferJournal.HasPrepared(player,original)&&!RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _))return false;
            if(!Saved())return false;
            var next=new RebirthHydrationTransferGrant(player,original,state){previous=old};issued[original.Operation]=next;
            if(!next.Current())return false;grant=next;return true;
        }
        catch{return false;}
    }
}
