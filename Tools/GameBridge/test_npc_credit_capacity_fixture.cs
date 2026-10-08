using System;
public class XUiC_ItemStack {public enum StackLocationTypes {Backpack,ToolBelt}}
public class ItemValue {public int type=1,texture;public bool bag=true,belt=true;public bool IsEmpty(){return type==0;}}
public class ItemClass {public int MaxCount=100;public static ItemClass current=new ItemClass();public static ItemClass GetForId(int id){return current;}}
public class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public bool IsEmpty(){return count==0;}public bool CanMoveTo(XUiC_ItemStack.StackLocationTypes x){return x==XUiC_ItemStack.StackLocationTypes.Backpack?itemValue.bag:itemValue.belt;}public bool CanStackPartlyWith(ItemStack other,out int n){n=Math.Min(ItemClass.current.MaxCount-count,other.count);return n>0&&itemValue.type==other.itemValue.type&&itemValue.texture==other.itemValue.texture;}}
public class Store {public ItemStack[] slots;public ItemStack[] GetSlots(){return slots;}}
public class EntityPlayer {public Store bag=new Store(),inventory=new Store();}
public static class Service {
// METHODS
}
public static class Checks {static void Check(bool v,string m){if(!v)throw new Exception(m);}public static void Main(){
 var v=new ItemValue();var p=new EntityPlayer();p.bag.slots=new[]{new ItemStack(v,90),new ItemStack(v,80)};p.inventory.slots=new[]{new ItemStack(v,70),new ItemStack(v,60),new ItemStack(v,0)};
 Check(Service.Capacity(p,v)==40,"best slot, not aggregate/internal slot");
 v.belt=false;Check(Service.Capacity(p,v)==20,"belt destination restriction");v.bag=false;Check(Service.Capacity(p,v)==0,"both forbidden");v.bag=true;v.belt=true;
 p.bag.slots=new[]{new ItemStack(new ItemValue{texture=2},1)};p.inventory.slots=new[]{new ItemStack(v,100),new ItemStack(v,0)};Check(Service.Capacity(p,v)==0,"texture mismatch and internal slot");
 p.bag.slots=new[]{new ItemStack(v,0)};ItemClass.current.MaxCount=75;Check(Service.Capacity(p,v)==75,"runtime max");
 Check(Service.Capacity(new ItemStack[0],v,true)==0,"empty belt");Check(Service.Capacity((ItemStack[])null,v,false)==0,"null slots");
 Console.WriteLine("PASS: production NPC credit capacity best-slot, destination restrictions, internal slot, texture compatibility and runtime max; native types stubbed");
}}
