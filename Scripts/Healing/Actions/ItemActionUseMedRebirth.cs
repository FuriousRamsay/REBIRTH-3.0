#nullable disable

/// <summary>
/// Direct medical-use gate for treatments that add medical regeneration.
/// The policy runs before ItemActionEat, so denied attempts do not consume the
/// item or apply any of its normal effects.
/// </summary>
public class ItemActionUseMedRebirth : ItemActionEat
{
    protected virtual bool TreatmentStopsBleeding { get { return false; } }

    internal bool CanUseTreatment(EntityAlive patient)
    {
        return patient != null && patient.Buffs != null &&
            RebirthHealingOverlapPolicy.Evaluate(patient, TreatmentStopsBleeding).Allowed;
    }

    public override bool ExecuteInstantAction(EntityAlive ent, ItemStack stack, bool isHeldItem, XUiC_ItemStack stackController)
    {
        if (!CanUseTreatment(ent)) return false;
        // The ItemActionEat instant-action completion patch owns outcome scaling and practice.
        // Calling a second reserve adjustment here would scale the same new reserve twice.
        bool result = base.ExecuteInstantAction(ent, stack, isHeldItem, stackController);
        return result;
    }

    public override void ExecuteAction(ItemActionData actionData, bool released)
    {
        EntityAlive patient = actionData != null && actionData.invData != null
            ? actionData.invData.holdingEntity
            : null;

        RebirthHealingOverlapDecision decision =
            RebirthHealingOverlapPolicy.Evaluate(patient, TreatmentStopsBleeding);

        if (decision.Allowed)
        {
            base.ExecuteAction(actionData, released);
            return;
        }

        // Only the local player owns presentation. The policy itself remains
        // deterministic and is also evaluated wherever the normal item action runs.
        EntityPlayerLocal localPlayer = patient as EntityPlayerLocal;
        if (localPlayer != null && released)
        {
            GameManager.ShowTooltip(
                localPlayer,
                Localization.Get("ttRebirthHealingAlreadyActive"),
                string.Empty,
                "ui_denied",
                null);
        }
    }
}

/// <summary>
/// Medical treatment that may bypass overlap prevention only when it is able
/// to address an active bleeding emergency.
/// </summary>
public sealed class ItemActionUseMedRebirthBleeding : ItemActionUseMedRebirth
{
    protected override bool TreatmentStopsBleeding { get { return true; } }
}
