using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool TryFinishClearedNativeMetadata(Attempt original)
    {
        if(original?.ClearedNativeMarker!=true)return false;
        if(original.NativeMetadataClosed)return true;
        if(original.EarnedChild!=null)return false; // Separate operation custody cannot be discarded here.
        try
        {
            // The latch is set only after saved-terminal gates and successful
            // exact marker clear. Retry disposals only; never recheck a marker
            // that was intentionally removed or repeat a native setter.
            original.PartialSuccessor?.Dispose();original.PartialSuccessor=null;
            original.Terminal?.Dispose();original.Terminal=null;
            original.Observation?.Dispose();original.Observation=null;
            original.PendingDefinitionRetention?.Dispose();original.PendingDefinitionRetention=null;
            original.PendingRecipeBinding=original.PendingExtras=null;original.Tools=null;original.Frame=null;
            original.CompletionRetention?.TryRetain();original.CompletionRetention=null;
            RebirthProductiveEarnedSavedNativeSource.Release(original.Artifact.LoadedSource);
            RebirthProductiveEarnedSavedNativeSource.ReleaseRegisteredTerminal(original.Artifact.Birth,original.Artifact.Operation);
            original.Retention?.Dispose();original.Retention=null;
            // Remove only after every retryable resource boundary returned.
            // On failure the same latched Attempt remains strongly retained.
            retained.Remove(original.Station);original.NativeMetadataClosed=true;return true;
        }
        catch{return false;}
    }
}
