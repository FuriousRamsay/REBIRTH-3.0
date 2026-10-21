using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    [ThreadStatic]private static HostTerminalPublication acceptingCachedOwnerClose;
    internal static string HydrationHashForCachedRetirement(byte[] bytes)=>bytes==null?null:HydrationHash(bytes);
    internal static bool IsAcceptingCachedOwnerClose(HostTerminalPublication proof)=>proof!=null&&ReferenceEquals(proof,acceptingCachedOwnerClose);
    private sealed class CachedGroupFinal:IDisposable
    {
        internal HostOwnerSession Session;internal RebirthWorkstationCompletionPipeline.CachedPhysicalPublication[] Sources;
        internal byte[] Image;internal string Path,Digest;internal IDisposable Allocation;
        internal HostCachedPhysicalReceived Received;
        internal bool Current()=>Session?.Final!=null&&Sources!=null&&Sources.Length>0&&Sources.All(value=>
            value.CurrentSavedSource()&&ReferenceEquals(value.Previous,Session.Final))&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image);
        public void Dispose(){Received?.Clear();Received=null;Session=null;Sources=null;Image=null;Allocation?.Dispose();Allocation=null;}
    }
    private static readonly Dictionary<Guid,CachedGroupFinal> cachedGroupFinals=new Dictionary<Guid,CachedGroupFinal>();
    private static bool HoldsCachedPhysicalOwnerGroup(TileEntityWorkstation station)=>station!=null&&hostOwners.Values.Any(session=>session.Final!=null&&
        ReferenceEquals(session.Scope.Station,station)&&ReferenceEquals(GameManager.Instance?.World,session.Scope.World)&&
        ReferenceEquals(session.Scope.World.GetTileEntity(station.ToWorldPos()),station)&&
        RebirthLogisticsEndpointIdentity.TryGetIncarnation(session.Scope.World,station,out var birth)&&birth==session.Scope.CustodyBirth.ToString("N")&&
        (session.Final.cachedMembers.Any(value=>value.IsPhysicalPending)||session.Final.remoteNativeMembers.Any(RebirthWorkstationCompletionPipeline.HasRetainedRemoteCachedPrimary)||
            cachedGroupFinals.ContainsKey(session.Hydration.Group)));
    private static bool RetainClosedCachedPhysicalGroup(HostClose close)
    {
        var session=close?.Session;
        if(session?.Final==null||!ReferenceEquals(session.Final,close.Terminal)||close.Mode!="consumed"||
            !close.AcceptedNative||close.Observed==null||close.Retired==null||
            !session.Final.cachedMembers.Any()&&!session.Final.remoteNativeMembers.Any(RebirthWorkstationCompletionPipeline.HasRetainedRemoteCachedPrimary))return false;
        session.CachedClosed=close;return true;
    }
    private static bool RetainsClosedCachedPhysicalGroup(HydrationDispatch dispatch)=>hostOwners.TryGetValue(dispatch.Group,out var session)&&
        ReferenceEquals(session.Hydration,dispatch)&&ClosedCachedGroupCurrent(session);
    private static bool ClosedCachedGroupCurrent(HostOwnerSession session)
    {
        var close=session?.CachedClosed;
        return close!=null&&close.AcceptedNative&&close.Finished&&ReferenceEquals(close.Session,session)&&ReferenceEquals(close.Terminal,session.Final)&&
            ReferenceEquals(GameManager.Instance?.World,close.World)&&ReferenceEquals(close.World.GetTileEntity(close.Station.ToWorldPos()),close.Station)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(close.World,close.Station,out var birth)&&birth==close.Birth.ToString("N")&&
            RebirthProductiveEarnedArtifact.FileMatches(close.Path+".header",close.Header)&&RebirthProductiveEarnedArtifact.FileMatches(close.Path+".native",close.Accepted)&&
            RebirthProductiveEarnedArtifact.FileMatches(close.Path+".observed",close.Observed)&&RebirthProductiveEarnedArtifact.FileMatches(close.Path+".retired",close.Retired);
    }
    private static void SweepCachedPhysicalGroups(World world)
    {foreach(var pair in cachedGroupFinals.Where(value=>!ReferenceEquals(value.Value.Session.Scope.World,world)).ToArray())
        {cachedGroupFinals.Remove(pair.Key);pair.Value.Dispose();}
        foreach(var group in clientCachedPhysicalReceipts.Keys.Where(value=>!clientHydrations.TryGetValue(value,out var source)||!ReferenceEquals(source.World,world)).ToArray())clientCachedPhysicalReceipts.Remove(group);}
    private static readonly Dictionary<Guid,string> clientCachedPhysicalReceipts=new Dictionary<Guid,string>();
    private static bool AcceptCachedPhysicalReply(NetPackageRebirthUiDeferredOwnerReplyV1 packet,World world)
    {
        if(packet==null||packet.Status<7)return false;
        if(AcceptHistoricalCachedRecipientReply(packet,world))return true;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!clientHydrations.TryGetValue(packet.Group,out var original)||
            !ReferenceEquals(original.World,world)||!original.CurrentSource()||!TryGetReceivedConnection(packet,out var connection)||
            !ReferenceEquals(connection,original.Connection)||original.Terminal?.CurrentSavedSource()!=true)return true;
        var actual=original.CachedPhysicalObservation;
        if(actual?.Current()!=true||actual.Digest!=packet.Digest)return true;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,actual.Body,actual.Offset))return true;
        string path=original.Path+".cached-physical-final";
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Group.ToString("N")),
            new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(world)),new XElement("before",
                new XElement("cachedPhysicalRecipientFinal",new XAttribute("version",1),new XAttribute("group",original.Group.ToString("N")),
                    new XAttribute("native",actual.Digest),new XAttribute("observation",actual.Path),new XAttribute("observationDigest",HydrationHash(actual.Image)),
                    new XAttribute("owner",original.Owner),new XAttribute("creation",original.Creation))));
        if(packet.Status==7)
        {
            if(clientCachedPhysicalReceipts.Count>=4&&!clientCachedPhysicalReceipts.ContainsKey(packet.Group)||
                !RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return true;
            clientCachedPhysicalReceipts[packet.Group]=packet.Digest;
            SendOwnerControl(original,6,null,packet.Digest);return true;
        }
        if(!clientCachedPhysicalReceipts.TryGetValue(packet.Group,out string digest)||digest!=packet.Digest||
            !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return true;
        var acknowledged=CachedRecipientAcknowledgement(original.Group,RebirthWorkstationCompletionOutbox.CurrentGuid(world),digest,original.Owner,original.Creation);
        if(!RebirthWorkstationCompletionPublication.Write(path+".ack",acknowledged))return true;
        clientCachedPhysicalReceipts.Remove(packet.Group);clientHydrations.Remove(packet.Group);original.Close();return true;
    }
    private static bool PumpCachedPhysicalRecipient(ClientHydration original)
    {
        if(!clientCachedPhysicalReceipts.TryGetValue(original.Group,out string digest))return false;
        SendOwnerControl(original,6,null,digest);return true;
    }
    private static void PumpClosedCachedPhysicalGroups()
    {
        foreach(var final in cachedGroupFinals.Values.ToArray())
            if(final.Current()&&ClosedCachedGroupCurrent(final.Session))
            {var received=HostCachedPhysicalReceived.AcceptClosed(final);if(received!=null)CommitCachedPhysicalGroup(final,received,null);}
    }
    private static bool ReceiveCachedPhysicalControl(NetPackageRebirthUiDeferredOwnerV1 packet,World world)
    {
        if(packet==null||packet.Action!=6&&packet.Action!=4)return false;
        if(!cachedGroupFinals.TryGetValue(packet.Group,out var final))return packet.Action==6&&ReplyRetiredCachedPhysical(packet,world);
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,final.Session.Scope.World)||
            packet.Sender==null||!packet.ValidEntityIdForSender(packet.Player)||
            packet.Total!=0||packet.Index!=0||packet.Part?.Length!=0||!final.Current())return true;
        if(packet.Action==4)
        {
            if(ReferenceEquals(packet.Sender,final.Session.Scope.Peer)&&packet.Player==final.Session.Scope.Actor.entityId&&packet.Digest==final.Session.AfterDigest)
                packet.Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerReplyV1>().Setup(packet.Group,7,final.Digest));
            return true;
        }
        var received=HostCachedPhysicalReceived.Accept(final,packet);if(received==null)return true;
        CommitCachedPhysicalGroup(final,received,packet.Sender);return true;
    }
    private static bool CommitCachedPhysicalGroup(CachedGroupFinal final,HostCachedPhysicalReceived received,ClientInfo peer)
    {
        if(!received.Current())return false;
        if(final.Session.Final.loadedNativeMembers==null||final.Session.Final.loadedNativeMembers.Any(value=>!value.HistoricalTerminalSaved))return false;
        if(final.Session.Final.loadedUiMembers==null||final.Session.Final.loadedUiMembers.Any(value=>!value.HistoricalTerminalSaved))return false;
        if(!AuthorityNativeRetirement.FromCached(received,out var authority))return false;
        Guid group=received.Group;var world=final.Session.Scope.World;
        // Every durable resource retirement is prepared while all original
        // graphs remain live. A failed later preparation leaves the group
        // retryable; no earlier original is closed as a side effect.
        var cached=new List<RebirthWorkstationCompletionPipeline.CachedGroupRetirement>();
        var native=new List<RebirthWorkstationCompletionPipeline.NativeRetirement>();
        foreach(var source in final.Sources)
        {
            var prepared=RebirthWorkstationCompletionPipeline.CachedGroupRetirement.Prepare(source,received);if(prepared==null)return true;cached.Add(prepared);
        }
        foreach(var original in final.Session.Final.remoteNativeMembers.Distinct())
        {
            var successor=final.Sources.FirstOrDefault(value=>ReferenceEquals(value.NativeSource,original))??final.Sources[0];
            if(!RebirthWorkstationCompletionPipeline.TryPrepareRetiredRemoteNativeOriginal(original,successor,out var retired))return false;
            native.Add(retired);
        }
        // All required immutable query outcomes now exist. The commit below
        // performs only resource closure, never native financial operations.
        var commit=new XElement("workstationCompletionIntent",new XAttribute("operation",group.ToString("N")),
            new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(world)),new XElement("before",
                new XElement("cachedPhysicalResourcesCommitted",new XAttribute("version",1),new XAttribute("group",group.ToString("N")),
                    new XAttribute("final",HydrationHash(final.Image)),new XAttribute("native",final.Digest))));
        if(!RebirthWorkstationCompletionPublication.Write(final.Path+".resource-commit",commit)||
            !XNode.DeepEquals(commit,RebirthWorkstationCompletionPublication.ReadPrimary(final.Path+".resource-commit")))return true;
        var session=final.Session;string digest=final.Digest;
        var memberOperations=session.Hydration.Sources.SelectMany(value=>value.ImmutableMembership.Element("members").Elements("member"))
            .Select(value=>ReadGuid(value,"operation")).ToArray();
        var contributions=host.Values.Where(value=>memberOperations.Contains(value.Operation)&&ReferenceEquals(value.World,session.Scope.World)&&
            ReferenceEquals(value.Station,session.Scope.Station)).Concat(session.Hydration.Sources).Distinct().ToArray();var close=session.CachedClosed;
        foreach(var retired in native)retired.Retire();
        foreach(var retired in authority)retired.Retire();
        foreach(var retired in cached)retired.Retire();
        cachedGroupFinals.Remove(group);hostOwners.Remove(group);
        if(removals.TryGetValue(session.Scope.Operation,out var removal)&&ReferenceEquals(removal.Scope,session.Scope)){removals.Remove(session.Scope.Operation);removal.Dispose();}
        if(dispatches.TryGetValue(group,out var dispatch)&&ReferenceEquals(dispatch,session.Hydration)){dispatches.Remove(group);dispatch.Close();}
        session.BeforeInput=session.AfterInput=null;session.Scope.Dispose();session.Scope=null;session.Hydration=null;session.Final=null;final.Dispose();
        foreach(var contribution in contributions)contribution.Dispose();
        if(close!=null){hostCloses.Remove(group);close.Session=null;close.Terminal=null;close.Owner=null;close.NativeBefore=null;close.NativeBeforeNative=null;close.Dispose();session.CachedClosed=null;}
        peer?.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerReplyV1>().Setup(group,8,digest));
        return true;
    }
    private static bool ReplyRetiredCachedPhysical(NetPackageRebirthUiDeferredOwnerV1 packet,World world)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||packet.Sender==null||packet.Total!=0||packet.Index!=0||packet.Part?.Length!=0||
            !packet.ValidEntityIdForSender(packet.Player)||!RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out var identity))return false;
        using(var memory=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
        if(memory==null)return false;
        string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",packet.Group.ToString("N")+".cached-physical-final.xml");
        var final=RebirthWorkstationCompletionPublication.ReadPrimary(path);var receipt=RebirthWorkstationCompletionPublication.ReadPrimary(path+".received");
        var saved=final?.Element("before")?.Element("cachedPhysicalGroupSaved");var received=receipt?.Element("before")?.Element("cachedPhysicalGroupReceived");
        var actor=world.GetEntity(packet.Player) as EntityPlayer;
        var commit=RebirthWorkstationCompletionPublication.ReadPrimary(path+".resource-commit")?.Element("before")?.Element("cachedPhysicalResourcesCommitted");
        if(saved==null||received==null||actor==null||actor.IsDead()||(string)saved.Attribute("owner")!=identity.StorageKey||
            (string)saved.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||(string)saved.Attribute("native")!=packet.Digest||
            (string)received.Attribute("native")!=packet.Digest||(string)received.Attribute("final")!=HydrationHash(RebirthWorkstationCompletionPublication.CopyPublishedBytes(path))||
            commit==null||(string)commit.Attribute("final")!=(string)received.Attribute("final")||(string)commit.Attribute("native")!=packet.Digest)return false;
        // A received record alone precedes retirement preparation. Every
        // member must also have its immutable compact retirement outcome.
        foreach(var member in saved.Elements("physical"))
        {
            string publication=(string)member.Attribute("path");
            if(publication==null||!publication.EndsWith(".physical-publication",StringComparison.Ordinal))return false;
            string retired=publication.Substring(0,publication.Length-".physical-publication".Length)+".group-physical-retired";
            var retirement=RebirthWorkstationCompletionPublication.ReadPrimary(retired)?.Element("cachedGroupPhysicalRetired");
            if(retirement==null||(string)retirement.Attribute("group")!=packet.Group.ToString("N"))return false;
        }
        packet.Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerReplyV1>().Setup(packet.Group,8,packet.Digest));return true;
        }
    }
    // Called only with a privately issued, actual Process+saved native+PDS+
    // durable outbox successor. Historical owner settlement is not delivery.
    internal static void ObserveSavedCachedPhysicalPublication(RebirthWorkstationCompletionPipeline.CachedPhysicalPublication publication)
    {
        if(publication?.CurrentSavedSource()!=true||publication.Previous==null)return;
        Guid group=publication.Previous.RecipientGroup;
        if(!hostOwners.TryGetValue(group,out var session)||!ReferenceEquals(session.Final,publication.Previous)||cachedGroupFinals.ContainsKey(group))return;
        var sources=new List<RebirthWorkstationCompletionPipeline.CachedPhysicalPublication>();
        foreach(var cached in session.Final.cachedMembers)
        {var physical=cached.SavedPhysical;if(physical?.CurrentSavedSource()!=true)return;sources.Add(physical);}
        foreach(var native in session.Final.remoteNativeMembers)
        {
            if(!RebirthWorkstationCompletionPipeline.TryGetRemoteCachedPhysicalPublication(native,out var physical,out bool known))
            {if(known)return;continue;}
            if(!sources.Contains(physical))sources.Add(physical);
        }
        if(sources.Count<1||sources.Count>4||!sources.Contains(publication)||sources.Any(value=>!ReferenceEquals(value.Previous,session.Final)))return;
        var latest=sources.OrderByDescending(value=>value.NativeReadFence).First();
        long bound=checked(65536+sources.Sum(value=>value.MeasureImage()));
        var allocation=session.Scope.TryEnterPublicationWorkspace(bound);if(allocation==null)return;
        try
        {
            var evidence=new XElement("cachedPhysicalGroupSaved",new XAttribute("version",1),new XAttribute("group",group.ToString("N")),
                new XAttribute("owner",session.Scope.Owner),new XAttribute("creation",session.Scope.Creation),
                new XAttribute("native",latest.NativeDigest),new XAttribute("readFence",latest.NativeReadFence),
                sources.Select(value=>new XElement("physical",new XAttribute("operation",value.Operation.ToString("N")),
                    new XAttribute("path",value.Path),new XAttribute("digest",HydrationHash(value.CopyImage())))));
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",group.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(session.Scope.World)),new XElement("before",evidence));
            string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",group.ToString("N")+".cached-physical-final.xml");
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
            var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(image==null)return;
            cachedGroupFinals.Add(group,new CachedGroupFinal{Session=session,Sources=sources.ToArray(),Path=path,Image=image,Digest=latest.NativeDigest,Allocation=allocation});allocation=null;
        }
        finally{allocation?.Dispose();}
    }
    internal sealed class HostCachedPhysicalReceived
    {
        private CachedGroupFinal source;private byte[] image;private readonly string path;
        internal Guid Group=>source.Session.Hydration.Group;
        internal HostTerminalPublication Previous=>source.Session.Final;
        private HostCachedPhysicalReceived(CachedGroupFinal final,string receiptPath,byte[] receipt){source=final;path=receiptPath;image=receipt;}
        internal void Clear(){source=null;image=null;}
        internal static HostCachedPhysicalReceived AcceptClosed(object original)
        {
            var final=original as CachedGroupFinal;
            if(final==null||!final.Current()||!ClosedCachedGroupCurrent(final.Session))return null;
            if(final.Received?.Current()==true)return final.Received;
            var close=final.Session.CachedClosed;var scope=final.Session.Scope;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",close.Group.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(close.World)),new XElement("before",
                    new XElement("cachedPhysicalGroupReceived",new XAttribute("version",1),new XAttribute("group",close.Group.ToString("N")),
                        new XAttribute("final",HydrationHash(final.Image)),new XAttribute("native",final.Digest),new XAttribute("owner",scope.Owner),
                        new XAttribute("creation",scope.Creation),new XAttribute("actualConsumedClose",HydrationHash(close.Retired)))));
            string path=final.Path+".received";
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return null;
            var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);return image==null?null:final.Received=new HostCachedPhysicalReceived(final,path,image);
        }
        internal bool Current()=>source?.Current()==true&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,image);
        internal long MeasureImage()=>checked(image.LongLength+source.Image.LongLength);
        internal byte[] CopyImage()=>Current()?(byte[])image.Clone():null;
        internal byte[] CopyFinal()=>Current()?(byte[])source.Image.Clone():null;
        // Issuance is nested inside the actual authenticated request handler;
        // neither a parsed receipt nor a getter can construct this token.
        internal static HostCachedPhysicalReceived Accept(object original,NetPackageRebirthUiDeferredOwnerV1 packet)
        {
            var final=original as CachedGroupFinal;
            if(final==null||!final.Current()||packet.Action!=6||packet.Digest!=final.Digest)return null;
            var scope=final.Session.Scope;
            // A new authenticated session may acknowledge historical metadata.
            // It never replaces scope.Actor/Peer or receives native action,
            // receipt removal, PDS upload or primary restoration permission.
            var actor=scope.World.GetEntity(packet.Player) as EntityPlayer;
            if(packet.Sender==null||!packet.ValidEntityIdForSender(packet.Player)||actor==null||actor.IsDead()||
                !ReferenceEquals(scope.World,GameManager.Instance?.World)||RebirthCamperCharacterAccess.CurrentCreation(actor)!=scope.Creation||
                !RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out var identity)||identity.StorageKey!=scope.Owner)return null;
            if(final.Received?.Current()==true)return final.Received;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",packet.Group.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(scope.World)),new XElement("before",
                    new XElement("cachedPhysicalGroupReceived",new XAttribute("version",1),new XAttribute("group",packet.Group.ToString("N")),
                        new XAttribute("final",HydrationHash(final.Image)),new XAttribute("native",final.Digest),new XAttribute("owner",scope.Owner),new XAttribute("creation",scope.Creation))));
            string path=final.Path+".received";
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return null;
            var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);return image==null?null:final.Received=new HostCachedPhysicalReceived(final,path,image);
        }
    }
}
