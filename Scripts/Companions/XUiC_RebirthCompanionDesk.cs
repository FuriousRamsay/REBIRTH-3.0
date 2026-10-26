using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthCompanionDesk : XUiController
{
    public const string Group = "rebirthCompanionDesk";
    private static XUiC_RebirthCompanionDesk active;
    private static long sequence;
    private sealed class ReadOnlyRequest { internal World World; internal float Expires; internal Action<XElement> Complete; internal bool Report; }
    // Hand-off from the Assignments tab: open the Desk on a worker/job, optionally straight into Add or target definition.
    private static string startNpc = "", startJob = "", startMode = "";
    internal static void Preselect(string npc, string job, string mode) { startNpc = npc ?? ""; startJob = job ?? ""; startMode = mode ?? ""; }
    private static readonly Dictionary<long,ReadOnlyRequest> readOnlyRequests = new Dictionary<long,ReadOnlyRequest>();
    private World requestWorld;
    private long requestToken;
    private string pendingOperation = "";
    private float timeout, poll;
    private bool pending, open, reports, modal, targets, returningFromSelection;
    private int rosterOffset, jobOffset, reportOffset, targetIndex;
    private string selectedNpc = "", selectedJob = "", selectedReport = "", reportState = "Unresolved", kind = "Craft";
    private XElement data = new XElement("desk"), roster = new XElement("companions");
    private XElement draft = new XElement("job");
    private XElement recipePage = new XElement("recipes");
    private int recipeOffset;
    private readonly string[] kinds = { "Craft", "Food", "Farm", "Repair", "Organize" };

    private string T(string key) => RebirthCompanionDeskStore.L(key);
    private string A(XElement e, string field) => RebirthCompanionDeskStore.Text(e, field);
    private void Set(string id, string value) { var l = GetChildById(id)?.ViewComponent as XUiV_Label; if (l != null) l.Text = value ?? ""; }
    private void Show(string id, bool visible) { var v = (GetChildById(id + "Host") ?? GetChildById(id))?.ViewComponent; if (v != null) v.IsVisible = visible; }
    private void Wire(string id, Action callback) { var c = GetChildById(id); if (c != null) c.OnPress += delegate { callback(); }; }
    private string Input(string id) => (GetChildById(id) as XUiC_TextInput)?.Text ?? "";
    private void Input(string id, string value) { var c = GetChildById(id) as XUiC_TextInput; if (c != null) c.Text = value; }
    private void Scroll(string id, Action<int> action)
    { var c = GetChildById(id); if (c == null) return; c.ViewComponent.EventOnScroll = true; c.OnScroll += delegate(XUiController sender, float delta) { action(delta > 0 ? -1 : 1); }; }
    private XElement[] Npcs => roster.Elements("npc").ToArray();
    private XElement[] Jobs => data.Elements("job").Where(j => A(j, "npc") == selectedNpc).OrderBy(j => (int?)j.Attribute("priority") ?? 1).ToArray();
    // targetViewKind "" follows the job kind (Farm or Structure); "Mining"/"Logging" browse those player-defined volumes.
    private string targetViewKind = "", editingTarget = "";
    private string TargetViewKind() => targetViewKind.Length > 0 ? targetViewKind : (kind == "Farm" ? "Farm" : "Structure");
    private XElement[] Targets => data.Elements("target").Where(t => A(t, "kind") == TargetViewKind()).ToArray();
    private bool TryCorner(string prefix, out Vector3i corner)
    {
        corner = default(Vector3i);
        int x, y, z;
        if (!int.TryParse(Input(prefix + "x"), out x) || !int.TryParse(Input(prefix + "y"), out y) || !int.TryParse(Input(prefix + "z"), out z)) return false;
        corner = new Vector3i(x, y, z);
        return true;
    }
    private XElement[] Reports => data.Elements("report").Where(r => A(r, "state") == reportState && (Input("reportSearch").Length == 0 || r.ToString().IndexOf(Input("reportSearch"), StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();

    public override void Init()
    {
        base.Init();
        Wire("deskDashboard", () => { reports = false; Refresh(); }); Wire("deskReports", () => { reports = true; Refresh(); });
        Wire("deskRefresh", () => Request("read"));
        Wire("deskManage", () => RebirthCompanionUiService.Open(xui, selectedNpc, "Overview"));
        Wire("deskAdd", () => Begin(null)); Wire("deskEdit", () => Begin(Jobs.FirstOrDefault(j => A(j, "id") == selectedJob)));
        Wire("deskRemove", () => JobOperation("remove")); Wire("deskUp", () => JobOperation("move", "up")); Wire("deskDown", () => JobOperation("move", "down"));
        Wire("rosterUp", () => { rosterOffset = Math.Max(0, rosterOffset - 1); Refresh(); });
        Wire("rosterDown", () => { rosterOffset = Math.Min(Math.Max(0, Npcs.Length - 6), rosterOffset + 1); Refresh(); });
        Wire("jobsUp", () => { jobOffset = Math.Max(0, jobOffset - 1); Refresh(); });
        Wire("jobsDown", () => { jobOffset = Math.Min(Math.Max(0, Jobs.Length - 5), jobOffset + 1); Refresh(); });
        Scroll("rosterPanel", d => { rosterOffset = Math.Max(0, Math.Min(Math.Max(0, Npcs.Length - 6), rosterOffset + d)); Refresh(); });
        Scroll("jobsPanel", d => { jobOffset = Math.Max(0, Math.Min(Math.Max(0, Jobs.Length - 5), jobOffset + d)); Refresh(); });
        Drag("rosterThumb", d => { rosterOffset = Math.Max(0, Math.Min(Math.Max(0, Npcs.Length - 6), rosterOffset + d)); Refresh(); });
        Drag("jobsThumb", d => { jobOffset = Math.Max(0, Math.Min(Math.Max(0, Jobs.Length - 5), jobOffset + d)); Refresh(); });
        Drag("reportsThumb", d => { reportOffset = Math.Max(0, Math.Min(Math.Max(0, Reports.Length - 6), reportOffset + d)); RefreshReportRows(); });
        Scroll("reportList", d => { reportOffset = Math.Max(0, Math.Min(Math.Max(0, Reports.Length - 6), reportOffset + d)); RefreshReportRows(); });
        for (int i = 0; i < 6; i++) { int n = i; Wire("npc" + i, () => { var rows = Npcs; if (rosterOffset + n >= rows.Length) return; selectedNpc = A(rows[rosterOffset + n], "id"); selectedJob = ""; jobOffset = 0; Refresh(); Request("read"); }); }
        for (int i = 0; i < 5; i++) { int n = i; Wire("job" + i, () => { var rows = Jobs; if (jobOffset + n < rows.Length) selectedJob = A(rows[jobOffset + n], "id"); Refresh(); }); }
        for (int i = 0; i < kinds.Length; i++) { string value = kinds[i]; Wire("type" + value, () => { kind = value; draft.SetAttributeValue("kind", kind); targetViewKind = ""; targetIndex = 0; DrawModal(); if(kind=="Craft") LoadRecipes(); }); }
        Wire("modalCancel", () => { modal = false; Show("assignmentModal", false); });
        Wire("modalSave", SaveDraft);
        Wire("recipeSearchButton",()=>{recipeOffset=0;LoadRecipes();});
        Wire("recipePrevious",()=>{recipeOffset=Math.Max(0,recipeOffset-7);LoadRecipes();});
        Wire("recipeNext",()=>{if(recipeOffset+7<((int?)recipePage.Attribute("total")??0)){recipeOffset+=7;LoadRecipes();}});
        Wire("craftMode",()=>{var modes=new[]{"Once","Repeat","Stock"};int index=Array.IndexOf(modes,A(draft,"mode"));draft.SetAttributeValue("mode",modes[(index+1)%modes.Length]);DrawModal();});
        for(int i=0;i<7;i++){int row=i;Wire("recipeRow"+i,()=>{var recipe=recipePage.Elements("recipe").Skip(row).FirstOrDefault();if(recipe!=null){draft.SetAttributeValue("recipe",A(recipe,"id"));DrawModal();}});}

        foreach (string field in new[] { "meals", "drinks", "limit", "fallback", "tag", "preserve", "merge", "sort" })
        { string f = field; Wire("toggle" + f, () => { draft.SetAttributeValue(f, A(draft, f) != "true" ? "true" : "false"); DrawModal(); }); }
        Wire("targetNext", () => { targetIndex++; DrawModal(); });
        Wire("defineTarget", () => { editingTarget = ""; targets = true; Input("targetName", ""); Show("targetModal", true); });
        Wire("orgPickTag", () => BeginOrgPick("container"));
        Wire("orgPickTagItem", () => BeginOrgPick("tagitem"));
        Wire("orgPickPin", () => BeginOrgPick("pin"));
        Wire("orgPickPreserve", () => BeginOrgPick("preserve"));
        Wire("orgPickDefault", () => BeginOrgPick("default"));
        // Planner switches saved with the policy; merge and within-container sort answer with the real hold reason while gated.
        foreach (string flag in new[] { "merge", "sortWithin", "distant", "sort" })
        {
            string name = flag;
            Wire("orgFlag" + name, () =>
            {
                if (selectedNpc.Length == 0) return;
                string key = "org" + char.ToUpperInvariant(name[0]) + name.Substring(1);
                Request("orgFlag", new XElement("command", new XAttribute("npc", selectedNpc), new XAttribute("flag", name),
                    new XAttribute("value", A(data, key) == "1" ? "0" : "1")));
            });
        }
        Wire("orgRuleNext", () => { orgRuleIndex++; DrawOrgRules(); });
        Wire("orgRuleRemove", RemoveOrgRule);
        Wire("orgEnable", () => { if (selectedNpc.Length > 0) Request("orgEnabled", new XElement("command", new XAttribute("npc", selectedNpc), new XAttribute("enabled", A(data, "orgEnabled") == "1" ? "0" : "1"))); });
        Wire("editTarget", BeginEditTarget);
        Wire("previewTarget", PreviewTarget);
        Wire("rebindTarget", RebindTarget);
        Wire("removeTarget", RemoveSelectedTarget);
        Wire("targetKind", () => { targetViewKind = targetViewKind.Length == 0 ? "Mining" : targetViewKind == "Mining" ? "Logging" : ""; targetIndex = 0; DrawModal(); });
        Wire("targetCancel", () => { editingTarget = ""; targets = false; Show("targetModal", false); }); Wire("targetSave", SaveTarget);
        Wire("cornerA", () => CaptureCorner("a")); Wire("cornerB", () => CaptureCorner("b"));
        Wire("preferredSource", () => SelectWorldBlock("water"));
        Wire("harvestOneCrop", () => SelectOneCrop("harvestOne"));
        Wire("waterProvider", () => SelectOneCrop("waterAssign"));
        Wire("clearSource", () => { draft.SetAttributeValue("water", ""); DrawModal(); });
        foreach (string state in new[] { "Unresolved", "Resolved", "Closed", "Archived" }) { string s = state; Wire("state" + s, () => { reportState = s; reportOffset = 0; selectedReport = ""; Refresh(); }); }
        for (int i = 0; i < 6; i++) { int n = i; Wire("report" + i, () => { var rows = Reports; if (reportOffset + n < rows.Length) selectedReport = A(rows[reportOffset + n], "id"); Refresh(); }); }
        Wire("reportUp", () => { reportOffset = Math.Max(0, reportOffset - 1); Refresh(); });
        Wire("reportDown", () => { reportOffset = Math.Min(Math.Max(0, Reports.Length - 6), reportOffset + 1); Refresh(); });
        Wire("reportRead", () => ReportOperation("readReport")); Wire("reportClose", () => ReportOperation("closeReport")); Wire("reportArchive", () => ReportOperation("archiveReport"));
        Wire("reportClear", () => Request("clearResolved", new XElement("command")));
        Wire("reportRecheck", () => ReportOperation("recheckReport"));
        Wire("reportAssignment", () => { var r = Reports.FirstOrDefault(e => A(e, "id") == selectedReport); if (r == null) return; selectedNpc = A(r, "npc"); selectedJob = A(r, "job"); reports = false; Refresh(); });
    }
    private void Drag(string id, Action<int> change)
    {
        var c = GetChildById(id); if (c?.ViewComponent == null) return;
        c.ViewComponent.EventOnDrag = true;
        float remainder = 0;
        c.OnDrag += delegate(XUiController sender, EDragType type, Vector2 delta)
        {
            if (type == EDragType.DragStart) remainder = 0;
            if (type == EDragType.DragEnd) return;
            remainder -= delta.y;
            int step = (int)(remainder / 12f);
            if (step != 0) { remainder -= step * 12f; change(step); }
        };
    }
    private void Thumb(string id, int height, int visible, int count, int offset)
    {
        bool needed = count > visible;
        string prefix = id == "rosterThumb" ? "roster" : id == "jobsThumb" ? "jobs" : "report";
        Show((prefix == "jobs" ? "job" : prefix) + "Track", needed);
        Show(prefix + "Up", needed);
        Show(prefix + "Down", needed);
        RebirthScrollbarPresentation.RenderThumb(GetChildById(id)?.ViewComponent, height, visible, count, offset, 14);
    }
    public override void OnOpen()
    {
        base.OnOpen(); open = true; active = this; pending = false; requestWorld = xui.playerUI.entityPlayer?.world;
        if (!returningFromSelection)
        {
            data = new XElement("desk"); roster = new XElement("companions"); selectedNpc = selectedJob = selectedReport = "";
            selectedNpc = startNpc; startNpc = "";
        }
        var cursor = xui.playerUI.CursorController; cursor.Locked = false; cursor.SetCursorHidden(false);
        xui.playerUI.entityPlayer?.SetControllable(false);
        GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(RebirthCraftingNavigationService.Destination.Companions);
        Refresh(); if (!returningFromSelection) Request("read");
    }
    public override void OnClose()
    {
        open = pending = modal = targets = false; if (active == this) active = null;
        startNpc = startJob = startMode = "";
        if (!returningFromSelection) { targetViewKind = ""; editingTarget = ""; }
        Show("assignmentModal", false); Show("targetModal", false);
        var cursor = xui.playerUI.CursorController;
        UIInput.selection?.RemoveFocus(); cursor.HoverTarget = null; cursor.Locked = false;
        // No ResetActionSets() here: the native Close pops the group's action set after OnClose(); resetting would make that pop mismatch.
        xui.playerUI.entityPlayer?.SetControllable(true); base.OnClose();
    }
    public override void Update(float dt)
    {
        base.Update(dt); if (!open) return;
        // The Desk shell has no Close button: Escape backs out of a popup first, then closes the Desk.
        if (XUiUtils.HotkeysAllowedFor(ViewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased || xui.playerUI.playerInput.GUIActions.Cancel.WasReleased))
        {
            if (targets) { targets = false; Show("targetModal", false); }
            else if (modal) { modal = false; Show("assignmentModal", false); }
            else xui.playerUI.windowManager.Close(windowGroup);
            return;
        }
        if (pending && Time.unscaledTime > timeout) { pending = false; Set("deskFeedback", T("Timeout")); }
        if (!modal && !targets && !pending && (poll -= dt) <= 0) { poll = 5; Request("read"); }
        if (reports && !modal) RefreshReportRows();
    }
    private void Request(string operation, XElement value = null)
    {
        if (pending || !open) return;
        var player = xui.playerUI.entityPlayer; var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        var persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        if (player == null || connection == null || persistent?.PrimaryId == null) return;
        if (operation == "read" && value == null)
            value = new XElement("command", new XAttribute("npc", selectedNpc));
        pending = true; pendingOperation = operation; timeout = Time.unscaledTime + 8; requestWorld = player.world; requestToken = ++sequence;
        if (connection.IsServer) Accept(requestToken, RebirthCompanionDeskStore.Process(player.world, player, persistent.PrimaryId, RebirthCompanionDeskStore.Revision(data), operation, value?.ToString(SaveOptions.DisableFormatting) ?? "<command/>"));
        else connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCompanionDeskRequest>().Setup(player.entityId, persistent.PrimaryId, requestToken, RebirthCompanionDeskStore.Revision(data), operation, value?.ToString(SaveOptions.DisableFormatting) ?? "<command/>"));
    }
    internal static void Receive(World world, long token, string value)
    {
        if(readOnlyRequests.TryGetValue(token,out var request))
        {
            readOnlyRequests.Remove(token);
            if(!ReferenceEquals(request.World,world))return;
            try
            {
                var response=RebirthCompanionDeskStore.Parse(value);
                if(RebirthCompanionDeskStore.Text(response,"error").Length==0||request.Report)request.Complete?.Invoke(response);
            }
            catch{return;}
            return;
        }
        if (active != null && ReferenceEquals(active.requestWorld, world)) active.Accept(token, value);
    }
    internal static bool RequestReadOnly(EntityPlayerLocal player,string requestedNpc,Action<XElement> complete)
    {
        var world=player?.world;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var persistent=GameManager.Instance?.GetPersistentLocalPlayer();
        if(world==null||connection==null||persistent?.PrimaryId==null||complete==null)return false;
        foreach(var stale in readOnlyRequests.Where(p=>!ReferenceEquals(p.Value.World,world)||p.Value.Expires<Time.unscaledTime).Select(p=>p.Key).ToArray())
            readOnlyRequests.Remove(stale);
        if(readOnlyRequests.Count>=16)return false;
        long token=++sequence;
        // The existing Desk read endpoint authenticates owner, actor, world and
        // recovery generation. This manager view cannot mutate saved jobs.
        readOnlyRequests[token]=new ReadOnlyRequest{World=world,Expires=Time.unscaledTime+10f,Complete=complete};
        string query = new XElement("command", new XAttribute("npc", requestedNpc ?? "")).ToString(SaveOptions.DisableFormatting);
        if(connection.IsServer)Receive(world,token,RebirthCompanionDeskStore.Process(world,player,persistent.PrimaryId,0,"read",query));
        else connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCompanionDeskRequest>().Setup(player.entityId,persistent.PrimaryId,token,0,"read",query));
        return true;
    }
    // Same authenticated endpoint as the Desk itself. The callback receives the response even when it carries an
    // error attribute, so the Assignments tab can show the exact server refusal.
    internal static bool RequestOperation(EntityPlayerLocal player, string operation, XElement payload, long expectedRevision, Action<XElement> complete)
    {
        var world=player?.world;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var persistent=GameManager.Instance?.GetPersistentLocalPlayer();
        if(world==null||connection==null||persistent?.PrimaryId==null||complete==null||payload==null)return false;
        foreach(var stale in readOnlyRequests.Where(p=>!ReferenceEquals(p.Value.World,world)||p.Value.Expires<Time.unscaledTime).Select(p=>p.Key).ToArray())
            readOnlyRequests.Remove(stale);
        if(readOnlyRequests.Count>=16)return false;
        long token=++sequence;
        readOnlyRequests[token]=new ReadOnlyRequest{World=world,Expires=Time.unscaledTime+10f,Complete=complete,Report=true};
        string xml=payload.ToString(SaveOptions.DisableFormatting);
        if(connection.IsServer)Receive(world,token,RebirthCompanionDeskStore.Process(world,player,persistent.PrimaryId,expectedRevision,operation,xml));
        else connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCompanionDeskRequest>().Setup(player.entityId,persistent.PrimaryId,token,expectedRevision,operation,xml));
        return true;
    }
    private void Accept(long token, string value)
    {
        if (!open || token != requestToken || !ReferenceEquals(requestWorld, xui.playerUI.entityPlayer?.world)) return;
        pending = false;
        try
        {
            var response = RebirthCompanionDeskStore.Parse(value); string error = A(response, "error");
            if (error.Length > 0) { Set("deskFeedback", error); Set("modalFeedback", error); return; }
            if(response.Element("recipes")!=null && A(response.Element("recipes"),"npc")==selectedNpc) recipePage=new XElement(response.Element("recipes"));
            data = response.Element("desk") ?? new XElement("desk"); roster = response.Element("companions") ?? new XElement("companions");
            if (!Npcs.Any(n => A(n, "id") == selectedNpc)) selectedNpc = A(Npcs.FirstOrDefault(), "id");
            if (A(response, "message").Length > 0)
            {
                targets = false; Show("targetModal", false);
                if (pendingOperation == "target")
                {
                    modal = true;
                    int edited = editingTarget.Length > 0 ? Array.FindIndex(Targets, t => A(t, "id") == editingTarget) : -1;
                    targetIndex = edited >= 0 ? edited : Math.Max(0, Targets.Length - 1);
                    editingTarget = "";
                    DrawModal();
                }
                else if (pendingOperation == "removeTarget" || pendingOperation == "orgRuleRemove" || pendingOperation == "orgEnabled")
                {
                    // Keep the popup and the draft; just refresh the target list after the removal.
                    modal = true; targetIndex = Math.Max(0, Math.Min(targetIndex, Targets.Length - 1)); DrawModal();
                }
                else { modal = false; Show("assignmentModal", false); }
            }
            Set("deskFeedback", A(response, "message")); Refresh(); if(modal) DrawModal();
            ApplyStart();
        }
        catch { Set("deskFeedback", T("StorageError")); }
    }
    private void ApplyStart()
    {
        string mode = startMode, job = startJob;
        if (mode.Length == 0 && job.Length == 0) return;
        startMode = startJob = "";
        if (job.Length > 0 && Jobs.Any(j => A(j, "id") == job)) selectedJob = job;
        if (mode == "reports")
        {
            reports = true; reportState = "Unresolved";
            var own = data.Elements("report").FirstOrDefault(r => A(r, "npc") == selectedNpc && A(r, "state") == "Unresolved" && (job.Length == 0 || A(r, "job") == job));
            selectedReport = A(own, "id"); Refresh();
        }
        else if (mode == "add") Begin(null);
        else if (mode == "edit") Begin(Jobs.FirstOrDefault(j => A(j, "id") == job));
        else if (mode == "target")
        {
            Begin(null);
            if (!modal) return;
            kind = "Farm"; draft.SetAttributeValue("kind", kind); DrawModal();
            targets = true; Input("targetName", ""); Show("targetModal", true);
        }
        else Refresh();
    }
    private void Refresh()
    {
        Show("dashboardPanel", !reports); Show("reportsPanel", reports);
        var npcs = Npcs; rosterOffset = Math.Min(rosterOffset, Math.Max(0, npcs.Length - 6));
        for (int i = 0; i < 6; i++)
        { var n = npcs.Skip(rosterOffset + i).FirstOrDefault(); Show("npc" + i, n != null); Set("npcText" + i, n == null ? "" : (A(n, "id") == selectedNpc ? "> " : "") + A(n, "name") + "\n" + A(n, "status")); }
        Set("rosterCount", string.Format(T("RosterCount"), npcs.Length));
        Thumb("rosterThumb", 510, 6, npcs.Length, rosterOffset);
        var selected = npcs.FirstOrDefault(n => A(n, "id") == selectedNpc); Set("deskSelected", A(selected, "name"));
        var crop = data.Elements("cropAssignment").OrderByDescending(c => (ulong?)c.Attribute("id") ?? 0UL).FirstOrDefault();
        Set("deskExecution", crop == null ? T("ExecutionHeld") :
            T("SingleCropStatus") + " #" + A(crop, "id") + " — " + T("WorkStatus" + A(crop, "status")) +
            " (" + A(crop, "x") + ", " + A(crop, "y") + ", " + A(crop, "z") + ")\n" + T("OtherPlansHeld"));
        var jobs = Jobs; jobOffset = Math.Min(jobOffset, Math.Max(0, jobs.Length - 5));
        for (int i = 0; i < 5; i++)
        {
            var j = jobs.Skip(jobOffset + i).FirstOrDefault(); Show("job" + i, j != null);
            var target = data.Elements("target").FirstOrDefault(t => A(t, "id") == A(j, "target"));
            Set("jobText" + i, j == null ? "" : (A(j, "id") == selectedJob ? "> " : "") + A(j, "priority") + "  " + T(A(j, "kind")) + "  " + A(target, "name") + "\n" + T("Planned"));
        }
        Set("jobCount", string.Format(T("JobCount"), jobs.Length));
        Thumb("jobsThumb", 370, 5, jobs.Length, jobOffset);
        RefreshReportRows();
    }
    private void Begin(XElement existing)
    {
        if (Npcs.FirstOrDefault(n => A(n, "id") == selectedNpc)?.Attribute("human")?.Value != "true") { Set("deskFeedback", T("HumanRequired")); return; }
        draft = existing == null ? new XElement("job", new XAttribute("npc", selectedNpc), new XAttribute("meals", "true"), new XAttribute("drinks", "true"), new XAttribute("fallback", "true"), new XAttribute("preserve", "true"), new XAttribute("tag", "true"), new XAttribute("sort", "true"), new XAttribute("merge", "true")) : new XElement(existing);
        kind = existing == null ? "Craft" : A(existing, "kind"); targetIndex = Array.FindIndex(Targets, t => A(t, "id") == A(draft, "target"));
        if (targetIndex < 0) targetIndex = 0;
        Input("mealCount", A(draft, "mealCount").Length > 0 ? A(draft, "mealCount") : "20"); Input("drinkCount", A(draft, "drinkCount").Length > 0 ? A(draft, "drinkCount") : "20");
        Input("craftQuantity", A(draft,"quantity").Length>0?A(draft,"quantity"):"1");
        Input("recipeSearch",""); recipeOffset=0; recipePage=new XElement("recipes");
        if(A(draft,"mode").Length==0)draft.SetAttributeValue("mode","Once");
        targetViewKind = ""; editingTarget = "";
        Set("modalFeedback", ""); modal = true; Show("assignmentModal", true); DrawModal(); if(kind=="Craft") LoadRecipes();
    }
    private void LoadRecipes()
    { Request("recipes",new XElement("query",new XAttribute("npc",selectedNpc),new XAttribute("search",Input("recipeSearch")),new XAttribute("offset",recipeOffset))); }
    private void DrawModal()
    {
        for(int i=0;i<7;i++)
        {
            var recipe=recipePage.Elements("recipe").Skip(i).FirstOrDefault();Show("recipeRow"+i,recipe!=null);
            Set("recipeRowText"+i,recipe==null?"":(A(recipe,"id")==A(draft,"recipe")?"> ":"")+Localization.Get(A(recipe,"id")));
        }
        Set("craftModeText",T("Mode"+A(draft,"mode")));
        Set("recipeSelected",A(draft,"recipe").Length==0?T("ChooseKnownRecipe"):Localization.Get(A(draft,"recipe")));
        Set("recipePageCount",string.Format(T("RecipePageCount"),recipeOffset+1,Math.Min(recipeOffset+7,(int?)recipePage.Attribute("total")??0),(int?)recipePage.Attribute("total")??0));
        foreach (string k in kinds) Show("fields" + k, kind == k);
        Show("targetControls", kind == "Farm" || kind == "Repair");
        Set("preferredSourceLabel", T("Preferred") + (A(draft, "water").Length > 0 ? ": " + A(draft, "water") : ""));
        Set("modalHeading", T(kind)); Set("modalNpc", A(Npcs.FirstOrDefault(n => A(n, "id") == selectedNpc), "name"));
        Set("targetKindLabel", T("VolumeKind" + (targetViewKind.Length == 0 ? "Job" : targetViewKind)));
        // Mining and Logging volumes are only browsed here; they are never assigned to a Farm or Repair draft.
        var list = Targets; if (list.Length > 0) { targetIndex = Math.Abs(targetIndex) % list.Length; if (targetViewKind.Length == 0) draft.SetAttributeValue("target", A(list[targetIndex], "id")); }
        Set("targetLabel", list.Length == 0 ? T("ChooseTarget") : A(list[targetIndex], "name"));
        Show("rebindTarget", list.Length > 0 && IsLegacyCamperTarget(list[targetIndex]));
        foreach (string f in new[] { "meals", "drinks", "limit", "fallback", "tag", "preserve", "merge", "sort" }) Set("value" + f, (A(draft, f) == "true" ? "[x] " : "[ ] ") + T(f));
        DrawOrgRules();
    }
    private void SaveDraft()
    {
        foreach (string field in new[] { "mealCount", "drinkCount" })
        { int count; if (!int.TryParse(Input(field), out count) || count < 1 || count > 10000) { Set("modalFeedback", T("Invalid")); return; } draft.SetAttributeValue(field, count); }
        if(kind=="Craft")
        {
            if(A(draft,"recipe").Length==0){Set("modalFeedback",T("ChooseKnownRecipe"));return;}
            if(!int.TryParse(Input("craftQuantity"),out var quantity)||quantity<1||quantity>10000){Set("modalFeedback",T("Invalid"));return;}
            draft.SetAttributeValue("quantity",quantity);
        }
        draft.SetAttributeValue("kind", kind); Request("save", draft);
    }
    private void JobOperation(string operation, string direction = "")
    { if (selectedJob.Length == 0) return; Request(operation, new XElement("command", new XAttribute("npc", selectedNpc), new XAttribute("id", selectedJob), new XAttribute("direction", direction))); }
    private void ReportOperation(string operation)
    { if (selectedReport.Length > 0) Request(operation, new XElement("command", new XAttribute("id", selectedReport))); }
    private void CaptureCorner(string prefix)
    {
        SelectWorldBlock(prefix);
    }
    // Picks one world block inside the chosen Farm target: a mature crop (harvestOne) or a water provider (waterAssign).
    private void SelectOneCrop(string operation)
    {
        if (pending || selectedNpc.Length == 0 || kind != "Farm") return;
        string farm = A(draft, "target");
        if (!Guid.TryParseExact(farm, "N", out _) ||
            !data.Elements("target").Any(t => A(t, "id") == farm && A(t, "kind") == "Farm"))
        { Set("modalFeedback", T("ChooseTarget")); return; }
        var player = xui.playerUI.entityPlayer;
        returningFromSelection = true;
        xui.playerUI.windowManager.Close(windowGroup);
        RebirthCompanionWorldSelection.Begin(player, position =>
        {
            if (player == null || !ReferenceEquals(player.world, GameManager.Instance?.World))
            { returningFromSelection = false; return; }
            xui.playerUI.windowManager.Open(Group, true, true);
            modal = true; Show("assignmentModal", true); DrawModal();
            returningFromSelection = false;
            if (position.HasValue)
                Request(operation, new XElement("command", new XAttribute("npc", selectedNpc),
                    new XAttribute("target", farm), new XAttribute("x", position.Value.x),
                    new XAttribute("y", position.Value.y), new XAttribute("z", position.Value.z)));
        });
    }
    private void SelectWorldBlock(string field)
    {
        if (pending) return;
        var player = xui.playerUI.entityPlayer;
        bool restoreTargets = targets, restoreModal = modal;
        returningFromSelection = true;
        xui.playerUI.windowManager.Close(windowGroup);
        // While picking one corner the other corner (if set) anchors a full-volume preview, so the whole box is visible before saving.
        Vector3i? anchor = null;
        if (field == "a" || field == "b") { Vector3i other; if (TryCorner(field == "a" ? "b" : "a", out other)) anchor = other; }
        RebirthCompanionWorldSelection.Begin(player, position =>
        {
            if (player == null || !ReferenceEquals(player.world, GameManager.Instance?.World)) { returningFromSelection = false; return; }
            xui.playerUI.windowManager.Open(Group, true, true);
            targets = restoreTargets; modal = restoreModal;
            if (position.HasValue)
            {
                var point = position.Value;
                if (field == "water") draft.SetAttributeValue("water", point.x + "," + point.y + "," + point.z);
                else { Input(field + "x", point.x.ToString()); Input(field + "y", point.y.ToString()); Input(field + "z", point.z.ToString()); }
            }
            Show("assignmentModal", modal); Show("targetModal", targets);
            if (modal) DrawModal();
            returningFromSelection = false;
        }, anchor);
    }
    // Edit the target currently shown: load its name and saved corners into the popup. Its id is kept on save.
    // Saved Organize policy rules (shared by every Organize job of this owner): list, pick and remove.
    private int orgRuleIndex;
    private XElement[] OrgRules => data.Elements("orgRule").ToArray();
    private void DrawOrgRules()
    {
        var rules = OrgRules;
        Set("orgEnableLabel", T(A(data, "orgEnabled") == "1" ? "OrgEnabledOn" : "OrgEnabledOff"));
        Set("orgFlagmergeLabel", A(data, "orgMergeReady") == "1" ? (A(data, "orgMerge") == "1" ? "[x] " : "[ ] ") + T("OrgFlagMerge") : T("OrgFlagMergeHeld"));
        Set("orgFlagsortWithinLabel", A(data, "orgSortReady") == "1" ? (A(data, "orgSortWithin") == "1" ? "[x] " : "[ ] ") + T("OrgFlagSortWithin") : T("OrgFlagSortWithinHeld"));
        Set("orgFlagdistantLabel", (A(data, "orgDistant") == "1" ? "[x] " : "[ ] ") + T("OrgFlagDistant"));
        Set("orgFlagsortLabel", (A(data, "orgSort") == "1" ? "[x] " : "[ ] ") + T("OrgFlagSort"));
        if (rules.Length == 0) { Set("orgRulesText", T("OrgNoRules")); return; }
        orgRuleIndex = Math.Abs(orgRuleIndex) % rules.Length;
        var r = rules[orgRuleIndex];
        string detail = A(r, "rtype") == "tagitem" ? A(r, "item") + " > " + A(r, "tag")
            : A(r, "rtype") == "pin" ? A(r, "item") + " @ " + A(r, "x") + "," + A(r, "y") + "," + A(r, "z")
            : A(r, "tag") + " @ " + A(r, "x") + "," + A(r, "y") + "," + A(r, "z");
        Set("orgRulesText", (orgRuleIndex + 1) + "/" + rules.Length + "  " + T("OrgRule" + A(r, "rtype")) + ": " + detail);
    }
    private void RemoveOrgRule()
    {
        var rules = OrgRules;
        if (rules.Length == 0) return;
        Request("orgRuleRemove", new XElement("command", new XAttribute("npc", selectedNpc), new XAttribute("id", A(rules[Math.Abs(orgRuleIndex) % rules.Length], "id"))));
    }
    // The pick happens in the world (a named container's cells), so the Desk closes and the pick finishes with Enter.
    private void BeginOrgPick(string rtype)
    {
        string tag = Input("orgTag").Trim();
        // A container may be listed without a tag; an item-to-tag rule needs one.
        if (rtype == "tagitem" && tag.Length == 0) { Set("modalFeedback", T("OrgTagRequired")); return; }
        if (selectedNpc.Length == 0) return;
        var player = xui.playerUI.entityPlayer;
        xui.playerUI.windowManager.Close(windowGroup);
        RebirthCompanionB09Selection.BeginOrg(player, selectedNpc, rtype, tag);
    }
    // Read-only world outline of the target currently shown (camper-bound volumes follow the camper). The Desk closes and reopens like a pick.
    private void PreviewTarget()
    {
        var list = Targets;
        if (pending || list.Length == 0) { Set("modalFeedback", T("ChooseTarget")); return; }
        var row = list[Math.Abs(targetIndex) % list.Length];
        var player = xui.playerUI.entityPlayer;
        bool restoreTargets = targets, restoreModal = modal;
        returningFromSelection = true;
        xui.playerUI.windowManager.Close(windowGroup);
        if (!RebirthCompanionVolumePreview.Begin(player, row, () =>
        {
            if (player != null && ReferenceEquals(player.world, GameManager.Instance?.World))
            {
                xui.playerUI.windowManager.Open(Group, true, true);
                targets = restoreTargets; modal = restoreModal;
                Show("assignmentModal", modal); Show("targetModal", targets);
                if (modal) DrawModal();
            }
            returningFromSelection = false;
        }))
        {
            xui.playerUI.windowManager.Open(Group, true, true);
            returningFromSelection = false;
            Set("modalFeedback", T("Invalid"));
        }
    }
    // Legacy camper volumes lack the stored matrix and fail closed. The owner re-binds from the CURRENT camper transform
    // only after previewing it: first press arms (asks to Preview and confirm), second press on the same target sends it.
    private string rebindArmed = "";
    private static bool IsLegacyCamperTarget(XElement row) => RebirthCompanionDeskStore.Text(row, "camper").Length > 0 && RebirthCompanionDeskStore.Text(row, "m00").Length == 0;
    private void RebindTarget()
    {
        var list = Targets;
        if (pending || list.Length == 0) { Set("modalFeedback", T("ChooseTarget")); return; }
        var row = list[Math.Abs(targetIndex) % list.Length];
        if (!IsLegacyCamperTarget(row)) return;
        if (rebindArmed != A(row, "id")) { rebindArmed = A(row, "id"); Set("modalFeedback", T("RebindConfirm")); return; }
        rebindArmed = "";
        Request("target", new XElement("target", new XAttribute("id", A(row, "id")), new XAttribute("name", A(row, "name")),
            new XAttribute("kind", A(row, "kind")), new XAttribute("camper", A(row, "camper")), new XAttribute("rebind", "1")));
    }
    private void BeginEditTarget()
    {
        var list = Targets;
        if (list.Length == 0) { Set("modalFeedback", T("ChooseTarget")); return; }
        var row = list[Math.Abs(targetIndex) % list.Length];
        editingTarget = A(row, "id"); targets = true;
        Input("targetName", A(row, "name"));
        foreach (string field in new[] { "ax", "ay", "az", "bx", "by", "bz" }) Input(field, A(row, field));
        Show("targetModal", true);
    }
    private void RemoveSelectedTarget()
    {
        var list = Targets;
        if (list.Length == 0) { Set("modalFeedback", T("ChooseTarget")); return; }
        // The server refuses while a saved job still points at this target and says so.
        Request("removeTarget", new XElement("command", new XAttribute("id", A(list[Math.Abs(targetIndex) % list.Length], "id"))));
    }
    private void SaveTarget()
    {
        var existing = editingTarget.Length > 0 ? data.Elements("target").FirstOrDefault(t => A(t, "id") == editingTarget) : null;
        var target = new XElement("target", new XAttribute("name", Input("targetName")), new XAttribute("kind", existing != null ? A(existing, "kind") : TargetViewKind()));
        bool geometry = existing == null;
        if (existing != null)
        {
            target.SetAttributeValue("id", editingTarget);
            // Unchanged corners are a rename: omit them so the saved geometry and any camper witness stay untouched.
            foreach (string field in new[] { "ax", "ay", "az", "bx", "by", "bz" }) if (Input(field) != A(existing, field)) geometry = true;
        }
        if (geometry) foreach (string field in new[] { "ax", "ay", "az", "bx", "by", "bz" }) target.SetAttributeValue(field, Input(field));
        // New corners for a camper-bound target keep the same camper binding (recomputed by the server); a rename sends none.
        if (existing != null && geometry && A(existing, "camper").Length > 0) target.SetAttributeValue("camper", A(existing, "camper"));
        Request("target", target);
    }
    private void RefreshReportRows()
    {
        if (!reports) return;
        var rows = Reports; reportOffset = Math.Min(reportOffset, Math.Max(0, rows.Length - 6));
        for (int i = 0; i < 6; i++) { var r = rows.Skip(reportOffset + i).FirstOrDefault(); Show("report" + i, r != null); Set("reportText" + i, r == null ? "" : A(r, "title")); }
        Set("reportCount", string.Format(T("ReportCount"), rows.Length));
        Thumb("reportsThumb", 460, 6, rows.Length, reportOffset);
        var selected = rows.FirstOrDefault(r => A(r, "id") == selectedReport);
        string detail = selected == null ? T("NoReports") : T(A(selected, "state")) + "\n" + (A(selected, "condition") == "releaseHold" ? T("ExecutionHeld") : A(selected, "detail"));
        if (selected != null && A(selected, "npc") == selectedNpc && data.Elements("job").Any(j => A(j, "id") == A(selected, "job") && A(j, "kind") == "Repair"))
            foreach (var shortfall in data.Elements("shortfall").Where(s => s.Attribute("job") == null).Take(8)) detail += "\n" + shortfall.Value;
        else if (selected != null && A(selected, "npc") == selectedNpc)
            foreach (var shortfall in data.Elements("shortfall").Where(s => A(s, "job") == A(selected, "job") && A(selected, "job").Length > 0).Take(4)) detail += "\n" + shortfall.Value;
        if (selected != null)
        {
            foreach (var entry in selected.Elements("transition").Take(8))
            {
                long ticks;
                string stamp = long.TryParse(A(entry, "at"), out ticks) && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks
                    ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("g") : "";
                detail += "\n" + stamp + "  " + T(A(entry, "to"));
            }
        }
        Set("reportDetail", detail);
        Show("reportArchive", selected != null && A(selected, "state") != "Unresolved");
        Show("reportClose", selected != null && A(selected, "state") == "Unresolved");
    }
}
