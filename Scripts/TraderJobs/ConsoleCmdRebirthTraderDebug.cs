#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

#nullable disable

/// <summary>
/// Focused trader diagnostics. This implementation is compiled only when DEBUG is defined;
/// Release builds use RebirthTraderDebug.Release.cs as a no-op facade.
/// </summary>
public static class RebirthTraderDebug
{
    public static bool Enabled;

    public static void Trace(string message)
    {
        if (!Enabled)
            return;

        Log.Out("[REBIRTH TraderDebug] " + message);
    }

    public static void DumpStatus()
    {
        Log.Out("[REBIRTH TraderDebug] === STATUS BEGIN ===");
        Log.Out("[REBIRTH TraderDebug] enabled=" + Enabled);
        Log.Out("[REBIRTH TraderDebug] harmony=" + RebirthHarmonyBootstrap.BuildStatus());
        Log.Out(
            "[REBIRTH TraderDebug] infestedMode=" +
            RebirthInfestedJobsRuntimePolicy.Mode +
            " hideIdentity=" +
            RebirthInfestedJobsRuntimePolicy.HideIdentity);

        Log.Out(
            "[REBIRTH TraderDebug] jobRecoveryGrace=" +
            RebirthTraderQuestGraceManager.Enabled +
            " durationMinutes=" +
            RebirthTraderQuestGraceManager.GraceMinutes);

        Log.Out(
            "[REBIRTH TraderDebug] repeatPoiPolicy=" +
            RebirthTraderJobPolicy.RepeatPoiPolicy +
            " repeatWindows=T1:" +
            RebirthTraderPoiHistory.GetWindow(
                RebirthTraderJobPolicy.RepeatPoiPolicy, 1) +
            ",T2:" +
            RebirthTraderPoiHistory.GetWindow(
                RebirthTraderJobPolicy.RepeatPoiPolicy, 2) +
            ",T3:" +
            RebirthTraderPoiHistory.GetWindow(
                RebirthTraderJobPolicy.RepeatPoiPolicy, 3) +
            ",T4:" +
            RebirthTraderPoiHistory.GetWindow(
                RebirthTraderJobPolicy.RepeatPoiPolicy, 4) +
            ",T5:" +
            RebirthTraderPoiHistory.GetWindow(
                RebirthTraderJobPolicy.RepeatPoiPolicy, 5) +
            ",T6:" +
            RebirthTraderPoiHistory.GetWindow(
                RebirthTraderJobPolicy.RepeatPoiPolicy, 6));

        DumpPatch(
            "XUiC_DialogResponseList.OnPressResponse",
            AccessTools.Method(
                typeof(XUiC_DialogResponseList),
                nameof(XUiC_DialogResponseList.OnPressResponse)));

        DumpPatch(
            "NetPackageNPCQuestList.ProcessPackage",
            AccessTools.Method(
                typeof(NetPackageNPCQuestList),
                nameof(NetPackageNPCQuestList.ProcessPackage)));

        DumpPatch(
            "EntityTrader.SetActiveQuests",
            AccessTools.Method(
                typeof(EntityTrader),
                nameof(EntityTrader.SetActiveQuests)));

        DumpPatch(
            "XUiC_QuestOfferWindow.OpenQuestOfferWindow",
            AccessTools.Method(
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
                }));

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        LocalPlayerUI ui = player != null ? LocalPlayerUI.GetUIForPlayer(player) : null;
        EntityTrader trader = ui != null && ui.xui != null
            ? ui.xui.Dialog.Respondent as EntityTrader
            : null;

        string statement = string.Empty;
        if (ui != null &&
            ui.xui != null &&
            ui.xui.Dialog != null &&
            ui.xui.Dialog.DialogWindowGroup != null &&
            ui.xui.Dialog.DialogWindowGroup.CurrentDialog != null &&
            ui.xui.Dialog.DialogWindowGroup.CurrentDialog.CurrentStatement != null)
        {
            statement =
                ui.xui.Dialog.DialogWindowGroup.CurrentDialog.CurrentStatement.ID ?? string.Empty;
        }

        Log.Out(
            "[REBIRTH TraderDebug] runtime mode=" +
            RebirthSandboxOptionManager.Current.TraderJobList +
            " player=" + (player != null ? player.entityId.ToString() : "<none>") +
            " trader=" + (trader != null ? trader.entityId.ToString() : "<none>") +
            " statement='" + statement + "'" +
            " dialogOpen=" + (ui != null && ui.windowManager.IsWindowOpen("dialog")) +
            " questOfferOpen=" + (ui != null && ui.windowManager.IsWindowOpen("questOffer")));

