// AUTHORED_NOT_RUN. Shared native setter/checkpoint adapters from owner apply.
// ACTUAL_OWNER_ADAPTERS
 private static void RemoteReady(){
  Fresh();player.world.Remote=true;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;
  NativeWrites.Calls=0;NativeWrites.Suppress=NativeWrites.ThrowAfter=false;NativeWrites.Callback=null;
  RebirthHydrationOwnerCheckpoint.Saved=RebirthHydrationOwnerCheckpoint.Applying=RebirthHydrationOwnerCheckpoint.Applied=true;
  player.Buffs.Values["rbGear_"+original.Operation.ToString("N")]=2;
 }
 public static void Main(){
  RemoteReady();Need(!RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,original.Operation,original.Digest),"grant without actual retained original admitted");
  Need(RebirthHydrationOwnerHold.TryAcquire(player,original),"remote native original hold unavailable");
  RebirthHydrationOwnerCheckpoint.Saved=false;Need(!RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,original.Operation,original.Digest),"unsaved Before admitted remote client grant");
  RebirthHydrationOwnerCheckpoint.Saved=true;
  Need(!RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,Guid.NewGuid(),original.Digest)&&!RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,original.Operation,new string('0',64)),"foreign operation/digest admitted");
  RebirthHydrationRemoteClientGrant remoteClientGrant=null;
  Need(RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,original.Operation,original.Digest)&&RebirthHydrationRemoteClientGrant.TryGet(player,out remoteClientGrant)&&remoteClientGrant.Current()&&NativeWrites.Calls==0,"qualified remote grant refused or auto-applied inventory");
  Need(RebirthHydrationOwnerApply.Apply(remoteClientGrant)==RebirthGearOwnerApplySequence.Result.Applied&&player.bag.ItemGrid.items[3].count==2&&NativeWrites.Calls==1,"remote owner native application failed");
  Need(RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,original.Operation,original.Digest)&&RebirthHydrationRemoteClientGrant.TryGet(player,out var duplicate)&&ReferenceEquals(duplicate,remoteClientGrant)&&RebirthHydrationOwnerApply.Apply(duplicate)==RebirthGearOwnerApplySequence.Result.Applied&&NativeWrites.Calls==1,"remote duplicate grant repeated debit or changed identity");
  SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0]=new Peer();Need(!remoteClientGrant.Current()&&!RebirthHydrationRemoteClientGrant.TryGet(player,out _)&&RebirthHydrationOwnerHold.IsHeld(player),"replacement peer inherited remote grant or cleared custody");
  RemoteReady();Need(RebirthHydrationOwnerHold.TryAcquire(player,original),"suppressed remote original unavailable");
  Need(RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,original.Operation,original.Digest)&&RebirthHydrationRemoteClientGrant.TryGet(player,out remoteClientGrant),"suppressed remote grant unavailable");
  NativeWrites.Suppress=true;Need(RebirthHydrationOwnerApply.Apply(remoteClientGrant)==RebirthGearOwnerApplySequence.Result.Pending&&player.bag.ItemGrid.items[3].count==3,"suppressed remote setter qualified applied");
  NativeWrites.Suppress=false;Need(RebirthHydrationOwnerApply.Apply(remoteClientGrant)==RebirthGearOwnerApplySequence.Result.Applied&&player.bag.ItemGrid.items[3].count==2,"retained remote grant could not resume");
  Console.WriteLine("PASS actual remote client grant/hold/owner application/permit; packet transport/native PDS/real remote topology NOT RUN");
 }
}
