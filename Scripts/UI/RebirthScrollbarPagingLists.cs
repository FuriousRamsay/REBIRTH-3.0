using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

#nullable disable

internal static class RebirthScrollbarPagingListAdapter
{
    internal interface IAdapter
    {
        bool Change(object list, int offset);
        void Absolute(object list, ref int value);
        bool Force(object list, int index);
        void Update(object list);
    }
    private sealed class Snapshot
    {
        internal Dictionary<Type, IAdapter> ByOwner, ByBase;
        internal MethodBase[] Changes, Absolutes, Forces, Updates;
    }
    private static Snapshot snapshot;
    private static readonly string[] NativeOwnerNames = {
        "XUiC_DiscordBlockedUsersList", "XUiC_DiscordFriendsList", "XUiC_DiscordLobbyMemberList", "XUiC_DiscordPendingList",
        "XUiC_ServersList", "XUiC_BugReportSavesList", "XUiC_DlcList", "XUiC_ProfilesList", "XUiC_SavegamesList",
        "XUiC_WorldList", "XUiC_WorldSelectionList", "XUiC_StringList", "XUiC_CamPositionsList", "XUiC_GameEventsList",
        "XUiC_PoiList", "XUiC_PrefabFeatureEditorList", "XUiC_PrefabFileList", "XUiC_PrefabFolderList", "XUiC_PrefabGroupList",
        "XUiC_PrefabMarkerList", "XUiC_PrefabTriggerEditorList", "XUiC_SpawnEntitiesList", "XUiC_SpawnersList", "XUiC_SpawnNearFriendsList",
        "XUiC_DMPlayersList", "XUiC_DMSavegamesList", "XUiC_DMWorldList" };
    internal static IEnumerable<MethodBase> Targets(string method)
    {
        Initialize();
        var current = snapshot;
        return method == "ChangePage" ? current.Changes : method == "set_CurrentBaseIndex" ? current.Absolutes : method == "forceEntryVisible" ? current.Forces : current.Updates;
    }
    private static MethodBase RequireClosed(Type parent, string name, Type argument)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var method = parent.GetMethod(name, flags, null, new[] { argument }, null);
        if (method == null || method.IsStatic || method.ReturnType != typeof(void) || method.ContainsGenericParameters || method.DeclaringType != parent)
            throw new MissingMethodException(parent.FullName, name);
        var parameters = method.GetParameters();
        if (parameters.Length != 1 || parameters[0].ParameterType != argument || parameters[0].IsOut || parameters[0].ParameterType.IsByRef) throw new MissingMethodException(parent.FullName, name);
        return method;
    }
    private static void Initialize()
    {
        if (snapshot != null) return;
        var owners = new Dictionary<Type, IAdapter>();
        var bases = new Dictionary<Type, IAdapter>();
        var changes = new List<MethodBase>(); var absolutes = new List<MethodBase>();
        var forces = new List<MethodBase>(); var updates = new List<MethodBase>();
        var assembly = typeof(XUiController).Assembly;
        foreach (var name in NativeOwnerNames)
        {
            var type = assembly.GetType(name, false);
            if (type == null || type.ContainsGenericParameters || !typeof(XUiController).IsAssignableFrom(type)) throw new TypeLoadException("Required native list owner: " + name);
            Type parent = type.BaseType;
            while (parent != null && (!parent.IsGenericType || parent.GetGenericTypeDefinition() != typeof(XUiC_List<>))) parent = parent.BaseType;
            if (parent == null || parent.ContainsGenericParameters) throw new TypeLoadException("Required closed native list base: " + name);
            IAdapter adapter;
            if (!bases.TryGetValue(parent, out adapter))
            {
                var change = RequireClosed(parent, "ChangePage", typeof(int));
                var absolute = RequireClosed(parent, "set_CurrentBaseIndex", typeof(int));
                var force = RequireClosed(parent, "forceEntryVisible", typeof(int));
                var update = RequireClosed(parent, "Update", typeof(float));
                adapter = (IAdapter)Activator.CreateInstance(typeof(Adapter<>).MakeGenericType(parent.GetGenericArguments()));
                bases.Add(parent, adapter); changes.Add(change); absolutes.Add(absolute); forces.Add(force); updates.Add(update);
            }
            owners.Add(type, adapter);
        }
        if (owners.Count != 27 || bases.Count != 27) throw new TypeLoadException("Incomplete exact native list discovery: owners=" + owners.Count + " bases=" + bases.Count);
        // Publish only after every required owner, closed base and method validates; failure leaves no partial cache.
        snapshot = new Snapshot { ByOwner = owners, ByBase = bases, Changes = changes.ToArray(), Absolutes = absolutes.ToArray(), Forces = forces.ToArray(), Updates = updates.ToArray() };
    }
    internal static IAdapter Get(object list)
    {
        var current = snapshot;
        if (list == null || current == null) return null;
        var type = list.GetType();
        IAdapter adapter;
        if (current.ByOwner.TryGetValue(type, out adapter)) return adapter;
        // Resolve a derived owner through a reviewed closed base once; cache unsupported misses as well.
        for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
            if (current.ByBase.TryGetValue(parent, out adapter)) { current.ByOwner.Add(type, adapter); return adapter; }
        current.ByOwner.Add(type, null);
        return null;
    }
    private sealed class Adapter<T> : IAdapter where T : XUiListEntry<T>
    {
        private sealed class State { internal int ContainingDepth, Count, Rows, Low, High; internal bool Mode; }
        private static readonly ConditionalWeakTable<XUiC_List<T>, State> States = new ConditionalWeakTable<XUiC_List<T>, State>();
        private static State StateFor(XUiC_List<T> list) => States.GetValue(list, _ => new State());
        private static bool Enabled(XUiC_List<T> list) => RebirthScrollbarPagingPolicy.Enabled && list.PagingStepSize == XUiC_List<T>.EPagingStepSize.SingleEntry && list.PageLength > 0;
        private static int Low(XUiC_List<T> list) => Math.Min(0, list.PagingOverscroll.x);
        private static int High(XUiC_List<T> list) => Math.Max(0, list.EntryCount - list.PageLength + list.PagingOverscroll.y);
        private static int Snap(XUiC_List<T> list, int value)
        {
            int low = Low(list), high = High(list);
            if (value < 0) return value - low <= -value ? low : 0;
            return (int)RebirthScrollbarPagingPolicy.SnapAbsolute(value, high, list.PageLength, true);
        }
        private static int PredictNative(XUiC_List<T> list, int value)
        {
            if (value == list.CurrentBaseIndex) return value;
            if (value < 0) value = Math.Max(value, list.PagingOverscroll.x);
            if (value > list.EntryCount - list.PageLength) value = Math.Min(value, list.EntryCount - list.PageLength + list.PagingOverscroll.y);
            return value;
        }
        private static bool Contains(XUiC_List<T> list, int value, int index)
        {
            int local = index - PredictNative(list, value);
            return local >= 0 && local < list.PageLength && list.listEntryControllers != null && local < list.listEntryControllers.Length;
        }
        private static bool Containing(XUiC_List<T> list, int index, out int value)
        {
            value = list.CurrentBaseIndex;
            if (index < 0 || index >= list.EntryCount || list.listEntryControllers == null || list.PageLength <= 0) return false;
            int candidate = (int)RebirthScrollbarPagingPolicy.EnsureVisible(Math.Max(0, value), index, index + 1, High(list), list.PageLength, list.PageLength);
            if (Contains(list, candidate, index)) { value = candidate; return true; }
            if (Contains(list, High(list), index)) { value = High(list); return true; }
            if (Contains(list, Low(list), index)) { value = Low(list); return true; }
            return false; // Invalid native geometry retains its existing force/selection behavior.
        }
        public bool Change(object instance, int offset)
        {
            var list = (XUiC_List<T>)instance;
            if (!Enabled(list)) return true;
            int current = list.CurrentBaseIndex, low = Low(list), next;
            if (offset == 0) return false;
            if (offset > 0) next = current < 0 ? 0 : (int)RebirthScrollbarPagingPolicy.Step(current, High(list), list.PageLength, 1);
            else next = current <= 0 ? low : (int)RebirthScrollbarPagingPolicy.Step(current, High(list), list.PageLength, -1);
            var state = StateFor(list);
            state.ContainingDepth++;
            try { list.CurrentBaseIndex = next; }
            finally { state.ContainingDepth--; }
            return false;
        }
        public void Absolute(object instance, ref int value)
        {
            var list = (XUiC_List<T>)instance;
            if (Enabled(list) && StateFor(list).ContainingDepth == 0) value = Snap(list, value);
        }
        public bool Force(object instance, int index)
        {
            var list = (XUiC_List<T>)instance;
            if (!Enabled(list)) return true;
            if (index < 0 || index >= list.EntryCount) return true;
            if (Contains(list, list.CurrentBaseIndex, index)) return false;
            int value;
            if (!Containing(list, index, out value)) return true;
            var state = StateFor(list);
            state.ContainingDepth++;
            try { list.CurrentBaseIndex = value; }
            finally { state.ContainingDepth--; }
            return false;
        }
        public void Update(object instance)
        {
            var list = (XUiC_List<T>)instance;
            var state = StateFor(list);
            if (!Enabled(list)) { state.Mode = false; return; }
            int count = list.EntryCount, rows = list.PageLength, low = Low(list), high = High(list);
            if (state.Mode && state.Count == count && state.Rows == rows && state.Low == low && state.High == high) return;
            state.Mode = true; state.Count = count; state.Rows = rows; state.Low = low; state.High = high;
            int value = Snap(list, list.CurrentBaseIndex), selected = list.SelectedEntryIndex;
            if (selected >= 0 && selected < count) Containing(list, selected, out value);
            value = PredictNative(list, value);
            if (list.minIndex != value) { list.minIndex = value; list.IsDirty = true; }
        }
    }
}
[HarmonyPatch]
internal static class RebirthScrollbarNativeListChangePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => RebirthScrollbarPagingListAdapter.Targets("ChangePage");
    private static bool Prefix(object __instance, int _offset) => RebirthScrollbarPagingListAdapter.Get(__instance)?.Change(__instance, _offset) ?? true;
}
[HarmonyPatch]
internal static class RebirthScrollbarNativeListAbsolutePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => RebirthScrollbarPagingListAdapter.Targets("set_CurrentBaseIndex");
    private static void Prefix(object __instance, ref int value) { RebirthScrollbarPagingListAdapter.Get(__instance)?.Absolute(__instance, ref value); }
}
[HarmonyPatch]
internal static class RebirthScrollbarNativeListForcePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => RebirthScrollbarPagingListAdapter.Targets("forceEntryVisible");
    private static bool Prefix(object __instance, int _index) => RebirthScrollbarPagingListAdapter.Get(__instance)?.Force(__instance, _index) ?? true;
}
[HarmonyPatch]
internal static class RebirthScrollbarNativeListUpdatePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => RebirthScrollbarPagingListAdapter.Targets("Update");
    private static void Prefix(object __instance) { RebirthScrollbarPagingListAdapter.Get(__instance)?.Update(__instance); }
}
