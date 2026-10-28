$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperRemoteStationGridTransfer.cs'))
$source=[regex]::Replace($source,'(?m)^using [^;]+;\r?\n','')
$fixture=@'
using System;using System.Text;
public static class ThreadManager {public static bool IsMainThread()=>true;}
public class ItemClass {public int MaxCount=100;public bool CanStack()=>true;}
public class ItemValue {public string Name;public ItemClass ItemClassOrMissing=new ItemClass();}
public class ItemStack {
 public ItemValue itemValue;public int count;
 public ItemStack(string name,int n){itemValue=new ItemValue{Name=name};count=n;}
 public static ItemStack Empty=>new ItemStack("",0);public bool IsEmpty()=>count<=0;
 public ItemStack Clone(){var x=new ItemStack(itemValue.Name,count);x.itemValue.ItemClassOrMissing.MaxCount=itemValue.ItemClassOrMissing.MaxCount;return x;}
 public bool CanMoveTo(XUiC_ItemStack.StackLocationTypes type,int slot)=>itemValue.Name!="forbidden";
}
public class XUiC_ItemStack {public enum StackLocationTypes {Workstation}}
public class RebirthCamperStationState {public ItemStack[] Fuel,Input,Tools,Output;public int InputSlotCount=1;public float GetFuelTime(ItemStack s)=>s.itemValue.Name=="wood"?10:0;}
public static class RebirthCamperInventorySnapshot {public static byte[] Capture(ItemStack[] a)=>Encoding.UTF8.GetBytes(a[0].itemValue.Name+":"+a[0].count);}
public static class StationGridFixture {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Refuse(Action work,string why){try{work();}catch(ArgumentException){return;}catch(InvalidOperationException){return;}throw new Exception(why);}
 public static void Run(){
  var state=new RebirthCamperStationState {Fuel=new[]{new ItemStack("wood",95)},Input=new[]{new ItemStack("iron",7),new ItemStack("molten",90000)},Tools=new[]{ItemStack.Empty},Output=new[]{new ItemStack("result",9)}};
  var p=RebirthCamperRemoteStationGridTransfer.Prepare(state,RebirthCamperRemoteStationGrid.Fuel,0,RebirthCamperRemoteStationGesture.Swap,new ItemStack("wood",20),state.Fuel[0]);
  Check(p.Cell.count==100&&p.Cursor.count==15&&state.Fuel[0].count==95,"Merge conservation/detached plan failed");
  p=RebirthCamperRemoteStationGridTransfer.Prepare(state,RebirthCamperRemoteStationGrid.Input,0,RebirthCamperRemoteStationGesture.Half,ItemStack.Empty,state.Input[0]);
  Check(p.Cell.count==4&&p.Cursor.count==3,"Native odd half split failed");
  p=RebirthCamperRemoteStationGridTransfer.Prepare(state,RebirthCamperRemoteStationGrid.Tools,0,RebirthCamperRemoteStationGesture.Swap,new ItemStack("tool",5),state.Tools[0]);
  Check(p.Cell.count==1&&p.Cursor.count==4,"Native tool maximum failed");
  p=RebirthCamperRemoteStationGridTransfer.Prepare(state,RebirthCamperRemoteStationGrid.Output,0,RebirthCamperRemoteStationGesture.Preferred,ItemStack.Empty,state.Output[0]);
  Check(p.Cell.IsEmpty()&&p.Cursor.count==9,"Output removal conservation failed");
  Refuse(()=>RebirthCamperRemoteStationGridTransfer.Prepare(state,RebirthCamperRemoteStationGrid.Fuel,0,RebirthCamperRemoteStationGesture.Swap,ItemStack.Empty,new ItemStack("wood",94)),"Stale cell accepted");
  Refuse(()=>RebirthCamperRemoteStationGridTransfer.Prepare(state,RebirthCamperRemoteStationGrid.Input,1,RebirthCamperRemoteStationGesture.Swap,ItemStack.Empty,state.Input[1]),"Molten counter exposed as physical cell");
  Refuse(()=>RebirthCamperRemoteStationGridTransfer.Prepare(state,RebirthCamperRemoteStationGrid.Output,0,RebirthCamperRemoteStationGesture.Swap,new ItemStack("result",1),state.Output[0]),"Output insertion accepted");
  Refuse(()=>RebirthCamperRemoteStationGridTransfer.Prepare(state,RebirthCamperRemoteStationGrid.Fuel,0,RebirthCamperRemoteStationGesture.Swap,new ItemStack("iron",1),state.Fuel[0]),"Nonfuel insertion accepted");
 }
}
'@
Add-Type -TypeDefinition ($fixture+"`n"+$source)
[StationGridFixture]::Run()
Write-Output 'PASS actual remote slot planner: merge/split/tool/output conservation, detached plans, stale-cell refusal, molten exclusion, output/fuel restrictions. Native adapters doubled; transport/UI/gameplay remain unqualified.'
