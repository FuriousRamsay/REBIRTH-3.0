using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

// Assignments tab (U4). One authoritative queue: the saved Work Board / Companion Desk rows plus the real
// work assignments behind them, read through the Desk's owner-authenticated endpoint. Every change made here
// goes through the same Desk operations as the Work Board, so both surfaces always show the same queue.
// Statuses are only what the server reports: no synthetic progress, and while the work release gate is
// closed every saved job is labelled held.
public sealed partial class XUiC_RebirthCompanions
{
    private const int AssignmentRowCount = 5;
    private bool assignmentsPending, assignmentsWired, assignmentOperationPending;
    private float assignmentsRequestedAt;
    private XElement assignmentsDesk;
    private string assignmentsForNpc = "";
    private string assignmentSelectedKey = "", assignmentFeedback = "";
    private int assignmentOffset;
    private string assignmentSnapSignature = "", assignmentsRequestedFor = "";
    // Finished physical rows the player removed from view (display only; the saved assignment is untouched).
    private readonly HashSet<string> assignmentHidden = new HashSet<string>();

    // Called from the window's Update while the Assignments tab is visible (RequestAssignments throttles itself),
    // and on window open to drop any state a lost reply could have left behind.
    internal void PollAssignments() { if (managementOpen && currentTab == Tab.Assignments) RequestAssignments(); }
    internal void ResetAssignmentsState()
    {
        assignmentOperationPending = false; assignmentsPending = false; assignmentsRequestedAt = -100f;
        assignmentsRequestedFor = ""; assignmentFeedback = "";
    }
    private float assignmentOperationAt;
    private List<AssignmentRow> assignmentRows = new List<AssignmentRow>();

    private sealed class AssignmentRow
    {
        public string Key, Id, Name, Target, State, Priority, Icon, Detail, Reports, Status, Rule = "", Kind = "", Def = "", Recipe = "";
        public bool Job, Blocked, DepositReady;
        public Color StateColor;
    }

    private static readonly Color AssignmentAmber = new Color(1f, .82f, .38f, 1f);
    private static readonly Color AssignmentRed = new Color(1f, .45f, .35f, 1f);
    private static readonly Color AssignmentGreen = new Color(.55f, .85f, .6f, 1f);

    private static string Atx(XElement e, string name) => RebirthCompanionDeskStore.Text(e, name);
    private static string Fit(string text, int max) => string.IsNullOrEmpty(text) || text.Length <= max ? text ?? "" : text.Substring(0, max - 1) + "…";
    private static string Ui(string key, string fallback) => RebirthSurvivorUiText.L(key, fallback);

    private bool SelectedIsHuman()
    {
        var entry = Selected();
        return entry != null && entry.Kind == RebirthCompanionTargetKind.Npc && !entry.IsDog && !entry.IsBoundUndead;
    }

    private void RequestAssignments()
    {
        // A different selected companion is requested at once; otherwise requests are throttled to one per 4 s.
        bool selectionChanged = assignmentsRequestedFor != selectedId;
        if (assignmentsPending && (selectionChanged || Time.unscaledTime - assignmentsRequestedAt > 10f)) assignmentsPending = false;
        if (!managementOpen || assignmentsPending || currentTab != Tab.Assignments ||
            (!selectionChanged && Time.unscaledTime - assignmentsRequestedAt < 4f)) return;
        EntityPlayerLocal player = xui?.playerUI?.entityPlayer;
        if (player == null) return;
        assignmentsPending = true;
        assignmentsRequestedAt = Time.unscaledTime;
        assignmentsRequestedFor = selectedId;
        string requestedNpc = selectedId;
        World requestedWorld = player.world;
        if (!XUiC_RebirthCompanionDesk.RequestReadOnly(player, requestedNpc, response =>
        {
            assignmentsPending = false;
            if (!managementOpen || !ReferenceEquals(requestedWorld, xui?.playerUI?.entityPlayer?.world)) return;
            // This data came from the Desk's owner-authenticated read endpoint.
            assignmentsDesk = response?.Element("desk") != null ? new XElement(response.Element("desk")) : null;
            assignmentsForNpc = requestedNpc;
            if (selectedId == requestedNpc) RenderAssignments();
        })) assignmentsPending = false;
    }

