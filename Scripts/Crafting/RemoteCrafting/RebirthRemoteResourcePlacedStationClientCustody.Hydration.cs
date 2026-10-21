using System;
using System.Linq;
using System.Reflection;

internal static partial class RebirthRemoteResourcePlacedStationClientCustody
{
    internal static bool ReleaseEndedNoPayment(RebirthRemoteResourcePlacedStationOriginal.EndedNeverEnteredClient proof)
    {
        if(proof?.Current!=true)return false;
        if(held.TryGetValue(proof.Original.Operation,out var hold))
        {if(hold.Original.Digest!=proof.Original.Digest)return false;held.Remove(proof.Original.Operation);}
        fresh.Remove(proof.Original.Operation);return true;
    }
    private sealed class HydrationWrite
    {internal TileEntityWorkstation Station;internal string Method;internal object Value;internal Func<bool> Current;internal bool Used;}
    [ThreadStatic]private static HydrationWrite hydrationWrite;
    private static bool ConsumeHydration(TileEntityWorkstation station,MethodBase method,object[] args)
    {
        var once=hydrationWrite;
        if(once==null||once.Used||!ReferenceEquals(once.Station,station)||method?.DeclaringType!=typeof(TileEntityWorkstation)||method.Name!=once.Method||
            args?.Length!=1||once.Current?.Invoke()!=true)return false;
        if(once.Value is bool flag?args[0] is not bool actual||actual!=flag:!ReferenceEquals(args[0],once.Value))return false;
        once.Used=true;return true;
    }
    private static bool WriteHydration(TileEntityWorkstation station,string method,object value,Func<bool> current,Action write)
    {
        if(hydrationWrite!=null||current?.Invoke()!=true)return false;
        var once=new HydrationWrite{Station=station,Method=method,Value=value,Current=current};hydrationWrite=once;
        try{write();return once.Used&&current();}finally{hydrationWrite=null;}
    }
    internal static bool ReleaseUsed(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!RebirthRemoteResourcePlacedStationOriginal.HasClientRetired(original,source,binding,proof))return false;
        if(held.TryGetValue(original.Operation,out var hold))
        {if(hold.Original.Digest!=original.Digest||hold.Current()&&!ReferenceEquals(hold.Player,player))return false;held.Remove(original.Operation);}
        fresh.Remove(original.Operation);return true;
    }
    internal static bool ReleaseNoPayment(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!RebirthRemoteResourceClientOriginal.HasNoPaymentTerminal(original,source,binding,proof))return false;
        if(held.TryGetValue(original.Operation,out var hold))
        {if(hold.Original.Digest!=original.Digest||hold.Current()&&!ReferenceEquals(hold.Player,player))return false;held.Remove(original.Operation);}
        fresh.Remove(original.Operation);return true;
    }
    // Authenticated Root4 projection only. The caller's durable ClientUsed
    // original must remain current throughout; this never writes owner items.
    internal static bool HydrateUsed(EntityPlayerLocal player,ItemActionEntryCraft entry,
        RebirthRemoteResourceClientOriginal.Intent original,byte[] fullAfter,RecipeQueueItem[] queue,Func<bool> authorized)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||authorized?.Invoke()!=true||!OwnsOriginal(player,original)||
                !held.TryGetValue(original.Operation,out var hold)||
                !RebirthRemoteResourcePlacedStationScope.TryObserveWindow(player,entry,out var station,out _)||!ReferenceEquals(station,hold.Station)||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(original.Copy().Element("action").Element("queue").Value,out var before)||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(fullAfter,out var after)||queue==null||station.Queue==null||queue.Length!=before.Length||queue.Length!=station.Queue.Length)return false;
            var window=entry.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>();
            var cards=window?.craftingQueue?.GetRecipesToCraft();if(cards==null||cards.Length!=queue.Length)return false;
            string Row(RecipeQueueItem row)=>RebirthRemoteResourcePlacedStationQueue.Encode(new[]{row});
            var changed=Enumerable.Range(0,queue.Length).Where(slot=>Row(before[slot])!=Row(queue[slot])).ToArray();
            if(changed.Length!=1)return false;int own=changed[0];
            // Preserve every unrelated native row/recipe object. Only the
            // original vacant slot may acquire this original's paid row.
            if(before[own].Recipe!=null||before[own].RepairItem!=null||before[own].AmountToRepair!=0||
                queue[own].Recipe==null||queue[own].RepairItem!=null||queue[own].AmountToRepair!=0||
                !queue[own].Recipe.ingredients.Any(item=>item?.itemValue!=null&&item.itemValue.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey,out string job)&&job==original.Operation.ToString("N")))return false;
            for(int slot=0;slot<queue.Length;slot++)
            {
                string actualRow=Row(station.Queue[slot]);if(actualRow!=Row(before[slot])&&actualRow!=Row(queue[slot]))return false;
                if(slot!=own&&actualRow!=Row(before[slot]))return false;
            }
            var next=(RecipeQueueItem[])station.Queue.Clone();next[own]=queue[own];
            // The full native image must agree before any setter, including
            // unchanged fuel/tools/completions/clock fields and the exact
            // authorized source-output delta. Input/queue hashes alone cannot
            // authorize terminal hydration.
            if(!RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,after.Input,after.Output,next,after.Burning,out var candidate,out _)||!candidate.SequenceEqual(fullAfter))return false;
            if(authorized()!=true||!OwnsOriginal(player,original))return false;
            bool Current()=>authorized()==true&&OwnsOriginal(player,original);
            if(!WriteHydration(station,"set_Input",after.Input,Current,()=>station.Input=after.Input)||
                !WriteHydration(station,"set_Output",after.Output,Current,()=>station.Output=after.Output)||
                !WriteHydration(station,"set_Queue",next,Current,()=>station.Queue=next)||
                !WriteHydration(station,"set_IsBurning",after.Burning,Current,()=>station.IsBurning=after.Burning))return false;
            // syncUIfromTE updates existing grid controllers; it does not
            // reconstruct unrelated queue cards. Native tick remains blocked.
            window.syncUIfromTE();var row=next[own];
            if(!cards[own].SetRecipe(row.Recipe,row.Multiplier,row.CraftingTimeLeft,true,row.StartingEntityId,row.Quality,row.OneItemCraftTime))return false;
            cards[own].IsCrafting=row.IsCrafting;cards[own].IsDirty=true;
            return authorized()==true&&OwnsOriginal(player,original)&&
                RebirthRemoteResourcePlacedStationQueue.Encode(station.Queue)==RebirthRemoteResourcePlacedStationQueue.Encode(queue)&&
                RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var actual,out _)&&actual.SequenceEqual(fullAfter);
        }
        catch{return false;}
    }
}
