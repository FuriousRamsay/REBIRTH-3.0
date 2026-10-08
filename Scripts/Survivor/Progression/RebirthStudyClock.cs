using UnityEngine;

// Study uses real seconds, but a genuinely paused world must not grant progress.
public static class RebirthStudyClock
{
    public static float ElapsedThisFrame()
    {
        var manager = GameManager.Instance;
        if (manager == null || manager.IsPaused()) return 0f;
        return Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.25f);
    }
}
