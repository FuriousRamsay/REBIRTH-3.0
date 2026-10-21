using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    internal static bool IsKnownLocalCachedUiCard(Original original,XUiC_RecipeStack card)=>
        original!=null&&original.Binding.World.IsRemote()==false&&originals.TryGetValue(card,out var actual)&&ReferenceEquals(actual,original)&&
        ReferenceEquals(original.Binding.Card,card)&&original.Current()&&original.CompletionAttempted&&original.EarnedSaved&&!original.Delivered&&
        original.Output?.FullBoundary==true&&!original.Output.Continued&&!original.Output.Accepted&&original.Output.Operation==original.Binding.Operation;

    // Called under the local owner's charged decoder. Only the dedicated
    // physical-delivery terminal sync can follow its earlier saved owner event.
    // A current equal tile or a generic scope flag cannot issue this transition.
    internal static bool TryQualifyLocalCachedUiSync(Original original,XUiC_WorkstationWindowGroup window,byte[] ownerBody,int ownerOffset,
        out byte[] before,out byte[] after,out int beforeOffset,out int afterOffset)
    {
        before=after=null;beforeOffset=afterOffset=-1;
        if(original==null||!ReferenceEquals(terminalSync,original)||original.Binding.World.IsRemote()||!ReferenceEquals(original.Binding.Window,window)||
            !original.Current()||!original.Delivered||original.Delivery==null||!original.TerminalSyncAttempted||original.TerminalSynced||original.TerminalBefore==null||original.TerminalBody==null||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Binding.Station,original.TerminalBefore,original.TerminalBeforeOffset))return false;
        // Native UpdateData may already have installed the witnessed output
        // while the native queue still has its original quantity. Account for
        // exactly that physical delivery; no other native field may differ.
        if(!RebirthProductiveEarnedNativeBody.TryRead(ownerBody,ownerOffset,out var old,out ulong elapsed)||
            !RebirthProductiveEarnedNativeBody.TryRead(original.TerminalBefore,original.TerminalBeforeOffset,out var current,out _)||
            !RebirthProductiveEarnedNativeBody.TryRead(original.TerminalBody,original.TerminalOffset,out var projected,out _))return false;
        string outputBefore=(string)original.Delivery.Element("outputBefore"),outputAfter=(string)original.Delivery.Element("outputAfter");
        if(Grid(old.Output)!=outputBefore&&original.Delivery.Element("cachedPredecessor")==null||
            Grid(projected.Output)!=outputAfter||Grid(current.Output)!=outputBefore&&Grid(current.Output)!=outputAfter)return false;
        int index=original.Binding.Index;
        if(index<0||index>=old.Queue.Length||current.Queue.Length!=old.Queue.Length||projected.Queue.Length!=old.Queue.Length||
            old.Queue[index]?.Recipe==null||old.Queue[index].Multiplier!=original.BeforeCount||projected.Queue[index].Multiplier!=original.BeforeCount-1||
            (projected.Queue[index].Multiplier>0?RebirthProductiveEarnedUiQueueOwner.Binding(projected.Queue[index])!=RebirthProductiveEarnedUiQueueOwner.Binding(old.Queue[index]):projected.Queue[index].Recipe!=null))return false;
        for(int i=0;i<old.Queue.Length;i++)if(i!=index&&!RebirthProductiveEarnedRowProof.CaptureRow(old.Queue[i]).SequenceEqual(RebirthProductiveEarnedRowProof.CaptureRow(projected.Queue[i])))return false;
        old.Output=current.Output;var expected=RebirthProductiveEarnedNativeBody.Encode(old,elapsed,out int offset);
        if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(expected,offset,original.TerminalBefore,original.TerminalBeforeOffset))return false;
        current.Queue=projected.Queue;current.Output=projected.Output;expected=RebirthProductiveEarnedNativeBody.Encode(current,elapsed,out offset);
        if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(expected,offset,original.TerminalBody,original.TerminalOffset))return false;
        before=original.TerminalBefore;after=original.TerminalBody;beforeOffset=original.TerminalBeforeOffset;afterOffset=original.TerminalOffset;return true;
    }
}
