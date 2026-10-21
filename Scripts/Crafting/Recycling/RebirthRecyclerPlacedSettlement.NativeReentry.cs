using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed class RecyclerReentryPublication:IDisposable
    {
        private RemoteHostOriginal source;private IDisposable memory;private readonly XElement image;private readonly bool after;
        private RecyclerReentryPublication(RemoteHostOriginal source,XElement image,bool after,IDisposable memory)
        {this.source=source;this.image=image;this.after=after;this.memory=memory;}
        internal long MeasureImage=>Encoding.UTF8.GetByteCount(image.ToString(SaveOptions.DisableFormatting));
        internal XElement Image=>Current()?new XElement(image):null;
        internal bool SourceCurrent()=>memory!=null&&source?.OwnerCurrent()==true&&source.NativeLineageCurrent();
        internal bool Current()=>SourceCurrent()&&source.Current()&&!source.AdmissionUnknown&&source.AcceptedStation==after&&
            RebirthRecyclerRuntime.HasPlacedNativeAccess(source.Actor,source.Station)&&
            (after?source.Publication?.SavedAfter()==true:source.Publication?.SavedBefore()==true);
        public void Dispose(){source=null;memory?.Dispose();memory=null;}
        internal static bool Capture(World world,TileEntityWorkstation station,ClientInfo peer,INetConnection connection,out RecyclerReentryPublication proof)
        {
            proof=null;
            var matches=remoteRecyclerOriginals.Values.Where(value=>ReferenceEquals(value.World,world)&&ReferenceEquals(value.Station,station)&&
                ReferenceEquals(value.Peer,peer)&&ReferenceEquals(value.Connection,connection)).ToArray();
            if(matches.Length!=1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out var memory))return false;
            try
            {
                var source=matches[0];var node=new XElement("recyclerReentry",new XAttribute("version",1),new XAttribute("operation",source.Operation.ToString("N")),
                    new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("original",RebirthStationPublicationRecord.Digest(source.Image)),
                    new XAttribute("owner",source.Owner),new XAttribute("creation",source.Creation),new XAttribute("deaths",source.Deaths),
                    new XAttribute("world",source.WorldKey),new XAttribute("stage",source.AcceptedStation?"after":"before"),
                    new XAttribute("ownerPhase",source.OwnerCheckpoint?.CurrentSaved()==true?source.OwnerCheckpoint.Phase:source.ColdNeedsOwner?source.ColdPhase:0),
                    new XAttribute("ownerCompleted",source.OwnerCheckpoint?.CurrentSaved()==true?source.OwnerCheckpoint.Completed:source.ColdNeedsOwner?source.ColdCompleted:0));
                if(source.ColdStation!=null||source.OwnerRejoined)node.Add(new XAttribute("cold",true),new XAttribute("phase",source.ColdPhase),new XAttribute("completed",source.ColdCompleted));
                var result=new RecyclerReentryPublication(source,node,source.AcceptedStation,memory);
                if(!result.Current())return false;proof=result;memory=null;return true;
            }
            finally{memory?.Dispose();}
        }
    }
    internal static bool HasRecyclerReentryCandidate(World world,TileEntityWorkstation station,ClientInfo peer,INetConnection connection)=>authorityInstalled&&
        remoteRecyclerOriginals.Values.Any(value=>ReferenceEquals(value.World,world)&&ReferenceEquals(value.Station,station)&&
            (peer==null&&connection==null||ReferenceEquals(value.Peer,peer)&&ReferenceEquals(value.Connection,connection)));
    internal static bool TryCaptureRecyclerReentry(World world,TileEntityWorkstation station,ClientInfo peer,INetConnection connection,out RecyclerReentryPublication proof)
        =>RecyclerReentryPublication.Capture(world,station,peer,connection,out proof);
    internal sealed class RecyclerReadReentry:IDisposable
    {
        internal RebirthProductiveEarnedUiDeferredTransport.ClientHydration Hydration;internal RecyclerClient Client;internal IDisposable Lease;
        internal bool Returned;internal long ReadVersion,Length;internal string Path,Digest,Stage;
        internal XUiC_WorkstationWindowGroup OpeningWindow;internal long OpeningSession;
        internal XElement Header;
        public void Dispose(){Hydration=null;Client=null;OpeningWindow=null;Header=null;Lease?.Dispose();Lease=null;}
    }
    internal static bool ImportActualRecyclerHydration(RebirthProductiveEarnedUiDeferredTransport.ClientHydration hydration)
    {
        var node=hydration?.Header?.Element("recyclerReentry");if(node==null)return true;
        if(!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(hydration)||hydration.Header.Elements("recyclerReentry").Count()!=1||
            node.Attributes().Count()!=((bool?)node.Attribute("cold")==true?14:11)||node.HasElements||(int?)node.Attribute("version")!=1||
            !Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||!Guid.TryParseExact((string)node.Attribute("nonce"),"N",out var nonce))return false;
        HoldActualRecyclerHydrationTarget(hydration,node,operation,nonce);
        var client=recyclerClients.SingleOrDefault(value=>value.Source?.Operation==operation&&ReferenceEquals(value.Connection,hydration.Connection)&&
            ReferenceEquals(value.Source.World,hydration.World)&&ReferenceEquals(value.Source.Station,hydration.Station)&&ReferenceEquals(value.Source.Actor,hydration.Actor));
        if(client==null)client=TryRebindActualRecyclerClient(hydration,node);
        bool prepared=client==null,retained=false;
        if(prepared&&(bool?)node.Attribute("cold")==true)client=PrepareActualColdRecyclerClient(hydration,node);
        try
        {
        if(client?.MetadataCurrent()!=true||nonce==Guid.Empty||client.Nonce!=Guid.Empty&&client.Nonce!=nonce||client.Unknown||(string)node.Attribute("owner")!=client.Source.Owner||
            (string)node.Attribute("creation")!=client.Source.Creation||(int?)node.Attribute("deaths")!=client.Source.Deaths||
            (string)node.Attribute("world")!=client.Source.WorldKey||(string)node.Attribute("original")!=RebirthStationPublicationRecord.Digest(client.Source.Image))return false;
        string stage=(string)node.Attribute("stage");
        if(stage=="before"?(client.Applied||client.PrincipalEntered):stage!="after"||!client.Applied||!client.NativeSent)return false;
        if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(client.Source.Station,stage=="after"?client.Source.AfterNative:client.Source.BeforeNative,
            stage=="after"?client.Source.AfterOffset:client.Source.BeforeOffset))return false;
        var replacement=new RecyclerReadReentry{Hydration=hydration,Client=client,ReadVersion=hydration.ReadVersion,Stage=stage,Header=new XElement(node)};
        var lease=hydration.Retain();if(lease==null)return false;replacement.Lease=lease;
        var prior=client.Reentry;
        client.Nonce=nonce;
        if(client.ColdJoined!=null&&client.Owner==null)client.Generation=hydration.Group;
        client.Reentry=replacement;prior?.Dispose();
        if(prepared)recyclerClients.Add(client);retained=true;return true;
        }
        finally{if(prepared&&!retained&&client!=null){client.ColdJoined=null;client.Dispose();}}
    }
    internal static void ObserveReturnedRecyclerHydration(RebirthProductiveEarnedUiDeferredTransport.ClientHydration hydration)
    {
        var client=recyclerClients.SingleOrDefault(value=>ReferenceEquals(value.Reentry?.Hydration,hydration));
        if(client?.Reentry!=null&&hydration.Processed&&hydration.ReadCompleted&&hydration.CurrentSource()&&client.MetadataCurrent())
        {
            var reentry=client.Reentry;reentry.Path=hydration.Path;reentry.Length=RebirthWorkstationCompletionPublication.PublishedLength(reentry.Path);reentry.Digest=RecyclerFileDigest(reentry.Path);
            if(reentry.Length<1||reentry.Digest==null)return;
            reentry.Returned=true;reentry.Hydration=null;reentry.Lease?.Dispose();reentry.Lease=null;
            TryFinishRecyclerHydrationOpen(client,reentry);
        }
    }
    internal static void ObserveActualRecyclerHydrationOpen(XUiC_WorkstationWindowGroup group,long session)
    {
        if(!RebirthProductiveEarnedUiDeferredTransport.IsActualCachedWindowOpen(group,session))return;
        var client=recyclerClients.SingleOrDefault(value=>ReferenceEquals(value.Source?.Station,group.WorkstationData?.TileEntity)&&
            ReferenceEquals(value.Source.Actor,group.xui?.playerUI?.entityPlayer));
        var reentry=client?.Reentry;
        if(reentry==null||!client.MetadataCurrent()||!reentry.Returned&&reentry.Hydration?.CurrentSource()!=true)return;
        reentry.OpeningWindow=group;reentry.OpeningSession=session;
        if(reentry.Returned)TryFinishRecyclerHydrationOpen(client,reentry);
    }
    private static void TryFinishRecyclerHydrationOpen(RecyclerClient client,RecyclerReadReentry reentry)
    {
        var group=reentry.OpeningWindow;long session=reentry.OpeningSession;
        if(group==null||session<1||!ReferenceEquals(client.Reentry,reentry)||!reentry.Returned||!client.MetadataCurrent()||
            !ReferenceEquals(group.WorkstationData?.TileEntity,client.Source.Station)||!ReferenceEquals(group.xui?.playerUI?.entityPlayer,client.Source.Actor)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(group,out long actualSession)||actualSession!=session||
            RebirthWorkstationCompletionPublication.PublishedLength(reentry.Path)!=reentry.Length||RecyclerFileDigest(reentry.Path)!=reentry.Digest||
            !RebirthProductiveEarnedUiDeferredTransport.IsCurrentQueueOwnerNativeRead(client.Source.World,client.Source.Station,reentry.ReadVersion)||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(client.Source.Station,reentry.Stage=="after"?client.Source.AfterNative:client.Source.BeforeNative,
                reentry.Stage=="after"?client.Source.AfterOffset:client.Source.BeforeOffset)||
            group.windowGroup?.isShowing!=true||LockManager.Instance?.IsLockedByLocalPlayer(client.Source.Station,0)!=true)return;
        if(client.ColdJoined!=null&&client.Owner==null)
        {client.Owner=OwnerPublication.FromActualRemoteHydration(client,reentry);if(client.Owner==null)return;}
        client.Window=group;client.Session=session;client.UnqualifiedStation=null;client.Reentry=null;reentry.Dispose();
        if(client.ColdJoined!=null)
        {
            var joined=client.ColdJoined;joined.UnqualifiedStations.Remove(client.Source.Operation);
            joined.Unstarted.Remove(client.Source.Operation);
            joined.Pending=joined.Pending.Where(operation=>operation!=client.Source.Operation).ToArray();
            client.ColdJoined=null;if(joined.Pending.Length==0)joined.Dispose();
        }
    }
    private static void HoldActualRecyclerHydrationTarget(RebirthProductiveEarnedUiDeferredTransport.ClientHydration hydration,XElement node,Guid operation,Guid nonce)
    {
        if(nonce==Guid.Empty||hydration.Station==null||!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(hydration))return;
        var client=recyclerClients.SingleOrDefault(value=>value.Source?.Operation==operation&&ReferenceEquals(value.Connection,hydration.Connection)&&
            ReferenceEquals(value.Source.World,hydration.World)&&ReferenceEquals(value.Source.Actor,hydration.Actor));
        if(client?.Source.OwnerLineageCurrent()==true&&(client.Nonce==Guid.Empty||client.Nonce==nonce)&&
            (string)node.Attribute("owner")==client.Source.Owner&&(string)node.Attribute("creation")==client.Source.Creation&&
            (int?)node.Attribute("deaths")==client.Source.Deaths&&(string)node.Attribute("world")==client.Source.WorldKey&&
            (string)node.Attribute("original")==RebirthStationPublicationRecord.Digest(client.Source.Image)&&
            hydration.Station.ToWorldPos()==client.Source.Station.ToWorldPos())
        {client.UnqualifiedStation=hydration.Station;return;}
        foreach(var joined in joinedRecyclerOwners)
            if(joined.Current()&&joined.Pending.Contains(operation)&&ReferenceEquals(joined.Actor,hydration.Actor)&&ReferenceEquals(joined.World,hydration.World)&&
                ReferenceEquals(joined.Connection,hydration.Connection)&&(string)node.Attribute("owner")==joined.Owner&&
                (string)node.Attribute("creation")==joined.Creation&&(int?)node.Attribute("deaths")==joined.Deaths&&(string)node.Attribute("world")==joined.WorldKey)
            {
                // Actual authenticated read classification only. A failed import
                // retains this fence; it never yields a slot or source permit.
                joined.UnqualifiedStations[operation]=hydration.Station;return;
            }
    }
}
