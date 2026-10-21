using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    [ThreadStatic]private static Frame selectingCachedBoundary;
    private static bool IsCurrentRemoteCachedBoundary(Frame frame)=>frame!=null&&!frame.Closed&&frame.Saved&&frame.Attempted&&
        frame.RemoteNativeNonce!=Guid.Empty&&frame.Observation.World.IsRemote()&&frame.ConfirmedOriginal==null&&
        frame.Observation.FullBoundary&&!frame.Observation.Continued&&!frame.Observation.Accepted&&
        frame.SavedFullBoundaryVersion==frame.Observation.NativeVersionAfter&&frame.Observation.Current()&&frame.Observation.RecipeMatches()&&
        RebirthRecipeOutputIntegration.IsCurrentOwningNativeVersion(frame.Observation.Card,frame.SavedFullBoundaryVersion);
    private static void SelectRemoteCachedBoundary(Frame frame)
    {
        if(frame.CachedBoundarySelected||!IsCurrentRemoteCachedBoundary(frame)||
            (frame.NativeOriginal!=null?!frame.NativeOriginal.SourceCurrentWithoutRow():frame.NativeViewerEffect?.SourceCurrentWithoutRow()!=true&&!IsLoadedDeferredCachedFrame(frame)))return;
        // This is a new purpose observation of the actual full boundary, not
        // a retry of Add/GiveExp, outputStack, insertion or a prior packet.
        frame.CachedBoundarySelected=true;selectingCachedBoundary=frame;
        try
        {
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(
                frame.Observation.Station,StreamModeWrite.ToServer,frame.Observation.Station.lockHandleWaitingFor));
        }
        catch{/* A selected but unproved packet never authorizes reconstruction. */}
        finally{selectingCachedBoundary=null;}
    }
    private static bool IsSavedCachedBoundaryResume(RemoteNativeSetup setup)=>setup.Phase==RemoteNativePhase.CachedBoundary&&
        ReferenceEquals(resumingNativeUpload?.Setup,setup)&&IsCurrentRemoteCachedBoundary(setup.Frame)&&
        (setup.Effect==null||NativeViewerCachedEffectSaved(setup.Frame));
    internal static bool HasExecutingCachedBoundary(World world,TileEntityWorkstation station)=>executingRemoteNative?.Event.CachedBoundary!=null&&
        executingRemoteNative.ReadCompleted&&executingRemoteNative.Reads==1&&!executingRemoteNative.ProcessCompleted&&
        ReferenceEquals(executingRemoteNative.Event.Source.World,world)&&ReferenceEquals(executingRemoteNative.Event.Source.Station,station);
    internal static bool SuppressCachedBoundaryEcho(NetPackageTileEntity packet,World world,TileEntityWorkstation station)
    {
        if(!HasExecutingCachedBoundary(world,station)||packet.ms==null)return false;
        var actual=executingRemoteNative.Event;if(!actual.Source.CurrentViewer()||packet.ms.Length!=actual.Body.Length)return false;
        lock(packet.ms)
        {
            long position=packet.ms.Position;
            try
            {
                packet.ms.Position=0;
                for(int index=0;index<actual.Body.Length;index++)
                {int value=packet.ms.ReadByte();if(value<0)return false;if((index<actual.Offset||index>=actual.Offset+8)&&value!=actual.Body[index])return false;}
                return true;
            }
            finally{packet.ms.Position=position;}
        }
    }
}
