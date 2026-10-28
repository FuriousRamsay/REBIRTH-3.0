// AUTHORED_NOT_RUN. Actual callback; native original-grid methods are explicit boundaries.
using System;using System.Collections.Generic;
class EntityPlayerLocal{}class Recipe{}class ItemStack{public int count;}
static class RebirthBackpackLibraryReservation{internal static bool BlocksResourceUse(EntityPlayerLocal player)=>false;}
static class RebirthSurvivorUiText{internal static string L(string key,string text)=>text;}
static class Localization{internal static string Get(string key)=>key;}
class RebirthCookingPullContinuation{internal RebirthCookingPullContinuation(Func<bool> current,Func<bool> held,Action action){}}
class Station{internal bool UsesSharedMillingPresentation;}
class Subject
{
 public bool pulling=true,open=true,Direct=true;public int pullUiGeneration=4,batch=2;public Recipe selected=new Recipe();public string status="current window";public int Returned,Placed,Pulls;
 Station station=new Station();RebirthCookingPullContinuation pendingPullPlacement;
 bool IsCurrentSession(int generation,EntityPlayerLocal player)=>open&&generation==pullUiGeneration;
 bool TryPlaceDirectPulledIngredients(EntityPlayerLocal player,long request,List<ItemStack> needs,IList<ItemStack> items){if(request!=17)throw new Exception("wrong original request");if(Direct)Placed++;return Direct;}
 void ReturnPulledIngredients(EntityPlayerLocal player,IList<ItemStack> items,long request){if(request!=17)throw new Exception("wrong return request");foreach(var item in items)Returned+=item.count;}
 void Render(){}void CompleteSharedCraft(){}void PlaceSettledPulledIngredients(EntityPlayerLocal player,List<ItemStack> needs){Pulls++;}
 // PRODUCTION_METHOD
 public void Complete(int generation,Recipe recipe,int count,string error,bool current=true){CompleteIngredientPull(new EntityPlayerLocal(),17,new[]{new ItemStack{count=3}},error,generation,recipe,count,new List<ItemStack>(),()=>current);}
}
class Check
{
 static void Main(){for(int mode=0;mode<8;mode++){var s=new Subject();var recipe=s.selected;int generation=4,count=2;string error="";if(mode==1)s.open=false;if(mode==2)generation=2;if(mode==3)recipe=new Recipe();if(mode==4)count=1;if(mode==5)error="unavailable";if(mode==7)s.Direct=false;s.Complete(generation,recipe,count,error,mode!=6);if(s.pulling!=(mode==1||mode==2||mode==6)||s.Placed!=(mode==0?1:0)||s.Returned!=(mode==0?0:3))throw new Exception("callback original custody case "+mode);if((mode>=1&&mode<=4||mode==6)&&s.status!="current window")throw new Exception("stale reply changed presentation");if(mode==5&&s.status!=error)throw new Exception("current failure status");}Console.WriteLine("Actual cooking callback authored: exact original placement/return, closed/replaced selection/batch/life and no stale UI mutation; native grid service separately covered.");}
}
