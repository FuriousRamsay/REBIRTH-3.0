using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

#nullable disable

/// <summary>Main-thread owner token for the HUD manager's native navigation lock.</summary>
internal sealed class RebirthHudNavigationLease
{
    private sealed class State
    {
        public long Revision;
        public bool Known;
        public XUiView View;
        public State() { }
    }
    private static readonly ConditionalWeakTable<CursorControllerAbs, State> States = new ConditionalWeakTable<CursorControllerAbs, State>();
    private static readonly HashSet<MethodBase> Patched = new HashSet<MethodBase>();
    private static readonly Harmony Patcher = new Harmony("rebirth.hud-tracking.navigation-ownership");
    private readonly CursorControllerAbs cursor;
    private readonly XUiView ownedView, previousView;
    private readonly long revision;
    private bool released;

    private RebirthHudNavigationLease(CursorControllerAbs value, XUiView view, XUiView previous, long token)
    { cursor = value; ownedView = view; previousView = previous; revision = token; }

    public static RebirthHudNavigationLease Acquire(CursorControllerAbs cursor, XUiView view, XUiView focus)
    {
        if (cursor == null || view == null) return null;
        // Methods are selected from the actual cursor type and base types, with the native
        // first-argument contract already used in this source. Abstract declarations are skipped.
        for (Type type = cursor.GetType(); type != null; type = type.BaseType)
            foreach (MethodInfo method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name != "SetNavigationLockView" || method.IsAbstract || parameters.Length == 0
                    || parameters[0].ParameterType != typeof(XUiView) || Patched.Contains(method)) continue;
                Patcher.Patch(method, postfix: new HarmonyMethod(typeof(RebirthHudNavigationLease), nameof(AfterSet)));
                Patched.Add(method); // A failed patch must not be recorded as installed.
            }
        if (Patched.Count == 0) return null;
        State state = States.GetOrCreateValue(cursor);
        XUiView previous = state.Known ? state.View : null;
        cursor.SetNavigationLockView(view, focus);
        // An unsupported native method must not confer release ownership.
        if (!state.Known || !ReferenceEquals(state.View, view)) return null;
        return new RebirthHudNavigationLease(cursor, view, previous, state.Revision);
    }
    private static void AfterSet(object __instance, object[] __args)
    {
        CursorControllerAbs cursor = __instance as CursorControllerAbs;
        if (cursor == null || __args == null || __args.Length == 0) return;
        State state = States.GetOrCreateValue(cursor);
        state.View = __args[0] as XUiView; state.Known = true;
        unchecked { state.Revision++; }
    }
    public bool TryRelease()
    {
        if (released) return false;
        released = true;
        State state = States.GetOrCreateValue(cursor);
        if (state.Revision != revision || !ReferenceEquals(state.View, ownedView)) return false;
        cursor.SetNavigationLockView(previousView);
        return true;
    }
}
