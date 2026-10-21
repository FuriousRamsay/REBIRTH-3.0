using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal static bool HasAdoptedRemoteViewerTools(TileEntityWorkstation station,Guid operation,byte[] after,int offset)
        =>retained.TryGetValue(station,out var original)&&original.Frame.Operation==operation&&original.Terminal?.ToolRebaseCurrent()==true&&
            RebirthProductiveEarnedNativeBody.SameExceptElapsed(original.Terminal.Body,original.Terminal.ElapsedOffset,after,offset)&&original.Terminal.Current();
    internal sealed partial class SavedPendingToolsObservation
    {
        internal bool AdoptSavedRemoteViewerTools(byte[] after,int offset,Func<bool> savedSource)
        {
            if(!RetainedContext()||artifact.Current()!=true||savedSource?.Invoke()!=true||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,after,offset)||
                !RebirthProductiveEarnedSavedNativeSource.ToolsPublicationCurrent(Birth,Operation))return false;
            var allocation=terminal.Memory?.TryObserveNative(after,offset);if(allocation==null)return false;
            if(!RetainedContext()||!savedSource()){allocation.Dispose();return false;}
            var previous=terminal.Allocation;terminal.Allocation=allocation;terminal.Body=(byte[])after.Clone();terminal.ElapsedOffset=offset;
            terminal.SavedOrigin=null;terminal.RemoteSavedSource=savedSource;terminal.RemoteToolsRebased=true;
            previous?.Dispose();return true;
        }
    }
}