    private void EnsureAssignmentsWired()
    {
        if (assignmentsWired || panelAssignments == null) return;
        assignmentsWired = true;
        foreach (string label in new[] { "asgNpcName", "asgOrder", "asgBase", "assignmentsCurrent", "assignmentsNext", "asgSelName", "asgHint", "asgWaterPercent" })
            { var view = Label(label); if (view != null) view.OverflowEllipsis = true; }
        for (int i = 0; i < AssignmentRowCount; i++)
            foreach (string label in new[] { "asgName", "asgTarget", "asgRule", "asgState" })
                { var view = Label(label + i); if (view != null) view.OverflowEllipsis = true; }
        for (int i = 0; i < AssignmentRowCount; i++)
        {
            int n = i;
            Wire("asgRow" + i, () => { var row = assignmentRows.ElementAtOrDefault(assignmentOffset + n); assignmentSelectedKey = row?.Key ?? ""; RenderAssignments(); });
            ScrollOn("asgRow" + i);
        }
        ScrollOn("asgTrack");
        Wire("asgAdd", () => AssignmentsOpenDesk("add"));
        Wire("asgEdit", () => AssignmentsOpenDesk("edit"));
        Wire("asgRemove", () => AssignmentJobOperation("remove", ""));
        Wire("asgUp", () => AssignmentJobOperation("move", "up"));
        Wire("asgDown", () => AssignmentJobOperation("move", "down"));
        Wire("asgDefineTarget", () => AssignmentsOpenDesk("target"));
        Wire("asgPickStorage", BeginStoragePick);
        Wire("asgResume", ResumeBaseWork);
        Wire("asgViewReport", () => AssignmentsOpenDesk("reports"));
        Wire("asgCtxA", () => ContextAction(0));
        Wire("asgCtxB", () => ContextAction(1));
        Wire("asgWaterMinus", () => ChangeWaterMinimum(-5));
        Wire("asgWaterPlus", () => ChangeWaterMinimum(5));
        Wire("asgWaterAuto", ToggleWaterAuto);
        var thumb = GetChildById("asgThumb");
        if (thumb?.ViewComponent != null)
        {
            thumb.ViewComponent.EventOnDrag = true;
            float remainder = 0;
            thumb.OnDrag += delegate (XUiController sender, EDragType type, Vector2 delta)
            {
                if (type == EDragType.DragStart) remainder = 0;
                if (type == EDragType.DragEnd) return;
                remainder -= delta.y;
                int step = (int)(remainder / 12f);
                if (step != 0) { remainder -= step * 12f; ScrollAssignments(step); }
            };
        }
    }

    private void ScrollOn(string id)
    {
        var c = GetChildById(id);
        if (c?.ViewComponent == null) return;
        c.ViewComponent.EventOnScroll = true;
        c.OnScroll += delegate (XUiController sender, float delta) { ScrollAssignments(delta > 0 ? -1 : 1); };
    }

    private void ScrollAssignments(int delta)
    {
        int max = Math.Max(0, assignmentRows.Count - AssignmentRowCount);
        int next = Math.Max(0, Math.Min(max, assignmentOffset + delta));
        if (next == assignmentOffset) return;
        assignmentOffset = next;
        RenderAssignments();
    }

