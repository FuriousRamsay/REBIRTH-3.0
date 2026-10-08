using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Native in-game interaction surface for authoritative NPC projections and mutations.
/// The controller never mutates runtime state directly; every action passes through the
/// interaction session and command router with an expected runtime revision.
/// </summary>
public static class RebirthNpcInteractionUiService
{
    public const string WindowGroupName = "rebirthNpcInteraction";
    private const float SearchRadius = 5f;

    public static bool OpenNearest(XUi xui, out string error)
    {
        error = string.Empty;
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player == null) { error = "A local player is required."; return false; }
        List<Entity> entities = new List<Entity>();
        Bounds bounds = new Bounds(player.position, new Vector3(SearchRadius * 2f, SearchRadius * 2f, SearchRadius * 2f));
        GameManager.Instance.World.GetEntitiesInBounds(typeof(EntityRebirthNPC), bounds, entities);
        EntityRebirthNPC nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < entities.Count; i++)
        {
            EntityRebirthNPC candidate = entities[i] as EntityRebirthNPC;
            if (candidate == null || candidate.IsDead()) continue;
            float distance = Vector3.SqrMagnitude(candidate.position - player.position);
            if (distance < nearestDistance) { nearestDistance = distance; nearest = candidate; }
        }
        if (nearest == null) { error = "No REBIRTH NPC is within " + SearchRadius.ToString("0", CultureInfo.InvariantCulture) + " metres."; return false; }
        return Open(xui, nearest, out error);
    }

    public static bool Open(XUi xui, EntityRebirthNPC npc, out string error)
    {
        error = string.Empty;
        if (xui == null || npc == null || npc.RebirthRuntimeState == null)
        { error = "The interaction target is unavailable."; return false; }
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player == null) { error = "A local player is required."; return false; }
        XUiController group = xui.FindWindowGroupByName(WindowGroupName);
        XUiC_RebirthNpcInteraction controller = group != null
            ? group.GetChildByType<XUiC_RebirthNpcInteraction>() : null;
        if (controller == null) { error = "The REBIRTH NPC interaction window is not registered."; return false; }
        RebirthNpcInteractionNetworkClient.RequestOpen(npc.entityId, delegate(RebirthNpcInteractionProjection projection, RebirthNpcInteractionCommandResponse ignored, string networkError)
        {
            if (projection == null) { Log.Out("[REBIRTH NPC] " + (string.IsNullOrEmpty(networkError) ? "Interaction request was rejected." : networkError)); return; }
            controller.Prepare(projection); xui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, false);
        });
        return true;
    }
}

[Preserve]
public sealed class XUiC_RebirthNpcInteraction : XUiController
{
    private readonly Dictionary<string, List<XUiController>> buttons = new Dictionary<string, List<XUiController>>(StringComparer.OrdinalIgnoreCase);
    private XUiV_Label npcName, npcStatus, feedback, detail;
    private RebirthNpcInteractionProjection projection;
    private bool requestPending;
    private long requestSequence;
    private int lessonPreviewPage;
    private string selectedLessonSubject;

    public override void Init()
    {
        base.Init();
        npcName = Label("npcName"); npcStatus = Label("npcStatus"); feedback = Label("feedback"); detail = Label("detail");
        Wire("btnInspect", RebirthNpcInteractionCommandKind.Inspect, string.Empty);
        XUiController talk = GetChildById("btnTalk"); if (talk != null) { AddButton("dialogue", talk); talk.OnPress += delegate { OpenDialogue(); }; }
        XUiController trade = GetChildById("btnTrade"); if (trade != null) { AddButton("trade", trade); trade.OnPress += delegate { OpenTrade(); }; }
        // Snapshot commands remain available to the protocol; this window has no item UI.
        WithholdSnapshotControl("btnInventory");
        WithholdSnapshotControl("btnEquipment");
        Wire("btnHire", RebirthNpcInteractionCommandKind.Hire, string.Empty);
        Wire("btnDismiss", RebirthNpcInteractionCommandKind.Dismiss, string.Empty);
        Wire("btnTeach", RebirthNpcInteractionCommandKind.Teach, string.Empty);
        var learn=GetChildById("btnLearn");
        if(learn!=null){AddButton("learn",learn);learn.OnPress+=delegate{
            if(!string.IsNullOrEmpty(selectedLessonSubject))Submit(RebirthNpcInteractionCommandKind.Learn,selectedLessonSubject);};}
        XUiController lessons=GetChildById("btnLessonPreview");
        if(lessons!=null){AddButton("lessonpreview",lessons);lessons.OnPress+=delegate{
            if(!requestPending)Submit(RebirthNpcInteractionCommandKind.LessonPreview,
                (lessonPreviewPage++).ToString(CultureInfo.InvariantCulture));};}
        Wire("btnFollow", RebirthNpcInteractionCommandKind.IssueOrder, "order=Follow");
        Wire("btnStay", RebirthNpcInteractionCommandKind.IssueOrder, "order=Stay");
        XUiController guard = GetChildById("btnGuard");
        if (guard != null) { AddButton("orders", guard); guard.OnPress += delegate { IssueGuard(); }; }
        XUiController refresh = GetChildById("btnRefresh"); if (refresh != null) refresh.OnPress += delegate { RefreshProjection(); };
        XUiController close = GetChildById("btnClose"); if (close != null) close.OnPress += delegate { Close(); };
    }

