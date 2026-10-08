using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
class ItemClass{public int MaxCount=999;public static ItemClass GetForId(int id)=>new();public string GetItemName()=>"scrap";} static class Block{public const int ItemsStartHere=1;}
class ItemValue{
 public int TextureFullArray;public bool IsShapeHelperBlock;public int type;public Dictionary<string,object> Metadata=new();public ItemClass ItemClass=new();
 public ItemValue(int t){type=t;}public int GetItemId()=>type;
 public ItemValue Clone(){var x=new ItemValue(type);foreach(var p in Metadata)x.Metadata.Add(p.Key,p.Value);return x;}
}
partial class ItemStack{
 public ItemValue itemValue;public int count;public static int Calls;public static bool Full,ThrowAfter,MutateContext;
 public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public ItemStack Clone()=>new(itemValue.Clone(),count);public bool IsEmpty()=>count<1||itemValue.type==0;public void Set(ItemValue v,int c){itemValue=v;count=c;}public bool CanStackPartly(ref int c)=>false;
 public static int AddToItemStackArray(ItemStack[] a,ItemStack s,int max=-1){Calls++;if(Full)return -1;
 int destination=NativeInsert(a,s,max);if(destination==-1)return -1;
 if(MutateContext)RebirthStationObservationDispatcher.Verified=false;
 if(ThrowAfter)throw NativeFailure;return destination;}
 public static Exception NativeFailure=new Exception("original insertion");
}
class Recipe{public int count=2,craftExpGain=7;public bool IsScrap,Marked;public string Name="recipe";public List<ItemStack> ingredients=new(){new(new ItemValue(2),1)};public string GetName()=>Name;}
class RecipeQueueItem{public Recipe Recipe;public int StartingEntityId;}
class CraftCompleteData{
 public int CrafterEntityID,CraftExpGain,RecipeUsedCount=1;public string RecipeName,ItemScrapped;public ItemStack CraftedItemStack;
 public CraftCompleteData(int actor,ItemStack stack,string name,string scrap,int xp){CrafterEntityID=actor;CraftedItemStack=stack;RecipeName=name;ItemScrapped=scrap;CraftExpGain=xp;}
}
class TileEntityWorkstation{
 public RecipeQueueItem[] Queue;public ItemStack[] Output={new(new ItemValue(0),0)};public List<CraftCompleteData> CraftCompleteList;
 public int NativeCalls,Modified;public bool ThrowModified,ThrowNative;
 public void SetModified(){Modified++;if(ThrowModified)throw new Exception("modified after write");}
 public void AddCraftComplete(int a,ItemValue v,string r,string s,int xp,int n){NativeCalls++;CraftCompleteList??=new();
 var row=CraftCompleteList.FirstOrDefault(x=>x.CraftedItemStack.itemValue.GetItemId()==v.GetItemId()&&x.ItemScrapped==s);
 if(row==null)CraftCompleteList.Add(new(a,new(v,n),r,s,xp));else row.CraftedItemStack.count+=n;
 if(ThrowNative)throw NativeFailure;}
 public static Exception NativeFailure=new Exception("original completion");public void HandleRecipeQueue(float time){}
}
class RebirthStationGridAdmission{public string JobId;}
static class RebirthStationGridQueue{public static bool IsMarked(Recipe r)=>r.Marked;}
static class RebirthStationObservationDispatcher{public static bool Verified=true;
 public static bool TryGetVerifiedQueuedAdmission(TileEntityWorkstation s,RecipeQueueItem q,out RebirthStationGridAdmission a){a=Verified?new(){JobId=q.Recipe.Name}:null;return Verified;}}
