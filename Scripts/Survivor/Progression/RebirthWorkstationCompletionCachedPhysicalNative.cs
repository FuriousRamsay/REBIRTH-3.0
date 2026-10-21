using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // A selected original-primary action has no native Add/GiveExp source.
    // Its own issuer observes one unchanged native packet read and the return
    // of that same ProcessPackage invocation; no parsed receipt issues it.
    private sealed class CachedPhysicalRead
    {
        internal CachedPhysicalHostAction Action;
        internal NetPackageTileEntity Packet;
        internal PooledBinaryReader Reader;
        internal XElement Active,Delivery;
        internal RebirthWorkstationCompletionOriginal Original;
        internal byte[] Body;
        internal int Offset,Phase,Reads;
        internal Guid Dispatch;
        internal bool ReadCompleted;
        internal long ReadFence;
        internal CachedPhysicalEchoSource Echo;
        internal CachedPeerRead[] Peers;
    }
    [ThreadStatic]private static CachedPhysicalRead executingCachedPhysical;
    private static void EnterCachedPhysicalRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        var read=executingCachedPhysical;if(read==null)return;
        if(!run||mode!=StreamModeRead.FromClient||!ReferenceEquals(station,read.Action.Station)||
            !ReferenceEquals(reader.BaseStream,read.Packet.ms)||!read.Action.Current()||++read.Reads!=1)throw new InvalidDataException();
        read.Reader=reader;
    }
    private static void ObserveCachedPhysicalRead(PooledBinaryReader reader,bool run)
    {
        var read=executingCachedPhysical;
        if(read==null||!ReferenceEquals(read.Reader,reader)||!run||read.Reads!=1||
            !ReferenceEquals(reader.BaseStream,read.Packet.ms)||reader.BaseStream.Position!=reader.BaseStream.Length||
            !read.Action.Issue.CurrentIdentity()||!RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(read.Action.World,read.Action.Station,out var fence))return;
        using(var callbacks=read.Action.Issue.Cache.Memory.TryEnterCallbacks())
            if(callbacks==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(read.Action.Station,read.Body,read.Offset))return;
        read.ReadFence=fence;read.ReadCompleted=true;
    }
    private static void ReceiveCachedPhysicalNative(XElement header,byte[] native,Guid dispatch,World world,GameManager callbacks,ClientInfo sender)
    {
        if(!Guid.TryParseExact((string)header.Attribute("operation"),"N",out var operation)||
            !cachedPhysicalActions.TryGetValue(operation,out var action)||!ReferenceEquals(action.World,world)||!ReferenceEquals(action.Sender,sender)||
            !action.Current()||(string)header.Attribute("nonce")!=action.Action.ToString("N")||
            header.Elements("cachedPhysicalSource").Count()!=1||header.Elements("deferredNativeContribution").Any()||header.Elements("sameOwnerEffect").Any())return;
        int phase=(int)header.Attribute("phase");if(phase!=2&&phase!=3&&phase!=4||phase!=4&&action.BeforeGeneration==null)return;
        bool loadedFinal=phase==3&&!action.PhysicalAccepted&&action.LoadedFinal?.Current()==true;
        if(action.LoadedFinal!=null&&phase!=3||phase==3&&action.PhysicalFinalObserved)return;
        var source=header.Element("cachedPhysicalSource");
        if((string)source.Attribute("ticket")!=action.Issue.Ticket.ToString("N")||(string)source.Attribute("original")!=action.SourceHash||
            phase!=4&&(string)source.Attribute("generation")!=action.BeforeGeneration||phase==2&&action.PhysicalAccepted||phase==3&&!action.PhysicalAccepted&&!loadedFinal)return;
        var active=header.Element("activeBefore")?.Elements().SingleOrDefault();
        var delivery=header.Element("physicalDelivery")?.Elements().SingleOrDefault();
        RebirthWorkstationCompletionOriginal original=null;
        if(phase!=4&&(active==null||delivery==null||phase==3&&(loadedFinal?!action.LoadedFinal.Matches(active,delivery):
                !XNode.DeepEquals(action.PhysicalActive,active)||!XNode.DeepEquals(action.PhysicalDelivery,delivery))||
            !RebirthWorkstationCompletionOriginal.TryRead(header.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out original)||
            original.Operation!=operation.ToString("N")||original.WorldGuid!=action.WorldGuid||original.ViewerKey!=action.ViewerKey||original.ViewerCreation!=action.ViewerCreation||
            original.OwnerKey!=(string)action.Before.Attribute("startingOwnerKey")||original.Creation!=(string)action.Before.Attribute("startingOwnerCreation")||
            original.StartingOwner!=(int)action.Before.Attribute("startingOwner")||original.RecipeName!=action.Recipe.GetName()||original.Count!=action.Recipe.count||
            !XNode.DeepEquals(original.Write().Element("delivery")?.Elements().SingleOrDefault(),delivery)))return;
        var packet=NetPackageManager.GetPackage<NetPackageTileEntity>();
        try
        {
            using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);if(reader.ReadUInt16()!=packet.PackageId)throw new InvalidDataException();packet.read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();}
            packet.Sender=sender;
            if(!packet.teWorldPos.Equals(action.Station.ToWorldPos())||packet.teBlockId!=action.Station.blockValue.type||packet.handle!=action.Station.lockHandleWaitingFor||
                packet.ms==null||packet.ms.Length<8||packet.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes)return;
            var cache=action.Issue.Cache;
            if(!cache.ReserveRetirementWorkspace(checked(16L*(native.Length+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(header))+65536)))return;
            byte[] body;
            lock(packet.ms)
            {long prior=packet.ms.Position;try{packet.ms.Position=0;body=new byte[checked((int)packet.ms.Length)];int n=0;while(n<body.Length){int next=packet.ms.Read(body,n,body.Length-n);if(next==0)throw new EndOfStreamException();n+=next;}}finally{packet.ms.Position=prior;}}
            int offset=(int)header.Attribute("elapsedOffset");
            byte[] before=phase==3?(loadedFinal?action.LoadedFinal.Before:action.PhysicalBody):cache.Native;
            int beforeOffset=phase==3?(loadedFinal?action.LoadedFinal.Offset:action.PhysicalOffset):cache.Offset;
            if(phase==4)
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder==null||!ValidateCachedPhysicalSpace(action,header.Element("actualCachedPhysicalSpace"),body,offset))return;
            }
            else if(loadedFinal?!action.LoadedFinal.Validate(active,delivery,body,offset):
                !ValidateCachedPhysicalDelivery(action,active,delivery,before,beforeOffset,body,offset,phase))return;
            using(var scope=cache.Memory.TryEnterCallbacks())
                if(scope==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(action.Station,before,beforeOffset))return;
            var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",action.WorldGuid),
                new XElement(cache.Original.Element("before")),
                new XElement("cachedPhysicalNativeAttempt",new XAttribute("dispatch",dispatch.ToString("N")),new XElement(header)));
            string path=action.Path+".native."+phase+(phase==4?"."+(int)header.Element("actualCachedPhysicalSpace").Attribute("sequence"):"");
            if(!RebirthWorkstationCompletionPublication.Write(path,attempt)||!XNode.DeepEquals(attempt,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!action.Current())return;
            var read=new CachedPhysicalRead{Action=action,Packet=packet,Body=body,Offset=offset,Phase=phase,Dispatch=dispatch,Active=active,Delivery=delivery,Original=original};
            read.Peers=CaptureCachedPeerReads(action,before,beforeOffset);
            nativeProcessed.Add(dispatch,world);executingCachedPhysical=read;
            try
            {
                packet.ProcessPackage(world,callbacks);
                if(!read.ReadCompleted||read.Reads!=1||!action.Issue.CurrentIdentity()||
                    !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(world,action.Station,out var currentFence)||currentFence!=read.ReadFence)return;
                using(var scope=cache.Memory.TryEnterCallbacks())
                    if(scope==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(action.Station,body,offset))return;
                // Only the completed actual read/Process may advance this
                // action's epoch. An equal raw read cannot renew it.
                action.ReadFence=read.ReadFence;
                var accepted=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",action.WorldGuid),
                    new XElement(cache.Original.Element("before")),
                    new XElement("cachedPhysicalNativeAccepted",new XAttribute("dispatch",dispatch.ToString("N")),new XAttribute("readFence",read.ReadFence),
                        new XAttribute("phase",phase),new XElement(header),new XElement("native",new XAttribute("offset",offset),Convert.ToBase64String(body))));
                if(!RebirthWorkstationCompletionPublication.Write(path+".accepted",accepted)||!XNode.DeepEquals(accepted,RebirthWorkstationCompletionPublication.ReadPrimary(path+".accepted"))||!action.Current())return;
                ObserveCachedPeerReads(read);
                if(phase==4)
                {
                    var space=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",action.WorldGuid),
                        new XElement(cache.Original.Element("before")),new XElement("remoteCachedSpaceAccepted",new XAttribute("version",1),
                            new XAttribute("original",HistoricalHash(cache.Image)),new XAttribute("dispatch",dispatch.ToString("N")),
                            new XAttribute("epoch",cache.Epoch+1),new XElement(header.Element("actualCachedPhysicalSpace"))));
                    string spacePath=cache.Path+".remote-space."+(cache.Epoch+1);
                    if(!RebirthWorkstationCompletionPublication.Write(spacePath,space)||!XNode.DeepEquals(space,RebirthWorkstationCompletionPublication.ReadPrimary(spacePath)))return;
                    string image=RebirthWorkstationCompletionPublication.ReadEvidence(spacePath);if(image==null)return;
                    cache.Watch?.Dispose();cache.Watch=null;cache.Native=body;cache.Offset=offset;cache.Saved=false;
                    cache.SpacePath=spacePath;cache.SpaceImage=image;cache.Epoch++;action.ObserveSavedSpaceEpoch((int)header.Element("actualCachedPhysicalSpace").Attribute("sequence"));
                    cache.TrySave();cache.Chunk.isModified=true;cache.Station.setModified();return;
                }
                if(phase==2)
                {
                    var seal=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".accepted");if(seal==null)return;
                    action.PhysicalBody=body;action.PhysicalOffset=offset;action.PhysicalActive=new XElement(active);action.PhysicalDelivery=new XElement(delivery);
                    action.PhysicalAcceptedPath=path+".accepted";action.PhysicalAcceptedImage=seal;action.PhysicalAccepted=true;return;
                }
                var physical=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",action.WorldGuid),
                    new XElement(cache.Original.Element("before")),new XElement("cachedContinuation",new XElement(active)),
                    new XElement("actualCachedPhysical",new XAttribute("original",HistoricalHash(cache.Image)),new XAttribute("remote",true),
                        new XAttribute("dispatch",dispatch.ToString("N")),new XAttribute("elapsedOffset",offset),
                        new XElement("confirmedOriginal",original.Write()),new XElement("native",Convert.ToBase64String(body))));
                var actorSaved=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CopySavedCachedPhysicalActor(action,active,delivery);
                if(actorSaved==null)return;physical.Element("actualCachedPhysical").Add(actorSaved);
                if(!RebirthWorkstationCompletionPublication.Write(cache.Path+".physical-attempt",physical)||
                    !XNode.DeepEquals(physical,RebirthWorkstationCompletionPublication.ReadPrimary(cache.Path+".physical-attempt")))return;
                var finalSeal=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".accepted");if(finalSeal==null)return;
                action.FinalAcceptedPath=path+".accepted";action.FinalAcceptedImage=finalSeal;action.PhysicalFinalObserved=true;
                cache.Native=body;cache.Offset=offset;cache.Saved=false;cache.PhysicalObserved=true;cache.Watch?.Dispose();cache.Watch=null;
                cache.TrySave();cache.Chunk.isModified=true;cache.Station.setModified();
            }
            finally{read.Echo?.Dispose();read.Echo=null;executingCachedPhysical=null;}
        }
        finally{NetPackageManager.FreePackage(packet);}
    }
}
