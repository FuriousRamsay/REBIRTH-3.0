using System;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedUiRemote
{
    private sealed class EmptyQueueAck:IDisposable
    {
        internal World World;internal TileEntityWorkstation Station;internal EntityPlayer Actor;internal ClientInfo Sender;
        internal RebirthProductiveEarnedWire.Reply Reply;internal IDisposable Memory;
        internal bool Matches(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,Guid nonce,string digest)
            =>ReferenceEquals(World,GameManager.Instance?.World)&&ReferenceEquals(Actor,actor)&&ReferenceEquals(Sender,sender)&&ReferenceEquals(Station,station)&&
                ReferenceEquals(World.GetTileEntity(station.ToWorldPos()),station)&&Reply.Nonce==nonce&&Reply.Digest==digest&&Reply.Creation==creation&&
                actor.Died==Reply.Deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&
                RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)&&owner.StorageKey==Reply.Owner&&
                RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation);
        public void Dispose(){Memory?.Dispose();Memory=null;World=null;Station=null;Actor=null;Sender=null;Reply=null;}
    }
    private static readonly System.Collections.Generic.Dictionary<Guid,EmptyQueueAck> emptyQueueAcks=new System.Collections.Generic.Dictionary<Guid,EmptyQueueAck>();
    private static bool FinishEmptyRecipientTransition(QueueTransition source,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;var token=SavedEmptyRecipientQueue.TryIssue(source);if(token==null||emptyQueueAcks.Count>=4096)return false;
        var result=QueueReply(source);long size=checked(2048L+8L*((result.World?.Length??0)+(result.Owner?.Length??0)+(result.Creation?.Length??0)+(result.Digest?.Length??0)));
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(size,out var memory))return false;
        var retained=new EmptyQueueAck{World=source.World,Station=source.Station,Actor=source.Actor,Sender=source.Sender,Reply=result,Memory=memory};
        if(!RebirthProductiveEarnedUiDeferredTransport.ReleaseHostRecipientQueueAuthority(token)){retained.Dispose();return false;}
        emptyQueueAcks.Add(result.Operation,retained);queueTransitions.Remove(result.Operation);source.ReleaseResources();reply=result;return true;
    }
    private static void ReleaseEmptyQueueAcks(){foreach(var value in emptyQueueAcks.Values)value.Dispose();emptyQueueAcks.Clear();}
    internal sealed class SavedEmptyRecipientQueue
    {
        private readonly QueueTransition previous;
        private SavedEmptyRecipientQueue(QueueTransition source){previous=source;}
        internal Guid Group=>previous.Operation;
        internal World World=>previous.World;
        internal TileEntityWorkstation Station=>previous.Station;
        internal RebirthProductiveEarnedUiDeferredTransport.RecipientScope PreviousScope=>previous.Recipient;
        internal RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication PreviousProof=>previous.Final;
        internal bool Current()
        {
            if(!queueTransitions.TryGetValue(Group,out var value)||!ReferenceEquals(value,previous)||!previous.NoChange||!previous.Accepted||
                previous.Recipient==null||previous.Final?.Current()!=true||!previous.Current())return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())return decoder!=null&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,previous.After,previous.AfterOffset)&&
                RebirthProductiveEarnedNativeBody.TryRead(previous.After,previous.AfterOffset,out var frame,out _)&&frame.Queue!=null&&
                frame.Queue.All(row=>row!=null&&row.Recipe==null&&row.Multiplier==0&&row.AmountToRepair==0&&(row.RepairItem==null||row.RepairItem.IsEmpty()))&&
                RebirthProductiveEarnedArtifact.FileMatches(previous.Path+".accepted",Encoding.ASCII.GetBytes("UQR1-accepted:"+previous.Digest+":"+previous.Nonce.ToString("N")));
        }
        internal static SavedEmptyRecipientQueue TryIssue(object candidate)
        {if(candidate is not QueueTransition source)return null;var result=new SavedEmptyRecipientQueue(source);return result.Current()?result:null;}
    }
}

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    internal sealed class SavedEmptyRecipientQueue
    {
        private readonly QueueHandoff previous;
        private SavedEmptyRecipientQueue(QueueHandoff source){previous=source;}
        internal Guid Group=>previous.Binding.RecipientSource.Operation;
        internal World World=>previous.Binding.World;
        internal TileEntityWorkstation Station=>previous.Binding.Station;
        internal RebirthProductiveEarnedUiDeferredTransport.ClientTerminalPublication PreviousProof=>previous.Binding.RecipientSource;
        internal bool Current()
        {
            if(!previous.NoChange||!previous.Submitted||!previous.ResponseObserved||!previous.Observed||!previous.Current()||!HandoffUiCurrent(previous)||
                !handoffs.TryGetValue(previous.Binding.Queue,out var current)||!ReferenceEquals(current,previous)||
                previous.Cards.Any(card=>card.GetRecipe()!=null||card.GetRecipeCount()!=0||card.isInventoryFull||card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty())||
                !RebirthProductiveEarnedArtifact.FileMatches(previous.Path+".accepted",Encoding.ASCII.GetBytes("UQR1-accepted:"+Hash(previous.Image))))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())return decoder!=null&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,previous.After,previous.AfterOffset)&&Station.Queue!=null&&Station.Queue.Length==previous.Cards.Length&&
                Station.Queue.All(row=>row!=null&&row.Recipe==null&&row.Multiplier==0&&row.AmountToRepair==0&&(row.RepairItem==null||row.RepairItem.IsEmpty()));
        }
        internal static SavedEmptyRecipientQueue TryIssue(object candidate)
        {if(candidate is not QueueHandoff source||source.Binding.RecipientSource==null)return null;var result=new SavedEmptyRecipientQueue(source);return result.Current()?result:null;}
    }
    private static bool ReleaseEmptyRecipientQueue(QueueHandoff value)
    {
        var proof=SavedEmptyRecipientQueue.TryIssue(value);
        if(proof==null||!RebirthProductiveEarnedUiDeferredTransport.ReleaseEmptyClientRecipientQueueAuthority(proof))return false;
        RebirthProductiveEarnedWire.ReleaseAcceptedQueueStatus(value.Binding.Operation,value.Nonce,Hash(value.Image));
        handoffs.Remove(value.Binding.Queue);value.Dispose();return true;
    }
}