    private List<AssignmentRow> BuildAssignmentRows()
    {
        var rows = new List<AssignmentRow>();
        if (assignmentsDesk == null || string.IsNullOrEmpty(selectedId) || !SelectedIsHuman()) return rows;
        var reports = assignmentsDesk.Elements("report").Where(r => Atx(r, "npc") == selectedId && Atx(r, "state") == "Unresolved").ToArray();
        foreach (var job in assignmentsDesk.Elements("job").Where(j => Atx(j, "npc") == selectedId)
            .OrderBy(j => (int?)j.Attribute("priority") ?? 1))
        {
            string id = Atx(job, "id"), kind = Atx(job, "kind");
            var target = assignmentsDesk.Elements("target").FirstOrDefault(t => Atx(t, "id") == Atx(job, "target"));
            var own = reports.Where(r => Atx(r, "job") == id).ToArray();
            bool blocked = own.Any(r => Atx(r, "condition") != "releaseHold");
            // The release gate is closed: a saved job is a plan. Never present it as ready or running.
            bool held = !RebirthNpcWorkReleaseGate.Enabled || own.Any(r => Atx(r, "condition") == "releaseHold");
            var detail = new StringBuilder();
            if (kind == "Craft")
            {
                string recipe = Localization.Get(Atx(job, "recipe"));
                detail.Append(string.Format(Ui("xuiRbNpcUiCraftQuantity", "{0} x {1}"), string.IsNullOrEmpty(recipe) ? Atx(job, "recipe") : recipe, Atx(job, "quantity")))
                    .Append("  ").Append(RebirthCompanionDeskStore.L("Mode" + Atx(job, "mode"))).Append((char)10);
            }
            else
                foreach (string field in new[] { "meals", "drinks", "limit", "fallback", "tag", "preserve", "merge", "sort" })
                    if (Atx(job, field) == "true") detail.Append("- ").Append(RebirthCompanionDeskStore.L(field)).Append('\n');
            rows.Add(new AssignmentRow
            {
                Key = "job:" + id, Id = id, Job = true, Blocked = blocked, Priority = Atx(job, "priority"), Icon = IconFor(kind),
                Name = RebirthCompanionDeskStore.L(kind), Target = target != null ? Atx(target, "name") : "-",
                State = blocked ? Ui("xuiRbNpcUiStateBlocked", "Blocked") : held ? Ui("xuiRbNpcUiStateHeld", "Held") : Ui("xuiRbNpcUiStateQueued", "Queued"),
                StateColor = blocked ? AssignmentRed : held ? AssignmentAmber : AssignmentGreen,
                Detail = detail.ToString(), Status = "",
                Rule = RuleFor(job, kind), Kind = kind, Recipe = Atx(job, "recipe"),
                Reports = string.Join("\n", own.Select(r => ReportTitle(r) + (ReportDetail(r).Length > 0 && ReportDetail(r) != ReportTitle(r) ? ": " + ReportDetail(r) : "")).ToArray())
            });
        }
        var work = new List<AssignmentRow>();
        // Physical work rows are projected for the requested worker only; never show another worker's.
        if (assignmentsForNpc != selectedId) return rows;
        foreach (var crop in assignmentsDesk.Elements("cropAssignment").OrderByDescending(c => (ulong?)c.Attribute("id") ?? 0UL))
            work.Add(WorkRow("crop:" + Atx(crop, "id"), "crop", RebirthCompanionDeskStore.L("SingleCropStatus"), crop));
        foreach (var item in assignmentsDesk.Elements("workAssignment").OrderByDescending(c => (ulong?)c.Attribute("id") ?? 0UL))
            work.Add(WorkRow("work:" + Atx(item, "id"), Atx(item, "def"),
                WorkName(Atx(item, "def")), item));
        // Live work first, then only the latest finished one of each kind so history does not bury the queue.
        var seen = new HashSet<string>();
        foreach (var row in work.OrderBy(r => IsTerminal(r) ? 1 : 0))
        {
            // Unclaimed finished output stays visible; other finished rows keep only the latest per definition and target.
            if (assignmentHidden.Contains(row.Key)) continue;
            if (IsTerminal(row) && !row.DepositReady && !seen.Add(row.Def + "|" + row.Target)) continue;
            rows.Add(row);
        }
        return rows;
    }

    private static bool IsTerminal(AssignmentRow row) =>
        !row.Job && (row.Status == "Completed" || row.Status == "Cancelled" || row.Status == "Failed");

    private static string WorkName(string def) =>
        def == RebirthP8NpcWaterRefillWork.DefinitionId ? RebirthCompanionDeskStore.L("WaterJob") :
        def == RebirthNpcStorageOrganizationWork.DefinitionId ? RebirthCompanionDeskStore.L("B09Job") :
        def == RebirthNpcRecipeStagingWork.DefinitionId ? RebirthCompanionDeskStore.L("CraftStageJob") :
        def == RebirthNpcRecipeQueueWork.DefinitionId ? RebirthCompanionDeskStore.L("CraftQueueJob") :
        Ui("xuiRbNpcUiOtherAssignment", "Other assignment");

    private AssignmentRow WorkRow(string key, string kindKey, string name, XElement source)
    {
        string status = Atx(source, "status");
        string coordinates = Atx(source, "pos") == "false" ? "-" : Atx(source, "x") + ", " + Atx(source, "y") + ", " + Atx(source, "z");
        return new AssignmentRow
        {
            Key = key, Id = Atx(source, "id"), Job = false, Priority = "", Icon = IconFor(kindKey), Name = name, Target = coordinates,
            Status = status, State = RebirthCompanionDeskStore.L("WorkStatus" + status),
            StateColor = status == "Failed" ? AssignmentRed : status == "Executing" ? AssignmentGreen : AssignmentAmber,
            Detail = Atx(source, "detail"), Reports = "", Def = Atx(source, "def").Length > 0 ? Atx(source, "def") : kindKey, DepositReady = Atx(source, "deposit") == "true"
        };
    }

