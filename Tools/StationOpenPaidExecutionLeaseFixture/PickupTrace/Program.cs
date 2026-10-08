using System;
sealed class ItemStack{public int count; public static ItemStack Empty=>new ItemStack();}
sealed class Inventory{public int Capacity,Received;public Action AfterAdd;public bool AddItem(ItemStack s){int n=Math.Min(Capacity,s.count);Received+=n;Capacity-=n;s.count-=n;AfterAdd?.Invoke();return s.count==0;}}
sealed class Ui{public Inventory PlayerInventory=new Inventory();}
partial class TraceSlot{public Ui xui=new Ui();public ItemStack ItemStack=new ItemStack();public int Changes;public void PlayPlaceSound(){}public void HandleSlotChangeEvent(){Changes++;}}
class Program{static int n;static void A(bool b,string m){if(!b)throw new Exception(m);n++;}static void Main(){
 var full=new TraceSlot{ItemStack=new ItemStack{count=10}};full.xui.PlayerInventory.Capacity=10;int seen=-1;full.xui.PlayerInventory.AfterAdd=()=>seen=full.Changes;full.Pickup();A(full.xui.PlayerInventory.Received==10,"full inventory credit");A(full.ItemStack.count==0&&full.Changes==1,"full slot clear");A(seen==0,"inventory mutation precedes slot callback");
 var partial=new TraceSlot{ItemStack=new ItemStack{count=10}};partial.xui.PlayerInventory.Capacity=4;partial.Pickup();A(partial.xui.PlayerInventory.Received==4&&partial.ItemStack.count==6,"partial transfer exact remainder");A(partial.Changes==1,"partial callback");partial.Pickup();A(partial.xui.PlayerInventory.Received==4&&partial.Changes==1,"zero capacity no false pickup");
 // Real source replay hazard: stale output hydration followed by native pickup credits twice.
 full.ItemStack=new ItemStack{count=10};full.xui.PlayerInventory.Capacity=10;full.Pickup();A(full.xui.PlayerInventory.Received==20,"stale output replay duplicates inventory");
 // Ignoring slot clear while preserving original output duplicates server custody even on partial transfer.
 int originalServerOutput=10;A(originalServerOutput+partial.xui.PlayerInventory.Received==14,"blind preserve after partial duplicates custody");
 Console.WriteLine("PASS "+n+": extracted actual native pickup branch; Inventory.AddItem is explicit capacity/mutation double; hazards demonstrated, no protocol implementation claim.");}}

