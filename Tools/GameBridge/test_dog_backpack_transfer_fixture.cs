using System;using System.Collections.Generic;
struct Vector3{public static Vector3 zero;}
struct RebirthNpcStableId{}
class Bag{public int Capacity,Added;}
class EntityPlayer{public Bag bag=new Bag();public Vector3 position;public int entityId=3;}
class ItemStack{public int count;public string Metadata="custom";public ItemStack Clone(){return new ItemStack{count=count,Metadata=Metadata};}}
static class LogisticsTransferService{public static void MoveIntoBag(EntityPlayer p,ItemStack s){if(s.Metadata!="custom")throw new Exception("metadata");int n=Math.Min(s.count,p.bag.Capacity);p.bag.Added+=n;s.count-=n;}}
class GameManager{public static GameManager Instance=new GameManager();public int Dropped;public void ItemDropServer(ItemStack s,Vector3 p,Vector3 v,int owner,float life,bool flag){if(s.Metadata!="custom"||owner!=3)throw new Exception("drop identity");Dropped+=s.count;}}
class Subject{public static int Stored,RestoreCapacity;static bool TryRemove(RebirthNpcStableId id,int slot,int count,bool locked,out ItemStack removed,int expected){if(!locked||expected!=7)throw new Exception("lock/type guard");int n=Math.Min(count,Stored);removed=new ItemStack{count=n};Stored-=n;return n>0;}static bool TryAdd(RebirthNpcStableId id,ItemStack s,out int added){if(s.Metadata!="custom")throw new Exception("restore metadata");added=Math.Min(s.count,RestoreCapacity);Stored+=added;return added>0;}
// PRODUCTION_METHOD
}
class Check{static void Main(){foreach(int room in new[]{0,2,5})foreach(int restore in new[]{0,5}){Subject.Stored=5;Subject.RestoreCapacity=restore;GameManager.Instance.Dropped=0;var p=new EntityPlayer();p.bag.Capacity=room;bool moved=Subject.TransferToPlayer(p,new RebirthNpcStableId(),0,5,7);if(p.bag.Added+Subject.Stored+GameManager.Instance.Dropped!=5||p.bag.Added!=room||moved!=(room>0))throw new Exception("conservation");}Console.WriteLine("PASS: full/partial/full-backpack transfers conserve items and metadata, restore only remainder, drop only unrestorable remainder");}}
