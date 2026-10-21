using System;

// Conservative separating-axis test between each entire 1m voxel and the
// clearance rectangle. Sampling only voxel centres misses rotated edge cells.
internal static class RebirthPurgeStartFootprint
{
    internal const double HalfWidth = 2.2, HalfLength = 7;
    internal static bool PlayerOutside(double localX,double localZ)
        =>Math.Abs(localX)>=HalfWidth+.75||Math.Abs(localZ)>=HalfLength+.75;
    internal static double WorldExtentX(double angle) => Math.Abs(Math.Cos(angle))*HalfWidth+Math.Abs(Math.Sin(angle))*HalfLength;
    internal static double WorldExtentZ(double angle) => Math.Abs(Math.Sin(angle))*HalfWidth+Math.Abs(Math.Cos(angle))*HalfLength;
    internal static bool IntersectsVoxel(double minimumX,double minimumZ,double angle)
    {
        double x=minimumX+.5,z=minimumZ+.5,c=Math.Cos(angle),s=Math.Sin(angle);
        if(Math.Abs(x)>WorldExtentX(angle)+.5||Math.Abs(z)>WorldExtentZ(angle)+.5)return false;
        double voxelRadius=.5*(Math.Abs(c)+Math.Abs(s));
        return Math.Abs(c*x-s*z)<=HalfWidth+voxelRadius&&Math.Abs(s*x+c*z)<=HalfLength+voxelRadius;
    }
}

// An authoritative remote player's IsSpawned can be true before the client
// completes native entry. Use the actual spawn event, plus local state locally.
internal static class RebirthPurgeStartHandoff
{
    internal static bool CanRun(bool nativeSpawnObserved,bool local,bool localIsSpawned)
        =>nativeSpawnObserved&&(!local||localIsSpawned);
}

internal sealed class RebirthPurgeStartStability
{
    private double since=-1,last=-1;
    internal static bool Accept(bool loaded,int wheels,double speedSquared,double angularSquared,double upright,double poseErrorSquared,double heightError)
        =>loaded&&wheels>=3&&Finite(speedSquared)&&speedSquared<=.25&&speedSquared>=0&&
          Finite(angularSquared)&&angularSquared<=.25&&angularSquared>=0&&Finite(upright)&&upright>=.96&&
          Finite(poseErrorSquared)&&poseErrorSquared<=.0625&&poseErrorSquared>=0&&Finite(heightError)&&Math.Abs(heightError)<=1.5;
    private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    internal bool Observe(double now,bool acceptable)
    {
        if(!Finite(now)||!acceptable){Reset();return false;}
        if(since<0||last<0||now<last||now-last>.5)since=now;
        last=now;return now-since>=1;
    }
    internal void Reset(){since=last=-1;}
}
