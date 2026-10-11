$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$stub=@"
using System;
using UnityEngine;
namespace UnityEngine {
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public class Transform {public UIWidget Widget=new UIWidget();public BoxCollider Collider=new BoxCollider();public T GetComponent<T>() where T:class=>typeof(T)==typeof(UIWidget)?Widget as T:Collider as T;}
 public class BoxCollider {public Vector3 center,size;}
 public static class Mathf {public static int RoundToInt(float n)=>(int)Math.Round(n);public static float Clamp01(float n)=>Math.Max(0,Math.Min(1,n));public static int Clamp(int n,int min,int max)=>Math.Max(min,Math.Min(max,n));}
}
public class UIWidget {public enum Pivot {TopLeft,Center};public Pivot pivot=Pivot.Center;public int width,height;}
public struct Vector2i {public int x,y;public Vector2i(int a,int b){x=a;y=b;}}
public class XUiView {public Vector2i Size,Position;public bool IsVisible;public Transform UiTransform=new Transform();public void TryUpdatePosition(){}}
public class XUiController {public XUiView ViewComponent=new XUiView();public XUiController GetChildById(string id)=>null;}
public class XUiV_ScrollBar:XUiView {public XUiController Controller=new XUiController();}
public static class ScrollbarFixture {
 public static string Run(){
 foreach(int height in new[]{24,96,210,316,455}) {
  var track=new XUiController();var thumb=new XUiController();
  if(Math.Abs(RebirthScrollbarPresentation.NativeFraction(height,height,height*4)*height-RebirthScrollbarPresentation.ThumbHeight(height,height,height*4))>.01)throw new Exception("Native/custom thumb policy mismatch");
  RebirthScrollbarPresentation.Render(track,thumb,new Vector2i(900,-10),height,height,height*4,0);
  if(!thumb.ViewComponent.IsVisible||thumb.ViewComponent.Position.y!=-10)throw new Exception("Top alignment failed");
  int h=thumb.ViewComponent.Size.y;
  if(h>height||h<Math.Min(30,height))throw new Exception("Thumb outside track");
  RebirthScrollbarPresentation.Render(track,thumb,new Vector2i(900,-10),height,height,height*4,height*3);
  if(thumb.ViewComponent.Position.y-h!=-10-height)throw new Exception("Bottom does not meet track");
  if(thumb.ViewComponent.UiTransform.Widget.height!=h||thumb.ViewComponent.UiTransform.Collider.size.y!=h)throw new Exception("Widget/collider not committed");
  RebirthScrollbarPresentation.Render(track,thumb,new Vector2i(900,-10),height,height,height,0);
  if(track.ViewComponent.IsVisible||thumb.ViewComponent.IsVisible)throw new Exception("Unneeded scrollbar visible");
  foreach(int width in new[]{6,7,8,12}){
   var compact=new XUiView();
   RebirthScrollbarPresentation.RenderThumb(compact,height,4,10,6,width,3);
   if(compact.Size.x!=width||compact.UiTransform.Widget.width!=width)throw new Exception("Window width ignored");
   if(compact.Position.y-compact.Size.y!=-height)throw new Exception("Compact bottom alignment failed");
   if(compact.UiTransform.Widget.pivot!=UIWidget.Pivot.TopLeft)throw new Exception("Native pivot not committed");
  }
 }
 return "PASS: production shared scrollbars fit five heights and four widths; top/bottom alignment, hidden overflow state, widget pivot and collider bounds agree.";
 }
}
"@
$production=[IO.File]::ReadAllText((Join-Path $root 'Scripts/UI/RebirthScrollbarPresentation.cs')).Replace('using System;','').Replace('using UnityEngine;','')
Add-Type -TypeDefinition ($stub+$production)
[ScrollbarFixture]::Run()