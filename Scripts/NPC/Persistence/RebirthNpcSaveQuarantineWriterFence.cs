using System;

public static partial class RebirthNpcPersistenceFile
{
    private static QuarantineWriterFence quarantineWriterFence;
    internal static bool HasQuarantineWriterFence=>System.Threading.Volatile.Read(ref quarantineWriterFence)!=null;
    internal sealed class QuarantineWriterFence:IDisposable
    {
        internal readonly RebirthNpcSaveQuarantineScope Scope;
        private readonly World admissionWorld;
        private bool disposed;
        internal QuarantineWriterFence(RebirthNpcSaveQuarantineScope scope){Scope=scope;admissionWorld=GameManager.Instance.World;}
        internal bool IsCurrent=>!disposed&&ThreadManager.IsMainThread()&&ReferenceEquals(quarantineWriterFence,this)&&ReferenceEquals(admissionWorld,GameManager.Instance?.World)&&Scope.IsUnchangedOriginal();
        public void Dispose()
        {lock(FailureSync){if(disposed)return;disposed=true;if(ReferenceEquals(quarantineWriterFence,this))quarantineWriterFence=null;}}
    }
    internal static bool TryFenceQuarantine(RebirthNpcSaveQuarantineScope scope,out QuarantineWriterFence fence,out string error)
    {
        fence=null;error=string.Empty;
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||scope==null||!scope.IsUnchangedOriginal())
        {error="Typed original quarantine requires authoritative thread and unchanged captured scope";return false;}
        return TryFenceOriginalScope(scope,out fence,out error);
    }
    // Outgoing authority was positively captured while that server World was
    // current. Incoming client role cannot confer authority over those stores;
    // this permit only freezes their exact unchanged original for detachment.
    internal static bool TryFenceCapturedOutgoingServer(RebirthNpcSaveQuarantineCapture.Snapshot capture,World incoming,out QuarantineWriterFence fence,out string error)
    {
        fence=null;error="Captured outgoing server quarantine unavailable";
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||capture?.Scope?.World?.IsRemote()!=false||incoming==null||
            !ReferenceEquals(incoming,GameManager.Instance?.World)||ReferenceEquals(capture.Scope.World,incoming)||manager==null||
            manager.IsServer==incoming.IsRemote()||!capture.IsUnchangedOriginal(out error))return false;
        return TryFenceOriginalScope(capture.Scope,out fence,out error);
    }
    private static bool TryFenceOriginalScope(RebirthNpcSaveQuarantineScope scope,out QuarantineWriterFence fence,out string error)
    {
        fence=null;error=string.Empty;
        lock(FailureSync)
        {
            if(quarantineWriterFence!=null||Writers.Count!=0){error="NPC publication writer or quarantine already active";return false;}
            // Existing recovery fences remain owned by the retained Session.
            // This transaction neither disposes nor steals those original handles.
            var next=new QuarantineWriterFence(scope);quarantineWriterFence=next;fence=next;return true;
        }
    }
}
