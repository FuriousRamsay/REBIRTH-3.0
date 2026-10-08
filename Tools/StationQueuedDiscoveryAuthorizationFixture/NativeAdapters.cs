using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
class ItemClass{public int MaxCount=999;public bool HasQuality;public string Name="item";public static Dictionary<int,ItemClass> list=new(){{10,new ItemClass()}};public static ItemClass GetForId(int id)=>new();public string GetItemName()=>"scrap";} static class Block{public const int ItemsStartHere=1;}
class ItemValue{
 public int TextureFullArray;public bool IsShapeHelperBlock;public int type;public Dictionary<string,object> Metadata=new();public ItemClass ItemClass=new();
 public ItemValue(int t,int min=0,int max=0){type=t;}public int GetItemId()=>type;
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
class Recipe{public int itemValueType=10;public int count=2,craftExpGain=7;public bool IsScrap,Marked;public string Name="recipe";public List<ItemStack> ingredients=new(){new(new ItemValue(2),1)};public string GetName()=>Name;}
class RecipeQueueItem{public Recipe Recipe;public int StartingEntityId,Multiplier=1,Quality;public float CraftingTimeLeft=-5f,OneItemCraftTime=1f;}
class CraftCompleteData{
 public int CrafterEntityID,CraftExpGain;public ushort RecipeUsedCount;public string RecipeName,ItemScrapped;public ItemStack CraftedItemStack;
 public CraftCompleteData(int actor,ItemStack stack,string name,string scrap,int xp,ushort recipeUsedCount){RecipeUsedCount=recipeUsedCount;CrafterEntityID=actor;CraftedItemStack=stack;RecipeName=name;ItemScrapped=scrap;CraftExpGain=xp;}
}
partial class TileEntityWorkstation{
 public RecipeQueueItem[] Queue;public ItemStack[] Output={new(new ItemValue(0),0)};public List<CraftCompleteData> CraftCompleteList;
 public BlockStub block=new();public int NativeCalls,Modified;public bool ThrowModified,ThrowNative;
 public void SetModified(){Modified++;if(ThrowModified)throw new Exception("modified after write");}
 public void AddCraftComplete(int a,ItemValue v,string r,string s,int xp,int n){NativeCalls++;CraftCompleteList??=new();
 var row=CraftCompleteList.FirstOrDefault(x=>x.CraftedItemStack.itemValue.GetItemId()==v.GetItemId()&&x.ItemScrapped==s);
 if(row==null)CraftCompleteList.Add(new(a,new(v,n),r,s,xp,1));else row.CraftedItemStack.count+=n;
 if(ThrowNative)throw NativeFailure;}
 public static Exception NativeFailure=new Exception("original completion");public void HandleRecipeQueue(float time){}
}
public class RebirthStationGridAdmission{
 public string JobId="11111111111111111111111111111111",CreationId="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",DefinitionId=new string('A',64);public bool IsPublicationAttempted=true;
 public System.Xml.Linq.XElement Write()=>new("stationAdmission",new System.Xml.Linq.XAttribute("version",1),new System.Xml.Linq.XAttribute("phase",IsPublicationAttempted?"publicationAttempted":"prepared"),new System.Xml.Linq.XAttribute("job",JobId),new System.Xml.Linq.XAttribute("creation",CreationId),new System.Xml.Linq.XAttribute("definition",DefinitionId),new System.Xml.Linq.XAttribute("x",0),new System.Xml.Linq.XAttribute("y",0),new System.Xml.Linq.XAttribute("z",0),new System.Xml.Linq.XAttribute("block","station"));
 public RebirthStationGridAdmission Clone()=>new(){JobId=JobId,CreationId=CreationId,DefinitionId=DefinitionId,IsPublicationAttempted=IsPublicationAttempted};
 public bool TryMarkPublicationAttempted(out RebirthStationGridAdmission a){a=Clone();a.IsPublicationAttempted=true;return !IsPublicationAttempted;}
 public static bool TryReadStored(System.Xml.Linq.XElement n,out RebirthStationGridAdmission a){a=null;if(n?.Name!="stationAdmission")return false;a=new(){JobId=(string)n.Attribute("job"),CreationId=(string)n.Attribute("creation"),DefinitionId=(string)n.Attribute("definition"),IsPublicationAttempted=(string)n.Attribute("phase")=="publicationAttempted"};return true;}
}
static class RebirthStationGridQueue{public static bool MultiplierValid=true,DefinitionValid=true;public static bool HasValidMultiplier(Recipe r,int count)=>MultiplierValid;public static bool HasResolvedDefinition(Recipe r)=>DefinitionValid;public static bool IsMarked(Recipe r)=>r.Marked;public static bool TryGetJobId(Recipe r,out string job){job=r.Name;return r.Marked;}public static bool TryGetAdmittedSource(Recipe r,IList<Recipe> d,out Recipe source){source=r;return true;}}
static class RebirthStationObservationDispatcher{public static bool Verified=true;public static bool IsCurrentAuthorityThread(World w)=>Verified&&ReferenceEquals(GameManager.Instance.World,w);
 public static bool TryGetVerifiedQueuedAdmission(TileEntityWorkstation s,RecipeQueueItem q,out RebirthStationGridAdmission a){a=Verified?new(){JobId=q.Recipe.Name}:null;return Verified;}}
static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&a.itemValue.type==b.itemValue.type&&a.itemValue.Metadata.Count==b.itemValue.Metadata.Count&&a.itemValue.Metadata.All(p=>b.itemValue.Metadata.TryGetValue(p.Key,out var v)&&Equals(v,p.Value));}
static class RebirthStationCompletionReceipt{
 public const string Prefix="receipt.";public class Identity{public string Job;public int Count;}
 public static bool HasReservedMarker(CraftCompleteData r)=>r?.CraftedItemStack?.itemValue.Metadata.ContainsKey(Prefix+"job")==true;
 public static bool TryReadIdentity(CraftCompleteData r,out Identity i){i=null;if(!HasReservedMarker(r))return false;i=new(){Job=(string)r.CraftedItemStack.itemValue.Metadata[Prefix+"job"],Count=r.CraftedItemStack.count};return true;}
 public static bool HasNoJobReceipt(List<CraftCompleteData> rows,string job)=>rows==null||rows.All(r=>!HasReservedMarker(r)||(string)r.CraftedItemStack.itemValue.Metadata[Prefix+"job"]!=job);
 public static bool TryCreate(RebirthStationGridAdmission a,RecipeQueueItem q,ItemValue v,int count,out CraftCompleteData r){var copy=v.Clone();copy.Metadata[Prefix+"job"]=q.Recipe.Name;r=new(q.StartingEntityId,new(copy,count),q.Recipe.Name,"",q.Recipe.craftExpGain,1);return true;}
}
