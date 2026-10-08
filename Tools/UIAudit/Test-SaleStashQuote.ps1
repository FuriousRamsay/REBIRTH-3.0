$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$stub=@"
using System;
using System.Globalization;
public class XUi {public Trader Trader;}
public class Trader {public TraderData TraderData;}
public class TraderData {public int Existing;public int GetPrimaryItemCount(ItemValue v)=>Existing;}
public static class TraderInfo {public static int TraderBuyLimit=1;}
public class ItemClass {public int MaxCount=10,EconomicBundleSize=1;public bool SellableToTrader=true;public bool IsBlock()=>false;}
public class ItemValue {public ItemClass ItemClass;public int type;}
public class ItemStack {public ItemValue itemValue;public int count;public bool IsEmpty()=>count==0;public ItemStack Clone()=>new ItemStack{itemValue=itemValue,count=count};}
public class Block {public static Block[] list=new Block[1];public bool SellableToTrader=true;public int EconomicBundleSize=1;}
public class RebirthBackpackSellStashView {public ItemStack[] Slots;public int Capacity=>Slots.Length;public bool TryGetSlot(int i,out ItemStack stack){stack=Slots[i];return true;}}
public static class RebirthItemSaleEstimate {public static bool TryGet(XUi ui,ItemStack s,out int price){price=s.count*3;return true;}}
public static class StashQuoteFixture {public static string Run(){
var empty=new ItemStack{count=0,itemValue=new ItemValue()};
var full=new ItemStack{count=6,itemValue=new ItemValue{ItemClass=new ItemClass()}};
var view=new RebirthBackpackSellStashView{Slots=new[]{empty,full,empty,full}};
if(RebirthBackpackSaleQuote.Format(null,view)!="36 $")throw new Exception("Empty slots or duplicate unconstrained stack failed");
var ui=new XUi{Trader=new Trader{TraderData=new TraderData{Existing=3}}};
if(RebirthBackpackSaleQuote.Format(ui,view)!="21 $")throw new Exception("Duplicate stacks exceeded native trader limit");
if(RebirthBackpackSaleQuote.Format(ui,new RebirthBackpackSellStashView{Slots=new[]{empty}})!="0 $")throw new Exception("Empty section failed");
full.itemValue.ItemClass.SellableToTrader=false;
if(RebirthBackpackSaleQuote.Format(ui,view)!="0 $")throw new Exception("Unsellable item quoted");
return "PASS: production sale-section total handles empty slots, unconstrained duplicates, shared trader limits and unsellable items.";
}}
"@
$production=[IO.File]::ReadAllText((Join-Path $root 'Scripts/UI/RebirthBackpackSaleQuote.cs')).Replace('using System;','').Replace('using System.Globalization;','')
Add-Type -TypeDefinition ($stub+$production)
[StashQuoteFixture]::Run()