using System;
using UnityEngine;
using UnityEngine.Scripting;

// One enlarged offer preview; only its explicit button may request acceptance.
[Preserve]
public sealed class XUiC_RebirthTraderJobPopup : XUiController
{
    internal static XUiC_RebirthTraderJobCard HoveredCard;
    private static XUiC_RebirthTraderJobCard requestedFocus, showingCard;
    private static int requestVersion;
    private XUiC_RebirthTraderJobCard source, rejectedCard;
    private object acceptanceToken;
    private XUiV_Label text;
    private XUiV_Texture background, image;
    private XUiV_Sprite placeholder;
    private XUiView frame, reader, viewport;
    private XUiC_RebirthReadableText readingController;
    private XUiC_SimpleButton accept;
    private float showAt, nextTextRefresh, leaveAfter;
    private int seenRequest;
    private bool accepting;

    internal static void ShowForCard(XUiC_RebirthTraderJobCard card, bool focusAccept)
    {
        HoveredCard = card;
        requestedFocus = focusAccept ? card : null;
        unchecked { requestVersion++; }
    }
    internal static bool IsShowingFor(XUiC_RebirthTraderJobCard card) => ReferenceEquals(showingCard, card);

    public override void Init()
    {
        base.Init();
        text = GetChildById("jobDetailText")?.ViewComponent as XUiV_Label;
        background = GetChildById("jobDetailBackground")?.ViewComponent as XUiV_Texture;
        image = GetChildById("jobPreview")?.ViewComponent as XUiV_Texture;
        placeholder = GetChildById("jobPreviewPlaceholder")?.ViewComponent as XUiV_Sprite;
        frame = GetChildById("jobDetailFrame")?.ViewComponent;
        reader = GetChildById("jobDetailReader")?.ViewComponent;
        readingController = GetChildById("jobDetailReader") as XUiC_RebirthReadableText;
        viewport = GetChildById("readableTextViewport")?.ViewComponent;
        accept = GetChildById("jobDetailAccept") as XUiC_SimpleButton;
        if (accept != null) accept.OnPressed += AcceptPressed;
        if (background != null)
        {
            background.AutoUnload = false;
            background.Texture = Texture2D.whiteTexture;
            background.Color = new Color32(24,24,29,250);
        }
        // The card owns this texture. Clearing the preview never unloads it.
        if (image != null) image.AutoUnload = false;
    }
    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if (source != null && source.GetBindingValueInternal(ref value, bindingName)) return true;
        // XUi resolves a binding's owner while constructing the hidden preview.
        // Recognize its card bindings even before the first hover supplies a source.
        switch (bindingName)
        {
            case "jobcardvisible": case "jobpreviewvisible": case "jobplaceholdervisible":
            case "jobinfestedvisible": case "jobhighlightvisible": value = "false"; return true;
            case "jobstripcolor": value = "10,18,12,238"; return true;
            case "joblinecolor": value = "70,108,72,225"; return true;
            case "jobbiomecolor": value = "220,220,210,255"; return true;
            case "jobbackgroundsprite": value = "rb_job_frame_normal"; return true;
            case "jobframesprite": value = "rb_job_frame_normal_outline"; return true;
            case "jobhighlightsprite": value = "rb_job_card_hover"; return true;
            case "jobplaceholdersprite": value = "rb_preview_missing"; return true;
            case "jobbiomesprite": value = "rb_biome_unknown"; return true;
            case "jobtypesprite": value = "rb_job_special"; return true;
            case "jobpreviewuri": case "jobbiome": case "jobtier":
            case "jobdistancedisplay": case "jobprefabname": case "jobcompletionhistory":
                value = string.Empty; return true;
        }
        return base.GetBindingValueInternal(ref value, bindingName);
    }
    public override void OnOpen() { base.OnOpen(); rejectedCard = null; Hide(); }
    public override void OnClose()
    {
        if (HoveredCard?.xui == xui) HoveredCard = null;
        if (requestedFocus?.xui == xui) requestedFocus = null;
        rejectedCard = null;
        Hide(); base.OnClose();
    }
    public override void Cleanup()
    {
        if (accept != null) accept.OnPressed -= AcceptPressed;
        if (HoveredCard?.xui == xui) HoveredCard = null;
        if (requestedFocus?.xui == xui) requestedFocus = null;
        Hide(); base.Cleanup();
    }
    private void Visible(bool value)
    {
        if (ViewComponent == null) return;
        if (ViewComponent.IsVisible != value) ViewComponent.IsVisible = value;
        var root = ViewComponent.UiTransform;
        if (root != null && root.gameObject.activeSelf != value) root.gameObject.SetActive(value);
        if (value) showingCard = source;
        else if (ReferenceEquals(showingCard, source)) showingCard = null;
    }
    private void Hide()
    {
        Visible(false); source = null; acceptanceToken = null; nextTextRefresh = 0; leaveAfter = 0;
        if (accept != null) accept.Enabled = false;
        if (image != null) image.Texture = null;
    }
    private bool ValidSource(XUiC_RebirthTraderJobCard card) => card != null && card.xui == xui
        && card.windowGroup == windowGroup && card.CardMode && card.CurrentResponse is DialogResponseQuest
        && card.ViewComponent?.UiTransform != null && card.ViewComponent.IsVisible
        && card.ViewComponent.IsActiveInHierarchy && windowGroup != null && windowGroup.isShowing;
    private bool Inside(Transform pointer, XUiView view) => pointer != null && view?.UiTransform != null
        && pointer.IsChildOf(view.UiTransform);

    private void AcceptPressed(XUiController sender, int mouseButton)
    {
        if ((mouseButton != -1 && mouseButton != 0) || accepting || accept == null || !ReferenceEquals(sender, accept)
            || ViewComponent?.IsVisible != true || !accept.Enabled) return;
        var card = source;
        var token = acceptanceToken;
        if (!ValidSource(card) || token == null || !card.IsPreviewAcceptanceCurrent(token)
            || !ReferenceEquals(source, card) || !ReferenceEquals(acceptanceToken, token)) return;
        accepting = true;
        try
        {
            bool accepted = card.TryAcceptPreview(token);
            // Failed/pending requests keep the visible offer. Do not repaint a replacement context.
            if (accepted && ReferenceEquals(source, card) && ReferenceEquals(acceptanceToken, token)) { rejectedCard = card; Hide(); }
        }
        finally { accepting = false; }
    }

    public override void Update(float dt)
    {
        var candidate = HoveredCard;
        bool requested = seenRequest != requestVersion && candidate?.xui == xui;
        if (requested) { seenRequest = requestVersion; if (!ReferenceEquals(candidate, rejectedCard) || ReferenceEquals(requestedFocus, candidate)) rejectedCard = null; }
        var pointer = UICamera.hoveredObject?.transform;
        var navigation = xui?.playerUI?.CursorController?.navigationTarget;
        bool popupVisible = ViewComponent?.IsVisible == true;
        bool inPopup = popupVisible && Inside(pointer, ViewComponent);
        bool focusInPopup = popupVisible && Inside(navigation?.UiTransform, ViewComponent);
        if ((inPopup || focusInPopup) && source != null && !requested) candidate = source;
        if (text == null || background == null || image == null || placeholder == null || frame == null
            || reader == null || viewport == null || accept == null || !ValidSource(candidate))
        { Hide(); return; }
        if (ReferenceEquals(candidate, rejectedCard)) { Hide(); return; }
        bool inSource = Inside(pointer, candidate.ViewComponent);
        bool focusInSource = Inside(navigation?.UiTransform, candidate.ViewComponent);
        if (!requested && !inPopup && !focusInPopup && !inSource && !focusInSource)
        {
            if (source == candidate && popupVisible && Time.realtimeSinceStartup < leaveAfter)
            { base.Update(dt); return; }
            Hide(); return;
        }
        leaveAfter = Time.realtimeSinceStartup + .25f;
        if (source != candidate)
        {
            Hide(); source = candidate;
            acceptanceToken = source.CapturePreviewAcceptance();
            showAt = Time.realtimeSinceStartup + (requested ? 0f : .2f);
            readingController?.ResetReadingPosition();
        }
        if (acceptanceToken == null || !source.IsPreviewAcceptanceCurrent(acceptanceToken))
        { rejectedCard = source; Hide(); return; }
        if (Time.realtimeSinceStartup < showAt) return;
        if (Time.realtimeSinceStartup >= nextTextRefresh)
        {
            nextTextRefresh = Time.realtimeSinceStartup + .25f;
            string details = source.ReadableDetails();
            if (string.IsNullOrEmpty(details)) { Hide(); return; }
            if (text.Text != details) text.Text = details;
            var sharedTexture = source.PreviewTexture;
            if (image.Texture != sharedTexture) image.Texture = sharedTexture;
            image.UVRect = source.PreviewUVRect;
            image.IsVisible = image.Texture != null;
            placeholder.SpriteName = source.PreviewPlaceholderSprite;
            placeholder.IsVisible = image.Texture == null;
        }
        var parent = Parent?.ViewComponent;
        var camera = xui?.playerUI?.camera;
        if (parent?.UiTransform == null || camera == null) { Hide(); return; }
        var pixel = camera.pixelRect;
        float depth = camera.WorldToScreenPoint(parent.UiTransform.position).z;
        var low = parent.UiTransform.InverseTransformPoint(camera.ScreenToWorldPoint(new Vector3(pixel.xMin,pixel.yMin,depth)));
        var high = parent.UiTransform.InverseTransformPoint(camera.ScreenToWorldPoint(new Vector3(pixel.xMax,pixel.yMax,depth)));
        float minX = Math.Min(low.x,high.x)+12, maxX = Math.Max(low.x,high.x)-12;
        float minY = Math.Min(low.y,high.y)+12, maxY = Math.Max(low.y,high.y)-12;
        int width = Math.Min(318,(int)(maxX-minX));
        int height = Math.Min(392,(int)(maxY-minY));
        if (width < 240 || height < 260) { Hide(); return; }
        Resize(ViewComponent,width,height); Resize(background,width,height); Resize(frame,width,height);
        var cardView = GetChildById("jobDetailCard")?.ViewComponent;
        if (cardView?.UiTransform != null)
            cardView.UiTransform.localScale = Vector3.one * (width / 294f);
        Place(accept.ViewComponent,8,-height+44,width-16,36);
        var acceptLabel = accept.GetChildById("btnLabel")?.ViewComponent;
        if (acceptLabel != null) Place(acceptLabel,(width-16)/2,-18,width-24,30);
        RefreshBindings();
        var sourceArrow = source.GetChildById("directionArrow")?.ViewComponent;
        var previewArrow = GetChildById("directionArrow")?.ViewComponent;
        if (sourceArrow != null && previewArrow != null) previewArrow.Rotation = sourceArrow.Rotation;
        var point = parent.UiTransform.InverseTransformPoint(source.ViewComponent.UiTransform.position);
        var position = new Vector2i((int)Mathf.Clamp(point.x-(width-source.ViewComponent.Size.x)/2f,minX,maxX-width),
            (int)Mathf.Clamp(point.y+16,minY+height,maxY));
        if (ViewComponent.Position != position) { ViewComponent.Position = position; ViewComponent.TryUpdatePosition(); }
        Visible(true);
        accept.Enabled = !accepting && source.IsPreviewAcceptanceCurrent(acceptanceToken);
        if (requestedFocus == source)
        {
            requestedFocus = null;
            accept.SelectCursorElement(false,true);
        }
        base.Update(dt);
    }
    private static void Place(XUiView view,int x,int y,int width,int height)
    {
        Resize(view,width,height);
        var position = new Vector2i(x,y);
        if (view.Position != position) { view.Position = position; view.TryUpdatePosition(); }
    }
    private static void Resize(XUiView view,int width,int height)
    {
        var size = new Vector2i(width,height);
        if (view.Size != size) view.Size = size;
    }
}
