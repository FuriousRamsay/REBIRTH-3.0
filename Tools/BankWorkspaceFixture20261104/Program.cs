using System;
using System.Collections.Generic;
class Program {
 static void Main(){int pass=0;
 foreach(string remembered in new string[]{null,"","otherScreenSlot","bankSlot"}){
  var bank=new XUiC_RebirthBankBackpackWindow();bank.Nodes.Add("bankSlot",new Node());
  XUiC_BackpackWindow.defaultSelectedElement=remembered;
  bank.OnOpen();
  if(bank.OpenCount!=1||XUiC_BackpackWindow.defaultSelectedElement!=""&&XUiC_BackpackWindow.defaultSelectedElement!=null)throw new Exception("open failed");
  if(bank.Nodes["bankSlot"].Selected!=(remembered=="bankSlot"))throw new Exception("valid focus changed");pass++;
  bank.OnOpen();if(bank.OpenCount!=2)throw new Exception("reopen failed");pass++;
 }
 Console.WriteLine($"PASS {pass} extracted production remembered-selection cases (native UI adapter doubled); no game");
 }
}
public class Node{public bool Selected;public void SelectCursorElement(bool delay){Selected=true;}}
public class XUiC_BackpackWindow {
 public static string defaultSelectedElement;public int OpenCount;
 public Dictionary<string,Node> Nodes=new();public Node GetChildById(string id)=>Nodes.TryGetValue(id,out var value)?value:null;
 public virtual void OnOpen(){if(!string.IsNullOrEmpty(defaultSelectedElement)){GetChildById(defaultSelectedElement).SelectCursorElement(true);defaultSelectedElement="";}OpenCount++;}
}
