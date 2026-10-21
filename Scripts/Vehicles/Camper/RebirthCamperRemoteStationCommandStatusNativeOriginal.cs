using System;
// Detached existing native original observation; not a retry capability.
internal sealed class RebirthCamperRemoteStationCommandStatusNativeOriginal
{
    internal readonly RebirthCamperTransferJournal.Phase Phase;
    internal readonly RebirthCamperStationState Before,After;
    internal readonly string Proof;
    internal readonly int OriginalDeaths;
    internal RebirthCamperRemoteStationCommandStatusNativeOriginal(RebirthCamperTransferJournal.Phase phase,RebirthCamperStationState before,RebirthCamperStationState after,string proof,int deaths)
    {Phase=phase;Before=before;After=after;Proof=proof;OriginalDeaths=deaths;}
}
