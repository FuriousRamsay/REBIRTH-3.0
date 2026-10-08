using System;
using System.Collections.Generic;
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
public struct Vector3i { public int x; public Vector3i(int n){x=n;} }
public struct BlockValue { public int type; }
public class Block { public string IndexName="treeStump"; public string GetBlockName()=>IndexName; }
public class Entity { public int entityId; }
public class EntityVehicle:Entity {}
public class EntityPlayer:Entity { public Inventory inventory=new(); public Inventory bag=new(); public Entity AttachedToEntity=new EntityVehicle(); }
public class Inventory {
 public ItemValue holdingItemItemValue=new(){type=2}; public bool Stack,ThrowProbe,ThrowAdd,Partial,Accept; public int Calls,Received; public Action BeforeAdd;
 public bool CanStackNoEmpty(ItemStack stack){if(ThrowProbe)throw new Exception();return Stack;}
 public bool AddItem(ItemStack stack){BeforeAdd?.Invoke();Calls++;if(ThrowAdd)throw new Exception();if(Partial){Received++;stack.count--;return false;}if(Accept){Received+=stack.count;stack.count=0;return true;}return false;}
}
public class ItemValue { public int type; public ushort Seed,Quality; public ItemClass ItemClass; public bool IsEmpty()=>type<=0; }
public class ItemClass { public static int Mode; public static ItemValue GetItem(string n,bool b){if(Mode==1)throw new Exception();if(Mode==2)return null;if(Mode==3)return new ItemValue();return new ItemValue{type=3,ItemClass=new ItemClass()};} }
public class ItemStack { public ItemValue itemValue; public int count; public ItemStack(ItemValue v,int n){itemValue=v;count=n;} }
public class WorldBase { public bool Remote; public Dictionary<int,Entity> Entities=new(); public bool IsRemote()=>Remote; public Entity GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null; }
public class GameManager { public static GameManager Instance=new(); public WorldBase World; }
public static class GameIO { public static string Save="A"; public static string GetSaveGameDir()=>Save; }
class Program {
 static Vector3i pos=new(1); static EntityPlayer player; static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static void Setup(){GameIO.Save="A";GameManager.Instance.World=new();player=new(){entityId=7};GameManager.Instance.World.Entities[7]=player;UnityEngine.Time.realtimeSinceStartup=10;RebirthStumpHarvestContextStore.Clear();RebirthStumpHarvestContextPolicy.EnableManualTest(true);RebirthStumpHarvestRewardPolicy.EnableManualTest("resourceHoney",2,true,true,true);ItemClass.Mode=0;Store();}
 static void Store(){Check(RebirthStumpHarvestContextStore.TryStoreFromBlockDestroyed(new Block(),GameManager.Instance.World,pos,new BlockValue{type=3},7),"capture authoritative stump");}
 static void Main(){
 Setup();Check(RebirthStumpHarvestContextStore.TryReserve(pos,out var c),"reserve");UnityEngine.Time.realtimeSinceStartup=20;Check(RebirthStumpHarvestContextStore.TryRestoreReservation(pos,c),"same-world clean retry");Check(RebirthStumpHarvestContextStore.TryGet(pos,out c)&&c.StoredAtRealtime==10,"retry preserves event age");UnityEngine.Time.realtimeSinceStartup=26;Check(RebirthStumpHarvestContextStore.Count==0,"original event expires");Check(!RebirthStumpHarvestContextStore.TryRestoreReservation(pos,c),"expired reservation refused");
 Setup();RebirthStumpHarvestContextStore.TryReserve(pos,out c);GameManager.Instance.World=new();Check(!RebirthStumpHarvestContextStore.TryRestoreReservation(pos,c),"different world refuses restore");
 Setup();RebirthStumpHarvestContextStore.TryReserve(pos,out c);GameIO.Save="B";Check(!RebirthStumpHarvestContextStore.TryRestoreReservation(pos,c),"different save refuses restore");
 Setup();RebirthStumpHarvestContextStore.TryReserve(pos,out c);UnityEngine.Time.realtimeSinceStartup=5;Check(!RebirthStumpHarvestContextStore.TryRestoreReservation(pos,c),"clock reset refuses restore");
 Setup();RebirthStumpHarvestContextStore.TryReserve(pos,out c);Store();Check(!RebirthStumpHarvestContextStore.TryRestoreReservation(pos,c),"new event prevents stale overwrite");
 for(int mode=1;mode<=3;mode++){Setup();ItemClass.Mode=mode;Check(!RebirthStumpHarvestRewardService.TryGrant(pos,out var report)&&report.Contains("InvalidReward"),"bad item mapping "+mode);Check(RebirthStumpHarvestContextStore.Count==1&&player.inventory.Calls==0&&player.bag.Calls==0,"bad mapping preserves custody "+mode);}
 Setup();player.inventory.ThrowProbe=true;Check(!RebirthStumpHarvestRewardService.TryGrant(pos,out var before)&&before.Contains("InvalidReward")&&RebirthStumpHarvestContextStore.Count==1,"clean probe exception restores");
 Setup();Check(!RebirthStumpHarvestRewardService.TryGrant(pos,out var full)&&full.Contains("InventoryFull")&&RebirthStumpHarvestContextStore.Count==1,"full inventory clean retry");
 Setup();player.bag.BeforeAdd=()=>UnityEngine.Time.realtimeSinceStartup=26;Check(!RebirthStumpHarvestRewardService.TryGrant(pos,out var aged)&&aged.Contains("InventoryFull")&&aged.Contains("contextConsumed: True")&&aged.Contains("retry evidence was not restored")&&RebirthStumpHarvestContextStore.Count==0,"expired clean failure reports consumed evidence");
 Setup();player.bag.Partial=true;Check(!RebirthStumpHarvestRewardService.TryGrant(pos,out var partial)&&partial.Contains("IndeterminateAfterCommit")&&RebirthStumpHarvestContextStore.Count==0&&player.bag.Received==1,"partial grant cannot replay");
 Setup();player.bag.ThrowAdd=true;Check(!RebirthStumpHarvestRewardService.TryGrant(pos,out var uncertain)&&uncertain.Contains("IndeterminateAfterCommit")&&RebirthStumpHarvestContextStore.Count==0,"mutation exception cannot replay");
 Setup();player.bag.Accept=true;Check(RebirthStumpHarvestRewardService.TryGrant(pos,out _)&&player.bag.Received==2&&RebirthStumpHarvestContextStore.Count==0,"successful grant consumes once");Check(!RebirthStumpHarvestRewardService.TryGrant(pos,out _)&&player.bag.Received==2,"duplicate request gives nothing");
 Setup();GameManager.Instance.World.Remote=true;Check(!RebirthStumpHarvestContextStore.TryStoreFromBlockDestroyed(new Block(),GameManager.Instance.World,new Vector3i(2),new BlockValue(),7),"remote capture refused");
 Console.WriteLine("PASS all "+checks+" actual-source checks; native adapters doubled, no gameplay claim");
 }
}
