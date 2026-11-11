using System;
using System.Diagnostics;

internal static partial class RebirthHydrationProposalClient
{
    internal sealed partial class Intent
    {
        private readonly string fragmentScope="hydration-proposal|"+Guid.NewGuid().ToString("N");
        internal bool ReceiveFragment(string digest,int total,int count,int index,string part,double now)
        {
            if (!Current()) return false;
            if (!RebirthHydrationOriginalTransport.TryReceive(fragmentScope, Operation, digest, total, count, index, part, now, out var candidate))
            {
                RebirthDebug.Log("metabolism", $"[REBIRTH HydrationTransfer] proposal fragment refused operation={Operation:N} index={index}");
                return false;
            }
            if (candidate == null) return Current();
            bool accepted = Receive(candidate);
            RebirthDebug.Log("metabolism", $"[REBIRTH HydrationTransfer] proposal received operation={Operation:N} originalSaved={originalSaved} ownerCheckpoint={accepted}");
            return accepted;
        }
    }
    internal static bool ReceiveFragment(World world,int playerId,Guid operation,string digest,int total,int count,int index,string part)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World))return false;
        var player=world.GetPrimaryPlayer();
        if(player==null||player.entityId!=playerId||!intents.TryGetValue(player,out var intent)||intent.Operation!=operation||!intent.Current())return false;
        return intent.ReceiveFragment(digest,total,count,index,part,Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency);
    }
}
