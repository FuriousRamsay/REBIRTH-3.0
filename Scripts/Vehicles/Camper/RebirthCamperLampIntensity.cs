// Native state mapping, independent of Unity rendering.
internal static class RebirthCamperLampIntensity
{
    internal const float ReverseBeamMultiplier=5f,ReverseBeamRange=6f,ReverseBeamAngle=60f;
    internal static bool Reversing(bool broken,float velocity)=>PickupReverse(broken,velocity)>0;
    internal static float Calculate(bool broken,bool braking,float forwardVelocity,bool headlightsOn)
    {
        if(broken||float.IsNaN(forwardVelocity)||float.IsInfinity(forwardVelocity))return 0f;
        return braking||forwardVelocity<-.05f?.65f:headlightsOn?.12f:0f;
    }
    internal static float Marker(bool engineRunning){return engineRunning?.08f:0f;}
    internal static float PickupRed(bool broken,bool braking,float velocity,bool headlightsOn)
        => float.IsNaN(velocity)||float.IsInfinity(velocity)?0f:Calculate(broken,braking,0,headlightsOn);
    internal static float PickupReverse(bool broken,float velocity)
        => !broken&&!float.IsNaN(velocity)&&!float.IsInfinity(velocity)&&velocity<-.05f?.65f:0f;
    // Names are from the approved mesh export: two red and one clear segment
    // per side, each with four separate flute meshes using the same atlas.
    internal static bool PickupLens(string name,out bool reverse)
    {
        reverse=false;
        if(name=="Rear lamp lens segment")return true;
        const string segment="Rear lamp lens segment.";
        const string flute="Fine lens flute.";
        if(name!=null&&name.StartsWith(segment,System.StringComparison.Ordinal)
            &&int.TryParse(name.Substring(segment.Length),out var s)&&s>=1&&s<=5)
        {reverse=s==2||s==5;return true;}
        if(name=="Fine lens flute")return true;
        if(name!=null&&name.StartsWith(flute,System.StringComparison.Ordinal)
            &&int.TryParse(name.Substring(flute.Length),out var f)&&f>=1&&f<=23)
        {reverse=f>=8&&f<=11||f>=20;return true;}
        return false;
    }
}
