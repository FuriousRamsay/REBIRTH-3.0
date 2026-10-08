using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Original authority-thread pair staging. No native item/output effects or binding regeneration.
internal static class RebirthStationDiscoveryPreparationIntegration
{
    internal static bool RegisterOriginal(EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission,
        RebirthStationDiscoveryAdmissionBinding binding,RebirthStationDiscoveryAuthorityScope scope,Func<bool> save)
    {
        try
        {
            if(save==null||scope==null||!scope.IsCurrent()||!ReferenceEquals(scope.Owner,owner)||owner?.Progression==null||
                admission==null||admission.IsPublicationAttempted||!RebirthStationDiscoveryPreparationPair.Matches(binding,admission)||
                !RebirthStationPreparationPersistence.MatchesOwner(owner.Progression.StationPreparations,scope.CreationId))return false;
            var preparations=owner.Progression.StationPreparations;var bindings=owner.Progression.StationDiscoveryAdmissions;
            bool hadAdmission=preparations.TryGetValue(admission.JobId,out var previousAdmission);
            bool hadBinding=bindings.TryGetValue(admission.JobId,out var previousBinding);
            if(hadAdmission!=hadBinding||hadAdmission&&(previousAdmission==null||previousBinding==null||
                !XNode.DeepEquals(previousAdmission.Write(),admission.Write())||!XNode.DeepEquals(previousBinding.Write(),binding.Write())))return false;
            if(!hadAdmission&&(preparations.Count>=RebirthStationPreparationPersistence.MaximumRecords||
                preparations.Values.Any(p=>p.SharesStation(admission))))return false;
            var heldAdmission=hadAdmission?previousAdmission:admission.Clone();var heldBinding=hadBinding?previousBinding:binding.Clone();
            var checkedBindings=new Dictionary<string,RebirthStationDiscoveryAdmissionBinding>(bindings,StringComparer.Ordinal);
            checkedBindings[admission.JobId]=heldBinding;
            RebirthStationDiscoveryAdmissionPersistence.Write(checkedBindings,scope.OwnerKey,scope.CreationId);
            if(!scope.IsCurrent()||!RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission)||!scope.IsCurrent())return false;
            // Concrete Ordinal string dictionaries; no external callback between these original additions.
            if(!hadAdmission)
            {
                preparations.Add(admission.JobId,heldAdmission);
                try{bindings.Add(admission.JobId,heldBinding);}
                catch{preparations.Remove(admission.JobId);throw;} // no callback/save occurred: local staging rollback only
            }
            Func<bool> intact=()=>scope.IsCurrent()&&ReferenceEquals(owner.Progression.StationPreparations,preparations)&&ReferenceEquals(owner.Progression.StationDiscoveryAdmissions,bindings)&&preparations.TryGetValue(admission.JobId,out var a)&&ReferenceEquals(a,heldAdmission)&&
                bindings.TryGetValue(admission.JobId,out var b)&&ReferenceEquals(b,heldBinding)&&RebirthStationDiscoveryPreparationPair.Matches(b,a);
            // Once the save callback can observe the pair, uncertainty retains BOTH originals.
            if(!intact())return false;bool saved=save();return saved&&intact();
        }
        catch{return false;}
    }
    internal static bool IsAllowedIntent(EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission,Recipe source)
    {
        if(owner?.Progression==null||admission==null||source==null)return false;
        if(owner.Progression.StationDiscoveryAdmissions.TryGetValue(admission.JobId,out var binding))
            return RebirthStationDiscoveryPreparationPair.TryValidateLive(player,admission,binding,out var scope)&&scope.IsCurrent()&&ReferenceEquals(scope.Owner,owner);
        return source.IsUnlocked(player);
    }
    internal static bool HasSavedIntent(RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission)
        =>owner?.Progression!=null&&admission!=null&&(!owner.Progression.StationDiscoveryAdmissions.TryGetValue(admission.JobId,out var binding)||
            RebirthWorldCharacterRepository.HasSavedStationDiscoveryAdmission(identity,admission,binding));
}