using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthPlayerHelp : XUiController
{
    private static long sequence;
    private readonly RebirthWindowHudScope hud = new RebirthWindowHudScope();
    private long pending;
    private bool opened, allowed, waiting, confirmForce;
    private float deadline, nextPermissionCheck;
    private int screenWidth, screenHeight;
    private string result = Localization.Get("xuiRebirthPlayerHelpChecking");
    private XUiController actions;
    private XUiV_Label resultLabel, accessLabel;
    private XUiC_SimpleButton force;

    public override void Init()
    {
        base.Init();
        actions = GetChildById("sleeperActions");
        resultLabel = GetChildById("helpResult").ViewComponent as XUiV_Label;
        accessLabel = GetChildById("helpAccess").ViewComponent as XUiV_Label;
        Bind("helpStatus", RebirthPlayerHelpService.Operation.Status);
        Bind("helpStart", RebirthPlayerHelpService.Operation.Start);
        Bind("helpStop", RebirthPlayerHelpService.Operation.Stop);
        force = GetChildById("helpForce").GetChildByType<XUiC_SimpleButton>();
        force.OnPressed += (sender, button) =>
        {
            if (!IsPrimaryPress(button) || waiting || !allowed) return;
            if (!confirmForce)
            {
                confirmForce = true;
                result = Localization.Get("xuiRebirthPlayerHelpForceConfirmation");
                Render();
                return;
            }
            Send(RebirthPlayerHelpService.Operation.Force);
        };
        GetChildById("helpRefresh").GetChildByType<XUiC_SimpleButton>().OnPressed += (s, b) => { if (IsPrimaryPress(b) && !waiting) Send(RebirthPlayerHelpService.Operation.Permissions); };
        GetChildById("helpClose").GetChildByType<XUiC_SimpleButton>().OnPressed += (s, b) => { if (IsPrimaryPress(b)) Close(); };
    }

    private void Bind(string id, RebirthPlayerHelpService.Operation operation)
    {
        GetChildById(id).GetChildByType<XUiC_SimpleButton>().OnPressed += (s, b) =>
        { if (IsPrimaryPress(b) && allowed && !waiting) Send(operation); };
    }

    private static bool IsPrimaryPress(int button) => button == 0 || button == -1;

    public override void OnOpen()
    {
        base.OnOpen();
        opened = true; allowed = false; waiting = false; confirmForce = false;
        result = Localization.Get("xuiRebirthPlayerHelpChecking");
        RebirthPlayerHelpService.Reply += Receive;
        Fit(); hud.Maintain(xui);
        Send(RebirthPlayerHelpService.Operation.Permissions);
    }

    public override void OnClose()
    {
        opened = false; pending = 0;
        RebirthPlayerHelpService.Reply -= Receive;
        hud.Restore(); base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (!opened) return;
        hud.Maintain(xui);
        var screen = xui.GetXUiScreenSize();
        if (screenWidth != (int)screen.x || screenHeight != (int)screen.y) Fit();
        if (waiting && Time.realtimeSinceStartup >= deadline)
        {
            waiting = false; pending = 0; allowed = false;
            result = Localization.Get("xuiRebirthPlayerHelpTimeout");
            Render();
        }
        // Refresh the visibility decision, but never perform a gameplay action automatically.
        if (!waiting && !confirmForce && Time.realtimeSinceStartup >= nextPermissionCheck)
            Send(RebirthPlayerHelpService.Operation.Permissions);
        if (XUiUtils.HotkeysAllowedFor(ViewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased || xui.playerUI.playerInput.GUIActions.Cancel.WasReleased)) Close();
    }

    private void Send(RebirthPlayerHelpService.Operation operation)
    {
        confirmForce = false; pending = Interlocked.Increment(ref sequence); waiting = true;
        deadline = Time.realtimeSinceStartup + 8f; nextPermissionCheck = Time.realtimeSinceStartup + 10f;
        if (operation != RebirthPlayerHelpService.Operation.Permissions) result = Localization.Get("xuiRebirthPlayerHelpCommandPending");
        Render();
        RebirthPlayerHelpService.Request(pending, operation);
    }

    private void Receive(long token, bool permitted, string message)
    {
        if (!opened || token != pending) return;
        waiting = false; allowed = permitted;
        // Permission refreshes must not erase a useful command result.
        if (!permitted || result == Localization.Get("xuiRebirthPlayerHelpChecking") || result == Localization.Get("xuiRebirthPlayerHelpCommandPending") || result == Localization.Get("xuiRebirthPlayerHelpTimeout")) result = message;
        Render();
    }

    private void Render()
    {
        actions.ViewComponent.IsVisible = allowed;
        accessLabel.Text = allowed ? (waiting ? Localization.Get("xuiRebirthPlayerHelpServerPending") : Localization.Get("xuiRebirthPlayerHelpAvailable")) : (waiting ? Localization.Get("xuiRebirthPlayerHelpPermissionsPending") : Localization.Get("xuiRebirthPlayerHelpPermissionRequired"));
        resultLabel.Text = result;
    }

    private void Fit()
    {
        var screen = xui.GetXUiScreenSize();
        screenWidth = (int)screen.x; screenHeight = (int)screen.y;
        float scale = Mathf.Min(1f, Mathf.Min((screen.x - 48) / 960f, (screen.y - 48) / 660f));
        ViewComponent.Position = new Vector2i(-Mathf.RoundToInt(480 * scale), Mathf.RoundToInt(330 * scale));
        ViewComponent.TryUpdatePosition();
        ViewComponent.UiTransform.localScale = new Vector3(scale, scale, 1);
    }

    private void Close() => xui.playerUI.windowManager.Close((GUIWindow)WindowGroup);
}
