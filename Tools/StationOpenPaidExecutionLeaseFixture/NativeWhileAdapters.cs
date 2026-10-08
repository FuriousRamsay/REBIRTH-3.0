partial class TileEntityWorkstation
{
 public bool bUserAccessing,isBurning=true;public bool[] isModuleUsed=new bool[4];private RecipeQueueItem[] queue=>Queue;private ItemStack[] output=>Output;
 private bool hasRecipeInQueue()=>System.Linq.Enumerable.Any(Queue,q=>q?.Recipe!=null);
 private void cycleRecipeQueue(){for(int i=Queue.Length-1;i>0;i--)Queue[i]=Queue[i-1];Queue[0]=new RecipeQueueItem{Multiplier=0,Recipe=null,CraftingTimeLeft=0};}
}
static class GameSparksCollector{public enum GSDataKey{CraftedItems}public static bool Throw;public static void IncrementCounter(GSDataKey key,string name,int count){if(Throw)throw Failure;}public static System.Exception Failure=new("original telemetry after receipt");}
