#if DEBUG
using System;

// Detached production-policy vectors only. No fabricated ACK/native state.
internal static class RebirthDogCapacityRecoveryFixture
{
    internal static void Verify()
    {
        Require(RebirthDogCapacitySyncService.CanReplaceCapacity(true,4,900,5,1));
        Require(!RebirthDogCapacitySyncService.CanReplaceCapacity(true,5,1,4,900));
        Require(!RebirthDogCapacitySyncService.CanReplaceCapacity(true,5,900,5,1));
        Require(!RebirthDogCapacitySyncService.CanReplaceCapacity(true,5,1,0,900));
        Require(RebirthDogCapacitySyncService.CanReplaceCapacity(true,5,900,5,900));
    }
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Dog capacity recovery policy mismatch");}
}

#endif
