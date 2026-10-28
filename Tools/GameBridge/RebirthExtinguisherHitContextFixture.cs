// Actual scoped event helper; explicit native types. Deferred, NOT_RUN.
using System;
using UnityEngine;
namespace UnityEngine {struct Vector3 {internal int Value;internal Vector3(int value){Value=value;}}}
struct BlockValue {internal int Value;internal BlockValue(int value){Value=value;}}
enum MinEventTypes {Hit}
class MinEventParams {internal UnityEngine.Vector3 Position;internal EntityAlive Other;internal BlockValue BlockValue;}
class EntityAlive
{
    internal MinEventParams MinEventContext=new MinEventParams();internal Action Callback;
    internal void FireEvent(MinEventTypes type){Callback();}
}
// PRODUCTION_HIT_CONTEXT
static class RebirthExtinguisherHitContextFixture
{
    static void Need(bool value){if(!value)throw new Exception("Shared hit context mismatch");}
    public static void Main()
    {
        var shooter=new EntityAlive();var oldTarget=new EntityAlive();var target=new EntityAlive();
        shooter.MinEventContext.Position=new UnityEngine.Vector3(1);shooter.MinEventContext.Other=oldTarget;shooter.MinEventContext.BlockValue=new BlockValue(2);
        Action restored=()=>Need(shooter.MinEventContext.Position.Value==1&&ReferenceEquals(shooter.MinEventContext.Other,oldTarget)&&shooter.MinEventContext.BlockValue.Value==2);
        shooter.Callback=()=>Need(shooter.MinEventContext.Position.Value==9&&ReferenceEquals(shooter.MinEventContext.Other,target)&&shooter.MinEventContext.BlockValue.Value==0);
        RebirthExtinguisherHitContext.Fire(shooter,new UnityEngine.Vector3(9),target,new BlockValue(0),MinEventTypes.Hit);restored();
        shooter.Callback=()=>{Need(shooter.MinEventContext.Other==null&&shooter.MinEventContext.BlockValue.Value==3);throw new InvalidOperationException();};
        try{RebirthExtinguisherHitContext.Fire(shooter,new UnityEngine.Vector3(9),null,new BlockValue(3),MinEventTypes.Hit);throw new Exception("Swallowed configured event failure");}catch(InvalidOperationException){}restored();
        shooter.Callback=()=>
        {
            shooter.Callback=()=>Need(shooter.MinEventContext.Position.Value==7);
            RebirthExtinguisherHitContext.Fire(shooter,new UnityEngine.Vector3(7),null,new BlockValue(4),MinEventTypes.Hit);
            Need(shooter.MinEventContext.Position.Value==9&&ReferenceEquals(shooter.MinEventContext.Other,target));
        };
        RebirthExtinguisherHitContext.Fire(shooter,new UnityEngine.Vector3(9),target,new BlockValue(0),MinEventTypes.Hit);restored();
    }
}

