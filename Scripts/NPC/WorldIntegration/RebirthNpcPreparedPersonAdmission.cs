using System;

#nullable disable

// Saved-person eligibility only; not permission to reconstruct controllers or enable native AI.
internal static class RebirthNpcPreparedPersonAdmission
{
    internal static bool TryValidate(RebirthNpcPersistentRecord person,out string profile,out string reason,bool observedLive=false)
    {
        profile=null;reason="Compatible living persistent humanoid record required.";
        if(person?.Identity==null||person.Identity.StableNpcId.IsEmpty||person.Profile==null||person.Lifecycle==null||
            person.Lifecycle.TombstoneState||!string.Equals(person.Lifecycle.LifecycleDomain,"persistent",StringComparison.OrdinalIgnoreCase)||
            !string.IsNullOrEmpty(person.Lifecycle.RemovalReason)||
            (!string.IsNullOrEmpty(person.Lifecycle.DismissalState)&&person.Lifecycle.DismissalState!="active")||
            person.Presence==null||person.Vitals==null||person.Vitals.CurrentHealth<=0||
            !string.Equals(person.Vitals.DeathOrIncapacitationState,"alive",StringComparison.OrdinalIgnoreCase)||
            !(string.Equals(person.Presence.PresenceState,"UnloadedPersistent",StringComparison.OrdinalIgnoreCase)||
                observedLive&&string.Equals(person.Presence.PresenceState,"Active",StringComparison.OrdinalIgnoreCase)))return false;
        if(!RebirthNpcProductionProfileCatalogue.TryGet(person.Profile.ProfileId,out var production)||!production.Persistent||
            !(production.Category==RebirthNpcCategory.Survivor||production.Category==RebirthNpcCategory.Bandit||production.Category==RebirthNpcCategory.SpecialHumanoid)||
            !string.Equals(person.Identity.Category,production.Category.ToString(),StringComparison.OrdinalIgnoreCase)||
            !string.Equals(person.Identity.Species,"human",StringComparison.OrdinalIgnoreCase))return false;
        profile=production.ProfileId;reason=string.Empty;return true;
    }
}