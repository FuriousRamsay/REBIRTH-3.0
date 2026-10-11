using System;

#nullable disable

/// <summary>
/// REBIRTH dialog response layout owner.
///
/// v77 deliberately does NOT place responses inside an NGUI UIGrid. The 3.1 grid mutates
/// child transforms directly and was fighting the v76 card-board positions after the dialog
/// switched from ordinary text rows to trader jobs. Instead, windowResponses owns a plain
/// rect containing 24 response entries and this controller positions every entry explicitly.
/// Ordinary dialogue keeps the compact text-row presentation; trader quest statements use
/// fixed-size job cards with a compact Previous/Next/Nevermind command rail on the right.
/// </summary>
public sealed class XUiC_RebirthDialogResponseList : XUiC_DialogResponseList
{
    private const int StandardMaxRowsPerColumn = 8;
    private const int StandardMaxColumns = 3;
    private const int StandardCellWidth = 580;
    private const int StandardCellHeight = 40;
    private const int StandardHorizontalPadding = 18;
    private const int StandardHeaderHeight = 52;
    private const int StandardBottomPadding = 12;
    private const int StandardBottomMargin = 180;

    // Fixed job-card geometry. Card size is invariant for 5 / 11 / 17 jobs.
    private const int JobCardWidth = 294;
    private const int JobCardHeight = 320;
    private const int JobGap = 12;
    private const int JobBoardColumns = 6;
    private const int JobHorizontalPadding = 16;
    private const int JobHeaderHeight = 8;
    private const int JobBottomPadding = 12;
    private const int JobBottomMargin = 14;

    // Bottom-right rail: status strip + up to three compact commands.
    private const int JobStatusHeight = 38;
    private const int JobStatusGap = 8;
    private const int ActionHeight = 86;
    private const int ActionGap = 8;

    private XUiController backgroundController;
    private XUiController itemsController;
    private XUiController jobStatusController;
    private XUiV_Label jobStatusLabel;
    private int lastVisibleCount = -1;
    private int lastQuestCount = -1;
    private int lastActionCount = -1;
    private bool lastJobCardMode;
    private bool layoutInitialized;
    private int lastSemanticHash;
    private string statusFormat, statusText;
    private System.Globalization.CultureInfo statusCulture;
    private int statusAccepted, statusMaximum;

    public bool IsJobCardMode { get { return lastJobCardMode; } }

    public override void Init()
    {
        base.Init();
        backgroundController = GetChildById("background");
        itemsController = GetChildById("items");
        jobStatusController = GetChildById("jobAcceptanceStatus");
        if (jobStatusController != null)
        {
            XUiController statusText = jobStatusController.GetChildById("jobAcceptanceStatusText");
            jobStatusLabel = statusText != null ? statusText.ViewComponent as XUiV_Label : null;
        }
    }

    public override void OnOpen()
    {
        lastVisibleCount = -1;
        lastQuestCount = -1;
        lastActionCount = -1;
        layoutInitialized = false;
        lastSemanticHash = 0;
        base.OnOpen();
    }