    // The Rule column carries only what the saved job or policy holds; blank when there is nothing to say.
    private string RuleFor(XElement job, string kind)
    {
        switch (kind)
        {
            case "Craft":
                return RebirthCompanionDeskStore.L("Mode" + Atx(job, "mode")) + " " + Atx(job, "quantity");
            case "Food":
                return string.Format(Ui("xuiRbNpcUiRuleFood", "Meals {0} / Drinks {1}"),
                    Atx(job, "meals") == "true" ? Atx(job, "mealCount") : "-", Atx(job, "drinks") == "true" ? Atx(job, "drinkCount") : "-");
            case "Farm":
                return DeskIsCurrent() && Atx(assignmentsDesk, "waterPercent").Length > 0
                    ? string.Format(Ui("xuiRbNpcUiWaterRule", "Water min. {0}%"), Atx(assignmentsDesk, "waterPercent")) : "";
            case "Organize":
                return string.Join(", ", new[] { "tag", "merge", "sort", "preserve" }.Where(field => Atx(job, field) == "true")
                    .Select(field => RebirthCompanionDeskStore.L(field)).ToArray());
            default:
                return "";
        }
    }

    // The Desk stores the hold report already localized; recognise the hold by its condition so the text follows the current language.
    private static string ReportTitle(XElement report) => Atx(report, "condition") == "releaseHold" ? RebirthCompanionDeskStore.L("ExecutionHeld") : Atx(report, "title");
    private static string ReportDetail(XElement report) => Atx(report, "condition") == "releaseHold" ? RebirthCompanionDeskStore.L("ExecutionHeld") : Atx(report, "detail");

    // Repair rows carry no job attribute (P9, per worker); N10 rows name their job id.
    private string ShortfallText(string jobId = null)
    {
        var lines = !DeskIsCurrent() ? null : assignmentsDesk.Elements("shortfall")
            .Where(e => jobId == null ? e.Attribute("job") == null : Atx(e, "job") == jobId)
            .Select(e => e.Value).Where(v => v.Length > 0).Take(3).ToArray();
        return lines == null || lines.Length == 0 ? "" : "\n" + string.Join("\n", lines);
    }

    private static string IconFor(string kind)
    {
        switch (kind)
        {
            case "Craft": return "ui_game_symbol_workbench";
            case "Food": return "ui_game_symbol_campfire";
            case "Farm": case "crop": return "ui_game_symbol_crops";
            case "Repair": return "ui_game_symbol_wrench";
            default: return kind == RebirthP8NpcWaterRefillWork.DefinitionId ? "ui_game_symbol_water" : "ui_game_symbol_backpack";
        }
    }

    private void SetHost(string id, bool visible) => SetVisible(GetChildById(id + "Host") ?? GetChildById(id), visible);
    private void SetText(string id, string value) => Set(Label(id), value);