        if (player != null)
        {
            QuestJournal journal = player.QuestJournal;
            int open = RebirthTraderJobPolicy.CountOpenPersonalTraderJobs(journal);
            int dailyUsed = RebirthTraderJobPolicy.CountDailyReservedTraderJobs(journal);
            int dailyLimit = RebirthTraderJobPolicy.GetDailyQuestLimit();

            Log.Out(
                "[REBIRTH TraderDebug] playerJobs open=" + open +
                " dailyUsed=" + dailyUsed +
                " dailyLimit=" + dailyLimit);

            Quest graceQuest =
                RebirthTraderQuestGraceManager.FindGraceQuest(journal);

            Log.Out(
                "[REBIRTH TraderDebug] grace quest=" +
                (graceQuest != null ? graceQuest.ID : "<none>") +
                " code=" +
                (graceQuest != null ? graceQuest.QuestCode.ToString() : "-") +
                " remainingSeconds=" +
                (graceQuest != null
                    ? RebirthTraderQuestGraceManager.GetRemainingSeconds(graceQuest)
                        .ToString("0.0")
                    : "0"));

            for (int tier = 1; tier <= 6; tier++)
            {
                int tierLimit = RebirthTraderJobPolicy.GetAcceptedJobLimit(
                    tier,
                    RebirthTraderJobPolicy.IsMultiplayerClient());

                int effectiveLimit =
                    RebirthTraderJobPolicy.GetEffectiveAcceptedJobLimit(tier);

                Log.Out(
                    "[REBIRTH TraderDebug] tier=" + tier +
                    " concurrentLimit=" + tierLimit +
                    " effectiveMax=" + effectiveLimit +
                    " canAccept=" +
                    RebirthTraderJobPolicy.CanAcceptAnotherJobAtTier(journal, tier));
            }
        }

        if (trader != null)
        {
            int count = trader.activeQuests != null ? trader.activeQuests.Count : -1;
            Log.Out("[REBIRTH TraderDebug] offers count=" + count);

            if (trader.activeQuests != null)
            {
                for (int i = 0; i < trader.activeQuests.Count; i++)
                {
                    Quest q = trader.activeQuests[i];
                    if (q == null || q.QuestClass == null)
                    {
                        Log.Out("[REBIRTH TraderDebug] offer[" + i + "]=<null>");
                        continue;
                    }

                    string poi = string.Empty;
                    try { poi = q.GetPOIName() ?? string.Empty; } catch { }

                    Log.Out(
                        "[REBIRTH TraderDebug] offer[" + i + "] id='" +
                        q.QuestClass.ID +
                        "' type='" + q.QuestClass.QuestType +
                        "' tier=" + RebirthTraderPoiHistory.GetTier(q) +
                        " poi='" + poi +
                        "' code=" + q.QuestCode);
                }
            }
        }

        Log.Out("[REBIRTH TraderDebug] === STATUS END ===");
    }

    private static void DumpPatch(string name, MethodBase method)
    {
        if (method == null)
        {
            Log.Out("[REBIRTH TraderDebug] patch " + name + " method=<missing>");
            return;
        }

        Patches patches = Harmony.GetPatchInfo(method);
        if (patches == null)
        {
            Log.Out("[REBIRTH TraderDebug] patch " + name + " owners=<none>");
            return;
        }

        List<string> owners = new List<string>();
        AddOwners(owners, patches.Prefixes);
        AddOwners(owners, patches.Postfixes);
        AddOwners(owners, patches.Transpilers);
        AddOwners(owners, patches.Finalizers);

        Log.Out(
            "[REBIRTH TraderDebug] patch " + name +
            " owners=" + (owners.Count > 0 ? string.Join(",", owners.ToArray()) : "<none>"));
    }

    private static void AddOwners(List<string> owners, IEnumerable<Patch> patches)
    {
        if (patches == null)
            return;

        foreach (Patch patch in patches)
        {
            if (patch == null || string.IsNullOrEmpty(patch.owner))
                continue;
            if (!owners.Contains(patch.owner))
                owners.Add(patch.owner);
        }
    }
}

public sealed class ConsoleCmdRebirthTraderDebug : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbtraderdebug", "rbtdbg" };
    }

    public override string getDescription()
    {
        return "Logs REBIRTH trader Jobs-entry, random-refresh and direct-card acceptance flow.";
    }

    public override string getHelp()
    {
        return "rbtraderdebug on|off|status\n" +
               "Recommended: rbtraderdebug on, reproduce the trader issue, then rbtraderdebug status.";
    }

    public override void Execute(List<string> args, CommandSenderInfo senderInfo)
    {
        string sub = args != null && args.Count > 0
            ? args[0].ToLowerInvariant()
            : "status";

        if (sub == "on")
        {
            RebirthTraderDebug.Enabled = true;
            Log.Out("[REBIRTH TraderDebug] enabled=True");
            RebirthTraderDebug.DumpStatus();
            return;
        }

        if (sub == "off")
        {
            Log.Out("[REBIRTH TraderDebug] enabled=False");
            RebirthTraderDebug.Enabled = false;
            return;
        }

        if (sub == "status" || sub == "dump")
        {
            RebirthTraderDebug.DumpStatus();
            return;
        }

        Log.Out(getHelp());
    }
}
#endif
