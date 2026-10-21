using System;

// Continuous manual stops follow actual clearance instead of a rectangular
// reduction of all four turrets' available yaw/pitch ranges.
internal static class RebirthCamperAimBoundary
{
    internal static bool Clamp(float startYaw,float startPitch,ref float yaw,ref float pitch,Func<float,float,bool> clear)
    {
        float targetYaw=yaw,targetPitch=pitch;
        if(!Finite(startYaw)||!Finite(startPitch)||!Finite(yaw)||!Finite(pitch))
        {yaw=startYaw;pitch=startPitch;return false;}
        bool reached=Sweep(startYaw,startPitch,targetYaw,targetPitch,clear,out yaw,out pitch);
        if(!reached)
        {
            float edgeYaw=yaw,edgePitch=pitch;
            Sweep(edgeYaw,edgePitch,targetYaw,edgePitch,clear,out float ay,out float ap);
            Sweep(ay,ap,ay,targetPitch,clear,out ay,out ap);
            Sweep(edgeYaw,edgePitch,edgeYaw,targetPitch,clear,out float by,out float bp);
            Sweep(by,bp,targetYaw,bp,clear,out by,out bp);
            if(Error(ay,ap,targetYaw,targetPitch)<=Error(by,bp,targetYaw,targetPitch)){yaw=ay;pitch=ap;}
            else{yaw=by;pitch=bp;}
        }
        clear(yaw,pitch);
        return reached||Error(yaw,pitch,targetYaw,targetPitch)<.000001f;
    }
    private static bool Sweep(float sy,float sp,float ty,float tp,Func<float,float,bool> clear,out float y,out float p)
    {
        y=sy;p=sp;int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(ty-sy),Math.Abs(tp-sp))/.5f));
        for(int i=1;i<=steps;i++)
        {
            float fraction=(float)i/steps,ny=sy+(ty-sy)*fraction,np=sp+(tp-sp)*fraction;
            if(clear(ny,np)){y=ny;p=np;continue;}
            float low=(float)(i-1)/steps,high=fraction;
            for(int n=0;n<10;n++){float mid=(low+high)*.5f;if(clear(sy+(ty-sy)*mid,sp+(tp-sp)*mid))low=mid;else high=mid;}
            y=sy+(ty-sy)*low;p=sp+(tp-sp)*low;return false;
        }
        return true;
    }
    private static float Error(float y,float p,float ty,float tp)=>(y-ty)*(y-ty)+(p-tp)*(p-tp);
    private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
}
