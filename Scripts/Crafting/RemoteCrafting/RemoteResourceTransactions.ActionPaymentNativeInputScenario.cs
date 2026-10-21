#if REBIRTH_DEBUG
using System;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionNativeInputWatch
    {
        internal World World;internal EntityPlayer Actor;internal Vector3i Position;
        internal int Deaths;internal string Creation;
        internal Action<Guid,string> Callback;
    }
    private static ActionNativeInputWatch actionNativeInputWatch;
    internal static void WatchActionNativeInput(Vector3i position,int? actorEntity=null,string holdStage=null)
    {
        RequireActionScenario();
        if(actionNativeInputWatch!=null||ActionPaymentScenarioBoundary!=null||actionScenarioOriginals.Count>=8)
            throw new InvalidOperationException("A scenario observation is already retained");
        if(holdStage!=null&&holdStage!="target-before-saved"&&holdStage!="paid"&&holdStage!="publication-returned"&&holdStage!="service-after-saved"&&holdStage!="target-after-saved"&&holdStage!="used"&&holdStage!="returned")
            throw new ArgumentException("Named existing positive original checkpoint required");
        var world=GameManager.Instance.World;var actor=actorEntity.HasValue?world.GetEntity(actorEntity.Value) as EntityPlayer:world.GetPrimaryPlayer();
        if(actor==null||actor.IsDead()||actionPayments.Any(payment=>!payment.Terminal&&!payment.Retired&&ReferenceEquals(payment.Actor,actor)))
            throw new InvalidOperationException("Prepare a current owner without an existing payment");
        var watch=new ActionNativeInputWatch{World=world,Actor=actor,Position=position,Deaths=actor.Died,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor)};
        watch.Callback=(operation,stage)=>
        {
            if(!ReferenceEquals(actionNativeInputWatch,watch))return;
            if(!ReferenceEquals(GameManager.Instance?.World,watch.World)||!ReferenceEquals(watch.World.GetEntity(watch.Actor.entityId),watch.Actor)||watch.Actor.Died!=watch.Deaths||RebirthCamperCharacterAccess.CurrentCreation(watch.Actor)!=watch.Creation)
            {StopActionNativeInputWatch();return;}
            var payment=actionPayments.SingleOrDefault(value=>value.Operation==operation);
            if(payment==null||!ReferenceEquals(payment.Actor,watch.Actor)||payment.ServiceCandidate.Position!=watch.Position||!payment.ActorCurrent())return;
            // Observe the genuine normal-input original. No service entry,
            // native setter, payment advance or artificial outcome is invoked.
            if(stage=="prepared"&&!actionScenarioOriginals.ContainsKey(operation)){payment.ScenarioRetain=true;actionScenarioOriginals.Add(operation,payment);Log.Out("[MP040 NATIVE ARM] "+operation.ToString("N"));}
            if(holdStage==null&&stage=="prepared"){StopActionNativeInputWatch();return;}
            if(holdStage!=null&&stage==holdStage)
            {payment.ScenarioPaused=true;StopActionNativeInputWatch();throw new System.IO.IOException("CodexTest retained actual durable native-input checkpoint "+stage);}
        };
        actionNativeInputWatch=watch;ActionPaymentScenarioBoundary=watch.Callback;
    }
    internal static void StopActionNativeInputWatch()
    {
        var watch=actionNativeInputWatch;actionNativeInputWatch=null;
        if(watch!=null&&ReferenceEquals(ActionPaymentScenarioBoundary,watch.Callback))ActionPaymentScenarioBoundary=null;
    }
    private static void RetireActionNativeInputWatch()
    {
        var watch=actionNativeInputWatch;
        if(watch!=null&&(!ReferenceEquals(GameManager.Instance?.World,watch.World)||!ReferenceEquals(watch.World.GetEntity(watch.Actor.entityId),watch.Actor)||watch.Actor.IsDead()||watch.Actor.Died!=watch.Deaths||RebirthCamperCharacterAccess.CurrentCreation(watch.Actor)!=watch.Creation))
            StopActionNativeInputWatch();
    }
}
#endif
