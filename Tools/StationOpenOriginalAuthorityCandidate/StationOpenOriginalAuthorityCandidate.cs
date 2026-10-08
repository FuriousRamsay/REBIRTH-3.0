using System;
// Inactive Tools-only hooks. Caller route must bind exact installed original callsites, never packet values.
internal static class StationOpenOriginalAuthorityCandidate
{
 [ThreadStatic] static Frame current; static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_RecipeStack,Frame> retained=new System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_RecipeStack,Frame>();
 internal sealed class Frame
 {
  internal readonly XUiC_RecipeStack Source;internal readonly RebirthStationCompletionOriginalScope Original;
  internal readonly XUiC_WorkstationWindowGroup Group;internal readonly XUiM_Workstation Model;internal readonly object Ui,Window;internal readonly EntityPlayerLocal Viewer;internal readonly RecipeQueueItem Active;internal readonly Recipe Recipe;internal readonly int Count;internal readonly Frame Previous;internal string FirstName,SecondName;internal int Names,RawXp;internal bool RawSeen,Awarded,Busy,Closed,Quarantined;
  internal ItemValue AwardItem;internal RebirthStationCompletionCapture.EarlyReceiptObservation Early;
  internal Frame(XUiC_RecipeStack s,RebirthStationCompletionOriginalScope o,Frame p){Source=s;Original=o;Previous=p;Group=s.windowGroup.Controller as XUiC_WorkstationWindowGroup;Model=Group.WorkstationData;Ui=s.xui;Window=s.windowGroup;Viewer=s.xui.playerUI.entityPlayer;Active=o.Station.Queue[o.Station.Queue.Length-1];Recipe=s.recipe;Count=s.recipeCount;}
 }
 static bool Current(Frame f)=>f!=null&&!f.Closed&&!f.Quarantined&&ReferenceEquals(current,f)&&f.Original.IsCurrent()&&ReferenceEquals(f.Source.xui,f.Ui)&&ReferenceEquals(f.Source.windowGroup,f.Window)&&f.Source.windowGroup.isShowing&&ReferenceEquals(f.Source.windowGroup.Controller,f.Group)&&ReferenceEquals(f.Group.WorkstationData,f.Model)&&ReferenceEquals(f.Model.TileEntity,f.Original.Station)&&ReferenceEquals(f.Source.xui.playerUI.entityPlayer,f.Viewer)&&f.Viewer!=null&&!f.Viewer.world.IsRemote()&&ReferenceEquals(f.Source.Owner,f.Group.craftingQueue)&&f.Source.Owner.GetRecipesToCraft()!=null&&ReferenceEquals(System.Linq.Enumerable.LastOrDefault(f.Source.Owner.GetRecipesToCraft()),f.Source)&&f.Original.Station.Queue!=null&&f.Original.Station.Queue.Length>0&&ReferenceEquals(f.Original.Station.Queue[f.Original.Station.Queue.Length-1],f.Active)&&ReferenceEquals(f.Source.recipe,f.Recipe)&&ReferenceEquals(f.Active.Recipe,f.Recipe)&&f.Source.recipeCount==f.Count&&f.Active.Multiplier==f.Count&&f.Source.startingEntityId==f.Original.Actor&&f.Active.StartingEntityId==f.Original.Actor&&(f.Source.originalItem==null||f.Source.originalItem.IsEmpty())&&f.Original.IsCurrent();
 internal static bool Begin(XUiC_RecipeStack source,out Frame frame)
 {
  frame=null;try {var station=(source?.windowGroup?.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity;if(station?.Queue==null||station.Queue.Length==0||!RebirthStationCompletionOriginalScope.TryCapture(station,station.Queue[station.Queue.Length-1],out var original))return false;frame=new Frame(source,original,current);if(retained.TryGetValue(source,out var previous)){if(previous.Quarantined||previous.Early==null||!ReferenceEquals(previous.Active,frame.Active)||!ReferenceEquals(previous.Recipe,frame.Recipe)||!ReferenceEquals(previous.Model,frame.Model)||!ReferenceEquals(previous.Ui,frame.Ui)||!ReferenceEquals(previous.Window,frame.Window)||previous.Count!=frame.Count||previous.Original!=original&&!previous.Original.IsCurrent()){frame.Quarantined=true;}else{frame.Early=previous.Early;frame.Awarded=previous.Awarded;frame.RawSeen=previous.RawSeen;frame.RawXp=previous.RawXp;frame.Names=previous.Names;frame.FirstName=previous.FirstName;frame.SecondName=previous.SecondName;}}if(current!=null){current.Quarantined=true;frame.Quarantined=true;}current=frame;return Current(frame);}catch{return false;}
 }
 internal static string OriginalRecipeName(Recipe recipe,XUiC_RecipeStack source)
 {
  var name=recipe.GetName();var f=current;try{if(Current(f)&&ReferenceEquals(f.Source,source)&&ReferenceEquals(source.recipe,recipe)){if(f.Names++==0)f.FirstName=name;else if(f.Names==2)f.SecondName=name;else f.Quarantined=true;}}catch{if(f!=null)f.Quarantined=true;}return name;
 }
 internal static int OriginalCraftExp(Recipe recipe,XUiC_RecipeStack source)
 {
  int xp=recipe.craftExpGain;var f=current;try{if(Current(f)&&ReferenceEquals(f.Source,source)&&ReferenceEquals(source.recipe,recipe)&&!f.RawSeen){f.RawSeen=true;f.RawXp=xp;}else if(f!=null)f.Quarantined=true;}catch{if(f!=null)f.Quarantined=true;}return xp;
 }
 internal static int OriginalAward(Progression receiver,int xp,string cvar,Progression.XPTypes type,bool bonus,bool notify,int instigator,ItemValue item,XUiC_RecipeStack source)
 {
  int result=receiver.AddLevelExp(xp,cvar,type,bonus,notify,instigator,item); // Original call/return/exception exactly once.
  var f=current;try{var viewer=source?.xui?.playerUI?.entityPlayer;if(Current(f)&&ReferenceEquals(f.Source,source)&&!f.Awarded&&f.RawSeen&&f.Names==2&&f.FirstName==f.SecondName&&viewer!=null&&viewer.entityId==f.Original.Actor&&ReferenceEquals(receiver,viewer.Progression)&&cvar=="_xpFromCrafting"&&type==Progression.XPTypes.Crafting){f.Awarded=true;f.AwardItem=item;}else if(f!=null)f.Quarantined=true;}catch{if(f!=null)f.Quarantined=true;}return result;
 }
 internal static ItemStack OriginalDestination(ItemValue value,int count,XUiC_RecipeStack source)
 {
  var stack=new ItemStack(value,count);var f=current;
  try{if(Current(f)&&ReferenceEquals(source,f.Source)&&f.Awarded&&f.Early==null&&!f.Busy){f.Busy=true;try{if(!RebirthStationCompletionCapture.EarlyReceiptObservation.TryWrite(f.Original.Station,f.Original.Actor,value,f.FirstName,string.Empty,f.RawXp,count,out f.Early)||!Current(f))f.Quarantined=true;}finally{f.Busy=false;}}}catch{if(f!=null)f.Quarantined=true;}return stack;
 }
 internal static void OriginalReceipt(TileEntityWorkstation station,int actor,ItemValue output,string recipe,string scrap,int xp,int quantity,XUiC_RecipeStack source)
 {
  var f=current;
  try {
   if(Current(f)&&ReferenceEquals(source,f.Source)&&ReferenceEquals(station,f.Original.Station)&&actor==f.Original.Actor&&source.xui?.playerUI?.entityPlayer?.entityId!=actor&&!f.Busy&&f.Early==null)
   {
    f.Busy=true;
    try{if(RebirthStationCompletionCapture.EarlyReceiptObservation.TryWrite(station,actor,output,recipe,scrap,xp,quantity,out f.Early)){if(!Current(f))f.Quarantined=true;return;}if(f.Early!=null){f.Quarantined=true;return;}}
    finally{f.Busy=false;}
   }
  }catch{if(f!=null){f.Quarantined=true;if(f.Early!=null)return;}}
  station.AddCraftComplete(actor,output,recipe,scrap,xp,quantity); // Ordinary/unqualified original fallback once.
 }
 internal static bool TryConfirmBuffer(Frame f,RebirthStationOpenOutputObserverCandidate.ConfirmedOutput physical,out RebirthStationCompletionCapture.SuccessfulOutput completed)
 {completed=null;try{if(!Current(f)||f.Early==null||physical==null||!physical.TryTake(out var observed)||observed==null||!ReferenceEquals(observed.World,f.Original.World)||!ReferenceEquals(observed.Owner,f.Original.Owner)||!ReferenceEquals(observed.Progression,f.Original.Progression)||!ReferenceEquals(observed.Admission,f.Original.Admission)||!ReferenceEquals(observed.Queued,f.Original.Queued)||observed.Actor!=f.Original.Actor||observed.WorldGuid!=f.Original.WorldGuid||observed.SaveRoot!=f.Original.SaveRoot||!observed.IsCurrent()||!Current(f)||!f.Early.TryConfirmPhysical(out var candidate)||!Current(f)||!observed.IsCurrent())return false;completed=candidate;return true;}catch{completed=null;return false;}}
 internal static Exception FinalizeOriginal(Exception exception,Frame frame)
 {if(frame!=null){if(exception!=null){frame.Quarantined=true;frame.Early?.QuarantineOriginalException();}if(frame.Early!=null){retained.Remove(frame.Source);retained.Add(frame.Source,frame);}frame.Closed=true;if(ReferenceEquals(current,frame))current=frame.Previous;else frame.Quarantined=true;}return exception;}
}




