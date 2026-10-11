using System;
using System.Collections.Generic;
using UnityEngine;

// Reserve one earned reward, ask the native director to fly it, and consume it
// when accepted. Native Tick owns plane movement, crate creation and observers.
internal static class RebirthPurgeSupplyCoordinator
{
    internal sealed class Reservation
    {
        internal AIDirectorAirDropComponent Controller;
        internal AIAirDrop Flight;
        internal EntityPlayer Player;
        internal RebirthPurgeSupplyCrateStamp Stamp;
        internal Vector3 Drop;
        internal bool Spawned;
        internal bool Current()=>NativePurge(Controller)&&ReferenceEquals(Controller.activeAirDrop,Flight)&&Flight!=null;
        internal void ProjectOwner(EntitySupplyCrate crate)
        {
            crate.spawnById=Player.entityId;crate.spawnByName=Player.EntityName??string.Empty;crate.spawnByAllowShare=false;
        }
    }
    private static World world;
    private static Reservation queued,active;
    private static RebirthPurgeSupplyCrateStamp awaitingAccount;
    private static float next;
    private static int playerCursor,entityCursor;
    internal static void Reset()
    {
        world=null;queued=active=null;awaitingAccount=null;next=0;playerCursor=entityCursor=0;
    }
    private static bool NativePurge(AIDirectorAirDropComponent owner)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var native=owner?.Director?.World;
        return RebirthPurgeReleasePolicy.Enabled&&RebirthSandboxOptionManager.Current.IsPurge&&ThreadManager.IsMainThread()&&
            native!=null&&!native.IsRemote()&&manager!=null&&manager.IsServer&&ReferenceEquals(GameManager.Instance?.World,native);
    }
    internal static bool BeforeScheduled(AIDirectorAirDropComponent __instance,ref bool __result)
    {
        if(!NativePurge(__instance)||queued!=null&&ReferenceEquals(queued.Controller,__instance))return true;
        __result=false;return false;
    }
    internal static bool BeforeDirectorTick(AIDirectorAirDropComponent __instance)=>
        !NativePurge(__instance)||__instance.activeAirDrop!=null;

    internal static bool BeforeFlightPaths(AIAirDrop __instance)
    {
        var request=queued;
        if(request==null||!NativePurge(request.Controller)||!ReferenceEquals(__instance.controller,request.Controller))return true;
        request.Flight=__instance;
        var random=request.Controller.Random;
        var center=new Vector2(request.Player.position.x,request.Player.position.z);
        var tangent=random.RandomOnUnitCircle;
        float half=random.RandomRange(50f,100f);
        var drop=new Vector2(request.Drop.x,request.Drop.z);
        var start=center+Vector2.ClampMagnitude(drop-tangent*half-center,100f);
        var end=center+Vector2.ClampMagnitude(drop+tangent*half-center,100f);
        var path=new AIAirDrop.FlightPath {
            Start=new Vector3(start.x,request.Player.position.y+180f,start.y),
            End=new Vector3(end.x,request.Player.position.y+180f,end.y),
            Delay=random.RandomRange(0f,15f)
        };
        path.Crates.Add(new AIAirDrop.SupplyCrateSpawn {
            SpawnPos=request.Drop,Delay=(start-drop).magnitude/AIAirDrop.cPlaneMetersPerSecond
        });
        __instance.flightPaths=new List<AIAirDrop.FlightPath>{path};
        return false;
    }
    internal static void BeforeCrate(AIDirectorAirDropComponent __instance,Vector3 __0,out RebirthPurgeSupplySpawnScope __state)
    {
        __state=null;var request=active;
        if(request==null||request.Spawned||!ReferenceEquals(request.Controller,__instance)||!request.Current()||
           (__0-request.Drop).sqrMagnitude>1f)return;
        request.Spawned=true;
        __state=new RebirthPurgeSupplySpawnScope(world,request.Stamp,request,request.Current);
    }
    internal static Exception AfterCrate(Exception __exception,RebirthPurgeSupplySpawnScope __state)
    {
        __state?.Dispose();return __exception;
    }
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge){Reset();return;}
        var native=GameManager.Instance?.World;
        var director=native?.aiDirector?.GetComponent<AIDirectorAirDropComponent>();
        if(!NativePurge(director))return;
        if(!ReferenceEquals(world,native)){Reset();world=native;}
        if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+5f;
        if(awaitingAccount!=null)
        {
            if(!RebirthPurgeSupplyService.TryLaunched(awaitingAccount))return;
            awaitingAccount=null;
        }
        if(active!=null&&!ReferenceEquals(active.Flight,director.activeAirDrop))active=null;
        // Only loaded legacy crates are reconciled. No forced region/chunk reads.
        var entities=world.Entities.list;
        for(int n=0,limit=Math.Min(64,entities.Count);n<limit;n++)
        {
            if(entityCursor>=entities.Count)entityCursor=0;
            var crate=entities[entityCursor++] as EntitySupplyCrate;
            if(crate!=null)RebirthPurgeSupplyService.ReconcileLoadedCrate(crate);
        }
        if(director.activeAirDrop!=null||LootContainer.NoLoot)return;
        var players=world.aiDirector.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
        var accounts=RebirthPurgeSupplyService.Published;
        if(players==null||players.Count==0||accounts==null)return;
        for(int n=0,limit=Math.Min(16,players.Count);n<limit;n++)
        {
            if(playerCursor>=players.Count)playerCursor=0;
            var player=players[playerCursor++].Player;
            if(player==null||player.IsDead()||player.AttachedToEntity!=null||player.IsFlyMode.Value)continue;
            var key=RebirthPurgeKillContributor.Identify(player,world);RebirthPurgeSupplyAccount account;
            if(key==null||!accounts.TryGetValue(key,out account)||account.DeliveredDrops>=account.EarnedDrops)continue;
            if(account.InFlight!=0&&(account.Delivery==null||account.Delivery.Phase!=RebirthPurgeSupplyDeliveryPhase.Prepared&&!account.Delivery.CanResumeFlight))continue;
            Guid id;if(!Guid.TryParseExact(world.worldState?.Guid,"N",out id))return;
            var offset=director.Random.RandomOnUnitCircle*director.Random.RandomRange(0f,100f);
            var drop=world.ClampToValidWorldPos(new Vector3(player.position.x+offset.x,player.position.y+170f,player.position.z+offset.y));
            if(new Vector2(drop.x-player.position.x,drop.z-player.position.z).sqrMagnitude>10000f)continue;
            if(account.InFlight==0)
            {
                RebirthPurgeSupplyAccount prepared;
                if(!RebirthPurgeSupplyService.TryPrepare(account,new RebirthPurgeSupplyDeliveryPlan((int)drop.x,(int)drop.y,(int)drop.z),out prepared))return;
                account=prepared;
            }
            var request=new Reservation {Controller=director,Player=player,Drop=drop,
                Stamp=new RebirthPurgeSupplyCrateStamp(id,key,account.InFlight,account.Token(id))};
            queued=request;
            try
            {
                bool accepted=director.SpawnAirDrop();
                // Native returns true with no flight when airdrops/loot are disabled.
                if(accepted&&request.Flight!=null&&ReferenceEquals(director.activeAirDrop,request.Flight))
                {
                    active=request;awaitingAccount=request.Stamp;
                    RebirthPurgeSupplyNotification.Send(player);
                    if(RebirthPurgeSupplyService.TryLaunched(awaitingAccount))awaitingAccount=null;
                }
            }
            finally {queued=null;}
            return;
        }
    }
}
