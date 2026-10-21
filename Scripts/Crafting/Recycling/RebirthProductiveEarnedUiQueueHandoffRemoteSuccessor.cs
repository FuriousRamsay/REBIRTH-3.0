using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private static readonly Dictionary<Guid,QueueHandoff> recipientQueueContinuations=new Dictionary<Guid,QueueHandoff>();
    internal sealed class SavedRecipientQueueSuccessor
    {
        private readonly QueueHandoff previous;private readonly Original next;private readonly RebirthWorkstationCompletionPipeline.NativeBeforePublication nativeNext;
        private SavedRecipientQueueSuccessor(QueueHandoff old,Original original){previous=old;next=original;}
        private SavedRecipientQueueSuccessor(QueueHandoff old,RebirthWorkstationCompletionPipeline.NativeBeforePublication original){previous=old;nativeNext=original;}
        internal RebirthProductiveEarnedUiDeferredTransport.ClientTerminalPublication PreviousProof=>previous.Binding.RecipientSource;
        internal World World=>previous.Binding.World;
        internal TileEntityWorkstation Station=>previous.Binding.Station;
        internal Guid Group=>PreviousProof.Operation;
        internal Guid SuccessorOperation=>next?.Binding.Operation??nativeNext.Operation;
        internal bool Current()
        {
            var b=previous.Binding;
            if(nativeNext!=null)return recipientQueueContinuations.TryGetValue(Group,out var held)&&ReferenceEquals(held,previous)&&previous.Submitted&&previous.Observed&&previous.ResponseObserved&&
                nativeNext.Current()&&nativeNext.Operation!=Group&&ReferenceEquals(nativeNext.World,World)&&ReferenceEquals(nativeNext.Station,Station)&&
                ReferenceEquals(nativeNext.Viewer,b.Actor)&&ReferenceEquals(nativeNext.Window,b.Window)&&nativeNext.WindowSession==previous.WindowSession&&
                ReferenceEquals(nativeNext.Connection,previous.Connection)&&RebirthProductiveEarnedUiQueueOwner.HandoffContainsOriginal(previous.Owners,nativeNext.EarlierOwner)&&
                RebirthProductiveEarnedArtifact.FileMatches(previous.Path+".accepted",Encoding.ASCII.GetBytes("UQR1-accepted:"+Hash(previous.Image)));
            var n=next.Binding;
            return recipientQueueContinuations.TryGetValue(Group,out var retained)&&ReferenceEquals(retained,previous)&&previous.Submitted&&previous.Observed&&
                ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&SuccessorOperation!=Group&&next.Current()&&next.Ui.CurrentBefore()&&
                remoteBefores.TryGetValue(next,out var before)&&before.Saved&&before.Sent&&before.Nonce!=Guid.Empty&&
                ReferenceEquals(n.World,World)&&ReferenceEquals(n.Station,Station)&&ReferenceEquals(n.Actor,b.Actor)&&ReferenceEquals(n.Window,b.Window)&&
                RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(n.Window,out long session)&&session==previous.WindowSession&&
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],previous.Connection)&&
                RebirthProductiveEarnedUiQueueOwner.HandoffContainsOriginal(previous.Owners,next.EarlierOwner)&&
                RebirthProductiveEarnedArtifact.FileMatches(previous.Path+".accepted",Encoding.ASCII.GetBytes("UQR1-accepted:"+Hash(previous.Image)));
        }
        internal static bool TryIssue(object candidate,Original original,out SavedRecipientQueueSuccessor proof)
        {proof=null;if(candidate is not QueueHandoff old||old.Binding.RecipientSource==null)return false;var value=new SavedRecipientQueueSuccessor(old,original);if(!value.Current())return false;proof=value;return true;}
        internal static bool TryIssueNative(object candidate,RebirthWorkstationCompletionPipeline.NativeBeforePublication original,out SavedRecipientQueueSuccessor proof)
        {proof=null;if(candidate is not QueueHandoff old||old.Binding.RecipientSource==null)return false;var value=new SavedRecipientQueueSuccessor(old,original);if(!value.Current())return false;proof=value;return true;}
    }
    private static bool RetainAcceptedRecipientQueue(QueueHandoff value)
    {
        var terminal=value.Binding.RecipientSource;if(terminal==null)return false;
        if(recipientQueueContinuations.TryGetValue(terminal.Operation,out var old))return ReferenceEquals(old,value);
        if(recipientQueueContinuations.Count>=4)return false;
        recipientQueueContinuations.Add(terminal.Operation,value);RebirthProductiveEarnedWire.ReleaseAcceptedQueueStatus(value.Binding.Operation,value.Nonce,Hash(value.Image));return true;
    }
    private static bool RetireRecipientQueueContinuations(Original next)
    {
        foreach(var old in recipientQueueContinuations.Values.Where(value=>ReferenceEquals(value.Binding.Station,next.Binding.Station)).ToArray())
        {
            if(!RebirthProductiveEarnedUiQueueOwner.HandoffContainsOriginal(old.Owners,next.EarlierOwner))continue;
            if(!SavedRecipientQueueSuccessor.TryIssue(old,next,out var proof)||!RebirthProductiveEarnedUiDeferredTransport.ReleaseClientRecipientQueueAuthority(proof))return false;
            recipientQueueContinuations.Remove(old.Binding.RecipientSource.Operation);old.Dispose();
        }
        return true;
    }
    private static void ReleaseRecipientQueueContinuations()
    {foreach(var old in recipientQueueContinuations.Values)old.Dispose();recipientQueueContinuations.Clear();}
    internal static bool AcceptNativeBeforePublication(RebirthWorkstationCompletionPipeline.NativeBeforePublication next)
    {
        if(next?.Current()!=true)return false;
        if(!next.World.IsRemote())return RetireNativeLocalQueueContinuations(next);
        foreach(var old in recipientQueueContinuations.Values.Where(value=>ReferenceEquals(value.Binding.Station,next.Station)).ToArray())
        {
            if(!RebirthProductiveEarnedUiQueueOwner.HandoffContainsOriginal(old.Owners,next.EarlierOwner))continue;
            if(!SavedRecipientQueueSuccessor.TryIssueNative(old,next,out var proof)||!RebirthProductiveEarnedUiDeferredTransport.ReleaseClientRecipientQueueAuthority(proof))return false;
            recipientQueueContinuations.Remove(old.Binding.RecipientSource.Operation);old.Dispose();
        }
        return true;
    }
}
