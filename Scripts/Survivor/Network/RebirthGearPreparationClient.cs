using System;

// Explicit initial-request producer. The caller owns one original intent and must
// retry it; this never creates an ID or clears custody on uncertain delivery.
internal static class RebirthGearPreparationClient
{
    private static readonly RebirthGearPreparationRequestThrottle Throttle=new RebirthGearPreparationRequestThrottle();
    // Reconnect/query producer. Sends the SAME original marker and current native
    // snapshot; never invokes the initial-preimage save or invents a transaction.
    internal static bool TryRequestSaved(EntityPlayerLocal player)
    {
        try
        {
            var game=GameManager.Instance;var world=player?.world;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(player==null||!ThreadManager.IsMainThread()||game==null||world==null||manager==null||manager.IsServer||
                manager.connectionToServer==null||manager.connectionToServer.Length==0||manager.connectionToServer[0]==null)return false;
            var connection=manager.connectionToServer[0];string nativeWorld=GamePrefs.GetString(EnumGamePrefs.GameGuidClient);
            if(!Guid.TryParse(nativeWorld,out var savedWorld)||savedWorld==Guid.Empty||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||
                game.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId||
                !RebirthGearPreparationPlayerFileWitness.TryReadOriginalPhase(identity,savedWorld,out var marker,out var intent,out _,out _))return false;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&
                ReferenceEquals(game.World,world)&&ReferenceEquals(player.world,world)&&world.IsRemote()&&
                ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&
                player.IsSpawned()&&!player.IsDead()&&GamePrefs.GetString(EnumGamePrefs.GameGuidClient)==nativeWorld&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&!manager.IsServer&&
                manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&
                ReferenceEquals(manager.connectionToServer[0],connection)&&!connection.IsDisconnected()&&
                game.getPersistentPlayerID(null)?.CombinedString==identity.CanonicalId&&
                RebirthSurvivorRequestScope.Matches(intent.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player))&&
                RebirthGearOwnerReservation.MatchesIntent(player,connection,intent);
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearPreparationRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearOfferFragment));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearBoundSettled));
            if(!Throttle.TryAdmit(world,manager,player.entityId,System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency)||
                !RebirthGearOwnerReservation.TryRestoreOriginal(player,connection)||!current())return false;
            var snapshot=NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player);
            if(!current())return false;
            manager.SendToServer(snapshot);
            if(!current())return false;
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthGearPreparationRequest>().Setup(player.entityId,marker));
            return current();
        }
        catch{return false;} // Original hold/marker remains on uncertain queueing.
    }
    // Explicit selection producer. Caller retains the supplied transaction for retries.
    // Only captures evidence; no hold, save, packet, item move or new identity here.
    internal static bool TryCreateUnequipOriginal(EntityPlayerLocal player,string slot,Guid transaction,
        out RebirthGearPreparationIntent intent)
        =>TryCreateCandidate(player,null,false,-1,transaction,slot,out intent);
    internal static bool TryCreateOriginal(EntityPlayerLocal player,ItemValue selected,bool bag,int index,
        Guid transaction,out RebirthGearPreparationIntent intent)
        =>TryCreateCandidate(player,selected,bag,index,transaction,null,out intent);
    private static bool TryCreateCandidate(EntityPlayerLocal player,ItemValue selected,bool bag,int index,
        Guid transaction,string unequipSlot,out RebirthGearPreparationIntent intent)
    {
        intent=null;
        try
        {
            var world=player?.world;var game=GameManager.Instance;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||transaction==Guid.Empty||
                (unequipSlot==null&&(selected==null||selected.IsEmpty()||selected.ItemClass==null))||world==null||game==null||!ReferenceEquals(game.World,world)||
                !world.IsRemote()||manager==null||manager.IsServer||
                !ReferenceEquals(world.GetPrimaryPlayer(),player)||!ReferenceEquals(world.GetEntity(player.entityId),player)||
                !player.IsSpawned()||player.IsDead()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
                manager.connectionToServer==null||manager.connectionToServer.Length==0||
                manager.connectionToServer[0]==null||manager.connectionToServer[0].IsDisconnected()||
                RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||
                !RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out var creation)||
                !RebirthBackpackLibraryClientViews.TryGetRevision(world,player.entityId,out var revision))return false;
            var connection=manager.connectionToServer[0];
            var belt=player.inventory?.ItemGrid?.items;
            if(belt==null||!RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,belt,
                RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length),out var snapshot))return false;
            RebirthGearPreparationIntent candidate;
            if(unequipSlot!=null)
            {
                if(string.IsNullOrEmpty(RebirthSurvivorClientState.GetProjectedGearItem(player,unequipSlot))||
                    !RebirthGearPreparationIntent.TryCreateUnequip(creation,transaction,revision,unequipSlot,snapshot,out candidate))return false;
            }
            else
            {
                if(!snapshot.IsUsableSource(bag,index))return false;
                var source=(bag?snapshot.Bag:snapshot.Belt)[index];
                if(source.Count<=0||source.ItemData!=RebirthNativeItemCodec.Encode(selected)||
                    !RebirthGearPreparationIntent.TryCreate(creation,transaction,revision,selected.type,selected.Seed,
                        bag,index,snapshot,out candidate))return false;
            }
            if(!ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(game.World,world)||
                !ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetPrimaryPlayer(),player)||
                !ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||manager.IsServer||
                manager.connectionToServer==null||manager.connectionToServer.Length==0||
                !ReferenceEquals(manager.connectionToServer[0],connection)||connection.IsDisconnected()||
                !RebirthSurvivorRequestScope.Matches(creation,RebirthSurvivorClientState.GetProjectedCreationId(player))||
                !RebirthBackpackLibraryClientViews.TryGetRevision(world,player.entityId,out var finalRevision)||
                finalRevision!=revision)return false;
            intent=candidate;return true;
        }
        catch{return false;}
    }
    internal static bool TryRequestOriginal(EntityPlayerLocal player,RebirthGearPreparationIntent intent)
    {
        if(player==null||intent==null||!ThreadManager.IsMainThread())return false;
        var game=GameManager.Instance;var world=player.world;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var connections=manager?.connectionToServer;
        if(game==null||world==null||manager==null||manager.IsServer||connections==null||connections.Length==0||connections[0]==null)return false;
        var connection=connections[0];
        string nativeWorld;Guid savedWorld;
        try {nativeWorld=GamePrefs.GetString(EnumGamePrefs.GameGuidClient);if(!Guid.TryParse(nativeWorld,out savedWorld)||savedWorld==Guid.Empty)return false;}
        catch {return false;}
        Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&
            ReferenceEquals(game.World,world)&&ReferenceEquals(player.world,world)&&world.IsRemote()&&
            ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&
            player.IsSpawned()&&!player.IsDead()&&GamePrefs.GetString(EnumGamePrefs.GameGuidClient)==nativeWorld&&
            ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&!manager.IsServer&&
            manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&
            ReferenceEquals(manager.connectionToServer[0],connection)&&!connection.IsDisconnected()&&
            RebirthSurvivorRequestScope.Matches(intent.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player));
        try
        {
            if(!current())return false;
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearPreparationRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearOfferFragment));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearBoundSettled));
            if(!current()||!Throttle.TryAdmit(world,manager,player.entityId,
                System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency)||
                !RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent)||
                !RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,savedWorld)||!current())return false;
            var belt=player.inventory?.ItemGrid?.items;
            if(belt==null||!RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,belt,
                RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length),out var original)||!intent.MatchesInventory(original)||
                !RebirthGearPreparationMarker.TryEncode(savedWorld,original.OwnedBeltSlots,intent,out var marker)||
                player.Buffs==null||player.Buffs.GetCustomVar(marker)!=1f||
                !RebirthGearOwnerReservation.MatchesIntent(player,connection,intent)||!current())return false;
            // Setup snapshots full native player data now. Merely scheduling a
            // later upload could observe a different request/phase.
            var snapshot=NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player);
            if(!current()||!RebirthGearOwnerReservation.MatchesIntent(player,connection,intent))return false;
            manager.SendToServer(snapshot);
            if(!current()||!RebirthGearOwnerReservation.MatchesIntent(player,connection,intent))return false;
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthGearPreparationRequest>().Setup(player.entityId,marker));
            return current()&&RebirthGearOwnerReservation.MatchesIntent(player,connection,intent);
        }
        catch {return false;} // Original hold/marker survive missing or reordered delivery.
    }
}