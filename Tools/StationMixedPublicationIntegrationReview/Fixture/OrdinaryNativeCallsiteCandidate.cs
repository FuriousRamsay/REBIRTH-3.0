using System;using System.Linq;using System.Collections.Generic;
// TOOLS ONLY. No installer. One ordinary original insertion and native receipt writer, even when observation fails.
internal static class OrdinaryNativeCallsiteCandidate {
 internal sealed class Frame {
  internal RebirthStationCompletionOriginalScope Anchor;internal TileEntityWorkstation Station;internal RecipeQueueItem Active;internal Recipe Recipe;internal ItemStack[] Before;internal string[] BeforeSeal,ReceiptBeforeSeal;internal CraftCompleteData[] Receipts;internal ItemStack Inserted;internal bool Invalid;internal int Actor,Count,Xp;internal string Name,Scrap;
 }
 internal sealed class Addition {
  readonly ItemStack[] before,after;readonly string[] beforeSeal,afterSeal,receiptBeforeSeal,receiptAfterSeal;readonly RebirthStationCompletionOriginalScope anchor;readonly object iteration;readonly TileEntityWorkstation station;internal readonly string NativeRecipeName;internal readonly int NativeCrafter,Quantity;internal readonly long RetainedCost;bool consumed,consuming;
  internal Addition(object key,Frame frame,ItemStack[] after,long cost){if(!ReferenceEquals(key,issuer))throw new InvalidOperationException();RetainedCost=cost;before=frame.Before;this.after=after;beforeSeal=frame.BeforeSeal;afterSeal=Seal(after);receiptBeforeSeal=frame.ReceiptBeforeSeal;receiptAfterSeal=ReceiptSeal(Rows(frame.Station));anchor=frame.Anchor;iteration=frame;station=frame.Station;NativeRecipeName=frame.Name;NativeCrafter=frame.Actor;Quantity=frame.Count;if(!SealMatches(before,beforeSeal)||!SealMatches(after,afterSeal)||!anchor.IsCurrent()||IsInvalid(station))throw new InvalidOperationException();}
  internal bool MatchesOriginal(RebirthStationCompletionCapture.SuccessfulOutput e)=>e!=null&&ReferenceEquals(anchor,e.Original);
  internal bool TryConsumeEvidence(out ItemStack[] b,out ItemStack[] a,out string[] rb,out string[] ra){rb=null;ra=null;if(!TryConsume(out b,out a))return false;rb=(string[])receiptBeforeSeal.Clone();ra=(string[])receiptAfterSeal.Clone();return true;}
  internal bool SameOriginal(Addition other)=>other!=null&&ReferenceEquals(iteration,other.iteration);
  internal bool TryConsume(out ItemStack[] b,out ItemStack[] a){b=null;a=null;bool entered=false;try{if(consumed||consuming||IsInvalid(station)||!anchor.IsCurrent())return false;consuming=true;entered=true;var cb=Copy(before);var ca=Copy(after);if(consumed||!SealMatches(before,beforeSeal)||!SealMatches(after,afterSeal)||!Same(before,cb)||!Same(after,ca)||IsInvalid(station)||!anchor.IsCurrent())return false;consumed=true;b=cb;a=ca;return true;}catch{return false;}finally{if(entered)consuming=false;}}
 }
 static readonly object issuer=new object();
 [ThreadStatic]static Dictionary<TileEntityWorkstation,RebirthStationCompletionOriginalScope> anchors;
 [ThreadStatic]static Dictionary<TileEntityWorkstation,Frame> frames;
 [ThreadStatic]static Dictionary<TileEntityWorkstation,List<Addition>> additions;
 [ThreadStatic]static HashSet<TileEntityWorkstation> invalid;
 internal static bool Bind(RebirthStationCompletionCapture.SuccessfulOutput completed){try{if(completed==null||!completed.Original.IsCurrent()||!completed.MatchesReceipt()||!completed.Original.IsCurrent())return false;anchors=anchors??new Dictionary<TileEntityWorkstation,RebirthStationCompletionOriginalScope>();var s=completed.Original.Station;if(anchors.ContainsKey(s)&&!ReferenceEquals(anchors[s],completed.Original)||anchors.Count>=64&&!anchors.ContainsKey(s))return false;if(!completed.Original.IsCurrent())return false;anchors[s]=completed.Original;return true;}catch{return false;}}
 static void Invalidate(TileEntityWorkstation s){if(s==null)return;invalid=invalid??new HashSet<TileEntityWorkstation>();invalid.Add(s);if(frames!=null&&frames.TryGetValue(s,out var f))f.Invalid=true;}
 internal static bool IsInvalid(TileEntityWorkstation s)=>invalid!=null&&invalid.Contains(s);
 internal static Addition[] Pending(TileEntityWorkstation s)=>additions!=null&&additions.TryGetValue(s,out var a)?a.ToArray():new Addition[0];
 static ItemStack[] Copy(ItemStack[] a){if(a==null||a.Length<1||a.Length>255)throw new InvalidOperationException();var cloned=a.Select(x=>x.Clone()).ToArray();if(!Budget(cloned)||!Same(a,cloned))throw new InvalidOperationException();return cloned;}
 static bool Budget(params ItemStack[][] images){long ignored;return Measure(out ignored,images);}
 static bool Measure(out long chars,params ItemStack[][] images){chars=0;foreach(var image in images)foreach(var cell in image){if(cell==null||cell.itemValue==null||cell.count<0||cell.count>65535)return false;var text=RebirthNativeItemCodec.Encode(cell.itemValue);if(text==null||text.Length<1||text.Length>262144||(chars+=text.Length)>8*1024*1024)return false;}return true;}
 static string[] Seal(ItemStack[] a){return a.Select(x=>x.count.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+RebirthNativeItemCodec.Encode(x.itemValue)).ToArray();}
 static string[] ReceiptSeal(CraftCompleteData[] rows)=>rows.Select(r=>new System.Xml.Linq.XElement("nativeReceipt",new System.Xml.Linq.XAttribute("actor",r.CrafterEntityID),new System.Xml.Linq.XAttribute("recipe",r.RecipeName??""),new System.Xml.Linq.XAttribute("scrap",r.ItemScrapped??""),new System.Xml.Linq.XAttribute("xp",r.CraftExpGain),new System.Xml.Linq.XAttribute("used",r.RecipeUsedCount),new System.Xml.Linq.XAttribute("count",r.CraftedItemStack.count),new System.Xml.Linq.XAttribute("image",RebirthNativeItemCodec.Encode(r.CraftedItemStack.itemValue))).ToString(System.Xml.Linq.SaveOptions.DisableFormatting)).ToArray();
 internal static bool TrySnapshotReceiptImages(TileEntityWorkstation s,out string[] images){images=null;try{var list=s.CraftCompleteList;var originals=list?.ToArray()??new CraftCompleteData[0];var snapshots=Rows(s);var frozen=ReceiptSeal(snapshots);if(frozen.Any(x=>x.Length>262144)||frozen.Sum(x=>(long)x.Length)>8*1024*1024||!frozen.SequenceEqual(ReceiptSeal(snapshots))||!ReferenceEquals(list,s.CraftCompleteList)||(list?.Count??0)!=originals.Length||list!=null&&!list.Zip(originals,(a,b)=>ReferenceEquals(a,b)).All(x=>x)||!SameRows(originals,snapshots))return false;images=frozen;return true;}catch{return false;}}
 static bool SealMatches(ItemStack[] a,string[] seal){var candidate=Seal(a);return candidate.SequenceEqual(seal);}
 static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Zip(b,(l,r)=>RebirthStationGridIngredients.IsSameStackSnapshot(l,r)).All(x=>x);
 static CraftCompleteData Clone(CraftCompleteData r){var actor=r.CrafterEntityID;var name=r.RecipeName;var scrap=r.ItemScrapped;var xp=r.CraftExpGain;var used=r.RecipeUsedCount;var originalStack=r.CraftedItemStack;var stack=originalStack.Clone();var copy=new CraftCompleteData(actor,stack,name,scrap,xp,used);if(!ReferenceEquals(r.CraftedItemStack,originalStack)||!SameRows(new[]{r},new[]{copy}))throw new InvalidOperationException();return copy;}
 static CraftCompleteData[] Rows(TileEntityWorkstation s){if(s.CraftCompleteList==null)return new CraftCompleteData[0];if(s.CraftCompleteList.Count>255)throw new InvalidOperationException();var list=s.CraftCompleteList;var originals=list.ToArray();var clones=originals.Select(Clone).ToArray();if(!ReferenceEquals(list,s.CraftCompleteList)||list.Count!=originals.Length||!list.Zip(originals,(a,b)=>ReferenceEquals(a,b)).All(x=>x)||!SameRows(originals,clones))throw new InvalidOperationException();return clones;}
 static bool SameRows(CraftCompleteData[] a,CraftCompleteData[] b)=>a.Length==b.Length&&a.Zip(b,(l,r)=>l.CrafterEntityID==r.CrafterEntityID&&l.CraftExpGain==r.CraftExpGain&&l.RecipeUsedCount==r.RecipeUsedCount&&l.RecipeName==r.RecipeName&&l.ItemScrapped==r.ItemScrapped&&RebirthStationGridIngredients.IsSameStackSnapshot(l.CraftedItemStack,r.CraftedItemStack)).All(x=>x);
 // Matches exact original static insertion stack; only appended ldarg.0 supplies the actual native station.
 internal static int Insert(ItemStack[] output,ItemStack input,int max,TileEntityWorkstation s){Frame frame=null;try{frames=frames??new Dictionary<TileEntityWorkstation,Frame>();if(frames.ContainsKey(s)){Invalidate(s);}else if(anchors!=null&&anchors.TryGetValue(s,out var anchor)&&!IsInvalid(s)){var q=s.Queue.LastOrDefault();if(q!=null&&!RebirthStationGridQueue.IsMarked(q.Recipe)&&ReferenceEquals(output,s.Output)&&anchor.IsCurrent()){var r=q.Recipe;frame=new Frame{Anchor=anchor,Station=s,Active=q,Recipe=r,Actor=q.StartingEntityId,Count=r.count,Xp=r.craftExpGain,Name=r.GetName(),Scrap=r.IsScrap?r.ingredients[0].itemValue.ItemClass.GetItemName():"",Before=Copy(output),Receipts=Rows(s),Inserted=input.Clone()};if(input.count!=frame.Count||frame.Count<1||frame.Count>65535||!Same(output,frame.Before)||!SameRows(Rows(s),frame.Receipts)||!anchor.IsCurrent())frame=null;else{frame.BeforeSeal=Seal(frame.Before);frame.ReceiptBeforeSeal=ReceiptSeal(frame.Receipts);if(!Same(output,frame.Before)||!SealMatches(frame.Before,frame.BeforeSeal)||!anchor.IsCurrent())frame=null;else frames.Add(s,frame);}}}}catch{Invalidate(s);frame=null;}
  int result;try{result=ItemStack.AddToItemStackArray(output,input,max);}catch{Invalidate(s);throw;}
  if(result==-1){if(frame!=null&&frames.TryGetValue(s,out var current)&&ReferenceEquals(frame,current))frames.Remove(s);return result;}
  if(frame!=null&&(frame.Invalid||!frame.Anchor.IsCurrent()))Invalidate(s);if(frame==null&&anchors!=null&&anchors.ContainsKey(s)){try{if(!RebirthStationGridQueue.IsMarked(s.Queue.Last().Recipe))Invalidate(s);}catch{Invalidate(s);}}return result;
 }
 // Original instance receiver and all six args unchanged; reevaluated by original IL after insertion.
 internal static void Receipt(TileEntityWorkstation s,int actor,ItemValue value,string name,string scrap,int xp,int count){Frame frame=null;if(frames!=null)frames.TryGetValue(s,out frame);try{s.AddCraftComplete(actor,value,name,scrap,xp,count);}catch{Invalidate(s);throw;}
  try{if(frame==null)return;if(frame.Invalid||!frame.Anchor.IsCurrent()||!ReferenceEquals(s.Queue.LastOrDefault(),frame.Active)||!ReferenceEquals(frame.Active.Recipe,frame.Recipe)||actor!=frame.Actor||name!=frame.Name||scrap!=frame.Scrap||xp!=frame.Xp||count!=frame.Count||!RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(value,count),frame.Inserted)){Invalidate(s);return;}
   var expected=frame.Receipts.Select(Clone).ToList();var target=expected.FirstOrDefault(r=>r.CraftedItemStack.itemValue.GetItemId()==value.GetItemId()&&r.ItemScrapped==scrap);if(target==null)expected.Add(new CraftCompleteData(actor,new ItemStack(value.Clone(),count),name,scrap,xp,1));else target.CraftedItemStack.count+=count;
   var after=Copy(s.Output);long delta=0;for(int i=0;i<after.Length;i++){if(after[i].count<frame.Before[i].count)throw new InvalidOperationException();if(frame.Before[i].count>0&&!RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(frame.Before[i].itemValue,frame.Before[i].count),new ItemStack(after[i].itemValue,frame.Before[i].count)))throw new InvalidOperationException();if(frame.Before[i].count==0&&after[i].count>0&&!RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(after[i].itemValue,count),frame.Inserted))throw new InvalidOperationException();delta+=(long)after[i].count-frame.Before[i].count;}
   long cost;if(!Measure(out cost,frame.Before,after,new[]{frame.Inserted},expected.Select(x=>x.CraftedItemStack).ToArray())||!SealMatches(frame.Before,frame.BeforeSeal)||delta!=count||!SameRows(expected.ToArray(),Rows(s))||!Same(s.Output,after)||!frame.Anchor.IsCurrent()||frame.Invalid){Invalidate(s);return;}
   cost+=frame.BeforeSeal.Sum(x=>(long)x.Length)+Seal(after).Sum(x=>(long)x.Length)+frame.ReceiptBeforeSeal.Sum(x=>(long)x.Length)+ReceiptSeal(expected.ToArray()).Sum(x=>(long)x.Length);
   additions=additions??new Dictionary<TileEntityWorkstation,List<Addition>>();if(!additions.TryGetValue(s,out var list)){list=new List<Addition>();additions.Add(s,list);}if(!SealMatches(frame.Before,frame.BeforeSeal)||!ReceiptSeal(frame.Receipts).SequenceEqual(frame.ReceiptBeforeSeal)||!Same(s.Output,after)||!frame.Anchor.IsCurrent()||frame.Invalid||list.Count>=64||list.Sum(x=>x.RetainedCost)+cost>8*1024*1024){Invalidate(s);return;}var minted=new Addition(issuer,frame,after,cost);if(!SealMatches(frame.Before,frame.BeforeSeal)||!Same(s.Output,after)||!SameRows(expected.ToArray(),Rows(s))||!frame.Anchor.IsCurrent()||frame.Invalid){Invalidate(s);return;}list.Add(minted);
  }catch{Invalidate(s);}finally{if(frame!=null&&frames!=null&&frames.TryGetValue(s,out var current)&&ReferenceEquals(current,frame))frames.Remove(s);}
 }
 internal static Exception OriginalExit(TileEntityWorkstation s,Exception original){if(original!=null)Invalidate(s);AbandonOriginalExit(s);return original;}
 internal static void AbandonOriginalExit(TileEntityWorkstation s){if(frames!=null&&frames.ContainsKey(s)){Invalidate(s);frames.Remove(s);}}
}












