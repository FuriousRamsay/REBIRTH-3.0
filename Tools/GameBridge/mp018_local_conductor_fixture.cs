// AUTHORED_NOT_RUN. Actual local conductor + actual warm terminal composition.
// Native saved owner, wrapper authentication and physiology storage are adapters.
// PROGRAM_PREFIX
 private static void Unheld(){
  Fresh();NativeWrites.Calls=0;NativeWrites.Suppress=NativeWrites.ThrowAfter=false;NativeWrites.Callback=null;
  RebirthHydrationOwnerCheckpoint.Applying=RebirthHydrationOwnerCheckpoint.Applied=RebirthHydrationOwnerCheckpoint.SavedApplied=true;
 }
 public static void Main(){
  Unheld();Need(RebirthMetabolismService.Transfer(player,true,3,false,out var message)&&message=="xuiRebirthWaterSupportEquipped"&&NativeWrites.Calls==1&&RebirthMetabolismService.Snapshots==1,"actual service did not enter saved local conductor");
  Unheld();RebirthMetabolismStateRepository.SaveAllowed=false;
  Need(!RebirthMetabolismService.Transfer(player,true,3,false,out message)&&message=="xuiRebirthHydrationTransferPending"&&NativeWrites.Calls==1&&RebirthHydrationOwnerHold.IsHeld(player),"actual service claimed pending original success");
  RebirthMetabolismStateRepository.SaveAllowed=true;
  Need(RebirthHydrationLocalTransfer.Tick()&&NativeWrites.Calls==1&&!RebirthHydrationOwnerHold.IsHeld(player),"actual update did not resume same saved local original");
  Need(!RebirthHydrationLocalTransfer.Tick()&&NativeWrites.Calls==1,"update replayed completed native debit");
  Unheld();RebirthMetabolismStateRepository.BeforeSaved=false;
  Need(RebirthHydrationLocalTransfer.TryBegin(player,true,3,false)==RebirthHydrationLocalTransfer.Result.Pending&&NativeWrites.Calls==0&&RebirthHydrationOwnerHold.IsHeld(player),"native owner effect entered before original source was saved");
  RebirthMetabolismStateRepository.BeforeSaved=true;
  Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==1,"saved source retry did not complete same original");
  Unheld();Need(RebirthHydrationLocalTransfer.TryBegin(player,true,3,false)==RebirthHydrationLocalTransfer.Result.Applied&&player.bag.ItemGrid.items[3].count==2&&RebirthMetabolismStateRepository.State.HydrationSlotItem.count==1&&!RebirthHydrationOwnerHold.IsHeld(player),"local conductor did not complete both saved legs/release");
  Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Refused&&NativeWrites.Calls==1,"completed conductor retained replay permission");
  Unheld();RebirthMetabolismStateRepository.SaveAllowed=false;
  Need(RebirthHydrationLocalTransfer.TryBegin(player,true,3,false)==RebirthHydrationLocalTransfer.Result.Pending&&RebirthHydrationOwnerHold.IsHeld(player),"failed metabolism publication lost original conductor");
  RebirthMetabolismStateRepository.SaveAllowed=true;
  Need(RebirthHydrationLocalTransfer.TryBegin(player,false,1,true)==RebirthHydrationLocalTransfer.Result.Applied&&NativeWrites.Calls==1&&RebirthMetabolismStateRepository.State.HydrationSlotItem.count==1,"changed selection/action replaced retained original");
  Unheld();NativeWrites.Suppress=true;
  Need(RebirthHydrationLocalTransfer.TryBegin(player,true,3,false)==RebirthHydrationLocalTransfer.Result.Pending&&player.bag.ItemGrid.items[3].count==3&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"suppressed debit minted hydration");
  NativeWrites.Suppress=false;Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Applied&&player.bag.ItemGrid.items[3].count==2,"suppressed original did not resume");
  Unheld();RebirthHydrationLocalTransfer.Result nested=RebirthHydrationLocalTransfer.Result.Refused;
  NativeWrites.Callback=()=>nested=RebirthHydrationLocalTransfer.TryBegin(player,true,3,false);
  Need(RebirthHydrationLocalTransfer.TryBegin(player,true,3,false)==RebirthHydrationLocalTransfer.Result.Applied&&nested==RebirthHydrationLocalTransfer.Result.Pending&&NativeWrites.Calls==1,"native callback reentered conductor financial application");
  Unheld();RebirthMetabolismStateRepository.SaveAllowed=false;Need(RebirthHydrationLocalTransfer.TryBegin(player,true,3,false)==RebirthHydrationLocalTransfer.Result.Pending,"pending scope case unavailable");
  player.world.Primary=null;Need(RebirthHydrationLocalTransfer.TryResume(player)==RebirthHydrationLocalTransfer.Result.Pending&&RebirthHydrationOwnerHold.IsHeld(player),"lost native owner released pending conductor");
  Console.WriteLine("PASS actual local hydration conductor/terminal/application/WAL; remote/auth/PDS/schema/cold recovery NOT RUN");
 }
}
