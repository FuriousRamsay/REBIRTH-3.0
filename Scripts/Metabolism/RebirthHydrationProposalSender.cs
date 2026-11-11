using System;

internal static class RebirthHydrationProposalSender
{
    internal static bool TrySend(RebirthHydrationProposalServer.Proposal proposal)
    {
        try
        {
            if(proposal?.Lease==null||!proposal.Current()||!proposal.Lease.Current())return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationProposalFragment));
            for(int index=0;index<proposal.Body.Count;index++)
            {
                if(!proposal.Current()||!proposal.Lease.Current()||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return false;
                var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationProposalFragment>().Setup(proposal,index);
                if(!proposal.Current())return false;
                proposal.Peer.SendPackage(packet);
            }
            RebirthDebug.Log("metabolism", $"[REBIRTH HydrationTransfer] proposal queued operation={proposal.Original.Operation:N} fragments={proposal.Body.Count}");
            return proposal.Current();
        }
        catch{return false;}
    }
}
