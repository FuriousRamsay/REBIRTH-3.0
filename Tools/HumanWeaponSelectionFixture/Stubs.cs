using System;
using System.Collections.Generic;
namespace UnityEngine { public static class Time {public static float time;} }
class EntityRebirthNPC {
 public Inventory inventory=new Inventory();public Bag bag=new Bag();public object world=new object();public bool IsBreakingBlocks,IsBreakingDoors,Ready=true,Dead;public EntityRebirthNPC Target;public float Distance=10;
 public EntityRebirthNPC GetAttackTarget()=>Target;public bool IsDead()=>Dead;public float GetDistance(EntityRebirthNPC target)=>Distance;
}
class EntityRebirthHumanoidNPC:EntityRebirthNPC {}
static class RebirthHumanAiPolicy {public static bool Ready(EntityRebirthNPC npc,bool combat)=>npc is EntityRebirthHumanoidNPC&&npc.Ready;}
class Inventory {
 public Grid ItemGrid=new Grid();public int holdingItemIdx,Switches;public bool Busy,Switching;
 public bool IsHandSwitching()=>Switching;public bool IsHoldingItemActionRunning()=>Busy;
 public void SetHoldingItemIdx(int i){holdingItemIdx=i;Switches++;}
 public int GetItemCount(ItemValue v){int n=0;foreach(var s in ItemGrid.items)if(s!=null&&!s.IsEmpty()&&s.itemValue.type==v.type)n+=s.count;return n;}
}
class Grid {public ItemStack[] items=new ItemStack[4];}
class Bag {public Dictionary<int,int> Counts=new Dictionary<int,int>();public int GetItemCount(ItemValue v)=>Counts.TryGetValue(v.type,out int n)?n:0;}
class ItemStack {public int count=1;public ItemValue itemValue;public bool IsEmpty()=>count==0||itemValue==null||itemValue.IsEmpty();}
class ItemValue {public int type,Meta,SelectedAmmoTypeIndex,MaxUseTimes=100;public float UseTimes;public ItemClass ItemClass;public bool IsEmpty()=>type==0;}
class ItemClass {public ItemAction[] Actions;public static ItemValue GetItem(string name,bool unused)=>new ItemValue{type=name=="ammo"?3:0};}
class ItemAction {}
class ItemActionMelee:ItemAction {}
class ItemActionDynamicMelee:ItemAction {}
class ItemActionRanged:ItemAction {public string[] MagazineItemNames=new[]{"ammo"};}
class ItemActionCatapult:ItemActionRanged {}
class ItemActionLauncher:ItemActionRanged {}
