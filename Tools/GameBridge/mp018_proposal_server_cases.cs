// AUTHORED_NOT_RUN. Actual proposal builder over native per-peer upload adapters.
  Fresh();WitnessFile.Set(original,false);var proposalPeer=SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer;proposalPeer.latestPlayerData=RebirthGearNativePlayerFile.File;Guid proposalOperation=Guid.NewGuid();
  RebirthHydrationProposalServer.Proposal proposal=null;
  Need(RebirthHydrationProposalServer.Proposal.TryPrepare(player,proposalPeer,proposalOperation,true,3,false,null,out proposal)&&proposal.Current()&&proposal.Original.Operation==proposalOperation&&proposal.Lease.Current()&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"native uploaded proposal refused or mutated source");
  RebirthMetabolismStateRepository.State.Revision++;proposalPeer.latestPlayerData=null;
  Need(RebirthHydrationProposalServer.Proposal.TryPrepare(player,proposalPeer,proposalOperation,true,3,false,null,out var sameProposal)&&ReferenceEquals(proposal,sameProposal),"retained proposal rebuilt from missing upload/unrelated revision");
  Need(!RebirthHydrationProposalServer.Proposal.TryPrepare(player,proposalPeer,proposalOperation,false,1,false,null,out _)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"changed selection replaced pending original");
  Fresh();proposalPeer=SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer;
  Need(!RebirthHydrationProposalServer.Proposal.TryPrepare(player,proposalPeer,Guid.NewGuid(),true,3,false,null,out _),"missing native upload fell back to server inventory mirror");
  WitnessFile.Set(original,false);proposalPeer.latestPlayerData=RebirthGearNativePlayerFile.File;proposalPeer.latestPlayerData.deaths++;
  Need(!RebirthHydrationProposalServer.Proposal.TryPrepare(player,proposalPeer,Guid.NewGuid(),true,3,false,null,out _),"other-life native upload admitted proposal");
  Fresh();WitnessFile.Set(original,false);proposalPeer=SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer;proposalPeer.latestPlayerData=RebirthGearNativePlayerFile.File;
  Need(!RebirthHydrationProposalServer.Proposal.TryPrepare(player,proposalPeer,Guid.NewGuid(),true,-1,false,null,out _),"equip request silently became unequip");
  var lockedProposal=new bool[44];lockedProposal[3]=true;Need(!RebirthHydrationProposalServer.Proposal.TryPrepare(player,proposalPeer,Guid.NewGuid(),true,3,false,lockedProposal,out _),"locked native source produced proposal");
