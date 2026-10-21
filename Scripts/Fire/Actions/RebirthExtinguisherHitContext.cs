using UnityEngine;

// Scope the original owner-side ray-hit event to its actual ray result. Restore
// the shared native event context even when a configured min-event throws.
internal static class RebirthExtinguisherHitContext
{
    internal static void Fire(EntityAlive shooter,Vector3 position,EntityAlive target,BlockValue block,MinEventTypes type)
    {
        var context=shooter.MinEventContext;
        var previousPosition=context.Position;
        var previousOther=context.Other;
        var previousBlock=context.BlockValue;
        try
        {
            context.Position=position;context.Other=target;context.BlockValue=block;
            shooter.FireEvent(type);
        }
        finally
        {
            context.Position=previousPosition;context.Other=previousOther;context.BlockValue=previousBlock;
        }
    }
}
