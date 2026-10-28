// Actual invocation scope is extracted by the parent-hooks runner. Native
// services and durable store are explicit doubles; no persistence qualification.
using System;
class EntityPlayer {}
struct RebirthCamperStationAddress {}
class RebirthCamperRemoteStationCommand {}
static class RebirthCamperRemoteStationCommandStatus
{internal static bool HasNativeOriginal(RebirthCamperRemoteStationCommand c)=>true;}
static class RebirthCamperRemoteStationCommandStatusStore
{
    internal static int Saved;
    internal static bool TrySaveBeforeEffectRefusal(RebirthCamperRemoteStationActions.BeforeEffectRefusal proof)
    {if(proof==null)throw new Exception("No positive warm refusal");Saved++;return true;}
}
static class RebirthCamperRemoteStationActions
{
    // PRODUCTION_TRACKED_SCOPE
    private static Func<bool> body;
    internal static bool Handle(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand r)
    {Need(BeforeEffectRefusal.CaptureReturnedRefusal(p,a,c,l,r)==null);return body();}
    static bool Invoke()=>HandleTracked(new EntityPlayer(),new RebirthCamperStationAddress(),"original",new object(),new RebirthCamperRemoteStationCommand());
    static void Need(bool value){if(!value)throw new Exception("Invocation classification failed");}
    public static void Main()
    {
        body=()=>false;Need(!Invoke()&&RebirthCamperRemoteStationCommandStatusStore.Saved==1);
        body=()=>{EnterEffects();return false;};Need(!Invoke()&&RebirthCamperRemoteStationCommandStatusStore.Saved==1);
        body=()=>true;Need(Invoke()&&RebirthCamperRemoteStationCommandStatusStore.Saved==1);
        body=()=>{throw new InvalidOperationException();};try{Invoke();throw new Exception("Swallowed failure");}catch(InvalidOperationException){}
        Need(RebirthCamperRemoteStationCommandStatusStore.Saved==1);
        body=()=>{EnterEffects();throw new InvalidOperationException();};try{Invoke();throw new Exception("Swallowed effect failure");}catch(InvalidOperationException){}
        Need(RebirthCamperRemoteStationCommandStatusStore.Saved==1);
        // An earlier exception must not leave a scope that blocks the next call.
        body=()=>false;Need(!Invoke()&&RebirthCamperRemoteStationCommandStatusStore.Saved==2);
        Need(BeforeEffectRefusal.CaptureReturnedRefusal(null,new RebirthCamperStationAddress(),null,null,null)==null);
    }
}

