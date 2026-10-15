using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

// Presentation of the observed authoritative countdown only. No local deadline,
// speculative mode, time subtraction or client-side completion is introduced.
[Preserve]
public sealed class XUiC_RebirthCamperTransitionHud : XUiController
{
    private static EntityVehicle requestedVehicle;
    private static EntityPlayerLocal requestingPlayer;
    private EntityVehicle observedVehicle;
    private XUiView card;
    private bool closed, visible;
    private float nextObservation, fill;
    private string caption = string.Empty;
    internal static void Track(EntityVehicle vehicle, EntityPlayerLocal player)
    {
        if (!ThreadManager.IsMainThread() || player == null || !RebirthCamperRadial.IsCamper(vehicle)) return;
        requestedVehicle = vehicle; requestingPlayer = player;
    }
    public override void Init()
    {
        base.Init();
        card = GetChildById("camperTransitionCard")?.ViewComponent;
        if (card != null) card.IsVisible = false;
        closed = false;
    }
    public override void OnOpen() { base.OnOpen(); closed = false; nextObservation = 0; }
    public override void OnClose() { closed = true; Hide(); base.OnClose(); }
    public override void Cleanup()
    {
        closed = true; Hide(); observedVehicle = null;
        if (ReferenceEquals(requestingPlayer, xui?.playerUI?.entityPlayer))
        { requestedVehicle = null; requestingPlayer = null; }
        base.Cleanup();
    }
    private void Hide()
    {
        visible = false;
        if (card != null) card.IsVisible = false;
    }
    private static bool Observe(EntityVehicle vehicle, EntityPlayerLocal player,
        out RebirthCamperEnergyMode mode, out float remaining)
    {
        mode = RebirthCamperEnergyMode.Travel; remaining = 0;
        if (player == null || player.IsDead() || vehicle == null || vehicle.IsDead() || vehicle.world == null
            || !ReferenceEquals(GameManager.Instance?.World, vehicle.world) || !ReferenceEquals(player.world, vehicle.world)
            || !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId), vehicle)
            || !ReferenceEquals(vehicle.world.GetEntity(player.entityId), player)
            || (vehicle.position - player.position).sqrMagnitude > 64f || !RebirthCamperRadial.IsCamper(vehicle)) return false;
        var owner = vehicle.GetOwner();
        var data = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        return owner != null && data != null && (owner.Equals(data.PrimaryId) || owner.Equals(data.NativeId))
            && RebirthCamperEnergyRuntime.TryObservePresentation(vehicle, out mode, out _, out _, out remaining)
            && (mode == RebirthCamperEnergyMode.Deploying || mode == RebirthCamperEnergyMode.Packing)
            && !float.IsNaN(remaining) && !float.IsInfinity(remaining) && remaining > 0;
    }
    private bool Select(EntityPlayerLocal player, out RebirthCamperEnergyMode mode, out float remaining)
    {
        mode = RebirthCamperEnergyMode.Travel; remaining = 0;
        if (ReferenceEquals(requestingPlayer, player) && Observe(requestedVehicle, player, out mode, out remaining))
        { observedVehicle = requestedVehicle; return true; }
        if (Observe(observedVehicle, player, out mode, out remaining)) return true;
        var selected = xui?.Vehicle?.CurrentVehicle;
        if (Observe(selected, player, out mode, out remaining)) { observedVehicle = selected; return true; }
        selected = XUiC_RebirthVehicleHudStatBar.ResolveLookVehicle(player);
        if (Observe(selected, player, out mode, out remaining)) { observedVehicle = selected; return true; }
        observedVehicle = null;
        return false;
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        var player = xui?.playerUI?.entityPlayer;
        var windows = xui?.playerUI?.windowManager;
        if (closed || player == null || player.IsDead() || windows == null
            || windows.IsCursorWindowOpen() || windows.IsModalWindowOpen()
            || RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
        {
            // Remember the native Energy interaction's vehicle while its UI is
            // open, so closing that window can reveal an accepted transition
            // without requiring the player to reacquire the camper with the ray.
            var selected = xui?.Vehicle?.CurrentVehicle;
            if (!closed && player != null && !player.IsDead() && RebirthCamperRadial.IsCamper(selected)) observedVehicle = selected;
            Hide(); nextObservation = 0; return;
        }
        float now = Time.realtimeSinceStartup;
        if (now < nextObservation && now >= nextObservation - .2f) return;
        nextObservation = now + .2f;
        bool wasVisible = visible;
        string previousCaption = caption; float previousFill = fill;
        visible = Select(player, out var mode, out float remaining);
        if (visible)
        {
            var config = RebirthCamperEnergyConfiguration.Current;
            float duration = mode == RebirthCamperEnergyMode.Deploying ? config.DeploySeconds : config.PackSeconds;
            fill = Mathf.Clamp01(1f - remaining / Mathf.Max(.001f, duration));
            caption = string.Format(CultureInfo.InvariantCulture,
                Localization.Get(mode == RebirthCamperEnergyMode.Deploying ? "xuiRebirthEnergyHudDeploying" : "xuiRebirthEnergyHudPacking"),
                Mathf.CeilToInt(remaining));
        }
        else { caption = string.Empty; fill = 0; }
        if (card != null) card.IsVisible = visible;
        if (visible != wasVisible || caption != previousCaption || !Mathf.Approximately(fill, previousFill)) RefreshBindings();
    }
    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        switch (bindingName)
        {
            case "campertransitioncaption": value = caption; return true;
            case "campertransitionfill": value = fill.ToString("0.####", CultureInfo.InvariantCulture); return true;
        }
        return base.GetBindingValueInternal(ref value, bindingName);
    }
}
