using System;
using System.IO;

public static partial class RebirthNpcPersistenceTimelineService
{
    // Same native branch only. This does not invoke EnsureAligned's backup or
    // checkpoint restoration arms and never advances a saved generation.
    internal static bool TryAdoptNativeAlignment(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(source?.Current()!=true)return false;
        lock(TimelineSync)try
        {
            if(alignmentFailed||rollbackUnresolved||timelineWorld!=null&&!ReferenceEquals(timelineWorld,source.World))return false;
            string save=source.Directory;
            if(!RebirthNpcSaveQuarantineScope.SameDirectory(save,GameIO.GetSaveGameDir()))return false;
            if(alignmentAttempted)return ReferenceEquals(timelineWorld,source.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(timelineDirectory,save);
            // An interrupted timeline restore remains its original transaction;
            // a native load cannot authorize choosing another historical head.
            foreach(string pending in Directory.EnumerateDirectories(save,".rebirth-npc-restore-rollback-*"))
                if(!File.Exists(Path.Combine(pending,RestoreDoneName)))return false;
            var group=RebirthNpcLiveRecoveryGroup.Read(save);
            if(group!=null)
            {
                if(group.Phase!=2)return false;
                foreach(var member in group.Members)if(!RebirthNpcRecoveryPublications.IsInstalled(Path.Combine(save,member.File)))return false;
            }
            else foreach(string file in SidecarNames)
                if(File.Exists(RebirthNpcPersistenceFile.RecoveryIntentPath(Path.Combine(save,file))))return false;
            string root=Path.Combine(save,TimelineDirectoryName);
            var checkpoints=Directory.Exists(root)?EnumerateCheckpoints(root):new System.Collections.Generic.List<ulong>();
            ulong newest=checkpoints.Count==0?0:checkpoints[checkpoints.Count-1];
            ulong time=source.World.worldTime;
            if(newest>time||!source.Current()||source.World.worldTime!=time)return false;
            timelineWorld=source.World;timelineDirectory=RebirthNpcSaveQuarantineScope.ScopeDirectory(save);
            currentWorldTime=time;newestCheckpoint=newest;restoredCheckpoint=0;
            alignmentAttempted=true;alignmentFailed=false;rollbackDetected=false;rollbackUnresolved=false;
            lastDetail="Actual incoming native scope aligned to current saved branch; no timeline restore invoked";return true;
        }
        catch{return false;}
    }
    internal static bool TryAdoptNativeOwnerAlignment(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(source?.Current()!=true)return false;
        lock(TimelineSync)try
        {
            if(alignmentFailed||rollbackUnresolved||timelineWorld!=null&&!ReferenceEquals(timelineWorld,source.World))return false;
            string save=source.Directory;
            if(!RebirthNpcSaveQuarantineScope.SameDirectory(save,GameIO.GetSaveGameDir()))return false;
            if(alignmentAttempted)return ReferenceEquals(timelineWorld,source.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(timelineDirectory,save);
            // An interrupted timeline restore remains its original transaction;
            // a native load cannot authorize choosing another historical head.
            foreach(string pending in Directory.EnumerateDirectories(save,".rebirth-npc-restore-rollback-*"))
                if(!File.Exists(Path.Combine(pending,RestoreDoneName)))return false;
            var group=RebirthNpcLiveRecoveryGroup.Read(save);
            if(group!=null)
            {
                if(group.Phase!=2)return false;
                foreach(var member in group.Members)if(!RebirthNpcRecoveryPublications.IsInstalled(Path.Combine(save,member.File)))return false;
            }
            else foreach(string file in SidecarNames)
                if(File.Exists(RebirthNpcPersistenceFile.RecoveryIntentPath(Path.Combine(save,file))))return false;
            string root=Path.Combine(save,TimelineDirectoryName);
            var checkpoints=Directory.Exists(root)?EnumerateCheckpoints(root):new System.Collections.Generic.List<ulong>();
            ulong newest=checkpoints.Count==0?0:checkpoints[checkpoints.Count-1];
            ulong time=source.World.worldTime;
            if(newest>time||!source.Current()||source.World.worldTime!=time)return false;
            timelineWorld=source.World;timelineDirectory=RebirthNpcSaveQuarantineScope.ScopeDirectory(save);
            currentWorldTime=time;newestCheckpoint=newest;restoredCheckpoint=0;
            alignmentAttempted=true;alignmentFailed=false;rollbackDetected=false;rollbackUnresolved=false;
            lastDetail="Actual incoming native scope aligned to current saved branch; no timeline restore invoked";return true;
        }
        catch{return false;}
    }
}

internal static partial class RebirthNpcLiveRecoveryRuntime
{
    internal static bool TryAdoptNativeDriver(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(source?.Current()!=true||pumping||current!=null||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        try
        {
            if(context!=null&&!ReferenceEquals(context,source.World))return false;
            if(context!=null&&!RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory))return false;
            // Pending sessions keep their original coordinator/fences/operation.
            // Native readiness alone cannot construct a second recovery owner.
            if(RebirthNpcLiveRecoveryGroup.Read(source.Directory)!=null)return false;
            foreach(var pair in deferred)
                if(!ReferenceEquals(pair.Value,source.World)||pair.Key==null||!ReferenceEquals(pair.Key.world,source.World))return false;
            if(!source.Current())return false;
            context=source.World;readyWorld=source.World;directory=GameIO.GetSaveGameDir();retry=0;
            lastDetail="Actual incoming native recovery-driver scope; retained sessions are not replayed";
            return true;
        }
        catch{return false;}
    }
    internal static bool TryAdoptNativeOwnerDriver(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(source?.Current()!=true||deferred.Count!=0||pumping||current!=null||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        try
        {
            if(context!=null&&!ReferenceEquals(context,source.World))return false;
            if(context!=null&&!RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory))return false;
            // Pending sessions keep their original coordinator/fences/operation.
            // Native readiness alone cannot construct a second recovery owner.
            if(RebirthNpcLiveRecoveryGroup.Read(source.Directory)!=null)return false;
            foreach(var pair in deferred)
                if(!ReferenceEquals(pair.Value,source.World)||pair.Key==null||!ReferenceEquals(pair.Key.world,source.World))return false;
            if(!source.Current())return false;
            context=source.World;readyWorld=source.World;directory=GameIO.GetSaveGameDir();retry=0;
            lastDetail="Actual incoming native recovery-driver scope; retained sessions are not replayed";
            return true;
        }
        catch{return false;}
    }
}