    public void Prepare(RebirthNpcInteractionProjection value)
    {
        projection = value; requestPending = false; lessonPreviewPage=0; selectedLessonSubject=null; Set(detail,string.Empty); Render();
    }

    public override void OnOpen()
    {
        base.OnOpen(); windowGroup.isEscClosable = false; Render();
    }

    public override void OnClose()
    {
        if (projection != null && !string.IsNullOrWhiteSpace(projection.SessionKey))
            RebirthNpcInteractionNetworkClient.RequestClose(projection.SessionKey);
        projection = null; requestPending = false; base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (XUiUtils.HotkeysAllowedFor(viewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased || xui.playerUI.playerInput.GUIActions.Cancel.WasReleased)) Close();
    }

    private void WithholdSnapshotControl(string controlId)
    {
        XUiController control = GetChildById(controlId);
        if (control?.ViewComponent == null) return;
        control.ViewComponent.IsVisible = false;
        control.ViewComponent.Enabled = false;
    }

    private void Wire(string controlId, RebirthNpcInteractionCommandKind kind, string payload)
    {
        XUiController control = GetChildById(controlId);
        if (control == null) return;
        AddButton(EntryId(kind), control);
        control.OnPress += delegate { Submit(kind, payload); };
    }

    private void OpenDialogue()
    {
        if (projection == null) return; string error;
        if (!RebirthNpcTradeDialogueUiService.OpenDialogue(xui, projection, out error)) Set(feedback, error);
    }

    private void OpenTrade()
    {
        if (projection == null) return; string error;
        if (!RebirthNpcTradeDialogueUiService.OpenTrade(xui, projection, out error)) Set(feedback, error);
    }

