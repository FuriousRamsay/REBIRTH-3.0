using System;using System.Linq;using System.Collections.Generic;
public class ItemValue{public int type=1;public bool Full=true;public string Metadata="original";public ItemValue Clone(){return (ItemValue)MemberwiseClone();}}
public class ItemStack{public ItemValue itemValue=new ItemValue();public int count=3;public bool IsEmpty(){return count<=0;}public ItemStack Clone(){return new ItemStack{count=count,itemValue=itemValue.Clone()};}}
public class XUiC_RebirthCookingSlot{public ItemStack ItemStack=new ItemStack();}
public class Recipe{public int craftingTier=1,count=2,craftingToolType;public float craftingTime;public bool UseIngredientModifier,materialBasedRecipe;public string tags="tags",wildcardForgeCategory="",wildcardCampfireCategory="";public object Effects;public List<ItemStack> ingredients=new List<ItemStack>();public void Write(Writer w){w.Write(count);w.Write(craftingTier);w.Write(craftingTime);w.Write(ingredients.Count);foreach(var i in ingredients){w.Write(i.count);w.Write(i.itemValue.Metadata);}}}
public class Writer:System.IO.BinaryWriter{public Writer():base(new System.IO.MemoryStream()){}public void SetBaseStream(System.IO.Stream s){OutStream=s;}}public class WriterPool{public Writer AllocSync(bool b){return new Writer();}}public static class MemoryPools{public static WriterPool poolBinaryWriter=new WriterPool();}
public class World{public EntityPlayerLocal Player;public object GetEntity(int i){return Player;}}
public class EntityPlayerLocal{public World world;public int entityId=10;}
public class GameManager{public static GameManager Instance=new GameManager();public World World;}
public class PlayerUI{public EntityPlayerLocal entityPlayer;}
public class XUi{public PlayerUI playerUI=new PlayerUI();}
public class Group{public bool isShowing=true;}public class XUiController{public Group windowGroup=new Group();public XUi xui=new XUi();}
public class RebirthCookingPreparation{public bool IsPreparing;}
public class QueueEntry{public Recipe Recipe;public Recipe GetRecipe(){return Recipe;}}public class Queue{public List<QueueEntry> Entries=new List<QueueEntry>{new QueueEntry()};public List<QueueEntry> GetRecipesToCraft(){return Entries;}}
public class XUiC_RebirthCookingStation{public Queue craftingQueue=new Queue();public bool IsMilling,Valid=true;public Action Callback;public bool CraftingRequirementsValid(Recipe r){Callback?.Invoke();return Valid;}public string CraftingRequirementsInvalidMessage(Recipe r){return "Needs fuel";}}
public static class RebirthBackpackLibraryReservation{public static bool Block;public static bool BlocksResourceUse(EntityPlayerLocal p){return Block;}}
public static class Localization{public static string Get(string k){return k;}}
public static class RebirthSurvivorUiText{public static string L(string k,string f){return f;}}
public static class RebirthCookingItemStats{public static bool FullIngredient(ItemValue v){return v.Full;}}
public static class RebirthStationGridIngredients{public class Allocation{public int Count=3;public ItemStack Snapshot(){return new ItemStack();}}public class Plan{public List<Allocation> Inputs=new List<Allocation>{new Allocation()};}public static bool Valid=true;public static int Calls;public static Action Callback;public static Action<ItemStack> CompareCallback;public static bool TryPlan(EntityPlayerLocal p,Recipe r,ItemStack[] g,int batch,int tier,out Plan plan){Calls++;Callback?.Invoke();plan=new Plan();return Valid;}public static bool IsSameStackSnapshot(ItemStack a,ItemStack b){CompareCallback?.Invoke(a);return a.count==b.count&&a.itemValue.type==b.itemValue.type&&a.itemValue.Full==b.itemValue.Full&&a.itemValue.Metadata==b.itemValue.Metadata;}}