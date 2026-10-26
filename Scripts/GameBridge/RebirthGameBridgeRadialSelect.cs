using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

#nullable disable

/// <summary>
/// POST /radial/select  - choose an entry of the native activation radial, the way a player does:
/// aim at the target, HOLD Activate (E) until the radial opens, hover the wanted entry with the (virtual) cursor,
/// then RELEASE Activate so the game itself runs the selected command (XUiC_Radial.Update -> CallContextAction ->
/// the context's command handler: handleEntityCommand / handleBlockCommand).
///
/// Query: entity=ID | x,y,z[&amp;block=1]   target to aim at (omit to use what the crosshair is already on)
///        command=talk | text=Service | index=N   entry to choose (command matches the native command id, or the
///                       block command text; text matches the localized label, exact first then substring; index is menuIndex)
///        [maxOpenSeconds=3] [hoverSeconds=0.8] [expectWindow=<window group id>]
///
/// SINGLE-COMMAND TARGETS (e.g. an NPC that declares only `talk`): the native radial auto-dispatches on the press frame
/// (XUiC_Radial.SetCommonData with one enabled entry) and never becomes visible. When the entity's enabled command list has
/// exactly one entry the bridge taps Activate and proves the effect by expectWindow (default for command=talk:
/// rebirthCompanionInteraction); reply method "single-command-auto-dispatch". Never calls OnEntityActivated or opens UI.
///
/// Fails loudly (4xx, never ok:true) when the radial does not open, the entry is not in it (the reply lists the
/// available entries), or the hover never selected it. A closed radial plus ok:true proves the input was delivered,
/// NOT that the command succeeded: assert the resulting window/buff/item separately.
///
/// Offline-unverified native assumptions (see the report): releasing Activate while an entry is selected runs the
/// command; the cursor hovering an entry sets radial.selectedIndex through XUiC_Radial_OnHover/SetHovered; the radial
/// is shown when ViewComponent.IsVisible and at least one menuItemState entry is set; XUiC_Radial.Close() cancels
/// without running a command.
/// </summary>
public static class RebirthGameBridgeRadialSelect
{
    private sealed class Entry
    {
        internal int MenuIndex, CommandIndex;
        internal string CommandId, Text;
        internal XUiC_RadialEntry Control;
    }

    private static List<Entry> Entries(XUiC_Radial radial)
    {
        var list = new List<Entry>();
        if (radial == null || radial.menuItem == null || radial.menuItemState == null) return list;
        int count = Math.Min(64, radial.menuItem.Length);
        for (int i = 0; i < count; i++)
        {
            XUiC_RadialEntry control = radial.menuItem[i];
            if (control == null || i >= radial.menuItemState.Length || !radial.menuItemState[i]) continue;
            list.Add(new Entry
            {
                MenuIndex = i, CommandIndex = control.CommandIndex, Control = control, Text = control.SelectionText,
                CommandId = RebirthGameBridgeRadialObservation.Command(radial.context, control.CommandIndex)
            });
        }
        return list;
    }

    private static JObject Describe(Entry e, XUiC_Radial radial)
        => new JObject { ["menuIndex"] = e.MenuIndex, ["commandIndex"] = e.CommandIndex, ["commandId"] = e.CommandId,
            ["text"] = e.Text, ["selected"] = radial.selectedIndex == e.MenuIndex };

    private static JArray DescribeAll(List<Entry> entries, XUiC_Radial radial)
    {
        var arr = new JArray();
        foreach (Entry e in entries) arr.Add(Describe(e, radial));
        return arr;
    }

