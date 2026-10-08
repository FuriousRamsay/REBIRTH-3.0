using System;
using System.Collections.Generic;
public class ItemValue { public int type; public ItemValue(int v){type=v;} }
public class ItemStack { public ItemValue itemValue; public int count; public ItemStack(int t,int c){itemValue=new ItemValue(t);count=c;} }
public class Recipe {public List<ItemStack> ingredients=new List<ItemStack>();}
public class Station {public bool IsMilling;}
public class PlayerUI {public object entityPlayer;}
public class Xui {public PlayerUI playerUI=new PlayerUI();}
// Native quantity/effect double: exposes independently adjusted requirements for each role.
public static class RebirthCraftingIngredientQuantity {
 public static Dictionary<ItemStack,int> Adjusted=new Dictionary<ItemStack,int>();
 public static bool TryResolveTotal(object p,Recipe r,ItemStack i,int tier,int batches,out int total){
  total=0;if(batches<=0||!Adjusted.TryGetValue(i,out int amount)||amount<0)return false;
  long n=(long)amount*batches;if(n>int.MaxValue)return false;total=(int)n;return true;
 }
}
public class Workspace {
 public Station station=new Station();public Recipe selected;public ItemStack[] ghosts=new ItemStack[12];public Xui xui=new Xui();
 private static bool TryBatchQuantity(int p,int b,out int total){total=0;long n=(long)p*b;if(p<=0||b<=0||n>int.MaxValue)return false;total=(int)n;return true;}
 // PRODUCTION_CLASS
}
public static class Test {
 static void Check(bool v,string label){if(!v)throw new Exception(label);}
 public static void Main(){
  var w=new Workspace();w.station.IsMilling=true;w.selected=new Recipe();
  var a=new ItemStack(7,9);var b=new ItemStack(8,20);var c=new ItemStack(7,6);
  w.selected.ingredients.AddRange(new[]{a,b,c});w.ghosts[0]=new ItemStack(7,1);w.ghosts[1]=new ItemStack(8,1);
  var effects=RebirthCraftingIngredientQuantity.Adjusted;effects[a]=3;effects[b]=0;effects[c]=2;
  Check(w.TryGhostQuantity(0,4,out int n)&&n==20,"repeated roles summed after adjustment");
  Check(w.TryGhostQuantity(1,4,out n)&&n==0,"zero cost");
  effects[c]=5;Check(w.TryGhostQuantity(0,4,out n)&&n==32,"current effects");
  effects[a]=int.MaxValue;effects[c]=1;Check(!w.TryGhostQuantity(0,1,out n),"sum overflow");
  effects[a]=-1;Check(!w.TryGhostQuantity(0,1,out n),"invalid native quantity");
  Check(!w.TryGhostQuantity(0,0,out n),"invalid batches");
  w.ghosts[2]=new ItemStack(99,1);Check(!w.TryGhostQuantity(2,1,out n),"unknown material");
  Check(!w.TryGhostQuantity(-1,1,out n)&&!w.TryGhostQuantity(12,1,out n),"slot bounds");
  w.station.IsMilling=false;w.ghosts[0].count=9;Check(w.TryGhostQuantity(0,4,out n)&&n==36,"food counts retained");
  w.station.IsMilling=true;w.ghosts[9]=new ItemStack(7,2);Check(w.TryGhostQuantity(9,4,out n)&&n==8,"helper multiplication retained");
  Console.WriteLine("PASS: production milling ghost quantity method; repeated adjusted roles, zero cost, changing effects, overflow, invalid inputs, food and helper paths (native effect doubles)");
 }
}
