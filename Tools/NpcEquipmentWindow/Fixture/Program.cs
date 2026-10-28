using System;using System.Linq;using System.Xml.Linq;
class Program {
 static int passed;static void Check(bool condition,string name){if(!condition)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
 static EntityRebirthHumanoidNPC Actor(){RebirthNpcInventoryTransactionService.Reset();var a=new EntityRebirthHumanoidNPC{world=GameManager.Instance.World};a.world.Actor=a;return a;}
 static void Main(){
  var a=Actor();a.inventory.ItemGrid.items[0]=new ItemStack("rifle",1,"quality5.modsScope.ammo7.wear13");RebirthNpcInventoryTransactionService.Quantity("rifle",1);
  var x=new XElement("humanEquipment");Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x),"adopt actual native loadout");
  Check(RebirthNpcInventoryTransactionService.Count("rifle")==1,"existing semantic quantity is not doubled during adoption");
  string id=(string)x.Element("bindings").Element("binding").Attribute("id");uint revision=RebirthNpcInventoryTransactionService.Revision;
  Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x)&&RebirthNpcInventoryTransactionService.Revision==revision,"unchanged observation does not advance inventory revision");
  a.inventory.ItemGrid.items[0].itemValue.Image="quality5.modsScope.ammo6.wear14";
  Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x),"capture native firing and wear metadata");
  Check((string)RebirthNpcInventoryTransactionService.Native().Element("nativeStack").Attribute("id")==Guid.Parse(id).ToString(),"weapon identity survives metadata changes");
  Check(RebirthNpcInventoryTransactionService.Count("rifle")==1,"firing does not manufacture a second weapon");
  x.Add(new XElement("swap",new XAttribute("oldStack",id),new XAttribute("done",0),new XElement("after","rifle|1|quality5.modsScope.ammo6.wear14")));a.inventory.ItemGrid.items[0]=null;
  Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x)&&RebirthNpcInventoryTransactionService.Count("rifle")==1,"prepared displaced weapon stays in retained escrow");
  string fresh=Guid.NewGuid().ToString("N");x.Element("bindings").Element("binding").SetAttributeValue("id",fresh);x.Element("swap").SetAttributeValue("done",1);a.inventory.ItemGrid.items[0]=new ItemStack("club",1,"quality4.modsGrip.wear2");
  Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x),"commit incoming physical weapon after owner exchange");
  Check(RebirthNpcInventoryTransactionService.Count("rifle")==0&&RebirthNpcInventoryTransactionService.Count("club")==1,"outgoing custody debited and incoming custody credited once");
  Check(RebirthNpcInventoryTransactionService.Native().Elements().Count()==1&&(string)RebirthNpcInventoryTransactionService.Native().Element("nativeStack").Attribute("payload")=="quality4.modsGrip.wear2","full item payload retained");
  revision=RebirthNpcInventoryTransactionService.Revision;Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x)&&RebirthNpcInventoryTransactionService.Revision==revision,"replayed completed observation is idempotent");
  a.inventory.ItemGrid.items[2]=new ItemStack("ammo",120,"ball");Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x),"observe a native ammunition slot");a.inventory.ItemGrid.items[2].count=115;
  Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x)&&RebirthNpcInventoryTransactionService.Count("ammo")==115,"native reload debit updates custody quantities");
  a.bag.ItemGrid.items[0]=new ItemStack("food",3,"quality0.meta");Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x),"native cargo uses same physical custody");
  a.equipment.ItemGrid.items[12]=new ItemStack("backpack",1,"quality3.modsDye");Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x),"backpack full native payload observed");
  RebirthNpcInventoryTransactionService.Reserve("ammo",1);Check(!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x),"physical custody cannot consume a separately reserved quantity");RebirthNpcInventoryTransactionService.Reserve("ammo",0);
  a.world.Remote=true;Check(!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x),"remote world cannot publish custody");a.world.Remote=false;
  a.world.Actor=null;Check(!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(a,x),"detached entity cannot publish custody");a.world.Actor=a;
  var b=Actor();var y=new XElement("humanEquipment");b.inventory.ItemGrid.items[0]=new ItemStack("club",1,"same");b.inventory.ItemGrid.items[1]=new ItemStack("club",1,"same");
  Check(RebirthNpcInventoryTransactionService.ObserveHumanEquipment(b,y)&&RebirthNpcInventoryTransactionService.Native().Elements().Count()==2,"two identical physical items receive distinct retained identities");
  Check(y.Element("bindings").Elements().Select(e=>(string)e.Attribute("id")).Distinct().Count()==2,"identical slot bindings remain unambiguous");
  b.Hand.switchingCoroutine=new object();Check(RebirthNpcIdleHandActionQualification.IsIdle(b),"completed native coroutine handle does not block idle checkpoint");
  b.Hand.IsSwitching=true;Check(!RebirthNpcIdleHandActionQualification.IsIdle(b),"active hand transition blocks checkpoint");b.Hand.IsSwitching=false;
  b.Hand.Busy=true;Check(!RebirthNpcIdleHandActionQualification.IsIdle(b),"active attack blocks checkpoint");b.Hand.Busy=false;
  b.Hand.Held.actionData=new object[]{new ItemActionRanged.ItemActionDataRanged{isReloading=true}};Check(!RebirthNpcIdleHandActionQualification.IsIdle(b),"active native reload blocks checkpoint");
  Check(RebirthNpcIdleHandActionQualification.Qualified(new ItemValue("actual melee")),"native DynamicMelee is supported for idle reconstruction");
  Console.WriteLine("RESULT: "+passed+" production custody checks passed against native API stubs; no game execution.");
 }
}


