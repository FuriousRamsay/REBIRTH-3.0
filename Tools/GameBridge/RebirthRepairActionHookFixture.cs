// Deferred actual three repair hook extraction. Native actions are explicit
// counters only; this does not simulate native inventory or saved publication.
using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
class XUiC_RecipeStack {}
class ItemValue {}
class ItemActionEntryRepair {}
class EntityPlayer {public int entityId;public World world;}
class EntityPlayerLocal:EntityPlayer {}
class World {public EntityPlayerLocal Primary;public Dictionary<int,EntityPlayer> Bodies=new Dictionary<int,EntityPlayer>();public EntityPlayer GetEntity(int id)=>Bodies.TryGetValue(id,out var value)?value:null;public EntityPlayerLocal GetPrimaryPlayer()=>Primary;}
class GameManager {public static GameManager Instance=new GameManager();public World World;}
class RebirthStablePlayerIdentity
{
    public string CanonicalId;public static RebirthStablePlayerIdentity Local;
    public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=Local;return identity!=null;}
    public static bool TryFromClientInfo(ClientInfo peer,out RebirthStablePlayerIdentity identity){identity=peer.Identity;return identity!=null;}
}
class ClientInfo {public int entityId;public RebirthStablePlayerIdentity Identity;}
class ConnectionManager {public ConnectionManager(){}public ClientList Clients=new ClientList();public sealed class ClientList {public Dictionary<int,ClientInfo> Values=new Dictionary<int,ClientInfo>();public ClientInfo ForEntityId(int id)=>Values.TryGetValue(id,out var peer)?peer:null;}}
static class SingletonMonoBehaviour<T> where T:new() {public static T Instance=new T();}
class ItemStack {}
class PlayerDataFile {public MemoryStream bagData,inventoryData,equipmentData;public ItemStack dragAndDropItem;}
class NetPackage {}
class NetPackagePlayerData:NetPackage {public PlayerDataFile playerDataFile;}
class NetPackagePlayerInventory:NetPackage {public MemoryStream bagData,inventoryData,equipmentData;public ItemStack dragAndDropItem;}
static partial class RebirthRepairOriginalCorrection
{
    internal static int OutputCalls,Succeeded,CancelCalls,CancelledCalls,Accepted,QueueCalls,ActivationCalls,ActivationEnds;
    internal static bool ActivationRan;
    internal static bool Permit=true;
    internal static bool QueuePrefix(XUiC_RecipeStack card,ItemValue item,int amount){QueueCalls++;return Permit;}
    internal static void QueueAccepted(XUiC_RecipeStack card,bool accepted){if(accepted)Accepted++;}
    internal static bool CancelPrefix(XUiC_RecipeStack card,out object state){CancelCalls++;state=card;return Permit;}
    internal static void Cancelled(XUiC_RecipeStack card,object state){CancelledCalls++;}
    internal static bool OutputPrefix(XUiC_RecipeStack card){OutputCalls++;return Permit;}
    internal static void OutputSucceeded(XUiC_RecipeStack card,bool result){Succeeded++;}
    internal static bool PackageAccepted(NetPackage package,PlayerDataFile head)=>AcceptedNativeUpload(package,head);
    internal static bool Identity(EntityPlayer player,out RebirthStablePlayerIdentity identity)=>TryRepairIdentity(player,out identity);
    internal static object BeginActivation(ItemActionEntryRepair entry){ActivationCalls++;return entry;}
    internal static void EndActivation(object prior,Exception failure,bool nativeRan=true){ActivationEnds++;ActivationRan=nativeRan;}
}
static class RepairActionHookFixture
{
    private static object Invoke(Type type,string name,params object[] args)=>type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
    private static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void Main()
    {
        var card=new XUiC_RecipeStack();
        var skipped=new object[]{card,true,false,null};
        Need((bool)Invoke(typeof(RebirthRepairSignatureCompletionPatch),"Prefix",skipped)&&!(bool)skipped[3]&&
            (bool)skipped[1]&&RebirthRepairOriginalCorrection.OutputCalls==0,"earlier skip acquired output invocation or changed native result");
        var admitted=new object[]{card,false,true,null};
        Need((bool)Invoke(typeof(RebirthRepairSignatureCompletionPatch),"Prefix",admitted)&&(bool)admitted[3],"actual output admission refused");
        Invoke(typeof(RebirthRepairSignatureCompletionPatch),"Postfix",card,true,true,false);
        Need(RebirthRepairOriginalCorrection.Succeeded==0,"skipped original issued output success");
        Invoke(typeof(RebirthRepairSignatureCompletionPatch),"Postfix",card,true,true,true);
        Need(RebirthRepairOriginalCorrection.Succeeded==1,"actual output not observed");
        var cancelled=new object[]{card,false,null};
        Need((bool)Invoke(typeof(RebirthRepairSignatureCancelPatch),"Prefix",cancelled)&&cancelled[2]==null&&
            RebirthRepairOriginalCorrection.CancelCalls==0,"earlier skip acquired refund custody");
        Invoke(typeof(RebirthRepairSignatureCancelPatch),"Postfix",card,card,false);
        Need(RebirthRepairOriginalCorrection.CancelledCalls==0,"skipped cancellation settled refund");
        Invoke(typeof(RebirthRepairSignatureQueuePatch),"Postfix",card,true,false);
        Need(RebirthRepairOriginalCorrection.Accepted==0,"skipped SetRepairRecipe admitted queue");
        var skippedQueue=new object[]{card,new ItemValue(),1,true,false};
        Need((bool)Invoke(typeof(RebirthRepairSignatureQueuePatch),"Prefix",skippedQueue)&&RebirthRepairOriginalCorrection.QueueCalls==0&&
            (bool)skippedQueue[3],"skipped SetRepairRecipe mutated original input/result");
        Invoke(typeof(RebirthRepairSignatureQueuePatch),"Postfix",card,true,true);
        Need(RebirthRepairOriginalCorrection.Accepted==1,"actual queue result not forwarded");
        var head=new PlayerDataFile();var complete=new NetPackagePlayerData{playerDataFile=head};
        Need(RebirthRepairOriginalCorrection.PackageAccepted(complete,head)&&!RebirthRepairOriginalCorrection.PackageAccepted(complete,new PlayerDataFile()),"full native foreign head accepted");
        var changed=new NetPackagePlayerInventory();Need(!RebirthRepairOriginalCorrection.PackageAccepted(changed,head),"empty packet refreshed stale owner proof");
        changed.bagData=new MemoryStream(new byte[]{1});head.bagData=changed.bagData;
        Need(RebirthRepairOriginalCorrection.PackageAccepted(changed,head),"actual accepted changed native bag refused");
        head.bagData=new MemoryStream(new byte[]{1});Need(!RebirthRepairOriginalCorrection.PackageAccepted(changed,head),"equal replacement blob became actual accepted packet");
        var skippedActivation=new object[]{new ItemActionEntryRepair(),false,null};
        Invoke(typeof(RebirthRepairOriginalActivation),"Prefix",skippedActivation);
        Need(skippedActivation[2]==null&&RebirthRepairOriginalCorrection.ActivationCalls==0,"skipped activation acquired action scope");
        var activeActivation=new object[]{new ItemActionEntryRepair(),true,null};Invoke(typeof(RebirthRepairOriginalActivation),"Prefix",activeActivation);
        var originalFailure=new Exception("native failure");
        Need(ReferenceEquals(Invoke(typeof(RebirthRepairOriginalActivation),"Finalizer",originalFailure,activeActivation[2],false),originalFailure)&&
            RebirthRepairOriginalCorrection.ActivationEnds==1&&!RebirthRepairOriginalCorrection.ActivationRan,"activation finalizer lost skip/exception or prior-scope cleanup");
        var world=new World();GameManager.Instance.World=world;var local=new EntityPlayerLocal{world=world,entityId=1};world.Primary=local;world.Bodies[1]=local;
        RebirthStablePlayerIdentity.Local=new RebirthStablePlayerIdentity{CanonicalId="actual-local"};
        Need(RebirthRepairOriginalCorrection.Identity(local,out var localIdentity)&&ReferenceEquals(localIdentity,RebirthStablePlayerIdentity.Local),"local identity not rooted in platform");
        Need(!RebirthRepairOriginalCorrection.Identity(new EntityPlayerLocal{world=world,entityId=1},out _),"same ID replacement local body accepted");
        var remote=new EntityPlayer{world=world,entityId=2};world.Bodies[2]=remote;
        Need(!RebirthRepairOriginalCorrection.Identity(remote,out _),"remote identity resolved without authenticated current peer");
        var peer=new ClientInfo{entityId=2,Identity=new RebirthStablePlayerIdentity{CanonicalId="actual-remote"}};
        SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Values[2]=peer;
        Need(RebirthRepairOriginalCorrection.Identity(remote,out var remoteIdentity)&&ReferenceEquals(remoteIdentity,peer.Identity),"remote identity not rooted in actual peer");
        Console.WriteLine("PASS actual repair hook run-original guards; no native gameplay qualification");
    }
}
