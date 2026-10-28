using System;using System.Collections.Generic;using System.Linq;
// Native UI/player, eligibility, quantity resolution and source planning are
// adapters. This cohort proves source call routing, not native source payment.
class EntityPlayerLocal{}
class ItemValue{internal ItemValue Clone()=>new ItemValue();}
class ItemStack{internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue item,int amount){itemValue=item;count=amount;}}
class Recipe{internal List<ItemStack> ingredients=new List<ItemStack>{new ItemStack(new ItemValue(),1)};}
class XUiC_RecipeEntry{internal Recipe Recipe=new Recipe();}
class CountControl{internal int Count=4;}
class ItemActionEntryCraft{internal object ItemController=new XUiC_RecipeEntry();internal CountControl craftCountControl=new CountControl();internal int craftingTier=2;}
static class RemoteResourcesRuntimePolicy{internal static bool Enabled=true;}
static class RebirthCraftingIngredientQuantity{internal static bool Valid=true;internal static bool TryResolveTotal(EntityPlayerLocal p,Recipe recipe,ItemStack ingredient,int tier,int count,out int total){total=count*3;return Valid;}}
static class RemoteResourceClientAvailability{internal static int Invalidated;internal static void InvalidateAndRequest(EntityPlayerLocal p){Invalidated++;}}
static class RebirthRemoteResourceFetchedStationOriginal
{
 internal static bool ProtocolActive=true,Candidate=true,Attached;internal static int Began;internal static List<ItemStack> Full,Remote;
 internal static bool IsEntryCandidate(EntityPlayerLocal player,ItemActionEntryCraft entry)=>Candidate;
 internal static bool TryAttachOwningEntry(EntityPlayerLocal player,ItemActionEntryCraft entry)=>Attached;
 internal static bool BeginClient(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> full,List<ItemStack> remote){Began++;Full=full;Remote=remote;return true;}
}
static class RemoteResourceClientTransactionCoordinator
{
 internal static bool IsDedicatedClient=true,Local,Available=true;static ulong clientSessionEpoch=7;
 static bool HasAllLocal(EntityPlayerLocal player,List<ItemStack> full)=>Local;
 static bool TryBuildRemoteShortfall(EntityPlayerLocal player,List<ItemStack> full,out List<ItemStack> remote){remote=full.Select(value=>new ItemStack(value.itemValue.Clone(),value.count-1)).ToList();return Available;}
 static ulong NextRequestId()=>11;
 // ACTUAL_HELPER
}
static class Program
{
 static int assertions;static EntityPlayerLocal actor=new EntityPlayerLocal();static ItemActionEntryCraft entry;
 static void Require(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static void Reset(){entry=new ItemActionEntryCraft();RemoteResourcesRuntimePolicy.Enabled=true;RemoteResourceClientTransactionCoordinator.IsDedicatedClient=true;RemoteResourceClientTransactionCoordinator.Local=false;RemoteResourceClientTransactionCoordinator.Available=true;RebirthCraftingIngredientQuantity.Valid=true;RebirthRemoteResourceFetchedStationOriginal.ProtocolActive=true;RebirthRemoteResourceFetchedStationOriginal.Candidate=true;RebirthRemoteResourceFetchedStationOriginal.Attached=false;RebirthRemoteResourceFetchedStationOriginal.Began=0;RemoteResourceClientAvailability.Invalidated=0;}
 static bool Own()=>RemoteResourceClientTransactionCoordinator.TryOwnMobileResourceActivation(actor,entry);
 static void Main()
 {
  Reset();Require(Own()&&RebirthRemoteResourceFetchedStationOriginal.Began==1&&RebirthRemoteResourceFetchedStationOriginal.Full.Single().count==12&&RebirthRemoteResourceFetchedStationOriginal.Remote.Single().count==11,"Actual owning mobile boundary resolves complete modified requirements without vanilla tempIngredientList");
  Reset();RebirthRemoteResourceFetchedStationOriginal.Attached=true;Require(Own()&&RebirthRemoteResourceFetchedStationOriginal.Began==0,"Existing original reattaches without new source request");
  Reset();RemoteResourceClientTransactionCoordinator.Local=true;Require(!Own()&&RebirthRemoteResourceFetchedStationOriginal.Began==0,"Fully local mobile payment retains owning backend");
  Reset();RemoteResourceClientTransactionCoordinator.Available=false;Require(Own()&&RebirthRemoteResourceFetchedStationOriginal.Began==0&&RemoteResourceClientAvailability.Invalidated==1,"Unavailable nearby source cannot fall through to another payment route");
  Reset();RebirthCraftingIngredientQuantity.Valid=false;Require(Own()&&RebirthRemoteResourceFetchedStationOriginal.Began==0,"Unresolved exact modified total cannot issue original");
  Reset();RebirthRemoteResourceFetchedStationOriginal.Candidate=false;Require(!Own()&&RebirthRemoteResourceFetchedStationOriginal.Began==0,"Other established station architectures retain owning backend");
  Reset();RebirthRemoteResourceFetchedStationOriginal.ProtocolActive=false;Require(!Own(),"Inactive typed source cannot claim owning action");
  Reset();RemoteResourceClientTransactionCoordinator.IsDedicatedClient=false;Require(!Own(),"Authority/local primary does not enter remote owner transport");
  Console.WriteLine("Actual mobile fetched entry: "+assertions+" assertions passed; native lifecycle remains separate evidence.");
 }
}
