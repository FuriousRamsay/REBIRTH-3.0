using System;
using System.Collections.Generic;

// Work-only synchronous observation. Grants no lease, payment or native effect.
internal static class RebirthNpcWorkCallbackObservation
{
    private static readonly HashSet<Frame> Active=new HashSet<Frame>();
    internal static bool IsIdle=>ThreadManager.IsMainThread()&&Active.Count==0;
    internal static IDisposable Enter()
    {
        var world=GameManager.Instance?.World;
        if(!RebirthNpcWorkReleaseGate.Enabled||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||Active.Count>=64)throw new InvalidOperationException("Work callback original scope unavailable");
        foreach(var frame in Active)if(!ReferenceEquals(frame.World,world))throw new InvalidOperationException("Original Work callback remains in another native world");
        var next=new Frame(world);Active.Add(next);return next;
    }
    internal static IDisposable Enter(RebirthNpcWorkAssignmentRequest request)
    {
        if(!RebirthNpcQualifiedWorkGate.Allows(request))throw new InvalidOperationException("Unqualified literature callback.");
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||Active.Count>=64)
            throw new InvalidOperationException("Literature callback original scope unavailable");
        foreach(var frame in Active)if(!ReferenceEquals(frame.World,world))throw new InvalidOperationException("Original callback remains in another native world");
        var next=new Frame(world);Active.Add(next);return next;
    }
    internal static IDisposable Enter(RebirthNpcWorkAssignment assignment)
    {
        if(!RebirthNpcQualifiedWorkGate.Allows(assignment))throw new InvalidOperationException("Unqualified literature outcome callback.");
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||Active.Count>=64)
            throw new InvalidOperationException("Literature outcome callback original scope unavailable");
        foreach(var frame in Active)if(!ReferenceEquals(frame.World,world))throw new InvalidOperationException("Original callback remains in another native world");
        var next=new Frame(world);Active.Add(next);return next;
    }
    private sealed class Frame:IDisposable
    {
        internal readonly World World;private bool disposed;
        internal Frame(World world){World=world;}
        public void Dispose(){if(disposed)return;if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Original Work callback observation requires original main thread");disposed=true;Active.Remove(this);}
    }
}
