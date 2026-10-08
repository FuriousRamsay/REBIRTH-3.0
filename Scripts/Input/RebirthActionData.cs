public static class RebirthActionData
{
    public static readonly PlayerActionData.ActionTab TabRebirth =
        new PlayerActionData.ActionTab("inpTabRebirth", 35);

    public static readonly PlayerActionData.ActionGroup GroupVehicles =
        new PlayerActionData.ActionGroup("inpGrpRebirthVehiclesName", "inpGrpRebirthVehiclesDesc", 10, TabRebirth);

    public static readonly PlayerActionData.ActionGroup GroupGeneral =
        new PlayerActionData.ActionGroup("inpGrpRebirthGeneralName", "inpGrpRebirthGeneralDesc", 20, TabRebirth);

    public static readonly PlayerActionData.ActionGroup GroupBuilding =
        new PlayerActionData.ActionGroup("inpGrpRebirthBuildingName", "inpGrpRebirthBuildingDesc", 30, TabRebirth);

    public static readonly PlayerActionData.ActionGroup GroupCompanions =
        new PlayerActionData.ActionGroup("inpGrpRebirthCompanionsName", "inpGrpRebirthCompanionsDesc", 40, TabRebirth);

    public static readonly PlayerActionData.ActionGroup GroupLoadouts =
        new PlayerActionData.ActionGroup("inpGrpRebirthLoadoutsName", "inpGrpRebirthLoadoutsDesc", 50, TabRebirth);

    public static readonly PlayerActionData.ActionGroup GroupAudio =
        new PlayerActionData.ActionGroup("inpGrpRebirthAudioName", "inpGrpRebirthAudioDesc", 60, TabRebirth);

    public static readonly PlayerActionData.ActionGroup GroupClasses =
        new PlayerActionData.ActionGroup("inpGrpRebirthClassesName", "inpGrpRebirthClassesDesc", 70, TabRebirth);
}
