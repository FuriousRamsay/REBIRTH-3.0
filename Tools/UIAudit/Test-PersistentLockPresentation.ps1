$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$stub=@"
using UnityEngine;
namespace UnityEngine {
 public struct Color {public float a;public static implicit operator Color(Color32 c)=>new Color();public static bool operator ==(Color a,Color b)=>true;public static bool operator !=(Color a,Color b)=>false;public override bool Equals(object o)=>true;public override int GetHashCode()=>0;}
 public struct Color32 {public Color32(byte a,byte b,byte c,byte d){}}
 public static class Mathf {public static bool Approximately(float a,float b)=>a==b;}
}
public class XUiView {public bool IsVisible;public int Commits;public void Update(float dt){Commits++;}}
public class Sprite {public Color color;}
public class XUiV_Sprite:XUiView {public Color Color;public Sprite Sprite;public void SetColorImmediately(Color c){}}
public class XUiController {public XUiView ViewComponent;public virtual XUiController GetChildById(string id)=>null;}
public class XUiC_ItemStack:XUiController {public bool UserLockedSlot,AttributeLock;public int Lookups;public XUiController Icon=new XUiController{ViewComponent=new XUiView()};public void ParseAttribute(string n,string v){}public override XUiController GetChildById(string id){Lookups++;return Icon;}}
public class Bar {public float alpha;}
public class XUiV_ScrollBar {public Bar ScrollBar;public XUiController Controller;}
public static class LockFixture {
 public static string Run(){var slot=new XUiC_ItemStack();int checks=0;
 foreach(bool locked in new[]{true,false,true,false}){
 slot.UserLockedSlot=locked;
 // Simulate native empty-slot bindings hiding the icon before the shared postfix.
 slot.Icon.ViewComponent.IsVisible=false;RebirthSlotPalette.ApplyLockIcon(slot);
 if(slot.Icon.ViewComponent.IsVisible!=locked)throw new System.Exception("Persistent lock differs from slot state");checks++;
 int commits=slot.Icon.ViewComponent.Commits;RebirthSlotPalette.ApplyLockIcon(slot);
 if(slot.Icon.ViewComponent.Commits!=commits)throw new System.Exception("Unchanged lock forces a redraw");checks++;
 }
 if(slot.Lookups!=1)throw new System.Exception("Lock presentation repeats tree searches");
 return "PASS: persistent lock follows locked/unlocked state after native hiding, avoids unchanged redraws and resolves its icon once; native widgets are doubled.";
 }
}
"@
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/UI/RebirthSlotPalette.cs')).Replace('using UnityEngine;','')
Add-Type -TypeDefinition ($stub+$source)
[LockFixture]::Run()
