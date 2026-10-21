using System;
using Newtonsoft.Json.Linq;

internal static partial class RebirthRemoteResourceBlockProtocol
{
    internal static JObject CaptureOwnerRelease(EntityPlayer actor,Guid operation)
    {
        var result=new JObject{["available"]=false,["operation"]=operation.ToString("N")};
#if REBIRTH_DEBUG
        try
        {
            if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||operation==Guid.Empty||
                actor is not EntityPlayerLocal player||!player.IsSpawned()||player.IsDead()||player.world?.IsRemote()!=true||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player)||
                !RebirthRemoteResourceClientOriginal.TryRead(operation,out var original)||
                !RebirthRemoteResourceBlockOriginal.TerminalScope(player,original))return result;
            bool completed=RebirthRemoteResourceClientOriginal.IsCompletedBlockOriginal(original);
            bool pending=clientPending.TryGetValue(operation,out var retained),held=RebirthRemoteResourceOwnerAction.HasRetainedBlockHold(operation);
            bool financial=pending&&!retained.UsedTerminal;
            result["available"]=true;result["originalDigest"]=original.Digest;result["completedOriginal"]=completed;
            result["retainedHold"]=held;result["pendingOriginal"]=pending;result["pendingFinancial"]=financial;
            result["released"]=completed&&!held&&!financial;
        }
        catch{result["unknown"]=true;}
#endif
        return result;
    }
}
