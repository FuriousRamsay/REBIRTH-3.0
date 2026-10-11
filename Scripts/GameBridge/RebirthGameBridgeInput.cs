using System.Collections.Generic;
using System.Reflection;
using InControl;
using Platform;

#nullable disable

/// <summary>
/// Virtual input for the Game Bridge. Feeds values into the game's own InControl actions (movement,
/// attack, activate, toolbelt, UI clicks...), drives the soft cursor, and reports virtual modifier keys.
/// Everything goes through the same code paths as a real keyboard/mouse, so game logic and REBIRTH
/// Harmony patches behave exactly as they do for a player.
///
/// Real input is never blocked: InControl keeps the strongest value per frame, and moving the physical
/// mouse immediately returns the cursor to the user.
///
/// Patches are only installed when the bridge is enabled (-rebirthbridge).
/// </summary>
public static class RebirthGameBridgeInput
{
    private sealed class Held
    {
        public float Value;
        public OwnedInputScope Owner;
        public Func<bool> Proof;
        public Action FirstInjection;
        public bool FirstInjectionNotified;
        public int UntilFrame = int.MaxValue;     // inclusive
        public float UntilTime = float.MaxValue;  // realtimeSinceStartup
    }

    private static readonly Dictionary<PlayerAction, Held> held = new Dictionary<PlayerAction, Held>();
    private static readonly Dictionary<string, FieldInfo> actionFields = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
    private static bool installed;

    private sealed class FlagWrite
    {
        internal readonly bool Value;
        internal readonly OwnedInputScope Owner;
        internal readonly FlagWrite Previous;
        internal FlagWrite(bool value, OwnedInputScope owner, FlagWrite previous)
        { Value = value; Owner = owner; Previous = previous; }
    }
    private static FlagWrite shiftWrite = new FlagWrite(false, null, null);
    private static FlagWrite lockWrite = new FlagWrite(false, null, null);
    public static bool Shift { get { return ReadFlag(true); } set { shiftWrite = new FlagWrite(value, null, null); } }
    public static bool Control, Alt;
    private static bool ReadFlag(bool shift)
    {
        FlagWrite current = shift ? shiftWrite : lockWrite;
        if (current.Owner == null) return current.Value;
        bool admitted = current.Owner.Admitted;
        if (!ReferenceEquals(current, shift ? shiftWrite : lockWrite)) return false;
        if (!admitted || !current.Owner.OwnershipCurrent) { current.Owner.Dispose(); return false; }
        return current.Value;
    }
    /// <summary>While true (an instinct is clicking through a menu), moving the real mouse does not take the cursor back.</summary>
    public static bool LockCursor { get { return ReadFlag(false); } set { lockWrite = new FlagWrite(value, null, null); } }
    private sealed class CursorWrite
    {
        internal readonly OwnedInputScope Owner;
        internal readonly CursorWrite Previous;
        internal CursorWrite(OwnedInputScope owner, CursorWrite previous) { Owner = owner; Previous = previous; }
    }
    private static OwnedInputScope cursorOwner;
    private static CursorWrite cursorWrite = new CursorWrite(null, null);
    /// <summary>UI pacing: 1 = human speed (watchable), &lt;1 faster (bulk tests), &gt;1 slower.</summary>
    public static float Pace = 0.55f;

    // Virtual cursor, in Unity screen coordinates (origin bottom-left).
    public static bool CursorActive { get; private set; }
    public static Vector2 CursorPosition { get; private set; }
    private static Vector3 realMouseAtActivation;

