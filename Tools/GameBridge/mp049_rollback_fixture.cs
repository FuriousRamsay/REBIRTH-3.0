// AUTHORED_NOT_RUN. Actual group rollback method; typed domain closures doubled.
using System;
using System.Collections.Generic;
class RebirthNpcSaveQuarantineDomain {
 internal Action Restore;internal Func<bool> Check;internal void Rollback()=>Restore();internal bool IsCurrent=>Check();
}
class Actual {
 // ACTUAL_ROLLBACK
 internal static void Run(){
  var order=new List<int>();bool first=false,second=false;
  var a=new RebirthNpcSaveQuarantineDomain{Restore=()=>{order.Add(1);first=true;},Check=()=>first&&second};
  var b=new RebirthNpcSaveQuarantineDomain{Restore=()=>{order.Add(2);second=true;},Check=()=>first&&second};
  Require(RestoreOriginalDomains(new[]{a,b}),"complete restoration refused");
  Require(order.Count==2&&order[0]==2&&order[1]==1,"reverse restoration order changed");
  first=false;second=false;order.Clear();
  b.Restore=()=>{order.Add(2);};
  Require(!RestoreOriginalDomains(new[]{a,b}),"silent non-restoration accepted");
  Require(first&&order.Count==2,"other domain restoration skipped");
  b.Restore=()=>{order.Add(2);throw new Exception();};order.Clear();
  Require(!RestoreOriginalDomains(new[]{a,b})&&order.Count==2,"throw skipped remaining original");
  b.Restore=()=>second=true;b.Check=()=>throw new Exception();
  Require(!RestoreOriginalDomains(new[]{a,b}),"unknown original current check accepted");
  Require(!RestoreOriginalDomains(null),"unknown membership accepted");
  Require(!RestoreOriginalDomains(new[]{a,null}),"missing member accepted");
  Require(RestoreOriginalDomains(new RebirthNpcSaveQuarantineDomain[0]),"positive empty group refused");
  Console.WriteLine("PASS actual rollback group with explicit typed-domain doubles; not native save isolation");
 }
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
class Program {static void Main()=>Actual.Run();}