    public override void Update(float _dt)
    {
        // Base remains authoritative for assigning CurrentResponse and click handling.
        base.Update(_dt);

        int visibleCount = 0;
        int questCount = 0;
        int actionCount = 0;
        int semanticHash = 17;

        for (int i = 0; i < entryList.Count; i++)
        {
            XUiC_DialogResponseEntry entry = entryList[i];
            if (entry == null || entry.CurrentResponse == null)
                continue;

            visibleCount++;
            semanticHash = unchecked(semanticHash * 31 + entry.CurrentResponse.GetType().FullName.GetHashCode());
            DialogResponseQuest semanticQuest = entry.CurrentResponse as DialogResponseQuest;
            if (semanticQuest != null && semanticQuest.Quest != null && semanticQuest.Quest.QuestClass != null)
            {
                semanticHash = unchecked(semanticHash * 31 + (semanticQuest.Quest.QuestClass.ID ?? string.Empty).GetHashCode());
                semanticHash = unchecked(semanticHash * 31 + semanticQuest.Quest.GetLocation().GetHashCode());
            }
            if (IsQuestResponse(entry.CurrentResponse))
                questCount++;
            else
                actionCount++;
        }

        bool jobCardMode = questCount > 0;

        if (jobCardMode)
            UpdateJobAcceptanceStatus();
        else
            SetJobStatusVisible(false);

        for (int i = 0; i < entryList.Count; i++)
        {
            XUiC_RebirthTraderJobCard card = entryList[i] as XUiC_RebirthTraderJobCard;
            if (card != null)
                card.SetCardMode(jobCardMode);
        }

        bool changed = !layoutInitialized ||
                       visibleCount != lastVisibleCount ||
                       questCount != lastQuestCount ||
                       actionCount != lastActionCount ||
                       jobCardMode != lastJobCardMode ||
                       semanticHash != lastSemanticHash;

        if (!changed)
            return;

        layoutInitialized = true;
        lastVisibleCount = visibleCount;
        lastQuestCount = questCount;
        lastActionCount = actionCount;
        lastJobCardMode = jobCardMode;
        lastSemanticHash = semanticHash;

        if (jobCardMode)
            ApplyJobCardLayout(questCount, actionCount);
        else
            ApplyStandardLayout(Math.Max(1, visibleCount));
    }

    private static bool IsQuestResponse(DialogResponse response)
    {
        DialogResponseQuest questResponse = response as DialogResponseQuest;
        return questResponse != null && questResponse.Quest != null;
    }

    private void ApplyJobCardLayout(int questCount, int actionCount)
    {
        int rows = GetJobRows(questCount);
        int contentWidth = JobBoardColumns * JobCardWidth + (JobBoardColumns - 1) * JobGap;
        int contentHeight = rows * JobCardHeight + Math.Max(0, rows - 1) * JobGap;
        int width = contentWidth + JobHorizontalPadding * 2;
        int height = JobHeaderHeight + contentHeight + JobBottomPadding;

        SetView(itemsController, new Vector2i(JobHorizontalPadding, -JobHeaderHeight),
            new Vector2i(contentWidth, contentHeight));

        if (backgroundController != null && backgroundController.ViewComponent != null)
        {
            backgroundController.ViewComponent.Size = new Vector2i(width, height);
            XUiV_Sprite sprite = backgroundController.ViewComponent as XUiV_Sprite;
            if (sprite != null)
            {
                sprite.SpriteName = "menu_empty";
                sprite.Color = new UnityEngine.Color32(0, 0, 0, 42);
            }
        }

        if (lblResponderName != null)
        {
            // Card mode replaces only the offer presentation. Do not drag the ordinary
            // respondent-name label into the centered board over the NPC model.
            lblResponderName.IsVisible = false;
        }

        if (ViewComponent != null)
        {
            ViewComponent.Size = new Vector2i(width, height);
            ViewComponent.Position = new Vector2i(-width / 2, height + JobBottomMargin);
            ViewComponent.TryUpdatePosition();
        }

        PositionJobEntries(rows, actionCount);
    }

