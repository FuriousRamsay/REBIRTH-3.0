using System;

internal static class RebirthCamperStationBatchLimit
{
    // Matches the admitted plan's bound. No linear scan or per-frame 9999-plan loop.
    internal static int Find(Func<int,bool> canPlan)
    {
        if(canPlan==null||!canPlan(1))return 0;
        int low=1,high=9999;
        while(low<high)
        {
            int middle=low+(high-low+1)/2;
            if(canPlan(middle))low=middle;else high=middle-1;
        }
        return low;
    }
}
