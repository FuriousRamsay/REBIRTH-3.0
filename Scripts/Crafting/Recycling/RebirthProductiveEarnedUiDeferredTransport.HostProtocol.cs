using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class HostOwnerSession
    {
        internal HydrationDispatch Hydration;internal RecipientScope Scope;
        internal HostOwnerFrame BeforeInput,AfterInput;
        internal string BeforeDigest,AfterDigest;internal bool Ready,RemovalArmed;
        internal HostTerminalPublication Final;
        internal HostClose CachedClosed;
    }
    private sealed class HostOwnerFrame
    {
        internal string Digest;internal byte[] Bytes;internal bool[] Received;internal int Count;
    }
    private static readonly Dictionary<Guid,HostOwnerSession> hostOwners=new Dictionary<Guid,HostOwnerSession>();
    // Loss of mutation permission does not release positively issued custody.
    // A replaced World/TE/incarnation never inherits this original hold.
    internal static bool HostReadyHold(TileEntityWorkstation station)
    {
        if(!ThreadManager.IsMainThread()||station==null)return false;
        foreach(var value in hostOwners.Values)
            if(value.Ready&&value.Final==null&&ReferenceEquals(value.Scope.Station,station)&&
                ReferenceEquals(GameManager.Instance?.World,value.Scope.World)&&
                ReferenceEquals(value.Scope.World.GetTileEntity(station.ToWorldPos()),station)&&
                RebirthLogisticsEndpointIdentity.TryGetIncarnation(value.Scope.World,station,out var birth)&&birth==value.Scope.CustodyBirth.ToString("N"))return true;
        return false;
    }
    internal static bool HasHostRecipientQueueAuthority=>ThreadManager.IsMainThread()&&hostOwners.Values.Any(value=>value.Final?.Current()==true);
    internal static bool TryGetReadyScope(Guid group,out RecipientScope scope)
    {
        scope=null;if(!ThreadManager.IsMainThread()||!hostOwners.TryGetValue(group,out var value)||!value.Ready||!value.Scope.Current())return false;
        scope=value.Scope;return true;
    }
    internal static bool TryGetHostRecipientQueueAuthority(Guid group,out RecipientScope scope,out HostTerminalPublication proof)
    {
        scope=null;proof=null;
        if(!ThreadManager.IsMainThread()||!hostOwners.TryGetValue(group,out var value)||value.Final?.Current()!=true)return false;
        scope=value.Scope;proof=value.Final;return true;
    }
    internal static bool ReleaseHostRecipientQueueAuthority(RebirthProductiveEarnedUiRemote.SavedQueueSuccessor successor)
    {
        if(!ThreadManager.IsMainThread()||successor==null||!successor.Current()||
            !hostOwners.TryGetValue(successor.Group,out var session)||session.Final==null||
            !ReferenceEquals(successor.PreviousScope,session.Scope)||!ReferenceEquals(successor.PreviousProof,session.Final)||
            !ReferenceEquals(successor.World,session.Scope.World)||!ReferenceEquals(successor.Station,session.Scope.Station)||
            successor.SuccessorOperation==session.Scope.Operation)return false;
        // The old full native removal body intentionally changed under the
        // positively accepted handoff. Its private saved-successor issuer now
        // proves the new Before; do not re-adopt it from body equality.
        if(!TryPrepareAuthorityNativeRetirements(successor,out var authority))return false;
        return ReleaseSettledHostOwnerResources(successor.Group,session,authority);
    }
    internal static bool ReleaseHostRecipientQueueAuthority(RebirthProductiveEarnedUiRemote.SavedEmptyRecipientQueue successor)
    {
        if(!ThreadManager.IsMainThread()||successor==null||!successor.Current()||
            !hostOwners.TryGetValue(successor.Group,out var session)||session.Final==null||
            !ReferenceEquals(successor.PreviousScope,session.Scope)||!ReferenceEquals(successor.PreviousProof,session.Final)||
            !ReferenceEquals(successor.World,session.Scope.World)||!ReferenceEquals(successor.Station,session.Scope.Station))return false;
        if(!TryPrepareAuthorityNativeRetirements(successor,out var authority))return false;
        return ReleaseSettledHostOwnerResources(successor.Group,session,authority);
    }
    private static bool ReleaseSettledHostOwnerResources(Guid group,HostOwnerSession session,AuthorityNativeRetirement[] authority)
    {
        // Owner progression settlement does not deliver the original cached
        // primary. Its separate physical custody must complete before a generic
        // queue successor can release this group's retained native handles.
        if(session.Hydration.Sources.Any(value=>value.CachedOriginal?.IsPhysicalPending==true||
            RebirthWorkstationCompletionPipeline.HasPendingRemoteCachedPrimary(value.NativeOriginal)))return false;
        if(session.Final.cachedMembers.Any(value=>value.IsPhysicalPending)||
            session.Final.remoteNativeMembers.Any(RebirthWorkstationCompletionPipeline.HasPendingRemoteCachedPrimary))return false;
        if(session.Final.loadedNativeMembers==null||session.Final.loadedNativeMembers.Any(value=>!value.HistoricalTerminalSaved))return false;
        if(session.Final.loadedUiMembers==null||session.Final.loadedUiMembers.Any(value=>!value.HistoricalTerminalSaved))return false;
        var native=session.Hydration.Sources.SelectMany(value=>value.ImmutableMembership.Element("members").Elements("member"))
            .Select(value=>ReadGuid(value,"operation")).Distinct().Select(operation=>host.TryGetValue(operation,out var source)?source:null)
            .Where(source=>source?.NativeOriginal!=null).ToArray();
        // Preserve metadata-only terminal queries for disconnected creators
        // before any completed original graph is released.
        var retirements=new List<RebirthWorkstationCompletionPipeline.NativeRetirement>(native.Length);
        foreach(var source in native)
        {
            if(!RebirthWorkstationCompletionPipeline.TryPrepareRetiredRemoteNativeOriginal(source.NativeOriginal,out var prepared))return false;
            retirements.Add(prepared);
        }
        // All durable query outcomes exist while the full group still qualifies.
        // Retirement below performs resource closure only, without further I/O.
        foreach(var prepared in retirements)prepared.Retire();
        foreach(var prepared in authority)prepared.Retire();
        hostOwners.Remove(group);
        if(session.BeforeInput!=null){session.BeforeInput.Bytes=null;session.BeforeInput.Received=null;}
        if(session.AfterInput!=null){session.AfterInput.Bytes=null;session.AfterInput.Received=null;}
        session.BeforeInput=session.AfterInput=null;
        if(removals.TryGetValue(session.Scope.Operation,out var removal)&&ReferenceEquals(removal.Scope,session.Scope))
        {removals.Remove(session.Scope.Operation);removal.Dispose();}
        session.Scope.Dispose();
        if(dispatches.TryGetValue(group,out var hydration)&&ReferenceEquals(hydration,session.Hydration))
        {dispatches.Remove(group);hydration.Close();}
        foreach(var source in native)source.Dispose();
        session.Scope=null;session.Hydration=null;session.Final=null;
        return true;
    }
    internal static void SweepHostOwnerWorld(World current)
    {
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,current))return;
        SweepCachedPhysicalGroups(current);
        foreach(var pair in hostOwners.Where(pair=>!ReferenceEquals(pair.Value.Hydration.World,current)).ToArray())
        {
            var value=pair.Value;hostOwners.Remove(pair.Key);
            if(value.BeforeInput!=null){value.BeforeInput.Bytes=null;value.BeforeInput.Received=null;}
            if(value.AfterInput!=null){value.AfterInput.Bytes=null;value.AfterInput.Received=null;}
            value.BeforeInput=value.AfterInput=null;value.Final=null;
            // Drop assembled graphs BEFORE their shared scope charge. Durable
            // original/Ready/consumed records remain held for qualified recovery.
            value.Scope.Dispose();value.Scope=null;value.Hydration=null;
        }
    }
    private static bool HostFramePart(HostOwnerSession session,NetPackageRebirthUiDeferredOwnerV1 packet,bool after,out XElement image)
    {
        image=null;var scope=session.Scope;
        if(packet.Total<1||packet.Total>NetPackageRebirthUiDeferredOwnerV1.Maximum||packet.Index<0||
            packet.Index>=(packet.Total+NetPackageRebirthUiDeferredOwnerV1.Chunk-1)/NetPackageRebirthUiDeferredOwnerV1.Chunk||
            packet.Part==null||packet.Part.Length!=Math.Min(NetPackageRebirthUiDeferredOwnerV1.Chunk,packet.Total-packet.Index*NetPackageRebirthUiDeferredOwnerV1.Chunk)||
            !scope.ReserveFrameInput(packet.Total,after))return false;
        var frame=after?session.AfterInput:session.BeforeInput;
        if(frame==null)
        {
            frame=new HostOwnerFrame{Digest=packet.Digest,Bytes=new byte[packet.Total],Received=new bool[(packet.Total+NetPackageRebirthUiDeferredOwnerV1.Chunk-1)/NetPackageRebirthUiDeferredOwnerV1.Chunk]};
            if(after)session.AfterInput=frame;else session.BeforeInput=frame;
        }
        if(frame.Digest!=packet.Digest||frame.Bytes.Length!=packet.Total)return false;
        int start=packet.Index*NetPackageRebirthUiDeferredOwnerV1.Chunk;
        if(frame.Received[packet.Index])
        {for(int index=0;index<packet.Part.Length;index++)if(frame.Bytes[start+index]!=packet.Part[index])return false;}
        else{Buffer.BlockCopy(packet.Part,0,frame.Bytes,start,packet.Part.Length);frame.Received[packet.Index]=true;frame.Count++;}
        if(frame.Count!=frame.Received.Length)return false;
        if(HydrationHash(frame.Bytes)!=frame.Digest)return false;
        // Bound the XML topology before constructing its detached object graph.
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=NetPackageRebirthUiDeferredOwnerV1.Maximum};
        using(var stream=new MemoryStream(frame.Bytes,false))
        {
            int nodes=0;using(var reader=XmlReader.Create(stream,settings))while(reader.Read())if(reader.Depth>64||++nodes>262144)throw new InvalidDataException();
            stream.Position=0;using(var reader=XmlReader.Create(stream,settings))image=XElement.Load(reader);
        }
        return true;
    }
    private static bool SaveHostOwnerStage(HostOwnerSession session,string stage,XElement evidence)
    {
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",session.Hydration.Group.ToString("N")+"."+stage+".xml");
        long bound=checked(65536+MeasurePublicationElement(evidence)+MeasurePublicationElement(session.Hydration.Header));
        return WriteWithPublicationWorkspace(session.Scope,path,bound,()=>new XElement("workstationCompletionIntent",new XAttribute("operation",session.Hydration.Group.ToString("N")),
            new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(session.Scope.World)),new XElement("before",
                new XElement("deferredNativeOwnerStage",new XAttribute("version",1),new XAttribute("stage",stage),
                    new XAttribute("group",session.Hydration.Group.ToString("N")),new XAttribute("operation",session.Scope.Operation.ToString("N")),
                    new XAttribute("nonce",session.Scope.Nonce.ToString("N")),new XAttribute("birth",session.Scope.Birth.ToString("N")),
                    new XAttribute("owner",session.Scope.Owner),new XAttribute("creation",session.Scope.Creation),
                    new XAttribute("entity",session.Scope.Actor.entityId),new XAttribute("deaths",session.Scope.Deaths),
                    new XElement("nativeHydration",new XElement(session.Hydration.Header)),new XElement("evidence",new XElement(evidence))))));

    }
    internal static void ReceiveOwnerRequest(NetPackageRebirthUiDeferredOwnerV1 packet,World world)
    {
        try
        {
            if(ReceiveHydrationMetadataControl(packet,world))return;
            if(ReceiveCachedPhysicalControl(packet,world))return;
            // This purpose qualifies its actual accepted native close before
            // allowing post-Unlock metadata. The ordinary mutation gate below
            // must remain strict for every other owner request.
            if(packet?.Action==5){ReceiveRemoteCloseRequest(packet,world);return;}
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||packet==null||packet.Sender==null||
                packet.Group==Guid.Empty||packet.Action>5||packet.Digest==null||packet.Digest.Length!=64||
                !dispatches.TryGetValue(packet.Group,out var hydration)||!hydration.Enqueued||!hydration.Current()||
                !ReferenceEquals(hydration.World,world)||!ReferenceEquals(hydration.Peer,packet.Sender)||hydration.Actor.entityId!=packet.Player||
                !packet.ValidEntityIdForSender(packet.Player))return;
            if(!hostOwners.TryGetValue(packet.Group,out var session))
            {
                if(packet.Action!=0||hostOwners.Count>=4||!RecipientScope.TryCreate(hydration,out var scope))return;
                session=new HostOwnerSession{Hydration=hydration,Scope=scope};hostOwners.Add(packet.Group,session);
            }
            if(!ReferenceEquals(session.Hydration,hydration)||!session.Scope.Current())return;
            if(packet.Action==0||packet.Action==2)
            {
                bool after=packet.Action==2;if(after&&!session.Ready)return;
                string acceptedDigest=after?session.AfterDigest:session.BeforeDigest;
                if(acceptedDigest!=null)
                {if(acceptedDigest!=packet.Digest)return;ReplyHostOwner(session);return;}
                if(!HostFramePart(session,packet,after,out var image))return;
                if(!after)
                {
                    if(!OwnerFrame(image)||!SaveHostOwnerStage(session,"before",image)||!session.Scope.BindBefore(image)||!OwnerPds.Arm(session.Scope,false))return;
                    session.BeforeDigest=packet.Digest;session.BeforeInput=null;
                }
                else
                {
                    if(!TryValidateConsumedReport(session.Scope,image,out var ownerAfter)||
                        !SaveHostOwnerStage(session,"consumed",image)||!session.Scope.BindObservedAfter(ownerAfter)||!OwnerPds.Arm(session.Scope,true))return;
                    session.AfterDigest=packet.Digest;session.AfterInput=null;
                }
            }
            else
            {
                if(packet.Total!=0||packet.Index!=0||packet.Part==null||packet.Part.Length!=0)return;
                string expected=packet.Action==1?session.BeforeDigest:session.AfterDigest??session.BeforeDigest;
                if(expected==null||packet.Digest!=expected)return;
                // Close alone is not a no-Check/consumption witness. Keep custody.
                if(packet.Action==5)return;
            }
            ReplyHostOwner(session);
        }
        catch{/* Malformed/unknown originals never issue Ready or effect permission. */}
    }
    private static void ReplyHostOwner(HostOwnerSession session)
    {
        if(!session.Scope.Current()||session.BeforeDigest==null)return;
        byte status=1;
        if(!session.Ready&&OwnerPds.TryReadBefore(session.Scope,out var before)&&before.Current())
        {
            var evidence=new XElement("deferredNativeOwnerReady",new XAttribute("version",1),new XAttribute("group",session.Hydration.Group.ToString("N")),
                new XAttribute("before",session.BeforeDigest),new XAttribute("owner",session.Scope.Owner),new XAttribute("creation",session.Scope.Creation),
                new XAttribute("entity",session.Scope.Actor.entityId),new XAttribute("deaths",session.Scope.Deaths),new XAttribute("nativeFile",before.Path),
                new XAttribute("nativeDigest",before.NativeDigest));
            if(!SaveHostOwnerStage(session,"ready",evidence))return;session.Ready=true;
        }
        if(session.Ready)status=2;
        if(session.AfterDigest!=null)
        {
            status=3;
            if(!session.RemovalArmed&&OwnerPds.TryReadAfter(session.Scope,out var after)&&after.Current()&&TryArmKnownRemoval(session.Scope))session.RemovalArmed=true;
            if(session.RemovalArmed)status=4;
            if(session.Final==null&&session.RemovalArmed&&TryFinalizeHostGroup(session.Scope,out var final))session.Final=final;
            if(session.Final?.Current()==true)status=5;
        }
        string digest=status>=3?session.AfterDigest:session.BeforeDigest;
        session.Hydration.Peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerReplyV1>().Setup(session.Hydration.Group,status,digest));
    }
}



