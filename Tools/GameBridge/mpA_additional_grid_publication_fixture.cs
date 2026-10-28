// AUTHORED_NOT_RUN. Actual source methods with explicit native grid publication double.
using System;using System.Collections.Generic;
class Value{internal int Type;internal Value Clone()=>new Value{Type=Type};}
class ItemStack
{
 internal Value itemValue;internal int count;
 internal ItemStack(Value value,int quantity){itemValue=value;count=quantity;}
 internal bool IsEmpty()=>count==0;internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);
 internal static ItemStack[] Clone(ItemStack[] cells){var copy=new ItemStack[cells.Length];for(int i=0;i<copy.Length;i++)copy[i]=cells[i].Clone();return copy;}
 internal bool CanStackWith(ItemStack other)=>!IsEmpty()&&itemValue.Type==other.itemValue.Type&&count+other.count<=10;
}
class XUiC_WorkstationGrid{}
class XUiC_WorkstationOutputGrid:XUiC_WorkstationGrid
{internal int Writes;internal ItemStack[] Published;internal void SetSlots(ItemStack[] cells){Writes++;Published=ItemStack.Clone(cells);}}
class Subject
{
 // Original publication is an explicit adapter in this older grid-only cohort.
 class UiOriginal{internal void EnterGrid(ItemStack[] cells){}}
 class UiScope{internal XUiC_WorkstationOutputGrid Grid;internal IReadOnlyList<ItemStack> Outputs;internal bool Inserted;internal ItemStack[] PreparedArgument;internal UiOriginal Original;}
 static UiScope ui;
 // Cached original entry remains an explicit excluded boundary here.
 static void EnterUiCachedOutput(UiScope scope){}
 static void GuardUiCachedGrid(UiScope scope,XUiC_WorkstationGrid grid){}
 // PRODUCTION_INSERT
 // PRODUCTION_PREPARE
 // PRODUCTION_UI_INSERT
 static ItemStack S(int type,int count)=>new ItemStack(new Value{Type=type},count);
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static void Main()
 {
  var grid=new XUiC_WorkstationOutputGrid();var cells=new[]{S(1,1),S(0,0),S(0,0)};
  ui=new UiScope{Grid=grid,Outputs=new[]{S(2,1),S(3,1)}};
  PrepareUiSlots(grid,cells);Check(cells[1].itemValue.Type==2&&cells[2].itemValue.Type==3&&grid.Writes==0,"all extras prepared before first native setter");
  grid.SetSlots(cells);UiInsert(grid,ref cells);Check(grid.Writes==1&&ui.Inserted&&grid.Published[2].itemValue.Type==3,"UpdateData does not issue second grid setter or extra set");
  UiInsert(grid,ref cells);Check(cells[1].count==1&&grid.Writes==1,"same invocation cannot duplicate extras");
  grid=new XUiC_WorkstationOutputGrid();cells=new[]{S(1,1),S(0,0)};ui=new UiScope{Grid=grid,Outputs=new[]{S(2,1),S(3,1)}};
  bool refused=false;try{PrepareUiSlots(grid,cells);}catch(InvalidOperationException){refused=true;}
  Check(refused&&cells[1].IsEmpty()&&ui.PreparedArgument==null&&grid.Writes==0,"later extra refusal leaves complete original argument unchanged");
  cells=new[]{S(1,1),S(0,0)};ui=new UiScope{Grid=grid,Outputs=new[]{S(2,1)}};
  PrepareUiSlots(new XUiC_WorkstationOutputGrid(),cells);Check(cells[1].IsEmpty(),"unrelated native grid untouched");
  PrepareUiSlots(grid,cells);var foreign=ItemStack.Clone(cells);refused=false;try{UiInsert(grid,ref foreign);}catch(InvalidOperationException){refused=true;}
  Check(refused&&grid.Writes==0,"different array cannot borrow actual prepared publication");
  grid=new XUiC_WorkstationOutputGrid();cells=new[]{S(1,1),S(0,0)};ui=new UiScope{Grid=grid,Outputs=new[]{S(2,1)}};
  UiInsert(grid,ref cells);Check(grid.Writes==1&&cells[1].count==1&&ReferenceEquals(ui.PreparedArgument,cells),"direct backend fallback premarks exact array before its one setter");
  Console.WriteLine("PASS coupled native output-grid preparation: 7 scenarios");
 }
}
