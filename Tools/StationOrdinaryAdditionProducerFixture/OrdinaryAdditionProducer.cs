using System;
using System.Linq;
// Tools-only original closed insertion/receipt observation candidate. No activation or attribution grants.
sealed class OrdinaryAdditionProducer {
 static readonly object issuer=new(); public sealed class Event {
  readonly ItemStack[] before,after;readonly RebirthStationCompletionOriginalScope original;bool consumed;
  internal Event(object key,ItemStack[] b,ItemStack[] a,RebirthStationCompletionOriginalScope o){if(!ReferenceEquals(key,issuer))throw new InvalidOperationException();before=b;after=a;original=o;}
  public bool TryConsume(out ItemStack[] b,out ItemStack[] a){b=null;a=null;if(consumed||!original.IsCurrent())return false;try{var cb=Copy(before);var ca=Copy(after);if(!Same(before,cb)||!Same(after,ca)||!original.IsCurrent())return false;consumed=true;b=cb;a=ca;return true;}catch{return false;}}
 }
 static ItemStack[] Copy(ItemStack[] a)=>a.Select(x=>x.Clone()).ToArray();
 static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Zip(b,(left,right)=>RebirthStationGridIngredients.IsSameStackSnapshot(left,right)).All(x=>x);
 static CraftCompleteData Clone(CraftCompleteData r)=>new(r.CrafterEntityID,r.CraftedItemStack.Clone(),r.RecipeName,r.ItemScrapped,r.CraftExpGain,r.RecipeUsedCount);
 static bool Rows(CraftCompleteData[] a,CraftCompleteData[] b)=>a.Length==b.Length&&a.Zip(b,(left,right)=>new[]{left,right}).All(x=>x[0].CrafterEntityID==x[1].CrafterEntityID&&x[0].RecipeName==x[1].RecipeName&&x[0].ItemScrapped==x[1].ItemScrapped&&x[0].CraftExpGain==x[1].CraftExpGain&&x[0].RecipeUsedCount==x[1].RecipeUsedCount&&RebirthStationGridIngredients.IsSameStackSnapshot(x[0].CraftedItemStack,x[1].CraftedItemStack));
 // Wrapper preserves native arguments and exception flow. Failure to observe never blocks vanilla insertion/receipt.
 public static int ExecuteClosed(RebirthStationCompletionOriginalScope anchor,TileEntityWorkstation s,RecipeQueueItem active,ItemValue value,out Event observation){
  observation=null;ItemStack[] before=null;CraftCompleteData[] receipts=null;Recipe recipe=active.Recipe;int actor=active.StartingEntityId,count=recipe.count,xp=recipe.craftExpGain;string name=recipe.GetName(),scrap=recipe.IsScrap?recipe.ingredients[0].itemValue.ItemClass.GetItemName():"";ItemValue frozen=null;bool qualified=false;
  try{qualified=anchor!=null&&ReferenceEquals(anchor.Station,s)&&anchor.IsCurrent()&&ReferenceEquals(s.Queue.LastOrDefault(),active)&&!RebirthStationGridQueue.IsMarked(recipe)&&count>0&&count<=65535; if(qualified){before=Copy(s.Output);receipts=(s.CraftCompleteList??new()).Select(Clone).ToArray();frozen=value.Clone();qualified=Same(s.Output,before)&&Rows((s.CraftCompleteList??new()).ToArray(),receipts)&&anchor.IsCurrent();}}catch{qualified=false;}
  int result=ItemStack.AddToItemStackArray(s.Output,new ItemStack(value,count));if(result==-1)return result;
  s.AddCraftComplete(active.StartingEntityId,value,active.Recipe.GetName(),active.Recipe.IsScrap?active.Recipe.ingredients[0].itemValue.ItemClass.GetItemName():"",active.Recipe.craftExpGain,active.Recipe.count);
  try{
   if(!qualified||!anchor.IsCurrent()||!ReferenceEquals(s.Queue.LastOrDefault(),active)||!ReferenceEquals(active.Recipe,recipe)||active.StartingEntityId!=actor||recipe.count!=count||recipe.craftExpGain!=xp||recipe.GetName()!=name||RebirthStationGridQueue.IsMarked(recipe)||!RebirthStationGridIngredients.IsSameStackSnapshot(new(value,count),new(frozen,count)))return result;
   var expected=receipts.Select(Clone).ToList();var target=expected.FirstOrDefault(r=>r.CraftedItemStack.itemValue.GetItemId()==value.GetItemId()&&r.ItemScrapped==scrap);if(target==null)expected.Add(new(actor,new(value.Clone(),count),name,scrap,xp,1));else target.CraftedItemStack.count+=count;
   var after=Copy(s.Output);if(!Rows(expected.ToArray(),(s.CraftCompleteList??new()).ToArray())||!Same(s.Output,after))return result;
   long delta=0;for(int i=0;i<before.Length;i++){if(after[i].count<before[i].count)return result;if(before[i].count>0&&!RebirthStationGridIngredients.IsSameStackSnapshot(new(before[i].itemValue,before[i].count),new(after[i].itemValue,before[i].count)))return result;if(before[i].count==0&&after[i].count>0&&!RebirthStationGridIngredients.IsSameStackSnapshot(new(after[i].itemValue,count),new(frozen,count)))return result;delta+=(long)after[i].count-before[i].count;}
   if(delta!=count||!anchor.IsCurrent()||!Same(s.Output,after)||!Rows(expected.ToArray(),(s.CraftCompleteList??new()).ToArray()))return result;
   observation=new(issuer,before,after,anchor);
  }catch{}return result;
 }
}




