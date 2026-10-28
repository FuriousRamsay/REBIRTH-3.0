#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// Disposable CodexTest visual fixture only; excluded from Release.
public sealed class ConsoleCmdRebirthCamperArmorPhoto : ConsoleCmdAbstract
{
 public override string[] getCommands()=>new[]{"rbarmorphoto"};
 public override string getDescription()=>"Prepare/inspect the approved armor and plough visual fixture in CodexTest.";
 public override string getHelp()=>"rbarmorphoto ENTITY pattern|plain|inspect|health=N; local disposable CodexTest only.";
 public override void Execute(List<string> args,CommandSenderInfo sender)
 {
  try
  {
   var world=GameManager.Instance.World;var player=world?.GetPrimaryPlayer();
   if(args.Count!=2||player==null||world.IsRemote()||GameManager.IsDedicatedServer||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new Exception(getHelp());
   var e=world.GetEntity(int.Parse(args[0])) as EntityVehicle;
   if(e==null||!RebirthCamperModProtection.IsCamper(e.vehicle)||(e.position-player.position).sqrMagnitude>900||e.GetFirstAttached()!=null)throw new Exception("Nearby dismounted camper required");
   if(args[1].StartsWith("health=",StringComparison.Ordinal))
   {
    int health=int.Parse(args[1].Substring(7)),maximum=(int)e.Stats.Health.BaseMax;
    if(health<1||health>maximum)throw new Exception("Health outside current native capacity");
    e.Health=health;e.vehicle.GetUpdatedItemValue().UseTimes=maximum-health;
    e.vehicle.FireEvent(Vehicle.Event.HealthChanged);
   }
   else if(args[1]!="inspect")
   {
    if(args[1]!="pattern"&&args[1]!="plain")throw new Exception(getHelp());
    var iv=e.vehicle.GetUpdatedItemValue();
    if(iv.ModificationCount<2)throw new Exception("Two existing native mod slots required");
    iv.SetModification(0,new ItemValue(ItemClass.GetItemClass("modVehicleArmor").Id,false));iv.SetModification(1,new ItemValue(ItemClass.GetItemClass("modVehiclePlow").Id,false));
    if(iv.CosmeticModCount<1)iv.ResizeCosmeticMods(1);
    iv.SetCosmeticMod(0,args[1]=="pattern"?new ItemValue(ItemClass.GetItemClass("modVehiclePatternDisco").Id,false):ItemValue.None);
    e.vehicle.OnModsChanged();
   }
   var ts=e.ModelTransform.GetComponentsInChildren<Transform>(true);
   var surfaces=e.ModelTransform.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("REBIRTHArmorSurface_")).ToArray();int rangeCount=0;var pieceIds=new HashSet<string>();var visibleIds=new HashSet<string>();bool validRanges=surfaces.Length>=3;
   foreach(var surface in surfaces){int expected=0;foreach(Transform marker in surface.transform){var fields=marker.name.Split('_');if(fields.Length!=5||fields[0]!="ArmorRange")continue;rangeCount++;pieceIds.Add(fields[1]);if(RebirthCamperArmorVisibility.Visible(e.Health,(int)e.Stats.Health.BaseMax,int.Parse(fields[2]))){visibleIds.Add(fields[1]);expected+=int.Parse(fields[4]);}}validRanges&=surface.sharedMesh.triangles.Length==expected;}
   SdtdConsole.Instance.Output("armorRanges="+rangeCount+" armorPieces="+pieceIds.Count+" visiblePieces="+visibleIds.Count+" visibilityValid="+(validRanges&&pieceIds.Count==54));
   string[] names={"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"};
   foreach(string n in names){var t=ts.Single(x=>x.name==n);SdtdConsole.Instance.Output(n+" active="+t.gameObject.activeInHierarchy);}
   var hull=e.PhysicsTransform.Find("REBIRTHModPlowHull");var colliders=hull.GetComponentsInChildren<MeshCollider>(true);
   SdtdConsole.Instance.Output("health="+e.Health+"/"+e.vehicle.GetMaxHealth()+" itemMaximum="+e.vehicle.itemValue.MaxUseTimes+" colliders="+colliders.Length+" enabled="+hull.gameObject.activeInHierarchy+" valid="+colliders.All(c=>c.convex&&!c.isTrigger&&c.CompareTag("E_VehicleStrong")));
   foreach(var c in colliders.Where(c=>!c.convex||c.isTrigger||!c.CompareTag("E_VehicleStrong")))SdtdConsole.Instance.Output("COLLIDER_INVALID "+c.name+" tag="+c.tag+" convex="+c.convex+" trigger="+c.isTrigger);
   SdtdConsole.Instance.Output("cosmetic="+e.vehicle.itemValue.GetCosmeticMod(0)?.ItemClass?.GetItemName()+"; visual fixture only; combat/driving untested");
  }
  catch(Exception ex){SdtdConsole.Instance.Output("ERROR rbarmorphoto: "+ex.Message);}
 }
}
#endif

