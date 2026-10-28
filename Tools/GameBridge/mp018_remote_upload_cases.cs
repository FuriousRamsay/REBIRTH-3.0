// AUTHORED_NOT_RUN. Actual explicit application/upload dispatcher.
  RemoteReady();Need(RebirthHydrationOwnerHold.TryAcquire(player,original)&&RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,original.Operation,original.Digest),"upload original grant unavailable");
  GameManager.Uploads=0;GameManager.DuringUpload=null;NetPackageManager.Available=false;
  Need(!RebirthHydrationRemoteClientDispatch.TryApplyAndUpload(player)&&NativeWrites.Calls==0&&GameManager.Uploads==0&&RebirthHydrationOwnerHold.IsHeld(player),"unmapped upload entered financial application");
  NetPackageManager.Available=true;Need(RebirthHydrationRemoteClientDispatch.TryApplyAndUpload(player)&&NativeWrites.Calls==1&&GameManager.Uploads==1&&RebirthHydrationOwnerHold.IsHeld(player),"native upload scheduling failed or released owner");
  Need(RebirthHydrationRemoteClientDispatch.TryApplyAndUpload(player)&&NativeWrites.Calls==1&&GameManager.Uploads==2,"upload retry repeated debit");
  bool nestedUpload=true;GameManager.DuringUpload=()=>nestedUpload=RebirthHydrationRemoteClientDispatch.TryApplyAndUpload(player);
  Need(RebirthHydrationRemoteClientDispatch.TryApplyAndUpload(player)&&!nestedUpload&&GameManager.Uploads==3&&NativeWrites.Calls==1,"native upload callback reentered dispatcher");GameManager.DuringUpload=null;
  RemoteReady();Need(RebirthHydrationOwnerHold.TryAcquire(player,original)&&RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,original.Operation,original.Digest),"mapping-scope original unavailable");
  NetPackageManager.DuringMapping=()=>SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0]=new Peer();
  Need(!RebirthHydrationRemoteClientDispatch.TryApplyAndUpload(player)&&NativeWrites.Calls==0&&RebirthHydrationOwnerHold.IsHeld(player),"peer replacement during mapping entered financial application");NetPackageManager.DuringMapping=null;
