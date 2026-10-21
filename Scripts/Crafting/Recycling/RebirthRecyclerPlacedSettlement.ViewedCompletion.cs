using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private sealed class ViewedCompletion:IDisposable
    {
        internal EntityPlayerLocal Viewer;internal World World;internal TileEntityWorkstation Station;
        internal XUiC_WorkstationWindowGroup Window;internal INetConnection Connection;internal long Session;
        internal Guid Operation,Group;internal int Deaths;internal string Creation,Request;
        internal float Next;internal bool Terminal;internal IDisposable Memory;
        internal bool Current(bool window)=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&
            ReferenceEquals(World.GetPrimaryPlayer(),Viewer)&&ReferenceEquals(World.GetEntity(Viewer.entityId),Viewer)&&Viewer.IsSpawned()&&!Viewer.IsDead()&&Viewer.Died==Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Viewer)==Creation&&ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection)&&Connection!=null&&!Connection.IsDisconnected()&&
            (!window||Window?.windowGroup?.isShowing==true&&ReferenceEquals(Window.WorkstationData?.TileEntity,Station)&&ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Viewer)&&
                LockManager.Instance?.IsLockedByLocalPlayer(Station,0)==true&&RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Window,out long current)&&current==Session);
        public void Dispose(){Memory?.Dispose();Memory=null;Request=null;Window=null;}
    }
    private static readonly List<ViewedCompletion> viewedCompletions=new List<ViewedCompletion>();
    private static bool ViewedCompletionHeld(TileEntityWorkstation station)=>viewedCompletions.Any(value=>ReferenceEquals(value.Station,station)&&value.Current(false));
    internal static bool BeginViewedCompletion(EntityPlayerLocal viewer,TileEntityWorkstation station,Guid operation,Guid group,float seconds)
    {
        if(!authorityInstalled||viewer?.world?.IsRemote()!=true||operation==Guid.Empty||group==Guid.Empty)return false;
        var prior=viewedCompletions.SingleOrDefault(value=>value.Operation==operation);
        if(prior!=null)return ReferenceEquals(prior.Viewer,viewer)&&ReferenceEquals(prior.Station,station)&&prior.Current(false);
        var window=XUiC_RebirthStationWorkspace.ActiveInstance;var connection=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0];
        if(viewedCompletions.Count>=4||HoldsStation(station)||window?.windowGroup?.isShowing!=true||!ReferenceEquals(window.WorkstationData?.TileEntity,station)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out long session)||
            !RebirthRecyclerRuntime.TryPlanPlacedViewerCompletion(viewer,station,seconds,out var plan)||plan.Operation!=operation||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return false;
        ViewedCompletion issued=null;bool retained=false;
        try
        {
            byte[] before,after;int beforeOffset,afterOffset;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(station,ProjectNativeCycle(plan.Queue),plan.Input,plan.Output,null,plan.Tools,
                    out before,out after,out beforeOffset,out afterOffset))return false;
            var position=station.ToWorldPos();
            var request=new XElement("recyclerViewedCompletion",new XAttribute("version",1),new XAttribute("player",viewer.entityId),
                new XAttribute("recipientOwner",plan.RecipientOwner),new XAttribute("recipientCreation",plan.RecipientCreation),
                new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(viewer)),new XAttribute("deaths",viewer.Died),
                new XAttribute("operation",operation.ToString("N")),new XAttribute("group",group.ToString("N")),new XAttribute("seconds",seconds),
                new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),
                new XAttribute("beforeOffset",beforeOffset),new XAttribute("afterOffset",afterOffset),Bytes("beforeNative",before),Bytes("afterNative",after));
            string text=request.ToString(SaveOptions.DisableFormatting);int length=Encoding.UTF8.GetByteCount(text);
            if(length>8*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(8L*length+65536)))return false;
            issued=new ViewedCompletion{Viewer=viewer,World=viewer.world,Station=station,Window=window,Connection=connection,Session=session,Operation=operation,Group=group,Deaths=viewer.Died,
                Creation=RebirthCamperCharacterAccess.CurrentCreation(viewer),Request=text,Memory=memory};memory=null;
            if(!issued.Current(true))return false;
            viewedCompletions.Add(issued);retained=true;
            // The ordinary pump sends the immutable station-only introduction.
            // No recipient inventory or native output has been changed here.
            return true;
        }
        finally{if(!retained)issued?.Dispose();memory?.Dispose();}
    }
    private static void PumpViewedCompletions(World world)
    {
        foreach(var value in viewedCompletions.ToArray())
        {
            if(!ReferenceEquals(value.World,world)||!value.Current(false))
            {viewedCompletions.Remove(value);value.Dispose();continue;}
            if(UnityEngine.Time.realtimeSinceStartup<value.Next)continue;
            value.Next=UnityEngine.Time.realtimeSinceStartup+1f;
            if(value.Terminal&&ProjectViewedCompletion(value))
            {viewedCompletions.Remove(value);value.Dispose();continue;}
            if(value.Terminal)continue;
            try{SendPlacedText(value.Request);}catch{/* Keep this same station-only introduction. */}
        }
    }
    private static bool ProjectViewedCompletion(ViewedCompletion value)
    {
        try
        {
            if(value.Station.Queue==null||value.Station.Queue.Any(row=>RebirthRecyclerRuntime.TryGroup(row?.Recipe,out var group)&&group==value.Group))return false;
            var window=value.Window;
            if(window?.windowGroup?.isShowing!=true||!ReferenceEquals(window.WorkstationData?.TileEntity,value.Station))return true;
            // Saved completion is historical. A later legitimate station image
            // can be projected, but the paid group must already be gone from
            // the native replica. Never project the frozen old after over it.
            if(!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out long session))return false;
            if(session!=value.Session)return true; // A reopened native window owns its fresh projection.
            if(!value.Current(true)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return false;
            using(memory)
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(value.Station,value.Station.Queue,value.Station.Input,value.Station.Output,null,value.Station.Tools,
                out var image,out _,out int offset,out _))return false;
            bool Current()=>value.Current(true)&&RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Station,image,offset);
            if(!Current()||!RebirthProductiveEarnedNativeBody.TryRead(image,offset,out var native,out _))return false;
            var cards=window.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
            var input=window.GetChildByType<XUiC_WorkstationInputGrid>();var output=window.GetChildByType<XUiC_WorkstationOutputGrid>();var tools=window.GetChildByType<XUiC_WorkstationToolGrid>();
            if(cards==null||cards.Length!=native.Queue.Length||input==null||output==null||tools==null||native.Queue.Any(row=>row==null||row.RepairItem!=null||row.AmountToRepair!=0))return false;
            for(int index=0;index<cards.Length;index++)
            {
                if(!Current()||cards[index]==null)return false;var row=native.Queue[index];
                if(!cards[index].SetRecipe(row.Recipe,row.Multiplier,row.CraftingTimeLeft,true,row.StartingEntityId,row.Quality,row.OneItemCraftTime)||!Current())return false;
                cards[index].IsCrafting=false;cards[index].IsDirty=true;
            }
            if(!Current())return false;input.SetSlots(ItemStack.Clone(native.Input));
            if(!Current())return false;output.SetSlots(ItemStack.Clone(native.Output));
            if(!Current())return false;tools.SetSlots(ItemStack.Clone(native.Tools));
            return Current()&&SameOwnerGrid(input.GetSlots(),native.Input)&&SameOwnerGrid(output.GetSlots(),native.Output)&&SameOwnerGrid(tools.GetSlots(),native.Tools);
            }
        }
        catch{return false;}
    }
    private static void ReceiveViewedCompletion(NetPackageRebirthRecyclerPlacedRequestV1 packet,World world,XElement request)
    {
        if(TryReplyViewedTerminal(packet,world,request))return;
        if(!TryResolveViewedAttempt(world,request,out var priorOperation))return;
        {
            string prior=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),priorOperation.ToString("N")+".terminal.xml");
            if(new[]{prior,prior+".tmp",prior+".bak"}.Any(ArtifactNodeExists))return;
        }
        if(ResolveViewedRecipient(world,request) is EntityPlayerLocal)
        {TryBeginViewedLocalCompletion(packet,world,request);return;}
        var source=ClosedAuthorityCompletion.CaptureViewed(packet,world,request);if(source==null)return;
        bool retained=false;
        try
        {
            source.Access=AccessHold.CapturePending(source);if(source.Access==null)return;
            closedAuthorityCompletions.Add(source);retained=true;
            try{source.EnsureOriginalPublished();}catch{/* Existing recipient pump retains this exact source. */}
        }
        finally{if(!retained)source.Dispose();}
    }
    private static bool ObserveViewedTerminal(XElement reply,INetConnection connection)
    {
        if(reply.Name!="recyclerViewedTerminal")return false;
        if((int?)reply.Attribute("version")!=1||!Guid.TryParseExact((string)reply.Attribute("operation"),"N",out var operation))return true;
        var value=viewedCompletions.SingleOrDefault(candidate=>candidate.Operation==operation&&ReferenceEquals(candidate.Connection,connection));
        if(value?.Current(false)!=true||(string)reply.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(value.World)||
            (string)reply.Attribute("request")!=RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(value.Request))||
            ((string)reply.Attribute("terminal"))?.Length!=64)return true;
        value.Terminal=true;return true;
    }
    private static bool MatchesViewedFrames(XElement request,byte[] before,int beforeOffset,byte[] after,int afterOffset)
    {
        if((int?)request.Attribute("beforeOffset")!=beforeOffset||(int?)request.Attribute("afterOffset")!=afterOffset)return false;
        var offeredBefore=ColdBytes(request.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
        var offeredAfter=ColdBytes(request.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
        // The host keeps its own qualified native elapsed clock. That clock
        // changes during transport; every station/material/queue byte must match.
        return RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,offeredBefore,beforeOffset)&&
            RebirthProductiveEarnedNativeBody.SameExceptElapsed(after,afterOffset,offeredAfter,afterOffset);
    }
    private static bool TryReplyViewedTerminal(NetPackageRebirthRecyclerPlacedRequestV1 packet,World world,XElement request)
    {
        if(!ReferenceEquals(processingRecyclerRequest,packet)||world?.IsRemote()!=false||!ThreadManager.IsMainThread()||
            request.Name!="recyclerViewedCompletion"||(int?)request.Attribute("version")!=1||
            !Guid.TryParseExact((string)request.Attribute("operation"),"N",out var operation)||operation==Guid.Empty)return false;
        var viewer=world.GetEntity((int?)request.Attribute("player")??-1) as EntityPlayer;var peer=packet.Sender;
        if(viewer==null||!packet.ValidEntityIdForSender(viewer.entityId)||!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection))return false;
        bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(viewer.world,world)&&ReferenceEquals(world.GetEntity(viewer.entityId),viewer)&&
            viewer.IsSpawned()&&!viewer.IsDead()&&(int?)request.Attribute("deaths")==viewer.Died&&(string)request.Attribute("creation")==RebirthCamperCharacterAccess.CurrentCreation(viewer)&&
            peer?.loginDone==true&&!peer.disconnecting&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(viewer.entityId),peer)&&
            ReferenceEquals(peer.netConnection?[0],connection)&&connection!=null&&!connection.IsDisconnected();
        if(!Current()||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return false;
        using(memory)
        {
            Guid requestedOperation=operation;
            if(!TryResolveViewedAttempt(world,request,out operation))return false;
            string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),terminalPath=System.IO.Path.Combine(root,operation.ToString("N")+".terminal.xml");
            if(!ArtifactNodeExists(terminalPath))return false;
            var terminal=RebirthWorkstationCompletionPublication.ReadPrimary(terminalPath);var original=terminal?.Element("before")?.Elements().SingleOrDefault();
            if(terminal?.Name!="workstationCompletionIntent"||(string)terminal.Attribute("operation")!=operation.ToString("N")||terminal.Element("recyclerPlacedTerminal")==null||
                original?.Name!="placedRecyclerOriginal"||(string)original.Attribute("purpose")!="complete"||(string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||
                (string)original.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("account")!=(string)request.Attribute("recipientOwner")||
                (string)original.Attribute("creation")!=(string)request.Attribute("recipientCreation"))return false;
            foreach(string name in new[]{"x","y","z","beforeOffset","afterOffset","workSeconds"})
                if((string)original.Attribute(name)!=(string)request.Attribute(name=="workSeconds"?"seconds":name))return false;
            if(!MatchesViewedFrames(request,ColdBytes(original.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),(int)original.Attribute("beforeOffset"),
                ColdBytes(original.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),(int)original.Attribute("afterOffset")))return false;
            byte[] bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(terminalPath);if(bytes==null)return false;
            string digest=RebirthStationPublicationRecord.Digest(bytes);
            bool Cleanup(string suffix,string name)
            {
                string path=System.IO.Path.Combine(root,operation.ToString("N")+suffix);if(!ArtifactNodeExists(path))return false;
                var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var evidence=record?.Element(name);
                return record?.Name=="workstationCompletionIntent"&&(string)record.Attribute("operation")==operation.ToString("N")&&
                    (string)record.Attribute("world")==RebirthWorkstationCompletionOutbox.CurrentGuid(world)&&XNode.DeepEquals(record.Element("before")?.Elements().SingleOrDefault(),original)&&
                    (string)evidence?.Attribute("terminal")==digest&&(name=="recyclerCleanupSaved"||evidence.Element("recyclerCleanupSaved")!=null);
            }
            if(!Cleanup(".cleanup-saved.xml","recyclerCleanupSaved")&&!Cleanup(".remote-completed.xml","recyclerRemoteCompleted"))return false;
            if(!Current()||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(terminalPath,bytes))return false;
            var reply=new XElement("recyclerViewedTerminal",new XAttribute("version",1),new XAttribute("operation",requestedOperation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(world)),new XAttribute("terminal",digest),
                new XAttribute("request",RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(request.ToString(SaveOptions.DisableFormatting)))));
            peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthRecyclerPlacedReplyV1>().Setup(reply.ToString(SaveOptions.DisableFormatting)));return true;
        }
    }
    private static EntityPlayer ResolveViewedRecipient(World world,XElement request)
    {
        var position=new Vector3i((int)request.Attribute("x"),(int)request.Attribute("y"),(int)request.Attribute("z"));
        return RebirthRecyclerRuntime.FindPlacedCompletionRecipient(world,world?.GetTileEntity(position) as TileEntityWorkstation);
    }
    [ThreadStatic]private static Original admittingViewedLocalOriginal;
    [ThreadStatic]private static NetPackageRebirthRecyclerPlacedRequestV1 admittingViewedLocalPacket;
    private static bool IsViewedLocalAdmission(Original source)=>ReferenceEquals(admittingViewedLocalOriginal,source)&&
        admittingViewedLocalPacket!=null&&ReferenceEquals(processingRecyclerRequest,admittingViewedLocalPacket)&&source.Current()&&
        XElement.Parse(Encoding.UTF8.GetString(source.Image)).Element("nativeViewer")!=null;
    private static bool TryBeginViewedLocalCompletion(NetPackageRebirthRecyclerPlacedRequestV1 packet,World world,XElement request)
    {
        if(!authorityInstalled||!ThreadManager.IsMainThread()||admittingViewedLocalOriginal!=null||!ReferenceEquals(processingRecyclerRequest,packet)||
            world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||request?.Name!="recyclerViewedCompletion"||(int?)request.Attribute("version")!=1)return false;
        Original original=null;bool retained=false;IDisposable workspace=null;
        try
        {
            if(!Guid.TryParseExact((string)request.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                !Guid.TryParseExact((string)request.Attribute("group"),"N",out var group)||group==Guid.Empty)return false;
            var viewer=world.GetEntity((int)request.Attribute("player")) as EntityPlayer;var recipient=ResolveViewedRecipient(world,request) as EntityPlayerLocal;
            var peer=packet.Sender;
            if(viewer==null||recipient==null||ReferenceEquals(viewer,recipient)||!viewer.IsSpawned()||viewer.IsDead()||!packet.ValidEntityIdForSender(viewer.entityId)||
                peer?.loginDone!=true||peer.disconnecting||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(viewer.entityId),peer)||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(peer.netConnection?[0],connection)||connection.IsDisconnected())return false;
            int viewerDeaths=viewer.Died;string viewerCreation=RebirthCamperCharacterAccess.CurrentCreation(viewer);
            if((int?)request.Attribute("deaths")!=viewerDeaths||(string)request.Attribute("creation")!=viewerCreation)return false;
            var position=new Vector3i((int)request.Attribute("x"),(int)request.Attribute("y"),(int)request.Attribute("z"));var station=world.GetTileEntity(position) as TileEntityWorkstation;
            float seconds=(float)request.Attribute("seconds");
            if(station==null||HoldsStation(station)||HasNativeOriginalMarker(station)||HoldsOwner(recipient)||
                !RebirthRecyclerRuntime.TryGroup(station.Queue?.LastOrDefault()?.Recipe,out var actualGroup)||actualGroup!=group||
                !RebirthRecyclerRuntime.TryPlanPlacedCompletionForViewer(viewer,recipient,station,seconds,out var plan)||plan.Operation!=operation||
                (string)request.Attribute("recipientOwner")!=plan.RecipientOwner||(string)request.Attribute("recipientCreation")!=plan.RecipientCreation)return false;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out workspace))return false;
            byte[] before=ColdBytes(request.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),after=ColdBytes(request.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
            int beforeOffset=(int)request.Attribute("beforeOffset"),afterOffset=(int)request.Attribute("afterOffset");
            bool Current()=>ReferenceEquals(processingRecyclerRequest,packet)&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(viewer.world,world)&&
                ReferenceEquals(world.GetEntity(viewer.entityId),viewer)&&viewer.IsSpawned()&&!viewer.IsDead()&&viewer.Died==viewerDeaths&&RebirthCamperCharacterAccess.CurrentCreation(viewer)==viewerCreation&&
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(viewer.entityId),peer)&&peer.loginDone&&!peer.disconnecting&&
                ReferenceEquals(peer.netConnection?[0],connection)&&!connection.IsDisconnected()&&RebirthRecyclerRuntime.HasPlacedNativeAccess(viewer,station)&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(station,before,beforeOffset);
            if(!Current())return false;
            if(!TrySelectAuthorityAttempt(recipient,station,operation,"complete",out operation))return false;
            original=Original.Capture(recipient,station,operation,"complete",plan.Input,plan.Queue,plan.Returns,plan.Output,plan.Tools,Current,true,seconds,
                new XElement("nativeViewer",new XAttribute("entity",viewer.entityId),new XAttribute("creation",viewerCreation),new XAttribute("deaths",viewerDeaths),new XAttribute("group",group.ToString("N"))));
            if(original==null||!MatchesViewedFrames(request,original.BeforeNative,original.BeforeOffset,original.AfterNative,original.AfterOffset)||!Current())return false;
            admittingViewedLocalOriginal=original;admittingViewedLocalPacket=packet;
            retained=TryRetainAuthority(original);return retained;
        }
        catch{return false;}
        finally
        {
            admittingViewedLocalOriginal=null;admittingViewedLocalPacket=null;
            if(!retained)original?.Dispose();
            workspace?.Dispose();
        }
    }
    internal sealed partial class ClosedAuthorityCompletion
    {
        internal bool ViewedOrigin;
        internal static ClosedAuthorityCompletion CaptureViewed(NetPackageRebirthRecyclerPlacedRequestV1 packet,World world,XElement request)
        {
            if(!authorityInstalled||!ThreadManager.IsMainThread()||!ReferenceEquals(processingRecyclerRequest,packet)||world?.IsRemote()!=false||
                !ReferenceEquals(GameManager.Instance?.World,world)||request?.Name!="recyclerViewedCompletion"||(int?)request.Attribute("version")!=1)return null;
            IDisposable memory=null;ClosedAuthorityCompletion issued=null;
            try
            {
                if(!Guid.TryParseExact((string)request.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                    !Guid.TryParseExact((string)request.Attribute("group"),"N",out var group)||group==Guid.Empty||
                    closedAuthorityCompletions.Any(value=>value.Operation==operation)||closedAuthorityCompletions.Count>=4)return null;
                var viewer=world.GetEntity((int)request.Attribute("player")) as EntityPlayer;var recipient=ResolveViewedRecipient(world,request);
                var peer=packet.Sender;
                if(viewer==null||recipient==null||recipient is EntityPlayerLocal||ReferenceEquals(viewer,recipient)||!viewer.IsSpawned()||viewer.IsDead()||
                    !packet.ValidEntityIdForSender(viewer.entityId)||peer?.loginDone!=true||peer.disconnecting||
                    !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(viewer.entityId),peer)||
                    !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var received)||!ReferenceEquals(peer.netConnection?[0],received)||received.IsDisconnected()||
                    (int?)request.Attribute("deaths")!=viewer.Died||(string)request.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(viewer))return null;
                var position=new Vector3i((int)request.Attribute("x"),(int)request.Attribute("y"),(int)request.Attribute("z"));var station=world.GetTileEntity(position) as TileEntityWorkstation;
                int viewerDeaths=viewer.Died;string viewerCreation=RebirthCamperCharacterAccess.CurrentCreation(viewer);
                bool ViewerCurrent()=>ReferenceEquals(processingRecyclerRequest,packet)&&ReferenceEquals(GameManager.Instance?.World,world)&&
                    ReferenceEquals(viewer.world,world)&&ReferenceEquals(world.GetEntity(viewer.entityId),viewer)&&viewer.IsSpawned()&&!viewer.IsDead()&&
                    viewer.Died==viewerDeaths&&RebirthCamperCharacterAccess.CurrentCreation(viewer)==viewerCreation&&
                    ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(viewer.entityId),peer)&&peer.loginDone&&!peer.disconnecting&&
                    ReferenceEquals(peer.netConnection?[0],received)&&!received.IsDisconnected()&&ReferenceEquals(world.GetTileEntity(position),station)&&
                    RebirthRecyclerRuntime.HasPlacedNativeAccess(viewer,station);
                float seconds=(float)request.Attribute("seconds");
                if(station==null||HoldsStation(station)||HasNativeOriginalMarker(station)||!RebirthRecyclerRuntime.TryGroup(station.Queue?.LastOrDefault()?.Recipe,out var actualGroup)||actualGroup!=group||
                    !RebirthRecyclerRuntime.TryPlanPlacedCompletionForViewer(viewer,recipient,station,seconds,out var plan)||plan.Operation!=operation||
                    (string)request.Attribute("recipientOwner")!=plan.RecipientOwner||(string)request.Attribute("recipientCreation")!=plan.RecipientCreation)return null;
                var recipientPeer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(recipient.entityId);
                if(recipientPeer?.loginDone!=true||recipientPeer.disconnecting||recipientPeer.netConnection?[0]==null||recipientPeer.netConnection[0].IsDisconnected()||
                    !RebirthStablePlayerIdentity.TryFromClientInfo(recipientPeer,out var identity)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out memory))return null;
                byte[] before,after;int beforeOffset,afterOffset;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(station,ProjectNativeCycle(plan.Queue),plan.Input,plan.Output,null,plan.Tools,
                        out before,out after,out beforeOffset,out afterOffset))return null;
                if(!ViewerCurrent()||!MatchesViewedFrames(request,before,beforeOffset,after,afterOffset))return null;
                if(!TrySelectServerCompletionAttempt(recipient,station,operation,out operation))return null;
                if(!ViewerCurrent()||!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,before,beforeOffset))return null;
                var image=new XElement("recyclerClosedAuthorityOriginal",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("group",group.ToString("N")),
                    new XAttribute("owner",identity.StorageKey),new XAttribute("account",identity.CanonicalId),new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(recipient)),
                    new XAttribute("deaths",recipient.Died),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(world)),new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),
                    new XAttribute("block",station.block.GetBlockName()),new XAttribute("beforeOffset",beforeOffset),new XAttribute("afterOffset",afterOffset),new XAttribute("workSeconds",seconds),
                    Bytes("beforeNative",before),Bytes("afterNative",after),Grid("returns",plan.Returns),new XElement("nativeViewer",new XAttribute("entity",viewer.entityId),
                        new XAttribute("creation",viewerCreation),new XAttribute("deaths",viewerDeaths)));
                byte[] bytes=Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));
                if(bytes.Length>8*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(16L*(bytes.Length+before.Length+after.Length)+65536)))return null;
                issued=new ClosedAuthorityCompletion(recipient,recipientPeer,identity,station,station.Queue.Last(),operation,group,seconds,plan.Input,plan.Queue,plan.Output,plan.Tools,plan.Returns,
                    before,after,beforeOffset,afterOffset,bytes,memory){ViewedOrigin=true};memory=null;
                if(!issued.Current()||!ViewerCurrent())return null;
                var result=issued;issued=null;return result;
            }
            catch{return null;}
            finally{issued?.Dispose();memory?.Dispose();}
        }
    }
}
