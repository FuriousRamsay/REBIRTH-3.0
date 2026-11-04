const fs=require('fs');
const dir=__dirname,root=process.cwd();
function block(s,mark){let a=s.indexOf(mark);if(a<0)throw Error(mark);let b=s.indexOf('{',a),n=1,i=b+1;for(;i<s.length&&n;i++){if(s[i]=='{')n++;if(s[i]=='}')n--;}if(n)throw Error('braces');return s.slice(a,i);}
const inputs={baseline:'_Documentation/UI/InventoryStutter20261102/before/',candidate:''};
let code='using System;\n';
code+='class Time { public static float realtimeSinceStartup=10; }\nclass Bag { public object SelectedItem; }\nclass Workspace { public bool IsWorkspaceOpen=true, PresentationInteractionBusy; public Bag BackpackView=new Bag(); }\nclass Catalogue { public float FrameClock=10; public bool FrameAssemblyActive; }\nclass RebirthSlotPresentationGate { public bool Hidden; public int Calls; public RebirthSlotPresentationGate(object owner){} public bool Hide(){Calls++;Hidden=true;return true;} }\n';
for(const [key,prefix] of Object.entries(inputs)){
 const bag=fs.readFileSync(prefix+'Scripts/Survivor/UI/XUiC_RebirthCharacterBackpack.cs','utf8');
 const creative=fs.readFileSync(prefix+'Scripts/UI/XUiC_RebirthCreativeWorkspace.cs','utf8');
 const prop=bag.slice(bag.indexOf('private bool PinnedForNativeAction =>'),bag.indexOf('    // Keep the native slot ROOT active:')).trim();
 const cull=block(bag,'    private bool CullCreativePresentation()');
 const pinned=creative.match(/bool pinned = IsSelected[^;]+;/)[0];
 code+='class '+key+' { public bool IsSelected,IsHolding,IsLocked,IsDragAndDrop,presentationHovered,hovered,culled; public float presentationWakeUntil,wakeUntil; public Workspace creativeOwner=new Workspace(); public Catalogue Catalogue=new Catalogue(); public RebirthSlotPresentationGate presentation; public bool Outside=true; public int HoverCalls; bool OutsideViewport(bool value)=>Outside; void Hovered(bool value){HoverCalls++;}\n'+prop+'\n'+cull+'\n public bool Hide()=>CullCreativePresentation(); public bool CataloguePinned(){var workspace=creativeOwner;'+pinned+' return pinned;} }\n';
}
code+=String.raw`class Program {
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
}`;
fs.writeFileSync(dir+'/Generated.cs',code);
console.log('Extracted candidate and baseline production cull predicates/methods into isolated regression harness.');