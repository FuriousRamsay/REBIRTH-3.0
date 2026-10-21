using System;

public static partial class RebirthNpcRuntimeRegistry
{
    // Positively captured EMPTY original roster plus an actual player native
    // load can establish the incoming namespace without inventing an NPC.
    internal static bool StageEmptyIncomingNativeRuntime(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(source?.Current()!=true||!RebirthNpcSaveQuarantineBundle.CanStageEmptyServerRuntimeNativeScope(source))return false;
        lock(Sync)
        {
            if(!source.Current()||ByEntityId.Count!=0||EntityByStableId.Count!=0||
                registryWorld!=null&&!ReferenceEquals(registryWorld,source.World)||
                registryDirectory!=null&&!RebirthNpcSaveQuarantineScope.SameDirectory(registryDirectory,source.Directory))return false;
            registryWorld=source.World;registryDirectory=source.Directory;return true;
        }
    }
}

public static partial class RebirthNpcNetworkEpoch
{
    // The actual owner native load is the source. A parsed epoch number or a
    // marker/file cannot construct this namespace bootstrap capability.
    internal static bool StageIncomingNativeOwnerEpoch(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(source?.Current()!=true||!RebirthNpcSaveQuarantineBundle.CanStageEmptyServerRuntimeNativeScope(source))return false;
        uint epoch=GetServerEpoch();
        lock(Sync)return epoch!=0&&source.Current()&&ReferenceEquals(serverWorld,source.World)&&
            ReferenceEquals(serverManager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&activeServerEpoch==epoch&&
            clientWorld==null&&clientManager==null&&clientTransport==null&&clientDirectory==null&&clientEpoch==0&&clientConnectionEpoch==0;
    }
}
