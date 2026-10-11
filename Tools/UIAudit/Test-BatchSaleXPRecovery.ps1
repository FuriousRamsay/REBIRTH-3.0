$ErrorActionPreference='Stop'
$owner=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthBackpackLibraryOwnerTransfer.cs') -Raw
$start=$owner.IndexOf('    private static RebirthBackpackLibraryOwnerResult ApplyBatch(')
$end=$owner.IndexOf('    public static string ReceiptKey', $start)
$body=$owner.Substring($start,$end-$start).Replace('private static','public static')
$fixture=@"
using System;using System.Collections.Generic;
public enum RebirthBackpackLibraryOwnerResult{Pending,Applied,Rejected,Indeterminate}
public class ItemStack{public int count;public ItemStack(int n){count=n;}public ItemStack Clone()=>new ItemStack(count);}
public class Buffs{public Dictionary<string,float> Data=new Dictionary<string,float>();public float GetCustomVar(string k)=>Data.TryGetValue(k,out var v)?v:0;public void SetCustomVar(string k,float v,bool sync){Data[k]=v;}}
public class Locks{public int Length=>0;public bool this[int i]=>false;}
public class Bag{public ItemStack[] Slots={new ItemStack(0),new ItemStack(0)};public Locks LockedSlots;public bool Throw;public int Setters;public void SetSlot(int i,ItemStack s){Slots[i]=s;Setters++;if(Throw){Throw=false;throw new Exception("Setter listener");}}}
public class Progression{public enum XPTypes{Selling=4}public int Calls,XP;public bool Throw;public void AddLevelExp(int n,string key,XPTypes type,bool a,bool b,int c,object d){XP+=n;Calls++;if(Throw){Throw=false;throw new Exception("XP listener");}}}
public class EntityPlayerLocal{public Buffs Buffs=new Buffs();public Bag bag=new Bag();public Progression Progression=new Progression();}
public class RebirthBackpackLibraryReceipt{public int Quantity=30;public class WalletChange{public int Slot;public ItemStack Before,After;}public bool TryGetWallet(out List<WalletChange> c){c=new List<WalletChange>{new WalletChange{Slot=0,Before=new ItemStack(0),After=new ItemStack(10)},new WalletChange{Slot=1,Before=new ItemStack(0),After=new ItemStack(20)}};return true;}}
public static class OwnerFixture{
static bool Same(ItemStack a,ItemStack b)=>a.count==b.count;
$body
public static string Run(){var p=new EntityPlayerLocal();var r=new RebirthBackpackLibraryReceipt();p.bag.Throw=true;if(ApplyBatch(p,r,"r",p.bag.Slots)!=RebirthBackpackLibraryOwnerResult.Indeterminate||p.Progression.Calls!=0)throw new Exception("Partial wallet prematurely awards XP");p.Progression.Throw=true;if(ApplyBatch(p,r,"r",p.bag.Slots)!=RebirthBackpackLibraryOwnerResult.Indeterminate||p.Progression.XP!=30)throw new Exception("Recovery payout/XP wrong");if(ApplyBatch(p,r,"r",p.bag.Slots)!=RebirthBackpackLibraryOwnerResult.Applied||p.Progression.Calls!=1||p.bag.Setters!=2||p.bag.Slots[0].count!=10||p.bag.Slots[1].count!=20)throw new Exception("Recovery repeats payout or XP");return "PASS: actual batch owner method recovers partial currency setters and throwing XP listeners without repeating either payout or selling XP.";}}
"@
Add-Type -TypeDefinition $fixture
[OwnerFixture]::Run()
