using System;using System.Collections.Generic;
public class EntityVehicle { public int entityId=7; }
public class RebirthVehicleAssembly { public Guid AssemblyId=Guid.NewGuid(); }
static class RebirthVehicleEntityPersistence { public static bool Success;public static int Calls; public static bool WriteAssemblyToVehicleItem(EntityVehicle e,RebirthVehicleAssembly a){Calls++;return Success;} }
static class Check {
static readonly object Sync=new object();static readonly Dictionary<int,RebirthVehicleAssembly> Entities=new Dictionary<int,RebirthVehicleAssembly>();static readonly Dictionary<Guid,string> Carriers=new Dictionary<Guid,string>();
// METHODS
static void Main(){var e=new EntityVehicle();var old=new RebirthVehicleAssembly();var next=new RebirthVehicleAssembly();Entities[e.entityId]=old;Carriers[old.AssemblyId]="entity:7";
bool failed=false;try{RegisterEntity(e,next);}catch(InvalidOperationException){failed=true;}if(!failed||Entities[e.entityId]!=old||Carriers.ContainsKey(next.AssemblyId))throw new Exception("Failed metadata write published");
RebirthVehicleEntityPersistence.Success=true;RegisterEntity(e,next);if(Entities[e.entityId]!=next||Carriers[next.AssemblyId]!="entity:7")throw new Exception("Successful registration missing");
int calls=RebirthVehicleEntityPersistence.Calls;RegisterEntity(null,next);RegisterEntity(e,new RebirthVehicleAssembly{AssemblyId=Guid.Empty});if(calls!=RebirthVehicleEntityPersistence.Calls)throw new Exception("Invalid registration wrote");Console.WriteLine("PASS: actual RegisterEntity blocks publication on failed metadata write, preserves prior index and publishes successful writes. Native metadata persistence substituted.");}
}
