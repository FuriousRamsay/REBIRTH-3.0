using System;
using System.IO;

public static partial class RebirthMetabolismStateRepository
{
    internal static bool HasSavedLocalHydrationReentrySource(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,bool after)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!serverAuthority||player?.world?.IsRemote()!=false||
                !RebirthHydrationOwnerHold.Matches(player,original)||!original.TryGetPlan(out _))return false;
            lock(Sync)
            {
                if(!ScopeCurrentLocked()||!ReferenceEquals(scopeWorld,player.world))return false;
                string id=GetStablePlayerId(player),path=scopePath;int generation=loadGeneration;
                if(!States.TryGetValue(id,out var state)||(after?!ColdHydrationAfterRow(state,original):!ColdRecoveryBeforeRow(state,original)))return false;
                var info=new FileInfo(path);if(!info.Exists||info.Length<1||info.Length>MaximumStoreBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                var saved=ReadCandidate(path);
                return saved.TryGetValue(id,out var row)&&(after?ColdHydrationAfterRow(row,original):ColdRecoveryBeforeRow(row,original))&&
                    generation==loadGeneration&&path==scopePath&&ScopeCurrentLocked()&&
                    States.TryGetValue(id,out var current)&&ReferenceEquals(state,current)&&
                    (after?ColdHydrationAfterRow(current,original):ColdRecoveryBeforeRow(current,original))&&RebirthHydrationOwnerHold.Matches(player,original);
            }
        }
        catch{return false;}
    }
}
