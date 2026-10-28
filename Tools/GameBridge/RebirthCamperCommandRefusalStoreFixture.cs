// Real isolated filesystem and actual production admission/refusal store/scope.
// Native identity, lease and effect service doubles only. All cases NOT_RUN.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
class WorldState {internal Guid Guid=Guid.NewGuid();}
class World {internal WorldState worldState=new WorldState();internal EntityPlayer Player;internal bool IsRemote()=>false;internal EntityPlayer GetEntity(int id)=>Player;}
class EntityPlayer {internal World world;internal int entityId=1,Died=2;internal bool IsDead()=>false;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthSurvivorMode {internal static bool IsEnabledForCurrentWorld()=>false;}
static class GameIO {internal static string Directory;internal static string GetSaveGameDir()=>Directory;}
class Identity {internal string StorageKey=new string('a',64);}
static class RebirthOriginalActionScope {internal static bool Matches(EntityPlayer p,string owner,string creation)=>true;}
static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer p)=>"creation";}
static class RebirthCamperRemoteStationTransport {internal static Func<bool> Probe;internal static bool IsCurrent(EntityPlayer p,RebirthCamperStationAddress a,string c,object l)=>Probe==null||Probe();}
static class RebirthCamperStationNativeActorPayment {internal static bool RemoteIdentity(EntityPlayer p,out Identity i,out object ignored){i=new Identity();ignored=null;return true;}}
static class RebirthCamperRemoteStationCommandStatus
{
    internal enum State {Unknown,Pending,Applied,Rejected}
    internal sealed class Observation
    {
        internal State Status;internal Guid RequestOperation,Job;
        internal RebirthCamperRemoteStationCommandKind Kind;
        internal string Owner,Creation,Address,RequestBinding;
    }
    // PRODUCTION_KIND_CLASSIFIER
    // PRODUCTION_BINDING
}
// PRODUCTION_SERVER_STORE_TYPES
static class RebirthCamperRemoteStationActions
{
    // PRODUCTION_TRACKED_SCOPE
    internal static bool Effect;
    internal static bool Handle(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand r)
    {if(Effect)EnterEffects();return false;}
    internal static void Invoke(EntityPlayer p,RebirthCamperStationAddress a,object l,RebirthCamperRemoteStationCommand r)=>HandleTracked(p,a,"creation",l,r);
}
static class RebirthCamperCommandRefusalStoreFixture
{
    static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void Main()
    {
        string temp=Path.GetFullPath(Path.GetTempPath()),root=Path.GetFullPath(Path.Combine(temp,"rebirth-command-refusal-"+Guid.NewGuid().ToString("N")));
        Need(root.StartsWith(temp,StringComparison.OrdinalIgnoreCase)&&!Directory.Exists(root),"Fresh contained fixture root required");
        Directory.CreateDirectory(root);GameIO.Directory=root;
        try
        {
            var player=new EntityPlayer();player.world=new World{Player=player};GameManager.Instance.World=player.world;
            var address=new RebirthCamperStationAddress(Guid.NewGuid(),Guid.NewGuid());var lease=new object();
            Func<RebirthCamperRemoteStationCommand> fresh=()=>new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.CraftPlayer,Guid.NewGuid(),Guid.NewGuid(),"recipe",2,1);
            var command=fresh();
            Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,command,out _,out var disposition,out var firstPermit)&&disposition==RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.NewlyPublished,"First identity admission failed");
            Need(!RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,command,out _,out bool candidate)&&!candidate,"Missing effect is not refusal");
            RebirthCamperRemoteStationCommandStatusStore.InvokeFresh(firstPermit,()=>true,()=>{RebirthCamperRemoteStationActions.Invoke(player,address,lease,command);return false;});
            Need(RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,command,out var deaths,out candidate)&&candidate&&deaths==2,"Saved actual normal refusal missing");
            Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,command,out _,out disposition)&&disposition==RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.ExistingStatusOnly,"Refusal permitted fresh invocation");
            // Separate original proves entering a service cannot create refusal.
            var entered=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,entered,out _,out _,out var enteredPermit),"Independent original admission failed");
            RebirthCamperRemoteStationActions.Effect=true;RebirthCamperRemoteStationCommandStatusStore.InvokeFresh(enteredPermit,()=>true,()=>{RebirthCamperRemoteStationActions.Invoke(player,address,lease,entered);return false;});
            Need(!RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,entered,out _,out _),"Effect-service false minted refusal");
            // Applied evidence is an explicit double: only identity migration
            // is under test here, never a native saved-payment assertion.
            var archived=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,archived,out var binding,out _,out var archivePermit),"Archive original admission failed");
            Need(RebirthCamperRemoteStationCommandStatusStore.InvokeFresh(archivePermit,()=>true,()=>true),"Archive source invocation fence absent");
            var observation=new RebirthCamperRemoteStationCommandStatus.Observation{Status=RebirthCamperRemoteStationCommandStatus.State.Applied,RequestOperation=archived.Operation,Job=archived.Job,Kind=archived.Kind,Owner=new string('a',64),Creation="creation",Address=address.ToString(),RequestBinding=binding};
            Need(RebirthCamperRemoteStationCommandStatusStore.TryArchiveAppliedIdentity(player,address,"creation",lease,archived,observation),"Positive identity archive failed");
            Need(RebirthCamperRemoteStationCommandStatusStore.TryReadExact(player,address,"creation",lease,archived,out var historical)&&historical==binding,"Archived identity lost");
            Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,archived,out _,out disposition)&&disposition==RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.ExistingStatusOnly,"Archived identity permitted handler replay");
            observation.Status=RebirthCamperRemoteStationCommandStatus.State.Pending;
            Need(!RebirthCamperRemoteStationCommandStatusStore.TryArchiveAppliedIdentity(player,address,"creation",lease,entered,observation),"Pending identity archived");
            // Model the real crash boundary by moving only the fixture-owned
            // full refusal, preserving the active primary without modification.
            string activeIdentity=Directory.EnumerateFiles(Path.Combine(root,"REBIRTH","RemoteStationCommands"),command.Operation.ToString("N")+".xml",SearchOption.AllDirectories).Single();
            string historyIdentity=Path.Combine(root,"REBIRTH","RemoteStationCommandHistory",new string('a',64),command.Operation.ToString("N")+".xml");
            File.Move(activeIdentity+".refusal.xml",historyIdentity+".refusal.xml");
            Need(RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,command,out deaths,out candidate)&&candidate&&deaths==2,"Interrupted archive lost positive refusal");
            Need(RebirthCamperRemoteStationCommandStatusStore.TryArchiveBeforeEffectIdentity(player,address,"creation",lease,command),"Interrupted refusal archive did not finish");
            Need(!File.Exists(activeIdentity)&&File.Exists(historyIdentity),"Refusal identity did not leave active capacity");
            Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,command,out _,out disposition)&&disposition==RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.ExistingStatusOnly,"Archived refusal permitted replay");
            string file=Directory.EnumerateFiles(root,"*.refusal.xml",SearchOption.AllDirectories).Single(),original=File.ReadAllText(file);
            File.WriteAllText(file,"<wrong/>");
            Need(!RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,command,out _,out candidate)&&candidate,"Malformed candidate disappeared");
            Need(File.ReadAllText(file)=="<wrong/>","Malformed evidence changed");File.WriteAllText(file,original);
            File.WriteAllText(file+".tmp",original);
            Need(!RebirthCamperRemoteStationCommandStatusStore.TryReadExact(player,address,"creation",lease,command,out _),"Foreign staged candidate ignored");
            Need(File.Exists(file+".tmp"),"Unknown stage deleted");
            InvocationCases(Path.Combine(root,"invocation-cases"),player,address,lease,fresh);
            NativeRejectedCases(Path.Combine(root,"native-rejected-cases"),player,address,lease,fresh);

        }
        finally
        {
            Need(Path.GetFullPath(root).StartsWith(temp,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("rebirth-command-refusal-",StringComparison.Ordinal),"Cleanup escaped generated root");
            Directory.Delete(root,true);
        }
    }
    private static void NativeRejectedCases(string root,EntityPlayer player,RebirthCamperStationAddress address,object lease,Func<RebirthCamperRemoteStationCommand> fresh)
    {
        Directory.CreateDirectory(root);GameIO.Directory=root;
        var command=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,command,out var binding,out _,out var permit),"Native rejection identity not admitted");
        Need(RebirthCamperRemoteStationCommandStatusStore.InvokeFresh(permit,()=>true,()=>true),"Native rejection invocation fence not published");
        // Explicit qualified-outcome double: this cohort proves only full
        // metadata migration. It does not manufacture native no-effect proof.
        var observation=new RebirthCamperRemoteStationCommandStatus.Observation{Status=RebirthCamperRemoteStationCommandStatus.State.Pending,RequestOperation=command.Operation,Job=command.Job,Kind=command.Kind,Owner=new string('a',64),Creation="creation",Address=address.ToString(),RequestBinding=binding};
        Need(!RebirthCamperRemoteStationCommandStatusStore.TryArchiveRejectedIdentity(player,address,"creation",lease,command,observation),"Pending observation archived as native rejection");
        observation.Status=RebirthCamperRemoteStationCommandStatus.State.Applied;Need(!RebirthCamperRemoteStationCommandStatusStore.TryArchiveRejectedIdentity(player,address,"creation",lease,command,observation),"Applied observation archived through rejection API");
        observation.Status=RebirthCamperRemoteStationCommandStatus.State.Rejected;observation.RequestBinding="changed";Need(!RebirthCamperRemoteStationCommandStatusStore.TryArchiveRejectedIdentity(player,address,"creation",lease,command,observation),"Changed native rejection request archived");observation.RequestBinding=binding;
        Need(!RebirthCamperRemoteStationCommandStatusStore.TryArchiveAppliedIdentity(player,address,"creation",lease,command,observation),"Native rejection accepted through applied API");
        Need(RebirthCamperRemoteStationCommandStatusStore.TryArchiveRejectedIdentity(player,address,"creation",lease,command,observation),"Qualified native rejection identity not archived");
        string active=Path.Combine(root,"REBIRTH","RemoteStationCommands",new string('a',64),command.Operation.ToString("N")+".xml");
        Need(!File.Exists(active)&&RebirthCamperRemoteStationCommandStatusStore.TryReadExact(player,address,"creation",lease,command,out var retained)&&retained==binding&&RebirthCamperRemoteStationCommandStatusStore.HasInvocation(player,address,"creation",lease,command),"Native rejection retained active capacity or lost exact historical binding/fence");
        string activeFence=Path.Combine(root,"REBIRTH","RemoteStationCommandInvocations",new string('a',64),command.Operation.ToString("N")+".xml"),historyFence=Path.Combine(root,"REBIRTH","RemoteStationCommandInvocationHistory",new string('a',64),command.Operation.ToString("N")+".xml");
        Need(!File.Exists(activeFence)&&File.Exists(historyFence),"Terminal invocation still consumes bounded active fence capacity");
        // Simulate the exact identity-first/fence-later rename boundary using
        // only this fixture's complete immutable files.
        File.Move(historyFence,activeFence);
        Need(RebirthCamperRemoteStationCommandStatusStore.HasInvocation(player,address,"creation",lease,command)&&!RebirthCamperRemoteStationCommandStatusStore.TrySaveNeverInvoked(player,address,"creation",lease,command),"Interrupted fence migration lost entered original or inferred unused");
        Need(RebirthCamperRemoteStationCommandStatusStore.TryArchiveRejectedIdentity(player,address,"creation",lease,command,observation)&&File.Exists(historyFence)&&!File.Exists(activeFence),"Historical identity did not finish fence migration");
        File.Copy(historyFence,activeFence);
        Need(!RebirthCamperRemoteStationCommandStatusStore.HasInvocation(player,address,"creation",lease,command)&&!RebirthCamperRemoteStationCommandStatusStore.TrySaveNeverInvoked(player,address,"creation",lease,command)&&File.Exists(historyFence)&&File.Exists(activeFence),"Double invocation locations were treated as entered/unused or repaired");
        File.Delete(activeFence); // restore only the fixture-owned conflict
        Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,command,out _,out var disposition)&&disposition==RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.ExistingStatusOnly,"Archived native rejection authorized handler replay");
        var staged=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,staged,out binding,out _),"Staged original fixture not admitted");
        observation.RequestOperation=staged.Operation;observation.Job=staged.Job;observation.RequestBinding=binding;
        string stagedIdentity=Path.Combine(root,"REBIRTH","RemoteStationCommands",new string('a',64),staged.Operation.ToString("N")+".xml");File.WriteAllText(stagedIdentity+".tmp","retained unknown");
        Need(!RebirthCamperRemoteStationCommandStatusStore.TryArchiveRejectedIdentity(player,address,"creation",lease,staged,observation)&&File.Exists(stagedIdentity)&&File.Exists(stagedIdentity+".tmp"),"Unknown rejected-identity stage was archived/deleted");
    }
    private static void InvocationCases(string root,EntityPlayer player,RebirthCamperStationAddress address,object lease,Func<RebirthCamperRemoteStationCommand> fresh)
    {
        Directory.CreateDirectory(root);GameIO.Directory=root;int effects=0;
        var unreceived=fresh();
        Need(RebirthCamperRemoteStationCommandStatusStore.TryRefuseUnreceived(player,address,"creation",lease,unreceived)&&effects==0&&
            RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,unreceived,out int unreceivedDeaths,out _)&&unreceivedDeaths==player.Died&&
            !RebirthCamperRemoteStationCommandStatusStore.HasInvocation(player,address,"creation",lease,unreceived),"status-only absent-original reservation did not save exact no-handler refusal");
        Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,unreceived,out _,out var reserved,out var noPermit)&&reserved==RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.ExistingStatusOnly&&noPermit==null,"delayed original command obtained dispatch after status refusal");
        var changedUnreceived=new RebirthCamperRemoteStationCommand(unreceived.Kind,unreceived.Operation,unreceived.Job,"changed",unreceived.Batches,unreceived.Tier);
        Need(!RebirthCamperRemoteStationCommandStatusStore.TryRefuseUnreceived(player,address,"creation",lease,changedUnreceived),"query refusal changed complete admitted original");
        var never=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,never,out _,out _),"New protocol cold admission failed");
        Need(RebirthCamperRemoteStationCommandStatusStore.TrySaveNeverInvoked(player,address,"creation",lease,never)&&RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,never,out int deaths,out _)&&deaths==2,"Explicit unentered v2 admission did not settle metadata only");
        var good=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,good,out _,out _,out var permit),"Fresh invocation admission failed");
        bool denied=false;try{RebirthCamperRemoteStationCommandStatusStore.FreshAdmission.Capture(root,"untrusted",new XElement("untrusted"),new object());}catch(InvalidOperationException){denied=true;}Need(denied,"Cold data minted dispatch permission");
        Need(RebirthCamperRemoteStationCommandStatusStore.InvokeFresh(permit,()=>true,()=>{effects++;return true;})&&effects==1&&RebirthCamperRemoteStationCommandStatusStore.HasInvocation(player,address,"creation",lease,good),"Saved invocation did not dispatch once");
        Need(!RebirthCamperRemoteStationCommandStatusStore.InvokeFresh(permit,()=>true,()=>{effects++;return true;})&&effects==1&&!RebirthCamperRemoteStationCommandStatusStore.TrySaveNeverInvoked(player,address,"creation",lease,good),"Entered original replayed or became unused");
        Need(!RebirthCamperRemoteStationCommandStatusStore.TryRefuseUnreceived(player,address,"creation",lease,good)&&effects==1,"entered original was rejected by absent-original query route");
        var expired=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,expired,out _,out _,out permit),"Expired admission failed");
        Need(!RebirthCamperRemoteStationCommandStatusStore.InvokeFresh(permit,()=>{player.Died++;return false;},()=>{effects++;return true;})&&effects==1,"Expired original entered handler");
        Need(RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,expired,out deaths,out _)&&deaths==2,"Lease expiry lost original-life never-invoked proof");
        var throwing=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,throwing,out _,out _,out permit),"Throwing admission failed");bool threw=false;
        try{RebirthCamperRemoteStationCommandStatusStore.InvokeFresh(permit,()=>true,()=>{effects++;throw new IOException("native handler uncertain");});}catch(IOException){threw=true;}
        Need(threw&&effects==2&&!RebirthCamperRemoteStationCommandStatusStore.TrySaveNeverInvoked(player,address,"creation",lease,throwing)&&!RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,throwing,out _,out _),"Handler exception manufactured unused proof");
        var legacy=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,legacy,out _,out _),"Legacy fixture source admission failed");
        string identity=Directory.EnumerateFiles(Path.Combine(root,"REBIRTH","RemoteStationCommands"),legacy.Operation.ToString("N")+".xml",SearchOption.AllDirectories).Single();var old=XElement.Load(identity);old.SetAttributeValue("version",1);old.Attribute("invocationProtocol").Remove();old.Attribute("admittedDeaths").Remove();File.WriteAllText(identity,old.ToString(SaveOptions.DisableFormatting));
        Need(RebirthCamperRemoteStationCommandStatusStore.TryReadExact(player,address,"creation",lease,legacy,out _)&&!RebirthCamperRemoteStationCommandStatusStore.TrySaveNeverInvoked(player,address,"creation",lease,legacy),"Legacy absence became new-protocol unused proof");
        Need(!RebirthCamperRemoteStationCommandStatusStore.TryRefuseUnreceived(player,address,"creation",lease,legacy),"legacy identity became absent-original refusal");
        var staged=fresh();Need(RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,staged,out _,out _,out permit),"Staged fixture admission failed");
        string stage=Path.Combine(root,"REBIRTH","RemoteStationCommandInvocations",new string('a',64),staged.Operation.ToString("N")+".xml.tmp");File.WriteAllText(stage,"<partial/>");
        Need(!RebirthCamperRemoteStationCommandStatusStore.HasInvocation(player,address,"creation",lease,staged)&&!RebirthCamperRemoteStationCommandStatusStore.TrySaveNeverInvoked(player,address,"creation",lease,staged)&&File.Exists(stage),"Unknown entered candidate became absent");
        Need(!RebirthCamperRemoteStationCommandStatusStore.InvokeFresh(permit,()=>true,()=>{effects++;return true;})&&effects==2&&RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,staged,out _,out _)&&File.Exists(stage),"Warm positive no-handler scope failed or discarded unknown fence");
        var lost=fresh();string source=Path.Combine(root,"REBIRTH","RemoteStationCommands",new string('a',64),lost.Operation.ToString("N")+".xml");RebirthCamperRemoteStationTransport.Probe=()=>!File.Exists(source);
        Need(!RebirthCamperRemoteStationCommandStatusStore.TryAdmit(player,address,"creation",lease,lost,out _,out _),"Expired post-publication lease returned dispatch permission");RebirthCamperRemoteStationTransport.Probe=null;
        Need(RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(player,address,"creation",lease,lost,out deaths,out _)&&deaths==player.Died,"Post-rename scope expiry lost positive never-invoked outcome");
        var absentStage=fresh();string absentPath=Path.Combine(root,"REBIRTH","RemoteStationCommands",new string('a',64),absentStage.Operation.ToString("N")+".xml");File.WriteAllText(absentPath+".tmp","retained unknown original");
        Need(!RebirthCamperRemoteStationCommandStatusStore.TryRefuseUnreceived(player,address,"creation",lease,absentStage)&&File.Exists(absentPath+".tmp")&&!File.Exists(absentPath),"unreceived query bypassed or deleted staged original evidence");
    }
}



