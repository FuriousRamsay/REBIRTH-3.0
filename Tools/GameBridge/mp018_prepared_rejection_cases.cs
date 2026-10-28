// AUTHORED_NOT_RUN. Actual positive untouched rejection and source release.
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease),"untouched rejection source unavailable");
  Need(RebirthHydrationTransferJournal.PreparedRejection.TryPublish(lease,out var preparedRejection)&&lease.Current()&&RebirthHydrationServerSourceHold.HoldsPlayer(player)&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"positive rejection failed or changed/released source during publication");
  Need(RebirthHydrationServerSourceHold.TryReleasePreparedRejection(preparedRejection)&&!lease.Current()&&!RebirthHydrationServerSourceHold.HoldsPlayer(player),"qualified untouched source did not release");
  Need(!RebirthHydrationServerSourceHold.TryReleasePreparedRejection(preparedRejection),"retired rejection reacquired permission");
  Need(RebirthHydrationTransferJournal.SavedRejection.TryRead(player,original.Operation,original.Digest,out var historicalRejection)&&historicalRejection.Current(),"retired source lost saved rejection description");
  RebirthMetabolismStateRepository.State.HydrationSlotItem=new ItemStack{count=1,itemValue=new ItemValue{Data="later source"}};
  Need(historicalRejection.Current()&&!RebirthHydrationServerSourceHold.HoldsPlayer(player),"historical rejection demanded frozen source or reacquired custody");
  Need(!RebirthHydrationTransferJournal.SavedRejection.TryRead(player,original.Operation,new string('0',64),out _),"foreign rejection digest accepted");
  player.Died++;Need(!historicalRejection.Current(),"new life inherited historical rejection authority");player.Died--;
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease)&&RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _),"uncertain rejection fixture unavailable");
  Need(!RebirthHydrationTransferJournal.PreparedRejection.TryPublish(lease,out _)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"uncertain entered original inferred no effects");
  Need(!RebirthHydrationTransferJournal.SavedRejection.TryRead(player,original.Operation,original.Digest,out _),"uncertain original described as saved rejection");
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease),"issued rejection source unavailable");WitnessFile.Set(original,false);Need(RebirthHydrationRemoteGrant.TryIssue(lease,out remoteGrant),"issued rejection grant unavailable");
  Need(!RebirthHydrationTransferJournal.PreparedRejection.TryPublish(lease,out _)&&remoteGrant.Current()&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"issued original rejected as untouched");
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease),"unknown rejection source unavailable");
  Action<string,RebirthCamperTransferJournal.Record,bool> rejectionFault=(path,record,published)=>{if(record.State==RebirthCamperTransferJournal.Phase.Rejected&&published)throw new IOException("unknown rejection publication");};
  RebirthCamperTransferJournal.PublicationFinished+=rejectionFault;try{Need(!RebirthHydrationTransferJournal.PreparedRejection.TryPublish(lease,out _)&&lease.Current()&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"unknown rejection publication cleared source");}finally{RebirthCamperTransferJournal.PublicationFinished-=rejectionFault;}
  Need(RebirthHydrationTransferJournal.PreparedRejection.TryPublish(lease,out preparedRejection)&&RebirthHydrationServerSourceHold.TryReleasePreparedRejection(preparedRejection),"same saved rejection could not finish metadata-only retry");
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease)&&RebirthHydrationTransferJournal.PreparedRejection.TryPublish(lease,out preparedRejection),"stale rejection capability unavailable");SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=new ClientInfo();
  Need(!RebirthHydrationServerSourceHold.TryReleasePreparedRejection(preparedRejection)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"replacement peer inherited no-effect release capability");