    private static bool WindowOpen(string id)
    {
        foreach (JToken t in RebirthGameBridgeUi.OpenWindowIds()) if (string.Equals((string)t, id, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool IsOpen(XUiC_Radial radial)
    {
        if(radial==null||radial.WindowGroup?.isShowing!=true||Entries(radial).Count==0)return false;
        // XUiC_Radial itself has no ViewComponent in the current native UI;
        // windowRadial is the visible view owned by its window group.
        var window=radial.GetParentByType<XUiController>()?.GetChildById("windowRadial")?.ViewComponent
            ??radial.WindowGroup.Controller?.GetChildById("windowRadial")?.ViewComponent;
        return window?.IsVisible==true;
    }

    private static Entry Pick(List<Entry> entries, string command, string text, int index)
    {
        if (index >= 0) return entries.Find(e => e.MenuIndex == index);
        if (!string.IsNullOrEmpty(command))
        {
            Entry hit = entries.Find(e => string.Equals(e.CommandId, command, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }
        string want = !string.IsNullOrEmpty(text) ? text : command;
        if (string.IsNullOrEmpty(want)) return null;
        Entry exact = entries.Find(e => string.Equals(e.Text, want, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        return entries.Find(e => e.Text != null && e.Text.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0
            || e.CommandId != null && e.CommandId.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    internal static IEnumerator Routine(BridgeRequest req, EntityPlayerLocal p, Func<Vector3?> point, Vector3i? block, int entity)
    {
        var world = p.world;
        // Earlier Bridge hover/click requests can leave an unowned virtual cursor
        // active. Release that Bridge cursor before this request acquires its
        // own cursor scope, otherwise TrySetCursor refuses a legitimate radial.
        RebirthGameBridgeInput.ReleaseCursor();
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => ReferenceEquals(p.world, world)))
        {
            yield return RebirthGameBridgeUi.RunGuarded(Owned(req, p, point, block, entity, scope), scope);
            if (!req.Done.IsSet) req.Fail("Radial selection aborted: " + (scope.Failure ?? "input scope ended") + ". Activate was released.", 409);
        }
    }

    private static IEnumerator Owned(BridgeRequest req, EntityPlayerLocal p, Func<Vector3?> point, Vector3i? block, int entity, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        string command = req.QueryString("command"), text = req.QueryString("text");
        int index = req.QueryInt("index", -1);
        if (string.IsNullOrEmpty(command) && string.IsNullOrEmpty(text) && index < 0)
        { req.Fail("radial select needs command=, text= or index=.", 400); yield break; }
        XUiC_Radial radial = p.PlayerUI?.xui?.RadialWindow;
        if (radial == null) { req.Fail("Native radial window unavailable.", 409); yield break; }
        // The native radial listens to the physical E binding on permanent
        // actions. local.Activate (gamepad Action4) can be held without opening
        // this menu; using it made bridge selections fail despite a live target.
        var activate = p.playerInput.PermanentActions.Activate;
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        if (RebirthGameBridgeUi.AnyModalOpen()) { req.Fail("A modal window is open; close it before opening a radial.", 409); yield break; }
        if (RebirthGameBridgeInput.IsHeld(activate) || activate.IsPressed) { req.Fail("Activate is already held; nothing was released or replaced.", 409); yield break; }

        // Follow the live position with ordinary movement input. A separate walkto request
        // targets a stale point and may take several seconds before this activation begins.
        // Keep this optional and diagnostic: the native Activate path below remains unchanged.
        bool approachedMoving = false;
        if (req.QueryBool("approachBehind", false))
        {
            Entity moving = entity >= 0 ? p.world.GetEntity(entity) : null;
            if (moving == null) { req.Fail("approachBehind requires a live entity=ID.", 400); yield break; }
            float until = Time.realtimeSinceStartup + Mathf.Clamp(req.QueryFloat("approachSeconds", 12f), 1f, 30f);
            float required = Mathf.Max(0.1f, req.QueryFloat("requireEntitySpeed", 0.4f));
            float behind = Mathf.Max(0.1f, req.QueryFloat("requireBehind", 0.25f));
            Vector3 previous = moving.position;
            float previousTime = Time.time;
            bool reached = false;
            try
            {
                while (Time.realtimeSinceStartup < until && scope.Admitted)
                {
                    if (!ReferenceEquals(p.world.GetEntity(entity), moving)) break;
                    Vector3 forward = Quaternion.Euler(0f, moving.rotation.y, 0f) * Vector3.forward;
                    Vector3 toPlayer = p.position - moving.position;
                    toPlayer.y = 0f;
                    Vector3 motion = moving.position - previous;
                    motion.y = 0f;
                    float speed = motion.magnitude / Mathf.Max(0.001f, Time.time - previousTime);
                    previous = moving.position;
                    previousTime = Time.time;
                    float rear = -Vector3.Dot(toPlayer, forward);
                    if (toPlayer.magnitude <= 1.45f && rear >= behind && speed >= required &&
                        RebirthGameBridgePlayer.RequestedTargetMatches(p, block, entity))
                    { reached = true; break; }
                    Vector3 destination = moving.position - forward * 0.95f;
                    Vector3 to = destination - p.position;
                    to.y = 0f;
                    if (to.magnitude > 0.25f)
                    {
                        float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                        if (toPlayer.magnitude <= 3f)
                            RebirthGameBridgePlayer.SmoothAimAt(p, moving.position + Vector3.up, 0.07f);
                        else
                            RebirthGameBridgePlayer.SmoothTurn(p, yaw, -5f, 0.12f);
                        if (Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, yaw)) < 35f)
                        {
                            scope.TryHoldFrames(a.MoveForward, 1);
                            if (to.magnitude > 1.5f) scope.TryHoldFrames(a.Run, 1);
                        }
                    }
                    yield return null;
                }
            }
            finally { scope.Release(a.MoveForward); scope.Release(a.Run); }
            if (!reached)
            { req.Fail("Could not approach a moving entity from behind within the requested time; Activate was not pressed.", 409); yield break; }
            approachedMoving = true;
        }

        bool matched = approachedMoving;
        if (!approachedMoving)
        {
            if (point != null) yield return RebirthGameBridgePlayer.TurnToRoutine(p, point, p.rotation.y, p.rotation.x, 4f, scope);
            yield return null; yield return null;
            yield return RebirthGameBridgePlayer.WaitForRequestedTarget(p, block, entity, () => scope.Admitted, ok => matched = ok);
        }
        if (!matched) { req.Fail("Requested radial target did not settle under the crosshair; Activate was not pressed.", 409); yield break; }
        JObject target = RebirthGameBridgePlayer.DescribeTarget(p);

        // Native single-command auto-dispatch (XUiC_Radial.SetCommonData): the radial is opened on the Activate PRESS frame
        // (PlayerMoveController -> RadialWindow.Open + SetCurrentEntityData) and, when exactly ONE command is enabled, the
        // command runs in that same call (CallContextAction) and the radial never becomes visible. Read the target's enabled
        // commands (cached list the tooltip pass already built; no UpdateActivationCommands, no OnEntityActivated) and, if
        // exactly one is enabled, prove the effect by the expected window group instead of by a radial.
        string expectWindow = req.QueryString("expectWindow");
        if (string.IsNullOrEmpty(expectWindow) && string.Equals(command, "talk", StringComparison.OrdinalIgnoreCase)) expectWindow = "rebirthCompanionInteraction";
        JArray enabledCommands = null; int enabledCount = -1; string onlyId = null;
        Entity targetEntity = entity >= 0 ? p.world.GetEntity(entity) : null;
        if (targetEntity != null)
        {
            enabledCommands = new JArray(); enabledCount = 0;
            EntityActivationCommand[] all = null;
            try { all = targetEntity.GetActivationCommands(); } catch { }
            if (all != null) foreach (var c in all)
            { if (!c.enabled) continue; enabledCount++; onlyId = c.commandId; enabledCommands.Add(new JObject { ["commandId"] = c.commandId, ["text"] = c.commandText }); }
        }
        bool injected1 = false;
        float effectWait = Mathf.Clamp(req.QueryFloat("maxOpenSeconds", 3f), 1f, 10f);
        if (enabledCount == 1)
        {
            bool wantsIt = (!string.IsNullOrEmpty(command) && string.Equals(onlyId, command, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(text) && enabledCommands[0]["text"] != null && ((string)enabledCommands[0]["text"]).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) || index == 0;
            if (!wantsIt)
            { req.Complete(new JObject { ["ok"] = false, ["error"] = "Target has exactly one enabled command (" + onlyId + "); it is not the requested one. Nothing was pressed.", ["enabledCommands"] = enabledCommands }, 404); yield break; }
            if (string.IsNullOrEmpty(expectWindow))
            { req.Fail("Single-command target: its effect cannot be proven without expectWindow=<window group>. Nothing was pressed.", 400); yield break; }
            float requiredSpeed = Mathf.Max(0f, req.QueryFloat("requireEntitySpeed", 0f));
            float requiredBehind = Mathf.Max(0f, req.QueryFloat("requireBehind", 0f));
            float observedSpeed = -1f, behindDistance = float.NaN;
            if (requiredSpeed > 0f || requiredBehind > 0f)
            {
                if (targetEntity == null)
                { req.Fail("Movement/behind preconditions require entity=ID. Activate was not pressed.", 400); yield break; }
                Vector3 before = targetEntity.position;
                float beforeTime = Time.time;
                yield return null;
                yield return null;
                Vector3 travel = targetEntity.position - before;
                travel.y = 0f;
                observedSpeed = travel.magnitude / Mathf.Max(0.001f, Time.time - beforeTime);
                Vector3 toPlayer = p.position - targetEntity.position;
                toPlayer.y = 0f;
                Vector3 forward = Quaternion.Euler(0f, targetEntity.rotation.y, 0f) * Vector3.forward;
                behindDistance = -Vector3.Dot(toPlayer, forward);
                if (observedSpeed < requiredSpeed || behindDistance < requiredBehind ||
                    !RebirthGameBridgePlayer.RequestedTargetMatches(p, block, entity))
                {
                    req.Complete(new JObject { ["ok"] = false, ["error"] = "Moving-behind precondition was not met on the game thread; Activate was not pressed.",
                        ["entitySpeed"] = observedSpeed, ["behindDistance"] = behindDistance,
                        ["requiredEntitySpeed"] = requiredSpeed, ["requiredBehind"] = requiredBehind,
                        ["targetAfter"] = RebirthGameBridgePlayer.DescribeTarget(p) }, 409);
                    yield break;
                }
            }
            if (!scope.TryHoldFrames(activate, 3, 1f, () => RebirthGameBridgePlayer.RequestedTargetMatches(p, block, entity), () => injected1 = true))
            { req.Fail("Owned Activate admission refused.", 409); yield break; }
            float effectBy = Time.realtimeSinceStartup + effectWait;
            while (Time.realtimeSinceStartup < effectBy && !WindowOpen(expectWindow)) yield return null;
            bool proven = WindowOpen(expectWindow);
            scope.Release(activate);
            for (int i = 0; i < 3; i++) yield return null;
            if (!proven)
            { req.Complete(new JObject { ["ok"] = false, ["error"] = "Single-command auto-dispatch did not open " + expectWindow + " within " + effectWait + " s.", ["enabledCommands"] = enabledCommands,
                ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds(), ["injected"] = injected1,
                ["targetBefore"] = target, ["targetAfter"] = RebirthGameBridgePlayer.DescribeTarget(p),
                ["entityDistanceAfter"] = targetEntity == null ? -1f : Vector3.Distance(p.position, targetEntity.position) }, 409); yield break; }
            req.Complete(new JObject { ["method"] = "single-command-auto-dispatch", ["command"] = onlyId, ["effectWindow"] = expectWindow, ["enabledCommands"] = enabledCommands,
                ["entitySpeedBeforePress"] = observedSpeed, ["behindDistanceBeforePress"] = float.IsNaN(behindDistance) ? -1f : behindDistance,
                ["target"] = target, ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds(), ["note"] = "One enabled command: the native radial dispatched it on the press frame; proven by the window opening." });
            yield break;
        }

        // 1. Hold Activate (safety cap 20 s; released explicitly below or by scope disposal).
        bool injected = false;
        if (!scope.TryHoldSeconds(activate, 20f, 1f, () => RebirthGameBridgePlayer.RequestedTargetMatches(p, block, entity), () => injected = true))
        { req.Fail("Owned Activate hold admission refused.", 409); yield break; }

        // 2. Wait for the native radial to show (stable for 3 frames).
        float openBy = Time.realtimeSinceStartup + Mathf.Clamp(req.QueryFloat("maxOpenSeconds", 3f), 1f, 10f);
        int stable = 0;
        while (Time.realtimeSinceStartup < openBy && stable < 3)
        {
            stable = IsOpen(radial) ? stable + 1 : 0;
            if (stable == 0 && !string.IsNullOrEmpty(expectWindow) && WindowOpen(expectWindow)) break; // auto-dispatched (enabled list unknown)
            yield return null;
        }
        if (stable < 3 && !string.IsNullOrEmpty(expectWindow) && WindowOpen(expectWindow))
        {
            scope.Release(activate);
            for (int i = 0; i < 3; i++) yield return null;
            req.Complete(new JObject { ["method"] = "single-command-auto-dispatch", ["effectWindow"] = expectWindow, ["target"] = target,
                ["enabledCommands"] = enabledCommands, ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds() });
            yield break;
        }
        if (stable < 3)
        {
            scope.Release(activate);
            for (int i = 0; i < 5; i++) yield return null;
            req.Fail("The activation radial did not open while holding Activate on the target (injected=" + injected
                + "). A target with a single quick action may have run it directly or opened a window instead. openWindows="
                + RebirthGameBridgeUi.OpenWindowIds().ToString(Newtonsoft.Json.Formatting.None)
                + " enabledCommands=" + (enabledCommands == null ? "unknown" : enabledCommands.ToString(Newtonsoft.Json.Formatting.None)), 409);
            yield break;
        }
        yield return null;
        List<Entry> entries = Entries(radial);
        Entry wanted = Pick(entries, command, text, index);
        if (wanted == null)
        {
            JArray available = DescribeAll(entries, radial);
            try { radial.Close(); } catch { }   // cancel without running a command, then let go of E
            yield return null;
            scope.Release(activate);
            for (int i = 0; i < 5; i++) yield return null;
            req.Complete(new JObject { ["ok"] = false, ["error"] = "Radial entry not found (command=" + command + ", text=" + text + ", index=" + index + ")",
                ["available"] = available, ["target"] = target }, 404);
            yield break;
        }

        // 3. Hover the entry with the cursor (the native mouse path), verify the radial selected it.
        string method = "cursor";
        Vector2 center;
        if (RebirthGameBridgeUi.TryScreenCenter(wanted.Control, out center) && scope.TrySetCursor(center))
        {
            float until = Time.realtimeSinceStartup + Mathf.Clamp(req.QueryFloat("hoverSeconds", 0.8f), 0.3f, 5f);
            int ok = 0;
            while (Time.realtimeSinceStartup < until && ok < 3) { ok = radial.selectedIndex == wanted.MenuIndex ? ok + 1 : 0; yield return null; }
            if (ok < 3) method = "cursor-missed";
        }
        else method = "no-cursor";
        if (radial.selectedIndex != wanted.MenuIndex)
        {
            // Same call the entry's hover handler makes (XUiC_Radial_OnHover -> SetHovered).
            radial.SetHovered(wanted.Control);
            method += "+SetHovered";
            for (int i = 0; i < 3; i++) yield return null;
        }
        if (radial.selectedIndex != wanted.MenuIndex)
        {
            JArray available = DescribeAll(Entries(radial), radial);
            try { radial.Close(); } catch { }
            yield return null;
            scope.Release(activate);
            for (int i = 0; i < 5; i++) yield return null;
            req.Complete(new JObject { ["ok"] = false, ["error"] = "Radial never selected the wanted entry (selectedIndex=" + radial.selectedIndex + ", wanted=" + wanted.MenuIndex + ")",
                ["available"] = available, ["method"] = method }, 409);
            yield break;
        }
        JObject chosen = Describe(wanted, radial);

        // 4. Release Activate: the game runs the selected command.
        scope.Release(activate);
        float closeBy = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < closeBy && IsOpen(radial)) yield return null;
        for (int i = 0; i < 6; i++) yield return null;
        bool closed = !IsOpen(radial);
        if (!closed)
        {
            req.Complete(new JObject { ["ok"] = false, ["error"] = "Radial stayed open after Activate was released; the command did not run.",
                ["selected"] = chosen, ["method"] = method, ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds() }, 409);
            yield break;
        }
        req.Complete(new JObject { ["selected"] = chosen, ["method"] = method, ["radialClosed"] = true, ["target"] = target,
            ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds(),
            ["note"] = "Input delivered; assert the command's effect (window, buff, item) separately." });
    }
}
