using Platform;
using System;
using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// REBIRTH presentation/selection layer for the native Players list.
/// Native XUiC_PlayersList remains responsible for population, party/allies actions,
/// voice/text block toggles, map tracking, reporting, paging, and refresh cadence.
/// </summary>
[Preserve]
public sealed partial class XUiC_RebirthPlayersList : XUiC_PlayersList
{
    private XUiC_RebirthPlayersListEntry[] rebirthEntries = Array.Empty<XUiC_RebirthPlayersListEntry>();
    private PlatformUserIdentifierAbs selectedPlayerId;
    private PersistentPlayerData selectedPlayerData;
    private float selectionRefreshTimer;

    public event Action SelectionChanged;

    public PersistentPlayerData SelectedPlayerData
    {
        get
        {
            if (selectedPlayerId != null && GameManager.Instance != null && GameManager.Instance.persistentPlayers != null)
            {
                PersistentPlayerData current = GameManager.Instance.persistentPlayers.GetPlayerData(selectedPlayerId);
                if (current != null)
                    selectedPlayerData = current;
            }
            return selectedPlayerData;
        }
    }

    public override void Init()
    {
        base.Init();
        playerScroll=GetChildById("rebirthRosterScroll") as XUiC_RebirthCharacterOverviewList;
        playerScroll.DataRangeChanged+=RefreshScrollablePlayers;
        RebirthHarmonyBootstrap.PatchClassOnce(new HarmonyLib.Harmony("rebirth.players.scroll"),typeof(RebirthPlayersPopulate));
        rebirthEntries = GetChildrenByType<XUiC_RebirthPlayersListEntry>() ?? Array.Empty<XUiC_RebirthPlayersListEntry>();

        for (int i = 0; i < rebirthEntries.Length; ++i)
        {
            XUiC_RebirthPlayersListEntry entry = rebirthEntries[i];
            if (entry == null)
                continue;

            XUiC_RebirthPlayersListEntry captured = entry;
            entry.OnPress += delegate(XUiController _sender, int _mouseButton)
            {
                SelectEntry(captured);
            };
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        RebirthPartyIdentityClientState.Changed -= OnGroupIdentityChanged;
        RebirthPartyIdentityClientState.Changed += OnGroupIdentityChanged;
        EntityPlayerLocal local = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (local != null) RebirthPartyIdentityService.RequestSnapshot(local, true, "players-open");
        RebindSelection(selectFirstIfMissing: true);
    }

    public override void OnClose()
    {
        RebirthPartyIdentityClientState.Changed -= OnGroupIdentityChanged;
        base.OnClose();
    }

    private void OnGroupIdentityChanged()
    {
        RefreshGroupIdentityRows();
        SelectionChanged?.Invoke();
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        selectionRefreshTimer -= _dt;
        if (selectionRefreshTimer > 0f)
            return;

        selectionRefreshTimer = 0.2f;
        RebindSelection(selectFirstIfMissing: selectedPlayerId == null);
    }

    public void SelectEntry(XUiC_RebirthPlayersListEntry entry)
    {
        if (entry == null || entry.PlayerData == null)
            return;

        selectedPlayerId = entry.PlayerData.PrimaryId;
        selectedPlayerData = entry.PlayerData;
        ApplySelectionVisuals();
        SelectionChanged?.Invoke();
    }

    public XUiC_RebirthPlayersListEntry GetVisibleSelectedEntry()
    {
        if (selectedPlayerId == null)
            return null;

        for (int i = 0; i < rebirthEntries.Length; ++i)
        {
            XUiC_RebirthPlayersListEntry entry = rebirthEntries[i];
            if (entry == null || entry.PlayerData == null || entry.ViewComponent == null || !entry.ViewComponent.IsVisible)
                continue;
            if (object.Equals(entry.PlayerData.PrimaryId, selectedPlayerId))
                return entry;
        }
        return null;
    }

    private void RebindSelection(bool selectFirstIfMissing)
    {
        XUiC_RebirthPlayersListEntry firstVisible = null;
        XUiC_RebirthPlayersListEntry localVisible = null;
        XUiC_RebirthPlayersListEntry selectedVisible = null;
        int localEntityId = xui != null && xui.playerUI != null && xui.playerUI.entityPlayer != null
            ? xui.playerUI.entityPlayer.entityId
            : -1;

        for (int i = 0; i < rebirthEntries.Length; ++i)
        {
            XUiC_RebirthPlayersListEntry entry = rebirthEntries[i];
            if (entry == null || entry.PlayerData == null || entry.ViewComponent == null || !entry.ViewComponent.IsVisible)
                continue;

            if (firstVisible == null)
                firstVisible = entry;
            if (entry.EntityId == localEntityId)
                localVisible = entry;
            if (selectedPlayerId != null && object.Equals(entry.PlayerData.PrimaryId, selectedPlayerId))
            {
                selectedVisible = entry;
                selectedPlayerData = entry.PlayerData;
            }
        }

        if (selectedPlayerId == null && selectFirstIfMissing)
        {
            XUiC_RebirthPlayersListEntry initial = localVisible ?? firstVisible;
            if (initial != null)
            {
                selectedPlayerId = initial.PlayerData.PrimaryId;
                selectedPlayerData = initial.PlayerData;
                SelectionChanged?.Invoke();
            }
        }
        else if (selectedPlayerId != null && selectedVisible == null && selectedPlayerData == null)
        {
            PersistentPlayerData current = GameManager.Instance != null && GameManager.Instance.persistentPlayers != null
                ? GameManager.Instance.persistentPlayers.GetPlayerData(selectedPlayerId)
                : null;
            if (current != null)
                selectedPlayerData = current;
        }

        RefreshGroupIdentityRows();
        ApplySelectionVisuals();
    }

    private void RefreshGroupIdentityRows()
    {
        for (int i = 0; i < rebirthEntries.Length; ++i)
            rebirthEntries[i]?.RefreshRebirthGroupIdentity();
    }

    private void ApplySelectionVisuals()
    {
        for (int i = 0; i < rebirthEntries.Length; ++i)
        {
            XUiC_RebirthPlayersListEntry entry = rebirthEntries[i];
            bool selected = entry != null && entry.PlayerData != null && selectedPlayerId != null && object.Equals(entry.PlayerData.PrimaryId, selectedPlayerId);
            entry?.SetRebirthSelected(selected);
        }
    }
}

/// <summary>
/// Native PlayersListEntry behavior plus a REBIRTH selection outline only.
/// All existing row actions remain owned by XUiC_PlayersListEntry.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthPlayersListEntry : XUiC_PlayersListEntry
{
    private XUiV_Sprite rebirthSelection;
    private XUiV_Label rebirthGroupName;

    public override void Init()
    {
        base.Init();
        XUiController selection = GetChildById("rebirthSelection");
        rebirthSelection = selection != null ? selection.ViewComponent as XUiV_Sprite : null;
        XUiController group = GetChildById("rebirthGroupName");
        rebirthGroupName = group != null ? group.ViewComponent as XUiV_Label : null;
        SetRebirthSelected(false);
        RefreshRebirthGroupIdentity();
    }

    public void RefreshRebirthGroupIdentity()
    {
        if (rebirthGroupName == null) return;
        RebirthPartyIdentityView view;
        if (EntityId >= 0 && RebirthPartyIdentityClientState.TryGet(EntityId, out view) && view != null && !string.IsNullOrEmpty(view.Name))
        {
            rebirthGroupName.Text = view.Name;
            rebirthGroupName.Color = RebirthPartyIdentityService.GetColor(view.ColorId);
            rebirthGroupName.IsVisible = true;
        }
        else
        {
            rebirthGroupName.Text = string.Empty;
            rebirthGroupName.IsVisible = false;
        }
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        bool grouped=rebirthGroupName!=null&&rebirthGroupName.IsVisible;
        if(PlayerName?.lblName!=null)
        {
            // Every setter marks the view dirty and makes the native update re-parse its anchors (garbage each frame),
            // so only write values that actually differ.
            FitNameLabel(PlayerName.lblName,grouped);
            FitNameLabel(PlayerName.lblNameCrossplay,grouped);
            if(rebirthGroupName!=null)
            {
                if(rebirthGroupName.FontSize!=18)rebirthGroupName.FontSize=18;
                if(rebirthGroupName.Size.x!=245||rebirthGroupName.Size.y!=22)rebirthGroupName.Size=new Vector2i(245,22);
                if(rebirthGroupName.Pivot!=UIWidget.Pivot.TopLeft)rebirthGroupName.Pivot=UIWidget.Pivot.TopLeft;
                if(rebirthGroupName.Position.x!=5||rebirthGroupName.Position.y!=-22)rebirthGroupName.Position=new Vector2i(5,-22);
            }
        }
    }

    private static void FitNameLabel(XUiV_Label label,bool grouped)
    {
        if(label==null)return;
        if(label.FontSize!=18)label.FontSize=18;
        if(label.Size.y!=22)label.Size=new Vector2i(label.Size.x,22);
        int y=grouped?-12:-23;
        if(label.Position.y!=y)label.Position=new Vector2i(label.Position.x,y);
    }

    public void SetRebirthSelected(bool selected)
    {
        if (rebirthSelection != null)
            rebirthSelection.IsVisible = selected;
    }
}

/// <summary>
/// Read-only selected-player detail projection. It deliberately exposes only state
/// that is already authoritative/available on the native Players screen.
/// Remote equipment, remote buffs, invented status categories, messaging, and mute
/// actions are intentionally not synthesized here.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthPlayerDetails : XUiController
{
    private XUiC_RebirthPlayersList playersList;
    private XUiController content;
    private XUiController emptyHint;

    private XUiV_Label nameLabel;
    private XUiV_Label statusLabel;
    private XUiV_Label levelValue;
    private XUiV_Label gameStageValue;
    private XUiV_Label pingValue;
    private XUiV_Label zombieKillsValue;
    private XUiV_Label playerKillsValue;
    private XUiV_Label deathsValue;
    private XUiV_Label partyValue;
    private XUiV_Label alliesValue;
    private XUiV_Label distanceValue;
    private XUiV_Label groupReadOnlyName;
    private XUiV_Label groupPermission;
    [XuiBindComponent("rebirthGroupIdentityNameInput", true)]
    public readonly XUiC_TextInput rebirthGroupIdentityNameInput;
    private XUiC_TextInput groupNameInput;
    private XUiController groupSaveButton;
    private XUiController teachSelectedButton;
    private int teachingTargetEntityId = -1;
    private string teachingTargetName = string.Empty;
    private readonly XUiController[] groupColorButtons = new XUiController[RebirthPartyIdentityService.ColorCount];
    private readonly XUiV_Sprite[] groupColorSelections = new XUiV_Sprite[RebirthPartyIdentityService.ColorCount];
    private int selectedGroupColor;
    private XUiController groupColorButton;
    private XUiV_Sprite groupColorSwatch;
    private int editorEntityId = -1;
    private long editorRevision = -1L;
    private string editorGroupId = string.Empty;
    private bool editorDirty;
    private bool editorConflict;

    private float refreshTimer;
    private RebirthCharacterModelBinder model;
    private int profileEntityId=-1;
    private float profileRefreshAt;
    private RebirthPublicPlayerProfile publicProfile;
    private XUiV_Label profileBackgroundLabel, profileIdentityLabel;
    private readonly XUiV_Label[] profileSkillNames = new XUiV_Label[4];
    private readonly XUiV_Label[] profileSkillValues = new XUiV_Label[4];
    private readonly XUiV_Sprite[] profileSkillIcons = new XUiV_Sprite[4];

    public override void Init()
    {
        base.Init();
        content = GetChildById("rebirthPlayerDetailsContent");
        model=new RebirthCharacterModelBinder(GetChildById("rebirthSelectedPlayerModel"));
        backgroundArt=new RebirthSurvivorArtTextureBinder(GetChildById("rebirthProfileBackgroundArt"),1f);
        profileBackgroundLabel=GetLabel("rebirthProfileBackground");
        profileIdentityLabel=GetLabel("rebirthProfileIdentity");
        for(int i=0;i<4;i++)
        {
            profileSkillNames[i]=GetLabel("rebirthProfileSkillName"+i);
            profileSkillValues[i]=GetLabel("rebirthProfileSkillValue"+i);
            profileSkillIcons[i]=GetChildById("rebirthProfileSkillIcon"+i)?.ViewComponent as XUiV_Sprite;
        }
        emptyHint = GetChildById("rebirthPlayerDetailsEmpty");

        nameLabel = GetLabel("rebirthPlayerDetailsName");
        statusLabel = GetLabel("rebirthPlayerDetailsStatus");
        levelValue = GetLabel("rebirthPlayerDetailsLevelValue");
        gameStageValue = GetLabel("rebirthPlayerDetailsGameStageValue");
        pingValue = GetLabel("rebirthPlayerDetailsPingValue");
        zombieKillsValue = GetLabel("rebirthPlayerDetailsZombieKillsValue");
        playerKillsValue = GetLabel("rebirthPlayerDetailsPlayerKillsValue");
        deathsValue = GetLabel("rebirthPlayerDetailsDeathsValue");
        partyValue = GetLabel("rebirthPlayerDetailsPartyValue");
        alliesValue = GetLabel("rebirthPlayerDetailsAlliesValue");
        distanceValue = GetLabel("rebirthPlayerDetailsDistanceValue");
        groupReadOnlyName = GetLabel("rebirthGroupIdentityReadOnlyName");
        groupPermission = GetLabel("rebirthGroupIdentityPermission");
        groupNameInput = rebirthGroupIdentityNameInput ?? (GetChildById("rebirthGroupIdentityNameInput") as XUiC_TextInput);
        if (groupNameInput != null) groupNameInput.OnChangeHandler += delegate(XUiController sender,string value,bool fromCode) { if(!fromCode) editorDirty=true; };
        groupSaveButton = GetChildById("btnRebirthGroupIdentitySave");
        teachSelectedButton = GetChildById("btnRebirthTeachSelected");
        groupColorButton=GetChildById("btnRebirthGroupColorPicker");
        groupColorSwatch=GetChildById("rebirthGroupColorSwatch")?.ViewComponent as XUiV_Sprite;
        if(groupColorButton!=null)groupColorButton.OnPress+=(sender,button)=>
        {
            int identity=editorEntityId;
            RebirthPartyIdentityView current;
            var local=xui.playerUI.entityPlayer;
            if(local==null||!RebirthPartyIdentityClientState.TryGet(local.entityId,out current)||!current.CanEdit)return;
            XUiC_RebirthGroupColorPicker.Open(xui,RebirthPartyIdentityService.GetColor(selectedGroupColor),color=>
            {if(editorEntityId!=identity)return;selectedGroupColor=RebirthPartyIdentityService.EncodeColor(color);editorDirty=true;if(groupColorSwatch!=null)groupColorSwatch.Color=color;});
        };
        if (teachSelectedButton != null) teachSelectedButton.OnPress += delegate { OpenTeaching(); };
        if (groupSaveButton != null) groupSaveButton.OnPress += delegate { SaveGroupIdentity(); };
        for (int i = 0; i < groupColorButtons.Length; ++i)
        {
            int captured = i;
            groupColorButtons[i] = GetChildById("btnRebirthGroupColor" + i);
            XUiController selection = GetChildById("rebirthGroupColorSelected" + i);
            groupColorSelections[i] = selection != null ? selection.ViewComponent as XUiV_Sprite : null;
            if (groupColorButtons[i] != null) groupColorButtons[i].OnPress += delegate { SelectGroupColor(captured); };
        }

        BindPlayersList();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        RebirthPublicPlayerProfile.Received-=ReceiveProfile;RebirthPublicPlayerProfile.Received+=ReceiveProfile;profileEntityId=-1;profileRefreshAt=0;
        RebirthPartyIdentityClientState.Changed -= OnGroupIdentityChanged;
        RebirthPartyIdentityClientState.Changed += OnGroupIdentityChanged;
        EntityPlayerLocal local = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (local != null) RebirthPartyIdentityService.RequestSnapshot(local, true, "player-details-open");
        BindPlayersList();
        RefreshDetails();
    }

    public override void OnClose()
    {
        RebirthPartyIdentityClientState.Changed -= OnGroupIdentityChanged;
        if (playersList != null)
            playersList.SelectionChanged -= OnSelectionChanged;
        playersList = null;
        editorEntityId = -1;
        editorRevision = -1L; editorGroupId = string.Empty; editorDirty=false; editorConflict=false;
        model?.Clear();RebirthPublicPlayerProfile.Received-=ReceiveProfile;publicProfile=null;profileEntityId=-1;
        base.OnClose();
    }

    private void OnGroupIdentityChanged()
    {
        RefreshDetails();
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        refreshTimer -= _dt;
        if (refreshTimer > 0f)
            return;
        refreshTimer = 0.25f;
        RefreshDetails();
    }

    private void BindPlayersList()
    {
        XUiC_RebirthPlayersList found = windowGroup != null && windowGroup.Controller != null
            ? windowGroup.Controller.GetChildByType<XUiC_RebirthPlayersList>()
            : null;
        if (found == playersList)
            return;
        if (playersList != null)
            playersList.SelectionChanged -= OnSelectionChanged;
        playersList = found;
        if (playersList != null)
            playersList.SelectionChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        RefreshDetails();
    }

    private XUiV_Label GetLabel(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Label : null;
    }

    private static void SetText(XUiV_Label label, string text)
    {
        if (label != null)
            label.Text = text ?? string.Empty;
    }

    private void RefreshDetails()
    {
        if (playersList == null)
            BindPlayersList();

        PersistentPlayerData ppd = playersList != null ? playersList.SelectedPlayerData : null;
        bool hasSelection = ppd != null;
        if (content != null && content.ViewComponent != null)
            content.ViewComponent.IsVisible = hasSelection;
        if (emptyHint != null && emptyHint.ViewComponent != null)
            emptyHint.ViewComponent.IsVisible = !hasSelection;
        if (!hasSelection)
            return;

        EntityPlayerLocal local = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        EntityPlayer selected = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetEntity(ppd.EntityId) as EntityPlayer
            : null;
        bool online = selected != null;
        bool isLocal = online && local != null && selected.entityId == local.entityId;
        RefreshProfile(selected,local,ppd.EntityId);

        // PersistentPlayerData constructs PlayerName alongside PlayerData; mirror the native
        // Players code path and avoid inventing a second identity/name source.
        string displayName = ppd.PlayerName.SafeDisplayName;
        if (string.IsNullOrEmpty(displayName))
            displayName = ppd.PlayerName.DisplayName;
        if (isLocal)
            displayName = displayName + " (" + Localization.Get("xuiRebirthPlayerYou") + ")";
        SetText(nameLabel, displayName);

        string status = isLocal
            ? Localization.Get("xuiRebirthPlayerStatusYou")
            : (online ? Localization.Get("xuiRebirthPlayerStatusOnline") : Localization.Get("xuiRebirthPlayerStatusOffline"));
        SetText(statusLabel, status);
        if (statusLabel != null)
            statusLabel.Color = isLocal ? new Color32(181, 140, 255, 255) : (online ? new Color32(143, 209, 143, 255) : new Color32(130, 130, 130, 255));

        teachingTargetEntityId = online && !isLocal ? selected.entityId : -1;
        teachingTargetName = online && !isLocal ? displayName : string.Empty;
        if (teachSelectedButton != null && teachSelectedButton.ViewComponent != null)
        {
            bool canTeach = RebirthSurvivorMode.IsEnabledForCurrentWorld() && online && !isLocal && local != null && (selected.position-local.position).sqrMagnitude <= 64f;
            teachSelectedButton.ViewComponent.IsVisible = online && !isLocal && RebirthSurvivorMode.IsEnabledForCurrentWorld();
            teachSelectedButton.ViewComponent.Enabled = canTeach;
        }

        SetText(levelValue, online ? selected.Progression.GetLevel().ToString() : "--");
        SetText(gameStageValue, online ? selected.gameStage.ToString() : "--");
        SetText(pingValue, GetPingText(selected, local));
        SetText(zombieKillsValue, online ? selected.KilledZombies.ToString() : "--");
        SetText(playerKillsValue, online ? selected.KilledPlayers.ToString() : "--");
        SetText(deathsValue, online ? selected.Died.ToString() : "--");

        RebirthPartyIdentityView groupView = null;
        bool hasGroupView = online && RebirthPartyIdentityClientState.TryGet(selected.entityId, out groupView) && groupView != null;
        SetText(partyValue, GetPartyText(selected, local, isLocal, hasGroupView ? groupView : null));
        if (partyValue != null) partyValue.Color = hasGroupView && !string.IsNullOrEmpty(groupView.Name)
            ? RebirthPartyIdentityService.GetColor(groupView.ColorId)
            : new Color32(255,255,255,255);
        SetText(alliesValue, GetAlliesText(selected, isLocal));
        SetText(distanceValue, GetDistanceText(selected, local, isLocal));
        RefreshGroupIdentityEditor(selected, isLocal, hasGroupView ? groupView : null);
    }

    private void ReceiveProfile(RebirthPublicPlayerProfile profile)
    {
        if(profile!=null&&profile.EntityId==profileEntityId){publicProfile=profile;RenderProfile();}
    }
    private void RefreshProfile(EntityPlayer selected,EntityPlayerLocal local,int entityId)
    {
        bool changed=profileEntityId!=entityId;
        if(changed){profileEntityId=entityId;publicProfile=null;model?.Clear();profileRefreshAt=0;}
        if(selected!=null)model?.Bind(selected,changed);else model?.Clear();
        if(Time.realtimeSinceStartup>=profileRefreshAt)
        {
            profileRefreshAt=Time.realtimeSinceStartup+5f;
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(connection!=null&&connection.IsServer)publicProfile=RebirthPublicPlayerProfile.FromServer(selected);
            else if(selected!=null&&local!=null&&selected.entityId==local.entityId)publicProfile=RebirthPublicPlayerProfile.Build(selected,RebirthSurvivorClientState.GetOwnerStateSnapshot());
            else if(local!=null&&connection!=null)connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPublicProfileRequest>().Setup(local.entityId,entityId));
        }
        RenderProfile();
    }
    private RebirthSurvivorArtTextureBinder backgroundArt;
    private void RenderProfile()
    {
        RebirthBackgroundDefinition definition;
        if(publicProfile!=null && RebirthSurvivorDefinitionRegistry.TryGetBackground(publicProfile.BackgroundId,out definition))backgroundArt.BindBackgroundThumbnail(definition);else backgroundArt.Clear();
        SetText(profileBackgroundLabel,publicProfile?.Background??"Profile unavailable");
        SetText(profileIdentityLabel,publicProfile?.Identity??"");
        for(int i=0;i<4;i++)
        {
            var name=publicProfile?.Names[i];bool show=!string.IsNullOrEmpty(name);
            SetText(profileSkillNames[i],name);
            SetText(profileSkillValues[i],show?publicProfile.Values[i].ToString("0.#",System.Globalization.CultureInfo.InvariantCulture):"");
            var icon=profileSkillIcons[i];
            if(icon!=null){icon.IsVisible=show;if(show)icon.SpriteName=publicProfile.Icons[i];}
        }
    }

