// Native peer/frame/owner/controller/publication/budget boundaries are explicit
// adapters. No inventory setters or native persistence success is asserted.
struct Vector3i{internal int x,y,z;internal Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
interface INetConnection{bool IsDisconnected();}
class Connection:INetConnection{public bool IsDisconnected()=>false;}
class ClientInfo{internal int entityId;internal bool loginDone=true,disconnecting;internal INetConnection[] netConnection=new INetConnection[]{new Connection()};}
class ClientRoster{internal Dictionary<int,ClientInfo> Values=new Dictionary<int,ClientInfo>();internal ClientInfo ForEntityId(int id)=>Values.TryGetValue(id,out var peer)?peer:null;}
class ConnectionManager{internal ClientRoster Clients=new ClientRoster();}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
class NetPackage{internal ClientInfo Sender;internal bool ValidEntityIdForSender(int id)=>Sender?.entityId==id;}
class NetPackageRebirthRecyclerPlacedRequestV1:NetPackage{}
class RebirthStablePlayerIdentity{internal string StorageKey="storage",CanonicalId="recipient";internal static bool TryFromClientInfo(ClientInfo peer,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return peer!=null;}}
static class RebirthProductiveEarnedUiDeferredTransport{internal static bool TryGetReceivedConnection(NetPackage packet,out INetConnection connection){connection=packet.Sender?.netConnection?[0];return connection!=null;}}
static class RebirthProductiveEarnedObservationBudget{
    private sealed class Lease:IDisposable{public void Dispose(){}}
    internal static bool TryRetainDetached(long size,out IDisposable lease){lease=new Lease();return true;}
    internal static bool TryResizeDetached(IDisposable lease,long size)=>true;
    internal static IDisposable TryEnterDecoder()=>new Lease();
}
static class RebirthWorkstationCompletionOutbox{internal static string CurrentGuid(World world)=>"world";internal static string Root(string domain)=>"fixture-"+domain;}
static class RebirthProductiveEarnedNativeBody{
    internal const int MaximumBodyBytes=1024;
    internal static Action CaptureCallback;
    internal static bool TryCaptureUiProjection(TileEntityWorkstation station,RecipeQueueItem[] queue,ItemStack[] input,ItemStack[] output,object fuel,ItemStack[] tools,out byte[] before,out byte[] after,out int beforeOffset,out int afterOffset){before=new byte[]{1};after=new byte[]{2};beforeOffset=afterOffset=0;CaptureCallback?.Invoke();return true;}
    internal static bool MatchesCurrent(TileEntityWorkstation station,byte[] bytes,int offset)=>station!=null&&offset==0&&bytes.SequenceEqual(new byte[]{1});
    internal static bool SameExceptElapsed(byte[] left,int leftOffset,byte[] right,int rightOffset)=>leftOffset==rightOffset&&left.SequenceEqual(right); // Native clock grammar is a separate boundary.
}
public static partial class RebirthRecyclerRuntime{internal static void SetHandlerGroup(Guid id){group=new Group{Id=id};}}
internal static partial class RebirthRecyclerPlacedSettlement{
    private static bool authorityInstalled=true;
    private static NetPackage processingRecyclerRequest;
    private static Original admittingViewedLocalOriginal;
    private static NetPackageRebirthRecyclerPlacedRequestV1 admittingViewedLocalPacket;
    private static readonly List<ClosedAuthorityCompletion> closedAuthorityCompletions=new List<ClosedAuthorityCompletion>();
    private static int localAdmissions,archives;private static EntityPlayer lastRecipient;
    private static bool HoldsStation(TileEntityWorkstation station)=>closedAuthorityCompletions.Any(value=>ReferenceEquals(value.Station,station));
    private static bool HoldsOwner(EntityPlayerLocal player)=>false;
    private static bool HasNativeOriginalMarker(TileEntityWorkstation station)=>false;
    private static bool ArtifactNodeExists(string path)=>false;
    private static bool TryResolveViewedAttempt(World world,XElement request,out Guid attempt)=>Guid.TryParseExact((string)request.Attribute("operation"),"N",out attempt); // Existing retry metadata is a separate boundary.
    private static bool TrySelectAuthorityAttempt(EntityPlayerLocal actor,TileEntityWorkstation station,Guid requested,string purpose,out Guid attempt){attempt=requested;return true;}
    private static bool TrySelectServerCompletionAttempt(EntityPlayer actor,TileEntityWorkstation station,Guid requested,out Guid attempt){attempt=requested;return true;}
    private static bool TryReplyViewedTerminal(NetPackageRebirthRecyclerPlacedRequestV1 packet,World world,XElement request)=>false; // Terminal publication is a separate boundary.
    private static byte[] ColdBytes(XElement node,int maximum){var bytes=Convert.FromBase64String(node.Value);if(bytes.Length>maximum)throw new Exception();return bytes;}
    private static XElement Bytes(string name,byte[] bytes)=>new XElement(name,Convert.ToBase64String(bytes));
    private static XElement Grid(string name,ItemStack[] items)=>new XElement(name,items.Sum(item=>item.count));
    private static RecipeQueueItem[] ProjectNativeCycle(RecipeQueueItem[] queue)=>queue;
    private static bool TryRetainAuthority(Original source){if(!IsViewedLocalAdmission(source))throw new Exception("unbound local admission");localAdmissions++;lastRecipient=source.Actor;return true;}
    private sealed class AccessHold{internal static AccessHold CapturePending(ClosedAuthorityCompletion source)=>source.Current()?new AccessHold():null;}
    private sealed class Original:IDisposable{
        internal EntityPlayerLocal Actor;internal byte[] BeforeNative=new byte[]{1},AfterNative=new byte[]{2},Image;internal int BeforeOffset,AfterOffset;
        internal bool Current()=>Actor!=null&&!Actor.IsDead();public void Dispose(){}
        internal static Original Capture(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,string purpose,ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> returns,ItemStack[] output,ItemStack[] tools,Func<bool> current,bool cycle=false,float workSeconds=0,XElement nativeViewer=null)
            =>current()?new Original{Actor=actor,Image=Encoding.UTF8.GetBytes(new XElement("placedRecyclerOriginal",new XElement(nativeViewer)).ToString(SaveOptions.DisableFormatting))}:null;
    }
    private sealed class ClosedAuthorityCompletion:IDisposable{
        internal Guid Operation;internal TileEntityWorkstation Station;internal EntityPlayer Recipient;internal AccessHold Access;internal bool ViewedOrigin;private IDisposable memory;
        private ClosedAuthorityCompletion(EntityPlayer recipient,ClientInfo peer,RebirthStablePlayerIdentity identity,TileEntityWorkstation station,RecipeQueueItem row,Guid operation,Guid group,float seconds,
            ItemStack[] input,RecipeQueueItem[] queue,ItemStack[] output,ItemStack[] tools,ItemStack[] returns,byte[] before,byte[] after,int beforeOffset,int afterOffset,byte[] image,IDisposable memory){Recipient=recipient;Station=station;Operation=operation;this.memory=memory;}
        internal bool Current()=>Recipient!=null&&Recipient.IsSpawned()&&!Recipient.IsDead()&&ReferenceEquals(Recipient.world.GetEntity(Recipient.entityId),Recipient);
        internal bool EnsureOriginalPublished(){archives++;lastRecipient=Recipient;return true;}
        public void Dispose(){memory?.Dispose();}
        // ACTUAL_REMOTE_FACTORY
    }
    // ACTUAL_SERVER
    private static void Need(bool value,string text){if(!value)throw new Exception(text);}
    internal static void ExerciseHandler(){
        for(int variant=0;variant<10;variant++){
            RebirthProductiveEarnedNativeBody.CaptureCallback=null;
            closedAuthorityCompletions.Clear();localAdmissions=archives=0;lastRecipient=null;admittingViewedLocalOriginal=null;admittingViewedLocalPacket=null;
            var world=new World();var station=new TileEntityWorkstation();world.Station=station;GameManager.Instance.World=world;
            Guid group=Guid.NewGuid();RebirthRecyclerRuntime.SetHandlerGroup(group);station.Queue=new[]{new RecipeQueueItem{Recipe=new Recipe{Group=group}}};
            var viewer=new EntityPlayer{world=world,entityId=1,Owner="viewer",Lease=true};EntityPlayer recipient=variant==1?new EntityPlayerLocal():new EntityPlayer();
            recipient.world=world;recipient.entityId=2;recipient.Owner="recipient";recipient.Lease=false;
            world.Registered[1]=viewer;world.Registered[2]=recipient;world.Players.list.Add(viewer);world.Players.list.Add(recipient);
            var viewerPeer=new ClientInfo{entityId=1};var recipientPeer=new ClientInfo{entityId=2};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
            SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Values[1]=viewerPeer;SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Values[2]=recipientPeer;
            var packet=new NetPackageRebirthRecyclerPlacedRequestV1{Sender=viewerPeer};processingRecyclerRequest=packet;
            var request=new XElement("recyclerViewedCompletion",new XAttribute("version",1),new XAttribute("player",1),new XAttribute("recipientOwner","recipient"),new XAttribute("recipientCreation","creation"),
                new XAttribute("creation","creation"),new XAttribute("deaths",0),new XAttribute("operation",group.ToString("N")),new XAttribute("group",group.ToString("N")),new XAttribute("seconds",2),
                new XAttribute("x",1),new XAttribute("y",2),new XAttribute("z",3),new XAttribute("beforeOffset",0),new XAttribute("afterOffset",0),Bytes("beforeNative",new byte[]{1}),Bytes("afterNative",new byte[]{2}));
            switch(variant){case 2:viewer.Lease=false;break;case 3:request.SetAttributeValue("recipientOwner","foreign");break;case 4:request.SetElementValue("afterNative",Convert.ToBase64String(new byte[]{3}));break;case 5:viewerPeer.entityId=9;break;case 6:viewer.Died++;break;case 7:recipient.Creation="other";break;}
            if(variant==8)RebirthProductiveEarnedNativeBody.CaptureCallback=()=>viewer.Died++;
            if(variant==9)RebirthProductiveEarnedNativeBody.CaptureCallback=()=>viewerPeer.netConnection[0]=new Connection();
            ReceiveViewedCompletion(packet,world,request);
            Need(localAdmissions==(variant==1?1:0)&&archives==(variant==0?1:0)&&closedAuthorityCompletions.Count==(variant==0?1:0),"actual viewer handler role/admission result "+variant);
            if(variant<=1)Need(ReferenceEquals(lastRecipient,recipient)&&!ReferenceEquals(lastRecipient,viewer),"viewer received recipient reward");
            if(variant==0){ReceiveViewedCompletion(packet,world,request);Need(archives==1&&closedAuthorityCompletions.Count==1,"duplicate ordinary introduction recaptured source");}
            Need(station.Input[0].count==7&&station.Output[0].count==0&&station.Queue[0].Recipe!=null,"introduction changed station before controller admission");
            Need(admittingViewedLocalOriginal==null&&admittingViewedLocalPacket==null,"local packet capability escaped handler");
        }
        processingRecyclerRequest=null;
        RebirthProductiveEarnedNativeBody.CaptureCallback=null;
        Console.WriteLine("MP027 actual ordinary local/remote viewer introduction handlers and actual planner; owner/native/controller boundaries are adapters");
    }
}