    private void RenderAssignments()
    {
        if (panelAssignments == null) return;
        EnsureAssignmentsWired();
        var entry = Selected();
        bool human = SelectedIsHuman();
        SetText("asgNpcName", entry?.Name ?? "");
        SetText("asgOrder", entry?.Status ?? "");
        SetHost("asgCtxA", false); SetHost("asgCtxB", false);
        // Dogs and drones never show the human-only job controls.
        foreach (string id in new[] { "asgAdd", "asgEdit", "asgRemove", "asgUp", "asgDown", "btnAssignmentsBoard", "asgDefineTarget", "asgPickStorage", "asgResume", "asgViewReport", "asgWaterMinus", "asgWaterPlus", "asgWaterAuto" })
            SetHost(id, human);
        SetText("asgWaterPercent", human && assignmentsDesk != null && !DeskIsCurrent() ? Ui("xuiRbNpcUiLoading", "Loading...") :
            human && assignmentsDesk != null
            ? string.Format(Ui("xuiRbNpcUiWaterMinimum", "Farm water minimum: {0}%"), Atx(assignmentsDesk, "waterPercent").Length > 0 ? Atx(assignmentsDesk, "waterPercent") : "50") : "");
        SetText("asgWaterAutoLabel", human && assignmentsDesk != null ? WaterAutoLabel() : "");
        if (!human || assignmentsDesk == null)
        {
            ClearAssignmentRows();
            Set(assignmentsCurrent, ""); Set(assignmentsNext, ""); Set(assignmentsBlocked, "");
            SetText("asgBase", ""); SetText("asgSelName", ""); SetText("asgSelPriority", ""); SetText("asgHint", "");
            Set(assignmentsText, entry == null ? "" : !human ? Ui("xuiRbNpcUiAssignmentsHumanOnly", "Only survivor companions have work assignments.")
                : Ui("xuiRbNpcUiAssignmentsUnavailable", "Saved assignments are unavailable."));
            return;
        }
        assignmentRows = BuildAssignmentRows();
        if (!assignmentRows.Any(r => r.Key == assignmentSelectedKey)) assignmentSelectedKey = assignmentRows.FirstOrDefault()?.Key ?? "";
        int selectedIndex = assignmentRows.FindIndex(r => r.Key == assignmentSelectedKey);
        int max = Math.Max(0, assignmentRows.Count - AssignmentRowCount);
        assignmentOffset = Math.Max(0, Math.Min(max, assignmentOffset));
        // Bring the selection into view only when the selection or the row list changed, so wheel and thumb can scroll past it.
        string snapSignature = assignmentSelectedKey + "|" + assignmentRows.Count;
        if (snapSignature != assignmentSnapSignature)
        {
            assignmentSnapSignature = snapSignature;
            if (selectedIndex >= 0 && selectedIndex < assignmentOffset) assignmentOffset = selectedIndex;
            if (selectedIndex >= assignmentOffset + AssignmentRowCount) assignmentOffset = Math.Min(max, selectedIndex - AssignmentRowCount + 1);
        }

        for (int i = 0; i < AssignmentRowCount; i++)
        {
            var row = assignmentRows.ElementAtOrDefault(assignmentOffset + i);
            SetVisible(GetChildById("asgRow" + i + "Host"), row != null);
            SetVisible(GetChildById("asgSel" + i), row != null && row.Key == assignmentSelectedKey);
            SetText("asgPri" + i, row?.Priority ?? "");
            SetText("asgName" + i, row?.Name ?? "");
            SetText("asgTarget" + i, row?.Target ?? "");
            SetText("asgRule" + i, row?.Rule ?? "");
            var state = Label("asgState" + i);
            if (state != null && row != null) { state.Text = row.State; state.Color = row.StateColor; }
            var icon = GetChildById("asgIcon" + i)?.ViewComponent as XUiV_Sprite;
            if (icon != null && row != null) { icon.UIAtlas = "UIAtlas"; icon.SetSpriteImmediately(row.Icon); }
        }
        // The scrollbar exists only while the queue does not fit.
        SetVisible(GetChildById("asgTrack"), assignmentRows.Count > AssignmentRowCount);
        RebirthScrollbarPresentation.RenderThumb(GetChildById("asgThumb")?.ViewComponent, 360, AssignmentRowCount, assignmentRows.Count, assignmentOffset, 14);

        // Summary strip. "Held" is the truthful base-work state while the release gate is closed.
        bool following = entry.Order == "Follow";
        bool gate = RebirthNpcWorkReleaseGate.Enabled;
        // "Held" applies to saved jobs; a physical assignment that reports Executing is shown as working.
        bool working = assignmentRows.Any(r => !r.Job && r.Status == "Executing");
        SetText("asgBase", working ? Ui("xuiRbNpcUiBaseWorking", "Working on an assignment")
            : !gate ? Ui("xuiRbNpcUiBaseHeld", "Work execution held")
            : following ? Ui("xuiRbNpcUiBasePaused", "Paused while following") : Ui("xuiRbNpcUiBaseFree", "Base work available"));
        var active = assignmentRows.FirstOrDefault(r => !r.Job && r.Status == "Executing") ??
            assignmentRows.FirstOrDefault(r => !r.Job && !IsTerminal(r));
        Set(assignmentsCurrent, active != null ? active.Name + " - " + active.State
            : following ? Ui("xuiRbNpcUiJobsRetained", "Assigned jobs are retained.") : RebirthCompanionDeskStore.L("NoActiveAssignment"));
        var next = assignmentRows.FirstOrDefault(r => r.Job && !r.Blocked);
        Set(assignmentsNext, next != null ? next.Name + " - " + next.State :
            assignmentRows.Any(r => r.Job) ? assignmentRows.First(r => r.Job).Name + " - " + Ui("xuiRbNpcUiStateBlocked", "Blocked") : Ui("xuiRbNpcUiNoAssignments", "No saved assignments."));

        var selected = selectedIndex >= 0 ? assignmentRows[selectedIndex] : null;
        SetText("asgSelName", selected?.Name ?? "");
        SetText("asgSelPriority", selected != null && selected.Job ? Ui("xuiRbNpcUiPriority", "Priority") + " " + selected.Priority : "");
        if (selected == null)
        {
            Set(assignmentsText, assignmentRows.Count == 0 ? Ui("xuiRbNpcUiNoAssignments", "No saved assignments.") : "");
            Set(assignmentsBlocked, "");
        }
        else
        {
            Set(assignmentsText, Ui("xuiRbNpcUiTarget", "Target") + ": " + selected.Target + "\n" + selected.State + "\n" +
                Fit(selected.Detail.TrimEnd('\n'), 260) + (selected.Job && !gate ? "\n" + RebirthCompanionDeskStore.L("ExecutionHeld") : "") +
                (selected.Job && selected.Kind == "Craft" && !RebirthCompanionDeskStore.CraftPickupAvailable ?
                    "\n" + Ui("xuiRbNpcUiCraftHeld", "Physical craft is held: collecting the finished item is not enabled yet.") : ""));
            Set(assignmentsBlocked, selected.Job ? Fit(selected.Reports + (selected.Kind == "Repair" ? ShortfallText() : ShortfallText(selected.Id)), 240) : "");
        }
        RenderContextActions(selected);
        SetText("asgHint", assignmentFeedback.Length > 0 ? assignmentFeedback :
            WaterAutoStatus().Length > 0 ? WaterAutoStatus() :
            gate ? Ui("xuiRbNpcUiBlockedStays", "A blocked job stays in the queue; the next feasible job can run.") :
            selected == null || selected.Job ? RebirthCompanionDeskStore.L("ExecutionHeld") : "");
    }

