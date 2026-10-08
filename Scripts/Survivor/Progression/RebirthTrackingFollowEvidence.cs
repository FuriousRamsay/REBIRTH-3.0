using System;

// Directional follow evidence only. Does not prove player locomotion or award progression.
public static class RebirthTrackingFollowEvidence
{
    public static float Credit(float previousX,float previousZ,float currentX,float currentZ,
        float targetX,float targetZ,float maximumSampleDistance)
    {
        if(!Finite(previousX)||!Finite(previousZ)||!Finite(currentX)||!Finite(currentZ)||
            !Finite(targetX)||!Finite(targetZ)||!Finite(maximumSampleDistance)||maximumSampleDistance<=0)return 0;
        double dx=(double)currentX-previousX,dz=(double)currentZ-previousZ;
        double moved=Math.Sqrt(dx*dx+dz*dz);
        if(moved<0.10||moved>maximumSampleDistance)return 0;
        double tx=(double)targetX-previousX,tz=(double)targetZ-previousZ;
        double range=Math.Sqrt(tx*tx+tz*tz);
        if(range<0.10)return 0;
        // Only movement along the animal bearing counts; sideways and retreat contribute zero.
        double projected=(dx*tx+dz*tz)/range;
        return projected>0?(float)Math.Min(range,Math.Min(moved,projected)):0;
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}