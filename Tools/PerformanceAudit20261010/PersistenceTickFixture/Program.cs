using System;
static class GameManager { public static Holder Instance = new Holder(); public sealed class Holder { public object World = new object(); } }
public static class EquipmentBefore {
public static long Now=1,nextSaveUtcTicks;
public static int Loads,Saves;
public static bool Server=true;
public static string Path="fixture";
static bool IsServer()=>Server;
static string GetPath()=>Path;
static void EnsureLoaded(){if(GameManager.Instance.World!=null && Path.Length>0) Loads++;}
static void Save(){EnsureLoaded();Saves++;}
public static void Tick()
    {
        if (!IsServer()) return; EnsureLoaded(); long now = Now;
        if (now < nextSaveUtcTicks) return; nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks; Save();
    }
}
public static class EquipmentAfter {
public static long Now=1,nextSaveUtcTicks;
public static int Loads,Saves;
public static bool Server=true;
public static string Path="fixture";
static bool IsServer()=>Server;
static string GetPath()=>Path;
static void EnsureLoaded(){if(GameManager.Instance.World!=null && Path.Length>0) Loads++;}
static void Save(){EnsureLoaded();Saves++;}
public static void Tick()
    {
        if (!IsServer() || GameManager.Instance?.World == null) return;
        long now = Now;
        if (now < nextSaveUtcTicks) return;
        if (string.IsNullOrEmpty(GetPath())) return;
        // Save ensures loading before its dirty check. Keep automatic failed-load
        // retries on the save cadence; explicit operations still load immediately.
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks;
        Save();
    }
}
public static class InventoryBefore {
public static long Now=1,nextSaveUtcTicks;
public static int Loads,Saves;
public static bool Server=true;
public static string Path="fixture";
static bool IsServer()=>Server;
static string GetPath()=>Path;
static void EnsureLoaded(){if(GameManager.Instance.World!=null && Path.Length>0) Loads++;}
static void Save(){EnsureLoaded();Saves++;}
public static void Tick()
    {
        if (!IsServer()) return;
        EnsureLoaded();
        long now = Now;
        if (now < nextSaveUtcTicks) return;
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks;
        Save();
    }
}
public static class InventoryAfter {
public static long Now=1,nextSaveUtcTicks;
public static int Loads,Saves;
public static bool Server=true;
public static string Path="fixture";
static bool IsServer()=>Server;
static string GetPath()=>Path;
static void EnsureLoaded(){if(GameManager.Instance.World!=null && Path.Length>0) Loads++;}
static void Save(){EnsureLoaded();Saves++;}
public static void Tick()
    {
        if (!IsServer() || GameManager.Instance?.World == null) return;
        long now = Now;
        if (now < nextSaveUtcTicks) return;
        if (string.IsNullOrEmpty(GetPath())) return;
        // Save ensures loading before its dirty check. Keep automatic failed-load
        // retries on the save cadence; explicit operations still load immediately.
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks;
        Save();
    }
}
public static class SettlementBefore {
public static long Now=1,nextSaveUtcTicks;
public static int Loads,Saves;
public static bool Server=true;
public static string Path="fixture";
static bool IsServer()=>Server;
static string GetPath()=>Path;
static void EnsureLoaded(){if(GameManager.Instance.World!=null && Path.Length>0) Loads++;}
static void Save(){EnsureLoaded();Saves++;}
public static void Tick(){if(!IsServer())return;EnsureLoaded();long now=Now;if(now<nextSaveUtcTicks)return;nextSaveUtcTicks=now+TimeSpan.FromSeconds(30).Ticks;Save();}
}
public static class SettlementAfter {
public static long Now=1,nextSaveUtcTicks;
public static int Loads,Saves;
public static bool Server=true;
public static string Path="fixture";
static bool IsServer()=>Server;
static string GetPath()=>Path;
static void EnsureLoaded(){if(GameManager.Instance.World!=null && Path.Length>0) Loads++;}
static void Save(){EnsureLoaded();Saves++;}
public static void Tick()
    {
        if (!IsServer() || GameManager.Instance?.World == null) return;
        long now = Now;
        if (now < nextSaveUtcTicks) return;
        if (string.IsNullOrEmpty(GetPath())) return;
        // Save ensures loading before its dirty check. Keep automatic failed-load
        // retries on the save cadence; explicit operations still load immediately.
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks;
        Save();
    }
}
static class ConnectionManager { public static Holder Instance=new Holder(); public sealed class Holder { public bool IsServer=true; } }
static class SimulationBefore { public static long Now=1,nextNeedsTick,nextAssignmentTick; public static int Loads,Needs,Assignments; static void TickNeeds(long now){Needs++;} static void TickAssignments(long now){Assignments++;} public static void Tick()
    {
        if (ConnectionManager.Instance != null && !ConnectionManager.Instance.IsServer) return;
        Loads++;
        long now=Now;
        if(now>=nextNeedsTick){nextNeedsTick=now+TimeSpan.FromSeconds(10).Ticks;TickNeeds(now);}
        if(now>=nextAssignmentTick){nextAssignmentTick=now+TimeSpan.FromSeconds(2).Ticks;TickAssignments(now);}
    } }
