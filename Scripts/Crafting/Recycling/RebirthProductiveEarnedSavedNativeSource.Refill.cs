using System;
using System.IO;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    private static string startupRefillSave,startupRefillWorldKey;
    private static World startupRefillWorld;
    private static bool startupRefillPending;
    private static DateTime startupRefillRetry;
    private static void ConfigureStartupRefill(string save)
    {startupRefillSave=save;startupRefillWorld=null;startupRefillWorldKey=null;startupRefillPending=false;startupRefillRetry=DateTime.MinValue;}
    private static void MarkStartupRefillNeeded(){startupRefillPending=true;}
    internal static void PumpStartupRefill()
    {
        if(!ThreadManager.IsMainThread())return;
        lock(cachedLoadGate)
        {
            if(!startupRefillPending||cachedLoadRequests.Count>=4||startupRefillSave==null||DateTime.UtcNow<startupRefillRetry)return;
            try
            {
                var world=GameManager.Instance?.World;
                if(world?.IsRemote()!=false||!string.Equals(Path.GetFullPath(GameIO.GetSaveGameDir()),startupRefillSave,
                    RebirthStationCompletionOriginalScope.PathComparison))return;
                string key=RebirthProductiveEarnedArtifact.WorldIdentity(world);if(key==null)return;
                if(startupRefillWorld==null){startupRefillWorld=world;startupRefillWorldKey=key;}
                if(!ReferenceEquals(startupRefillWorld,world)||startupRefillWorldKey!=key)return;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null)return; // No file scan while an active graph owns the phase.
                    string root=Path.Combine(startupRefillSave,"RebirthProductiveEarned");
                    if(!RebirthProductiveEarnedArtifact.SafePath(root)||!Directory.Exists(root)){startupRefillPending=false;return;}
                    startupRefillPending=false;
                    // Refill obligations only. Re-adding complete history after
                    // absent-marker retirement would endlessly consume slots.
                    // Admitted candidates remain Loaded=null until a future
                    // actual region/read/factory chain qualifies them.
                    ScanStartupCandidates(startupRefillSave,root,2);
                }
            }
            catch{startupRefillPending=true;startupRefillRetry=DateTime.UtcNow.AddSeconds(5);}
        }
    }
}
