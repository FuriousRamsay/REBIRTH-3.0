// AUTHORED_NOT_RUN. Native IO/buff framing and saved source are disclosed adapters.
  RebirthHydrationTransferJournal.LocalPreparedRejection LocalRefusal()
  {
    Fresh();RebirthMetabolismStateRepository.BeforeSaved=true;RebirthHydrationTransferGrant.Issued=false;
    Need(RebirthHydrationTransferJournal.TryPrepare(player,original,out _)&&RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying),"local refusal Before unavailable");
    Need(RebirthHydrationTransferJournal.LocalPreparedRejection.TryPublish(player,original,out var capability),"actual local rejection token unavailable");return capability;
  }
  var refusedLocal=LocalRefusal();Need(!RebirthHydrationLocalRefusedCheckpoint.HasSaved(refusedLocal),"absent refusal marker invented durable refusal");
  NativeStore.Suppress=true;Need(!RebirthHydrationLocalRefusedCheckpoint.TryPersist(refusedLocal)&&RebirthHydrationOwnerHold.IsHeld(player),"suppressed native refusal save released custody");NativeStore.Suppress=false;
  Need(RebirthHydrationLocalRefusedCheckpoint.TryPersist(refusedLocal)&&RebirthHydrationLocalRefusedCheckpoint.HasSaved(refusedLocal)&&RebirthHydrationOwnerHold.TryReleaseLocalPreparedRefusal(refusedLocal)&&player.bag.ItemGrid.items[3].count==3,"saved local no-effect refusal failed or changed source");
  refusedLocal=LocalRefusal();NativeStore.ThrowAfterSave=true;Need(!RebirthHydrationLocalRefusedCheckpoint.TryPersist(refusedLocal)&&RebirthHydrationOwnerHold.IsHeld(player),"unknown native refusal publication released custody");NativeStore.ThrowAfterSave=false;int refusedSaves=NativeStore.Saves;
  Need(RebirthHydrationLocalRefusedCheckpoint.TryPersist(refusedLocal)&&NativeStore.Saves==refusedSaves,"positive saved refusal retry repeated publication");
  NativeStore.File.Bag[3].count=2;Need(!RebirthHydrationLocalRefusedCheckpoint.HasSaved(refusedLocal)&&!RebirthHydrationOwnerHold.TryReleaseLocalPreparedRefusal(refusedLocal)&&RebirthHydrationOwnerHold.IsHeld(player),"refusal marker without same-file Before released custody");
  refusedLocal=LocalRefusal();Need(RebirthHydrationLocalRefusedCheckpoint.TryPersist(refusedLocal),"seen conflict fixture unavailable");player.Buffs.Values["rbHydrationSeen_"+original.Operation.ToString("N")+"_"+original.Digest]=1f;NativeStore.Save(player);
  Need(!RebirthHydrationLocalRefusedCheckpoint.HasSaved(refusedLocal),"conflicting terminal-seen marker qualified no-effect refusal");
  refusedLocal=LocalRefusal();Need(RebirthHydrationLocalRefusedCheckpoint.TryPersist(refusedLocal),"issued conflict fixture unavailable");RebirthHydrationTransferGrant.Issued=true;
  Need(!RebirthHydrationOwnerHold.TryReleaseLocalPreparedRefusal(refusedLocal)&&RebirthHydrationOwnerHold.IsHeld(player),"issued grant override released no-effect hold");RebirthHydrationTransferGrant.Issued=false;
  Fresh();Need(RebirthHydrationTransferJournal.TryPrepare(player,original,out _)&&RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying),"cross-leg changed Before fixture unavailable");
  RebirthMetabolismStateRepository.DuringBeforeRead=()=>player.bag.ItemGrid.items[3].count=2;
  Need(!RebirthHydrationTransferJournal.LocalPreparedRejection.TryPublish(player,original,out _)&&RebirthHydrationOwnerHold.IsHeld(player),"source-file callback changed native Before but rejection published");RebirthMetabolismStateRepository.DuringBeforeRead=null;
