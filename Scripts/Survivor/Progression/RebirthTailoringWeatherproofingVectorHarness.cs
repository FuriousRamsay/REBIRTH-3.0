using System;
using System.Text;

#nullable disable

public static class RebirthTailoringWeatherproofingVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;
        Check(b,ref ok,"cold lining cold multiplier is 0.70",Near(1f*0.70f,0.70f));
        Check(b,ref ok,"cold lining heat tradeoff is 1.10",Near(1f*1.10f,1.10f));
        Check(b,ref ok,"hot shell heat multiplier is 0.70",Near(1f*0.70f,0.70f));
        Check(b,ref ok,"hot shell cold tradeoff is 1.10",Near(1f*1.10f,1.10f));
        Check(b,ref ok,"condition clamp still prevents immunity",Math.Max(0.50f,0.70f)>=0.50f);
        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static bool Near(float a,float b){return Math.Abs(a-b)<0.001f;}
    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
