using System;using System.Collections.Generic;
struct Vector3 {public static Vector3 zero;}
class EntityPlayerLocal {public Vector3 position;public int entityId=7;}
class ItemStack {public int count;public bool IsEmpty(){return count<=0;}}
class Inventory {public int Capacity=2,Added,Calls;public bool AddItem(ItemStack s,bool notify){Calls++;int n=Math.Min(Capacity,s.count);Capacity-=n;Added+=n;s.count-=n;return s.count==0;}}
class XUi {public Inventory PlayerInventory=new Inventory();}
class GameManager {public static GameManager Instance=new GameManager();public int Dropped,Calls;public void ItemDropServer(ItemStack s,Vector3 p,Vector3 v,int id,float t,bool b){if(id!=7||s.count<=0)throw new Exception("invalid drop");Dropped+=s.count;Calls++;}}
class Subject {public XUi xui=new XUi();
// RETURN
public void Run(EntityPlayerLocal p,IList<ItemStack> items){ReturnPulledIngredients(p,items);}}
class Check {static void Main(){var s=new Subject();var p=new EntityPlayerLocal();s.Run(p,new[]{null,new ItemStack {count=0},new ItemStack {count=5}});if(s.xui.PlayerInventory.Added!=2||GameManager.Instance.Dropped!=3||s.xui.PlayerInventory.Calls!=1)throw new Exception("partial return");s.xui.PlayerInventory.Capacity=5;s.Run(p,new[]{new ItemStack {count=4}});if(s.xui.PlayerInventory.Added!=6||GameManager.Instance.Calls!=1)throw new Exception("full return");s.Run(null,new[]{new ItemStack {count=1}});s.Run(p,null);Console.WriteLine("PASS returned quantity plus overflow equals receipt; no empty drop; invalid inputs ignored");}}
