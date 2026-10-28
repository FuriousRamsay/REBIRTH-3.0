// PREPARED NOT_RUN. Production scoped readback method injected by runner.
// Native restore, installed-item clone, budget and thread status are explicit
// doubles. Checks reservation/disposal only, never native-file authority.
using System;
interface IRebirthStationSnapshotByteBudget {bool TryReserveAllocation(long bytes,out IDisposable allocation);}
static class ThreadManager {internal static bool MainThread=true;internal static bool IsMainThread()=>MainThread;}
sealed class RebirthCamperStationState {}
sealed class ItemStack {}
static class RebirthRemoteResourcePlacedStationArtifact {internal const long MaximumOperationWorkspace=3000;}
static class RebirthCamperStationSnapshot
{
    internal static int Restores;internal static bool Throw;
    internal static RebirthCamperStationState Restore(Guid id,ItemStack item,byte[] bytes)
    {Restores++;if(Throw)throw new InvalidOperationException("fixture restore failure");return new RebirthCamperStationState();}
}
sealed class Budget:IRebirthStationSnapshotByteBudget
{
    internal bool Refuse;internal int Held,Released;
    private sealed class Token:IDisposable {internal Budget Owner;public void Dispose(){if(Owner==null)return;Owner.Held--;Owner.Released++;Owner=null;}}
    public bool TryReserveAllocation(long bytes,out IDisposable allocation)
    {allocation=null;if(Refuse)return false;Held++;allocation=new Token{Owner=this};return true;}
}
sealed class Publication
{
    internal byte[] snapshot=new byte[]{1,2,3};internal ItemStack installedItem=new ItemStack();internal IRebirthStationSnapshotByteBudget budget;
    internal sealed class Location {internal Guid Installation;}
    internal Location Address=new Location();
    private static ItemStack CloneInstalled(ItemStack item,IRebirthStationSnapshotByteBudget budget,out IDisposable allocation)
    {allocation=null;if(budget!=null&&!budget.TryReserveAllocation(1,out allocation))throw new InvalidOperationException("fixture clone pressure");return new ItemStack();}
    // ACTUAL_COMPARISON
}
static class Program
{
    private static void Need(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static void Main()
    {
        var budget=new Budget();var p=new Publication{budget=budget};
        Need(p.ReadStationSnapshot(s=>budget.Held==2)&&budget.Held==0&&budget.Released==2,"read callback under both reservations then release");
        Need(!p.ReadStationSnapshot(s=>false)&&budget.Held==0&&budget.Released==4,"predicate refusal releases both");
        budget.Refuse=true;Need(!p.ReadStationSnapshot(s=>true)&&RebirthCamperStationSnapshot.Restores==2,"pressure prevents restore");budget.Refuse=false;
        ThreadManager.MainThread=false;Need(!p.ReadStationSnapshot(s=>true)&&RebirthCamperStationSnapshot.Restores==2,"wrong thread prevents restore");ThreadManager.MainThread=true;
        RebirthCamperStationSnapshot.Throw=true;bool threw=false;try{p.ReadStationSnapshot(s=>true);}catch(InvalidOperationException){threw=true;}
        Need(threw&&budget.Held==0&&budget.Released==6,"restore exception releases both");RebirthCamperStationSnapshot.Throw=false;
        threw=false;try{p.ReadStationSnapshot(s=>{throw new InvalidOperationException();});}catch(InvalidOperationException){threw=true;}
        Need(threw&&budget.Held==0&&budget.Released==8,"predicate exception releases both");
        p.snapshot=null;Need(!p.ReadStationSnapshot(s=>true)&&RebirthCamperStationSnapshot.Restores==4,"closed publication prevents restore");
    }
}