    public static void Install(Harmony harmony)
    {
        if (installed) return;
        installed = true;

        harmony.Patch(AccessTools.Method(typeof(PlayerAction), "UpdateBindings"),
            prefix: new HarmonyMethod(typeof(RebirthGameBridgeInput), nameof(UpdateBindingsPrefix)));

        PatchGetter(harmony, typeof(InputUtils), nameof(InputUtils.ShiftKeyPressed), nameof(ShiftPostfix));
        PatchGetter(harmony, typeof(InputUtils), nameof(InputUtils.ControlKeyPressed), nameof(ControlPostfix));
        PatchGetter(harmony, typeof(InputUtils), nameof(InputUtils.AltKeyPressed), nameof(AltPostfix));

        foreach (Type cursorType in new[] { typeof(SoftCursor), AccessTools.TypeByName("FlexibleCursor") })
        {
            if (cursorType == null) continue;
            MethodInfo getPos = AccessTools.DeclaredMethod(cursorType, "GetScreenPosition", Type.EmptyTypes);
            if (getPos != null) harmony.Patch(getPos, postfix: new HarmonyMethod(typeof(RebirthGameBridgeInput), nameof(GetScreenPositionPostfix)));
        }
        MethodInfo handleMovement = AccessTools.DeclaredMethod(typeof(SoftCursor), "HandleMovement");
        if (handleMovement != null)
            harmony.Patch(handleMovement, postfix: new HarmonyMethod(typeof(RebirthGameBridgeInput), nameof(HandleMovementPostfix)));

        // Keep InControl updating while the game window is in the background, so the bridge can drive
        // the game while the user works in another window.
        InputManager.SuspendInBackground = false;
    }

    private static void PatchGetter(Harmony harmony, Type type, string property, string postfix)
    {
        MethodInfo getter = AccessTools.PropertyGetter(type, property);
        if (getter != null) harmony.Patch(getter, postfix: new HarmonyMethod(typeof(RebirthGameBridgeInput), postfix));
    }

    // ------------------------------------------------------------------ patches

    private static void UpdateBindingsPrefix(PlayerAction __instance, ulong updateTick, float deltaTime)
    {
        if (held.Count == 0) return;
        Held h;
        if (!held.TryGetValue(__instance, out h)) return;
        if (h.Owner != null && !h.Owner.Admitted)
        { h.Owner.Dispose(); return; }
        Held currentEntry;
        if (h.Owner != null && (!held.TryGetValue(__instance, out currentEntry) || !ReferenceEquals(currentEntry, h))) return;
        if (Time.frameCount > h.UntilFrame || Time.realtimeSinceStartup > h.UntilTime)
        {
            held.Remove(__instance);
            if (h.Owner != null) h.Owner.Expired(__instance);
            return;
        }
        if (h.Owner != null && h.Proof != null)
        {
            bool allowed;
            try { allowed = h.Proof(); } catch { allowed = false; }
            if (!allowed || !h.Owner.Admitted) { h.Owner.Refuse("owned action item proof refused"); h.Owner.Dispose(); return; }
        }
        // This fence is deliberately predicate-free and last: callbacks may have released/replaced the held entry.
        if (h.Owner != null && (!h.Owner.OwnershipCurrent || !held.TryGetValue(__instance, out currentEntry) || !ReferenceEquals(currentEntry, h)))
        { h.Owner.Dispose(); return; }
        // Same call the real binding sources make; InControl keeps the strongest value this tick.
        if (h.Owner == null) __instance.UpdateWithValue(h.Value, updateTick, deltaTime);
        else
        {
            var transferPhase = h.Owner.TransferPhase;
            try { __instance.UpdateWithValue(h.Value, updateTick, deltaTime); transferPhase?.Forwarded(); }
            catch { transferPhase?.Fault(); h.Owner.Refuse("native owned input forwarding failed"); h.Owner.Dispose(); throw; }
            if (!h.FirstInjectionNotified && h.FirstInjection != null)
            {
                h.FirstInjectionNotified = true;
                try { h.FirstInjection(); }
                catch { h.Owner.Refuse("owned first-injection notification failed"); h.Owner.Dispose(); }
            }
        }
    }

    private static void ShiftPostfix(ref bool __result) { __result |= Shift; }
    private static void ControlPostfix(ref bool __result) { __result |= Control; }
    private static void AltPostfix(ref bool __result) { __result |= Alt; }

