using System.Linq;

public static partial class RebirthDogLifecycleService
{
    // Session retirement is not a financial outcome. The original WAL and native
    // stamps remain the only inputs to a subsequent actual-load qualification.
    private static void RetireReplacedHireWorld()
    {
        if(!ThreadManager.IsMainThread())return;
        var current=GameManager.Instance?.World;
        if(current==null)return; // shutdown alone supplies no replacement witness.
        foreach(var original in HireSettlements.Values.Where(value=>value.World!=null&&!ReferenceEquals(value.World,current)).ToArray())
        {
            if(original.Busy)continue;
            original.ObservationRetired=true;
            HireSettlements.Remove(original.State.StableId);
            ReleaseHireOwner(original); // active file workers retain their exact allocation until completion.
        }
        foreach(var entry in HireRemoteReadies.Where(value=>!ReferenceEquals(value.Value.World,current)).ToArray())HireRemoteReadies.Remove(entry.Key);
        foreach(var entry in HireRemoteTerminals.Where(value=>!ReferenceEquals(value.Value.World,current)).ToArray())HireRemoteTerminals.Remove(entry.Key);
        lock(HireColdGate)
        {
            foreach(var load in HireNativeOwners.Values.Where(value=>value.World!=null&&!ReferenceEquals(value.World,current)).ToArray())
            {
                if(ReferenceEquals(load,EnteredHireNativeOwnerLoad))continue;
                HireNativeOwners.Remove(load.Data);
                load.Budget?.Dispose();load.Budget=null;load.Native=null;load.NativeBag=null;load.NativeBelt=null;load.NativeBuffs=null;
                load.BeltGrid=null;load.BagGrid=null;load.BeltRefs=null;load.BagRefs=null;load.Actor=null;load.Data=null;load.World=null;load.Spawn=null;load.SourceStream=null;
            }
            foreach(var body in HireNativeDogs.Values.Where(value=>value.World!=null&&!ReferenceEquals(value.World,current)).ToArray())
            {HireNativeDogs.Remove(body.Dog);body.Dog=null;body.World=null;}
        }
    }
    private static void DropRetiredHireGraph(HireSettlement original)
    {
        original.Sources.Clear();original.World=null;original.Player=null;original.Dog=null;original.State=null;
        original.Inventory=null;original.Bag=null;
    }
}
