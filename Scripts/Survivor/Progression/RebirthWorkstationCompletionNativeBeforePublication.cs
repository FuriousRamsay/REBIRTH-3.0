using System;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static INetConnection CurrentNativeServerConnection()
    {var connections=SingletonMonoBehaviour<ConnectionManager>.Instance?.GetConnectionToServer();return connections!=null&&connections.Length>0?connections[0]:null;}
    [ThreadStatic]private static Frame authenticatedNativeBeforeAck;
    internal sealed class NativeBeforePublication
    {
        private readonly Frame frame;private readonly long nativeVersion;
        internal readonly Guid Operation,Nonce;internal readonly string SourceHash;
        internal readonly World World;internal readonly TileEntityWorkstation Station;internal readonly EntityPlayerLocal Viewer;
        internal readonly XUiC_WorkstationWindowGroup Window;internal readonly long WindowSession;internal readonly INetConnection Connection;
        internal readonly RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;
        internal string LocalSavedPath=>frame.LocalBefore?.SavedPath;
        internal string LocalSavedDigest=>frame.LocalBefore?.SavedDigest;
        private NativeBeforePublication(Frame source,long session,INetConnection connection)
        {
            frame=source;var observed=source.Observation;nativeVersion=observed.NativeVersionBefore;Operation=observed.Operation;Nonce=source.RemoteNativeNonce;
            SourceHash=source.RemoteNativeSourceHash;World=observed.World;Station=observed.Station;Viewer=observed.Viewer;Window=observed.Window;
            WindowSession=session;Connection=connection;EarlierOwner=source.EarlierOwner;
        }
        internal bool Current()=>!frame.Closed&&frame.Prepared&&frame.Saved&&!frame.Observation.RanUpdate&&
            frame.NativeBefore==this&&frame.RemoteNativeNonce==Nonce&&frame.RemoteNativeSourceHash==SourceHash&&
            ReferenceEquals(frame.Observation.World,World)&&ReferenceEquals(frame.Observation.Station,Station)&&ReferenceEquals(frame.Observation.Window,Window)&&
            ReferenceEquals(frame.Observation.Viewer,Viewer)&&frame.Observation.Current()&&frame.Observation.RecipeMatches()&&
            (nativeVersion==0||RebirthRecipeOutputIntegration.IsCurrentOwningNativeVersion(frame.Observation.Card,nativeVersion))&&
            ReferenceEquals(frame.EarlierOwner,EarlierOwner)&&EarlierOwner.SourcePublished()&&EarlierOwner.Current()&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Window,out var session)&&session==WindowSession&&
            (World.IsRemote()?ReferenceEquals(CurrentNativeServerConnection(),Connection)&&Connection!=null&&!Connection.IsDisconnected():
                Connection==null&&frame.LocalBefore?.Saved==true&&frame.LocalBefore.Current());
        internal static NativeBeforePublication FromLocalSaved(Frame source)
        {
            if(source?.Observation.World.IsRemote()!=false||source.LocalBefore?.Saved!=true||!source.LocalBefore.Current())return null;
            return new NativeBeforePublication(source,source.LocalBefore.Session,null);
        }
        internal static NativeBeforePublication FromAcceptedAck(Frame source)
        {
            // Called only inside the existing authenticated ACK receiver after
            // its nonce/source/owner/creation/challenge and saved-frame checks.
            if(!ReferenceEquals(source,authenticatedNativeBeforeAck)||source?.Prepared!=true||!source.Saved||source.RemoteNativeNonce==Guid.Empty||source.Observation.RanUpdate||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(source.Observation.Window,out var session))return null;
            var connection=CurrentNativeServerConnection();
            return connection==null?null:new NativeBeforePublication(source,session,connection);
        }
    }
}
