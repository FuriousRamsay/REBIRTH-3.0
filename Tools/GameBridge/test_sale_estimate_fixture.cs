using System;
public class ItemClass { public bool IsBlock(){return false;} public string GetItemDescriptionKey(){return "desc";} }
public class ItemValue {public ItemClass ItemClassOrMissing=new ItemClass();public int type;}
public class ItemStack {public ItemValue itemValue=new ItemValue();public int count=3;public bool IsEmpty(){return count==0;} }
public class Block {public static Block[] list=new Block[0];public string DescriptionKey;}
public class PlayerUI {public object entityPlayer=new object();}
public class XUi {public object Trader=new object();public PlayerUI playerUI=new PlayerUI();}
static class Localization {
 public static bool BadFormat;public static bool Exists(string key){return true;}
 public static string Get(string key){return key=="desc"?"Item description":BadFormat?"{bad}":"Sale {0}";}
}
static class XUiM_Trader {
 public static bool Fail;public static int Price=42;public static int Calls;
 public static int GetSellPrice(XUi ui,ItemValue value,int count){Calls++;if(Fail)throw new Exception("native price failure");return Price;}
}
// HELPER
public class Check {
// SOURCE
 static void Expect(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main(){
  var ui=new XUi();var stack=new ItemStack();int price;
  Expect(ResolveDescription(stack,ui)=="Sale 42\nItem description","normal description quote");
  XUiM_Trader.Fail=true;
  Expect(!RebirthItemSaleEstimate.TryGet(ui,stack,out price),"price exception escaped");
  Expect(ResolveDescription(stack,ui)=="Item description","price failure hid description");
  XUiM_Trader.Fail=false;Localization.BadFormat=true;
  Expect(ResolveDescription(stack,ui)=="Item description","format failure hid description");
  Localization.BadFormat=false;XUiM_Trader.Price=0;
  Expect(RebirthItemSaleEstimate.TryGet(ui,stack,out price)&&price==0,"valid zero lost");
  int calls=XUiM_Trader.Calls;
  Expect(!RebirthItemSaleEstimate.TryGet(null,stack,out price),"missing UI");
  Expect(!RebirthItemSaleEstimate.TryGet(ui,null,out price),"missing stack");
  Expect(!RebirthItemSaleEstimate.TryGet(ui,new ItemStack{count=0},out price),"empty stack");
  ui.playerUI.entityPlayer=null;Expect(!RebirthItemSaleEstimate.TryGet(ui,stack,out price),"missing player");
  Expect(calls==XUiM_Trader.Calls,"invalid inputs called native pricing");
  Console.WriteLine("PASS actual sale helper and description: native pricing failure/format failure preserve prose, normal and zero quotes, invalid inputs skip pricing. Native pricing/localization substituted.");
 }
}
