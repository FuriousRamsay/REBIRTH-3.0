using System;
using System.IO;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // This is an invocation-bound source arm, not a restored permission.
    // Only the actual admitted envelope owns these private reader instances.
    internal static bool EnterCachedNativePacket(NetPackageTileEntity packet,World world,bool run,out object state)
    {
        state=null;
        var physical=executingCachedPhysical;
        var unused=readingCachedUnused;
        bool isPhysical=physical!=null&&ReferenceEquals(physical.Packet,packet);
        bool isUnused=unused!=null&&ReferenceEquals(unused.Packet,packet);
        if(!isPhysical&&!isUnused)return true;
        try
        {
            if(!run||isPhysical&&isUnused||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||world?.IsRemote()!=false)return false;
            CachedCustody cache;byte[] expected;int offset;EntityPlayer viewer;ClientInfo sender;object original;
            if(isPhysical)
            {
                if(physical.Reads!=0||!physical.Action.Current())return false;
                cache=physical.Action.Issue.Cache;expected=physical.Body;offset=physical.Offset;viewer=physical.Action.Viewer;sender=physical.Action.Sender;original=physical;
            }
            else
            {
                if(unused.Reads!=0||!unused.Source.Action.Current())return false;
                cache=unused.Source.Cache;expected=unused.Source.After;offset=unused.Source.AfterOffset;
                viewer=unused.Source.Viewer;sender=unused.Source.Sender;original=unused;
            }
            if(!ReferenceEquals(cache.World,world)||!ReferenceEquals(world.GetTileEntity(packet.teWorldPos),cache.Station)||
                !ReferenceEquals(packet.Sender,sender)||!ReferenceEquals(world.GetEntity(sender.entityId),viewer)||
                packet.teBlockId!=cache.Station.blockValue.type||packet.handle!=cache.Station.lockHandleWaitingFor||packet.ms==null||packet.ms.Length!=expected.Length||offset<0||offset>expected.Length-8)return false;
            using(var callback=cache.Memory.TryEnterCallbacks())
            {
                if(callback==null)return false;
                lock(packet.ms)
                {
                    long prior=packet.ms.Position;
                    try
                    {
                        packet.ms.Position=0;
                        for(int i=0;i<expected.Length;i++)
                        {int next=packet.ms.ReadByte();if(next<0||i<offset||i>=offset+8){if(next<0||next!=expected[i])return false;}}
                        if(packet.ms.Position!=packet.ms.Length)return false;
                    }
                    finally{packet.ms.Position=prior;}
                }
            }
            state=original;return true;
        }
        catch{return false;}
    }
}
