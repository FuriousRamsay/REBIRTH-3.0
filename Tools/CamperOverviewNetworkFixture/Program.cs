using System;
public static class RebirthCamperCraftingStation { public const int ModuleSlots=20; }
public sealed class RebirthCamperTransferSnapshot { public RebirthCamperStationInstallations Stations; }
public sealed class RebirthCamperStationInstallations
{
    public Guid VehicleIdentity; public long Revision;
    public sealed class Installation { public Guid Id; public State State=new State(); }
    public sealed class State { public long Revision; public int Queue; }
    public readonly Installation[] Slots=new Installation[20];
    public Installation GetSlot(int i)=>Slots[i];
}
internal static class Program
{
    static int passed;
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);passed++;}
    static RebirthCamperTransferSnapshot Image(Guid vehicle,Guid station,long manifest,long revision,int queue)
    {
        var result=new RebirthCamperTransferSnapshot{Stations=new RebirthCamperStationInstallations{VehicleIdentity=vehicle,Revision=manifest}};
        result.Stations.Slots[19]=new RebirthCamperStationInstallations.Installation{Id=station,State=new RebirthCamperStationInstallations.State{Revision=revision,Queue=queue}};
        return result;
    }
    static void Main()
    {
        foreach(var mode in new[]{"peer host/client","dedicated server/client"})
        {
            var vehicle=Guid.NewGuid();var station=Guid.NewGuid();
            var old=Image(vehicle,station,3,10,4);var fresh=Image(vehicle,station,3,12,2);
            Check(RebirthCamperOverviewSnapshotPolicy.IsFresh(null,old),mode+" initial reply");
            var displayed=old;
            if(RebirthCamperOverviewSnapshotPolicy.IsFresh(displayed,fresh))displayed=fresh;
            if(RebirthCamperOverviewSnapshotPolicy.IsFresh(displayed,old))displayed=old;
            Check(displayed.Stations.GetSlot(19).State.Queue==2,mode+" delayed reply must not restore old queue");
            Check(RebirthCamperOverviewSnapshotPolicy.IsFresh(displayed,fresh),mode+" duplicate equal reply");
            Check(!RebirthCamperOverviewSnapshotPolicy.IsFresh(displayed,Image(Guid.NewGuid(),station,4,20,0)),mode+" other vehicle reply");
            Check(!RebirthCamperOverviewSnapshotPolicy.IsFresh(displayed,Image(vehicle,station,2,20,0)),mode+" older manifest reply");
            Check(!RebirthCamperOverviewSnapshotPolicy.IsFresh(displayed,Image(vehicle,Guid.NewGuid(),3,20,0)),mode+" inconsistent identity");
            Check(RebirthCamperOverviewSnapshotPolicy.IsFresh(displayed,Image(vehicle,Guid.NewGuid(),4,0,0)),mode+" new installation manifest");
            var missing=Image(vehicle,station,3,12,0);missing.Stations.Slots[19]=null;
            Check(!RebirthCamperOverviewSnapshotPolicy.IsFresh(displayed,missing),mode+" missing station without manifest revision");
            Check(!RebirthCamperOverviewSnapshotPolicy.IsFresh(displayed,null),mode+" absent reply");
        }
        Console.WriteLine("PASS "+passed+" simulated network-order assertions; native UI/transport are doubles.");
    }
}
