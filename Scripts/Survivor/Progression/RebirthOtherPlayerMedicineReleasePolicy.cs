using System;
// Release isolation only; not an authentication or target-admission witness.
internal static class RebirthOtherPlayerMedicineReleasePolicy
{
    internal static bool Enabled => false;
    internal static bool Refuses(EntityAlive actor, EntityAlive patient, string itemName)
    {
        return !Enabled && actor is EntityPlayer && patient is EntityPlayer &&
            !ReferenceEquals(actor, patient) &&
            RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(itemName);
    }
}