static class SimulationAfter { public static long Now=1,nextNeedsTick,nextAssignmentTick; public static int Loads,Needs,Assignments; static void TickNeeds(long now){Needs++;} static void TickAssignments(long now){Assignments++;} public static void Tick()
    {
        if (ConnectionManager.Instance != null && !ConnectionManager.Instance.IsServer) return;
        long now=Now;
        if (now < nextNeedsTick && now < nextAssignmentTick) return;
        Loads++;
        if(now>=nextNeedsTick){nextNeedsTick=now+TimeSpan.FromSeconds(10).Ticks;TickNeeds(now);}
        if(now>=nextAssignmentTick){nextAssignmentTick=now+TimeSpan.FromSeconds(2).Ticks;TickAssignments(now);}
    } }
class Program { static void CheckSilent(bool ok){if(!ok)throw new Exception("Simulation scheduling changed");} static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);} static void Main(){for(int frame=0;frame<1800;frame++) { EquipmentBefore.Now=1+frame*TimeSpan.TicksPerSecond/60; EquipmentAfter.Now=EquipmentBefore.Now; EquipmentBefore.Tick(); EquipmentAfter.Tick(); }
Check(EquipmentBefore.Loads==1801 && EquipmentAfter.Loads==1,"Equipment: failed-load attempts 1801 to 1 over 30 seconds");
EquipmentAfter.Now=1+TimeSpan.FromSeconds(30).Ticks; EquipmentAfter.Tick(); Check(EquipmentAfter.Loads==2,"Equipment: retry at deadline");
EquipmentAfter.nextSaveUtcTicks=0; GameManager.Instance.World=null; EquipmentAfter.Tick(); Check(EquipmentAfter.Loads==2 && EquipmentAfter.nextSaveUtcTicks==0,"Equipment: no world does not consume deadline");
GameManager.Instance.World=new object(); EquipmentAfter.Path=""; EquipmentAfter.Tick(); Check(EquipmentAfter.nextSaveUtcTicks==0,"Equipment: unavailable path does not consume deadline");
EquipmentAfter.Path="fixture"; EquipmentAfter.Server=false; EquipmentAfter.Tick(); Check(EquipmentAfter.Loads==2,"Equipment: client does not load");
EquipmentAfter.Server=true; EquipmentAfter.Tick(); Check(EquipmentAfter.Loads==3,"Equipment: first ready update loads immediately");
for(int frame=0;frame<1800;frame++) { InventoryBefore.Now=1+frame*TimeSpan.TicksPerSecond/60; InventoryAfter.Now=InventoryBefore.Now; InventoryBefore.Tick(); InventoryAfter.Tick(); }
Check(InventoryBefore.Loads==1801 && InventoryAfter.Loads==1,"Inventory: failed-load attempts 1801 to 1 over 30 seconds");
InventoryAfter.Now=1+TimeSpan.FromSeconds(30).Ticks; InventoryAfter.Tick(); Check(InventoryAfter.Loads==2,"Inventory: retry at deadline");
InventoryAfter.nextSaveUtcTicks=0; GameManager.Instance.World=null; InventoryAfter.Tick(); Check(InventoryAfter.Loads==2 && InventoryAfter.nextSaveUtcTicks==0,"Inventory: no world does not consume deadline");
GameManager.Instance.World=new object(); InventoryAfter.Path=""; InventoryAfter.Tick(); Check(InventoryAfter.nextSaveUtcTicks==0,"Inventory: unavailable path does not consume deadline");
InventoryAfter.Path="fixture"; InventoryAfter.Server=false; InventoryAfter.Tick(); Check(InventoryAfter.Loads==2,"Inventory: client does not load");
InventoryAfter.Server=true; InventoryAfter.Tick(); Check(InventoryAfter.Loads==3,"Inventory: first ready update loads immediately");
for(int frame=0;frame<1800;frame++) { SettlementBefore.Now=1+frame*TimeSpan.TicksPerSecond/60; SettlementAfter.Now=SettlementBefore.Now; SettlementBefore.Tick(); SettlementAfter.Tick(); }
Check(SettlementBefore.Loads==1801 && SettlementAfter.Loads==1,"Settlement: failed-load attempts 1801 to 1 over 30 seconds");
SettlementAfter.Now=1+TimeSpan.FromSeconds(30).Ticks; SettlementAfter.Tick(); Check(SettlementAfter.Loads==2,"Settlement: retry at deadline");
SettlementAfter.nextSaveUtcTicks=0; GameManager.Instance.World=null; SettlementAfter.Tick(); Check(SettlementAfter.Loads==2 && SettlementAfter.nextSaveUtcTicks==0,"Settlement: no world does not consume deadline");
GameManager.Instance.World=new object(); SettlementAfter.Path=""; SettlementAfter.Tick(); Check(SettlementAfter.nextSaveUtcTicks==0,"Settlement: unavailable path does not consume deadline");
SettlementAfter.Path="fixture"; SettlementAfter.Server=false; SettlementAfter.Tick(); Check(SettlementAfter.Loads==2,"Settlement: client does not load");
SettlementAfter.Server=true; SettlementAfter.Tick(); Check(SettlementAfter.Loads==3,"Settlement: first ready update loads immediately");
for(int frame=0;frame<3600;frame++){SimulationBefore.Now=1+frame*TimeSpan.TicksPerSecond/60;SimulationAfter.Now=SimulationBefore.Now;SimulationBefore.Tick();SimulationAfter.Tick();CheckSilent(SimulationBefore.Needs==SimulationAfter.Needs && SimulationBefore.Assignments==SimulationAfter.Assignments);} Check(SimulationBefore.Loads==3600 && SimulationAfter.Loads==30,"Simulation: identical needs/assignment timing; 3600 to 30 load checks in 60 seconds");}}