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
        internal Vector3 PlayerPosition,Drop;
        internal float Radius;
        internal bool Spawned;
        internal bool OriginAnchored;
        internal bool Current()=>NativePurge(Controller)&&ReferenceEquals(Controller.activeAirDrop,Flight)&&Flight!=null;
        internal void ProjectOwner(EntitySupplyCrate crate)
        {
            crate.spawnById=Player?.entityId??-1;crate.spawnByName=Player?.EntityName??Localization.Get("xuiRebirthPurgeSupplyOwner");crate.spawnByAllowShare=false;
        }
    }
    private static World world;
    private static Reservation queued,active;
    private static RebirthPurgeSupplyCrateStamp awaitingAccount;
    private static float next;
    private static int entityCursor;
    private static readonly List<string> accountKeys=new List<string>();
    private static string lastAccount;
    private static IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> pendingSnapshot;
    internal static string Diagnostic()=>"worldCurrent="+ReferenceEquals(world,GameManager.Instance?.World)+
        ", queued="+(queued!=null)+", active="+(active!=null)+", flightCurrent="+(active?.Current()??false)+
        ", crateSpawned="+(active?.Spawned??false)+", awaitingAccount="+(awaitingAccount!=null)+
        ", accountScan="+entityCursor+"/"+accountKeys.Count;
    internal static void Reset(bool preserveMarkerLabelsForFinalWrite=false)
    {
        if(preserveMarkerLabelsForFinalWrite)RebirthPurgeSupplyMarkerLabels.PreserveUntilFinalWrite();
        else RebirthPurgeSupplyMarkerLabels.Reset();
        world=null;queued=active=null;awaitingAccount=null;next=0;entityCursor=0;accountKeys.Clear();lastAccount=null;pendingSnapshot=null;
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
        // Native constructs the flight during SpawnAirDrop, but builds paths on
        // its first Tick. Retain the accepted reservation for that later call.
        var request=queued??active;
        if(request==null||!NativePurge(request.Controller)||!ReferenceEquals(__instance.controller,request.Controller))return true;
        if(request.Flight!=null&&!ReferenceEquals(request.Flight,__instance))return true;
        request.Flight=__instance;
        var random=request.Controller.Random;
        // Native builds paths on a later Tick; retain the original queued location,
        // as 2.6 did, rather than mixing a moved player with the reserved drop.
        var center=new Vector2(request.PlayerPosition.x,request.PlayerPosition.z);
        var tangent=random.RandomOnUnitCircle;
        float half=random.RandomRange(50f,100f);
        var drop=new Vector2(request.Drop.x,request.Drop.z);
        var start=center+Vector2.ClampMagnitude(drop-tangent*half-center,request.Radius);
        var end=center+Vector2.ClampMagnitude(drop+tangent*half-center,request.Radius);
        var path=new AIAirDrop.FlightPath {
            Start=new Vector3(start.x,request.PlayerPosition.y+180f,start.y),
            End=new Vector3(end.x,request.PlayerPosition.y+180f,end.y),
            Delay=random.RandomRange(0f,15f)
        };
        if(request.OriginAnchored)
        {path.Start=request.Controller.Director.World.ClampToValidWorldPos(path.Start);path.End=request.Controller.Director.World.ClampToValidWorldPos(path.End);}
        var crate=new AIAirDrop.SupplyCrateSpawn {
            SpawnPos=request.Drop,Delay=(start-drop).magnitude/AIAirDrop.cPlaneMetersPerSecond
        };
        // Match native CreateFlightPaths: its Tick waits for this location, then
        // passes the observer into SpawnSupplyCrate for native landing cleanup.
        crate.ChunkRef=request.Controller.Director.World.GetGameManager().AddChunkObserver(crate.SpawnPos,false,3,-1);
        path.Crates.Add(crate);
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
        RebirthPurgeSupplyMarkerLabels.Bind(director);
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
        if(director.activeAirDrop!=null||LootContainer.NoLoot||AIDirectorAirDropComponent.MaxDayCount==0)return;
        var players=world.aiDirector.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
        var accounts=RebirthPurgeSupplyService.Published;
        var rules=RebirthPurgeSupplyPolicy.Current;
        if(players==null||accounts==null||rules==null)return;
        if(!ReferenceEquals(pendingSnapshot,accounts))
        {accountKeys.Clear();foreach(var pair in accounts)if(pair.Value.DeliveredDrops<pair.Value.EarnedDrops&&(pair.Value.InFlight==0||pair.Value.Delivery!=null&&(pair.Value.Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.Prepared||pair.Value.Delivery.CanResumeFlight)))accountKeys.Add(pair.Key);accountKeys.Sort(StringComparer.Ordinal);pendingSnapshot=accounts;}
        if(accountKeys.Count==0)return;
        var online=new Dictionary<string,EntityPlayer>(StringComparer.Ordinal);bool nativeHasLivingPlayer=false;
        foreach(var tracked in players)
        {
            var player=tracked.Player;if(player==null)continue;
            if(!player.IsDead())nativeHasLivingPlayer=true;
            string key=RebirthPurgeKillContributor.Identify(player,world);if(key!=null)online[key]=player;
        }
        // Native SpawnAirDrop refuses an empty living-player list. Keep receipts
        // pending; never fabricate a player/entity or bypass native construction.
        if(!nativeHasLivingPlayer)return;
        for(int n=0;n<16;n++)
        {
            if(accountKeys.Count==0)return;
            int index=lastAccount==null?-1:accountKeys.BinarySearch(lastAccount,StringComparer.Ordinal);
            index=index<0?(lastAccount==null?0:~index):index+1;if(index>=accountKeys.Count)index=0;
            string key=accountKeys[index];lastAccount=key;var account=accounts[key];
            if(account.DeliveredDrops>=account.EarnedDrops)continue;
            if(account.InFlight!=0&&(account.Delivery==null||account.Delivery.Phase!=RebirthPurgeSupplyDeliveryPhase.Prepared&&!account.Delivery.CanResumeFlight))continue;
            EntityPlayer player;online.TryGetValue(key,out player);
            var origin=account.Origins?.Destination(account.InFlight==0?account.DeliveredDrops+1:account.InFlight);
            if(origin==null&&(player==null||!player.IsSpawned()||!player.IsAlive()||player.IsSpectator||player.AttachedToEntity!=null||player.IsFlyMode.Value))continue;
            Guid id;if(!Guid.TryParseExact(world.worldState?.Guid,"N",out id))return;
            float radius=rules.FlightRadius;
            var playerPosition=origin==null?player.position:world.ClampToValidWorldPos(new Vector3(origin.X,origin.Y,origin.Z));
            Vector3 drop;
            if(account.InFlight!=0)
            {
                drop=new Vector3(account.Delivery.X,account.Delivery.Y,account.Delivery.Z);
                // Prepared retries retain their original saved destination.
                playerPosition=new Vector3(drop.x,drop.y-170f,drop.z);
            }
            else
            {
                var offset=origin==null?director.Random.RandomOnUnitCircle*director.Random.RandomRange(0f,radius):Vector2.zero;
                drop=world.ClampToValidWorldPos(new Vector3(playerPosition.x+offset.x,playerPosition.y+170f,playerPosition.z+offset.y));
                if(new Vector2(drop.x-playerPosition.x,drop.z-playerPosition.z).sqrMagnitude>radius*radius)continue;
                if(Math.Abs(drop.x)>1000000f||Math.Abs(drop.z)>1000000f||drop.y< -4096f||drop.y>4096f)continue;
                RebirthPurgeSupplyAccount prepared;
                if(!RebirthPurgeSupplyService.TryPrepare(account,new RebirthPurgeSupplyDeliveryPlan((int)drop.x,(int)drop.y,(int)drop.z,version:origin==null?2:3),out prepared))return;
                account=prepared;
            }
            if(account.Delivery.Version==3)
            {
                RebirthPurgeSupplyAccount invoking;
                // Persist exact owner/token/POI destination BEFORE native effect.
                // A restarted uncertain v3 invocation is retained, never replayed.
                if(!RebirthPurgeSupplyService.TryInvoking(account,id,out invoking))return;
                account=invoking;
            }
            var request=new Reservation {Controller=director,Player=player,PlayerPosition=playerPosition,Drop=drop,Radius=radius,OriginAnchored=origin!=null,
                Stamp=new RebirthPurgeSupplyCrateStamp(id,key,account.InFlight,account.Token(id))};
            queued=request;
            try
            {
                bool accepted=director.SpawnAirDrop();
                if(accepted&&director.activeAirDrop!=null&&
                    (request.Flight==null||ReferenceEquals(director.activeAirDrop,request.Flight)))
                {
                    request.Flight=director.activeAirDrop;
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
