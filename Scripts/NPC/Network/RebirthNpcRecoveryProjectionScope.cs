using System;

// A recovery generation belongs to the current native world/connection only.
// Semantic revisions remain the exact restored values. A V1 packet is generation
// zero and cannot undo any domain after a newer recovery generation was observed.
internal static partial class RebirthNpcRecoveryProjectionScope
{
    private static World world;
    private static string directory;
    private static uint epoch, connection;
    private static ulong generation;
    internal static ulong Current
    {
        get{if(!ThreadManager.IsMainThread())throw new InvalidOperationException("NPC recovery generation requires main thread.");EnsureScope();return GameManager.Instance?.World?.IsRemote()==true?generation:RebirthNpcRecoveryGeneration.RequireCurrent();}
    }
    private static void EnsureScope()
    {
        var current=GameManager.Instance?.World;string save=GameIO.GetSaveGameDir();
        uint nextEpoch=RebirthNpcNetworkEpoch.ClientEpoch,nextConnection=RebirthNpcNetworkEpoch.ClientConnectionEpoch;
        if(!ReferenceEquals(world,current)||directory!=save||epoch!=nextEpoch||connection!=nextConnection)
        {
            if(world!=null&&world.IsRemote())throw new InvalidOperationException("Original client recovery-generation floor requires typed projection retirement");
            world=current;directory=save;epoch=nextEpoch;connection=nextConnection;generation=0;
        }
    }
    internal static bool Observe(ulong incoming)
    {
        if(!ThreadManager.IsMainThread())return false;
        var current=GameManager.Instance?.World;
        if(current?.IsRemote()!=true)return true;
        EnsureScope();
        if(incoming<generation)return false;
        if(incoming>generation)generation=incoming;
        return true;
    }
}
