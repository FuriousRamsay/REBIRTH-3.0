function Add-NativeQueueFixture([string]$base,[string]$root){
$recipe=[IO.File]::ReadAllText((Join-Path $root 'Tools/TheorySoloFixture/native/Recipe.cs'))
$queue=[IO.File]::ReadAllText((Join-Path $root 'Tools/TheorySoloFixture/native/RecipeQueueItem.cs'))
$r='public class Recipe{public ushort Version=1;public int itemValueType=1,count=1,craftExpGain;public bool IsScrap;public int craftingToolType,craftingTier;public string tags="";public float craftingTime;public string craftingArea="";public List<ItemStack> ingredients=new List<ItemStack>();public ItemClass GetOutputItemClass(){return ItemClass.GetForId(itemValueType);}'+(Member $recipe 'public void Write(PooledBinaryWriter')+(Member $recipe 'public static Recipe Read(PooledBinaryReader')+(Member $recipe 'public void AddIngredients(List<ItemStack>')+'}'
if($base.Contains('public class Recipe{}')){$base=$base.Replace('public class Recipe{}',$r)}else{$base+=$r}
$base=$base.Replace('public class ItemClass{','public class ItemClass{public int Id;public float ScrapTimeOverride,CraftComponentTime=1;public bool HasAnyTags(string tag){return false;}public static ItemClass GetForId(int i){return i>=0&&i<list.Length?list[i]:null;}')
$base=$base.Replace('public class ItemStack{','public class ItemStack{public ItemStack(){}public ItemStack(ItemValue v,int c){itemValue=v;count=c;}')
$base+='public class RecipeQueueItem{public Recipe Recipe;public short Multiplier;public float CraftingTimeLeft,OneItemCraftTime=-1f;public bool IsCrafting;public ItemValue RepairItem;public ushort AmountToRepair;public byte Quality;public int StartingEntityId=-1;'+(Member $queue 'public void Write(PooledBinaryWriter')+(Member $queue 'public void Read(PooledBinaryReader')+'}'
return $base
}