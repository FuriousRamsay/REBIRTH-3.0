using System;
enum RebirthTransactionState {Prepared,Committed,RolledBack,Failed,Indeterminate}
interface IRebirthNpcExternalInventoryReservation {}
interface IRebirthNpcExternalInventoryReservationOutcome {}
class ItemValue {public int value;public override bool Equals(object o){return o is ItemValue && ((ItemValue)o).value==value;}public override int GetHashCode(){return value;}}
class ItemStack {public int count=4;public ItemValue itemValue=new ItemValue();public ItemStack Clone(){return new ItemStack {count=count,itemValue=new ItemValue {value=itemValue.value}};}}
class Bag {public ItemStack[] Slots=new[]{new ItemStack()};public int Revision;public ItemStack[] GetSlots(){return Slots;}}
class Player {public Bag bag=new Bag();}
static class LogisticsTransferService {public static bool Allowed=true;public static bool CanMutateResource(Player p,Bag b,bool d){return Allowed;}}
class Subject {
// PRODUCTION_RESERVATION
public Player player=new Player();public Bag source;public string EndpointId="test";public int Mode,Calls;public Subject(){source=player.bag;}
public bool Remove(string k,int q,out string e){Calls++;e="failed";if(Mode==2||Mode==3)source.Slots[0].count--;if(Mode==3)throw new Exception("callback");return Mode==1;}
public bool Add(string k,int q,out string e){return Remove(k,q,out e);}
public static void Check(){for(int mode=0;mode<4;mode++){var o=new Subject {Mode=mode};var r=new Reservation(o,Guid.NewGuid(),"x",1,true/* REVISION */);string e;bool ok=r.Commit(out e);Assert(ok==(mode==1),"first result");Assert(r.State==(mode==1?RebirthTransactionState.Committed:mode==0?RebirthTransactionState.Failed:RebirthTransactionState.Indeterminate),"state");Assert(r.Commit(out e)==ok&&o.Calls==1,"retry");Assert(r.Rollback(out e)==(mode==0),"rollback");}
var a=new Subject();var b=new Reservation(a,Guid.NewGuid(),"x",1,false/* REVISION */);string err;b.Dispose();Assert(!b.Commit(out err)&&a.Calls==0,"disposed");}
static void Assert(bool b,string m){if(!b)throw new Exception(m);}}
class Check {static void Main(){Subject.Check();Console.WriteLine("PASS reservation failure, partial write, exception, retry, rollback, dispose");}}
