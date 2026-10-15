using System;
using System.Collections.Generic;
using System.Globalization;

// Optional explicit safe pad interior. No Unity dependencies: the exact parser
// and containment checks can be verified without dispatching game input.
internal sealed class RebirthGameBridgeVehicleBounds
{
    internal readonly float XMin,XMax,ZMin,ZMax;
    private RebirthGameBridgeVehicleBounds(float xMin,float xMax,float zMin,float zMax)
    {XMin=xMin;XMax=xMax;ZMin=zMin;ZMax=zMax;}
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    internal bool Contains(float x,float z)=>Finite(x)&&Finite(z)&&x>=XMin&&x<=XMax&&z>=ZMin&&z<=ZMax;
    internal static bool TryParse(IList<string> args,out RebirthGameBridgeVehicleBounds bounds)
    {
        bounds=null;
        if(args==null)return false;
        if(args.Count==1)return true;
        if(args.Count!=5)return false;
        var values=new float[4];
        for(int i=0;i<4;i++)if(!float.TryParse(args[i+1],NumberStyles.Float,CultureInfo.InvariantCulture,out values[i])||!Finite(values[i]))return false;
        if(values[0]>=values[1]||values[2]>=values[3]||values[1]-values[0]>80f||values[3]-values[2]>80f)return false;
        bounds=new RebirthGameBridgeVehicleBounds(values[0],values[1],values[2],values[3]);return true;
    }
}
