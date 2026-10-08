using System; public static class CorridorSlopeFixture{    public static bool TrySlope(float horizontal,float vertical,float accuracy,float offset,out float slope)
    {
        slope=0f;
        if(!Finite(horizontal)||!Finite(vertical)||!Finite(accuracy)||!Finite(offset)||accuracy<0f)return false;
        float degrees=(Math.Abs(horizontal)+Math.Abs(vertical))*accuracy+Math.Abs(offset);
        if(!Finite(degrees)||degrees>=80f)return false;
        slope=(float)Math.Tan(degrees*Math.PI/180d);
        return Finite(slope)&&slope>=0f;
    }
    private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
private static void Check(bool pass,string name){if(!pass)throw new Exception(name);}
public static string Run(){float slope;
Check(TrySlope(0,0,1,0,out slope)&&slope==0,"Zero spread");
Check(TrySlope(5,5,1,0,out slope)&&slope>0.17f&&slope<0.18f,"Ten degree corridor");
Check(TrySlope(-5,-5,1,-2,out slope)&&slope>0.21f,"Signed bounds become absolute");
Check(TrySlope(5,5,0.5f,0,out slope)&&slope>0.08f&&slope<0.09f,"Accuracy scales spread");
Check(!TrySlope(float.NaN,0,1,0,out slope),"Invalid horizontal");
Check(!TrySlope(0,float.PositiveInfinity,1,0,out slope),"Invalid vertical");
Check(!TrySlope(0,0,-1,0,out slope),"Negative accuracy");
Check(!TrySlope(0,0,1,float.NaN,out slope),"Invalid offset");
Check(!TrySlope(40,40,1,0,out slope),"Unbounded cone denied");
Check(!TrySlope(float.MaxValue,float.MaxValue,1,0,out slope),"Overflow denied");
return "PASS: actual corridor-slope policy,10 spread/malformed boundaries.";}
}