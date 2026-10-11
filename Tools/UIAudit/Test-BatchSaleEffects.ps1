$ErrorActionPreference='Stop'
$fixture=@"
using System;using System.Collections.Generic;
public class ItemValue{public int type;}
public class ItemStack{public ItemValue itemValue;public int count;public ItemStack(int type,int n){itemValue=new ItemValue{type=type};count=n;}public ItemStack Clone()=>new ItemStack(itemValue.type,count);public bool IsEmpty()=>count==0;}
public class TraderData{public List<ItemStack> Stock=new List<ItemStack>();public void AddToPrimaryInventory(ItemStack s,bool sync){if(!sync)throw new Exception("No stock sync");Stock.Add(s.Clone());}}
public class EntityTrader{public TraderData TraderData=new TraderData();}
public class World{public EntityTrader Trader=new EntityTrader();public object GetEntity(int id)=>id==7?Trader:null;}
public class EntityPlayer{public World world=new World();}
public class EntityPlayerLocal:EntityPlayer{public PlayerUI PlayerUI=new PlayerUI();}
public class PlayerUI{public XUi xui=new XUi();}public class XUi{public TraderUI Trader=new TraderUI();}public class TraderUI{public Group TraderWindowGroup=new Group();}public class Group{public int Refresh;public void RefreshTraderItems(){Refresh++;}}
public class RebirthBackpackLibraryReceipt{public bool IsBatchSale=true;public int BatchTraderId=7;public ItemValue Before=new ItemValue{type=1},After=new ItemValue{type=2};public bool TryGetImages(out ItemValue a,out ItemValue b,out ItemStack c,out ItemStack d){a=Before;b=After;c=d=null;return true;}}
public static class RebirthBackpackSellStashContents{public static ItemStack[] Before={new ItemStack(1,4),new ItemStack(2,8)},After={new ItemStack(1,1),new ItemStack(2,2)};public static bool TryRead(ItemValue v,out ItemStack[] s){s=v.type==1?Before:After;return true;}}
public static class RebirthStashBatchPrice{public static int Price(EntityPlayer p,TraderData t,ItemValue v,int n)=>v.type*n*10;}
public static class RebirthSkillWaveAService{public static ItemStack Trained;public static int Value,Calls;public static void ReportCommittedStashSale(EntityPlayer p,ItemStack s,int value){Trained=s;Value=value;Calls++;}}
public static class EffectsFixture{public static string Run(){var p=new EntityPlayerLocal();var r=new RebirthBackpackLibraryReceipt();RebirthBackpackBatchSaleEffects.Apply(p,r);var stock=p.world.Trader.TraderData.Stock;if(stock.Count!=2||stock[0].count!=3||stock[1].count!=6)throw new Exception("Wrong trader stock delta");if(RebirthSkillWaveAService.Calls!=1||RebirthSkillWaveAService.Trained.itemValue.type!=2||RebirthSkillWaveAService.Value!=120)throw new Exception("Batch bypasses award interval or wrong training line");if(p.PlayerUI.xui.Trader.TraderWindowGroup.Refresh!=1)throw new Exception("Stale trader view");if(RebirthBackpackSellStashContents.Before[0].count!=4)throw new Exception("Mutated receipt images");r.IsBatchSale=false;RebirthBackpackBatchSaleEffects.Apply(p,r);if(stock.Count!=2)throw new Exception("Library transfer alters trader");return "PASS: production batch effects preserve exact sold counts, sync stock, single highest-value training line, refresh view and immutable source images; ordinary library transfers untouched.";}}
"@
$production=[regex]::Replace((Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthBackpackBatchSaleEffects.cs') -Raw),'(?m)^using [^;]+;\r?\n','')
Add-Type -TypeDefinition ($fixture+"`n"+$production)
[EffectsFixture]::Run()
$server=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthBackpackLibraryServer.cs') -Raw
if($server -notmatch 'if\(firstCommit&&receipt.IsBatchSale\)RebirthBackpackBatchSaleEffects.Apply'){throw 'Effects can repeat on settlement retries'}
$owner=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthBackpackLibraryOwnerTransfer.cs') -Raw
if($owner -notmatch 'GetCustomVar\(xpKey\)==0f' -or $owner.IndexOf('SetCustomVar(xpKey,1f,true)') -gt $owner.IndexOf('Progression.AddLevelExp')){throw 'Selling XP has no retry guard'}
$skill=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Progression/RebirthSkillWaveAService.cs') -Raw
if($skill -notmatch '!committedStash && !ConsumeAuthoritativeTradeWitness' -or $skill -notmatch 'TryValidateTradingValue\(itemType,count,value,out trustedValue\)'){throw 'Native witness or neutral valuation guard removed'}
