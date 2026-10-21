using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Issued from actual Runtime.ProcessOne or an authenticated native viewer
    // completion verified against the host station's complete native image.
    // This is station custody, never a remote inventory setter permit.
    internal sealed partial class ClosedAuthorityCompletion:IDisposable,IStationOriginal
    {
        public Guid Operation{get;}internal readonly Guid Group,Ticket;
        public string Purpose=>"complete";public string Owner{get;}internal readonly string Account;
        public string Creation{get;}public int Deaths{get;}public string WorldKey{get;}
        public World World{get;}public TileEntityWorkstation Station{get;private set;}
        internal EntityPlayer Recipient{get;private set;}internal ClientInfo Peer{get;private set;}internal INetConnection Connection{get;private set;}
        internal Guid OwnerGeneration{get;private set;}
        internal RecipeQueueItem Row;internal Recipe Recipe;internal ItemStack[] Input,Output,Tools,Returns;internal RecipeQueueItem[] Queue;
        public byte[] BeforeNative{get;private set;}public byte[] AfterNative{get;private set;}public byte[] Image{get;private set;}
        public int BeforeOffset{get;}public int AfterOffset{get;}internal readonly float WorkSeconds;
        internal AccessHold Access;internal RemoteHostOriginal Attached;
        private readonly object provider;private readonly string save;private IDisposable memory;
        private ClosedAuthorityCompletion(EntityPlayer recipient,ClientInfo peer,RebirthStablePlayerIdentity identity,TileEntityWorkstation station,RecipeQueueItem row,
            Guid operation,Guid group,float seconds,ItemStack[] input,RecipeQueueItem[] queue,ItemStack[] output,ItemStack[] tools,ItemStack[] returns,
            byte[] before,byte[] after,int beforeOffset,int afterOffset,byte[] image,IDisposable memory,Guid ticket=default)
        {
            Recipient=recipient;Peer=peer;Connection=peer.netConnection[0];World=recipient.world;Station=station;Operation=operation;Group=group;Ticket=ticket==Guid.Empty?Guid.NewGuid():ticket;
            Owner=identity.StorageKey;Account=identity.CanonicalId;Creation=RebirthCamperCharacterAccess.CurrentCreation(recipient);Deaths=recipient.Died;
            WorldKey=RebirthWorkstationCompletionOutbox.CurrentGuid(World);Row=row;Recipe=row?.Recipe;Input=input;Queue=queue;Output=output;Tools=tools;Returns=returns;
            BeforeNative=before;AfterNative=after;BeforeOffset=beforeOffset;AfterOffset=afterOffset;Image=image;WorkSeconds=seconds;
            provider=World.ChunkCache?.ChunkProvider;save=Path.GetFullPath(GameIO.GetSaveGameDir());this.memory=memory;
        }
        internal bool RecipientCurrent()=>Attached!=null?memory!=null&&Attached.OwnerCurrent():memory!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&
            ReferenceEquals(World.GetEntity(Recipient.entityId),Recipient)&&Recipient.IsSpawned()&&!Recipient.IsDead()&&Recipient.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Recipient)==Creation&&
            Peer.loginDone&&!Peer.disconnecting&&!Connection.IsDisconnected()&&ReferenceEquals(Peer.netConnection?[0],Connection)&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(Recipient.entityId),Peer)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var identity)&&identity.StorageKey==Owner&&identity.CanonicalId==Account;
        public bool NativeLineageCurrent()=>memory!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            ReferenceEquals(World.ChunkCache?.ChunkProvider,provider)&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldKey;
        internal bool NativeOriginCurrent()=>memory!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&
            ReferenceEquals(World.ChunkCache?.ChunkProvider,provider)&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldKey;
        public bool Current()=>RecipientCurrent()&&NativeLineageCurrent()&&
            (Attached==null?ReferenceEquals(Station.Queue?.LastOrDefault(),Row)&&ReferenceEquals(Row.Recipe,Recipe)&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,BeforeNative,BeforeOffset):
                ReferenceEquals(Attached.Station,Station)&&Attached.Operation==Operation&&Attached.OwnerCurrent()&&Attached.NativeLineageCurrent()&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Attached.AcceptedStation?AfterNative:BeforeNative,Attached.AcceptedStation?AfterOffset:BeforeOffset));
        internal bool MatchesOwnerOriginal(XElement original)=>Current()&&original?.Name=="placedRecyclerOriginal"&&
            (string)original.Attribute("operation")==Operation.ToString("N")&&(string)original.Attribute("purpose")=="complete"&&
            (string)original.Attribute("owner")==Owner&&(string)original.Attribute("account")==Account&&(string)original.Attribute("creation")==Creation&&
            (int?)original.Attribute("deaths")==Deaths&&(string)original.Attribute("world")==WorldKey&&
            (int?)original.Attribute("x")==Station.ToWorldPos().x&&(int?)original.Attribute("y")==Station.ToWorldPos().y&&(int?)original.Attribute("z")==Station.ToWorldPos().z&&
            (string)original.Attribute("block")==Station.block.GetBlockName()&&(int?)original.Attribute("cycle")==1&&(float?)original.Attribute("workSeconds")==WorkSeconds&&
            (int?)original.Attribute("beforeOffset")==BeforeOffset&&(int?)original.Attribute("afterOffset")==AfterOffset&&
            ColdBytes(original.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes).SequenceEqual(BeforeNative)&&
            ColdBytes(original.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes).SequenceEqual(AfterNative)&&
            XNode.DeepEquals(original.Element("closedAuthority"),new XElement("closedAuthority",new XAttribute("ticket",Ticket.ToString("N")),new XAttribute("group",Group.ToString("N")),
                new XAttribute("nativeOriginal",RebirthStationPublicationRecord.Digest(Image))));
        internal long MeasureImage=>Image?.LongLength??0;
        internal XElement CopyImage()=>Current()?XElement.Parse(Encoding.UTF8.GetString(Image)):null;
        internal bool EnsureOriginalPublished()
        {
            if(!Current())return false;
            // Keep the privately issued runtime event and its ticket across an
            // uncertain write. The publication owner can resume only these bytes.
            var native=CopyImage();if(native==null)return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldKey),
                new XElement("before",new XElement(native)),
                new XElement("recyclerClosedAuthoritySource",new XAttribute("ticket",Ticket.ToString("N")),new XAttribute("actualRuntimeRow",!ViewedOrigin),ViewedOrigin?new XAttribute("actualNativeUi",true):null,native));
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Operation.ToString("N")+".closed-authority."+Ticket.ToString("N")+".xml");
            return RebirthWorkstationCompletionPublication.Write(path,record)&&
                XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))&&Current();
        }
        internal bool Attach(RemoteHostOriginal original)
        {
            if(Attached!=null||original==null||!ReferenceEquals(original.Peer,Peer)||!ReferenceEquals(original.Connection,Connection)||!ReferenceEquals(original.Actor,Recipient)||
                !ReferenceEquals(original.Station,Station)||!MatchesOwnerOriginal(XElement.Parse(Encoding.UTF8.GetString(original.Image))))return false;
            Attached=original;if(Current())return true;Attached=null;return false;
        }
        internal AccessHold TransferAccess(RemoteHostOriginal original)=>ReferenceEquals(Attached,original)&&Access?.RebindClosedAuthority(this,original)==true?Access:null;
        internal bool RebindActualStation(RemoteHostOriginal source,LoadedStation loaded)
        {
            if(!ReferenceEquals(Attached,source)||!ReferenceEquals(source.ClosedAuthority,this)||loaded?.Current()!=true||!ReferenceEquals(source.PendingReentry,loaded)||
                !loaded.MatchesOriginal(source.Image)||loaded.Operation!=Operation||!ReferenceEquals(loaded.World,World)||
                !ReferenceEquals(source.Station,loaded.Station)||!source.Current())return false;
            var previous=Station;Station=loaded.Station;if(Current())return true;Station=previous;return false;
        }
        public void Dispose()
        {closedAuthorityCompletions.Remove(this);closedGrantQueries.Remove(this);Pending?.Dispose();Pending=null;Attached=null;Row=null;Recipe=null;Input=Output=Tools=Returns=null;Queue=null;BeforeNative=AfterNative=Image=null;memory?.Dispose();memory=null;}
        internal static ClosedAuthorityCompletion Capture(EntityPlayer actor,TileEntityWorkstation station,RecipeQueueItem row,Guid operation,Guid group,float seconds,
            ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> returns,ItemStack[] output,ItemStack[] tools)
        {
            if(!authorityInstalled||!ThreadManager.IsMainThread()||actor==null||actor is EntityPlayerLocal||actor.world?.IsRemote()!=false||station==null||station is RebirthCamperStationState||
                operation==Guid.Empty||group==Guid.Empty||!RebirthRecyclerRuntime.IsProcessing(station)||!ReferenceEquals(station.Queue?.LastOrDefault(),row)||row?.Recipe==null||
                closedAuthorityCompletions.Count>=4||HoldsStation(station)||HasNativeOriginalMarker(station)||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)return null;
            if(!TrySelectServerCompletionAttempt(actor,station,operation,out operation))return null;
            var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId);
            if(peer?.loginDone!=true||peer.disconnecting||peer.netConnection?[0]==null||peer.netConnection[0].IsDisconnected()||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||
                string.IsNullOrEmpty(RebirthCamperCharacterAccess.CurrentCreation(actor))||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return null;
            ClosedAuthorityCompletion issued=null;
            try
            {
                byte[] before,after;int beforeOffset,afterOffset;var projected=ProjectNativeCycle(queue);
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder==null||projected==null||!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(station,projected,input,output,null,tools,out before,out after,out beforeOffset,out afterOffset))return null;
                var image=new XElement("recyclerClosedAuthorityOriginal",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("group",group.ToString("N")),
                    new XAttribute("owner",identity.StorageKey),new XAttribute("account",identity.CanonicalId),new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(actor)),
                    new XAttribute("deaths",actor.Died),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(actor.world)),new XAttribute("x",station.ToWorldPos().x),
                    new XAttribute("y",station.ToWorldPos().y),new XAttribute("z",station.ToWorldPos().z),new XAttribute("block",station.block.GetBlockName()),
                    new XAttribute("beforeOffset",beforeOffset),new XAttribute("afterOffset",afterOffset),new XAttribute("workSeconds",seconds),Bytes("beforeNative",before),Bytes("afterNative",after),Grid("returns",returns.ToArray()));
                byte[] bytes=Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));
                if(bytes.Length>8*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(16L*(bytes.Length+before.Length+after.Length)+65536)))return null;
                var value=issued=new ClosedAuthorityCompletion(actor,peer,identity,station,row,operation,group,seconds,ItemStack.Clone(input),(RecipeQueueItem[])queue.Clone(),ItemStack.Clone(output),
                    ItemStack.Clone(tools),returns.Select(item=>item.Clone()).ToArray(),before,after,beforeOffset,afterOffset,bytes,memory);memory=null;
                if(!value.Current())return null;issued=null;return value;
            }
            finally{issued?.Dispose();memory?.Dispose();}
        }
    }
    private static readonly List<ClosedAuthorityCompletion> closedAuthorityCompletions=new List<ClosedAuthorityCompletion>();
    internal static ClosedAuthorityCompletion TryFindClosedAuthority(Guid operation,Guid ticket)=>closedAuthorityCompletions.SingleOrDefault(value=>value.Operation==operation&&value.Ticket==ticket&&value.Current());
    private static bool ClosedAuthorityStationHeld(TileEntityWorkstation station)=>closedAuthorityCompletions.Any(value=>ReferenceEquals(value.Station,station)&&value.NativeLineageCurrent()&&value.Attached?.FinancialReleased!=true);
    internal static void ReleaseClosedAuthoritySource(RemoteHostOriginal source)
    {
        var token=source?.ClosedAuthority;if(token==null||!ReferenceEquals(token.Attached,source))return;
        // Failed adoption owns only this new observation graph. The actual
        // loaded marker and all durable originals remain available for retry.
        if(token.RestoredActualLoad&&!remoteRecyclerOriginals.Values.Any(value=>ReferenceEquals(value,source))&&source.Access==null)
        {token.Dispose();return;}
        if(source.FinancialReleased&&source.Access?.Ready()==true&&source.Terminal!=null&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(source.TerminalPath,source.Terminal))
        {token.Dispose();return;}
        if(!remoteRecyclerOriginals.Values.Any(value=>ReferenceEquals(value,source))&&(!source.PlanSaved||token.Pending?.CanReturnUnenteredAttachment(source)==true)&&source.Publication==null&&source.OwnerCheckpoint==null&&
            source.NativeLineageCurrent()&&RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.BeforeNative,source.BeforeOffset))
        {
            if(source.Access!=null&&!source.Access.RollbackClosedAuthority(source,token))return;
            token.Attached=null;
        }
    }
    internal static bool BeginAuthorityRemoteCompletion(EntityPlayer recipient,TileEntityWorkstation station,RecipeQueueItem row,Guid operation,Guid group,float seconds,
        ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> returns,ItemStack[] output,ItemStack[] tools)
    {
        var retained=closedAuthorityCompletions.SingleOrDefault(value=>value.Operation==operation||value.Group==group&&ReferenceEquals(value.Station,station));
        if(retained!=null)return ReferenceEquals(retained.Recipient,recipient)&&ReferenceEquals(retained.Station,station)&&retained.Group==group&&retained.Current();
        var source=ClosedAuthorityCompletion.Capture(recipient,station,row,operation,group,seconds,input,queue,returns,output,tools);if(source==null)return false;
        bool owned=false;
        try
        {
            source.Access=AccessHold.CapturePending(source);if(source.Access==null)return false;
            // Retain before the first possible write. Failed publication is a
            // pending admission, never permission to create another original.
            closedAuthorityCompletions.Add(source);owned=true;
            try{source.EnsureOriginalPublished();}catch{/* The pump retries this exact retained event. */}
            return true;
        }
        finally{if(!owned)source.Dispose();}
    }
}
