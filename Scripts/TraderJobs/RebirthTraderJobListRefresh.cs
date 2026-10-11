using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Shared implementation for REBIRTH trader job-list refresh and one-click acceptance.
/// Harmony bindings are intentionally split into conventional class-level patch classes
/// below so the targeted CreateClassProcessor installer cannot silently miss them.
/// </summary>
public static class RebirthTraderJobListRefresh
{
    internal enum DirectAcceptResult
    {
        Allowed,
        Stale,
        RepeatPoi,
        DailyLimit,
        ConcurrentLimit
    }

    public static void ResetPendingListRequest()
    {
        pendingTraderId = -1; pendingPlayerId = -1; pendingJobsStatement = string.Empty;
        pendingWorld = null; pendingDialog = null; pendingListEpoch = 0; pendingListRequestId = 0; pendingListDeadline = 0f;
        unchecked { clientListEpoch++; }
        if (clientListEpoch == 0) clientListEpoch = 1;
    }

    private static bool PendingListExpired()
    {
        if (pendingListRequestId == 0) return false;
        if (pendingWorld == null || !ReferenceEquals(pendingWorld, GameManager.Instance != null ? GameManager.Instance.World : null) ||
            Time.unscaledTime >= pendingListDeadline)
        {
            ResetPendingListRequest();
            return true;
        }
        return false;
    }

    private static int pendingTraderId = -1;
    private static int pendingPlayerId = -1;
    private static string pendingJobsStatement = string.Empty;
    private static World pendingWorld;
    private static Dialog pendingDialog;
    private static ulong clientListEpoch = 1;
    private static ulong nextListRequestId;
    private static ulong pendingListEpoch;
    private static ulong pendingListRequestId;
    private static float pendingListDeadline;
    private const float ListRequestTimeoutSeconds = 10f;

