using System;

// Stable ownership is server authority. Native spawnBy fields are display and
// compatibility projection only, and are rebound to the authenticated opener.
internal static class RebirthPurgeSupplyAccess
{
    private static bool Owned(EntitySupplyCrate crate,out RebirthPurgeSupplyCrateStamp stamp)
    {
        stamp=null;
        if(crate==null||!(crate.world is World))return false;
        Guid world;
        return Guid.TryParseExact(((World)crate.world).worldState?.Guid,"N",out world)&&
            RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(crate,world,out stamp);
    }
    internal static bool AllowOpen(EntitySupplyCrate crate,EntityPlayer player)
    {
        RebirthPurgeSupplyCrateStamp stamp;
        if(!Owned(crate,out stamp))return true;
        if(RebirthPurgeKillContributor.Identify(player,crate.world as World)!=stamp.Player)return false;
        crate.spawnById=player.entityId;crate.spawnByName=player.EntityName??string.Empty;crate.spawnByAllowShare=false;
        return true;
    }
    internal static void AfterActivationText(EntitySupplyCrate __instance,ref string __result,bool __runOriginal)
    {
        RebirthPurgeSupplyCrateStamp projection;
        if(!__runOriginal||string.IsNullOrEmpty(__result)||!RebirthPurgeSupplyCrateSerialization.TryReadProjection(__instance,out projection))return;
        string owner=string.IsNullOrWhiteSpace(__instance.spawnByName)?Localization.Get("xuiRebirthPurgeSupplyOwner"):__instance.spawnByName;
        __result=string.Format(Localization.Get("xuiRebirthPurgeSupplyReserved"),owner)+"\n"+__result;
    }
    internal static bool BeforeLock(EntityAlive __instance,int _lockingPlayerID,ref bool __result)
    {
        var crate=__instance as EntitySupplyCrate;
        if(crate==null||AllowOpen(crate,crate.world?.GetEntity(_lockingPlayerID) as EntityPlayer))return true;
        __result=false;return false;
    }
    internal static bool BeforeBag(NetPackageBag __instance,World _world)
    {
        var crate=_world?.GetEntity(__instance?.entityId??-1) as EntitySupplyCrate;RebirthPurgeSupplyCrateStamp stamp;
        if(!Owned(crate,out stamp))return true;
        if(_world.IsRemote()||__instance.Sender==null||!__instance.Sender.loginDone)return false;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager==null||!ReferenceEquals(manager.Clients.ForEntityId(__instance.Sender.entityId),__instance.Sender))return false;
        var player=_world.GetEntity(__instance.Sender.entityId) as EntityPlayer;int owner;
        return AllowOpen(crate,player)&&LockManager.Instance.singleLocks.TryGetByValue(new LockManager.LockEntry(crate,0),out owner)&&owner==player.entityId;
    }
}
