using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed class CachedUnusedHost:IDisposable
    {
        internal CachedPhysicalHostAction Action;internal CachedCustody Cache;internal Guid Close,Dispatch;
        internal string Digest,Path;internal XElement Header;
        internal byte[] Before,After,HeaderImage,AcceptedImage,SavedImage;internal int BeforeOffset,AfterOffset;
        internal long ReadFence;internal bool NativeAccepted;internal IDisposable Lease;
        internal Guid Operation;internal int NextEpoch;
        internal EntityPlayer Viewer;internal ClientInfo Sender;internal INetConnection Connection;internal string Owner,Creation;internal int Deaths;
        internal bool TerminalPeer(EntityPlayer viewer,ClientInfo sender)=>ReferenceEquals(viewer,Viewer)&&ReferenceEquals(sender,Sender)&&
            ReferenceEquals(GameManager.Instance?.World,Cache.World)&&ReferenceEquals(Cache.World.GetTileEntity(Cache.Station.ToWorldPos()),Cache.Station)&&
            ReferenceEquals(Cache.World.GetEntity(viewer.entityId),viewer)&&!viewer.IsDead()&&viewer.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(viewer)==Creation&&
            ReferenceEquals(sender.netConnection?[0],Connection)&&!Connection.IsDisconnected()&&RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var identity)&&identity.StorageKey==Owner;
        internal bool Identity()=>Cache.Active&&Cache.SourceCurrent()&&Action?.Issue?.RecordedIdentity()==true&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,HeaderImage);
        internal bool AcceptedCurrent()=>NativeAccepted&&Identity()&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path+".accepted",AcceptedImage)&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(Cache.World,Cache.Station,out var fence)&&fence==ReadFence;
        public void Dispose(){Before=After=HeaderImage=AcceptedImage=SavedImage=null;Header=null;Action=null;Cache=null;Lease?.Dispose();Lease=null;}
    }
    private sealed class CachedUnusedRead
    {internal CachedUnusedHost Source;internal NetPackageTileEntity Packet;internal PooledBinaryReader Reader;internal int Reads;internal bool Complete;internal long Fence;}
    [ThreadStatic]private static CachedUnusedRead readingCachedUnused;
    private static readonly Dictionary<Guid,CachedUnusedHost> cachedUnusedHosts=new Dictionary<Guid,CachedUnusedHost>();
    private static CachedUnusedHost PrepareCachedUnusedHost(EntityPlayer viewer,ClientInfo sender,Guid operation,XElement header)
    {
        if(header?.Name!="cachedUnusedClose"||(string)header.Attribute("phase")!="prepare"||
            !Guid.TryParseExact((string)header.Attribute("close"),"N",out var close)||close==Guid.Empty||
            (long?)header.Attribute("nativeVersion") is not long version||version<0||
            (long?)header.Attribute("session") is not long session||session<1||
            !cachedCustodies.TryGetValue(operation,out var cache))return null;
        var canonical=new XElement(header);canonical.Attribute("challenge")?.Remove();
        if(canonical.Attributes().Count()!=9||canonical.Elements().Count()!=3||canonical.Elements("nativeOwnerFrame").Count()!=1||
            canonical.Elements("before").Count()!=1||canonical.Elements("after").Count()!=1||(bool?)canonical.Attribute("primary") is not bool)return null;
        string digest=RebirthWorkstationCompletionOriginal.Hash(canonical.ToString(SaveOptions.DisableFormatting));
        cachedUnusedHosts.TryGetValue(operation,out var prior);
        if(prior!=null&&prior.Close==close)return prior.Digest==digest&&prior.Identity()?prior:null;
        if(prior!=null&&(prior.SavedImage==null||!ReferenceEquals(prior.Cache,cache)||!ReferenceEquals(prior.Sender,sender)||
            !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(prior.Path+".saved",prior.SavedImage)))return null;
        if(prior==null&&cachedUnusedHosts.Count>=4)return null;
        if(!cachedPhysicalActions.TryGetValue(operation,out var action))
        {
            var request=new XElement("cachedPhysicalPrepare",new XAttribute("ticket",(string)header.Attribute("ticket")),new XAttribute("group",(string)header.Attribute("group")),
                new XAttribute("action",(string)header.Attribute("action")),new XAttribute("original",(string)header.Attribute("original")),new XAttribute("session",session));
            PrepareCachedPhysical(viewer,sender,operation,request);
            if(!cachedPhysicalActions.TryGetValue(operation,out action))return null;
        }
        if(!action.Current()||!cache.NativeCurrent()||action.PhysicalAccepted||!ReferenceEquals(action.Viewer,viewer)||!ReferenceEquals(action.Sender,sender)||
            (string)header.Attribute("ticket")!=action.Issue.Ticket.ToString("N")||(string)header.Attribute("group")!=action.Issue.Group.ToString("N")||
            (string)header.Attribute("action")!=action.Action.ToString("N")||(string)header.Attribute("original")!=action.SourceHash||session!=action.Session)return null;
        long measure=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(header);
        if(!cache.ReserveRetirementWorkspace(checked(16*measure+65536)))return null;
        var before=ReadCachedSpaceBody(header,"before");var after=ReadCachedSpaceBody(header,"after");
        int beforeOffset=(int)header.Element("before").Attribute("offset"),afterOffset=(int)header.Element("after").Attribute("offset");
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,cache.Native,cache.Offset)||
                !RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var first,out ulong elapsed)||
                !RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var proposed,out _))return null;
            int slot=(int)action.Before.Attribute("slot");if(slot<0||slot>=first.Queue.Length)return null;
            if((bool?)header.Attribute("primary")==true)
            {first.Queue[slot].CraftingTimeLeft=(float)cache.CurrentFull.Attribute("cachedTime");first.Queue[slot].IsCrafting=(bool)cache.CurrentFull.Attribute("cachedCrafting");}
            var expected=RebirthProductiveEarnedNativeBody.Encode(first,elapsed,out int offset);
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(expected,offset,after,afterOffset))return null;
        }
        var scope=new CachedUnusedHost{Action=action,Cache=cache,Close=close,Digest=digest,Header=canonical,Before=before,After=after,
            BeforeOffset=beforeOffset,AfterOffset=afterOffset,Path=cache.Path+".unused-close."+close.ToString("N"),Lease=cache.RetainResource(),
            Operation=operation,Viewer=viewer,Sender=sender,Connection=action.Issue.Connection,Owner=action.ViewerKey,Creation=action.ViewerCreation,Deaths=action.ViewerDeaths};
        if(scope.Lease==null)return null;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",cache.WorldGuid),new XElement(cache.Original.Element("before")),new XElement(canonical));
        if(!RebirthWorkstationCompletionPublication.Write(scope.Path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(scope.Path)))
        {scope.Dispose();return null;}
        scope.HeaderImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(scope.Path);if(scope.HeaderImage==null){scope.Dispose();return null;}
        // A new actual grant/action can exist only after the client's old
        // unused-close ACK retired its old source. Preserve the immutable old
        // query files, dropping only the positively saved predecessor graph.
        if(prior!=null){cachedUnusedHosts.Remove(operation);prior.Dispose();}
        cachedUnusedHosts.Add(operation,scope);return scope;
    }
    private static void EnterCachedUnusedRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        var read=readingCachedUnused;if(read==null)return;
        if(!run||mode!=StreamModeRead.FromClient||!ReferenceEquals(station,read.Source.Cache.Station)||!ReferenceEquals(reader.BaseStream,read.Packet.ms)||
            !read.Source.Action.Current()||++read.Reads!=1)throw new InvalidDataException();read.Reader=reader;
    }
    private static void ObserveCachedUnusedRead(PooledBinaryReader reader,bool run)
    {
        var read=readingCachedUnused;if(read==null||!run||!ReferenceEquals(reader,read.Reader)||read.Reads!=1||
            !ReferenceEquals(reader.BaseStream,read.Packet.ms)||reader.BaseStream.Position!=reader.BaseStream.Length||!read.Source.Identity()||
            !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(read.Source.Cache.World,read.Source.Cache.Station,out var fence))return;
        using(var callback=read.Source.Cache.Memory.TryEnterCallbacks())
            if(callback==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(read.Source.Cache.Station,read.Source.After,read.Source.AfterOffset))return;
        read.Fence=fence;read.Complete=true;
    }
    private static void ReceiveCachedUnusedNative(XElement header,byte[] native,Guid dispatch,World world,GameManager callbacks,ClientInfo sender)
    {
        if((int?)header.Attribute("phase")!=6||!Guid.TryParseExact((string)header.Attribute("operation"),"N",out var operation)||
            header.Elements("cachedUnusedCloseSource").Count()!=1||header.Elements("cachedPhysicalSource").Any()||header.Elements("sameOwnerEffect").Any()||header.Elements("deferredNativeContribution").Any())return;
        var section=header.Element("cachedUnusedCloseSource");var manifest=section.Element("cachedUnusedClose");
        var viewer=world.GetEntity(sender.entityId) as EntityPlayer;var scope=PrepareCachedUnusedHost(viewer,sender,operation,manifest);
        if(scope==null||scope.NativeAccepted||(string)section.Attribute("close")!=scope.Close.ToString("N")||
            (string)section.Attribute("header")!=scope.Digest||(string)header.Attribute("nonce")!=scope.Action.Action.ToString("N"))return;
        var packet=NetPackageManager.GetPackage<NetPackageTileEntity>();
        try
        {
            using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);if(reader.ReadUInt16()!=packet.PackageId)throw new InvalidDataException();packet.read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();}
            packet.Sender=sender;
            if(!scope.Action.Current()||!packet.teWorldPos.Equals(scope.Cache.Station.ToWorldPos())||packet.teBlockId!=scope.Cache.Station.blockValue.type||
                packet.handle!=scope.Cache.Station.lockHandleWaitingFor||packet.ms==null||packet.ms.Length!=scope.After.Length)return;
            byte[] body=new byte[scope.After.Length];
            lock(packet.ms){long prior=packet.ms.Position;try{packet.ms.Position=0;int n=0;while(n<body.Length){int read=packet.ms.Read(body,n,body.Length-n);if(read==0)throw new EndOfStreamException();n+=read;}}finally{packet.ms.Position=prior;}}
            int offset=(int)header.Attribute("elapsedOffset");
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(body,offset,scope.After,scope.AfterOffset))return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryRead(body,offset,out _,out ulong elapsed)||elapsed>GameTimer.Instance.ticks)return;
            var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",scope.Cache.WorldGuid),
                new XElement(scope.Cache.Original.Element("before")),
                new XElement("cachedUnusedNativeAttempt",new XAttribute("dispatch",dispatch.ToString("N")),new XAttribute("header",scope.Digest)));
            if(!RebirthWorkstationCompletionPublication.Write(scope.Path+".attempt",attempt)||!XNode.DeepEquals(attempt,RebirthWorkstationCompletionPublication.ReadPrimary(scope.Path+".attempt")))return;
            var readState=new CachedUnusedRead{Source=scope,Packet=packet};nativeProcessed.Add(dispatch,world);readingCachedUnused=readState;
            try
            {
                packet.ProcessPackage(world,callbacks);
                if(!readState.Complete||readState.Reads!=1||!scope.Identity()||
                    !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(world,scope.Cache.Station,out var fence)||fence!=readState.Fence)return;
                using(var callback=scope.Cache.Memory.TryEnterCallbacks())
                    if(callback==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(scope.Cache.Station,body,offset))return;
                var accepted=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",scope.Cache.WorldGuid),
                    new XElement(scope.Cache.Original.Element("before")),
                    new XElement("cachedUnusedNativeAccepted",new XAttribute("dispatch",dispatch.ToString("N")),new XAttribute("header",scope.Digest),new XAttribute("readFence",fence),
                        new XElement("native",new XAttribute("offset",offset),Convert.ToBase64String(body))));
                if(!RebirthWorkstationCompletionPublication.Write(scope.Path+".accepted",accepted)||!XNode.DeepEquals(accepted,RebirthWorkstationCompletionPublication.ReadPrimary(scope.Path+".accepted")))return;
                var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(scope.Path+".accepted");if(image==null)return;
                scope.After=body;scope.AfterOffset=offset;scope.ReadFence=fence;scope.Dispatch=dispatch;scope.AcceptedImage=image;scope.NativeAccepted=true;
            }
            finally{readingCachedUnused=null;}
        }
        finally{NetPackageManager.FreePackage(packet);}
    }
    private static void ReceiveCachedUnusedClose(EntityPlayer viewer,ClientInfo sender,Guid operation,XElement report)
    {
        if((string)report.Attribute("phase")!="observed"||!cachedUnusedHosts.TryGetValue(operation,out var scope)||!scope.TerminalPeer(viewer,sender)||
            (string)report.Attribute("close")!=scope.Close.ToString("N")||(string)report.Attribute("header")!=scope.Digest||
            (string)report.Attribute("dispatch")!=scope.Dispatch.ToString("N"))return;
        var cache=scope.Cache;
        if(scope.SavedImage==null)
        {
            if(!scope.AcceptedCurrent())return;
            using(var callback=cache.Memory.TryEnterCallbacks())
                if(callback==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(cache.Station,scope.After,scope.AfterOffset))return;
            var canonical=new XElement(report);canonical.Attribute("challenge")?.Remove();
            var observed=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                new XElement(cache.Original.Element("before")),
                new XElement("cachedUnusedCloseObserved",new XAttribute("original",HistoricalHash(cache.Image)),new XAttribute("header",scope.Digest),
                    new XElement(canonical),new XElement(scope.Header),new XElement("accepted",new XAttribute("path",scope.Path+".accepted"),
                        new XAttribute("digest",HistoricalHash(scope.AcceptedImage)))));
            if(!RebirthWorkstationCompletionPublication.Write(scope.Path+".observed",observed)||
                !XNode.DeepEquals(observed,RebirthWorkstationCompletionPublication.ReadPrimary(scope.Path+".observed")))return;
            if(scope.NextEpoch==0)
            {
                if(cache.Epoch>=64)return;int next=cache.Epoch+1;
                var guard=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                    new XElement(cache.Original.Element("before")),
                    new XElement("cachedUnusedGuard",new XAttribute("original",HistoricalHash(cache.Image)),new XAttribute("epoch",next),new XAttribute("path",scope.Path)));
                string path=cache.Path+".remote-action."+next;
                if(!RebirthWorkstationCompletionPublication.Write(path,guard)||!XNode.DeepEquals(guard,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
                string image=RebirthWorkstationCompletionPublication.ReadEvidence(scope.Path+".observed");if(image==null)return;
                scope.NextEpoch=next;cache.Native=scope.After;cache.Offset=scope.AfterOffset;cache.Saved=false;cache.Watch?.Dispose();cache.Watch=null;
                cache.Epoch=next;cache.SpacePath=scope.Path+".observed";cache.SpaceImage=image;
            }
            cache.Chunk.isModified=true;
            if(!cache.TrySave()||!cache.SavedCurrent()||!cache.NativeCurrent())return;
            var saved=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                new XElement(cache.Original.Element("before")),
                new XElement("cachedUnusedCloseSaved",new XAttribute("original",HistoricalHash(cache.Image)),new XAttribute("epoch",scope.NextEpoch),
                    new XAttribute("path",scope.Path),new XAttribute("header",scope.Digest),new XAttribute("readFence",scope.ReadFence),
                    new XAttribute("accepted",HistoricalHash(scope.AcceptedImage)),new XAttribute("observed",RebirthWorkstationCompletionOriginal.Hash(cache.SpaceImage)),
                    new XAttribute("savedPath",cache.SavedPath),new XAttribute("saved",HistoricalHash(cache.SavedImage))));
            if(!RebirthWorkstationCompletionPublication.Write(scope.Path+".saved",saved)||!XNode.DeepEquals(saved,RebirthWorkstationCompletionPublication.ReadPrimary(scope.Path+".saved")))return;
            scope.SavedImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(scope.Path+".saved");if(scope.SavedImage==null)return;
            cache.CreatorClosePath=scope.Path+".saved";cache.CreatorCloseImage=scope.SavedImage;cache.CreatorCloseReadFence=scope.ReadFence;cache.CreatorCloseObserved=true;cache.RestoreAttempted=false;
            cachedPhysicalActions.Remove(operation);scope.Action.ClearEvidence();scope.Action=null;
        }
        if(!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(scope.Path+".saved",scope.SavedImage))return;
        Send(false,new XElement("ack",new XAttribute("world",cache.WorldGuid),new XAttribute("viewerCreation",scope.Creation),
            new XAttribute("stage","cachedUnusedCloseSaved"),new XAttribute("close",scope.Close.ToString("N")),new XAttribute("header",scope.Digest),
            new XAttribute("digest",HistoricalHash(cache.Image)),new XAttribute("challenge",(string)report.Attribute("challenge")??"")),operation,viewer.entityId);
    }
    private static bool IsHistoricalCachedUnusedCloseSaved(CachedCustody cache)
    {
        try
        {
            string guardPath=cache.Path+".remote-action."+cache.Epoch;
            long guardLength=RebirthWorkstationCompletionPublication.PublishedLength(guardPath);
            if(guardLength<1||!cache.ReserveRetirementWorkspace(checked(16*guardLength+65536)))return false;
            var guard=RebirthWorkstationCompletionPublication.ReadPrimary(guardPath)?.Element("cachedUnusedGuard");
            string path=(string)guard?.Attribute("path"),prefix=cache.Path+".unused-close.";
            if(guard==null||(string)guard.Attribute("original")!=HistoricalHash(cache.Image)||(int?)guard.Attribute("epoch")!=cache.Epoch||
                path==null||!path.StartsWith(prefix,StringComparison.Ordinal)||!Guid.TryParseExact(path.Substring(prefix.Length),"N",out var close)||close==Guid.Empty)return false;
            string[] paths={path,path+".accepted",path+".observed",path+".saved"};long length=guardLength;
            foreach(string value in paths){long n=RebirthWorkstationCompletionPublication.PublishedLength(value);if(n<1)return false;length=checked(length+n);}
            if(!cache.ReserveRetirementWorkspace(checked(16*length+65536)))return false;
            var saved=RebirthWorkstationCompletionPublication.ReadPrimary(path+".saved")?.Element("cachedUnusedCloseSaved");
            var accepted=RebirthWorkstationCompletionPublication.ReadPrimary(path+".accepted")?.Element("cachedUnusedNativeAccepted");
            var observed=RebirthWorkstationCompletionPublication.ReadPrimary(path+".observed")?.Element("cachedUnusedCloseObserved");
            var header=RebirthWorkstationCompletionPublication.ReadPrimary(path)?.Element("cachedUnusedClose");
            var acceptedBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".accepted");
            string observedImage=RebirthWorkstationCompletionPublication.ReadEvidence(path+".observed");
            if(saved==null||accepted==null||observed==null||header==null||acceptedBytes==null||observedImage==null||
                (string)saved.Attribute("original")!=HistoricalHash(cache.Image)||(int?)saved.Attribute("epoch")!=cache.Epoch||
                (string)saved.Attribute("path")!=path||(string)saved.Attribute("savedPath")!=cache.SavedPath||
                (string)saved.Attribute("saved")!=HistoricalHash(cache.SavedImage)||(string)saved.Attribute("accepted")!=HistoricalHash(acceptedBytes)||
                (string)saved.Attribute("observed")!=RebirthWorkstationCompletionOriginal.Hash(observedImage)||
                (string)saved.Attribute("header")!=RebirthWorkstationCompletionOriginal.Hash(header.ToString(SaveOptions.DisableFormatting))||
                (string)accepted.Attribute("header")!=(string)saved.Attribute("header")||(string)observed.Attribute("header")!=(string)saved.Attribute("header")||
                (string)header.Attribute("close")!=close.ToString("N")||
                (string)observed.Element("cachedUnusedClose")?.Attribute("dispatch")!=(string)accepted.Attribute("dispatch")||
                !XNode.DeepEquals(observed.Elements("cachedUnusedClose").LastOrDefault(),header))return false;
            var native=ReadCachedSpaceBody(accepted,"native");int offset=(int)accepted.Element("native").Attribute("offset");
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,offset,cache.Native,cache.Offset))return false;
            return true; // Classification only; actual installed load still issues custody.
        }
        catch{return false;}
    }
}
