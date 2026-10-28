// Deferred target-assembly fixture. NOT RUN. Requires independently prepared
// disposable CodexTest reward and two actual authoritative-world players.
using System;

internal static class RebirthBossRewardAccessFixture
{
    private static void Need(bool value,string message)
    {if(!value)throw new InvalidOperationException(message);}

    internal static void Exercise(World world,Entity reward,EntityPlayer owner,EntityPlayer other)
    {
        Need(ThreadManager.IsMainThread()&&world!=null&&!world.IsRemote()&&ReferenceEquals(GameManager.Instance.World,world),"Actual authoritative main-thread world required.");
        Need(GameIO.GetSaveGameDir().IndexOf("CodexTest",StringComparison.OrdinalIgnoreCase)>=0,"Disposable CodexTest only.");
        Need(reward!=null&&owner!=null&&other!=null&&owner.entityId!=other.entityId,"Distinct actual players and reward required.");
        Need(ReferenceEquals(world.GetEntity(reward.entityId),reward)&&ReferenceEquals(world.GetEntity(owner.entityId),owner)&&ReferenceEquals(world.GetEntity(other.entityId),other),"Loaded native objects required.");
        var encounter=RebirthBossEventRewardService.FindReward(reward.entityId);
        Need(encounter!=null&&encounter.Progression!=null,"Prepared durable reward required.");
        Need(RebirthBossEventIdentity.GetStableId(owner)==encounter.Progression.OwnerStableId&&RebirthBossEventIdentity.GetStableId(other)!=encounter.Progression.OwnerStableId,"Fixture identities must match durable owner/nonowner.");
        var original=encounter.SpawnOriginals.Find(value=>value.Role==3);
        Need(original!=null&&original.Phase==3&&!original.RemovalInvoking&&!original.NativeRemoved&&world.worldTime<encounter.RewardExpiresWorldTime,"Published unexpired original required.");
        Need(RebirthBossNativePublication.Matches(world,encounter,original,reward),"Exact published native incarnation required.");
        // Call the production predicate, not an extracted imitation. Native lock
        // acquisition and remote packet identity are separate live scenarios.
        Need(!RebirthBossEventRewardService.AllowRewardLock(reward,other.entityId),"Nonowner admitted.");
        Need(!RebirthBossEventRewardService.AllowRewardLock(reward,int.MinValue),"Absent player admitted.");
        Need(RebirthBossEventRewardService.AllowRewardLock(reward,owner.entityId),"Durable owner denied.");
        Need(reward.spawnById==owner.entityId&&!reward.spawnByAllowShare,"Transient native owner not rebound to current owner.");
        Need(!RebirthBossEventRewardService.AllowRewardLock(reward,other.entityId),"Owner reconciliation opened nonowner access.");
    }
}
