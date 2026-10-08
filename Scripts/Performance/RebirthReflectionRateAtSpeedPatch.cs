using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Vanilla re-renders a realtime reflection probe (a cube-map capture) whenever the player has moved 0.3 m / rateScale from the last probe position.
/// On a fast vehicle that is nearly every frame: profiled at about 84 ms per second (8% of the main thread) while riding at ~28 m/s.
///
/// Above walking/sprinting speed this scales the game's own <c>rateScale</c> down in proportion to the speed, so the probe is refreshed about as
/// often per second as it is at sprint speed instead of once per frame. Reflections lag a little at vehicle speed; below the threshold nothing changes.
/// The quality preset itself is never modified (optionsSelected is a copy), and a quality change made elsewhere is picked up as the new base.
/// </summary>
[HarmonyPatch(typeof(ReflectionManager), "FrameUpdate")]
public static class RebirthReflectionRateAtSpeedPatch
{
    public const float FullRateUpToSpeed = 6f;   // m/s: sprinting on foot keeps the vanilla rate
    public const float MinimumFactor = 0.1f;

    private static float baseScale = -1f;
    private static float lastSet = -1f;
    private static Vector3 lastPos;
    private static float lastTime;
    private static float speed;

    [HarmonyPrefix]
    public static void Prefix(ReflectionManager __instance)
    {
        EntityPlayerLocal player = __instance.player;
        if (player == null || ReflectionManager.optionsSelected.resolution == 0)
            return;

        float now = Time.unscaledTime;
        float dt = now - lastTime;
        Vector3 pos = player.position;
        float moved = (pos - lastPos).magnitude;
        if (dt > 0.0001f && dt < 1f && moved < 60f)   // a jump (teleport, respawn) is not speed
            speed = Mathf.Lerp(speed, moved / dt, 0.15f);
        else
            speed = 0f;
        lastPos = pos;
        lastTime = now;

        float current = ReflectionManager.optionsSelected.rateScale;
        if (baseScale < 0f || !Mathf.Approximately(current, lastSet))
            baseScale = current;   // first call, or the quality setting changed: that is the new base

        float factor = speed > FullRateUpToSpeed ? Mathf.Clamp(FullRateUpToSpeed / speed, MinimumFactor, 1f) : 1f;
        float wanted = baseScale * factor;
        if (!Mathf.Approximately(wanted, current))
            ReflectionManager.optionsSelected.rateScale = wanted;
        lastSet = ReflectionManager.optionsSelected.rateScale;
    }
}
