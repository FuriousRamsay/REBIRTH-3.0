using System;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private sealed class LocalQueueContinuation:IDisposable
    {
        internal RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication Terminal;
        internal object Owners;internal string Path;internal byte[] Image;internal IDisposable Retention;
        public void Dispose(){Retention?.Dispose();Retention=null;Image=null;Owners=null;}
    }
    private static readonly System.Collections.Generic.Dictionary<Guid,LocalQueueContinuation> localQueueContinuations=new System.Collections.Generic.Dictionary<Guid,LocalQueueContinuation>();
    internal sealed class SavedLocalQueueSuccessor
    {
        private readonly LocalQueueContinuation previous;private readonly Original next;private readonly RebirthWorkstationCompletionPipeline.NativeBeforePublication nativeNext;
        internal readonly Guid CustodyBirth;
        private SavedLocalQueueSuccessor(LocalQueueContinuation source,Original original){previous=source;next=original;CustodyBirth=original.EarlierOwner.CurrentCustodyBirth;}
        private SavedLocalQueueSuccessor(LocalQueueContinuation source,RebirthWorkstationCompletionPipeline.NativeBeforePublication original){previous=source;nativeNext=original;CustodyBirth=original.EarlierOwner.CurrentCustodyBirth;}
        internal RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner=>next?.EarlierOwner??nativeNext.EarlierOwner;
        internal string Path=>previous.Path;
        internal long MeasureImage()=>previous.Image?.LongLength??0;
        internal byte[] CopyImage()=>previous.Image==null?null:(byte[])previous.Image.Clone();
        internal RebirthProductiveEarnedUiOwnerFile.LocalOwnerScope PreviousScope=>previous.Terminal.Scope;
        internal RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication PreviousProof=>previous.Terminal;
        internal World World=>previous.Terminal.World;
        internal TileEntityWorkstation Station=>previous.Terminal.Station;
        internal Guid Group=>previous.Terminal.Operation;
        internal Guid SuccessorOperation=>next?.Binding.Operation??nativeNext.Operation;
        internal bool Current()=>nativeNext!=null?ThreadManager.IsMainThread()&&localQueueContinuations.TryGetValue(Group,out var retained)&&ReferenceEquals(retained,previous)&&
            PreviousScope.HandoffAccepted&&PreviousScope.TerminalAccepted&&ReferenceEquals(PreviousScope.Terminal,previous.Terminal)&&
            ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&nativeNext.Current()&&nativeNext.Operation!=Group&&
            ReferenceEquals(nativeNext.World,World)&&ReferenceEquals(nativeNext.Station,Station)&&ReferenceEquals(nativeNext.Viewer,PreviousScope.Actor)&&
            ReferenceEquals(nativeNext.Window,PreviousScope.Window)&&nativeNext.WindowSession==PreviousScope.WindowSession&&
            RebirthProductiveEarnedUiQueueOwner.HandoffContainsOriginal(previous.Owners,nativeNext.EarlierOwner)&&RebirthProductiveEarnedArtifact.FileMatches(previous.Path,previous.Image):
            ThreadManager.IsMainThread()&&localQueueContinuations.TryGetValue(Group,out var source)&&ReferenceEquals(source,previous)&&
            PreviousScope.HandoffAccepted&&PreviousScope.TerminalAccepted&&ReferenceEquals(PreviousScope.Terminal,previous.Terminal)&&
            ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&ReferenceEquals(next.Binding.World,World)&&
            ReferenceEquals(next.Binding.Station,Station)&&SuccessorOperation!=Group&&next.Current()&&next.Before?.Purpose=="before"&&next.Before.Saved()&&
            RebirthProductiveEarnedUiQueueOwner.HandoffContainsOriginal(previous.Owners,next.EarlierOwner)&&
            RebirthProductiveEarnedArtifact.FileMatches(previous.Path,previous.Image);
        internal static bool TryIssue(object candidate,Original original,out SavedLocalQueueSuccessor proof)
        {proof=null;if(candidate is not LocalQueueContinuation source)return false;var value=new SavedLocalQueueSuccessor(source,original);if(!value.Current())return false;proof=value;return true;}
        internal static bool TryIssueNative(object candidate,RebirthWorkstationCompletionPipeline.NativeBeforePublication original,out SavedLocalQueueSuccessor proof)
        {proof=null;if(candidate is not LocalQueueContinuation source)return false;var value=new SavedLocalQueueSuccessor(source,original);if(!value.Current())return false;proof=value;return true;}
    }
    private static bool RetainUnchangedLocalQueue(RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication terminal,byte[] native,int offset)
    {
        if(localQueueContinuations.TryGetValue(terminal.Operation,out var known))return ReferenceEquals(known.Terminal,terminal);
        if(localQueueContinuations.Count>=4)return false;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*native.Length+65536),out var memory))return false;
        IDisposable hold=null;
        try
        {
            var owners=RebirthProductiveEarnedUiQueueOwner.CaptureHandoffOwners(terminal.Station);
            byte[] image=Encoding.ASCII.GetBytes("UQR1-local-unchanged:"+terminal.Operation.ToString("N")+":"+Hash(native)+":"+offset);
            string path=terminal.Path+".queue.unchanged";hold=terminal.Scope.Retain();if(hold==null||!WriteExact(path,image))return false;
            localQueueContinuations.Add(terminal.Operation,new LocalQueueContinuation{Terminal=terminal,Owners=owners,Path=path,Image=image,
                Retention=new RecipientHandoffLease{Source=hold,Memory=memory}});hold=memory=null;return true;
        }
        finally{hold?.Dispose();memory?.Dispose();}
    }
    private static bool RetainAcceptedLocalQueue(QueueHandoff handoff)
    {
        var terminal=handoff.Binding.LocalOwnerSource;if(terminal==null)return false;
        if(localQueueContinuations.ContainsKey(terminal.Operation)||localQueueContinuations.Count>=4)return false;
        localQueueContinuations.Add(terminal.Operation,new LocalQueueContinuation{Terminal=terminal,Owners=handoff.Owners,
            Path=handoff.Path+".accepted",Image=Encoding.ASCII.GetBytes("UQR1-local-sync:"+Hash(handoff.Image)),Retention=handoff});return true;
    }
    private static bool RetireLocalQueueContinuations(Original next)
    {
        foreach(var previous in localQueueContinuations.Values.Where(value=>ReferenceEquals(value.Terminal.Station,next.Binding.Station)).ToArray())
        {
            // An unrelated newly admitted source does not inherit this old
            // metadata graph. Retain it without denying the new saved Before.
            if(!RebirthProductiveEarnedUiQueueOwner.HandoffContainsOriginal(previous.Owners,next.EarlierOwner))continue;
            if(!SavedLocalQueueSuccessor.TryIssue(previous,next,out var proof)||!RebirthProductiveEarnedUiOwnerFile.ReleaseLocalOwnerQueueAuthority(proof))return false;
            localQueueContinuations.Remove(previous.Terminal.Operation);previous.Dispose();
        }
        return true;
    }
    private static void ReleaseLocalQueueContinuations()
    {foreach(var previous in localQueueContinuations.Values)previous.Dispose();localQueueContinuations.Clear();}
    private static bool RetireNativeLocalQueueContinuations(RebirthWorkstationCompletionPipeline.NativeBeforePublication next)
    {
        foreach(var previous in localQueueContinuations.Values.Where(value=>ReferenceEquals(value.Terminal.Station,next.Station)).ToArray())
        {
            if(!RebirthProductiveEarnedUiQueueOwner.HandoffContainsOriginal(previous.Owners,next.EarlierOwner))continue;
            if(!SavedLocalQueueSuccessor.TryIssueNative(previous,next,out var proof)||!RebirthProductiveEarnedUiOwnerFile.ReleaseLocalOwnerQueueAuthority(proof))return false;
            localQueueContinuations.Remove(previous.Terminal.Operation);previous.Dispose();
        }
        return true;
    }
}
