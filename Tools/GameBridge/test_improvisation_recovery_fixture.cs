using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
// Disclosed item doubles test detached count/payload custody, not native serialization.
public class ItemValue { public int type; public string payload; }
public class ItemStack {
 public ItemValue itemValue; public int count;
 public bool IsEmpty()=>itemValue.type==0||count==0;
 public ItemStack Clone()=>new ItemStack{count=count,itemValue=itemValue==null?null:new ItemValue{type=itemValue.type,payload=itemValue.payload}};
}
// PRODUCTION_CLASS
public static class Check {
 static void Assert(bool ok,string name){if(!ok)throw new Exception(name);}
 static int Sum(IList<ItemStack> a){int n=0;foreach(var i in a)n+=i.count;return n;}
 public static void Main(){
 var skills=new Dictionary<string,float>{{"building",100},{"electrical",0}};
 var items=new List<ItemStack>{new ItemStack{count=40,itemValue=new ItemValue{type=1,payload="owner/seed/mods"}},new ItemStack{count=1,itemValue=new ItemValue{type=2,payload="single"}}};
 RebirthImprovisationRecovery.Plan b,e,again;
 Assert(RebirthImprovisationRecovery.TryPlan("building",skills,items,555,.1,.75,out b),"building plan");
 Assert(RebirthImprovisationRecovery.TryPlan("electrical",skills,items,555,.1,.75,out e),"electrical plan");
 Assert(b.Proficiency==100&&e.Proficiency==0&&Sum(b.ReturnedSnapshot())>Sum(e.ReturnedSnapshot()),"category isolation");
 Assert(Sum(b.ReturnedSnapshot())+Sum(b.LostSnapshot())==41,"exact conservation");
 var snapshot=b.ReturnedSnapshot(); Assert(snapshot[0].itemValue.payload=="owner/seed/mods","payload retained");
 snapshot[0].count=0;snapshot[0].itemValue.payload="changed";
 Assert(b.ReturnedSnapshot()[0].count>0&&b.ReturnedSnapshot()[0].itemValue.payload=="owner/seed/mods"&&items[0].count==40,"detached custody");
 Assert(RebirthImprovisationRecovery.TryPlan("building",skills,items,555,.1,.75,out again)&&Sum(again.ReturnedSnapshot())==Sum(b.ReturnedSnapshot()),"fixed receipt replay");
 int prior=-1;for(int p=0;p<=100;p++){skills["electrical"]=p;Assert(RebirthImprovisationRecovery.TryPlan("electrical",skills,items,555,.1,.75,out again),"progress plan");int n=Sum(again.ReturnedSnapshot());Assert(n>=prior&&n<=41,"monotonic bounded recovery");prior=n;}
 var insensitive=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase){{"BUILDING",100}};
 Assert(RebirthImprovisationRecovery.TryPlan("building",insensitive,items,555,.1,.75,out again)&&again.Proficiency==0,"exact ID no cross alias");
 Assert(!RebirthImprovisationRecovery.TryPlan("food",skills,items,555,.8,.2,out again),"invalid range");
 Assert(!RebirthImprovisationRecovery.TryPlan("building",skills,items,555,0,1,out again),"full recovery cap rejected");
 skills["building"]=float.NaN;Assert(!RebirthImprovisationRecovery.TryPlan("building",skills,items,555,.1,.75,out again),"nonfinite proficiency");
 items[0].count=65536;Assert(!RebirthImprovisationRecovery.TryPlan("electrical",skills,items,555,.1,.75,out again),"native count bound");
 Console.WriteLine("PASS category isolation, monotonic recovery, exact custody, detached snapshots, stable receipt and invalid inputs");
 }
}
