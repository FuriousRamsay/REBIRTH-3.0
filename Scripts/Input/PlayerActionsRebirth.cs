using System;
using InControl;
using Platform;

public sealed class PlayerActionsRebirth : PlayerActionsBase
{
    public const string ActionSetName = "rebirth";

    public PlayerAction ToggleCruiseControl { get; private set; }
    public PlayerAction QuickStack { get; private set; }
    public PlayerAction CompanionTeleport { get; private set; }
    public PlayerAction ToggleCompanionCardStyle { get; private set; }
    public PlayerAction CopyShapeRotation { get; private set; }
    public PlayerAction OreSense { get; private set; }
    public PlayerAction MinimapVisibility { get; private set; }
    public PlayerAction MinimapZoomIn { get; private set; }
    public PlayerAction MinimapZoomOut { get; private set; }
    public PlayerAction MusicPlayPause { get; private set; }
    public PlayerAction MusicStop { get; private set; }
    public PlayerAction MusicNext { get; private set; }
    public PlayerAction MusicVolumeDown { get; private set; }
    public PlayerAction MusicVolumeUp { get; private set; }

    public PlayerActionsRebirth(PlayerInputManager inputManager)
    {
        Name = ActionSetName;
        Enabled = true;

        PlayerActionsLocal localActions = inputManager.PrimaryPlayer;
        PlayerActionsVehicle vehicleActions = inputManager.PrimaryPlayer.VehicleActions;
        PlayerActionsPermanent permanentActions = inputManager.PrimaryPlayer.PermanentActions;

        UserData = new PlayerActionData.ActionSetUserData(
            new PlayerActionsBase[] { localActions, vehicleActions, permanentActions });

        AddBindingConflict(localActions, this);
        AddBindingConflict(vehicleActions, this);
        AddBindingConflict(permanentActions, this);
    }

    private static void AddBindingConflict(PlayerActionsBase actionSet, PlayerActionsBase conflict)
    {
        if (actionSet == null || conflict == null)
            return;

        PlayerActionData.ActionSetUserData data = actionSet.UserData as PlayerActionData.ActionSetUserData;
        PlayerActionsBase[] existing = data != null && data.bindingsConflictWithSet != null
            ? data.bindingsConflictWithSet
            : Array.Empty<PlayerActionsBase>();

        if (Array.IndexOf(existing, conflict) >= 0)
            return;

        PlayerActionsBase[] merged = new PlayerActionsBase[existing.Length + 1];
        Array.Copy(existing, merged, existing.Length);
        merged[existing.Length] = conflict;
        actionSet.UserData = new PlayerActionData.ActionSetUserData(merged);
    }

