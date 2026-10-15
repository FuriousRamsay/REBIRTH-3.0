using System;

// Presentation only: lifetime custody and supply accounts remain the existing authorities.
internal static class RebirthPurgeStatisticsProjection
{
    internal static void Personal(EntityPlayer player,RebirthStatisticsSnapshot snapshot)
    {
        if(player?.world==null||player.world.IsRemote()||!RebirthSandboxOptionManager.Current.IsPurge)return;
        string key=RebirthPurgeKillContributor.Identify(player,player.world as World);if(key==null)return;
        var kills=RebirthPurgeSupplyService.LifetimeKills;
        if(kills!=null){snapshot.PurgeKillsKnown=true;long count;if(kills.TryGetValue(key,out count))snapshot.PurgeKills=count;}
        var accounts=RebirthPurgeSupplyService.Published;
        if(accounts==null)return;
        snapshot.PurgeSuppliesKnown=true;RebirthPurgeSupplyAccount account;
        if(accounts.TryGetValue(key,out account)){snapshot.PurgeDropsEarned=account.EarnedDrops;snapshot.PurgeDropsLaunched=account.DeliveredDrops;}
    }
}
