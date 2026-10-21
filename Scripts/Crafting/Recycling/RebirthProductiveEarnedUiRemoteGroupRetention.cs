using System;

internal static partial class RebirthProductiveEarnedUiRemote
{
    private sealed class DeferredGroupLease:IDisposable
    {
        private Registration original;
        internal DeferredGroupLease(Registration source){original=source;}
        public void Dispose(){var value=original;if(value==null)return;original=null;value.GroupReferences--;}
    }
    internal static IDisposable RetainDeferredGroup(Registration original)
    {
        if(!ThreadManager.IsMainThread()||original?.Frame==null||!registrations.TryGetValue(original.Frame.Operation,out var retained)||
            !ReferenceEquals(retained,original)||!original.Current()&&original.StationPublication?.Current()!=true)return null;
        original.GroupReferences++;return new DeferredGroupLease(original);
    }
}
