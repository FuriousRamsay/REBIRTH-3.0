using System;
using System.IO;

internal static partial class RebirthProductiveEarnedArtifact
{
    internal static bool TryLoadObservedPending(TileEntityWorkstation station,out Prepared prepared)
    {
        prepared=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(!(RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station))||
                !RebirthProductiveEarnedSavedNativeSource.TryObserved(station,out var source)||!source.Terminal||
                !source.PendingNextWork||source.PendingTimerAmbiguous||
                !(source.ToolsRebased?source.Path.EndsWith(".tools-load",StringComparison.Ordinal):source.Path.EndsWith(".terminal-load",StringComparison.Ordinal))||
                !source.CurrentIdentity(world,station,source.Frame.Birth,source.Frame.Operation)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset))return false;
            string path=source.Path.Substring(0,source.Path.Length-(source.ToolsRebased?".tools-load":".terminal-load").Length);
            if(!FileMatches(path,source.Original))return false;
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var custody))
            {
                using(var admission=RebirthLogisticsEndpointIdentity.Begin(world,station,()=>
                    (RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station))&&source.CurrentIdentity(world,station,source.Frame.Birth,source.Frame.Operation)))
                    if(admission?.Current()!=true)return false;
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out custody))return false;
            }
            if(!Guid.TryParseExact(custody,"N",out source.CustodyBirth)||source.CustodyBirth==Guid.Empty)return false;
            var value=new Prepared(world,station,Path.GetFullPath(GameIO.GetSaveGameDir()),path,source.Original,source.Frame.Birth,source.Frame.Operation)
                {LoadedSource=source};
            // This is a saved predecessor TERMINAL, never an earned row. Do
            // not call MatchesAfter or route it through TryLoadObserved.
            if(!value.Current())return false;prepared=value;return true;
        }
        catch{return false;}
    }
}
