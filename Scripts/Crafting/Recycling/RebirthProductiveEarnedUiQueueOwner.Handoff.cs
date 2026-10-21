using System.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private sealed class HandoffOwners
    {internal TileEntityWorkstation Station;internal Witness[] Owners;internal string[] Bindings;internal int[] Counts;}
    internal static object CaptureHandoffOwners(TileEntityWorkstation station)
    {
        if(!installed||station==null||GameManager.Instance?.World==null)return null;
        var rows=station.Queue;if(rows==null)return null;
        var value=new HandoffOwners{Station=station,Owners=new Witness[rows.Length],Bindings=new string[rows.Length],Counts=new int[rows.Length]};
        for(int i=0;i<rows.Length;i++)
        {
            var row=rows[i];if(row?.Recipe==null||row.Multiplier<1)continue;
            value.Bindings[i]=NativeBinding(row);value.Counts[i]=row.Multiplier;
            var found=originals.Where(owner=>System.Object.ReferenceEquals(owner.Station,station)&&owner.Current()&&owner.Matches(row)&&Binding(row)==owner.RecipeBinding).ToArray();
            if(found.Length==1)value.Owners[i]=found[0];
        }
        return value;
    }
    internal static void ObserveHandoffOwners(object state)
    {
        if(state is not HandoffOwners value)return;
        var rows=value.Station.Queue;if(rows==null)return;
        foreach(var row in rows)
        {
            if(row?.Recipe==null||row.Multiplier<1)continue;string binding=NativeBinding(row);
            if(rows.Count(other=>other?.Recipe!=null&&other.Multiplier>0&&NativeBinding(other)==binding)!=1)continue;
            var prior=Enumerable.Range(0,value.Bindings.Length).Where(index=>value.Bindings[index]==binding).ToArray();
            if(prior.Length==1&&row.Multiplier<=value.Counts[prior[0]]&&value.Owners[prior[0]] is Witness owner&&owner.SourceCurrent())
                owner.Recipe=row.Recipe;
        }
    }
    internal static bool HandoffContainsOriginal(object state,Witness source)
        =>state is HandoffOwners value&&source!=null&&source.SourceCurrent()&&value.Owners.Any(owner=>System.Object.ReferenceEquals(owner,source));
}
