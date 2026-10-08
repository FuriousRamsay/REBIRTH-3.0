using System;

// Display-only requests. Contents cannot authorize a debit, withdrawal or owner transfer.
public static class RebirthBackpackLibraryClientViews
{
    private static EntityPlayerLocal owner;
    private static World boundWorld;
    private static object session;
    private static string creation;
    private static Guid pendingRequest;
    private static DateTime nextRemoteRequest;
    private static RebirthBackpackLibraryViewCache cache;
    private static bool Resolve(World world,int playerId,out EntityPlayerLocal player,out object connection,out string current)
    {
        player=null;connection=null;current=null;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(world==null||GameManager.Instance==null||!ReferenceEquals(world,GameManager.Instance.World)||manager==null)return false;
        if(!world.IsRemote())
        {
            player=world.GetPrimaryPlayer();
            if(!ThreadManager.IsMainThread()||!manager.IsServer||player==null||player.entityId!=playerId||
                !ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(playerId),player)||
                !player.IsSpawned()||player.IsDead()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
                !RebirthWorldCharacterRepository.IsServerAuthority||
                !RebirthWorldCharacterService.TryGet(player,out var record)||record==null||!record.IsComplete||
                record.Support==null||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||
                !RebirthSurvivorRequestScope.TryNormalize(record.Origin?.CreationId,out current))return false;
            connection=player;return true;
        }
        if(manager.IsServer)return false;
        player=world.GetPrimaryPlayer();var native=manager.connectionToServer;
        if(player==null||player.entityId!=playerId||native==null||native.Length==0||native[0]==null||native[0].IsDisconnected()||
            !RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out current))return false;
        connection=native[0];return true;
    }
    public static bool Request(World world,int playerId)
    {
        if(!Resolve(world,playerId,out var player,out var connection,out var current))return false;
        if(!ReferenceEquals(boundWorld,world)||!ReferenceEquals(owner,player)||!ReferenceEquals(session,connection)||creation!=current)
        {Reset();boundWorld=world;owner=player;session=connection;creation=current;cache=new RebirthBackpackLibraryViewCache(connection,current);}
        if(world.IsRemote()&&DateTime.UtcNow<nextRemoteRequest)return false;
        var token=Guid.NewGuid();
        try
        {
            if(!world.IsRemote())
            {
                if(!cache.BeginRequest(connection,current,token))return false;
                var status=RebirthBackpackLibraryServer.GetLocalViewStatus(player,current,out var view,out long revision);
                return RebirthBackpackLibraryViewResponse.TryCreate(current,token,revision,status,view,out var response)&&response.Deliver(cache,connection);
            }
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibraryViewRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibraryView));
            if(!cache.BeginRequest(connection,current,token))return false;
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthBackpackLibraryViewRequest>().Setup(playerId,current,token));
            pendingRequest=token;nextRemoteRequest=DateTime.UtcNow.AddSeconds(5);
            return true;
        }
        catch(Exception error)
        {Log.Warning("[REBIRTH Library] Owner view request deferred: "+error.GetType().Name);return false;}
    }
    public static bool Receive(World world,int playerId,RebirthBackpackLibraryViewResponse response)
    {
        return response!=null&&Resolve(world,playerId,out var player,out var connection,out var current)&&
            ReferenceEquals(boundWorld,world)&&ReferenceEquals(owner,player)&&ReferenceEquals(session,connection)&&creation==current&&Deliver(response,connection);
    }
    private static bool Deliver(RebirthBackpackLibraryViewResponse response,object connection)
    {
        // Only the current authenticated response may release refresh backoff.
        if(response.RequestId==pendingRequest&&response.CreationId==creation)
        {pendingRequest=Guid.Empty;nextRemoteRequest=DateTime.MinValue;}
        return response.Deliver(cache,connection);
    }
    public static bool TryGet(World world,int playerId,out RebirthBackpackLibraryView view)
    {
        view=null;return Resolve(world,playerId,out var player,out var connection,out var current)&&
            ReferenceEquals(boundWorld,world)&&ReferenceEquals(owner,player)&&ReferenceEquals(session,connection)&&creation==current&&cache!=null&&cache.TryGet(connection,current,out view);
    }
    // Scoped hint for preparing first gear before any backpack is equipped.
    // Existing view requests/response tokens supply it; no new payload or custody.
    public static bool TryGetRevision(World world,int playerId,out long revision)
    {
        revision=-1;
        return Resolve(world,playerId,out var player,out var connection,out var current)&&
            ReferenceEquals(boundWorld,world)&&ReferenceEquals(owner,player)&&
            ReferenceEquals(session,connection)&&creation==current&&cache!=null&&
            cache.TryGetRevision(connection,current,out revision);
    }
    public static bool IsNoBackpack(World world,int playerId)
    {
        return Resolve(world,playerId,out var player,out var connection,out var current)&&
            ReferenceEquals(boundWorld,world)&&ReferenceEquals(owner,player)&&ReferenceEquals(session,connection)&&creation==current&&
            cache!=null&&cache.IsNoBackpack(connection,current);
    }
    public static void Reset(){pendingRequest=Guid.Empty;nextRemoteRequest=DateTime.MinValue;boundWorld=null;cache=null;owner=null;session=null;creation=null;}
}