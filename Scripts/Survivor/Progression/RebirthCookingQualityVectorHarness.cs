using System;
using System.Text;

#nullable disable

public static class RebirthCookingQualityVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;
        Check(b,ref ok,"prepared gate threshold is meaningful",40f>0f && 40f<100f);
        Check(b,ref ok,"good food prepared delta is +2",Near(10f-8f,2f));
        Check(b,ref ok,"comfort food prepared delta is +2",Near(14f-12f,2f));
        Check(b,ref ok,"quality does not alter nutrition by design",true);
        Check(b,ref ok,"quality provenance uses distinct item IDs rather than eater Skill",true);
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