    private void PositionJobEntries(int rows, int actionCount)
    {
        int questIndex = 0;
        int actionIndex = 0;

        // The final cell of the final row is permanently reserved for dialog actions.
        // Jobs fill row-major from the top-left, so 5/11/17 normal jobs naturally become:
        //   row 1: 5 jobs + actions
        //   rows 1-2: 6 jobs, then 5 jobs + actions
        //   rows 1-3: 6 jobs, 6 jobs, then 5 jobs + actions
        // This keeps Previous / Next / Nevermind anchored at the true bottom-right.
        int actionStackHeight = actionCount > 0
            ? actionCount * ActionHeight + Math.Max(0, actionCount - 1) * ActionGap
            : 0;
        int actionX = (JobBoardColumns - 1) * (JobCardWidth + JobGap);
        int bottomRowY = Math.Max(0, rows - 1) * (JobCardHeight + JobGap);

        // Status owns the top of the reserved bottom-right cell.
        SetView(
            jobStatusController,
            new Vector2i(actionX, -bottomRowY),
            new Vector2i(JobCardWidth, JobStatusHeight));
        SetJobStatusVisible(true);

        int actionAreaTop = bottomRowY + JobStatusHeight + JobStatusGap;
        int actionAreaHeight = Math.Max(0, JobCardHeight - JobStatusHeight - JobStatusGap);
        int actionStartY = actionAreaTop +
            Math.Max(0, actionAreaHeight - actionStackHeight);

        for (int i = 0; i < entryList.Count; i++)
        {
            XUiC_DialogResponseEntry entry = entryList[i];
            if (entry == null || entry.CurrentResponse == null || entry.ViewComponent == null)
                continue;

            if (IsQuestResponse(entry.CurrentResponse))
            {
                int row = questIndex / JobBoardColumns;
                int col = questIndex % JobBoardColumns;

                int x = col * (JobCardWidth + JobGap);
                int y = row * (JobCardHeight + JobGap);
                SetEntry(entry, x, -y, JobCardWidth, JobCardHeight);
                questIndex++;
            }
            else
            {
                int y = actionStartY + actionIndex * (ActionHeight + ActionGap);
                SetEntry(entry, actionX, -y, JobCardWidth, ActionHeight);
                actionIndex++;
            }
        }
    }

    private static int GetJobRows(int questCount)
    {
        // One cell is reserved for the action rail in the final row.
        questCount = Math.Max(1, questCount);
        return Math.Max(1, (questCount + 1 + JobBoardColumns - 1) / JobBoardColumns);
    }

    private void ApplyStandardLayout(int visibleCount)
    {
        SetJobStatusVisible(false);

        int columns = Math.Max(1, Math.Min(StandardMaxColumns,
            (visibleCount + StandardMaxRowsPerColumn - 1) / StandardMaxRowsPerColumn));
        int rows = Math.Max(1, (visibleCount + columns - 1) / columns);
        rows = Math.Min(StandardMaxRowsPerColumn, rows);

        int contentWidth = columns * StandardCellWidth;
        int contentHeight = rows * StandardCellHeight;
        int width = contentWidth + StandardHorizontalPadding * 2;
        int height = StandardHeaderHeight + contentHeight + StandardBottomPadding;

        SetView(itemsController, new Vector2i(StandardHorizontalPadding, -StandardHeaderHeight),
            new Vector2i(contentWidth, contentHeight));

        int visibleIndex = 0;
        for (int i = 0; i < entryList.Count; i++)
        {
            XUiC_DialogResponseEntry entry = entryList[i];
            if (entry == null || entry.CurrentResponse == null || entry.ViewComponent == null)
                continue;

            int col = visibleIndex / rows;
            int row = visibleIndex % rows;
            SetEntry(entry, col * StandardCellWidth, -row * StandardCellHeight,
                StandardCellWidth - 10, StandardCellHeight);
            visibleIndex++;
        }

        if (backgroundController != null && backgroundController.ViewComponent != null)
        {
            backgroundController.ViewComponent.Size = new Vector2i(width, height);
            XUiV_Sprite sprite = backgroundController.ViewComponent as XUiV_Sprite;
            if (sprite != null)
            {
                sprite.SpriteName = "ui_game_text_body_back";
                sprite.Color = UnityEngine.Color.white;
            }
        }

        if (lblResponderName != null)
        {
            lblResponderName.IsVisible = true;
            lblResponderName.Position = new Vector2i(StandardHorizontalPadding, -8);
            lblResponderName.Size = new Vector2i(width - StandardHorizontalPadding * 2, 40);
            lblResponderName.Alignment = NGUIText.Alignment.Left;
            lblResponderName.TryUpdatePosition();
        }

        if (ViewComponent != null)
        {
            ViewComponent.Size = new Vector2i(width, height);
            ViewComponent.Position = new Vector2i(-width / 2, height + StandardBottomMargin);
            ViewComponent.TryUpdatePosition();
        }
    }

