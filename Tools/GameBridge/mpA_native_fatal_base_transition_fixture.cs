using System;

// PREPARED, NOT RUN. Pause at the REAL SetDead prefix/postfix and supply the
// actual observer token. Fixture never changes native bDead/damage/health/Died.
internal static class mpA_native_fatal_base_transition_fixture
{
    internal static object CaptureActualBaseAlive(EntityPlayer actor,bool fatalAlreadyRecorded)
    {
        if(actor==null||actor.bDead||actor.RecordedDamage.Fatal!=fatalAlreadyRecorded)throw new Exception("Actual base-alive native boundary required.");
        var token=RebirthPlacedOriginalDeathPartition.BeforeTerminalNativeFatal(actor);
        if(token==null)throw new Exception("Valid native base transition was missed by logical IsDead.");return token;
    }
    internal static void ActualRepeatedDeadMustNotCapture(EntityPlayer actor)
    {
        if(actor?.bDead!=true)throw new Exception("Actual already-dead native base flag required.");
        if(RebirthPlacedOriginalDeathPartition.BeforeTerminalNativeFatal(actor)!=null)throw new Exception("Repeated SetDead minted a new fatal episode.");
    }
    internal static void ObserveActualSuccessfulNativeCompletion(EntityPlayer actor,object actualToken)
    {
        if(actor?.bDead!=true||actor.Health>0||actualToken==null)throw new Exception("Actual successful native SetDead completion required.");
        // Sole production Postfix performs After once. Do not replay it here.
    }
    // Harness cases: RecordedDamage.Fatal true while bDead false must capture;
    // false/bDead false also capture; bDead true always refuses repeated episode;
    // native exception gets no Postfix/After; genuine SetAlive resets bDead for
    // next actual episode, while existing identity/world/token checks remain.
}
