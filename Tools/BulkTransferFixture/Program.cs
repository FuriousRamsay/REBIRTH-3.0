using System;
using System.Reflection;
class Program {
 static MethodInfo Method(string type,string name) => typeof(RebirthBulkTransferRefresh).GetNestedType(type,BindingFlags.NonPublic).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static);
 static void Begin()=>Method("Transfer","Prefix").Invoke(null,null);
 static Exception End(Exception e)=>(Exception)Method("Transfer","Finalizer").Invoke(null,new object[]{e});
 public static bool Bag(XUiC_Backpack b)=>(bool)Method("BackpackRefresh","Prefix").Invoke(null,new object[]{b});
 public static bool Container(XUiC_LootContainer c,ITileEntity t)=>(bool)Method("ContainerRefresh","Prefix").Invoke(null,new object[]{c,t});
 static int checks; static void Check(bool b,string n){if(!b)throw new Exception(n);checks++;}
 static void Main(){
 var a=new XUiC_Backpack();var b=new XUiC_Backpack();var c=new XUiC_LootContainer();var t=new Tile();var latest=new Tile();
 Check(Bag(a),"outside native");Begin();for(int i=0;i<90;i++){Check(!Bag(a),"defer bag");Check(!Container(c,t),"defer container");}
 Begin();Check(!Bag(b),"nested bag");Container(c,latest);Check(End(null)==null&&a.Calls==0&&c.Calls==0,"nested no flush");
 Check(End(null)==null&&a.Calls==1&&b.Calls==1&&c.Calls==1&&ReferenceEquals(c.Last,latest),"one flush latest tile");
 a.Fail=true;var original=new Exception("native transaction failed");Begin();Bag(a);Bag(b);Container(c,t);
 Check(ReferenceEquals(End(original),original),"preserve original exception");Check(b.Calls==2&&c.Calls==2,"flush later views after failure");
 Begin();Bag(a);Container(c,latest);Check(End(null)?.Message=="refresh failed","surface presentation exception");Check(c.Calls==3,"container flush despite bag failure");
 a.Fail=false;Begin();Check(End(null)==null&&a.Calls==3&&c.Calls==3,"queues cleared after failure");
 Begin();Bag(a);Check(End(null)==null&&a.Calls==4,"next transfer healthy");Check(Bag(a)&&Container(c,t),"depth restored");
 Console.WriteLine($"PASS {checks} assertions: production batching, nested scope, repeated callbacks, latest tile, failures and recovery. Native/Harmony collaborators doubled; no runtime or timing claim.");
 }
}
public interface ITileEntity{} class Tile:ITileEntity{}
public class XUiM_LootContainer{public void StashItems(){}}
public class XUiC_Backpack{public int Calls;public bool Fail;public void RefreshBackpackSlots(){if(!Program.Bag(this))return;Calls++;if(Fail)throw new Exception("refresh failed");}}
public class XUiC_LootContainer{public int Calls;public ITileEntity Last;public void OnTileEntityChanged(ITileEntity t){if(!Program.Container(this,t))return;Calls++;Last=t;}}
public static class Log{public static void Warning(string s){}}
public static class RebirthHarmonyBootstrap{public static void PatchClassOnce(HarmonyLib.Harmony h,Type t){}}
namespace HarmonyLib{public class Harmony{} [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string s){}}}
