using System;
using System.Linq;

// A single actual native callback, never a retry of an uncertain original.
// The complete producer must supply its correlated saved host permission;
// this helper does not install hooks or issue permission itself.
internal static class RebirthRemoteResourceBlockExecution
{
    internal static bool TryExecute(EntityPlayerLocal player,ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,
        RebirthRemoteResourceClientOriginal.Intent original,Func<bool> actualSavedHostPermission)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||RemoteResourceClientGrantContext.Active||
                !RebirthRemoteResourceBlockPlan.RestorePrepared(player,action,data,original,actualSavedHostPermission)||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out _,out _,out var paid))return false;
            using(var captured=RebirthRemoteResourceBlockNativeCapture.Begin(player,action,data,original,actualSavedHostPermission))
            {
                if(captured==null||!RebirthRemoteResourceBlockOriginal.BeginExecution(player,original,actualSavedHostPermission))return false;
                using(var owner=RebirthRemoteResourceOwnerAction.BeginBlockOriginalScope(player,original,actualSavedHostPermission))
                {
                    if(owner==null)return false; // Attempted remains retained; never replay.
                    RemoteResourceClientGrantContext.Begin(paid.Select(stack=>stack.Clone()).ToList());
                    try
                    {
                        if(!RemoteResourceClientTransactionCoordinator.InvokeOriginalBlock(action,data,original))return false;
                        return RebirthRemoteResourceOwnerAction.SaveBlockPaidOwner(player,original,captured);
                    }
                    finally{RemoteResourceClientGrantContext.End();}
                }
            }
        }
        catch{return false;} // Retain original/attempt/paid payload on every unknown outcome.
    }
}
