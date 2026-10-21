#if DEBUG
using System;

// Requires a REAL previously attempted native executor.End fault/undelivered
// outcome. This observer never constructs a session, receipt, lease or executor.
internal static class RebirthNpcMissionEndRetentionFixture
{
    internal static void Verify(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation))throw new InvalidOperationException(RebirthNpcQualificationMutationGuard.Refused);
        int before=RebirthNpcMissionRuntime.PendingEndCount;
        if(before<=0||RebirthNpcMissionRuntime.CanRetireEmptyController)throw new InvalidOperationException("Actual retained original Mission End precondition missing");
        var scope=RebirthNpcSaveQuarantineScope.CaptureCurrent();
        bool refused=false;
        try{RebirthNpcMissionRuntime.PrepareEmptyControllerQuarantine(scope);}
        catch(InvalidOperationException){refused=true;}
        if(!refused||RebirthNpcMissionRuntime.PendingEndCount!=before||RebirthNpcMissionRuntime.CanRetireEmptyController)throw new InvalidOperationException("Original Mission End was not preserved by retirement refusal");
    }
}

#endif
