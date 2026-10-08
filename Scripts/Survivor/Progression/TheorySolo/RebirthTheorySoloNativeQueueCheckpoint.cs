using System;
using System.IO;

// Synchronous original enqueue checkpoint. No admission, reward or queue-pause authority.
internal static class RebirthTheorySoloNativeQueueCheckpoint
{
    internal static bool TryPersistAndUpload(EntityPlayerLocal player,object originalConnection,Guid expectedWorld,RebirthTheorySoloOriginalTaskLedger.Task task)
    {
        if(player==null||task==null||!task.Published||task.Held||task.Kind!="personal"||expectedWorld==Guid.Empty||!ThreadManager.IsMainThread()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return false;
        var game=GameManager.Instance;var world=player.world;var state=world?.worldState;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        string directory;
        try{directory=Path.GetFullPath(GameIO.GetPlayerDataDir());}catch{return false;}
        Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&game!=null&&ReferenceEquals(game.World,world)&&ReferenceEquals(player.world,world)&&world!=null&&ReferenceEquals(world.worldState,state)&&state!=null&&ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead()&&task.OriginalActor==player.entityId&&RebirthSurvivorMode.IsEnabledForCurrentWorld()&&!RebirthCharacterCreationHoldService.IsHeld(player)&&ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager!=null&&game.getPersistentPlayerID(null)?.CombinedString==identity.CanonicalId&&Path.GetFullPath(GameIO.GetPlayerDataDir())==directory;
        Func<bool> bound=()=>current()&&(manager.IsServer?!world.IsRemote()&&Guid.TryParse(world.Guid,out var hostWorld)&&hostWorld==expectedWorld&&HasSavedIssuedTask(player,task):world.IsRemote()&&originalConnection!=null&&manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&ReferenceEquals(manager.connectionToServer[0],originalConnection)&&!manager.connectionToServer[0].IsDisconnected()&&Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var nativeWorld)&&nativeWorld==expectedWorld&&RebirthSurvivorRequestScope.Matches(task.Creation,RebirthSurvivorClientState.GetProjectedCreationId(player)));
        try
        {
            if(!bound())return false;
            // Setup snapshots current native queue before any subsequent Unity craft tick.
            var upload=NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player);
            if(!bound()||!RebirthTheorySoloNativeQueueWitness.TryMatchPaidOriginal(upload.playerDataFile,task,task.Portions,out _))return false;
            game.SaveLocalPlayerData();
            if(!bound()||!RebirthTheorySoloNativeQueueWitness.TryReadPaidOriginal(identity,task,task.Portions,out _)||!bound())return false;
            if(!manager.IsServer){if(!bound())return false;manager.SendToServer(upload);}
            return bound(); // Server commit still requires its own exact uploaded final-file witness.
        }
        catch{return false;} // Unknown checkpoint is never original enqueue permission or completion.
    }
    private static bool HasSavedIssuedTask(EntityPlayerLocal player,RebirthTheorySoloOriginalTaskLedger.Task task)
    {
        if(!RebirthSkillAwardService.TryGetEligible(player,out var owner,out var record)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||record?.Origin?.CreationId!=task.Creation)return false;
        var solo=record.Progression?.SoloTheory;
        if(solo?.OriginalTasks==null||!solo.OriginalTasks.TryGet(task.Ordinal,task.Creation,task.Lease,out var issued)||!issued.Published||issued.Held||issued.Completed!=0||issued.RequestId!=task.RequestId||issued.OriginalActor!=task.OriginalActor||issued.Kind!=task.Kind||issued.RecipeBinding!=task.RecipeBinding||issued.BeforeInventoryHash!=task.BeforeInventoryHash||issued.AfterInventoryHash!=task.AfterInventoryHash||issued.Subject!=task.Subject||issued.Portions!=task.Portions||issued.Difficulty!=task.Difficulty)return false;
        return RebirthWorldCharacterRepository.HasSavedSoloOriginalTasks(owner,solo);
    }}