    private void ClearAssignmentRows()
    {
        assignmentRows = new List<AssignmentRow>();
        for (int i = 0; i < AssignmentRowCount; i++) SetVisible(GetChildById("asgRow" + i + "Host"), false);
        SetVisible(GetChildById("asgTrack"), false);
        RebirthScrollbarPresentation.RenderThumb(GetChildById("asgThumb")?.ViewComponent, 360, AssignmentRowCount, 0, 0, 14);
    }

    // Contextual actions for the selected row. Only real backend entry points; anything unavailable stays hidden.
    private string assignmentPreview = "", assignmentPreviewKey = "";

    private void RenderContextActions(AssignmentRow row)
    {
        string a = "", b = "";
        if (row != null && row.Job && row.Kind == "Organize") { a = "xuiRbNpcUiPlanOrganize"; b = "xuiRbNpcUiStartStep"; }
        // Physical Craft stays hidden (and labelled held in the detail text) while the P7 pickup leg is held.
        else if (row != null && row.Job && row.Kind == "Craft") a = RebirthCompanionDeskStore.CraftPickupAvailable ? "xuiRbNpcUiPhysicalCraft" : "";
        else if (row != null && !row.Job && row.Def == RebirthNpcRecipeStagingWork.DefinitionId && row.Status == "Completed")
            a = RebirthCompanionDeskStore.CraftPickupAvailable ? "xuiRbNpcUiQueueAtStation" : "";
        else if (row != null && !row.Job && row.DepositReady) a = "xuiRbNpcUiDepositOutput";
        SetHost("asgCtxA", a.Length > 0);
        SetHost("asgCtxB", b.Length > 0);
        SetText("asgCtxALabel", a.Length > 0 ? Ui(a, a) : "");
        SetText("asgCtxBLabel", b.Length > 0 ? Ui(b, b) : "");
        if (row != null && assignmentPreviewKey == row.Key && assignmentPreview.Length > 0) Set(assignmentsText, assignmentPreview);
    }

    private void ContextAction(int which)
    {
        var row = SelectedAssignmentRow();
        if (row == null || !BeginAssignmentOperation(out var player)) return;
        string npc = selectedId;
        if (row.Job && row.Kind == "Organize")
        {
            RunContextOperation(player, which == 0 ? "b09plan" : "b09step",
                new XElement("command", new XAttribute("npc", npc), new XAttribute("job", row.Id)), row.Key);
        }
        else if (row.Job && row.Kind == "Craft")
        {
            string recipe = row.Recipe;
            RunContextOperation(player, "craftCheck", new XElement("command", new XAttribute("npc", npc), new XAttribute("recipe", recipe)), row.Key, ok =>
            {
                if (!ok || !managementOpen) return;
                // Eligible: choose the workstation block in the world, then the ingredient cell in a named container.
                Close();
                RebirthCompanionWorldSelection.Begin(player, position =>
                {
                    if (position.HasValue) RebirthCompanionB09Selection.BeginCraft(player, npc, recipe, position.Value);
                });
            });
        }
        else if (!row.Job && row.Def == RebirthNpcRecipeStagingWork.DefinitionId)
            RunContextOperation(player, "craftQueue", new XElement("command", new XAttribute("npc", npc), new XAttribute("assignment", row.Id)), row.Key);
        else if (!row.Job && row.DepositReady)
        {
            RebirthCompanionB09Selection.BeginDeposit(player, npc, row.Id);
            Close();
        }
    }

