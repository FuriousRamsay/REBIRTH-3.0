// Actual complete status reader. Persistence, native owner receipts and record
// codecs below are explicit adapters; this does not prove native serialization.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
enum RebirthCamperRemoteStationCommandKind {Craft,CraftPlayer,Cancel,ScrapQueue}
class RebirthCamperRemoteStationCommand {internal Guid Operation,Job;internal string RecipeName="recipe";internal int Batches=2,Tier=3;internal RebirthCamperRemoteStationCommandKind Kind;}
struct RebirthCamperStationAddress {internal int Id;}
class EntityPlayer {internal World world;internal int entityId=1,Died;internal bool Dead;internal bool IsDead()=>Dead;}
class World {internal EntityPlayer Player;internal bool Remote;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Player;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthSurvivorMode {internal static bool Enabled=true;internal static bool IsEnabledForCurrentWorld()=>Enabled;}
static class GameIO {internal static string GetSaveGameDir()=>"isolated-adapter";}
class RebirthStablePlayerIdentity {internal string StorageKey="owner",CanonicalId="native";}
static class RebirthCamperRemoteStationCommandStatus {internal enum State {Unknown,Pending,Applied,Rejected}}
class Recipe {internal string Name="recipe";internal string GetName()=>Name;}
static class XUiM_Recipes {internal static object GetRecipes()=>null;}
class Payment {internal string OwnerKey="owner",OwnerId="native";}
class RebirthStationGridAdmission
{
    internal static RebirthStationGridAdmission Stored;
    internal string JobId,CreationId="creation";internal bool IsPublicationAttempted=true,IsPlayerPayment,IsPlayerPaymentConfirmed=true,Materializes=true,Quality=true,Valid=true;internal int OriginalBatches=2;
    internal RebirthCamperStationAddress Address;internal Payment Payment=new Payment();internal Recipe Recipe=new Recipe();
    internal XElement Write()=>new XElement("stationAdmission",new XAttribute("job",JobId));
    internal static bool TryReadStored(XElement n,out RebirthStationGridAdmission a){a=Stored;return n!=null&&a!=null&&a.Valid;}
    internal bool TryGetMobileAddress(out RebirthCamperStationAddress a){a=Address;return true;}
    internal bool TryGetPlayerPayment(out Payment p){p=Payment;return p!=null;}
    internal bool TryMaterialize(object definitions,out Recipe q,out object before,out object after){q=Recipe;before=after=null;return Materializes;}
    internal bool MatchesInitialQueuedQuality(Recipe q,int batches,int tier)=>Quality&&batches==OriginalBatches&&tier==3;
}
class RebirthCamperStationPublicationRecord
{
    internal static bool Valid=true,Scope=true;
    internal XElement Write()=>new XElement("camperStationPublication");
    internal static bool TryRead(XElement n,RebirthStationGridAdmission a,out RebirthCamperStationPublicationRecord p){p=new RebirthCamperStationPublicationRecord();return n!=null&&Valid;}
    internal bool MatchesSaveScope(string save)=>Scope;
}
class Terminal {internal bool IsCompletion;}
class RebirthCamperStationRefundDeliveryRecord
{
    internal static bool Valid=true;internal bool IsSaved;internal string DeliveryId="delivery";
    internal XElement Write()=>new XElement("delivery");
    internal static bool TryRead(XElement n,string owner,RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord q,Terminal i,object r,object t,object c,out RebirthCamperStationRefundDeliveryRecord d)
    {d=Fixture.State.CamperStationRefundDeliveries[Fixture.Job];return Valid&&n!=null;}
}
class RebirthStationRefundArchive
{
    internal static bool Valid=true;internal bool IsMobile=true,IsCompletion;internal string DeliveryId="delivery";
    internal XElement Write()=>new XElement("archive",RebirthStationGridAdmission.Stored.Write(),new XElement("camperStationPublication"));
    internal static bool TryRead(XElement n,string owner,out RebirthStationRefundArchive a){a=Fixture.State.StationRefundArchives[Fixture.Job];return n!=null&&Valid;}
}
class Progression
{
    internal Dictionary<string,RebirthStationGridAdmission> StationPreparations=new Dictionary<string,RebirthStationGridAdmission>();
    internal Dictionary<string,RebirthStationRefundArchive> StationRefundArchives=new Dictionary<string,RebirthStationRefundArchive>();
    internal Dictionary<string,RebirthCamperStationPublicationRecord> CamperStationPublications=new Dictionary<string,RebirthCamperStationPublicationRecord>();
    internal Dictionary<string,Terminal> StationTerminalIntents=new Dictionary<string,Terminal>();
    internal Dictionary<string,object> StationCancellationRefunds=new Dictionary<string,object>(),StationCancellationAttempts=new Dictionary<string,object>(),CamperStationCancellations=new Dictionary<string,object>();
    internal Dictionary<string,RebirthCamperStationRefundDeliveryRecord> CamperStationRefundDeliveries=new Dictionary<string,RebirthCamperStationRefundDeliveryRecord>();
}
class Origin {internal string CreationId="creation";}
class Owner {internal Progression Progression;internal Origin Origin=new Origin();}
static class RebirthWorldCharacterService
{
    internal static bool Identity=true;internal static RebirthStablePlayerIdentity Id=new RebirthStablePlayerIdentity();internal static Owner Owner;
    internal static bool TryGetIdentity(EntityPlayer p,out RebirthStablePlayerIdentity i){i=Id;return Identity;}
    internal static bool TryGet(EntityPlayer p,out Owner o){o=Owner;return o!=null;}
}
static class RebirthCamperStationNativeActorPayment
{internal static RebirthStablePlayerIdentity Identity=new RebirthStablePlayerIdentity();internal static bool RemoteIdentity(EntityPlayer p,out RebirthStablePlayerIdentity i,out object unused){i=Identity;unused=null;return i!=null;}}
static class RebirthCamperRemoteStationTransport {internal static bool Current=true;internal static bool IsCurrent(EntityPlayer p,RebirthCamperStationAddress a,string c,object l)=>Current;}
static class RebirthWorldCharacterRepository
{
    internal static bool Cached=true,SavedAdmission=true,SavedPublication=true,SavedArchive=true,SavedDelivery=true;internal static Action DuringRead;
    internal static bool IsCurrentCachedRecord(Owner o)=>Cached;
    private static bool Read(bool saved){DuringRead?.Invoke();return saved;}
    internal static bool HasSavedStationPreparation(RebirthStablePlayerIdentity i,RebirthStationGridAdmission a)=>Read(SavedAdmission);
    internal static bool HasSavedCamperStationPublication(RebirthStablePlayerIdentity i,RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord q)=>Read(SavedPublication);
    internal static bool HasSavedStationRefundArchive(RebirthStablePlayerIdentity i,RebirthStationRefundArchive a)=>Read(SavedArchive);
    internal static bool HasSavedCamperStationRefundDelivery(RebirthStablePlayerIdentity i,RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord q,Terminal t,object r,object at,object c,RebirthCamperStationRefundDeliveryRecord d)=>Read(SavedDelivery);
}
static class RebirthCamperStationPlayerPayment {internal static bool Saved=true;internal static bool HasSavedReceipt(EntityPlayer p,RebirthStationGridAdmission a)=>Saved;}
static class RebirthStationRefundPlayerFileWitness {internal static bool Saved=true;internal static Action DuringRead;internal static bool HasSavedReceipt(RebirthStablePlayerIdentity i,string d){DuringRead?.Invoke();return Saved;}}
// ACTUAL_SURVIVOR_STATUS
static class Fixture
{
    internal static Progression State;internal static string Job;private static EntityPlayer actor;private static RebirthCamperStationAddress address;private static RebirthCamperRemoteStationCommand command;
    private static void Reset(bool paid=false,bool cancel=false)
    {
        var id=Guid.NewGuid();Job=id.ToString("N");address=new RebirthCamperStationAddress{Id=7};
        command=new RebirthCamperRemoteStationCommand{Operation=id,Job=id,Kind=cancel?RebirthCamperRemoteStationCommandKind.Cancel:paid?RebirthCamperRemoteStationCommandKind.CraftPlayer:RebirthCamperRemoteStationCommandKind.Craft};
        if(cancel){command.Operation=Guid.NewGuid();command.RecipeName="";command.Batches=command.Tier=0;}
        var world=new World();actor=new EntityPlayer{world=world};world.Player=actor;GameManager.Instance.World=world;
        State=new Progression();RebirthWorldCharacterService.Owner=new Owner{Progression=State};RebirthWorldCharacterService.Identity=true;
        RebirthCamperStationNativeActorPayment.Identity=new RebirthStablePlayerIdentity();
        RebirthStationGridAdmission.Stored=new RebirthStationGridAdmission{JobId=Job,Address=address,IsPlayerPayment=paid};State.StationPreparations[Job]=RebirthStationGridAdmission.Stored;
        RebirthWorldCharacterRepository.Cached=RebirthWorldCharacterRepository.SavedAdmission=RebirthWorldCharacterRepository.SavedPublication=RebirthWorldCharacterRepository.SavedArchive=RebirthWorldCharacterRepository.SavedDelivery=true;RebirthWorldCharacterRepository.DuringRead=null;
        RebirthCamperStationPublicationRecord.Valid=RebirthCamperStationPublicationRecord.Scope=RebirthStationRefundArchive.Valid=RebirthCamperStationRefundDeliveryRecord.Valid=true;
        RebirthCamperStationPlayerPayment.Saved=RebirthStationRefundPlayerFileWitness.Saved=RebirthCamperRemoteStationTransport.Current=ThreadManager.Main=RebirthSurvivorMode.Enabled=true;RebirthStationRefundPlayerFileWitness.DuringRead=null;
    }
    private static void Expect(RebirthCamperRemoteStationCommandStatus.State expected,string why)
    {var actual=RebirthCamperRemoteStationSurvivorStatus.Read(actor,address,"creation",new object(),command,out var proof);if(actual!=expected||(actual!=RebirthCamperRemoteStationCommandStatus.State.Unknown&&string.IsNullOrEmpty(proof)))throw new Exception(why+": "+actual);}
    private static void Published()=>State.CamperStationPublications[Job]=new RebirthCamperStationPublicationRecord();
    public static void Main()
    {
        foreach(bool paid in new[]{false,true})
        {
            Reset(paid);Expect(RebirthCamperRemoteStationCommandStatus.State.Pending,"saved admission awaits publication");Published();Expect(RebirthCamperRemoteStationCommandStatus.State.Applied,"saved queued original");
            RebirthWorldCharacterRepository.SavedPublication=false;Expect(RebirthCamperRemoteStationCommandStatus.State.Unknown,"cache cannot prove saved queue");RebirthWorldCharacterRepository.SavedPublication=true;
            if(paid){RebirthCamperStationPlayerPayment.Saved=false;Expect(RebirthCamperRemoteStationCommandStatus.State.Unknown,"actor debit requires native receipt");RebirthCamperStationPlayerPayment.Saved=true;}
            command.Batches++;Expect(RebirthCamperRemoteStationCommandStatus.State.Unknown,"changed full request");command.Batches--;
            State.StationRefundArchives[Job]=new RebirthStationRefundArchive{IsCompletion=true};State.StationPreparations.Clear();State.CamperStationPublications.Clear();Expect(RebirthCamperRemoteStationCommandStatus.State.Applied,"completed original archive");
            RebirthStationRefundArchive.Valid=false;Expect(RebirthCamperRemoteStationCommandStatus.State.Unknown,"corrupt archive cannot fall through");
        }
        Reset(cancel:true);Published();State.StationTerminalIntents[Job]=new Terminal();State.StationCancellationRefunds[Job]=new object();State.StationCancellationAttempts[Job]=new object();State.CamperStationCancellations[Job]=new object();State.CamperStationRefundDeliveries[Job]=new RebirthCamperStationRefundDeliveryRecord();
        Expect(RebirthCamperRemoteStationCommandStatus.State.Pending,"unsaved refund");State.CamperStationRefundDeliveries[Job].IsSaved=true;RebirthStationRefundPlayerFileWitness.Saved=false;Expect(RebirthCamperRemoteStationCommandStatus.State.Pending,"owner delivery receipt absent");RebirthStationRefundPlayerFileWitness.Saved=true;Expect(RebirthCamperRemoteStationCommandStatus.State.Applied,"both saved cancellation witnesses");
        RebirthStationRefundPlayerFileWitness.DuringRead=()=>actor.Died++;Expect(RebirthCamperRemoteStationCommandStatus.State.Unknown,"life changes during final owner receipt read");RebirthStationRefundPlayerFileWitness.DuringRead=null;
        State.StationRefundArchives[Job]=new RebirthStationRefundArchive{IsCompletion=true};Expect(RebirthCamperRemoteStationCommandStatus.State.Unknown,"completion cannot prove cancellation");
        foreach(Action change in new Action[]{()=>actor.Died++,()=>actor.Dead=true,()=>actor.world.Player=new EntityPlayer(),()=>RebirthCamperRemoteStationTransport.Current=false,()=>RebirthWorldCharacterRepository.Cached=false,()=>RebirthWorldCharacterService.Owner.Progression=new Progression(),()=>GameManager.Instance.World=new World()})
        {Reset();Published();RebirthWorldCharacterRepository.DuringRead=change;Expect(RebirthCamperRemoteStationCommandStatus.State.Unknown,"scope expires during primary-file qualification");}
        Reset();RebirthCamperStationNativeActorPayment.Identity.CanonicalId="other";Expect(RebirthCamperRemoteStationCommandStatus.State.Unknown,"native identity differs");
        Console.WriteLine("MP071 actual Survivor status scenarios: PASS (persistence/native codecs are adapters)");
    }
}
