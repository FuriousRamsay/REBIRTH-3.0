using System;
using System.Linq;

#nullable disable

/// <summary>A one-shot preview capability bound to an exact live trader offer and UI scope.</summary>
internal static class RebirthTraderJobPreviewAcceptance
{
    private sealed class Token
    {
        internal XUiC_RebirthTraderJobCard Card;
        internal XUi Ui;
        internal LocalPlayerUI PlayerUi;
        internal EntityPlayerLocal Player;
        internal EntityTrader Trader;
        internal XUiC_DialogResponseList List;
        internal Dialog Dialog;
        internal DialogResponseQuest Response;
        internal Quest Quest;
        internal object Game, World, State, Manager, Group, Statement, Entries, Offers, QuestClass, Requirements, DialogModel, CardGroup, CardView, Journal;
        internal string Guid, ResponseId, QuestType, NextStatement, LastStatement;
        internal bool ListedNormal;
        internal object ResponseDialog;
        internal BaseDialogRequirement[] RequirementItems;
        internal object Actions;
        internal object[] ActionItems, OfferItems;
        internal int[] ActionIndices;
        internal bool Consumed;
    }

    internal static object Capture(XUiC_RebirthTraderJobCard card)
    {
        var ui = card?.xui;
        var playerUi = ui?.playerUI;
        var player = playerUi?.entityPlayer;
        var group = ui?.Dialog?.DialogWindowGroup;
        var list = group?.responseWindow;
        var response = card?.CurrentResponse as DialogResponseQuest;
        var trader = ui?.Dialog?.Respondent as EntityTrader;
        var game = GameManager.Instance;
        var world = game?.World;
        if (card == null || !card.CardMode || ui == null || playerUi == null || player == null ||
            group == null || list == null || trader == null || response == null || player.QuestJournal == null ||
            !response.IsValid || response.Quest?.QuestClass == null || world?.worldState == null ||
            response.RequirementList == null || list.entryList == null || list.CurrentDialog == null || string.IsNullOrEmpty(world.worldState.Guid))
            return null;
        var token = new Token {
            Card = card, Ui = ui, PlayerUi = playerUi, Player = player, Trader = trader,
            List = list, Dialog = list.CurrentDialog, Response = response, Quest = response.Quest,
            Game = game, World = world, State = world.worldState, Guid = world.worldState.Guid,
            Manager = playerUi.windowManager, Group = group, DialogModel = ui.Dialog, CardGroup = card.windowGroup, CardView = card.ViewComponent, Journal = player.QuestJournal, Statement = list.CurrentDialog.CurrentStatement,
            Entries = list.entryList, Offers = trader.activeQuests, QuestClass = response.Quest.QuestClass,
            Requirements = response.RequirementList, RequirementItems = response.RequirementList.ToArray(),
            Actions = response.Actions, ActionItems = response.Actions?.Cast<object>().ToArray() ?? new object[0],
            ActionIndices = response.Actions?.Select(action => (action as DialogActionAddQuest)?.ListIndex ?? -1).ToArray() ?? new int[0],
            OfferItems = trader.activeQuests?.Cast<object>().ToArray() ?? new object[0],
            ListedNormal = RebirthTraderJobListRefresh.IsNormalTraderJobResponse(response) && string.IsNullOrEmpty(response.ID) && trader.activeQuests?.Contains(response.Quest) == true,
            ResponseId = response.ID, QuestType = response.Quest.QuestClass.QuestType, ResponseDialog = response.OwnerDialog,
            NextStatement = response.NextStatementID, LastStatement = response.LastStatementID
        };
        return Current(token, true) ? token : null;
    }

    internal static bool IsCurrent(XUiC_RebirthTraderJobCard card, object token)
    {
        var value = token as Token;
        return value != null && ReferenceEquals(value.Card, card) && !value.Consumed && Current(value, true) && !value.Consumed;
    }

