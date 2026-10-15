using System;
using System.Collections.Generic;

// Original native output observations only. No output replay, durable completion
// acknowledgement or reward grant is authorized by this capture.
internal static class RebirthCamperStationCompletionCapture
{
    private static readonly object mintAuthority=new object();
    internal sealed class Observation
    {
        internal readonly RebirthCamperStationQueuedAuthority.Binding Binding;
        internal readonly RecipeQueueItem Active;
        internal readonly Recipe Recipe;
        internal readonly ItemValue NativeOutput;
        internal readonly ItemStack[] Before;
        internal readonly CraftCompleteData Receipt;
        internal readonly int Thread;
        internal bool Consumed;
        internal Observation(RebirthCamperStationQueuedAuthority.Binding binding,RecipeQueueItem active,
            ItemValue output,ItemStack[] before,CraftCompleteData receipt,object authority)
        {if(!ReferenceEquals(authority,mintAuthority))throw new InvalidOperationException("Unissued mobile output observation.");Binding=binding;Active=active;Recipe=active.Recipe;NativeOutput=output;Before=before;Receipt=receipt;Thread=Environment.CurrentManagedThreadId;}
    }
    internal sealed class SuccessfulOutput
    {
        internal readonly RebirthCamperStationQueuedAuthority.Binding Binding;
        private readonly ItemStack[] before,after;
        private readonly CraftCompleteData receipt;
        internal SuccessfulOutput(Observation observation,ItemStack[] final,object authority)
        {
            if(!ReferenceEquals(authority,mintAuthority)||!observation.Consumed)throw new InvalidOperationException("Unissued mobile completion.");
            Binding=observation.Binding;before=Clone(observation.Before);after=Clone(final);
            var row=observation.Receipt;
            receipt=new CraftCompleteData(row.CrafterEntityID,row.CraftedItemStack.Clone(),row.RecipeName,row.ItemScrapped,row.CraftExpGain,row.RecipeUsedCount);
        }
        internal bool MatchesLiveOutput()
        {
            try
            {
                if(!ThreadManager.IsMainThread()||!Binding.IsProcessingCurrent()||Binding.Station.Output?.Length!=after.Length||
                    !RebirthStationOutputDelta.Matches(before,after,receipt))return false;
                for(int i=0;i<after.Length;i++)
                    if(!RebirthStationGridIngredients.IsSameStackSnapshot(after[i],Binding.Station.Output[i]))return false;
                int matches=0;
                foreach(var row in Binding.Station.CraftCompleteList)
                {
                    if(!RebirthStationCompletionReceipt.HasReservedMarker(row))continue;
                    if(!RebirthStationCompletionReceipt.TryReadIdentity(row,out var identity))return false;
                    if(identity.Job!=Binding.Admission.JobId)continue;
                    if(++matches!=1||row.CrafterEntityID!=receipt.CrafterEntityID||row.RecipeName!=receipt.RecipeName||
                        row.ItemScrapped!=receipt.ItemScrapped||row.CraftExpGain!=receipt.CraftExpGain||row.RecipeUsedCount!=receipt.RecipeUsedCount||
                        !RebirthStationGridIngredients.IsSameStackSnapshot(row.CraftedItemStack,receipt.CraftedItemStack))return false;
                }
                return matches==1;
            }
            catch{return false;}
        }
        // Detached handoff only. Caller still owns native invocation, player
        // save confirmation and retirement; this never mutates the held row.
        internal bool TryGetNativeReward(RebirthCamperStationTrainingPlan plan,out CraftCompleteData reward)
        {
            reward=null;
            try
            {
                if(plan==null||!ReferenceEquals(plan.Completed,this)||!ReferenceEquals(plan.Binding,Binding)||
                    !plan.IsCurrent()||!MatchesLiveOutput()||
                    !Binding.Progression.StationTerminalIntents.TryGetValue(Binding.Admission.JobId,out var intent)||intent==null||
                    !RebirthStationTerminalIntent.TryRead(intent.Write(),Binding.Admission,out var choice)||!choice.IsCompletion||
                    (int)choice.Write().Attribute("actor")!=Binding.Player.entityId||
                    !RebirthWorldCharacterRepository.HasSavedStationTerminalIntent(Binding.Identity,Binding.Admission,choice)||
                    !RebirthCamperStationTrainingRecord.TryCreate(plan,out var training)||
                    !RebirthWorldCharacterRepository.HasSavedCamperStationTrainingPlan(Binding.Identity,Binding.Admission,
                        Binding.Publication,plan.Publication,training,plan.HasTraining))return false;
                var stack=receipt.CraftedItemStack.Clone();
                var reserved=new List<string>();
                if(stack.itemValue.Metadata!=null)
                    foreach(var key in stack.itemValue.Metadata.Keys)
                        if(key!=null&&(key.StartsWith(RebirthStationCompletionReceipt.Prefix,StringComparison.Ordinal)||
                            key=="rebirth.cooking.token"))reserved.Add(key);
                foreach(var key in reserved)stack.itemValue.Metadata.Remove(key);
                var detached=new CraftCompleteData(Binding.Player.entityId,stack,receipt.RecipeName,
                    receipt.ItemScrapped,receipt.CraftExpGain,receipt.RecipeUsedCount);
                if(!TryAggregateReward(Binding.Admission,detached,out detached))return false;
                if(RebirthStationCompletionReceipt.HasReservedMarker(detached)||!plan.IsCurrent()||!MatchesLiveOutput())return false;
                reward=detached;return true;
            }
            catch{return false;}
        }
        internal bool MatchesRetiredOutput(RebirthCamperStationState state)
        {
            try
            {
                if(!ThreadManager.IsMainThread()||state==null||state.InstallationId!=Binding.Address.Installation||
                    state.Output?.Length!=after.Length||state.Queue==null||
                    !RebirthStationOutputDelta.Matches(before,after,receipt)||
                    !RebirthStationCompletionReceipt.HasNoJobReceipt(state.CraftCompleteList,Binding.Admission.JobId))return false;
                for(int i=0;i<after.Length;i++)
                    if(!RebirthStationGridIngredients.IsSameStackSnapshot(after[i],state.Output[i]))return false;
                foreach(var queued in state.Queue)
                    if(queued?.Recipe!=null&&RebirthStationGridQueue.TryGetJobId(queued.Recipe,out var job)&&job==Binding.Admission.JobId)return false;
                return true;
            }
            catch{return false;}
        }
        internal bool MatchesSavedOutput(RebirthCamperStationState saved)
        {
            try
            {
                if(!ThreadManager.IsMainThread()||saved==null||saved.InstallationId!=Binding.Address.Installation||
                    saved.InstalledBlock.Block.GetBlockName()!=(string)Binding.Admission.WritePaymentImage().Attribute("block")||
                    saved.Output?.Length!=after.Length||saved.Queue==null||saved.CraftCompleteList==null||
                    !RebirthStationOutputDelta.Matches(before,after,receipt))return false;
                for(int i=0;i<after.Length;i++)
                    if(!RebirthStationGridIngredients.IsSameStackSnapshot(after[i],saved.Output[i]))return false;
                foreach(var entry in saved.Queue)
                {
                    if(entry==null)return false;
                    if(!RebirthStationGridQueue.IsMarked(entry.Recipe))continue;
                    if(!RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var job)||job==Binding.Admission.JobId)return false;
                }
                int matches=0;
                foreach(var row in saved.CraftCompleteList)
                {
                    if(!RebirthStationCompletionReceipt.HasReservedMarker(row))continue;
                    if(!RebirthStationCompletionReceipt.TryReadIdentity(row,out var identity))return false;
                    if(identity.Job!=Binding.Admission.JobId)continue;
                    if(++matches!=1||row.CrafterEntityID!=receipt.CrafterEntityID||row.RecipeName!=receipt.RecipeName||
                        row.ItemScrapped!=receipt.ItemScrapped||row.CraftExpGain!=receipt.CraftExpGain||row.RecipeUsedCount!=receipt.RecipeUsedCount||
                        !RebirthStationGridIngredients.IsSameStackSnapshot(row.CraftedItemStack,receipt.CraftedItemStack))return false;
                }
                return matches==1;
            }
            catch{return false;}
        }
    }
    private static ItemStack[] Clone(ItemStack[] items)
    {
        if(items==null||items.Length<1||items.Length>255)throw new InvalidOperationException("Invalid station output.");
        var result=new ItemStack[items.Length];
        for(int i=0;i<items.Length;i++)
        {
            if(items[i]?.itemValue==null||items[i].count<0)throw new InvalidOperationException("Invalid output cell.");
            result[i]=items[i].Clone();
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(items[i],result[i]))throw new InvalidOperationException("Output clone changed.");
        }
        return result;
    }
    internal static bool TryObserveBefore(RebirthCamperStationState station,ItemStack incoming,int maxItemCount,out Observation observation)
    {
        observation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||station?.Queue==null||station.Queue.Length==0||incoming?.itemValue==null)return false;
            var active=station.Queue[station.Queue.Length-1];
            if(!RebirthCamperStationQueuedAuthority.TryGetCurrent(station,active,out var binding)||
                !RebirthStationCompletionReceipt.TryCreate(binding.Admission,active,incoming.itemValue,incoming.count,out var receipt)||
                !RebirthStationCompletionReceipt.HasNoJobReceipt(station.CraftCompleteList,binding.Admission.JobId)||
                !RebirthStationPaidOutputPreflight.TryConform(station.Output,incoming,maxItemCount,receipt))return false;
            var before=Clone(station.Output);
            if(!binding.IsProcessingCurrent())return false;
            observation=new Observation(binding,active,incoming.itemValue,before,receipt,mintAuthority);return true;
        }
        catch{return false;}
    }
    internal static bool TryAppend(Observation observation,int actor,ItemValue output,string recipe,string scrapped,
        int experience,int count,out SuccessfulOutput completed)
    {
        completed=null;
        try
        {
            if(observation==null||observation.Consumed||!ThreadManager.IsMainThread()||
                observation.Thread!=Environment.CurrentManagedThreadId||!observation.Binding.IsProcessingCurrent())return false;
            var station=observation.Binding.Station;var row=observation.Receipt;
            if(station.Queue==null||station.Queue.Length==0||!ReferenceEquals(station.Queue[station.Queue.Length-1],observation.Active)||
                !ReferenceEquals(observation.Active.Recipe,observation.Recipe)||!ReferenceEquals(output,observation.NativeOutput)||
                actor!=row.CrafterEntityID||recipe!=row.RecipeName||scrapped!=row.ItemScrapped||experience!=row.CraftExpGain||count!=row.CraftedItemStack.count||
                !RebirthStationOutputDelta.Matches(observation.Before,station.Output,row)||
                !RebirthStationCompletionReceipt.HasNoJobReceipt(station.CraftCompleteList,observation.Binding.Admission.JobId)||
                station.CraftCompleteList!=null&&station.CraftCompleteList.Count>=short.MaxValue)return false;
            var after=Clone(station.Output);
            var replacement=station.CraftCompleteList==null?new List<CraftCompleteData>():new List<CraftCompleteData>(station.CraftCompleteList);
            replacement.Add(row);
            observation.Consumed=true;
            station.CraftCompleteList=replacement;station.SetModified();
            var candidate=new SuccessfulOutput(observation,after,mintAuthority);
            if(!candidate.MatchesLiveOutput())return false;
            completed=candidate;return true;
        }
        catch{return false;}
    }
    internal static bool TryAppendIntermediate(Observation observation,int actor,ItemValue output,string recipe,string scrapped,
        int experience,int count)
    {
        try
        {
            if(observation==null||observation.Consumed||!ThreadManager.IsMainThread()||
                observation.Thread!=Environment.CurrentManagedThreadId||!observation.Binding.IsProcessingCurrent()||
                !observation.Binding.Admission.IsPerBatch||observation.Active.Multiplier<=1)return false;
            var station=observation.Binding.Station;var row=observation.Receipt;
            if(station.Queue==null||station.Queue.Length==0||!ReferenceEquals(station.Queue[station.Queue.Length-1],observation.Active)||
                !ReferenceEquals(observation.Active.Recipe,observation.Recipe)||!ReferenceEquals(output,observation.NativeOutput)||
                actor!=row.CrafterEntityID||recipe!=row.RecipeName||scrapped!=row.ItemScrapped||experience!=row.CraftExpGain||count!=row.CraftedItemStack.count||
                !RebirthStationOutputDelta.Matches(observation.Before,station.Output,row)||
                !RebirthStationCompletionReceipt.HasNoJobReceipt(station.CraftCompleteList,observation.Binding.Admission.JobId))return false;
            // Native output is already inserted. Leave the native remaining
            // multiplier as durable progress; terminal rewards settle once.
            observation.Consumed=true;station.SetModified();return true;
        }
        catch{return false;}
    }
    internal static bool TryAggregateReward(RebirthStationGridAdmission admission,CraftCompleteData oneBatch,out CraftCompleteData reward)
    {
        reward=null;
        if(admission==null||oneBatch?.CraftedItemStack?.itemValue==null)return false;
        int cycles=admission.IsPerBatch?admission.OriginalBatches:1;
        long outputs=(long)oneBatch.CraftedItemStack.count*cycles,xp=(long)oneBatch.CraftExpGain*cycles;
        if(cycles<1||cycles>9999||outputs<1||outputs>int.MaxValue||xp<0||xp>int.MaxValue)return false;
        var stack=oneBatch.CraftedItemStack.Clone();stack.count=(int)outputs;
        reward=new CraftCompleteData(oneBatch.CrafterEntityID,stack,oneBatch.RecipeName,oneBatch.ItemScrapped,(int)xp,
            checked((ushort)(admission.IsPerBatch?cycles:oneBatch.RecipeUsedCount)));return true;
    }
}
