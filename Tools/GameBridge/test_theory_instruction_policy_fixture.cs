using System;
// PRODUCTION_CLASS
public static class Test
{
    static float gain;static double remaining;static string reason;
    static bool Calculate(float student=0,float instructor=50,float request=3,long completed=0,long now=20000000000)
    {return RebirthTheoryInstructionPolicy.TryCalculate(student,instructor,request,0,100,20,5,3,1800,completed,now,out gain,out remaining,out reason);}
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    public static void Main()
    {
        Check(Calculate()&&gain==3,"normal capped lesson");
        Check(Calculate(request:100)&&gain==3,"large request bounded");
        Check(Calculate(student:45,instructor:50)&&gain==2.5f,"half gap");
        Check(!Calculate(student:46,instructor:50),"minimum gap");
        Check(!Calculate(instructor:19),"instructor minimum");
        Check(!Calculate(student:100,instructor:100),"ceiling");
        Check(!Calculate(student:float.NaN),"invalid student");
        Check(!Calculate(request:float.PositiveInfinity),"invalid request");
        Check(!Calculate(completed:DateTime.MaxValue.Ticks+1),"invalid history");
        Check(!Calculate(completed:10000000000,now:20000000000)&&remaining==800,"cooldown seconds");
        Check(Calculate(completed:10000000000,now:28000000000),"cooldown boundary");
        Check(!Calculate(completed:21000000000,now:20000000000)&&remaining==1900,"future history fails closed");
        Check(Calculate(student:-15)&&gain==3,"student clamp");
        Console.WriteLine("PASS actual instruction policy: cap, gap, ceiling, malformed state, cooldown boundary/future history");
    }
}
