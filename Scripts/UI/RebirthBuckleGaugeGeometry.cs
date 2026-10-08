using System;
using UnityEngine;

/// <summary>
/// The original quarter-ellipse float geometry, evaluated once. Orientation is an
/// integer permutation of the canonical pixels, not a different approximation.
/// Texture creation/upload and authoritative vital values remain with the controller.
/// </summary>
internal static class RebirthBuckleGaugeGeometry
{
    internal const int Width = 128, Height = 96;
    private struct Geometry
    {
        internal float Position;
        internal byte Alpha;
        internal bool Draw;
    }
    private static readonly Geometry[] geometry = Build();

    private static Geometry[] Build()
    {
        Geometry[] result = new Geometry[Width * Height];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        {
            float u = x + 0.5f, v = y + 0.5f;
            float dx = (u - 128) / 124f, dy = v / 92f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float t = Mathf.Atan2(dy, -dx) / (Mathf.PI / 2);
            float segment = t * 10f;
            float within = segment - Mathf.Floor(segment);
            if (radius < 0.74f || radius > 1f || within <= 0.055f || within >= 0.945f) continue;
            float edge = Mathf.Min((radius - 0.74f) * 92, (1 - radius) * 92);
            result[y * Width + x] = new Geometry { Position = t, Alpha = (byte)(255 * Mathf.Clamp01(edge)), Draw = true };
        }
        return result;
    }

    internal static void Render(Color32[] pixels, float current, float maximum, Color32 color, bool right, bool bottom)
    {
        if (pixels == null || pixels.Length != Width * Height) throw new ArgumentException("Gauge buffer dimensions must be 128 x 96.", "pixels");
        maximum = Mathf.Clamp01(maximum);
        current = Mathf.Clamp(current, 0, maximum);
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        {
            Geometry sample = geometry[y * Width + x];
            Color32 value = new Color32(0, 0, 0, 0);
            if (sample.Draw)
            {
                value = sample.Position >= maximum ? new Color32(0, 0, 0, 255) :
                    sample.Position >= current ? new Color32(106, 108, 112, 255) : color;
                value.a = sample.Alpha;
            }
            int targetX = right ? Width - 1 - x : x;
            int targetY = bottom ? Height - 1 - y : y;
            pixels[targetY * Width + targetX] = value;
        }
    }
}
