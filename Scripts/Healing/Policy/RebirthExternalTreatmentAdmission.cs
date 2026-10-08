using System;

/// <summary>Rechecks the cached native other-player treatment at consumption time.</summary>
public static class RebirthExternalTreatmentAdmission
{
    // Mirror the two native release exits that occur before any healing event or consumption.
    // The original still runs its cooldown/sound/refusal; this only prevents evidence attribution.
    internal static bool HasNativeEarlyRefusal(ItemActionUseOther action, ItemActionData data, EntityAlive patient)
    {
        EntityAlive actor = data?.invData?.holdingEntity;
        if (action == null || actor?.inventory == null || data.invData.item == null || patient == null) return true;
        return patient.HasAnyTags(action.noMedBuffsTag) ||
            EffectManager.GetValue(PassiveEffects.DisableItem, actor.inventory.holdingItemItemValue,
                0f, actor, null, data.invData.item.ItemTags) > 0f;
    }
    public static bool Allows(ItemActionUseOther action, ItemActionData data, EntityAlive patient)
    {
        EntityAlive actor = data?.invData?.holdingEntity;
        string item = data?.invData?.itemValue?.ItemClass?.GetItemName();
        if (!RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(item)) return true;
        if (!RebirthExternalTreatmentTargetPolicy.Allows(actor != null && !actor.IsDead(),
            patient != null && !patient.IsDead(), patient is EntityPlayer,
            actor?.world != null && ReferenceEquals(actor.world, patient?.world),
            ReferenceEquals(actor, patient), actor == null || patient == null ? float.NaN :
                UnityEngine.Vector3.SqrMagnitude(actor.position - patient.position))) return false;
        if (!(actor is EntityPlayer) || patient.Buffs == null || actor.MinEventContext == null ||
            !ReferenceEquals(actor.world.GetEntity(actor.entityId), actor) ||
            !ReferenceEquals(actor.world.GetEntity(patient.entityId), patient)) return false;

        // Target-dependent native requirements must see the same patient that will receive effects.
        // Do not raycast again or change the native cooldown/consumption lifecycle.
        actor.MinEventContext.Other = patient;
        actor.MinEventContext.ItemValue = data.invData.itemValue;
        if (action?.ExecutionRequirements != null && !action.ExecutionRequirements.IsValid(actor.MinEventContext))
            return false;

        // Only these authored treatments add an HP reserve. A cast or sewing kit must not be
        // blocked merely because an unrelated reserve is active on the patient.
        bool addsReserve = item == "medicalAloeCream" || item == "medicalFirstAidBandage" ||
            item == "medicalFirstAidKit";
        return !addsReserve || RebirthHealingOverlapPolicy.Evaluate(patient,
            item != "medicalAloeCream").Allowed;
    }
}
