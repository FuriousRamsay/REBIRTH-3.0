// Ordinary reset only admits a positively empty projection/observation domain.
// Nonempty entries keep their original typed handles for the full bundle; an ACK
// or elapsed timeout is not used here as a substitute for ownership transfer.
public static partial class RebirthNpcNativeEquipmentBridge
{internal static bool CanRetireEmptyProjection{get{lock(Sync)return Applied.Count==0;}}}
internal static partial class RebirthNpcRecoveryProjectionNetwork
{internal static bool CanRetireEmptyMailbox=>ThreadManager.IsMainThread()&&Deliveries.Count==0&&Pending.Count==0&&Names.Count==0;}
internal static partial class RebirthNpcPendingRuntimeStates
{internal static bool CanRetireEmptyProjection=>ThreadManager.IsMainThread()&&Pending.Count==0&&ByEntity.Count==0;}
internal static partial class RebirthNpcPendingEquipmentSnapshots
{internal static bool CanRetireEmptyProjection=>ThreadManager.IsMainThread()&&pending.Count==0;}
internal static partial class RebirthNpcPendingAppearanceSnapshots
{internal static bool CanRetireEmptyProjection=>ThreadManager.IsMainThread()&&Pending.Count==0;}
public static partial class RebirthNpcInteractionNetworkClient
{internal static bool CanRetireEmptyCallbacks{get{lock(Sync)return Pending.Count==0;}}}
internal static partial class RebirthNpcLiveRecoveryRuntime
{
    internal static bool CanRetireEmptyDriver=>ThreadManager.IsMainThread()&&!pumping&&current==null&&deferred.Count==0&&deferredProjected.Count==0;
    internal static bool TryRetireEmptyDriver()
    {
        if(!CanRetireEmptyDriver)return false;
        if(ReferenceEquals(readyWorld,context))readyWorld=null;
        context=null;directory=null;retry=0;loadedAfterRecovery=false;
        lastCompleted=System.Guid.Empty;lastCompletedTarget=null;lastCompletedGeneration=0;
        return true;
    }
}
