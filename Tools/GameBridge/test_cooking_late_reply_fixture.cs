using System;using System.Collections.Generic;
class EntityPlayerLocal{} class Recipe{} class ItemStack{public int count;}
class Subject {public bool pulling=true,open=true;public int pullUiGeneration=4,batch=2;public Recipe selected=new Recipe();public string status="current window";public int Returned,Pulls;
void ReturnPulledIngredients(EntityPlayerLocal p,IList<ItemStack> items){foreach(var i in items)Returned+=i.count;}
void Pull(){Pulls++;}
// PRODUCTION_METHOD
public void Complete(int generation,Recipe recipe,int count,string error){CompleteIngredientPull(new EntityPlayerLocal(),new[]{new ItemStack{count=3}},error,generation,recipe,count);}}
class Check{static void Main(){for(int mode=0;mode<6;mode++){var s=new Subject();var recipe=s.selected;int generation=4,count=2;string error="";if(mode==1)s.open=false;if(mode==2)generation=2;if(mode==3)recipe=new Recipe();if(mode==4)count=1;if(mode==5)error="unavailable";s.Complete(generation,recipe,count,error);if(s.Returned!=3||s.pulling||s.Pulls!=(mode==0?1:0))throw new Exception("receipt/resume case "+mode);if(mode>=1&&mode<=4&&s.status!="current window")throw new Exception("stale status overwrite");if(mode==5&&s.status!=error)throw new Exception("failure status");}Console.WriteLine("PASS: receipts retained; closed/reopened/changed recipe/batch replies cannot resume or overwrite UI; current success/failure handled");}}
