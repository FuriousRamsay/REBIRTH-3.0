using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
class ItemValue {
 public Dictionary<string,object> Metadata=new Dictionary<string,object>();public bool Empty;
 public bool IsEmpty()=>Empty;
 public bool TryGetMetadata<T>(string k,out T value){if(Metadata.TryGetValue(k,out var v)&&v is T t){value=t;return true;}value=default(T);return false;}
 public void SetMetadata<T>(string k,T value){Metadata[k]=value;}
 public ItemValue Copy()=>new ItemValue{Empty=Empty,Metadata=new Dictionary<string,object>(Metadata)};
}
class ItemStack {public ItemValue itemValue=new ItemValue();}
class Recipe {public string Name="potato";public int Output=1;public List<ItemStack> ingredients=new List<ItemStack>{new ItemStack()};}
class RecipeQueueItem {
 public Recipe Recipe;public ItemValue RepairItem;public int AmountToRepair,Multiplier=1,StartingEntityId=171,Quality=1;public float CraftingTimeLeft=20;public bool IsCrafting=true;
 public RecipeQueueItem Copy(){var r=(RecipeQueueItem)MemberwiseClone();if(Recipe!=null)r.Recipe=new Recipe{Name=Recipe.Name,Output=Recipe.Output,ingredients=Recipe.ingredients.Select(i=>new ItemStack{itemValue=i.itemValue.Copy()}).ToList()};if(RepairItem!=null)r.RepairItem=RepairItem.Copy();return r;}
}
static class RebirthCookingHeat {public static bool Managed(Recipe r)=>r!=null&&r.ingredients.Count>0&&r.ingredients[0].itemValue.Metadata.ContainsKey("held");}
static class RebirthCookingRemoteAdmission {public const string PendingKey="pending";}
static class RebirthCookingRemoteAdmissionBackend {
 public static Dictionary<string,RecipeQueueItem[]> Images=new Dictionary<string,RecipeQueueItem[]>();
 public static RecipeQueueItem[] ReadQueue(string name)=>Images[name].Select(r=>r.Copy()).ToArray();
 public static string Queue(RecipeQueueItem[] rows)=>string.Join("|",rows.Select(r=>r.StartingEntityId+":"+r.Multiplier+":"+r.Quality+":"+r.CraftingTimeLeft.ToString(CultureInfo.InvariantCulture)+":"+r.IsCrafting+":"+(r.Recipe==null?"empty":r.Recipe.Name+":"+r.Recipe.Output+":"+string.Join(",",r.Recipe.ingredients.Select(i=>string.Join(";",i.itemValue.Metadata.OrderBy(x=>x.Key).Select(x=>x.Key+"="+Convert.ToString(x.Value,CultureInfo.InvariantCulture))))))));
}
public static class AdmissionProgressFixture {
 static Guid op=Guid.NewGuid();static RecipeQueueItem[] baseline,live;
 static ItemValue Marker(RecipeQueueItem[] rows)=>rows[0].Recipe.ingredients[0].itemValue;
 static void Setup(){
  baseline=new[]{new RecipeQueueItem{Recipe=new Recipe()},new RecipeQueueItem{Recipe=null,Multiplier=0}};
  var m=Marker(baseline);m.SetMetadata("held",1);m.SetMetadata("rebirth.cooking.queue.token",op.ToString("N"));m.SetMetadata("pending",1);m.SetMetadata("rebirth.cooking.queue.duration",20f);m.SetMetadata("rebirth.cooking.queue.elapsed",0f);m.SetMetadata("rebirth.cooking.queue.overdue",0f);m.SetMetadata("registeredOwner","owner");
  live=baseline.Select(r=>r.Copy()).ToArray();live[0].CraftingTimeLeft=0;live[0].IsCrafting=false;Marker(live).SetMetadata("rebirth.cooking.queue.elapsed",20f);Marker(live).SetMetadata("rebirth.cooking.queue.reported",1f);Marker(live).SetMetadata("rebirth.cooking.delivery.outputs","meal");
 }
 static bool Match(bool committed,out string result){RebirthCookingRemoteAdmissionBackend.Images["base"]=baseline;RebirthCookingRemoteAdmissionBackend.Images["live"]=live;return RebirthCookingAdmissionProgress.TryMatch("base","live",op,committed,out result);}
 static void Need(bool value,string why){if(!value)throw new Exception(why);}
 public static void Run(){
  Setup();Need(Match(false,out var result)&&result.Contains("elapsed=20")&&result.Contains("delivery.outputs=meal"),"Same original progress/Take plan must survive");
  Setup();live[0].Multiplier=2;Need(!Match(false,out result),"Changed quantity accepted");
  Setup();live[0].Recipe.Name="steak";Need(!Match(false,out result),"Changed recipe accepted");
  Setup();live[0].Recipe.Output=2;Need(!Match(false,out result),"Changed output count accepted");
  Setup();live[0].StartingEntityId=999;Need(!Match(false,out result),"Changed actor accepted");
  Setup();Marker(live).SetMetadata("registeredOwner","foreign");Need(!Match(false,out result),"Changed cook identity accepted");
  Setup();Marker(live).SetMetadata("rebirth.cooking.queue.token",Guid.NewGuid().ToString("N"));Need(!Match(false,out result),"Foreign operation accepted");
  Setup();Marker(live).SetMetadata("pending",2);Need(!Match(false,out result),"Prepared request accepted committed phase");Need(Match(true,out result)&&result.Contains("pending=1"),"Uncertain request must retain its own paused projection");
  Setup();Marker(live).SetMetadata("pending",99);Need(!Match(true,out result),"Unknown payment phase accepted");
  Setup();Marker(live).SetMetadata("rebirth.cooking.queue.elapsed",float.NaN);Need(!Match(false,out result),"NaN progress accepted");
  Setup();Marker(live).SetMetadata("rebirth.cooking.queue.elapsed",21f);Need(!Match(false,out result),"Beyond-duration progress accepted");
  Setup();Marker(live).SetMetadata("ingredient","foreign");Need(!Match(false,out result),"Changed ingredient metadata accepted");
  Setup();live[1].StartingEntityId=-1;live[1].Quality=0;Need(Match(false,out result),"Harmless empty-row presentation rejected");
  Setup();live[1].Recipe=new Recipe{Name="foreign"};Need(!Match(false,out result),"Inserted second job accepted");
  Setup();live[1].RepairItem=new ItemValue();Need(!Match(false,out result),"Inserted repair accepted");
  Console.WriteLine("PASS actual admission progress matcher with explicit queue codec doubles: same original retained; recipe/quantity/output/actor/owner/token/phase/invalid-progress/foreign-job changes refused. Native gameplay NOT RUN.");
 }
}
