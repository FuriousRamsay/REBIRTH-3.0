using System;
using System.Linq;

/// <summary>Persistent pending sends. Native tooltip transport has no display acknowledgement.</summary>
public static class RebirthRecipeUnlockNotifications
{
    private static bool TrySave(Func<bool> save)
    {
        try {return save();} catch(Exception){return false;}
    }

    public static bool TrySend(RebirthCookingMemory memory,Func<bool> save,Action<string> send)
    {
        if(memory==null||memory.PendingUnlocks.Count==0||save==null||send==null)return false;
        string name=memory.PendingUnlocks.First();
        if(!TrySave(save))return false; // Unlock and pending notice must be durable before sending.
        try {send(name);} catch(Exception){return false;}
        memory.PendingUnlocks.Remove(name);
        if(TrySave(save))return true;
        memory.PendingUnlocks.Add(name); // Failed send settlement remains retryable; duplicates are possible.
        return false;
    }
}
