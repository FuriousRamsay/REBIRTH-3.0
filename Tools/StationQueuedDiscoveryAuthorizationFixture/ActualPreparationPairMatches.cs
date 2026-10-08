using System.Xml.Linq; static class RebirthStationDiscoveryPreparationPair{internal static bool Matches(RebirthStationDiscoveryAdmissionBinding binding,RebirthStationGridAdmission current)
    {
        try
        {
            if(binding==null||current==null||binding.JobId!=current.JobId||
                !RebirthStationDiscoveryAdmissionBinding.TryReadStored(binding.Write(),out _)||
                !RebirthStationGridAdmission.TryReadStored(binding.Write().Element("stationAdmission"),out var original)||original.IsPublicationAttempted)return false;
            if(current.IsPublicationAttempted)
            {if(!original.TryMarkPublicationAttempted(out var attempted))return false;return XNode.DeepEquals(attempted.Write(),current.Write());}
            return XNode.DeepEquals(original.Write(),current.Write());
        }
        catch{return false;}
    }}