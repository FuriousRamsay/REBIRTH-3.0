using System;
namespace EventGateDoubles {
class EntityAlive{}
class EntityRebirthHumanoidNPC:EntityAlive{internal bool IsPreparedRestorationPending;}
class Dog:EntityAlive{}
class MinEventParams{internal EntityAlive Self;}
static class EventGateFixture {
internal static int Run(){int n=0;Action<bool,string> check=(b,s)=>{if(!b)throw new Exception(s);n++;};check(ActualEventGate.Prefix(null),"null context preserved");check(ActualEventGate.Prefix(new MinEventParams()),"no owner preserved");check(ActualEventGate.Prefix(new MinEventParams{Self=new EntityAlive()}),"ordinary actor preserved");check(ActualEventGate.Prefix(new MinEventParams{Self=new Dog()}),"dog preserved");var actor=new EntityRebirthHumanoidNPC();check(ActualEventGate.Prefix(new MinEventParams{Self=actor}),"ordinary humanoid preserved");actor.IsPreparedRestorationPending=true;check(!ActualEventGate.Prefix(new MinEventParams{Self=actor}),"original held humanoid suppressed");actor.IsPreparedRestorationPending=false;check(ActualEventGate.Prefix(new MinEventParams{Self=actor}),"released actor preserved");return n;}}
}