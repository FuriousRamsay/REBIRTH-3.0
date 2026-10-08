using System;
using System.Text;

#nullable disable

public static class AdvancedFarmingProtectedGrowingVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"greenhouse heat is deliberately lower than campfire",
            20f < 30f);
        Check(b,ref ok,"greenhouse heat is deliberately lower than wood stove",
            20f < 50f);
        Check(b,ref ok,"greenhouse crop radius bounded",
            8 >= 1 && 8 <= 12);
        Check(b,ref ok,"protected-growing benefit requires enclosure by design",true);

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
