using System;
using Stack=RebirthGearInventoryPlan.Stack;

// Bounded detached encoded data only; no native source authentication.
public static class RebirthGearEncodedSnapshot
{
    public static bool TryCopy(Stack[] bag,Stack[] belt,int owned,out Stack[] bagCopy,out Stack[] beltCopy)
    {
        bagCopy=null;beltCopy=null;
        if(bag==null||bag.Length<52||bag.Length>169||belt==null||belt.Length<4||belt.Length>20||
            owned<4||owned>18||owned>belt.Length)return false;
        try
        {
            int budget=4*1024*1024;
            var b=new Stack[bag.Length];var t=new Stack[belt.Length];
            for(int group=0;group<2;group++)
            {
                var input=group==0?bag:belt;var output=group==0?b:t;
                for(int i=0;i<input.Length;i++)
                {
                    var s=input[i];
                    if(s==null||s.Count<0||s.ItemData==null)return false;
                    if(s.Count==0){if(s.ItemData.Length!=0)return false;}
                    else
                    {
                        if(s.ItemData.Length==0||s.ItemData.Length>262144||s.ItemData.Length>budget)return false;
                        var bytes=Convert.FromBase64String(s.ItemData);
                        if(bytes.Length==0||bytes.Length>196608||Convert.ToBase64String(bytes)!=s.ItemData)return false;
                        budget-=s.ItemData.Length;
                    }
                    output[i]=new Stack{ItemData=s.ItemData,Count=s.Count};
                }
            }
            bagCopy=b;beltCopy=t;return true;
        }
        catch{return false;}
    }
}