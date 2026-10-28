// AUTHORED_NOT_RUN. Actual saved Before qualification; native/schema IO adapted.
  Fresh();Need(RebirthHydrationOwnerHold.TryAcquire(player,original),"local source Before hold unavailable");
  int beforeRevision=RebirthMetabolismStateRepository.State.Revision;
  Need(RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original)&&RebirthMetabolismStateRepository.HasSavedLocalHydrationBefore(player,original),"exact local source Before did not save/read back");
  int beforeSaves=RebirthMetabolismStateRepository.Saves;
  Need(RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original)&&RebirthMetabolismStateRepository.Saves==beforeSaves&&RebirthMetabolismStateRepository.State.Revision==beforeRevision&&player.bag.ItemGrid.items[3].count==3&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"saved Before observation repeated publication or changed financial legs");
  Fresh();Need(RebirthHydrationOwnerHold.TryAcquire(player,original),"suppressed local source hold unavailable");RebirthMetabolismStateRepository.SuppressSave=true;
  Need(!RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original)&&RebirthHydrationOwnerHold.IsHeld(player),"suppressed source save admitted stale primary");
  RebirthMetabolismStateRepository.SuppressSave=false;Need(RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original),"suppressed source save could not resume");
  Fresh();Need(RebirthHydrationOwnerHold.TryAcquire(player,original),"unknown local source hold unavailable");RebirthMetabolismStateRepository.ThrowAfterSave=true;
  Need(!RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original)&&RebirthHydrationOwnerHold.IsHeld(player),"unknown source save claimed positive completion");beforeSaves=RebirthMetabolismStateRepository.Saves;RebirthMetabolismStateRepository.ThrowAfterSave=false;
  Need(RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original)&&RebirthMetabolismStateRepository.Saves==beforeSaves,"positive source primary retry repeated publication");
  RebirthMetabolismStateRepository.AfterDecode=()=>player.world.Primary=null;
  Need(!RebirthMetabolismStateRepository.HasSavedLocalHydrationBefore(player,original)&&RebirthHydrationOwnerHold.IsHeld(player),"native actor invalidated during primary read still qualified Before");
  Fresh();Need(RebirthHydrationOwnerHold.TryAcquire(player,original)&&RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original),"changed row source fixture unavailable");
  RebirthMetabolismStateRepository.AfterDecode=()=>RebirthMetabolismStateRepository.State.Revision++;
  Need(!RebirthMetabolismStateRepository.HasSavedLocalHydrationBefore(player,original),"live revision changed during saved read admitted rejection");
  Need(!RebirthMetabolismStateRepository.HasCurrentLocalHydrationBefore(player,original),"live-only source guard accepted changed revision");
  var issuedBefore=Ready();Need(!RebirthMetabolismStateRepository.HasSavedLocalHydrationBefore(player,original)&&!RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original)&&issuedBefore.Current(),"issued original admitted local no-effect saved source");
