// TITLE_STATE
public struct Vector3i {}
public class World {}
public class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
public class Loot {public Vector3i ToWorldPos(){return new Vector3i();}}
public class XUiC_LootWindow {public Loot te=new Loot();public string lootContainerName="Storage";public int refreshes;public void RefreshBindings(){refreshes++;}}
public static class RebirthContainerRenameService {
 public static string CustomName="";public static string GetCustomName(World w,Vector3i p){return CustomName;}
// APPLY_TITLE
}
public static class Checks {
 static void Apply(XUiC_LootWindow w,RebirthLootTitleProjection s,string custom,string expected,int refreshes){RebirthContainerRenameService.CustomName=custom;RebirthContainerRenameService.ApplyCustomNameToLootWindow(w,s);if(w.lootContainerName!=expected||w.refreshes!=refreshes)throw new Exception("Expected "+expected+" refreshes="+refreshes+" got "+w.lootContainerName+"/"+w.refreshes);}
 public static void Main(){
 var w=new XUiC_LootWindow();var s=new RebirthLootTitleProjection();
 Apply(w,s,"","Storage",0);Apply(w,s,"Food","Food",1);Apply(w,s,"Food","Food",1);
 Apply(w,s,"Medicine","Medicine",2);Apply(w,s,"","Storage",3);Apply(w,s,null,"Storage",3);
 Apply(w,s,"Supplies","Supplies",4);w.lootContainerName="Native sign text";
 Apply(w,s,"Supplies","Supplies",5);Apply(w,s,"","Native sign text",6);
 Apply(w,s,"Old container","Old container",7);w.te=new Loot();w.lootContainerName="New container";
 Apply(w,s,"","New container",7);Apply(w,s,"Renamed new","Renamed new",8);
 s.Reset();w.lootContainerName="Reopened native title";Apply(w,s,"","Reopened native title",8);
 Apply(w,s,"Again","Again",9);s.Reset();w.te=null;Apply(w,s,"","Again",9);
 w.te=new Loot();w.lootContainerName="After close";Apply(w,s,"","After close",9);
 Console.WriteLine("PASS: live rename/clear, no redundant refresh, native sign update, target change and reopen");
 }
}
