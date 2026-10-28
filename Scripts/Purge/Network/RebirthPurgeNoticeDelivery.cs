using System;
using System.Collections.Generic;

// Presentation custody only: wait for the existing map handshake, never infer a session.
internal static class RebirthPurgeNoticeDelivery
{
    private sealed class Pending
    {
        internal World World;
        internal Guid WorldId, Session;
        internal Action Show;
        internal float Expires;
    }
    private static readonly Queue<Pending> pending=new Queue<Pending>();
    internal static void Reset()=>pending.Clear();
    private static bool CanShow(World world)
    {
        var player=world?.GetPrimaryPlayer();
        return player!=null&&player.IsSpawned()&&!player.IsDead();
    }
    internal static void Receive(World world,Guid worldId,Guid session,Action show)
    {
        if(world==null||!world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
            worldId==Guid.Empty||session==Guid.Empty||show==null)return;
        if(RebirthPoiMapSync.TransportWorld!=Guid.Empty)
        {
            if(worldId!=RebirthPoiMapSync.TransportWorld||session!=RebirthPoiMapSync.TransportSession)return;
            // Map synchronization may finish before the native local player spawns.
            // Keep the existing presentation request rather than calling a Show
            // method which must drop it when no player recipient exists yet.
            if(CanShow(world)){show();return;}
        }
        if(pending.Count>=64)return;
        pending.Enqueue(new Pending{World=world,WorldId=worldId,Session=session,Show=show,
            Expires=UnityEngine.Time.realtimeSinceStartup+60f});
    }
    internal static void Pulse()
    {
        int count=pending.Count;
        for(int i=0;i<count;i++)
        {
            var notice=pending.Dequeue();
            if(!ReferenceEquals(GameManager.Instance?.World,notice.World)||
                UnityEngine.Time.realtimeSinceStartup>notice.Expires)continue;
            if(RebirthPoiMapSync.TransportWorld==Guid.Empty||!CanShow(notice.World)){pending.Enqueue(notice);continue;}
            if(notice.WorldId==RebirthPoiMapSync.TransportWorld&&notice.Session==RebirthPoiMapSync.TransportSession)notice.Show();
        }
    }
}