    public override void CreateActions()
    {
        MusicVolumeDown = CreatePlayerAction("MusicVolumeDown");
        MusicVolumeDown.UserData = new PlayerActionData.ActionUserData("inpActRebirthMusicVolumeDownName", "inpActRebirthMusicVolumeDownDesc", RebirthActionData.GroupGeneral, PlayerActionData.EAppliesToInputType.Both, true);
        ControllerRebindableActions.Add(MusicVolumeDown);
        MusicVolumeUp = CreatePlayerAction("MusicVolumeUp");
        MusicVolumeUp.UserData = new PlayerActionData.ActionUserData("inpActRebirthMusicVolumeUpName", "inpActRebirthMusicVolumeUpDesc", RebirthActionData.GroupGeneral, PlayerActionData.EAppliesToInputType.Both, true);
        ControllerRebindableActions.Add(MusicVolumeUp);
        MusicPlayPause = CreatePlayerAction("MusicPlayPause");
        MusicPlayPause.UserData = new PlayerActionData.ActionUserData("inpActRebirthMusicPlayPauseName", "inpActRebirthMusicPlayPauseDesc", RebirthActionData.GroupGeneral, PlayerActionData.EAppliesToInputType.Both, true);
        ControllerRebindableActions.Add(MusicPlayPause);
        MusicStop = CreatePlayerAction("MusicStop");
        MusicStop.UserData = new PlayerActionData.ActionUserData("inpActRebirthMusicStopName", "inpActRebirthMusicStopDesc", RebirthActionData.GroupGeneral, PlayerActionData.EAppliesToInputType.Both, true);
        ControllerRebindableActions.Add(MusicStop);
        MusicNext = CreatePlayerAction("MusicNext");
        MusicNext.UserData = new PlayerActionData.ActionUserData("inpActRebirthMusicNextName", "inpActRebirthMusicNextDesc", RebirthActionData.GroupGeneral, PlayerActionData.EAppliesToInputType.Both, true);
        ControllerRebindableActions.Add(MusicNext);
        ToggleCruiseControl = CreatePlayerAction("ToggleCruiseControl");
        ToggleCruiseControl.UserData = new PlayerActionData.ActionUserData(
            "inpActRebirthToggleCruiseControlName",
            "inpActRebirthToggleCruiseControlDesc",
            RebirthActionData.GroupVehicles,
            PlayerActionData.EAppliesToInputType.Both,
            true);
        ControllerRebindableActions.Add(ToggleCruiseControl);

        QuickStack = CreatePlayerAction("QuickStack");
        QuickStack.UserData = new PlayerActionData.ActionUserData(
            "inpActRebirthQuickStackName",
            "inpActRebirthQuickStackDesc",
            RebirthActionData.GroupGeneral,
            PlayerActionData.EAppliesToInputType.Both,
            true);
        ControllerRebindableActions.Add(QuickStack);

        CompanionTeleport = CreatePlayerAction("CompanionTeleport");
        CompanionTeleport.UserData = new PlayerActionData.ActionUserData(
            "inpActRebirthCompanionTeleportName",
            "inpActRebirthCompanionTeleportDesc",
            RebirthActionData.GroupCompanions,
            PlayerActionData.EAppliesToInputType.Both,
            true);
        ControllerRebindableActions.Add(CompanionTeleport);

        ToggleCompanionCardStyle = CreatePlayerAction("ToggleCompanionCardStyle");
        ToggleCompanionCardStyle.UserData = new PlayerActionData.ActionUserData(
            "inpActRebirthToggleCompanionCardStyleName",
            "inpActRebirthToggleCompanionCardStyleDesc",
            RebirthActionData.GroupCompanions,
            PlayerActionData.EAppliesToInputType.Both,
            true);
        ControllerRebindableActions.Add(ToggleCompanionCardStyle);

        CopyShapeRotation = CreatePlayerAction("CopyShapeRotation");
        CopyShapeRotation.UserData = new PlayerActionData.ActionUserData(
            "inpActRebirthCopyShapeRotationName",
            "inpActRebirthCopyShapeRotationDesc",
            RebirthActionData.GroupBuilding,
            PlayerActionData.EAppliesToInputType.Both,
            true);
        ControllerRebindableActions.Add(CopyShapeRotation);

        MinimapVisibility = CreatePlayerAction("MinimapVisibility");
        MinimapVisibility.UserData = new PlayerActionData.ActionUserData("inpActRebirthMinimapVisibilityName", "inpActRebirthMinimapVisibilityDesc", RebirthActionData.GroupGeneral, PlayerActionData.EAppliesToInputType.Both, true);
        ControllerRebindableActions.Add(MinimapVisibility);
        MinimapZoomIn = CreatePlayerAction("MinimapZoomIn");
        MinimapZoomIn.UserData = new PlayerActionData.ActionUserData("inpActRebirthMinimapZoomInName", "inpActRebirthMinimapZoomInDesc", RebirthActionData.GroupGeneral, PlayerActionData.EAppliesToInputType.Both, true);
        MinimapZoomOut = CreatePlayerAction("MinimapZoomOut");
        MinimapZoomOut.UserData = new PlayerActionData.ActionUserData("inpActRebirthMinimapZoomOutName", "inpActRebirthMinimapZoomOutDesc", RebirthActionData.GroupGeneral, PlayerActionData.EAppliesToInputType.Both, true);
        ControllerRebindableActions.Add(MinimapZoomIn);
        ControllerRebindableActions.Add(MinimapZoomOut);
        OreSense = CreatePlayerAction("OreSense");
        OreSense.UserData = new PlayerActionData.ActionUserData(
            "inpActRebirthOreSenseName",
            "inpActRebirthOreSenseDesc",
            RebirthActionData.GroupGeneral,
            PlayerActionData.EAppliesToInputType.Both,
            true);
        ControllerRebindableActions.Add(OreSense);
    }

    public override void CreateDefaultKeyboardBindings()
    {
        MusicPlayPause.AddDefaultBinding(new[] { Key.Backspace });
        MusicNext.AddDefaultBinding(new[] { Key.Return });
        MusicVolumeDown.AddDefaultBinding(new[] { Key.Minus });
        MusicVolumeUp.AddDefaultBinding(new[] { Key.Equals });
        // Cruise and Recall intentionally share Q because their gameplay
        // contexts are mutually exclusive: cruise dispatches only while driving and
        // Recall dispatches only on foot. Quick Stack uses Shift+Q.
        ToggleCruiseControl.AddDefaultBinding(new[] { Key.Q });
        QuickStack.AddDefaultBinding(new[] { Key.Shift, Key.Q });
        CompanionTeleport.AddDefaultBinding(new[] { Key.Q });
        ToggleCompanionCardStyle.AddDefaultBinding(new[] { Key.Shift, Key.L });
        CopyShapeRotation.AddDefaultBinding(new[] { Key.F });
        MinimapVisibility.AddDefaultBinding(new[] { Key.PadEnter });
        MinimapZoomIn.AddDefaultBinding(new[] { Key.PadPlus });
        MinimapZoomOut.AddDefaultBinding(new[] { Key.PadMinus });
        OreSense.AddDefaultBinding(new[] { Key.Shift, Key.O });
    }

    public override void CreateDefaultJoystickBindings()
    {
        // Intentionally unbound by default. All REBIRTH actions remain listed and fully
        // rebindable on the stock controller Controls screen.
    }
}
