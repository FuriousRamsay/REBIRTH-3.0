using System;
namespace HarmonyLib {public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string method){}}}
class EntityVehicle {public int Mode;public bool Camper=true,Observed=true;}
class Vehicle {public EntityVehicle entity;}
class VPEngine {public Vehicle vehicle;}
static class RebirthCamperEnergyDrivingInput {public static bool Blocked(EntityVehicle e)=>e!=null&&e.Camper&&e.Observed&&e.Mode!=0;}
class Program {
 static int checks;static void Check(bool value,bool expected){checks++;if(value!=expected)throw new Exception("Engine admission failed");}
 static void Main(){
 foreach(bool observed in new[]{false,true})foreach(bool camper in new[]{false,true})foreach(int mode in new[]{0,1,2,3}){
 var e=new EntityVehicle{Observed=observed,Camper=camper,Mode=mode};
 Check(RebirthCamperCampEngine.Prefix(new VPEngine{vehicle=new Vehicle{entity=e}}),!observed||!camper||mode==0);
 }
 Check(RebirthCamperCampEngine.Prefix(null),true);Check(RebirthCamperCampEngine.Prefix(new VPEngine()),true);
 Console.WriteLine("PASS "+checks+" actual engine-prefix admission assertions; mode observation boundary stubbed.");
 }
}
