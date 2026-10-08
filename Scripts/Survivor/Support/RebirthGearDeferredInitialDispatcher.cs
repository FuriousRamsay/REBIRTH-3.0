using System;
// Defers the original remote gear intent until native item-use UI cleanup; no custody before cleanup.
internal static class RebirthGearDeferredInitialDispatcher
{
    private sealed class Pending{internal EntityPlayerLocal Player;internal World World;internal GameManager Game;internal ConnectionManager Manager;internal object Peer,PlayerUI,Xui;internal RebirthGearPreparationIntent Intent;internal bool Dispatching;}
    private static Pending pending;private static long epoch;
    private static bool Current(Pending p){try{return ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance,p.Game)&&
        ReferenceEquals(p.Game.World,p.World)&&ReferenceEquals(p.Player.world,p.World)&&p.World.IsRemote()&&
        ReferenceEquals(p.World.GetPrimaryPlayer(),p.Player)&&ReferenceEquals(p.World.GetEntity(p.Player.entityId),p.Player)&&
        p.Player.IsSpawned()&&!p.Player.IsDead()&&ReferenceEquals(p.Player.PlayerUI,p.PlayerUI)&&ReferenceEquals(p.Player.PlayerUI?.xui,p.Xui)&&
        ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,p.Manager)&&!p.Manager.IsServer&&
        p.Manager.connectionToServer!=null&&p.Manager.connectionToServer.Length>0&&p.Manager.connectionToServer[0]!=null&&
        ReferenceEquals(p.Manager.connectionToServer[0],p.Peer)&&!p.Manager.connectionToServer[0].IsDisconnected()&&
        (p.Intent==null||RebirthSurvivorRequestScope.Matches(p.Intent.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(p.Player)));}catch{return false;}}
    private static void Clear(Pending p){if(ReferenceEquals(pending,p)){pending=null;epoch++;}}
    internal static bool TryQueue(EntityPlayerLocal player,ItemValue selected,bool bag,int index)
    {
        try {
            if(!ThreadManager.IsMainThread()||pending!=null||player?.PlayerUI?.xui==null||!player.PlayerUI.xui.IsUsingItemActionEntryUse)return false;
            var m=SingletonMonoBehaviour<ConnectionManager>.Instance;var peers=m?.connectionToServer;
            if(m==null||peers==null||peers.Length==0||peers[0]==null)return false;
            var p=new Pending{Player=player,World=player.world,Game=GameManager.Instance,Manager=m,Peer=peers[0],PlayerUI=player.PlayerUI,Xui=player.PlayerUI.xui};
            long started=epoch;if(p.Game==null||p.World==null||!Current(p))return false;
            if(!RebirthGearPreparationClient.TryCreateOriginal(player,selected,bag,index,Guid.NewGuid(),out var intent))return false;
            p.Intent=intent;
            if(epoch!=started||pending!=null||!Current(p)||!player.PlayerUI.xui.IsUsingItemActionEntryUse)return false;
            pending=p;return true;
        }catch{return false;}
    }
    internal static void Tick()
    {
        if(!ThreadManager.IsMainThread())return;
        var p=pending;if(p==null||p.Dispatching)return;
        try {
            if(!Current(p)){Clear(p);return;}
            if(p.Player.PlayerUI.xui.IsUsingItemActionEntryUse||p.Player.inventory?.IsHoldingItemActionRunning()==true)return;
            var belt=p.Player.inventory?.ItemGrid?.items;
            bool matches=belt!=null&&RebirthGearInventorySnapshot.TryCapture(p.Player.bag?.ItemGrid?.items,belt,
                RebirthToolbeltCapacity.GetOwnedSlotCount(p.Player,belt.Length),out var snapshot)&&p.Intent.MatchesInventory(snapshot);
            if(!ReferenceEquals(pending,p))return;
            if(!Current(p)||!matches){Clear(p);return;}
            if(p.Player.PlayerUI.xui.IsUsingItemActionEntryUse||p.Player.inventory?.IsHoldingItemActionRunning()==true)return;
            p.Dispatching=true;
            try {
                bool queued=RebirthGearPreparationClient.TryRequestOriginal(p.Player,p.Intent);
                if(!ReferenceEquals(pending,p))return;
                bool held=RebirthGearOwnerReservation.MatchesIntent(p.Player,p.Peer,p.Intent);
                if(!ReferenceEquals(pending,p))return;
                if(queued||held){Clear(p);return;} // Existing pump owns uncertain acquired custody.
                if(!Current(p)){Clear(p);return;}
                var afterBelt=p.Player.inventory?.ItemGrid?.items;
                bool unchanged=afterBelt!=null&&RebirthGearInventorySnapshot.TryCapture(p.Player.bag?.ItemGrid?.items,afterBelt,
                    RebirthToolbeltCapacity.GetOwnedSlotCount(p.Player,afterBelt.Length),out var after)&&p.Intent.MatchesInventory(after);
                if(ReferenceEquals(pending,p)&&(!Current(p)||!unchanged))Clear(p);
            }finally {p.Dispatching=false;}
        }catch {if(ReferenceEquals(pending,p)&&!Current(p))Clear(p);}
    }
    internal static void Reset(){pending=null;epoch++;} // Never releases existing native custody.
}