    private void RunContextOperation(EntityPlayerLocal player, string operation, XElement payload, string key, Action<bool> done = null)
    {
        string npc = selectedId;
        assignmentOperationPending = true;
        if (!XUiC_RebirthCompanionDesk.RequestOperation(player, operation, payload, 0, response =>
        {
            FinishAssignmentOperation(npc, response, false);
            assignmentPreviewKey = key;
            assignmentPreview = BuildPlanPreview(response?.Element("b09plan"));
            if (selectedId == npc) RenderAssignments();
            done?.Invoke(Atx(response, "ok") == "true");
        })) assignmentOperationPending = false;
    }

    // Steps and held items exactly as the planner returned them; each held item carries its real reason.
    private static string BuildPlanPreview(XElement plan)
    {
        if (plan == null) return "";
        var text = new StringBuilder();
        int shown = 0;
        foreach (var step in plan.Elements("step"))
        {
            if (shown++ >= 3) break;
            text.Append(string.Format(Ui("xuiRbNpcUiPlanStep", "Move {0} ({1})"), Atx(step, "item"), RebirthCompanionDeskStore.L("B09Rule" + Atx(step, "rule")))).Append((char)10);
        }
        foreach (var held in plan.Elements("held"))
        {
            if (shown++ >= 4) break;
            text.Append(string.Format(Ui("xuiRbNpcUiPlanHeld", "Held {0}: {1}"), Atx(held, "item"), RebirthCompanionDeskStore.L("B09Hold" + Atx(held, "reason")))).Append((char)10);
        }
        int total = plan.Elements("step").Count() + plan.Elements("held").Count();
        if (total > shown) text.Append("...");
        return text.ToString().TrimEnd((char)10);
    }

    private AssignmentRow SelectedAssignmentRow() => assignmentRows.FirstOrDefault(r => r.Key == assignmentSelectedKey);

    private bool BeginAssignmentOperation(out EntityPlayerLocal player)
    {
        player = xui?.playerUI?.entityPlayer;
        // A reply can be lost (epoch change, world change); never leave the buttons dead for longer than 8 s.
        if (assignmentOperationPending && Time.unscaledTime - assignmentOperationAt > 8f) assignmentOperationPending = false;
        bool allowed = player != null && !assignmentOperationPending && SelectedIsHuman();
        if (allowed) assignmentOperationAt = Time.unscaledTime;
        return allowed;
    }

    private void FinishAssignmentOperation(string npc, XElement response, bool render = true)
    {
        assignmentOperationPending = false;
        if (!managementOpen) return;
        string error = Atx(response, "error");
        assignmentFeedback = error.Length > 0 ? error : Atx(response, "message");
        if (error.Length == 0 && response.Element("desk") != null) { assignmentsDesk = new XElement(response.Element("desk")); assignmentsForNpc = npc; }
        else assignmentsRequestedAt = -100f;
        if (render && selectedId == npc) RenderAssignments();
    }

    // Remove and priority use the Work Board's own revision-checked operations.
    private void AssignmentJobOperation(string operation, string direction)
    {
        var row = SelectedAssignmentRow();
        if (row != null && !row.Job && operation == "remove" && IsTerminal(row) && !row.DepositReady)
        {
            // A finished physical row has nothing left to cancel: just hide it from this view.
            assignmentHidden.Add(row.Key);
            RenderAssignments();
            return;
        }
        if (row == null || (!row.Job && operation != "remove") || !BeginAssignmentOperation(out var player)) return;
        string npc = selectedId;
        assignmentOperationPending = true;
        long revision = assignmentsDesk != null ? RebirthCompanionDeskStore.Revision(assignmentsDesk) : 0;
        // Physical assignments are cancelled server side only when no original custody is retained.
        bool physical = !row.Job;
        var payload = physical ? new XElement("command", new XAttribute("npc", npc), new XAttribute("assignment", row.Id))
            : new XElement("command", new XAttribute("npc", npc), new XAttribute("id", row.Id), new XAttribute("direction", direction));
        if (!XUiC_RebirthCompanionDesk.RequestOperation(player, physical ? "cancelAssignment" : operation, payload,
            revision, response => FinishAssignmentOperation(npc, response))) assignmentOperationPending = false;
    }

    // Releases the Follow override only; saved jobs, assignments and reports are not touched.
    private void ResumeBaseWork()
    {
        if (!BeginAssignmentOperation(out var player)) return;
        string npc = selectedId;
        assignmentOperationPending = true;
        if (!XUiC_RebirthCompanionDesk.RequestOperation(player, "resumeBase", new XElement("command", new XAttribute("npc", npc)), 0, response =>
        {
            FinishAssignmentOperation(npc, response);
            RefreshSnapshot(true);
        })) assignmentOperationPending = false;
    }

