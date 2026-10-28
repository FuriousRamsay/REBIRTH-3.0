using System;
static class Program {
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static ItemStack Weapon(int type,ItemAction action,int rounds=0)=>new ItemStack{itemValue=new ItemValue{type=type,Meta=rounds,ItemClass=new ItemClass{Actions=new[]{action}}}};
 static EntityRebirthHumanoidNPC Actor(){var a=new EntityRebirthHumanoidNPC();a.Target=new EntityRebirthNPC{world=a.world};a.inventory.ItemGrid.items[0]=Weapon(1,new ItemActionMelee());a.inventory.ItemGrid.items[1]=Weapon(2,new ItemActionRanged(),7);return a;}
 static void Tick(EntityRebirthNPC a,float distance){UnityEngine.Time.time+=2;a.Distance=distance;RebirthHumanWeaponSelection.Tick(a);}
 static void Main(){
  var a=Actor();var melee=a.inventory.ItemGrid.items[0];var gun=a.inventory.ItemGrid.items[1];gun.itemValue.UseTimes=17;
  Tick(a,10);Check(a.inventory.holdingItemIdx==1,"far target selects equipped gun");
  Tick(a,4);Check(a.inventory.holdingItemIdx==1,"mid distance retains gun");
  Tick(a,3);Check(a.inventory.holdingItemIdx==0,"close target selects melee");
  Tick(a,4);Check(a.inventory.holdingItemIdx==0,"mid distance retains melee");
  Tick(a,6);Check(a.inventory.holdingItemIdx==1,"far threshold selects gun");
  Check(ReferenceEquals(a.inventory.ItemGrid.items[0],melee)&&ReferenceEquals(a.inventory.ItemGrid.items[1],gun)&&gun.itemValue.Meta==7&&gun.itemValue.UseTimes==17,"switch preserves exact stacks and loaded rounds/wear");
  UnityEngine.Time.time+=.3f;a.Distance=2;RebirthHumanWeaponSelection.Tick(a);Check(a.inventory.holdingItemIdx==1,"switch cooldown");
  gun.itemValue.Meta=0;Tick(a,10);Check(a.inventory.holdingItemIdx==0,"empty gun without reserves falls back to melee");
  a.inventory.ItemGrid.items[2]=new ItemStack{count=9,itemValue=new ItemValue{type=3}};Tick(a,10);Check(a.inventory.holdingItemIdx==1,"native belt ammunition permits ranged selection");
  Check(a.inventory.ItemGrid.items[2].count==9,"selection does not consume ammunition");
  a.inventory.ItemGrid.items[2]=null;a.bag.Counts[3]=4;Tick(a,2);Tick(a,10);Check(a.inventory.holdingItemIdx==1,"native bag ammunition also recognized");
  a.inventory.Busy=true;Tick(a,2);Check(a.inventory.holdingItemIdx==1,"attack/reload busy prevents switching");a.inventory.Busy=false;
  a.inventory.Switching=true;Tick(a,2);Check(a.inventory.holdingItemIdx==1,"native hand transition prevents switching");a.inventory.Switching=false;
  a.IsBreakingBlocks=true;Tick(a,2);Check(a.inventory.holdingItemIdx==1,"obstruction attack retains its hand");a.IsBreakingBlocks=false;
  a.Ready=false;Tick(a,2);Check(a.inventory.holdingItemIdx==1,"server/recovery readiness gate");a.Ready=true;
  gun.itemValue.UseTimes=100;Tick(a,10);Check(a.inventory.holdingItemIdx==0,"broken firearm excluded");
  var b=Actor();b.inventory.ItemGrid.items[0]=null;Tick(b,1);Check(b.inventory.holdingItemIdx==1,"gun-only loadout retains ranged at close distance");
  b.inventory.ItemGrid.items[1].itemValue.Meta=0;Tick(b,10);Check(b.inventory.holdingItemIdx==0,"no usable weapon selects native empty-hand slot");
  var dog=new EntityRebirthNPC{Distance=10};dog.Target=new EntityRebirthNPC{world=dog.world};dog.inventory.ItemGrid.items[1]=Weapon(2,new ItemActionRanged(),5);Tick(dog,10);Check(dog.inventory.Switches==0,"nonhuman/dog excluded");
  var launcher=Actor();launcher.inventory.ItemGrid.items[1]=Weapon(2,new ItemActionLauncher(),4);Tick(launcher,10);Check(launcher.inventory.holdingItemIdx==0,"unsupported launcher excluded");
  Check(RebirthHumanWeaponRules.Choose(float.NaN,1,0,1)==1&&RebirthHumanWeaponRules.Choose(-1,1,0,1)==1,"invalid distances preserve held selection");
  Console.WriteLine("RESULT: "+count+" checks passed against production selector/rules with native API stubs. No game execution.");
 }
}
