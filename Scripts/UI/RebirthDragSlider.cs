using System;
using UnityEngine;

#nullable disable

/// <summary>
/// The REBIRTH drag slider, as used by the camper Energy "POI lights selected" bar: a track sprite with on_press and
/// on_drag, and a filled sprite on top showing the amount. Pressing or dragging along the track sets the value from
/// the pointer position. Markup: &lt;sprite name="X" sprite="menu_empty2px" type="sliced" fillcenter="true"
/// color="85,75,105,255" on_press="true" on_drag="true"/&gt; plus &lt;filledsprite name="XFill" type="filled"
/// color="150,101,215,255"/&gt; inset 3 px.
/// </summary>
public sealed class RebirthDragSlider
{
    private readonly XUiController track;
    private readonly XUiV_Sprite fill;
    private readonly Action<int> changed;
    private bool held;

    public int Minimum { get; private set; }
    public int Maximum { get; private set; }
    public int Value { get; private set; }
    public bool Enabled { get; set; } = true;

    public RebirthDragSlider(XUiController track, XUiV_Sprite fill, Action<int> changed)
    {
        this.track = track;
        this.fill = fill;
        this.changed = changed;
        if (track == null) return;
        track.OnMouseUpDown += (s, pressed) =>
        {
            if (pressed)
            {
                if (!Enabled || Maximum <= Minimum) return;
                held = true;
                FromPointer();
            }
            else if (held) { FromPointer(); held = false; }
        };
        track.OnDrag += (s, type, delta) =>
        {
            if (!held) return;
            FromPointer();
            if (type == EDragType.DragEnd) held = false;
        };
    }

    /// <summary>Sets the range and value without raising the change callback.</summary>
    public void Set(int minimum, int maximum, int value)
    {
        Minimum = Math.Max(0, minimum);
        Maximum = Math.Max(Minimum, maximum);
        Value = Mathf.Clamp(value, Minimum, Maximum);
        Paint();
    }

    private void Paint()
    {
        if (fill == null) return;
        fill.Fill = Maximum > Minimum ? Mathf.Clamp01((Value - Minimum) / (float)(Maximum - Minimum)) : Maximum > 0 ? 1f : 0f;
    }

    private void FromPointer()
    {
        var view = track?.ViewComponent;
        var transform = view?.UiTransform;
        var camera = UICamera.currentCamera;
        if (transform == null || camera == null || view.Size.x <= 0) return;
        Vector2 mouse = UICamera.currentTouch != null ? UICamera.currentTouch.pos : (Vector2)Input.mousePosition;
        var ray = camera.ScreenPointToRay(mouse);
        if (!new Plane(transform.forward, transform.position).Raycast(ray, out float distance)) return;
        float fraction = Mathf.Clamp01(transform.InverseTransformPoint(ray.GetPoint(distance)).x / view.Size.x);
        int value = Mathf.Clamp(Minimum + Mathf.RoundToInt(fraction * (Maximum - Minimum)), Minimum, Maximum);
        if (value == Value) return;
        Value = value;
        Paint();
        changed?.Invoke(value);
    }
}
