using System;
using System.Collections.Generic;

// Server grant producer only. Client transport must authenticate this original
// in its retained native peer/hold scope before any owning-action application.
internal sealed partial class RebirthHydrationRemoteGrant
{
    private static readonly Dictionary<Guid,RebirthHydrationRemoteGrant> issued=new Dictionary<Guid,RebirthHydrationRemoteGrant>();
    internal readonly RebirthHydrationServerSourceHold.Lease Lease;
    internal RebirthHydrationTransferOriginal Original=>Lease.Original;
    internal static bool HasIssued(Guid operation)=>issued.ContainsKey(operation);
    private RebirthHydrationRemoteGrant(RebirthHydrationServerSourceHold.Lease lease){Lease=lease;}
    internal bool Current()
    {
        try{return issued.TryGetValue(Original.Operation,out var current)&&ReferenceEquals(current,this)&&Lease.Current()&&
            RebirthHydrationTransferJournal.HasAttempted(Lease.Player,Original);}
        catch{return false;}
    }
    internal static bool TryIssue(RebirthHydrationServerSourceHold.Lease lease,out RebirthHydrationRemoteGrant grant)
    {
        grant=null;
        try
        {
            if(lease==null||!lease.Current())return false;
            if(issued.TryGetValue(lease.Original.Operation,out var retained))
            {
                if(ReferenceEquals(retained.Lease,lease)&&retained.Original.Digest==lease.Original.Digest&&retained.Current()){grant=retained;return true;}
                if(!lease.Reentered||retained.Original.Digest!=lease.Original.Digest||retained.Current()||
                    !RebirthHydrationServerSourceHold.Lease.SavedReentry(lease))return false;
                issued.Remove(lease.Original.Operation);
            }
            if(lease.Reentered)
            {
                if(issued.Count>=64||!RebirthHydrationServerSourceHold.Lease.SavedReentry(lease))return false;
                if(!RebirthHydrationTransferJournal.HasAttempted(lease.Player,lease.Original)&&
                    (!RebirthHydrationRemoteOwnerWitness.HasSavedBefore(lease)||!RebirthMetabolismStateRepository.HasSavedRemoteHydrationBefore(lease)||
                        !RebirthHydrationTransferJournal.TryMarkAttempted(lease.Player,lease.Original,out _)))return false;
                if(!RebirthHydrationServerSourceHold.Lease.SavedReentry(lease))return false;
                var recovered=new RebirthHydrationRemoteGrant(lease);issued.Add(lease.Original.Operation,recovered);
                if(!recovered.Current())return false;grant=recovered;return true;
            }
            if(issued.Count>=64||!RebirthMetabolismStateRepository.HasSavedRemoteHydrationBefore(lease)||
                !RebirthHydrationRemoteOwnerWitness.HasSavedBefore(lease)||!lease.Current()||
                !RebirthHydrationTransferJournal.TryMarkAttempted(lease.Player,lease.Original,out _)||
                !lease.Current()||!RebirthMetabolismStateRepository.HasSavedRemoteHydrationBefore(lease)||
                !RebirthHydrationRemoteOwnerWitness.HasSavedBefore(lease))return false;
            var next=new RebirthHydrationRemoteGrant(lease);issued.Add(lease.Original.Operation,next);
            if(!next.Current())return false;grant=next;return true;
        }
        catch{return false;}
    }
}
