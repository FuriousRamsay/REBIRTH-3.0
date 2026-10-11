$ErrorActionPreference='Stop'
$fixture=@"
using System;using System.Collections.Generic;using UnityEngine;
namespace UnityEngine {public static class Mathf {public static float Lerp(float a,float b,float t)=>a+(b-a)*Math.Max(0,Math.Min(1,t));public static int CeilToInt(float n)=>(int)Math.Ceiling(n);}}
public enum PassiveEffects{EconomicValue=76,BarteringSelling=149}public enum EnumGamePrefs{Income=130}
public class EntityPlayer{public float Bonus;}
public static class EffectManager{public static float GetValue(PassiveEffects e,ItemValue i,float v,EntityPlayer p,int tags=0)=>e==PassiveEffects.BarteringSelling?p.Bonus:v;}
public static class GamePrefs{public static float GetFloat(EnumGamePrefs e)=>0;}
namespace SandboxOptions { public enum SandboxOptions { TraderSellPrices=130 } public static class SandboxOptionManager { public static float Income=1; public static float GetFloat(SandboxOptions o)=>Income; } }
public class TraderInfo{public float OverrideSellMarkdown=-1;public static float SellMarkdown=.2f,QualityMinMod=.1f,QualityMaxMod=1f;public static int TraderBuyLimit=1;}
public class TraderData{public TraderInfo TraderInfo=new TraderInfo();public int Existing;public int GetPrimaryItemCount(ItemValue v)=>Existing;}
public class ItemClass{public float EconomicValue=100,EconomicSellScale=1,TraderQualityMinMod,TraderQualityMaxMod;public int MaxCount=10,EconomicBundleSize=1,ItemTags;public bool HasSubItems;public bool IsBlock()=>false;}
public class ItemValue{public ItemClass ItemClass=new ItemClass();public int type,Quality,ModificationCount;public bool HasQuality;public float PercentUsesLeft=1;public bool IsEmpty()=>false;public ItemValue GetModification(int i)=>this;}
public class ItemStack{public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public bool IsEmpty()=>count==0;public ItemStack Clone()=>new ItemStack(itemValue,count);public static ItemStack Empty=new ItemStack(null,0);}
public class Block{public static Block[] list=new Block[1];public float EconomicValue,EconomicSellScale;public int EconomicBundleSize;}
public static class RebirthBackpackSaleQuote{public static int TradableCount(ItemStack s)=>s.count/Math.Max(1,s.itemValue.ItemClass.EconomicBundleSize)*Math.Max(1,s.itemValue.ItemClass.EconomicBundleSize);}
public static class RebirthBackpackSellStashContents{public static ItemStack[] Cells,Written;public static bool TryRead(ItemValue p,out ItemStack[] c){c=Array.ConvertAll(Cells,s=>s.Clone());return true;}public static bool TryWrite(ItemValue p,ItemStack[] c,out ItemValue n){Written=c;n=p;return true;}}
public static class BatchPriceFixture{
static void Check(bool b,string why){if(!b)throw new Exception(why);}
public static string Run(){var p=new EntityPlayer{Bonus=.5f};var t=new TraderData();var v=new ItemValue();
Check(RebirthStashBatchPrice.Price(p,t,v,2)==60,"Selling bonus omitted");
t.TraderInfo.OverrideSellMarkdown=.4f;Check(RebirthStashBatchPrice.Price(p,t,v,2)==80,"Override applied bonus twice");t.TraderInfo.OverrideSellMarkdown=-1;
v.HasQuality=true;v.Quality=6;v.PercentUsesLeft=.5f;Check(RebirthStashBatchPrice.Price(p,t,v,1)==15,"Quality/condition quote wrong");
v.HasQuality=false;v.ItemClass.EconomicBundleSize=3;Check(RebirthStashBatchPrice.Price(p,t,v,6)==60,"Bundle count wrong");
SandboxOptions.SandboxOptionManager.Income=.75f;Check(RebirthStashBatchPrice.Price(p,t,v,3)==23,"Income rounding wrong");SandboxOptions.SandboxOptionManager.Income=1;
v.ItemClass.EconomicBundleSize=1;RebirthBackpackSellStashContents.Cells=new[]{new ItemStack(v,6),new ItemStack(v,6)};t.Existing=3;
Check(RebirthStashBatchPrice.Plan(p,t,new ItemValue(),out _,out var sold,out int total)&&total==210&&sold.Count==2,"Duplicate stacks exceeded shared trader limit");
Check(RebirthBackpackSellStashContents.Written[0].count==0&&RebirthBackpackSellStashContents.Written[1].count==5&&RebirthBackpackSellStashContents.Cells[0].count==6,"Planning mutated source or remainder wrong");
v.ItemClass.EconomicValue=0;Check(!RebirthStashBatchPrice.Plan(p,t,new ItemValue(),out _,out _,out _),"Zero payout emptied stash");
return "PASS: production batch price/planner: bonuses, override, quality, condition, bundles, rounding, shared buy limit, detached plan and zero-price refusal.";}}
"@
$production=[regex]::Replace((Get-Content (Join-Path $PSScriptRoot '../../Scripts/UI/RebirthStashBatchPrice.cs') -Raw),'(?m)^using [^;]+;\r?\n','')
Add-Type -TypeDefinition ($fixture+"`n"+$production)
[BatchPriceFixture]::Run()
