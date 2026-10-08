using System;
class Recipe { public string craftingArea, Name; public string GetName(){return Name;} }
static class RebirthCookingCatalogue {
 public class Dish { public string Station; }
 public static string Station;
 public static Dish Get(string name){return name=="catalogue"?new Dish{Station=Station}:null;}
}
static class Subject {
// HEAT_METHOD
 public static float BurnAfter(string method){return 600;}
// ADVANCE_METHOD
}
class Check {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
 var r=new Recipe{Name="native",craftingArea="WorkbenchMortarPestle001_FR"};
 Assert(!Subject.NeedsHeat(r),"Native milling must not require fuel");
 r.Name="catalogue"; r.craftingArea="campfire"; RebirthCookingCatalogue.Station="WorkbenchMortarPestle001_FR";
 Assert(!Subject.NeedsHeat(r),"Catalogue milling overrides native area");
 RebirthCookingCatalogue.Station="cold";Assert(!Subject.NeedsHeat(r),"Cold regression");
 RebirthCookingCatalogue.Station="campfire";r.craftingArea="WorkbenchMortarPestle001_FR";
 Assert(Subject.NeedsHeat(r),"Heated catalogue override regression");
 r.Name="native";r.craftingArea="campfire";Assert(Subject.NeedsHeat(r),"Campfire regression");
 Assert(Subject.NeedsHeat(null),"Unknown must retain conservative heat default");
 foreach(bool burning in new[]{false,true}){float elapsed=0,overdue=0;
 Subject.Advance(60,ref elapsed,ref overdue,600,burning,true,"Soup");
 Assert(elapsed==60&&overdue==0,"Milling must finish without deterioration, with or without stray burning state");}
 float e=0,o=0;Subject.Advance(60,ref e,ref o,120,false,false,"Soup");Assert(e==0&&o==0,"Heated recipe must pause without fuel");
 Subject.Advance(60,ref e,ref o,120,true,false,"Soup");Assert(e==60&&o==60,"Heated overcooking preserved");
 Console.WriteLine("PASS: actual NeedsHeat and Advance methods; milling/cold/heated overrides, no-fuel completion and no milling deterioration");
 }
}