    private void OpenTeaching()
    {
        EntityPlayerLocal local=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        if(local==null||teachingTargetEntityId<0)return;
        RebirthTeachingUiService.Open(local.PlayerUI!=null?local.PlayerUI.xui:xui,teachingTargetEntityId,teachingTargetName);
    }

    private string GetPingText(EntityPlayer selected, EntityPlayerLocal local)
    {
        if (selected == null)
            return "--";
        if (local != null && selected.entityId == local.entityId && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return "--";
        return selected.pingToServer < 0 ? "--" : selected.pingToServer.ToString();
    }

    private string GetPartyText(EntityPlayer selected, EntityPlayerLocal local, bool isLocal, RebirthPartyIdentityView groupView)
    {
        if (selected == null) return Localization.Get("xuiRebirthPlayerStatusOffline");
        if (local == null) return "--";

        string nativeState;
        if (isLocal)
        {
            if (!local.IsInParty()) nativeState = Localization.Get("xuiRebirthPlayerPartyNone");
            else nativeState = local.IsPartyLead() ? Localization.Get("xuiRebirthPlayerPartyLeader") : Localization.Get("xuiRebirthPlayerPartyMember");
        }
        else if (local.IsInParty() && local.Party != null && local.Party.MemberList.Contains(selected))
            nativeState = selected.IsPartyLead() ? Localization.Get("xuiRebirthPlayerPartyLeader") : Localization.Get("xuiRebirthPlayerPartyMember");
        else if (local.partyInvites != null && local.partyInvites.Contains(selected)) nativeState = Localization.Get("xuiRebirthPlayerPartyInviteReceived");
        else if (selected.partyInvites != null && selected.partyInvites.Contains(local)) nativeState = Localization.Get("xuiRebirthPlayerPartyInviteSent");
        else nativeState = Localization.Get("xuiRebirthPlayerPartyNone");

        if (groupView != null && !string.IsNullOrEmpty(groupView.Name))
            return groupView.Name + " · " + nativeState;
        return nativeState;
    }

    private void RefreshGroupIdentityEditor(EntityPlayer selected, bool isLocal, RebirthPartyIdentityView view)
    {
        bool hasView = selected != null && view != null;
        string displayName = hasView && !string.IsNullOrEmpty(view.Name) ? view.Name : Localization.Get("xuiRebirthGroupUnnamed");
        SetText(groupReadOnlyName, displayName);
        if (groupReadOnlyName != null) groupReadOnlyName.Color = hasView ? RebirthPartyIdentityService.GetColor(view.ColorId) : new Color32(160,160,160,255);

        bool canEdit = isLocal && hasView && view.CanEdit;
        int selectedEntityId = selected != null ? selected.entityId : -1;
        string incomingGroupId = hasView ? (view.GroupId ?? string.Empty) : string.Empty;
        long incomingRevision = hasView ? view.Revision : -1L;
        bool identityChanged = editorEntityId != selectedEntityId || !string.Equals(editorGroupId,incomingGroupId,StringComparison.Ordinal);
        bool sameIdentityChanged = !identityChanged && editorRevision != incomingRevision;
        if (groupNameInput != null)
        {
            groupNameInput.Enabled = canEdit;
            if (identityChanged || (!editorDirty && sameIdentityChanged))
            {
                groupNameInput.Text = hasView ? (view.Name ?? string.Empty) : string.Empty;
                editorDirty = false; editorConflict = false;
            }
            else if (editorDirty && sameIdentityChanged) editorConflict = true;
        }
        if (groupSaveButton != null && groupSaveButton.ViewComponent != null) groupSaveButton.ViewComponent.Enabled = canEdit;
        for (int i = 0; i < groupColorButtons.Length; ++i)
            if (groupColorButtons[i] != null && groupColorButtons[i].ViewComponent != null) groupColorButtons[i].ViewComponent.Enabled = canEdit;

        if(identityChanged || (!editorDirty && sameIdentityChanged)) selectedGroupColor=hasView?view.ColorId:0;
        if(groupColorButton?.ViewComponent!=null)groupColorButton.ViewComponent.Enabled=canEdit;
        if(groupColorSwatch!=null)groupColorSwatch.Color=RebirthPartyIdentityService.GetColor(selectedGroupColor);
        for (int i = 0; i < groupColorSelections.Length; ++i) if (groupColorSelections[i] != null) groupColorSelections[i].IsVisible = i == selectedGroupColor;

        string permission = editorConflict ? RebirthSurvivorUiText.L("xuiRebirthGroupChangedWhileEditing","Group identity changed while you were editing. Your draft was preserved; save to replace it or change selection to discard it.") : !isLocal ? Localization.Get("xuiRebirthGroupViewOnly")
            : (!hasView ? Localization.Get("xuiRebirthGroupSyncing")
            : (canEdit ? (view.IsParty ? Localization.Get("xuiRebirthGroupLeaderCanEdit") : Localization.Get("xuiRebirthGroupSoloCanEdit"))
                       : Localization.Get("xuiRebirthGroupMemberViewOnly")));
        SetText(groupPermission, permission);
        editorEntityId = selectedEntityId;
        editorGroupId = incomingGroupId;
        editorRevision = incomingRevision;
    }

    private void SelectGroupColor(int colorId)
    {
        selectedGroupColor = RebirthPartyIdentityService.NormalizeColor(colorId);
        editorDirty = true;
        for (int i = 0; i < groupColorSelections.Length; ++i) if (groupColorSelections[i] != null) groupColorSelections[i].IsVisible = i == selectedGroupColor;
    }

    [XuiBindEvent("OnSubmitHandler", "rebirthGroupIdentityNameInput")]
    public void GroupIdentityName_OnSubmit(XUiController sender, string text)
    {
        SaveGroupIdentity();
    }

    private void SaveGroupIdentity()
    {
        EntityPlayerLocal local = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (local == null || groupNameInput == null) return;
        RebirthPartyIdentityView view;
        if (!RebirthPartyIdentityClientState.TryGet(local.entityId, out view) || view == null || !view.CanEdit) return;
        string name = RebirthPartyIdentityService.NormalizeName(groupNameInput.Text);
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c == null) return;
        editorDirty = false; editorConflict = false;
        if (c.IsServer)
        {
            string ignored; RebirthPartyIdentityService.ProcessServerRequest(local, name, selectedGroupColor, out ignored);
        }
        else c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPartyIdentityRequest>().SetupEdit(local.entityId, name, selectedGroupColor));
        SetText(groupPermission, Localization.Get("xuiRebirthGroupSaving"));
    }

    private string GetAlliesText(EntityPlayer selected, bool isLocal)
    {
        if (selected == null)
            return Localization.Get("xuiRebirthPlayerStatusOffline");
        if (isLocal)
            return Localization.Get("xuiRebirthPlayerYou");
        return selected.IsFriendOfLocalPlayer
            ? Localization.Get("xuiRebirthPlayerAllied")
            : Localization.Get("xuiRebirthPlayerNotAllied");
    }

    private string GetDistanceText(EntityPlayer selected, EntityPlayerLocal local, bool isLocal)
    {
        if (selected == null || local == null || isLocal)
            return "--";

        bool inParty = selected.IsInPartyOfLocalPlayer;
        bool allied = selected.IsFriendOfLocalPlayer;
        if (!inParty && !allied)
            return "--";

        float distance = (selected.GetPosition() - local.GetPosition()).magnitude;
        return ValueDisplayFormatters.Distance(distance);
    }
}