    private void IssueGuard()
    {
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player == null) { Set(feedback, Localization.Get("xuiRebirthNpcTargetUnavailable")); return; }
        Vector3 p = player.position;
        Submit(RebirthNpcInteractionCommandKind.IssueOrder,
            "order=Guard;x=" + p.x.ToString("R", CultureInfo.InvariantCulture) +
            ";y=" + p.y.ToString("R", CultureInfo.InvariantCulture) +
            ";z=" + p.z.ToString("R", CultureInfo.InvariantCulture));
    }

    private void Submit(RebirthNpcInteractionCommandKind kind, string payload)
    {
        if (requestPending || projection == null) return;
        if(kind==RebirthNpcInteractionCommandKind.LessonPreview)selectedLessonSubject=null;
        requestPending = true; RenderButtons();
        RebirthNpcInteractionCommand command = new RebirthNpcInteractionCommand
        {
            RequestId = projection.SessionKey + ":xui:" + (++requestSequence).ToString(CultureInfo.InvariantCulture),
            SessionKey = projection.SessionKey, Kind = kind, Payload = payload ?? string.Empty, ExpectedRevision = projection.RuntimeRevision
        };
        RebirthNpcInteractionNetworkClient.RequestCommand(command, delegate(RebirthNpcInteractionProjection refreshed, RebirthNpcInteractionCommandResponse response, string networkError)
        {
            requestPending = false;
            if (!string.IsNullOrEmpty(networkError)) Set(feedback, networkError);
            else if (response == null) Set(feedback, Localization.Get("xuiRebirthNpcNoResponse"));
            else
            {
                Set(feedback,response.Detail);
                if(kind==RebirthNpcInteractionCommandKind.LessonPreview&&response.Status==RebirthNpcInteractionCommandStatus.Accepted)
                {
                    string payloadText=response.ResponsePayload??string.Empty;
                    int split=payloadText.IndexOf('\n');
                    if(split>=0&&split<=128)
                    {
                        string subject=payloadText.Substring(0,split);
                        selectedLessonSubject=subject.StartsWith("skill.",StringComparison.Ordinal)?subject:null;
                        Set(detail,payloadText.Substring(split+1));
                    }
                }
                else if(kind!=RebirthNpcInteractionCommandKind.Learn){selectedLessonSubject=null;Set(detail,string.Empty);}
            }
            if (response != null && response.Status == RebirthNpcInteractionCommandStatus.Accepted && refreshed != null) { projection = refreshed; Render(); } else RenderButtons();
        });
    }

    private void RefreshProjection()
    {
        if (projection == null) return;
        requestPending=true; RenderButtons(); RebirthNpcInteractionNetworkClient.RequestRefresh(projection.SessionKey, delegate(RebirthNpcInteractionProjection refreshed,RebirthNpcInteractionCommandResponse ignored,string networkError){requestPending=false;if(refreshed==null){Set(feedback,string.IsNullOrEmpty(networkError)?Localization.Get("xuiRebirthNpcSessionExpired"):networkError);RenderButtons();return;}projection=refreshed;Render();});
    }

    private void Render()
    {
        if (projection == null) return;
        Set(npcName, projection.DisplayName);
        Set(npcStatus, projection.StatusText);
        if (string.IsNullOrWhiteSpace(feedback != null ? feedback.Text : string.Empty))
            Set(feedback, Localization.Get("xuiRebirthNpcSelectAction"));
        RenderButtons();
    }

    private void RenderButtons()
    {
        foreach (KeyValuePair<string, List<XUiController>> pair in buttons)
            for (int i = 0; i < pair.Value.Count; i++)
                if (pair.Value[i] != null) pair.Value[i].ViewComponent.IsVisible = false;
        if (projection == null || projection.Entries == null) return;
        for (int i = 0; i < projection.Entries.Length; i++)
        {
            RebirthNpcInteractionMenuEntry entry = projection.Entries[i];
            List<XUiController> controls;
            if (!buttons.TryGetValue(entry.Id, out controls)) continue;
            for (int j = 0; j < controls.Count; j++)
                if (controls[j] != null) controls[j].ViewComponent.IsVisible = entry.Enabled && !requestPending && (entry.Id!="learn"||!string.IsNullOrEmpty(selectedLessonSubject));
        }
    }

    private void AddButton(string entryId, XUiController control)
    {
        List<XUiController> controls;
        if (!buttons.TryGetValue(entryId, out controls))
        {
            controls = new List<XUiController>();
            buttons[entryId] = controls;
        }
        controls.Add(control);
    }

    private static string EntryId(RebirthNpcInteractionCommandKind kind)
    {
        switch (kind)
        {
            case RebirthNpcInteractionCommandKind.Inspect: return "inspect";
            case RebirthNpcInteractionCommandKind.Dialogue: return "dialogue";
            case RebirthNpcInteractionCommandKind.Trade: return "trade";
            case RebirthNpcInteractionCommandKind.OpenInventory: return "inventory";
            case RebirthNpcInteractionCommandKind.OpenEquipment: return "equipment";
            case RebirthNpcInteractionCommandKind.Hire: return "hire";
            case RebirthNpcInteractionCommandKind.Dismiss: return "dismiss";
            case RebirthNpcInteractionCommandKind.IssueOrder: return "orders";
            case RebirthNpcInteractionCommandKind.Teach: return "teach";
            default: return kind.ToString().ToLowerInvariant();
        }
    }

    private XUiV_Label Label(string id) { XUiController c = GetChildById(id); return c != null ? c.ViewComponent as XUiV_Label : null; }
    private static void Set(XUiV_Label label, string value) { if (label != null) label.Text = value ?? string.Empty; }
    private void Close() { xui.playerUI.windowManager.Close((GUIWindow)windowGroup); }
}
