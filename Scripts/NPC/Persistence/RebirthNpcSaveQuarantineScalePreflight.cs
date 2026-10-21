using System;

public static partial class RebirthNpcScaleHardeningService
{
    internal static bool CanRetireScaleDomains=>ThreadManager.IsMainThread()&&RebirthNpcSpatialIndex.CanRetireEmptySpatial&&RebirthNpcPathBudgetCoordinator.CanRetirePathScope&&RebirthNpcNetworkBatcher.CanRetireEmptyBatches&&RebirthNpcRelationshipBatcher.CanRetireEmptyRelationships&&RebirthNpcSerializationBufferPool.CanRetireIdlePool&&RebirthNpcRetentionCoordinator.CanRetireEmptyRetention&&RebirthNpcReplicationPerformanceService.CanRetireEmptyProjection;
    internal static bool CanRecoverScaleDomains=>CanRetireScaleDomains&&RebirthNpcSerializationBufferPool.CanRecoverEmptyPool;
}
public static partial class RebirthNpcSerializationBufferPool
{
    internal static bool CanRecoverEmptyPool{get{lock(Sync)return CanRetireIdlePool&&Buffers.Count==0;}}
}
