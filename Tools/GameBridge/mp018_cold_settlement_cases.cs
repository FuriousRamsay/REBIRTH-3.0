// AUTHORED_NOT_RUN. Actual cold source correction/lease/native owner/WAL;
// native file/type and physiology schema/writer are explicit adapters.
  RebirthHydrationColdRecoveryLease PartialCold()
  {
    coldOwner=ColdOwner();Need(original.TryGetPlan(out var partialPlan),"partial recovery plan unavailable");
    RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=original.Creation,HydrationSlotItem=new ItemStack{count=partialPlan.GearBefore.Count,Data=partialPlan.GearBefore.ItemData}};
    Need(RebirthMetabolismStateRepository.PublishColdFixtureSource()&&RebirthHydrationTransferJournal.TryPrepare(player,original,out _)&&RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _),"partial recovery saved Before/attempted WAL unavailable");
    Need(RebirthHydrationColdRecoveryLease.TryAcquire(player,original,out var partial),"partial recovery lease unavailable");return partial;
  }
  var partialLease=PartialCold();int partialNativeSaves=NativeStore.Saves,partialRevision=partialLease.State.Revision;
  Need(RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease)&&RebirthMetabolismStateRepository.HasSavedColdRecoveryHydrationAfter(partialLease)&&partialLease.State.HydrationSlotItem.count==1&&partialLease.State.Revision==partialRevision+1&&NativeStore.Saves==partialNativeSaves&&RebirthHydrationOwnerHold.IsHeld(player),"actual cold source leg did not settle once or released/replayed native custody");
  int partialSaves=RebirthMetabolismStateRepository.Saves;Need(RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease)&&partialLease.State.Revision==partialRevision+1&&RebirthMetabolismStateRepository.Saves==partialSaves,"cold source saved retry repeated mutation/publication");
  partialLease=PartialCold();RebirthMetabolismStateRepository.SaveAllowed=false;
  Need(!RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease)&&partialLease.Current()&&RebirthHydrationOwnerHold.IsHeld(player),"cold source publication failure lost custody/result");partialRevision=partialLease.State.Revision;RebirthMetabolismStateRepository.SaveAllowed=true;
  Need(RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease)&&partialLease.State.Revision==partialRevision,"cold source publication retry repeated correction");
  partialLease=PartialCold();RebirthMetabolismStateRepository.ThrowAfterSave=true;
  Need(!RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease)&&partialLease.Current(),"unknown cold source publication claimed success/lost lease");partialSaves=RebirthMetabolismStateRepository.Saves;partialRevision=partialLease.State.Revision;RebirthMetabolismStateRepository.ThrowAfterSave=false;
  Need(RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease)&&RebirthMetabolismStateRepository.Saves==partialSaves&&partialLease.State.Revision==partialRevision,"positive cold saved result retry repeated publication/correction");
  partialLease=PartialCold();bool nestedPartial=true;RebirthMetabolismStateRepository.AfterDecode=()=>nestedPartial=RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease);
  Need(RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease)&&!nestedPartial,"saved source callback reentered partial financial correction");RebirthMetabolismStateRepository.AfterDecode=null;
  partialLease=PartialCold();RebirthMetabolismStateRepository.AfterDecode=()=>partialLease.State.HydrationSlotItem=new ItemStack{count=9,Data="conflict"};
  Need(!RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease)&&partialLease.State.HydrationSlotItem.count==9&&RebirthHydrationOwnerHold.IsHeld(player),"changed source during saved Before read was overwritten/released");RebirthMetabolismStateRepository.AfterDecode=null;
  partialLease=PartialCold();string missingBeforePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"metabolism.xml"),heldBeforePath=missingBeforePath+".missing-before";
  File.Move(missingBeforePath,heldBeforePath);try{Need(!RebirthMetabolismStateRepository.TrySettleColdLocalHydration(partialLease)&&partialLease.State.HydrationSlotItem.IsEmpty()&&RebirthHydrationOwnerHold.IsHeld(player),"absent final-primary Before was manufactured or custody released");}finally{File.Move(heldBeforePath,missingBeforePath);}
