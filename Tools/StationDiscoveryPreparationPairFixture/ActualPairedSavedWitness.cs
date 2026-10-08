using System;using System.Xml.Linq;
partial class RebirthWorldCharacterRepository{
    internal static bool HasSavedStationDiscoveryAdmission(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationDiscoveryAdmissionBinding binding)
    {
        if(!serverAuthority||identity==null||!RebirthStationDiscoveryPreparationPair.Matches(binding,admission))return false;
        var image=binding.Write();var witness=image.Element("stationDiscoveryWitness");
        if((string)witness?.Attribute("owner")!=identity.StorageKey)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var stored)||stored==null||
                    !saved.Progression.StationDiscoveryAdmissions.TryGetValue(admission.JobId,out var frozen)||frozen==null||
                    !XNode.DeepEquals(stored.Write(),admission.Write())||!XNode.DeepEquals(frozen.Write(),image)||
                    !RebirthStationDiscoveryPreparationPair.Matches(frozen,stored)||!serverAuthority||path!=GetPath(identity.StorageKey))return false;
                return true;
            }
            catch{return false;}
        }
    }
    internal static bool TryGetSavedStationPreparationIntent(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission expected,out RebirthStationSavedPreparationIntent intent)
    {
        intent=null;if(!serverAuthority||identity==null||expected==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(expected.CreationId,saved.Origin?.CreationId))return false;
                bool hasPrepared=saved.Progression.StationPreparations.TryGetValue(expected.JobId,out var prepared);
                bool hasBinding=saved.Progression.StationDiscoveryAdmissions.TryGetValue(expected.JobId,out var binding);
                RebirthStationSavedPreparationIntent result;
                if(!hasPrepared)
                {
                    if(hasBinding||expected.IsPublicationAttempted)return false;
                    result=new RebirthStationSavedPreparationIntent(RebirthStationSavedIntentKind.NewOriginal,null);
                }
                else
                {
                    if(prepared==null)return false;
                    bool exact=XNode.DeepEquals(prepared.Write(),expected.Write());
                    if(!exact&&expected.IsPublicationAttempted&&!prepared.IsPublicationAttempted&&prepared.TryMarkPublicationAttempted(out var next))
                        exact=XNode.DeepEquals(next.Write(),expected.Write());
                    if(!exact)return false;
                    if(hasBinding)
                    {
                        if(!RebirthStationDiscoveryPreparationPair.Matches(binding,prepared)||
                            !RebirthStationDiscoveryPreparationPair.Matches(binding,expected)||
                            (string)binding.Write().Element("stationDiscoveryWitness")?.Attribute("owner")!=identity.StorageKey)return false;
                        result=new RebirthStationSavedPreparationIntent(RebirthStationSavedIntentKind.Discovery,binding);
                    }
                    else result=new RebirthStationSavedPreparationIntent(RebirthStationSavedIntentKind.Ordinary,null);
                }
                if(path!=GetPath(identity.StorageKey)||!serverAuthority)return false;
                intent=result;return true;
            }
            catch{return false;}
        }
    }
}