    public static bool IsJobsEntryResponse(DialogResponse response)
    {
        string id = response != null ? response.ID : string.Empty;
        if (string.IsNullOrEmpty(id) ||
            !id.StartsWith("jobshave", StringComparison.OrdinalIgnoreCase))
            return false;

        return !id.Equals("jobshavenotallowed", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNormalTraderJobResponse(DialogResponse response)
    {
        DialogResponseQuest qr = response as DialogResponseQuest;
        return qr != null &&
               qr.Quest != null &&
               qr.Quest.QuestClass != null &&
               string.IsNullOrEmpty(qr.Quest.QuestClass.QuestType);
    }

    /// <summary>
    /// Called from the real XUi response-click handler BEFORE Dialog.SelectResponse.
    /// Host/SP regenerates synchronously. Remote clients suppress the click transition
    /// until the server-authoritative replacement list arrives.
    /// </summary>
    public static bool PrepareJobsEntry(
        XUiC_DialogResponseList responseList,
        DialogResponse response,
        EntityPlayer player)
    {
        if (RebirthSandboxOptionManager.Current.IsPurge) return false;
        RebirthTraderDebug.Trace(
            "JOBS_ENTRY begin response=" + SafeResponse(response) +
            " mode=" + RebirthSandboxOptionManager.Current.TraderJobList);

        EntityPlayerLocal local = player as EntityPlayerLocal;
        LocalPlayerUI ui = local != null ? LocalPlayerUI.GetUIForPlayer(local) : null;
        EntityTrader trader = ui != null && ui.xui != null
            ? ui.xui.Dialog.Respondent as EntityTrader
            : null;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;

        if (trader == null || connection == null)
        {
            RebirthTraderDebug.Trace(
                "JOBS_ENTRY cannot resolve trader/connection -> vanilla");
            return true;
        }

        // The base jobshaveN response points at the player's highest unlocked tier.
        // That tier may be full while a lower unlocked tier still has room (for example
        // five open jobs: Tier II is 5/5 but Tier I is 5/6). Route Jobs to the highest
        // tier that can ACTUALLY accept another job.
        int targetTier =
            RebirthTraderJobPolicy.GetHighestTierWithAcceptanceRoom(
                player,
                trader);

        if (targetTier <= 0)
        {
            RebirthTraderDebug.Trace(
                "JOBS_ENTRY no unlocked tier has acceptance room -> suppress");
            return false;
        }

        string targetStatement = "currentjobs" + targetTier;

        RebirthTraderDebug.Trace(
            "JOBS_ENTRY capacityTarget tier=" + targetTier +
            " statement='" + targetStatement + "'");

        if (RebirthSandboxOptionManager.Current.TraderJobList !=
            RebirthTraderJobListMode.Random)
        {
            if (string.Equals(
                    response.NextStatementID,
                    targetStatement,
                    StringComparison.OrdinalIgnoreCase))
            {
                RebirthTraderDebug.Trace(
                    "JOBS_ENTRY fixed-mode target already correct -> vanilla");
                return true;
            }

            RebirthTraderDebug.Trace(
                "JOBS_ENTRY fixed-mode reroute from '" +
                (response.NextStatementID ?? "") +
                "' to '" + targetStatement + "'");

            EnterJobsStatement(ui, targetStatement);
            return false;
        }

        int previousCount =
            trader.activeQuests != null ? trader.activeQuests.Count : -1;
        trader.activeQuests = null;

        RebirthTraderDebug.Trace(
            "JOBS_ENTRY cleared local activeQuests trader=" + trader.entityId +
            " player=" + player.entityId +
            " previousCount=" + previousCount +
            " isServer=" + connection.IsServer);

        if (connection.IsServer)
        {
            QuestEventManager manager = QuestEventManager.Current;
            if (manager == null)
            {
                RebirthTraderDebug.Trace(
                    "JOBS_ENTRY QuestEventManager null -> suppress stale board");
                return false;
            }

            manager.ClearQuestListForPlayer(
                trader.entityId,
                player.entityId);

            RebirthTraderDebug.Trace(
                "JOBS_ENTRY cleared server player/trader quest cache");

            trader.SetupActiveQuestsForPlayer(player);

            RebirthTraderDebug.Trace(
                "JOBS_ENTRY host regeneration complete count=" +
                (trader.activeQuests != null ? trader.activeQuests.Count : -1) +
                " signature=" + BuildOfferSignature(trader.activeQuests));

            // Enter explicitly so a full highest tier can transparently route to the
            // lower tier that still has capacity.
            EnterJobsStatement(ui, targetStatement);
            return false;
        }

        PendingListExpired();
        Dialog currentDialog = ui != null && ui.xui != null && ui.xui.Dialog != null && ui.xui.Dialog.DialogWindowGroup != null
            ? ui.xui.Dialog.DialogWindowGroup.CurrentDialog : null;
        if (pendingListRequestId != 0)
        {
            // Never overlap native list fetches. Exact response correlation is carried by
            // the generated offer stamp, but suppressing duplicates also avoids needless regeneration.
            RebirthTraderDebug.Trace("JOBS_ENTRY remote request already pending request=" + pendingListRequestId);
            return false;
        }
        unchecked { pendingListRequestId = ++nextListRequestId; }
        if (pendingListRequestId == 0) pendingListRequestId = ++nextListRequestId;
        pendingListEpoch = clientListEpoch;
        pendingTraderId = trader.entityId;
        pendingPlayerId = player.entityId;
        pendingJobsStatement = targetStatement;
        pendingWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
        pendingDialog = currentDialog;
        pendingListDeadline = Time.unscaledTime + ListRequestTimeoutSeconds;

        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId) : null;
        if (persistent == null || persistent.PrimaryId == null)
        {
            ResetPendingListRequest();
            return false;
        }

        RebirthTraderDebug.Trace(
            "JOBS_ENTRY remote request epoch=" + pendingListEpoch + " request=" + pendingListRequestId +
            " pendingStatement='" + pendingJobsStatement + "'");

        // Ordered side-band intent lets the server stamp the subsequently generated native
        // Quest objects with this exact request identity. The native list remains authoritative.
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthTraderListIntent>()
            .Setup(trader.entityId, player.entityId, persistent.PrimaryId, pendingListEpoch, pendingListRequestId));
        connection.SendToServer(
            NetPackageManager.GetPackage<NetPackageNPCQuestList>()
                .Setup(trader.entityId, player.entityId, -1));

        return false;
    }

