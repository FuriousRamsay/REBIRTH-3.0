// AUTHORED_NOT_RUN. Actual cold source/terminal/lease/native hold release/WAL.
  partialLease=PartialCold();partialNativeSaves=NativeStore.Saves;
  Need(partialLease.TryComplete()&&!partialLease.Current()&&!RebirthHydrationOwnerHold.IsHeld(player)&&!RebirthHydrationOwnerHold.BlocksInventory(player.bag,true)&&NativeStore.Saves==partialNativeSaves&&player.bag.ItemGrid.items[3].count==2&&partialLease.State.HydrationSlotItem.count==1,"joint cold terminal did not retire exact custody without replay");
  Need(!partialLease.TryComplete()&&!RebirthHydrationColdRecoveryLease.TryGetRetained(player,out _),"retired cold recovery retained replay permission");
  partialLease=PartialCold();partialNativeSaves=NativeStore.Saves;
  Action<string,RebirthCamperTransferJournal.Record,bool> partialTerminalFault=(path,record,published)=>{if(published&&!path.Contains("HydrationLocalCompletions")&&record.State==RebirthCamperTransferJournal.Phase.Applied)throw new IOException("unknown cold financial terminal");};
  RebirthCamperTransferJournal.PublicationFinished+=partialTerminalFault;try{Need(!partialLease.TryComplete()&&partialLease.Current()&&RebirthHydrationOwnerHold.IsHeld(player),"unknown cold financial terminal released custody");}finally{RebirthCamperTransferJournal.PublicationFinished-=partialTerminalFault;}
  partialRevision=partialLease.State.Revision;partialSaves=RebirthMetabolismStateRepository.Saves;
  Need(partialLease.TryComplete()&&partialLease.State.Revision==partialRevision&&RebirthMetabolismStateRepository.Saves==partialSaves&&NativeStore.Saves==partialNativeSaves,"financial terminal retry repeated source/native work");
  partialLease=PartialCold();partialNativeSaves=NativeStore.Saves;
  partialTerminalFault=(path,record,published)=>{if(published&&path.Contains("HydrationLocalCompletions")&&record.State==RebirthCamperTransferJournal.Phase.Applied)throw new IOException("unknown cold closure terminal");};
  RebirthCamperTransferJournal.PublicationFinished+=partialTerminalFault;try{Need(!partialLease.TryComplete()&&partialLease.Current()&&RebirthHydrationOwnerHold.IsHeld(player),"unknown cold closure terminal released custody");}finally{RebirthCamperTransferJournal.PublicationFinished-=partialTerminalFault;}
  partialRevision=partialLease.State.Revision;Need(partialLease.TryComplete()&&partialLease.State.Revision==partialRevision&&NativeStore.Saves==partialNativeSaves,"cold closure retry replayed financial work");
  partialLease=PartialCold();Need(RebirthHydrationTransferJournal.ColdRecoveryTerminal.TryPublish(partialLease,out var coldTerminal),"cold terminal capability unavailable");
  player.world.Primary=null;Need(!RebirthHydrationOwnerHold.TryReleaseColdRecovery(coldTerminal)&&RebirthHydrationOwnerHold.IsHeld(player),"stale cold terminal released native custody");
