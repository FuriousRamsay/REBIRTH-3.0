using System;

/// <summary>
/// Measures the horizontal body movement used by the SDCS blend tree. This owns
/// presentation state only; it never writes an entity's movement or rotation.
/// Inspired by NPCCore's windowed locomotion feed; independently implemented.
/// </summary>
internal sealed class RebirthSdcsLocomotionSampler
{
    private bool initialized;
    private bool sampled;
    private float lastX, lastZ, lastTime, worldX, worldZ;

    internal void Reset() { initialized = sampled = false; worldX = worldZ = 0f; }

    internal bool Sample(float x, float z, float yawDegrees, float now, out float forward, out float strafe)
    {
        forward = strafe = 0f;
        if (!Finite(x) || !Finite(z) || !Finite(yawDegrees) || !Finite(now))
        { Reset(); return false; }
        if (!initialized)
        { initialized = true; lastX = x; lastZ = z; lastTime = now; return false; }

        float dt = now - lastTime;
        float dx = x - lastX, dz = z - lastZ;
        // Re-anchor immediately after a teleport, time reset, or paused/stalled
        // rendering. Never preserve stale velocity across one of these arrivals.
        if (dt < 0f || dt > 0.5f || dx * dx + dz * dz > 25f)
        { Reset(); initialized = true; lastX = x; lastZ = z; lastTime = now; return false; }
        if (dt >= 0.1f)
        {
            float vx = dx / dt, vz = dz / dt;
            if (vx * vx + vz * vz < 0.0004f) vx = vz = 0f;
            float blend = 1f - (float)Math.Exp(-dt / 0.15f);
            worldX += (vx - worldX) * blend;
            worldZ += (vz - worldZ) * blend;
            if (worldX * worldX + worldZ * worldZ < 0.0004f) worldX = worldZ = 0f;
            lastX = x; lastZ = z; lastTime = now; sampled = true;
        }
        if (!sampled) return false;
        // Store world velocity so turning between samples reprojects the same
        // movement into the new facing, rather than rotating stale local feeds.
        double yaw = yawDegrees * (Math.PI / 180.0);
        float sin = (float)Math.Sin(yaw), cos = (float)Math.Cos(yaw);
        forward = worldX * sin + worldZ * cos;
        strafe = worldX * cos - worldZ * sin;
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
