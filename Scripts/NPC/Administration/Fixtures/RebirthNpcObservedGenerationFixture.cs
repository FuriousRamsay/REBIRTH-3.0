#if DEBUG
using System;

// AUTHORED NOT RUN: real current metadata read plus observational cache only.
// No generation publication, invented G0, native effect or payment authority.
internal static class RebirthNpcObservedGenerationFixture
{
    internal static void Verify(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation))throw new InvalidOperationException(RebirthNpcQualificationMutationGuard.Refused);
        var world=GameManager.Instance.World;ulong actual=RebirthNpcRecoveryGeneration.RequireCurrent();
        Require(RebirthNpcRecoveryGeneration.TryReadObservedGeneration(world,out ulong observed)&&observed==actual);
        RebirthNpcRecoveryGeneration.InvalidateObservedGeneration();
        Require(!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(world,out _));
        Require(!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(null,out _));
        Require(RebirthNpcRecoveryGeneration.RequireCurrent()==actual);
        Require(RebirthNpcRecoveryGeneration.TryReadObservedGeneration(world,out observed)&&observed==actual);
    }
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Observed generation positive-read/cache-miss contract failed");}
}

#endif
