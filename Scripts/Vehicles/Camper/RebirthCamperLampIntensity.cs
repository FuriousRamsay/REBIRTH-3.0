// Native state mapping, independent of Unity rendering.
internal static class RebirthCamperLampIntensity
{
    internal static float Calculate(bool broken,bool braking,float forwardVelocity,bool headlightsOn)
    {
        if(broken||float.IsNaN(forwardVelocity)||float.IsInfinity(forwardVelocity))return 0f;
        return braking||forwardVelocity<-.05f?.65f:headlightsOn?.12f:0f;
    }
    internal static float Marker(bool engineRunning){return engineRunning?.08f:0f;}
}
