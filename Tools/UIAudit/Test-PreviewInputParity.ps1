$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$stub=@"
using HarmonyLib;using System;using System.Collections.Generic;using System.Globalization;using System.Runtime.CompilerServices;using UnityEngine;
namespace HarmonyLib {public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n){}}public class HarmonyPostfix:Attribute {}public class HarmonyPriority:Attribute {public HarmonyPriority(int p){}}public static class Priority {public const int Last=0;}} namespace UnityEngine {
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
 public struct Color {public static bool operator ==(Color x,Color y)=>x.r==y.r&&x.g==y.g&&x.b==y.b&&x.a==y.a;public static bool operator !=(Color x,Color y)=>!(x==y);public override bool Equals(object o)=>o is Color c&&this==c;public override int GetHashCode()=>0;public float r,g,b,a;public static implicit operator Color(Color32 c)=>new Color{r=c.r,g=c.g,b=c.b,a=c.a};}
 public struct Color32 {public byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}public static implicit operator Color32(Color c)=>new Color32((byte)c.r,(byte)c.g,(byte)c.b,(byte)c.a);}
 public class Transform {public Vector3 localScale=new Vector3(1,1,1);}
 public static class Mathf {public static bool Approximately(float a,float b)=>Math.Abs(a-b)<.0001f;public static int RoundToInt(float n)=>(int)Math.Round(n);public static float Clamp01(float n)=>Math.Max(0,Math.Min(1,n));}
}
public struct Vector2i {public int x,y;public static Vector2i zero=>new Vector2i();public Vector2i(int x,int y){this.x=x;this.y=y;}}
public class UIWidget {public enum Pivot {TopLeft,Top,TopRight,Left,Center,Right,BottomLeft,Bottom,BottomRight};public Pivot pivot;}
public static class NGUIText {public enum Alignment {Right,Center};}
public static class UILabel {public enum Overflow {ClampContent};}
public class XUiView {public string ID;public Vector2i Position,Size;public UIWidget.Pivot Pivot;public UIWidget widget=new UIWidget();public bool IsVisible=true;public Transform UiTransform=new Transform();public void TryUpdatePosition(){}public void Update(float dt){}}
public class FakeSprite {public Color color;} public class XUiV_Sprite:XUiView {public FakeSprite Sprite=new FakeSprite();public float Fill=1;public Color Color;public string SpriteName="native";public void SetColorImmediately(Color c){Color=c;}}
public class XUiV_FilledSprite:XUiV_Sprite {}
public class XUiV_Label:XUiView {public NGUIText.Alignment Alignment;public UILabel.Overflow Overflow;public bool OverflowEllipsis;public int FontSize=26;public string Text="";public void SetTextImmediately(string s){Text=s;}}
public class XUi {public DragWindow DragAndDropWindow=new DragWindow();}
public class DragWindow {public XUiC_ItemStack ItemStackControl;}
public class Group {public XUiController Controller;}
public class XUiController {
 public string Id;public XUi xui;public Group windowGroup;public XUiView ViewComponent=new XUiView();public List<XUiController> Children=new List<XUiController>();
 public XUiController GetChildById(string id){if(Id==id)return this;foreach(var c in Children){var n=c.GetChildById(id);if(n!=null)return n;}return null;}
 public T GetChildByType<T>() where T:XUiController {if(this is T t)return t;foreach(var c in Children){var n=c.GetChildByType<T>();if(n!=null)return n;}return null;}
 public virtual void GetBindingValue(ref string s,string n){s=n=="hasdurability"?"true":n=="haspermadurability"?"false":"1";}
}
public class XUiC_Backpack:XUiController {public XUiC_ItemStack[] Slots;public XUiC_ItemStack[] GetItemStackControllers()=>Slots;}
public class XUiC_ItemStack:XUiController {public void Update(float dt){}public bool IsDragAndDrop;public ItemStack ItemStack;}
public class XUiC_EquipmentStack:XUiController {public ItemValue ItemValue;public XUiV_Sprite durability,durabilityRemoveBackground,durabilityBackground;}
public class ItemStack {public int count;public ItemValue itemValue;public bool IsEmpty()=>itemValue==null;}
public class ItemValue {public int Quality;public ItemClass ItemClass=new ItemClass();public float Ml=500;public bool Drink;public bool IsEmpty()=>false;}
public class ItemClass {public bool HasQuality;public bool Drink;}
public class Definition {public bool IsDrink;}
public static class RebirthSlotPalette {public static void ApplyLockIcon(XUiC_ItemStack slot){}} public static class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld()=>true;} public static class RebirthConsumableResolver {public static bool TryResolve(ItemClass v,out Definition d){d=new Definition{IsDrink=v?.Drink==true};return v!=null;}public static bool TryResolve(ItemValue v,out Definition d){d=new Definition{IsDrink=v?.Drink==true};return v!=null;}}
public static class RebirthLiquidContainerService {public static float GetFill01(ItemValue v,Definition d)=>v.Ml/500;public static float GetRemainingMl(ItemValue v,Definition d)=>v.Ml;public static string FormatVolume(float f)=>f.ToString(CultureInfo.InvariantCulture)+" mL";}
public static class StringParsers {public static Color ParseColor32(string s)=>new Color32(66,139,190,255);}
public static class RebirthCharacterItemStatsTooltip {public static XUiController Surface;public static XUiController ActiveSurface(XUi ui)=>Surface;}
public static class PreviewParityFixture {
 static int checks;
 static void A(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static XUiController Add(XUiController root,string id,XUiView v){var c=new XUiController{Id=id,ViewComponent=v};v.ID=id;root.Children.Add(c);return c;}
 static XUiC_ItemStack Slot(int cell,int font,ItemStack image,bool cursor=false){
  var s=new XUiC_ItemStack{ItemStack=image,IsDragAndDrop=cursor};
  Add(s,"itemIcon",new XUiV_Sprite{Position=new Vector2i(cell/2,-cell/2),Size=new Vector2i(cell-11,cell-11),Pivot=UIWidget.Pivot.Center});
  Add(s,"stackValue",new XUiV_Label{Position=new Vector2i(-2,-3*cell/5),Size=new Vector2i(cell-5,28),FontSize=font});
  Add(s,"durability",new XUiV_Sprite{Position=new Vector2i(1,-4*cell/5),Size=new Vector2i(cell-5,10)});
  Add(s,"durability",new XUiV_FilledSprite{Position=new Vector2i(1,-4*cell/5),Size=new Vector2i(cell-5,10),Fill=.6f});
  Add(s,"durabilityBackground",new XUiV_Sprite{Position=new Vector2i(1,-4*cell/5),Size=new Vector2i(cell-5,10),IsVisible=false});return s;
 }
 static XUiController Panel(XUi ui,Group group,int size){var p=new XUiController{xui=ui,windowGroup=group};
  Add(p,"theoryInspectIcon",new XUiV_Sprite{Position=new Vector2i(20,-10),Size=new Vector2i(size,size)});
  var rail=Add(p,"theorySelectedDurability",new XUiView());Add(rail,"qualityNumber",new XUiV_Label{FontSize=9});Add(rail,"track",new XUiV_Sprite());Add(rail,"fill",new XUiV_FilledSprite());Add(rail,"permanent",new XUiV_Sprite());Add(p,"theorySelectedDurabilityCount",new XUiV_Label());return p;
 }
 static void Compare(XUiController a,XUiController b,string name){
  foreach(string id in new[]{"theorySelectedDurability","qualityNumber"}){
   var x=a.GetChildById(id).ViewComponent;var y=b.GetChildById(id).ViewComponent;
   A(x.Position.x==y.Position.x&&x.Position.y==y.Position.y,name+" position");
   A(x.Size.x==y.Size.x&&x.Size.y==y.Size.y,name+" size");
   A(x.UiTransform.localScale.x==y.UiTransform.localScale.x,name+" scale");
   if(x is XUiV_Label lx && y is XUiV_Label ly)A(lx.FontSize==ly.FontSize&&lx.Alignment==ly.Alignment&&lx.Text==ly.Text,name+" label");
  }
 }
 public static string Run(){
 foreach(int size in new[]{64,80,96,110,120})foreach(int kind in new[]{0,1,2}){
  var image=new ItemStack{count=kind==1?1:2,itemValue=new ItemValue{Drink=kind<2,Quality=4,ItemClass=new ItemClass{HasQuality=kind==2,Drink=kind<2},Ml=250}};
  var ui=new XUi();var source=Slot(76,26,image);var cursor=Slot(106,11,image,true);
  var bag=new XUiC_Backpack{Slots=new[]{source}};var group=new Group{Controller=bag};source.xui=ui;cursor.xui=ui;cursor.windowGroup=new Group{Controller=cursor};ui.DragAndDropWindow.ItemStackControl=cursor;RebirthCharacterItemStatsTooltip.Surface=bag;bag.windowGroup=group;
  var selected=Panel(ui,group,size);var dragged=Panel(ui,group,size);RebirthSelectedDurability.Render(selected,"theorySelectedDurability",source);RebirthSelectedDurability.Render(dragged,"theorySelectedDurability",cursor);Compare(selected,dragged,"kind="+kind+" size="+size);
  // The cursor itself uses the same reference, never its independently authored font.
  cursor.IsDragAndDrop=false;typeof(RebirthDrinkSlotPresentationPatch).GetMethod("Postfix",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{cursor});var cursorCount=(XUiV_Label)cursor.GetChildById("stackValue").ViewComponent;
  A(cursorCount.FontSize==(kind==1?21:26),"cursor inherited its own font");
  A(cursorCount.Alignment==(kind==0?NGUIText.Alignment.Right:NGUIText.Alignment.Center),"cursor alignment");
  A(cursorCount.Text==(kind==0?"2":kind==1?"250 mL":"4"),"cursor label");
  var nativeTarget=Slot(size+11,8,image);nativeTarget.xui=ui;nativeTarget.windowGroup=group;
  RebirthSelectedDurability.CopyNativePresentation(nativeTarget,cursor);var nativeCount=(XUiV_Label)nativeTarget.GetChildById("stackValue").ViewComponent;
  A(nativeCount.FontSize==cursorCount.FontSize&&nativeCount.Text==cursorCount.Text&&nativeCount.Alignment==cursorCount.Alignment,"native/manual/drag label rules differ");
 }
 return "PASS: "+checks+" production-renderer checks; clicked and held previews match at five sizes for stacked drinks, partial single drinks and quality equipment; cursor and native panels share typography and alignment. Unity/native widget behavior is explicitly doubled.";
 }
}
"@
$production=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/UI/RebirthSelectedDurability.cs')).Replace('using System;','').Replace('using System.Globalization;','').Replace('using System.Runtime.CompilerServices;','').Replace('using UnityEngine;','')
$layout=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/UI/RebirthItemPreviewLayout.cs')).Replace('using System;','')
$hook=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Metabolism/RebirthDrinkSlotPresentationPatch.cs')) -replace '(?m)^using [^;]+;\r?\n',''
Add-Type -TypeDefinition ($stub+$layout+$production+$hook)
[PreviewParityFixture]::Run()