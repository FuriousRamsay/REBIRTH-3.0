using System;

// Native receipt capture helpers. No physical output mutation or reward settlement.
internal static class RebirthStationCompletionCapture
{
    private static readonly object mintAuthority=new object();
    // Session-local, one-use observation. This is not durable terminal evidence.
    internal sealed class OutputObservation
    {
        private readonly TileEntityWorkstation station;
        private readonly RecipeQueueItem active;
        private readonly Recipe recipe;
        private readonly ItemStack[] before;
        private readonly CraftCompleteData expected;
        private readonly int thread;
        private bool consumed;
        private readonly RebirthStationCompletionOriginalScope original;
        internal OutputObservation(TileEntityWorkstation station,RecipeQueueItem active,ItemStack[] before,CraftCompleteData expected,RebirthStationCompletionOriginalScope original,object authority)
        {if(!ReferenceEquals(authority,mintAuthority))throw new InvalidOperationException("Unissued output observation.");this.station=station;this.active=active;recipe=active.Recipe;this.before=before;this.expected=expected;this.original=original;thread=Environment.CurrentManagedThreadId;}
        internal bool Matches(TileEntityWorkstation current,CraftCompleteData receipt)
        {
            return original!=null&&original.IsCurrent()&&!consumed&&thread==Environment.CurrentManagedThreadId&&ReferenceEquals(station,current)&&
                current.Queue!=null&&current.Queue.Length>0&&ReferenceEquals(active,current.Queue[current.Queue.Length-1])&&
                ReferenceEquals(recipe,active.Recipe)&&expected.CrafterEntityID==receipt.CrafterEntityID&&
                expected.RecipeName==receipt.RecipeName&&expected.ItemScrapped==receipt.ItemScrapped&&
                expected.CraftExpGain==receipt.CraftExpGain&&expected.RecipeUsedCount==receipt.RecipeUsedCount&&
                RebirthStationGridIngredients.IsSameStackSnapshot(expected.CraftedItemStack,receipt.CraftedItemStack)&&
                RebirthStationOutputDelta.Matches(before,current.Output,receipt);
        }
        private bool issued;
        internal bool CanIssue(CraftCompleteData row)
        {
            if(issued||!consumed||original==null||!original.IsCurrent()||row==null||
                station.Queue==null||station.Queue.Length==0||!ReferenceEquals(active,station.Queue[station.Queue.Length-1])||
                !ReferenceEquals(recipe,active.Recipe)||expected.CrafterEntityID!=row.CrafterEntityID||expected.RecipeName!=row.RecipeName||
                expected.ItemScrapped!=row.ItemScrapped||expected.CraftExpGain!=row.CraftExpGain||expected.RecipeUsedCount!=row.RecipeUsedCount||
                !RebirthStationGridIngredients.IsSameStackSnapshot(expected.CraftedItemStack,row.CraftedItemStack)||
                !RebirthStationOutputDelta.Matches(before,station.Output,row))return false;
            return true;
        }
                // Candidate: freeze only issued original delta; caller cannot supply replacement snapshots.
        internal bool TryCopyIssuedDelta(CraftCompleteData row,out ItemStack[] frozenBefore,out ItemStack[] frozenAfter)
        {
            frozenBefore=null;frozenAfter=null;
            if(issued||!consumed||thread!=Environment.CurrentManagedThreadId||original==null||!original.IsCurrent()||
                !ReferenceEquals(station,original.Station)||!RebirthStationOutputDelta.Matches(before,station.Output,row)||
                !TryClonePair(before,station.Output,out var b,out var a)||
                !RebirthStationOutputDelta.Matches(b,a,row)||!original.IsCurrent())return false;
            frozenBefore=b;frozenAfter=a;return true;
        }
                internal bool TryCommitIssue(CraftCompleteData row,SuccessfulOutput candidate)
        {
            if(candidate==null||!CanIssue(row)||!candidate.MatchesReceipt()||!original.IsCurrent()||
                !candidate.MatchesFrozenLiveOutput(station)||!CanIssue(row))return false;
            issued=true;return true;
        }
        internal RebirthStationCompletionOriginalScope Original=>original;
        internal bool IsConsumed=>consumed;
        internal bool TryConsume(){if(consumed||thread!=Environment.CurrentManagedThreadId)return false;consumed=true;return true;}
    }
    internal static bool TryObserveBefore(TileEntityWorkstation station,int actor,ItemValue output,string recipeName,
        string scrapped,int experience,int count,out OutputObservation observation)
    {
        observation=null;
        try
        {
            if(!TryBuild(station,actor,output,recipeName,scrapped,experience,count,out var receipt)||
                station.Output==null||station.Output.Length<1||station.Output.Length>255)return false;
            var before=new ItemStack[station.Output.Length];
            for(int i=0;i<before.Length;i++)
            {
                var item=station.Output[i];
                if(item?.itemValue==null||item.count<0)return false;
                before[i]=item.Clone();
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(item,before[i]))return false;
            }
            if(!RebirthStationCompletionOriginalScope.TryCapture(station,station.Queue[station.Queue.Length-1],out var original))return false;
            observation=new OutputObservation(station,station.Queue[station.Queue.Length-1],before,receipt,original,mintAuthority);return true;
        }
        catch{return false;}
    }
    // Live list insertion only; a false result after assignment retains uncertain native effect.
    internal static bool TryAppend(TileEntityWorkstation station,int actor,ItemValue output,string recipeName,
        string scrapped,int experience,int count,OutputObservation observation)
        =>TryAppend(station,actor,output,recipeName,scrapped,experience,count,observation,out _);
    internal static bool TryAppend(TileEntityWorkstation station,int actor,ItemValue output,string recipeName,
        string scrapped,int experience,int count,OutputObservation observation,out SuccessfulOutput completed)
    {
        completed=null;
        if(!TryBuild(station,actor,output,recipeName,scrapped,experience,count,out var receipt)||
            !RebirthStationCompletionReceipt.TryReadIdentity(receipt,out var identity)||
            observation==null||!observation.Matches(station,receipt))return false;
        var existing=station.CraftCompleteList;
        if(existing!=null&&(existing.Count>=short.MaxValue||!RebirthStationCompletionReceipt.HasNoJobReceipt(existing,identity.Job)))return false;
        var replacement=existing==null?new System.Collections.Generic.List<CraftCompleteData>():
            new System.Collections.Generic.List<CraftCompleteData>(existing);
        replacement.Add(receipt);
        if(!observation.TryConsume())return false;
        station.CraftCompleteList=replacement;
        try{station.SetModified();return SuccessfulOutput.TryMint(mintAuthority,observation,station,receipt,out completed);}catch{return false;}
    }
    internal sealed class SuccessfulOutput
    {
        internal readonly RebirthStationCompletionOriginalScope Original;
        private readonly CraftCompleteData receipt;
        private readonly ItemStack[] physicalBefore,physicalAfter;
        private bool claimed;
        private bool claiming;
        private SuccessfulOutput(RebirthStationCompletionOriginalScope original,CraftCompleteData row,ItemStack[] before,ItemStack[] after)
        {Original=original;physicalBefore=before;physicalAfter=after;receipt=new CraftCompleteData(row.CrafterEntityID,row.CraftedItemStack.Clone(),row.RecipeName,row.ItemScrapped,row.CraftExpGain,row.RecipeUsedCount);}
        internal static bool TryMint(object authority,OutputObservation observation,TileEntityWorkstation station,CraftCompleteData row,out SuccessfulOutput result)
        {
            result=null;
            if(!ReferenceEquals(authority,mintAuthority)||observation==null||!observation.IsConsumed||observation.Original==null||!observation.Original.IsCurrent()||
                !ReferenceEquals(observation.Original.Station,station)||station.CraftCompleteList==null||!station.CraftCompleteList.Contains(row)||!observation.CanIssue(row))return false;
            if(!observation.TryCopyIssuedDelta(row,out var before,out var after))return false;
            var candidate=new SuccessfulOutput(observation.Original,row,before,after);
            if(!candidate.MatchesReceipt()||!observation.TryCommitIssue(row,candidate))return false;
            result=candidate;return true;
        }
        internal bool MatchesReceipt()
        {
            if(!Original.IsCurrent()||Original.Station.CraftCompleteList==null)return false;
            int found=0;
            foreach(var row in Original.Station.CraftCompleteList)
            {
                if(!RebirthStationCompletionReceipt.HasReservedMarker(row))continue;
                if(!RebirthStationCompletionReceipt.TryReadIdentity(row,out var identity))return false;
                if(identity.Job!=Original.Admission.JobId)continue;
                if(++found!=1||row.CrafterEntityID!=receipt.CrafterEntityID||row.RecipeName!=receipt.RecipeName||
                    row.ItemScrapped!=receipt.ItemScrapped||row.CraftExpGain!=receipt.CraftExpGain||row.RecipeUsedCount!=receipt.RecipeUsedCount||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(row.CraftedItemStack,receipt.CraftedItemStack))return false;
            }
            return found==1;
        }
                        internal bool MatchesFrozenLiveOutput(TileEntityWorkstation station)
        {
            if(!ReferenceEquals(Original.Station,station)||station.Output==null||station.Output.Length!=physicalAfter.Length||
                !RebirthStationOutputDelta.Matches(physicalBefore,physicalAfter,receipt))return false;
            for(int i=0;i<physicalAfter.Length;i++)
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(station.Output[i],physicalAfter[i]))return false;
            return true;
        }
        // Retrieval only: original one-use claim remains owned exclusively by CompletionObservation.
        internal bool TryCopyPhysicalDelta(out ItemStack[] before,out ItemStack[] after,out CraftCompleteData row)
        {
            before=null;after=null;row=null;
            if(!MatchesReceipt()||!RebirthStationOutputDelta.Matches(physicalBefore,physicalAfter,receipt)||
                !TryClonePair(physicalBefore,physicalAfter,out var b,out var a)||!Original.IsCurrent())return false;
            row=new CraftCompleteData(receipt.CrafterEntityID,receipt.CraftedItemStack.Clone(),receipt.RecipeName,
                receipt.ItemScrapped,receipt.CraftExpGain,receipt.RecipeUsedCount);
                        if(!SameCompleteRow(row,receipt)||!RebirthStationOutputDelta.Matches(b,a,row)||
                !Original.IsCurrent()||!MatchesReceipt()||!Original.IsCurrent()){row=null;return false;}
            before=b;after=a;return true;
        }
                private static bool SameCompleteRow(CraftCompleteData left,CraftCompleteData right)
        {
            return left!=null&&right!=null&&left.CrafterEntityID==right.CrafterEntityID&&
                left.RecipeName==right.RecipeName&&left.ItemScrapped==right.ItemScrapped&&
                left.CraftExpGain==right.CraftExpGain&&left.RecipeUsedCount==right.RecipeUsedCount&&
                RebirthStationGridIngredients.IsSameStackSnapshot(left.CraftedItemStack,right.CraftedItemStack);
        }
        internal bool TryClaim()
        {
            if(claimed||claiming)return false;
            claiming=true;
            try
            {
                if(!MatchesReceipt()||!Original.IsCurrent())return false;
                claimed=true;return true;
            }
            finally{claiming=false;}
        }
    }
        private static bool TryClonePair(ItemStack[] left,ItemStack[] right,out ItemStack[] before,out ItemStack[] after)
    {
        before=null;after=null;int budget=8*1024*1024;
        if(!TryCloneOutput(left,ref budget,out var b)||!TryCloneOutput(right,ref budget,out var a))return false;
        before=b;after=a;return true;
    }
    private static bool TryCloneOutput(ItemStack[] input,ref int budget,out ItemStack[] result)
    {
        result=null;
        try
        {
            if(input==null||input.Length<1||input.Length>255)return false;
            var copy=new ItemStack[input.Length];
            for(int i=0;i<input.Length;i++)
            {
                var cell=input[i];if(cell?.itemValue==null||cell.count<0)return false;
                // Exact native image size, including retained empty payload; no metadata normalization.
                string image=RebirthNativeItemCodec.Encode(cell.itemValue);
                if(image.Length<1||image.Length>262144||(budget-=image.Length)<0)return false;
                copy[i]=cell.Clone();if(!RebirthStationGridIngredients.IsSameStackSnapshot(cell,copy[i]))return false;
            }
            result=copy;return true;
        }
        catch{return false;}
    }
    internal static bool TryBuild(TileEntityWorkstation station,int actor,ItemValue output,string recipeName,
        string scrapped,int experience,int count,out CraftCompleteData receipt)
    {
        receipt=null;
        if(station?.Queue==null||station.Queue.Length==0||scrapped!=string.Empty)return false;
        var active=station.Queue[station.Queue.Length-1];
        if(active?.Recipe==null||actor!=active.StartingEntityId||experience!=active.Recipe.craftExpGain||
            count!=active.Recipe.count||recipeName!=active.Recipe.GetName()||
            !RebirthStationObservationDispatcher.TryGetVerifiedQueuedAdmission(station,active,out var admission))return false;
        return RebirthStationCompletionReceipt.TryCreate(admission,active,output,count,out receipt);
    }
}


