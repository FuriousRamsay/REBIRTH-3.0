// Actual central Execute and kind classifier; explicit lease, persistence and
// native-effect doubles. NOT_RUN. No saved outcome/transport proof is invented.
using System;
using System.Collections.Generic;
class EntityPlayer {internal int entityId=1;}
struct RebirthCamperStationAddress {}
enum RebirthCamperRemoteStationCommandKind {Craft,Cancel,CraftPlayer,ScrapQueue,ScrapCancel,FuelOn,FuelOff}
class RebirthCamperRemoteStationCommand {internal RebirthCamperRemoteStationCommandKind Kind;internal bool IsFuel=>Kind==RebirthCamperRemoteStationCommandKind.FuelOn||Kind==RebirthCamperRemoteStationCommandKind.FuelOff;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthSurvivorMode {internal static bool Enabled;internal static bool IsEnabledForCurrentWorld()=>Enabled;}
static class RebirthCamperRemoteStationCommandStatus
{
    internal enum State {Unknown,Pending,Applied,Rejected}
    internal static State Result;internal static int Reads;
    // PRODUCTION_KIND_CLASSIFIER
    internal static State ReadStatus(EntityPlayer player,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand request,out object result)
    {Reads++;result=null;return Result;}
}
static class RebirthCamperRemoteStationCommandStatusStore
{
    internal sealed class FreshAdmission{}
    internal enum AdmissionDisposition {Unknown,NewlyPublished,ExistingStatusOnly}
    internal static bool Admit=true;internal static int Admissions;internal static AdmissionDisposition Disposition;
    internal static bool TryAdmit(EntityPlayer player,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand request,out string binding,out AdmissionDisposition disposition,out FreshAdmission fresh)
    {Admissions++;binding="full-original-double";disposition=Disposition;fresh=new FreshAdmission();return Admit;}
    internal static bool InvokeFresh(FreshAdmission fresh,Func<bool> current,Func<bool> action)=>current()&&action();
}
static class RebirthCamperCommandAdmissionFixture
{
    private sealed class Lease {internal EntityPlayer Player;internal Guid Token;internal RebirthCamperStationAddress Address;internal string Creation="original";}
    private static Dictionary<int,Lease> leases=new Dictionary<int,Lease>();
    private static bool Current(Lease lease)=>true;
    private static int effects;
    private static Func<EntityPlayer,RebirthCamperStationAddress,string,object,RebirthCamperRemoteStationCommand,bool> command=(p,a,c,l,r)=>{effects++;return true;};
    // PRODUCTION_COMMAND_EXECUTE
    private static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    private static bool Invoke(RebirthCamperRemoteStationCommandKind kind)
    {var player=new EntityPlayer();var token=Guid.NewGuid();leases[1]=new Lease{Player=player,Token=token};return Execute(player,token,new RebirthCamperRemoteStationCommand{Kind=kind});}
    public static void Main()
    {
        foreach(bool survivor in new[]{false,true})
        foreach(var kind in new[]{RebirthCamperRemoteStationCommandKind.Craft,RebirthCamperRemoteStationCommandKind.CraftPlayer,RebirthCamperRemoteStationCommandKind.Cancel,RebirthCamperRemoteStationCommandKind.ScrapQueue,RebirthCamperRemoteStationCommandKind.ScrapCancel,RebirthCamperRemoteStationCommandKind.FuelOn,RebirthCamperRemoteStationCommandKind.FuelOff})
        {
            RebirthSurvivorMode.Enabled=survivor;
            Need(RebirthCamperRemoteStationCommandStatus.HasNativeOriginal(new RebirthCamperRemoteStationCommand{Kind=kind}),"Command bypassed durable original admission");
            RebirthCamperRemoteStationCommandStatusStore.Disposition=RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.NewlyPublished;
            effects=0;Need(Invoke(kind)&&effects==1,"New admitted original did not dispatch exactly once");
            RebirthCamperRemoteStationCommandStatusStore.Disposition=RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.ExistingStatusOnly;
            foreach(RebirthCamperRemoteStationCommandStatus.State state in Enum.GetValues(typeof(RebirthCamperRemoteStationCommandStatus.State)))
            {RebirthCamperRemoteStationCommandStatus.Result=state;effects=0;Need(Invoke(kind)==(state==RebirthCamperRemoteStationCommandStatus.State.Applied)&&effects==0,"Existing original replayed native handler or invented applied outcome");}
            RebirthCamperRemoteStationCommandStatusStore.Admit=false;effects=0;
            Need(!Invoke(kind)&&effects==0,"Failed durable admission dispatched native handler");RebirthCamperRemoteStationCommandStatusStore.Admit=true;
        }
    }
}