    /// <summary>
    /// Accept a normal trader job directly from its card. Returns true when REBIRTH
    /// handled the click and vanilla Dialog.SelectResponse must be skipped.
    /// </summary>
    public static bool TryAcceptNormalJobDirect(
        DialogResponseQuest questResponse,
        EntityPlayerLocal player,
        LocalPlayerUI ui,
        EntityTrader trader,
        Func<bool> admission = null)
    {
        if (questResponse == null || player == null || ui == null || trader == null ||
            questResponse.Quest == null || questResponse.Quest.QuestClass == null ||
            !string.IsNullOrEmpty(questResponse.Quest.QuestClass.QuestType))
            return false;

        if (RebirthSandboxOptionManager.Current.IsPurge) return false;
        Quest offered = questResponse.Quest;
        var originalJournal = player.QuestJournal;
        var originalManager = ui.windowManager;
        var originalXui = ui.xui;
        var originalQuestClass = offered.QuestClass;
        var originalOffers = trader.activeQuests;
        var offerSequence = originalOffers != null ? originalOffers.ToArray() : new Quest[0];
        bool ownOfferRemoved = false;
        bool Admitted()
        {
            if (admission == null) return true;
            if (!admission() || !ReferenceEquals(player.QuestJournal, originalJournal) ||
                !ReferenceEquals(ui.windowManager, originalManager) || !ReferenceEquals(ui.xui, originalXui) ||
                !ReferenceEquals(questResponse.Quest, offered) || !ReferenceEquals(offered.QuestClass, originalQuestClass) ||
                !ReferenceEquals(trader.activeQuests, originalOffers) || originalOffers == null)
                return false;
            int position = 0;
            for (int i = 0; i < offerSequence.Length; i++)
            {
                if (ownOfferRemoved && ReferenceEquals(offerSequence[i], offered)) continue;
                if (position >= originalOffers.Count || !ReferenceEquals(originalOffers[position++], offerSequence[i]))
                    return false;
            }
            return position == originalOffers.Count;
        }
        if (!Admitted()) return false;

        int tier = RebirthTraderPoiHistory.GetTier(offered);
        int listIndex = GetListIndex(questResponse);

        int openBefore = RebirthTraderJobPolicy.CountOpenPersonalTraderJobs(player.QuestJournal);
        int dailyUsedBefore = RebirthTraderJobPolicy.CountDailyReservedTraderJobs(player.QuestJournal);
        int dailyLimit = RebirthTraderJobPolicy.GetDailyQuestLimit();
        int tierLimit = RebirthTraderJobPolicy.GetAcceptedJobLimit(
            tier, RebirthTraderJobPolicy.IsMultiplayerClient());

        RebirthTraderDebug.Trace(
            "CARD_CLICK quest='" + (offered.QuestClass.ID ?? "") +
            "' tier=" + tier +
            " listIndex=" + listIndex +
            " poi='" + SafePoi(offered) +
            "' code=" + offered.QuestCode +
            " open=" + openBefore + "/" + tierLimit +
            " dailyUsed=" + dailyUsedBefore + "/" + dailyLimit);

        DirectAcceptResult validation = ValidateDirectAcceptance(player, offered);
        RebirthTraderDebug.Trace("CARD_CLICK validation=" + validation);

        if (validation != DirectAcceptResult.Allowed)
        {
            if (!Admitted()) return false;
            ShowDirectAcceptFailure(player, validation, tier);

            if (validation == DirectAcceptResult.DailyLimit ||
                validation == DirectAcceptResult.ConcurrentLimit)
            {
                bool anyTierRoom =
                    RebirthTraderJobPolicy.CanAcceptAnyListedJob(player, trader);

                RebirthTraderDebug.Trace(
                    "CARD_CLICK max reached before accept anyTierRoom=" +
                    anyTierRoom);

                if (!anyTierRoom && Admitted())
                    if (Admitted()) ui.windowManager.Close("dialog");
            }

            return true;
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        // Treat the visible offer as a reservation until QuestJournal confirms the accepted
        // clone is actually present. Never mutate/remove the visible offer first: a failed
        // AddQuest must leave the player able to retry the exact same card.
        Quest accepted = offered.Clone();
        if (accepted == null)
        {
            if (Admitted()) GameManager.ShowTooltip(player, "Unable to reserve this trader job. The offer was kept.");
            return true;
        }

        if (connection == null || !connection.IsServer)
        {
            if (accepted.CurrentState != Quest.QuestState.InProgress) accepted.ResetQuest();
            RebirthTraderDebug.Trace("CARD_CLICK accept path=remote-clone-reset");
        }
        else
            RebirthTraderDebug.Trace("CARD_CLICK accept path=server-clone");

        accepted.QuestGiverID = trader.entityId;
        RebirthTraderListRequestCorrelation.ClearOfferStamp(accepted);
        var journal = admission != null ? originalJournal : player.QuestJournal;
        int journalBefore = journal.quests != null ? journal.quests.Count : -1;
        Exception addFailure = null;
        try
        {
            if (!Admitted()) return false;
            journal.AddQuest(accepted, Quest.QuestSource.Trader);
        }
        catch (Exception ex)
        {
            addFailure = ex;
            RebirthTraderDebug.Trace("CARD_CLICK AddQuest threw " + ex.GetType().Name + ": " + ex.Message);
        }

        // AddQuest is irreversible. A retired scope is completion uncertainty, never a retry or rollback.
        if (!Admitted()) return true;
        Quest committed = journal.FindActiveQuest(accepted.QuestCode);
        int journalAfter = journal.quests != null ? journal.quests.Count : -1;
        // A pre-existing quest with the same code is not proof that this reservation committed.
        // Require the journal population to advance as well as the accepted identity to resolve.
        bool addCommitted = committed != null && journalAfter > journalBefore;
        RebirthTraderDebug.Trace(
            "CARD_CLICK AddQuest complete journalBefore=" + journalBefore +
            " journalAfter=" + journalAfter +
            " activeQuestFound=" + addCommitted);

        if (!addCommitted)
        {
            if (!Admitted()) return true;
            GameManager.ShowTooltip(player, addFailure != null
                ? "Trader job could not be accepted. The offer was kept."
                : "Trader job was not added. The offer was kept.");
            return true;
        }

        if (!Admitted()) return true;
        bool removed = false;
        if (trader.activeQuests != null) removed = trader.activeQuests.Remove(offered);
        ownOfferRemoved = removed;
        RebirthTraderDebug.Trace(
            "CARD_CLICK committed offer remove=" + removed +
            " remaining=" + (trader.activeQuests != null ? trader.activeQuests.Count : -1));

        // The remote offer-removal packet is sent only after the quest commit is observable.
        // If AddQuest fails, no server/local offer removal is published and no compensation is needed.
        if (connection != null && !connection.IsServer)
        {
            RebirthTraderDebug.Trace(
                "CARD_CLICK send RemoveQuest trader=" + trader.entityId +
                " player=" + player.entityId +
                " tier=" + tier +
                " tierRelativeIndex=" + listIndex);
            if (!Admitted()) return true;
            var removalPackage = NetPackageManager.GetPackage<NetPackageNPCQuestList>()
                .Setup(trader.entityId, player.entityId, tier, (byte)Math.Max(0, listIndex));
            if (!Admitted()) return true;
            connection.SendToServer(removalPackage);
        }

        if (!Admitted()) return true;
        trader.PlayVoiceSetEntry("quest_accepted", player);
        if (offered.QuestTags.Test_AnySet(QuestEventManager.treasureTag) && GameSparksCollector.CollectGamePlayData)
        {
            if (!Admitted()) return true;
            GameSparksCollector.IncrementCounter(
                GameSparksCollector.GSDataKey.QuestAcceptedDistance,
                ((int)Vector3.Distance(offered.Position, trader.position) / 50 * 50).ToString(),
                1);
        }

        bool mayTakeAnotherInThisTier =
            RebirthTraderJobPolicy.CanAcceptAnotherJobAtTier(
                player.QuestJournal, tier);

        bool mayTakeAnotherAnywhere =
            RebirthTraderJobPolicy.CanAcceptAnyListedJob(
                player,
                trader);

        RebirthTraderDebug.Trace(
            "CARD_CLICK postAccept tier=" + tier +
            " mayTakeAnotherInThisTier=" + mayTakeAnotherInThisTier +
            " mayTakeAnotherAnywhere=" + mayTakeAnotherAnywhere +
            " openNow=" +
            RebirthTraderJobPolicy.CountOpenPersonalTraderJobs(player.QuestJournal) +
            " dailyUsedNow=" +
            RebirthTraderJobPolicy.CountDailyReservedTraderJobs(player.QuestJournal));

        if (!mayTakeAnotherAnywhere && Admitted())
        {
            RebirthTraderDebug.Trace(
                "CARD_CLICK accepted final job allowed by all accessible tiers -> close trader dialog");
            if (Admitted()) ui.windowManager.Close("dialog");
        }

        return true;
    }

    private static bool EnterJobsStatement(
        LocalPlayerUI ui,
        string statementId)
    {
        if (ui == null ||
            ui.xui == null ||
            ui.xui.Dialog == null ||
            ui.xui.Dialog.DialogWindowGroup == null ||
            string.IsNullOrEmpty(statementId))
            return false;

        XUiC_DialogWindowGroup group =
            ui.xui.Dialog.DialogWindowGroup;

        if (group.CurrentDialog == null)
            return false;

        DialogStatement target =
            group.CurrentDialog.GetStatement(statementId);

        if (target == null)
        {
            RebirthTraderDebug.Trace(
                "JOBS_ENTRY target statement missing '" +
                statementId + "'");
            return false;
        }

        group.CurrentDialog.CurrentStatement = target;
        group.RefreshDialog();

        RebirthTraderDebug.Trace(
            "JOBS_ENTRY entered statement='" +
            statementId + "'");

        return true;
    }

    public static void ClientListArrived(EntityTrader trader, EntityPlayer player)
    {
        if (trader == null || player == null)
            return;

        RebirthTraderDebug.Trace(
            "LIST_ARRIVED trader=" + trader.entityId +
            " player=" + player.entityId +
            " count=" + (trader.activeQuests != null ? trader.activeQuests.Count : -1) +
            " signature=" + BuildOfferSignature(trader.activeQuests) +
            " pendingTrader=" + pendingTraderId +
            " pendingPlayer=" + pendingPlayerId +
            " pendingStatement='" + pendingJobsStatement + "'");

        PendingListExpired();
        if (pendingJobsStatement.Length == 0 || pendingListRequestId == 0 ||
            trader.entityId != pendingTraderId || player.entityId != pendingPlayerId) return;
        if (pendingWorld == null || !ReferenceEquals(pendingWorld, GameManager.Instance != null ? GameManager.Instance.World : null))
        {
            ResetPendingListRequest();
            return;
        }

        ulong arrivedEpoch, arrivedRequest;
        if (!RebirthTraderListRequestCorrelation.TryReadOfferStamp(trader.activeQuests, out arrivedEpoch, out arrivedRequest) ||
            arrivedEpoch != pendingListEpoch || arrivedRequest != pendingListRequestId)
        {
            RebirthTraderDebug.Trace("LIST_ARRIVED ignored obsolete/unstamped list epoch=" + arrivedEpoch + " request=" + arrivedRequest +
                " expectedEpoch=" + pendingListEpoch + " expectedRequest=" + pendingListRequestId);
            return;
        }

        EntityPlayerLocal local = player as EntityPlayerLocal;
        LocalPlayerUI ui = local != null ? LocalPlayerUI.GetUIForPlayer(local) : null;
        XUiC_DialogWindowGroup group = ui != null && ui.xui != null ? ui.xui.Dialog.DialogWindowGroup : null;
        if (group == null || group.CurrentDialog == null || !ReferenceEquals(group.CurrentDialog, pendingDialog))
        {
            RebirthTraderDebug.Trace("LIST_ARRIVED dialog generation no longer matches request");
            ResetPendingListRequest();
            return;
        }

        Dialog dialog = group.CurrentDialog;
        DialogStatement target = dialog.GetStatement(pendingJobsStatement);
        if (target == null)
        {
            RebirthTraderDebug.Trace(
                "LIST_ARRIVED target statement missing '" + pendingJobsStatement + "'");
            return;
        }

        dialog.CurrentStatement = target;

        RebirthTraderDebug.Trace(
            "LIST_ARRIVED entering fresh statement='" + pendingJobsStatement + "'");

        ResetPendingListRequest();
        group.RefreshDialog();
    }

    private static int GetListIndex(DialogResponseQuest response)
    {
        if (response == null || response.Actions == null)
            return 0;

        for (int i = 0; i < response.Actions.Count; i++)
        {
            DialogActionAddQuest add = response.Actions[i] as DialogActionAddQuest;
            if (add != null)
                return add.ListIndex;
        }

        return 0;
    }

    private static DirectAcceptResult ValidateDirectAcceptance(
        EntityPlayerLocal player,
        Quest quest)
    {
        QuestJournal journal = player != null ? player.QuestJournal : null;
        if (journal == null || quest == null)
            return DirectAcceptResult.Stale;

        RebirthTraderOfferValidation snapshot =
            RebirthTraderOfferSnapshotService.Validate(journal, quest);

        if (snapshot != RebirthTraderOfferValidation.Allowed)
            return DirectAcceptResult.Stale;

        int tier = RebirthTraderPoiHistory.GetTier(quest);
        RebirthTraderPoiIdentity identity = RebirthTraderPoiIdentity.FromQuest(quest);

        if (identity.HasPoi &&
            RebirthTraderPoiHistory.Build(journal, tier)
                .IsActivePhysical(identity.PhysicalPoiKey))
            return DirectAcceptResult.RepeatPoi;

        if (!RebirthTraderJobPolicy.CanUseDailyQuestSlot(journal))
            return DirectAcceptResult.DailyLimit;

        int concurrentLimit = RebirthTraderJobPolicy.GetAcceptedJobLimit(
            tier,
            RebirthTraderJobPolicy.IsMultiplayerClient());

        if (RebirthTraderJobPolicy.CountOpenPersonalTraderJobs(journal) >= concurrentLimit)
            return DirectAcceptResult.ConcurrentLimit;

        return DirectAcceptResult.Allowed;
    }

    private static void ShowDirectAcceptFailure(
        EntityPlayerLocal player,
        DirectAcceptResult result,
        int tier)
    {
        if (player == null)
            return;

        if (result == DirectAcceptResult.RepeatPoi)
        {
            GameManager.ShowTooltip(
                player,
                Localization.Get("xuiRebirthRepeatPoiJobRejected"),
                string.Empty,
                "ui_denied");
            return;
        }

        if (result == DirectAcceptResult.DailyLimit ||
            result == DirectAcceptResult.ConcurrentLimit)
        {
            // Capacity is represented by the Jobs-entry visibility and bottom-right
            // Accepted Jobs counter. Do not emit a toolbelt/toast/sound notification.
            RebirthTraderDebug.Trace(
                "CARD_CLICK capacity rejection=" + result +
                " -> silent");
            return;
        }

        GameManager.ShowTooltip(
            player,
            Localization.Get("xuiRebirthTraderOfferStale"),
            string.Empty,
            "ui_denied");
    }

    private static string BuildOfferSignature(System.Collections.Generic.List<Quest> offers)
    {
        if (offers == null)
            return "<null>";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < offers.Count; i++)
        {
            Quest q = offers[i];
            if (q == null || q.QuestClass == null)
                continue;

            if (sb.Length > 0)
                sb.Append('|');

            sb.Append(q.QuestClass.ID);
            sb.Append('@');
            sb.Append(RebirthTraderPoiHistory.GetTier(q));
            sb.Append(':');
            sb.Append(SafePoi(q));
        }
        return sb.ToString();
    }

