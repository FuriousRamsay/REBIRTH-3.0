using System;
internal static class RebirthCamperCoastPolicy
{
    internal static float Brake(float current,float fullBrake,float throttle,float motor)
    {
        if(float.IsNaN(throttle)||float.IsInfinity(throttle)||Math.Abs(throttle)>.001f||motor!=0||
            float.IsNaN(fullBrake)||float.IsInfinity(fullBrake)||fullBrake<=0)return current;
        return Math.Max(current,fullBrake*.01f);
    }
}
