using System;

internal sealed partial class RebirthHydrationRemoteClientTerminal
{
    internal static bool ReceiveClosed(World world,int playerId,Guid operation,string digest)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World))return false;
        var player=world.GetPrimaryPlayer();
        if(player==null||player.entityId!=playerId||!TryGetAcknowledgment(player,operation,digest,out _)||
            !seen.TryGetValue(operation,out var original)||!original.Released||
            !RebirthHydrationProposalClient.TryRetireClosed(player,operation,digest))return false;
        // Scheduling/description records only. Saved native markers remain for
        // historical recovery; this never reopens or reapplies an item action.
        return seen.Remove(operation);
    }
}
