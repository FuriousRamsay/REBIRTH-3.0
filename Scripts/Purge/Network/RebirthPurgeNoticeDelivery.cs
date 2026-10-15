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
    internal static void Receive(World world,Guid worldId,Guid session,Action show)
    {
        if(world==null||!world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
            worldId==Guid.Empty||session==Guid.Empty||show==null)return;
        if(RebirthPoiMapSync.LocalWorld!=Guid.Empty)
        {
            if(worldId==RebirthPoiMapSync.LocalWorld&&session==RebirthPoiMapSync.LocalSession)show();
            return;
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
            if(RebirthPoiMapSync.LocalWorld==Guid.Empty){pending.Enqueue(notice);continue;}
            if(notice.WorldId==RebirthPoiMapSync.LocalWorld&&notice.Session==RebirthPoiMapSync.LocalSession)notice.Show();
        }
    }
}