    private static string SafeResponse(DialogResponse response)
    {
        if (response == null)
            return "<null>";
        return (response.ID ?? "") + "/" + response.GetType().Name +
               "->" + (response.NextStatementID ?? "");
    }

    private static string SafePoi(Quest q)
    {
        if (q == null)
            return string.Empty;

        try
        {
            return q.GetPOIName() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

public static class RebirthTraderListRequestCorrelation
{
    private const string EpochKey = "RebirthTraderListEpoch";
    private const string RequestKey = "RebirthTraderListRequest";
    private sealed class Stamp { public ulong Epoch; public ulong Request; }
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, Stamp> PendingServer = new Dictionary<string, Stamp>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Stamp> ActiveServer = new Dictionary<string, Stamp>(StringComparer.Ordinal);

    public static void RegisterServerIntent(World world, int traderId, int playerId, ulong epoch, ulong request)
    {
        if (world == null || world.IsRemote() || epoch == 0 || request == 0) return;
        lock (Gate) PendingServer[Key(traderId, playerId)] = new Stamp { Epoch = epoch, Request = request };
    }

    public static void BeginServerGeneration(int traderId, int playerId)
    {
        string key = Key(traderId, playerId);
        lock (Gate)
        {
            Stamp stamp;
            if (PendingServer.TryGetValue(key, out stamp))
            {
                PendingServer.Remove(key);
                ActiveServer[key] = stamp;
            }
            else ActiveServer.Remove(key);
        }
    }

    public static void EndServerGeneration(int traderId, int playerId)
    { lock (Gate) ActiveServer.Remove(Key(traderId, playerId)); }

    public static void StampGeneratedOffers(int traderId, int playerId, IList<Quest> offers)
    {
        Stamp stamp;
        lock (Gate)
        {
            if (!ActiveServer.TryGetValue(Key(traderId, playerId), out stamp)) return;
        }
        for (int i = 0; offers != null && i < offers.Count; i++)
        {
            Quest quest = offers[i];
            if (quest == null) continue;
            if (quest.DataVariables == null) quest.DataVariables = new Dictionary<string, string>();
            quest.DataVariables[EpochKey] = stamp.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture);
            quest.DataVariables[RequestKey] = stamp.Request.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public static bool TryReadOfferStamp(IList<Quest> offers, out ulong epoch, out ulong request)
    {
        epoch = 0; request = 0;
        for (int i = 0; offers != null && i < offers.Count; i++)
        {
            Quest quest = offers[i]; string e, r;
            if (quest == null || quest.DataVariables == null || !quest.DataVariables.TryGetValue(EpochKey, out e) || !quest.DataVariables.TryGetValue(RequestKey, out r)) continue;
            ulong parsedEpoch, parsedRequest;
            if (!ulong.TryParse(e, out parsedEpoch) || !ulong.TryParse(r, out parsedRequest) || parsedEpoch == 0 || parsedRequest == 0) continue;
            if (epoch == 0) { epoch = parsedEpoch; request = parsedRequest; }
            else if (epoch != parsedEpoch || request != parsedRequest) { epoch = 0; request = 0; return false; }
        }
        return epoch != 0 && request != 0;
    }

    public static void ClearOfferStamp(Quest quest)
    {
        if (quest == null || quest.DataVariables == null) return;
        quest.DataVariables.Remove(EpochKey); quest.DataVariables.Remove(RequestKey);
    }

    public static void Reset()
    { lock (Gate) { PendingServer.Clear(); ActiveServer.Clear(); } }

    private static string Key(int traderId, int playerId) { return traderId + "|" + playerId; }
}

[Preserve]
public sealed class NetPackageRebirthTraderListIntent : NetPackage
{
    private int traderId;
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private ulong epoch;
    private ulong request;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthTraderListIntent Setup(int trader, int player, PlatformUserIdentifierAbs user, ulong requestEpoch, ulong requestId)
    { traderId = trader; playerId = player; userId = user; epoch = requestEpoch; request = requestId; return this; }
    public override void read(PooledBinaryReader reader)
    { BinaryReader b = (BinaryReader)reader; traderId = b.ReadInt32(); playerId = b.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(b); epoch = b.ReadUInt64(); request = b.ReadUInt64(); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); BinaryWriter b = (BinaryWriter)writer; b.Write(traderId); b.Write(playerId); userId.ToStream(b); b.Write(epoch); b.Write(request); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || epoch == 0 || request == 0 || !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        EntityTrader trader = world.GetEntity(traderId) as EntityTrader;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId);
        if (player == null || trader == null || persistent == null || persistent.PrimaryId == null || !persistent.PrimaryId.Equals(userId)) return;
        RebirthTraderListRequestCorrelation.RegisterServerIntent(world, traderId, playerId, epoch, request);
    }
    public int GetLength() => 0;
}

/// <summary>
/// This is the actual response-click method used by the 3.1 XUi.
/// Keeping the Harmony target on the CLASS is deliberate: REBIRTH's targeted
/// CreateClassProcessor installer is class-centric.
/// </summary>
[HarmonyPatch(typeof(XUiC_DialogResponseList), nameof(XUiC_DialogResponseList.OnPressResponse))]
public static class RebirthTraderResponseClickPatch
{
    [HarmonyPrefix]
    public static bool Prefix(
        XUiC_DialogResponseList __instance,
        XUiController _sender,
        int _mouseButton)
    {
        XUiC_DialogResponseEntry entry = _sender as XUiC_DialogResponseEntry;
        DialogResponse response = entry != null ? entry.CurrentResponse : null;

        RebirthTraderDebug.Trace(
            "RESPONSE_PRESS sender=" + (_sender != null ? _sender.GetType().Name : "<null>") +
            " response=" + (response != null ? response.ID : "<null>") +
            " responseType=" + (response != null ? response.GetType().Name : "<null>") +
            " hasRequirement=" + (entry != null && entry.HasRequirement) +
            " mouse=" + _mouseButton);

        // A job-card press only presents details; never use native acceptance as a fallback.
        if (RebirthTraderJobListRefresh.IsNormalTraderJobResponse(response) ||
            (entry is XUiC_RebirthTraderJobCard card && card.CardMode && response is DialogResponseQuest))
            return false;
        if (entry == null || response == null || !entry.HasRequirement)
            return true;

        EntityPlayerLocal player = __instance.xui != null &&
                                   __instance.xui.playerUI != null
            ? __instance.xui.playerUI.entityPlayer
            : null;

        if (player == null)
            return true;

        // Random-mode top-level Jobs entry.
        if (RebirthTraderJobListRefresh.IsJobsEntryResponse(response))
        {
            bool continueVanilla =
                RebirthTraderJobListRefresh.PrepareJobsEntry(__instance, response, player);

            RebirthTraderDebug.Trace(
                "RESPONSE_PRESS jobs-entry continueVanilla=" + continueVanilla);

            return continueVanilla;
        }

        return true;
    }
}

/// <summary>
/// Server side of a remote client's explicit Random-mode fresh-list request.
/// </summary>
[HarmonyPatch(typeof(NetPackageNPCQuestList), nameof(NetPackageNPCQuestList.ProcessPackage))]
public static class RebirthTraderRandomFetchServerPatch
{
    [HarmonyPrefix]
    public static void Prefix(NetPackageNPCQuestList __instance, out bool __state)
    {
        __state = false;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;

        if (__instance == null ||
            connection == null ||
            !connection.IsServer ||
            RebirthSandboxOptionManager.Current.TraderJobList != RebirthTraderJobListMode.Random ||
            __instance.eventType != NetPackageNPCQuestList.NPCQuestEventTypes.FetchList)
            return;

        RebirthTraderListRequestCorrelation.BeginServerGeneration(__instance.npcEntityID, __instance.playerEntityID);
        __state = true;
        RebirthTraderDebug.Trace(
            "FETCHLIST_SERVER fresh request trader=" + __instance.npcEntityID +
            " player=" + __instance.playerEntityID);

        QuestEventManager manager = QuestEventManager.Current;
        if (manager != null)
            manager.ClearQuestListForPlayer(
                __instance.npcEntityID,
                __instance.playerEntityID);
    }

    [HarmonyPostfix]
    public static void Postfix(NetPackageNPCQuestList __instance, bool __state)
    {
        if (__state && __instance != null)
            RebirthTraderListRequestCorrelation.EndServerGeneration(__instance.npcEntityID, __instance.playerEntityID);
    }
}

/// <summary>
/// Client resumes the deferred Random-mode Jobs navigation only after the fresh
/// authoritative trader list has actually arrived.
/// </summary>
[HarmonyPatch(typeof(EntityTrader), nameof(EntityTrader.SetActiveQuests))]
public static class RebirthTraderRandomClientListArrivedPatch
{
    [HarmonyPostfix]
    public static void Postfix(EntityTrader __instance, EntityPlayer player)
    {
        RebirthTraderJobListRefresh.ClientListArrived(__instance, player);
    }
}

/// <summary>
/// Diagnostic only. If a normal trader job still opens questOffer while trader debug
/// is enabled, this line proves the direct-card interception did not stop the vanilla path.
/// </summary>
#if DEBUG
[HarmonyPatch(
    typeof(XUiC_QuestOfferWindow),
    nameof(XUiC_QuestOfferWindow.OpenQuestOfferWindow),
    new Type[]
    {
        typeof(XUi),
        typeof(Quest),
        typeof(int),
        typeof(XUiC_QuestOfferWindow.OfferTypes),
        typeof(int),
        typeof(Action<EntityNPC>)
    })]
public static class RebirthTraderQuestOfferTracePatch
{
    [HarmonyPostfix]
    public static void Postfix(
        Quest q,
        int listIndex,
        XUiC_QuestOfferWindow.OfferTypes offerType,
        int questGiverID)
    {
        if (!RebirthTraderDebug.Enabled)
            return;

        string qid = q != null && q.QuestClass != null ? q.QuestClass.ID : "<null>";
        string qtype = q != null && q.QuestClass != null ? q.QuestClass.QuestType : "<null>";

        RebirthTraderDebug.Trace(
            "QUEST_OFFER_OPEN qid='" + qid +
            "' questType='" + qtype +
            "' listIndex=" + listIndex +
            " offerType=" + offerType +
            " giver=" + questGiverID);
    }
}
#endif
