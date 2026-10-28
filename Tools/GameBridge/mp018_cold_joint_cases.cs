// AUTHORED_NOT_RUN. Actual source qualification/native owner/metadata producer; schema and native IO adapted.
  void ColdFinancialApplied()
  {
    Need(original.TryGetPlan(out var coldPlan),"cold source plan unavailable");
    RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=original.Creation,HydrationTransferReceipt=original,HydrationSlotItem=new ItemStack{count=coldPlan.GearAfter.Count,Data=coldPlan.GearAfter.ItemData}};
    Need(RebirthMetabolismStateRepository.PublishColdFixtureSource(),"cold source final primary seed unavailable");
    Need(RebirthHydrationTransferJournal.TryPrepare(player,original,out _)&&RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _),"cold financial WAL fixture unavailable");
    string financial=Directory.GetFiles(GameIO.Root,original.Operation.ToString("N")+".1.rcj",SearchOption.AllDirectories).Single();
    new RebirthCamperTransferJournal(Path.GetDirectoryName(financial)).Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
  }
  coldOwner=ColdOwner();ColdFinancialApplied();int coldNativeSaves=NativeStore.Saves;
  Need(RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,original)&&NativeStore.Saves==coldNativeSaves&&RebirthMetabolismStateRepository.Saves==1&&!RebirthHydrationOwnerHold.IsHeld(player)&&player.bag.ItemGrid.items[3].count==2,"joint committed cold result failed closure or repeated financial publication");
  Need(RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,original)&&NativeStore.Saves==coldNativeSaves,"saved cold closure retry repeated native publication");
  coldOwner=ColdOwner();ColdFinancialApplied();RebirthMetabolismStateRepository.State.HydrationTransferReceipt=null;
  Need(!RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,original)&&!Directory.GetFiles(GameIO.Root,original.Operation.ToString("N")+".2.rcj",SearchOption.AllDirectories).Any(path=>path.Contains("HydrationLocalCompletions")),"missing saved source created cold closure");
  coldOwner=ColdOwner();ColdFinancialApplied();bool nestedCold=true;
  RebirthMetabolismStateRepository.AfterDecode=()=>nestedCold=RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,original);
  Need(RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,original)&&!nestedCold,"cold source callback reentered completion producer");RebirthMetabolismStateRepository.AfterDecode=null;
  coldOwner=ColdOwner();ColdFinancialApplied();coldNativeSaves=NativeStore.Saves;
  Action<string,RebirthCamperTransferJournal.Record,bool> coldFault=(path,record,published)=>{if(published&&path.Contains("HydrationLocalCompletions")&&record.State==RebirthCamperTransferJournal.Phase.Applied)throw new IOException("unknown cold closure publication");};
  RebirthCamperTransferJournal.PublicationFinished+=coldFault;try{Need(!RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,original)&&NativeStore.Saves==coldNativeSaves,"unknown cold closure claimed completion or republished native owner");}finally{RebirthCamperTransferJournal.PublicationFinished-=coldFault;}
  Need(RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,original)&&NativeStore.Saves==coldNativeSaves,"positive cold closure metadata retry failed/replayed native work");
  coldOwner=ColdOwner();ColdFinancialApplied();RebirthMetabolismStateRepository.WrongSavedAfter=true;Need(RebirthMetabolismStateRepository.PublishColdFixtureSource(),"wrong saved source seed unavailable");
  Need(!RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,original)&&!Directory.GetFiles(GameIO.Root,original.Operation.ToString("N")+".2.rcj",SearchOption.AllDirectories).Any(path=>path.Contains("HydrationLocalCompletions")),"wrong final-primary hydration result admitted joint cold closure");
  coldOwner=ColdOwner();ColdFinancialApplied();RebirthMetabolismStateRepository.AfterDecode=()=>player.world.Primary=null;
  Need(!RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,original),"native owner replaced during actual source read admitted joint cold closure");RebirthMetabolismStateRepository.AfterDecode=null;
