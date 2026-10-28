// AUTHORED_NOT_RUN. Fresh loaded actor is modeled by new native objects in the
// same saved World GUID. Original warm owner handles are never adopted/released.
  RebirthHydrationColdOwnerWitness ColdOwner(bool returning=false)
  {
    Fresh(false,returning);RebirthHydrationTransferGrant.Issued=false;RebirthHydrationServerSourceHold.Held=false;
    RebirthHydrationRemoteGrant.Issued=RebirthHydrationRemoteClientGrant.Issued=false;
    Need(RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying),"cold native Before unavailable");
    Need(original.TryGetPlan(out var ownerFixturePlan),"cold native immutable plan unavailable");
    var ownerFixtureChange=ownerFixturePlan.Changes[0];Need(ownerFixtureChange.IsBag,"cold native fixture expected bag transfer");
    player.bag.ItemGrid.items[ownerFixtureChange.Index]=new ItemStack{count=ownerFixtureChange.After.Count,Data=ownerFixtureChange.After.ItemData};
    Need(RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applied),"cold native saved result unavailable");
    Need(!RebirthHydrationColdOwnerWitness.TryObserve(player,original,out _),"cold proof adopted warm held native body");
    var loadedWorld=new World{Guid=player.world.Guid};var loaded=new EntityPlayerLocal{world=loadedWorld,Creation=original.Creation,Died=original.Deaths,
      bag=new Bag{ItemGrid=new Grid{items=NativeStore.Copy(NativeStore.File.Bag)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=NativeStore.Copy(NativeStore.File.Belt)}}};
    foreach(var pair in player.Buffs.Values)loaded.Buffs.Values[pair.Key]=pair.Value;
    loadedWorld.Actor=loadedWorld.Primary=loaded;player=loaded;GameManager.Instance.World=loadedWorld;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
    Need(RebirthHydrationColdOwnerWitness.TryObserve(player,original,out var observed),"exact cold native Applied result not observed");return observed;
  }
  var coldOwner=ColdOwner();Need(coldOwner.Current()&&!RebirthHydrationOwnerHold.IsHeld(player)&&player.bag.ItemGrid.items[3].count==2,"cold observation acquired custody or changed native result");
  NativeStore.File.Bag[3].count=3;Need(!coldOwner.Current(),"saved Before qualified cold Applied result");
  coldOwner=ColdOwner();player.bag.ItemGrid.items[3].count=3;Need(!coldOwner.Current(),"live Before qualified cold Applied result");
  coldOwner=ColdOwner();RebirthHydrationTransferGrant.Issued=true;Need(!coldOwner.Current(),"issued warm original became cold authority");RebirthHydrationTransferGrant.Issued=false;
  coldOwner=ColdOwner();RebirthHydrationServerSourceHold.Held=true;Need(!coldOwner.Current(),"active server source custody became cold authority");RebirthHydrationServerSourceHold.Held=false;
  coldOwner=ColdOwner();RebirthGearNativePlayerFile.AfterRead=()=>player.world.Primary=null;Need(!coldOwner.Current(),"native primary changed during cold owner read still qualified");
  coldOwner=ColdOwner();RebirthHydrationRemoteGrant.Issued=true;Need(!coldOwner.Current(),"retained remote server grant admitted cold local recovery");RebirthHydrationRemoteGrant.Issued=false;
  coldOwner=ColdOwner();RebirthHydrationRemoteClientGrant.Issued=true;Need(!coldOwner.Current(),"retained remote client grant admitted cold local recovery");RebirthHydrationRemoteClientGrant.Issued=false;
  coldOwner=ColdOwner();player.Buffs.Values[RebirthHydrationOwnerCheckpoint.Marker(original)]=2f;Need(!coldOwner.Current(),"live Applying receipt with saved Applied file admitted cold completion");
  coldOwner=ColdOwner();RebirthGearNativePlayerFile.AfterRead=()=>player.Buffs.Values["rbGear_"+original.Operation.ToString("N")]=2f;Need(!coldOwner.Current(),"live receipt changed during saved observation admitted cold completion");
