using System;
using System.Xml.Linq;
internal enum RebirthStationSavedIntentKind { NewOriginal, Ordinary, Discovery }
internal sealed class RebirthStationSavedPreparationIntent
{
    internal readonly RebirthStationSavedIntentKind Kind;
    internal readonly RebirthStationDiscoveryAdmissionBinding Binding;
    internal RebirthStationSavedPreparationIntent(RebirthStationSavedIntentKind kind,RebirthStationDiscoveryAdmissionBinding binding)
    {Kind=kind;Binding=binding?.Clone();}
    internal bool MatchesCached(RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission)
    {
        if(owner?.Progression==null||admission==null)return false;
        bool held=owner.Progression.StationDiscoveryAdmissions.TryGetValue(admission.JobId,out var live);
        if(Kind==RebirthStationSavedIntentKind.NewOriginal)return !admission.IsPublicationAttempted;
        if(Kind==RebirthStationSavedIntentKind.Ordinary)return !held;
        return held&&live!=null&&Binding!=null&&XNode.DeepEquals(live.Write(),Binding.Write())&&RebirthStationDiscoveryPreparationPair.Matches(live,admission);
    }
}