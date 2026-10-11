using System.Xml.Linq;using System.Collections;using System.Linq;using System;using System.IO;using System.Reflection;
class Program {
 static Assembly mod;static object Call(string type,string method,params object[] args)=>mod.GetTypes().Single(t=>t.Name==type).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Single(m=>m.Name==method&&m.GetParameters().Length==args.Length).Invoke(null,args);
 static void Main(string[] args){string root=Path.GetFullPath(args[0]);AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{foreach(string folder in new[]{root,Path.GetFullPath(Path.Combine(root,"../../7DaysToDie_Data/Managed")),Path.GetFullPath(Path.Combine(root,"../0_TFP_Harmony"))}){string p=Path.Combine(folder,new AssemblyName(e.Name).Name+".dll");if(File.Exists(p))return Assembly.LoadFrom(p);}return null;};mod=Assembly.LoadFrom(Path.Combine(root,"RebirthUtils.dll"));Call("RebirthSurvivorDefinitionLoader","SetPreferredModRoot",root);var bundle=Call("RebirthSurvivorDefinitionLoader","Load",Path.Combine(root,"Config/_Survivor"));Call("RebirthSurvivorDefinitionRegistry","Install",bundle);int checkedTiers=0;
foreach(var profile in XDocument.Load(Path.Combine(root,"Config/_Survivor/support_profiles.xml")).Descendants("support_profile")){
 if((string)profile.Attribute("gear_slot_id")!="backpack")continue;
 string id=(string)profile.Attribute("gear_item_id"); int expected=(int)profile.Attribute("bag_slot_bonus")/11*10;
 int actual=(int)Call("RebirthBackpackSellStashPolicy","CapacityForBackpack",id);
 if(actual!=expected || actual==0)throw new Exception(id+" capacity "+actual+" expected "+expected);
 Console.WriteLine("PASS "+id+" sale capacity="+actual);checkedTiers++;
}
if(checkedTiers!=8)throw new Exception("Expected all 8 backpack tiers");
if((int)Call("RebirthBackpackSellStashPolicy","CapacityForBackpack","")!=0)throw new Exception("Empty gear grants stash");
Console.WriteLine("PASS all 8 real loaded backpack tiers plus no-backpack capacity");
}}