static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&a.itemValue.type==b.itemValue.type&&a.itemValue.Metadata.Count==b.itemValue.Metadata.Count&&a.itemValue.Metadata.All(p=>b.itemValue.Metadata.TryGetValue(p.Key,out var v)&&Equals(v,p.Value));}
static class RebirthStationCompletionReceipt{
 public const string Prefix="receipt.";public class Identity{public string Job;public int Count;}
 public static bool HasReservedMarker(CraftCompleteData r)=>r?.CraftedItemStack?.itemValue.Metadata.ContainsKey(Prefix+"job")==true;
 public static bool TryReadIdentity(CraftCompleteData r,out Identity i){i=null;if(!HasReservedMarker(r))return false;i=new(){Job=(string)r.CraftedItemStack.itemValue.Metadata[Prefix+"job"],Count=r.CraftedItemStack.count};return true;}
 public static bool HasNoJobReceipt(List<CraftCompleteData> rows,string job)=>rows==null||rows.All(r=>!HasReservedMarker(r)||(string)r.CraftedItemStack.itemValue.Metadata[Prefix+"job"]!=job);
 public static bool TryCreate(RebirthStationGridAdmission a,RecipeQueueItem q,ItemValue v,int count,out CraftCompleteData r){var copy=v.Clone();copy.Metadata[Prefix+"job"]=q.Recipe.Name;r=new(q.StartingEntityId,new(copy,count),q.Recipe.Name,"",q.Recipe.craftExpGain);return true;}
}
class Program{
 static int tests;static void Check(bool v,string name){tests++;if(!v)throw new Exception(name);}
 static TileEntityWorkstation Station(bool paid=true,int actor=1,string job="job") {ItemStack.Full=false;ItemStack.ThrowAfter=false;ItemStack.MutateContext=false;RebirthStationObservationDispatcher.Verified=true;return new(){Queue=new[]{new RecipeQueueItem{StartingEntityId=actor,Recipe=new(){Marked=paid,Name=job}}}};}
 static void Step(TileEntityWorkstation s){var q=s.Queue[0];var v=new ItemValue(10);int n=q.Recipe.count;
 if(RebirthStationPaidCompletionCallsite.Insert(s.Output,new(v,n),-1,s)!=-1)RebirthStationPaidCompletionCallsite.Complete(s,q.StartingEntityId,v,q.Recipe.GetName(),"",q.Recipe.craftExpGain,n);}
 static Exception Error(Action a){try{a();return null;}catch(Exception e){return e;}}
 static void Main(){
 var s=Station(false);Step(s);Check(s.NativeCalls==1&&s.Output[0].count==2,"ordinary original once");
 Step(s);Check(s.NativeCalls==2&&s.CraftCompleteList.Count==1&&s.CraftCompleteList[0].CraftedItemStack.count==4,"ordinary merged unchanged");
 s=Station();Step(s);Check(s.NativeCalls==0&&s.CraftCompleteList.Count==1&&s.CraftCompleteList[0].CrafterEntityID==1,"paid one marked");
 s.Queue[0]=new(){StartingEntityId=2,Recipe=new(){Marked=true,Name="job2"}};Step(s);Check(s.CraftCompleteList.Count==2&&s.CraftCompleteList[1].CrafterEntityID==2&&s.Output[0].count==4,"same output different actor separate receipts");
 s.Queue[0]=new(){StartingEntityId=3,Recipe=new(){Marked=false}};int calls=ItemStack.Calls;Check(Error(()=>Step(s))!=null&&ItemStack.Calls==calls&&s.Output[0].count==4,"ordinary collision before insertion");
 s=Station();ItemStack.Full=true;Step(s);Check(s.Output[0].count==0&&s.CraftCompleteList==null&&!RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"full no append no fault");ItemStack.Full=false;Step(s);Check(s.CraftCompleteList.Count==1,"full retry exact");
 s=Station();RebirthStationObservationDispatcher.Verified=false;calls=ItemStack.Calls;Check(Error(()=>Step(s))!=null&&ItemStack.Calls==calls,"unknown marked auth before insertion");
 s=Station();ItemStack.ThrowAfter=true;Check(ReferenceEquals(Error(()=>Step(s)),ItemStack.NativeFailure)&&s.Output[0].count==2,"native insertion exception preserved");
 ItemStack.ThrowAfter=false;calls=ItemStack.Calls;Check(Error(()=>Step(s))!=null&&ItemStack.Calls==calls&&RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"uncertain insert retry blocked");
 s=Station();s.ThrowModified=true;Check(Error(()=>Step(s))!=null&&s.CraftCompleteList.Count==1&&RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"receipt write uncertain retained");
 calls=ItemStack.Calls;Check(Error(()=>Step(s))!=null&&ItemStack.Calls==calls,"uncertain receipt retry blocked");
 s=Station();ItemStack.MutateContext=true;Check(Error(()=>Step(s))!=null&&s.Output[0].count==2&&s.CraftCompleteList==null&&RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"authority mutation after output retained");
 s=Station(false);s.ThrowNative=true;Check(ReferenceEquals(Error(()=>Step(s)),TileEntityWorkstation.NativeFailure)&&s.NativeCalls==1,"ordinary native exception exact");
 s=Station();var value=new ItemValue(10);RebirthStationPaidCompletionCallsite.Insert(s.Output,new(value,2),-1,s);s.Queue[0]=new(){StartingEntityId=2,Recipe=new(){Marked=true}};
 Check(Error(()=>RebirthStationPaidCompletionCallsite.Complete(s,1,value,"job","",7,2))!=null&&RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"queue swap after native output");
 Check(Error(()=>RebirthStationPaidCompletionCallsite.Transform(new List<CodeInstruction>(),typeof(TileEntityWorkstation).GetMethod("HandleRecipeQueue")))!=null,"unsupported module refuses transform");
 var same=typeof(RebirthStationPaidCompletionCallsite).GetMethod("SameInstructions",BindingFlags.NonPublic|BindingFlags.Static);
 var left=new List<CodeInstruction>{new(OpCodes.Ldc_I4_1),new(OpCodes.Ret)};var right=left.Select(c=>new CodeInstruction(c)).ToList();
 Check((bool)same.Invoke(null,new object[]{left,right}),"exact instruction match");right[0].opcode=OpCodes.Ldc_I4_2;Check(!(bool)same.Invoke(null,new object[]{left,right}),"changed opcode refused");
 s=Station(false);Step(s);var ordinary=s.CraftCompleteList[0];s.Queue[0]=new(){StartingEntityId=2,Recipe=new(){Marked=true,Name="paidafterordinary"}};Step(s);
 Check(s.NativeCalls==1&&s.CraftCompleteList.Count==2&&ReferenceEquals(s.CraftCompleteList[0],ordinary)&&ordinary.CraftedItemStack.count==2,"paid append preserves prior ordinary row");
 s=Station();Step(s);calls=ItemStack.Calls;Check(Error(()=>Step(s))!=null&&s.Output[0].count==2&&s.CraftCompleteList.Count==1&&!RebirthStationPaidCompletionCallsite.HasUncertainEffect(s)&&ItemStack.Calls==calls,"same job duplicate before output safe retry hold");
 s=Station();value=new ItemValue(10);RebirthStationPaidCompletionCallsite.Insert(s.Output,new(value,2),-1,s);
 var second=Station();calls=ItemStack.Calls;Check(Error(()=>Step(second))!=null&&ItemStack.Calls==calls&&RebirthStationPaidCompletionCallsite.HasUncertainEffect(s)&&RebirthStationPaidCompletionCallsite.HasUncertainEffect(second),"reentrant iteration retains both");
 // Consume the retained thread token without permitting a paid receipt.
 Check(Error(()=>RebirthStationPaidCompletionCallsite.Complete(s,1,value,"job","",7,2))!=null&&s.CraftCompleteList==null,"faulted iteration cannot append even exact original");
 var generator=new DynamicMethod("labels",typeof(void),Type.EmptyTypes).GetILGenerator();var l1=generator.DefineLabel();var l2=generator.DefineLabel();
 left=new(){new(OpCodes.Br_S,l1),new(OpCodes.Nop),new(OpCodes.Ret)};left[2].labels.Add(l1);
 right=new(){new(OpCodes.Br_S,l2),new(OpCodes.Nop),new(OpCodes.Ret)};right[2].labels.Add(l2);
 Check((bool)same.Invoke(null,new object[]{left,right}),"equivalent regenerated branch labels");right[2].labels.Clear();right[1].labels.Add(l2);
 Check(!(bool)same.Invoke(null,new object[]{left,right}),"changed branch target refused");
 s=Station();s.Output=new[]{new ItemStack(new ItemValue(10),1)};s.Output[0].itemValue.Metadata["foreign"]=1;calls=ItemStack.Calls;
 Check(Error(()=>Step(s))!=null&&ItemStack.Calls==calls&&s.Output[0].count==1&&!RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"metadata differing native merge preflight refuses before output");
 s.Output[0]=new(new ItemValue(0),0);Step(s);Check(s.Output[0].count==2&&s.CraftCompleteList.Count==1,"unsafe cleared output retries successfully");
 s=Station();s.Output=new[]{new ItemStack(new ItemValue(10),0)};calls=ItemStack.Calls;Check(Error(()=>Step(s))!=null&&ItemStack.Calls==calls&&!RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"retained empty native payload rejected beforeoutput");
 s=Station();s.Output=new[]{new ItemStack(new ItemValue(20),0)};calls=ItemStack.Calls;Check(Error(()=>Step(s))!=null&&ItemStack.Calls==calls,"different retained empty replacement rejected");
 s=Station();s.Output=new[]{new ItemStack(new ItemValue(20),999)};Step(s);Check(s.CraftCompleteList==null&&!RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"genuine full output native minusone");
 s=Station();value=new ItemValue(10);RebirthStationPaidCompletionCallsite.Insert(s.Output,new(value,2),-1,s);var exception=new Exception("arguments after insertion");
 var unrelated=Station();Check(ReferenceEquals(RebirthStationPaidCompletionCallsite.FinalizeIteration(unrelated,exception),exception)&&!RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"wrongstation finalizer preserves original token");
 Check(ReferenceEquals(RebirthStationPaidCompletionCallsite.FinalizeIteration(s,exception),exception)&&RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"exactstation abandoned output retains fault original exception");
 unrelated=Station();Step(unrelated);Check(unrelated.CraftCompleteList.Count==1,"retired abandoned token does not poison other station");
 s=Station();RebirthStationObservationDispatcher.Verified=false;Error(()=>Step(s));Check(ReferenceEquals(RebirthStationPaidCompletionCallsite.FinalizeIteration(s,exception),exception)&&!RebirthStationPaidCompletionCallsite.HasUncertainEffect(s),"preinsert refusal finalizer no uncertainty");
 Console.WriteLine($"PASS {tests} actual candidate + actual Capture/OutputDelta; native auth/admission/item/receipt adapters, no installed transform activation.");
 }
}