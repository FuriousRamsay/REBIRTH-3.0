// AUTHORED_NOT_RUN. Distinct recovery custody; no source correction activated.
  coldOwner=ColdOwner();RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=original.Creation};RebirthHydrationTransferJournal.Attempted=false;
  Need(!RebirthHydrationColdRecoveryLease.TryAcquire(player,original,out _)&&!RebirthHydrationOwnerHold.IsHeld(player),"unattempted original acquired partial recovery custody");RebirthHydrationTransferJournal.Attempted=true;
  int recoveryNativeSaves=NativeStore.Saves;
  Need(RebirthHydrationColdRecoveryLease.TryAcquire(player,original,out var recoveryLease)&&recoveryLease.Current()&&RebirthHydrationOwnerHold.IsHeld(player)&&RebirthHydrationOwnerHold.BlocksInventory(player.bag,true)&&RebirthHydrationOwnerHold.BlocksInventory(player.inventory,false),"qualified cold Applied owner did not freeze native recovery handles");
  Need(RebirthHydrationColdRecoveryLease.TryAcquire(player,original,out var sameRecovery)&&ReferenceEquals(recoveryLease,sameRecovery)&&NativeStore.Saves==recoveryNativeSaves&&player.bag.ItemGrid.items[3].count==2&&recoveryLease.State.HydrationSlotItem.IsEmpty(),"recovery custody retry changed lease or repeated a financial leg");
  RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=original.Creation};
  Need(!recoveryLease.Current()&&RebirthHydrationOwnerHold.IsHeld(player)&&!RebirthHydrationColdRecoveryLease.TryAcquire(player,original,out _),"replacement metabolism row inherited recovery capability or released hold");
  coldOwner=ColdOwner();RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=original.Creation};Need(RebirthHydrationColdRecoveryLease.TryAcquire(player,original,out recoveryLease),"changed native recovery fixture unavailable");
  NativeStore.File.Bag[3].count=3;Need(!recoveryLease.Current()&&RebirthHydrationOwnerHold.IsHeld(player),"lost native saved Applied witness released recovery custody");
