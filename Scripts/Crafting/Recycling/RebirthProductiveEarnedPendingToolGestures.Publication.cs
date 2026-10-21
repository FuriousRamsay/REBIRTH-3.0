using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedPendingToolGestures
{
    internal sealed class SavedToolPublication:IDisposable
    {
        internal Observation Scope;internal NativeToolSync Sync;internal byte[] Record;
        internal RebirthProductiveEarnedObservationBudget Memory;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal IDisposable Retention;internal bool Published;
        internal bool Current()=>Scope?.ReturnedContext()==true&&Scope.Source.CurrentContext()&&ReferenceEquals(Scope.StationSync,Sync)&&Sync.Current()&&
            Scope.OwnerAfter?.Saved(Scope)==true&&Scope.Steps==Sync.Steps&&Scope.Journal!=null;
        public void Dispose(){var watch=Watch;Watch=null;var memory=Memory;Memory=null;var retained=Retention;Retention=null;Scope=null;Sync=null;Record=null;try{watch?.Dispose();}finally{try{memory?.Dispose();}finally{retained?.Dispose();}}}
    }
    private static void PumpToolPublications()
    {
        foreach(var scope in observations.Values.ToArray())
        {
            try
            {
                if(scope.StationSync?.Current()!=true||scope.OwnerAfter?.Saved(scope)!=true||!scope.ReturnedContext())continue;
                scope.ToolPublication??=new SavedToolPublication{Scope=scope,Sync=scope.StationSync};
                var publication=scope.ToolPublication;
                if(!publication.Current()||!scope.Source.TryPublishToolRebase(publication))continue;
                scope.Dispose(); // Saved rebase owns its new native body/dependencies.
            }
            catch{/* Only actual owner/native saved proof can finish this original transfer. */}
        }
    }
}
