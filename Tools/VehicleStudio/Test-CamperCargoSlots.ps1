$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperCargoSlots.cs'))
$source=$source.Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.Xml.Linq;','').Replace('using HarmonyLib;','')
$doubles=@'
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
namespace HarmonyLib {public sealed class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n){}}}
public struct Vector2i {public int x,y;}
public sealed class ItemStackGrid {public int Length;}
public sealed class Bag {public ItemStackGrid ItemGrid=new ItemStackGrid();}
public sealed class EntityVehicle {public bool Camper;public Bag bag=new Bag();}
public static class RebirthCamperRadial {public static bool IsCamper(EntityVehicle v){return v!=null&&v.Camper;}}
public static class RebirthCamperStorageCapacity {public const int MaximumNativeSlots=32767;}
public sealed class XUiM_Vehicle {public EntityVehicle CurrentVehicle;}
public sealed class XUi {public XUiM_Vehicle Vehicle=new XUiM_Vehicle();}
public sealed class XUiWindowGroup {}
public class XUiController {
 public List<XUiC_ItemStack> Cells=new List<XUiC_ItemStack>();
 public T[] GetChildrenByType<T>() where T:class {return Cells.ToArray() as T[];}
 public void SetAllChildrenDirty(){}
}
public sealed class XUiC_ItemStack:XUiController {
 public int Slot,Stage;
 public void AutoBindComponents(){if(Stage++!=0)throw new Exception("Wrong initialization order.");}
 public void Init(){if(Stage++!=1)throw new Exception("Wrong initialization order.");}
 public void AutoBindEvents(){if(Stage++!=2)throw new Exception("Wrong initialization order.");}
}
public sealed class XUiV_Grid {
 public XUiController Controller;public int Rows,Columns=10;
 public void SetRepeatContentTemplateParams(Dictionary<string,object> p,int slot){p["repeat_col"]=slot%Columns;p["repeat_row"]=slot/Columns;}
}
public sealed class XUiView {public XUiController Controller;}
public sealed class XUiC_BagContainer:XUiController {
 public XUi xui=new XUi();public XUiV_Grid grid=new XUiV_Grid();
 public XUiC_ItemStack[] itemControllers;public XUiWindowGroup WindowGroup=new XUiWindowGroup();
 public Vector2i GridCellSize {get{return new Vector2i{x=75,y=75};}}
 public void SetBag(Bag b){}
}
public static class XUiFromXml {
 public static int Created;
 public static XUiView parseViewComponents(XElement node,XUiWindowGroup group,XUiController parent,string id,Dictionary<string,object> p){
  int slot=int.Parse(id);if(node.Name!="item_stack"||(int)node.Attribute("cell_size")!=75||(int)p["repeat_i"]!=slot||(int)p["repeat_row"]!=slot/10)throw new Exception("Native template parameters lost.");
  var cell=new XUiC_ItemStack{Slot=slot};parent.Cells.Add(cell);Created++;return new XUiView{Controller=cell};
 }
}
public static class CamperCargoSlotChecks {
 static void Invoke(string name,XUiC_BagContainer ui,Bag bag){typeof(RebirthCamperCargoSlots).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{ui,bag});}
 public static void Run(){
  var v=new EntityVehicle{Camper=true};v.bag.ItemGrid.Length=501;
  var ui=new XUiC_BagContainer();ui.xui.Vehicle.CurrentVehicle=v;ui.grid.Controller=ui;
  for(int i=0;i<150;i++)ui.Cells.Add(new XUiC_ItemStack{Slot=i,Stage=3});ui.itemControllers=ui.Cells.ToArray();
  Invoke("Prefix",ui,v.bag);Invoke("Postfix",ui,v.bag);
  if(ui.itemControllers.Length!=501||XUiFromXml.Created!=351||ui.grid.Rows!=51)throw new Exception("High cargo positions are inaccessible.");
  for(int i=0;i<501;i++)if(ui.itemControllers[i].Slot!=i||ui.itemControllers[i].Stage!=3)throw new Exception("Cargo slot identity/init lost.");
  Invoke("Prefix",ui,v.bag);if(XUiFromXml.Created!=351)throw new Exception("Reopen duplicated controllers.");
  v.bag.ItemGrid.Length=20;Invoke("Postfix",ui,v.bag);if(ui.grid.Rows!=2)throw new Exception("Shrunk cargo did not resize scrolling.");
  var other=new Bag();other.ItemGrid.Length=900;Invoke("Prefix",ui,other);if(XUiFromXml.Created!=351)throw new Exception("Unrelated bag was intercepted.");
  v.Camper=false;v.bag.ItemGrid.Length=900;Invoke("Prefix",ui,v.bag);if(XUiFromXml.Created!=351)throw new Exception("Ordinary vehicle was intercepted.");
 }
}
'@
Add-Type -TypeDefinition ($doubles+"`n"+$source)
[CamperCargoSlotChecks]::Run()
Write-Output 'PASS: all501 native cargo cells accessible; exact native template/init order; reopen reuse; shrink rows; unrelated bag/vehicle unchanged. UI adapters are doubles, not gameplay evidence.'
