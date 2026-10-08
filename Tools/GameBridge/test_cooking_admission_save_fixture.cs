using System;using System.Collections.Generic;
class EntityPlayer{}
class Ready {public string Book;}
class Cooking {public Dictionary<string,object> Tickets=new Dictionary<string,object>();public Dictionary<string,Ready> Recipes=new Dictionary<string,Ready>();public int Changes;public void JobsChanged(){Changes++;}}
class Progression {public Cooking Cooking=new Cooking();}
class Record {public Progression Progression=new Progression();public string Dirty;}
static class RebirthWorldCharacterService {public static bool Save;public static bool FlushPlayer(EntityPlayer p,string reason)=>Save;public static void MarkDirty(Record r,string reason){r.Dirty=reason;}}
class Service {
 static string ReadyState(Cooking memory)=>"saved-ready";
 internal static string Register(EntityPlayer player,Record record,string token){
// REGISTRATION
 }
 internal static string Prepare(EntityPlayer player,Record record,string recipe,Ready previousPreparation){
// PREPARATION
 }
}
class Check {static void Assert(bool v,string text){if(!v)throw new Exception(text);}static void Main(){var p=new EntityPlayer();var r=new Record();var job=new object();r.Progression.Cooking.Tickets["new"]=job;r.Progression.Cooking.Tickets["other"]=new object();RebirthWorldCharacterService.Save=false;Assert(Service.Register(p,r,"new").Length>0,"Unsaved ticket must fail admission");Assert(!r.Progression.Cooking.Tickets.ContainsKey("new")&&r.Progression.Cooking.Tickets.ContainsKey("other")&&r.Progression.Cooking.Changes==1,"Only unsaved ticket rolled back and revision changed");Assert(r.Dirty=="cooking-batch-admission-rollback","Rollback remains dirty");r.Progression.Cooking.Tickets["new"]=job;RebirthWorldCharacterService.Save=true;Assert(Service.Register(p,r,"new")==""&&r.Progression.Cooking.Tickets["new"]==job,"Saved ticket accepted");var prior=new Ready{Book="earned"};var replacement=new Ready{Book="replacement"};r.Progression.Cooking.Recipes["dish"]=replacement;RebirthWorldCharacterService.Save=false;Assert(Service.Prepare(p,r,"dish",prior).Length>0&&r.Progression.Cooking.Recipes["dish"]==prior,"Failed preparation restores older earned benefit");r.Progression.Cooking.Recipes["dish"]=replacement;Assert(Service.Prepare(p,r,"dish",null).Length>0&&!r.Progression.Cooking.Recipes.ContainsKey("dish"),"Failed first preparation grants nothing");r.Progression.Cooking.Recipes["dish"]=replacement;RebirthWorldCharacterService.Save=true;Assert(Service.Prepare(p,r,"dish",prior)=="saved-ready"&&r.Progression.Cooking.Recipes["dish"]==replacement,"Saved benefit acknowledged");Console.WriteLine("PASS actual cooking registration/preparation persistence branches with repository doubles: rejected unsaved admission, isolated ticket rollback, preserved older benefit and success acknowledgement");}}
