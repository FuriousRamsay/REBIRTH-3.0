using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

#nullable disable

/// <summary>Cached reflection adapter for the supported native UIScrollView/scrollbar bridge.</summary>
public static class RebirthNativeScrollbarUtil
{
    private sealed class MemberAccessor
    {
        public PropertyInfo Property;
        public FieldInfo Field;
        public object Get(object instance) { return Property != null ? Property.GetValue(instance, null) : (Field != null ? Field.GetValue(instance) : null); }
        public bool Set(object instance, object value)
        {
            if (Property != null && Property.CanWrite) { Property.SetValue(instance, value, null); return true; }
            if (Field != null) { Field.SetValue(instance, value); return true; }
            return false;
        }
    }
    private sealed class ScrollMetadata
    {
        public MemberAccessor VerticalBar;
        public MethodInfo InvalidateBounds;
        public MethodInfo UpdateScrollbars;
        public MethodInfo ResetPosition;
    }
    private sealed class BarMetadata
    {
        public MemberAccessor Value;
        public MemberAccessor BarSize;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<Type, ScrollMetadata> ScrollCache = new Dictionary<Type, ScrollMetadata>();
    private static readonly Dictionary<Type, BarMetadata> BarCache = new Dictionary<Type, BarMetadata>();
    private static readonly object[] UpdateScrollbarsArgs = { true };

    private static Component FindScrollView(XUiController controller)
    {
        if (controller == null || controller.ViewComponent == null || controller.ViewComponent.UiTransform == null) return null;
        Transform t = controller.ViewComponent.UiTransform;
        Component c = t.GetComponent("UIScrollView");
        return c ?? (t.parent != null ? t.parent.GetComponent("UIScrollView") : null);
    }

    private static MemberAccessor ResolveMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo p = type.GetProperty(name, flags);
        if (p != null) return new MemberAccessor { Property = p };
        FieldInfo f = type.GetField(name, flags);
        return f != null ? new MemberAccessor { Field = f } : null;
    }

    private static ScrollMetadata GetScrollMetadata(Type type)
    {
        lock (Sync)
        {
            ScrollMetadata m;
            if (ScrollCache.TryGetValue(type, out m)) return m;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            m = new ScrollMetadata
            {
                VerticalBar = ResolveMember(type, "verticalScrollBar"),
                InvalidateBounds = type.GetMethod("InvalidateBounds", flags, null, Type.EmptyTypes, null),
                UpdateScrollbars = type.GetMethod("UpdateScrollbars", flags, null, new[] { typeof(bool) }, null),
                ResetPosition = type.GetMethod("ResetPosition", flags, null, Type.EmptyTypes, null)
            };
            ScrollCache[type] = m;
            return m;
        }
    }

    private static BarMetadata GetBarMetadata(Type type)
    {
        lock (Sync)
        {
            BarMetadata m;
            if (BarCache.TryGetValue(type, out m)) return m;
            m = new BarMetadata { Value = ResolveMember(type, "value"), BarSize = ResolveMember(type, "barSize") };
            BarCache[type] = m;
            return m;
        }
    }

    private static bool TryGetBar(XUiController controller, out object bar, out BarMetadata metadata)
    {
        bar = null; metadata = null;
        Component scroll = FindScrollView(controller);
        if (scroll == null) return false;
        ScrollMetadata sm = GetScrollMetadata(scroll.GetType());
        if (sm.VerticalBar == null) return false;
        bar = sm.VerticalBar.Get(scroll);
        if (bar == null) return false;
        metadata = GetBarMetadata(bar.GetType());
        return true;
    }

    public static bool TryGetValue(XUiController controller, out float value)
    {
        value = 0f; object bar; BarMetadata m;
        if (!TryGetBar(controller, out bar, out m) || m.Value == null) return false;
        object raw = m.Value.Get(bar); if (raw == null) return false;
        try { value = Mathf.Clamp01(Convert.ToSingle(raw, CultureInfo.InvariantCulture)); return true; } catch { return false; }
    }

    public static bool TryGetBarSize(XUiController controller, out float value)
    {
        value = 0f; object bar; BarMetadata m;
        if (!TryGetBar(controller, out bar, out m) || m.BarSize == null) return false;
        object raw = m.BarSize.Get(bar); if (raw == null) return false;
        try { value = Mathf.Clamp01(Convert.ToSingle(raw, CultureInfo.InvariantCulture)); return true; } catch { return false; }
    }

    public static bool TrySetValue(XUiController controller, float value)
    {
        object bar; BarMetadata m;
        if (!TryGetBar(controller, out bar, out m) || m.Value == null) return false;
        try { return m.Value.Set(bar, Mathf.Clamp01(value)); } catch { return false; }
    }

    public static void Refresh(XUiController controller)
    {
        Component scroll = FindScrollView(controller); if (scroll == null) return;
        ScrollMetadata m = GetScrollMetadata(scroll.GetType());
        try { if (m.InvalidateBounds != null) m.InvalidateBounds.Invoke(scroll, null); } catch { }
        try
        {
            if (m.UpdateScrollbars != null) { m.UpdateScrollbars.Invoke(scroll, UpdateScrollbarsArgs); return; }
            if (m.ResetPosition != null) m.ResetPosition.Invoke(scroll, null);
        }
        catch { }
    }
}
