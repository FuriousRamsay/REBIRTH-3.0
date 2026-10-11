using System;

#nullable disable

/// <summary>Explicit native handoff for independent/special quest cards. Never presses native Accept.</summary>
internal static class RebirthTraderSpecialJobPreviewAcceptance
{
    internal static bool TryOpen(DialogResponseQuest response, EntityPlayerLocal player, LocalPlayerUI ui,
        EntityTrader trader, Func<bool> admission, Action<DialogStatement> statementChanged)
    {
        if (RebirthSandboxOptionManager.Current.IsPurge) return false;
        if (response == null || !response.IsValid || response.Quest?.QuestClass == null ||
            player == null || ui?.xui == null || trader == null || admission == null || !admission()) return false;
        var quest = response.Quest;
        var questClass = quest.QuestClass;
        var xui = ui.xui;
        var model = xui.Dialog;
        var group = model?.DialogWindowGroup;
        var dialog = group?.CurrentDialog;
        var list = group?.responseWindow;
        var manager = ui.windowManager;
        var journal = player.QuestJournal;
        var game = GameManager.Instance;
        var world = game?.World;
        var state = world?.worldState;
        string guid = state?.Guid;
        if (dialog == null || journal == null || state == null || string.IsNullOrEmpty(guid) ||
            !ReferenceEquals(response.OwnerDialog, dialog) || response.Actions == null || response.Actions.Count != 1)
            return false;
        var action = response.Actions[0] as DialogActionAddQuest;
        if (action == null || !ReferenceEquals(action.Quest, quest) || !ReferenceEquals(action.Owner, response) ||
            !ReferenceEquals(action.OwnerDialog, dialog)) return false;
        int index = action.ListIndex;
        string next = response.NextStatementID, last = response.LastStatementID;
        bool Provenance() => response.IsValid && ReferenceEquals(response.Quest, quest) && ReferenceEquals(quest.QuestClass, questClass) &&
            ReferenceEquals(response.OwnerDialog, dialog) && response.Actions != null && response.Actions.Count == 1 &&
            ReferenceEquals(response.Actions[0], action) && ReferenceEquals(action.Quest, quest) &&
            ReferenceEquals(action.Owner, response) && ReferenceEquals(action.OwnerDialog, dialog) &&
            action.ListIndex == index && response.NextStatementID == next && response.LastStatementID == last;
        object expectedStatement = dialog.CurrentStatement;
        bool Context()
        {
            if (!ReferenceEquals(ui.windowManager, manager) || manager == null || !manager.IsWindowOpen("dialog"))
                return false;
            return ReferenceEquals(ui.windowManager, manager) && ReferenceEquals(GameManager.Instance, game) && ReferenceEquals(game.World, world) &&
                ReferenceEquals(world.worldState, state) && state.Guid == guid &&
                ReferenceEquals(ui.xui, xui) && ReferenceEquals(xui.playerUI, ui) &&
                ReferenceEquals(ui.entityPlayer, player) && ReferenceEquals(player.PlayerUI, ui) &&
                ReferenceEquals(player.QuestJournal, journal) && ReferenceEquals(player.world, world) &&
                ReferenceEquals(world.GetPrimaryPlayer(), player) && ReferenceEquals(world.GetEntity(player.entityId), player) &&
                player.IsSpawned() && !player.IsDead() &&
                ReferenceEquals(world.GetEntity(trader.entityId), trader) &&
                ReferenceEquals(xui.Dialog, model) && ReferenceEquals(model.Respondent, trader) &&
                ReferenceEquals(model.DialogWindowGroup, group) && ReferenceEquals(group.CurrentDialog, dialog) &&
                ReferenceEquals(group.responseWindow, list) && ReferenceEquals(list?.CurrentDialog, dialog) &&
                ReferenceEquals(dialog.CurrentStatement, expectedStatement);
        }
        var nativeGroup = xui.FindWindowGroupByName("questOffer");
        var nativeOffer = nativeGroup?.GetChildByType<XUiC_QuestOfferWindow>();
        bool NativeReady()
        {
            var currentGroup = xui.FindWindowGroupByName("questOffer");
            var currentOffer = currentGroup?.GetChildByType<XUiC_QuestOfferWindow>();
            return nativeGroup != null && nativeOffer != null &&
                ReferenceEquals(currentGroup, nativeGroup) && ReferenceEquals(currentOffer, nativeOffer) &&
                ReferenceEquals(nativeGroup.xui, xui) && ReferenceEquals(nativeOffer.xui, xui) &&
                nativeGroup.windowGroup != null && ReferenceEquals(nativeOffer.windowGroup, nativeGroup.windowGroup);
        }
        // Native lookup may call back into UI/world state. Admit only after all dependency getters.
        if (!NativeReady() || !Context() || !admission() || !Provenance()) return false;
        var existing = journal.FindNonSharedQuest(quest.QuestCode);
        if (existing != null && existing.Active) return false;
        if (!admission() || !Provenance()) return false;

        // Match native SelectResponse's quest statement without executing its bulk action list.
        var statement = new DialogStatement("") {
            NextStatementID = next,
            Text = quest.GetParsedText(quest.QuestClass.StatementText)
        };
        if (!NativeReady() || !Context() || !admission() || !Provenance()) return false;
        dialog.CurrentStatement = statement;
        expectedStatement = statement;
        statementChanged(statement);

        if (!NativeReady() || !Context() || !admission() || !Provenance()) return false;
        XUiC_QuestOfferWindow offer = null;
        try
        {
        offer = XUiC_QuestOfferWindow.OpenQuestOfferWindow(xui, quest, index,
            XUiC_QuestOfferWindow.OfferTypes.Dialog, trader.entityId, npc => {
                // Native offer windows may be reused. A stale decline must never redirect a new conversation.
                try
                {
                if (offer == null || !ReferenceEquals(offer, nativeOffer) || !ReferenceEquals(offer.Quest, quest) || !ReferenceEquals(quest.QuestClass, questClass) ||
                    offer.QuestGiverID != trader.entityId || offer.listIndex != index ||
                    !NativeReady() ||
                    !ReferenceEquals(npc, trader) || !Context()) return;
                model.ReturnStatement = last;
                }
                catch (Exception) { /* A retired native decline context has no safe UI effect. */ }
            });
        }
        catch (Exception)
        {
            // OnOpen may already have changed native UI. Preserve one-shot completion uncertainty.
            // Never reopen, rollback, close a replacement window or reapply journal acceptance.
            return true;
        }
        // The native open/lifecycle owns subsequent confirmation. Never repeat an open on callback retirement.
        return true;
    }
}