    // Per-worker values (water minimum, auto flag, shortfall) are only valid for the worker the last reply was for.
    private bool DeskIsCurrent() => assignmentsDesk != null && assignmentsForNpc == selectedId;

    private string[] FarmTargetIds() => !DeskIsCurrent() ? new string[0] :
        assignmentsDesk.Elements("target").Where(t => Atx(t, "kind") == "Farm").Select(t => Atx(t, "id")).ToArray();

    private bool WaterAutoEnabled() => DeskIsCurrent() && Atx(assignmentsDesk, "waterEnabled") == "1";

    private string WaterAutoLabel() => !DeskIsCurrent() ? Ui("xuiRbNpcUiLoading", "Loading...") : FarmTargetIds().Length == 0
        ? Ui("xuiRbNpcUiWaterAutoNoFarm", "Define a Farm target first")
        : (WaterAutoEnabled() ? "[x] " : "[ ] ") + Ui("xuiRbNpcUiWaterAuto", "Auto-maintain water");

    // The maintainer's own status text, shown only while the standing maintenance is on.
    private string WaterAutoStatus() => WaterAutoEnabled() && Atx(assignmentsDesk, "waterStatus").Length > 0
        ? Ui("xuiRbNpcUiWaterAutoStatus", "Water") + ": " + Atx(assignmentsDesk, "waterStatus") : "";

    private string SavedOrFirstFarm(string[] farms)
    {
        string[] parts = Atx(assignmentsDesk, "waterFarm").Split(':');
        return parts.Length == 3 && Array.IndexOf(farms, parts[1]) >= 0 ? parts[1] : farms[0];
    }

    private void ToggleWaterAuto()
    {
        if (!DeskIsCurrent() || !SelectedIsHuman()) return;
        var farms = FarmTargetIds();
        if (farms.Length == 0) { assignmentFeedback = Ui("xuiRbNpcUiWaterAutoNoFarm", "Define a Farm target first"); RenderAssignments(); return; }
        if (!BeginAssignmentOperation(out var player)) return;
        string npc = selectedId;
        assignmentOperationPending = true;
        // The server keeps the worker's saved Farm target when it is still defined, otherwise it uses the first one.
        if (!XUiC_RebirthCompanionDesk.RequestOperation(player, "waterAuto",
            new XElement("command", new XAttribute("npc", npc), new XAttribute("enabled", WaterAutoEnabled() ? "0" : "1"), new XAttribute("target", SavedOrFirstFarm(farms))), 0,
            response => FinishAssignmentOperation(npc, response))) assignmentOperationPending = false;
    }

    private void ChangeWaterMinimum(int delta)
    {
        if (!DeskIsCurrent() || !BeginAssignmentOperation(out var player)) return;
        int.TryParse(Atx(assignmentsDesk, "waterPercent"), out int current);
        int percent = Math.Max(5, Math.Min(100, (current == 0 ? 50 : current) + delta));
        string npc = selectedId;
        assignmentOperationPending = true;
        if (!XUiC_RebirthCompanionDesk.RequestOperation(player, "waterPolicy",
            new XElement("command", new XAttribute("npc", npc), new XAttribute("percent", percent)), 0,
            response => FinishAssignmentOperation(npc, response))) assignmentOperationPending = false;
    }

    // Selecting two exact cells is a world interaction: close this window, let the player open a named
    // container, and finish with Enter. Nothing is moved until the server accepts the commit.
    private void BeginStoragePick()
    {
        var player = xui?.playerUI?.entityPlayer;
        if (player == null || !SelectedIsHuman()) return;
        RebirthCompanionB09Selection.Begin(player, selectedId);
        Close();
    }

    private void AssignmentsOpenDesk(string mode)
    {
        if (!managementOpen || currentTab != Tab.Assignments || !SelectedIsHuman()) return;
        var row = SelectedAssignmentRow();
        if (mode == "edit" && (row == null || !row.Job)) { assignmentFeedback = Ui("xuiRbNpcUiSelectJob", "Select a saved job first."); RenderAssignments(); return; }
        XUiC_RebirthCompanionDesk.Preselect(selectedId, row != null && row.Job ? row.Id : "", mode);
        if (RebirthCraftingNavigationService.Navigate(this, RebirthCraftingNavigationService.Destination.Companions)) Close();
        else XUiC_RebirthCompanionDesk.Preselect("", "", "");
    }

    private void OpenAssignmentsBoard() => AssignmentsOpenDesk("");
}