    private void UpdateJobAcceptanceStatus()
    {
        if (jobStatusLabel == null || xui == null || xui.playerUI == null)
            return;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        QuestJournal journal = player != null ? player.QuestJournal : null;
        int tier = GetCurrentJobTier();

        if (journal == null || tier < 1)
        {
            jobStatusLabel.Text = string.Empty;
            return;
        }

        int dailyLimit = RebirthTraderJobPolicy.GetDailyQuestLimit();
        int tierLimit = RebirthTraderJobPolicy.GetAcceptedJobLimit(
            tier,
            RebirthTraderJobPolicy.IsMultiplayerClient());

        int effectiveMax =
            RebirthTraderJobPolicy.GetEffectiveAcceptedJobLimit(tier);

        // The numerator follows whichever cap is actually the lower one.
        // If the daily cap is the limiting cap, completed/reserved jobs today matter.
        // If the tier concurrent cap is lower, currently open accepted jobs matter.
        int accepted;
        if (dailyLimit != -1 && dailyLimit <= tierLimit)
            accepted = RebirthTraderJobPolicy.CountDailyReservedTraderJobs(journal);
        else
            accepted = RebirthTraderJobPolicy.CountOpenPersonalTraderJobs(journal);

        string format = Localization.Get("xuiRebirthAcceptedJobsStatus");
        if (string.IsNullOrEmpty(format) || format == "xuiRebirthAcceptedJobsStatus")
            format = "Accepted Jobs: {0} / {1}";

        // Keep all live policy/count reads; only reuse unchanged presentation text.
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        if (!culture.IsReadOnly || !ReferenceEquals(statusCulture, culture) || statusText == null || statusFormat != format || statusAccepted != accepted || statusMaximum != effectiveMax)
        {
            statusCulture = culture;
            statusFormat = format;
            statusAccepted = accepted;
            statusMaximum = effectiveMax;
            statusText = string.Format(format, accepted, effectiveMax);
        }
        if (jobStatusLabel.Text != statusText) jobStatusLabel.Text = statusText;
    }

    private int GetCurrentJobTier()
    {
        if (currentDialog != null &&
            currentDialog.CurrentStatement != null &&
            !string.IsNullOrEmpty(currentDialog.CurrentStatement.ID))
        {
            string id = currentDialog.CurrentStatement.ID;
            if (id.StartsWith("currentjobs", StringComparison.OrdinalIgnoreCase))
            {
                int parsed;
                if (int.TryParse(id.Substring("currentjobs".Length), out parsed))
                    return parsed;
            }
        }

        for (int i = 0; i < entryList.Count; i++)
        {
            DialogResponseQuest response = entryList[i] != null
                ? entryList[i].CurrentResponse as DialogResponseQuest
                : null;

            if (response != null && response.Quest != null)
                return RebirthTraderPoiHistory.GetTier(response.Quest);
        }

        return -1;
    }

    private void SetJobStatusVisible(bool visible)
    {
        if (jobStatusController != null && jobStatusController.ViewComponent != null)
            jobStatusController.ViewComponent.IsVisible = visible;
    }

    private static void SetView(XUiController controller, Vector2i position, Vector2i size)
    {
        if (controller == null || controller.ViewComponent == null)
            return;

        controller.ViewComponent.Size = size;
        controller.ViewComponent.Position = position;
        controller.ViewComponent.TryUpdatePosition();
    }

    private static void SetEntry(XUiC_DialogResponseEntry entry, int x, int y, int width, int height)
    {
        entry.ViewComponent.Size = new Vector2i(width, height);
        entry.ViewComponent.Position = new Vector2i(x, y);
        entry.ViewComponent.TryUpdatePosition();
    }
}
