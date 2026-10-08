const fs=require('fs'),p='Scripts/Survivor/Progression/TheorySolo/RebirthTheorySoloLockpickService.cs';let s=fs.readFileSync(p,'utf8');function edit(a,b){if(!s.includes(a))throw Error(a);s=s.replace(a,b);}
edit('internal string Creation;internal float Earliest,Expires;','internal TimerEventData Timer;internal string Creation;internal float Earliest,Expires;');
edit('    [ThreadStatic] private static Transition activeTransition;',`    internal sealed class TimerSetup { internal Binding Binding,Previous;internal bool Finished; }
    [ThreadStatic] private static Binding activeTimerSetup;
    [ThreadStatic] private static Transition activeTransition;`);
edit('!ReferenceEquals(timer.Data,b.Player)||!ReferenceEquals(feature,b.Feature)','!ReferenceEquals(timer.Data,b.Player)||!ReferenceEquals(timer,b.Timer)||!ReferenceEquals(feature,b.Feature)');
const at=s.indexOf('    internal static Transition BeforeLocal(');s=s.slice(0,at)+`    internal static TimerSetup BeforeShowUi(TEFeatureLockPickable feature,bool granted)
    {
        if(!granted)return null;
        foreach(var b in Bindings.Values)
        {
            if(b.Peer!=null||!ReferenceEquals(b.Feature,feature)||!Current(b,true)||b.Held||b.Completed)continue;
            var locks=LockManager.Instance;if(locks==null||!locks.singleLocks.TryGetByValue(new LockManager.LockEntry(feature,b.Channel),out var owner)||owner!=b.Player.entityId)return null;
            var setup=new TimerSetup{Binding=b,Previous=activeTimerSetup};activeTimerSetup=b;return setup;
        }
        return null;
    }
    internal static void CaptureTimer(XUiC_Timer controller,TimerEventData timer,float seconds,bool ran)
    {
        var b=activeTimerSetup;if(!ran||b==null||!Current(b,true)||b.Peer!=null||controller==null||timer==null||!ReferenceEquals(controller.eventData,timer)||!ReferenceEquals(timer.Data,b.Player)||!Finite(seconds)||seconds<=0)return;
        b.Timer=timer;
    }
    internal static void AfterShowUi(TimerSetup setup,bool ran)
    {
        if(setup==null||setup.Finished)return;setup.Finished=true;
        if(ReferenceEquals(activeTimerSetup,setup.Binding))activeTimerSetup=setup.Previous;
        if(!ran)setup.Binding.Held=true;
    }
    internal static void UnknownShowUi(TimerSetup setup,Exception failure)
    {if(setup!=null&&failure!=null){setup.Binding.Held=true;AfterShowUi(setup,false);}}
`+s.slice(at);
edit('Bindings.Clear();activeTransition=null;currentWorld=world;','Bindings.Clear();activeTransition=null;activeTimerSetup=null;currentWorld=world;');edit('Bindings.Clear();activeTransition=null;currentWorld=null;','Bindings.Clear();activeTransition=null;activeTimerSetup=null;currentWorld=null;');
fs.writeFileSync(p,s);