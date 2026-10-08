using System;
using System.Collections.Generic;
using Platform;
using UnityEngine;
using HarmonyLib;
public sealed partial class XUiC_RebirthPlayersList
{
    private XUiC_RebirthCharacterOverviewList playerScroll;
    private bool refreshingPlayers;
  public void RefreshScrollablePlayers()
  {
    if(playerScroll==null||refreshingPlayers)return;
    refreshingPlayers=true;
    try {
    this.sortedPlayerList.Clear();
    EntityPlayerLocal entityPlayer = this.xui.playerUI.entityPlayer;
    for (int index = 0; index < GameManager.Instance.World.Players.list.Count; ++index)
    {
      PersistentPlayerData persistentPlayerData = GameManager.Instance.World.Players.list[index].PersistentPlayerData;
      if (persistentPlayerData != null)
        this.sortedPlayerList.Add(persistentPlayerData);
    }
    foreach (PlatformUserIdentifierAbs enumerateAlly in GameManager.Instance.persistentPlayers.Allies.EnumerateAllies(GameManager.Instance.persistentLocalPlayer.PrimaryId))
    {
      PersistentPlayerData playerData = GameManager.Instance.persistentPlayers.GetPlayerData(enumerateAlly);
      if (playerData != null && !((UnityEngine.Object) GameManager.Instance.World.GetEntity(playerData.EntityId) != (UnityEngine.Object) null))
        this.sortedPlayerList.Add(playerData);
    }
    this.sortedPlayerList.Sort(new Comparison<PersistentPlayerData>(XUiC_PlayersList.PlayerComparator));
    this.numberOfPlayers.Text = this.sortedPlayerList.Count.ToString();
    playerScroll.SetItemCount(sortedPlayerList.Count,"No players.");
    GameServerInfo gameServerInfo = SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer ? SingletonMonoBehaviour<ConnectionManager>.Instance.LocalServerInfo : SingletonMonoBehaviour<ConnectionManager>.Instance.LastGameServerInfo;
    bool _showCrossplay = gameServerInfo != null && gameServerInfo.AllowsCrossplay;
    if (!_showCrossplay)
    {
      EPlayGroup playGroup1 = DeviceFlag.StandaloneWindows.ToPlayGroup();
      for (int index = 0; index < this.sortedPlayerList.Count; ++index)
      {
        EPlayGroup playGroup2 = this.sortedPlayerList[index].PlayGroup;
        if (playGroup2 != EPlayGroup.Unknown && playGroup2 != playGroup1)
        {
          _showCrossplay = true;
          break;
        }
      }
    }
    int index1;
    for (index1 = 0; index1 < this.playerList.Rows && index1 < this.sortedPlayerList.Count; ++index1)
    {
      int index2 = index1 + playerScroll.FirstDataIndex;
      if (index2 < this.sortedPlayerList.Count)
      {
        XUiC_PlayersListEntry playerEntry = this.playerEntries[index1];
        if (playerEntry != null)
        {
          PersistentPlayerData sortedPlayer = this.sortedPlayerList[index2];
          EntityPlayer entity = GameManager.Instance.World.GetEntity(sortedPlayer.EntityId) as EntityPlayer;
          bool flag1 = (UnityEngine.Object) entity != (UnityEngine.Object) null && (UnityEngine.Object) entity != (UnityEngine.Object) entityPlayer && entity.IsInPartyOfLocalPlayer;
          int num;
          if (!((UnityEngine.Object) entity == (UnityEngine.Object) null))
          {
            if ((UnityEngine.Object) entity != (UnityEngine.Object) entityPlayer)
            {
              PersistentPlayerData persistentLocalPlayer = GameManager.Instance.persistentLocalPlayer;
              num = persistentLocalPlayer != null ? (persistentLocalPlayer.IsAlly(sortedPlayer) ? 1 : 0) : 0;
            }
            else
              num = 0;
          }
          else
            num = 1;
          bool flag2 = num != 0;
          foreach (EBlockType _blockType in (IEnumerable<EBlockType>) EnumUtils.Values<EBlockType>())
            playerEntry.playerBlockStateChanged(sortedPlayer.PlatformData, _blockType, sortedPlayer.PlatformData.Blocked[_blockType].State);
          if ((UnityEngine.Object) entity != (UnityEngine.Object) null)
          {
            playerEntry.IsOffline = false;
            playerEntry.EntityId = entity.entityId;
            playerEntry.PlayerData = sortedPlayer;
            playerEntry.ViewComponent.IsVisible = true;
            playerEntry.PlayerName.UpdatePlayerData(sortedPlayer.PlayerData, _showCrossplay, sortedPlayer.PlayerName.DisplayName);
            playerEntry.AdminSprite.IsVisible = entity.IsAdmin;
            playerEntry.TwitchSprite.IsVisible = entity.TwitchEnabled && entity.TwitchActionsEnabled == EntityPlayer.TwitchActionsStates.Enabled;
            playerEntry.TwitchDisabledSprite.IsVisible = entity.TwitchActionsEnabled != EntityPlayer.TwitchActionsStates.Enabled || entity.TwitchSafe;
            playerEntry.TwitchDisabledSprite.SpriteName = entity.TwitchActionsEnabled != EntityPlayer.TwitchActionsStates.Enabled ? "ui_game_symbol_twitch_action_disabled" : "ui_game_symbol_brick";
            playerEntry.TwitchDisabledSprite.ToolTip = entity.TwitchActionsEnabled != EntityPlayer.TwitchActionsStates.Enabled ? XUiC_PlayersList.twitchDisabled : XUiC_PlayersList.twitchSafe;
            playerEntry.ZombieKillsText.Text = entity.KilledZombies.ToString();
            playerEntry.PlayerKillsText.Text = entity.KilledPlayers.ToString();
            playerEntry.DeathsText.Text = entity.Died.ToString();
            playerEntry.LevelText.Text = entity.Progression.GetLevel().ToString();
            playerEntry.GamestageText.Text = entity.gameStage.ToString();
            playerEntry.PingText.Text = !((UnityEngine.Object) entity == (UnityEngine.Object) entityPlayer) || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer ? (entity.pingToServer < 0 ? "--" : entity.pingToServer.ToString()) : "--";
            playerEntry.Voice.IsVisible = (UnityEngine.Object) entity != (UnityEngine.Object) entityPlayer;
            playerEntry.Chat.IsVisible = (UnityEngine.Object) entity != (UnityEngine.Object) entityPlayer;
            playerEntry.IsFriend = flag2;
            playerEntry.ShowOnMapEnabled = flag2 | flag1 && World.MapEnabled;
            if (flag2 | flag1)
            {
              float magnitude = (entity.GetPosition() - entityPlayer.GetPosition()).magnitude;
              playerEntry.DistanceToFriend.Text = ValueDisplayFormatters.Distance(magnitude);
            }
            else
              playerEntry.DistanceToFriend.Text = "--";
            playerEntry.buttonReportPlayer.IsVisible = PlatformManager.MultiPlatform.PlayerReporting != null && (UnityEngine.Object) entity != (UnityEngine.Object) entityPlayer;
            playerEntry.IsLocalPlayer = (UnityEngine.Object) entity == (UnityEngine.Object) entityPlayer;
            if ((UnityEngine.Object) entity == (UnityEngine.Object) entityPlayer)
              playerEntry.PartyStatus = !entityPlayer.partyInvites.Contains(entity) ? (!entityPlayer.IsInParty() ? XUiC_PlayersListEntry.EnumPartyStatus.LocalPlayer_NoParty : ((UnityEngine.Object) entityPlayer.Party.Leader == (UnityEngine.Object) entityPlayer ? XUiC_PlayersListEntry.EnumPartyStatus.LocalPlayer_InPartyAsLead : XUiC_PlayersListEntry.EnumPartyStatus.LocalPlayer_InParty)) : XUiC_PlayersListEntry.EnumPartyStatus.LocalPlayer_Received;
            else if (entityPlayer.IsInParty())
            {
              bool flag3 = entityPlayer.IsPartyLead();
              playerEntry.PartyStatus = !entityPlayer.Party.MemberList.Contains(entity) ? (!entity.IsInParty() || !entity.Party.IsFull() ? (flag3 ? XUiC_PlayersListEntry.EnumPartyStatus.OtherPlayer_NoPartyAsLead : XUiC_PlayersListEntry.EnumPartyStatus.OtherPlayer_NoParty) : XUiC_PlayersListEntry.EnumPartyStatus.OtherPlayer_PartyFullAsLead) : (!flag3 ? (entity.IsPartyLead() ? XUiC_PlayersListEntry.EnumPartyStatus.OtherPlayer_InPartyIsLead : XUiC_PlayersListEntry.EnumPartyStatus.OtherPlayer_InParty) : XUiC_PlayersListEntry.EnumPartyStatus.OtherPlayer_InPartyAsLead);
            }
            else if (entityPlayer.partyInvites.Contains(entity))
            {
              if (entity.IsInParty() && entity.Party.IsFull())
              {
                playerEntry.PartyStatus = XUiC_PlayersListEntry.EnumPartyStatus.OtherPlayer_NoPartyAsLead;
                entityPlayer.partyInvites.Remove(entity);
              }
              else
                playerEntry.PartyStatus = XUiC_PlayersListEntry.EnumPartyStatus.LocalPlayer_Received;
            }
            else
              playerEntry.PartyStatus = XUiC_PlayersListEntry.EnumPartyStatus.OtherPlayer_NoPartyAsLead;
          }
          else
          {
            playerEntry.IsOffline = true;
            playerEntry.EntityId = -1;
            playerEntry.PlayerData = sortedPlayer;
            playerEntry.PlayerName.UpdatePlayerData(sortedPlayer.PlayerData, _showCrossplay, sortedPlayer.PlayerName.DisplayName ?? sortedPlayer.PrimaryId.CombinedString);
            playerEntry.AdminSprite.IsVisible = false;
            playerEntry.TwitchSprite.IsVisible = false;
            playerEntry.TwitchDisabledSprite.IsVisible = false;
            playerEntry.DistanceToFriend.IsVisible = true;
            playerEntry.DistanceToFriend.Text = "--";
            playerEntry.ZombieKillsText.Text = "--";
            playerEntry.PlayerKillsText.Text = "--";
            playerEntry.DeathsText.Text = "--";
            playerEntry.LevelText.Text = "--";
            playerEntry.GamestageText.Text = "--";
            playerEntry.PingText.Text = "--";
            playerEntry.Voice.IsVisible = false;
            playerEntry.Chat.IsVisible = false;
            playerEntry.IsOffline = true;
            playerEntry.IsLocalPlayer = (UnityEngine.Object) entity == (UnityEngine.Object) entityPlayer;
            playerEntry.PartyStatus = XUiC_PlayersListEntry.EnumPartyStatus.Offline;
            playerEntry.labelPartyIcon.IsVisible = true;
            playerEntry.buttonReportPlayer.IsVisible = true;
            playerEntry.buttonShowOnMap.IsVisible = false;
            playerEntry.labelShowOnMap.IsVisible = true;
          }
          playerEntry.RefreshBindings();
        }
      }
      else
        break;
    }
    for (; index1 < this.playerList.Rows; ++index1)
    {
      XUiC_PlayersListEntry playerEntry = this.playerEntries[index1];
      if (playerEntry != null)
      {
        playerEntry.EntityId = -1;
        playerEntry.PlayerData = (PersistentPlayerData) null;
        playerEntry.PlayerName.ClearPlayerData();
        playerEntry.AdminSprite.IsVisible = false;
        playerEntry.TwitchSprite.IsVisible = false;
        playerEntry.TwitchDisabledSprite.IsVisible = false;
        playerEntry.ZombieKillsText.Text = string.Empty;
        playerEntry.PlayerKillsText.Text = string.Empty;
        playerEntry.DeathsText.Text = string.Empty;
        playerEntry.LevelText.Text = string.Empty;
        playerEntry.GamestageText.Text = string.Empty;
        playerEntry.PingText.Text = string.Empty;
        playerEntry.Voice.IsVisible = false;
        playerEntry.Chat.IsVisible = false;
        playerEntry.ShowOnMapEnabled = false;
        playerEntry.DistanceToFriend.IsVisible = false;
        playerEntry.IsLocalPlayer = false;
        playerEntry.PartyStatus = XUiC_PlayersListEntry.EnumPartyStatus.Offline;
        playerEntry.buttonReportPlayer.IsVisible = false;
        playerEntry.labelAllyIcon.IsVisible = false;
        playerEntry.labelPartyIcon.IsVisible = false;
        playerEntry.labelShowOnMap.IsVisible = false;
      }
    }
    foreach(var row in playerEntries)row.ViewComponent.IsVisible=row.PlayerData!=null;
    } finally {refreshingPlayers=false;}
  }

}
[HarmonyPatch(typeof(XUiC_PlayersList),"updatePlayersList")]
internal static class RebirthPlayersPopulate
{
    static bool Prefix(XUiC_PlayersList __instance){var list=__instance as XUiC_RebirthPlayersList;if(list==null)return true;list.RefreshScrollablePlayers();return false;}
}
