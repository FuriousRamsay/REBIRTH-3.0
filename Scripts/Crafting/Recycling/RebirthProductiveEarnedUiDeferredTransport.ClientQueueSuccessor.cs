using System;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static bool ReleaseEmptyClientRecipientQueueAuthority(RebirthProductiveEarnedUiCoordinator.SavedEmptyRecipientQueue proof)
    {
        try
        {
            if(proof?.Current()!=true||!clientHydrations.TryGetValue(proof.Group,out var original)||!ReferenceEquals(original,proof.PreviousProof.Source)||
                !ReferenceEquals(original.Terminal,proof.PreviousProof)||!ReferenceEquals(original.World,proof.World)||!ReferenceEquals(original.Station,proof.Station)||
                !original.HandoffAccepted||original.Unknown||!original.Consumed||original.OwnerStatus!=5||
                !XNode.DeepEquals(proof.PreviousProof.Image,RebirthWorkstationCompletionPublication.ReadPrimary(proof.PreviousProof.Path)))return false;
            clientHydrations.Remove(proof.Group);original.Close();return true;
        }
        catch{return false;}
    }
    internal static bool ReleaseClientRecipientQueueAuthority(RebirthProductiveEarnedUiCoordinator.SavedRecipientQueueSuccessor successor)
    {
        try
        {
            if(successor?.Current()!=true||!clientHydrations.TryGetValue(successor.Group,out var original)||
                !ReferenceEquals(original,successor.PreviousProof.Source)||!ReferenceEquals(original.Terminal,successor.PreviousProof)||
                !ReferenceEquals(original.World,successor.World)||!ReferenceEquals(original.Station,successor.Station)||!original.HandoffAccepted||
                original.Unknown||!original.Consumed||original.OwnerStatus!=5||
                !XNode.DeepEquals(successor.PreviousProof.Image,RebirthWorkstationCompletionPublication.ReadPrimary(successor.PreviousProof.Path)))return false;
            // The private transition and next saved Before deliberately replace
            // the old removal body. This is resource retirement, not reentry.
            clientHydrations.Remove(successor.Group);original.Close();return true;
        }
        catch{return false;}
    }
}
