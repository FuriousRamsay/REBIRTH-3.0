using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Finds the largest feasible batch using distinct ingredient types for distinct roles.</summary>
public static class RebirthCookingIngredientPlan
{
    public sealed class Choice { public int Type, Available, Required; }
    private const int MaxRoles = 32;
    private const int MaxChoicesPerRole = 128;

    public static int Solve(List<Choice>[] roles,out int[] types)
    {
        types=roles==null?Array.Empty<int>():new int[roles.Length];
        if(roles==null||roles.Length==0)return 0;
        if(roles.Length>MaxRoles)return 0;
        var ordered=new Choice[roles.Length][];
        for(int r=0;r<roles.Length;r++)
        {
            if(roles[r]==null||roles[r].Count==0||roles[r].Count>MaxChoicesPerRole)return 0;
            ordered[r]=roles[r].Where(Valid).OrderByDescending(Score).ThenBy(c=>c.Type).ToArray();
            if(ordered[r].Length==0)return 0;
        }
        int low=0,high=9999;
        while(low<high)
        {
            int mid=(low+high+1)/2;
            if(Assign(ordered,mid,0,new HashSet<int>(),new int[roles.Length]))low=mid;else high=mid-1;
        }
        Assign(ordered,low,0,new HashSet<int>(),types);
        return low;
    }
    private static bool Valid(Choice c)=>c!=null&&c.Type>0&&c.Available>=0&&c.Required>0;
    private static double Score(Choice c)=>(double)c.Available/c.Required;
    private static bool Assign(Choice[][] roles,int count,int role,HashSet<int> used,int[] types)
    {
        if(role==roles.Length)return true;
        foreach(var choice in roles[role])
        {
            long required=(long)choice.Required*count;
            if(required<0L||required>choice.Available||!used.Add(choice.Type))continue;
            types[role]=choice.Type;
            if(Assign(roles,count,role+1,used,types))return true;
            used.Remove(choice.Type);
        }
        return false;
    }
}