    private static bool Current(Token t, bool requireOffer)
    {
        var game = GameManager.Instance;
        var world = game?.World;
        if (t == null || !ReferenceEquals(t.PlayerUi.windowManager, t.Manager) ||
            t.PlayerUi.windowManager == null || !t.PlayerUi.windowManager.IsWindowOpen("dialog")) return false;
        game = GameManager.Instance; world = game?.World;
        if (!ReferenceEquals(game, t.Game) || !ReferenceEquals(world, t.World) ||
            world == null || !ReferenceEquals(world.worldState, t.State) ||
            !string.Equals(world.worldState?.Guid, t.Guid, StringComparison.Ordinal) ||
            !ReferenceEquals(t.Card.xui, t.Ui) || !ReferenceEquals(t.Ui.playerUI, t.PlayerUi) ||
            !ReferenceEquals(t.PlayerUi.xui, t.Ui) ||
            !ReferenceEquals(t.Player.QuestJournal, t.Journal) ||
            !ReferenceEquals(t.PlayerUi.entityPlayer, t.Player) ||
            !ReferenceEquals(t.PlayerUi.windowManager, t.Manager) ||
            !ReferenceEquals(t.Player.PlayerUI, t.PlayerUi) || !ReferenceEquals(t.Player.world, world) ||
            !ReferenceEquals(world.GetPrimaryPlayer(), t.Player) ||
            !ReferenceEquals(world.GetEntity(t.Player.entityId), t.Player) ||
            !t.Player.IsSpawned() || t.Player.IsDead() ||
            !ReferenceEquals(t.Ui.Dialog, t.DialogModel) ||
            !ReferenceEquals(t.Ui.Dialog?.Respondent, t.Trader) ||
            !ReferenceEquals(world.GetEntity(t.Trader.entityId), t.Trader) ||
            !ReferenceEquals(t.Ui.Dialog?.DialogWindowGroup, t.Group) ||
            !ReferenceEquals(t.Ui.Dialog.DialogWindowGroup.responseWindow, t.List) ||
            !ReferenceEquals(t.List.xui, t.Ui) || !ReferenceEquals(t.List.CurrentDialog, t.Dialog) ||
            !ReferenceEquals(t.Ui.Dialog.DialogWindowGroup.CurrentDialog, t.Dialog) ||
            !ReferenceEquals(t.Card.CurrentResponse, t.Response) ||
            !ReferenceEquals(t.Dialog.CurrentStatement, t.Statement) ||
            !ReferenceEquals(t.List.entryList, t.Entries) || !t.List.entryList.Contains(t.Card) ||
            !ReferenceEquals(t.Card.windowGroup, t.CardGroup) ||
            !ReferenceEquals(t.Card.ViewComponent, t.CardView) ||
            t.Card.windowGroup == null || !t.Card.windowGroup.isShowing)
            return false;
        if (!requireOffer) return true;
        if (!t.Card.CardMode || !t.Card.HasRequirement ||
            !ReferenceEquals(t.Card.CurrentResponse, t.Response) ||
            !ReferenceEquals(t.Response.Quest, t.Quest) || !ReferenceEquals(t.Quest.QuestClass, t.QuestClass) ||
            !ReferenceEquals(t.Trader.activeQuests, t.Offers) || (t.ListedNormal && t.Trader.activeQuests?.Contains(t.Quest) != true) ||
            !ReferenceEquals(t.Response.RequirementList, t.Requirements) ||
            t.Response.RequirementList.Count != t.RequirementItems.Length)
            return false;
        for (int i = 0; i < t.RequirementItems.Length; i++)
            if (!ReferenceEquals(t.Response.RequirementList[i], t.RequirementItems[i])) return false;
        if (!ReferenceEquals(t.Response.Actions, t.Actions) ||
            (t.Response.Actions?.Count ?? 0) != t.ActionItems.Length ||
            (t.Trader.activeQuests?.Count ?? 0) != t.OfferItems.Length) return false;
        for (int i = 0; i < t.ActionItems.Length; i++)
            if (!ReferenceEquals(t.Response.Actions[i], t.ActionItems[i]) ||
                ((t.Response.Actions[i] as DialogActionAddQuest)?.ListIndex ?? -1) != t.ActionIndices[i]) return false;
        for (int i = 0; i < t.OfferItems.Length; i++)
            if (!ReferenceEquals(t.Trader.activeQuests[i], t.OfferItems[i])) return false;
        return t.Response.IsValid && t.Response.ID == t.ResponseId &&
            t.Quest.QuestClass.QuestType == t.QuestType &&
            ReferenceEquals(t.Response.OwnerDialog, t.ResponseDialog) &&
            t.Response.NextStatementID == t.NextStatement && t.Response.LastStatementID == t.LastStatement &&
            RebirthUtilities.IsVanillaTrader(t.Trader.entityId);
    }

    internal static bool TryAccept(XUiC_RebirthTraderJobCard card, object token)
    {
        var t = token as Token;
        if (!IsCurrent(card, t)) return false;
        // Consume before callbacks: recursive or repeated submit cannot accept twice.
        t.Consumed = true;
        foreach (var requirement in t.RequirementItems)
        {
            if (requirement == null || !requirement.CheckRequirement(t.Player, t.Trader) || !Current(t, true))
                return false;
        }
        if (!Current(t, true)) return false;
        if (!t.ListedNormal)
            return RebirthTraderSpecialJobPreviewAcceptance.TryOpen(t.Response, t.Player, t.PlayerUi, t.Trader,
                () => Current(t, true), statement => t.Statement = statement);
        bool handled = RebirthTraderJobListRefresh.TryAcceptNormalJobDirect(t.Response, t.Player, t.PlayerUi, t.Trader, () => Current(t, false));
        // The direct path may close/rebuild the dialogue; never refresh a replacement context.
        if (handled && Current(t, false)) t.Ui.Dialog.DialogWindowGroup.RefreshDialog();
        return handled;
    }
}