// AUTHORED_NOT_RUN. Actual server grant producer and qualified retirement.
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease),"remote grant source unavailable");WitnessFile.Set(original,false);
  Need(RebirthHydrationRemoteGrant.TryIssue(lease,out var remoteGrant)&&remoteGrant.Current()&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"qualified server grant refused or changed source");
  Need(RebirthHydrationRemoteGrant.TryIssue(lease,out var sameGrant)&&ReferenceEquals(remoteGrant,sameGrant),"same server grant changed identity");
  WitnessFile.Set(original,true);Need(remoteGrant.Current()&&RebirthHydrationRemoteGrant.TryIssue(lease,out sameGrant)&&ReferenceEquals(remoteGrant,sameGrant),"warm grant could not resume after owner applied");
  Need(RebirthHydrationTransferJournal.RemoteTerminal.TryPublish(lease,out var grantTerminal)&&RebirthHydrationServerSourceHold.TryReleaseRemote(grantTerminal)&&!remoteGrant.Current(),"qualified terminal did not retire issued grant/source");
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease),"applied-only grant source unavailable");WitnessFile.Set(original,true);
  Need(!RebirthHydrationRemoteGrant.TryIssue(lease,out _)&&!RebirthHydrationTransferJournal.HasAttempted(player,original),"applied-only owner file created fresh server permission");
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease),"wrong-Before grant source unavailable");WitnessFile.Set(original,false);RebirthGearNativePlayerFile.File.Bag[3].count=2;
  Need(!RebirthHydrationRemoteGrant.TryIssue(lease,out _)&&!RebirthHydrationTransferJournal.HasAttempted(player,original),"applying postimage created fresh grant");
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease),"unknown grant attempt source unavailable");WitnessFile.Set(original,false);
  Action<string,RebirthCamperTransferJournal.Record,bool> remoteGrantFault=(path,record,published)=>{if(record.State==RebirthCamperTransferJournal.Phase.Uncertain&&published)throw new IOException("unknown grant WAL caller");};
  RebirthCamperTransferJournal.PublicationFinished+=remoteGrantFault;try{Need(!RebirthHydrationRemoteGrant.TryIssue(lease,out _)&&lease.Current()&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"unknown attempted WAL discarded source");}finally{RebirthCamperTransferJournal.PublicationFinished-=remoteGrantFault;}
  Need(RebirthHydrationRemoteGrant.TryIssue(lease,out remoteGrant)&&remoteGrant.Current(),"unknown attempted WAL could not resume same original");
  SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=new ClientInfo();Need(!remoteGrant.Current()&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"replacement peer inherited grant or cleared source custody");
