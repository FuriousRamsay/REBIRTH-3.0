// NOT RUN. Actual queue classifier/enum/DTO injected by parent hook runner.
// Native queue rows are explicit doubles; fuel ticking/save/network are not tested.
using System;
class Recipe {}
class RecipeQueueItem
{
    public Recipe Recipe;
    public int Multiplier;
    public float CraftingTimeLeft,OneItemCraftTime;
}
static class RebirthFuelQueueFixture
{
    // PRODUCTION_QUEUE_CLASSIFIER
    static RecipeQueueItem Row(float left,float one,int count=1)=>new RecipeQueueItem{Recipe=new Recipe(),Multiplier=count,CraftingTimeLeft=left,OneItemCraftTime=one};
    static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void Main()
    {
        var future=Row(20,20);var blocked=Row(0,5);
        var state=EvaluateQueue(new[]{future,blocked});
        Need(state.HasQueuedRecipe&&state.State==ProductiveWorkState.Blocked,"Future row authorized fuel for blocked active tail");
        state=EvaluateQueue(new[]{blocked,future});
        Need(state.State==ProductiveWorkState.Active,"Active tail hidden by future blocked row");
        state=EvaluateQueue(new[]{future,Row(0,0)});
        Need(state.State==ProductiveWorkState.Pending,"Uninitialized active tail misclassified");
        state=EvaluateQueue(new[]{future,Row(-5,5)});
        Need(state.State==ProductiveWorkState.Blocked,"Negative active carryover treated as future work");
        state=EvaluateQueue(new[]{future,null,new RecipeQueueItem(),Row(7,4,3),new RecipeQueueItem{Recipe=new Recipe(),Multiplier=0}});
        Need(state.State==ProductiveWorkState.Active&&Math.Abs(state.RemainingSeconds-35)<.001f,"Sparse tail/order/remaining count changed");
        Need(EvaluateQueue(null).State==ProductiveWorkState.Complete&&!EvaluateQueue(new RecipeQueueItem[0]).HasQueuedRecipe,"Empty queue fabricated work");
    }
}
