using System;

internal static class Program
{
    static int assertions;
    static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static bool IndependentOverlap(double minimumX,double minimumZ,double angle)
    {
        double c=Math.Cos(angle),s=Math.Sin(angle);
        double[] ax={1,0,c,s},az={0,1,-s,c};
        var corners=new double[,]{{-2.2,-7},{2.2,-7},{2.2,7},{-2.2,7}};
        for(int a=0;a<4;a++)
        {
            double low=double.MaxValue,high=double.MinValue;
            for(int k=0;k<4;k++)
            {
                double x=c*corners[k,0]+s*corners[k,1],z=-s*corners[k,0]+c*corners[k,1];
                double p=x*ax[a]+z*az[a];low=Math.Min(low,p);high=Math.Max(high,p);
            }
            double squareLow=double.MaxValue,squareHigh=double.MinValue;
            for(int k=0;k<4;k++)
            {
                double x=minimumX+(k%2),z=minimumZ+(k/2),p=x*ax[a]+z*az[a];
                squareLow=Math.Min(squareLow,p);squareHigh=Math.Max(squareHigh,p);
            }
            if(high<squareLow-1e-9||squareHigh<low-1e-9)return false;
        }
        return true;
    }
    static int Main()
    {
        Check(RebirthPurgeStartStability.Accept(true,4,0,0,1,0,.5),"Settled native grounded truck rejected");
        Check(!RebirthPurgeStartStability.Accept(false,4,0,0,1,0,.5),"Unloaded collision admitted");
        Check(!RebirthPurgeStartStability.Accept(true,0,0,0,1,0,.5),"Airborne model admitted");
        Check(!RebirthPurgeStartStability.Accept(true,2,0,0,1,0,.5),"Unstable two-wheel support admitted");
        Check(!RebirthPurgeStartStability.Accept(true,4,400,0,1,0,.5),"Launch velocity admitted");
        Check(!RebirthPurgeStartStability.Accept(true,4,0,1,1,0,.5),"Rotating truck admitted");
        Check(!RebirthPurgeStartStability.Accept(true,4,0,0,.5,0,.5),"Tipped truck admitted");
        Check(!RebirthPurgeStartStability.Accept(true,4,0,0,1,1,.5),"Physics/model disagreement admitted");
        Check(!RebirthPurgeStartStability.Accept(true,4,0,0,1,0,158.3),"Actual logged airborne altitude admitted");
        Check(!RebirthPurgeStartStability.Accept(true,4,double.NaN,0,1,0,.5),"Invalid native sample admitted");
        Check(!RebirthPurgeStartFootprint.PlayerOutside(0,0),"Shared player/truck origin admitted");
        Check(RebirthPurgeStartFootprint.PlayerOutside(0,-10),"Qualified staging point refused");
        Check(!RebirthPurgeStartFootprint.PlayerOutside(0,-2),"Alternate truck at -8m encloses staged player");
        Check(!RebirthPurgeStartFootprint.PlayerOutside(0,6),"Alternate truck at -16m encloses staged player");
        Check(RebirthPurgeStartFootprint.PlayerOutside(0,14),"Separate alternate truck at -24m refused");
        var settle=new RebirthPurgeStartStability();
        Check(!settle.Observe(0,true),"First sample is not stable interval");
        Check(!settle.Observe(.25,true),"Early reveal quarter second");
        Check(!settle.Observe(.5,true),"Early reveal half second");
        Check(!settle.Observe(.75,true),"Early reveal three-quarter second");
        Check(settle.Observe(1,true),"Continuous one second rejected");
        Check(!settle.Observe(1.25,false),"Contact loss did not revoke readiness");
        Check(!settle.Observe(1.5,true),"Contact regain skipped fresh interval");
        settle.Reset();
        Check(!settle.Observe(2,true),"Attachment transition failed to reset interval");
        Check(!settle.Observe(3,true),"Stale gap reused old witness");
        Check(!settle.Observe(2.5,true),"Clock reversal reused old witness");
        // The temporary player capsule is ten metres behind the truck, beyond
        // its full seven-metre clearance half-length plus a .3m player radius.
        for(int degrees=0;degrees<360;degrees+=5)
        {
            double a=degrees*Math.PI/180;
            double stageX=-Math.Sin(a)*10,stageZ=-Math.Cos(a)*10;
            double localZ=Math.Sin(a)*stageX+Math.Cos(a)*stageZ;
            Check(RebirthPurgeStartFootprint.PlayerOutside(0,localZ),"Staged player overlaps truck at heading="+degrees);
        }
        for(int eventSeen=0;eventSeen<2;eventSeen++)for(int local=0;local<2;local++)for(int reportedSpawned=0;reportedSpawned<2;reportedSpawned++)
        {
            bool allowed=RebirthPurgeStartHandoff.CanRun(eventSeen!=0,local!=0,reportedSpawned!=0);
            bool expected=eventSeen!=0&&(local==0||reportedSpawned!=0);
            Check(allowed==expected,"Native spawn-event handoff policy regression");
        }
        Check(!RebirthPurgeStartHandoff.CanRun(false,false,true),"Remote always-true IsSpawned cannot authorize pre-entry handoff");
        Check(RebirthPurgeStartFootprint.IntersectsVoxel(2.1,0,0),"Partial side voxel must be inspected even when centre is outside");
        Check(RebirthPurgeStartFootprint.IntersectsVoxel(0,6.9,0),"Partial end voxel must be inspected");
        Check(!RebirthPurgeStartFootprint.IntersectsVoxel(20,20,0),"Remote voxel should not be inspected");
        int edgeCells=0;
        for(int degrees=0;degrees<360;degrees+=5)
        {
            double angle=degrees*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
            for(int x=-10;x<10;x++)for(int z=-10;z<10;z++)
            {
                // Fractional truck placement exercises both sides of voxel boundaries.
                double px=x-.137,pz=z-.619;
                bool actual=RebirthPurgeStartFootprint.IntersectsVoxel(px,pz,angle),expected=IndependentOverlap(px,pz,angle);
                Check(actual==expected,"Voxel mismatch at angle="+degrees+" x="+px+" z="+pz);
                double cx=px+.5,cz=pz+.5;
                if(actual&&(Math.Abs(c*cx-s*cz)>2.2||Math.Abs(s*cx+c*cz)>7))edgeCells++;
            }
        }
        Check(edgeCells>0,"Fixture must cover cells missed by the old centre sampling");
        Console.WriteLine("PASS "+assertions+" footprint/handoff assertions; "+edgeCells+" intersecting edge cells missed by old centre-only sampling.");
        return 0;
    }
}
