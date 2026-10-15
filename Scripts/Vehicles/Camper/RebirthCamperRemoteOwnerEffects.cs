using System;

// Explicit production registration; startup invokes this only after the full
// camper integration is installed. Payload interpreters retain their own native
// owner/creation/receipt checks before applying any reward, refund or debit.
internal static class RebirthCamperRemoteOwnerEffects
{
    internal static void Install()
    {
        if (RebirthCamperRemoteOwnerApplication.IsRegistered) return;
        RebirthCamperRemoteOwnerApplication.RegisterOwnerApplication(
            RebirthCamperCharacterAccess.CurrentCreation, Apply);
    }

    private static bool Apply(EntityPlayerLocal player, Guid operation,
        RebirthCamperRemoteOwnerEffectKind kind, byte[] payload)
    {
        switch (kind)
        {
            case RebirthCamperRemoteOwnerEffectKind.Reward:
                return RebirthCamperStationRemoteRewardPayload.TryApplyReward(player, operation, payload);
            case RebirthCamperRemoteOwnerEffectKind.Refund:
                return RebirthCamperStationRemoteRefundPayload.TryApplyRefund(player, operation, payload);
            case RebirthCamperRemoteOwnerEffectKind.Debit:
                return RebirthCamperStationPlayerDebitPayload.TryApply(player, operation, payload);
            case RebirthCamperRemoteOwnerEffectKind.EnergyTransfer:
                return RebirthCamperEnergyOwnerPayload.TryApply(player, operation, payload);
            default:
                return false;
        }
    }
}
