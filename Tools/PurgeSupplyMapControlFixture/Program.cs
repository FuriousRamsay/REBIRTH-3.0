using System;using System.Collections.Generic;
struct Vector3 {}
internal sealed class RebirthPurgeSupplyTarget
{
    internal readonly int Id;
    internal readonly Vector3 Position;
    internal RebirthPurgeSupplyTarget(int id,Vector3 position){Id=id;Position=position;}
    internal static int Next(IList<RebirthPurgeSupplyTarget> targets,int previous)
    {for(int i=0;i<targets.Count;i++)if(targets[i].Id>previous)return i;return targets.Count==0?-1:0;}
}
class Program {
    internal static string CounterText(IList<RebirthPurgeSupplyTarget> values,int selected)
    {
        for(int i=0;i<values.Count;i++)if(values[i].Id==selected)return (i+1)+" / "+values.Count;
        return "— / "+values.Count;
    }
 static int checks;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
 static List<RebirthPurgeSupplyTarget> Values(params int[] ids){var result=new List<RebirthPurgeSupplyTarget>();foreach(int id in ids)result.Add(new RebirthPurgeSupplyTarget(id,new Vector3()));return result;}
 static void Main(){
 var drops=Values(10,20,30);int current=0;
 Check(CounterText(drops,current)=="— / 3","no centered drop does not falsely display first selection");
 for(int expected=0;expected<3;expected++){int index=RebirthPurgeSupplyTarget.Next(drops,current);Check(index==expected,"cycle selects drop "+(expected+1));current=drops[index].Id;Check(CounterText(drops,current)==(expected+1)+" / 3","counter follows centered drop "+(expected+1));}
 Check(RebirthPurgeSupplyTarget.Next(drops,current)==0,"third drop wraps to first");
 drops.RemoveAt(1);Check(RebirthPurgeSupplyTarget.Next(drops,20)==1,"removed middle selection continues to next existing drop");
 Check(CounterText(drops,20)=="— / 2","removed selected drop is not misidentified");
 Check(CounterText(drops,30)=="2 / 2","removal refreshes current position and count");
 Check(RebirthPurgeSupplyTarget.Next(Values(),30)==-1,"empty list cannot select a drop");
 Check(RebirthPurgeSupplyTarget.Next(Values(7),7)==0&&CounterText(Values(7),7)=="1 / 1","one drop remains usable");
 Console.WriteLine(checks+" checks passed from actual production Next and CounterText source");
 }
}
