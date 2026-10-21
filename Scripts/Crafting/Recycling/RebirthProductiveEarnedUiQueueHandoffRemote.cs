using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiRemote
{
    private sealed class QueueTransition
    {
        internal Registration Original;internal Guid Nonce;internal byte[] Image,Before,After;internal int BeforeOffset,AfterOffset;
        internal RebirthProductiveEarnedUiDeferredTransport.RecipientScope Recipient;
        internal RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication Final;
        internal byte[] TerminalReceipt;
        internal EntityPlayer Actor=>Original?.Actor??Recipient.Actor;
        internal ClientInfo Sender=>Original?.Sender??Recipient.Peer;
        internal World World=>Original?.Actor.world??Recipient.World;
        internal TileEntityWorkstation Station=>Original?.Station??Recipient.Station;
        internal string Creation=>Original?.Creation??Recipient.Creation;
        internal string Owner=>Original?.Owner??Recipient.Owner;
        internal string WorldKey=>Original?.Frame.World??Recipient.Hydration.WorldKey;
        internal Guid Operation=>Original?.Frame.Operation??Recipient.Hydration.Group;
        internal Guid Birth=>Original?.Frame.Birth??Recipient.Birth;
        internal int Deaths=>Original?.Frame.Deaths??Recipient.Deaths;
        internal string Digest,Path;internal IDisposable Retention;internal bool Accepted,NoChange;internal RebirthProductiveEarnedUiQueueOwner.Witness[] Owners;
        internal ulong BeforeClock;
        private int resourceReferences=1,rootReleased;
        internal IDisposable RetainResponseResources()
        {if(System.Threading.Volatile.Read(ref rootReleased)!=0)return null;System.Threading.Interlocked.Increment(ref resourceReferences);return new ResponseResources(this);}
        internal void ReleaseResources(){if(System.Threading.Interlocked.Exchange(ref rootReleased,1)==0)ReleaseReference();}
        private void ReleaseReference(){if(System.Threading.Interlocked.Decrement(ref resourceReferences)!=0)return;Image=Before=After=TerminalReceipt=null;Retention?.Dispose();Retention=null;}
        private sealed class ResponseResources:IDisposable
        {private QueueTransition source;internal ResponseResources(QueueTransition value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.ReleaseReference();}}
        internal bool Current()=>(Original!=null?Original.Completed&&Original.Current():Recipient?.Current()==true&&Final!=null&&
            RebirthProductiveEarnedArtifact.FileMatches(Final.Path,TerminalReceipt))&&RebirthProductiveEarnedArtifact.FileMatches(Path,Image);
    }
    private sealed class QueueRead
    {internal QueueTransition Source;internal NetPackageTileEntity Packet;internal int Reads;internal bool ReadCompleted;internal byte[] PacketBody;internal IDisposable Allocation;}
    private static readonly Dictionary<Guid,QueueTransition> queueTransitions=new Dictionary<Guid,QueueTransition>();
    internal static bool HasRetainedQueueTransition=>queueTransitions.Count>0||emptyQueueAcks.Count>0;
    [ThreadStatic]private static QueueRead readingQueue;
    internal static bool CanTickHostQueueHandoff(TileEntityWorkstation station)=>!queueTransitions.Values.Any(value=>ReferenceEquals(value.Station,station)&&
        ReferenceEquals(value.World,GameManager.Instance?.World));
    internal static bool BlocksQueueHandoff(TileEntityWorkstation station)=>!CanTickHostQueueHandoff(station);
    internal static bool RegisterQueueHandoff(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,Guid operation,byte[] image,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        try
        {
            if(image==null||image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||RebirthProductiveEarnedUiDeferredTransport.HostReadyHold(station)||
                RebirthRemoteResourceSourceCustody.BlocksStation(station)||RebirthRemoteResourcePlacedStationClientCustody.Blocks(station)||RebirthRemoteResourcePlacedStationCustody.Blocks(station))return false;
            string hash=Hash(image);
            if(queueTransitions.TryGetValue(operation,out var retained))
            {if(!retained.Current()||!ReferenceEquals(retained.Actor,actor)||!ReferenceEquals(retained.Sender,sender)||!ReferenceEquals(retained.Station,station)||retained.Creation!=creation||retained.Digest!=hash||!retained.Image.SequenceEqual(image))return false;reply=QueueReply(retained);return true;}
            Registration original=null;RebirthProductiveEarnedUiDeferredTransport.RecipientScope recipient=null;RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication terminal=null;
            if(registrations.TryGetValue(operation,out var candidate)&&candidate.Completed&&candidate.Current()&&ReferenceEquals(candidate.Actor,actor)&&
                ReferenceEquals(candidate.Sender,sender)&&ReferenceEquals(candidate.Station,station)&&candidate.Creation==creation)original=candidate;
            else if(!RebirthProductiveEarnedUiDeferredTransport.TryGetHostRecipientQueueAuthority(operation,out recipient,out terminal)||!terminal.Current()||!recipient.Current()||
                !ReferenceEquals(recipient.Actor,actor)||!ReferenceEquals(recipient.Peer,sender)||!ReferenceEquals(recipient.Station,station)||recipient.Creation!=creation)return false;
            // The real final token's native callback phase completed above;
            // only now open the separate bounded header/native graph decoder.
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
            if(decoder==null)return false;
            var node=XElement.Parse(Encoding.UTF8.GetString(image),LoadOptions.None);
            if(node.Name!="nativeUiQueueHandoff"||(int)node.Attribute("version")!=1||(string)node.Attribute("operation")!=operation.ToString("N"))return false;
            string source=(string)node.Attribute("source");Guid birth;
            if(source=="uio")
            {
                if(original==null)return false;
                birth=original.Frame.Birth;
            }
            else if(source=="recipient")
            {
                if(recipient==null||terminal==null)return false;
                birth=recipient.Birth;
            }
            else return false;
            if((string)node.Attribute("birth")!=birth.ToString("N"))return false;
            bool noChange=(bool?)node.Attribute("noChange")==true;
            int shifts=(int)node.Attribute("shifts"),beforeOffset=(int)node.Attribute("beforeOffset"),afterOffset=(int)node.Attribute("afterOffset");
            byte[] before=Convert.FromBase64String((string)node.Element("before")),after=Convert.FromBase64String((string)node.Element("after"));
            if(terminal!=null&&!RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,terminal.Body,terminal.Offset)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,before,beforeOffset)||
                !RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var expected,out ulong elapsed)||
                !RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var supplied,out ulong afterElapsed)||elapsed!=afterElapsed||
                (noChange?source!="recipient"||shifts!=0:shifts<1||shifts>=expected.Queue.Length)||expected.Queue.Length!=supplied.Queue.Length||
                expected.Queue.Any(row=>row==null||row.RepairItem!=null||row.AmountToRepair!=0)||!noChange&&expected.Queue[^1].Recipe!=null)return false;
            var owners=new List<RebirthProductiveEarnedUiQueueOwner.Witness>();
            for(int i=0;i<station.Queue.Length;i++)
            {
                var row=station.Queue[i];if(row?.Recipe==null||row.Multiplier<1)continue;
                if(!RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(station,i,row,out var owner)||RebirthProductiveEarnedUiQueueOwner.Binding(row)!=owner.RecipeBinding)return false;
                owners.Add(owner);
            }
            for(int shift=0;shift<shifts;shift++)
            {
                if(expected.Queue[^1].Recipe!=null)return false;
                var old=expected.Queue;
                var next=new RecipeQueueItem[old.Length];
                next[0]=new RecipeQueueItem{Multiplier=0,IsCrafting=old[0].IsCrafting,CraftingTimeLeft=0,Quality=1,StartingEntityId=actor.entityId,OneItemCraftTime=0};
                for(int i=1;i<old.Length;i++)next[i]=old[i-1];
                if(next[^1].Recipe!=null)next[^1].IsCrafting=true;
                expected.Queue=next;
            }
            if(!noChange&&expected.Queue[^1].Recipe==null||!RebirthProductiveEarnedNativeBody.Encode(expected,elapsed,out int derived).SequenceEqual(after)||derived!=afterOffset)return false;
            if(queueTransitions.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*image.Length+8L*(before.Length+after.Length)+4L*(terminal?.ReceiptLength??0)+65536),out var retention))return false;
            var value=new QueueTransition{Original=original,Recipient=recipient,Final=terminal,TerminalReceipt=terminal?.Image,Nonce=Guid.NewGuid(),Image=(byte[])image.Clone(),Digest=hash,Before=before,After=after,BeforeOffset=beforeOffset,AfterOffset=afterOffset,
                Path=(original?.Path??terminal.Path)+".queue",NoChange=noChange,Retention=retention,Owners=owners.Distinct().ToArray(),BeforeClock=station.lastTickTime};
            if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(value.Path,value.Image)){retention.Dispose();return false;}
            queueTransitions.Add(operation,value);reply=QueueReply(value);return true;
            }
        }
        catch{return false;}
    }
    private static RebirthProductiveEarnedWire.Reply QueueReply(QueueTransition value)
    {
        return new RebirthProductiveEarnedWire.Reply{Operation=value.Operation,Birth=value.Birth,Nonce=value.Nonce,Digest=value.Digest,
            State=value.Accepted?RebirthProductiveEarnedWire.Status.UiQueueAccepted:RebirthProductiveEarnedWire.Status.UiQueueHeader,
            World=value.WorldKey,Owner=value.Owner,Creation=value.Creation,Deaths=value.Deaths,Position=value.Station.ToWorldPos(),QueueOwner=value.Original?.Frame.OwnerPurpose??""};
    }
    internal static bool QueryQueueHandoff(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,Guid operation,Guid nonce,string digest,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        if(emptyQueueAcks.TryGetValue(operation,out var ack))
        {if(!ack.Matches(actor,sender,station,creation,nonce,digest))return false;reply=ack.Reply;return true;}
        if(!queueTransitions.TryGetValue(operation,out var value)||!value.Current()||value.Nonce!=nonce||value.Digest!=digest||
            !ReferenceEquals(value.Actor,actor)||!ReferenceEquals(value.Sender,sender)||!ReferenceEquals(value.Station,station)||value.Creation!=creation)return false;
        if(value.Accepted&&value.NoChange&&value.Recipient!=null&&value.Station.Queue.All(row=>row!=null&&row.Recipe==null&&row.Multiplier==0))
            return FinishEmptyRecipientTransition(value,out reply);
        reply=QueueReply(value);return true;
    }
    internal static bool EnterQueueNativeUpload(NetPackageTileEntity packet,World world,bool run,out object state)
    {
        state=null;if(!run)return true;
        if(world?.IsRemote()==true){RebirthProductiveEarnedUiCoordinator.EnterQueueEcho(packet,world,run,out state);return true;}
        if(world?.IsRemote()!=false)return true;
        try
        {
            var found=queueTransitions.Values.Where(value=>!value.Accepted&&ReferenceEquals(value.World,world)&&value.Station.ToWorldPos().Equals(packet.teWorldPos)).ToArray();
            if(found.Length==0)return true;if(found.Length!=1||readingQueue!=null)return false;var value=found[0];
            if(!value.Current()||!ReferenceEquals(value.Sender,packet.Sender)||packet.teBlockId!=world.GetBlock(packet.teWorldPos).type||packet.ms==null||
                packet.ms.Length<1||packet.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes)return false;
            var allocation=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(allocation==null)return false;
            try
            {
                byte[] raw=new byte[(int)packet.ms.Length];lock(packet.ms)
                {long prior=packet.ms.Position;try{packet.ms.Position=0;int read=0;while(read<raw.Length){int n=packet.ms.Read(raw,read,raw.Length-read);if(n<1)return false;read+=n;}}finally{packet.ms.Position=prior;}}
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,value.AfterOffset,value.After,value.AfterOffset)||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Station,value.Before,value.BeforeOffset)||value.Station.lastTickTime!=value.BeforeClock||BitConverter.ToUInt64(raw,value.AfterOffset)>GameTimer.Instance.ticks)return false;
                readingQueue=new QueueRead{Source=value,Packet=packet,PacketBody=raw,Allocation=allocation};state=readingQueue;allocation=null;return true;
            }
            finally{allocation?.Dispose();}
        }
        catch{return false;}
    }
    internal static void EnterQueueNativeRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {RebirthProductiveEarnedUiCoordinator.EnterQueueEchoRead(station,reader,mode,run);var value=readingQueue;if(run&&value!=null&&mode==StreamModeRead.FromClient&&ReferenceEquals(station,value.Source.Station)&&ReferenceEquals(reader.BaseStream,value.Packet.ms))value.Reads++;}
    internal static void ObserveQueueNativeRead(PooledBinaryReader reader,bool run)
    {RebirthProductiveEarnedUiCoordinator.ObserveQueueEchoRead(reader,run);var value=readingQueue;if(run&&value!=null&&value.Reads==1&&ReferenceEquals(reader.BaseStream,value.Packet.ms)&&reader.BaseStream.Position==value.Packet.ms.Length)value.ReadCompleted=true;}
    internal static void ObserveQueueNativeUpload(NetPackageTileEntity packet,World world,bool run,object state)
    {
        RebirthProductiveEarnedUiCoordinator.ObserveQueueEcho(packet,world,run,state);
        try
        {
            if(state is not QueueRead value||!run||!ReferenceEquals(readingQueue,value)||!ReferenceEquals(value.Packet,packet)||value.Reads!=1||!value.ReadCompleted||!value.Source.Current())return;
            value.Allocation?.Dispose();value.Allocation=null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Source.Station,value.PacketBody,value.Source.AfterOffset)||
                    value.Source.Station.lastTickTime!=GameTimer.Instance.ticks-BitConverter.ToUInt64(value.PacketBody,value.Source.AfterOffset))return;
                var receipt=Encoding.ASCII.GetBytes("UQR1-accepted:"+value.Source.Digest+":"+value.Source.Nonce.ToString("N"));
                value.Source.Accepted=RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(value.Source.Path+".accepted",receipt);
            }
        }
        catch{/* No actual read-complete postimage means no accepted transition. */}
    }
    internal static void EndQueueNativeUpload(object state)
    {RebirthProductiveEarnedUiCoordinator.EndQueueEcho(state);if(state is QueueRead value){value.Allocation?.Dispose();value.Allocation=null;value.PacketBody=null;if(ReferenceEquals(readingQueue,value))readingQueue=null;}}
    internal static void ReleaseQueueNativeAllocation(object state)
    {if(state is QueueRead value){value.Allocation?.Dispose();value.Allocation=null;}RebirthProductiveEarnedUiCoordinator.ReleaseQueueEchoAllocation(state);}
    // A successor must carry one of the SAME earlier insertion handles through
    // the actual accepted UQR event. Equal rows/descriptors cannot create it.
    internal static bool QueueTransitionAllows(Registration next)
    {
        var relevant=queueTransitions.Values.Where(value=>ReferenceEquals(value.Station,next.Station)&&ReferenceEquals(value.World,next.Actor.world)).ToArray();
        return relevant.Length==0||relevant.All(value=>value.Accepted&&value.Current()&&value.Owners.Contains(next.EarlierOwner));
    }
    internal static bool AllowsBeforeSource(TileEntityWorkstation station,EntityPlayer actor,RebirthProductiveEarnedUiQueueOwner.Witness owner)
    {
        var relevant=queueTransitions.Values.Where(value=>ReferenceEquals(value.Station,station)&&ReferenceEquals(value.World,actor.world)).ToArray();
        return relevant.Length==0||relevant.All(value=>value.Accepted&&value.Current()&&value.Owners.Contains(owner));
    }
    private static void ReleaseQueueTransitions()
    {readingQueue?.Allocation?.Dispose();foreach(var value in queueTransitions.Values)value.ReleaseResources();queueTransitions.Clear();ReleaseEmptyQueueAcks();readingQueue=null;}
    private static bool RetainsQueueTransition(Registration original)=>queueTransitions.Values.Any(value=>ReferenceEquals(value.Original,original))||
        original.Completed&&original.Current()&&original.Station.Queue?.Length>1&&original.Station.Queue[^1]?.Recipe==null&&
        original.Station.Queue.Any(row=>row?.Recipe!=null&&row.Multiplier>0);
    internal sealed class SavedQueueSuccessor
    {
        private readonly QueueTransition previous;private readonly Registration next;private readonly RebirthWorkstationCompletionPipeline.NativeHostOriginal nativeNext;
        private SavedQueueSuccessor(QueueTransition source,Registration successor){previous=source;next=successor;}
        private SavedQueueSuccessor(QueueTransition source,RebirthWorkstationCompletionPipeline.NativeHostOriginal successor){previous=source;nativeNext=successor;}
        internal Guid Group=>previous.Operation;
        internal World World=>previous.World;
        internal TileEntityWorkstation Station=>previous.Station;
        internal RebirthProductiveEarnedUiDeferredTransport.RecipientScope PreviousScope=>previous.Recipient;
        internal RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication PreviousProof=>previous.Final;
        internal Guid SuccessorOperation=>next?.Frame.Operation??nativeNext.Operation;
        internal bool Current()=>nativeNext!=null?previous.Accepted&&previous.Current()&&previous.Owners.Contains(nativeNext.EarlierOwner)&&
            nativeNext.CurrentViewer()&&nativeNext.AdmissionCurrent()&&nativeNext.BeforeSaved&&nativeNext.IsSavedActiveBefore(nativeNext.Before)&&nativeNext.TrySealBefore()&&
            ReferenceEquals(World,nativeNext.World)&&ReferenceEquals(Station,nativeNext.Station)&&SuccessorOperation!=Group:
            previous.Accepted&&previous.Current()&&previous.Owners.Contains(next.EarlierOwner)&&next.Current()&&
            ReferenceEquals(World,next.Actor.world)&&ReferenceEquals(Station,next.Station)&&
            RebirthProductiveEarnedRemoteHost.TryBeforePublished(next.Actor,next.Sender,next.Frame.Operation,next.Nonce,next.RowHash,out bool saved)&&saved;
        internal static bool TryCreate(object source,Registration successor,out SavedQueueSuccessor token)
        {
            token=null;
            if(source is not QueueTransition prior||prior.Recipient==null||prior.Final==null||!queueTransitions.TryGetValue(prior.Operation,out var retained)||!ReferenceEquals(prior,retained))return false;
            var result=new SavedQueueSuccessor(prior,successor);if(!result.Current())return false;token=result;return true;
        }
        internal static bool TryCreateNative(object source,RebirthWorkstationCompletionPipeline.NativeHostOriginal successor,out SavedQueueSuccessor token)
        {token=null;if(source is not QueueTransition prior||prior.Recipient==null||prior.Final==null||!queueTransitions.TryGetValue(prior.Operation,out var retained)||!ReferenceEquals(prior,retained))return false;
            var result=new SavedQueueSuccessor(prior,successor);if(!result.Current())return false;token=result;return true;}
    }
    internal static bool AcceptSavedNativeQueueBefore(RebirthWorkstationCompletionPipeline.NativeHostOriginal next)
    {
        if(next?.CurrentViewer()!=true||!next.BeforeSaved||!next.IsSavedActiveBefore(next.Before)||!next.TrySealBefore())return false;
        foreach(var pair in queueTransitions.Where(pair=>ReferenceEquals(pair.Value.World,next.World)&&ReferenceEquals(pair.Value.Station,next.Station)).ToArray())
        {
            if(!pair.Value.Accepted||!pair.Value.Current()||!pair.Value.Owners.Contains(next.EarlierOwner))return false;
            if(pair.Value.Recipient!=null&&(!SavedQueueSuccessor.TryCreateNative(pair.Value,next,out var proof)||!RebirthProductiveEarnedUiDeferredTransport.ReleaseHostRecipientQueueAuthority(proof)))return false;
            pair.Value.ReleaseResources();queueTransitions.Remove(pair.Key);
        }
        return true;
    }
    private static void RetireQueueTransitions(Registration next)
    {
        if(!QueueTransitionAllows(next))return;
        foreach(var pair in queueTransitions.Where(pair=>ReferenceEquals(pair.Value.Station,next.Station)&&!ReferenceEquals(pair.Value.Original,next)).ToArray())
        {
            if(pair.Value.Recipient!=null&&(!SavedQueueSuccessor.TryCreate(pair.Value,next,out var saved)||
                !RebirthProductiveEarnedUiDeferredTransport.ReleaseHostRecipientQueueAuthority(saved)))continue;
            pair.Value.ReleaseResources();queueTransitions.Remove(pair.Key);
        }
    }
}