    private static void GetScreenPositionPostfix(ref Vector2 __result)
    {
        if (CheckCursorActive()) __result = CursorPosition;
    }

    private static void HandleMovementPostfix(SoftCursor __instance)
    {
        if (!CheckCursorActive()) return;
        OwnedInputScope expectedOwner = cursorOwner; CursorWrite expectedWrite = cursorWrite;
        Vector2 point = CursorPosition;
        try
        {
            UICamera cam = __instance.uiCamera;
            if (cam != null)
            {
                Vector3 position = cam.cachedCamera.ScreenToWorldPoint(expectedOwner == null ? CursorPosition : point);
                if ((expectedOwner != null || cursorOwner != null) && (!ReferenceEquals(cursorOwner, expectedOwner)
                    || !ReferenceEquals(cursorWrite, expectedWrite) || !CursorActive || !expectedOwner.OwnershipCurrent)) return;
                __instance.Position = position;
            }
        }
        catch { }
    }

    private static bool CheckCursorActive()
    {
        if (!CursorActive) return false;
        OwnedInputScope expectedOwner = cursorOwner;
        CursorWrite expectedWrite = cursorWrite;
        if (expectedOwner != null)
        {
            bool admitted = expectedOwner.Admitted;
            if (!ReferenceEquals(cursorOwner, expectedOwner) || !ReferenceEquals(cursorWrite, expectedWrite)) return false;
            if (!admitted || !expectedOwner.OwnershipCurrent) { expectedOwner.Dispose(); return false; }
        }
        bool locked = LockCursor;
        if (!ReferenceEquals(cursorOwner, expectedOwner) || !ReferenceEquals(cursorWrite, expectedWrite)) return false;
        if (expectedOwner != null && !expectedOwner.OwnershipCurrent) { expectedOwner.Dispose(); return false; }
        if (!locked && Application.isFocused && (Input.mousePosition - realMouseAtActivation).sqrMagnitude > 25f * 25f)
        {
            CursorActive = false;
            if (cursorOwner != null) cursorOwner.Refuse("user took cursor control");
            cursorOwner = null; cursorWrite = new CursorWrite(null, null);
        }
        return CursorActive;
    }

    // ------------------------------------------------------------------ API

    public static void SetCursor(Vector2 unityScreenPos)
    {
        if (cursorOwner != null) cursorOwner.Refuse("cursor replaced by another writer");
        cursorOwner = null; cursorWrite = new CursorWrite(null, null);
        CursorPosition = unityScreenPos;
        realMouseAtActivation = Input.mousePosition;
        CursorActive = true;
    }

    public static void ReleaseCursor() { if (cursorOwner != null) cursorOwner.Refuse("cursor released by another writer"); cursorOwner = null; cursorWrite = new CursorWrite(null, null); CursorActive = false; }

    /// <summary>Hold an action at `value` for `frames` rendered frames (a "press" is 2+ frames).</summary>
    public static void HoldFrames(PlayerAction action, int frames, float value = 1f)
    {
        Held previous; if (held.TryGetValue(action, out previous) && previous.Owner != null) previous.Owner.Refuse("action replaced by another writer");
        held[action] = new Held { Value = value, UntilFrame = Time.frameCount + Math.Max(1, frames) };
    }

    /// <summary>Hold an action for `seconds` of real time (seconds &lt;= 0 means until released).</summary>
    public static void HoldSeconds(PlayerAction action, float seconds, float value = 1f)
    {
        Held previous; if (held.TryGetValue(action, out previous) && previous.Owner != null) previous.Owner.Refuse("action replaced by another writer");
        held[action] = new Held { Value = value, UntilTime = seconds > 0f ? Time.realtimeSinceStartup + seconds : float.MaxValue };
    }

    public static void Release(PlayerAction action)
    {
        Held previous;
        if (action != null && held.TryGetValue(action, out previous))
        { if (previous.Owner != null) previous.Owner.Refuse("action released by another writer"); held.Remove(action); }
    }

