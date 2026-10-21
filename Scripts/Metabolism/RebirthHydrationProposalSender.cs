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
                manager.SendPackage(packet,_attachedToEntityId:proposal.Player.entityId);
            }
            return proposal.Current();
        }
        catch{return false;}
    }
}
