using System;
using System.Collections.Generic;
class EntityActivationCommand {public string commandId;public EntityActivationCommand(string id,string a,object b,string c){commandId=id;}}
class Parent {public virtual void InitLocalActivationCommands(Action<EntityActivationCommand> addCallback){}}
class World {public bool Remote;public bool IsRemote(){return Remote;}}
class ClassInfo {public string entityClassName;}
class Runtime {public string StableId="id";}
class RebirthNpcPersistentRecordView {}
class RebirthDogPersistentRecordView {public bool IsTamedWild;}
static class RebirthDogStateService {public static int Reads;public static bool Wild;public static bool TryGetView(string id,out RebirthNpcPersistentRecordView r,out RebirthDogPersistentRecordView d){Reads++;r=null;d=new RebirthDogPersistentRecordView {IsTamedWild=Wild};return true;}}
static class RebirthBeastmasterService {public static bool ready=true;public static HashSet<string> TamedEntityClasses=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
// LOOKUP
}
class Subject:Parent {public ClassInfo EntityClass=new ClassInfo();public World world=new World();public Runtime RebirthRuntimeState=new Runtime();
// MENU
}
class Check {static void Assert(bool b,string m){if(!b)throw new Exception(m);}static void Main(){RebirthBeastmasterService.TamedEntityClasses.Add("TamedWolf");var s=new Subject();s.world.Remote=true;s.EntityClass.entityClassName="tamedwolf";var rows=new List<string>();s.InitLocalActivationCommands(c=>rows.Add(c.commandId));Assert(rows.Contains("rebirthWildCare")&&!rows.Contains("rebirthDogStorage")&&!rows.Contains("rebirthDogPickup"),"wild client actions");Assert(RebirthDogStateService.Reads==0,"client queried persistence");s.EntityClass.entityClassName="Dog";rows.Clear();s.InitLocalActivationCommands(c=>rows.Add(c.commandId));Assert(rows.Contains("rebirthDogStorage")&&rows.Contains("rebirthDogPickup")&&!rows.Contains("rebirthWildCare"),"dog actions");Assert(RebirthDogStateService.Reads==0,"client dog queried persistence");s.world.Remote=false;RebirthDogStateService.Wild=true;rows.Clear();s.InitLocalActivationCommands(c=>rows.Add(c.commandId));Assert(rows.Contains("rebirthWildCare")&&RebirthDogStateService.Reads==1,"server persisted custom animal");Assert(!RebirthBeastmasterService.IsTamedEntityClass(null),"null class");RebirthBeastmasterService.ready=false;Assert(!RebirthBeastmasterService.IsTamedEntityClass("TamedWolf"),"unready authoring");Console.WriteLine("PASS tamed client care, no client persistence, domestic commands, server fallback and lookup guards");}}
