using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// UIO1 purpose binding around the existing private native Before/After
// observation. A generic REW2 status is never a UI stage response.
internal static partial class RebirthProductiveEarnedUiRemote
{
    internal sealed class Registration
    {
        internal EntityPlayer Actor;internal ClientInfo Sender;internal TileEntityWorkstation Station;
        internal RebirthProductiveEarnedUiOriginal.UiFrame Frame;
        internal byte[] Image;internal string Path,Hash,RowHash,Creation,Owner,Save;
        internal Guid Nonce;internal RebirthProductiveEarnedObservationBudget Memory;internal bool Completed;internal int GroupReferences;
        internal RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;internal IDisposable OwnerRetention;
        internal byte[] TerminalBody,OwnerAfter,TerminalReceipt;internal int TerminalOffset;internal XElementHolder Terminal;
        internal TypedStationPublication StationPublication;
        internal RebirthProductiveEarnedUiDeferredTransport.HostContribution Deferred;
        internal bool Current()=>RebirthProductiveEarnedUiOriginal.Enabled&&
            Sender!=null&&Sender.loginDone&&!Sender.disconnecting&&Frame!=null&&
            RebirthProductiveEarnedArtifact.RemoteAuthority(Actor,Sender,Station,Creation)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Sender,out var identity)&&identity.StorageKey==Owner&&
            Frame.World==RebirthProductiveEarnedArtifact.WorldIdentity(Actor.world)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(Actor.world,Station,out var birth)&&birth==Frame.Birth.ToString("N")&&
            Save==System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())&&RebirthProductiveEarnedArtifact.FileMatches(Path,Image);
    }
    internal sealed class XElementHolder {internal System.Xml.Linq.XElement Delivery;internal RebirthProductiveEarnedUiOwnerFile.RemoteWitness Owner;internal IDisposable Allocation;}
    internal static bool TryFindRegisteredContribution(Guid operation,out Registration original)
    {
        original=null;
        return ThreadManager.IsMainThread()&&operation!=Guid.Empty&&registrations.TryGetValue(operation,out original)&&
            (original.Current()||original.StationPublication?.Current()==true);
    }
    [ThreadStatic]private static Registration preparing;
    private static readonly Dictionary<Guid,Registration> registrations=new Dictionary<Guid,Registration>();
    internal static bool IsPreparing(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,byte[] row)
        =>preparing!=null&&preparing.Current()&&ReferenceEquals(preparing.Actor,actor)&&ReferenceEquals(preparing.Sender,sender)&&
            ReferenceEquals(preparing.Station,station)&&(row==null||preparing.Frame.RowImage.SequenceEqual(row));
    internal static bool Register(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,byte[] image,
        RebirthProductiveEarnedObservationBudget.Decoder decoder,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        try
        {
            if(!RebirthProductiveEarnedUiOriginal.Enabled||decoder?.IsCurrent!=true||preparing!=null||
                !RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var identity))return false;
            var frame=RebirthProductiveEarnedUiOriginal.ReadUiArtifact(image);
            if(frame.Owner!=identity.StorageKey||frame.Creation!=creation||frame.Deaths!=actor.Died||frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)||
                !decoder.Admit(frame.Earned,image.Length))return false;
            var row=station.Queue?[frame.Index];
            if(row?.Recipe==null||!RebirthProductiveEarnedUiQueueOwner.MatchesPurpose(station,frame.Index,row,frame.OwnerPurpose,out var earlierOwner)||
                earlierOwner.StartingEntityId==actor.entityId&&(earlierOwner.OriginalOwnerKey!=identity.StorageKey||earlierOwner.Creation!=creation||earlierOwner.Deaths!=actor.Died)||
                !RebirthProductiveEarnedUiOwnerFile.MatchesRemoteBefore(actor,sender,frame,row.Recipe)||RebirthStationGridQueue.IsMarked(row.Recipe)&&
                (!RebirthStationObservationDispatcher.TryGetVerifiedQueuedAdmission(station,row,out var admission)||
                    !admission.MatchesQueuedQuality(row.Recipe,row.Multiplier,row.Quality)))return false;
            string hash=Hash(image),rowHash=Hash(frame.RowImage);
            if(!RebirthProductiveEarnedUiRemoteBefore.TryGetSaved(actor,sender,station,frame.Operation,hash,earlierOwner,
                frame.Earned.BeforeNative,frame.Earned.BeforeElapsedOffset,decoder,out var initialBefore))return false;
            Registration original;
            if(registrations.TryGetValue(frame.Operation,out original))
            {
                if(!original.Current()||original.Hash!=hash||!original.Image.SequenceEqual(image)||!ReferenceEquals(original.Actor,actor)||!ReferenceEquals(original.Sender,sender))return false;
            }
            else
            {
                PruneCompleted();if(registrations.Count>=8||registrations.Values.Count(value=>!value.Completed)>=4)return false;
                string save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());
                string path=System.IO.Path.Combine(save,"RebirthProductiveEarnedUi","remote",identity.StorageKey,frame.Operation.ToString("N"),"original.uio");
                if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(path,image))return false;
                var memory=RebirthProductiveEarnedObservationBudget.TryOpenUiFrame(actor.world,frame.Birth,frame.Operation,image);if(memory==null)return false;
                original=new Registration{Actor=actor,Sender=sender,Station=station,Frame=frame,Image=(byte[])image.Clone(),Path=path,Hash=hash,RowHash=rowHash,
                    Creation=creation,Owner=identity.StorageKey,Save=save,Memory=memory,EarlierOwner=earlierOwner,OwnerRetention=earlierOwner.RetainForCompletion()};
                if(original.OwnerRetention==null){memory.Dispose();return false;}
                registrations.Add(frame.Operation,original);
            }
            if(!QueueTransitionAllows(original))return false;
            RebirthProductiveEarnedRemoteHost.Install();preparing=original;
            try
            {
                if(!RebirthProductiveEarnedRemoteHost.TryHoldEarly(actor,sender,station,creation,frame.Operation,rowHash,
                    RebirthProductiveEarnedRemoteHost.EarlyManifest(frame.Earned,frame.RowImage),out var held,out var birth)||birth!=frame.Birth||
                    !RebirthProductiveEarnedRemoteHost.Prepare(actor,sender,station,creation,frame.RowImage,held,decoder,out var nonce,out var digest)||digest!=rowHash)return false;
                original.Nonce=nonce;
                if(!RebirthProductiveEarnedRemoteHost.TryAdoptUiBefore(actor,sender,frame.Operation,nonce,rowHash,hash,earlierOwner,initialBefore,decoder))return false;
            }
            finally{preparing=null;}
            reply=Reply(original,RebirthProductiveEarnedWire.Status.Prepared);return true;
        }
        catch{return false;}
    }
    internal static bool Query(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,Guid operation,Guid nonce,string hash,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        if(!registrations.TryGetValue(operation,out var original))return TryQueryLoadedDeferredTerminal(actor,sender,station,creation,operation,nonce,hash,out reply);
        if(!original.Current()||original.Nonce!=nonce||nonce==Guid.Empty||original.Hash!=hash||
            !ReferenceEquals(original.Actor,actor)||!ReferenceEquals(original.Sender,sender)||!ReferenceEquals(original.Station,station)||original.Creation!=creation)return false;
        var state=RebirthProductiveEarnedWire.Status.Header;
        if(original.Terminal!=null)
        {state=original.Completed?RebirthProductiveEarnedWire.Status.UiTerminalSaved:original.Deferred!=null?
            (original.Deferred.TrySealStation()?RebirthProductiveEarnedWire.Status.UiTerminalStationSaved:RebirthProductiveEarnedWire.Status.UiTerminalHeader):
            (TerminalSaved(original)?RebirthProductiveEarnedWire.Status.UiTerminalSaved:RebirthProductiveEarnedWire.Status.UiTerminalHeader);
         reply=Reply(original,state);return true;}
        if(RebirthProductiveEarnedRemoteHost.TryPublished(actor,sender,operation,nonce,original.RowHash,out bool after)&&after)state=RebirthProductiveEarnedWire.Status.Published;
        else if(RebirthProductiveEarnedRemoteHost.TryBeforePublished(actor,sender,operation,nonce,original.RowHash,out bool before)&&before)
        {state=RebirthProductiveEarnedWire.Status.Prepared;RetireQueueTransitions(original);}
        reply=Reply(original,state);return true;
    }
    private static RebirthProductiveEarnedWire.Reply Reply(Registration original,RebirthProductiveEarnedWire.Status status)=>new RebirthProductiveEarnedWire.Reply
    {Operation=original.Frame.Operation,Birth=original.Frame.Birth,Nonce=original.Nonce,World=original.Frame.World,Owner=original.Owner,Creation=original.Creation,
        Deaths=original.Frame.Deaths,Position=original.Station.ToWorldPos(),Digest=original.Hash,State=status,QueueOwner=original.Frame.OwnerPurpose};
    private static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
}

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private sealed class Description {internal Guid Operation;internal XUiC_RecipeStack Card;internal World World;internal double Next;}
    private static readonly Dictionary<XUiC_RecipeStack,Description> descriptions=new Dictionary<XUiC_RecipeStack,Description>();
    internal static bool WriteRemoteExact(string path,byte[] image)=>WriteExact(path,image);
    private static void DescribeRemote(XUiC_RecipeStack card,Guid operation)
    {
        var actor=card.xui?.playerUI?.entityPlayer;var station=card.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity;
        if(actor?.world?.IsRemote()!=true||station==null||descriptions.Count>=4)return;
        if(!descriptions.TryGetValue(card,out var original)){original=new Description{Operation=operation,Card=card,World=actor.world};descriptions.Add(card,original);}
        double now=Monotonic();if(now<original.Next)return;original.Next=now+0.5;
        RebirthProductiveEarnedWire.Install();RebirthProductiveEarnedWire.Send(actor,station,RebirthProductiveEarnedWire.Kind.Describe,original.Operation,Guid.Empty,null,null);
    }
    private static void PrepareRemote(Original original)
    {
        if(!EnsureRemoteBefore(original))return;
        double now=Monotonic();if(now<original.NextHeader)return;original.NextHeader=now+0.5;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {if(decoder!=null)RebirthProductiveEarnedWire.Send(original.Binding.Actor,original.Binding.Station,RebirthProductiveEarnedWire.Kind.UiPrepare,
            original.Binding.Operation,Guid.Empty,original.Image,original.Digest);}
    }
    private static bool AdvanceRemoteWear(Original original,XUiC_RecipeStack card)
    {
        if(!original.Current()||!original.Ui.CurrentBefore())return true;
        if(!RebirthProductiveEarnedWire.TryReadReply(original.Binding.Operation,out var reply)||reply.Digest!=original.Digest||reply.Birth!=original.Binding.Birth||
            reply.Owner!=original.Binding.Owner||reply.Creation!=original.Binding.Creation||reply.Deaths!=original.Binding.Deaths||reply.World!=original.Binding.WorldKey||
            !reply.Position.Equals(original.Binding.Station.ToWorldPos())){PrepareRemote(original);return true;}
        if(original.Nonce==Guid.Empty){original.Nonce=reply.Nonce;if(!Stage(original,"Registered"))return true;}
        if(reply.Nonce!=original.Nonce||original.Nonce==Guid.Empty)return true;
        // UBF1 owns the sole Before upload; a missing saved receipt remains unknown.
        if(reply.State==RebirthProductiveEarnedWire.Status.Prepared&&Stage(original,"BeforeSaved")&&RetireRecipientQueueContinuations(original))ApplyWear(original,card);
        QueryUi(original);return true;
    }
    private static void UploadUi(Original original)
    {
        if(!original.Current()||!(original.Worn?original.Ui.CurrentEarnedProjection():original.Ui.CurrentBefore()))return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager?.IsClient!=true||manager.IsServer||!RebirthLogisticsOriginalTransport.CanSendNative(manager.connectionToServer?[0]))return;
        if(!RebirthProductiveEarnedUiOriginal.TryGetOwnedExplicitHandle(original.Worn?original.EarnedFlush:original.BeforeFlush,out byte handle))return;
        manager.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(original.Binding.Station,StreamModeWrite.ToServer,handle));
    }
}



