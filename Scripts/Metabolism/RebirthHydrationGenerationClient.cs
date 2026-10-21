using System;

// Native-mode metadata projection only. It grants no item/source mutation and
// never clears originals retained under an older generation/native session.
internal static partial class RebirthHydrationGenerationClient
{
    private sealed class Entry
    {
        internal EntityPlayerLocal Player;internal World World;internal GameManager Game;internal ConnectionManager Manager;
        internal object Peer;internal RebirthHydrationGenerationFrame Frame;
        internal bool Current()=>Resolve(Player,out var peer,out var world,out var owner)&&ReferenceEquals(Player.world,World)&&
            ReferenceEquals(Game,GameManager.Instance)&&ReferenceEquals(Manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&
            ReferenceEquals(peer,Peer)&&Frame.PlayerId==Player.entityId&&Frame.World==world&&Frame.Owner==owner&&Frame.Deaths==Player.Died;
    }
    private static Entry current;private static World retiredWorld;
    internal static void RetireWorld()
    {
        if(!ThreadManager.IsMainThread())return;
        retiredWorld=GameManager.Instance?.World??current?.World;current=null;
    }
    internal static bool Receive(World world,RebirthHydrationGenerationFrame frame)
    {
        try
        {
            var player=world?.GetPrimaryPlayer();
            if(frame==null||!ReferenceEquals(world,GameManager.Instance?.World)||!Resolve(player,out var peer,out var serverWorld,out var owner)||
                frame.PlayerId!=player.entityId||frame.World!=serverWorld||frame.Owner!=owner||frame.Deaths!=player.Died)return false;
            if(current!=null&&current.Current()&&ReferenceEquals(current.Player,player))
            {
                if(frame.Sequence<current.Frame.Sequence)return false;
                if(frame.Sequence==current.Frame.Sequence)return frame.Generation==current.Frame.Generation;
            }
            var next=new Entry{Player=player,World=world,Game=GameManager.Instance,Manager=SingletonMonoBehaviour<ConnectionManager>.Instance,Peer=peer,Frame=frame};
            if(!next.Current())return false;current=next;return true;
        }
        catch{return false;}
    }
    internal static bool TryGet(EntityPlayerLocal player,out string generation)
    {
        generation=null;
        try{if(player==null||current==null||!ReferenceEquals(current.Player,player)||!current.Current())return false;generation=current.Frame.Generation;return true;}
        catch{return false;}
    }
    private static bool Resolve(EntityPlayerLocal player,out object peer,out Guid serverWorld,out string owner)
    {
        peer=null;serverWorld=Guid.Empty;owner=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||RebirthSurvivorMode.IsEnabledForCurrentWorld()||player?.world?.IsRemote()!=true||manager==null||manager.IsServer||
            ReferenceEquals(player.world,retiredWorld)||!ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.IsDead()||!player.IsSpawned()||
            !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out serverWorld)||serverWorld==Guid.Empty||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return false;
        var peers=manager.connectionToServer;if(peers==null||peers.Length==0||peers[0]==null||peers[0].IsDisconnected())return false;
        owner=identity.StorageKey;peer=peers[0];return true;
    }
}
