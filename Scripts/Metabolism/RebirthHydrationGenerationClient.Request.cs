using System;
using System.Diagnostics;

internal static partial class RebirthHydrationGenerationClient
{
    internal sealed class RequestScope
    {
        internal readonly EntityPlayerLocal Player;internal readonly Guid WorldId;internal readonly int PlayerId,Deaths;
        private readonly World world;private readonly GameManager game;private readonly ConnectionManager manager;
        private readonly object peer;private readonly string owner;
        private RequestScope(EntityPlayerLocal player,object peer,Guid worldId,string owner)
        {Player=player;PlayerId=player.entityId;Deaths=player.Died;world=player.world;game=GameManager.Instance;
            manager=SingletonMonoBehaviour<ConnectionManager>.Instance;this.peer=peer;WorldId=worldId;this.owner=owner;}
        internal bool Current()=>Resolve(Player,out var nowPeer,out var nowWorld,out var nowOwner)&&
            ReferenceEquals(Player.world,world)&&ReferenceEquals(game,GameManager.Instance)&&
            ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&ReferenceEquals(peer,nowPeer)&&
            PlayerId==Player.entityId&&Deaths==Player.Died&&WorldId==nowWorld&&owner==nowOwner;
        internal bool SameSession(RequestScope other)=>other!=null&&ReferenceEquals(Player,other.Player)&&
            ReferenceEquals(world,other.world)&&ReferenceEquals(game,other.game)&&ReferenceEquals(manager,other.manager)&&
            ReferenceEquals(peer,other.peer)&&PlayerId==other.PlayerId&&Deaths==other.Deaths&&WorldId==other.WorldId&&owner==other.owner;
        internal static bool TryCapture(EntityPlayerLocal player,out RequestScope scope)
        {
            scope=null;if(!Resolve(player,out var peer,out var world,out var owner))return false;
            var next=new RequestScope(player,peer,world,owner);if(!next.Current())return false;scope=next;return true;
        }
    }
    private static RequestScope requested;private static double nextRequest,lastRequest;private static bool requesting;
    private static World bootstrapWorld;private static double nextBootstrap,lastBootstrap;
    internal static bool TickBootstrap()=>TickBootstrap(Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency);
    internal static bool TickBootstrap(double now)
    {
        if(!ThreadManager.IsMainThread()||double.IsNaN(now)||double.IsInfinity(now))return false;
        var world=GameManager.Instance?.World;
        if(world?.IsRemote()!=true||RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        if(ReferenceEquals(world,bootstrapWorld)&&now>=lastBootstrap&&now<nextBootstrap)return false;
        bootstrapWorld=world;lastBootstrap=now;nextBootstrap=now+0.75d;
        return TryRequest(world.GetPrimaryPlayer(),now);
    }
    internal static bool TryRequest(EntityPlayerLocal player)
        =>TryRequest(player,Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency);
    internal static bool TryRequest(EntityPlayerLocal player,double now)
    {
        if(!ThreadManager.IsMainThread()||requesting||double.IsNaN(now)||double.IsInfinity(now))return false;
        requesting=true;
        try
        {
            if(TryGet(player,out _)||!RequestScope.TryCapture(player,out var scope))return false;
            if(scope.SameSession(requested)&&now>=lastRequest&&now<nextRequest)return false;
            requested=scope;lastRequest=now;nextRequest=now+0.75d;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationGenerationRequest));
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationGenerationRequest>().Setup(scope);
            if(!scope.Current())return false;
            manager.SendToServer(packet);return scope.Current();
        }
        catch{return false;}
        finally{requesting=false;}
    }
}
