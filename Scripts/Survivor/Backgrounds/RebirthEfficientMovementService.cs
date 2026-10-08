/// <summary>Personal Trainer discount applies to current movement, never net attack/mining Stamina spend.</summary>
public static class RebirthEfficientMovementService
{
    private static readonly FastTags<TagGroup.Global> Running = FastTags<TagGroup.Global>.Parse("running");
    private static readonly FastTags<TagGroup.Global> Jumping = FastTags<TagGroup.Global>.Parse("jumping");
    private static readonly FastTags<TagGroup.Global> Swimming = FastTags<TagGroup.Global>.Parse("swimming");
    private static readonly FastTags<TagGroup.Global> SwimmingRun = FastTags<TagGroup.Global>.Parse("swimmingRun");
    public static float Apply(EntityPlayer player, float activityEnergy)
    {
        if (player == null || activityEnergy <= 0f || !RebirthSurvivorMode.IsEnabledForCurrentWorld() ||
            !RebirthBackgroundBonusService.HasBonus(player, "background_bonus.efficient_movement")) return activityEnergy;
        var tag = player.CurrentMovementTag;
        float movementEnergy;
        if (tag.Test_AnySet(SwimmingRun)) movementEnergy = RebirthMetabolismConfig.EnergyExtremeUsePerRealMinute;
        else if (tag.Test_AnySet(Running)) movementEnergy = RebirthMetabolismConfig.EnergyHighUsePerRealMinute;
        else if (tag.Test_AnySet(Swimming)) movementEnergy = RebirthMetabolismConfig.EnergyHighUsePerRealMinute;
        else if (tag.Test_AnySet(Jumping)) movementEnergy = RebirthMetabolismConfig.EnergyModerateUsePerRealMinute;
        else return activityEnergy;
        float multiplier = UnityEngine.Mathf.Clamp(RebirthResourceSignatureService.GetTuning(
            "background_bonus.efficient_movement", "movement_energy_multiplier", 0.85f), 0f, 1f);
        return activityEnergy - UnityEngine.Mathf.Min(activityEnergy, movementEnergy) * (1f - multiplier);
    }
}