using System;
class Time { public static float realtimeSinceStartup=10; }
class Bag { public object SelectedItem; }
class Workspace { public bool IsWorkspaceOpen=true, PresentationInteractionBusy; public Bag BackpackView=new Bag(); }
class Catalogue { public float FrameClock=10; public bool FrameAssemblyActive; }
class RebirthSlotPresentationGate { public bool Hidden; public int Calls; public RebirthSlotPresentationGate(object owner){} public bool Hide(){Calls++;Hidden=true;return true;} }
class baseline { public bool IsSelected,IsHolding,IsLocked,IsDragAndDrop,presentationHovered,hovered,culled; public float presentationWakeUntil,wakeUntil; public Workspace creativeOwner=new Workspace(); public Catalogue Catalogue=new Catalogue(); public RebirthSlotPresentationGate presentation; public bool Outside=true; public int HoverCalls; bool OutsideViewport(bool value)=>Outside; void Hovered(bool value){HoverCalls++;}
private bool PinnedForNativeAction => IsSelected || IsHolding || IsLocked || IsDragAndDrop || presentationHovered ||
        Time.realtimeSinceStartup < presentationWakeUntil || creativeOwner?.BackpackView?.SelectedItem == this ||
        creativeOwner?.PresentationInteractionBusy == true;
    private bool CullCreativePresentation()
    {
        if (creativeOwner?.IsWorkspaceOpen != true || !OutsideViewport(true) || PinnedForNativeAction)
            return false;
        if (!culled) Hovered(false);
        culled = true;
        presentation = presentation ?? new RebirthSlotPresentationGate(this);
        return presentation.Hide(); // If grouping cannot be initialized, retain the native path.
    }
 public bool Hide()=>CullCreativePresentation(); public bool CataloguePinned(){var workspace=creativeOwner;bool pinned = IsSelected || IsHolding || IsLocked || IsDragAndDrop || hovered ||
            (Catalogue != null && Catalogue.FrameClock < wakeUntil) || workspace?.BackpackView?.SelectedItem == this || workspace?.PresentationInteractionBusy == true ||
            Catalogue?.FrameAssemblyActive == true; return pinned;} }
class candidate { public bool IsSelected,IsHolding,IsLocked,IsDragAndDrop,presentationHovered,hovered,culled; public float presentationWakeUntil,wakeUntil; public Workspace creativeOwner=new Workspace(); public Catalogue Catalogue=new Catalogue(); public RebirthSlotPresentationGate presentation; public bool Outside=true; public int HoverCalls; bool OutsideViewport(bool value)=>Outside; void Hovered(bool value){HoverCalls++;}
private bool PinnedForNativeAction => IsSelected || IsHolding || IsLocked || IsDragAndDrop || presentationHovered ||
        // A global press/held cursor needs visible drop targets, not off-screen visuals.
        // Native inventory roots stay active; only this cell's own live action pins it.
        Time.realtimeSinceStartup < presentationWakeUntil || creativeOwner?.BackpackView?.SelectedItem == this;
    private bool CullCreativePresentation()
    {
        if (creativeOwner?.IsWorkspaceOpen != true || !OutsideViewport(true) || PinnedForNativeAction)
            return false;
        if (!culled) Hovered(false);
        culled = true;
        presentation = presentation ?? new RebirthSlotPresentationGate(this);
        return presentation.Hide(); // If grouping cannot be initialized, retain the native path.
    }
 public bool Hide()=>CullCreativePresentation(); public bool CataloguePinned(){var workspace=creativeOwner;bool pinned = IsSelected || IsHolding || IsLocked || IsDragAndDrop || hovered ||
            (Catalogue != null && Catalogue.FrameClock < wakeUntil) || workspace?.BackpackView?.SelectedItem == this ||
            Catalogue?.FrameAssemblyActive == true; return pinned;} }
class Program {
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Main(){
  var old=new baseline();old.creativeOwner.PresentationInteractionBusy=true;
  Check(!old.Hide(),"baseline must reproduce global gesture wake");
  var fixedCell=new candidate();fixedCell.creativeOwner.PresentationInteractionBusy=true;
  Check(fixedCell.Hide() && fixedCell.presentation.Hidden,"offscreen global gesture must stay hidden");
  Check(old.CataloguePinned() && !fixedCell.CataloguePinned(),"catalogue buffered rows reproduce/fix same fault");
  foreach(bool busy in new[]{false,true}){
   for(int flags=0;flags<128;flags++){
    var c=new candidate{IsSelected=(flags&1)!=0,IsHolding=(flags&2)!=0,IsLocked=(flags&4)!=0,IsDragAndDrop=(flags&8)!=0,presentationHovered=(flags&16)!=0,hovered=(flags&16)!=0,presentationWakeUntil=(flags&32)!=0?11:0,wakeUntil=(flags&32)!=0?11:0};
    c.creativeOwner.PresentationInteractionBusy=busy;
    if((flags&64)!=0)c.creativeOwner.BackpackView.SelectedItem=c;
    Check(c.Hide()==(flags==0),"intrinsic backpack actions must stay awake");
    Check(c.CataloguePinned()==(flags!=0),"intrinsic catalogue actions must stay awake");
    c.Outside=false;Check(!c.Hide(),"visible native target must remain awake");
   }
  }
  var editor=new candidate();editor.Catalogue.FrameAssemblyActive=true;Check(editor.CataloguePinned(),"open editor safety gate retained");
  int before=0,after=0;
  for(int frame=0;frame<60;frame++) for(int i=0;i<170;i++){
    var a=new baseline{Outside=i>=77};a.creativeOwner.PresentationInteractionBusy=true;
    var b=new candidate{Outside=i>=77};b.creativeOwner.PresentationInteractionBusy=true;
    if(!a.Hide())before++;if(!b.Hide())after++;
  }
  Check(before==10200 && after==4620,"60-frame modeled backpack traversal regression");
  Console.WriteLine($"PASS {checks} extracted-production-predicate assertions. Baseline reproduces offscreen global gesture wake. 170-slot/77-visible/60-frame modeled eligible passes {before}->{after}. Not Unity timing or native acceptance.");
 }
}