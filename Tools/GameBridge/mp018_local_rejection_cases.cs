// AUTHORED_NOT_RUN. Native file and physiology Before witnesses are adapters.
  Fresh();Need(RebirthHydrationOwnerHold.TryAcquire(player,original)&&RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"untouched local original unavailable");
  RebirthMetabolismStateRepository.BeforeSaved=false;
  Need(!RebirthHydrationTransferJournal.LocalPreparedRejection.TryPublish(player,original,out _)&&RebirthHydrationOwnerHold.IsHeld(player),"unsaved local Before released custody");
  RebirthMetabolismStateRepository.BeforeSaved=true;
  Need(RebirthHydrationTransferJournal.LocalPreparedRejection.TryPublish(player,original,out var localRejection)&&localRejection.Current(),"positive untouched Prepared rejection refused");
  RebirthHydrationLocalRefusedCheckpoint.Saved=false;Need(!RebirthHydrationOwnerHold.TryReleaseLocalPreparedRefusal(localRejection)&&RebirthHydrationOwnerHold.IsHeld(player),"unsaved native refusal released hold");
  RebirthHydrationLocalRefusedCheckpoint.Saved=true;Need(RebirthHydrationOwnerHold.TryReleaseLocalPreparedRefusal(localRejection)&&!RebirthHydrationOwnerHold.IsHeld(player)&&player.bag.ItemGrid.items[3].count==3&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"positive local no-effect retirement changed a financial leg");
  Fresh();Need(RebirthHydrationOwnerHold.TryAcquire(player,original)&&RebirthHydrationTransferJournal.TryPrepare(player,original,out _)&&RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _),"uncertain rejection fixture unavailable");
  Need(!RebirthHydrationTransferJournal.LocalPreparedRejection.TryPublish(player,original,out _)&&RebirthHydrationOwnerHold.IsHeld(player),"Uncertain original rejected from current Before appearance");
  var issuedLocal=Ready();Need(RebirthHydrationTransferGrant.HasIssued(original.Operation)&&!RebirthHydrationTransferJournal.LocalPreparedRejection.TryPublish(player,original,out _),"issued local grant admitted no-effect retirement");
  Fresh();Need(RebirthHydrationOwnerHold.TryAcquire(player,original)&&RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"unknown rejection fixture unavailable");
  Action<string,RebirthCamperTransferJournal.Record,bool> rejectionFault=(path,record,published)=>{if(published&&record.State==RebirthCamperTransferJournal.Phase.Rejected)throw new IOException("unknown local rejection publication");};
  RebirthCamperTransferJournal.PublicationFinished+=rejectionFault;try{Need(!RebirthHydrationTransferJournal.LocalPreparedRejection.TryPublish(player,original,out _)&&RebirthHydrationOwnerHold.IsHeld(player),"unknown local rejection released custody");}finally{RebirthCamperTransferJournal.PublicationFinished-=rejectionFault;}
  Need(RebirthHydrationTransferJournal.LocalPreparedRejection.TryPublish(player,original,out localRejection)&&RebirthHydrationOwnerHold.TryReleaseLocalPreparedRefusal(localRejection),"saved rejection metadata retry failed");
