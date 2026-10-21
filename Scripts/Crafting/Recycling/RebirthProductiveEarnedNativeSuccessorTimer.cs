using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal static bool DeferNativeSuccessorTimer(TileEntityWorkstation station,RecipeQueueItem row,float before,float target)
    {
        if(RebirthProductiveEarnedRemoteClient.DeferNativeOutputSuccessor(station,row,before,target))return true;
        if(!Enabled||!retained.TryGetValue(station,out var original)||!original.OutputTimerObserved||original.NativeOutput==null||
            !ReferenceEquals(original.NativeOutput,RebirthRecyclerNativeLoop.InvocationToken(station))||
            row?.Recipe==null||row.Multiplier<=0)return false;
        // Empty completed rows must receive their native increase so native
        // cycleRecipeQueue can calculate the exact carryover. A live successor
        // remains unworked until the prior full station terminal is saved.
        // This includes the increase on the SAME row after Multiplier--.
        int index=-1;
        for(int i=0;i<station.Queue.Length;i++)
            if(ReferenceEquals(station.Queue[i],row))
            {if(index!=-1){original.PendingTimerAmbiguous=true;return true;}index=i;}
        bool sameApplication=ReferenceEquals(original.Row,row)&&ReferenceEquals(original.Recipe,row.Recipe)&&
            row.Multiplier==original.Multiplier-1;
        // Native always emits the cycled-row write, including += 0 when no
        // debt remains. That no-op creates no successor work obligation.
        if(!sameApplication&&index>=0&&row.CraftingTimeLeft==before&&target==before)return false;
        if(index<0||float.IsNaN(before)||float.IsInfinity(before)||float.IsNaN(target)||float.IsInfinity(target)||
            row.CraftingTimeLeft!=before||
            (sameApplication?before>=0||row.OneItemCraftTime<=0||float.IsNaN(row.OneItemCraftTime)||
                float.IsInfinity(row.OneItemCraftTime)||target!=before+row.OneItemCraftTime:target>before))
        {original.PendingTimerAmbiguous=true;return true;}
        if(original.PendingTimerObserved&&(original.PendingTimerIndex!=index||original.PendingTimerBefore!=before||
            original.PendingTimerTarget!=target))
        {original.PendingTimerAmbiguous=true;return true;}
        if(!CapturePendingDefinition(original,row)){original.PendingTimerAmbiguous=true;return true;}
        original.PendingTimerObserved=true;original.PendingTimerIndex=index;
        original.PendingTimerBefore=before;original.PendingTimerTarget=target;return true;
    }

}
