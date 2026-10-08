$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Progression/RebirthLiteratureService.cs')
$start=$source.IndexOf('    public static bool TryFindMatchingInventoryStackPublic(')
if($start -lt 0){throw 'Production lookup missing'}
$methods=$source.Substring($start).TrimEnd()
$methods=$methods.Substring(0,$methods.LastIndexOf('}'))
$methods=$methods.Replace('owner?.latestPlayerData==null','owner==null || owner.latestPlayerData==null').Replace('owner.latestPlayerData.bag?.GetSlots()','owner.latestPlayerData.bag==null?null:owner.latestPlayerData.bag.GetSlots()')
Add-Type -TypeDefinition (@'
using System;
public class ItemValue {public int type;public ushort Seed;}
public class ItemStack {public ItemValue itemValue;public int count=1;public bool IsEmpty(){return count<=0;}}
public class Storage {public Storage ItemGrid {get{return this;}} public ItemStack[] items {get{return Slots;}} public ItemStack[] Slots=new ItemStack[3];public ItemStack[] GetSlots(){return Slots;}}
public class EntityPlayer {public int entityId=7;public Storage bag=new Storage(),inventory=new Storage();}
public class EntityPlayerLocal:EntityPlayer {}
public class PlayerData {public Storage bag=new Storage();public ItemStack[] inventory=new ItemStack[3];}
public static class RebirthPlayerDataInventory {public static ItemStack[] ReadSlots(PlayerData d,bool bag){return d==null?null:(bag?d.bag.Slots:d.inventory);}}
public class ClientInfo {public PlayerData latestPlayerData=new PlayerData();}
public class Clients {public ClientInfo Owner;public ClientInfo ForEntityId(int id){return id==7?Owner:null;}}
public class ConnectionManager {public bool IsServer=true;public Clients Clients=new Clients();}
public class SingletonMonoBehaviour<T> {public static T Instance;}
public static class RebirthToolbeltCapacity {public static int GetOwnedSlotCount(EntityPlayer p,int n){return Math.Max(0,Math.Min(2,n));}}
public static class LiteratureInventoryChecks {
'@ + $methods + @'
 public static void Run(){
  var c=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=c;
  var p=new EntityPlayer();var book=new ItemStack{itemValue=new ItemValue{type=8,Seed=9}};p.bag.Slots[0]=book;
  ItemStack result;
  if(TryFindMatchingInventoryStackPublic(p,8,9,out result))throw new Exception("missing owner fell back to entity bag");
  c.Clients.Owner=new ClientInfo();
  if(TryFindMatchingInventoryStackPublic(p,8,9,out result))throw new Exception("stale entity bag accepted");
  c.Clients.Owner.latestPlayerData.bag.Slots[0]=book;
  if(!TryFindMatchingInventoryStackPublic(p,8,9,out result)||!Object.ReferenceEquals(result,book))throw new Exception("snapshot bag rejected");
  if(TryFindMatchingInventoryStackPublic(p,8,0,out result))throw new Exception("seed zero treated as wildcard");
  c.Clients.Owner.latestPlayerData.bag.Slots[0]=null;c.Clients.Owner.latestPlayerData.inventory[2]=book;
  if(TryFindMatchingInventoryStackPublic(p,8,9,out result))throw new Exception("locked slot accepted");
  c.Clients.Owner.latestPlayerData.inventory[1]=book;
  if(!TryFindMatchingInventoryStackPublic(p,8,9,out result))throw new Exception("owned belt slot rejected");
  c.Clients.Owner.latestPlayerData=null;
  if(TryFindMatchingInventoryStackPublic(p,8,9,out result))throw new Exception("missing data accepted");
  var local=new EntityPlayerLocal();local.bag.Slots[0]=book;
  if(!TryFindMatchingInventoryStackPublic(local,8,9,out result))throw new Exception("host local bag rejected");
  if(TryFindMatchingInventoryStackPublic(null,8,9,out result))throw new Exception("missing player accepted");
 }
}
'@)
[LiteratureInventoryChecks]::Run()
Write-Output 'PASS: production literature lookup uses remote snapshot, rejects stale/missing sources and wrong seed, bounds toolbelt, preserves local ownership. Native transport stubbed.'
