using System;
public class EntityPlayer {}
public class ItemValue {}
public class ItemStack { public ItemValue itemValue=new ItemValue(); }
public class ItemInventoryData { public ItemStack itemStack; public object holdingEntity; }
public class ItemActionData { public ItemInventoryData invData; }
public class ItemActionEat {
 public class MyInventoryData : ItemActionData { public bool bEatingStarted; }
 public virtual void OnHoldingUpdate(ItemActionData d){throw new Exception("Invalid native callback");}
}
public class Check : ItemActionEat {
 static int calls;
 static void Dispatch(EntityPlayer p,ItemValue v){calls++;}
// METHODS
 public static void Main(){
 var c=new Check();c.OnHoldingUpdate(null);c.OnHoldingUpdate(new ItemActionData());
 c.OnHoldingUpdate(new ItemActionData{invData=new ItemInventoryData()});
 var d=new MyInventoryData{bEatingStarted=true,invData=new ItemInventoryData{itemStack=new ItemStack(),holdingEntity=new EntityPlayer()}};
 c.OnHoldingUpdate(d);c.OnHoldingUpdate(d);
 if(calls!=1 || d.bEatingStarted)throw new Exception("Dispatch must occur once");
 Console.WriteLine("PASS actual audiobook holding callback: invalid inputs never reach native eating; valid use dispatches once");
 }
}
