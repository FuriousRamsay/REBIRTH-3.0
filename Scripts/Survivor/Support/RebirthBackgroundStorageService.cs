using System;
using System.Globalization;

// Storage benefits are tuning on the existing signature, preserving its other effects.
public static class RebirthBackgroundStorageService
{
    public static int Bonus(string backgroundId, string key)
    {
        RebirthBackgroundBonusDefinition definition;
        RebirthBackgroundBonusTuningValue tuning;
        int value;
        return RebirthBackgroundBonusRegistry.TryGetByBackground(backgroundId, out definition)
            && definition.TryGetTuning(key, out tuning)
            && int.TryParse(tuning.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            ? Math.Max(0, Math.Min(key == "backpack_slot_bonus" ? 13 : 12, value)) : 0;
    }

    public static int ToolbeltBonus(EntityPlayer player)
    {
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return 0;
        if (player.world != null && player.world.IsRemote())
        {
            RebirthSurvivorOwnerScalars state;
            return RebirthSurvivorClientState.TryGetOwnerScalars(player, out state)
                ? Bonus(state.BackgroundId, "toolbelt_slot_bonus") : 0;
        }
        RebirthWorldCharacterRecord record;
        return RebirthWorldCharacterService.TryGet(player, out record) && record != null && record.IsComplete && record.Origin != null
            ? Bonus(record.Origin.BackgroundId, "toolbelt_slot_bonus") : 0;
    }

    public static int BackpackBonus(RebirthWorldCharacterRecord record)
    {
        return record != null && record.IsComplete && record.Origin != null
            ? Bonus(record.Origin.BackgroundId, "backpack_slot_bonus") : 0;
    }
}