    public static void ReleaseAll()
    {
        foreach (Held entry in held.Values) if (entry.Owner != null) entry.Owner.Refuse("all actions released by another writer");
        held.Clear();
        Shift = Control = Alt = false;
        ReleaseCursor();
    }

    public static bool IsHeld(PlayerAction action) { return action != null && held.ContainsKey(action); }

    public static List<string> HeldNames()
    {
        var names = new List<string>();
        foreach (PlayerAction a in held.Keys) names.Add(a.Name);
        return names;
    }

    /// <summary>Opt-in real-input custody for a captured native player/world. Default APIs remain available.</summary>
    internal sealed class OwnedWearReceipt
    {
        internal readonly EntityPlayerLocal Player; internal readonly World World;
        internal readonly int Slot, Count; internal readonly ItemStack Stack; internal readonly ItemValue Value;
        internal readonly string BeforeKey, AfterKey; internal readonly float BeforeUseTimes, AfterUseTimes;
        internal OwnedWearReceipt(EntityPlayerLocal player, World world, int slot, ItemStack stack, ItemValue value, int count,
            string beforeKey, string afterKey, float beforeUseTimes, float afterUseTimes)
        { Player = player; World = world; Slot = slot; Stack = stack; Value = value; Count = count;
          BeforeKey = beforeKey; AfterKey = afterKey; BeforeUseTimes = beforeUseTimes; AfterUseTimes = afterUseTimes; }
    }
    public sealed class OwnedInputScope : IDisposable
    {
        private readonly EntityPlayerLocal player;
        private readonly World world;
        private readonly Func<bool> admission;
        private readonly OwnedInputScope parent;
        private readonly Dictionary<PlayerAction, Held> actions = new Dictionary<PlayerAction, Held>();
        private readonly List<OwnedWearReceipt> wearReceipts = new List<OwnedWearReceipt>();
        private FlagWrite ownShift, ownLock;
        private OwnedInputScope previousCursorOwner;
        private CursorWrite ownCursor, expectedCursor;
        private Vector3 cursorBaseline;
        private bool acquiredCursor, disposed, invalid;
        private enum RefusalKind { None, AwakeThreat, ParentEnded, Independent }
        private RefusalKind refusalKind;
        private RebirthGameBridgePoiTransferPhase transferPhase;
        internal RebirthGameBridgePoiTransferPhase TransferPhase { get { return transferPhase ?? parent?.TransferPhase; } }
        internal bool AttachTransferPhase(RebirthGameBridgePoiTransferPhase phase)
        { if (phase == null || TransferPhase != null || !Admitted) return false; transferPhase=phase; return true; }
        internal void DetachTransferPhase(RebirthGameBridgePoiTransferPhase phase)
        { if (ReferenceEquals(transferPhase,phase)) transferPhase=null; }
        internal void RefuseAwakeThreat(RebirthGameBridgePoiTransferPhase phase)
        {
            if (!ReferenceEquals(TransferPhase,phase)) { phase.Fault(); return; }
            invalid=true; if(refusalKind==RefusalKind.None) refusalKind=RefusalKind.AwakeThreat;
            if(Failure==null) Failure="input admission refused";
        }
        internal void InheritFailure(OwnedInputScope child)
        {
            var phase=TransferPhase;
            if (child != null && phase != null && ReferenceEquals(phase,child.TransferPhase)
                && phase.GenuineThreat && !phase.Disqualified
                && (child.refusalKind==RefusalKind.AwakeThreat || child.refusalKind==RefusalKind.ParentEnded))
            { invalid=true; if(refusalKind==RefusalKind.None)refusalKind=RefusalKind.ParentEnded; if(Failure==null)Failure=child.Failure; }
            else Refuse(child?.Failure ?? "unknown owned child refusal");
        }
        public EntityPlayerLocal Player { get { return player; } }
        public string Failure { get; private set; }
        public bool CursorCustodyPending { get; private set; }
        public OwnedInputScope(EntityPlayerLocal player, Func<bool> admission, OwnedInputScope parent = null)
        { this.player = player; world = player?.world; this.admission = admission; this.parent = parent; }
        internal bool PublishWear(OwnedWearReceipt receipt)
        {
            if (!Admitted || receipt == null || !ReferenceEquals(receipt.Player, player) || !ReferenceEquals(receipt.World, world)
                || player.inventory.holdingItemIdx != receipt.Slot || !ReferenceEquals(player.inventory.GetItem(receipt.Slot), receipt.Stack)
                || !ReferenceEquals(receipt.Stack.itemValue, receipt.Value) || receipt.Stack.count != receipt.Count
                || RebirthGameBridgePoiStorage.OwnedItemKey(receipt.Stack) != receipt.AfterKey)
            { Refuse("owned wear receipt context or item changed"); return false; }
            if (parent != null && !parent.PublishWear(receipt)) { Refuse("owned wear ancestor refused receipt"); return false; }
            for (int i = wearReceipts.Count - 1; i >= 0; i--)
            {
                var prior = wearReceipts[i];
                if (!ReferenceEquals(prior.Stack, receipt.Stack)) continue;
                if (ReferenceEquals(prior.Value, receipt.Value) && prior.Slot == receipt.Slot && prior.Count == receipt.Count && prior.AfterKey == receipt.BeforeKey)
                { wearReceipts[i] = new OwnedWearReceipt(player, world, receipt.Slot, receipt.Stack, receipt.Value, receipt.Count,
                    prior.BeforeKey, receipt.AfterKey, prior.BeforeUseTimes, receipt.AfterUseTimes); return true; }
                break;
            }
            wearReceipts.Add(receipt); return true;
        }
        internal bool TryConsumeWear(ItemStack original, int originalSlot, string beforeKey, out string afterKey)
        {
            afterKey = null;
            for (int i = 0; i < wearReceipts.Count; i++)
            {
                var receipt = wearReceipts[i];
                if (!ReferenceEquals(receipt.Stack, original) || receipt.Slot != originalSlot || receipt.BeforeKey != beforeKey) continue;
                if (!Admitted || !ReferenceEquals(receipt.Player, player) || !ReferenceEquals(receipt.World, world)
                    || originalSlot != player.inventory.holdingItemIdx || !ReferenceEquals(player.inventory.GetItem(originalSlot), original)
                    || !ReferenceEquals(original.itemValue, receipt.Value) || original.count != receipt.Count
                    || RebirthGameBridgePoiStorage.OwnedItemKey(original) != receipt.AfterKey)
                { Refuse("owned wear receipt became stale before restoration"); return false; }
                wearReceipts.RemoveAt(i); afterKey = receipt.AfterKey; return true;
            }
            return false;
        }
        public bool ContextCurrent
        {
            get
            {
                if (player == null || world == null) return false;
                bool dead = player.IsDead(); EntityPlayerLocal primary = world.GetPrimaryPlayer();
                return !dead && ReferenceEquals(primary, player) && ReferenceEquals(player.world, world)
                    && GameManager.Instance != null && ReferenceEquals(GameManager.Instance.World, world);
            }
        }
        // Pure fence: no admission delegate or input mutation. Safe after callback-bearing checks.
        internal bool OwnershipCurrent
        {
            get
            {
                if (disposed || invalid || !ContextCurrent || (parent != null && !parent.OwnershipCurrent)) return false;
                if ((ownShift != null && !Includes(shiftWrite, ownShift)) || (ownLock != null && !Includes(lockWrite, ownLock))) return false;
                if (expectedCursor != null && !IncludesCursor(cursorWrite, expectedCursor)) return false;
                if (acquiredCursor && (ReferenceEquals(cursorOwner, this) || cursorOwner == null) && Application.isFocused
                    && (Input.mousePosition - cursorBaseline).sqrMagnitude > 25f * 25f) return false;
                return true;
            }
        }
        private void RefuseCurrentOwnership(string reason)
        {
            Invalidate(reason);
            if (acquiredCursor && ReferenceEquals(cursorOwner, this) && ReferenceEquals(cursorWrite, ownCursor)
                && Application.isFocused && (Input.mousePosition - cursorBaseline).sqrMagnitude > 25f * 25f) ReleaseCursor();
        }
        public bool Admitted
        {
            get
            {
                if (!OwnershipCurrent) { RefuseCurrentOwnership("native input context or ownership changed"); return false; }
                if (parent != null && !parent.Admitted) { RefuseCurrentOwnership("parent input operation ended"); return false; }
                if (!OwnershipCurrent) { RefuseCurrentOwnership("native input context or ownership changed during parent admission"); return false; }
                try { if (admission == null || !admission()) { if (!invalid) Invalidate("input admission refused"); return false; } }
                catch { Invalidate("input admission failed"); return false; }
                if (!OwnershipCurrent) { RefuseCurrentOwnership("native input context or ownership changed during admission"); return false; }
                return true;
            }
        }
        private static bool Includes(FlagWrite current, FlagWrite expected)
        { for (var write = current; write != null; write = write.Previous) if (ReferenceEquals(write, expected)) return true; return false; }
        private static bool IncludesCursor(CursorWrite current, CursorWrite expected)
        { for (var write = current; write != null; write = write.Previous) if (ReferenceEquals(write, expected)) return true; return false; }
        public void Refuse(string reason) { Invalidate(reason); }
        private void Invalidate(string reason) { TransferPhase?.Fault(); refusalKind=RefusalKind.Independent; invalid = true; if (Failure == null) Failure = reason; }
        public void MarkCursorCustodyPending() { TransferPhase?.CursorPending(); CursorCustodyPending = true; Invalidate("native cursor item custody remains unresolved"); }
        internal void Expired(PlayerAction action) { actions.Remove(action); }
        public bool TryHoldFrames(PlayerAction action, int frames, float value = 1f, Func<bool> proof = null, Action firstInjection = null)
        { return TryHold(action, new Held { Value = value, UntilFrame = Time.frameCount + Math.Max(1, frames), Owner = this, Proof = proof, FirstInjection = firstInjection }); }
        public bool TryHoldSeconds(PlayerAction action, float seconds, float value = 1f)
        { return TryHold(action, new Held { Value = value, UntilTime = seconds > 0f ? Time.realtimeSinceStartup + seconds : float.MaxValue, Owner = this }); }
        private bool TryHold(PlayerAction action, Held entry)
        {
            if (!Admitted || action == null) return false;
            if (entry.Proof != null)
            {
                bool allowed; try { allowed = entry.Proof(); } catch { allowed = false; }
                if (!allowed || !Admitted) { Invalidate("owned action item proof refused"); return false; }
            }
            Held current;
            if (held.TryGetValue(action, out current) && !ReferenceEquals(current.Owner, this))
            { Invalidate("action already held by another operation"); return false; }
            held[action] = entry; actions[action] = entry; return true;
        }
        public void Release(PlayerAction action)
        {
            Held own, current;
            if (action != null && actions.TryGetValue(action, out own))
            { if (held.TryGetValue(action, out current) && ReferenceEquals(current, own)) held.Remove(action); actions.Remove(action); }
        }
        private bool TryFlag(bool value, bool shift)
        {
            if (!Admitted) return false;
            FlagWrite current = shift ? shiftWrite : lockWrite;
            if (current.Owner != null && !ReferenceEquals(current.Owner, this) && !ReferenceEquals(current.Owner, parent))
            { Invalidate("modifier owned by another operation"); return false; }
            if (current.Owner == null && current.Value && !value)
            { Invalidate("active modifier belongs to another writer"); return false; }
            FlagWrite own = shift ? ownShift : ownLock;
            FlagWrite write = new FlagWrite(value, this, own != null ? own.Previous : current);
            if (shift) { ownShift = write; shiftWrite = write; } else { ownLock = write; lockWrite = write; }
            return true;
        }
        public bool TrySetShift(bool value) { return TryFlag(value, true); }
        public bool TrySetLockCursor(bool value) { return TryFlag(value, false); }
        public bool TrySetCursor(Vector2 point)
        {
            if (!Admitted) return false;
            if (cursorOwner != null && !ReferenceEquals(cursorOwner, this) && !ReferenceEquals(cursorOwner, parent))
            { Invalidate("cursor owned by another operation"); return false; }
            if (!acquiredCursor)
            {
                if (CursorActive && cursorOwner == null) { Invalidate("cursor belongs to another writer"); return false; }
                acquiredCursor = true; previousCursorOwner = cursorOwner;
                cursorBaseline = previousCursorOwner != null ? previousCursorOwner.cursorBaseline : Input.mousePosition;
            }
            ownCursor = new CursorWrite(this, ownCursor != null ? ownCursor.Previous : cursorWrite);
            expectedCursor = ownCursor; cursorWrite = ownCursor;
            cursorOwner = this; realMouseAtActivation = cursorBaseline; CursorPosition = point; CursorActive = true; return true;
        }
        public void ReleaseCursor()
        {
            if (ownCursor != null && ReferenceEquals(cursorWrite, ownCursor))
            {
                CursorWrite expected = ownCursor;
                CursorWrite prior = expected.Previous;
                while (prior != null && prior.Owner != null && !prior.Owner.Admitted) prior = prior.Previous;
                if (!ReferenceEquals(cursorWrite, expected) || !ReferenceEquals(cursorOwner, this)) return;
                if (prior != null && prior.Owner != null)
                { cursorWrite = prior; cursorOwner = prior.Owner; }
                else { cursorWrite = new CursorWrite(null, null); cursorOwner = null; CursorActive = false; }
                expectedCursor = cursorWrite; ownCursor = null;
            }
        }
        private static FlagWrite PriorLive(FlagWrite write)
        {
            FlagWrite prior = write.Previous;
            while (prior != null && prior.Owner != null && !prior.Owner.Admitted) prior = prior.Previous;
            return prior ?? new FlagWrite(false, null, null);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var pair in actions)
            { Held current; if (held.TryGetValue(pair.Key, out current) && ReferenceEquals(current, pair.Value)) held.Remove(pair.Key); }
            actions.Clear();
            FlagWrite expectedShift = ownShift;
            if (expectedShift != null && ReferenceEquals(shiftWrite, expectedShift))
            { FlagWrite prior = PriorLive(expectedShift); if (ReferenceEquals(shiftWrite, expectedShift)) shiftWrite = prior; }
            FlagWrite expectedLock = ownLock;
            if (expectedLock != null && ReferenceEquals(lockWrite, expectedLock))
            { FlagWrite prior = PriorLive(expectedLock); if (ReferenceEquals(lockWrite, expectedLock)) lockWrite = prior; }
            ReleaseCursor();
        }
    }
    // ------------------------------------------------------------------ action lookup

    public static PlayerActionsLocal LocalActions()
    {
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        if (ui != null && ui.playerInput != null) return ui.playerInput;
        // Main-menu XUi survives world unload without a LocalPlayerUI input reference.
        // Use the same native primary action sets that drive its physical keyboard/mouse.
        var platform = PlatformManager.NativePlatform;
        return platform != null && platform.Input != null ? platform.Input.PrimaryPlayer : null;
    }

    /// <summary>
    /// Resolve "set.Action" where set is local (default), gui, vehicle, permanent or rebirth.
    /// Examples: "Primary", "local.Jump", "gui.LeftClick", "InventorySlot3".
    /// </summary>
    public static PlayerAction Find(string qualifiedName)
    {
        PlayerActionsLocal local = LocalActions();
        if (local == null || string.IsNullOrEmpty(qualifiedName)) return null;
        string set = "local", name = qualifiedName;
        int dot = qualifiedName.IndexOf('.');
        if (dot > 0) { set = qualifiedName.Substring(0, dot); name = qualifiedName.Substring(dot + 1); }

        object owner = ResolveSet(local, set);
        if (owner == null) return null;
        FieldInfo f = GetActionField(owner.GetType(), name);
        if (f != null) return f.GetValue(owner) as PlayerAction;
        // REBIRTH actions are properties, registered with the native action set.
        var actions = owner as PlayerActionSet;
        if (actions != null)
            foreach (PlayerAction action in actions.Actions)
                if (string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase)) return action;
        return null;
    }

    private static object ResolveSet(PlayerActionsLocal local, string set)
    {
        switch (set.ToLowerInvariant())
        {
            case "local": return local;
            case "gui": return local.GUIActions;
            case "vehicle": return local.VehicleActions;
            case "permanent": return local.PermanentActions;
            case "rebirth": return RebirthNativeControls.Actions;
            default: return null;
        }
    }

    private static FieldInfo GetActionField(Type setType, string name)
    {
        string key = setType.FullName + "." + name;
        FieldInfo f;
        if (actionFields.TryGetValue(key, out f)) return f;
        foreach (FieldInfo candidate in setType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!typeof(PlayerAction).IsAssignableFrom(candidate.FieldType)) continue;
            if (candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { f = candidate; break; }
        }
        actionFields[key] = f;
        return f;
    }

    /// <summary>
    /// Every action (in native and REBIRTH sets) currently bound to a physical key/button, matched by the
    /// binding's display name ("Escape", "Tab", "E", "Left Shift", "Left Mouse Button", "1"...).
    /// Pressing all of them reproduces a real key press, including the user's custom keybindings.
    /// </summary>
    public static List<PlayerAction> FindByKey(string keyName)
    {
        var result = new List<PlayerAction>();
        PlayerActionsLocal local = LocalActions();
        if (local == null || string.IsNullOrEmpty(keyName)) return result;
        string wanted = Normalize(keyName);
        foreach (string set in new[] { "local", "gui", "vehicle", "permanent", "rebirth" })
        {
            var owner = ResolveSet(local, set) as PlayerActionSet;
            if (owner == null) continue;
            foreach (PlayerAction a in owner.Actions)
            {
                foreach (BindingSource b in a.Bindings)
                {
                    if (b == null || Normalize(b.Name) != wanted) continue;
                    result.Add(a);
                    break;
                }
            }
        }
        return result;
    }

    private static string Normalize(string s)
    {
        return s == null ? "" : s.Replace(" ", "").Replace("_", "").ToLowerInvariant();
    }

    public static Dictionary<string, List<string>> ListActions()
    {
        var result = new Dictionary<string, List<string>>();
        PlayerActionsLocal local = LocalActions();
        if (local == null) return result;
        foreach (string set in new[] { "local", "gui", "vehicle", "permanent", "rebirth" })
        {
            object owner = ResolveSet(local, set);
            if (owner == null) continue;
            var names = new List<string>();
            foreach (FieldInfo fi in owner.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!typeof(PlayerAction).IsAssignableFrom(fi.FieldType)) continue;
                var a = fi.GetValue(owner) as PlayerAction;
                var keys = new List<string>();
                if (a != null) foreach (BindingSource b in a.Bindings) if (b != null) keys.Add(b.Name);
                names.Add(keys.Count > 0 ? fi.Name + " [" + string.Join(", ", keys.ToArray()) + "]" : fi.Name);
            }
            if (set == "rebirth" && owner is PlayerActionSet custom)
                foreach (PlayerAction action in custom.Actions)
                {
                    var keys = new List<string>();
                    foreach (BindingSource binding in action.Bindings)
                        if (binding != null) keys.Add(binding.Name);
                    names.Add(keys.Count > 0 ? action.Name + " [" + string.Join(", ", keys.ToArray()) + "]" : action.Name);
                }
            result[set] = names;
        }
        return result;
    }
}







