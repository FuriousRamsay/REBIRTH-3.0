using System;

internal static class RebirthCamperLaserNearField
{
    // Perspective diameter in pixels = diameter * imageHeight / (2*z*tan(FOV/2)).
    // Keep the native beam beyond its near-camera flare; do not change its width.
    internal static float Start(float diameter,int imageHeight,float verticalFov,float originDepth,float alignment,float length)
    {
        if(!Finite(diameter)||diameter<=0||imageHeight<=0||!Finite(verticalFov)||verticalFov<=0||verticalFov>=180
            ||!Finite(originDepth)||!Finite(alignment)||alignment<.95f||!Finite(length)||length<=.001f)return 0;
        double depth=diameter*imageHeight/(4*Math.Tan(verticalFov*Math.PI/360));
        return (float)Math.Max(0,Math.Min(length-.001f,(depth-originDepth)/alignment));
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}
