using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    [ThreadStatic]private static NetPackage processingRecyclerRequest;
    // The host owns only native station custody and actual accepted owner-file
    // publications. The authentic remote actor performs every owner slot.
    internal sealed partial class RemoteHostOriginal:IDisposable,IStationOriginal
    {
        internal ClientInfo Peer{get;private set;}internal EntityPlayer Actor{get;private set;}internal Guid Nonce{get;private set;}
        internal INetConnection Connection{get;private set;}
        public Guid Operation{get;}public string Purpose{get;}public string Owner{get;}public string Creation{get;}public string WorldKey{get;}
        public int Deaths{get;}public World World{get;}public TileEntityWorkstation Station{get;private set;}
        public byte[] BeforeNative{get;private set;}public byte[] AfterNative{get;private set;}public byte[] Image{get;private set;}
        public int BeforeOffset{get;}public int AfterOffset{get;}
        internal ItemStack[] BeforeBag,BeforeBelt,AfterBag,AfterBelt;internal bool[] BagLocks,BeltLocks;internal int BeltCount;
        internal Slot[] Slots;internal RemoteOwnerCheckpoint OwnerCheckpoint,OwnerBeforeCheckpoint;
        internal readonly string Account;private readonly string save;private readonly object provider;private IDisposable memory;
        internal bool AcceptedStation;internal StationPublication Publication;
        internal AccessHold Access;internal bool FinancialReleased;internal byte[] Terminal;internal string TerminalPath;
        internal bool PlanSaved,AdmissionEntered,AdmissionUnknown;
        internal long AutomaticOwnerSequence;
        internal bool ColdNeedsOwner;internal int ColdPhase,ColdCompleted;internal LoadedStation ColdStation;
        internal bool OwnerRejoined;
        internal bool UnusedRequested;
        internal LoadedStation PendingReentry;
        internal ClosedAuthorityCompletion ClosedAuthority;
        internal IDisposable TerminalMemory;
        private RemoteHostOriginal(ClientInfo peer,EntityPlayer actor,TileEntityWorkstation station,RebirthStablePlayerIdentity identity,Guid operation,string purpose,
            byte[] original,byte[] before,byte[] after,int beforeOffset,int afterOffset,IDisposable memory,Guid nonce=default)
        {
            Peer=peer;Connection=peer.netConnection[0];Actor=actor;Station=station;World=actor.world;Operation=operation;Purpose=purpose;Owner=identity.StorageKey;Account=identity.CanonicalId;
            Creation=RebirthCamperCharacterAccess.CurrentCreation(actor);Deaths=actor.Died;WorldKey=RebirthWorkstationCompletionOutbox.CurrentGuid(World);
            Image=original;BeforeNative=before;AfterNative=after;BeforeOffset=beforeOffset;AfterOffset=afterOffset;Nonce=nonce==Guid.Empty?Guid.NewGuid():nonce;
            save=Path.GetFullPath(GameIO.GetSaveGameDir());provider=World.ChunkCache?.ChunkProvider;this.memory=memory;
        }
        internal bool OwnerCurrent()=>memory!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()==false&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&ReferenceEquals(Actor.world,World)&&Actor.IsSpawned()&&!Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            Peer.loginDone&&!Peer.disconnecting&&Connection!=null&&!Connection.IsDisconnected()&&ReferenceEquals(Peer.netConnection?[0],Connection)&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(Actor.entityId),Peer)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var identity)&&identity.StorageKey==Owner&&identity.CanonicalId==Account;
        public bool NativeLineageCurrent()=>memory!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            ReferenceEquals(World.ChunkCache?.ChunkProvider,provider)&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&WorldKey==RebirthWorkstationCompletionOutbox.CurrentGuid(World);
        internal bool SavedOwnerOriginCurrent()=>memory!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&
            ReferenceEquals(World.ChunkCache?.ChunkProvider,provider)&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&WorldKey==RebirthWorkstationCompletionOutbox.CurrentGuid(World);
        public bool Current()=>OwnerCurrent()&&NativeLineageCurrent()&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,AcceptedStation?AfterNative:BeforeNative,AcceptedStation?AfterOffset:BeforeOffset);
        internal bool HasOwnerAdmission()=>OwnerCurrent()&&(ClosedAuthority!=null?
            ReferenceEquals(ClosedAuthority.Attached,this)&&ClosedAuthority.Current():RebirthRecyclerRuntime.HasPlacedNativeAccess(Actor,Station));
        internal static RemoteHostOriginal Capture(NetPackage packet,World world,XElement header)
        {
            if(!authorityInstalled||!ReferenceEquals(processingRecyclerRequest,packet)||!ThreadManager.IsMainThread()||packet?.Sender==null||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||
                header?.Name!="recyclerRemotePrepare"||(int?)header.Attribute("version")!=1)return null;
            IDisposable memory=null;RemoteHostOriginal issued=null;
            try
            {
                int player=(int)header.Attribute("player");var peer=packet.Sender;var actor=world.GetEntity(player) as EntityPlayer;
                if(actor==null||!actor.IsSpawned()||actor.IsDead()||!peer.loginDone||peer.disconnecting||!ReferenceEquals(actor.world,world)||!packet.ValidEntityIdForSender(player)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(player),peer)||
                    !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(peer.netConnection?[0],connection)||connection.IsDisconnected()||
                    !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity))return null;
                var original=header.Element("original")?.Elements().SingleOrDefault();
                if(original?.Name!="placedRecyclerOriginal"||(int?)original.Attribute("version")!=1||
                    !Guid.TryParseExact((string)original.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                    (string)original.Attribute("owner")!=identity.StorageKey||(string)original.Attribute("account")!=identity.CanonicalId||
                    (string)original.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||(int?)original.Attribute("deaths")!=actor.Died||
                    (string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world))return null;
                string purpose=(string)original.Attribute("purpose");if(purpose!="enqueue"&&purpose!="cancel"&&purpose!="complete")return null;
                var position=new Vector3i((int)original.Attribute("x"),(int)original.Attribute("y"),(int)original.Attribute("z"));
                var station=world.GetTileEntity(position) as TileEntityWorkstation;
                ClosedAuthorityCompletion closed=null;
                var closedHeader=original.Element("closedAuthority");
                if(closedHeader!=null)
                {
                    if(purpose!="complete"||!Guid.TryParseExact((string)closedHeader.Attribute("ticket"),"N",out var ticket)||ticket==Guid.Empty)return null;
                    closed=TryFindClosedAuthority(operation,ticket);
                    if(closed==null||closed.Attached!=null||!ReferenceEquals(closed.Recipient,actor)||!ReferenceEquals(closed.Peer,peer)||
                        !ReferenceEquals(closed.Connection,connection)||!ReferenceEquals(closed.Station,station)||!closed.MatchesOwnerOriginal(original))return null;
                }
                if(station==null||station is RebirthCamperStationState||(string)original.Attribute("block")!=station.block.GetBlockName()||
                    (closed==null&&(!RebirthRecyclerRuntime.HasPlacedNativeAccess(actor,station)||HoldsStation(station)))||
                    !Guid.TryParseExact((string)header.Attribute("request"),"N",out var request)||request==Guid.Empty||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out memory))return null;
                RebirthRecyclerRuntime.PlacedActionPlan plan;
                if(closed!=null)
                    plan=new RebirthRecyclerRuntime.PlacedActionPlan{Actor=actor,Station=station,Operation=operation,Purpose="complete",Input=closed.Input,
                        Queue=closed.Queue,Output=closed.Output,Tools=closed.Tools,Returns=closed.Returns,Cycle=true};
                else if(purpose=="complete")
                {if(!RebirthRecyclerRuntime.TryPlanPlacedCompletion(actor,station,(float?)original.Attribute("workSeconds")??-1,out plan))return null;}
                else if(!RebirthRecyclerRuntime.TryPlanPlacedAction(actor,station,(int?)header.Attribute("slot")??0,(int?)header.Attribute("units")??0,request,purpose=="cancel",()=>
                    ReferenceEquals(world.GetEntity(player),actor)&&RebirthRecyclerRuntime.HasPlacedNativeAccess(actor,station),out plan))return null;
                byte[] before=ColdBytes(original.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),after=ColdBytes(original.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                int beforeOffset=(int)original.Attribute("beforeOffset"),afterOffset=(int)original.Attribute("afterOffset");
                if((int?)original.Attribute("cycle")!=(plan.Cycle?1:0)||!RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var native,out ulong elapsed)||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,before,beforeOffset))return null;
                native.Input=plan.Input;native.Queue=plan.Cycle?ProjectNativeCycle(plan.Queue):plan.Queue;native.Output=plan.Output;native.Tools=plan.Tools;
                byte[] expected=RebirthProductiveEarnedNativeBody.Encode(native,elapsed,out int expectedOffset);
                if(expectedOffset!=afterOffset||!expected.SequenceEqual(after))return null;
                var returns=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("returns"),4*1024*1024));if(!SameOwnerGrid(returns,plan.Returns))return null;
                byte[] image=Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting));if(image.Length>8*1024*1024)return null;
                issued=new RemoteHostOriginal(peer,actor,station,identity,operation,purpose,image,before,after,beforeOffset,afterOffset,memory);memory=null;
                issued.ClosedAuthority=closed;
                issued.BeforeBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBag"),4*1024*1024));
                issued.BeforeBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBelt"),4*1024*1024));
                issued.AfterBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBag"),4*1024*1024));
                issued.AfterBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBelt"),4*1024*1024));
                string bag=(string)original.Attribute("bagLocks"),belt=(string)original.Attribute("beltLocks");
                if(bag==null||belt==null||bag.Length!=issued.BeforeBag?.Length||belt.Length!=issued.BeforeBelt?.Length||
                    bag.Any(value=>value!='0'&&value!='1')||belt.Any(value=>value!='0'&&value!='1'))return null;
                issued.BagLocks=bag.Select(value=>value=='1').ToArray();issued.BeltLocks=belt.Select(value=>value=='1').ToArray();issued.BeltCount=(int)original.Attribute("beltCount");
                if(!RebirthRecyclerOwnerDelivery.Plan(issued.BeforeBag,issued.BeforeBelt,issued.BagLocks,issued.BeltLocks,issued.BeltCount,plan.Returns,out var afterBag,out var afterBelt)||
                    !SameOwnerGrid(afterBag,issued.AfterBag)||!SameOwnerGrid(afterBelt,issued.AfterBelt)||!issued.Current())return null;
                issued.Slots=Enumerable.Range(0,2).SelectMany(area=>Enumerable.Range(0,area==0?issued.BeforeBag.Length:issued.BeforeBelt.Length)
                    .Where(index=>!Same((area==0?issued.BeforeBag:issued.BeforeBelt)[index],(area==0?issued.AfterBag:issued.AfterBelt)[index])).Select(index=>new Slot(area,index))).ToArray();
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(issued.memory,checked(16L*(image.Length+before.Length+after.Length)+65536)))return null;
                if(closed!=null&&!closed.Attach(issued))return null;
                var result=issued;issued=null;return result;
            }
            catch{return null;}
            finally{issued?.Dispose();memory?.Dispose();}
        }
        internal bool MatchesOwnerCheckpoint(ItemStack[] bag,ItemStack[] belt,int completed)
        {
            if(completed<0||completed>Slots.Length||bag?.Length!=BeforeBag.Length||belt?.Length!=BeforeBelt.Length)return false;
            for(int area=0;area<2;area++)for(int index=0;index<(area==0?bag:belt).Length;index++)
            {
                int ordinal=Array.FindIndex(Slots,value=>value.Area==area&&value.Index==index);
                if(!Same((area==0?bag:belt)[index],ordinal>=0&&ordinal<completed?(area==0?AfterBag:AfterBelt)[index]:(area==0?BeforeBag:BeforeBelt)[index]))return false;
            }
            return true;
        }
        public void Dispose(){ReleaseClosedAuthoritySource(this);AcceptedUpload=null;if(!ReferenceEquals(OwnerBeforeCheckpoint,OwnerCheckpoint))OwnerBeforeCheckpoint?.Dispose();OwnerBeforeCheckpoint=null;
            OwnerCheckpoint?.Dispose();OwnerCheckpoint=null;Publication?.Dispose();Publication=null;if(ColdStation?.Adopted==true)ColdStation.Dispose();ColdStation=null;
            if(PendingReentry?.Adopted==true)PendingReentry.Dispose();PendingReentry=null;
            BeforeNative=AfterNative=Image=Terminal=null;BeforeBag=BeforeBelt=AfterBag=AfterBelt=null;BagLocks=BeltLocks=null;Slots=null;TerminalMemory?.Dispose();TerminalMemory=null;memory?.Dispose();memory=null;}
    }
}
