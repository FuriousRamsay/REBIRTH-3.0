// AUTHORED_NOT_RUN. Actual cold return source correction and terminal release.
  RebirthHydrationColdRecoveryLease PartialColdReturn()
  {
    coldOwner=ColdOwner(true);Need(original.TryGetPlan(out var returnPlan)&&!original.Equip&&returnPlan.GearBefore.Count==1&&returnPlan.GearAfter.Count==0,"cold return original invalid");
    RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=original.Creation,HydrationSlotItem=new ItemStack{count=returnPlan.GearBefore.Count,Data=returnPlan.GearBefore.ItemData}};
    Need(RebirthMetabolismStateRepository.PublishColdFixtureSource()&&RebirthHydrationTransferJournal.TryPrepare(player,original,out _)&&RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _),"cold return saved source Before unavailable");
    Need(RebirthHydrationColdRecoveryLease.TryAcquire(player,original,out var returningLease),"cold return custody unavailable");return returningLease;
  }
  var returningCold=PartialColdReturn();int returnNativeSaves=NativeStore.Saves,returnRevision=returningCold.State.Revision;
  string returnedItemData=player.bag.ItemGrid.items[0].Data;
  Need(returningCold.TryComplete()&&returningCold.State.HydrationSlotItem.IsEmpty()&&returningCold.State.Revision==returnRevision+1&&player.bag.ItemGrid.items[0].count==1&&player.bag.ItemGrid.items[0].Data==returnedItemData&&player.bag.ItemGrid.items[3].count==3&&NativeStore.Saves==returnNativeSaves&&!RebirthHydrationOwnerHold.IsHeld(player),"cold unequip duplicated/lost returned native item or failed source retirement");
  Need(!returningCold.TryComplete()&&player.bag.ItemGrid.items[0].count==1,"retired cold return replayed native return");
  returningCold=PartialColdReturn();returnNativeSaves=NativeStore.Saves;RebirthMetabolismStateRepository.ThrowAfterSave=true;
  Need(!returningCold.TryComplete()&&returningCold.State.HydrationSlotItem.IsEmpty()&&RebirthHydrationOwnerHold.IsHeld(player),"unknown cold unequip source publication lost custody/result");returnRevision=returningCold.State.Revision;int returnSourceSaves=RebirthMetabolismStateRepository.Saves;RebirthMetabolismStateRepository.ThrowAfterSave=false;
  Need(returningCold.TryComplete()&&returningCold.State.Revision==returnRevision&&RebirthMetabolismStateRepository.Saves==returnSourceSaves&&NativeStore.Saves==returnNativeSaves&&player.bag.ItemGrid.items[0].count==1,"cold return publication retry repeated source/native work");
